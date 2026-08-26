using UnityEngine;

public class PlayerProjectile : MonoBehaviour
{
    private void OnEnable() => SceneRegistry.Register(this);
    private void OnDisable() { SceneRegistry.Unregister(this); ResetState(); }

    public float Speed = 8f;
    public float Lifetime = 3f;

    public Vector2 Direction => direction;
    public int Damage => damage;

    private Vector2 direction;
    private int damage;
    private PlayerController attacker;
    private Rigidbody2D rb;
    private Color projectileColor = new Color(0.4f, 0.6f, 1f);
    private float explodeRadius;
    private bool hasExplodeRadius;

    // 变异参数
    private int splitCount;
    private float frostZoneDuration;
    private bool _isRelicSpawned; // Prevents SplitShot relic infinite recursion

    // 符文参数
    private int pierceCount;
    private int chainCount;
    private int chainDepth; // tracks recursion depth
    private const int MaxChainDepth = 3;
    private SkillData runeSource;
    private System.Collections.Generic.HashSet<int> hitEnemyIds = new System.Collections.Generic.HashSet<int>();

    // Cached buffers to avoid per-hit allocations
    private static readonly Collider2D[] _chainBuffer = new Collider2D[16];
    private static readonly Collider2D[] _aoeBuffer = new Collider2D[32];

    private float trailTimer;
    private const float TrailInterval = 0.015f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void Initialize(Vector2 dir, int dmg, PlayerController owner = null)
    {
        direction = dir.normalized;
        damage = dmg;
        attacker = owner;
        if (rb != null)
            rb.velocity = direction * Speed;
        // Auto-return to pool after lifetime expires
        StartCoroutine(ReturnAfterLifetime());
    }

    private System.Collections.IEnumerator ReturnAfterLifetime()
    {
        yield return new WaitForSeconds(Lifetime);
        VFXPool.Return("player_proj", gameObject);
    }

    /// <summary>Reset all state before returning to pool or reusing.</summary>
    private void ResetState()
    {
        StopAllCoroutines();
        hitEnemyIds.Clear();
        splitCount = 0;
        frostZoneDuration = 0;
        _isRelicSpawned = false;
        pierceCount = 0;
        chainCount = 0;
        chainDepth = 0;
        runeSource = null;
        hasExplodeRadius = false;
        explodeRadius = 0;
        trailTimer = 0;
        if (rb != null) rb.velocity = Vector2.zero;
        transform.localScale = Vector3.one;
    }

    public void SetColor(Color color)
    {
        projectileColor = color;
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = color;
    }

    /// <summary>标记为遗物生成的投射物, 防止SplitShot无限递归</summary>
    public void SetRelicSpawned() { _isRelicSpawned = true; }

    public void SetExplodeRadius(float radius)
    {
        explodeRadius = radius;
        hasExplodeRadius = radius > 0;
    }

    public void SetPierce(int count) { pierceCount = count; }
    public void SetChain(int count) { chainCount = count; chainDepth = 0; }
    private void SetChainWithDepth(int count, int depth) { chainCount = count; chainDepth = depth; }
    public void SetRuneData(SkillData skill) { runeSource = skill; }
    public void SetSplit(int count) { splitCount = count; }
    public void SetFrostZone(float duration) { frostZoneDuration = duration; }

