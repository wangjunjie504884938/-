using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 赛季通行证面板 — 双轨道奖励列表 + 进度条 + 一键领取
/// </summary>
public class SeasonPassUI : MonoBehaviour
{
    public static SeasonPassUI Instance { get; private set; }

    private GameObject panel;
    private Coroutine showRoutine;
    private Text _levelText;
    private Text _xpText;
    private Slider _xpBar;
    private Text _premiumStatus;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Show()
    {
        SeasonPass.CheckNewSeason();
        if (panel != null) { Destroy(panel); panel = null; }
        BuildUI();
        UIHelper.SetupTopBar("通行证", () => { Hide(); UIHelper.EnsureUIManager(); UIManager.Instance.ShowHub(); });
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

        panel = UiPrefabLoader.TryLoad("SeasonPassPanel", canvas);
        if (panel == null)
        panel = new GameObject("SeasonPassPanel");
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

        // Level + XP bar
        var levelObj = new GameObject("LevelInfo");
        levelObj.transform.SetParent(panel.transform, false);
        var lr = levelObj.AddComponent<RectTransform>();
        lr.anchorMin = new Vector2(0.5f, 1); lr.anchorMax = new Vector2(0.5f, 1);
        lr.pivot = new Vector2(0.5f, 1);
        lr.offsetMin = new Vector2(-200, -130); lr.offsetMax = new Vector2(200, -100);
        _levelText = levelObj.AddComponent<Text>();
        _levelText.alignment = TextAnchor.MiddleCenter;
        _levelText.fontSize = 22; _levelText.color = UIHelper.Accent; _levelText.font = font;
        _levelText.raycastTarget = false;
        _levelText.text = $"Lv.{SeasonPass.Level}/{SeasonPass.MaxLevel}";

        var xpBarObj = new GameObject("XPBar");
        xpBarObj.transform.SetParent(panel.transform, false);
        var xpr = xpBarObj.AddComponent<RectTransform>();
        xpr.anchorMin = new Vector2(0.1f, 1); xpr.anchorMax = new Vector2(0.9f, 1);
        xpr.pivot = new Vector2(0.5f, 1);
        xpr.offsetMin = new Vector2(0, -180); xpr.offsetMax = new Vector2(0, -145);
        var xpBg = xpBarObj.AddComponent<Image>();
        xpBg.color = new Color(0.10f, 0.08f, 0.14f, 0.9f);
        _xpBar = xpBarObj.AddComponent<Slider>();
        _xpBar.targetGraphic = xpBg;
        var fillArea = new GameObject("FillArea");
        fillArea.transform.SetParent(xpBarObj.transform, false);
        var far = fillArea.AddComponent<RectTransform>();
        far.anchorMin = Vector2.zero; far.anchorMax = Vector2.one; far.offsetMin = Vector2.zero; far.offsetMax = Vector2.zero;
        var fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(fillArea.transform, false);
        var fr = fillObj.AddComponent<RectTransform>();
        fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = Vector2.zero; fr.offsetMax = Vector2.zero;
        fillObj.AddComponent<Image>().color = UIHelper.Accent;
        _xpBar.fillRect = fr;
        _xpBar.minValue = 0; _xpBar.maxValue = 100;
        _xpBar.value = SeasonPass.XP;
        _xpBar.interactable = false;

        // XP text on a separate child object (can't share with Image/Slider)
        var xpTextObj = new GameObject("XPLabel");
        xpTextObj.transform.SetParent(xpBarObj.transform, false);
        var xptR = xpTextObj.AddComponent<RectTransform>();
        xptR.anchorMin = Vector2.zero; xptR.anchorMax = Vector2.one;
        xptR.offsetMin = Vector2.zero; xptR.offsetMax = Vector2.zero;
        _xpText = xpTextObj.AddComponent<Text>();
        _xpText.alignment = TextAnchor.MiddleCenter;
        _xpText.fontSize = 14; _xpText.color = UIHelper.TextPrimary; _xpText.font = font;
        _xpText.raycastTarget = false;
        _xpText.text = $"{SeasonPass.XP}/100 XP";

        // Premium status + purchase button
        var premObj = new GameObject("PremiumStatus");
        premObj.transform.SetParent(panel.transform, false);
        var premR = premObj.AddComponent<RectTransform>();
        premR.anchorMin = new Vector2(0.5f, 1); premR.anchorMax = new Vector2(0.5f, 1);
        premR.pivot = new Vector2(0.5f, 1);
        premR.offsetMin = new Vector2(-150, -230); premR.offsetMax = new Vector2(150, -195);
        _premiumStatus = premObj.AddComponent<Text>();
        _premiumStatus.alignment = TextAnchor.MiddleCenter;
        _premiumStatus.fontSize = 18; _premiumStatus.font = font;
        _premiumStatus.supportRichText = true;
        UpdatePremiumDisplay();

        // Reward list (scrollable) — 固定位置和尺寸
        var scrollObj = new GameObject("ScrollArea");
        scrollObj.transform.SetParent(panel.transform, false);
        var sr = scrollObj.AddComponent<RectTransform>();
        sr.anchorMin = new Vector2(0.5f, 0.5f); sr.anchorMax = new Vector2(0.5f, 0.5f);
        sr.pivot = new Vector2(0.5f, 0.5f);
        sr.anchoredPosition = new Vector2(0, -100);
        sr.sizeDelta = new Vector2(1580, 800);
        // 添加背景便于查看
        var scrollBg = scrollObj.AddComponent<Image>();
        scrollBg.color = new Color(0.04f, 0.03f, 0.06f, 0.5f);
        var scrollRect = scrollObj.AddComponent<ScrollRect>();
        scrollRect.vertical = true; scrollRect.horizontal = false;
        scrollRect.scrollSensitivity = 20f;

        // Viewport — RectMask2D 裁剪
        var vpObj = new GameObject("Viewport", typeof(RectTransform));
        vpObj.transform.SetParent(scrollObj.transform, false);
        var vpR = vpObj.GetComponent<RectTransform>();
        vpR.anchorMin = Vector2.zero; vpR.anchorMax = Vector2.one;
        vpR.offsetMin = Vector2.zero; vpR.offsetMax = Vector2.zero;
        vpObj.AddComponent<RectMask2D>();
        scrollRect.viewport = vpR;

        var contentObj = new GameObject("Content");
        contentObj.transform.SetParent(vpObj.transform, false);
        var cr = contentObj.AddComponent<RectTransform>();
        cr.anchorMin = new Vector2(0, 1); cr.anchorMax = new Vector2(1, 1);
        cr.pivot = new Vector2(0.5f, 1);
        cr.offsetMin = Vector2.zero; cr.offsetMax = Vector2.zero;
        var vlg = contentObj.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 4; vlg.padding = new RectOffset(4, 4, 4, 4);
        vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
        var csf = contentObj.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.content = cr;

        // Build reward rows (show levels 1-30, but only show up to Level+5 for preview)
        int showCount = Mathf.Min(SeasonPass.Level + 5, SeasonPass.MaxLevel);
        for (int i = 1; i <= showCount; i++)
            BuildRewardRow(contentObj.transform, font, i);

        // Claim all button
        var claimObj = new GameObject("ClaimAllBtn");
        claimObj.transform.SetParent(panel.transform, false);
        var clr = claimObj.AddComponent<RectTransform>();
        clr.anchorMin = new Vector2(0.5f, 0.5f); clr.anchorMax = new Vector2(0.5f, 0.5f);
        clr.pivot = new Vector2(0.5f, 0.5f);
        clr.anchoredPosition = new Vector2(0, -15.3527f);
        clr.sizeDelta = new Vector2(0, 0);
        clr.offsetMin = new Vector2(13, 14.51996f);
        clr.offsetMax = new Vector2(-13, -15.3527f);
        var claimImg = claimObj.AddComponent<Image>();
        claimImg.color = UIHelper.BtnConfirm;
        var claimBtn = claimObj.AddComponent<Button>();
        var claimLbl = new GameObject("Lbl");
        claimLbl.transform.SetParent(claimObj.transform, false);
        var clblR = claimLbl.AddComponent<RectTransform>();
        clblR.anchorMin = Vector2.zero; clblR.anchorMax = Vector2.one;
        clblR.offsetMin = Vector2.zero; clblR.offsetMax = Vector2.zero;
        var claimTxt = claimLbl.AddComponent<Text>();
        claimTxt.text = "一键领取全部"; claimTxt.alignment = TextAnchor.MiddleCenter;
        claimTxt.fontSize = 20; claimTxt.color = UIHelper.TextPrimary; claimTxt.font = font;
        claimTxt.raycastTarget = false;
        claimBtn.onClick.AddListener(() =>
        {
            int total = SeasonPass.ClaimAll();
            if (total > 0)
            {
                if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("通行证奖励", $"领取{total}金币");
                Show(); // Refresh
            }
        });

        panel.SetActive(false);
    }

