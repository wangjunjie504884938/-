using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 被动技能树节点类型
/// </summary>
public enum PassiveNodeType
{
    Minor,      // 小节点: 微量属性提升
    Notable,    // 显著节点: 改变build的重要加成
    Keystone    // 关键节点: 大有优大劣的trade-off
}

/// <summary>
/// 被动技能树节点 — PoE2风格的被动树
/// </summary>
[System.Serializable]
public class PassiveNode
{
    public int Id;
    public string Name;
    public string Description;
    public PassiveNodeType Type;

    // 节点效果 (可叠加多个)
    public float MaxHpBonus;
    public float AttackBonus;
    public float DefenseBonus;
    public float MoveSpeedBonus;
    public float CritChanceBonus;
    public float CritDamageBonus;
    public float AttackSpeedBonus;
    public float AttackRangeBonus;
    public float LifeStealBonus;
    public float HpRegenBonus;
    public float XpMultiplierBonus;
    public float SpiritMaxBonus;
    public float SpiritRegenBonus;

    // Keystone 特殊效果
    public bool BloodMagic;      // 技能消耗生命替代精神，伤害+40%
    public bool ResoluteTechnique; // 永不暴击，但命中率+100%（这里改为命中+30%攻击）
    public bool ElementalOverload;  // 元素伤害+50%，物理-30%
    public bool ZealousOath;        // 每秒回血+3，最大生命-20%
    public bool MindOverMatter;     // 30%伤害从精神承受

    // 连接的节点ID (树结构)
    public List<int> ConnectedNodeIds = new List<int>();

    // 运行时状态
    public bool Allocated;

    /// <summary>
    /// 获取节点效果摘要文本
    /// </summary>
    public string GetEffectText()
    {
        var parts = new List<string>();
        if (MaxHpBonus > 0) parts.Add($"+{(int)MaxHpBonus}最大生命");
        if (AttackBonus > 0) parts.Add($"+{(int)AttackBonus}攻击");
        if (DefenseBonus > 0) parts.Add($"+{(int)DefenseBonus}防御");
        if (MoveSpeedBonus > 0) parts.Add($"+{MoveSpeedBonus:F1}移速");
        if (CritChanceBonus > 0) parts.Add($"+{CritChanceBonus * 100:F0}%暴击");
        if (CritDamageBonus > 0) parts.Add($"+{CritDamageBonus * 100:F0}%暴击伤害");
        if (AttackSpeedBonus > 0) parts.Add($"+{AttackSpeedBonus * 100:F0}%攻速");
        if (AttackRangeBonus > 0) parts.Add($"+{AttackRangeBonus:F1}范围");
        if (LifeStealBonus > 0) parts.Add($"+{LifeStealBonus * 100:F0}%吸血");
        if (HpRegenBonus > 0) parts.Add($"+{HpRegenBonus:F1}回血/s");
        if (XpMultiplierBonus > 0) parts.Add($"+{XpMultiplierBonus * 100:F0}%经验");
        if (SpiritMaxBonus > 0) parts.Add($"+{(int)SpiritMaxBonus}精神上限");
        if (SpiritRegenBonus > 0) parts.Add($"+{SpiritRegenBonus:F1}精神恢复/s");
        if (BloodMagic) parts.Add("血魔法: 技能耗血, 伤害+40%");
        if (ResoluteTechnique) parts.Add("坚定: +30%攻击, 无法暴击");
        if (ElementalOverload) parts.Add("元素超载: 元素伤害+50%, 物理-30%");
        if (ZealousOath) parts.Add("狂热之誓: 回血+3/s, 生命-20%");
        if (MindOverMatter) parts.Add("心胜于物: 30%伤害从精神承受");
        return string.Join(" ", parts);
    }
}
