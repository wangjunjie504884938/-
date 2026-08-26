using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 变异池 — 定义所有可用的技能变异，并提供随机抽取逻辑。
/// 每局开局时从池中随机抽取可用变异，增加rogue重玩差异性。
/// 参见 GAME_DESIGN_PLAN.md §4.1。
/// </summary>
public static class MutationPool
{
    // === 变异ID常量 ===
    // 战士 — 旋风斩
    public const string FlameWhirl = "flame_whirl";
    public const string ThunderSlash = "thunder_slash";
    public const string PullWhirl = "pull_whirl";

    // 法师 — 火球术
    public const string SplitFireball = "split_fireball";
    public const string FrostFire = "frost_fire";
    public const string ChainLightning = "chain_lightning";

    // 牧师 — 治疗术
    public const string HolyShield = "holy_shield";
    public const string HealingAura = "healing_aura";
    public const string Retribution = "retribution";

    private static readonly List<SkillMutation> AllMutations = new List<SkillMutation>
    {
        // === 战士 — 旋风斩 (Skill1) ===
        new SkillMutation(
            FlameWhirl, "烈焰旋风",
            "旋风留下火域，经过的敌人持续受到伤害",
            SkillSlot.Skill1, HeroClass.Warrior,
            MutationRarity.Powerful, 3,
            new Color(1f, 0.3f, 0.1f, 1f),
            "+1.5s火域持续时间, +20%伤害"),

        new SkillMutation(
            ThunderSlash, "雷鸣斩",
            "旋风有40%概率眩晕敌人1.5秒",
            SkillSlot.Skill1, HeroClass.Warrior,
            MutationRarity.Powerful, 3,
            new Color(0.3f, 0.5f, 1f, 1f),
            "+15%眩晕概率, +0.5s眩晕时长"),

        new SkillMutation(
            PullWhirl, "吸引旋风",
            "旋风将范围内敌人拉向中心，范围+30%",
            SkillSlot.Skill1, HeroClass.Warrior,
            MutationRarity.Normal, 3,
            new Color(0.6f, 0.2f, 0.8f, 1f),
            "+0.5拉力, +10%范围"),

        // === 法师 — 火球术 (Skill1) ===
        new SkillMutation(
            SplitFireball, "分裂火球",
            "火球命中后分裂为3个小火球，每个造成60%伤害",
            SkillSlot.Skill1, HeroClass.Mage,
            MutationRarity.Powerful, 3,
            new Color(1f, 0.5f, 0.1f, 1f),
            "+1分裂数量, +10%伤害"),

        new SkillMutation(
            FrostFire, "冰火交融",
            "火球爆炸后留下冰域3秒，减速60%，并有概率触发碎冰",
            SkillSlot.Skill1, HeroClass.Mage,
            MutationRarity.Powerful, 3,
            new Color(0.3f, 0.7f, 1f, 1f),
            "+1.5s冰域, +15%减速"),

        new SkillMutation(
            ChainLightning, "连锁闪电",
            "火球命中后跳至最近3个敌人（连锁伤害不衰减）",
            SkillSlot.Skill1, HeroClass.Mage,
            MutationRarity.Legendary, 2,
            new Color(0.5f, 0.3f, 1f, 1f),
            "+1次跳跃, 伤害不衰减"),

        // === 牧师 — 治疗术 (Skill2) ===
        new SkillMutation(
            HolyShield, "圣疗",
            "治疗同时获得30%最大HP护盾（4秒）",
            SkillSlot.Skill2, HeroClass.Priest,
            MutationRarity.Powerful, 3,
            new Color(0.9f, 0.9f, 0.3f, 1f),
            "+15%护盾比例, +1s持续时间"),

        new SkillMutation(
            HealingAura, "光环",
            "治疗留下4秒光环，范围内每秒回复5%最大HP",
            SkillSlot.Skill2, HeroClass.Priest,
            MutationRarity.Powerful, 3,
            new Color(0.3f, 1f, 0.5f, 1f),
            "+1.5s光环, +2%回复/s"),

        new SkillMutation(
            Retribution, "惩戒",
            "治疗时对周围敌人造成治疗量80%神圣伤害，并净化其增益",
            SkillSlot.Skill2, HeroClass.Priest,
            MutationRarity.Legendary, 2,
            new Color(1f, 0.4f, 0.3f, 1f),
            "+30%伤害转化, 范围+2"),
    };

