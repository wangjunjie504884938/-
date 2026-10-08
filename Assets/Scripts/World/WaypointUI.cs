using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 世界地图/传送点面板 — 图层分离: 完整地图作底图, 16个传送目标按区域类型做成独立图标按钮叠加
/// 已发现: 类型图标+区名Lv(可点击传送) / 未发现: 灰色? / 当前区: 金色高亮+呼吸动效; 底部类型图例
/// </summary>
public class WaypointUI : MonoBehaviour
{
    public static WaypointUI Instance { get; private set; }
    private GameObject _panel;
    private RectTransform _curDot;   // 当前区图标(呼吸动效)
    private float _pulseT;

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
        if (Instance != null) Instance._curDot = null;
    }

    // 当前区图标呼吸动效 (面板打开时)
    private void Update()
    {
        if (_panel == null || _curDot == null) return;
        _pulseT += Time.unscaledDeltaTime;
        float s = 1f + 0.10f * Mathf.Sin(_pulseT * 4f);
        _curDot.localScale = new Vector3(s, s, 1f);
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

        // ===== 图层分离: 底图之上叠加类型化传送图标按钮 =====
        var curPos = GameManager.Instance?.Player?.transform.position ?? Vector3.zero;
        var curZone = WorldData.GetAt(curPos.x, curPos.y);

        BuildLegend(mapObj.transform, font);

        foreach (var zone in WorldData.Zones)
        {
            int idx = zone.Index;
            bool discovered = WorldData.IsDiscovered(idx);
            bool here = curZone != null && curZone.Index == idx;
            var anchor = WorldData.MapAnchors[idx];
            var ui = WorldData.ZoneUi[idx];

            var dot = new GameObject($"Spot_{idx}");
            dot.transform.SetParent(mapObj.transform, false);
            var dR = dot.AddComponent<RectTransform>();
            dR.anchorMin = anchor; dR.anchorMax = anchor;
            dR.pivot = new Vector2(0.5f, 0.5f);
            dR.anchoredPosition = Vector2.zero;
            dR.sizeDelta = new Vector2(34, 34);

            // 图标按钮 (类型字符+类型底色; 未发现=灰; 当前区=金)
            var icon = dot.AddComponent<Image>();
            icon.raycastTarget = discovered;
            if (!discovered)
                icon.color = new Color(0.25f, 0.25f, 0.3f, 0.8f);
            else if (here)
                icon.color = new Color(1f, 0.8f, 0.25f, 1f);
            else
                icon.color = new Color(ui.R, ui.G, ui.B, 0.95f);

            var chrObj = new GameObject("Icon");
            chrObj.transform.SetParent(dot.transform, false);
            var cR = chrObj.AddComponent<RectTransform>();
            cR.anchorMin = Vector2.zero; cR.anchorMax = Vector2.one;
            cR.offsetMin = Vector2.zero; cR.offsetMax = Vector2.zero;
            var chr = chrObj.AddComponent<Text>();
            chr.text = discovered ? ui.Icon : "?";
            chr.alignment = TextAnchor.MiddleCenter;
            chr.fontSize = 18;
            chr.color = discovered ? new Color(1f, 1f, 1f, 0.96f) : new Color(0.75f, 0.75f, 0.8f, 0.9f);
            chr.font = font;
            chr.raycastTarget = false;

            // 名称标签 (当前/已发现=区名+难度; 未发现=???)
            string label;
            Color lblColor;
            if (here)
            {
                label = $"{zone.Name}·当前";
                if (WorldData.IsZoneDone(idx)) label += " ✓";
                lblColor = new Color(1f, 0.85f, 0.4f);
            }
            else if (discovered)
            {
                label = $"{zone.Name} Lv.{zone.Tier + 1}";
                if (WorldData.IsZoneDone(idx)) label += " ✓";
                lblColor = new Color(1f, 1f, 1f, 0.92f);
            }
            else
            {
                label = "???";
                lblColor = new Color(0.8f, 0.8f, 0.85f, 0.7f);
            }

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
            lbl.color = lblColor;
            lbl.font = font;
            lbl.raycastTarget = false;

            if (here)
            {
                _curDot = dR;
            }
            else if (discovered)
            {
                var btn = dot.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                UIHelper.SetupButtonFeedback(btn, icon.color);
                btn.onClick.AddListener(() =>
                {
                    WorldManager.TeleportTo(idx);
                    Hide();
                });
            }
        }
    }

    // ===== 底部图例 (类型图标速查) =====
    private static void BuildLegend(Transform parent, Font font)
    {
        (string icon, string kind, Color col)[] items =
        {
            ("城", "主城", new Color(0.40f, 0.70f, 1.00f)),
            ("村", "新手", new Color(0.45f, 0.85f, 0.45f)),
            ("野", "野外", new Color(0.30f, 0.80f, 0.70f)),
            ("宗", "宗门", new Color(0.45f, 0.65f, 1.00f)),
            ("探", "高级", new Color(1.00f, 0.55f, 0.75f)),
            ("塞", "要塞", new Color(0.60f, 0.65f, 0.80f)),
            ("战", "争夺", new Color(1.00f, 0.50f, 0.30f)),
            ("渊", "魔都", new Color(0.95f, 0.30f, 0.30f)),
        };

        var legend = new GameObject("Legend");
        legend.transform.SetParent(parent, false);
        var lRt = legend.AddComponent<RectTransform>();
        lRt.anchorMin = new Vector2(0f, 0f); lRt.anchorMax = new Vector2(1f, 0f);
        lRt.pivot = new Vector2(0.5f, 0f);
        lRt.anchoredPosition = new Vector2(0f, 4f);
        lRt.sizeDelta = new Vector2(0f, 26f);
        var lbg = legend.AddComponent<Image>();
        lbg.color = new Color(0f, 0f, 0f, 0.55f);
        lbg.raycastTarget = false;

        int n = items.Length;
        for (int i = 0; i < n; i++)
        {
            var item = new GameObject($"Item_{i}");
            item.transform.SetParent(legend.transform, false);
            var iRt = item.AddComponent<RectTransform>();
            float x0 = (float)i / n, x1 = (float)(i + 1) / n;
            iRt.anchorMin = new Vector2(x0, 0f); iRt.anchorMax = new Vector2(x1, 1f);
            iRt.offsetMin = Vector2.zero; iRt.offsetMax = Vector2.zero;

            var boxObj = new GameObject("Box");
            boxObj.transform.SetParent(item.transform, false);
            var bR = boxObj.AddComponent<RectTransform>();
            bR.anchorMin = new Vector2(0.06f, 0.18f); bR.anchorMax = new Vector2(0.26f, 0.82f);
            bR.offsetMin = Vector2.zero; bR.offsetMax = Vector2.zero;
            var box = boxObj.AddComponent<Image>();
            box.color = items[i].col;
            box.raycastTarget = false;
            var chrObj = new GameObject("C");
            chrObj.transform.SetParent(boxObj.transform, false);
            var cR = chrObj.AddComponent<RectTransform>();
            cR.anchorMin = Vector2.zero; cR.anchorMax = Vector2.one;
            cR.offsetMin = Vector2.zero; cR.offsetMax = Vector2.zero;
            var chr = chrObj.AddComponent<Text>();
            chr.text = items[i].icon;
            chr.alignment = TextAnchor.MiddleCenter;
            chr.fontSize = 12;
            chr.color = Color.white;
            chr.font = font;
            chr.raycastTarget = false;

            var txtObj = new GameObject("T");
            txtObj.transform.SetParent(item.transform, false);
            var tR = txtObj.AddComponent<RectTransform>();
            tR.anchorMin = new Vector2(0.30f, 0f); tR.anchorMax = new Vector2(1f, 1f);
            tR.offsetMin = Vector2.zero; tR.offsetMax = Vector2.zero;
            var txt = txtObj.AddComponent<Text>();
            txt.text = items[i].kind;
            txt.alignment = TextAnchor.MiddleLeft;
            txt.fontSize = 12;
            txt.color = new Color(1f, 1f, 1f, 0.85f);
            txt.font = font;
            txt.raycastTarget = false;
        }
    }
}
