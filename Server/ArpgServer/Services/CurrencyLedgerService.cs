using System.Text.Json;
using ArpgServer.Data;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace ArpgServer.Services;

/// <summary>
/// 货币账本服务 (规划阶段03) — Lua 原子扣款 + OperationId 幂等 + currency_ledger 流水。
/// Redis = 实时账本 (HASH: player:{id}:currency, 字段=Gold/Life/Plane/Fragments);
/// MySQL currency_ledger = 持久化流水 (对账/审计); 余额权威迁移在后续阶段完成。
/// 同一 OperationId 重复请求返回首次结果 (幂等)。
/// 键命名 (规划4.2): player:{id}:currency + player:{id}:currency:operation:{opId} —
/// {id} 为 hash tag, Redis Cluster 下 Lua 双键落在同一 slot; 旧无tag键首次触达时惰性迁移。
/// </summary>
public class CurrencyLedgerService
{
    private readonly RedisCacheService _redis;
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    /// 原子货币操作 Lua 脚本:
    /// KEYS[1]=余额HASH(tagged)  KEYS[2]=操作幂等键(tagged, 同slot)  KEYS[3]=旧无tag余额键(一次性惰性迁移)  ARGV[1]=币种  ARGV[2]=金额(负=扣)  ARGV[3]=幂等键TTL秒
    /// 0) 旧键存在且新键不存在 → 迁移余额到新键 (集群改造过渡; 单节点执行)
    /// 1) 幂等键已存在 → 直接返回首次结果
    /// 2) 余额+负金额 < 0 → 失败(insufficient)
    /// 3) HSET 新余额 + 记录幂等结果
    /// </summary>
    public const string LuaAtomic =
        """
        if redis.call('EXISTS', KEYS[1]) == 0 and redis.call('EXISTS', KEYS[3]) == 1 then
            local vals = redis.call('HGETALL', KEYS[3])
            for i = 1, #vals, 2 do redis.call('HSET', KEYS[1], vals[i], vals[i+1]) end
            redis.call('DEL', KEYS[3])
        end
        if redis.call('EXISTS', KEYS[2]) == 1 then
            local bal = redis.call('HGET', KEYS[2], 'balanceAfter')
            return cjson.encode({ success = true, repeated = true, balanceAfter = tonumber(bal) })
        end
        local bal = tonumber(redis.call('HGET', KEYS[1], ARGV[1]) or '0')
        local amt = tonumber(ARGV[2])
        if amt < 0 and bal + amt < 0 then
            return cjson.encode({ success = false, reason = 'insufficient', balanceAfter = bal })
        end
        local newBal = bal + amt
        redis.call('HSET', KEYS[1], ARGV[1], newBal)
        redis.call('HSET', KEYS[2], 'balanceAfter', newBal)
        redis.call('HSET', KEYS[2], 'amount', amt)
        redis.call('EXPIRE', KEYS[2], tonumber(ARGV[3]))
        return cjson.encode({ success = true, repeated = false, balanceAfter = newBal })
        """;

    public CurrencyLedgerService(RedisCacheService redis, IServiceScopeFactory scopeFactory)
    {
        _redis = redis;
        _scopeFactory = scopeFactory;
    }

    private string BalanceKey(long playerId) => $"player:{{{playerId}}}:currency";
    private string OpKey(long playerId, string operationId) => $"player:{{{playerId}}}:currency:operation:{operationId}";
    private string LegacyBalanceKey(long playerId) => $"player:{playerId}:currency";

    public class CurrencyResult
    {
        public bool Success;
        public bool Repeated;      // true = 幂等命中 (返回首次结果)
        public long BalanceAfter;
        public string? Reason;
    }

