using System.ComponentModel.DataAnnotations;

namespace ArpgServer.Models;

// ========== 1. 职业属性表 ==========

/// <summary>
/// 职业属性表 — 各职业基础数值
/// </summary>
public class GameClassStats
{
    public int Id { get; set; }
    /// <summary>职业ID(0=战士 1=法师 2=牧师)</summary>
    public int ClassId { get; set; }
    /// <summary>职业名称</summary>
    public string ClassName { get; set; } = "";
    /// <summary>职业描述</summary>
    public string Description { get; set; } = "";
    /// <summary>技能1名称</summary>
    public string Skill1Name { get; set; } = "";
    /// <summary>技能2名称</summary>
    public string Skill2Name { get; set; } = "";
    /// <summary>技能3名称</summary>
    public string Skill3Name { get; set; } = "";

    /// <summary>基础生命值</summary>
    public int BaseHp { get; set; }
    /// <summary>基础攻击力</summary>
    public int BaseAttack { get; set; }
    /// <summary>基础防御力</summary>
    public int BaseDefense { get; set; }
    /// <summary>基础移动速度</summary>
    public float BaseMoveSpeed { get; set; }
    /// <summary>基础攻击范围</summary>
    public float BaseAttackRange { get; set; }
    /// <summary>基础攻击冷却(秒)</summary>
    public float BaseAttackCooldown { get; set; }
    /// <summary>基础暴击率</summary>
    public float BaseCritChance { get; set; }
    /// <summary>基础生命恢复/秒</summary>
    public float BaseHpRegen { get; set; }

    /// <summary>每级生命增长</summary>
    public int HpPerLevel { get; set; }
    /// <summary>每级攻击增长</summary>
    public int AttackPerLevel { get; set; }
    /// <summary>每级防御增长</summary>
    public int DefensePerLevel { get; set; }

    /// <summary>是否远程职业</summary>
    public bool IsRanged { get; set; }
    /// <summary>普攻射程</summary>
    public float AutoAttackRange { get; set; }

    // 颜色分量 (0-1)
    public float PrimaryR { get; set; }
    public float PrimaryG { get; set; }
    public float PrimaryB { get; set; }
    public float PrimaryA { get; set; }
    public float SecondaryR { get; set; }
    public float SecondaryG { get; set; }
    public float SecondaryB { get; set; }
    public float SecondaryA { get; set; }
    public float Skill1R { get; set; }
    public float Skill1G { get; set; }
    public float Skill1B { get; set; }
    public float Skill1A { get; set; }
    public float Skill2R { get; set; }
    public float Skill2G { get; set; }
    public float Skill2B { get; set; }
    public float Skill2A { get; set; }
    public float Skill3R { get; set; }
    public float Skill3G { get; set; }
    public float Skill3B { get; set; }
    public float Skill3A { get; set; }
}

// ========== 2. 技能数据表 ==========

/// <summary>
/// 技能数据表 — 各职业技能详细信息
/// </summary>
public class GameSkillData
{
    public int Id { get; set; }
    /// <summary>职业ID(0=战士 1=法师 2=牧师)</summary>
    public int ClassId { get; set; }
    /// <summary>技能槽位(0=技能1 1=技能2 2=技能3)</summary>
    public int SkillSlot { get; set; }
    /// <summary>技能名称</summary>
    public string Name { get; set; } = "";
    /// <summary>技能描述</summary>
    public string Description { get; set; } = "";

    /// <summary>冷却时间(秒)</summary>
    public float Cooldown { get; set; }
    /// <summary>技能范围</summary>
    public float Range { get; set; }
    /// <summary>伤害倍率</summary>
    public float DamageMultiplier { get; set; }

    /// <summary>每级冷却(CSV格式: "5,4.5,4,3.5,3")</summary>
    public string CooldownPerLevel { get; set; } = "";
    /// <summary>每级伤害倍率(CSV格式)</summary>
    public string DamageMultiplierPerLevel { get; set; } = "";
    /// <summary>每级范围(CSV格式)</summary>
    public string RangePerLevel { get; set; } = "";

    /// <summary>进化名称</summary>
    public string EvolutionName { get; set; } = "";
    /// <summary>进化描述</summary>
    public string EvolutionDescription { get; set; } = "";
    /// <summary>进化伤害倍率</summary>
    public float EvolutionDamageMultiplier { get; set; }
    /// <summary>进化额外投射物数量</summary>
    public int EvolutionProjectileBonus { get; set; }
}

