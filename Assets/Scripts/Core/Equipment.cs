using UnityEngine;
using System.Collections.Generic;
using System.Text;

[System.Serializable]
public class EquipmentItemData
{
    public string Name;
    public int Rarity;
    public int SlotType;
    public int AttackBonus;
    public int DefenseBonus;
    public int HpBonus;
    public float SpeedBonus;
    public float CritBonus;
    public float LifeStealBonus;
    public float RangeBonus;
    public float AttackSpeedBonus;
    public int SellPrice;
    public int UpgradeLevel;
    public int SetId;
    public List<EquipmentAffix> Affixes;

    public EquipmentItemData() { }

    public EquipmentItemData(EquipmentItem item)
    {
        Name = item.Name;
        Rarity = (int)item.Rarity;
        SlotType = (int)item.SlotType;
        AttackBonus = item.AttackBonus;
        DefenseBonus = item.DefenseBonus;
        HpBonus = item.HpBonus;
        SpeedBonus = item.SpeedBonus;
        CritBonus = item.CritBonus;
        LifeStealBonus = item.LifeStealBonus;
        RangeBonus = item.RangeBonus;
        AttackSpeedBonus = item.AttackSpeedBonus;
        SellPrice = item.SellPrice;
        UpgradeLevel = item.UpgradeLevel;
        SetId = item.SetId;
        Affixes = item.Affixes;
    }

    public EquipmentItem ToItem()
    {
        if (string.IsNullOrEmpty(Name)) return null;
        return new EquipmentItem
        {
            Name = Name,
            Rarity = (ItemRarity)Rarity,
            SlotType = (EquipSlotType)SlotType,
            AttackBonus = AttackBonus,
            DefenseBonus = DefenseBonus,
            HpBonus = HpBonus,
            SpeedBonus = SpeedBonus,
            CritBonus = CritBonus,
            LifeStealBonus = LifeStealBonus,
            RangeBonus = RangeBonus,
            AttackSpeedBonus = AttackSpeedBonus,
            SellPrice = SellPrice,
            UpgradeLevel = UpgradeLevel,
            SetId = SetId,
            IconColor = ItemData.GetRarityColor((ItemRarity)Rarity),
            Affixes = Affixes ?? new List<EquipmentAffix>()
        };
    }
}

[System.Serializable]
public class EquipmentSaveData
{
    public EquipmentItemData[] EquippedItems = new EquipmentItemData[3]; // 3 slots, null = empty
    public EquipmentItemData[] BackpackItems;
}

public enum EquipSlotType
{
    Weapon,
    Armor,
    Accessory
}

/// <summary>装备词缀类型</summary>
public enum AffixType
{
    AttackPct,      // 攻击力%
    DefensePct,     // 防御力%
    HpPct,           // 生命值%
    MoveSpeed,       // 移动速度
    CritChance,      // 暴击率
    Lifesteal,       // 吸血
    AttackSpeed,     // 攻速
    AttackRange      // 攻击范围
}

/// <summary>装备词缀 — 随机前缀/后缀属性</summary>
[System.Serializable]
public class EquipmentAffix
{
    public int Type;            // AffixType enum
    public float Value;         // 数值
    public string DisplayName;  // 显示名称

    public AffixType AffixType => (AffixType)Type;
}

[System.Serializable]
public class EquipmentItem
{
    public string Name;
    public ItemRarity Rarity;
    public EquipSlotType SlotType;
    public int AttackBonus;
    public int DefenseBonus;
    public int HpBonus;
    public float SpeedBonus;
    public float CritBonus;
    public float LifeStealBonus;
    public float RangeBonus;
    public float AttackSpeedBonus;
    public int SellPrice;
    public Color IconColor;
    public int UpgradeLevel;
    public int SetId;
    /// <summary>随机词缀列表 (Common=1, Rare=2, Epic=3, Legendary=4)</summary>
    public List<EquipmentAffix> Affixes = new List<EquipmentAffix>();

    public static int MaxUpgradeLevel =>
        GameConfigManager.Instance != null && GameConfigManager.Instance.IsLoaded
            ? GameConfigManager.Instance.GetGlobalInt("MaxUpgradeLevel", 10) : 10;

