using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 规划阶段12 HTTP层压力测试 — 固定口径: N虚拟玩家 × T秒。
/// 玩家语义: 开局重试至成功 → 全程持有会话 → 每5秒快照save/load (Tier P0) → 每30秒save/ping → 每60秒支付创建/回调/重放。
/// 指标: 每操作次数/p50/p95/p99/max延迟、错误数、支付重放违规数(必须为0)。
/// 运行: dotnet run --project Server/ArchTest -c Release -- loadhttp [baseUrl] [bots] [seconds]
/// 默认: http://127.0.0.1:5132 50 600
/// 虚拟玩家账号: loadbot-01..NN (自动注册); 虚拟节点: loadnode-xxx (自动注册+心跳, 结束注销)。
/// </summary>
internal static class LoadTestHttp
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    private class OpStat
    {
        public long Count;
        public long Errors;
        public readonly object Lock = new();
        public readonly List<double> Latencies = new();
    }

    private static readonly ConcurrentDictionary<string, OpStat> Stats = new();
    private static long _replayViolations;
    private static long _totalOps;

    private static void Record(string op, double ms, bool ok)
    {
        var s = Stats.GetOrAdd(op, _ => new OpStat());
        lock (s.Lock)
        {
            s.Count++;
            s.Latencies.Add(ms);
            if (!ok) s.Errors++;
        }
        Interlocked.Increment(ref _totalOps);
    }

    private static double Pct(List<double> sorted, double p)
    {
        if (sorted.Count == 0) return 0;
        var idx = (int)Math.Ceiling(sorted.Count * p) - 1;
        return sorted[Math.Clamp(idx, 0, sorted.Count - 1)];
    }

    public static async Task<int> Run(string[] args)
    {
        var baseUrl = args.Length > 1 ? args[1].TrimEnd('/') : "http://127.0.0.1:5132";
        var botCount = args.Length > 2 ? int.Parse(args[2]) : 50;
        var seconds = args.Length > 3 ? int.Parse(args[3]) : 600;

        Console.WriteLine($"==== 阶段12 HTTP压测: {botCount}虚拟玩家 × {seconds}s @ {baseUrl} ====");
        var deadline = DateTime.UtcNow.AddSeconds(seconds);

        // ---------- 0. 注册/登录虚拟玩家账号 ----------
        var tokens = new string[botCount];
        for (int i = 0; i < botCount; i++)
        {
            var name = $"loadbot-{i + 1:D2}";
            var sw = Stopwatch.StartNew();
            var (regStatus, _) = await Post($"{baseUrl}/api/auth/register",
                JsonSerializer.Serialize(new { username = name, password = "123456" }));
            Record("register", sw.Elapsed.TotalMilliseconds, regStatus is 200 or 400 or 409); // 已存在也算成功

            sw.Restart();
            var (loginStatus, loginBody) = await Post($"{baseUrl}/api/auth/login",
                JsonSerializer.Serialize(new { username = name, password = "123456" }));
            Record("login", sw.Elapsed.TotalMilliseconds, loginStatus == 200);
            tokens[i] = loginStatus == 200
                ? Json(loginBody).GetProperty("token").GetString()!
                : null;
        }
        var loggedIn = tokens.Count(t => t != null);
        Console.WriteLine($"[预热] 登录成功 {loggedIn}/{botCount}");

        // ---------- 0.5 注册压测虚拟节点 (阶段11起无预分配线, 开局必须有节点; 心跳防摘除) ----------
        var loadNodeId = "loadnode-" + Guid.NewGuid().ToString("N")[..8];
        var (regN, regNBody) = await PostNodeAuth($"{baseUrl}/api/node/register",
            JsonSerializer.Serialize(new { nodeId = loadNodeId, host = "127.0.0.1", mirrorPort = 7777, capacity = botCount, buildVersion = 1 }),
            "dev-node-key");
        Console.WriteLine($"[预热] 压测节点注册: {regN} {(regNBody.Length > 80 ? regNBody[..80] : regNBody)}");
        var nodeHbCts = new CancellationTokenSource();
        var nodeHb = Task.Run(async () =>
        {
            while (!nodeHbCts.IsCancellationRequested)
            {
                try
                {
                    await PostNodeAuth($"{baseUrl}/api/node/heartbeat",
                        JsonSerializer.Serialize(new { nodeId = loadNodeId, load = 0, accepting = true }), "dev-node-key");
                }
                catch { }
                await Task.Delay(10000, nodeHbCts.Token);
            }
        });

        // ---------- 1. 虚拟玩家循环 (开局重试至成功, 全程持有会话) ----------
        var deadlineCts = new CancellationTokenSource();
        var workers = new List<Task>();
        for (int i = 0; i < botCount; i++)
        {
            var idx = i;
            workers.Add(Task.Run(() => WorkerLoop(idx, baseUrl, tokens[idx], deadline, deadlineCts.Token)));
        }

        var progress = Task.Run(async () =>
        {
            while (!deadlineCts.IsCancellationRequested)
            {
                try { await Task.Delay(10000, deadlineCts.Token); }
                catch (TaskCanceledException) { break; }
                var total = Interlocked.Read(ref _totalOps);
                Console.WriteLine($"[进度] {DateTime.UtcNow:HH:mm:ss} 累计操作={total}");
            }
        });

        await Task.WhenAll(workers);
        deadlineCts.Cancel();
        nodeHbCts.Cancel();
        await PostNodeAuth($"{baseUrl}/api/node/deregister",
            JsonSerializer.Serialize(new { nodeId = loadNodeId }), "dev-node-key");

        // ---------- 2. 报告 ----------
        Console.WriteLine("\n==== 阶段12 HTTP压测报告 ====");
        Console.WriteLine($"{"操作",-24}{"次数",8}{"错误",6}{"p50",10}{"p95",10}{"p99",10}{"max",10}  (ms)");
        var totalErrors = 0L;
        var totalCount = 0L;
        foreach (var kv in Stats.OrderBy(k => k.Key))
        {
            List<double> sorted;
            long cnt, err;
            lock (kv.Value.Lock)
            {
                sorted = new List<double>(kv.Value.Latencies);
                sorted.Sort();
                cnt = kv.Value.Count;
                err = kv.Value.Errors;
            }
            totalErrors += err;
            totalCount += cnt;
            Console.WriteLine($"{kv.Key,-24}{cnt,8}{err,6}{Pct(sorted, 0.5),10:F1}{Pct(sorted, 0.95),10:F1}{Pct(sorted, 0.99),10:F1}{(sorted.Count > 0 ? sorted[^1] : 0),10:F1}");
        }
        var violations = Interlocked.Read(ref _replayViolations);
        Console.WriteLine($"----");
        Console.WriteLine($"总操作={totalCount} 错误={totalErrors} 错误率={(totalCount > 0 ? 100.0 * totalErrors / totalCount : 0):F2}%  支付重放违规={violations}");
        var fail = totalErrors > 0 || violations > 0;
        Console.WriteLine(fail ? "==== 阶段12 HTTP压测: FAIL ====" : "==== 阶段12 HTTP压测: PASS ====");
        return fail ? 1 : 0;
    }

    /// <summary>
    /// 单虚拟玩家 (真实玩家语义): 开局重试至成功 → 全程持有会话 →
    /// 每5秒快照save→load (Tier P0) → 每30秒save/ping → 每60秒支付创建/回调/重放 → 结束关闭。
    /// </summary>
    private static async Task WorkerLoop(int idx, string baseUrl, string token, DateTime deadline, CancellationToken ct)
    {
        if (token == null) return;

        // 开局 (满载口径下初始竞争503属预期, 重试至成功)
        var sessionId = "";
        while (sessionId == "" && DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            var sw = Stopwatch.StartNew();
            var (openStatus, openBody) = await PostAuth($"{baseUrl}/api/session/open", token,
                JsonSerializer.Serialize(new { connectionId = Guid.NewGuid().ToString("N"), buildVersion = 1 }));
            Record("session/open", sw.Elapsed.TotalMilliseconds, openStatus == 200);
            if (openStatus == 200)
                sessionId = Json(openBody).GetProperty("sessionId").GetString()!;
            else
                await Task.Delay(1000, ct);
        }
        if (sessionId == "") return;

        var iteration = 0;
        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            try
            {
                iteration++;

                // 每5秒: 快照 save→load (Tier P0口径)
                var inst = $"lt-{idx}-{iteration}";
                var snapBody = JsonSerializer.Serialize(new
                {
                    instanceId = inst,
                    playerId = (long)idx + 1,
                    tier = 0,
                    configVersion = 1,
                    templateId = 3,
                    riftLayer = 2,
                    randomSeed = 777,
                    wave = 2,
                    survivalTime = 123.5f,
                    membersJson = $"[{{\"playerId\":{idx + 1},\"x\":1.5,\"y\":2.5,\"hp\":800,\"maxHp\":1000,\"downed\":false}}]",
                    monstersJson = "[{\"spawnId\":3,\"hp\":450,\"phase\":2}]",
                    rewardFlagsJson = "[\"boss_3_gold\"]",
                    state = "Fighting",
                });

                var sw = Stopwatch.StartNew();
                var (s1, _) = await PostAuth($"{baseUrl}/api/dungeon/snapshot/save", token, snapBody);
                Record("dungeon/snapshot/save", sw.Elapsed.TotalMilliseconds, s1 == 200);

                sw.Restart();
                using (var req = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/dungeon/snapshot/load?instanceId={inst}"))
                {
                    req.Headers.Add("Authorization", "Bearer " + token);
                    using var resp = await Http.SendAsync(req);
                    Record("dungeon/snapshot/load", sw.Elapsed.TotalMilliseconds, (int)resp.StatusCode == 200);
                }

                // 每30秒 (6次迭代): save/ping
                if (iteration % 6 == 0)
                {
                    sw.Restart();
                    var (s3, _) = await PostAuth($"{baseUrl}/api/save/ping", token, "{}");
                    Record("save/ping", sw.Elapsed.TotalMilliseconds, s3 == 200);
                }

                // 每60秒 (12次迭代): 支付创建/回调/重放 (playerId=1=专用测试账号testbot; 重放幂等必须成立)
                if (iteration % 12 == 0)
                {
                    sw.Restart();
                    var (c1, c1Body) = await PostAuth($"{baseUrl}/api/payment/create", token,
                        JsonSerializer.Serialize(new { playerId = 1, productId = "plane_60", channel = "wechat" }));
                    Record("payment/create", sw.Elapsed.TotalMilliseconds, c1 == 200);
                    if (c1 == 200)
                    {
                        var j = Json(c1Body);
                        var orderId = j.GetProperty("orderId").GetString()!;
                        var sign = j.GetProperty("sign").GetString()!;
                        var amount = j.GetProperty("amountCents").GetInt64();
                        var cbBody = JsonSerializer.Serialize(new { orderId, sign, amountCents = amount, playerId = 1 });

                        sw.Restart();
                        var (cb1, cb1Body) = await PostAuth($"{baseUrl}/api/payment/callback", token, cbBody);
                        Record("payment/callback", sw.Elapsed.TotalMilliseconds, cb1 == 200 && cb1Body.Contains("\"repeated\":false"));

                        sw.Restart();
                        var (cb2, cb2Body) = await PostAuth($"{baseUrl}/api/payment/callback", token, cbBody);
                        var replayOk = cb2 == 200 && cb2Body.Contains("\"repeated\":true");
                        Record("payment/replay", sw.Elapsed.TotalMilliseconds, replayOk);
                        if (!replayOk) Interlocked.Increment(ref _replayViolations);
                    }
                }

                await Task.Delay(5000, ct); // P0快照口径5秒
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Record("worker-exception", 0, false);
                Console.WriteLine($"[worker{idx}] {ex.Message}");
                await Task.Delay(1000, ct);
            }
        }

        // 结束关闭
        try
        {
            var sw = Stopwatch.StartNew();
            var (cl, _) = await PostAuth($"{baseUrl}/api/session/close", token,
                JsonSerializer.Serialize(new { sessionId }));
            Record("session/close", sw.Elapsed.TotalMilliseconds, cl == 200);
        }
        catch { }
    }

    private static async Task<(int status, string body)> Post(string url, string json)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        var resp = await Http.SendAsync(req);
        return ((int)resp.StatusCode, await resp.Content.ReadAsStringAsync());
    }

    private static async Task<(int status, string body)> PostAuth(string url, string token, string json)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Add("Authorization", "Bearer " + token);
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        var resp = await Http.SendAsync(req);
        return ((int)resp.StatusCode, await resp.Content.ReadAsStringAsync());
    }

    private static async Task<(int status, string body)> PostNodeAuth(string url, string json, string nodeKey)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Add("X-Node-Key", nodeKey);
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        var resp = await Http.SendAsync(req);
        return ((int)resp.StatusCode, await resp.Content.ReadAsStringAsync());
    }

    private static JsonElement Json(string body) => JsonSerializer.Deserialize<JsonElement>(body);
}
