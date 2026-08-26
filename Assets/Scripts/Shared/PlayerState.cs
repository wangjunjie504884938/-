using UnityEngine;

namespace ArpgShared
{
    /// <summary>玩家状态 — 客户端和服务器共享</summary>
    [System.Serializable]
    public struct PlayerState
    {
        public int playerId;
        public string username;
        public int classType;
        public int level;
        public int currentHp;
        public int maxHp;
        public Vector2 position;
        public Vector2 velocity;
        public int gold;
        public int xp;
        public float attackCooldown;
        public bool isDead;
        public float timestamp;

        public PlayerState(int id, string name, int cls, int lv, int hp, int maxH, Vector2 pos)
        {
            playerId = id; username = name; classType = cls; level = lv;
            currentHp = hp; maxHp = maxH; position = pos; velocity = Vector2.zero;
            gold = 0; xp = 0; attackCooldown = 0; isDead = false; timestamp = 0;
        }
    }
}
