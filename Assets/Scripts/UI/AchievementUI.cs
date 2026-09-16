using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 成就面板 — 分类展示、进度条、奖励领取、统计面板
/// </summary>
public class AchievementUI : MonoBehaviour
{
    public static AchievementUI Instance { get; private set; }

    private GameObject panel;
    private Coroutine showRoutine;
    private Text _statsText;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Show()
    {
        if (panel != null) { Destroy(panel); panel = null; }
        BuildUI();
        UIHelper.SetupTopBar("成就", () => { Hide(); UIHelper.EnsureUIManager(); UIManager.Instance.ShowHub(); });
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

        panel = UiPrefabLoader.TryLoad("AchievementPanel", canvas);
        if (panel == null)
        panel = new GameObject("AchievementPanel");
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

        // 顶部统计栏
        var statsBar = new GameObject("StatsBar");
        statsBar.transform.SetParent(panel.transform, false);
        var sbr = statsBar.AddComponent<RectTransform>();
        sbr.anchorMin = new Vector2(0.1f, 0.88f); sbr.anchorMax = new Vector2(0.9f, 0.96f);
        sbr.offsetMin = Vector2.zero; sbr.offsetMax = Vector2.zero;
        var statsBg = statsBar.AddComponent<Image>();
        statsBg.color = new Color(0.08f, 0.06f, 0.14f, 0.9f);

        int unlocked = AchievementManager.Instance?.UnlockedCount ?? 0;
        int total = AchievementManager.Instance?.TotalCount ?? 0;
        float pct = total > 0 ? (float)unlocked / total : 0f;

        var statsTxtObj = new GameObject("StatsText");
        statsTxtObj.transform.SetParent(statsBar.transform, false);
        var stR = statsTxtObj.AddComponent<RectTransform>();
        stR.anchorMin = new Vector2(0.03f, 0); stR.anchorMax = new Vector2(0.97f, 1);
        stR.offsetMin = Vector2.zero; stR.offsetMax = Vector2.zero;
        _statsText = statsTxtObj.AddComponent<Text>();
        _statsText.text = $"成就进度  {unlocked}/{total}  ({pct*100:F0}%)";
        _statsText.alignment = TextAnchor.MiddleLeft;
        _statsText.fontSize = 18; _statsText.color = UIHelper.Accent; _statsText.font = font;
        _statsText.raycastTarget = false;

        // 进度条
        var progressBar = new GameObject("ProgressBar");
        progressBar.transform.SetParent(statsBar.transform, false);
        var pbR = progressBar.AddComponent<RectTransform>();
        pbR.anchorMin = new Vector2(0.5f, 0.1f); pbR.anchorMax = new Vector2(0.95f, 0.4f);
        pbR.offsetMin = Vector2.zero; pbR.offsetMax = Vector2.zero;
        var pbBg = progressBar.AddComponent<Image>();
        pbBg.color = new Color(0.1f, 0.1f, 0.15f, 0.9f);
        var pbFill = new GameObject("Fill");
        pbFill.transform.SetParent(progressBar.transform, false);
        var pfR = pbFill.AddComponent<RectTransform>();
        pfR.anchorMin = Vector2.zero;         pfR.anchorMax = new Vector2(Mathf.Clamp01(pct), 1);
        pfR.offsetMin = Vector2.zero; pfR.offsetMax = Vector2.zero;
        var pfImg = pbFill.AddComponent<Image>();
        pfImg.color = UIHelper.Accent;

        // 统计数据行
        var detailObj = new GameObject("DetailStats");
        detailObj.transform.SetParent(panel.transform, false);
        var dsR = detailObj.AddComponent<RectTransform>();
        dsR.anchorMin = new Vector2(0.1f, 0.80f); dsR.anchorMax = new Vector2(0.9f, 0.87f);
        dsR.offsetMin = Vector2.zero; dsR.offsetMax = Vector2.zero;
        var dsTxt = detailObj.AddComponent<Text>();
        int kills = AchievementManager.Instance?.TotalKills ?? 0;
        int gold = AchievementManager.Instance?.TotalGoldEarned ?? 0;
        dsTxt.text = $"击杀: {kills}  |  金币: {UIStringBuilderPool.FormatNumber(gold)}  |  已领取奖励: {unlocked}";
        dsTxt.alignment = TextAnchor.MiddleCenter;
        dsTxt.fontSize = 14; dsTxt.color = UIHelper.TextSecondary; dsTxt.font = font;
        dsTxt.raycastTarget = false;

        // 滚动列表
        var scrollObj = new GameObject("ScrollArea");
        scrollObj.transform.SetParent(panel.transform, false);
        var sr = scrollObj.AddComponent<RectTransform>();
        sr.anchorMin = new Vector2(0.08f, 0.05f); sr.anchorMax = new Vector2(0.92f, 0.78f);
        sr.offsetMin = Vector2.zero; sr.offsetMax = Vector2.zero;
        var scrollRect = scrollObj.AddComponent<ScrollRect>();
        scrollRect.vertical = true; scrollRect.horizontal = false;
        scrollRect.scrollSensitivity = 20f;

        // Viewport
        var vpObj = new GameObject("Viewport");
        vpObj.transform.SetParent(scrollObj.transform, false);
        var vpR = vpObj.AddComponent<RectTransform>();
        vpR.anchorMin = Vector2.zero; vpR.anchorMax = Vector2.one;
        vpR.offsetMin = Vector2.zero; vpR.offsetMax = Vector2.zero;
        vpObj.AddComponent<RectMask2D>();
        scrollRect.viewport = vpR;

        var contentObj = new GameObject("Content");
        contentObj.transform.SetParent(vpObj.transform, false);
        var cr = contentObj.AddComponent<RectTransform>();
        cr.anchorMin = new Vector2(0f, 1f); cr.anchorMax = new Vector2(1f, 1f);
        cr.pivot = new Vector2(0.5f, 1);
        var vlg = contentObj.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8; vlg.padding = new RectOffset(8, 8, 8, 8);
        vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
        vlg.childControlHeight = true; vlg.childForceExpandHeight = false;
        var csf = contentObj.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.content = cr;

        var achievements = AchievementManager.Instance?.GetAllAchievements();
        if (achievements == null) return;

        // 分类标签
        string[] categories = { "战斗", "关卡", "收集", "经济", "等级" };
        int[] categoryRanges = { 0, 5, 9, 14, 16 };

        for (int cat = 0; cat < categories.Length; cat++)
        {
            int start = categoryRanges[cat];
            int end = cat + 1 < categories.Length ? categoryRanges[cat + 1] : achievements.Length;
            if (start >= achievements.Length) break;

            // 分类标题
            var catObj = new GameObject($"Cat_{cat}");
            catObj.transform.SetParent(contentObj.transform, false);
            var catR = catObj.AddComponent<RectTransform>();
            var catLe = catObj.AddComponent<LayoutElement>();
            catLe.preferredHeight = 28;
            var catTxt = catObj.AddComponent<Text>();
            int catUnlocked = 0;
            int catTotal = 0;
            for (int i = start; i < end && i < achievements.Length; i++)
            {
                catTotal++;
                if (achievements[i].Unlocked) catUnlocked++;
            }
            catTxt.text = $"<color=#FFCC44>{categories[cat]}</color>  <size=13>{catUnlocked}/{catTotal}</size>";
            catTxt.alignment = TextAnchor.MiddleLeft;
            catTxt.fontSize = 16; catTxt.color = UIHelper.TextSecondary; catTxt.font = font;
            catTxt.supportRichText = true; catTxt.raycastTarget = false;

            // 分类下成就
            for (int i = start; i < end && i < achievements.Length; i++)
            {
                var ach = achievements[i];
                int idx = i;
                CreateAchievementCard(contentObj.transform, ach, font, idx);
            }
        }

        panel.SetActive(false);
    }

