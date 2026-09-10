using UnityEngine;
using System.Collections;

/// <summary>
/// PlayerCombatController partial — Rune effects and mutation implementations
/// Contains: ApplyRuneEffectsToEnemy, ApplyRuneLifeSteal, SpawnBurnZones, BurnZoneTick,
/// ApplyMeleeMutations, ApplyHealMutations, HealingAuraTick
/// </summary>
public partial class PlayerCombatController
{
    private void ApplyRuneEffectsToEnemy(SkillData skill, EnemyController enemy)
    {
        if (skill.HasSlow && enemy.StatusFx != null)
            enemy.StatusFx.ApplySlowFromSkill(skill);
        if (skill.HasBurn && enemy.StatusFx != null)
            enemy.StatusFx.ApplyBurnFromSkill(skill);
        if (skill.HasElementConvert && enemy.StatusFx != null)
        {
            int roll = Random.Range(0, 3);
            if (roll == 0) enemy.StatusFx.ApplyEffect(StatusEffectManager.EffectType.Freeze, 1.5f, 0, 1f, 0.5f);
            else if (roll == 1) enemy.StatusFx.ApplyEffect(StatusEffectManager.EffectType.Burn, 3f, Mathf.RoundToInt(player.Stats.BuffedAttack * 0.1f), 0.5f, 0.9f);
            else enemy.StatusFx.ApplyEffect(StatusEffectManager.EffectType.Poison, 4f, Mathf.RoundToInt(player.Stats.BuffedAttack * 0.08f), 0.8f, 0.7f);
        }
    }

    private void ApplyRuneLifeSteal(SkillData skill, int totalDamage)
    {
        if (skill.LifeStealPct > 0 && totalDamage > 0)
        {
            int healAmt = Mathf.Max(1, Mathf.RoundToInt(totalDamage * skill.LifeStealPct));
            player.Heal(healAmt);
        }
    }

    private void SpawnBurnZones(SkillData skill, Vector3 center, float radius)
    {
        VFXHelper.SpawnAreaPulse(center, radius, new Color(0.3f, 0.5f, 0.1f, 0.3f));
        StartCoroutine(BurnZoneTick(center, radius, skill.BurnDps, skill.BurnDuration));
    }

    private IEnumerator BurnZoneTick(Vector3 center, float radius, float dpsRatio, float duration)
    {
        float elapsed = 0f;
        float tickInterval = GameConfigManager.Instance?.GetGlobal("BurnTickInterval", 0.5f) ?? 0.5f;
        float tickTimer = 0f;
        int dmgPerTick = Mathf.Max(1, Mathf.RoundToInt(player.Stats.BuffedAttack * dpsRatio * tickInterval));

        while (elapsed < duration)
        {
            yield return null;
            elapsed += Time.deltaTime;
            tickTimer += Time.deltaTime;
            if (tickTimer >= tickInterval)
            {
                tickTimer -= tickInterval;
                int hitCount = Physics2D.OverlapCircleNonAlloc(center, radius, _overlapBuffer, LayerMask.GetMask("Enemy"));
                for (int i = 0; i < hitCount; i++)
                {
                    EnemyController enemy = _overlapBuffer[i].GetComponent<EnemyController>();
                    if (enemy != null && !enemy.IsDead)
                        enemy.TakeDamage(dmgPerTick, player);
                }
            }
        }
    }

