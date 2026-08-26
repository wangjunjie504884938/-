using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 变异稀有度 — 决定出现概率和叠加上限。
/// </summary>
public enum MutationRarity
{
    Normal,       // 单属性增强，60%概率，最多3层
    Powerful,     // 机制改变，30%概率，最多2层
    Legendary     // 质变，10%概率，最多1层
}

/// <summary>
/// 技能变异定义 — 每局可解锁的技能改变效果，可叠加。
/// 参见 GAME_DESIGN_PLAN.md §4.1 技能变异系统。
/// </summary>
public class SkillMutation
{
    public string Id { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public SkillSlot TargetSlot { get; }
    public HeroClass TargetClass { get; }
    public MutationRarity Rarity { get; }
    public int MaxStacks { get; }
    public Color IconColor { get; }

    /// <summary>每层叠加的效果描述（用于UI提示）。</summary>
    public string StackEffect { get; }

    public SkillMutation(string id, string displayName, string description,
        SkillSlot targetSlot, HeroClass targetClass,
        MutationRarity rarity, int maxStacks, Color iconColor,
        string stackEffect = "")
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
        TargetSlot = targetSlot;
        TargetClass = targetClass;
        Rarity = rarity;
        MaxStacks = maxStacks;
        IconColor = iconColor;
        StackEffect = stackEffect;
    }

    public float RollWeight => Rarity switch
    {
        MutationRarity.Normal => 60f,
        MutationRarity.Powerful => 30f,
        MutationRarity.Legendary => 10f,
        _ => 60f
    };

    public string GetStackDescription(int stacks)
    {
        if (stacks <= 0) return Description;
        return $"{Description} (×{stacks})";
    }
}

/// <summary>
/// 已激活的变异实例 — 跟踪当前叠加层数。
/// </summary>
public class ActiveMutation
{
    public SkillMutation Mutation { get; }
    public int Stacks { get; private set; }

    public bool CanStack => Stacks < Mutation.MaxStacks;

    public ActiveMutation(SkillMutation mutation)
    {
        Mutation = mutation;
        Stacks = 1;
    }

    /// <summary>叠加一层，返回是否成功。</summary>
    public bool AddStack()
    {
        if (!CanStack) return false;
        Stacks++;
        return true;
    }
}