    private void CreateAchievementCard(Transform parent, AchievementData ach, Font font, int idx)
    {
        var card = UIHelper.MakeGlowCard(parent, $"Ach_{idx}",
            new Vector2(0, 1), new Vector2(1, 1),
            Vector2.zero, Vector2.zero,
            ach.Unlocked ? new Color(0.06f, 0.10f, 0.06f, 0.92f) : new Color(0.05f, 0.04f, 0.08f, 0.92f),
            UIHelper.GlowBottom,
            ach.Unlocked ? new Color(0.2f, 0.6f, 0.2f) : UIHelper.BorderSubtle);

        var le = card.AddComponent<LayoutElement>();
        le.preferredHeight = 72;

        // 左侧状态图标
        var iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(card.transform, false);
        var ir = iconObj.AddComponent<RectTransform>();
        ir.anchorMin = new Vector2(0, 0.5f); ir.anchorMax = new Vector2(0, 0.5f);
        ir.pivot = new Vector2(0.5f, 0.5f);
        ir.anchoredPosition = new Vector2(30, 0);
        ir.sizeDelta = new Vector2(40, 40);
        var iconBg = iconObj.AddComponent<Image>();
        iconBg.color = ach.Unlocked ? new Color(0.1f, 0.2f, 0.1f, 0.8f) : new Color(0.1f, 0.08f, 0.12f, 0.5f);
        var iconTxtObj = new GameObject("Txt");
        iconTxtObj.transform.SetParent(iconObj.transform, false);
        var itR = iconTxtObj.AddComponent<RectTransform>();
        itR.anchorMin = Vector2.zero; itR.anchorMax = Vector2.one;
        itR.offsetMin = Vector2.zero; itR.offsetMax = Vector2.zero;
        var iconTxt = iconTxtObj.AddComponent<Text>();
        iconTxt.text = ach.Unlocked ? "已解锁" : "未解锁";
        iconTxt.alignment = TextAnchor.MiddleCenter;
        iconTxt.fontSize = 11;
        iconTxt.color = ach.Unlocked ? new Color(0.3f, 0.9f, 0.3f) : UIHelper.TextDim;
        iconTxt.font = font;
        iconTxt.raycastTarget = false;

        // 名称+描述
        var infoObj = new GameObject("Info");
        infoObj.transform.SetParent(card.transform, false);
        var infoR = infoObj.AddComponent<RectTransform>();
        infoR.anchorMin = new Vector2(0.12f, 0); infoR.anchorMax = new Vector2(0.72f, 1);
        infoR.offsetMin = new Vector2(4, 4); infoR.offsetMax = new Vector2(-4, -4);
        var infoTxt = infoObj.AddComponent<Text>();
        infoTxt.text = $"<size=16>{ach.Name}</size>\n<size=13>{ach.Description}</size>";
        infoTxt.alignment = TextAnchor.MiddleLeft;
        infoTxt.fontSize = 16;
        infoTxt.color = ach.Unlocked ? UIHelper.TextPrimary : UIHelper.TextDim;
        infoTxt.font = font;
        infoTxt.supportRichText = true;
        infoTxt.raycastTarget = false;

        // 奖励显示
        var rewardObj = new GameObject("Reward");
        rewardObj.transform.SetParent(card.transform, false);
        var rr = rewardObj.AddComponent<RectTransform>();
        rr.anchorMin = new Vector2(0.75f, 0.1f); rr.anchorMax = new Vector2(0.98f, 0.9f);
        rr.offsetMin = Vector2.zero; rr.offsetMax = Vector2.zero;
        var rewardBg = rewardObj.AddComponent<Image>();
        rewardBg.color = ach.Unlocked ? new Color(0.15f, 0.12f, 0.04f, 0.8f) : new Color(0.08f, 0.06f, 0.12f, 0.5f);
        var rewardTxtObj = new GameObject("Txt");
        rewardTxtObj.transform.SetParent(rewardObj.transform, false);
        var rtR = rewardTxtObj.AddComponent<RectTransform>();
        rtR.anchorMin = Vector2.zero; rtR.anchorMax = Vector2.one;
        rtR.offsetMin = Vector2.zero; rtR.offsetMax = Vector2.zero;
        var rewardTxt = rewardTxtObj.AddComponent<Text>();
        rewardTxt.text = ach.Unlocked ? $"金币\n{UIStringBuilderPool.FormatNumber(ach.RewardGold)}" : $"<size=13>奖励\n{UIStringBuilderPool.FormatNumber(ach.RewardGold)}</size>";
        rewardTxt.alignment = TextAnchor.MiddleCenter;
        rewardTxt.fontSize = 15;
        rewardTxt.color = ach.Unlocked ? UIHelper.Accent : UIHelper.TextDim;
        rewardTxt.font = font;
        rewardTxt.supportRichText = true;
        rewardTxt.raycastTarget = false;
    }

    private void OnDestroy()
    {
        if (panel != null) Destroy(panel);
        if (Instance == this) Instance = null;
    }
}
