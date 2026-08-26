using UnityEngine;
using System.Collections.Generic;

public class EnemyCombatController : MonoBehaviour
{
    private static readonly Collider2D[] _slamBuffer = new Collider2D[16];
    private static readonly Collider2D[] _chainBuffer = new Collider2D[32];
    private static float _globalVfxTimer = 0f; // 全局VFX节流：所有敌人共享
    private const float GlobalVfxInterval = 0.15f; // 全局每0.15秒最多1次敌人受击VFX

    private EnemyController enemy;
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private StatusEffectManager _cachedStatusFx;
    private SpriteRenderer[] _cachedSpriteRenderers;
    private Transform playerTransform;
    private PlayerController playerCache;

    private int attack;
    private int defense;
    private int currentHp;
    private int maxHp;
    private float moveSpeed;
    private float lastAttackTime;
    private float attackCooldownScale = 1f;
    private EnemyHealthBar healthBar;
    private EnemySpriteAnimator spriteAnimator;

    // Affix runtime state
    private int shieldCurrentHp;
    private bool shieldBroken;
    private float shieldRegenTimer;
    private float infernoTimer;
    private float spawnerTimer;
    private float regenTimer;
    private bool chainDeathTriggered;
    private bool hasChainBuff;

    public bool IsDead { get; private set; }
    public int XpReward { get; private set; }
    public int CurrentHp => currentHp;
    public int MaxHp => maxHp;
    public float MoveSpeed => moveSpeed;
    public int ShieldCurrentHp => shieldCurrentHp;
    public bool ShieldBroken => shieldBroken;

    public void Initialize(EnemyController enemy, Rigidbody2D rb, SpriteRenderer spriteRenderer)
    {
        this.enemy = enemy;
        this.rb = rb;
        this.spriteRenderer = spriteRenderer;
        _cachedStatusFx = enemy.GetComponent<StatusEffectManager>();
    }

