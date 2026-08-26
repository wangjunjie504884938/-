using UnityEngine;
using System.Collections.Generic;

public enum HeroClass
{
    Warrior,
    Mage,
    Priest
}

public enum SkillSlot
{
    Skill1,
    Skill2,
    Skill3
}

[System.Serializable]
public class ClassData
{
    public HeroClass Class;
    public string ClassName;
    public string Description;
    public string Skill1Name;
    public string Skill2Name;
    public string Skill3Name;

    // Base stats at level 1
    public int BaseHp;
    public int BaseAttack;
    public int BaseDefense;
    public float BaseMoveSpeed;
    public float BaseAttackRange;
    public float BaseAttackCooldown;
    public float BaseCritChance;
    public float BaseHpRegen;

    // Per-level growth
    public int HpPerLevel;
    public int AttackPerLevel;
    public int DefensePerLevel;

    // Auto-attack type
    public bool IsRanged;
    public float AutoAttackRange;

    // Colors
    public Color PrimaryColor;
    public Color SecondaryColor;
    public Color SkillColor1;
    public Color SkillColor2;
    public Color SkillColor3;

    public static ClassData GetClassData(HeroClass heroClass)
    {
        // 优先从数据库配置读取
        if (GameConfigManager.Instance != null && GameConfigManager.Instance.IsLoaded)
        {
            var dto = GameConfigManager.Instance.GetClassData(heroClass);
            if (dto != null)
            {
                return new ClassData
                {
                    Class = (HeroClass)dto.classId,
                    ClassName = dto.className,
                    Description = dto.description,
                    Skill1Name = dto.skill1Name,
                    Skill2Name = dto.skill2Name,
                    Skill3Name = dto.skill3Name,
                    BaseHp = dto.baseHp,
                    BaseAttack = dto.baseAttack,
                    BaseDefense = dto.baseDefense,
                    BaseMoveSpeed = dto.baseMoveSpeed,
                    BaseAttackRange = dto.baseAttackRange,
                    BaseAttackCooldown = dto.baseAttackCooldown,
                    BaseCritChance = dto.baseCritChance,
                    BaseHpRegen = dto.baseHpRegen,
                    HpPerLevel = dto.hpPerLevel,
                    AttackPerLevel = dto.attackPerLevel,
                    DefensePerLevel = dto.defensePerLevel,
                    IsRanged = dto.isRanged,
                    AutoAttackRange = dto.autoAttackRange,
                    PrimaryColor = new Color(dto.primaryR, dto.primaryG, dto.primaryB, dto.primaryA),
                    SecondaryColor = new Color(dto.secondaryR, dto.secondaryG, dto.secondaryB, dto.secondaryA),
                    SkillColor1 = new Color(dto.skill1R, dto.skill1G, dto.skill1B, dto.skill1A),
                    SkillColor2 = new Color(dto.skill2R, dto.skill2G, dto.skill2B, dto.skill2A),
                    SkillColor3 = new Color(dto.skill3R, dto.skill3G, dto.skill3B, dto.skill3A)
                };
            }
        }

        // 回退到硬编码默认值
        return GetClassDataDefault(heroClass);
    }

