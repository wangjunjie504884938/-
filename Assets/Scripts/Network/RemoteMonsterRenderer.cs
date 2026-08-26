using UnityEngine;
using UnityEngine.UI;
using ArpgShared;

/// <summary>
/// 远程怪物渲染器 — 在客户端显示服务器端的怪物
/// 接收Mirror状态快照后更新位置/血量
/// </summary>
public class RemoteMonsterRenderer : MonoBehaviour
{
    public int monsterId;
    public int monsterType;

    private SpriteRenderer _sr;
    private Vector2 _targetPosition;
    private float _smoothSpeed = 6f;

    private static readonly Color[] TypeColors =
    {
        new Color(0.8f, 0.3f, 0.3f, 1f), // 近战-红
        new Color(0.5f, 0.3f, 0.8f, 1f), // 远程-紫
        new Color(0.9f, 0.6f, 0.1f, 1f), // 精英-橙
        new Color(0.9f, 0.1f, 0.1f, 1f), // Boss-深红
    };

    private static readonly float[] TypeSizes = { 0.5f, 0.45f, 0.7f, 1.2f };

    /// <summary>初始化怪物显示</summary>
    public void Initialize(int id, int type, Vector2 pos)
    {
        monsterId = id;
        monsterType = type;
        _targetPosition = pos;
        transform.position = pos;

        int typeIdx = Mathf.Clamp(type, 0, 3);
        var color = TypeColors[typeIdx];
        float size = TypeSizes[typeIdx];

        _sr = gameObject.AddComponent<SpriteRenderer>();
        _sr.sprite = SpriteCache.WhitePixel;
        _sr.color = color;
        _sr.sortingOrder = type == 3 ? 10 : 6;
        transform.localScale = new Vector3(size, size * 1.2f, 1f);

        // Boss名字
        if (type == 3)
        {
            var nameObj = new GameObject("BossName");
            nameObj.transform.SetParent(transform, false);
            var nameR = nameObj.AddComponent<RectTransform>();
            nameR.anchorMin = new Vector2(0.5f, 0); nameR.anchorMax = new Vector2(0.5f, 0);
            nameR.pivot = new Vector2(0.5f, 0);
            nameR.anchoredPosition = new Vector2(0, size * 0.8f);
            nameR.sizeDelta = new Vector2(200, 24);
            var nameText = nameObj.AddComponent<Text>();
            nameText.text = "<color=#FF4444>☠ BOSS</color>";
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.fontSize = 14;
            nameText.color = Color.white;
            nameText.font = GameManager.GetUIFont();
            nameText.supportRichText = true;
            nameText.raycastTarget = false;
        }

        gameObject.layer = LayerMask.NameToLayer("Enemy");
    }

    /// <summary>从状态快照更新</summary>
    public void UpdateFromState(ArpgShared.MonsterState state)
    {
        _targetPosition = state.position;
        transform.position = Vector2.Lerp(transform.position, _targetPosition, Time.deltaTime * _smoothSpeed);

        if (_sr != null)
        {
            var c = _sr.color;
            _sr.color = new Color(c.r, c.g, c.b, state.isDead ? 0f : 1f);
        }

        if (state.isDead)
            gameObject.SetActive(false);
    }
}