// ========== 3. 敌人基础属性表 ==========

/// <summary>
/// 敌人基础属性表 — 敌人数值与缩放公式
/// </summary>
public class GameEnemyBase
{
    public int Id { get; set; }
    /// <summary>敌人类型(0=近战 1=远程 2=精英 3=Boss)</summary>
    public int EnemyType { get; set; }

    /// <summary>基础生命值</summary>
    public int BaseHp { get; set; }
    /// <summary>基础攻击力</summary>
    public int BaseAttack { get; set; }
    /// <summary>基础防御力</summary>
    public int BaseDefense { get; set; }
    /// <summary>基础移动速度</summary>
    public float BaseMoveSpeed { get; set; }
    /// <summary>基础经验奖励</summary>
    public int BaseXpReward { get; set; }
    /// <summary>攻击范围</summary>
    public float AttackRange { get; set; }
    /// <summary>攻击冷却(秒)</summary>
    public float AttackCooldown { get; set; }

    // 缩放公式参数
    public float ScaleEarly { get; set; }
    public float ScaleEarlyStep { get; set; }
    public float ScaleMid { get; set; }
    public float ScaleMidStep { get; set; }
    public float ScaleLate { get; set; }
    public float ScaleLateStep { get; set; }

    public float MoveSpeedEarlyMult { get; set; }
    public float MoveSpeedMidStep { get; set; }
    public float MoveSpeedLateBase { get; set; }
    public float MoveSpeedLateStep { get; set; }

    /// <summary>敌人攻击力倍率</summary>
    public float EnemyAttackMult { get; set; }

    // Boss/精英 倍率
    public float BossHpMult { get; set; }
    public float BossAtkMult { get; set; }
    public int BossDefBonusBase { get; set; }
    public float BossXpMult { get; set; }
    public float EliteHpMult { get; set; }
    public float EliteAtkMult { get; set; }
    public float EliteXpMult { get; set; }

    // 攻速缩放
    public int AttackCdScaleStartLevel { get; set; }
    public float AttackCdScalePerLevel { get; set; }
    public float AttackCdScaleMin { get; set; }

    // 召唤物属性
    public int MinionBaseHp { get; set; }
    public int MinionBaseAttack { get; set; }
    public float MinionBaseMoveSpeed { get; set; }
    public int MinionBaseXpReward { get; set; }
}

// ========== 4. 精英词缀表 ==========

/// <summary>
/// 精英词缀表 — 精英怪特殊属性
/// </summary>
public class GameEliteAffix
{
    public int Id { get; set; }
    /// <summary>词缀类型(0-7)</summary>
    public int AffixType { get; set; }
    /// <summary>词缀名称</summary>
    public string Name { get; set; } = "";
    /// <summary>词缀描述</summary>
    public string Description { get; set; } = "";

    public float AuraRadius { get; set; }
    public int AuraDamage { get; set; }
    public float TeleportCooldown { get; set; }
    public float TeleportRange { get; set; }
    public int ShieldHp { get; set; }
    public float ShieldRegenDelay { get; set; }
    public float SpawnInterval { get; set; }
    public int SpawnCount { get; set; }
    public float HasteSpeedMult { get; set; }
    public float HasteAttackMult { get; set; }
    public float ReflectPct { get; set; }
    public int RegenPerSecond { get; set; }
    public float ChainBuffRadius { get; set; }
    public float ChainBuffDuration { get; set; }

    public float AffixScaleBase { get; set; }
    public float AffixScalePerLevel { get; set; }

    // 颜色
    public float ColorR { get; set; }
    public float ColorG { get; set; }
    public float ColorB { get; set; }
    public float ColorA { get; set; }
}

// ========== 5. 符文数据表 ==========

/// <summary>
/// 符文数据表 — 技能强化符文
/// </summary>
public class GameRuneData
{
    public int Id { get; set; }
    /// <summary>符文类型(0-7)</summary>
    public int RuneType { get; set; }
    /// <summary>符文名称</summary>
    public string Name { get; set; } = "";
    /// <summary>符文描述</summary>
    public string Description { get; set; } = "";

    public float DamageMultBase { get; set; }
    public float DamageMultPerLevel { get; set; }
    public float DamageMultMin { get; set; }

