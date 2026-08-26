using UnityEngine;
using Mirror;
using ArpgShared;

namespace ArpgGameServer
{
    /// <summary>
    /// 网络怪物 — 服务器权威AI/血量/掉落
    /// SyncVar同步位置和血量给客户端
    /// </summary>
    public class NetworkedMonster : NetworkBehaviour
    {
        [SyncVar] public int monsterId;
        [SyncVar] public int monsterType; // 0=近战 1=远程 2=精英 3=Boss
        [SyncVar] public int currentHp;
        [SyncVar] public int maxHp;
        [SyncVar] public Vector2 serverPosition;
        [SyncVar] public bool isDead = false;
        [SyncVar] public int attackTargetId = -1;

        public bool IsDead => isDead;

        private int _attackDamage;
        private float _moveSpeed = 2f;
        private float _attackRange = 2f;
        private float _attackCooldown = 1.5f;
        private float _lastAttackTime;
        private int _dungeonLevel;
        private float _aiThinkTimer;
        private static readonly Collider2D[] _playerBuffer = new Collider2D[8];
        private static int _playerLayerMask = -1;

        /// <summary>初始化怪物</summary>
        public void Initialize(int id, int type, int hp, Vector2 pos, int dungeonLevel)
        {
            monsterId = id;
            monsterType = type;
            maxHp = hp;
            currentHp = hp;
            serverPosition = pos;
            _dungeonLevel = dungeonLevel;
            transform.position = pos;

            _attackDamage = Mathf.RoundToInt((5 + dungeonLevel) * (type == 3 ? 1.5f : type == 2 ? 1.2f : 1f));
            _moveSpeed = type switch { 0 => 2.5f, 1 => 1.5f, 2 => 2.8f, 3 => 1.8f, _ => 2f };
            _attackRange = type switch { 0 => 2f, 1 => 5f, 2 => 2.5f, 3 => 3f, _ => 2f };
            _attackCooldown = type switch { 0 => 1.5f, 1 => 2f, 2 => 1.2f, 3 => 0.8f, _ => 1.5f };

            if (_playerLayerMask == -1)
                _playerLayerMask = LayerMask.GetMask("Player");
        }

        private void Update()
        {
            if (isDead) return;
            UpdateAI();
        }

        /// <summary>怪物AI — 追击/攻击玩家</summary>
        private void UpdateAI()
        {
            _aiThinkTimer -= Time.deltaTime;
            if (_aiThinkTimer > 0) return;
            _aiThinkTimer = 0.2f; // 5次/秒

            // 查找最近的玩家
            NetworkedPlayer nearestPlayer = null;
            float nearestDist = float.MaxValue;

            if (CurrentRoom_GameRoom != null)
            {
                foreach (var p in CurrentRoom_GameRoom.Players)
                {
                    if (p == null || p.isDead) continue;
                    float d = Vector2.Distance(serverPosition, p.serverPosition);
                    if (d < nearestDist)
                    {
                        nearestDist = d;
                        nearestPlayer = p;
                    }
                }
            }

            if (nearestPlayer == null) return;

            attackTargetId = nearestPlayer.playerId;

            // 追击
            if (nearestDist > _attackRange)
            {
                Vector2 dir = (nearestPlayer.serverPosition - serverPosition).normalized;
                serverPosition += dir * _moveSpeed * Time.deltaTime * 5f; // 加速追赶
                transform.position = serverPosition;
            }
            // 攻击
            else if (Time.time - _lastAttackTime > _attackCooldown)
            {
                _lastAttackTime = Time.time;
                int damage = Mathf.Max(1, _attackDamage - Random.Range(0, 3));
                nearestPlayer.TakeDamage(damage, -1);
                Debug.Log($"[Combat] Monster #{monsterId} → {nearestPlayer.playerName}: {damage}");
            }
        }

        public GameRoom CurrentRoom_GameRoom;

        /// <summary>怪物受到伤害</summary>
        public void TakeDamage(int damage, int attackerId)
        {
            if (isDead) return;
            currentHp -= damage;
            if (currentHp <= 0)
            {
                currentHp = 0;
                isDead = true;
                Debug.Log($"[Combat] Monster #{monsterId} 被击败! (attacker={attackerId})");

                // 如果是Boss,触发结算
                if (monsterType == 3 && CurrentRoom_GameRoom != null && !CurrentRoom_GameRoom.IsSettled)
                {
                    CurrentRoom_GameRoom.IsSettled = true;
                    StartCoroutine(SettlementGenerator.Instance.GenerateAndPush(CurrentRoom_GameRoom));
                }
            }
        }

        /// <summary>获取状态快照</summary>
        public MonsterState GetState()
        {
            return new MonsterState
            {
                monsterId = monsterId,
                monsterType = monsterType,
                currentHp = currentHp,
                maxHp = maxHp,
                position = serverPosition,
                velocity = Vector2.zero,
                attackTarget = attackTargetId,
                isDead = isDead,
                timestamp = Time.time
            };
        }
    }
}
