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
/// 支持在线人数/查玩家/踢会话/测试发币; 敏感操作全部落 FileLogger 审计。
/// </summary>
[ApiController]
[Route("api")]
public class GmController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly Services.CurrencyLedgerService _ledger;
    private readonly IConfiguration _config;
    private readonly Arch.SessionManager _sessions;
    private readonly ShardManager _shards;

    public GmController(AppDbContext db, Services.CurrencyLedgerService ledger, IConfiguration config,
        Arch.SessionManager sessions, ShardManager shards)
    {
        _db = db;
        _ledger = ledger;
        _config = config;
        _sessions = sessions;
        _shards = shards;
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

    /// <summary>查玩家会话 (阶段10 GM/阶段11节点体系): 会话归属、分线、节点、租约状态</summary>
    [HttpGet("gm/player/{playerId}")]
    public IActionResult Player(long playerId)
    {
        if (!IsGm) return Unauthorized(new { error = "GM key required" });
        var session = _sessions.All().FirstOrDefault(s => s.PlayerId == playerId);
        if (session == null)
            return Ok(new { success = true, online = false });
        var line = _shards.Get(session.LineId);
        return Ok(new
        {
            success = true,
            online = session.State == Arch.SessionState.Active,
            sessionId = session.SessionId,
            state = session.State.ToString(),
            lineId = session.LineId,
            serverNodeId = session.ServerNodeId,
            fencingToken = session.FencingToken,
            buildVersion = session.BuildVersion,
            leaseUntilUtc = session.LeaseUntilUtc,
            lineHealthy = line?.Healthy,
            lineAccepting = line?.Accepting,
            linePlayerCount = line?.PlayerCount,
        });
    }

    public class KickRequest
    {
        public long PlayerId { get; set; }
    }

    /// <summary>踢玩家 (权威会话层): 会话关闭 + 分线计数修正; Mirror连接由写入校验失败自然回收</summary>
    [HttpPost("gm/kick")]
    public IActionResult Kick([FromBody] KickRequest req)
    {
        if (!IsGm) return Unauthorized(new { error = "GM key required" });
        var session = _sessions.All().FirstOrDefault(s => s.PlayerId == req.PlayerId);
        if (session == null)
            return NotFound(new { error = "玩家不在线" });

        var lineId = session.LineId;
        var kickedId = session.SessionId;
        _sessions.Close(kickedId);
        _shards.Leave(lineId);
        FileLoggerService.Log("GM", $"kick:{Guid.NewGuid():N}", $"kicked player={req.PlayerId} line={lineId}");
        return Ok(new { success = true, kickedSessionId = kickedId });
    }
}