    private void Update()
    {
        // Continuous trail particles
        trailTimer += Time.deltaTime;
        if (trailTimer >= TrailInterval)
        {
            trailTimer = 0f;
            // 尾迹已优化
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        EnemyController enemy = other.GetComponent<EnemyController>();
        if (enemy != null && !enemy.IsDead && !hitEnemyIds.Contains(enemy.GetInstanceID()))
        {
            hitEnemyIds.Add(enemy.GetInstanceID());
            enemy.TakeDamage(damage, attacker);
            VFXHelper.SpawnDamageNumber(enemy.transform.position, damage, damage > 30);
            VFXHelper.SpawnProjectileHitEffect(enemy.transform.position, direction, projectileColor, damage > 30);
            CameraFollow.Shake(0.15f, 0.1f);

            // 符文效果：减速
            if (runeSource != null && runeSource.HasSlow)
            {
                var enemyRb = enemy.GetComponent<Rigidbody2D>();
                if (enemyRb != null) enemyRb.velocity *= (1f - runeSource.SlowPct);
            }

            // 符文效果：吸血
            if (runeSource != null && runeSource.LifeStealPct > 0 && attacker != null)
            {
                int healAmt = Mathf.Max(1, Mathf.RoundToInt(damage * runeSource.LifeStealPct));
                attacker.Heal(healAmt);
            }

            // 穿透符文：不销毁，继续飞行
            if (pierceCount > 0)
            {
                pierceCount--;
                VFXHelper.SpawnHitParticles(enemy.transform.position, projectileColor, 3);
                return;
            }

            // AOE爆炸
            if (hasExplodeRadius)
            {
                VFXHelper.SpawnFireExplosion(transform.position, explodeRadius);
                int aoeCount = Physics2D.OverlapCircleNonAlloc(transform.position, explodeRadius, _aoeBuffer, LayerMask.GetMask("Enemy"));
                for (int i = 0; i < aoeCount; i++)
                {
                    EnemyController aoeEnemy = _aoeBuffer[i].GetComponent<EnemyController>();
                    if (aoeEnemy != null && !aoeEnemy.IsDead && aoeEnemy != enemy)
                    {
                        int aoeDmg = Mathf.RoundToInt(damage * 0.6f);
                        aoeEnemy.TakeDamage(aoeDmg, attacker);
                        VFXHelper.SpawnDamageNumber(aoeEnemy.transform.position, aoeDmg, false);
                        VFXHelper.SpawnHitParticles(aoeEnemy.transform.position, projectileColor, 3);
                    }
                }
            }
            else
            {
                VFXHelper.SpawnProjectileHitEffect(transform.position, direction, projectileColor);
            }

            // 变异：分裂火球 — 命中后向随机方向射出小火球
            if (splitCount > 0)
            {
                SpawnSplitProjectiles();
            }

            // 变异：冰火交融 — 爆炸后留下冰域减速敌人
            if (frostZoneDuration > 0)
            {
                ApplyFrostZone(transform.position, frostZoneDuration);
            }

            // 连锁符文：命中后生成弹射投射物
            if (chainCount > 0)
            {
                SpawnChainProjectiles(enemy.transform.position, chainCount);
            }

            // 遗物效果：投射物命中 (防止分裂弹递归)
            if (!_isRelicSpawned && RelicManager.Instance != null)
                RelicManager.Instance.OnProjectileHit(this, enemy);

            VFXPool.Return("player_proj", gameObject);
        }
    }

    private void SpawnChainProjectiles(Vector3 origin, int remaining)
    {
        if (chainDepth >= MaxChainDepth) return;

        int count = Physics2D.OverlapCircleNonAlloc(origin, 2.5f, _chainBuffer, LayerMask.GetMask("Enemy"));
        int spawned = 0;
        for (int i = 0; i < count && spawned < remaining; i++)
        {
            if (_chainBuffer[i] == null) break;
            EnemyController e = _chainBuffer[i].GetComponent<EnemyController>();
            if (e == null || e.IsDead || hitEnemyIds.Contains(e.GetInstanceID())) continue;

            // 直接伤害+闪电特效
            int chainDmg = Mathf.RoundToInt(damage * 0.5f);
            e.TakeDamage(chainDmg, attacker);
            hitEnemyIds.Add(e.GetInstanceID());

            // 闪电视觉效果
            VFXHelper.SpawnLightningStrike(e.transform.position, 0.2f);

            spawned++;

            // 递归连锁（只递归1层）
            if (chainDepth < MaxChainDepth - 1 && remaining > 1)
            {
                int nextRemaining = remaining - 1;
                int nextCount = Physics2D.OverlapCircleNonAlloc(e.transform.position, 2.5f, _chainBuffer, LayerMask.GetMask("Enemy"));
                for (int j = 0; j < nextCount && nextRemaining > 0; j++)
                {
                    if (_chainBuffer[j] == null) break;
                    EnemyController next = _chainBuffer[j].GetComponent<EnemyController>();
                    if (next == null || next.IsDead || hitEnemyIds.Contains(next.GetInstanceID())) continue;

                    int nextDmg = Mathf.RoundToInt(damage * 0.3f);
                    next.TakeDamage(nextDmg, attacker);
                    hitEnemyIds.Add(next.GetInstanceID());
                    VFXHelper.SpawnLightningStrike(next.transform.position, 0.15f);
                    nextRemaining--;
                }
            }
        }
    }

    /// <summary>变异：分裂火球 — 命中后向随机方向射出小火球。</summary>
    private void SpawnSplitProjectiles()
    {
        float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        for (int i = 0; i < splitCount; i++)
        {
            float spread = baseAngle + Random.Range(-80f, 80f);
            float rad = spread * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            GameObject splitObj = VFXPool.GetPlayerProjectile();
            if (splitObj == null) continue;
            splitObj.transform.position = transform.position;
            splitObj.layer = LayerMask.NameToLayer("Player");

            var sr = splitObj.GetComponent<SpriteRenderer>();
            sr.sprite = VFXHelper.GetSharedWhiteSprite();
            sr.color = projectileColor;
            sr.sortingOrder = 8;
            splitObj.transform.localScale = new Vector3(0.3f, 0.3f, 1f);

            var splitRb = splitObj.GetComponent<Rigidbody2D>();
            splitRb.gravityScale = 0f;
            splitRb.velocity = dir * Speed * 0.8f;

            var splitCol = splitObj.GetComponent<CircleCollider2D>();
            splitCol.radius = 0.25f;
            splitCol.isTrigger = true;

            var splitProj = splitObj.GetComponent<PlayerProjectile>();
            int splitDmg = Mathf.RoundToInt(damage * 0.5f);
            splitProj.Initialize(dir, splitDmg, attacker);
            splitProj.SetColor(projectileColor);
            if (hasExplodeRadius) splitProj.SetExplodeRadius(explodeRadius * 0.5f);
        }
    }

    /// <summary>变异：冰火交融 — 在命中点施加减速效果。</summary>
    private void ApplyFrostZone(Vector3 position, float duration)
    {
        VFXHelper.SpawnAreaPulse(position, 2f, new Color(0.3f, 0.7f, 1f, 0.4f));
        int count = Physics2D.OverlapCircleNonAlloc(position, 2f, _aoeBuffer, LayerMask.GetMask("Enemy"));
        for (int i = 0; i < count; i++)
        {
            EnemyController enemy = _aoeBuffer[i].GetComponent<EnemyController>();
            if (enemy != null && !enemy.IsDead && enemy.StatusFx != null)
            {
                enemy.StatusFx.ApplyEffect(
                    StatusEffectManager.EffectType.Slow, duration, 0, 0.5f, 0.5f);
            }
        }
    }
}
