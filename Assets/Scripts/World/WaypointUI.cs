using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 世界地图/传送点面板 — 列出全部区域，已发现的传送点可快速旅行
/// </summary>
public class WaypointUI : MonoBehaviour
{
    public static WaypointUI Instance { get; private set; }
    private GameObject _panel;

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
        bg.color = new Color(0f, 0f, 0f, 0.75f);
        bg.raycastTarget = true;

        // 主卡片
        var box = UIHelper.MakeGlowCard(_panel.transform, "Box",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-430, -400), new Vector2(430, 400),
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

        // 滚动列表 4列×4行
        var grid = new GameObject("Grid");
        grid.transform.SetParent(box.transform, false);
        var gR = grid.AddComponent<RectTransform>();
        gR.anchorMin = new Vector2(0.03f, 0.08f); gR.anchorMax = new Vector2(0.97f, 0.88f);
        gR.offsetMin = Vector2.zero; gR.offsetMax = Vector2.zero;

        int cols = 4;
        float cellW = 1f / cols, cellH = 1f / 4f;
        foreach (var zone in WorldData.Zones)
        {
            int idx = zone.Index;
            bool discovered = WorldData.IsDiscovered(idx);
            int gx = idx % cols, gy = idx / cols;

            var cell = new GameObject($"Zone_{idx}");
            cell.transform.SetParent(grid.transform, false);
            var cR = cell.AddComponent<RectTransform>();
            cR.anchorMin = new Vector2(cellW * gx, 1 - cellH * (gy + 1));
            cR.anchorMax = new Vector2(cellW * (gx + 1), 1 - cellH * gy);
            cR.offsetMin = new Vector2(3, 3); cR.offsetMax = new Vector2(-3, -3);
            var cImg = cell.AddComponent<Image>();
            cImg.color = discovered ? new Color(0.1f, 0.14f, 0.2f, 0.9f) : new Color(0.06f, 0.06f, 0.08f, 0.9f);

            var nameObj = new GameObject("Name");
            nameObj.transform.SetParent(cell.transform, false);
            var nR = nameObj.AddComponent<RectTransform>();
            nR.anchorMin = new Vector2(0, 0.55f); nR.anchorMax = new Vector2(1, 0.9f);
            nR.offsetMin = Vector2.zero; nR.offsetMax = Vector2.zero;
            var nameTxt = nameObj.AddComponent<Text>();
            nameTxt.text = discovered ? zone.Name : "？？？";
            nameTxt.alignment = TextAnchor.MiddleCenter; nameTxt.fontSize = 15;
            nameTxt.color = discovered ? UIHelper.TextPrimary : UIHelper.TextDim; nameTxt.font = font;
            nameTxt.raycastTarget = false;

            var infoObj = new GameObject("Info");
            infoObj.transform.SetParent(cell.transform, false);
            var iR = infoObj.AddComponent<RectTransform>();
            iR.anchorMin = new Vector2(0, 0.12f); iR.anchorMax = new Vector2(1, 0.5f);
            iR.offsetMin = Vector2.zero; iR.offsetMax = Vector2.zero;
            var infoTxt = infoObj.AddComponent<Text>();
            string infoText;
            if (!discovered) infoText = "未发现";
            else if (WorldData.IsZoneDone(idx)) infoText = "✓ 声望完成";
            else infoText = $"击杀 {WorldManager.GetZoneKills(idx)}/10";
            infoTxt.text = infoText;
            infoTxt.alignment = TextAnchor.MiddleCenter; infoTxt.fontSize = 12;
            infoTxt.color = UIHelper.TextSecondary; infoTxt.font = font;
            infoTxt.raycastTarget = false;

            if (discovered)
            {
                var curPos = GameManager.Instance?.Player?.transform.position ?? Vector3.zero;
                var curZone = WorldData.GetAt(curPos.x, curPos.y);
                bool here = curZone != null && curZone.Index == idx;
                UIHelper.MakeButton(cell.transform, "TpBtn", here ? "当前" : "传送", font,
                    new Vector2(0.25f, 0.02f), new Vector2(0.75f, 0.2f), Vector2.zero, Vector2.zero,
                    here ? new Color(0.2f, 0.4f, 0.2f, 0.9f) : UIHelper.BtnPrimary, 12,
                    () =>
                    {
                        if (!here)
                        {
                            WorldManager.TeleportTo(idx);
                            Hide();
                        }
                    });
            }
        }
    }
}
