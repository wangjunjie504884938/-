using System;
using System.Threading;
using System.Threading.Tasks;
using Arch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ArpgServer.Services;

/// <summary>
/// 节点健康检查后台循环 (规划阶段11) — 周期执行:
/// 1. NodeRegistry.CheckTimeouts: 心跳超时节点摘除;
/// 2. SessionManager.SweepExpired: Detached 超租约会话清理 + player:{id}:session 镜像删除 (规划4.2)。
/// 间隔: Arch:NodeProbeIntervalSeconds (默认15秒)。
/// </summary>
public class NodeHealthProbeService : BackgroundService
{
    private readonly NodeRegistryService _registry;
    private readonly SessionManager _sessions;
    private readonly RedisCacheService _redis;
    private readonly ILogger<NodeHealthProbeService> _logger;
    private readonly TimeSpan _interval;

    public NodeHealthProbeService(
        NodeRegistryService registry,
        SessionManager sessions,
        RedisCacheService redis,
        IConfiguration config,
        ILogger<NodeHealthProbeService> logger)
    {
        _registry = registry;
        _sessions = sessions;
        _redis = redis;
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
                foreach (var (_, playerId) in swept)
                {
                    try { await SessionRedisWriter.DeleteAsync(_redis, playerId); }
                    catch { /* 镜像删除失败不影响权威会话 */ }
                }
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
