using UnityEngine;
using System.Collections.Generic;

public enum RelicType
{
    SplitShot,
    KillHeal,
    CritBurst,
    SpeedOnKill,
    BloodMagic,
    ChainLightning,
    Thorns,
    DoubleStrike,
    DodgeHeal,
    XpMagnet
}

public enum RelicRarity
{
    Common,
    Rare,
    Legendary
}

[System.Serializable]
public class RelicData
{
    public RelicType Type;
    public string Name;
    public string Description;
    public Color IconColor;
    public RelicRarity Rarity;

    /// <summary>SplitShot: 投射物命中时额外分裂出2个小投射物</summary>
    public int SplitCount => Type == RelicType.SplitShot ? 2 : 0;

    /// <summary>KillHeal: 击杀敌人恢复HP</summary>
    public int KillHealAmount => Type == RelicType.KillHeal ? 15 : 0;

    /// <summary>CritBurst: 暴击时产生范围爆炸</summary>
    public float CritBurstRadius => Type == RelicType.CritBurst ? 2.5f : 0f;

    /// <summary>SpeedOnKill: 击杀后短暂加速</summary>
    public float SpeedBoostDuration => Type == RelicType.SpeedOnKill ? 2f : 0f;
    public float SpeedBoostMult => Type == RelicType.SpeedOnKill ? 1.5f : 1f;

    /// <summary>BloodMagic: 技能消耗HP替代冷却(50%概率)，但伤害+40%</summary>
    public float BloodMagicDamageBonus => Type == RelicType.BloodMagic ? 1.4f : 1f;
    public float BloodMagicHpCostPct => Type == RelicType.BloodMagic ? 0.05f : 0f;

    /// <summary>ChainLightning: 投射物命中时弹射闪电到附近2个敌人</summary>
    public int ChainTargets => Type == RelicType.ChainLightning ? 2 : 0;

    /// <summary>Thorns: 受到伤害时反弹30%给攻击者</summary>
    public float ThornsPct => Type == RelicType.Thorns ? 0.3f : 0f;

    /// <summary>DoubleStrike: 20%概率普攻双重打击</summary>
    public float DoubleStrikeChance => Type == RelicType.DoubleStrike ? 0.2f : 0f;

    /// <summary>DodgeHeal: 受伤时10%概率恢复20HP</summary>
    public float DodgeHealChance => Type == RelicType.DodgeHeal ? 0.1f : 0f;
    public int DodgeHealAmount => Type == RelicType.DodgeHeal ? 20 : 0;

    /// <summary>XpMagnet: 经验获取+30%</summary>
    public float XpBonus => Type == RelicType.XpMagnet ? 0.3f : 0f;

    public bool HasEffect(RelicType type) => Type == type;

    private static List<RelicData> allRelics;

    public static List<RelicData> GetAllRelics()
    {
        // 从数据库配置读取
        if (GameConfigManager.Instance != null && GameConfigManager.Instance.IsLoaded)
        {
            var dtos = GameConfigManager.Instance.GetAllRelics();
            if (dtos != null && dtos.Count > 0)
            {
                allRelics = new List<RelicData>();
                foreach (var dto in dtos)
                {
                    allRelics.Add(new RelicData
                    {
                        Type = (RelicType)dto.relicType,
                        Name = dto.name,
                        Description = dto.description,
                        IconColor = new Color(dto.colorR, dto.colorG, dto.colorB),
                        Rarity = (RelicRarity)dto.rarity
                    });
                }
                return allRelics;
            }
        }

        if (allRelics != null) return allRelics;

        return GetAllRelicsDefault();
    }

