using UnityEngine;

public class EnemyBrainController : MonoBehaviour
{
    private EnemyController enemy;
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Transform playerTransform;

    // Pre-allocated buffers (avoid per-frame GC allocation)
    private static readonly Collider2D[] _separationBuffer = new Collider2D[8];
    private static int _enemyLayerMask = -1;

    // Real delta time tracking — TickUpdate now runs every 0.2s via AIUpdateLoop coroutine.
    // Time.deltaTime inside TickUpdate would only be one frame's delta (~0.0167s),
    // so we compute the actual elapsed time since the last tick.
    private float _lastTickTime;
    private float _aiDeltaTime = 0.016f;

    private static void InitStaticBuffers()
    {
        if (_enemyLayerMask < 0)
            _enemyLayerMask = LayerMask.GetMask("Enemy");
    }

    // Elite charge attack
    private bool isCharging;
    private float chargeTimer;
    private float chargeCooldown;
    private Vector2 chargeDir;
    private const float ChargeSpeed = 12f;
    private const float ChargeDuration = 0.3f;
    private const float ChargeCooldownTime = 4f;

    // Boss slam cooldown
    private float slamCooldown;
    private const float SlamCooldownTime = 6f;
    private const float SlamRange = 2f;

    // Boss phase tracking
    private int bossPhase = 1;
    private float enrageSlamCooldown;
    private const float EnrageSlamCooldownTime = 3f;
    private float aoeCooldown;
    private const float AoeCooldownTime = 8f;
    private bool enrageChargeActive;
    private float enrageChargeTimer;
    private Vector2 enrageChargeDir;
    private const float EnrageChargeSpeed = 14f;
    private const float EnrageChargeDuration = 0.35f;

    // PoE2-style telegraphed attack patterns
    private BossPattern bossPattern;

    // Ranged strafe
    private float strafeDir = 1f;
    private float strafeChangeTimer;

    // Teleport affix
    private float teleportCooldown;

    // Stuck avoidance
    private float stuckCheckTimer;
    private Vector3 lastStuckPos;
    private float stuckWiggleDir;

    public bool IsCharging => isCharging;
    public float ChargeCooldown => chargeCooldown;
    public float SlamCooldown => slamCooldown;

    public void Initialize(EnemyController enemy, Rigidbody2D rb, SpriteRenderer spriteRenderer)
    {
        InitStaticBuffers();
        this.enemy = enemy;
        this.rb = rb;
        this.spriteRenderer = spriteRenderer;
    }

    public void SetPlayerTransform(Transform t) => playerTransform = t;

    public void InitStuckAvoidance()
    {
        stuckCheckTimer = 0.5f;
        lastStuckPos = transform.position;
        stuckWiggleDir = 0f;
        _lastTickTime = Time.time; // 初始化时间戳，防止第一次tick计算巨大delta
    }