    /// <summary>获取指定职业的所有变异。</summary>
    public static List<SkillMutation> GetMutationsForClass(HeroClass heroClass)
    {
        var result = new List<SkillMutation>();
        foreach (var m in AllMutations)
            if (m.TargetClass == heroClass)
                result.Add(m);
        return result;
    }

    /// <summary>获取指定职业+技能槽位的变异。</summary>
    public static List<SkillMutation> GetMutationsForSkill(HeroClass heroClass, SkillSlot slot)
    {
        var result = new List<SkillMutation>();
        foreach (var m in AllMutations)
            if (m.TargetClass == heroClass && m.TargetSlot == slot)
                result.Add(m);
        return result;
    }

    /// <summary>获取指定ID的变异，未找到返回null。</summary>
    public static SkillMutation GetMutation(string id)
    {
        foreach (var m in AllMutations)
            if (m.Id == id)
                return m;
        return null;
    }

    /// <summary>
    /// 每局开局随机抽取可用变异池。
    /// 从职业全部变异中随机抽取指定数量，保证rogue差异性。
    /// </summary>
    /// <param name="heroClass">当前职业</param>
    /// <param name="count">抽取数量（默认全部，可减少以增加限制感）</param>
    public static List<SkillMutation> RollRunPool(HeroClass heroClass, int count = -1)
    {
        var classMutations = GetMutationsForClass(heroClass);
        if (count <= 0 || count >= classMutations.Count)
            return new List<SkillMutation>(classMutations);

        // 加权随机抽取（不重复）
        var pool = new List<SkillMutation>(classMutations);
        var result = new List<SkillMutation>();

        while (result.Count < count && pool.Count > 0)
        {
            float totalWeight = 0f;
            foreach (var m in pool)
                totalWeight += m.RollWeight;

            float roll = Random.Range(0f, totalWeight);
            float cumulative = 0f;
            int selectedIdx = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                cumulative += pool[i].RollWeight;
                if (roll <= cumulative)
                {
                    selectedIdx = i;
                    break;
                }
            }

            result.Add(pool[selectedIdx]);
            pool.RemoveAt(selectedIdx);
        }

        return result;
    }

    /// <summary>
    /// 从可用变异池中随机抽取升级选项（3选1）。
    /// 已满层的变异不会出现。
    /// </summary>
    public static List<SkillMutation> RollUpgradeChoices(
        List<SkillMutation> availablePool,
        List<SkillData> skills,
        int choiceCount = 3)
    {
        var candidates = new List<SkillMutation>();

        foreach (var mutation in availablePool)
        {
            // 找到对应技能
            SkillData skill = null;
            foreach (var s in skills)
            {
                if (s.Slot == mutation.TargetSlot && s.RequiredClass == mutation.TargetClass)
                {
                    skill = s;
                    break;
                }
            }

            if (skill == null) continue;

            // 检查是否已满层
            var active = skill.GetMutation(mutation.Id);
            if (active != null && !active.CanStack) continue;

            candidates.Add(mutation);
        }

        if (candidates.Count <= choiceCount)
            return new List<SkillMutation>(candidates);

        // 随机抽取
        var pool = new List<SkillMutation>(candidates);
        var result = new List<SkillMutation>();
        while (result.Count < choiceCount && pool.Count > 0)
        {
            float totalWeight = 0f;
            foreach (var m in pool)
                totalWeight += m.RollWeight;

            float roll = Random.Range(0f, totalWeight);
            float cumulative = 0f;
            int selectedIdx = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                cumulative += pool[i].RollWeight;
                if (roll <= cumulative)
                {
                    selectedIdx = i;
                    break;
                }
            }

            result.Add(pool[selectedIdx]);
            pool.RemoveAt(selectedIdx);
        }

        return result;
    }
}
