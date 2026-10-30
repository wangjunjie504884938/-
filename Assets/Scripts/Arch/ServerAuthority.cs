using System;
using System.Collections.Concurrent;

namespace Arch
{
    /// <summary>
    /// 服务器权威验证器 — 纯数学逻辑 (无Unity依赖), 客户端/服务器/harness 三端共享同一份源码。
    /// Unity 专属部分 (视线Raycast) 在 GameServer/ServerAuthority.cs。
    /// </summary>
    public static class ServerAuthority
    {
        /// <summary>单帧最大速度容差倍数 (网络延迟容差)</summary>
        public const float MoveToleranceMultiplier = 1.5f;
        public const float MoveBaseTolerance = 0.5f;      // 基础容差(米)
        public const float AttackRangeTolerance = 1f;     // 攻击距离容差(米, 网络延迟)
        public const float MaxSingleDamageHpPct = 0.5f;   // 单次伤害上限 = 最大HP 50%

        public enum MoveVerdict { Ok, TooFast, OutOfBounds }

        /// <summary>移动验证: 单帧位移 ≤ 速度×Δt×容差 + 基础容差; 地图边界校验 (lx/ly=上次合法位置, tx/ty=目标位置)</summary>
        public static MoveVerdict ValidateMove(
            float lastX, float lastY, float targetX, float targetY, float moveSpeed, float deltaTime,
            float mapHalfX = 50f, float mapHalfY = 50f)
        {
            if (Math.Abs(targetX) > mapHalfX || Math.Abs(targetY) > mapHalfY)
                return MoveVerdict.OutOfBounds;

            float dx = targetX - lastX, dy = targetY - lastY;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);
            float maxDist = moveSpeed * Math.Max(0.033f, deltaTime) * MoveToleranceMultiplier + MoveBaseTolerance;
            return dist <= maxDist ? MoveVerdict.Ok : MoveVerdict.TooFast;
        }

        public enum AttackVerdict { Ok, OnCooldown, TargetTooFar, TargetDead, NoTarget }

        /// <summary>攻击验证: 冷却/目标存活/距离(含容差)。视线检测由调用方在服务器上做 Raycast (需要场景碰撞体)。</summary>
        public static AttackVerdict ValidateAttack(
            float timeSinceLastAttack, float attackCooldown,
            bool hasTarget, bool targetAlive, float distanceToTarget, float attackRange)
        {
            if (timeSinceLastAttack < attackCooldown * 0.9f) // 10%网络容差
                return AttackVerdict.OnCooldown;
            if (!hasTarget || !targetAlive)
                return AttackVerdict.TargetDead;
            if (distanceToTarget > attackRange + AttackRangeTolerance)
                return AttackVerdict.TargetTooFar;
            return AttackVerdict.Ok;
        }

        /// <summary>伤害截断: 单次伤害不超过最大HP的50% (防异常数值)</summary>
        public static int ClampDamage(int damage, int maxHp)
        {
            int max = (int)Math.Round(maxHp * MaxSingleDamageHpPct);
            return Math.Clamp(damage, 0, Math.Max(1, max));
        }
    }

    /// <summary>
    /// RPC 限流器 (规划阶段05) — 每连接每消息类型滑动窗口限频 + 违规计分 + 断开决策。纯 C# 可单测。
    /// </summary>
    public class RpcRateLimiter
    {
        private readonly double _windowSeconds;
        private readonly int _maxPerWindow;
        private readonly int _maxViolations;

        private readonly double[] _timestamps;
        private int _head;
        private int _violations;

        public int ViolationScore => _violations;
        public bool ShouldDisconnect => _violations >= _maxViolations;

        public RpcRateLimiter(double windowSeconds, int maxPerWindow, int maxViolations)
        {
            _windowSeconds = windowSeconds;
            _maxPerWindow = Math.Max(1, maxPerWindow);
            _maxViolations = Math.Max(1, maxViolations);
            _timestamps = new double[_maxPerWindow + 1]; // +1: 允许溢出插入一条以触发违规检测
        }

        /// <summary>
        /// 记录一条消息: 0=通过 1=超频(计分+1) 2=达到断开阈值。
        /// 语义: 每条消息(含被拒的)都插入时间窗 — 窗口内消息数 &gt; 上限 即违规; 持续超频持续计分直至断开。
        /// </summary>
        public int OnMessage(double nowSeconds)
        {
            // 移出窗口外的旧时间戳
            while (_head > 0 && nowSeconds - _timestamps[0] > _windowSeconds)
            {
                for (int i = 0; i < _head - 1; i++) _timestamps[i] = _timestamps[i + 1];
                _head--;
            }

            // 插入当前消息 (窗口满时挤掉最旧)
            int pos = _head;
            if (pos < _timestamps.Length)
            {
                while (pos > 0 && _timestamps[pos - 1] > nowSeconds)
                {
                    _timestamps[pos] = _timestamps[pos - 1];
                    pos--;
                }
                _timestamps[pos] = nowSeconds;
                _head++;
            }
            else
            {
                for (int i = 0; i < _head - 1; i++) _timestamps[i] = _timestamps[i + 1];
                _timestamps[_head - 1] = nowSeconds;
            }

            // 窗口内消息数超过上限 → 违规计分
            if (_head > _maxPerWindow)
            {
                _violations++;
                return _violations >= _maxViolations ? 2 : 1;
            }
            return 0;
        }

        public void Reset() => _head = 0;
    }

    /// <summary>每连接限流注册表 — 按 (connectionId, msgType) 索引; 连接断开时清理防泄漏。</summary>
    public class RpcRateLimiterRegistry
    {
        private class Key : IEquatable<Key>
        {
            public readonly int ConnectionId;
            public readonly string MsgType;
            public Key(int c, string m) { ConnectionId = c; MsgType = m; }
            public bool Equals(Key other) => other != null && ConnectionId == other.ConnectionId && MsgType == other.MsgType;
            public override bool Equals(object o) => o is Key k && Equals(k);
            public override int GetHashCode() => HashCode.Combine(ConnectionId, MsgType);
        }

        private readonly ConcurrentDictionary<Key, RpcRateLimiter> _limiters = new();
        private readonly double _windowSeconds;
        private readonly int _maxPerWindow;
        private readonly int _maxViolations;

        public RpcRateLimiterRegistry(double windowSeconds, int maxPerWindow, int maxViolations)
        {
            _windowSeconds = windowSeconds;
            _maxPerWindow = maxPerWindow;
            _maxViolations = maxViolations;
        }

        public int OnMessage(int connectionId, string msgType, double nowSeconds)
        {
            var limiter = _limiters.GetOrAdd(new Key(connectionId, msgType),
                _ => new RpcRateLimiter(_windowSeconds, _maxPerWindow, _maxViolations));
            return limiter.OnMessage(nowSeconds);
        }

        public void RemoveConnection(int connectionId)
        {
            foreach (var key in _limiters.Keys)
                if (key.ConnectionId == connectionId)
                    _limiters.TryRemove(key, out _);
        }
    }
}