    private static ClassData GetClassDataDefault(HeroClass heroClass)
    {
        return heroClass switch
        {
            HeroClass.Warrior => new ClassData
            {
                Class = HeroClass.Warrior,
                ClassName = "战士",
                Description = "近战重甲，高生命高防御\n旋风斩/盾击/战吼",
                Skill1Name = "旋风斩",
                Skill2Name = "盾击",
                Skill3Name = "战吼",
                BaseHp = 250,
                BaseAttack = 18,
                BaseDefense = 8,
                BaseMoveSpeed = 5.5f,
                BaseAttackRange = 1.0f,
                BaseAttackCooldown = 0.35f,
                BaseCritChance = 0.08f,
                BaseHpRegen = 0.5f,
                HpPerLevel = 18,
                AttackPerLevel = 3,
                DefensePerLevel = 2,
                IsRanged = false,
                AutoAttackRange = 1.0f,
                PrimaryColor = new Color(0.85f, 0.2f, 0.2f),
                SecondaryColor = new Color(0.7f, 0.65f, 0.5f),
                SkillColor1 = new Color(1f, 0.5f, 0.2f),
                SkillColor2 = new Color(0.6f, 0.7f, 1f),
                SkillColor3 = new Color(1f, 0.9f, 0.3f)
            },
            HeroClass.Mage => new ClassData
            {
                Class = HeroClass.Mage,
                ClassName = "法师",
                Description = "远程魔法，高攻击低生命\n火球/冰新星/奥术冲击",
                Skill1Name = "火球术",
                Skill2Name = "冰新星",
                Skill3Name = "奥术冲击",
                BaseHp = 120,
                BaseAttack = 25,
                BaseDefense = 3,
                BaseMoveSpeed = 5.8f,
                BaseAttackRange = 1.5f,
                BaseAttackCooldown = 0.5f,
                BaseCritChance = 0.12f,
                BaseHpRegen = 0.2f,
                HpPerLevel = 8,
                AttackPerLevel = 5,
                DefensePerLevel = 1,
                IsRanged = true,
                AutoAttackRange = 1.5f,
                PrimaryColor = new Color(0.3f, 0.4f, 0.9f),
                SecondaryColor = new Color(0.7f, 0.5f, 1f),
                SkillColor1 = new Color(1f, 0.4f, 0.1f),
                SkillColor2 = new Color(0.4f, 0.8f, 1f),
                SkillColor3 = new Color(0.7f, 0.3f, 1f)
            },
            HeroClass.Priest => new ClassData
            {
                Class = HeroClass.Priest,
                ClassName = "牧师",
                Description = "治疗辅助，攻守兼备\n圣光弹/治疗术/神圣护盾",
                Skill1Name = "圣光弹",
                Skill2Name = "治疗术",
                Skill3Name = "神圣护盾",
                BaseHp = 180,
                BaseAttack = 15,
                BaseDefense = 5,
                BaseMoveSpeed = 5.6f,
                BaseAttackRange = 1.2f,
                BaseAttackCooldown = 0.45f,
                BaseCritChance = 0.06f,
                BaseHpRegen = 1.0f,
                HpPerLevel = 12,
                AttackPerLevel = 3,
                DefensePerLevel = 1,
                IsRanged = true,
                AutoAttackRange = 1.2f,
                PrimaryColor = new Color(1f, 0.95f, 0.6f),
                SecondaryColor = new Color(0.95f, 0.9f, 0.7f),
                SkillColor1 = new Color(1f, 1f, 0.5f),
                SkillColor2 = new Color(0.3f, 1f, 0.5f),
                SkillColor3 = new Color(0.8f, 0.9f, 1f)
            },
            _ => new ClassData
            {
                ClassName = "未知",
                Description = "未知职业",
                Skill1Name = "技能1", Skill2Name = "技能2", Skill3Name = "技能3",
                BaseHp = 200, BaseAttack = 15, BaseDefense = 5,
                BaseMoveSpeed = 5.5f, BaseAttackRange = 1.2f, BaseAttackCooldown = 0.4f,
                BaseCritChance = 0.08f, BaseHpRegen = 0.5f,
                HpPerLevel = 10, AttackPerLevel = 3, DefensePerLevel = 1,
                IsRanged = false, AutoAttackRange = 1.2f,
                PrimaryColor = new Color(0.5f, 0.5f, 0.5f),
                SecondaryColor = new Color(0.5f, 0.5f, 0.5f),
                SkillColor1 = new Color(0.5f, 0.5f, 0.5f),
                SkillColor2 = new Color(0.5f, 0.5f, 0.5f),
                SkillColor3 = new Color(0.5f, 0.5f, 0.5f)
            }
        };
    }
}

[System.Serializable]
public class SkillData
{
    public string Name;
    public SkillSlot Slot;
    public HeroClass RequiredClass;
    public float Cooldown;
    public float Range;
    public float DamageMultiplier;
    public string Description;

    // Per-level values: index 0 = level 1, index 4 = level 5
    public float[] CooldownPerLevel;
    public float[] DamageMultiplierPerLevel;
    public float[] RangePerLevel;