    private void BuildRewardRow(Transform parent, Font font, int level)
    {
        bool unlocked = level <= SeasonPass.Level;
        bool freeClaimed = SeasonPass.IsFreeClaimed(level);
        bool premClaimed = SeasonPass.IsPremiumClaimed(level);
        int freeReward = SeasonPass.GetFreeReward(level);
        int premReward = SeasonPass.GetPremiumReward(level);

        var row = UIHelper.MakeGlowCard(parent, $"Row_{level}",
            new Vector2(0, 1), new Vector2(1, 1),
            Vector2.zero, Vector2.zero,
            unlocked ? new Color(0.06f, 0.08f, 0.06f, 0.92f) : new Color(0.05f, 0.04f, 0.08f, 0.92f),
            UIHelper.GlowBottom, unlocked ? UIHelper.Accent : UIHelper.BorderSubtle);

        var le = row.AddComponent<LayoutElement>();
        le.preferredHeight = 50;

        // Level number
        var lvlObj = new GameObject("Lvl");
        lvlObj.transform.SetParent(row.transform, false);
        var lvlR = lvlObj.AddComponent<RectTransform>();
        lvlR.anchorMin = new Vector2(0, 0); lvlR.anchorMax = new Vector2(0.1f, 1);
        lvlR.offsetMin = Vector2.zero; lvlR.offsetMax = Vector2.zero;
        var lvlTxt = lvlObj.AddComponent<Text>();
        lvlTxt.text = $"{level}"; lvlTxt.alignment = TextAnchor.MiddleCenter;
        lvlTxt.fontSize = 18; lvlTxt.color = unlocked ? UIHelper.Accent : UIHelper.TextDim;
        lvlTxt.font = font; lvlTxt.raycastTarget = false;

        // Free track
        var freeObj = new GameObject("Free");
        freeObj.transform.SetParent(row.transform, false);
        var freeR = freeObj.AddComponent<RectTransform>();
        freeR.anchorMin = new Vector2(0.12f, 0); freeR.anchorMax = new Vector2(0.48f, 1);
        freeR.offsetMin = Vector2.zero; freeR.offsetMax = Vector2.zero;
        var freeTxt = freeObj.AddComponent<Text>();
        freeTxt.alignment = TextAnchor.MiddleLeft;
        freeTxt.fontSize = 16; freeTxt.font = font; freeTxt.supportRichText = true;
        freeTxt.raycastTarget = false;
        freeTxt.color = freeClaimed ? new Color(0.4f, 0.6f, 0.4f) : UIHelper.TextPrimary;
        freeTxt.text = freeClaimed ? $"✓ {freeReward}" : $"{freeReward}";

        if (unlocked && !freeClaimed)
        {
            var btn = freeObj.AddComponent<Button>();
            int lvl = level;
            btn.onClick.AddListener(() => { SeasonPass.ClaimFree(lvl); Show(); });
        }

        // Premium track
        var premObj = new GameObject("Prem");
        premObj.transform.SetParent(row.transform, false);
        var premR2 = premObj.AddComponent<RectTransform>();
        premR2.anchorMin = new Vector2(0.52f, 0); premR2.anchorMax = new Vector2(0.98f, 1);
        premR2.offsetMin = Vector2.zero; premR2.offsetMax = Vector2.zero;
        var premTxt = premObj.AddComponent<Text>();
        premTxt.alignment = TextAnchor.MiddleLeft;
        premTxt.fontSize = 16; premTxt.font = font; premTxt.supportRichText = true;
        premTxt.raycastTarget = false;

        if (!SeasonPass.IsPremium)
        {
            premTxt.color = UIHelper.TextDim;
            premTxt.text = $"{premReward} (高级)";
        }
        else if (premClaimed)
        {
            premTxt.color = new Color(0.4f, 0.6f, 0.4f);
            premTxt.text = $"✓ {premReward}";
        }
        else if (unlocked)
        {
            premTxt.color = new Color(0.8f, 0.6f, 0.2f);
            premTxt.text = $"{premReward}";
            var btn = premObj.AddComponent<Button>();
            int lvl = level;
            btn.onClick.AddListener(() => { SeasonPass.ClaimPremium(lvl); Show(); });
        }
        else
        {
            premTxt.color = UIHelper.TextDim;
            premTxt.text = $"{premReward}";
        }
    }

    private void UpdatePremiumDisplay()
    {
        if (_premiumStatus == null) return;
        if (SeasonPass.IsPremium)
        {
            _premiumStatus.text = "<color=#FFD700>高级通行证已激活</color>";
            _premiumStatus.color = new Color(1f, 0.85f, 0.3f);
        }
        else
        {
            _premiumStatus.text = "<color=#888>普通通行证\n购买高级: 5000</color>";
            _premiumStatus.color = UIHelper.TextSecondary;
            // Make it clickable to purchase
            var btn = _premiumStatus.gameObject.GetComponent<Button>();
            if (btn == null)
            {
                btn = _premiumStatus.gameObject.AddComponent<Button>();
                btn.onClick.AddListener(() =>
                {
                    if (SeasonPass.PurchasePremium(5000))
                    {
                        if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("购买成功", "高级通行证已激活!");
                        Show();
                    }
                    else
                    {
                        if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("金币不足", "需要5000金币");
                    }
                });
            }
        }
    }

    private void OnDestroy()
    {
        if (panel != null) Destroy(panel);
        if (Instance == this) Instance = null;
    }
}
