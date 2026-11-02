using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// 规划阶段06 验收测试 — 地牢快照 E2E (HttpClient, 需本地服务器运行)。
/// 运行: dotnet run --project Server/ArchTest -c Release -- phase06
/// </summary>
internal static class Phase06Test
{
    private static readonly HttpClient Http = new();

    private static int _pass, _fail;
    private static void Check(string name, bool ok, string detail = "")
    {
        Console.WriteLine((ok ? "[PASS] " : "[FAIL] ") + name + (detail.Length > 0 ? "  (" + detail + ")" : ""));
        if (ok) _pass++; else _fail++;
    }

    private static async Task<(int status, string body)> HttpAsync(string method, string url, string token, string jsonBody)
    {
        using var req = new HttpRequestMessage(new HttpMethod(method), url);
        if (!string.IsNullOrEmpty(token)) req.Headers.Add("Authorization", "Bearer " + token);
        if (jsonBody != null)
            req.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        var resp = await Http.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();
        return ((int)resp.StatusCode, body);
    }

    public static async Task<int> Run()
    {
        // ---------- 登录 testbot 拿token ----------
        var login = await HttpAsync("POST", "http://localhost:5132/api/auth/login", null,
            JsonSerializer.Serialize(new { username = "warrior1", password = "123456" }));
        if (login.status != 200) { Check("登录warrior1", false, login.body); return 1; }
        var token = JsonSerializer.Deserialize<JsonElement>(login.body).GetProperty("token").GetString()!;
        Check("登录warrior1", true);

        const string inst = "arch-test-inst-1";

        // ---------- save 快照 ----------
        var saveBody = JsonSerializer.Serialize(new
        {
            instanceId = inst,
            playerId = 1L,
            tier = 0,
            configVersion = 1,
            templateId = 3,
            riftLayer = 2,
            randomSeed = 777,
            wave = 2,
            survivalTime = 123.5f,
            membersJson = "[{\"playerId\":1,\"x\":1.5,\"y\":2.5,\"hp\":800,\"maxHp\":1000,\"downed\":false}]",
            monstersJson = "[{\"spawnId\":3,\"hp\":450,\"phase\":2}]",
            rewardFlagsJson = "[\"boss_3_gold\"]",
            state = "Fighting",
        });
        var save = await HttpAsync("POST", "http://localhost:5132/api/dungeon/snapshot/save", token, saveBody);
        Check("快照保存", save.status == 200, save.body.Length > 80 ? save.body[..80] : save.body);

        // ---------- load (Redis热) 一致性 ----------
        var load1 = await HttpAsync("GET", $"http://localhost:5132/api/dungeon/snapshot/load?instanceId={inst}", token, null);
        Check("load(Redis热)", load1.status == 200 && load1.body.Contains("\"source\":\"redis\""), load1.body.Length > 60 ? load1.body[..60] : load1.body);
        Check("load内容: 怪物HP=450", load1.body.Contains("450"));

        // ---------- settle 幂等 ×2 (唯一flag: 每次运行新GUID, 避免远程Redis残留claim) ----------
        var uniqueFlag = "boss_3_gold_" + Guid.NewGuid().ToString("N")[..8];
        var settleBody = JsonSerializer.Serialize(new { instanceId = inst, rewardFlag = uniqueFlag, rewardPlayerId = 1L, rewardGold = 300 });
        var s1 = await HttpAsync("POST", "http://localhost:5132/api/dungeon/snapshot/settle", token, settleBody);
        var s2 = await HttpAsync("POST", "http://localhost:5132/api/dungeon/snapshot/settle", token, settleBody);
        Check("结算首次: +300金币入账", s1.status == 200 && s1.body.Contains("\"repeated\":false") && s1.body.Contains("300"), s1.body.Length > 60 ? s1.body[..60] : s1.body);
        Check("结算重复: 不重复发奖", s2.status == 200 && s2.body.Contains("\"repeated\":true") && s2.body.Contains("\"rewardGold\":0"), s2.body.Length > 60 ? s2.body[..60] : s2.body);

        // ---------- delete + load 404 ----------
        var del = await HttpAsync("DELETE", $"http://localhost:5132/api/dungeon/snapshot?instanceId={inst}", token, null);
        var load2 = await HttpAsync("GET", $"http://localhost:5132/api/dungeon/snapshot/load?instanceId={inst}", token, null);
        Check("delete后load=404", load2.status == 404, $"status={load2.status}");

        Console.WriteLine($"==== 阶段06 地牢快照 验收: {_pass} 通过 / {_fail} 失败 ====");
        return _fail == 0 ? 0 : 1;
    }
}
