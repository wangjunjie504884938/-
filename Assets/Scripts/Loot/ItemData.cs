using UnityEngine;

public enum ItemType
{
    Weapon,
    Armor,
    Potion,
    Accessory
}

public enum ItemRarity
{
    Common,
    Rare,
    Epic,
    Legendary
}

[CreateAssetMenu(fileName = "NewItemData", menuName = "Dungeon/ItemData")]
public class ItemData : ScriptableObject
{
    public string ItemName;
    public ItemType Type;
    public ItemRarity Rarity;
    public int AttackBonus;
    public int DefenseBonus;
    public int HpBonus;
    public float SpeedBonus;
    public Sprite Icon;
    public Color RarityColor => GetRarityColor(Rarity);

    public static Color GetRarityColor(ItemRarity rarity)
    {
        return rarity switch
        {
            ItemRarity.Common => new Color(0.60f, 0.60f, 0.62f, 1f),
            ItemRarity.Rare => new Color(0.25f, 0.45f, 0.85f, 1f),
            ItemRarity.Epic => new Color(0.65f, 0.25f, 0.90f, 1f),
            ItemRarity.Legendary => new Color(0.85f, 0.60f, 0.12f, 1f),
            _ => new Color(0.60f, 0.60f, 0.62f, 1f)
        };
    }

    public static ItemRarity RollRarity(int dungeonLevel)
    {
        float legendaryChance = 0.02f + dungeonLevel * 0.005f;
        float epicChance = 0.08f + dungeonLevel * 0.01f;
        float rareChance = 0.2f + dungeonLevel * 0.02f;

        // 从数据库配置读取概率
        if (GameConfigManager.Instance != null && GameConfigManager.Instance.IsLoaded)
        {
            legendaryChance = GameConfigManager.Instance.GetGlobal("RarityLegendaryBase", 0.02f)
                + dungeonLevel * GameConfigManager.Instance.GetGlobal("RarityLegendaryPerLevel", 0.005f);
            epicChance = GameConfigManager.Instance.GetGlobal("RarityEpicBase", 0.08f)
                + dungeonLevel * GameConfigManager.Instance.GetGlobal("RarityEpicPerLevel", 0.01f);
            rareChance = GameConfigManager.Instance.GetGlobal("RarityRareBase", 0.2f)
                + dungeonLevel * GameConfigManager.Instance.GetGlobal("RarityRarePerLevel", 0.02f);
        }

        float roll = Random.Range(0f, 1f);
        if (roll < legendaryChance) return ItemRarity.Legendary;
        if (roll < legendaryChance + epicChance) return ItemRarity.Epic;
        if (roll < legendaryChance + epicChance + rareChance) return ItemRarity.Rare;
        return ItemRarity.Common;
    }

    /// <summary>Boss掉落专用稀有度 — 比普通掉落更高概率出高品质</summary>
    public static ItemRarity RollBossRarity(int dungeonLevel)
    {
        float legendaryChance = 0.03f + dungeonLevel * 0.002f;
        float epicChance = 0.12f + dungeonLevel * 0.008f;
        float rareChance = 0.30f + dungeonLevel * 0.005f;

        if (GameConfigManager.Instance != null && GameConfigManager.Instance.IsLoaded)
        {
            legendaryChance = GameConfigManager.Instance.GetGlobal("BossRarityLegendaryBase", 0.03f)
                + dungeonLevel * GameConfigManager.Instance.GetGlobal("BossRarityLegendaryPerLevel", 0.002f);
            epicChance = GameConfigManager.Instance.GetGlobal("BossRarityEpicBase", 0.12f)
                + dungeonLevel * GameConfigManager.Instance.GetGlobal("BossRarityEpicPerLevel", 0.008f);
            rareChance = GameConfigManager.Instance.GetGlobal("BossRarityRareBase", 0.30f)
                + dungeonLevel * GameConfigManager.Instance.GetGlobal("BossRarityRarePerLevel", 0.005f);
        }

        float roll = Random.Range(0f, 1f);
        if (roll < legendaryChance) return ItemRarity.Legendary;
        if (roll < legendaryChance + epicChance) return ItemRarity.Epic;
        if (roll < legendaryChance + epicChance + rareChance) return ItemRarity.Rare;
        return ItemRarity.Common;
    }
}
