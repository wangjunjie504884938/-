using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 符文类型 — 类似 PoE 辅助宝石，改变技能行为
/// </summary>
public enum RuneType
{
    Scatter,         // 散射：投射物数量+2，伤害-30%
    Pierce,          // 穿透：投射物穿透1个敌人，伤害-20%
    Chain,           // 连锁：命中后弹射到附近敌人，弹射2次，伤害-40%
    Burn,            // 燃烧：命中点燃地面3秒，DOT伤害
    AreaExpand,      // 范围扩大：AOE范围+50%，伤害-15%
    ElementConvert,  // 元素转换：将伤害转为冰霜/闪电/火焰，附带元素效果
    Vampiric,        // 吸血：伤害的15%转为生命，伤害-25%
    Slowing          // 减速：命中敌人减速40%持续2秒，伤害-10%
}

/// <summary>
/// 符文数据 — 可装备到技能的符文槽位上
/// </summary>
[System.Serializable]
public class RuneData
{
    public string Name;
    public RuneType Type;
    public string Description;
    public int Level = 1;
    public int MaxLevel = 5;

    // 符文效果参数（随等级缩放）
    public float DamageMultiplier;       // 伤害倍率修正（<1=减伤代价）
    public int ProjectileBonus;           // 额外投射物数量
    public int PierceCount;               // 穿透次数
    public int ChainCount;                // 连锁弹射次数
    public float AoeRangeMultiplier;      // AOE范围倍率
    public float LifeStealPct;            // 吸血百分比
    public float SlowPct;                 // 减速百分比
    public float SlowDuration;            // 减速持续时间
    public float BurnDps;                 // 燃烧每秒伤害（基于攻击力的比例）
    public float BurnDuration;            // 燃烧持续时间
    public Color RuneColor;               // 符文视觉颜色

    /// <summary>
    /// 根据符文类型和等级创建符文实例
    /// </summary>
    public static RuneData Create(RuneType type, int level = 1)
    {
        var rune = new RuneData { Type = type, Level = Mathf.Clamp(level, 1, 5) };
        ApplyTypeDefaults(rune);
        return rune;
    }

    public static List<RuneData> GetAllRunes()
    {
        var list = new List<RuneData>();
        foreach (RuneType type in System.Enum.GetValues(typeof(RuneType)))
            list.Add(Create(type));
        return list;
    }

    private static void ApplyTypeDefaults(RuneData r)
    {
        int lvl = r.Level;

        // 从数据库配置读取
        if (GameConfigManager.Instance != null && GameConfigManager.Instance.IsLoaded)
        {
            var cfg = GameConfigManager.Instance.GetRuneData((int)r.Type);
            if (cfg != null)
            {
                r.Name = cfg.name;
                r.Description = cfg.description;
                r.DamageMultiplier = Mathf.Max(cfg.damageMultMin, cfg.damageMultBase - lvl * cfg.damageMultPerLevel);
                r.ProjectileBonus = cfg.projectileBonus;
                r.PierceCount = cfg.pierceBase + cfg.piercePerLevel * lvl;
                r.ChainCount = cfg.chainBase + cfg.chainPerLevel * lvl;
                r.AoeRangeMultiplier = cfg.aoeRangeMultBase + cfg.aoeRangeMultPerLevel * lvl;
                r.LifeStealPct = cfg.lifeStealBase + cfg.lifeStealPerLevel * lvl;
                r.SlowPct = cfg.slowPctBase + cfg.slowPctPerLevel * lvl;
                r.SlowDuration = cfg.slowDurationBase + cfg.slowDurationPerLevel * lvl;
                r.BurnDps = cfg.burnDpsBase + cfg.burnDpsPerLevel * lvl;
                r.BurnDuration = cfg.burnDurationBase + cfg.burnDurationPerLevel * lvl;
                r.RuneColor = new Color(cfg.colorR, cfg.colorG, cfg.colorB);
                return;
            }
        }

        // 回退到硬编码默认值
        ApplyTypeDefaultsHardcoded(r, lvl);
    }

