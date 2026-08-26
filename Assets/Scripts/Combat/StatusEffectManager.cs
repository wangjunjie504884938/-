using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Status effects that can be applied to enemies: Burn, Freeze, Poison, ElementConvert.
/// Attached to EnemyController's GameObject. Ticks DOT damage and applies speed modifiers.
/// </summary>
public class StatusEffectManager : MonoBehaviour
{
    public enum EffectType
    {
        Burn,
        Freeze,
        Poison,
        Slow,
        Shock,      // PoE2-style: 感电, 受到伤害+20%
        Corrode     // 腐蚀: 防御-50% (Burn+Poison反应)
    }

    private static readonly Collider2D[] _reactionBuffer = new Collider2D[16];

    /// <summary>元素反应类型</summary>
    public enum ElementReaction
    {
        None,
        Shatter,    // 碎冰: 冰冻+物理攻击 → 即杀冰冻敌人
        Steam,       // 蒸汽: 冰冻+燃烧 → 范围伤害+致盲
        Corrode,    // 腐蚀: 燃烧+中毒 → 防御-50%
        Conduct      // 感电: 电击+任意 → 受到伤害+20%
    }

    [System.Serializable]
    public struct ActiveEffect
    {
        public EffectType Type;
        public float RemainingTime;
        public float TickInterval;
        public float TickTimer;
        public int DamagePerTick;
        public float SpeedMultiplier;
        public Color TintColor;
    }

    private List<ActiveEffect> effects = new List<ActiveEffect>();
    private EnemyController enemy;
    private Rigidbody2D rb;
    private SpriteRenderer[] spriteRenderers;
    private float originalMoveSpeed;

    public bool IsFrozen => HasEffect(EffectType.Freeze);

    private void Awake()
    {
        enemy = GetComponent<EnemyController>();
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        if (enemy != null && enemy.IsDead) { effects.Clear(); return; }

        for (int i = effects.Count - 1; i >= 0; i--)
        {
            var e = effects[i];
            e.RemainingTime -= Time.deltaTime;

            // DOT tick
            if (e.DamagePerTick > 0)
            {
                e.TickTimer -= Time.deltaTime;
                if (e.TickTimer <= 0f)
                {
                    e.TickTimer = e.TickInterval;
                    ApplyTickDamage(e);
                }
            }

            effects[i] = e;

            if (e.RemainingTime <= 0f)
            {
                effects.RemoveAt(i);
                VFXHelper.SpawnHitParticles(transform.position, e.TintColor * 0.5f, 2);
            }
        }

        ApplySpeedModifier();
        ApplyTint();
    }

