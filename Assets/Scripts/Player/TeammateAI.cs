using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// AI队友 — 进入副本后跟随玩家并自动攻击敌人
/// 组队模式下由GameManager创建，最多3个队友
/// </summary>
public class TeammateAI : MonoBehaviour
{
    public string teammateName = "队友";
    public HeroClass teammateClass = HeroClass.Warrior;
    public int teammateLevel = 1;

    private Transform _player;
    private SpriteRenderer _sr;
    private Rigidbody2D _rb;
    private float _attackCooldown;
    private float _lastAttackTime;
    private int _attackDamage;
    private float _moveSpeed = 6f;
    private float _followDistance = 1.5f;
    private float _attackRange = 3f;
    private int _maxHp;
    private int _hp;
    private float _respawnTimer;
    private bool _isDown;
    private GameObject _hpBarObj;
    private SpriteRenderer _hpBarFill;
    private Text _nameText;

    private static readonly Collider2D[] _enemyBuffer = new Collider2D[16];
    private static int _enemyLayerMask = -1;
    private int _playerLayer;
    private float _wanderTimer;
    private float _aiThinkTimer;
    private Vector2 _wanderOffset;

    private static readonly Color[] ClassColors =
    {
        new Color(0.85f, 0.2f, 0.2f, 1f), // 战士-红
        new Color(0.2f, 0.4f, 0.9f, 1f),  // 法师-蓝
        new Color(0.9f, 0.85f, 0.4f, 1f), // 牧师-黄
    };
    private static readonly string[] ClassNames = { "战士", "法师", "牧师" };

    /// <summary>初始化队友</summary>
    public void Initialize(Transform player, HeroClass cls, int level, string name, int dungeonLevel)
    {
        _player = player;
        teammateClass = cls;
        teammateLevel = level;
        teammateName = name;
        _playerLayer = LayerMask.NameToLayer("Player");

        var rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        _rb = rb;

        var col = GetComponent<BoxCollider2D>();
        if (col == null) col = gameObject.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.5f, 0.6f);
        col.isTrigger = false;
        gameObject.layer = LayerMask.NameToLayer("Player");

        int clsIdx = (int)cls;
        var classColor = ClassColors[clsIdx];

        // 属性根据职业和副本等级缩放
        _maxHp = 100 + level * 15 + dungeonLevel * 10;
        _attackDamage = Mathf.RoundToInt((8 + level * 2 + dungeonLevel * 1.5f));
        _moveSpeed = 6f;
        _followDistance = 1.5f + clsIdx * 0.5f;

        switch (cls)
        {
            case HeroClass.Warrior:
                _attackRange = 2f;
                _attackCooldown = 0.5f;
                _maxHp = Mathf.RoundToInt(_maxHp * 1.3f);
                _attackDamage = Mathf.RoundToInt(_attackDamage * 0.9f);
                break;
            case HeroClass.Mage:
                _attackRange = 5f;
                _attackCooldown = 0.8f;
                _attackDamage = Mathf.RoundToInt(_attackDamage * 1.3f);
                break;
            default: // Priest
                _attackRange = 4f;
                _attackCooldown = 0.7f;
                _attackDamage = Mathf.RoundToInt(_attackDamage * 0.8f);
                break;
        }
        _hp = _maxHp;

        CreateSprite(classColor);

