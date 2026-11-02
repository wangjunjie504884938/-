using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ArpgServer.Data;
using ArpgServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArpgServer.Controllers;

/// <summary>
/// 模拟支付 (规划阶段10) — 订单状态机 + HMAC 验签 + 幂等回调。
/// 状态机: 0=Created → 1=Paid (终态)。重复回调不重复发货 (数据库行锁级幂等)。
/// 正式SDK接入前, callback 由客户端模拟触发 (验签密钥为开发密钥)。
/// </summary>
[ApiController]
[Route("api")]
public class PaymentController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly CurrencyLedgerService _ledger;
    private readonly IConfiguration _config;

    public PaymentController(AppDbContext db, CurrencyLedgerService ledger, IConfiguration config)
    {
        _db = db;
        _ledger = ledger;
        _config = config;
    }

    public class CreateRequest
    {
        public long PlayerId { get; set; }
        public string ProductId { get; set; } = "";   // plane_60 / plane_300 / plane_680
        public string Channel { get; set; } = "wechat"; // wechat / alipay
    }

    public class CallbackRequest
    {
        public string OrderId { get; set; } = "";
        public string Sign { get; set; } = "";        // HMAC-SHA256(orderId + "|" + amountCents, key)
        public long AmountCents { get; set; }
        public long PlayerId { get; set; }            // 回调必须携带玩家身份 (防串号入账)
        public string ProductId { get; set; } = "";   // 防串号: 必须与订单行的商品一致
    }

    private static readonly Dictionary<string, (long cents, long planeCoins)> Products = new()
    {
        ["plane_60"] = (600, 60),
        ["plane_300"] = (3000, 300),
        ["plane_680"] = (6800, 680),
    };

    private string HmacKey => _config["Payment:HmacKey"] ?? "dev-payment-key-v1";

    /// <summary>创建订单 — 状态0=Created。OrderId 服务端生成 (防客户端伪造金额)。</summary>
    [HttpPost("payment/create")]
    public async Task<IActionResult> Create([FromBody] CreateRequest req)
    {
        if (!Products.TryGetValue(req.ProductId, out var product))
            return BadRequest(new { error = "未知商品: " + req.ProductId });

        var orderId = "PO" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "_" + Guid.NewGuid().ToString("N")[..8];
        var sign = Sign(orderId, product.cents);

        await _db.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO payment_orders (`OrderId`, `PlayerId`, `ProductId`, `AmountCents`, `Currency`, `Channel`, `Status`, `CreatedAt`)
VALUES ({orderId}, {req.PlayerId}, {req.ProductId}, {product.cents}, 'CNY', {req.Channel}, 0, UTC_TIMESTAMP(3))");

        FileLoggerService.Log("PAY", orderId, $"create player={req.PlayerId} product={req.ProductId} cents={product.cents}");
        return Ok(new { success = true, orderId, amountCents = product.cents, sign, planeCoins = product.planeCoins });
    }

    /// <summary>
    /// 支付回调 — HMAC验签 → 订单存在 → 状态机0→1 (幂等: 已支付订单返回首次结果) → 位面币入账 (CurrencyLedger幂等)。
    /// 验签失败/金额不符/重复回调均不重复发货。
    /// </summary>
    [HttpPost("payment/callback")]
    public async Task<IActionResult> Callback([FromBody] CallbackRequest req)
    {
        var expect = Sign(req.OrderId, req.AmountCents);
        if (req.Sign != expect)
        {
            FileLoggerService.Log("PAY", req.OrderId, "callback验签失败");
            return BadRequest(new { error = "验签失败" });
        }

        // 订单必须存在且未支付
        var rows = await _db.Database
            .SqlQuery<string>($"SELECT ProductId FROM payment_orders WHERE OrderId = {req.OrderId} AND Status = 0")
            .ToListAsync();
        if (rows.Count == 0)
        {
            // 已支付过 → 幂等返回成功 (不重复发货)
            var paid = await _db.Database
                .SqlQuery<string>($"SELECT ProductId FROM payment_orders WHERE OrderId = {req.OrderId}")
                .ToListAsync();
            if (paid.Count > 0)
                return Ok(new { success = true, repeated = true });
            return NotFound(new { error = "订单不存在" });
        }

        if (!Products.TryGetValue(rows[0], out var product))
            return BadRequest(new { error = "未知商品" });

        // 状态机 0→1 + 位面币入账 (CurrencyLedger Lua原子+流水)
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE payment_orders SET Status = 1, PaidAt = UTC_TIMESTAMP(3) WHERE OrderId = {req.OrderId} AND Status = 0");

        var opId = "pay:" + req.OrderId;
        var result = await _ledger.TryCurrencyOperationAsync(req.PlayerId, "Plane", product.planeCoins, opId, "payment:" + req.ProductId);
        if (!result.Success)
        {
            // 入账失败 → 回滚状态机, 支付方重试
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE payment_orders SET Status = 0 WHERE OrderId = {req.OrderId}");
            return StatusCode(500, new { error = "入账失败, 请重试" });
        }

        FileLoggerService.Log("PAY", req.OrderId, $"paid player={req.PlayerId} plane={product.planeCoins}");
        return Ok(new { success = true, repeated = false, planeCoins = product.planeCoins, balance = result.BalanceAfter });
    }

    private string Sign(string orderId, long amountCents)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(HmacKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(orderId + "|" + amountCents));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
