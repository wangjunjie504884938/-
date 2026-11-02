using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// 规划阶段10 验收测试 — 支付状态机 + HMAC验签 + GM接口 (HTTP E2E, 需本地服务器)。
/// 运行: dotnet run --project Server/ArchTest -c Release -- phase10
/// </summary>
internal static class Phase10Test
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
        // ---------- 登录 ----------
        var login = await HttpAsync("POST", "http://localhost:5132/api/auth/login", null,
            JsonSerializer.Serialize(new { username = "warrior1", password = "123456" }));
        if (login.status != 200) { Check("登录warrior1", false, login.body); return 1; }
        var token = JsonSerializer.Deserialize<JsonElement>(login.body).GetProperty("token").GetString()!;
        Check("登录warrior1", true);

        // ---------- GM接口: 无密钥401 ----------
        var noKey = await HttpAsync("GET", "http://localhost:5132/api/gm/stats", null, null);
        Check("GM无密钥401", noKey.status == 401, $"status={noKey.status}");

        // ---------- 创建订单 ----------
        var createBody = JsonSerializer.Serialize(new { playerId = 4L, productId = "plane_60", channel = "wechat" });
        var create = await HttpAsync("POST", "http://localhost:5132/api/payment/create", token, createBody);
        Check("订单创建", create.status == 200 && create.body.Contains("orderId"), create.body.Length > 60 ? create.body[..60] : create.body);
        var orderJson = JsonSerializer.Deserialize<JsonElement>(create.body);
        var orderIdReal = orderJson.GetProperty("orderId").GetString()!;
        var amountCents = orderJson.GetProperty("amountCents").GetInt64();
        var sign = orderJson.GetProperty("sign").GetString()!;

        // ---------- 伪造回调 (错签名) ----------
        var badBody = JsonSerializer.Serialize(new { orderId = orderIdReal, sign = "deadbeef", amountCents = amountCents });
        var bad = await HttpAsync("POST", "http://localhost:5132/api/payment/callback", token, badBody);
        Check("伪造回调被拒", bad.status == 400, $"status={bad.status}");

        // ---------- 正确回调 → 位面币入账 ----------
        var cb = await HttpAsync("POST", "http://localhost:5132/api/payment/callback", token,
            JsonSerializer.Serialize(new { orderId = orderIdReal, sign, amountCents = amountCents, playerId = 4L }));
        Check("正确回调200+入账", cb.status == 200 && cb.body.Contains("\"repeated\":false") && cb.body.Contains("60"), cb.body.Length > 60 ? cb.body[..60] : cb.body);

        // ---------- 重复回调 → 幂等 ----------
        var cb2 = await HttpAsync("POST", "http://localhost:5132/api/payment/callback", token,
            JsonSerializer.Serialize(new { orderId = orderIdReal, sign, amountCents = amountCents, playerId = 4L }));
        Check("重复回调幂等", cb2.status == 200 && cb2.body.Contains("\"repeated\":true"), cb2.body.Length > 60 ? cb2.body[..60] : cb2.body);

        Console.WriteLine($"==== 阶段10 支付/GM 验收: {_pass} 通过 / {_fail} 失败 ====");
        return _fail == 0 ? 0 : 1;
    }
}