    public void SetupStats(int dungeonLevel)
    {
        // 从数据库配置读取缩放参数 (回退到硬编码默认值)
        float scaleEarly = 0.7f, scaleEarlyStep = 0.15f;
        float scaleMid = 1.0f, scaleMidStep = 0.2f;
        float scaleLate = 1.6f, scaleLateStep = 0.35f;
        float moveSpdEarlyMult = 0.8f, moveSpdMidStep = 0.15f;
        float moveSpdLateBase = 0.45f, moveSpdLateStep = 0.2f;
        float enemyAtkMult = 1.5f;
        float bossHpMult = 3f, bossAtkMult = 1.5f;
        int bossDefBonusBase = 1;
        float bossXpMult = 5f;
        float eliteHpMult = 2f, eliteAtkMult = 1.3f, eliteXpMult = 3f;
        int atkCdScaleStart = 4;
        float atkCdScalePer = 0.08f, atkCdScaleMin = 0.4f;

        if (GameConfigManager.Instance != null && GameConfigManager.Instance.IsLoaded)
        {
            var cfg = GameConfigManager.Instance.GetEnemyData((int)enemy.Type);
            if (cfg != null)
            {
                scaleEarly = cfg.scaleEarly; scaleEarlyStep = cfg.scaleEarlyStep;
                scaleMid = cfg.scaleMid; scaleMidStep = cfg.scaleMidStep;
                scaleLate = cfg.scaleLate; scaleLateStep = cfg.scaleLateStep;
                moveSpdEarlyMult = cfg.moveSpeedEarlyMult;
                moveSpdMidStep = cfg.moveSpeedMidStep;
                moveSpdLateBase = cfg.moveSpeedLateBase;
                moveSpdLateStep = cfg.moveSpeedLateStep;
                enemyAtkMult = cfg.enemyAttackMult;
                bossHpMult = cfg.bossHpMult; bossAtkMult = cfg.bossAtkMult;
                bossDefBonusBase = cfg.bossDefBonusBase;
                bossXpMult = cfg.bossXpMult;
                eliteHpMult = cfg.eliteHpMult;
                eliteAtkMult = cfg.eliteAtkMult;
                eliteXpMult = cfg.eliteXpMult;
                atkCdScaleStart = cfg.attackCdScaleStartLevel;
                atkCdScalePer = cfg.attackCdScalePerLevel;
                atkCdScaleMin = cfg.attackCdScaleMin;
            }
        }

        float scale;
        if (dungeonLevel <= 2)
            scale = scaleEarly + (dungeonLevel - 1) * scaleEarlyStep;
        else if (dungeonLevel <= 4)
            scale = scaleMid + (dungeonLevel - 3) * scaleMidStep;
        else
            scale = scaleLate + (dungeonLevel - 5) * scaleLateStep;

        // Stage-based difficulty multiplier — later stages are harder
        int stageIndex = GameManager.Instance != null ? GameManager.Instance.CurrentStageIndex : 0;
        float stageMult = 1f + stageIndex * 0.15f; // +15% per stage
        scale *= stageMult;

        maxHp = Mathf.RoundToInt(enemy.BaseHp * scale);
        currentHp = maxHp;
        attack = Mathf.RoundToInt(enemy.BaseAttack * scale * enemyAtkMult);
        defense = Mathf.RoundToInt(enemy.BaseDefense * scale);

        if (dungeonLevel <= 2)
            moveSpeed = enemy.BaseMoveSpeed * moveSpdEarlyMult;
        else if (dungeonLevel <= 5)
            moveSpeed = enemy.BaseMoveSpeed + (dungeonLevel - 3) * moveSpdMidStep;
        else
            moveSpeed = enemy.BaseMoveSpeed + moveSpdLateBase + (dungeonLevel - 5) * moveSpdLateStep;

        XpReward = Mathf.RoundToInt(enemy.BaseXpReward * scale);

        if (dungeonLevel >= atkCdScaleStart)
            attackCooldownScale = Mathf.Max(atkCdScaleMin, 1f - (dungeonLevel - atkCdScaleStart) * atkCdScalePer);
        else
            attackCooldownScale = 1f;

        if (enemy.Type == EnemyType.Boss)
        {
            // Boss gets harder per stage — PoE2-style escalation
            float stageBossHpMult = bossHpMult * (1f + stageIndex * 0.1f); // +10% HP per stage
            maxHp = Mathf.RoundToInt(maxHp * stageBossHpMult);
            currentHp = maxHp;
            attack = Mathf.RoundToInt(attack * bossAtkMult * (1f + stageIndex * 0.05f));
            defense += bossDefBonusBase * dungeonLevel + stageIndex * 2;
            XpReward = Mathf.RoundToInt(XpReward * bossXpMult);
        }
        else if (enemy.Type == EnemyType.Elite)
        {
            float stageEliteHpMult = eliteHpMult * (1f + stageIndex * 0.08f);
            maxHp = Mathf.RoundToInt(maxHp * stageEliteHpMult);
            currentHp = maxHp;
            attack = Mathf.RoundToInt(attack * eliteAtkMult * (1f + stageIndex * 0.04f));
            XpReward = Mathf.RoundToInt(XpReward * eliteXpMult);
        }
        else if (enemy.Type == EnemyType.Bomber)
        {
            // Bomber: lower HP, higher attack (suicide unit)
            maxHp = Mathf.RoundToInt(maxHp * 0.7f);
            currentHp = maxHp;
            attack = Mathf.RoundToInt(attack * 1.3f);
            XpReward = Mathf.RoundToInt(XpReward * 1.2f);
        }
        else if (enemy.Type == EnemyType.Charger)
        {
            // Charger: moderate HP, faster move speed
            maxHp = Mathf.RoundToInt(maxHp * 0.9f);
            currentHp = maxHp;
            moveSpeed *= 1.2f;
        }
        else if (enemy.Type == EnemyType.Healer)
        {
            // Healer: low HP and attack, higher XP (support unit)
            maxHp = Mathf.RoundToInt(maxHp * 0.8f);
            currentHp = maxHp;
            attack = Mathf.RoundToInt(attack * 0.6f);
            XpReward = Mathf.RoundToInt(XpReward * 1.5f);
        }
        else if (enemy.Type == EnemyType.Shielder)
        {
            // Shielder: high HP and defense, low attack (tank)
            maxHp = Mathf.RoundToInt(maxHp * 1.4f);
            currentHp = maxHp;
            defense = Mathf.RoundToInt(defense * 1.5f);
            attack = Mathf.RoundToInt(attack * 0.7f);
            XpReward = Mathf.RoundToInt(XpReward * 1.3f);
        }

        IsDead = false;

        // Apply affixes
        ApplyAffixSetup();
    }

