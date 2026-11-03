using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Arch
{
    /// <summary>
    /// 会话状态机 (规划阶段04) — SessionId/租约/重连期限/重复登录防双会话。
    /// 状态流转: Connecting → Active → (断线) Detached(重连期限内) → Expired
    ///           Detached 期间重连 → Active (同一 SessionId, ConnectionId 更新)
    /// 重复登录: 旧会话被新登录顶替 (旧 SessionId 立即 Expired, fencing token 递增使旧节点写入失效)
    /// </summary>
    public class GameSession
    {
        public string SessionId;          // guid
        public long PlayerId;
        public string ConnectionId;       // 当前连接 (重连时更新)
        public int LineId;                // 分线归属
        public string ServerNodeId;       // 节点归属 (阶段11 Docker)
        public int BuildVersion;          // 协议/构建版本 (不兼容拒绝)
        public SessionState State;
        public long FencingToken;         // 每次顶替/复活递增, 旧 token 的写入被拒绝
        public DateTime LeaseUntilUtc;    // Detached 状态的租约期限 (重连期限内可回来)
        public DateTime CreatedAtUtc = DateTime.UtcNow;
    }

    public enum SessionState { Connecting, Active, Detached, Expired }

    /// <summary>
    /// 会话管理器 (服务器单例) — 唯一权威会话归属。
    /// 线程安全: ConcurrentDictionary + 每会话 fencing token。
    /// </summary>
    public class SessionManager
    {
        public static readonly TimeSpan ReconnectLease = TimeSpan.FromMinutes(3);

        private readonly ConcurrentDictionary<string, GameSession> _sessions = new();
        private readonly ConcurrentDictionary<long, string> _playerIndex = new(); // playerId → 当前 sessionId

        public int ActiveCount => _sessions.Values.Count(s => s.State == SessionState.Active);

        /// <summary>建立/顶替会话。重复登录: 旧会话 Expired, fencing token 单调递增继承 (旧写入永久失效)。</summary>
        public GameSession OpenSession(long playerId, string connectionId, int lineId, string serverNodeId, int buildVersion)
        {
            // 重复登录: 顶替旧会话 — fencing token 必须单调递增, 保证旧会话的写入永远无法通过校验
            long nextToken = 1;
            if (_playerIndex.TryGetValue(playerId, out var oldId) && _sessions.TryGetValue(oldId, out var old))
            {
                old.State = SessionState.Expired;
                nextToken = old.FencingToken + 1;
                _sessions.TryRemove(oldId, out _);
            }

            var session = new GameSession
            {
                SessionId = Guid.NewGuid().ToString("N"),
                PlayerId = playerId,
                ConnectionId = connectionId,
                LineId = lineId,
                ServerNodeId = serverNodeId,
                BuildVersion = buildVersion,
                State = SessionState.Active,
                FencingToken = nextToken,
            };
            _sessions[session.SessionId] = session;
            _playerIndex[playerId] = session.SessionId;
            return session;
        }

        /// <summary>断线: 进入 Detached (重连期限内保留, 队友继续战斗 — 用户决策)</summary>
        public GameSession Detach(string sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var s)) return null;
            if (s.State != SessionState.Active) return null;
            s.State = SessionState.Detached;
            s.LeaseUntilUtc = DateTime.UtcNow + ReconnectLease;
            return s;
        }

        /// <summary>重连: Detached → Active (租约内); 过期/不存在 → null (调用方拒绝)</summary>
        public GameSession Reconnect(string sessionId, string newConnectionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var s)) return null;
            if (s.State != SessionState.Detached || s.LeaseUntilUtc < DateTime.UtcNow)
            {
                s.State = SessionState.Expired;
                return null;
            }
            s.State = SessionState.Active;
            s.ConnectionId = newConnectionId;
            s.FencingToken++;
            return s;
        }

        /// <summary>登出/被踢/过期清理</summary>
        public void Close(string sessionId)
        {
            if (_sessions.TryGetValue(sessionId, out var s))
            {
                s.State = SessionState.Expired;
                _playerIndex.TryRemove(s.PlayerId, out _);
                _sessions.TryRemove(sessionId, out _);
            }
        }

        /// <summary>校验写入合法性: 会话必须 Active 且 fencing token 匹配 (防旧节点/旧会话继续写入)</summary>
        public bool IsWriteAllowed(string sessionId, long fencingToken)
        {
            return _sessions.TryGetValue(sessionId, out var s)
                && s.State == SessionState.Active
                && s.FencingToken == fencingToken;
        }

        public GameSession Get(string sessionId) => _sessions.TryGetValue(sessionId, out var s) ? s : null;

        /// <summary>按玩家查当前会话 (重复登录顶替时释放旧线容量用)</summary>
        public GameSession GetByPlayer(long playerId)
            => _playerIndex.TryGetValue(playerId, out var sid) && _sessions.TryGetValue(sid, out var s) ? s : null;

        /// <summary>全量会话快照 (阶段11节点摘除时遍历归属会话)</summary>
        public IEnumerable<GameSession> All() => _sessions.Values;

        /// <summary>过期清理 (心跳/定时调用): Detached 超过租约期限的会话 Expired; 返回(会话Id, 玩家Id)供镜像键清理</summary>
        public List<(string SessionId, long PlayerId)> SweepExpired()
        {
            var expired = new List<(string SessionId, long PlayerId)>();
            var now = DateTime.UtcNow;
            foreach (var kv in _sessions)
            {
                if (kv.Value.State == SessionState.Detached && kv.Value.LeaseUntilUtc < now)
                {
                    kv.Value.State = SessionState.Expired;
                    _playerIndex.TryRemove(kv.Value.PlayerId, out _);
                    _sessions.TryRemove(kv.Key, out _);
                    expired.Add((kv.Key, kv.Value.PlayerId));
                }
            }
            return expired;
        }
    }
}