    /// <summary>
    /// 原子货币操作: Redis Lua 校验+扣减+幂等一次完成; 成功后写 MySQL currency_ledger (INSERT IGNORE 幂等)。
    /// Redis 不可用时: Gold 走 PlayerSaves.Gold 直接扣减 (现有权威路径); Life/Plane 仅记流水。
    /// </summary>
    public async Task<CurrencyResult> TryCurrencyOperationAsync(
        long playerId, string kind, long amount, string operationId, string reason)
    {
        var result = new CurrencyResult { Success = false, Reason = "redis_unavailable" };

        if (_redis.IsAvailable)
        {
            var db = _redis.Database;
            if (db == null) return result;

            var redisResult = (RedisValue)await db.ScriptEvaluateAsync(
                LuaAtomic,
                new RedisKey[] { BalanceKey(playerId), OpKey(playerId, operationId), LegacyBalanceKey(playerId) },
                new RedisValue[] { kind, amount, 604800 });

            var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(redisResult.ToString());
            if (parsed == null) { result.Reason = "parse_error"; return result; }

            result.Success = parsed["success"].GetBoolean();
            result.Repeated = parsed.TryGetValue("repeated", out var rep) && rep.GetBoolean();
            result.BalanceAfter = parsed.TryGetValue("balanceAfter", out var bal) ? bal.GetInt64() : 0;
            if (!result.Success) result.Reason = parsed.TryGetValue("reason", out var rr) ? rr.GetString() : "failed";

            if (result.Success)
                await WriteLedgerAsync(operationId, playerId, kind, amount, result.BalanceAfter, reason);
            return result;
        }

        // Redis 不可用: Gold 走数据库权威扣减 (其余币种仅记流水, 余额由客户端钱包持有)
        if (kind == "Gold")
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var save = await db.PlayerSaves.OrderBy(s => s.Id)
                .FirstOrDefaultAsync(s => s.UserId == (int)playerId);
            if (save == null || save.Gold < amount) { result.Reason = "insufficient"; return result; }
            save.Gold -= (int)amount;
            save.DataVersion++;
            await db.SaveChangesAsync();
            result.Success = true;
            result.BalanceAfter = save.Gold;
            await WriteLedgerAsync(operationId, playerId, kind, amount, result.BalanceAfter, reason);
            return result;
        }

        result.Reason = "redis_unavailable";
        return result;
    }

    /// <summary>读取实时余额 (Redis 账本; 未初始化返回 0; 首次触达惰性迁移旧无tag键)</summary>
    public async Task<Dictionary<string, long>> GetBalancesAsync(long playerId)
    {
        var dict = new Dictionary<string, long>();
        var db = _redis.Database;
        if (db == null) return dict;

        var entries = await db.HashGetAllAsync(BalanceKey(playerId));
        if (entries.Length == 0)
        {
            // 惰性迁移: 旧无tag键 → 新tag键 (过渡期一次性; 集群环境需在单节点期完成迁移)
            var legacy = await db.HashGetAllAsync(LegacyBalanceKey(playerId));
            if (legacy.Length > 0)
            {
                await db.HashSetAsync(BalanceKey(playerId), legacy);
                await db.KeyDeleteAsync(LegacyBalanceKey(playerId));
                entries = await db.HashGetAllAsync(BalanceKey(playerId));
            }
        }
        foreach (var e in entries)
            if (long.TryParse(e.Value.ToString(), out var v)) dict[e.Name.ToString()] = v;
        return dict;
    }

    /// <summary>写 MySQL 流水 — INSERT IGNORE (OperationId 唯一键幂等); 失败重试3次(指数退避), 仍失败留待对账</summary>
    private async Task WriteLedgerAsync(string operationId, long playerId, string kind, long amount, long balanceAfter, string reason)
    {
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.ExecuteSqlRawAsync(
                    @"INSERT IGNORE INTO currency_ledger
                      (`OperationId`, `PlayerId`, `Kind`, `Amount`, `BalanceAfter`, `Reason`, `CreatedAt`)
                      VALUES ({0}, {1}, {2}, {3}, {4}, {5}, UTC_TIMESTAMP(3))",
                    operationId, playerId, kind, amount, balanceAfter, reason ?? "");
                return;
            }
            catch (Exception ex)
            {
                if (attempt == 3)
                    Console.WriteLine($"[CurrencyLedger] 流水写入失败×3 (待对账补偿): op={operationId} {ex.Message}");
                else
                    await Task.Delay(200 * attempt);
            }
        }
    }
}
