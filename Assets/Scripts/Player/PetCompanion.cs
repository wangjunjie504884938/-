using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pet companion — follows the player and auto-attacks nearby enemies.
/// 4 types with unique abilities, HP system with respawn, and visual variety.
/// </summary>
public class PetCompanion : MonoBehaviour
{
    public enum PetType
    {
        Spirit,    // 蓝 — 均衡: 闪电链
        Flame,     // 红 — 高伤: 火焰溅射
        Guardian,  // 绿 — 范围: 治疗光环
        Shadow     // 紫 — 快攻: 暴击穿刺
    }

    private Transform player;
    private SpriteRenderer sr;
    private SpriteRenderer glowSr;
    private Rigidbody2D rb;
    private float attackCooldown;
    private float lastAttackTime;
    private int attackDamage;
    private float moveSpeed;
    private float followDistance;
    private float attackRange;
    private PetType petType;
    private Color petColor;
    private Color glowColor;
    private bool isEvolved;

    private float abilityCooldown;
    private float lastAbilityTime;
    private string abilityName;

    private int maxHp;
    private int hp;
    private float respawnTimer;
    private int _lastRespawnSeconds = -1;
    private bool isDown;
    private const float RespawnTime = 8f;

    private static readonly Collider2D[] _enemyBuffer = new Collider2D[16];
    private static int _enemyLayerMask = -1;

    private GameObject petSprite;
    private GameObject glowSprite;
    private GameObject hpBarObj;
    private SpriteRenderer hpBarFill;
    private Text respawnText;

    private static Sprite _petCircleSprite;

    private static Sprite GetCircleSprite()
    {
        return GetCircleSpriteInternal();
    }

