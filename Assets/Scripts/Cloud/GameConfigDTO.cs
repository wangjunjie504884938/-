using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 游戏配置 DTO — 与服务器 GET /api/config/all 的 JSON 结构对应
/// 所有字段名使用小写开头以匹配 ASP.NET Core 的 camelCase JSON 序列化
/// </summary>

[Serializable]
public class GameConfigClassDTO
{
    public int classId;
    public string className;
    public string description;
    public string skill1Name;
    public string skill2Name;
    public string skill3Name;
    public int baseHp;
    public int baseAttack;
    public int baseDefense;
    public float baseMoveSpeed;
    public float baseAttackRange;
    public float baseAttackCooldown;
    public float baseCritChance;
    public float baseHpRegen;
    public int hpPerLevel;
    public int attackPerLevel;
    public int defensePerLevel;
    public bool isRanged;
    public float autoAttackRange;
    public float primaryR, primaryG, primaryB, primaryA;
    public float secondaryR, secondaryG, secondaryB, secondaryA;
    public float skill1R, skill1G, skill1B, skill1A;
    public float skill2R, skill2G, skill2B, skill2A;
    public float skill3R, skill3G, skill3B, skill3A;
}

[Serializable]
public class GameConfigSkillDTO
{
    public int classId;
    public int skillSlot;
    public string name;
    public string description;
    public float cooldown;
    public float range;
    public float damageMultiplier;
    public string cooldownPerLevel;
    public string damageMultiplierPerLevel;
    public string rangePerLevel;
    public string evolutionName;
    public string evolutionDescription;
    public float evolutionDamageMultiplier;
    public int evolutionProjectileBonus;
}

[Serializable]
public class GameConfigEnemyDTO
{
    public int enemyType;
    public int baseHp;
    public int baseAttack;
    public int baseDefense;
    public float baseMoveSpeed;
    public int baseXpReward;
    public float attackRange;
    public float attackCooldown;
    public float scaleEarly;
    public float scaleEarlyStep;
    public float scaleMid;
    public float scaleMidStep;
    public float scaleLate;
    public float scaleLateStep;
    public float moveSpeedEarlyMult;
    public float moveSpeedMidStep;
    public float moveSpeedLateBase;
    public float moveSpeedLateStep;
    public float enemyAttackMult;
    public float bossHpMult;
    public float bossAtkMult;
    public int bossDefBonusBase;
    public float bossXpMult;
    public float eliteHpMult;
    public float eliteAtkMult;
    public float eliteXpMult;
    public int attackCdScaleStartLevel;
    public float attackCdScalePerLevel;
    public float attackCdScaleMin;
    public int minionBaseHp;
    public int minionBaseAttack;
    public float minionBaseMoveSpeed;
    public int minionBaseXpReward;
}

[Serializable]
public class GameConfigAffixDTO
{
    public int affixType;
    public string name;
    public string description;
    public float auraRadius;
    public int auraDamage;
    public float teleportCooldown;
    public float teleportRange;
    public int shieldHp;
    public float shieldRegenDelay;
    public float spawnInterval;
    public int spawnCount;
    public float hasteSpeedMult;
    public float hasteAttackMult;
    public float reflectPct;
    public int regenPerSecond;
    public float chainBuffRadius;
    public float chainBuffDuration;
    public float affixScaleBase;
    public float affixScalePerLevel;
    public float colorR, colorG, colorB, colorA;
}

[Serializable]
public class GameConfigRuneDTO
{
    public int runeType;
    public string name;
    public string description;
    public float damageMultBase;
    public float damageMultPerLevel;
    public float damageMultMin;
    public int projectileBonus;
    public int pierceBase;
    public int piercePerLevel;
    public int chainBase;
    public int chainPerLevel;
    public float aoeRangeMultBase;
    public float aoeRangeMultPerLevel;
    public float lifeStealBase;
    public float lifeStealPerLevel;
    public float slowPctBase;
    public float slowPctPerLevel;
    public float slowDurationBase;
    public float slowDurationPerLevel;
    public float burnDpsBase;
    public float burnDpsPerLevel;
    public float burnDurationBase;
    public float burnDurationPerLevel;
    public float colorR, colorG, colorB;
}

[Serializable]
public class GameConfigRelicDTO
{
    public int relicType;
    public string name;
    public string description;
    public int rarity;
    public int splitCount;
    public int killHealAmount;
    public float critBurstRadius;
    public float speedBoostDuration;
    public float speedBoostMult;
    public float bloodMagicDamageBonus;
    public float bloodMagicHpCostPct;
    public int chainTargets;
    public float thornsPct;
    public float doubleStrikeChance;
    public float dodgeHealChance;
    public int dodgeHealAmount;
    public float xpBonus;
    public float colorR, colorG, colorB;
    public float rollLegendaryBase;
    public float rollLegendaryPerStage;
    public float rollRareBase;
    public float rollRarePerStage;
}

[Serializable]
public class GameConfigEquipmentDTO
{
    public int rarity;
    public float tierMultiplier;
    public int sellPriceBase;
    public int upgradeBaseCost;
    public float upgradeBoostPct;
    public float sellPriceUpgradeMult;
}

/// <summary>
/// 服务器 /api/config/all 的完整响应
/// </summary>
[Serializable]
public class GameConfigAll
{
    public List<GameConfigClassDTO> classes;
    public List<GameConfigSkillDTO> skills;
    public List<GameConfigEnemyDTO> enemies;
    public List<GameConfigAffixDTO> affixes;
    public List<GameConfigRuneDTO> runes;
    public List<GameConfigRelicDTO> relics;
    public List<GameConfigEquipmentDTO> equipment;
    public List<GameConfigGlobalEntry> global;

    [NonSerialized]
    public Dictionary<string, string> globalDict;

    public void BuildGlobalDict()
    {
        globalDict = new Dictionary<string, string>();
        if (global != null)
            foreach (var entry in global)
                globalDict[entry.key] = entry.value;
    }
}

[Serializable]
public class GameConfigGlobalEntry
{
    public string key;
    public string value;
}