    private void ApplyAffixSetup()
    {
        if (enemy.Affixes == null || enemy.Affixes.Count == 0) return;

        // Haste: speed + attack speed
        var haste = enemy.GetAffix(EliteAffixType.Haste);
        if (haste != null)
        {
            moveSpeed *= haste.HasteSpeedMult;
            attackCooldownScale *= haste.HasteAttackMult;
        }

        // Shield: set shield HP
        var shield = enemy.GetAffix(EliteAffixType.Shield);
        if (shield != null)
        {
            shieldCurrentHp = shield.ShieldHp;
            shieldBroken = false;
            shieldRegenTimer = 0f;
        }

        infernoTimer = 0f;
        spawnerTimer = 0f;
        regenTimer = 0f;
    }

    public void AffixUpdate(float dt = -1f)
    {
        if (IsDead || enemy.Affixes == null || enemy.Affixes.Count == 0) return;
        if (dt < 0f) dt = Time.deltaTime;

        // Inferno: burn nearby player
        var inferno = enemy.GetAffix(EliteAffixType.Inferno);
        if (inferno != null && playerTransform != null)
        {
            infernoTimer -= dt;
            if (infernoTimer <= 0f)
            {
                infernoTimer = 1f;
                float dist = Vector2.Distance(transform.position, playerTransform.position);
                if (dist <= inferno.AuraRadius)
                {
                    if (playerCache == null)
                        playerCache = playerTransform.GetComponent<PlayerController>();
                    if (playerCache != null && !playerCache.IsDead)
                    {
                        playerCache.TakeDamage(inferno.AuraDamage);
                        VFXHelper.SpawnHitParticles(playerTransform.position, new Color(1f, 0.8f, 0.3f), 1);
                    }
                }
            }
        }

        // Spawner: spawn minions periodically
        var spawner = enemy.GetAffix(EliteAffixType.Spawner);
        if (spawner != null)
        {
            spawnerTimer -= dt;
            if (spawnerTimer <= 0f)
            {
                spawnerTimer = spawner.SpawnInterval;
                SpawnMinions(spawner.SpawnCount);
            }
        }

        // Regeneration: regen HP over time
        var regen = enemy.GetAffix(EliteAffixType.Regeneration);
        if (regen != null && currentHp < maxHp)
        {
            regenTimer -= dt;
            if (regenTimer <= 0f)
            {
                regenTimer = 1f;
                int healAmt = Mathf.Min(regen.RegenPerSecond, maxHp - currentHp);
                currentHp += healAmt;
                if (healthBar != null) healthBar.UpdateHp(currentHp);
            }
        }

        // Shield: regen after delay
        var shield = enemy.GetAffix(EliteAffixType.Shield);
        if (shield != null && shieldBroken)
        {
            shieldRegenTimer -= dt;
            if (shieldRegenTimer <= 0f)
            {
                shieldCurrentHp = shield.ShieldHp;
                shieldBroken = false;
                VFXHelper.SpawnAreaPulse(transform.position, 1.5f, new Color(0.3f, 0.7f, 1f, 0.5f));
            }
        }
    }

