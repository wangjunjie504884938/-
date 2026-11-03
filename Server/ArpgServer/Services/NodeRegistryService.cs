using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Arch;
using Microsoft.Extensions.Configuration;

namespace ArpgServer.Services;

/// <summary>
/// 游戏节点注册表 (规划阶段11 Docker动态开线) — 控制面在 WebAPI 进程内 (会话/分线的同一权威)。
/// 生命周期: 节点启动 → POST /node/register (绑定开线) → 每10秒 /node/heartbeat;
///           心跳超时 → 健康摘除 (线停收新玩家 + 该节点 Active 会话转 Detached, 3分钟租约内重连被重分配到健康节点);
///           心跳恢复 → 线恢复接收; 优雅关闭 → POST /node/deregister (注册表移除, 线下线)。
/// 节点宕机无法发 deregister — 由健康检查循环 (NodeHealthProbeService) 判定摘除, 节点尸体保留在列表供运维观察。
/// </summary>
public class NodeRegistryService
{
    public class GameNode
    {
        public string NodeId = "";
        public string Host = "";          // 客户端可直连的 Mirror 地址 (节点自报公网/内网IP)
        public int MirrorPort;            // Mirror 传输端口 (7777)
        public int HttpPort;              // 节点自有 HTTP 端口 (保留字段, 当前健康判定走心跳超时)
        public int Capacity;              // 单线容量 (= 节点 maxRooms)
        public int BuildVersion;          // 节点构建版本
        public int Load;                  // 节点自报负载 (当前房间数/连接数)
        public bool Accepting = true;
        public bool Healthy = true;
        public DateTime RegisteredAtUtc = DateTime.UtcNow;
        public DateTime LastHeartbeatUtc = DateTime.UtcNow;
        public int LineId;                // 绑定的分线 (首次注册时分配)
    }

    private readonly ConcurrentDictionary<string, GameNode> _nodes = new();
    private readonly ShardManager _shards;
    private readonly SessionManager _sessions;
    private readonly TimeSpan _heartbeatTimeout;

    public NodeRegistryService(ShardManager shards, SessionManager sessions, IConfiguration config)
    {
        _shards = shards;
        _sessions = sessions;
        _heartbeatTimeout = TimeSpan.FromSeconds(config.GetValue("Arch:NodeHeartbeatTimeoutSeconds", 45));
    }

    public IReadOnlyCollection<GameNode> Nodes => _nodes.Values.ToList().AsReadOnly();
    public GameNode Get(string nodeId) => _nodes.TryGetValue(nodeId, out var n) ? n : null;

    /// <summary>注册/重注册 (重注册沿用原线并刷新容量, 避免重启节点产生线泄漏)。返回绑定线Id。</summary>
    public int Register(string nodeId, string host, int mirrorPort, int httpPort, int capacity, int buildVersion)
    {
        var node = _nodes.AddOrUpdate(nodeId,
            _ => new GameNode
            {
                NodeId = nodeId,
                Host = host,
                MirrorPort = mirrorPort,
                HttpPort = httpPort,
                Capacity = capacity,
                BuildVersion = buildVersion,
            },
            (_, existing) =>
            {
                existing.Host = host;
                existing.MirrorPort = mirrorPort;
                existing.HttpPort = httpPort;
                existing.Capacity = capacity;
                existing.BuildVersion = buildVersion;
                existing.LastHeartbeatUtc = DateTime.UtcNow;
                return existing;
            });

        if (node.LineId == 0)
        {
            var line = _shards.AddLineForNode(nodeId, capacity, buildVersion);
            node.LineId = line.LineId;
            FileLoggerService.Log("NODE", nodeId, $"registered host={host} mirror={mirrorPort} capacity={capacity} version={buildVersion} line={line.LineId}");
        }
        else
        {
            var line = _shards.Get(node.LineId);
            if (line != null) { line.Capacity = capacity; line.BuildVersion = buildVersion; }
            FileLoggerService.Log("NODE", nodeId, $"re-registered line={node.LineId}");
        }

        Heartbeat(nodeId, node.Load, true);
        return node.LineId;
    }

    /// <summary>心跳: 刷新存活时间/负载/接收开关。摘除后的节点心跳恢复 → 线重新开放。</summary>
    public bool Heartbeat(string nodeId, int load, bool accepting)
    {
        if (!_nodes.TryGetValue(nodeId, out var node)) return false;
        node.Load = load;
        node.Accepting = accepting;
        node.LastHeartbeatUtc = DateTime.UtcNow;

        var wasDown = !node.Healthy;
        node.Healthy = true;
        _shards.SetHealth(node.LineId, healthy: true, accepting: accepting);
        if (wasDown)
            FileLoggerService.Log("NODE", nodeId, $"recovered via heartbeat line={node.LineId}");
        return true;
    }

    /// <summary>优雅注销 (节点正常关闭): 从注册表移除 + 线下线 + 该节点会话转 Detached。</summary>
    public bool Deregister(string nodeId)
    {
        if (!_nodes.TryRemove(nodeId, out var node)) return false;
        TakeLineDown(node, reason: "deregistered");
        return true;
    }

    /// <summary>
    /// 健康检查 (NodeHealthProbeService 周期调用 / NodeController probe 手动触发):
    /// 心跳超时节点 → 摘除 (尸体保留供运维观察)。
    /// 恢复唯一路径 = 节点真实心跳 (Heartbeat) — 此处不做自动恢复, 避免手动摘除被周期循环误恢复。
    /// 返回新摘除的节点Id列表。
    /// </summary>
    public List<string> CheckTimeouts(TimeSpan? overrideTimeout = null)
    {
        var timeout = overrideTimeout ?? _heartbeatTimeout;
        var now = DateTime.UtcNow;
        var unhealthy = new List<string>();

        foreach (var node in _nodes.Values)
        {
            if (node.Healthy && node.LastHeartbeatUtc + timeout < now)
            {
                node.Healthy = false;
                node.Accepting = false;
                TakeLineDown(node, reason: $"heartbeat timeout >{timeout.TotalSeconds}s");
                unhealthy.Add(node.NodeId);
            }
        }
        return unhealthy;
    }

    /// <summary>线下线 + 该节点所有 Active 会话转 Detached (重连租约内可被重分配 — 状态交接入口)</summary>
    private void TakeLineDown(GameNode node, string reason)
    {
        _shards.SetHealth(node.LineId, healthy: false, accepting: false);

        var detached = 0;
        foreach (var session in _sessions.All())
        {
            if (session.ServerNodeId == node.NodeId && session.State == SessionState.Active)
            {
                _sessions.Detach(session.SessionId);
                _shards.Leave(session.LineId); // 死线计数释放, 节点重注册后线从0开始
                detached++;
            }
        }
        FileLoggerService.Log("NODE", node.NodeId, $"line {node.LineId} down ({reason}), detachedSessions={detached}");
    }
}
