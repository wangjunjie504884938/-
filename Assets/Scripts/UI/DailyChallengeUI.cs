using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 每日挑战面板 — 展示今日修饰符，开始挑战，领取奖励
/// </summary>
public class DailyChallengeUI : MonoBehaviour
{
    public static DailyChallengeUI Instance { get; private set; }

    private GameObject panel;
    private Coroutine showRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Show()
    {
        if (panel != null) { Destroy(panel); panel = null; }
        BuildUI();
        UIHelper.SetupTopBar("每日挑战", () => { Hide(); UIHelper.EnsureUIManager(); UIManager.Instance.ShowHub(); });
        panel.SetActive(true);
        if (showRoutine != null) StopCoroutine(showRoutine);
        showRoutine = StartCoroutine(UIHelper.FadeIn(panel, 0.2f));
    }

    public void Hide()
    {
        if (panel != null)
        {
            if (showRoutine != null) StopCoroutine(showRoutine);
            showRoutine = StartCoroutine(UIHelper.FadeOut(panel, 0.15f));
        }
    }

    private void BuildUI()
    {
        Font font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();

        panel = new GameObject("DailyChallengePanel");
        panel.transform.SetParent(canvas.transform, false);
        RectTransform pr = panel.AddComponent<RectTransform>();
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one;
        pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;
        panel.AddComponent<CanvasGroup>();

        var bg = new GameObject("BG");
        bg.transform.SetParent(panel.transform, false);
        var bgr = bg.AddComponent<RectTransform>();
        bgr.anchorMin = Vector2.zero; bgr.anchorMax = Vector2.one;
        bgr.offsetMin = Vector2.zero; bgr.offsetMax = Vector2.zero;
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = UIHelper.BgDark;
        bgImg.raycastTarget = true;

        var mod = DailyChallenge.GetTodayModifier();
        bool completed = DailyChallenge.IsCompleted();

        // 修饰符展示卡片
        var card = UIHelper.MakeGlowCard(panel.transform, "ModCard",
            new Vector2(0.15f, 0.35f), new Vector2(0.85f, 0.75f),
            Vector2.zero, Vector2.zero,
            new Color(0.05f, 0.04f, 0.08f, 0.92f),
            UIHelper.GlowBottom,
            mod.ThemeColor);

        // 图标
        var iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(card.transform, false);
        var ir = iconObj.AddComponent<RectTransform>();
        ir.anchorMin = new Vector2(0.5f, 1f); ir.anchorMax = new Vector2(0.5f, 1f);
        ir.pivot = new Vector2(0.5f, 1);
        ir.anchoredPosition = new Vector2(0, -15);
        ir.sizeDelta = new Vector2(80, 80);
        var iconTxt = iconObj.AddComponent<Text>();
        iconTxt.text = ""; iconTxt.alignment = TextAnchor.MiddleCenter;
        iconTxt.fontSize = 48; iconTxt.color = mod.ThemeColor; iconTxt.font = font;
        iconTxt.raycastTarget = false;

        // 名称
        var nameObj = new GameObject("Name");
        nameObj.transform.SetParent(card.transform, false);
        var nr = nameObj.AddComponent<RectTransform>();
        nr.anchorMin = new Vector2(0.1f, 0.55f); nr.anchorMax = new Vector2(0.9f, 0.72f);
        nr.offsetMin = Vector2.zero; nr.offsetMax = Vector2.zero;
        var nameTxt = nameObj.AddComponent<Text>();
        nameTxt.text = mod.Name; nameTxt.alignment = TextAnchor.MiddleCenter;
        nameTxt.fontSize = 26; nameTxt.color = mod.ThemeColor; nameTxt.font = font;
        nameTxt.raycastTarget = false;

        // 描述
        var descObj = new GameObject("Desc");
        descObj.transform.SetParent(card.transform, false);
        var dr2 = descObj.AddComponent<RectTransform>();
        dr2.anchorMin = new Vector2(0.1f, 0.30f); dr2.anchorMax = new Vector2(0.9f, 0.52f);
        dr2.offsetMin = Vector2.zero; dr2.offsetMax = Vector2.zero;
        var descTxt = descObj.AddComponent<Text>();
        descTxt.text = mod.Desc; descTxt.alignment = TextAnchor.MiddleCenter;
        descTxt.fontSize = 18; descTxt.color = UIHelper.TextPrimary; descTxt.font = font;
        descTxt.raycastTarget = false;

        // 奖励
        var rewardObj = new GameObject("Reward");
        rewardObj.transform.SetParent(card.transform, false);
        var rr2 = rewardObj.AddComponent<RectTransform>();
        rr2.anchorMin = new Vector2(0.1f, 0.10f); rr2.anchorMax = new Vector2(0.9f, 0.28f);
        rr2.offsetMin = Vector2.zero; rr2.offsetMax = Vector2.zero;
        var rewardTxt = rewardObj.AddComponent<Text>();
        rewardTxt.text = $"奖励: {DailyChallenge.RewardGold}金币"; rewardTxt.alignment = TextAnchor.MiddleCenter;
        rewardTxt.fontSize = 20; rewardTxt.color = UIHelper.Accent; rewardTxt.font = font;
        rewardTxt.raycastTarget = false;

        // 按钮
        var btnObj = UIHelper.MakeGlowCard(panel.transform, "ActionBtn",
            new Vector2(0.3f, 0.15f), new Vector2(0.7f, 0.28f),
            Vector2.zero, Vector2.zero,
            new Color(0.04f, 0.08f, 0.06f, 0.92f),
            new Color(0.02f, 0.04f, 0.03f, 0.92f),
            UIHelper.Accent);
        var btn = btnObj.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;

        var lbl = new GameObject("Lbl");
        lbl.transform.SetParent(btnObj.transform, false);
        var lr = lbl.AddComponent<RectTransform>();
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
        lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;
        var btnTxt = lbl.AddComponent<Text>();
        btnTxt.alignment = TextAnchor.MiddleCenter;
        btnTxt.fontSize = 22; btnTxt.color = UIHelper.TextPrimary; btnTxt.font = font;
        btnTxt.raycastTarget = false;

        if (completed)
        {
            btn.interactable = false;
            btnTxt.text = "✓ 今日已完成";
            btnTxt.color = new Color(0.4f, 0.8f, 0.4f);
        }
        else
        {
            btnTxt.text = "开始挑战";
            btn.onClick.AddListener(OnStartChallenge);
        }

        // 提示
        var hintObj = new GameObject("Hint");
        hintObj.transform.SetParent(panel.transform, false);
        var hr2 = hintObj.AddComponent<RectTransform>();
        hr2.anchorMin = new Vector2(0.1f, 0.05f); hr2.anchorMax = new Vector2(0.9f, 0.12f);
        hr2.offsetMin = Vector2.zero; hr2.offsetMax = Vector2.zero;
        var hintTxt = hintObj.AddComponent<Text>();
        hintTxt.text = "通关任意关卡即可完成挑战，每天刷新";
        hintTxt.alignment = TextAnchor.MiddleCenter;
        hintTxt.fontSize = 14; hintTxt.color = UIHelper.TextDim; hintTxt.font = font;
        hintTxt.raycastTarget = false;

        panel.SetActive(false);
    }

    private void OnStartChallenge()
    {
        DailyChallenge.StartChallenge();
        Hide();
        GameManager.Instance.StartDungeon(0);
    }

    private void OnDestroy()
    {
        if (panel != null) Destroy(panel);
        if (Instance == this) Instance = null;
    }
}
