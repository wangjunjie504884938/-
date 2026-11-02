using System.Collections.Generic;
using System.Text.Json;
using StackExchange.Redis;

/// <summary>
/// 规划阶段03 验收测试 (并发扣款/幂等/冷启动/版本保护)。
/// 运行: dotnet run --project Server/ArchTest
/// Redis 连接串从 Server/publish/appsettings.json 读取 (不硬编码密码)。
/// 测试使用一次性键 (Arpg:CurTest:*) 不触碰玩家数据。
/// </summary>
internal static class Program
{
    private const string LuaAtomic =
        """
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

    private static int _pass, _fail;
    private static void Check(string name, bool ok, string detail = "")
    {
        Console.WriteLine((ok ? "[PASS] " : "[FAIL] ") + name + (detail.Length > 0 ? "  (" + detail + ")" : ""));
        if (ok) _pass++; else _fail++;
    }

    private static (bool success, bool repeated, long balance) Parse(string json)
    {
        var d = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        return (
            d["success"].GetBoolean(),
            d.TryGetValue("repeated", out var r) && r.GetBoolean(),
            d["balanceAfter"].GetInt64()
        );
    }

    private static async Task<int> Main(string[] args)
    {
        // phase04: 会话/分线逻辑单测 (无需Redis/MySQL)
        if (args.Length > 0 && args[0] == "phase04")
            return Phase04Test.Run();

        // phase05: 服务器权威/限流逻辑单测 (无需Redis/MySQL)
        if (args.Length > 0 && args[0] == "phase05")
            return Phase05Test.Run();

        // phase06: 地牢快照 E2E (HTTP, 需本地服务器运行)
        if (args.Length > 0 && args[0] == "phase06")
            return await Phase06Test.Run();

        // Redis 连接串: 从服务器发布目录的 appsettings.json 读取
        var settingsPath = args.Length > 0 ? args[0] : "Server/publish/appsettings.json";
        var settingsJson = File.ReadAllText(settingsPath);
        var doc = JsonSerializer.Deserialize<JsonElement>(settingsJson);
        var connStr = doc.GetProperty("Redis").GetProperty("ConnectionString").GetString()!;

        var mux = await ConnectionMultiplexer.ConnectAsync(new ConfigurationOptions
        {
            EndPoints = { connStr.Split(',')[0] },
            Password = ExtractPassword(connStr),
            AbortOnConnectFail = false,
            ConnectTimeout = 5000,
        });
        var db = mux.GetDatabase();

        // ---------- 验收1: 100次并发扣款 (余额500 每次10) 恰好成功50次 ----------
        const string BalKey = "Arpg:CurTest:balance";
        const string OpPrefix = "Arpg:CurTest:op:";
        db.KeyDelete(BalKey);
        db.HashSet(BalKey, "Gold", 500);

        var tasks = new List<Task<RedisResult>>();
        for (int i = 0; i < 100; i++)
            tasks.Add(db.ScriptEvaluateAsync(LuaAtomic,
                new RedisKey[] { BalKey, OpPrefix + i },
                new RedisValue[] { "Gold", -10, 604800 }));

        var results = await Task.WhenAll(tasks);
        int success = 0;
        long finalBalance = 0;
        foreach (var r in results)
        {
            var (ok, repeated, bal) = Parse(r.ToString());
            if (ok) success++;
            finalBalance = bal;
        }
        Check("并发扣款: 100次请求恰好成功50次", success == 50, $"success={success}");
        Check("并发扣款: 最终余额=0", finalBalance == 0, $"balance={finalBalance}");
        Check("并发扣款: 失败50次返回不足", results.Count(r => Parse(r.ToString()).success == false) == 50);

        // ---------- 验收2: OperationId 幂等 (重复请求返回首次结果) ----------
        db.HashSet(BalKey, "Gold", 200);
        var opKey = OpPrefix + "idem";
        var first = Parse((await db.ScriptEvaluateAsync(LuaAtomic,
            new RedisKey[] { BalKey, opKey }, new RedisValue[] { "Gold", -50, 604800 })).ToString());
        var second = Parse((await db.ScriptEvaluateAsync(LuaAtomic,
            new RedisKey[] { BalKey, opKey }, new RedisValue[] { "Gold", -50, 604800 })).ToString());
        Check("幂等: 首次成功", first.success && !first.repeated && first.balance == 150);
        Check("幂等: 重复返回首次结果(不再扣)", second.success && second.repeated && second.balance == 150);

        // ---------- 验收3: 余额不足拒绝 ----------
        db.KeyDelete(BalKey);
        db.HashSet(BalKey, "Gold", 30);
        var insufficient = Parse((await db.ScriptEvaluateAsync(LuaAtomic,
            new RedisKey[] { BalKey, OpPrefix + "ins" }, new RedisValue[] { "Gold", -100, 604800 })).ToString());
        Check("余额不足拒绝", !insufficient.success && insufficient.balance == 30);

        // 清理测试键
        db.KeyDelete(BalKey);
        for (int i = 0; i < 100; i++) db.KeyDelete(OpPrefix + i);
        db.KeyDelete(OpPrefix + "idem");
        db.KeyDelete(OpPrefix + "ins");

        Console.WriteLine($"==== 阶段03 Redis Lua 验收: {_pass} 通过 / {_fail} 失败 ====");
        return _fail == 0 ? 0 : 1;
    }

    private static string? ExtractPassword(string connStr)
    {
        foreach (var part in connStr.Split(','))
        {
            var kv = part.Trim();
            if (kv.StartsWith("password=", StringComparison.OrdinalIgnoreCase))
                return kv["password=".Length..];
        }
        return null;
    }
}