    private static void ApplyTypeDefaultsHardcoded(RuneData r, int lvl)
    {
        switch (r.Type)
        {
            case RuneType.Scatter:
                r.Name = "散射";
                r.Description = $"投射物+{2 + lvl}，伤害略降";
                r.DamageMultiplier = Mathf.Max(0.55f, 0.85f - lvl * 0.05f); // Less penalty
                r.ProjectileBonus = 2 + lvl; // Scales with level
                r.RuneColor = new Color(0.4f, 0.8f, 1f);
                break;
            case RuneType.Pierce:
                r.Name = "穿透";
                r.Description = $"投射物穿透{1 + lvl}个敌人";
                r.DamageMultiplier = Mathf.Max(0.65f, 0.9f - lvl * 0.04f); // Less penalty
                r.PierceCount = 1 + lvl;
                r.RuneColor = new Color(0.9f, 0.9f, 0.3f);
                break;
            case RuneType.Chain:
                r.Name = "连锁";
                r.Description = $"命中后弹射至{1 + lvl}个附近敌人";
                r.DamageMultiplier = Mathf.Max(0.5f, 0.75f - lvl * 0.04f); // Less penalty
                r.ChainCount = 1 + lvl;
                r.RuneColor = new Color(0.5f, 0.3f, 1f);
                break;
            case RuneType.Burn:
                r.Name = "燃烧";
                r.Description = $"点燃地面，每秒{Mathf.RoundToInt((0.2f + lvl * 0.08f) * 100)}%攻击力伤害";
                r.DamageMultiplier = 0.9f; // Minimal penalty
                r.BurnDps = 0.2f + lvl * 0.08f; // Stronger burn
                r.BurnDuration = 3f + lvl * 0.5f;
                r.RuneColor = new Color(1f, 0.4f, 0.1f);
                break;
            case RuneType.AreaExpand:
                r.Name = "范围扩大";
                r.Description = $"AOE范围+{Mathf.RoundToInt((0.4f + lvl * 0.15f) * 100)}%";
                r.DamageMultiplier = Mathf.Max(0.75f, 0.95f - lvl * 0.03f); // Less penalty
                r.AoeRangeMultiplier = 1.4f + lvl * 0.15f; // Bigger area
                r.RuneColor = new Color(0.3f, 1f, 0.5f);
                break;
            case RuneType.ElementConvert:
                r.Name = "元素转换";
                r.Description = "伤害转为随机元素，触发元素反应";
                r.DamageMultiplier = 1.1f; // Damage BONUS instead of penalty
                r.RuneColor = new Color(0.6f, 0.5f, 1f);
                break;
            case RuneType.Vampiric:
                r.Name = "吸血";
                r.Description = $"伤害的{Mathf.RoundToInt((0.12f + lvl * 0.04f) * 100)}%转为生命";
                r.DamageMultiplier = Mathf.Max(0.6f, 0.85f - lvl * 0.04f); // Less penalty
                r.LifeStealPct = 0.12f + lvl * 0.04f; // Stronger lifesteal
                r.RuneColor = new Color(0.9f, 0.1f, 0.3f);
                break;
            case RuneType.Slowing:
                r.Name = "减速";
                r.Description = $"命中敌人减速{Mathf.RoundToInt((0.35f + lvl * 0.08f) * 100)}%";
                r.DamageMultiplier = Mathf.Max(0.8f, 0.95f - lvl * 0.03f); // Less penalty
                r.SlowPct = 0.35f + lvl * 0.08f; // Stronger slow
                r.SlowDuration = 2f + lvl * 0.5f;
                r.RuneColor = new Color(0.2f, 0.6f, 0.9f);
                break;
        }
    }

    public override string ToString() => $"{Name} Lv{Level}";
}
