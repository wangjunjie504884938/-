using UnityEngine;

/// <summary>
/// 角色槽位数据访问层 — 从CharacterSlotUI提取
/// 本地角色槽位概要信息（服务器权威，本地仅缓存）
/// </summary>
public static class SlotData
{
    /// <summary>槽位概要（服务器SlotSummary的本地版本）</summary>
    public struct SlotInfo
    {
        public int slot;
        public bool occupied;
        public int classType;
        public int level;
        public int highestStageCleared;
        public string characterName;
    }

    /// <summary>读取3个本地槽位概要</summary>
    public static SlotInfo[] GetLocalSlots()
    {
        var slots = new SlotInfo[3];
        for (int i = 0; i < 3; i++)
        {
            string prefix = $"ARPG_S{i}_";
            if (PlayerPrefs.HasKey(prefix + "Class"))
            {
                slots[i] = new SlotInfo
                {
                    slot = i,
                    occupied = true,
                    classType = PlayerPrefs.GetInt(prefix + "Class", 0),
                    level = PlayerPrefs.GetInt(prefix + "Level", 1),
                    highestStageCleared = PlayerPrefs.GetInt(prefix + "HighestStage", 0),
                    characterName = PlayerPrefs.GetString(prefix + "CharacterName", "")
                };
            }
            else
            {
                slots[i] = new SlotInfo
                {
                    slot = i, occupied = false, classType = 0, level = 0,
                    highestStageCleared = 0, characterName = ""
                };
            }
        }
        return slots;
    }

    /// <summary>获取指定槽位的角色名</summary>
    public static string GetCharacterName(int slotIndex)
    {
        return PlayerPrefs.GetString($"ARPG_S{slotIndex}_CharacterName", "");
    }
}