    public static EquipmentItem Generate(EquipSlotType slot, ItemRarity rarity, int dungeonLevel)
    {
        // 从配置读取稀有度参数
        float tierMult = rarity switch
        {
            ItemRarity.Rare => 1.5f,
            ItemRarity.Epic => 2.5f,
            ItemRarity.Legendary => 4f,
            _ => 1f
        };
        float levelScale = 1f + (dungeonLevel - 1) * 0.15f;

        if (GameConfigManager.Instance != null && GameConfigManager.Instance.IsLoaded)
        {
            var cfg = GameConfigManager.Instance.GetEquipmentConfig((int)rarity);
            if (cfg != null)
            {
                tierMult = cfg.tierMultiplier;
                levelScale = GameConfigManager.Instance.GetGlobal("EquipmentLevelScaleBase", 1f)
                    + (dungeonLevel - 1) * GameConfigManager.Instance.GetGlobal("EquipmentLevelScalePerLevel", 0.15f);
            }
        }

        float m = tierMult * levelScale;

        int sellPriceBase = rarity switch
        {
            ItemRarity.Common => 20,
            ItemRarity.Rare => 60,
            ItemRarity.Epic => 180,
            ItemRarity.Legendary => 500,
            _ => 20
        };
        if (GameConfigManager.Instance != null && GameConfigManager.Instance.IsLoaded)
        {
            var cfg = GameConfigManager.Instance.GetEquipmentConfig((int)rarity);
            if (cfg != null) sellPriceBase = cfg.sellPriceBase;
        }

        var item = new EquipmentItem
        {
            Name = GenerateName(slot, rarity),
            Rarity = rarity,
            SlotType = slot,
            SellPrice = Mathf.RoundToInt(sellPriceBase * levelScale),
            IconColor = ItemData.GetRarityColor(rarity)
        };

        switch (slot)
        {
            case EquipSlotType.Weapon:
                item.AttackBonus = Mathf.RoundToInt(Random.Range(3f, 6f) * m);
                item.CritBonus = Mathf.Min(0.50f, Random.Range(0.01f, 0.03f) * m);
                item.RangeBonus = Mathf.Min(3f, Random.Range(0f, 0.2f) * m);
                item.AttackSpeedBonus = Mathf.Min(0.50f, Random.Range(0.02f, 0.05f) * m);
                break;
            case EquipSlotType.Armor:
                item.DefenseBonus = Mathf.RoundToInt(Random.Range(2f, 5f) * m);
                item.HpBonus = Mathf.RoundToInt(Random.Range(10f, 25f) * m);
                break;
            case EquipSlotType.Accessory:
                item.LifeStealBonus = Mathf.Min(0.30f, Random.Range(0.02f, 0.05f) * m);
                item.SpeedBonus = Mathf.Min(5f, Random.Range(0.2f, 0.5f) * m);
                item.CritBonus = Mathf.Min(0.50f, Random.Range(0.01f, 0.04f) * m);
                break;
        }

        // Roll random affixes based on rarity
        item.Affixes = RollAffixes(rarity, dungeonLevel);

        // Assign set ID: Rare+ items have 25% chance to belong to a set
        if (rarity >= ItemRarity.Rare && Random.Range(0f, 1f) < 0.25f)
        {
            item.SetId = Random.Range(1, SetBonusData.Sets.Length + 1);
        }

        return item;
    }

    public static EquipmentItem GenerateRandom(int dungeonLevel)
    {
        EquipSlotType slot = (EquipSlotType)Random.Range(0, 3);
        ItemRarity rarity = ItemData.RollRarity(dungeonLevel);
        return Generate(slot, rarity, dungeonLevel);
    }

    /// <summary>Generate a shop-only item (Common / Rare only — Epic & Legendary are dungeon-exclusive).</summary>
    public static EquipmentItem GenerateShopItem(int dungeonLevel)
    {
        EquipSlotType slot = (EquipSlotType)Random.Range(0, 3);
        float roll = Random.Range(0f, 1f);
        float rareChance = 0.25f + dungeonLevel * 0.02f;
        if (GameConfigManager.Instance != null && GameConfigManager.Instance.IsLoaded)
        {
            rareChance = GameConfigManager.Instance.GetGlobal("ShopRareChanceBase", 0.25f)
                + dungeonLevel * GameConfigManager.Instance.GetGlobal("ShopRareChancePerLevel", 0.02f);
        }
        ItemRarity rarity = roll < rareChance ? ItemRarity.Rare : ItemRarity.Common;
        return Generate(slot, rarity, dungeonLevel);
    }