        if (_enemyLayerMask == -1)
            _enemyLayerMask = LayerMask.GetMask("Enemy");
    }

    private void CreateSprite(Color classColor)
    {
        // 角色圆形精灵
        var srObj = new GameObject("Sprite");
        srObj.transform.SetParent(transform, false);
        srObj.transform.localPosition = Vector3.zero;
        _sr = srObj.AddComponent<SpriteRenderer>();
        _sr.sprite = SpriteCache.WhitePixel;
        _sr.color = classColor;
        srObj.transform.localScale = new Vector3(0.5f, 0.7f, 1f);
        _sr.sortingOrder = 5;

        // 外圈光晕
        var glowObj = new GameObject("Glow");
        glowObj.transform.SetParent(transform, false);
        glowObj.transform.localPosition = Vector3.zero;
        var glowSr = glowObj.AddComponent<SpriteRenderer>();
        glowSr.sprite = SpriteCache.WhitePixel;
        glowSr.color = new Color(classColor.r, classColor.g, classColor.b, 0.2f);
        glowObj.transform.localScale = new Vector3(0.7f, 0.9f, 1f);
        glowSr.sortingOrder = 4;

        // 名字标签
        var nameObj = new GameObject("NameLabel");
        nameObj.transform.SetParent(transform, false);
        var nameR = nameObj.AddComponent<RectTransform>();
        nameR.anchorMin = new Vector2(0.5f, 0); nameR.anchorMax = new Vector2(0.5f, 0);
        nameR.pivot = new Vector2(0.5f, 0);
        nameR.anchoredPosition = new Vector2(0, 0.8f);
        nameR.sizeDelta = new Vector2(120, 20);
        _nameText = nameObj.AddComponent<Text>();
        int clsIdx = (int)teammateClass;
        string cc = ColorUtility.ToHtmlStringRGBA(ClassColors[clsIdx]);
        _nameText.text = $"<color=#{cc}>[{ClassNames[clsIdx]}]</color> {teammateName}";
        _nameText.alignment = TextAnchor.MiddleCenter;
        _nameText.fontSize = 11;
        _nameText.color = Color.white;
        _nameText.font = GameManager.GetUIFont();
        _nameText.supportRichText = true;
        _nameText.raycastTarget = false;

        // 血条
        _hpBarObj = new GameObject("HPBar");
        _hpBarObj.transform.SetParent(transform, false);
        var hpR = _hpBarObj.AddComponent<RectTransform>();
        hpR.anchorMin = new Vector2(0.5f, 0); hpR.anchorMax = new Vector2(0.5f, 0);
        hpR.pivot = new Vector2(0.5f, 0);
        hpR.anchoredPosition = new Vector2(0, 0.65f);
        hpR.sizeDelta = new Vector2(50, 5);
        var hpBg = _hpBarObj.AddComponent<Image>();
        hpBg.color = new Color(0.2f, 0.05f, 0.05f, 0.8f);

        var fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(_hpBarObj.transform, false);
        var fillR = fillObj.AddComponent<RectTransform>();
        fillR.anchorMin = Vector2.zero; fillR.anchorMax = Vector2.one;
        fillR.offsetMin = Vector2.zero; fillR.offsetMax = Vector2.zero;
        _hpBarFill = fillObj.AddComponent<SpriteRenderer>();
        _hpBarFill.sprite = SpriteCache.WhitePixel;
        _hpBarFill.color = new Color(0.2f, 0.9f, 0.2f, 1f);
        fillObj.transform.localScale = new Vector3(1, 1, 1);
    }

    private void Update()
    {
        if (_player == null) return;

        if (_isDown)
        {
            _respawnTimer -= Time.deltaTime;
            if (_nameText != null)
                _nameText.text = $"<color=#FF4444>倒地 {_respawnTimer:F0}s</color>";
            if (_respawnTimer <= 0)
            {
                _isDown = false;
                _hp = _maxHp;
                transform.position = _player.position + new Vector3(-1f, 0, 0);
                if (_nameText != null)
                {
                    int clsIdx = (int)teammateClass;
                    string cc = ColorUtility.ToHtmlStringRGBA(ClassColors[clsIdx]);
                    _nameText.text = $"<color=#{cc}>[{ClassNames[clsIdx]}]</color> {teammateName}";
                }
            }
            return;
        }

        // 跟随玩家（每帧执行，轻量计算）
        FollowPlayer();

        // 攻击检测降频到每0.2秒一次（减少Physics2D开销）
        _aiThinkTimer -= Time.deltaTime;
        if (_aiThinkTimer <= 0)
        {
            _aiThinkTimer = 0.2f;
            FindAndAttackEnemy();
        }

        UpdateHPBar();
    }

    private void FollowPlayer()
    {
        float distToPlayer = Vector2.SqrMagnitude((Vector2)_player.position - (Vector2)transform.position);
        if (distToPlayer > _followDistance * _followDistance)
        {
            Vector2 dir = ((Vector2)_player.position - (Vector2)transform.position).normalized;
            _wanderTimer -= Time.deltaTime;
            if (_wanderTimer <= 0)
            {
                _wanderOffset = Random.insideUnitCircle * 0.5f;
                _wanderTimer = 1f;
            }
            dir += _wanderOffset * 0.3f;
            _rb.MovePosition((Vector2)transform.position + dir * _moveSpeed * Time.deltaTime);
        }
    }

    private void FindAndAttackEnemy()
    {
        // 寻找附近敌人
        int count = Physics2D.OverlapCircleNonAlloc(transform.position, _attackRange + 2f, _enemyBuffer, _enemyLayerMask);
        if (count > 0 && Time.time - _lastAttackTime > _attackCooldown)
        {
            // 找最近的敌人
            Transform nearestEnemy = null;
            float nearestDist = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var enemy = _enemyBuffer[i];
                if (enemy == null) continue;
                float d = Vector2.Distance(transform.position, enemy.transform.position);
                if (d < nearestDist)
                {
                    nearestDist = d;
                    nearestEnemy = enemy.transform;
                }
            }

            if (nearestEnemy != null && nearestDist <= _attackRange)
            {
                AttackEnemy(nearestEnemy);
                _lastAttackTime = Time.time;
            }
        }
    }

    private void AttackEnemy(Transform enemy)
    {
        // 检查敌人是否死亡
        var ec = enemy.GetComponent<EnemyController>();
        if (ec == null || ec.IsDead) return;

        int dmg = _attackDamage + Random.Range(-3, 4);
        ec.TakeDamage(dmg, null);
    }

    public void TakeDamage(int damage)
    {
        if (_isDown) return;
        _hp -= damage;
        if (_hp <= 0)
        {
            _hp = 0;
            _isDown = true;
            _respawnTimer = 10f;
        }
    }

    private void UpdateHPBar()
    {
        if (_hpBarFill == null) return;
        float pct = (float)_hp / _maxHp;
        _hpBarFill.transform.localScale = new Vector3(pct, 1, 1);
        // 与战士一致：绿→黄，不变红
        _hpBarFill.color = pct > 0.5f ? new Color(0.2f, 0.9f, 0.2f) : new Color(0.9f, 0.8f, 0.2f);
    }

    private void OnDisable()
    {
        if (_nameText != null) _nameText.gameObject.SetActive(false);
        if (_hpBarObj != null) _hpBarObj.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_nameText != null) Destroy(_nameText.gameObject);
        if (_hpBarObj != null) Destroy(_hpBarObj);
    }
}
