using System;
using System.Linq;
using ArpgServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace ArpgServer.Controllers;

/// <summary>
/// 游戏节点管理 (规划阶段11 Docker动态开线) — 节点自注册/心跳/注销 + 运维查询。
/// 认证: register/heartbeat/deregister 用 X-Node-Key 共享密钥 (节点密钥, 非玩家JWT);
///       node/probe 用 X-GM-Key (运维手动触发超时检查, 与 GmController 同密钥)。
/// 密钥配置: appsettings "Node:Key" / "Gm:Key", 默认 dev 值; 生产必须改。
/// </summary>
[ApiController]
[Route("api")]
public class NodeController : ControllerBase
{
    private readonly NodeRegistryService _registry;
    private readonly IConfiguration _config;

    public NodeController(NodeRegistryService registry, IConfiguration config)
    {
        _registry = registry;
        _config = config;
    }

    private bool IsNode => Request.Headers.TryGetValue("X-Node-Key", out var v)
        && v.ToString() == (_config["Node:Key"] ?? "dev-node-key");

    private bool IsGm => Request.Headers.TryGetValue("X-GM-Key", out var v)
        && v.ToString() == (_config["Gm:Key"] ?? "dev-gm-key");

    public class RegisterRequest
    {
        public string NodeId { get; set; } = "";
        public string Host { get; set; } = "";      // 客户端可直连地址
        public int MirrorPort { get; set; } = 7777;
        public int HttpPort { get; set; }
        public int Capacity { get; set; } = ShardManager.DefaultLineCapacity;
        public int BuildVersion { get; set; } = 1;
    }

    /// <summary>节点注册 (启动时; 重注册沿用原线)。响应携带绑定线Id。</summary>
    [HttpPost("node/register")]
    public IActionResult Register([FromBody] RegisterRequest req)
    {
        if (!IsNode) return Unauthorized(new { error = "Node key required" });
        if (string.IsNullOrWhiteSpace(req.NodeId) || string.IsNullOrWhiteSpace(req.Host))
            return BadRequest(new { error = "nodeId/host 必填" });

        var lineId = _registry.Register(req.NodeId, req.Host, req.MirrorPort, req.HttpPort, req.Capacity, req.BuildVersion);
        return Ok(new { success = true, nodeId = req.NodeId, lineId, capacity = req.Capacity });
    }

    public class HeartbeatRequest
    {
        public string NodeId { get; set; } = "";
        public int Load { get; set; }               // 当前负载 (房间数)
        public bool Accepting { get; set; } = true; // 节点自报是否可接新玩家
    }

    /// <summary>节点心跳 (每10秒; 超时摘除后恢复心跳会重新开放线)</summary>
    [HttpPost("node/heartbeat")]
    public IActionResult Heartbeat([FromBody] HeartbeatRequest req)
    {
        if (!IsNode) return Unauthorized(new { error = "Node key required" });
        var ok = _registry.Heartbeat(req.NodeId, req.Load, req.Accepting);
        if (!ok) return NotFound(new { error = "节点未注册, 请重新 register" });
        var node = _registry.Get(req.NodeId)!;
        return Ok(new { success = true, healthy = node.Healthy, accepting = node.Accepting, lineId = node.LineId });
    }

    public class DeregisterRequest
    {
        public string NodeId { get; set; } = "";
    }

    /// <summary>节点优雅注销 (正常关闭时; 宕机场景由健康检查循环摘除)</summary>
    [HttpPost("node/deregister")]
    public IActionResult Deregister([FromBody] DeregisterRequest req)
    {
        if (!IsNode) return Unauthorized(new { error = "Node key required" });
        var ok = _registry.Deregister(req.NodeId);
        return Ok(new { success = ok });
    }

    /// <summary>节点列表 (运维/调试: 注册表全量, 含摘除尸体)</summary>
    [HttpGet("node/list")]
    public IActionResult List()
    {
        return Ok(new
        {
            nodes = _registry.Nodes.Select(n => new
            {
                n.NodeId,
                n.Host,
                n.MirrorPort,
                n.Capacity,
                n.Load,
                n.BuildVersion,
                n.Healthy,
                n.Accepting,
                n.LineId,
                lastHeartbeatUtc = n.LastHeartbeatUtc,
                registeredAtUtc = n.RegisteredAtUtc,
            }),
        });
    }

    public class ProbeRequest
    {
        public int? TimeoutSeconds { get; set; }   // 覆盖默认超时 (验收测试用小值模拟节点死亡)
    }

    /// <summary>手动触发健康检查 (运维/验收: 心跳超时摘除, 不等待循环周期)。恢复只能由节点真实心跳触发。</summary>
    [HttpPost("node/probe")]
    public IActionResult Probe([FromBody] ProbeRequest req)
    {
        if (!IsGm) return Unauthorized(new { error = "GM key required" });
        var unhealthy = _registry.CheckTimeouts(req?.TimeoutSeconds != null ? TimeSpan.FromSeconds(req.TimeoutSeconds.Value) : null);
        return Ok(new { success = true, unhealthy });
    }
}
