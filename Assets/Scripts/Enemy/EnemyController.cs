using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum EnemyType
{
    Melee,
    Ranged,
    Elite,
    Boss,
    Bomber,     // 死亡自爆，AOE伤害
    Charger,    // 周期性冲锋攻击
    Healer,     // 治疗周围敌人
    Shielder    // 给周围敌人护盾
}

public class EnemyController : MonoBehaviour
{
    [Header("Enemy Type")]
    public EnemyType Type = EnemyType.Melee;

    [Header("Base Stats")]
    public int BaseHp = 30;
    public int BaseAttack = 8;
    public int BaseDefense = 2;
    public float BaseMoveSpeed = 2f;
    public int BaseXpReward = 20;

    [Header("Ranged")]
    public float AttackRange = 5f;
    public float AttackCooldown = 1.5f;

    [Header("Visuals")]
    public Color EnemyColor = new Color(0.7f, 0.7f, 0.7f);
    public Color EliteColor = new Color(1f, 0.8f, 0f);
    public Color BossColor = new Color(0.8f, 0f, 0.8f);

    [Header("Affixes")]
    public List<EliteAffix> Affixes = new List<EliteAffix>();

    public bool HasAffix(EliteAffixType type)
    {
        if (Affixes == null) return false;
        for (int i = 0; i < Affixes.Count; i++)
            if (Affixes[i].Type == type) return true;
        return false;
    }
    public EliteAffix GetAffix(EliteAffixType type)
    {
        if (Affixes == null) return null;
        for (int i = 0; i < Affixes.Count; i++)
            if (Affixes[i].Type == type) return Affixes[i];
        return null;
    }

    // Sub-controllers
    public EnemyBrainController Brain { get; private set; }
    public EnemyCombatController Combat { get; private set; }

    // Backward-compatible read-only properties (delegate to Combat)
    public bool IsDead => Combat != null && Combat.IsDead;
    public int XpReward => Combat != null ? Combat.XpReward : 0;
    public int CurrentHp => Combat != null ? Combat.CurrentHp : 0;
    public int MaxHp => Combat != null ? Combat.MaxHp : 0;

    // Exposed for sub-controllers
    public float MeleeRange => 1.3f;
    public float MoveSpeed => Combat != null ? Combat.MoveSpeed : BaseMoveSpeed;
    public bool IsInitialized { get; private set; }
    public StatusEffectManager StatusFx { get; private set; }

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        gameObject.layer = LayerMask.NameToLayer("Enemy");
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        SceneRegistry.Register(this);

        Brain = gameObject.AddComponent<EnemyBrainController>();
        Combat = gameObject.AddComponent<EnemyCombatController>();
        StatusFx = gameObject.AddComponent<StatusEffectManager>();

        Brain.Initialize(this, rb, spriteRenderer);
        Combat.Initialize(this, rb, spriteRenderer);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.collider == null) return;
        if (collision.collider.CompareTag("Player")) return;

        Vector2 pushDir = (transform.position - (Vector3)collision.contacts[0].point).normalized;
        rb.AddForce(pushDir * MoveSpeed * 1.5f, ForceMode2D.Force);
    }

    private void Start()
    {
        if (!IsInitialized)
        {
            int dungeonLevel = GameManager.Instance != null ? GameManager.Instance.DungeonLevel : 1;
            Initialize(dungeonLevel);
        }
        StartCoroutine(AIUpdateLoop());
    }

    /// <summary>
    /// AI 逻辑协程 — 每 0.2 秒执行一次，减少每帧 AI 计算开销。
    /// Update() 仅保留位置修正。
    /// </summary>
    private IEnumerator AIUpdateLoop()
    {
        var wait = new WaitForSeconds(0.2f);
        while (true)
        {
            yield return wait;
            if (IsDead || !IsInitialized) continue;
            Brain.TickUpdate();
        }
    }

    public void Initialize(int dungeonLevel)
    {
        // Set type-specific defaults before config override
        switch (Type)
        {
            case EnemyType.Bomber:
                BaseHp = 20; BaseAttack = 12; BaseDefense = 1; BaseMoveSpeed = 2.8f; BaseXpReward = 25;
                break;
            case EnemyType.Charger:
                BaseHp = 35; BaseAttack = 10; BaseDefense = 2; BaseMoveSpeed = 3.2f; BaseXpReward = 25;
                break;
            case EnemyType.Healer:
                BaseHp = 25; BaseAttack = 5; BaseDefense = 1; BaseMoveSpeed = 1.8f; BaseXpReward = 30;
                break;
            case EnemyType.Shielder:
                BaseHp = 50; BaseAttack = 6; BaseDefense = 4; BaseMoveSpeed = 1.5f; BaseXpReward = 30;
                break;
        }

        // 从数据库配置读取基础属性 (回退到Inspector默认值)
        if (GameConfigManager.Instance != null && GameConfigManager.Instance.IsLoaded)
        {
            var cfg = GameConfigManager.Instance.GetEnemyData((int)Type);
            if (cfg != null)
            {
                BaseHp = cfg.baseHp;
                BaseAttack = cfg.baseAttack;
                BaseDefense = cfg.baseDefense;
                BaseMoveSpeed = cfg.baseMoveSpeed;
                BaseXpReward = cfg.baseXpReward;
                AttackRange = cfg.attackRange;
                AttackCooldown = cfg.attackCooldown;
            }
        }

        Combat.SetupStats(dungeonLevel);

        IsInitialized = true;
        Transform pTransform = (GameManager.Instance != null && GameManager.Instance.Player != null)
            ? GameManager.Instance.Player.transform
            : null;

        Brain.SetPlayerTransform(pTransform);
        Brain.InitStuckAvoidance();
        Combat.SetPlayerTransform(pTransform);
        Combat.SetupHealthBar();
    }

    private void Update()
    {
        if (IsDead || !IsInitialized) return;

        // Update() 只保留位置修正；AI 逻辑已移至 AIUpdateLoop 协程
        ClampToMapBounds();
    }

    // Backward-compatible facade
    public void TakeDamage(int rawDamage, PlayerController attacker)
    {
        Combat.TakeDamage(rawDamage, attacker);
    }

    private float _clampX, _clampY;
    private bool _clampCached;

    private void ClampToMapBounds()
    {
        if (GameManager.Instance == null) return;
        if (!_clampCached)
        {
            var mapData = DungeonMapData.GetStageMap(GameManager.Instance.CurrentStageIndex);
            _clampX = mapData.mapHalfWidth - 1.5f - 0.5f;
            _clampY = mapData.mapHalfHeight - 1.5f - 0.5f;
            _clampCached = true;
        }
        Vector3 pos = transform.position;
        bool wasOutside = pos.x < -_clampX || pos.x > _clampX || pos.y < -_clampY || pos.y > _clampY;
        if (wasOutside)
        {
            pos.x = Mathf.Clamp(pos.x, -_clampX, _clampX);
            pos.y = Mathf.Clamp(pos.y, -_clampY, _clampY);
            transform.position = pos;
            rb.velocity = Vector2.zero;
        }
    }

    private void OnDestroy()
    {
        SceneRegistry.Unregister(this);
    }
}
