using UnityEngine;
using Mirror;
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

            float dist = Vector2.Distance(serverPosition, msg.position);

            // 1. 速度验证 — 单帧移动距离不超过 速度×时间×容差
            float deltaTime = Mathf.Max(0.05f, (float)(System.DateTime.UtcNow - _lastMoveStateChanged).TotalSeconds);
            float maxMoveDist = moveSpeed * deltaTime + 2f; // 2f为网络延迟容差

            if (dist > maxMoveDist)
            {
                _speedViolationCount++;

                if (_speedViolationCount >= MaxSpeedViolations)
                {
                    Debug.LogWarning($"[AntiCheat] {playerName} 移动速度异常{MaxSpeedViolations}次，强制拉回");
                    // 强制拉回上次合法位置
                    serverPosition = _lastValidPosition;
                    transform.position = serverPosition;
                    _speedViolationCount = 0;
                }
                // 未达上限时静默拒绝（不发到当前位置）
                return;
            }

            // 2. 地图边界验证
            if (Mathf.Abs(msg.position.x) > 50f || Mathf.Abs(msg.position.y) > 50f)
            {
                Debug.LogWarning($"[AntiCheat] {playerName} 超出地图边界");
                return;
            }

            // 通过验证
            _speedViolationCount = Mathf.Max(0, _speedViolationCount - 1);
            serverPosition = msg.position;
            serverVelocity = msg.velocity;
            _lastValidPosition = serverPosition;
            transform.position = serverPosition;
        }

        private System.DateTime _lastMoveStateChanged = System.DateTime.UtcNow;

        // ====== 反作弊：技能验证 ======

        /// <summary>服务器处理技能释放</summary>
        public void OnServerSkillCast(SkillCastMessage msg)
        {
            if (isDead || _isKicked) return;

            // 1. 冷却验证 — 攻击间隔不足则拒绝
            if (Time.time - _lastAttackTime < attackCooldown * 0.9f) // 10%容差
            {
                _skillSpamCount++;
                if (_skillSpamCount >= MaxSkillSpamCount)
                {
                    Debug.LogWarning($"[AntiCheat] {playerName} 技能连发{MaxSkillSpamCount}次");
                    _skillSpamCount = 0;
                }
                return;
            }

            // 技能连发计数衰减
            if (Time.time - _lastAttackTime > SkillSpamWindow)
                _skillSpamCount = 0;

            _lastAttackTime = Time.time;

            // 2. 距离验证 — 技能目标必须在攻击范围内
            if (CurrentRoom == null) return;

            bool hitAny = false;
            foreach (var monster in CurrentRoom.Monsters)
            {
                if (monster == null || monster.IsDead) continue;
                var mState = monster.GetState();
                float dist = Vector2.Distance(serverPosition, mState.position);

                // 攻击距离 + 容差1f（网络延迟）
                if (dist <= attackRange + 1f)
                {
                    hitAny = true;

                    // 3. 伤害计算 — 使用Shared/CombatCalculator统一公式（服务器权威）
                    int damage = CombatCalculator.CalculateDamage(
                        attack,           // 服务器存储的攻击力（不信任客户端）
                        0,                // 怪物防御
                        1f,               // 技能倍率
                        critChance);      // 服务器存储的暴击率

                    monster.TakeDamage(damage, playerId);
                    damageDealt += damage;
                    Debug.Log($"[Combat] {playerName} → Monster #{monster.monsterId}: {damage}");
                    break;
                }
            }

            // 4. 无效目标 — 攻击范围内没有怪物但客户端声称释放了技能
            if (!hitAny)
            {
                // 不造成伤害但也不惩罚（可能是网络延迟导致位置偏差）
                Debug.Log($"[AntiCheat] {playerName} 技能无目标（可能是延迟）");
            }
        }

        /// <summary>玩家受到伤害</summary>
        public void TakeDamage(int damage, int attackerId)
        {
            if (isDead) return;

            // 伤害上限验证 — 单次伤害不超过最大HP的50%
            int maxSingleDamage = Mathf.RoundToInt(maxHp * 0.5f);
            if (damage > maxSingleDamage)
            {
                Debug.LogWarning($"[AntiCheat] {playerName} 受到异常伤害 {damage} (上限{maxSingleDamage})，截断");
                damage = maxSingleDamage;
            }

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