    // ===== 符文系统 =====
    public const int MaxRuneSlots = 2;
    public List<RuneData> EquippedRunes = new List<RuneData>();

    public int CurrentLevel { get; set; } = 1;
    public int MaxLevel => 5;
    public bool CanUpgrade => CurrentLevel < MaxLevel;

    public float CurrentCooldown => CooldownPerLevel != null && CurrentLevel >= 1 && CurrentLevel - 1 < CooldownPerLevel.Length
        ? CooldownPerLevel[CurrentLevel - 1] : Cooldown;
    public float CurrentDamageMultiplier => DamageMultiplierPerLevel != null && CurrentLevel >= 1 && CurrentLevel - 1 < DamageMultiplierPerLevel.Length
        ? DamageMultiplierPerLevel[CurrentLevel - 1] : DamageMultiplier;
    public float CurrentRange => RangePerLevel != null && CurrentLevel >= 1 && CurrentLevel - 1 < RangePerLevel.Length
        ? RangePerLevel[CurrentLevel - 1] : Range;

    public int UpgradeCost => CurrentLevel * 1; // Skill point cost

    /// <summary>Gold cost to upgrade with gold instead of skill points.</summary>
    public int GoldUpgradeCost => CurrentLevel * 500;

    /// <summary>Next-level damage multiplier (for preview). Returns current if maxed.</summary>
    public float NextDamageMultiplier => DamageMultiplierPerLevel != null && CurrentLevel < MaxLevel && CurrentLevel < DamageMultiplierPerLevel.Length
        ? DamageMultiplierPerLevel[CurrentLevel] : CurrentDamageMultiplier;

    /// <summary>Next-level cooldown (for preview).</summary>
    public float NextCooldown => CooldownPerLevel != null && CurrentLevel < MaxLevel && CurrentLevel < CooldownPerLevel.Length
        ? CooldownPerLevel[CurrentLevel] : CurrentCooldown;

    /// <summary>Next-level range (for preview).</summary>
    public float NextRange => RangePerLevel != null && CurrentLevel < MaxLevel && CurrentLevel < RangePerLevel.Length
        ? RangePerLevel[CurrentLevel] : CurrentRange;

    // ===== 技能进化 (Level 5) =====
    public string EvolutionName;
    public string EvolutionDescription;
    public float EvolutionDamageMultiplier;
    public int EvolutionProjectileBonus;

    // 符文派生属性
    /// <summary>符文修正后的伤害倍率（所有符文的 DamageMultiplier 相乘）</summary>
    public float RuneDamageMultiplier
    {
        get
        {
            float mult = 1f;
            foreach (var rune in EquippedRunes) mult *= rune.DamageMultiplier;
            return mult;
        }
    }

    /// <summary>额外投射物数量（来自散射符文）</summary>
    public int ProjectileBonus { get { int b = 0; foreach (var r in EquippedRunes) b += r.ProjectileBonus; return b; } }

    /// <summary>穿透次数（来自穿透符文）</summary>
    public int PierceCount { get { int p = 0; foreach (var r in EquippedRunes) p += r.PierceCount; return p; } }

    /// <summary>连锁弹射次数（来自连锁符文）</summary>
    public int ChainCount { get { int c = 0; foreach (var r in EquippedRunes) c += r.ChainCount; return c; } }

    /// <summary>AOE范围倍率（来自范围扩大符文，默认1.0）</summary>
    public float AoeRangeMultiplier { get { float m = 1f; foreach (var r in EquippedRunes) if (r != null && r.Type == RuneType.AreaExpand) m *= r.AoeRangeMultiplier; return m; } }

    /// <summary>是否有燃烧效果</summary>
    public bool HasBurn { get { foreach (var r in EquippedRunes) if (r.BurnDps > 0) return true; return false; } }
    public float BurnDps { get { float d = 0; foreach (var r in EquippedRunes) d += r.BurnDps; return d; } }
    public float BurnDuration { get { float d = 0; foreach (var r in EquippedRunes) d = Mathf.Max(d, r.BurnDuration); return d; } }

