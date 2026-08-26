using UnityEngine;
using System.Collections;

/// <summary>
/// Boss攻击模式 — PoE2风格的预警攻击系统
/// 每个攻击有预警阶段 → 爆发阶段 → 冷却阶段
/// 玩家必须通过Dodge Roll躲避预警攻击
/// </summary>
public class BossPattern : MonoBehaviour
{
    public enum AttackType
    {
        GroundCircle,    // 地面红圈 → AOE爆炸
        HomingBarrage,   // 追踪弹幕
        ScreenPulse,      // 全屏脉冲
        SpiralBullets     // 螺旋弹幕
    }

    private enum State
    {
        Idle,
        Telegraphing,  // 预警中 (红圈/闪烁)
        Attacking,     // 释放攻击
        Cooldown       // 冷却
    }

    private State state = State.Idle;
    private float stateTimer;
    private AttackType currentAttack;
    private Transform playerTransform;
    private EnemyController enemy;
    private SpriteRenderer spriteRenderer;

    // Telegraph visual
    private GameObject telegraphCircle;
    private Vector3 telegraphPos;

    // Config — scaled by boss phase
    private float telegraphDuration = 0.8f;
    private float attackDuration = 0.3f;
    private float cooldownDuration = 2f;
    private float nextAttackTimer = 3f;

    // spiralAngle removed — no longer used

    public void Initialize(EnemyController enemy, SpriteRenderer sr)
    {
        this.enemy = enemy;
        this.spriteRenderer = sr;
        playerTransform = GameManager.Instance?.Player?.transform;
    }

    public void Tick(float deltaTime)
    {
        if (enemy == null || enemy.IsDead)
        {
            // Boss死亡时清理残留的预警圈
            DestroyTelegraph();
            return;
        }

        stateTimer -= deltaTime;

        switch (state)
        {
            case State.Idle:
                nextAttackTimer -= deltaTime;
                if (nextAttackTimer <= 0f)
                {
                    StartTelegraph();
                }
                break;

            case State.Telegraphing:
                UpdateTelegraph();
                if (stateTimer <= 0f)
                {
                    ExecuteAttack();
                }
                break;

            case State.Attacking:
                UpdateAttack();
                if (stateTimer <= 0f)
                {
                    EndAttack();
                }
                break;

            case State.Cooldown:
                if (stateTimer <= 0f)
                {
                    state = State.Idle;
                    nextAttackTimer = Random.Range(2f, 4f);
                }
                break;
        }
    }

    private void StartTelegraph()
    {
        // Pick random attack based on boss phase
        float hpPct = enemy.MaxHp > 0 ? (float)enemy.CurrentHp / enemy.MaxHp : 1f;
        int phase = hpPct > 0.5f ? 1 : (hpPct > 0.25f ? 2 : 3);

        // Scale attack speed by phase — harder in later phases
        telegraphDuration = phase >= 3 ? 0.5f : (phase >= 2 ? 0.65f : 0.8f);
        cooldownDuration = phase >= 3 ? 1.2f : (phase >= 2 ? 1.5f : 2f);

        if (phase >= 3)
            currentAttack = (AttackType)Random.Range(0, 4);
        else if (phase >= 2)
            currentAttack = (AttackType)Random.Range(0, 3);
        else
            currentAttack = (AttackType)Random.Range(0, 2);

        state = State.Telegraphing;
        stateTimer = telegraphDuration;

        // Refresh player reference
        playerTransform = GameManager.Instance?.Player?.transform;
        if (playerTransform == null) { state = State.Idle; nextAttackTimer = 2f; return; }

        // Create telegraph visual
        switch (currentAttack)
        {
            case AttackType.GroundCircle:
                telegraphPos = playerTransform.position;
                CreateTelegraphCircle(telegraphPos, 2.5f, new Color(1f, 0.1f, 0.1f, 0.4f));
                break;
            case AttackType.HomingBarrage:
                CreateTelegraphCircle(transform.position, 1.5f, new Color(1f, 0.6f, 0.1f, 0.4f));
                break;
            case AttackType.ScreenPulse:
                CreateTelegraphCircle(transform.position, 8f, new Color(0.8f, 0.1f, 0.8f, 0.3f));
                break;
            case AttackType.SpiralBullets:
                CreateTelegraphCircle(transform.position, 2f, new Color(0.6f, 0.1f, 1f, 0.4f));
                break;
        }

        // Screen warning flash
        VFXHelper.SpawnScreenFlash(new Color(1f, 0.3f, 0.1f), 0.1f);
    }

    private void UpdateTelegraph()
    {
        // Pulse the telegraph circle
        if (telegraphCircle != null)
        {
            float progress = 1f - (stateTimer / telegraphDuration);
            var img = telegraphCircle.GetComponent<SpriteRenderer>();
            if (img != null)
            {
                Color c = img.color;
                c.a = 0.2f + progress * 0.5f;
                img.color = c;
                telegraphCircle.transform.localScale = Vector3.one * (1f + progress * 0.1f);
            }
        }
    }

    private void ExecuteAttack()
    {
        state = State.Attacking;
        stateTimer = attackDuration;
        DestroyTelegraph();

        switch (currentAttack)
        {
            case AttackType.GroundCircle:
                ExecuteGroundCircle();
                break;
            case AttackType.HomingBarrage:
                StartCoroutine(ExecuteHomingBarrage());
                break;
            case AttackType.ScreenPulse:
                ExecuteScreenPulse();
                break;
            case AttackType.SpiralBullets:
                StartCoroutine(ExecuteSpiralBullets());
                break;
        }
    }