    private void SpawnMinions(int count)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 offset = Random.insideUnitCircle * 2f;
            Vector3 spawnPos = transform.position + (Vector3)offset;

            GameObject minionObj = new GameObject("Minion");
            minionObj.transform.position = spawnPos;
            minionObj.layer = LayerMask.NameToLayer("Enemy");

            var sr = minionObj.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteCache.WhitePixel;
            sr.color = new Color(0.4f, 0.9f, 0.3f);
            sr.sortingOrder = 1;

            var rb2d = minionObj.AddComponent<Rigidbody2D>();
            rb2d.gravityScale = 0f;
            rb2d.freezeRotation = true;
            rb2d.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var col = minionObj.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.8f, 0.8f);

            var ec = minionObj.AddComponent<EnemyController>();
            ec.Type = EnemyType.Melee;
            // 从配置读取小怪属性
            if (GameConfigManager.Instance != null && GameConfigManager.Instance.IsLoaded)
            {
                var cfg = GameConfigManager.Instance.GetEnemyData((int)EnemyType.Melee);
                if (cfg != null)
                {
                    ec.BaseHp = cfg.minionBaseHp;
                    ec.BaseAttack = cfg.minionBaseAttack;
                    ec.BaseDefense = 0;
                    ec.BaseMoveSpeed = cfg.minionBaseMoveSpeed;
                    ec.BaseXpReward = cfg.minionBaseXpReward;
                }
                else
                {
                    ec.BaseHp = 10; ec.BaseAttack = 4;
                    ec.BaseDefense = 0; ec.BaseMoveSpeed = 3f;
                    ec.BaseXpReward = 5;
                }
            }
            else
            {
                ec.BaseHp = 10; ec.BaseAttack = 4;
                ec.BaseDefense = 0; ec.BaseMoveSpeed = 3f;
                ec.BaseXpReward = 5;
            }

            EnemySprite.ApplySprite(minionObj, EnemyType.Melee);
            minionObj.transform.localScale = new Vector3(0.7f, 0.7f, 1f);


            ec.Initialize(GameManager.Instance != null ? GameManager.Instance.DungeonLevel : 1);

            // Track spawned minion in combat director
            if (CombatDirector.Instance != null)
                CombatDirector.Instance.AddSpawnedEnemy();
        }
    }

    public void SetupHealthBar()
    {
        healthBar = gameObject.AddComponent<EnemyHealthBar>();
        healthBar.Initialize(transform, maxHp);

        // Setup sprite animator on EnemyVisual child
        var visual = transform.Find("EnemyVisual");
        if (visual != null)
        {
            spriteAnimator = gameObject.AddComponent<EnemySpriteAnimator>();
            spriteAnimator.Init(visual);
        }
    }

    public void SetPlayerTransform(Transform t)
    {
        playerTransform = t;
        playerCache = t != null ? t.GetComponent<PlayerController>() : null;
    }

    public void ScaleAttackSpeed(float cooldownMult)
    {
        attackCooldownScale *= cooldownMult;
    }

    public void TryMeleeAttack()
    {
        float cd = enemy.AttackCooldown * attackCooldownScale;
        if (Time.time - lastAttackTime < cd) return;
        lastAttackTime = Time.time;

        EnsurePlayerRef();
        if (playerCache != null && !playerCache.IsDead)
        {
            Vector2 lungeDir = (playerTransform.position - transform.position).normalized;
            playerCache.TakeDamage(attack);
            // Attack lunge
            if (rb != null)
                rb.velocity = lungeDir * 6f;
            // Animation + VFX
            if (spriteAnimator != null) spriteAnimator.PlayMeleeLunge(lungeDir);
            VFXHelper.SpawnSlashEffect(playerTransform.position, -lungeDir, 1.5f, new Color(1f, 0.8f, 0.3f, 0.6f));
            VFXHelper.SpawnImpactFlash(playerTransform.position, new Color(1f, 0.8f, 0.3f), 0.5f);
        }
    }

    public void TryRangedAttack()
    {
        float cd = enemy.AttackCooldown * attackCooldownScale;
        if (Time.time - lastAttackTime < cd) return;
        lastAttackTime = Time.time;

        EnsurePlayerRef();
        if (playerTransform != null)
        {
            Vector2 dir = (playerTransform.position - transform.position).normalized;
            if (spriteAnimator != null) spriteAnimator.PlayRangedCast(dir);
        }
        SpawnProjectile();
    }

    private void EnsurePlayerRef()
    {
        if (playerTransform == null || playerTransform.Equals(null) ||
            (GameManager.Instance != null && GameManager.Instance.Player != null &&
             playerTransform != GameManager.Instance.Player.transform))
        {
            if (GameManager.Instance != null && GameManager.Instance.Player != null)
            {
                playerTransform = GameManager.Instance.Player.transform;
                playerCache = GameManager.Instance.Player;
            }
        }
        else if (playerCache == null && playerTransform != null)
        {
            playerCache = playerTransform.GetComponent<PlayerController>();
        }
    }

    public void PerformSlamAttack()
    {
        // Jump animation
        if (spriteAnimator != null) spriteAnimator.PlaySlamJump();

        VFXHelper.SpawnGroundSlam(transform.position, 3f, enemy.BossColor);
        VFXHelper.SpawnShockwave(transform.position, enemy.BossColor, 4f, 10f);
        CameraFollow.Shake(0.4f, 0.25f);

        int slamCount = Physics2D.OverlapCircleNonAlloc(transform.position, 3f, _slamBuffer);
        for (int i = 0; i < slamCount; i++)
        {
            if (_slamBuffer[i].CompareTag("Player"))
            {
                PlayerController player = _slamBuffer[i].GetComponent<PlayerController>();
                if (player != null)
                    player.TakeDamage(Mathf.RoundToInt(attack * 1.5f));
            }
        }

        VFXHelper.SpawnHitParticles(transform.position, enemy.BossColor, 3);
    }

    private void SpawnProjectile()
    {
        if (playerTransform == null) return;
        Vector2 dir = (playerTransform.position - transform.position).normalized;

        GameObject projObj = VFXPool.GetEnemyProjectile();
        projObj.transform.position = transform.position;
        projObj.layer = 0;

        var sr = projObj.GetComponent<SpriteRenderer>();
        sr.sprite = VFXHelper.GetSharedWhiteSprite();
        sr.color = new Color(1f, 0.8f, 0.3f);
        sr.sortingOrder = 6;
        projObj.transform.localScale = new Vector3(0.3f, 0.3f, 1f);

        var rb2d = projObj.GetComponent<Rigidbody2D>();
        rb2d.gravityScale = 0f;
        rb2d.velocity = dir * 6f;

        var col = projObj.GetComponent<CircleCollider2D>();
        col.radius = 0.4f;
        col.isTrigger = true;

        var ep = projObj.GetComponent<EnemyProjectile>();
        ep.Initialize(dir, attack);

        // Muzzle flash + trail
        VFXHelper.SpawnProjectileMuzzleFlash(transform.position, dir, new Color(1f, 0.8f, 0.3f));
    }

    /// <summary>Heal this enemy (used by Healer enemy type).</summary>
    public void Heal(int amount)
    {
        if (IsDead) return;
        currentHp = Mathf.Min(currentHp + amount, maxHp);
    }

    /// <summary>Add shield to this enemy (used by Shielder enemy type).</summary>
    public void AddShield(int amount)
    {
        if (IsDead) return;
        shieldCurrentHp += amount;
        shieldBroken = false;
    }

    private static int _batchHitCount = 0; // 批量命中计数（每帧重置）
    private static bool _hitSoundPlayed = false; // 音效每帧只播1次
    private static float _frameResetTimer = 0f;

    public void TakeDamage(int rawDamage, PlayerController attacker)
    {
        if (IsDead) return;

        // 每帧重置批量计数
        if (_frameResetTimer < Time.time)
        {
            _frameResetTimer = Time.time;
            _batchHitCount = 0;
            _hitSoundPlayed = false;
        }

        // ===== 批量伤害计算（不触发任何表现）=====
        var statusFx = _cachedStatusFx;
        if (statusFx != null)
        {
            if (statusFx.IsFrozen && (Random.Range(0f, 1f) < 0.25f || rawDamage >= enemy.BaseHp * 0.3f))
            {
                statusFx.TryShatter(rawDamage);
                return;
            }
            rawDamage = Mathf.RoundToInt(rawDamage * statusFx.GetDamageMultiplier());
        }

        int effectiveDefense = defense;
        if (statusFx != null)
            effectiveDefense = Mathf.RoundToInt(defense * statusFx.GetDefenseMultiplier());

        var shield = (enemy.Affixes != null && enemy.Affixes.Count > 0) ? enemy.GetAffix(EliteAffixType.Shield) : null;
        if (shield != null && !shieldBroken && shieldCurrentHp > 0)
        {
            if (rawDamage >= shieldCurrentHp)
            {
                rawDamage -= shieldCurrentHp;
                shieldCurrentHp = 0;
                shieldBroken = true;
                shieldRegenTimer = shield.ShieldRegenDelay;
            }
            else
            {
                shieldCurrentHp -= rawDamage;
                if (healthBar != null) healthBar.UpdateHp(currentHp);
                return; // 护盾吸收，不触发受伤表现
            }
        }

        int actualDamage = Mathf.Max(1, rawDamage - effectiveDefense);
        currentHp -= actualDamage;

        // 反伤（只在前3个怪触发）
        if (_batchHitCount < 3)
        {
            var reflect = (enemy.Affixes != null && enemy.Affixes.Count > 0) ? enemy.GetAffix(EliteAffixType.Reflect) : null;
            if (reflect != null && attacker != null && !attacker.IsDead)
            {
                int reflectDmg = Mathf.RoundToInt(actualDamage * reflect.ReflectPct);
                if (reflectDmg > 0)
                    attacker.TakeDamage(reflectDmg);
            }
        }

        if (healthBar != null) healthBar.UpdateHp(currentHp);

        // ===== 受击表现：只前3个怪播放，音效只播1次 =====
        if (_batchHitCount < 3)
        {
            _batchHitCount++;

            // 全局VFX节流
            _globalVfxTimer -= Time.unscaledDeltaTime;
            if (_globalVfxTimer <= 0)
            {
                _globalVfxTimer = GlobalVfxInterval;
                VFXHelper.SpawnImpactFlash(transform.position, new Color(1f, 0.9f, 0.3f), 0.3f);
            }

            // FlashWhite
            if (_cachedSpriteRenderers == null)
                _cachedSpriteRenderers = GetComponentsInChildren<SpriteRenderer>();
            if (_cachedSpriteRenderers.Length > 0)
                StartCoroutine(FlashWhite());

            // 音效只播1次
            if (!_hitSoundPlayed)
            {
                _hitSoundPlayed = true;
                AudioManager.Instance?.PlayHit();
            }
        }

        if (currentHp <= 0)
            Die();
    }

    private System.Collections.IEnumerator HitStop(float duration)
    {
        if (rb != null)
        {
            rb.velocity *= 0.3f;
            yield return new WaitForSeconds(duration);
        }
    }

    // 死亡批量处理已移至CombatDirector.ProcessBatchDeaths

    private void Die()
    {
        IsDead = true;
        rb.velocity = Vector2.zero;
        // 禁用碰撞体
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
        // 立即隐藏血条
        if (healthBar != null) healthBar.Cleanup();
        // 注册到批量死亡处理器，不执行任何其他逻辑
        if (CombatDirector.Instance != null)
            CombatDirector.Instance.RegisterDeath(enemy);
    }

    /// <summary>处理死亡相关词缀（Chain/Bomber）— 由批量处理器调用</summary>
    public void ProcessDeathAffixs()
    {
        // Chain affix: buff nearby enemies on death
        var chain = (enemy.Affixes != null && enemy.Affixes.Count > 0) ? enemy.GetAffix(EliteAffixType.Chain) : null;
        if (chain != null && !chainDeathTriggered)
        {
            chainDeathTriggered = true;
            TriggerChainDeath(chain);
        }

        // Bomber: explode on death
        if (enemy.Type == EnemyType.Bomber)
        {
            VFXHelper.SpawnFireExplosion(transform.position, 3f);
            VFXHelper.SpawnAreaPulse(transform.position, 3f, new Color(1f, 0.6f, 0.2f, 0.6f));
            var player = GameManager.Instance?.Player;
            if (player != null && !player.IsDead)
            {
                float dist = Vector2.Distance(transform.position, player.transform.position);
                if (dist <= 3f)
                {
                    int explosionDmg = Mathf.RoundToInt(attack * 1.5f);
                    player.TakeDamage(explosionDmg);
                }
            }
        }
    }

    private void TriggerChainDeath(EliteAffix chain)
    {
        VFXHelper.SpawnAreaPulse(transform.position, chain.ChainBuffRadius, new Color(1f, 0.6f, 0.2f, 0.7f));
        CameraFollow.Shake(0.15f, 0.1f);

        int hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, chain.ChainBuffRadius, _chainBuffer, LayerMask.GetMask("Enemy"));
        for (int i = 0; i < hitCount; i++)
        {
            EnemyController nearby = _chainBuffer[i].GetComponent<EnemyController>();
            if (nearby != null && nearby != enemy && !nearby.IsDead)
            {
                // Buff: +30% attack + speed for duration
                nearby.Combat.ApplyChainBuff(chain.ChainBuffDuration);
            }
        }
    }

    public void ApplyChainBuff(float duration)
    {
        // Prevent stacking
        if (hasChainBuff) return;
        hasChainBuff = true;

        // Store the flat bonus amounts for clean removal
        int atkBonus = Mathf.RoundToInt(attack * 0.3f);
        float spdBonus = moveSpeed * 0.3f;
        attack += atkBonus;
        moveSpeed += spdBonus;
        VFXHelper.SpawnHitParticles(transform.position, new Color(1f, 0.6f, 0.2f), 2);
        StartCoroutine(RemoveChainBuff(duration, atkBonus, spdBonus));
    }

    private System.Collections.IEnumerator RemoveChainBuff(float delay, int atkBonus, float spdBonus)
    {
        yield return new WaitForSeconds(delay);
        if (!IsDead)
        {
            attack -= atkBonus;
            moveSpeed -= spdBonus;
            hasChainBuff = false;
        }
    }

    private static readonly Color[] _flashColorBuffer = new Color[16]; // 静态复用避免GC

    private System.Collections.IEnumerator FlashWhite()
    {
        if (_cachedSpriteRenderers == null)
            _cachedSpriteRenderers = GetComponentsInChildren<SpriteRenderer>();
        int len = _cachedSpriteRenderers.Length;
        if (len == 0) yield break;
        if (len > 16) len = 16; // 静态缓冲区上限

        for (int i = 0; i < len; i++)
        {
            _flashColorBuffer[i] = _cachedSpriteRenderers[i].color;
            _cachedSpriteRenderers[i].color = Color.white;
        }

        yield return null; // 只等1帧,不等待0.05秒

        for (int i = 0; i < len; i++)
            if (_cachedSpriteRenderers[i] != null) _cachedSpriteRenderers[i].color = _flashColorBuffer[i];
    }
}