    private static readonly (AffixType type, string name, float min, float max)[] AffixPool =
    {
        (AffixType.AttackPct,     "攻击力",   0.05f, 0.15f),
        (AffixType.DefensePct,    "防御力",   0.05f, 0.15f),
        (AffixType.HpPct,         "生命值",   0.05f, 0.15f),
        (AffixType.MoveSpeed,     "移速",     0.2f, 0.6f),
        (AffixType.CritChance,    "暴击",     0.02f, 0.06f),
        (AffixType.Lifesteal,     "吸血",     0.02f, 0.05f),
        (AffixType.AttackSpeed,   "攻速",     0.03f, 0.08f),
        (AffixType.AttackRange,   "范围",     0.1f, 0.3f),
    };

    /// <summary>按稀有度随机生成词缀 (Common=1, Rare=2, Epic=3, Legendary=4)</summary>
    private static List<EquipmentAffix> RollAffixes(ItemRarity rarity, int dungeonLevel)
    {
        int count = rarity switch
        {
            ItemRarity.Legendary => 4,
            ItemRarity.Epic => 3,
            ItemRarity.Rare => 2,
            _ => 1,
        };

        var result = new List<EquipmentAffix>();
        var available = new List<int>(AffixPool.Length);
        for (int i = 0; i < AffixPool.Length; i++) available.Add(i);

        float levelScale = 1f + (dungeonLevel - 1) * 0.1f;

        for (int i = 0; i < count && available.Count > 0; i++)
        {
            int pickIdx = Random.Range(0, available.Count);
            int affixIdx = available[pickIdx];
            available.RemoveAt(pickIdx);

            var (type, name, min, max) = AffixPool[affixIdx];
            float value = Random.Range(min, max) * levelScale;
            // Clamp affix values to prevent overflow at high levels
            value = type switch
            {
                AffixType.CritChance => Mathf.Min(value, 0.50f),
                AffixType.AttackSpeed => Mathf.Min(value, 0.50f),
                AffixType.Lifesteal => Mathf.Min(value, 0.30f),
                AffixType.MoveSpeed => Mathf.Min(value, 5f),
                AffixType.AttackRange => Mathf.Min(value, 0.5f),
                AffixType.AttackPct => Mathf.Min(value, 0.50f),
                AffixType.DefensePct => Mathf.Min(value, 0.50f),
                AffixType.HpPct => Mathf.Min(value, 0.50f),
                _ => value
            };

            result.Add(new EquipmentAffix
            {
                Type = (int)type,
                Value = value,
                DisplayName = $"+{value * 100:F0}%{name}" +
                    (type == AffixType.MoveSpeed || type == AffixType.CritChance || type == AffixType.Lifesteal || type == AffixType.AttackSpeed || type == AffixType.AttackRange
                        ? "" : "")
            });
        }

        return result;
    }

    /// <summary>获取词缀提供的加成值 (用于属性计算)</summary>
    public float GetAffixBonus(AffixType type)
    {
        float total = 0f;
        if (Affixes == null) return 0f;
        foreach (var affix in Affixes)
        {
            if (affix.AffixType == type)
                total += affix.Value;
        }
        return total;
    }

    /// <summary>获取所有词缀的显示文本</summary>
    public string GetAffixDisplayString()
    {
        if (Affixes == null || Affixes.Count == 0) return "";
        var sb = new System.Text.StringBuilder();
        foreach (var affix in Affixes)
        {
            float pct = affix.Value * 100f;
            string typeLabel = affix.AffixType switch
            {
                AffixType.AttackPct => $"攻击+{pct:F0}%",
                AffixType.DefensePct => $"防御+{pct:F0}%",
                AffixType.HpPct => $"生命+{pct:F0}%",
                AffixType.MoveSpeed => $"移速+{affix.Value:F1}",
                AffixType.CritChance => $"暴击+{pct:F0}%",
                AffixType.Lifesteal => $"吸血+{pct:F0}%",
                AffixType.AttackSpeed => $"攻速+{pct:F0}%",
                AffixType.AttackRange => $"范围+{affix.Value:F1}",
                _ => "",
            };
            sb.Append(typeLabel).Append(" ");
        }
        return sb.ToString().TrimEnd();
    }

