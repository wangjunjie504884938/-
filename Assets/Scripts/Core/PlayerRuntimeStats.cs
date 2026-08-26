using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Live combat stats (non-persistent). Includes bonus, buffs, shields, combo.
/// </summary>
[System.Serializable]
public class PlayerRuntimeStats
{
    public int Hp;
    public float HpRegen;
    public float XpMultiplier;
    public float LifeSteal;
    public float AttackSpeed;

    // Spirit system deprecated — skills now use cooldown timers
    public float Spirit;
    public float MaxSpirit = 100f;
    public float SpiritRegen = 12f;

    public int BonusAttack;
    public int BonusDefense;
    public int BonusMaxHp;
    public float BonusMoveSpeed;
    public float BonusCritChance;
    public float BonusLifeSteal;
    public float BonusAttackRange;
    public float BonusAttackSpeed;

    // Potion system
    public int Potions = 3;
    public const int MaxPotions = 5;

    public float BuffAttackMult = 1f;
    public float BuffDefenseMult = 1f;
    public float BuffEndTime;
    public int ShieldHp;
    public float ShieldEndTime;

    private float _lastDamageTime;
    private const float InvincibilityTime = 0.5f;
    private PlayerProgressData _progress;

    public PlayerRuntimeStats(PlayerProgressData progress)
    {
        _progress = progress;
        Hp = progress.BaseMaxHp;
        HpRegen = progress.BaseHpRegen;
        XpMultiplier = 1f;
        LifeSteal = 0f;
        AttackSpeed = 1f;
    }

    public int TotalAttack => _progress.BaseAttack + BonusAttack + GetDailyAttackBonus();
    public int TotalDefense => _progress.BaseDefense + BonusDefense - GetDailyDefensePenalty();
    public int TotalMaxHp => _progress.BaseMaxHp + BonusMaxHp;
    public float TotalMoveSpeed => _progress.BaseMoveSpeed + BonusMoveSpeed + GetDailySpeedBonus();
    public float TotalCritChance => _progress.BaseCritChance + BonusCritChance;
    public float TotalLifeSteal => LifeSteal + BonusLifeSteal + GetDailyVampireBonus();
    public float TotalAttackRange
    {
        get
        {
            var player = GameManager.Instance?.Player;
            var cls = player != null ? player.HeroClass : HeroClass.Warrior;

            // 基础范围 = 地图对角线 × 8%
            int stageIdx = GameManager.Instance != null ? GameManager.Instance.CurrentStageIndex : 0;
            float mapBase = DungeonMapData.GetMapDiagonal(stageIdx) * 0.08f;

            switch (cls)
            {
                case HeroClass.Warrior:
                    // 战士普攻 = 基础×0.6，封顶5m，不受装备加成
                    return Mathf.Min(mapBase * 0.6f, 5f);

                case HeroClass.Priest:
                    // 牧师普攻 = 基础×1.0×(1+装备加成)，封顶12m
                    {
                        float baseRange = mapBase * 1.0f;
                        float bonusPct = BonusAttackRange / Mathf.Max(baseRange, 0.1f);
                        float rawRange = baseRange * (1f + bonusPct);
                        return Mathf.Clamp(ApplyDiminishingReturns(rawRange, baseRange), 2.5f, 12f);
                    }

                case HeroClass.Mage:
                    // 法师普攻 = 基础×1.5×(1+装备加成)，封顶14m
                    {
                        float baseRange = mapBase * 1.5f;
                        float bonusPct = BonusAttackRange / Mathf.Max(baseRange, 0.1f);
                        float rawRange = baseRange * (1f + bonusPct);
                        return Mathf.Clamp(ApplyDiminishingReturns(rawRange, baseRange), 2.5f, 14f);
                    }

                default:
                    return Mathf.Min(mapBase * 0.6f, 5f);
            }
        }
    }

    /// <summary>技能范围计算 — 基于地图尺寸+技能类型</summary>
    public float GetSkillRange(float skillTypeCoefficient)
    {
        // 基础范围 = 地图对角线 × 8%
        int stageIdx = GameManager.Instance != null ? GameManager.Instance.CurrentStageIndex : 0;
        float mapBase = DungeonMapData.GetMapDiagonal(stageIdx) * 0.08f;

        var player = GameManager.Instance?.Player;
        var cls = player != null ? player.HeroClass : HeroClass.Warrior;

        // 技能倍率：战士0.8, 牧师1.2, 法师1.8
        float classMult = cls switch { HeroClass.Mage => 1.8f, HeroClass.Priest => 1.2f, _ => 0.8f };
        float baseRange = mapBase * classMult * skillTypeCoefficient;

        // 装备加成
        float attackBase = mapBase * classMult;
        float bonusPct = BonusAttackRange / Mathf.Max(attackBase, 0.1f);
        float rawRange = baseRange * (1f + bonusPct);

        // 收益递减
        float finalRange = ApplyDiminishingReturns(rawRange, baseRange);

        // 最小范围2.5m + 职业技能封顶
        float cap = cls switch { HeroClass.Mage => 18f, HeroClass.Priest => 16f, _ => 7f };
        return Mathf.Clamp(finalRange, 2.5f, cap);
    }

    /// <summary>收益递减：超过基础1.5倍后超出部分×0.5</summary>
    private static float ApplyDiminishingReturns(float raw, float baseValue)
    {
        float threshold = baseValue * 1.5f;
        if (raw <= threshold) return raw;
        return threshold + (raw - threshold) * 0.5f;
    }

