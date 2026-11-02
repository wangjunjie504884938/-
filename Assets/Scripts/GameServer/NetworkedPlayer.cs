using UnityEngine;
using Mirror;
using Arch;
using ArpgShared;

namespace ArpgGameServer
{
    /// <summary>
    /// 网络玩家 — 处理移动/技能/血量同步
    /// 服务器权威,客户端发送命令,服务器广播状态
    /// 包含反作弊验证：技能冷却/距离/移动速度/伤害频率
    /// </summary>
    public class NetworkedPlayer : NetworkBehaviour
    {
        [SyncVar] public int playerId;
        [SyncVar] public string playerName = "Player";
        [SyncVar] public int classType;
        [SyncVar] public int level = 1;
        [SyncVar] public int currentHp = 100;
        [SyncVar] public int maxHp = 100;
        [SyncVar] public bool isDead = false;

        [SyncVar] public Vector2 serverPosition;
        [SyncVar] public Vector2 serverVelocity;

        [Header("战斗属性")]
        public int attack = 20;
        public float attackRange = 3f;
        public float attackCooldown = 0.5f;
        public float critChance = 0.08f;
        public float moveSpeed = 6f;

        [Header("战斗统计")]
        public int kills = 0;
        public int damageDealt = 0;
        public int damageTaken = 0;

        public GameRoom CurrentRoom;

        // ====== 反作弊追踪 ======
        private float _lastAttackTime;
        private float _lastMoveTime;
        private Vector2 _lastSentPosition;
        private Vector2 _lastValidPosition;

        // 反作弊计数器
        private int _speedViolationCount;
        private int _skillSpamCount;
        private const int MaxSpeedViolations = 5;     // 速度违规次数上限
        private const int MaxSkillSpamCount = 5;      // 技能连发次数上限
        private const float SpeedCheckWindow = 2f;    // 速度检查窗口(秒)
        private const float SkillSpamWindow = 1f;     // 技能连发窗口(秒)
        private bool _isKicked;

        public override void OnStartServer()
        {
            base.OnStartServer();
            serverPosition = transform.position;
            _lastValidPosition = serverPosition;
        }

        /// <summary>初始化玩家属性</summary>
        public void Initialize(int id, string name, int cls, int lv, int dungeonLevel)
        {
            playerId = id;
            playerName = name;
            classType = cls;
            level = lv;
            maxHp = 100 + lv * 15 + dungeonLevel * 10;
            currentHp = maxHp;
            attack = 8 + lv * 2 + dungeonLevel;

            switch (cls)
            {
                case 0: attackRange = 2f; attackCooldown = 0.5f; attack = Mathf.RoundToInt(attack * 0.9f); maxHp = Mathf.RoundToInt(maxHp * 1.3f); break;
                case 1: attackRange = 5f; attackCooldown = 0.8f; attack = Mathf.RoundToInt(attack * 1.3f); break;
                case 2: attackRange = 4f; attackCooldown = 0.7f; attack = Mathf.RoundToInt(attack * 0.8f); break;
            }
            currentHp = maxHp;
            moveSpeed = 6f; // 服务器权威移动速度
        }

        // ====== 反作弊：移动验证 ======

        /// <summary>服务器处理移动消息</summary>
        public void OnServerMove(MoveMessage msg)
        {
            if (isDead || _isKicked) return;

            float deltaTime = Mathf.Max(0.033f, (float)(System.DateTime.UtcNow - _lastMoveStateChanged).TotalSeconds);

            // 规划阶段05: 服务器权威移动验证 (速度/边界 — 纯逻辑抽至 ServerAuthority 可单测)
            var verdict = Arch.ServerAuthority.ValidateMove(_lastValidPosition.x, _lastValidPosition.y, msg.position.x, msg.position.y, moveSpeed, deltaTime);

            if (verdict == ServerAuthority.MoveVerdict.OutOfBounds)
            {
                Debug.LogWarning($"[AntiCheat] {playerName} 超出地图边界");
                return;
            }

            if (verdict == ServerAuthority.MoveVerdict.TooFast)
            {
                _speedViolationCount++;
                if (_speedViolationCount >= MaxSpeedViolations)
                {
                    Debug.LogWarning($"[AntiCheat] {playerName} 移动速度异常{MaxSpeedViolations}次，强制拉回");
                    serverPosition = _lastValidPosition;   // 强制拉回上次合法位置 (超速校正)
                    transform.position = serverPosition;
                    _speedViolationCount = 0;
                }
                return; // 未达上限时静默拒绝
            }

            // 通过验证
            _speedViolationCount = Mathf.Max(0, _speedViolationCount - 1);
            serverPosition = msg.position;
            serverVelocity = msg.velocity;
            _lastValidPosition = serverPosition;
            transform.position = serverPosition;
            // AOI 网格同步 (规划阶段07) — 玩家位置进空间索引
            CurrentRoom?.GridMove(playerId, serverPosition);
        }

