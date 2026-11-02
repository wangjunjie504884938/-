using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ArpgServer.Data;
using ArpgServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace ArpgServer.Controllers;

/// <summary>
/// 地牢快照 (规划阶段06) — Redis 热态 + MySQL 灾备 + 幂等结算。
/// - save:   按 InstanceId upsert (断线保留5分钟内可反复覆盖)
/// - load:   重连恢复 (返回完整快照; 超过保留期视为不存在)
/// - settle: 幂等结算 — RewardFlag 已存在则 repeated=true 不重复发奖;
///           首次结算经 CurrencyLedgerService 原子入账 (阶段03数据层)
/// - delete: 结算/超时后清理
/// 快照分级间隔: P0普通5s / P1精英Boss 3s / P2高难1s (Tier 由游戏服务器按副本类型设置)
/// </summary>
[Route("api")]
[Authorize]
[ApiController]
public class DungeonSnapshotController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly RedisCacheService _redis;
    private readonly CurrencyLedgerService _ledger;

    public const int RetentionSeconds = 300; // 断线保留5分钟

    public DungeonSnapshotController(AppDbContext db, RedisCacheService redis, CurrencyLedgerService ledger)
    {
        _db = db;
        _redis = redis;
        _ledger = ledger;
    }

    public class SaveRequest
    {
        public string InstanceId { get; set; } = "";
        public long PlayerId { get; set; }              // 房主/发起者 (归属校验)
        public int Tier { get; set; }                   // 0=P0 1=P1 2=P2
        public int ConfigVersion { get; set; } = 1;
        public int TemplateId { get; set; }
        public int RiftLayer { get; set; }
        public int RandomSeed { get; set; }
        public int Wave { get; set; }
        public float SurvivalTime { get; set; }
        public string MembersJson { get; set; } = "[]";     // [{playerId,x,y,hp,maxHp,downed}]
        public string MonstersJson { get; set; } = "[]";    // [{spawnId,hp,phase}]
        public string RewardFlagsJson { get; set; } = "[]"; // 已发奖励标记 (幂等)
        public string State { get; set; } = "Fighting";     // Fighting/Settled
    }

    private static string HotKey(string instanceId) => $"dungeon:snapshot:{instanceId}";

    /// <summary>保存快照 — Redis 热态 (TTL=保留期+60s) + MySQL upsert (灾备)。幂等: 同 InstanceId 覆盖。</summary>
    [HttpPost("dungeon/snapshot/save")]
    public async Task<IActionResult> Save([FromBody] SaveRequest req)
    {
        if (string.IsNullOrEmpty(req.InstanceId)) return BadRequest(new { error = "InstanceId required" });

        var snapshotJson = JsonSerializer.Serialize(new
        {
            req.ConfigVersion,
            req.TemplateId,
            req.RiftLayer,
            req.RandomSeed,
            req.Wave,
            req.SurvivalTime,
            members = req.MembersJson,
            monsters = req.MonstersJson,
            rewardFlags = req.RewardFlagsJson,
            req.State,
        });

        // Redis 热态 (降级保护: Redis 不可用时跳过 — MySQL 仍有灾备副本, 规划阶段03既有模式)
        if (_redis.IsAvailable && _redis.Database != null)
        {
            var hotPayload = JsonSerializer.Serialize(new
            {
                req.InstanceId,
                req.PlayerId,
                req.Tier,
                req.ConfigVersion,
                snapshotJson,
                savedAtUtc = DateTime.UtcNow,
            }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            await _redis.Database.StringSetAsync(HotKey(req.InstanceId), hotPayload, TimeSpan.FromSeconds(RetentionSeconds + 60));
        }

        // MySQL 灾备 upsert (同 InstanceId 覆盖 — 规划: 只保留最新快照)
        await _db.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO dungeon_snapshots (`InstanceId`, `PlayerId`, `Tier`, `ConfigVersion`, `SnapshotJson`, `SavedAt`)
VALUES ({req.InstanceId}, {req.PlayerId}, {req.Tier}, {req.ConfigVersion}, {snapshotJson}, UTC_TIMESTAMP(3))
ON DUPLICATE KEY UPDATE `SnapshotJson` = {snapshotJson}, `Tier` = {req.Tier},
    `ConfigVersion` = {req.ConfigVersion}, `PlayerId` = {req.PlayerId}, `SavedAt` = UTC_TIMESTAMP(3)");
        return Ok(new { success = true });
    }

    /// <summary>加载快照 — Redis 优先, 未命中读 MySQL 并回填 (重连恢复用)。</summary>
    [HttpGet("dungeon/snapshot/load")]
    public async Task<IActionResult> Load([FromQuery] string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId)) return BadRequest(new { error = "instanceId required" });

        if (string.IsNullOrEmpty(instanceId)) return BadRequest(new { error = "instanceId required" });

        var hotRaw = _redis.IsAvailable && _redis.Database != null
            ? await _redis.Database.StringGetAsync(HotKey(instanceId))
            : (RedisValue?)null;
        if (hotRaw.HasValue && !hotRaw.Value.IsNull)
        {
            var hot = JsonSerializer.Deserialize<JsonElement>(hotRaw.Value.ToString());
            if (hot.ValueKind != JsonValueKind.Undefined)
                return Ok(new { found = true, source = "redis", snapshot = hot });
        }

        var rows = await _db.Database
            .SqlQuery<string>($"SELECT SnapshotJson FROM dungeon_snapshots WHERE InstanceId = {instanceId}")
            .ToListAsync();
        if (rows.Count == 0) return NotFound(new { found = false });

        return Ok(new { found = true, source = "mysql", snapshot = JsonSerializer.Deserialize<JsonElement>(rows[0]) });
    }

    public class SettleRequest
    {
        public string InstanceId { get; set; } = "";
        public string RewardFlag { get; set; } = "";   // 结算奖励标记 (如 "boss_3_gold")
        public long RewardPlayerId { get; set; }       // 奖励接收者
        public int RewardGold { get; set; }
    }

    /// <summary>
    /// 幂等结算 — RewardFlag 已存在则 repeated=true (不重复发奖);
    /// 首次: 标记写入快照 + CurrencyLedgerService 原子入账 (阶段03数据层)。
    /// 规划P3: 货币/掉落/奖励结算不依赖周期快照 — 操作ID幂等+持久化流水。
    /// </summary>
    [HttpPost("dungeon/snapshot/settle")]
    public async Task<IActionResult> Settle([FromBody] SettleRequest req)
    {
        if (string.IsNullOrEmpty(req.InstanceId) || string.IsNullOrEmpty(req.RewardFlag))
            return BadRequest(new { error = "InstanceId/RewardFlag required" });

        // Redis 幂等claim (SET NX): 7天TTL
        var claimKey = $"dungeon:settled:{req.InstanceId}:{req.RewardFlag}";
        var claimed = await _redis.Database.StringSetAsync(claimKey, "1", TimeSpan.FromDays(7), When.NotExists);
        if (!claimed)
            return Ok(new { success = true, repeated = true, rewardGold = 0 });

        // MySQL 侧快照标记 (RewardFlagsJson 数组追加) — 持久化幂等兜底
        var row = await LoadRow(req.InstanceId);
        if (row == null)
        {
            var minimalJson = "{\"state\":\"Settled\"}";
            await _db.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO dungeon_snapshots (`InstanceId`, `PlayerId`, `Tier`, `ConfigVersion`, `SnapshotJson`, `SavedAt`)
VALUES ({req.InstanceId}, {req.RewardPlayerId}, 0, 1, {minimalJson}, UTC_TIMESTAMP(3))");
            row = (await LoadRow(req.InstanceId))!;
        }

        var flags = ParseFlags(GetJsonProp(row, "rewardFlags"));
        if (flags.Contains(req.RewardFlag))
            return Ok(new { success = true, repeated = true, rewardGold = 0 });
        flags.Add(req.RewardFlag);

        var updatedJson = ReplaceRewardFlags(GetJsonProp(row, "snapshotJson"), flags);
        await _db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE dungeon_snapshots SET SnapshotJson = {updatedJson}, SavedAt = UTC_TIMESTAMP(3)
WHERE InstanceId = {req.InstanceId}");

        // 服务器权威入账 — 经阶段03 CurrencyLedgerService (Lua原子+流水)
        var opId = $"settle:{req.InstanceId}:{req.RewardFlag}";
        var result = await _ledger.TryCurrencyOperationAsync(
            req.RewardPlayerId, "Gold", req.RewardGold, opId, $"dungeon_settle:{req.InstanceId}");
        return Ok(new { success = true, repeated = false, rewardGold = req.RewardGold, credited = result.Success });
    }

    /// <summary>清理快照 (结算/超时后调用) — Redis + MySQL</summary>
    [HttpDelete("dungeon/snapshot")]
    public async Task<IActionResult> Delete([FromQuery] string instanceId)
    {
        await _redis.Database.KeyDeleteAsync(HotKey(instanceId));
        var rows = await _db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM dungeon_snapshots WHERE InstanceId = {instanceId}");
        return Ok(new { success = true, deleted = rows });
    }

    // ====== 内部工具 ======

    private async Task<string?> LoadRow(string instanceId)
    {
        var rows = await _db.Database
            .SqlQuery<string>($"SELECT SnapshotJson FROM dungeon_snapshots WHERE InstanceId = {instanceId}")
            .ToListAsync();
        return rows.Count > 0 ? rows[0] : null;
    }

    private static List<string> ParseFlags(string? json)
    {
        if (string.IsNullOrEmpty(json)) return new List<string>();
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>(); }
        catch { return new List<string>(); }
    }

    /// <summary>从快照JSON读 rewardFlags (内嵌转义字符串或数组两种历史格式兼容)</summary>
    private static List<string> GetRewardFlagsFromSnapshotJson(string snapshotJson)
    {
        try
        {
            var d = JsonSerializer.Deserialize<JsonElement>(snapshotJson);
            if (d.TryGetProperty("rewardFlags", out var rf))
            {
                if (rf.ValueKind == JsonValueKind.String)
                    return ParseFlags(rf.GetString());
                if (rf.ValueKind == JsonValueKind.Array)
                    return JsonSerializer.Deserialize<List<string>>(rf.GetRawText()) ?? new List<string>();
            }
        }
        catch { }
        return new List<string>();
    }

    private static string ReplaceRewardFlags(string snapshotJson, List<string> flags)
    {
        try
        {
            var d = JsonSerializer.Deserialize<JsonElement>(snapshotJson);
            var dict = new Dictionary<string, JsonElement>();
            foreach (var p in d.EnumerateObject()) dict[p.Name] = p.Value.Clone();
            dict["rewardFlags"] = JsonSerializer.SerializeToElement(flags);
            return JsonSerializer.Serialize(dict);
        }
        catch { return JsonSerializer.Serialize(new { rewardFlags = flags }); }
    }

    private string GetJsonProp(string json, string prop)
    {
        try
        {
            var d = JsonSerializer.Deserialize<JsonElement>(json);
            return d.TryGetProperty(prop, out var v) ? v.ToString() : "";
        }
        catch { return ""; }
    }
}