    public void TickUpdate()
    {
        if (enemy.IsDead || !enemy.IsInitialized) return;

        // Compute actual elapsed time since last tick (handles throttled coroutine update)
        _aiDeltaTime = Time.time - _lastTickTime;
        _lastTickTime = Time.time;
        if (_aiDeltaTime <= 0f) _aiDeltaTime = Time.deltaTime;

        // Tick affix effects (inferno, spawner, regen, shield regen)
        enemy.Combat.AffixUpdate(_aiDeltaTime);

        if (playerTransform == null)
        {
            playerTransform = (GameManager.Instance != null && GameManager.Instance.Player != null)
                ? GameManager.Instance.Player.transform
                : null;
            if (playerTransform == null) return;
        }

        float distToPlayer = Vector2.Distance(transform.position, playerTransform.position);

        // Teleport affix: blink toward player
        var teleport = enemy.GetAffix(EliteAffixType.Teleport);
        if (teleport != null)
        {
            teleportCooldown -= _aiDeltaTime;
            if (teleportCooldown <= 0f && distToPlayer > teleport.TeleportRange)
            {
                PerformTeleport(teleport);
                teleportCooldown = teleport.TeleportCooldown;
            }
        }

        switch (enemy.Type)
        {
            case EnemyType.Melee:
                MoveTowardsPlayer();
                if (distToPlayer <= enemy.MeleeRange)
                    enemy.Combat.TryMeleeAttack();
                break;
            case EnemyType.Elite:
                UpdateEliteAI(distToPlayer);
                break;
            case EnemyType.Boss:
                UpdateBossAI(distToPlayer);
                break;
            case EnemyType.Ranged:
                UpdateRangedAI(distToPlayer);
                break;
            case EnemyType.Bomber:
                // Bomber rushes player, explodes on contact or death
                MoveTowardsPlayer();
                if (distToPlayer <= enemy.MeleeRange)
                    enemy.Combat.TryMeleeAttack();
                break;
            case EnemyType.Charger:
                UpdateChargerAI(distToPlayer);
                break;
            case EnemyType.Healer:
                UpdateHealerAI(distToPlayer);
                break;
            case EnemyType.Shielder:
                UpdateShielderAI(distToPlayer);
                break;
        }

        // Update cooldowns
        if (chargeCooldown > 0) chargeCooldown -= _aiDeltaTime;
        if (slamCooldown > 0) slamCooldown -= _aiDeltaTime;
    }

    private void UpdateEliteAI(float distToPlayer)
    {
        if (isCharging)
        {
            chargeTimer -= _aiDeltaTime;
            rb.velocity = chargeDir * ChargeSpeed;
            // Speed trail during charge
            if (Random.Range(0f, 1f) < 0.6f)
                VFXHelper.SpawnHitParticles(transform.position, enemy.EliteColor, 1);
            if (chargeTimer <= 0f)
            {
                isCharging = false;
                rb.velocity = Vector2.zero;
                VFXHelper.SpawnLandingDust(transform.position, chargeDir);
            }
            if (distToPlayer <= enemy.MeleeRange)
                enemy.Combat.TryMeleeAttack();
            return;
        }

        MoveTowardsPlayer();

        if (distToPlayer > enemy.MeleeRange && distToPlayer <= 3f && chargeCooldown <= 0f)
        {
            isCharging = true;
            chargeTimer = ChargeDuration;
            chargeCooldown = ChargeCooldownTime;
            chargeDir = (playerTransform.position - transform.position).normalized;
            VFXHelper.SpawnHitParticles(transform.position, enemy.EliteColor, 3);
            CameraFollow.Shake(0.08f, 0.1f);
            return;
        }

        if (distToPlayer <= enemy.MeleeRange)
            enemy.Combat.TryMeleeAttack();
    }

