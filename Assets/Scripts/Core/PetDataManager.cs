using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Pet data manager — handles purchase, upgrade, evolution, and persistence.
/// Stored in PlayerPrefs. Each pet has: owned, level, evolved.
/// </summary>
public static class PetDataManager
{
    public struct PetInfo
    {
        public PetCompanion.PetType Type;
        public string Name;
        public int Price;
        public int UpgradeCost;
        public int MaxLevel;
        public int EvolveLevel;
        public string EvolveName;
        public string EvolveDesc;
    }

    public static readonly PetInfo[] PetDefs = new PetInfo[]
    {
        new PetInfo
        {
            Type = PetCompanion.PetType.Spirit, Name = "精灵",
            Price = 500, UpgradeCost = 100, MaxLevel = 10, EvolveLevel = 5,
            EvolveName = "雷灵", EvolveDesc = "闪电链范围+50%, 连锁3→5体"
        },
        new PetInfo
        {
            Type = PetCompanion.PetType.Flame, Name = "烈焰",
            Price = 800, UpgradeCost = 150, MaxLevel = 10, EvolveLevel = 5,
            EvolveName = "炎魔", EvolveDesc = "火焰范围+50%, AOE爆发"
        },
        new PetInfo
        {
            Type = PetCompanion.PetType.Guardian, Name = "守护",
            Price = 1000, UpgradeCost = 200, MaxLevel = 10, EvolveLevel = 5,
            EvolveName = "圣灵", EvolveDesc = "治疗量翻倍, 范围+30%"
        },
        new PetInfo
        {
            Type = PetCompanion.PetType.Shadow, Name = "暗影",
            Price = 1200, UpgradeCost = 180, MaxLevel = 10, EvolveLevel = 5,
            EvolveName = "影魔", EvolveDesc = "暴击3x→5x, 攻速提升"
        },
    };

    private const string KeyPrefix = "ARPG_Pet_";

    /// <summary>当前活跃角色槽位前缀 (包含槽位号, 确保每个角色宠物数据独立)</summary>
    private static string SlotPrefix => $"ARPG_S{PlayerProgressData.ActiveSlot}_Pet_";

    public static bool IsOwned(PetCompanion.PetType type)
        => PlayerPrefs.GetInt(SlotPrefix + type + "_Owned", 0) == 1;

    public static int GetLevel(PetCompanion.PetType type)
        => PlayerPrefs.GetInt(SlotPrefix + type + "_Level", 1);

    public static bool IsEvolved(PetCompanion.PetType type)
        => PlayerPrefs.GetInt(SlotPrefix + type + "_Evolved", 0) == 1;

    public static PetInfo? GetDef(PetCompanion.PetType type)
    {
        foreach (var d in PetDefs) if (d.Type == type) return d;
        return null;
    }

    public static string GetDisplayName(PetCompanion.PetType type)
    {
        var defOpt = GetDef(type);
        if (!defOpt.HasValue) return type.ToString();
        var def = defOpt.Value;
        return IsEvolved(type) ? def.EvolveName : def.Name;
    }

    public static bool Buy(PetCompanion.PetType type)
    {
        var defOpt = GetDef(type);
        if (!defOpt.HasValue || IsOwned(type)) return false;
        var def = defOpt.Value;
        var p = GameManager.Instance?.Player;
        if (p == null || p.Stats.Gold < def.Price) return false;
        p.Stats.SpendGold(def.Price);
        PlayerPrefs.SetInt(SlotPrefix + type + "_Owned", 1);
        PlayerPrefs.SetInt(SlotPrefix + type + "_Level", 1);
        PlayerPrefs.SetInt(SlotPrefix + type + "_Evolved", 0);
        PlayerPrefs.Save(); p.Stats.Save();
        return true;
    }

    public static bool Upgrade(PetCompanion.PetType type)
    {
        var defOpt = GetDef(type);
        if (!defOpt.HasValue || !IsOwned(type)) return false;
        var def = defOpt.Value;
        int lvl = GetLevel(type);
        if (lvl >= def.MaxLevel) return false;
        var p = GameManager.Instance?.Player;
        if (p == null) return false;
        int cost = def.UpgradeCost * lvl;
        if (p.Stats.Gold < cost) return false;
        p.Stats.SpendGold(cost);
        PlayerPrefs.SetInt(SlotPrefix + type + "_Level", lvl + 1);
        PlayerPrefs.Save(); p.Stats.Save();
        return true;
    }

