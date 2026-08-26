using UnityEngine;
using Mirror;
using ArpgShared;

namespace ArpgGameServer
{
    /// <summary>
    /// 网络玩家 — 处理移动/技能/血量同步
    /// 服务器权威,客户端发送命令,服务器广播状态
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

        private float _lastAttackTime;
        private float _lastMoveTime;
        private Vector2 _lastSentPosition;

        public override void OnStartServer()
        {
            base.OnStartServer();
            serverPosition = transform.position;
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
        }

        /// <summary>服务器处理移动消息</summary>
        public void OnServerMove(MoveMessage msg)
        {
            if (isDead) return;

            // 服务器权威: 校验移动距离(防作弊)
            float maxMoveDist = moveSpeed * 0.2f + 1f; // 允许一定误差
            float dist = Vector2.Distance(serverPosition, msg.position);
            if (dist > maxMoveDist)
            {
                // 移动距离异常,拉回上次位置
                return;
            }

            serverPosition = msg.position;
            serverVelocity = msg.velocity;
            transform.position = serverPosition;
        }

        /// <summary>服务器处理技能释放</summary>
        public void OnServerSkillCast(SkillCastMessage msg)
        {
            if (isDead || Time.time - _lastAttackTime < attackCooldown) return;
            _lastAttackTime = Time.time;

            // 查找攻击范围内的敌人
            if (CurrentRoom == null) return;

            foreach (var monster in CurrentRoom.Monsters)
            {
                if (monster == null || monster.IsDead) continue;
                float dist = Vector2.Distance(serverPosition, monster.GetState().position);
                if (dist <= attackRange + 1f)
                {
                    int damage = CombatCalculator.CalculateDamage(attack, 0, 1f, critChance);
                    monster.TakeDamage(damage, playerId);
                    damageDealt += damage;
                    Debug.Log($"[Combat] {playerName} → Monster #{monster.monsterId}: {damage}");
                    break;
                }
            }
        }

        /// <summary>玩家受到伤害</summary>
        public void TakeDamage(int damage, int attackerId)
        {
            if (isDead) return;
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
