using System.Text.Json;
using Arch;

namespace ArpgServer.Services;

/// <summary>
/// 会话 Redis 写通 (规划4.2: player:{id}:session) — 会话摘要供运维查询与未来多API实例可见。
/// 权威仍在内存 SessionManager (阶段04); Redis 仅为镜像, TTL 15分钟 (覆盖3分钟重连租约+裕量)。
/// 写入点: 开局/重连/摘除(Detached); 删除点: 关闭/过期清扫。
/// </summary>
public static class SessionRedisWriter
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(15);

    private static string Key(long playerId) => $"player:{{{playerId}}}:session";

    public static async Task WriteAsync(RedisCacheService redis, GameSession s)
    {
        try
        {
            var db = redis.Database;
            if (db == null) return;
            var json = JsonSerializer.Serialize(new
            {
                s.SessionId,
                s.PlayerId,
                state = s.State.ToString(),
                s.LineId,
                s.ServerNodeId,
                s.FencingToken,
                leaseUntilUtc = s.LeaseUntilUtc,
            });
            await db.StringSetAsync(Key(s.PlayerId), json, Ttl);
        }
        catch { /* 镜像写失败不影响权威会话 */ }
    }

    public static async Task DeleteAsync(RedisCacheService redis, long playerId)
    {
        try
        {
            var db = redis.Database;
            if (db == null) return;
            await db.KeyDeleteAsync(Key(playerId));
        }
        catch { }
    }
}