    private static string GenerateName(EquipSlotType slot, ItemRarity rarity)
    {
        string prefix = rarity switch
        {
            ItemRarity.Legendary => "传说·",
            ItemRarity.Epic => "史诗·",
            ItemRarity.Rare => "稀有·",
            _ => ""
        };

        return slot switch
        {
            EquipSlotType.Weapon => prefix + (rarity == ItemRarity.Legendary ? "天罚之刃" : rarity == ItemRarity.Epic ? "暗影长剑" : rarity == ItemRarity.Rare ? "精钢之剑" : "铁剑"),
            EquipSlotType.Armor => prefix + (rarity == ItemRarity.Legendary ? "不朽圣铠" : rarity == ItemRarity.Epic ? "秘银战甲" : rarity == ItemRarity.Rare ? "链甲" : "皮甲"),
            EquipSlotType.Accessory => prefix + (rarity == ItemRarity.Legendary ? "命运之环" : rarity == ItemRarity.Epic ? "龙心护符" : rarity == ItemRarity.Rare ? "银戒指" : "铜戒指"),
            _ => prefix + "物品"
        };
    }

    public string GetStatSummary()
    {
        var sb = new StringBuilder();
        if (AttackBonus > 0) { sb.Append("攻击+"); sb.Append(AttackBonus); sb.Append(' '); }
        if (DefenseBonus > 0) { sb.Append("防御+"); sb.Append(DefenseBonus); sb.Append(' '); }
        if (HpBonus > 0) { sb.Append("生命+"); sb.Append(HpBonus); sb.Append(' '); }
        if (SpeedBonus > 0.01f) { sb.Append("速度+"); sb.Append(SpeedBonus.ToString("F1")); sb.Append(' '); }
        if (CritBonus > 0.001f) { sb.Append("暴击+"); sb.Append((CritBonus * 100).ToString("F0")); sb.Append("% "); }
        if (LifeStealBonus > 0.001f) { sb.Append("吸血+"); sb.Append((LifeStealBonus * 100).ToString("F0")); sb.Append("% "); }
        if (RangeBonus > 0.01f) { sb.Append("范围+"); sb.Append(RangeBonus.ToString("F1")); sb.Append(' '); }
        if (AttackSpeedBonus > 0.001f) { sb.Append("攻速+"); sb.Append((AttackSpeedBonus * 100).ToString("F0")); sb.Append("% "); }
        if (sb.Length > 0) sb.Length--; // trim trailing space
        return sb.ToString();
    }

    public int GetUpgradeCost()
    {
        if (UpgradeLevel >= MaxUpgradeLevel) return 0;
        int baseCost = Rarity switch
        {
            ItemRarity.Legendary => 500,
            ItemRarity.Epic => 200,
            ItemRarity.Rare => 80,
            _ => 30
        };
        if (GameConfigManager.Instance != null && GameConfigManager.Instance.IsLoaded)
        {
            var cfg = GameConfigManager.Instance.GetEquipmentConfig((int)Rarity);
            if (cfg != null) baseCost = cfg.upgradeBaseCost;
        }
        return baseCost * (UpgradeLevel + 1);
    }

    /// <summary>Upgrade the item, boosting all stats by configured percentage.</summary>
    public bool Upgrade()
    {
        if (UpgradeLevel >= MaxUpgradeLevel) return false;
        UpgradeLevel++;
        float boost = 0.15f;
        float sellMult = 1.2f;
        if (GameConfigManager.Instance != null && GameConfigManager.Instance.IsLoaded)
        {
            var cfg = GameConfigManager.Instance.GetEquipmentConfig((int)Rarity);
            if (cfg != null)
            {
                boost = cfg.upgradeBoostPct;
                sellMult = cfg.sellPriceUpgradeMult;
            }
        }
        AttackBonus = Mathf.RoundToInt(AttackBonus * (1f + boost));
        DefenseBonus = Mathf.RoundToInt(DefenseBonus * (1f + boost));
        HpBonus = Mathf.RoundToInt(HpBonus * (1f + boost));
        SpeedBonus = Mathf.Min(5f, SpeedBonus * (1f + boost));
        CritBonus = Mathf.Min(0.50f, CritBonus * (1f + boost));
        LifeStealBonus = Mathf.Min(0.30f, LifeStealBonus * (1f + boost));
        RangeBonus = Mathf.Min(3f, RangeBonus * (1f + boost));
        AttackSpeedBonus = Mathf.Min(0.50f, AttackSpeedBonus * (1f + boost));
        SellPrice = Mathf.RoundToInt(SellPrice * sellMult);
        return true;
    }
}