    public static Sprite GetCircleSpriteInternal()
    {
        if (_petCircleSprite != null) return _petCircleSprite;
        int size = 32;
        Texture2D tex = new Texture2D(size, size);
        Color[] px = new Color[size * size];
        float center = size / 2f;
        float radius = size / 2f - 1f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - center + 0.5f;
                float dy = y - center + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist <= radius)
                    px[y * size + x] = Color.white;
                else
                    px[y * size + x] = Color.clear;
            }
        tex.SetPixels(px);
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();
        _petCircleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return _petCircleSprite;
    }

    public void Initialize(Transform playerTransform, int dungeonLevel, PetType type = PetType.Spirit)
    {
        player = playerTransform;
        petType = type;
        isDown = false;
        isEvolved = PetDataManager.IsEvolved(type);
        bool evolved = isEvolved;
        float dmgMult = PetDataManager.GetDamageMult(type);
        float hpMult = PetDataManager.GetHpMult(type);
        float cdMult = PetDataManager.GetCooldownMult(type);

        rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var col = GetComponent<BoxCollider2D>();
        if (col == null) col = gameObject.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.4f, 0.4f);
        col.isTrigger = true;

        maxHp = 30 + dungeonLevel * 5;
        hp = maxHp;

        switch (type)
        {
            case PetType.Flame:
                petColor = evolved ? new Color(1f, 0.3f, 0f, 1f) : new Color(1f, 0.5f, 0.1f, 1f);
                glowColor = new Color(1f, 0.3f, 0f, 0.3f);
                attackDamage = Mathf.RoundToInt((5 + dungeonLevel) * dmgMult);
                attackCooldown = 1.5f * cdMult;
                attackRange = evolved ? 3f : 2.5f;
                moveSpeed = 7f;
                followDistance = 1.5f;
                abilityName = evolved ? "炎魔·烈焰风暴" : "火焰溅射";
                abilityCooldown = 8f * cdMult;
                maxHp = Mathf.RoundToInt((25 + dungeonLevel * 4) * hpMult);
                break;
            case PetType.Guardian:
                petColor = evolved ? new Color(0.4f, 1f, 0.4f, 1f) : new Color(0.3f, 0.9f, 0.3f, 1f);
                glowColor = new Color(0.3f, 1f, 0.3f, 0.3f);
                attackDamage = Mathf.RoundToInt((2 + dungeonLevel) * dmgMult);
                attackCooldown = 1.8f * cdMult;
                attackRange = evolved ? 5.5f : 4.5f;
                moveSpeed = 5f;
                followDistance = 2.5f;
                abilityName = evolved ? "圣灵·神圣领域" : "治疗光环";
                abilityCooldown = 10f * cdMult;
                maxHp = Mathf.RoundToInt((50 + dungeonLevel * 8) * hpMult);
                break;
            case PetType.Shadow:
                petColor = evolved ? new Color(0.9f, 0.3f, 1f, 1f) : new Color(0.7f, 0.2f, 0.9f, 1f);
                glowColor = new Color(0.7f, 0.2f, 0.9f, 0.3f);
                attackDamage = Mathf.RoundToInt((3 + dungeonLevel) * dmgMult);
                attackCooldown = 0.7f * cdMult;
                attackRange = evolved ? 2.5f : 2f;
                moveSpeed = 8f;
                followDistance = 1.5f;
                abilityName = evolved ? "影魔·致命一击" : "暴击穿刺";
                abilityCooldown = 6f * cdMult;
                maxHp = Mathf.RoundToInt((20 + dungeonLevel * 3) * hpMult);
                break;
            default: // Spirit
                petColor = evolved ? new Color(0.4f, 0.9f, 1f, 1f) : new Color(0.3f, 0.8f, 1f, 1f);
                glowColor = new Color(0.3f, 0.8f, 1f, 0.3f);
                attackDamage = Mathf.RoundToInt((3 + dungeonLevel) * dmgMult);
                attackCooldown = 1.2f * cdMult;
                attackRange = evolved ? 4f : 3f;
                moveSpeed = 6f;
                followDistance = 2f;
                abilityName = evolved ? "雷灵·雷霆万钧" : "闪电链";
                abilityCooldown = 7f * cdMult;
                maxHp = Mathf.RoundToInt((35 + dungeonLevel * 6) * hpMult);
                break;
        }
        hp = maxHp;

        CreatePetSprite();

        if (_enemyLayerMask == -1)
            _enemyLayerMask = LayerMask.GetMask("Enemy");
    }

    private void CreatePetSprite()
    {
        var circle = GetCircleSprite();
        float baseSize = isEvolved ? 0.8f : 0.6f;

        // White outline ring (behind core, slightly larger, white)
        var ringObj = new GameObject("PetRing");
        ringObj.transform.SetParent(transform, false);
        ringObj.transform.localPosition = Vector3.zero;
        var ringSr = ringObj.AddComponent<SpriteRenderer>();
        ringSr.sprite = circle;
        ringSr.color = new Color(1f, 1f, 1f, 0.7f);
        ringSr.sortingOrder = 18;
        ringObj.transform.localScale = new Vector3(baseSize * 1.3f, baseSize * 1.3f, 1f);

        // Glow (behind, larger, transparent)
        glowSprite = new GameObject("PetGlow");
        glowSprite.transform.SetParent(transform, false);
        glowSprite.transform.localPosition = Vector3.zero;
        glowSr = glowSprite.AddComponent<SpriteRenderer>();
        glowSr.sprite = circle;
        glowSr.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0.35f);
        glowSr.sortingOrder = 17;
        glowSprite.transform.localScale = new Vector3(baseSize * 2f, baseSize * 2f, 1f);

        // Core sprite — bright, opaque, on top
        petSprite = new GameObject("PetSprite");
        petSprite.transform.SetParent(transform, false);
        petSprite.transform.localPosition = Vector3.zero;
        sr = petSprite.AddComponent<SpriteRenderer>();
        sr.sprite = circle;
        sr.color = petColor;
        sr.sortingOrder = 20;
        petSprite.transform.localScale = new Vector3(baseSize, baseSize, 1f);

        // HP bar above pet (using SpriteRenderer-based bar)
        hpBarObj = new GameObject("PetHPBar");
        hpBarObj.transform.SetParent(transform, false);
        hpBarObj.transform.localPosition = new Vector3(0, baseSize * 0.9f, 0);
        var hpBgSr = hpBarObj.AddComponent<SpriteRenderer>();
        hpBgSr.sprite = circle;
        hpBgSr.color = new Color(0.1f, 0.1f, 0.1f, 0.7f);
        hpBgSr.sortingOrder = 21;
        hpBarObj.transform.localScale = new Vector3(0.7f, 0.08f, 1f);

        var hpFillObj = new GameObject("Fill");
        hpFillObj.transform.SetParent(hpBarObj.transform, false);
        hpFillObj.transform.localPosition = Vector3.zero;
        var hpFillSr = hpFillObj.AddComponent<SpriteRenderer>();
        hpFillSr.sprite = circle;
        hpFillSr.color = new Color(0.3f, 1f, 0.3f, 0.9f);
        hpFillSr.sortingOrder = 22;
        hpFillObj.transform.localScale = new Vector3(0.95f, 0.8f, 1f);
        hpBarFill = hpFillSr;
        hpBarObj.SetActive(false);

        // Respawn countdown text (using Text but as world-space child)
        var rtObj = new GameObject("RespawnText");
        rtObj.transform.SetParent(transform, false);
        rtObj.transform.localPosition = new Vector3(0, 0.8f, 0);
        var rtRt = rtObj.AddComponent<RectTransform>();
        rtRt.sizeDelta = new Vector2(2f, 0.4f);
        respawnText = rtObj.AddComponent<Text>();
        respawnText.alignment = TextAnchor.MiddleCenter;
        respawnText.fontSize = 14;
        respawnText.color = new Color(1f, 0.3f, 0.3f, 0.9f);
        respawnText.raycastTarget = false;
        respawnText.gameObject.SetActive(false);
    }

    private void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null || !gm.IsInDungeon || gm.IsPaused) { rb.velocity = Vector2.zero; return; }

        if (isDown)
        {
            respawnTimer -= Time.deltaTime;
            float alpha = Mathf.Sin(Time.time * 3f) * 0.15f + 0.05f;
            if (sr != null) sr.color = new Color(petColor.r, petColor.g, petColor.b, alpha);
            if (glowSr != null) glowSr.color = new Color(glowColor.r, glowColor.g, glowColor.b, alpha * 0.5f);
            if (respawnText != null)
            {
                respawnText.gameObject.SetActive(true);
                int secondsLeft = Mathf.CeilToInt(respawnTimer);
                if (secondsLeft != _lastRespawnSeconds)
                {
                    _lastRespawnSeconds = secondsLeft;
                    respawnText.text = secondsLeft.ToString();
                }
            }
            if (hpBarObj != null) hpBarObj.SetActive(false);
            if (respawnTimer <= 0f)
            {
                isDown = false;
                hp = maxHp;
                if (sr != null) sr.color = petColor;
                if (glowSr != null) glowSr.color = glowColor;
                if (respawnText != null) respawnText.gameObject.SetActive(false);
                VFXHelper.SpawnHitParticles(transform.position, petColor, 5);
            }
            rb.velocity = Vector2.zero;
            return;
        }

        if (respawnText != null && respawnText.gameObject.activeSelf)
            respawnText.gameObject.SetActive(false);

        // Update HP bar
        if (hpBarObj != null)
        {
            bool showBar = hp < maxHp;
            hpBarObj.SetActive(showBar);
            if (showBar && hpBarFill != null)
            {
                float ratio = (float)hp / maxHp;
                // Scale the fill sprite horizontally to represent HP ratio
                hpBarFill.transform.localScale = new Vector3(0.95f * ratio, 0.8f, 1f);
                hpBarFill.color = ratio > 0.5f ? new Color(0.3f, 1f, 0.3f, 0.9f) : new Color(1f, 0.8f, 0.2f, 0.9f);
            }
        }

        // Always refresh player reference — it changes between dungeon runs
        if (gm.Player != null)
            player = gm.Player.transform;
        if (player == null) return;

        Vector2 playerPos = player.position;
        Vector2 myPos = transform.position;
        float distToPlayer = Vector2.Distance(myPos, playerPos);

        // Follow behind player based on facing direction
        var charSprite = gm.Player?.CharSprite;
        bool playerFacesLeft = charSprite != null && charSprite.IsFacingLeft;
        float offsetX = playerFacesLeft ? 1.8f : -1.8f;
        float offsetY = -0.3f;
        Vector2 targetPos = playerPos + new Vector2(offsetX, offsetY);
        float distToTarget = Vector2.Distance(myPos, targetPos);

        if (distToPlayer > followDistance)
        {
            // If too far (e.g. just spawned or dungeon changed), teleport closer
            if (distToPlayer > 10f)
            {
                transform.position = targetPos;
                rb.velocity = Vector2.zero;
            }
            else
            {
                Vector2 moveDir = (targetPos - myPos).normalized;
                rb.velocity = moveDir * moveSpeed;
            }
        }
        else
        {
            rb.velocity = Vector2.zero;
        }

        if (sr != null)
        {
            float vx = rb.velocity.x;
            if (Mathf.Abs(vx) > 0.1f)
                sr.flipX = vx < 0;
        }

        // Pulse animation with glow
        if (petSprite != null && glowSprite != null)
        {
            float pulseSpeed = petType == PetType.Shadow ? 8f : 4f;
            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * 0.12f;
            float baseSize = isEvolved ? 0.8f : 0.6f;
            petSprite.transform.localScale = new Vector3(baseSize * pulse, baseSize * pulse, 1f);

            float glowPulse = 1f + Mathf.Sin(Time.time * pulseSpeed * 0.7f) * 0.2f;
            glowSprite.transform.localScale = new Vector3(baseSize * 2f * glowPulse, baseSize * 2f * glowPulse, 1f);

            if (hp < maxHp * 0.3f)
            {
                float flash = Mathf.Sin(Time.time * 10f) * 0.3f;
                sr.color = new Color(petColor.r + flash, petColor.g, petColor.b, petColor.a);
            }
            else
            {
                sr.color = petColor;
            }
        }

        if (Time.time - lastAttackTime >= attackCooldown)
            TryAttack();

        if (Time.time - lastAbilityTime >= abilityCooldown)
            TryAbility();
    }

    private void TryAttack()
    {
        int hitCount = Physics2D.OverlapCircleNonAlloc(
            transform.position, attackRange, _enemyBuffer, _enemyLayerMask);

        EnemyController nearest = null;
        float nearestDist = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            var enemy = _enemyBuffer[i].GetComponent<EnemyController>();
            if (enemy != null && !enemy.IsDead)
            {
                float dist = Vector2.Distance(transform.position, enemy.transform.position);
                if (dist < nearestDist) { nearestDist = dist; nearest = enemy; }
            }
        }

        if (nearest != null)
        {
            lastAttackTime = Time.time;
            var p = GameManager.Instance?.Player;
            int scaledDmg = attackDamage + (p != null ? Mathf.RoundToInt(p.Stats.TotalAttack * 0.2f) : 0);
            nearest.TakeDamage(scaledDmg, p);
            VFXHelper.SpawnHitParticles(nearest.transform.position, petColor, 2);
            VFXHelper.SpawnSlashEffect(nearest.transform.position,
                (nearest.transform.position - transform.position).normalized, 0.8f,
                new Color(petColor.r, petColor.g, petColor.b, 0.5f));
        }
    }

    private void TryAbility()
    {
        var p = GameManager.Instance?.Player;
        if (p == null) return;
        int bonusDmg = Mathf.RoundToInt(p.Stats.TotalAttack * 0.2f);

        switch (petType)
        {
            case PetType.Flame:
                int hitCount = Physics2D.OverlapCircleNonAlloc(
                    transform.position, attackRange * 1.5f, _enemyBuffer, _enemyLayerMask);
                if (hitCount == 0) return;
                lastAbilityTime = Time.time;
                int aoeDmg = attackDamage + bonusDmg;
                for (int i = 0; i < hitCount; i++)
                {
                    var enemy = _enemyBuffer[i].GetComponent<EnemyController>();
                    if (enemy != null && !enemy.IsDead)
                    {
                        enemy.TakeDamage(aoeDmg, p);
                        VFXHelper.SpawnHitParticles(enemy.transform.position,
                            new Color(1f, 0.3f, 0f, 0.7f), 4);
                    }
                }
                VFXHelper.SpawnAreaPulse(transform.position, attackRange * 1.5f,
                    new Color(1f, 0.3f, 0f, 0.4f));
                break;

            case PetType.Guardian:
                lastAbilityTime = Time.time;
                int healAmt = (10 + GameManager.Instance.DungeonLevel * 2) * (isEvolved ? 2 : 1);
                p.Heal(healAmt);
                VFXHelper.SpawnHealEffect(p.transform.position, healAmt);
                VFXHelper.SpawnAreaPulse(transform.position, 3f,
                    new Color(0.3f, 1f, 0.3f, 0.4f));
                break;

            case PetType.Shadow:
                EnemyController nearest = FindNearestEnemy(attackRange);
                if (nearest == null) return;
                lastAbilityTime = Time.time;
                int critMult = isEvolved ? 5 : 3;
                int critDmg = (attackDamage + bonusDmg) * critMult;
                nearest.TakeDamage(critDmg, p);
                VFXHelper.SpawnHitParticles(nearest.transform.position,
                    new Color(0.8f, 0.3f, 1f, 0.8f), 6);
                VFXHelper.SpawnSlashEffect(nearest.transform.position, Vector2.up, 1.5f,
                    new Color(0.8f, 0.3f, 1f, 0.6f));
                break;

            default: // Spirit
                var chainTargets = new System.Collections.Generic.List<EnemyController>();
                int chainCount = Physics2D.OverlapCircleNonAlloc(
                    transform.position, attackRange * 2f, _enemyBuffer, _enemyLayerMask);
                int maxChain = isEvolved ? 5 : 3;
                for (int i = 0; i < chainCount && chainTargets.Count < maxChain; i++)
                {
                    var e = _enemyBuffer[i].GetComponent<EnemyController>();
                    if (e != null && !e.IsDead) chainTargets.Add(e);
                }
                if (chainTargets.Count == 0) return;
                lastAbilityTime = Time.time;
                int chainDmg = attackDamage + bonusDmg;
                foreach (var enemy in chainTargets)
                {
                    enemy.TakeDamage(chainDmg, p);
                    VFXHelper.SpawnHitParticles(enemy.transform.position,
                        new Color(0.3f, 0.8f, 1f, 0.7f), 3);
                }
                VFXHelper.SpawnAreaPulse(transform.position, attackRange * 2f,
                    new Color(0.3f, 0.8f, 1f, 0.3f));
                break;
        }
    }

    private EnemyController FindNearestEnemy(float range)
    {
        int hitCount = Physics2D.OverlapCircleNonAlloc(
            transform.position, range, _enemyBuffer, _enemyLayerMask);
        EnemyController nearest = null;
        float nearestDist = float.MaxValue;
        for (int i = 0; i < hitCount; i++)
        {
            var enemy = _enemyBuffer[i].GetComponent<EnemyController>();
            if (enemy != null && !enemy.IsDead)
            {
                float dist = Vector2.Distance(transform.position, enemy.transform.position);
                if (dist < nearestDist) { nearestDist = dist; nearest = enemy; }
            }
        }
        return nearest;
    }

    public void TakeDamage(int damage)
    {
        if (isDown) return;
        hp -= damage;
        if (hp <= 0)
        {
            hp = 0;
            isDown = true;
            respawnTimer = RespawnTime;
            VFXHelper.SpawnHitParticles(transform.position, petColor, 4);
        }
    }

    public bool IsDown => isDown;
    public PetType Type => petType;
    public string AbilityName => abilityName;
    public float AbilityCooldownRemaining => Mathf.Max(0, abilityCooldown - (Time.time - lastAbilityTime));
    public float AbilityCooldownMax => abilityCooldown;

    private void OnEnable() { SceneRegistry.Register(this); }
    private void OnDisable() { SceneRegistry.Unregister(this); }
}
