using System;
using System.Linq;
using System.Threading.Tasks;
using Arch;
using ArpgServer.Data;
using ArpgServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArpgServer.Controllers;

/// <summary>
/// 会话与分线 (规划阶段04) — LoginManager 编排的入口。
/// 客户端登录后调用 session/open 建立权威会话; 断线重连调用 session/reconnect。
/// 协议版本不兼容 → 426 拒绝并提示升级。
/// </summary>
[ApiController]
[Route("api")]
public class SessionController : ControllerBase
{
    public const int ProtocolVersion = 1;

    private readonly SessionManager _sessions;
    private readonly ShardManager _shards;
    private readonly NodeRegistryService _nodes;

    public SessionController(SessionManager sessions, ShardManager shards, NodeRegistryService nodes)
    {
        _sessions = sessions;
        _shards = shards;
        _nodes = nodes;
    }

    public class OpenSessionRequest
    {
        public long PlayerId { get; set; }
        public string ConnectionId { get; set; } = "";
        public int BuildVersion { get; set; } = ProtocolVersion;
    }

    /// <summary>建立会话 + 分线分配 (需JWT; 重复登录自动顶替旧会话)</summary>
    [Authorize]
    [HttpPost("session/open")]
    public IActionResult OpenSession([FromBody] OpenSessionRequest req)
    {
        if (req.BuildVersion != ProtocolVersion)
            return StatusCode(426, new { error = $"协议版本不兼容: 客户端{req.BuildVersion} / 服务器{ProtocolVersion}, 请更新游戏", serverVersion = ProtocolVersion });

        var line = _shards.AllocateLine(req.BuildVersion);
        if (line == null)
            return StatusCode(503, new { error = "所有分线已满或无版本兼容节点, 请稍后再试" });

        if (!_shards.TryJoin(line.LineId))
            return StatusCode(503, new { error = "分线加入失败, 请重试" });

        var userId = GetUserId();

        // 重复登录顶替: 旧会话占用的线容量先释放 (否则计数只涨不跌撑满线)
        var old = _sessions.GetByPlayer(userId);
        if (old != null) _shards.Leave(old.LineId);

        var session = _sessions.OpenSession(userId, Guid.NewGuid().ToString("N"), line.LineId, line.ServerNodeId ?? "local-node", req.BuildVersion);
        var node = line.ServerNodeId != null ? _nodes.Get(line.ServerNodeId) : null;
        return Ok(new
        {
            success = true,
            sessionId = session.SessionId,
            fencingToken = session.FencingToken,
            lineId = session.LineId,
            serverNodeId = session.ServerNodeId,
            nodeHost = node?.Host,       // 阶段11: 客户端按此直连节点Mirror (本地预分配线为空=用客户端默认地址)
            nodePort = node?.MirrorPort,
            protocolVersion = ProtocolVersion,
        });
    }

    /// <summary>
    /// 断线重连: Detached 租约内恢复 (fencing token 递增);
    /// 原线所属节点已摘除 (阶段11故障) → 重分配到健康线并返回新节点地址 — 状态交接入口,
    /// 客户端重连新节点 Mirror 后经地牢快照 (dungeon/load) 恢复进度。
    /// </summary>
    [Authorize]
    [HttpPost("session/reconnect")]
    public IActionResult Reconnect([FromBody] ReconnectRequest req)
    {
        // 预检: 原线健康性; 故障节点线先预占新线, 预占失败保持 Detached (503, 客户端稍后重试)
        var existing = _sessions.Get(req.SessionId);
        if (existing == null)
            return Unauthorized(new { error = "会话已过期, 请重新登录" });

        var oldLineId = existing.LineId;
        var oldLine = _shards.Get(oldLineId);
        var needMove = existing.State == SessionState.Detached && (oldLine == null || !oldLine.Healthy);
        LineData newLine = null;
        if (needMove)
        {
            newLine = _shards.AllocateLine(existing.BuildVersion);
            if (newLine == null)
                return StatusCode(503, new { error = "原节点已故障且暂无健康分线可接管, 请稍后重试" });
            if (!_shards.TryJoin(newLine.LineId))
                return StatusCode(503, new { error = "分线加入失败, 请重试" });
        }

        var session = _sessions.Reconnect(req.SessionId, Guid.NewGuid().ToString("N"));
        if (session == null)
        {
            if (newLine != null) _shards.Leave(newLine.LineId); // 归还预占容量
            return Unauthorized(new { error = "会话已过期, 请重新登录" });
        }

        if (needMove)
        {
            _shards.Leave(oldLineId); // 释放旧线容量 (旧节点线已摘除, 计数随之作废)
            session.LineId = newLine.LineId;
            session.ServerNodeId = newLine.ServerNodeId ?? session.ServerNodeId;
        }
        else
        {
            _shards.TryJoin(session.LineId); // 归还线容量计数 (原语义)
        }

        var node = session.ServerNodeId != null ? _nodes.Get(session.ServerNodeId) : null;
        return Ok(new
        {
            success = true,
            sessionId = session.SessionId,
            fencingToken = session.FencingToken,
            lineId = session.LineId,
            serverNodeId = session.ServerNodeId,
            nodeHost = node?.Host,
            nodePort = node?.MirrorPort,
            reassigned = needMove,
        });
    }

    public class ReconnectRequest
    {
        public string SessionId { get; set; } = "";
    }

    /// <summary>登出/断线通知: 会话关闭 + 分线容量释放 (仅Active占用者; 已被节点摘除释放的Detached会话不重复扣)</summary>
    [Authorize]
    [HttpPost("session/close")]
    public IActionResult Close([FromBody] CloseRequest req)
    {
        var s = _sessions.Get(req.SessionId);
        _sessions.Close(req.SessionId);
        if (s != null && s.State == SessionState.Active) _shards.Leave(s.LineId);
        return Ok(new { success = true });
    }

    public class CloseRequest
    {
        public string SessionId { get; set; } = "";
    }

    /// <summary>分线状态 (调试/运维: 在线人数与容量)</summary>
    [HttpGet("session/lines")]
    public IActionResult Lines()
    {
        return Ok(new
        {
            lines = _shards.Lines.Select(l => new
            {
                l.LineId,
                l.PlayerCount,
                l.Capacity,
                l.Healthy,
                l.Accepting,
            }),
        });
    }

    private int GetUserId()
    {
        // claim名必须与JwtService写入的一致 (ClaimTypes.NameIdentifier) — 之前找"UserId"/"sub"永远返回0,
        // 导致所有玩家会话都挂在playerId=0上互相顶替 (阶段11验收发现)
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim.Value, out var id) ? id : 0;
    }
}