    /// <summary>吸血百分比（来自吸血符文）</summary>
    public float LifeStealPct { get { float p = 0; foreach (var r in EquippedRunes) p += r.LifeStealPct; return p; } }

    /// <summary>是否有减速效果</summary>
    public bool HasSlow { get { foreach (var r in EquippedRunes) if (r.SlowPct > 0) return true; return false; } }
    public float SlowPct { get { float p = 0; foreach (var r in EquippedRunes) p = Mathf.Max(p, r.SlowPct); return p; } }
    public float SlowDuration { get { float d = 0; foreach (var r in EquippedRunes) d = Mathf.Max(d, r.SlowDuration); return d; } }

    /// <summary>是否有元素转换</summary>
    public bool HasElementConvert { get { foreach (var r in EquippedRunes) if (r.Type == RuneType.ElementConvert) return true; return false; } }

    /// <summary>符文组合颜色（用于VFX着色）</summary>
    public Color RuneColor
    {
        get
        {
            if (EquippedRunes.Count == 0) return Color.white;
            Color c = Color.white;
            foreach (var r in EquippedRunes) c = Color.Lerp(c, r.RuneColor, 0.6f);
            return c;
        }
    }

    // ===== 符文操作 =====
    public bool EquipRune(RuneData rune)
    {
        if (EquippedRunes.Count >= MaxRuneSlots) return false;
        if (EquippedRunes.Exists(r => r.Type == rune.Type)) return false;
        EquippedRunes.Add(rune);
        return true;
    }