    private void UpdateBossAI(float distToPlayer)
    {
        // Initialize boss pattern system on first call
        if (bossPattern == null)
        {
            bossPattern = gameObject.AddComponent<BossPattern>();
            bossPattern.Initialize(enemy, spriteRenderer);
        }

        // Phase detection based on HP
        float hpPct = enemy.MaxHp > 0 ? (float)enemy.CurrentHp / enemy.MaxHp : 1f;
        int newPhase = hpPct > 0.5f ? 1 : (hpPct > 0.25f ? 2 : 3);
        if (newPhase > bossPhase)
        {
            bossPhase = newPhase;
            OnBossPhaseTransition(bossPhase);
        }

        // PoE2 telegraphed patterns — tick the pattern system
        bossPattern.Tick(_aiDeltaTime);

        // Skip normal attacks while a telegraph pattern is active
        if (bossPattern.IsTelegraphing)
        {
            // Still move toward player during telegraph
            MoveTowardsPlayer();
            return;
        }

        // Phase 3: desperate charge
        if (enrageChargeActive)
        {
            enrageChargeTimer -= _aiDeltaTime;
            rb.velocity = enrageChargeDir * EnrageChargeSpeed;
            if (enrageChargeTimer <= 0f)
            {
                enrageChargeActive = false;
                rb.velocity = Vector2.zero;
            }
            if (distToPlayer <= enemy.MeleeRange)
                enemy.Combat.TryMeleeAttack();
            return;
        }

        MoveTowardsPlayer();

        if (distToPlayer <= enemy.MeleeRange)
            enemy.Combat.TryMeleeAttack();

        if (distToPlayer > enemy.MeleeRange && distToPlayer <= enemy.AttackRange)
            enemy.Combat.TryRangedAttack();

        // Phase 1+: slam
        if (distToPlayer <= SlamRange && slamCooldown <= 0f)
        {
            enemy.Combat.PerformSlamAttack();
            slamCooldown = bossPhase >= 2 ? SlamCooldownTime * 0.6f : SlamCooldownTime;
        }

        // Phase 2+: enrage slam (shorter range, faster)
        if (bossPhase >= 2 && distToPlayer <= 3f && enrageSlamCooldown <= 0f)
        {
            enemy.Combat.PerformSlamAttack();
            CameraFollow.Shake(0.15f, 0.1f);
            enrageSlamCooldown = EnrageSlamCooldownTime;
        }

        // Phase 2+: faster ranged attacks
        if (bossPhase >= 2 && distToPlayer > enemy.MeleeRange && distToPlayer <= enemy.AttackRange * 1.3f)
        {
            enemy.Combat.TryRangedAttack();
        }

        // Phase 3: AOE burst
        if (bossPhase >= 3 && aoeCooldown <= 0f && distToPlayer <= 6f)
        {
            enemy.Combat.PerformSlamAttack();
            VFXHelper.SpawnAreaPulse(transform.position, 5f, new Color(0.8f, 0.1f, 0.8f, 0.6f));
            CameraFollow.Shake(0.25f, 0.15f);
            aoeCooldown = AoeCooldownTime;
        }

        // Phase 3: desperate charge
        if (bossPhase >= 3 && distToPlayer > 3f && distToPlayer <= 8f && enrageSlamCooldown <= 0f && !enrageChargeActive)
        {
            enrageChargeActive = true;
            enrageChargeTimer = EnrageChargeDuration;
            enrageChargeDir = (playerTransform.position - transform.position).normalized;
            VFXHelper.SpawnHitParticles(transform.position, enemy.BossColor, 4);
            CameraFollow.Shake(0.1f, 0.1f);
        }

        // Update cooldowns
        if (enrageSlamCooldown > 0) enrageSlamCooldown -= _aiDeltaTime;
        if (aoeCooldown > 0) aoeCooldown -= _aiDeltaTime;
    }

    private void OnBossPhaseTransition(int phase)
    {
        VFXHelper.SpawnAreaPulse(transform.position, 6f, new Color(1f, 0.2f, 0.2f, 0.7f));
        VFXHelper.SpawnShockwave(transform.position, new Color(1f, 0.2f, 0.2f), 6f, 12f);
        VFXHelper.SpawnScreenFlash(new Color(1f, 0.2f, 0.2f), 0.2f);
        CameraFollow.Shake(0.5f, 0.3f);
        CameraFollow.SlowMotion(0.2f, 0.15f);
        VFXHelper.SpawnHitParticles(transform.position, enemy.BossColor, 12);
        AudioManager.Instance?.PlayHit();

        // Hit squash on phase transition
        var anim = GetComponent<EnemySpriteAnimator>();
        if (anim != null) anim.PlayHitSquash();

        // Apply phase-specific scaling (only once per transition)
        if (phase == 2)
        {
            enemy.Combat.ScaleAttackSpeed(0.7f);
        }
        else if (phase == 3)
        {
            enemy.Combat.ScaleAttackSpeed(0.5f);
        }
    }