    private void UpdateAttack()
    {
        // Attacks are handled by coroutines
    }

    private void EndAttack()
    {
        state = State.Cooldown;
        stateTimer = cooldownDuration;
    }

    private void ExecuteGroundCircle()
    {
        VFXHelper.SpawnAreaPulse(telegraphPos, 2.5f, new Color(1f, 0.2f, 0.1f, 0.8f));
        VFXHelper.SpawnHitParticles(telegraphPos, new Color(1f, 0.5f, 0.2f), 8);
        CameraFollow.Shake(0.2f, 0.12f);

        // Damage player if in range and not dodging
        if (playerTransform == null) return;
        float dist = Vector2.Distance(playerTransform.position, telegraphPos);
        if (dist <= 2.5f)
        {
            var player = GameManager.Instance?.Player;
            if (player != null && !player.IsDead && !player.StateMachine.IsInvincible)
                player.TakeDamage(Mathf.RoundToInt(enemy.BaseAttack * 2f));
        }
    }

    private IEnumerator ExecuteHomingBarrage()
    {
        int bulletCount = 5;
        for (int i = 0; i < bulletCount; i++)
        {
            if (enemy == null || enemy.IsDead) yield break;
            SpawnHomingBullet();
            yield return new WaitForSeconds(0.15f);
        }
    }

    private void SpawnHomingBullet()
    {
        if (playerTransform == null) return;
        Vector2 dir = (playerTransform.position - transform.position).normalized;

        GameObject proj = VFXPool.GetEnemyProjectile();
        proj.transform.position = transform.position;
        proj.layer = 0;

        var sr = proj.GetComponent<SpriteRenderer>();
        sr.sprite = VFXHelper.GetSharedWhiteSprite();
        sr.color = new Color(1f, 0.3f, 0.1f);
        sr.sortingOrder = 6;
        proj.transform.localScale = new Vector3(0.3f, 0.3f, 1f);

        var rb2d = proj.GetComponent<Rigidbody2D>();
        rb2d.gravityScale = 0f;
        rb2d.velocity = dir * 5f;

        var col = proj.GetComponent<CircleCollider2D>();
        col.radius = 0.35f;
        col.isTrigger = true;

        var ep = proj.GetComponent<EnemyProjectile>();
        ep.Initialize(dir, Mathf.RoundToInt(enemy.BaseAttack * 1.5f));

        VFXHelper.SpawnHitParticles(transform.position, new Color(1f, 0.3f, 0.1f), 2);
    }

    private void ExecuteScreenPulse()
    {
        VFXHelper.SpawnAreaPulse(transform.position, 8f, new Color(0.8f, 0.1f, 0.8f, 0.8f));
        VFXHelper.SpawnShockwave(transform.position, new Color(0.8f, 0.1f, 0.8f), 8f, 15f);
        VFXHelper.SpawnScreenFlash(new Color(0.8f, 0.1f, 0.8f), 0.15f);
        CameraFollow.Shake(0.35f, 0.2f);
        CameraFollow.SlowMotion(0.3f, 0.08f);

        // Damage player unless dodging (i-frame check)
        var player = GameManager.Instance?.Player;
        if (player != null && !player.IsDead && !player.StateMachine.IsInvincible)
        {
            float dist = Vector2.Distance(player.transform.position, transform.position);
            if (dist <= 8f)
                player.TakeDamage(Mathf.RoundToInt(enemy.BaseAttack * 2.5f));
        }
    }

    private IEnumerator ExecuteSpiralBullets()
    {
        int totalBullets = 16;
        float angleStep = 360f / totalBullets;

        for (int wave = 0; wave < 3; wave++)
        {
            if (enemy == null || enemy.IsDead) yield break;

            for (int i = 0; i < totalBullets; i++)
            {
                float angle = (i * angleStep + wave * 15f) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                SpawnSpiralBullet(dir);
            }
            yield return new WaitForSeconds(0.25f);
        }
    }

    private void SpawnSpiralBullet(Vector2 dir)
    {
        GameObject proj = VFXPool.GetEnemyProjectile();
        proj.transform.position = transform.position + (Vector3)(dir * 0.5f);
        proj.layer = 0;

        var sr = proj.GetComponent<SpriteRenderer>();
        sr.sprite = VFXHelper.GetSharedWhiteSprite();
        sr.color = new Color(0.6f, 0.1f, 1f);
        sr.sortingOrder = 6;
        proj.transform.localScale = new Vector3(0.25f, 0.25f, 1f);

        var rb2d = proj.GetComponent<Rigidbody2D>();
        rb2d.gravityScale = 0f;
        rb2d.velocity = dir * 4f;

        var col = proj.GetComponent<CircleCollider2D>();
        col.radius = 0.3f;
        col.isTrigger = true;

        var ep = proj.GetComponent<EnemyProjectile>();
        ep.Initialize(dir, Mathf.RoundToInt(enemy.BaseAttack * 1.2f));
    }

    private void CreateTelegraphCircle(Vector3 pos, float radius, Color color)
    {
        DestroyTelegraph();
        telegraphCircle = new GameObject("TelegraphCircle");
        telegraphCircle.transform.position = pos;
        telegraphCircle.transform.localScale = Vector3.one * radius * 2f;

        var sr = telegraphCircle.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteCache.WhitePixel;
        sr.color = color;
        sr.sortingOrder = 4;
    }

    private void DestroyTelegraph()
    {
        if (telegraphCircle != null)
        {
            DestroyImmediate(telegraphCircle);
            telegraphCircle = null;
        }
    }

    public bool IsTelegraphing => state == State.Telegraphing;
}