    public static bool Evolve(PetCompanion.PetType type)
    {
        var defOpt = GetDef(type);
        if (!defOpt.HasValue || !IsOwned(type) || IsEvolved(type)) return false;
        var def = defOpt.Value;
        if (GetLevel(type) < def.EvolveLevel) return false;
        var p = GameManager.Instance?.Player;
        if (p == null) return false;
        int cost = 2000;
        if (p.Stats.Gold < cost) return false;
        p.Stats.SpendGold(cost);
        PlayerPrefs.SetInt(SlotPrefix + type + "_Evolved", 1);
        PlayerPrefs.Save(); p.Stats.Save();
        return true;
    }

    public static float GetDamageMult(PetCompanion.PetType type)
    {
        float mult = 1f + (GetLevel(type) - 1) * 0.15f;
        if (IsEvolved(type)) mult *= 1.5f;
        return mult;
    }

    public static float GetHpMult(PetCompanion.PetType type)
    {
        float mult = 1f + (GetLevel(type) - 1) * 0.1f;
        if (IsEvolved(type)) mult *= 1.3f;
        return mult;
    }

    public static float GetCooldownMult(PetCompanion.PetType type)
    {
        float mult = 1f - (GetLevel(type) - 1) * 0.02f;
        if (IsEvolved(type)) mult -= 0.15f;
        return Mathf.Max(0.5f, mult);
    }

    /// <summary>Returns list of owned pet types for spawning in dungeon.</summary>
    public static List<PetCompanion.PetType> GetOwnedPets()
    {
        var result = new List<PetCompanion.PetType>();
        foreach (var def in PetDefs)
            if (IsOwned(def.Type)) result.Add(def.Type);
        return result;
    }

    /// <summary>是否拥有任何宠物</summary>
    public static bool HasAnyPet()
    {
        foreach (var def in PetDefs)
            if (IsOwned(def.Type)) return true;
        return false;
    }

    /// <summary>活跃宠物key (按槽位独立)</summary>
    private static string ActivePetKey => $"ARPG_S{PlayerProgressData.ActiveSlot}_Pet_ActiveType";

    /// <summary>无宠物时的哨兵值</summary>
    public const int NoPet = -1;

    public static PetCompanion.PetType GetActivePet()
    {
        int val = PlayerPrefs.GetInt(ActivePetKey, NoPet);
        if (val == NoPet || !System.Enum.IsDefined(typeof(PetCompanion.PetType), val))
            return PetCompanion.PetType.Spirit; // fallback, 但调用方应先检查HasAnyPet
        var type = (PetCompanion.PetType)val;
        if (IsOwned(type)) return type;
        return PetCompanion.PetType.Spirit;
    }

    /// <summary>是否有活跃宠物（已购买且选中）</summary>
    public static bool HasActivePet()
    {
        // 检查 ActivePetKey 是否被显式设置过
        if (!PlayerPrefs.HasKey(ActivePetKey)) return false;
        int val = PlayerPrefs.GetInt(ActivePetKey, NoPet);
        if (val == NoPet) return false;
        if (!System.Enum.IsDefined(typeof(PetCompanion.PetType), val)) return false;
        return IsOwned((PetCompanion.PetType)val);
    }

    /// <summary>清除旧版本自动赠送的精灵宠物（价格改为500后，需要购买才能拥有）</summary>
    public static void CleanupLegacyFreePet()
    {
        // 如果精灵是旧版本自动赠送的（没有购买记录），清除拥有标记
        if (IsOwned(PetCompanion.PetType.Spirit))
        {
            // 检查是否曾经手动设置过 ActivePetKey
            if (!PlayerPrefs.HasKey(ActivePetKey))
            {
                // 没有主动选择过宠物 → 旧版本自动赠送 → 清除
                PlayerPrefs.SetInt(SlotPrefix + PetCompanion.PetType.Spirit + "_Owned", 0);
                PlayerPrefs.Save();
            }
        }
    }

    public static void SaveActivePet(PetCompanion.PetType type)
    {
        PlayerPrefs.SetInt(ActivePetKey, (int)type);
        PlayerPrefs.Save();
    }

    /// <summary>取消携带宠物</summary>
    public static void ClearActivePet()
    {
        PlayerPrefs.SetInt(ActivePetKey, NoPet);
        PlayerPrefs.Save();
    }
}
