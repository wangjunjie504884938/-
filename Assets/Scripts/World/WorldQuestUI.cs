using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 世界任务面板 — 区域发布人对话：接取/进度/交付讨伐任务
/// 另含世界HUD下方的任务追踪文本 (UpdateTracker由WorldManager每帧驱动)
/// </summary>
public class WorldQuestUI : MonoBehaviour
{
    public static WorldQuestUI Instance { get; private set; }
    private GameObject _panel;
    private static GameObject _tracker;
    private static Text _trackerText;

    public static void Show(int zoneIndex)
    {
        if (Instance == null)
        {
            var go = new GameObject("WorldQuestUI");
            go.AddComponent<WorldQuestUI>();
        }
        Instance?.BuildPanel(zoneIndex);
    }

    public static void Hide()
    {
        if (Instance != null && Instance._panel != null)
        {
            Object.Destroy(Instance._panel);
            Instance._panel = null;
        }
    }

    /// <summary>退出世界时清理面板与追踪器</summary>
    public static void ShutdownWorld()
    {
        Hide();
        if (_tracker != null)
        {
            Object.Destroy(_tracker);
            _tracker = null;
            _trackerText = null;
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

    private void BuildPanel(int zoneIndex)
    {
        Hide();
        var zone = WorldData.Get(zoneIndex);
        var q = WorldQuests.Get(zoneIndex);
        int state = q?.state ?? WorldQuests.StateNone;
        int target = WorldQuests.Target(zoneIndex);
        var font = GameManager.GetUIFont();
        var canvas = GameManager.EnsureCanvas();

        _panel = new GameObject("WorldQuestPanel");
        _panel.transform.SetParent(canvas.transform, false);
        var pr = _panel.AddComponent<RectTransform>();
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one;
        pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;
        var bg = _panel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.75f);
        bg.raycastTarget = true;

        var box = UIHelper.MakeGlowCard(_panel.transform, "Box",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-280, -240), new Vector2(280, 240),
            new Color(0.06f, 0.04f, 0.09f, 0.97f), UIHelper.GlowBottom, UIHelper.Accent);

        // 标题
        var titleObj = new GameObject("Title");
        titleObj.transform.SetParent(box.transform, false);
        var tR = titleObj.AddComponent<RectTransform>();
        tR.anchorMin = new Vector2(0, 1); tR.anchorMax = new Vector2(1, 1);
        tR.pivot = new Vector2(0.5f, 1); tR.anchoredPosition = new Vector2(0, -10);
        tR.sizeDelta = new Vector2(0, 40);
        var title = titleObj.AddComponent<Text>();
        title.text = $"任务 · {zone.Name}发布人";
        title.alignment = TextAnchor.MiddleCenter;
        title.fontSize = 24; title.color = UIHelper.Accent; title.font = font;

        // 关闭按钮
        UIHelper.MakeButton(box.transform, "CloseBtn", "✕", font,
            new Vector2(0.93f, 0.92f), new Vector2(0.99f, 0.99f), Vector2.zero, Vector2.zero,
            UIHelper.BtnDanger, 18, Hide);

        // 任务描述
        AddLabel(box.transform, "Desc", $"讨伐：击杀 {zone.Name} 的怪物 {target} 只",
            new Vector2(0, 0.62f), new Vector2(1, 0.78f), 17, UIHelper.TextPrimary, font);

        // 奖励
        AddLabel(box.transform, "Reward",
            $"奖励：金币×{WorldQuests.GoldReward(zoneIndex)} 经验×{WorldQuests.XpReward(zoneIndex)}",
            new Vector2(0, 0.5f), new Vector2(1, 0.62f), 15, new Color(1f, 0.85f, 0.4f), font);

        // 状态 / 完成次数
        string statusText;
        switch (state)
        {
            case WorldQuests.StateInProgress: statusText = q.kills >= target ? "目标达成" : $"进度 {q.kills}/{target}"; break;
            case WorldQuests.StateTurnIn: statusText = "✓ 目标达成 — 可交付"; break;
            default: statusText = "可接取"; break;
        }
        if (q != null && q.doneCount > 0) statusText += $"   (已完成 {q.doneCount} 次)";
        AddLabel(box.transform, "Status", statusText,
            new Vector2(0, 0.32f), new Vector2(1, 0.5f), 14, UIHelper.TextSecondary, font);

        // 主按钮 (状态机驱动，MakeButton必须带onClick)
        string btnLabel;
        Color btnColor;
        System.Action onClick;
        switch (state)
        {
            case WorldQuests.StateInProgress:
                btnLabel = q.kills >= target ? "可交付 ▸" : "进行中";
                btnColor = new Color(0.25f, 0.25f, 0.3f, 0.9f);
                onClick = () => GameUI.Instance?.ShowItemPickupToast("讨伐任务", $"在 {zone.Name} 击杀怪物推进任务");
                break;
            case WorldQuests.StateTurnIn:
                btnLabel = "交付奖励";
                btnColor = new Color(0.2f, 0.5f, 0.25f, 0.95f);
                onClick = () => { WorldQuests.TurnIn(zoneIndex); BuildPanel(zoneIndex); };
                break;
            default:
                btnLabel = "接受任务";
                btnColor = UIHelper.BtnPrimary;
                onClick = () => { WorldQuests.Accept(zoneIndex); BuildPanel(zoneIndex); };
                break;
        }
        UIHelper.MakeButton(box.transform, "MainBtn", btnLabel, font,
            new Vector2(0.3f, 0.1f), new Vector2(0.7f, 0.26f), Vector2.zero, Vector2.zero,
            btnColor, 17, onClick);
    }

