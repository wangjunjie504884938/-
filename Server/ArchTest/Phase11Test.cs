using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// 规划阶段11 验收测试 — 节点注册/容量调度/健康摘除/重连重分配/心跳恢复 (HTTP E2E, 需本地服务器)。
/// 运行: dotnet run --project Server/ArchTest -c Release -- phase11
/// 流程: 注册节点A(3s后)B → 6用户开局验证容量调度分布 → probe(1s)摘除A →
///       A上会话重连被重分配到B(fencing递增) → B满时重连503 → A心跳恢复 → 重新开局 → 优雅注销B。
/// </summary>
internal static class Phase11Test
{
    private static readonly HttpClient Http = new();

    private static int _pass, _fail;
    private static void Check(string name, bool ok, string detail = "")
    {
        Console.WriteLine((ok ? "[PASS] " : "[FAIL] ") + name + (detail.Length > 0 ? "  (" + detail + ")" : ""));
        if (ok) _pass++; else _fail++;
    }

    private static async Task<(int status, string body)> HttpAsync(string method, string url, string token, string jsonBody,
        (string name, string value)[] headers = null)
    {
        using var req = new HttpRequestMessage(new HttpMethod(method), url);
        if (!string.IsNullOrEmpty(token)) req.Headers.Add("Authorization", "Bearer " + token);
        if (headers != null)
            foreach (var h in headers)
                req.Headers.Add(h.name, h.value);
        if (jsonBody != null)
            req.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        var resp = await Http.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();
        return ((int)resp.StatusCode, body);
    }

    private static JsonElement Json(string body) => JsonSerializer.Deserialize<JsonElement>(body);

    private static async Task<string> Login(string user)
    {
        var (status, body) = await HttpAsync("POST", "http://localhost:5132/api/auth/login", null,
            JsonSerializer.Serialize(new { username = user, password = "123456" }));
        return status == 200 ? Json(body).GetProperty("token").GetString()! : null;
    }

    private static readonly (string key, string value)[] NodeKey = { ("X-Node-Key", "dev-node-key") };
    private static readonly (string key, string value)[] GmKey = { ("X-GM-Key", "dev-gm-key") };

    private class UserSession
    {
        public string User;
        public string Token;
        public string SessionId;
        public int LineId;
        public long FencingToken;
        public string NodeHost;
        public int NodePort;
    }