    public int ProjectileBonus { get; set; }
    public int PierceBase { get; set; }
    public int PiercePerLevel { get; set; }
    public int ChainBase { get; set; }
    public int ChainPerLevel { get; set; }

    public float AoeRangeMultBase { get; set; }
    public float AoeRangeMultPerLevel { get; set; }

    public float LifeStealBase { get; set; }
    public float LifeStealPerLevel { get; set; }

    public float SlowPctBase { get; set; }
    public float SlowPctPerLevel { get; set; }
    public float SlowDurationBase { get; set; }
    public float SlowDurationPerLevel { get; set; }

    public float BurnDpsBase { get; set; }
    public float BurnDpsPerLevel { get; set; }
    public float BurnDurationBase { get; set; }
    public float BurnDurationPerLevel { get; set; }

    // 颜色
    public float ColorR { get; set; }
    public float ColorG { get; set; }
    public float ColorB { get; set; }
}

// ========== 6. 遗物数据表 ==========

/// <summary>
/// 遗物数据表 — 随机掉落遗物
/// </summary>
public class GameRelicData
{
    public int Id { get; set; }
    /// <summary>遗物类型(0-9)</summary>
    public int RelicType { get; set; }
    /// <summary>遗物名称</summary>
    public string Name { get; set; } = "";
    /// <summary>遗物描述</summary>
    public string Description { get; set; } = "";
    /// <summary>稀有度(0=普通 1=稀有 2=传说)</summary>
    public int Rarity { get; set; }

    public int SplitCount { get; set; }
    public int KillHealAmount { get; set; }
    public float CritBurstRadius { get; set; }
    public float SpeedBoostDuration { get; set; }
    public float SpeedBoostMult { get; set; }
    public float BloodMagicDamageBonus { get; set; }
    public float BloodMagicHpCostPct { get; set; }
    public int ChainTargets { get; set; }
    public float ThornsPct { get; set; }
    public float DoubleStrikeChance { get; set; }
    public float DodgeHealChance { get; set; }
    public int DodgeHealAmount { get; set; }
    public float XpBonus { get; set; }

    // 颜色
    public float ColorR { get; set; }
    public float ColorG { get; set; }
    public float ColorB { get; set; }

    // 掉落概率
    public float RollLegendaryBase { get; set; }
    public float RollLegendaryPerStage { get; set; }
    public float RollRareBase { get; set; }
    public float RollRarePerStage { get; set; }
}

// ========== 7. 装备配置表 ==========

/// <summary>
/// 装备配置表 — 各稀有度装备数值
/// </summary>
public class GameEquipmentConfig
{
    public int Id { get; set; }
    /// <summary>稀有度(0=普通 1=稀有 2=史诗 3=传说)</summary>
    public int Rarity { get; set; }
    /// <summary>品质倍率</summary>
    public float TierMultiplier { get; set; }
    /// <summary>基础售价</summary>
    public int SellPriceBase { get; set; }
    /// <summary>基础强化费用</summary>
    public int UpgradeBaseCost { get; set; }
    /// <summary>强化加成百分比</summary>
    public float UpgradeBoostPct { get; set; }
    /// <summary>强化售价倍率</summary>
    public float SellPriceUpgradeMult { get; set; }
}

// ========== 8. 全局配置表 (K-V) ==========

/// <summary>
/// 全局配置表 — 键值对配置
/// </summary>
public class GameGlobalConfig
{
    public int Id { get; set; }
    [MaxLength(64)]
    public string ConfigKey { get; set; } = "";
    public string ConfigValue { get; set; } = "";
    public string? Description { get; set; }
}

// ========== API 响应 DTO ==========

/// <summary>全局配置项 (键值对)</summary>
public class GameConfigGlobalItem
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

/// <summary>游戏配置聚合响应 — 包含所有配置表数据</summary>
public class GameConfigResponse
{
    public List<GameClassStats> Classes { get; set; } = new();
    public List<GameSkillData> Skills { get; set; } = new();
    public List<GameEnemyBase> Enemies { get; set; } = new();
    public List<GameEliteAffix> Affixes { get; set; } = new();
    public List<GameRuneData> Runes { get; set; } = new();
    public List<GameRelicData> Relics { get; set; } = new();
    public List<GameEquipmentConfig> Equipment { get; set; } = new();
    public List<GameConfigGlobalItem> Global { get; set; } = new();
}