    private void UpdateRangedAI(float distToPlayer)
    {
        if (distToPlayer > enemy.AttackRange)
        {
            MoveTowardsPlayer();
        }
        else if (distToPlayer < enemy.AttackRange * 0.5f)
        {
            MoveAwayFromPlayer();
        }
        else
        {
            strafeChangeTimer -= _aiDeltaTime;
            if (strafeChangeTimer <= 0f)
            {
                strafeDir = Random.Range(0f, 1f) < 0.5f ? -1f : 1f;
                strafeChangeTimer = Random.Range(1f, 2.5f);
            }

            Vector2 toPlayer = (playerTransform.position - transform.position).normalized;
            Vector2 perp = new Vector2(-toPlayer.y, toPlayer.x) * strafeDir;
            rb.velocity = perp * enemy.MoveSpeed * 0.6f;

            if (toPlayer.x < 0) spriteRenderer.flipX = true;
            else if (toPlayer.x > 0) spriteRenderer.flipX = false;
        }

        enemy.Combat.TryRangedAttack();
    }

    private void MoveTowardsPlayer()
    {
        Vector2 dir = (playerTransform.position - transform.position).normalized;

        stuckCheckTimer -= _aiDeltaTime;
        if (stuckCheckTimer <= 0f)
        {
            float moved = Vector3.Distance(transform.position, lastStuckPos);
            if (moved < 0.15f && Vector2.Distance(transform.position, playerTransform.position) > enemy.MeleeRange)
                stuckWiggleDir = Random.Range(0.5f, 1f) * (Random.Range(0f, 1f) < 0.5f ? 1f : -1f);
            else
                stuckWiggleDir *= 0.5f;
            lastStuckPos = transform.position;
            stuckCheckTimer = 0.4f;
        }

        Vector2 perp = new Vector2(-dir.y, dir.x) * stuckWiggleDir;

        // Separation from nearby enemies — 简化：不用OverlapCircleNonAlloc，避免Tuanjie引擎Bug
        Vector2 separation = Vector2.zero;

        Vector2 moveDir = (dir + perp + separation).normalized;
        rb.velocity = moveDir * enemy.MoveSpeed;

        if (dir.x < 0) spriteRenderer.flipX = true;
        else if (dir.x > 0) spriteRenderer.flipX = false;
    }

    private void MoveAwayFromPlayer()
    {
        Vector2 dir = (transform.position - playerTransform.position).normalized;
        rb.velocity = dir * enemy.MoveSpeed * 0.5f;
    }

    private void PerformTeleport(EliteAffix teleport)
    {
        // Fade out VFX
        VFXHelper.SpawnHitParticles(transform.position, teleport.AuraColor, 5);

        // Move to a position near the player
        Vector2 offset = Random.insideUnitCircle * 2f;
        Vector3 targetPos = playerTransform.position + (Vector3)offset;

        // Clamp to map bounds
        if (GameManager.Instance != null)
        {
            var mapData = DungeonMapData.GetStageMap(GameManager.Instance.CurrentStageIndex);
            float clampX = mapData.mapHalfWidth - 2f;
            float clampY = mapData.mapHalfHeight - 2f;
            targetPos.x = Mathf.Clamp(targetPos.x, -clampX, clampX);
            targetPos.y = Mathf.Clamp(targetPos.y, -clampY, clampY);
        }

        transform.position = targetPos;
        rb.velocity = Vector2.zero;

        // Fade in VFX
        VFXHelper.SpawnAreaPulse(transform.position, 1.5f, teleport.AuraColor);
        CameraFollow.Shake(0.08f, 0.06f);
    }

    // === Charger: periodic charge attack ===
    private float _chargerCooldown;
    private float _chargerDuration;
    private Vector2 _chargerDir;