        private System.DateTime _lastMoveStateChanged = System.DateTime.UtcNow;

        // ====== 反作弊：技能验证 ======

        /// <summary>服务器处理技能释放</summary>
        public void OnServerSkillCast(SkillCastMessage msg)
        {
            if (isDead || _isKicked) return;

            // 1. 冷却 + 目标 + 距离验证 — 规划阶段05 (逻辑抽至 ServerAuthority 可单测)
            NetworkedMonster targetMonster = null;
            float targetDist = float.MaxValue;

            if (CurrentRoom == null) return;

            foreach (var monster in CurrentRoom.Monsters)
            {
                if (monster == null || monster.IsDead) continue;
                var mState = monster.GetState();
                float dist = Vector2.Distance(serverPosition, mState.position);
                if (dist < targetDist)
                {
                    targetDist = dist;
                    targetMonster = monster;
                }
            }

            bool hasTarget = targetMonster != null;
            float sinceLast = Time.time - _lastAttackTime;
            var verdict = ServerAuthority.ValidateAttack(
                sinceLast, attackCooldown,
                hasTarget, hasTarget && !targetMonster.IsDead,
                targetDist, attackRange);

            if (verdict == ServerAuthority.AttackVerdict.OnCooldown)
            {
                _skillSpamCount++;
                if (_skillSpamCount >= MaxSkillSpamCount)
                {
                    Debug.LogWarning($"[AntiCheat] {playerName} 技能连发{MaxSkillSpamCount}次");
                    _skillSpamCount = 0;
                }
                return;
            }

            _lastAttackTime = Time.time;

            if (verdict != ServerAuthority.AttackVerdict.Ok)
            {
                Debug.Log($"[AntiCheat] {playerName} 技能判定未通过: {verdict} (可能是延迟)");
                return;
            }

            // 2. 视线检测 — 服务器 2D Raycast (墙体遮挡则无效; 规划阶段05: 攻击验证视线)
            if (!ArpgGameServer.ServerAuthorityLOS.HasLineOfSight(serverPosition, targetMonster.serverPosition))
            {
                Debug.LogWarning($"[AntiCheat] {playerName} 无视线攻击被拒绝 (墙后目标)");
                return;
            }

            // 3. 伤害计算 — 使用Shared/CombatCalculator统一公式（服务器权威）
            int damage = CombatCalculator.CalculateDamage(
                attack,           // 服务器存储的攻击力（不信任客户端）
                0,                // 怪物防御
                1f,               // 技能倍率
                critChance);      // 服务器存储的暴击率

            targetMonster.TakeDamage(damage, playerId);
            damageDealt += damage;
            Debug.Log($"[Combat] {playerName} → Monster #{targetMonster.monsterId}: {damage}");
        }

        /// <summary>玩家受到伤害</summary>
        public void TakeDamage(int damage, int attackerId)
        {
            if (isDead) return;

            // 伤害上限验证 — 规划阶段05: 单次伤害不超过最大HP的50% (逻辑抽至 ServerAuthority)
            damage = ServerAuthority.ClampDamage(damage, maxHp);

            currentHp -= damage;
            damageTaken += damage;
            if (currentHp <= 0)
            {
                currentHp = 0;
                isDead = true;
                Debug.Log($"[Combat] {playerName} 被击败!");
            }
        }

        /// <summary>获取当前状态快照</summary>
        public PlayerState GetState()
        {
            return new PlayerState
            {
                playerId = playerId,
                username = playerName,
                classType = classType,
                level = level,
                currentHp = currentHp,
                maxHp = maxHp,
                position = serverPosition,
                velocity = serverVelocity,
                gold = 0,
                xp = 0,
                attackCooldown = Mathf.Max(0, _lastAttackTime + attackCooldown - Time.time),
                isDead = isDead,
                timestamp = Time.time
            };
        }
    }
}
