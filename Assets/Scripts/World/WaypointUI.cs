using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 世界地图/传送点面板 — 完整大地图作底图, 透明热区按钮精准覆盖图中城池标注(完美重合不遮挡)
/// 已发现: 悬停高亮+点击传送+Lv小标注 / 未发现: 灰? / 当前区: 金点呼吸; 城池名与类型由地图绘制呈现
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
        // 世界页面导航栏: 返回+设置+四货币 (用户要求把导航上的返回和设置还给世界页面)
        UIHelper.SetupTopBar("世界地图", () => Hide());
    }

    public static void Hide()
    {
        if (Instance != null && Instance._panel != null)
        {
            Destroy(Instance._panel);
            Instance._panel = null;
        }
        if (Instance != null) Instance._curDot = null;
        // 世界与副本同一套导航: 关闭弹窗后收起导航栏 (InGameHud的返回/设置常显, 无需恢复导航条)
        TopNavBar.Instance?.HideBar();
    }

    // 当前区图标呼吸动效 (面板打开时)
    private void Update()
    {
        if (_panel == null || _curDot == null) return;
        _pulseT += Time.unscaledDeltaTime;
        float s = 1f + 0.18f * Mathf.Sin(_pulseT * 4f);
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

        foreach (var zone in WorldData.Zones)
        {
            int idx = zone.Index;
            bool discovered = WorldData.IsDiscovered(idx);
            bool here = curZone != null && curZone.Index == idx;
            var anchor = WorldData.MapAnchors[idx];
            string doneMark = WorldData.IsZoneDone(idx) ? " ✓" : "";

            var dot = new GameObject($"Spot_{idx}");
            dot.transform.SetParent(mapObj.transform, false);
            var dR = dot.AddComponent<RectTransform>();
            dR.anchorMin = anchor; dR.anchorMax = anchor;
            dR.pivot = new Vector2(0.5f, 0.5f);
            dR.anchoredPosition = Vector2.zero;

            if (here)
            {
                // 当前所在城池: 金色小点(呼吸动效) + 标签
                dR.sizeDelta = new Vector2(14, 14);
                var cur = dot.AddComponent<Image>();
                cur.color = new Color(1f, 0.8f, 0.25f, 0.95f);
                cur.raycastTarget = false;
                _curDot = dR;
                MakeSpotLabel(dot.transform, font, $"当前 Lv.{zone.Tier + 1}{doneMark}", new Color(1f, 0.85f, 0.4f, 0.95f), -30f);
            }
            else if (discovered)
            {
                // 已发现城池: 透明热区精确覆盖图中标注(完美重合不遮挡), 悬停高亮, 点击传送
                dR.sizeDelta = new Vector2(110, 48);
                var hit = dot.AddComponent<Image>();
                hit.color = Color.clear;
                hit.raycastTarget = true;

                var hoverObj = new GameObject("Hover");
                hoverObj.transform.SetParent(dot.transform, false);
                var hR = hoverObj.AddComponent<RectTransform>();
                hR.anchorMin = Vector2.zero; hR.anchorMax = Vector2.one;
                hR.offsetMin = Vector2.zero; hR.offsetMax = Vector2.zero;
                var hover = hoverObj.AddComponent<Image>();
                hover.color = new Color(1f, 0.85f, 0.4f, 0.22f);
                hover.raycastTarget = false;
                hoverObj.SetActive(false);

                MakeSpotLabel(dot.transform, font, $"Lv.{zone.Tier + 1}{doneMark}", new Color(1f, 1f, 1f, 0.78f), -26f);

                var btn = dot.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() =>
                {
                    WorldManager.TeleportTo(idx);
                    Hide();
                });

                var trig = dot.AddComponent<EventTrigger>();
                var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enter.callback.AddListener(_ => hoverObj.SetActive(true));
                trig.triggers.Add(enter);
                var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                exit.callback.AddListener(_ => hoverObj.SetActive(false));
                trig.triggers.Add(exit);
            }
            else
            {
                // 未发现城池: 灰色? 提示(不可点击)
                dR.sizeDelta = new Vector2(26, 26);
                var q = dot.AddComponent<Text>();
                q.text = "?";
                q.font = font;
                q.fontSize = 18;
                q.alignment = TextAnchor.MiddleCenter;
                q.color = new Color(0.85f, 0.85f, 0.9f, 0.6f);
                q.raycastTarget = false;
            }
        }
    }

    // 城池标注下方的小标签 (图中已绘制城池名与类型, 这里只补充难度/完成度信息)
    private static void MakeSpotLabel(Transform parent, Font font, string text, Color color, float yTop)
    {
        var lblObj = new GameObject("Name");
        lblObj.transform.SetParent(parent, false);
        var lR = lblObj.AddComponent<RectTransform>();
        lR.anchorMin = new Vector2(0.5f, 0f); lR.anchorMax = new Vector2(0.5f, 0f);
        lR.pivot = new Vector2(0.5f, 1f);
        lR.anchoredPosition = new Vector2(0, yTop);
        lR.sizeDelta = new Vector2(150, 20);
        var lbl = lblObj.AddComponent<Text>();
        lbl.text = text;
        lbl.alignment = TextAnchor.MiddleCenter;
        lbl.fontSize = 13;
        lbl.color = color;
        lbl.font = font;
        lbl.raycastTarget = false;
    }
}
