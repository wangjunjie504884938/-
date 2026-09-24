using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 世界地图/传送点面板 — 手绘世界地图为底图，16个传送点按生态对位放置
/// 已发现: 亮色圆点+区名(可点击传送) / 未发现: 灰点 / 当前区: 金色高亮
/// </summary>
public class WaypointUI : MonoBehaviour
{
    public static WaypointUI Instance { get; private set; }
    private GameObject _panel;

    private const float BoxW = 1000f;
    private const float BoxH = 840f;

    private static Sprite _mapSprite;
    private static Sprite MapSprite
    {
        get
        {
            if (_mapSprite == null)
            {
                var tex = Resources.Load<Texture2D>(ResourcePaths.WorldMap);
                if (tex != null)
                    _mapSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
            return _mapSprite;
        }
    }

    public static void Toggle()
    {
        if (Instance != null && Instance._panel != null && Instance._panel.activeSelf) Hide();
        else Show();
    }

    public static void Show()
    {
        if (Instance == null)
        {
            var go = new GameObject("WaypointUI");
            go.AddComponent<WaypointUI>();
        }
        Instance?.BuildPanel();
    }

    public static void Hide()
    {
        if (Instance != null && Instance._panel != null)
        {
            Destroy(Instance._panel);
            Instance._panel = null;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void BuildPanel()
    {
        if (_panel != null) { _panel.SetActive(true); return; }

        var canvas = GameManager.EnsureCanvas();
        var font = GameManager.GetUIFont();

        _panel = new GameObject("WaypointPanel");
        _panel.transform.SetParent(canvas.transform, false);
        var pr = _panel.AddComponent<RectTransform>();
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one;
        pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;
        var bg = _panel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.8f);
        bg.raycastTarget = true;

        // 主卡片
        var box = UIHelper.MakeGlowCard(_panel.transform, "Box",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-BoxW / 2f, -BoxH / 2f), new Vector2(BoxW / 2f, BoxH / 2f),
            new Color(0.06f, 0.04f, 0.09f, 0.97f), UIHelper.GlowBottom, UIHelper.Accent);

        // 标题
        var titleObj = new GameObject("Title");
        titleObj.transform.SetParent(box.transform, false);
        var tR = titleObj.AddComponent<RectTransform>();
        tR.anchorMin = new Vector2(0, 1); tR.anchorMax = new Vector2(1, 1);
        tR.pivot = new Vector2(0.5f, 1); tR.anchoredPosition = new Vector2(0, -10);
        tR.sizeDelta = new Vector2(0, 40);
        var title = titleObj.AddComponent<Text>();
        title.text = "世界地图 — 传送点"; title.alignment = TextAnchor.MiddleCenter;
        title.fontSize = 24; title.color = UIHelper.Accent; title.font = font;

        // 关闭按钮
        UIHelper.MakeButton(box.transform, "CloseBtn", "✕", font,
            new Vector2(0.93f, 0.92f), new Vector2(0.99f, 0.99f), Vector2.zero, Vector2.zero,
            UIHelper.BtnDanger, 18, Hide);

        // 世界等级切换 + 魔潮横幅
        var topRow = new GameObject("TopBar");
        topRow.transform.SetParent(box.transform, false);
        var trR = topRow.AddComponent<RectTransform>();
        trR.anchorMin = new Vector2(0, 0.88f); trR.anchorMax = new Vector2(1, 0.92f);
        trR.offsetMin = Vector2.zero; trR.offsetMax = Vector2.zero;

        int tier = WorldData.WorldTier;
        UIHelper.MakeButton(topRow.transform, "TierBtn", $"世界等级 {tier} ▸", font,
            new Vector2(0.02f, 0), new Vector2(0.35f, 1), Vector2.zero, Vector2.zero,
            UIHelper.BtnPrimary, 14, () =>
            {
                WorldData.SetWorldTier(tier % 3 + 1);
                GameUI.Instance?.ShowItemPickupToast("世界等级", $"已切换到 {WorldData.WorldTier} 级 — 敌人更强奖励更丰");
                Hide();
                Show();
            });

        if (WorldManager.EventZoneIndex >= 0)
        {
            var evObj = new GameObject("EventBanner");
            evObj.transform.SetParent(topRow.transform, false);
            var evR = evObj.AddComponent<RectTransform>();
            evR.anchorMin = new Vector2(0.4f, 0); evR.anchorMax = new Vector2(1, 1);
            evR.offsetMin = Vector2.zero; evR.offsetMax = Vector2.zero;
            var evTxt = evObj.AddComponent<Text>();
            evTxt.text = $"🔥 魔潮袭击 {WorldData.Get(WorldManager.EventZoneIndex).Name}";
            evTxt.alignment = TextAnchor.MiddleCenter; evTxt.fontSize = 15;
            evTxt.color = new Color(1f, 0.4f, 0.3f); evTxt.font = font;
            evTxt.raycastTarget = false;
        }

        // ===== 手绘地图底图 (等比适配) =====
        var mapArea = new GameObject("MapArea");
        mapArea.transform.SetParent(box.transform, false);
        var maR = mapArea.AddComponent<RectTransform>();
        maR.anchorMin = new Vector2(0.03f, 0.04f); maR.anchorMax = new Vector2(0.97f, 0.87f);
        maR.offsetMin = Vector2.zero; maR.offsetMax = Vector2.zero;

        var mapObj = new GameObject("MapImage");
        mapObj.transform.SetParent(mapArea.transform, false);
        var mR = mapObj.AddComponent<RectTransform>();
        mR.anchorMin = new Vector2(0.5f, 0.5f); mR.anchorMax = new Vector2(0.5f, 0.5f);
        mR.pivot = new Vector2(0.5f, 0.5f);

        var sprite = MapSprite;
        float areaW = BoxW * 0.94f, areaH = BoxH * 0.83f;
        float mapW = areaW, mapH = areaH;
        if (sprite != null)
        {
            float aspect = (float)sprite.rect.width / sprite.rect.height;
            mapH = Mathf.Min(areaH, areaW / aspect);
            mapW = mapH * aspect;
        }
        else
        {
            // 底图缺失: 深色底兜底
            mR.sizeDelta = new Vector2(areaW, areaH);
            var fallback = mapObj.AddComponent<Image>();
            fallback.color = new Color(0.08f, 0.1f, 0.14f, 1f);
        }
        if (sprite != null)
        {
            mR.sizeDelta = new Vector2(mapW, mapH);
            var img = mapObj.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = true; // 挡住穿透点击
        }

        // ===== 传送点热点 =====
        var curPos = GameManager.Instance?.Player?.transform.position ?? Vector3.zero;
        var curZone = WorldData.GetAt(curPos.x, curPos.y);

        foreach (var zone in WorldData.Zones)
        {
            int idx = zone.Index;
            bool discovered = WorldData.IsDiscovered(idx);
            bool here = curZone != null && curZone.Index == idx;
            var anchor = WorldData.MapAnchors[idx];

            var dot = new GameObject($"Spot_{idx}");
            dot.transform.SetParent(mapObj.transform, false);
            var dR = dot.AddComponent<RectTransform>();
            dR.anchorMin = anchor; dR.anchorMax = anchor;
            dR.pivot = new Vector2(0.5f, 0.5f);
            dR.anchoredPosition = Vector2.zero;
            dR.sizeDelta = new Vector2(30, 30);

            var outer = dot.AddComponent<Image>();
            outer.raycastTarget = discovered; // 未发现不挡点击(虽然也没别的东西可点)

            var innerObj = new GameObject("Core");
            innerObj.transform.SetParent(dot.transform, false);
            var iR = innerObj.AddComponent<RectTransform>();
            iR.anchorMin = Vector2.zero; iR.anchorMax = Vector2.one;
            iR.offsetMin = new Vector2(6, 6); iR.offsetMax = new Vector2(-6, -6);
            var inner = innerObj.AddComponent<Image>();
            inner.raycastTarget = false;

            if (!discovered)
            {
                outer.color = new Color(0.25f, 0.25f, 0.3f, 0.75f);
                inner.color = new Color(0.45f, 0.45f, 0.5f, 0.6f);
                continue;
            }

            // 当前区: 金色 / 其他: 青色
            outer.color = here ? new Color(1f, 0.8f, 0.25f, 0.95f) : new Color(0.15f, 0.4f, 0.55f, 0.95f);
            inner.color = here ? new Color(1f, 0.95f, 0.6f) : new Color(0.4f, 0.9f, 1f);

            string label = here ? $"{zone.Name}·当前" : zone.Name;
            if (WorldData.IsZoneDone(idx)) label += " ✓";
            var lblObj = new GameObject("Name");
            lblObj.transform.SetParent(dot.transform, false);
            var lR = lblObj.AddComponent<RectTransform>();
            lR.anchorMin = new Vector2(0.5f, 1); lR.anchorMax = new Vector2(0.5f, 1);
            lR.pivot = new Vector2(0.5f, 0);
            lR.anchoredPosition = new Vector2(0, 2);
            lR.sizeDelta = new Vector2(150, 22);
            var lbl = lblObj.AddComponent<Text>();
            lbl.text = label;
            lbl.alignment = TextAnchor.MiddleCenter;
            lbl.fontSize = 13;
            lbl.color = here ? new Color(1f, 0.85f, 0.4f) : new Color(1f, 1f, 1f, 0.92f);
            lbl.font = font;
            lbl.raycastTarget = false;

            if (!here)
            {
                var btn = dot.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                UIHelper.SetupButtonFeedback(btn, outer.color);
                btn.onClick.AddListener(() =>
                {
                    WorldManager.TeleportTo(idx);
                    Hide();
                });
            }
        }
    }
}
