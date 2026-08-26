using UnityEngine;
using UnityEngine.UI;
using ArpgShared;

/// <summary>
/// 远程玩家渲染器 — 在客户端显示其他在线玩家
/// 接收Mirror状态快照后更新位置/血量/名字
/// </summary>
public class RemotePlayerRenderer : MonoBehaviour
{
    public int playerId;
    public string playerName = "Player";
    public int classType;
    public int level;

    private SpriteRenderer _sr;
    private Text _nameText;
    private GameObject _hpBarObj;
    private SpriteRenderer _hpBarFill;
    private Vector2 _targetPosition;
    private float _smoothSpeed = 8f;

    private static readonly Color[] ClassColors =
    {
        new Color(0.85f, 0.2f, 0.2f, 1f), // 战士-红
        new Color(0.2f, 0.4f, 0.9f, 1f),  // 法师-蓝
        new Color(0.9f, 0.85f, 0.4f, 1f), // 牧师-黄
    };
    private static readonly string[] ClassNames = { "战士", "法师", "牧师" };

    /// <summary>初始化远程玩家显示</summary>
    public void Initialize(int id, string name, int cls, int lv)
    {
        playerId = id;
        playerName = name;
        classType = cls;
        level = lv;

        int clsIdx = Mathf.Clamp(cls, 0, 2);
        var color = ClassColors[clsIdx];

        // 角色精灵
        _sr = gameObject.AddComponent<SpriteRenderer>();
        _sr.sprite = SpriteCache.WhitePixel;
        _sr.color = color;
        _sr.sortingOrder = 5;
        transform.localScale = new Vector3(0.5f, 0.7f, 1f);

        var glowObj = new GameObject("Glow");
        glowObj.transform.SetParent(transform, false);
        var glowSr = glowObj.AddComponent<SpriteRenderer>();
        glowSr.sprite = SpriteCache.WhitePixel;
        glowSr.color = new Color(color.r, color.g, color.b, 0.15f);
        glowSr.sortingOrder = 4;
        glowObj.transform.localScale = new Vector3(1.4f, 1.3f, 1f);

        // 名字标签
        var nameObj = new GameObject("NameLabel");
        nameObj.transform.SetParent(transform, false);
        var nameR = nameObj.AddComponent<RectTransform>();
        nameR.anchorMin = new Vector2(0.5f, 0); nameR.anchorMax = new Vector2(0.5f, 0);
        nameR.pivot = new Vector2(0.5f, 0);
        nameR.anchoredPosition = new Vector2(0, 0.8f);
        nameR.sizeDelta = new Vector2(120, 20);
        _nameText = nameObj.AddComponent<Text>();
        string cc = ColorUtility.ToHtmlStringRGBA(color);
        _nameText.text = $"<color=#{cc}>[{ClassNames[clsIdx]}]</color> {name}";
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

        gameObject.layer = LayerMask.NameToLayer("Player");
    }

    /// <summary>从状态快照更新</summary>
    public void UpdateFromState(ArpgShared.PlayerState state)
    {
        _targetPosition = state.position;

        // 平滑移动到目标位置
        transform.position = Vector2.Lerp(transform.position, _targetPosition, Time.deltaTime * _smoothSpeed);

        // 更新血条
        if (_hpBarFill != null)
        {
            float pct = state.maxHp > 0 ? (float)state.currentHp / state.maxHp : 0;
            _hpBarFill.transform.localScale = new Vector3(pct, 1, 1);
            // 与战士一致：绿→黄，不变红
            _hpBarFill.color = pct > 0.5f ? new Color(0.2f, 0.9f, 0.2f) : new Color(0.9f, 0.8f, 0.2f);
        }

        // 死亡/复活显示
        if (_nameText != null)
        {
            if (state.isDead)
                _nameText.text = $"<color=#FF4444>☠ {playerName}</color>";
            else
            {
                int clsIdx = Mathf.Clamp(classType, 0, 2);
                string cc = ColorUtility.ToHtmlStringRGBA(ClassColors[clsIdx]);
                _nameText.text = $"<color=#{cc}>[{ClassNames[clsIdx]}]</color> {playerName}";
            }
        }

        // 死亡时半透明
        if (_sr != null)
        {
            var c = _sr.color;
            _sr.color = new Color(c.r, c.g, c.b, state.isDead ? 0.3f : 1f);
        }
    }

    private void OnDestroy()
    {
        if (_nameText != null) Destroy(_nameText.gameObject);
        if (_hpBarObj != null) Destroy(_hpBarObj);
    }
}