    /// <summary>技能类型系数</summary>
    public static class SkillRangeCoefficient
    {
        public const float Single = 1.0f;
        public const float SmallAOE = 1.5f;
        public const float MediumAOE = 2.0f;
        public const float LargeAOE = 2.5f;
        public const float ChainBounce = 1.8f;
        public const float HealSupport = 2.0f;
    }
    public float TotalAttackSpeed => AttackSpeed + BonusAttackSpeed;
    public float EffectiveAttackCooldown => _progress.BaseAttackCooldown / TotalAttackSpeed;
    public int BuffedAttack => Mathf.RoundToInt(TotalAttack * BuffAttackMult);
    public int BuffedDefense => Mathf.RoundToInt(TotalDefense * BuffDefenseMult);

    public int TakeDamage(int rawDamage)
    {
        if (ShieldHp > 0 && Time.time < ShieldEndTime)
        {
            if (rawDamage <= ShieldHp)
            {
                ShieldHp -= rawDamage;
                return 0;
            }
            rawDamage -= ShieldHp;
            ShieldHp = 0;
        }

        if (Time.time - _lastDamageTime < InvincibilityTime)
            return 0;

        int effectiveDef = BuffedDefense;
        int actualDamage = Mathf.Max(1, rawDamage - effectiveDef);
        Hp = Mathf.Max(0, Hp - actualDamage);
        _lastDamageTime = Time.time;
        return actualDamage;
    }

    public void RegenerateHp()
    {
        if (Hp < TotalMaxHp && HpRegen > 0)
            Hp = Mathf.Min(TotalMaxHp, Hp + Mathf.RoundToInt(HpRegen * Time.deltaTime));
    }

    public void UpdateBuffs()
    {
        if (BuffEndTime > 0 && Time.time > BuffEndTime)
        {
            BuffAttackMult = 1f;
            BuffDefenseMult = 1f;
            BuffEndTime = 0f;
        }
    }

    public void RecalculateEquipmentBonuses(List<EquipmentSlot> equipped)
    {
        BonusAttack = 0;
        BonusDefense = 0;
        BonusMaxHp = 0;
        BonusMoveSpeed = 0f;
        BonusCritChance = 0f;
        BonusLifeSteal = 0f;
        BonusAttackRange = 0f;
        BonusAttackSpeed = 0f;

        // Affix percentage bonuses (applied after flat bonuses)
        float atkPct = 0f, defPct = 0f, hpPct = 0f;

        foreach (var slot in equipped)
        {
            if (slot.Item == null) continue;
            var item = slot.Item;
            BonusAttack += item.AttackBonus;
            BonusDefense += item.DefenseBonus;
            BonusMaxHp += item.HpBonus;
            BonusMoveSpeed += item.SpeedBonus;
            BonusCritChance += item.CritBonus;
            BonusLifeSteal += item.LifeStealBonus;
            BonusAttackRange += item.RangeBonus;
            BonusAttackSpeed += item.AttackSpeedBonus;

            // Apply affix bonuses
            if (item.Affixes != null)
            {
                foreach (var affix in item.Affixes)
                {
                    switch (affix.AffixType)
                    {
                        case AffixType.AttackPct: atkPct += affix.Value; break;
                        case AffixType.DefensePct: defPct += affix.Value; break;
                        case AffixType.HpPct: hpPct += affix.Value; break;
                        case AffixType.MoveSpeed: BonusMoveSpeed += affix.Value; break;
                        case AffixType.CritChance: BonusCritChance += affix.Value; break;
                        case AffixType.Lifesteal: BonusLifeSteal += affix.Value; break;
                        case AffixType.AttackSpeed: BonusAttackSpeed += affix.Value; break;
                        case AffixType.AttackRange: BonusAttackRange += affix.Value; break;
                    }
                }
            }
        }

        // Apply percentage bonuses to base stats (additive on top of flat)
        if (atkPct > 0) BonusAttack += Mathf.RoundToInt(_progress.BaseAttack * atkPct);
        if (defPct > 0) BonusDefense += Mathf.RoundToInt(_progress.BaseDefense * defPct);
        if (hpPct > 0) BonusMaxHp += Mathf.RoundToInt(_progress.BaseMaxHp * hpPct);

        // Apply set bonuses
        SetBonusData.ApplySetBonuses(equipped, this);

        if (Hp > TotalMaxHp)
            Hp = TotalMaxHp;
    }

    public void SyncFromProgress()
    {
        // Sync HP to not exceed new max after equipment recalc
        if (Hp > TotalMaxHp)
            Hp = TotalMaxHp;
    }

    // === Daily Challenge modifiers ===
    private int GetDailyAttackBonus()
    {
        if (DailyChallenge.ActiveModifier == DailyChallenge.ModifierType.GlassCannon)
            return _progress.BaseAttack; // +100%
        if (DailyChallenge.ActiveModifier == DailyChallenge.ModifierType.DoubleDamage)
            return _progress.BaseAttack; // +100%
        return 0;
    }

    private int GetDailyDefensePenalty()
    {
        if (DailyChallenge.ActiveModifier == DailyChallenge.ModifierType.GlassCannon)
            return Mathf.RoundToInt(_progress.BaseDefense * 0.8f); // -80%
        return 0;
    }

    private float GetDailySpeedBonus()
    {
        if (DailyChallenge.ActiveModifier == DailyChallenge.ModifierType.SpeedDemon)
            return _progress.BaseMoveSpeed * 0.5f; // +50%
        return 0;
    }

    private float GetDailyVampireBonus()
    {
        if (DailyChallenge.ActiveModifier == DailyChallenge.ModifierType.Vampire)
            return 0.2f; // +20% lifesteal
        return 0;
    }
}