    private static List<RelicData> GetAllRelicsDefault()
    {
        allRelics = new List<RelicData>
        {
            new RelicData
            {
                Type = RelicType.SplitShot,
                Name = "分裂之矢",
                Description = "投射物命中时分裂出2个小投射物",
                IconColor = new Color(0.3f, 0.9f, 1f),
                Rarity = RelicRarity.Rare
            },
            new RelicData
            {
                Type = RelicType.KillHeal,
                Name = "生命汲取",
                Description = "击杀敌人恢复15点生命",
                IconColor = new Color(0.2f, 1f, 0.3f),
                Rarity = RelicRarity.Common
            },
            new RelicData
            {
                Type = RelicType.CritBurst,
                Name = "暴击风暴",
                Description = "暴击时产生范围爆炸(2.5m)",
                IconColor = new Color(1f, 0.9f, 0.1f),
                Rarity = RelicRarity.Rare
            },
            new RelicData
            {
                Type = RelicType.SpeedOnKill,
                Name = "杀意涌动",
                Description = "击杀后2秒内移动速度+50%",
                IconColor = new Color(0.9f, 0.5f, 1f),
                Rarity = RelicRarity.Common
            },
            new RelicData
            {
                Type = RelicType.BloodMagic,
                Name = "血之祭祀",
                Description = "技能5%HP消耗换40%伤害加成",
                IconColor = new Color(0.9f, 0.1f, 0.1f),
                Rarity = RelicRarity.Legendary
            },
            new RelicData
            {
                Type = RelicType.ChainLightning,
                Name = "雷霆链",
                Description = "命中时闪电弹射到附近2个敌人",
                IconColor = new Color(0.5f, 0.7f, 1f),
                Rarity = RelicRarity.Rare
            },
            new RelicData
            {
                Type = RelicType.Thorns,
                Name = "荆棘之盾",
                Description = "受到伤害时反弹30%给攻击者",
                IconColor = new Color(0.6f, 0.8f, 0.2f),
                Rarity = RelicRarity.Common
            },
            new RelicData
            {
                Type = RelicType.DoubleStrike,
                Name = "双刃",
                Description = "20%概率普攻双重打击",
                IconColor = new Color(1f, 0.6f, 0.2f),
                Rarity = RelicRarity.Common
            },
            new RelicData
            {
                Type = RelicType.DodgeHeal,
                Name = "回光返照",
                Description = "受伤时10%概率恢复20HP",
                IconColor = new Color(0.3f, 1f, 0.9f),
                Rarity = RelicRarity.Common
            },
            new RelicData
            {
                Type = RelicType.XpMagnet,
                Name = "智慧之心",
                Description = "经验获取+30%",
                IconColor = new Color(0.7f, 0.5f, 1f),
                Rarity = RelicRarity.Common
            }
        };

        return allRelics;
    }

    public static RelicData RollRelic(int stageIndex)
    {
        var all = GetAllRelics();
        float legendaryChance = 0.05f + stageIndex * 0.02f;
        float rareChance = 0.25f + stageIndex * 0.03f;

        // 从数据库配置读取概率
        if (GameConfigManager.Instance != null && GameConfigManager.Instance.IsLoaded)
        {
            var firstRelic = GameConfigManager.Instance.GetRelicData(0);
            if (firstRelic != null)
            {
                legendaryChance = firstRelic.rollLegendaryBase + stageIndex * firstRelic.rollLegendaryPerStage;
                rareChance = firstRelic.rollRareBase + stageIndex * firstRelic.rollRarePerStage;
            }
        }

        RelicRarity roll;
        float r = Random.Range(0f, 1f);
        if (r < legendaryChance) roll = RelicRarity.Legendary;
        else if (r < legendaryChance + rareChance) roll = RelicRarity.Rare;
        else roll = RelicRarity.Common;

        var pool = all.FindAll(rl => rl.Rarity == roll);
        if (pool.Count == 0) pool = all;

        return pool[Random.Range(0, pool.Count)];
    }

    public static List<RelicData> RollRelicChoice(int count, int stageIndex)
    {
        var choices = new List<RelicData>();
        var pool = new List<RelicData>(GetAllRelics());

        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            // Weighted roll: pick from pool, favoring appropriate rarity
            var relic = RollRelic(stageIndex);
            // Remove duplicates
            if (choices.Exists(c => c.Type == relic.Type))
            {
                // Try to find a non-duplicate
                var available = pool.FindAll(p => !choices.Exists(c => c.Type == p.Type));
                if (available.Count == 0) break;
                relic = available[Random.Range(0, available.Count)];
            }
            choices.Add(relic);
            pool.RemoveAll(p => p.Type == relic.Type);
        }

        return choices;
    }
}
