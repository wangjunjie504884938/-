using UnityEngine;
using System.Collections.Generic;

public enum EliteAffixType
{
    Inferno,       // 烈焰 — fire aura burns nearby player
    Teleport,      // 传送 — blinks toward player periodically
    Shield,        // 护盾 — absorbs damage, then breaks
    Spawner,       // 召唤 — spawns small minions periodically
    Haste,         // 疾风 — faster move + attack speed
    Reflect,       // 反弹 — reflects % melee damage back
    Regeneration,  // 再生 — slowly regenerates HP
    Chain          // 连锁 — on death, buffs nearby enemies
}

public class EliteAffix
{
    public EliteAffixType Type { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public Color AuraColor { get; private set; }

    // Affix parameters
    public float AuraRadius { get; private set; }        // Inferno
    public int AuraDamage { get; private set; }           // Inferno
    public float TeleportCooldown { get; private set; }   // Teleport
    public float TeleportRange { get; private set; }      // Teleport
    public int ShieldHp { get; private set; }             // Shield
    public float ShieldRegenDelay { get; private set; }   // Shield
    public float SpawnInterval { get; private set; }      // Spawner
    public int SpawnCount { get; private set; }           // Spawner
    public float HasteSpeedMult { get; private set; }     // Haste
    public float HasteAttackMult { get; private set; }   // Haste
    public float ReflectPct { get; private set; }         // Reflect
    public int RegenPerSecond { get; private set; }       // Regeneration
    public float ChainBuffRadius { get; private set; }    // Chain
    public float ChainBuffDuration { get; private set; }  // Chain

    private EliteAffix() { }

    public static EliteAffix Create(EliteAffixType type, int dungeonLevel)
    {
        float scale = 1f + dungeonLevel * 0.1f;
        var affix = new EliteAffix { Type = type };

        // 从数据库配置读取
        GameConfigAffixDTO cfg = null;
        if (GameConfigManager.Instance != null && GameConfigManager.Instance.IsLoaded)
        {
            cfg = GameConfigManager.Instance.GetAffixData((int)type);
            if (cfg != null)
            {
                scale = cfg.affixScaleBase + dungeonLevel * cfg.affixScalePerLevel;
                affix.Name = cfg.name;
                affix.Description = cfg.description;
                affix.AuraColor = new Color(cfg.colorR, cfg.colorG, cfg.colorB, cfg.colorA);
                affix.AuraRadius = cfg.auraRadius;
                affix.AuraDamage = Mathf.RoundToInt(cfg.auraDamage * scale);
                affix.TeleportCooldown = cfg.teleportCooldown;
                affix.TeleportRange = cfg.teleportRange;
                affix.ShieldHp = Mathf.RoundToInt(cfg.shieldHp * scale);
                affix.ShieldRegenDelay = cfg.shieldRegenDelay;
                affix.SpawnInterval = cfg.spawnInterval;
                affix.SpawnCount = cfg.spawnCount;
                affix.HasteSpeedMult = cfg.hasteSpeedMult;
                affix.HasteAttackMult = cfg.hasteAttackMult;
                affix.ReflectPct = cfg.reflectPct;
                affix.RegenPerSecond = Mathf.RoundToInt(cfg.regenPerSecond * scale);
                affix.ChainBuffRadius = cfg.chainBuffRadius;
                affix.ChainBuffDuration = cfg.chainBuffDuration;
                return affix;
            }
        }

        // 回退到硬编码默认值
        CreateDefault(affix, type, scale);
        return affix;
    }

    private static void CreateDefault(EliteAffix affix, EliteAffixType type, float scale)
    {
        switch (type)
        {
            case EliteAffixType.Inferno:
                affix.Name = "烈焰";
                affix.Description = "周围的敌人会被灼烧";
                affix.AuraColor = new Color(1f, 0.4f, 0.1f, 0.5f);
                affix.AuraRadius = 2.5f;
                affix.AuraDamage = Mathf.RoundToInt(5f * scale);
                break;
            case EliteAffixType.Teleport:
                affix.Name = "传送";
                affix.Description = "可以瞬间移动到玩家身边";
                affix.AuraColor = new Color(0.6f, 0.3f, 1f, 0.5f);
                affix.TeleportCooldown = 5f;
                affix.TeleportRange = 6f;
                break;
            case EliteAffixType.Shield:
                affix.Name = "护盾";
                affix.Description = "拥有可以吸收伤害的护盾";
                affix.AuraColor = new Color(0.3f, 0.7f, 1f, 0.5f);
                affix.ShieldHp = Mathf.RoundToInt(30f * scale);
                affix.ShieldRegenDelay = 8f;
                break;
            case EliteAffixType.Spawner:
                affix.Name = "召唤";
                affix.Description = "定期召唤小怪助战";
                affix.AuraColor = new Color(0.4f, 0.9f, 0.3f, 0.5f);
                affix.SpawnInterval = 6f;
                affix.SpawnCount = 2;
                break;
            case EliteAffixType.Haste:
                affix.Name = "疾风";
                affix.Description = "移动和攻击速度大幅提升";
                affix.AuraColor = new Color(0.9f, 0.9f, 0.2f, 0.5f);
                affix.HasteSpeedMult = 1.6f;
                affix.HasteAttackMult = 0.6f;
                break;
            case EliteAffixType.Reflect:
                affix.Name = "反弹";
                affix.Description = "将部分伤害反弹给攻击者";
                affix.AuraColor = new Color(0.9f, 0.5f, 0.9f, 0.5f);
                affix.ReflectPct = 0.3f;
                break;
            case EliteAffixType.Regeneration:
                affix.Name = "再生";
                affix.Description = "持续恢复生命值";
                affix.AuraColor = new Color(0.2f, 1f, 0.5f, 0.5f);
                affix.RegenPerSecond = Mathf.RoundToInt(3f * scale);
                break;
            case EliteAffixType.Chain:
                affix.Name = "连锁";
                affix.Description = "死亡时强化周围的敌人";
                affix.AuraColor = new Color(1f, 0.6f, 0.2f, 0.5f);
                affix.ChainBuffRadius = 5f;
                affix.ChainBuffDuration = 10f;
                break;
        }
    }

    public static List<EliteAffixType> AllTypes = new List<EliteAffixType>
    {
        EliteAffixType.Inferno,
        EliteAffixType.Teleport,
        EliteAffixType.Shield,
        EliteAffixType.Spawner,
        EliteAffixType.Haste,
        EliteAffixType.Reflect,
        EliteAffixType.Regeneration,
        EliteAffixType.Chain
    };

    /// <summary>Roll 1-2 random affixes for an elite, scaled by dungeon level.</summary>
    public static List<EliteAffix> RollAffixes(int dungeonLevel)
    {
        // More affixes on harder stages — PoE2-style escalating danger
        int count = dungeonLevel >= 5 ? 2 : 1;
        if (dungeonLevel >= 8) count = 3;
        var result = new List<EliteAffix>();
        var pool = new List<EliteAffixType>(AllTypes);

        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            int idx = Random.Range(0, pool.Count);
            result.Add(Create(pool[idx], dungeonLevel));
            pool.RemoveAt(idx);
        }

        return result;
    }

    /// <summary>Roll affixes for a boss — more affixes on harder stages.</summary>
    public static List<EliteAffix> RollBossAffixes(int stageIndex)
    {
        // Stage 0-1: 2 affixes, Stage 2-4: 3, Stage 5+: 4
        int count = stageIndex >= 5 ? 4 : (stageIndex >= 2 ? 3 : 2);
        var result = new List<EliteAffix>();
        var pool = new List<EliteAffixType>(AllTypes);

        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            int idx = Random.Range(0, pool.Count);
            result.Add(Create(pool[idx], stageIndex + 1));
            pool.RemoveAt(idx);
        }

        return result;
    }
}
