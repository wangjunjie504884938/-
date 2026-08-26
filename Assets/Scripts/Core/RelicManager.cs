using UnityEngine;
using System.Collections.Generic;

public class RelicManager : MonoBehaviour
{
    public static RelicManager Instance { get; private set; }

    public List<RelicData> ActiveRelics { get; private set; } = new List<RelicData>();
    private readonly Dictionary<RelicType, RelicData> _relicDict = new Dictionary<RelicType, RelicData>();
    public const int MaxRelics = 6;

    // Runtime state for timed buffs
    private float speedBoostEndTime;
    private bool speedBoostActive;
    private PlayerController player;
    private static readonly Collider2D[] _relicBuffer = new Collider2D[32];
    private static readonly Collider2D[] _chainBuffer = new Collider2D[16];

    public bool HasRelic(RelicType type) => _relicDict.ContainsKey(type);
    public RelicData GetRelic(RelicType type) => _relicDict.TryGetValue(type, out var r) ? r : null;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void SetPlayer(PlayerController pc) => player = pc;

    public bool AddRelic(RelicData relic)
    {
        if (ActiveRelics.Count >= MaxRelics) return false;
        if (HasRelic(relic.Type)) return false;
        ActiveRelics.Add(relic);
        _relicDict[relic.Type] = relic;

        // XpMagnet: apply immediately
        if (relic.Type == RelicType.XpMagnet && player != null)
            player.Stats.XpMultiplier += relic.XpBonus;

                    GameLog.Log($"Acquired relic: {relic.Name} — {relic.Description}");

        // Achievement tracking for relic count
        if (AchievementManager.Instance != null)
            AchievementManager.Instance.RecordRelicCount(ActiveRelics.Count);

        return true;
    }

    public void ClearRelics()
    {
        // Undo XpMagnet before clearing
        var xpRelic = GetRelic(RelicType.XpMagnet);
        if (xpRelic != null && player != null)
            player.Stats.XpMultiplier -= xpRelic.XpBonus;

        ActiveRelics.Clear();
        _relicDict.Clear();
        speedBoostEndTime = 0f;
    }

    private void Update()
    {
        // Handle speed boost expiry
        if (speedBoostActive && (speedBoostEndTime <= 0 || Time.time > speedBoostEndTime))
        {
            speedBoostActive = false;
            speedBoostEndTime = 0;
            if (player != null)
            {
                var speedRelic = GetRelic(RelicType.SpeedOnKill);
                if (speedRelic != null)
                    player.Stats.BonusMoveSpeed -= player.Stats.MoveSpeed * (speedRelic.SpeedBoostMult - 1f);
            }
        }
    }

    public bool IsSpeedBoosted => speedBoostEndTime > 0 && Time.time < speedBoostEndTime;

    // === Event Hooks ===

    public void OnEnemyKilled(EnemyController enemy)
    {
        if (player == null || player.IsDead) return;

        // KillHeal
        var killHeal = GetRelic(RelicType.KillHeal);
        if (killHeal != null)
        {
            player.Heal(killHeal.KillHealAmount);
            VFXHelper.SpawnHealEffect(player.transform.position, killHeal.KillHealAmount);
        }

        // SpeedOnKill
        var speedRelic = GetRelic(RelicType.SpeedOnKill);
        if (speedRelic != null)
        {
            // Remove old boost if already active
            if (speedBoostActive)
            {
                player.Stats.BonusMoveSpeed -= player.Stats.MoveSpeed * (speedRelic.SpeedBoostMult - 1f);
            }

            speedBoostEndTime = Time.time + speedRelic.SpeedBoostDuration;
            speedBoostActive = true;
            player.Stats.BonusMoveSpeed += player.Stats.MoveSpeed * (speedRelic.SpeedBoostMult - 1f);
            VFXHelper.SpawnHitParticles(player.transform.position, new Color(0.9f, 0.5f, 1f, 0.6f), 4);
        }

        // CritBurst on kill (if the killing blow was a crit, handled elsewhere)
    }

    public void OnCritDealt(Vector3 hitPosition, int damage)
    {
        // CritBurst
        var critRelic = GetRelic(RelicType.CritBurst);
        if (critRelic != null)
        {
            float radius = critRelic.CritBurstRadius;
            int burstDmg = Mathf.RoundToInt(damage * 0.5f);
            VFXHelper.SpawnAreaPulse(hitPosition, radius, new Color(1f, 0.9f, 0.2f, 0.6f));
            CameraFollow.Shake(0.15f, 0.08f);

            Collider2D[] buffer = _relicBuffer;
            int hitCount = Physics2D.OverlapCircleNonAlloc(hitPosition, radius, buffer, LayerMask.GetMask("Enemy"));
            for (int i = 0; i < hitCount; i++)
            {
                EnemyController enemy = buffer[i].GetComponent<EnemyController>();
                if (enemy != null && !enemy.IsDead)
                {
                    enemy.TakeDamage(burstDmg, player);
                    VFXHelper.SpawnDamageNumber(enemy.transform.position, burstDmg, false, new Color(1f, 0.9f, 0.2f));
                }
            }
        }
    }