    public bool UnequipRune(RuneType type)
    {
        for (int i = 0; i < EquippedRunes.Count; i++)
        {
            if (EquippedRunes[i].Type == type)
            {
                EquippedRunes.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    public bool HasRune(RuneType type) => EquippedRunes.Exists(r => r.Type == type);

    // ===== 技能变异系统 =====
    public List<ActiveMutation> ActiveMutations { get; private set; } = new List<ActiveMutation>();

    /// <summary>获取指定变异的叠加层数（0表示未激活）。</summary>
    public int GetMutationStacks(string mutationId)
    {
        foreach (var am in ActiveMutations)
            if (am.Mutation != null && am.Mutation.Id == mutationId) return am.Stacks;
        return 0;
    }

    /// <summary>是否拥有指定变异。</summary>
    public bool HasMutation(string mutationId) => GetMutationStacks(mutationId) > 0;

    /// <summary>获取指定变异的激活实例，未找到返回null。</summary>
    public ActiveMutation GetMutation(string mutationId)
    {
        foreach (var am in ActiveMutations)
            if (am.Mutation != null && am.Mutation.Id == mutationId) return am;
        return null;
    }

    /// <summary>尝试解锁或叠加变异，返回是否成功。</summary>
    public bool TryAddMutation(SkillMutation mutation)
    {
        if (mutation == null) return false;
        var existing = GetMutation(mutation.Id);
        if (existing != null)
        {
            if (!existing.CanStack) return false;
            existing.AddStack();
            return true;
        }
        ActiveMutations.Add(new ActiveMutation(mutation));
        return true;
    }

    /// <summary>清除所有变异（每局结束时调用）。</summary>
    public void ResetMutations()
    {
        ActiveMutations.Clear();
    }

    public static List<SkillData> GetClassSkills(HeroClass heroClass)
    {
        // 优先从数据库配置读取
        if (GameConfigManager.Instance != null && GameConfigManager.Instance.IsLoaded)
        {
            var dtos = GameConfigManager.Instance.GetClassSkills(heroClass);
            if (dtos != null && dtos.Count > 0)
            {
                var result = new List<SkillData>();
                foreach (var dto in dtos)
                {
                    result.Add(new SkillData
                    {
                        Name = dto.name,
                        Slot = (SkillSlot)dto.skillSlot,
                        RequiredClass = (HeroClass)dto.classId,
                        Cooldown = dto.cooldown,
                        Range = dto.range,
                        DamageMultiplier = dto.damageMultiplier,
                        Description = dto.description,
                        CooldownPerLevel = GameConfigManager.ParseCsvFloats(dto.cooldownPerLevel),
                        DamageMultiplierPerLevel = GameConfigManager.ParseCsvFloats(dto.damageMultiplierPerLevel),
                        RangePerLevel = GameConfigManager.ParseCsvFloats(dto.rangePerLevel),
                        EvolutionName = dto.evolutionName,
                        EvolutionDescription = dto.evolutionDescription,
                        EvolutionDamageMultiplier = dto.evolutionDamageMultiplier,
                        EvolutionProjectileBonus = dto.evolutionProjectileBonus
                    });
                }
                return result;
            }
        }

        // 回退到硬编码默认值
        return GetClassSkillsDefault(heroClass);
    }

    private static List<SkillData> GetClassSkillsDefault(HeroClass heroClass)
    {
        return heroClass switch
        {
            HeroClass.Warrior => new List<SkillData>
            {
                new SkillData
                {
                    Name = "旋风斩", Slot = SkillSlot.Skill1, RequiredClass = HeroClass.Warrior,
                    Cooldown = 5f, Range = 2.5f, DamageMultiplier = 1.5f,
                    Description = "旋转斩击周围所有敌人",
                    CooldownPerLevel = new[] { 5f, 4.5f, 4f, 3.5f, 3f },
                    DamageMultiplierPerLevel = new[] { 1.5f, 1.7f, 1.9f, 2.2f, 2.5f },
                    RangePerLevel = new[] { 2.5f, 2.7f, 2.9f, 3.2f, 3.5f },
                    EvolutionName = "龙卷风", EvolutionDescription = "范围+50%，吸引敌人",
                    EvolutionDamageMultiplier = 3f, EvolutionProjectileBonus = 0
                },
                new SkillData
                {
                    Name = "盾击", Slot = SkillSlot.Skill2, RequiredClass = HeroClass.Warrior,
                    Cooldown = 3f, Range = 2f, DamageMultiplier = 1.2f,
                    Description = "盾牌猛击前方敌人并击退",
                    CooldownPerLevel = new[] { 3f, 2.7f, 2.4f, 2.1f, 1.8f },
                    DamageMultiplierPerLevel = new[] { 1.2f, 1.4f, 1.6f, 1.9f, 2.2f },
                    RangePerLevel = new[] { 2f, 2.2f, 2.4f, 2.7f, 3f },
                    EvolutionName = "破甲猛击", EvolutionDescription = "无视敌人防御",
                    EvolutionDamageMultiplier = 2.8f, EvolutionProjectileBonus = 0
                },
                new SkillData
                {
                    Name = "战吼", Slot = SkillSlot.Skill3, RequiredClass = HeroClass.Warrior,
                    Cooldown = 10f, Range = 0f, DamageMultiplier = 0f,
                    Description = "提升攻击力和防御力8秒",
                    CooldownPerLevel = new[] { 10f, 9f, 8f, 7f, 6f },
                    DamageMultiplierPerLevel = new[] { 0.2f, 0.25f, 0.3f, 0.4f, 0.5f },
                    RangePerLevel = new[] { 0f, 0f, 0f, 0f, 0f },
                    EvolutionName = "狂暴", EvolutionDescription = "持续+4秒，额外+20%攻速",
                    EvolutionDamageMultiplier = 0.6f, EvolutionProjectileBonus = 0
                }
            },
            HeroClass.Mage => new List<SkillData>
            {
                new SkillData
                {
                    Name = "火球术", Slot = SkillSlot.Skill1, RequiredClass = HeroClass.Mage,
                    Cooldown = 3f, Range = 8f, DamageMultiplier = 2f,
                    Description = "发射爆炸火球，范围伤害",
                    CooldownPerLevel = new[] { 3f, 2.7f, 2.4f, 2.1f, 1.8f },
                    DamageMultiplierPerLevel = new[] { 2f, 2.3f, 2.6f, 3f, 3.5f },
                    RangePerLevel = new[] { 8f, 8.5f, 9f, 9.5f, 10f },
                    EvolutionName = "流星火雨", EvolutionDescription = "3个火球同时落下",
                    EvolutionDamageMultiplier = 4f, EvolutionProjectileBonus = 2
                },
                new SkillData
                {
                    Name = "冰新星", Slot = SkillSlot.Skill2, RequiredClass = HeroClass.Mage,
                    Cooldown = 6f, Range = 3f, DamageMultiplier = 1.8f,
                    Description = "以自身为中心释放冰霜新星",
                    CooldownPerLevel = new[] { 6f, 5.5f, 5f, 4.5f, 4f },
                    DamageMultiplierPerLevel = new[] { 1.8f, 2f, 2.3f, 2.6f, 3f },
                    RangePerLevel = new[] { 3f, 3.3f, 3.6f, 4f, 4.5f },
                    EvolutionName = "暴风雪", EvolutionDescription = "范围+50%，冰冻敌人2秒",
                    EvolutionDamageMultiplier = 3.5f, EvolutionProjectileBonus = 0
                },
                new SkillData
                {
                    Name = "奥术冲击", Slot = SkillSlot.Skill3, RequiredClass = HeroClass.Mage,
                    Cooldown = 8f, Range = 10f, DamageMultiplier = 3f,
                    Description = "蓄力发射强力奥术光束",
                    CooldownPerLevel = new[] { 8f, 7.5f, 7f, 6.5f, 6f },
                    DamageMultiplierPerLevel = new[] { 3f, 3.5f, 4f, 4.5f, 5f },
                    RangePerLevel = new[] { 10f, 10f, 11f, 11f, 12f },
                    EvolutionName = "虚空射线", EvolutionDescription = "穿透全屏，击中所有敌人",
                    EvolutionDamageMultiplier = 5.5f, EvolutionProjectileBonus = 0
                }
            },
            HeroClass.Priest => new List<SkillData>
            {
                new SkillData
                {
                    Name = "圣光弹", Slot = SkillSlot.Skill1, RequiredClass = HeroClass.Priest,
                    Cooldown = 2.5f, Range = 7f, DamageMultiplier = 1.5f,
                    Description = "发射追踪圣光弹攻击敌人",
                    CooldownPerLevel = new[] { 2.5f, 2.2f, 2f, 1.8f, 1.5f },
                    DamageMultiplierPerLevel = new[] { 1.5f, 1.7f, 1.9f, 2.2f, 2.5f },
                    RangePerLevel = new[] { 7f, 7.5f, 8f, 8.5f, 9f },
                    EvolutionName = "圣光审判", EvolutionDescription = "穿透+爆炸范围伤害",
                    EvolutionDamageMultiplier = 3f, EvolutionProjectileBonus = 0
                },
                new SkillData
                {
                    Name = "治疗术", Slot = SkillSlot.Skill2, RequiredClass = HeroClass.Priest,
                    Cooldown = 5f, Range = 0f, DamageMultiplier = 0f,
                    Description = "恢复自身大量生命值",
                    CooldownPerLevel = new[] { 5f, 4.5f, 4f, 3.5f, 3f },
                    DamageMultiplierPerLevel = new[] { 0.3f, 0.35f, 0.4f, 0.5f, 0.6f },
                    RangePerLevel = new[] { 0f, 0f, 0f, 0f, 0f },
                    EvolutionName = "群体治疗", EvolutionDescription = "治疗量+50%，同时获得护盾",
                    EvolutionDamageMultiplier = 0.9f, EvolutionProjectileBonus = 0
                },
                new SkillData
                {
                    Name = "神圣护盾", Slot = SkillSlot.Skill3, RequiredClass = HeroClass.Priest,
                    Cooldown = 12f, Range = 0f, DamageMultiplier = 0f,
                    Description = "获得护盾吸收伤害5秒",
                    CooldownPerLevel = new[] { 12f, 11f, 10f, 9f, 8f },
                    DamageMultiplierPerLevel = new[] { 0.2f, 0.25f, 0.3f, 0.4f, 0.5f },
                    RangePerLevel = new[] { 0f, 0f, 0f, 0f, 0f },
                    EvolutionName = "神圣之翼", EvolutionDescription = "护盾+反击伤害",
                    EvolutionDamageMultiplier = 0.6f, EvolutionProjectileBonus = 0
                }
            },
            _ => new List<SkillData>()
        };
    }
}
