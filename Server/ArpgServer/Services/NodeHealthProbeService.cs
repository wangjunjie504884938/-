using System;
using System.Threading;
using System.Threading.Tasks;
using Arch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ArpgServer.Services;

/// <summary>
/// 节点健康检查后台循环 (规划阶段11) — 周期执行:
/// 1. NodeRegistry.CheckTimeouts: 心跳超时节点摘除/恢复;
/// 2. SessionManager.SweepExpired: Detached 超租约会话清理 (阶段04遗留的定期清扫接线点)。
/// 间隔: Arch:NodeProbeIntervalSeconds (默认15秒)。
/// </summary>
public class NodeHealthProbeService : BackgroundService
{
    private readonly NodeRegistryService _registry;
    private readonly SessionManager _sessions;
    private readonly ILogger<NodeHealthProbeService> _logger;
    private readonly TimeSpan _interval;

    public NodeHealthProbeService(
        NodeRegistryService registry,
        SessionManager sessions,
        IConfiguration config,
        ILogger<NodeHealthProbeService> logger)
    {
        _registry = registry;
        _sessions = sessions;
        _logger = logger;
        _interval = TimeSpan.FromSeconds(config.GetValue("Arch:NodeProbeIntervalSeconds", 15));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("节点健康检查循环启动, 间隔{Interval}s", _interval.TotalSeconds);
        using var timer = new PeriodicTimer(_interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var unhealthy = _registry.CheckTimeouts();
                foreach (var id in unhealthy)
                    _logger.LogWarning("节点{NodeId}心跳超时摘除", id);

                var swept = _sessions.SweepExpired();
                if (swept.Count > 0)
                    _logger.LogInformation("清理过期会话{Count}个", swept.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "健康检查循环异常");
            }
        }
    }
}