    public void OnPlayerHit(int rawDamage, EnemyController attacker)
    {
        if (player == null) return;

        // Thorns
        var thorns = GetRelic(RelicType.Thorns);
        if (thorns != null && attacker != null && !attacker.IsDead)
        {
            int thornsDmg = Mathf.RoundToInt(rawDamage * thorns.ThornsPct);
            attacker.TakeDamage(thornsDmg, player);
            VFXHelper.SpawnHitParticles(attacker.transform.position, new Color(0.6f, 0.8f, 0.2f), 3);
        }

        // DodgeHeal
        var dodgeHeal = GetRelic(RelicType.DodgeHeal);
        if (dodgeHeal != null && Random.Range(0f, 1f) < dodgeHeal.DodgeHealChance)
        {
            player.Heal(dodgeHeal.DodgeHealAmount);
            VFXHelper.SpawnHealEffect(player.transform.position, dodgeHeal.DodgeHealAmount);
        }
    }

    public float GetBloodMagicDamageMultiplier()
    {
        var blood = GetRelic(RelicType.BloodMagic);
        if (blood == null || player == null) return 1f;

        // 50% chance to activate blood magic effect
        if (Random.Range(0f, 1f) < 0.5f)
        {
            int hpCost = Mathf.Max(1, Mathf.RoundToInt(player.Stats.TotalMaxHp * blood.BloodMagicHpCostPct));
            player.Stats.Hp = Mathf.Max(1, player.Stats.Hp - hpCost);
            VFXHelper.SpawnHitParticles(player.transform.position, new Color(0.9f, 0.1f, 0.1f, 0.6f), 3);
            return blood.BloodMagicDamageBonus;
        }
        return 1f;
    }

    public bool ShouldDoubleStrike()
    {
        var ds = GetRelic(RelicType.DoubleStrike);
        if (ds == null) return false;
        return Random.Range(0f, 1f) < ds.DoubleStrikeChance;
    }

    public void OnProjectileHit(PlayerProjectile projectile, EnemyController hitEnemy)
    {
        if (player == null) return;

        // SplitShot: spawn 2 small projectiles
        var split = GetRelic(RelicType.SplitShot);
        if (split != null && projectile != null)
        {
            Vector2 baseDir = projectile.Direction;
            for (int i = 0; i < split.SplitCount; i++)
            {
                float angle = (i == 0 ? -30f : 30f) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(
                    baseDir.x * Mathf.Cos(angle) - baseDir.y * Mathf.Sin(angle),
                    baseDir.x * Mathf.Sin(angle) + baseDir.y * Mathf.Cos(angle)
                );

                GameObject splitObj = VFXPool.GetPlayerProjectile();
                splitObj.transform.position = hitEnemy != null ? hitEnemy.transform.position : projectile.transform.position;
                splitObj.layer = LayerMask.NameToLayer("Player");

                var sr = splitObj.GetComponent<SpriteRenderer>();
                sr.sprite = VFXHelper.GetSharedWhiteSprite();
                sr.color = new Color(0.3f, 0.9f, 1f, 0.8f);
                sr.sortingOrder = 8;
                splitObj.transform.localScale = new Vector3(0.2f, 0.2f, 1f);

                var rb2d = splitObj.GetComponent<Rigidbody2D>();
                rb2d.gravityScale = 0f;
                rb2d.velocity = dir * 7f;

                var col = splitObj.GetComponent<CircleCollider2D>();
                col.radius = 0.25f;
                col.isTrigger = true;

                var proj = splitObj.GetComponent<PlayerProjectile>();
                int splitDmg = Mathf.RoundToInt(projectile.Damage * 0.4f);
                proj.Initialize(dir, splitDmg, player);
                proj.SetColor(new Color(0.3f, 0.9f, 1f, 0.8f));
                // Mark as relic-spawned to prevent infinite recursion
                proj.SetRelicSpawned();
            }
        }

        // ChainLightning: bounce to nearby enemies
        var chain = GetRelic(RelicType.ChainLightning);
        if (chain != null && hitEnemy != null)
        {
            Collider2D[] chainBuffer = _chainBuffer;
            int chainCount = Physics2D.OverlapCircleNonAlloc(hitEnemy.transform.position, 4f, chainBuffer, LayerMask.GetMask("Enemy"));
            int bounced = 0;
            for (int i = 0; i < chainCount && bounced < chain.ChainTargets; i++)
            {
                EnemyController target = chainBuffer[i].GetComponent<EnemyController>();
                if (target != null && target != hitEnemy && !target.IsDead)
                {
                    int chainDmg = Mathf.RoundToInt(projectile != null ? projectile.Damage * 0.3f : player.Stats.BuffedAttack * 0.3f);
                    target.TakeDamage(chainDmg, player);
                    VFXHelper.SpawnDamageNumber(target.transform.position, chainDmg, false, new Color(0.5f, 0.7f, 1f));
                    VFXHelper.SpawnHitParticles(target.transform.position, new Color(0.5f, 0.7f, 1f), 3);
                    bounced++;
                }
            }

            if (bounced > 0)
                VFXHelper.SpawnAreaPulse(hitEnemy.transform.position, 3f, new Color(0.5f, 0.7f, 1f, 0.3f));
        }
    }
}