[System.Serializable]
public class EquipmentSlot
{
    public EquipSlotType SlotType;
    public EquipmentItem Item;

    public bool IsEmpty => Item == null;

    public EquipmentSlot(EquipSlotType type)
    {
        SlotType = type;
    }
}

public class EquipmentInventory
{
    public List<EquipmentSlot> Equipped = new List<EquipmentSlot>
    {
        new EquipmentSlot(EquipSlotType.Weapon),
        new EquipmentSlot(EquipSlotType.Armor),
        new EquipmentSlot(EquipSlotType.Accessory)
    };
    public List<EquipmentItem> Backpack = new List<EquipmentItem>();
    public const int MaxBackpackSizeDefault = 50;
    public static int MaxBackpackSize =>
        GameConfigManager.Instance != null && GameConfigManager.Instance.IsLoaded
            ? Mathf.Max(50, GameConfigManager.Instance.GetGlobalInt("MaxBackpackSize", 50))
            : 50;

    /// <summary>Direct index lookup for equipped slots (0=Weapon, 1=Armor, 2=Accessory).</summary>
    private EquipmentSlot GetEquippedSlot(EquipSlotType type) => Equipped[(int)type];

    public bool Equip(EquipmentItem item)
    {
        var slot = GetEquippedSlot(item.SlotType);

        // Remove the item from backpack first (frees a slot for the swap)
        Backpack.Remove(item);

        // If slot occupied, move old item to backpack
        if (!slot.IsEmpty)
        {
            // After removing the incoming item, there's always room for the old item
            Backpack.Add(slot.Item);
        }

        slot.Item = item;
        RecalculateStats();
        return true;
    }

    public EquipmentItem Unequip(EquipSlotType slotType)
    {
        var slot = GetEquippedSlot(slotType);
        if (slot.IsEmpty) return null;

        if (Backpack.Count >= MaxBackpackSize) return null;

        var item = slot.Item;
        slot.Item = null;
        Backpack.Add(item);
        RecalculateStats();
        return item;
    }

    public bool SellFromBackpack(int index)
    {
        if (index < 0 || index >= Backpack.Count) return false;
        int gold = Backpack[index].SellPrice;
        Backpack.RemoveAt(index);
        if (GameManager.Instance?.Player != null)
            GameManager.Instance.Player.Stats.AddGold(gold);
        return true;
    }

    public bool SellEquipped(EquipSlotType slotType)
    {
        var slot = GetEquippedSlot(slotType);
        if (slot.IsEmpty) return false;

        int gold = slot.Item.SellPrice;
        slot.Item = null;
        if (GameManager.Instance?.Player != null)
            GameManager.Instance.Player.Stats.AddGold(gold);
        RecalculateStats();
        return true;
    }

    public bool AddToBackpack(EquipmentItem item)
    {
        if (Backpack.Count < MaxBackpackSize)
        {
            Backpack.Add(item);
            return true;
        }
        return false;
    }

    public string ToJson()
    {
        var data = new EquipmentSaveData();
        for (int i = 0; i < Equipped.Count; i++)
            data.EquippedItems[i] = Equipped[i].IsEmpty ? null : new EquipmentItemData(Equipped[i].Item);
        data.BackpackItems = new EquipmentItemData[Backpack.Count];
        for (int i = 0; i < Backpack.Count; i++)
            data.BackpackItems[i] = new EquipmentItemData(Backpack[i]);
        return JsonUtility.ToJson(data);
    }

    public void LoadFromJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return;

        var data = JsonUtility.FromJson<EquipmentSaveData>(json);
        if (data == null) return;

        // Restore equipped items
        for (int i = 0; i < Equipped.Count && i < data.EquippedItems.Length; i++)
            Equipped[i].Item = data.EquippedItems[i]?.ToItem();

        // Restore backpack
        Backpack.Clear();
        if (data.BackpackItems != null)
            foreach (var bd in data.BackpackItems)
            {
                var item = bd?.ToItem();
                if (item != null) Backpack.Add(item);
            }

        RecalculateStats();
    }

    public void RecalculateStats()
    {
        if (GameManager.Instance?.Player?.Stats != null)
            GameManager.Instance.Player.Stats.RecalculateEquipmentBonuses(Equipped);
    }
}
