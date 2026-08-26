using UnityEngine;
using System.Collections.Generic;

public enum RoomEventType
{
    Treasure,       // 宝箱 — free gold + maybe equipment
    HealingFountain,// 治愈之泉 — restore HP
    Merchant        // 商人 — buy buff with gold
}

public class RoomEventData
{
    public RoomEventType Type { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public Color IconColor { get; private set; }

    private RoomEventData() { }

    public static RoomEventData Create(RoomEventType type)
    {
        var data = new RoomEventData { Type = type };
        switch (type)
        {
            case RoomEventType.Treasure:
                data.Name = "宝箱";
                data.Description = "发现了一箱宝物！获得金币和装备";
                data.IconColor = new Color(1f, 0.85f, 0.2f);
                break;
            case RoomEventType.HealingFountain:
                data.Name = "治愈之泉";
                data.Description = "神秘的泉水恢复了你的生命";
                data.IconColor = new Color(0.3f, 0.9f, 1f);
                break;
            case RoomEventType.Merchant:
                data.Name = "旅行商人";
                data.Description = "用金币购买临时增益";
                data.IconColor = new Color(0.6f, 0.4f, 1f);
                break;
        }
        return data;
    }

    public static readonly List<RoomEventType> AllTypes = new List<RoomEventType>
    {
        RoomEventType.Treasure,
        RoomEventType.HealingFountain,
        RoomEventType.Merchant
    };

    /// <summary>Roll a random room event (weighted: treasure/heal more common).</summary>
    public static RoomEventData RollEvent()
    {
        float roll = Random.Range(0f, 1f);
        RoomEventType type;
        if (roll < 0.40f) type = RoomEventType.Treasure;
        else if (roll < 0.75f) type = RoomEventType.HealingFountain;
        else type = RoomEventType.Merchant;

        return Create(type);
    }
}
