using UnityEngine;
using Mirror;

namespace ArpgShared
{
    /// <summary>移动消息 — 客户端→服务器</summary>
    public struct MoveMessage : Mirror.NetworkMessage
    {
        public int playerId;
        public Vector2 position;
        public Vector2 velocity;
        public float timestamp;
    }

    /// <summary>技能释放消息 — 客户端→服务器</summary>
    public struct SkillCastMessage : Mirror.NetworkMessage
    {
        public int playerId;
        public int skillId;
        public Vector2 targetPosition;
        public float timestamp;
    }

    /// <summary>状态快照消息 — 服务器→客户端广播</summary>
    public struct StateSnapshotMessage : Mirror.NetworkMessage
    {
        public float timestamp;
        public PlayerState[] players;
        public MonsterState[] monsters;
    }

    /// <summary>结算消息 — 服务器→客户端广播</summary>
    public struct SettlementMessage : Mirror.NetworkMessage
    {
        public bool victory;
        public int goldReward;
        public int xpReward;
        public DropInfo[] drops;
        public int newHighestStage;
    }

    /// <summary>创建房间请求 — Web API→Game Server</summary>
    [System.Serializable]
    public class CreateRoomRequest
    {
        public string roomId;
        public int dungeonId;
        public int[] memberUserIds;
        public int difficulty;
        public int maxMembers;
    }

    /// <summary>创建房间响应 — Game Server→Web API</summary>
    [System.Serializable]
    public class CreateRoomResponse
    {
        public bool success;
        public string roomId;
        public int port;
        public string errorMessage;
    }
}
