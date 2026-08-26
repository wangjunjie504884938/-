using System.Text.Json.Serialization;

namespace ArpgServer.Models;

// ========== 共享数据类型 — 与客户端ArpgShared命名空间对应 ==========

/// <summary>创建房间请求 — Web API→Game Server</summary>
public class CreateRoomRequest
{
    public string RoomId { get; set; } = "";
    public int DungeonId { get; set; }
    public int[] MemberUserIds { get; set; } = System.Array.Empty<int>();
    public int Difficulty { get; set; }
    public int MaxMembers { get; set; } = 5;
}

/// <summary>创建房间响应 — Game Server→Web API</summary>
public class CreateRoomResponse
{
    public bool Success { get; set; }
    public string RoomId { get; set; } = "";
    public int Port { get; set; }
    public string ErrorMessage { get; set; } = "";
}

/// <summary>结算数据 — Game Server推送给Web API</summary>
public class SettlementData
{
    public string RoomId { get; set; } = "";
    public int DungeonId { get; set; }
    public bool Victory { get; set; }
    public float TimeUsed { get; set; }
    public List<PlayerReward> PlayerRewards { get; set; } = new();
    public List<DropInfo> Drops { get; set; } = new();
}

/// <summary>单个玩家的奖励</summary>
public class PlayerReward
{
    public int PlayerId { get; set; }
    public string Username { get; set; } = "";
    public int GoldReward { get; set; }
    public int XpReward { get; set; }
    public int Kills { get; set; }
    public int DamageDealt { get; set; }
    public int DamageTaken { get; set; }
}

/// <summary>掉落物品信息</summary>
public class DropInfo
{
    public int Rarity { get; set; }
    public int SlotType { get; set; }
    public int DungeonLevel { get; set; }
}