    private void UpdateChargerAI(float distToPlayer)
    {
        _chargerCooldown -= _aiDeltaTime;
        if (_chargerCooldown <= 0f && distToPlayer > enemy.MeleeRange && distToPlayer < 8f)
        {
            // Start charge
            _chargerDir = (playerTransform.position - transform.position).normalized;
            _chargerDuration = ChargeDuration;
            _chargerCooldown = ChargeCooldownTime;
            VFXHelper.SpawnAreaPulse(transform.position, 1f, new Color(1f, 0.3f, 0f, 0.5f));
        }

        if (_chargerDuration > 0f)
        {
            _chargerDuration -= _aiDeltaTime;
            rb.velocity = _chargerDir * ChargeSpeed;
            if (_chargerDuration <= 0f) rb.velocity = Vector2.zero;
            if (distToPlayer <= enemy.MeleeRange)
                enemy.Combat.TryMeleeAttack();
        }
        else
        {
            MoveTowardsPlayer();
            if (distToPlayer <= enemy.MeleeRange)
                enemy.Combat.TryMeleeAttack();
        }
    }

    // === Healer: heals nearby enemies periodically ===
    private float _healTimer;
    private const float HealInterval = 5f;
    private const float HealRadius = 4f;
    private const int HealAmount = 15;
    private static readonly Collider2D[] _healBuffer = new Collider2D[16];

    private void UpdateHealerAI(float distToPlayer)
    {
        // Keep distance from player, stay behind other enemies
        if (distToPlayer < 3f)
            MoveAwayFromPlayer();
        else if (distToPlayer > 6f)
            MoveTowardsPlayer();
        else
            rb.velocity *= 0.8f;

        // Heal pulse
        _healTimer -= _aiDeltaTime;
        if (_healTimer <= 0f)
        {
            _healTimer = HealInterval;
            int count = Physics2D.OverlapCircleNonAlloc(transform.position, HealRadius, _healBuffer, LayerMask.GetMask("Enemy"));
            int healed = 0;
            for (int i = 0; i < count; i++)
            {
                var ec = _healBuffer[i].GetComponent<EnemyController>();
                if (ec != null && ec != enemy && !ec.IsDead && ec.CurrentHp < ec.MaxHp)
                {
                    ec.Combat.Heal(HealAmount);
                    VFXHelper.SpawnHealEffect(ec.transform.position, HealAmount);
                    healed++;
                }
            }
            if (healed > 0)
                VFXHelper.SpawnAreaPulse(transform.position, HealRadius, new Color(0.3f, 1f, 0.4f, 0.4f));
        }
    }

    // === Shielder: gives shield to nearby enemies ===
    private float _shieldTimer;
    private const float ShieldInterval = 6f;
    private const float ShieldRadius = 4f;
    private const int ShieldAmount = 20;
    private static readonly Collider2D[] _shieldBuffer = new Collider2D[16];

    private void UpdateShielderAI(float distToPlayer)
    {
        // Slow approach, similar to melee but slower
        if (distToPlayer > enemy.MeleeRange)
        {
            MoveTowardsPlayer();
        }
        else
        {
            enemy.Combat.TryMeleeAttack();
        }

        // Shield pulse
        _shieldTimer -= _aiDeltaTime;
        if (_shieldTimer <= 0f)
        {
            _shieldTimer = ShieldInterval;
            int count = Physics2D.OverlapCircleNonAlloc(transform.position, ShieldRadius, _shieldBuffer, LayerMask.GetMask("Enemy"));
            int shielded = 0;
            for (int i = 0; i < count; i++)
            {
                var ec = _shieldBuffer[i].GetComponent<EnemyController>();
                if (ec != null && ec != enemy && !ec.IsDead)
                {
                    ec.Combat.AddShield(ShieldAmount);
                    shielded++;
                }
            }
            if (shielded > 0)
                VFXHelper.SpawnAreaPulse(transform.position, ShieldRadius, new Color(0.3f, 0.5f, 1f, 0.4f));
        }
    }
}