    private static void AddLabel(Transform parent, string name, string text,
        Vector2 anchorMin, Vector2 anchorMax, int fontSize, Color color, Font font)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var r = obj.AddComponent<RectTransform>();
        r.anchorMin = anchorMin; r.anchorMax = anchorMax;
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        var txt = obj.AddComponent<Text>();
        txt.text = text;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontSize = fontSize;
        txt.color = color;
        txt.font = font;
        txt.raycastTarget = false;
    }

    // ===== 任务追踪器 (世界HUD按钮下方，右对齐) =====
    public static void UpdateTracker()
    {
        if (!WorldManager.Active) return;
        var player = GameManager.Instance?.Player;
        if (player == null) return;
        if (_tracker == null) CreateTracker();

        var zone = WorldData.GetAt(player.transform.position.x, player.transform.position.y);
        var q = WorldQuests.Get(zone.Index);
        string txt = "";
        if (q != null)
        {
            if (q.state == WorldQuests.StateInProgress)
                txt = $"📋 讨伐·{zone.Name}  {q.kills}/{WorldQuests.Target(zone.Index)}";
            else if (q.state == WorldQuests.StateTurnIn)
                txt = "📋 任务完成 → 找发布人交付";
        }
        if (_trackerText != null && _trackerText.text != txt) _trackerText.text = txt;
    }

    private static void CreateTracker()
    {
        var canvas = GameManager.EnsureCanvas();
        _tracker = new GameObject("WorldQuestTracker");
        _tracker.transform.SetParent(canvas.transform, false);
        var r = _tracker.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(1, 1); r.anchorMax = new Vector2(1, 1);
        r.pivot = new Vector2(1, 1);
        r.anchoredPosition = new Vector2(-14, -60);
        r.sizeDelta = new Vector2(420, 26);
        _trackerText = _tracker.AddComponent<Text>();
        _trackerText.alignment = TextAnchor.UpperRight;
        _trackerText.fontSize = 15;
        _trackerText.color = new Color(1f, 0.85f, 0.4f);
        _trackerText.font = GameManager.GetUIFont();
        _trackerText.raycastTarget = false;
    }
}