    public void ApplyEffect(EffectType type, float duration, int damagePerTick = 0, float tickInterval = 0.5f, float speedMultiplier = 1f)
    {
        // Check for element reactions BEFORE applying the new effect
        CheckElementReaction(type, damagePerTick);

        // Refresh existing effect of same type, or add new
        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i].Type == type)
            {
                var e = effects[i];
                e.RemainingTime = Mathf.Max(e.RemainingTime, duration);
                e.DamagePerTick = Mathf.Max(e.DamagePerTick, damagePerTick);
                e.SpeedMultiplier = Mathf.Min(e.SpeedMultiplier, speedMultiplier);
                effects[i] = e;
                return;
            }
        }

        Color tint = type switch
        {
            EffectType.Burn => new Color(1f, 0.4f, 0.1f, 0.6f),
            EffectType.Freeze => new Color(0.5f, 0.8f, 1f, 0.5f),
            EffectType.Poison => new Color(0.3f, 0.8f, 0.2f, 0.5f),
            EffectType.Slow => new Color(0.3f, 0.5f, 1f, 0.4f),
            EffectType.Shock => new Color(0.9f, 0.9f, 0.2f, 0.5f),
            EffectType.Corrode => new Color(0.5f, 0.4f, 0.1f, 0.4f),
            _ => Color.white
        };

        effects.Add(new ActiveEffect
        {
            Type = type,
            RemainingTime = duration,
            TickInterval = tickInterval,
            TickTimer = tickInterval,
            DamagePerTick = damagePerTick,
            SpeedMultiplier = speedMultiplier,
            TintColor = tint
        });

        AudioManager.Instance?.PlayHit();
    }

    /// <summary>Convenience: Apply burn from a SkillData's rune parameters.</summary>
    public void ApplyBurnFromSkill(SkillData skill)
    {
        if (skill == null || !skill.HasBurn) return;
        float duration = 3f;
        int dotDmg = Mathf.RoundToInt(enemy != null ? enemy.BaseAttack * skill.BurnDps * 0.5f : 5);
        ApplyEffect(EffectType.Burn, duration, dotDmg, 0.5f, 0.9f);
    }

    /// <summary>
    /// 检查元素反应 — 当新状态施加时触发
    /// 碎冰: 冰冻+物理伤害 → 即杀冰冻敌人并造成范围冰刺
    /// 蒸汽: 冰冻+燃烧 → 范围蒸汽伤害
    /// 腐蚀: 燃烧+中毒 → 防御-50%
    /// 感电: 电击+任意 → 受到伤害+20%
    /// </summary>
    private void CheckElementReaction(EffectType newType, int damagePerTick)
    {
        if (enemy == null || enemy.IsDead) return;

        // 碎冰: 敌人冰冻时受到物理攻击 → 即杀
        if (newType == EffectType.Freeze && HasEffect(EffectType.Burn))
        {
            // 蒸汽反应: 冰冻+燃烧 = 蒸汽爆炸
            TriggerSteamExplosion();
            return;
        }

        if (newType == EffectType.Burn && HasEffect(EffectType.Freeze))
        {
            TriggerSteamExplosion();
            return;
        }

        // 腐蚀: 燃烧+中毒 = 防御-50%
        if (newType == EffectType.Burn && HasEffect(EffectType.Poison))
        {
            ApplyEffect(EffectType.Corrode, 5f, 0, 1f, 1f);
            VFXHelper.SpawnAreaPulse(transform.position, 2f, new Color(0.5f, 0.4f, 0.1f, 0.6f));
            return;
        }

        if (newType == EffectType.Poison && HasEffect(EffectType.Burn))
        {
            ApplyEffect(EffectType.Corrode, 5f, 0, 1f, 1f);
            VFXHelper.SpawnAreaPulse(transform.position, 2f, new Color(0.5f, 0.4f, 0.1f, 0.6f));
            return;
        }

        // 感电: 电击+任意其他元素 = 受到伤害+20% (通过Shock效果实现)
        if (newType == EffectType.Shock)
        {
            VFXHelper.SpawnHitParticles(transform.position, new Color(0.9f, 0.9f, 0.2f, 0.8f), 4);
            return;
        }
    }

    /// <summary>触发蒸汽爆炸 (冰冻+燃烧反应)</summary>
    private void TriggerSteamExplosion()
    {
        // 移除冰冻和燃烧
        RemoveEffect(EffectType.Freeze);
        RemoveEffect(EffectType.Burn);

        // 造成范围伤害
        int steamDamage = Mathf.RoundToInt((enemy.MaxHp + enemy.CurrentHp) * 0.15f);
        enemy.TakeDamage(steamDamage, null);

        // 蒸汽视觉效果
        VFXHelper.SpawnAreaPulse(transform.position, 3f, new Color(0.8f, 0.8f, 0.9f, 0.7f));
        VFXHelper.SpawnHitParticles(transform.position, new Color(0.7f, 0.8f, 1f, 0.6f), 8);
        VFXHelper.SpawnDamageNumber(transform.position, steamDamage, true, new Color(0.8f, 0.8f, 1f));

        // 对周围敌人也造成伤害
        var buffer = _reactionBuffer;
        int hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, 3f, buffer, LayerMask.GetMask("Enemy"));
        for (int i = 0; i < hitCount; i++)
        {
            var nearbyEnemy = buffer[i].GetComponent<EnemyController>();
            if (nearbyEnemy != null && nearbyEnemy != enemy && !nearbyEnemy.IsDead)
                nearbyEnemy.TakeDamage(Mathf.RoundToInt(steamDamage * 0.5f), null);
        }

        CameraFollow.Shake(0.2f, 0.1f);
    }

    /// <summary>检查物理攻击是否触发碎冰 (由EnemyCombatController调用)</summary>
    public bool TryShatter(int physicalDamage)
    {
        if (!HasEffect(EffectType.Freeze)) return false;

        // 先移除冰冻效果, 防止TakeDamage再次触发TryShatter递归
        RemoveEffect(EffectType.Freeze);

        // 碎冰: 冰冻敌人受到物理攻击 → 即杀
        int shatterDamage = Mathf.RoundToInt(enemy.MaxHp * 0.5f) + physicalDamage;
        enemy.TakeDamage(shatterDamage, null);

        VFXHelper.SpawnAreaPulse(transform.position, 2.5f, new Color(0.5f, 0.8f, 1f, 0.8f));
        VFXHelper.SpawnHitParticles(transform.position, new Color(0.7f, 0.9f, 1f), 8);
        VFXHelper.SpawnDamageNumber(transform.position, shatterDamage, true, new Color(0.5f, 0.9f, 1f));
        CameraFollow.Shake(0.15f, 0.08f);
        AudioManager.Instance?.PlayCrit();
        return true;
    }

    /// <summary>获取受到伤害的倍率 (感电+20%, 腐蚀防-50%等效)</summary>
    public float GetDamageMultiplier()
    {
        float mult = 1f;
        if (HasEffect(EffectType.Shock)) mult *= 1.2f;
        return mult;
    }

    /// <summary>获取防御降低值 (腐蚀-50%)</summary>
    public float GetDefenseMultiplier()
    {
        if (HasEffect(EffectType.Corrode)) return 0.5f;
        return 1f;
    }

    private void RemoveEffect(EffectType type)
    {
        for (int i = effects.Count - 1; i >= 0; i--)
        {
            if (effects[i].Type == type)
                effects.RemoveAt(i);
        }
    }

    /// <summary>Convenience: Apply slow from a SkillData's rune parameters.</summary>
    public void ApplySlowFromSkill(SkillData skill)
    {
        if (skill == null || !skill.HasSlow) return;
        ApplyEffect(EffectType.Slow, skill.SlowDuration, 0, 1f, 1f - skill.SlowPct);
    }

    public float GetSpeedMultiplier()
    {
        float mult = 1f;
        for (int i = 0; i < effects.Count; i++)
            mult = Mathf.Min(mult, effects[i].SpeedMultiplier);
        return mult;
    }

    public bool HasEffect(EffectType type)
    {
        for (int i = 0; i < effects.Count; i++)
            if (effects[i].Type == type) return true;
        return false;
    }

    private void ApplyTickDamage(ActiveEffect e)
    {
        if (enemy == null || enemy.IsDead) return;
        enemy.TakeDamage(e.DamagePerTick, null);
        VFXHelper.SpawnDamageNumber(transform.position, e.DamagePerTick, false, e.TintColor);
        VFXHelper.SpawnHitParticles(transform.position, e.TintColor, 2);
    }

    private void ApplySpeedModifier()
    {
        if (rb == null) return;
        float mult = GetSpeedMultiplier();
        if (mult < 1f && rb.velocity.magnitude > 0.1f)
        {
            rb.velocity *= mult;
        }
    }

    private void ApplyTint()
    {
        if (spriteRenderers == null || spriteRenderers.Length == 0)
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>();

        if (effects.Count == 0) return;

        Color tint = Color.white;
        for (int i = 0; i < effects.Count; i++)
            tint = Color.Lerp(tint, effects[i].TintColor, 0.5f);

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
                spriteRenderers[i].color = Color.Lerp(Color.white, tint, 0.3f);
        }
    }

    private void LateUpdate()
    {
        // Reset tint when no effects
        if (effects.Count == 0 && spriteRenderers != null)
        {
            // Let normal color restore happen via FlashWhite/etc
        }
    }
}