    private static async Task<UserSession> OpenSession(string user, int buildVersion = 1)
    {
        var token = await Login(user);
        if (token == null) return null;
        var (status, body) = await HttpAsync("POST", "http://localhost:5132/api/session/open", token,
            JsonSerializer.Serialize(new { playerId = 0, connectionId = Guid.NewGuid().ToString("N"), buildVersion }));
        if (status != 200) return null;
        var j = Json(body);
        return new UserSession
        {
            User = user,
            Token = token,
            SessionId = j.GetProperty("sessionId").GetString()!,
            LineId = j.GetProperty("lineId").GetInt32(),
            FencingToken = j.GetProperty("fencingToken").GetInt64(),
            NodeHost = j.TryGetProperty("nodeHost", out var h) && h.ValueKind == JsonValueKind.String ? h.GetString() : null,
            NodePort = j.TryGetProperty("nodePort", out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : 0,
        };
    }

    public static async Task<int> Run()
    {
        // 每次运行用唯一节点ID — 测试可重复执行, 不被上一轮残留的节点/线状态污染
        var nodeAId = "node-a-" + Guid.NewGuid().ToString("N")[..8];
        var nodeBId = "node-b-" + Guid.NewGuid().ToString("N")[..8];
        var nodeCId = "node-c-" + Guid.NewGuid().ToString("N")[..8];

        // ---------- 1. 节点注册 (A先注册, 让它比B老3秒以上供超时判定) ----------
        var (regA, regABody) = await HttpAsync("POST", "http://localhost:5132/api/node/register", null,
            JsonSerializer.Serialize(new { nodeId = nodeAId, host = "10.0.0.1", mirrorPort = 7801, httpPort = 7802, capacity = 3, buildVersion = 1 }),
            NodeKey);
        Check("节点A注册", regA == 200 && regABody.Contains("lineId"), regABody.Length > 80 ? regABody[..80] : regABody);
        var lineA = Json(regABody).GetProperty("lineId").GetInt32();

        await Task.Delay(3200); // A的心跳年龄拉开 (后面用1秒超时判定摘除)

        var (regB, regBBody) = await HttpAsync("POST", "http://localhost:5132/api/node/register", null,
            JsonSerializer.Serialize(new { nodeId = nodeBId, host = "10.0.0.2", mirrorPort = 7802, httpPort = 7803, capacity = 3, buildVersion = 1 }),
            NodeKey);
        Check("节点B注册", regB == 200 && regBBody.Contains("lineId"), regBBody.Length > 80 ? regBBody[..80] : regBBody);
        var lineB = Json(regBBody).GetProperty("lineId").GetInt32();
        Check("A/B绑定不同线", lineA != lineB && lineA >= 100 && lineB >= 100, $"lineA={lineA} lineB={lineB}");

        // 版本兼容 (规划阶段11): v2节点对v1客户端不可见 — 4个开局都不应落在线C上
        var (regC, regCBody) = await HttpAsync("POST", "http://localhost:5132/api/node/register", null,
            JsonSerializer.Serialize(new { nodeId = nodeCId, host = "10.0.0.3", mirrorPort = 7803, capacity = 3, buildVersion = 2 }),
            NodeKey);
        var lineC = Json(regCBody).GetProperty("lineId").GetInt32();
        Check("v2节点C注册", regC == 200 && lineC > lineB, $"status={regC} line={lineC}");

        // ---------- 2. 容量调度: 4用户按最少人数优先+LineId升序分布 (v1客户端只见v1线) ----------
        var users = new[] { "warrior1", "mage1", "priest1", "assassin1" };
        var sessions = new List<UserSession>();
        foreach (var u in users)
        {
            var s = await OpenSession(u);
            if (s == null) { Check($"开局{u}", false, "open失败"); continue; }
            // 服务器规则: 最少人数优先, 平局取LineId升序 (阶段11起无预分配线, 只在节点线上分配)
            var countA = sessions.Count(x => x.LineId == lineA);
            var countB = sessions.Count(x => x.LineId == lineB);
            var expectLine = countA <= countB ? lineA : lineB;
            Check($"开局{u}→线{s.LineId}", s.LineId == expectLine, $"期望{expectLine} (分布A={countA} B={countB})");
            sessions.Add(s);
        }
        var sWarrior = sessions.First(s => s.User == "warrior1");
        var sMage = sessions.First(s => s.User == "mage1");
        Check("开局返回节点地址", sWarrior.NodeHost == "10.0.0.1" || sMage.NodeHost == "10.0.0.2",
            $"warrior={sWarrior.NodeHost}:{sWarrior.NodePort} mage={sMage.NodeHost}:{sMage.NodePort}");

        // ---------- 3. 故障摘除: node-a 心跳超时 (probe用1秒超时; 先心跳B/C刷新年龄, 只让A过期) ----------
        var (hbb, _) = await HttpAsync("POST", "http://localhost:5132/api/node/heartbeat", null,
            JsonSerializer.Serialize(new { nodeId = nodeBId, load = 2, accepting = true }), NodeKey);
        Check("node-b心跳刷新", hbb == 200, $"status={hbb}");
        await HttpAsync("POST", "http://localhost:5132/api/node/heartbeat", null,
            JsonSerializer.Serialize(new { nodeId = nodeCId, load = 0, accepting = true }), NodeKey);

        var (probe, probeBody) = await HttpAsync("POST", "http://localhost:5132/api/node/probe", null,
            JsonSerializer.Serialize(new { timeoutSeconds = 1 }), GmKey);
        var probeJson = Json(probeBody);
        var unhealthyCount = probeJson.GetProperty("unhealthy").GetArrayLength();
        Check("probe摘除node-a", probe == 200 && unhealthyCount == 1, probeBody.Length > 80 ? probeBody[..80] : probeBody);

        var (list1, list1Body) = await HttpAsync("GET", "http://localhost:5132/api/node/list", null, null);
        Check("node/list标记A不健康", list1 == 200 && list1Body.Contains("\"healthy\":false"), list1Body.Length > 60 ? list1Body[..60] : list1Body);

        // ---------- 4. 重连重分配: A上的会话转投B ----------
        var (re1, re1Body) = await HttpAsync("POST", "http://localhost:5132/api/session/reconnect", sWarrior.Token,
            JsonSerializer.Serialize(new { sessionId = sWarrior.SessionId }));
        var re1Json = Json(re1Body);
        var reassigned = re1Json.TryGetProperty("reassigned", out var ra) && ra.GetBoolean();
        var newLine = re1Json.TryGetProperty("lineId", out var nl) ? nl.GetInt32() : -1;
        var newToken = re1Json.TryGetProperty("fencingToken", out var ft) ? ft.GetInt64() : -1;
        var newHost = re1Json.TryGetProperty("nodeHost", out var nh) && nh.ValueKind == JsonValueKind.String ? nh.GetString() : null;
        Check("warrior1重连重分配到B", re1 == 200 && reassigned && newLine == lineB && newHost == "10.0.0.2",
            $"status={re1} reassigned={reassigned} line={newLine} host={newHost}");
        Check("fencing token递增", newToken == sWarrior.FencingToken + 1, $"{sWarrior.FencingToken}→{newToken}");

        // B线现在 3/3 满 (mage/priest/assassin + warrior转投) → priest1 (原A线) 重连应503
        var sPriest = sessions.First(s => s.User == "priest1");
        var (re2, re2Body) = await HttpAsync("POST", "http://localhost:5132/api/session/reconnect", sPriest.Token,
            JsonSerializer.Serialize(new { sessionId = sPriest.SessionId }));
        Check("无健康线可接管503", re2 == 503, $"status={re2} {re2Body[..Math.Min(60, re2Body.Length)]}");

        // ---------- 5. 心跳恢复: node-a恢复 → 线重新接收 ----------
        var (hb, hbBody) = await HttpAsync("POST", "http://localhost:5132/api/node/heartbeat", null,
            JsonSerializer.Serialize(new { nodeId = nodeAId, load = 0, accepting = true }), NodeKey);
        var hbHealthy = hb == 200 && Json(hbBody).GetProperty("healthy").GetBoolean();
        Check("node-a心跳恢复", hbHealthy, $"status={hb} {hbBody[..Math.Min(60, hbBody.Length)]}");

        var sUser5 = await OpenSession("tank1");
        Check("恢复后新开局进A线", sUser5 != null && sUser5.LineId == lineA,
            sUser5 != null ? $"line={sUser5.LineId} host={sUser5.NodeHost}" : "open失败");

        // ---------- 6. 优雅注销: B注销后不可见 ----------
        var (dereg, _) = await HttpAsync("POST", "http://localhost:5132/api/node/deregister", null,
            JsonSerializer.Serialize(new { nodeId = nodeBId }), NodeKey);
        var (list2, list2Body) = await HttpAsync("GET", "http://localhost:5132/api/node/list", null, null);
        Check("node-b优雅注销", dereg == 200 && !list2Body.Contains(nodeBId), $"status={dereg}");

        // ---------- 7. 无密钥拒绝 ----------
        var (noKey, _) = await HttpAsync("POST", "http://localhost:5132/api/node/register", null,
            JsonSerializer.Serialize(new { nodeId = "evil", host = "x" }));
        Check("无Node密钥401", noKey == 401, $"status={noKey}");

        Console.WriteLine($"==== 阶段11 节点注册/健康摘除/重连重分配 验收: {_pass} 通过 / {_fail} 失败 ====");
        return _fail == 0 ? 0 : 1;
    }
}
