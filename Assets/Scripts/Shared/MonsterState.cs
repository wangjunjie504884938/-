using UnityEngine;
using System.Collections.Generic;

namespace ArpgShared
{
    /// <summary>怪物状态 — 客户端和服务器共享</summary>
    [System.Serializable]
    public struct MonsterState
    {
        public int monsterId;
        public int monsterType;     // 0=近战 1=远程 2=精英 3=Boss
        public int currentHp;
        public int maxHp;
        public Vector2 position;
        public Vector2 velocity;
        public int attackTarget;    // 目标玩家ID, -1=无
        public bool isDead;
        public float timestamp;

        public MonsterState(int id, int type, int hp, int maxH, Vector2 pos)
        {
            monsterId = id; monsterType = type; currentHp = hp; maxHp = maxH;
            position = pos; velocity = Vector2.zero; attackTarget = -1;
            isDead = false; timestamp = 0;
        }
    }

    /// <summary>掉落物品信息</summary>
    [System.Serializable]
    public struct DropInfo
    {
        public int rarity;
        public int slotType;
        public int dungeonLevel;
    }

    /// <summary>结算数据 — Game Server推送给Web API</summary>
    [System.Serializable]
    public class SettlementData
    {
        public string roomId;
        public int dungeonId;
        public bool victory;
        public float timeUsed;
        public List<PlayerReward> playerRewards = new();
        public List<DropInfo> drops = new();
    }

    /// <summary>单个玩家的奖励</summary>
    [System.Serializable]
    public class PlayerReward
    {
        public int playerId;
        public string username;
        public int goldReward;
        public int xpReward;
        public int kills;
        public int damageDealt;
        public int damageTaken;
    }
}