    private void ApplyMeleeMutations(SkillData skill, float range)
    {
        int flameStacks = skill.GetMutationStacks(MutationPool.FlameWhirl);
        if (flameStacks > 0)
        {
            float burnDuration = 2f + flameStacks;
            float burnRadius = range * 0.8f;
            VFXHelper.SpawnAreaPulse(player.transform.position, burnRadius,
                new Color(0.3f, 0.5f, 0.1f, 0.3f));
            StartCoroutine(BurnZoneTick(player.transform.position, burnRadius, 0.2f, burnDuration));
        }

        int thunderStacks = skill.GetMutationStacks(MutationPool.ThunderSlash);
        int pullStacks = skill.GetMutationStacks(MutationPool.PullWhirl);
        if (thunderStacks > 0 || pullStacks > 0)
        {
            int hitCount = Physics2D.OverlapCircleNonAlloc(
                player.transform.position, range, _overlapBuffer, LayerMask.GetMask("Enemy"));
            for (int i = 0; i < hitCount; i++)
            {
                EnemyController enemy = _overlapBuffer[i].GetComponent<EnemyController>();
                if (enemy == null || enemy.IsDead) continue;

                if (thunderStacks > 0 && Random.Range(0f, 1f) < 0.3f + thunderStacks * 0.1f && enemy.StatusFx != null)
                    enemy.StatusFx.ApplyEffect(StatusEffectManager.EffectType.Freeze, 1f, 0, 1f, 0.3f);

                if (pullStacks > 0)
                {
                    var enemyRb = enemy.GetComponent<Rigidbody2D>();
                    if (enemyRb != null)
                    {
                        Vector2 pullDir = (player.transform.position - enemy.transform.position).normalized;
                        enemyRb.AddForce(pullDir * (8f + pullStacks * 0.5f), ForceMode2D.Impulse);
                    }
                }
            }
        }
    }

    private void ApplyHealMutations(SkillData skill, int healAmount)
    {
        int shieldStacks = skill.GetMutationStacks(MutationPool.HolyShield);
        if (shieldStacks > 0)
        {
            float shieldPct = 0.2f + shieldStacks * 0.1f;
            int shieldAmt = Mathf.RoundToInt(player.Stats.TotalMaxHp * shieldPct);
            player.Stats.ShieldHp = Mathf.Max(player.Stats.ShieldHp, shieldAmt);
            player.Stats.ShieldEndTime = Time.time + 3f;
            VFXHelper.SpawnAreaPulse(player.transform.position, 1.2f,
                new Color(0.9f, 0.9f, 0.3f, 0.4f));
        }

        int auraStacks = skill.GetMutationStacks(MutationPool.HealingAura);
        if (auraStacks > 0)
        {
            float auraDuration = 3f + auraStacks;
            VFXHelper.SpawnHealAura(player.transform.position, 2f);
            StartCoroutine(HealingAuraTick(auraDuration));
        }

        int retributionStacks = skill.GetMutationStacks(MutationPool.Retribution);
        if (retributionStacks > 0)
        {
            float damageRatio = 0.5f + retributionStacks * 0.25f;
            int damage = Mathf.RoundToInt(healAmount * damageRatio);
            int hitCount = Physics2D.OverlapCircleNonAlloc(
                player.transform.position, 4f, _overlapBuffer, LayerMask.GetMask("Enemy"));
            for (int i = 0; i < hitCount; i++)
            {
                EnemyController enemy = _overlapBuffer[i].GetComponent<EnemyController>();
                if (enemy == null || enemy.IsDead) continue;
                enemy.TakeDamage(damage, player);
                VFXHelper.SpawnDamageNumber(enemy.transform.position, damage, false,
                    new Color(1f, 0.4f, 0.3f));
                VFXHelper.SpawnHitParticles(enemy.transform.position,
                    new Color(1f, 0.4f, 0.3f), 4);
            }
        }
    }

    private IEnumerator HealingAuraTick(float duration)
    {
        float elapsed = 0f;
        float tickInterval = 1f;
        float tickTimer = 0f;

        while (elapsed < duration)
        {
            yield return null;
            elapsed += Time.deltaTime;
            tickTimer += Time.deltaTime;
            if (tickTimer >= tickInterval)
            {
                tickTimer -= tickInterval;
                int healPerTick = Mathf.Max(1, Mathf.RoundToInt(player.Stats.TotalMaxHp * 0.02f));
                player.Heal(healPerTick);
                VFXHelper.SpawnHealEffect(player.transform.position, healPerTick);
            }
        }
    }
}
