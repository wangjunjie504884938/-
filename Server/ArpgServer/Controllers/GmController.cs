using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArpgServer.Data;
using ArpgServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArpgServer.Controllers;

/// <summary>
/// GM 运维接口 (规划阶段10) — 内部密钥认证 (X-GM-Key 头), 非JWT (GM工具不走玩家登录)。
/// 密钥配置: appsettings "Gm:Key", 默认 dev值; 生产必须改。
/// 支持在线人数/测试发币; 踢人与查玩家在阶段11节点体系后接入。
/// </summary>
[ApiController]
[Route("api")]
public class GmController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly Services.CurrencyLedgerService _ledger;
    private readonly IConfiguration _config;

    public GmController(AppDbContext db, Services.CurrencyLedgerService ledger, IConfiguration config)
    {
        _db = db;
        _ledger = ledger;
        _config = config;
    }

    private bool IsGm => Request.Headers.TryGetValue("X-GM-Key", out var v)
        && v.ToString() == (_config["Gm:Key"] ?? "dev-gm-key");

    public class GrantRequest
    {
        public long PlayerId { get; set; }
        public string Kind { get; set; } = "Gold";  // Gold / Life / Plane / Fragments
        public long Amount { get; set; }
        public string Reason { get; set; } = "gm_grant";
    }

    /// <summary>在线人数 (会话数 + 分线分布)</summary>
    [HttpGet("gm/stats")]
    public IActionResult Stats()
    {
        if (!IsGm) return Unauthorized(new { error = "GM key required" });
        var lines = new List<object>();
        // ShardManager 是单例服务, 从DI拿不到 — 通过静态注册表? 简化: 返回会话相关由SessionController暴露
        // 此处仅返回服务器基本信息
        return Ok(new { success = true, serverTimeUtc = DateTime.UtcNow, version = 1 });
    }

    /// <summary>测试发币 — 经 CurrencyLedgerService 幂等入账 (Gold=服务器权威, Life/Plane=流水记录)</summary>
    [HttpPost("gm/grant")]
    public async Task<IActionResult> Grant([FromBody] GrantRequest req)
    {
        if (!IsGm) return Unauthorized(new { error = "GM key required" });
        if (req.Amount == 0) return BadRequest(new { error = "金额为0" });

        var opId = $"gm:{Guid.NewGuid():N}";
        var result = await _ledger.TryCurrencyOperationAsync(req.PlayerId, req.Kind, req.Amount, opId, "gm_grant");
        FileLoggerService.Log("GM", opId, $"grant player={req.PlayerId} kind={req.Kind} amount={req.Amount} success={result.Success}");
        return Ok(new { success = result.Success, balanceAfter = result.BalanceAfter, reason = result.Reason });
    }
}
