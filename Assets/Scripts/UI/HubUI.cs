using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 大本营 — 左列表+右信息面板布局（参照原神Paimon Menu）
/// 左侧垂直菜单列表 + 右侧玩家信息面板
/// </summary>
public class HubUI : MonoBehaviour
{
    public static HubUI Instance { get; private set; }

    private GameObject panel;
    private Coroutine _friendRefreshRoutine;
    private const float FriendRefreshInterval = 15f;
    private Text classText;
    private Text hpText;
    private Text atkText;
    private Text defText;
    private Text weaponText;
    private Text armorText;
    private Text accessoryText;
    private Text backpackText;
    private GameObject _friendListContainer;
    private Text _friendCountText;
    private Coroutine showRoutine;
    private Coroutine hideRoutine;
    private bool _lastEndlessUnlocked;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Show()
    {
        // Rebuild if endless unlock state may have changed
        bool endlessUnlocked = RuntimePlayerData.Instance?.IsEndlessUnlocked ?? false;
        if (panel != null && _lastEndlessUnlocked != endlessUnlocked)
        {
            Destroy(panel);
            panel = null;
        }
        _lastEndlessUnlocked = endlessUnlocked;

        // 关闭残留的宠物面板
        if (_petPanel != null) { Destroy(_petPanel); _petPanel = null; }

        if (panel == null) BuildUI();
        UIHelper.SetupTopBar("大本营", () => { Hide(); GameManager.Instance.ShowCharacterSlots(); });
        UpdateInfo();
        panel.SetActive(true);
        if (showRoutine != null) StopCoroutine(showRoutine);
        showRoutine = StartCoroutine(UIHelper.FadeIn(panel, 0.2f));

        // Check for offline rewards
        var reward = OfflineRewards.CalculateReward();
        if (reward.eligible && reward.goldReward > 0)
            ShowOfflineRewardPopup(reward);

        // 检查公会邀请横幅
        if (GuildInviteBanner.Instance == null)
            gameObject.AddComponent<GuildInviteBanner>();
        GuildInviteBanner.Instance?.CheckInvites();

        // 通知中心
        if (NotificationCenter.Instance == null)
            gameObject.AddComponent<NotificationCenter>();
        NotificationCenter.Instance?.RefreshNow();

        // 刷新好友列表 + 启动定时刷新
        StartCoroutine(RefreshFriendListHub());
        if (_friendRefreshRoutine != null) StopCoroutine(_friendRefreshRoutine);
        _friendRefreshRoutine = StartCoroutine(FriendRefreshLoop());
    }

    private System.Collections.IEnumerator FriendRefreshLoop()
    {
        while (panel != null && panel.activeSelf)
        {
            yield return new UnityEngine.WaitForSeconds(FriendRefreshInterval);
            if (panel != null && panel.activeSelf)
                yield return RefreshFriendListHub();
        }
    }

    public void Hide()
    {
        if (_friendRefreshRoutine != null) { StopCoroutine(_friendRefreshRoutine); _friendRefreshRoutine = null; }
        GuildInviteBanner.Instance?.HideBanner();
        if (_petPanel != null) { Destroy(_petPanel); _petPanel = null; }
        if (panel != null)
        {
            var cg = panel.GetComponent<CanvasGroup>();
            if (cg != null) { cg.alpha = 0f; cg.blocksRaycasts = false; }
            panel.SetActive(false);
        }
    }

    private void BuildUI()
    {
        Font font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();

        panel = new GameObject("HubPanel");
        panel.transform.SetParent(canvas.transform, false);
        RectTransform pr = panel.AddComponent<RectTransform>();
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one;
        pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;
        panel.AddComponent<CanvasGroup>();

        // === 暗色背景 ===
        var bg = new GameObject("BG");
        bg.transform.SetParent(panel.transform, false);
        var bgr = bg.AddComponent<RectTransform>();
        bgr.anchorMin = Vector2.zero; bgr.anchorMax = Vector2.one;
        bgr.offsetMin = Vector2.zero; bgr.offsetMax = Vector2.zero;
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = UIHelper.BgDark;
        bgImg.raycastTarget = true;

        // === 导航顶栏 (TopNavBar 单例) ===

        // === 左侧菜单列表 (40%宽度) ===
        bool endlessUnlocked = RuntimePlayerData.Instance?.IsEndlessUnlocked ?? false;
        var leftPanel = new GameObject("LeftPanel");
        leftPanel.transform.SetParent(panel.transform, false);
        var lpr = leftPanel.AddComponent<RectTransform>();
        lpr.anchorMin = new Vector2(0, 0); lpr.anchorMax = new Vector2(0.42f, 1);
        lpr.offsetMin = new Vector2(8, 8);
        lpr.offsetMax = new Vector2(-4, -64);

        // 4个标签页，每页4项 (宠物移到右侧快捷按钮)
        string[][] tabLabels = {
            new[] { "进入副本", "无尽模式", "竞技场", "排行榜" },
            new[] { "技能升级", "符文装备", "背 包", "Boss图鉴" },
            new[] { "商 场", "碎片商店", "抽 卡", "熔 炉" },
            new[] { "每日任务", "每日挑战", "公 会", "好 友", "邮 件" }
        };
        string[][] tabSubs = {
            new[] { "开始你的冒险", "通关后解锁", "挑战其他玩家", "无尽模式排行" },
            new[] { "提升技能等级", "镶嵌技能符文", "查看你的物品", "查看Boss信息" },
            new[] { "装备·道具", "碎片兑换资源", "抽取稀有装备", "装备熔炼强化" },
            new[] { "每日签到奖励", "特殊修饰符", "创建·加入公会", "好友列表·组队", "查看系统邮件" }
        };
        string[][] tabIcons = {
            new[] { "剑", "∞", "斗", "杯" },
            new[] { "技", "符", "包", "书" },
            new[] { "店", "碎", "抽", "炉" },
            new[] { "任", "挑", "会", "友", "信" }
        };
        string[] tabNames = { "冒险", "养成", "商店", "社交" };
        Color[][] tabIconColors = {
            new[] { new Color(0.3f, 0.7f, 0.3f), endlessUnlocked ? new Color(0.8f, 0.2f, 0.2f) : new Color(0.15f, 0.15f, 0.15f), new Color(0.8f, 0.3f, 0.5f), new Color(0.2f, 0.5f, 0.8f) },
            new[] { new Color(0.3f, 0.5f, 0.9f), new Color(0.6f, 0.3f, 0.8f), new Color(0.7f, 0.5f, 0.8f), new Color(0.7f, 0.5f, 0.3f) },
            new[] { new Color(0.85f, 0.7f, 0.2f), new Color(0.4f, 0.8f, 1f), new Color(0.8f, 0.4f, 0.6f), new Color(0.9f, 0.5f, 0.3f) },
            new[] { new Color(0.3f, 0.8f, 0.3f), new Color(0.9f, 0.5f, 0.2f), new Color(0.6f, 0.5f, 0.9f), new Color(0.3f, 0.8f, 0.6f), new Color(0.8f, 0.65f, 0.2f) }
        };
        System.Action[][] tabActions = {
            new System.Action[] {
                () => { EnsureUIManager(); UIManager.Instance.ShowStageSelect(); },
                () => { if (endlessUnlocked) { ShowEndlessModePopup(); } },
                () => { EnsureUIManager(); UIManager.Instance.ShowAsyncPvp(); },
                () => { EnsureUIManager(); UIManager.Instance.ShowLeaderboard(); }
            },
            new System.Action[] {
                () => { EnsureUIManager(); UIManager.Instance.ShowSkillTree(); },
                () => { EnsureUIManager(); UIManager.Instance.ShowRuneEquip(0); },
                () => { EnsureUIManager(); UIManager.Instance.ShowEquip(); },
                () => { EnsureUIManager(); UIManager.Instance.ShowBossCodex(); }
            },
            new System.Action[] {
                () => { EnsureUIManager(); UIManager.Instance.ShowShop(); },
                () => { EnsureUIManager(); UIManager.Instance.ShowFragmentShop(); },
                () => { EnsureUIManager(); UIManager.Instance.ShowGacha(); },
                () => { EnsureUIManager(); UIManager.Instance.ShowForge(); }
            },
            new System.Action[] {
                () => { EnsureUIManager(); UIManager.Instance.ShowDailyTask(); },
                () => { EnsureUIManager(); UIManager.Instance.ShowDailyChallenge(); },
                () => { EnsureUIManager(); UIManager.Instance.ShowGuild(); },
                () => { EnsureUIManager(); UIManager.Instance.ShowFriend(); },
                () => { EnsureUIManager(); UIManager.Instance.ShowMail(); }
            }
        };

        // 菜单内容容器 (在标签栏之前定义, 供 RefreshTab 引用)
        var menuContent = new GameObject("MenuContent");
        menuContent.transform.SetParent(leftPanel.transform, false);
        var mcR = menuContent.AddComponent<RectTransform>();
        mcR.anchorMin = new Vector2(0, 0); mcR.anchorMax = new Vector2(1, 1);
        mcR.offsetMin = Vector2.zero; mcR.offsetMax = new Vector2(0, -48);

        // 标签栏
        var tabRow = new GameObject("TabRow");
        tabRow.transform.SetParent(leftPanel.transform, false);
        var tabRowRt = tabRow.AddComponent<RectTransform>();
        tabRowRt.anchorMin = new Vector2(0, 1); tabRowRt.anchorMax = new Vector2(1, 1);
        tabRowRt.pivot = new Vector2(0.5f, 1);
        tabRowRt.anchoredPosition = Vector2.zero;
        tabRowRt.sizeDelta = new Vector2(0, 44);

        int currentTab = 0;
        GameObject[] tabBtns = new GameObject[4];
        Image[] tabBtnImgs = new Image[4];
        Text[] tabBtnLabels = new Text[4];
        for (int t = 0; t < 4; t++)
        {
            int tabIdx = t;
            var tabObj = new GameObject($"Tab_{t}");
            tabObj.transform.SetParent(tabRow.transform, false);
            var tbR = tabObj.AddComponent<RectTransform>();
            float x1 = t * 0.25f, x2 = (t + 1) * 0.25f;
            tbR.anchorMin = new Vector2(x1, 0); tbR.anchorMax = new Vector2(x2, 1);
            tbR.offsetMin = new Vector2(2, 0); tbR.offsetMax = new Vector2(-2, 0);
            var tbImg = tabObj.AddComponent<Image>();
            tbImg.color = t == 0 ? new Color(0.12f, 0.08f, 0.18f, 0.95f) : new Color(0.04f, 0.03f, 0.06f, 0.6f);
            tabBtnImgs[t] = tbImg;
            var tbBtn = tabObj.AddComponent<Button>();
            tbBtn.transition = Selectable.Transition.None;
            UIHelper.SetupButtonFeedback(tbBtn, tbImg.color);
            tbBtn.onClick.AddListener(() => { currentTab = tabIdx; RefreshTab(); });
            var tbLbl = new GameObject("Lbl");
            tbLbl.transform.SetParent(tabObj.transform, false);
            var tbLr = tbLbl.AddComponent<RectTransform>();
            tbLr.anchorMin = Vector2.zero; tbLr.anchorMax = Vector2.one;
            tbLr.offsetMin = Vector2.zero; tbLr.offsetMax = Vector2.zero;
            var tbTxt = tbLbl.AddComponent<Text>();
            tbTxt.text = tabNames[t]; tbTxt.alignment = TextAnchor.MiddleCenter;
            tbTxt.fontSize = 16; tbTxt.color = t == 0 ? UIHelper.TextPrimary : UIHelper.TextDim;
            tbTxt.font = font; tbTxt.raycastTarget = false;
            tabBtnLabels[t] = tbTxt;
            tabBtns[t] = tabObj;
        }

        // 初始显示第0页
        RefreshTab();

        void RefreshTab()
        {
            // 更新标签颜色
            for (int t = 0; t < 4; t++)
            {
                bool active = t == currentTab;
                tabBtnImgs[t].color = active ? new Color(0.12f, 0.08f, 0.18f, 0.95f) : new Color(0.04f, 0.03f, 0.06f, 0.6f);
                tabBtnLabels[t].color = active ? UIHelper.TextPrimary : UIHelper.TextDim;
            }

            // 清空旧菜单项
            for (int i = menuContent.transform.childCount - 1; i >= 0; i--)
                Destroy(menuContent.transform.GetChild(i).gameObject);

            // 创建当前标签的菜单项
            var labels2 = tabLabels[currentTab];
            var subs2 = tabSubs[currentTab];
            var icons2 = tabIcons[currentTab];
            var colors2 = tabIconColors[currentTab];
            var acts2 = tabActions[currentTab];
            float itemH = 60f, gap = 6f;
            float startY = -itemH / 2f - 4f;

            for (int i = 0; i < labels2.Length; i++)
            {
                int idx = i;
                float y = startY - i * (itemH + gap);
                var item = UIHelper.MakeGlowCard(menuContent.transform, $"MenuItem_{i}",
                    new Vector2(0, 1), new Vector2(1, 1),
                    new Vector2(4, y - itemH / 2f), new Vector2(-4, y + itemH / 2f),
                    UIHelper.GlowTop, UIHelper.GlowBottom, UIHelper.Accent);

                var btn = item.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                UIHelper.SetupButtonFeedback(btn, Color.white);
                btn.onClick.AddListener(() => acts2[idx]());

                // 无尽模式未解锁时禁用
                if (currentTab == 0 && idx == 1 && !endlessUnlocked)
                    btn.interactable = false;

                item.AddComponent<CardHoverEffect>();

                // 图标
                var iconObj = new GameObject("Icon");
                iconObj.transform.SetParent(item.transform, false);
                var ir = iconObj.AddComponent<RectTransform>();
                ir.anchorMin = new Vector2(0, 0.5f); ir.anchorMax = new Vector2(0, 0.5f);
                ir.pivot = new Vector2(0.5f, 0.5f);
                ir.anchoredPosition = new Vector2(35, 0);
                ir.sizeDelta = new Vector2(40, 40);
                var iconTxt = iconObj.AddComponent<Text>();
                iconTxt.text = icons2[i]; iconTxt.alignment = TextAnchor.MiddleCenter;
                iconTxt.fontSize = 26; iconTxt.color = colors2[i]; iconTxt.font = font;
                iconTxt.raycastTarget = false;

                // 标签
                var lblObj = new GameObject("Lbl");
                lblObj.transform.SetParent(item.transform, false);
                var lr = lblObj.AddComponent<RectTransform>();
                lr.anchorMin = new Vector2(0.15f, 0.45f); lr.anchorMax = new Vector2(1, 1);
                lr.offsetMin = Vector2.zero; lr.offsetMax = new Vector2(-8, 0);
                var lblTxt = lblObj.AddComponent<Text>();
                lblTxt.text = labels2[i]; lblTxt.alignment = TextAnchor.LowerLeft;
                lblTxt.fontSize = 20; lblTxt.color = UIHelper.TextPrimary; lblTxt.font = font;
                lblTxt.raycastTarget = false;

                // 副标题
                var subObj = new GameObject("Sub");
                subObj.transform.SetParent(item.transform, false);
                var subR = subObj.AddComponent<RectTransform>();
                subR.anchorMin = new Vector2(0.15f, 0); subR.anchorMax = new Vector2(1, 0.45f);
                subR.offsetMin = new Vector2(0, 2); subR.offsetMax = new Vector2(-8, 0);
                var subTxt = subObj.AddComponent<Text>();
                subTxt.text = subs2[i]; subTxt.alignment = TextAnchor.UpperLeft;
                subTxt.fontSize = 13; subTxt.color = UIHelper.TextDim; subTxt.font = font;
                subTxt.raycastTarget = false;
            }
        }

        // === 右侧玩家信息面板 (60%宽度) ===
        var rightPanel = new GameObject("RightPanel", typeof(RectTransform));
        rightPanel.transform.SetParent(panel.transform, false);
        var rpR = rightPanel.GetComponent<RectTransform>();
        rpR.anchorMin = new Vector2(0.44f, 0); rpR.anchorMax = new Vector2(1, 1);
        rpR.offsetMin = new Vector2(4, 8); rpR.offsetMax = new Vector2(-8, -64);
        var rpImg = rightPanel.AddComponent<Image>();
        rpImg.color = UIHelper.CardBg;
        rpImg.raycastTarget = false;
        // 边框
        var rpBorder = new GameObject("Border", typeof(RectTransform));
        rpBorder.transform.SetParent(rightPanel.transform, false);
        var rpbR = rpBorder.GetComponent<RectTransform>();
        rpbR.anchorMin = Vector2.zero; rpbR.anchorMax = Vector2.one;
        rpbR.offsetMin = new Vector2(-1, -1); rpbR.offsetMax = new Vector2(1, 1);
        var rpbImg = rpBorder.AddComponent<Image>();
        rpbImg.color = UIHelper.BorderSubtle;
        rpbImg.raycastTarget = false;
        rpBorder.transform.SetAsFirstSibling();
        // 顶部线
        var rpLine = new GameObject("TopLine", typeof(RectTransform));
        rpLine.transform.SetParent(rightPanel.transform, false);
        var rplR = rpLine.GetComponent<RectTransform>();
        rplR.anchorMin = new Vector2(0.1f, 1); rplR.anchorMax = new Vector2(0.9f, 1);
        rplR.pivot = new Vector2(0.5f, 1);
        rplR.sizeDelta = new Vector2(0, 2);
        var rplImg = rpLine.AddComponent<Image>();
        rplImg.color = UIHelper.Accent;
        rplImg.raycastTarget = false;

        // 副标题 — removed to avoid overlap with quick buttons above

        // 成就快捷入口
        var achBtnObj = new GameObject("AchBtn");
        achBtnObj.transform.SetParent(rightPanel.transform, false);
        var achR = achBtnObj.AddComponent<RectTransform>();
        achR.anchorMin = new Vector2(0.80f, 0.94f); achR.anchorMax = new Vector2(0.97f, 0.99f);
        achR.offsetMin = Vector2.zero; achR.offsetMax = Vector2.zero;
        var achImg = achBtnObj.AddComponent<Image>();
        achImg.color = new Color(0.12f, 0.08f, 0.18f, 0.92f);
        var achBtn = achBtnObj.AddComponent<Button>();
        achBtn.transition = Selectable.Transition.None;
        achBtn.targetGraphic = achImg;
        UIHelper.SetupButtonFeedback(achBtn, achImg.color);
        var achLbl = new GameObject("Lbl");
        achLbl.transform.SetParent(achBtnObj.transform, false);
        var achLr = achLbl.AddComponent<RectTransform>();
        achLr.anchorMin = Vector2.zero; achLr.anchorMax = Vector2.one;
        achLr.offsetMin = Vector2.zero; achLr.offsetMax = Vector2.zero;
        var achTxt = achLbl.AddComponent<Text>();
        achTxt.text = "成就"; achTxt.alignment = TextAnchor.MiddleCenter;
        achTxt.fontSize = 14; achTxt.color = UIHelper.Accent; achTxt.font = font;
        achTxt.raycastTarget = false;
        achBtn.onClick.AddListener(() => { EnsureUIManager(); UIManager.Instance.ShowAchievement(); });

        // 被动树快捷入口
        var ptBtnObj = new GameObject("PassiveBtn");
        ptBtnObj.transform.SetParent(rightPanel.transform, false);
        var ptR = ptBtnObj.AddComponent<RectTransform>();
        ptR.anchorMin = new Vector2(0.61f, 0.94f); ptR.anchorMax = new Vector2(0.78f, 0.99f);
        ptR.offsetMin = Vector2.zero; ptR.offsetMax = Vector2.zero;
        var ptImg = ptBtnObj.AddComponent<Image>();
        ptImg.color = new Color(0.08f, 0.10f, 0.18f, 0.92f);
        var ptBtn = ptBtnObj.AddComponent<Button>();
        ptBtn.transition = Selectable.Transition.None;
        ptBtn.targetGraphic = ptImg;
        UIHelper.SetupButtonFeedback(ptBtn, ptImg.color);
        var ptLbl = new GameObject("Lbl");
        ptLbl.transform.SetParent(ptBtnObj.transform, false);
        var ptLr = ptLbl.AddComponent<RectTransform>();
        ptLr.anchorMin = Vector2.zero; ptLr.anchorMax = Vector2.one;
        ptLr.offsetMin = Vector2.zero; ptLr.offsetMax = Vector2.zero;
        var ptTxt = ptLbl.AddComponent<Text>();
        ptTxt.text = "天赋"; ptTxt.alignment = TextAnchor.MiddleCenter;
        ptTxt.fontSize = 14; ptTxt.color = new Color(0.4f, 0.8f, 0.4f); ptTxt.font = font;
        ptTxt.raycastTarget = false;
        ptBtn.onClick.AddListener(() => { EnsureUIManager(); UIManager.Instance.ShowPassiveTree(); });

        // 熔炉快捷入口
        var forgeBtnObj = new GameObject("ForgeBtn");
        forgeBtnObj.transform.SetParent(rightPanel.transform, false);
        var forgeR = forgeBtnObj.AddComponent<RectTransform>();
        forgeR.anchorMin = new Vector2(0.42f, 0.94f); forgeR.anchorMax = new Vector2(0.59f, 0.99f);
        forgeR.offsetMin = Vector2.zero; forgeR.offsetMax = Vector2.zero;
        var forgeImg = forgeBtnObj.AddComponent<Image>();
        forgeImg.color = new Color(0.12f, 0.06f, 0.10f, 0.92f);
        var forgeBtn = forgeBtnObj.AddComponent<Button>();
        forgeBtn.transition = Selectable.Transition.None;
        forgeBtn.targetGraphic = forgeImg;
        UIHelper.SetupButtonFeedback(forgeBtn, forgeImg.color);
        var forgeLbl = new GameObject("Lbl");
        forgeLbl.transform.SetParent(forgeBtnObj.transform, false);
        var forgeLr = forgeLbl.AddComponent<RectTransform>();
        forgeLr.anchorMin = Vector2.zero; forgeLr.anchorMax = Vector2.one;
        forgeLr.offsetMin = Vector2.zero; forgeLr.offsetMax = Vector2.zero;
        var forgeTxt = forgeLbl.AddComponent<Text>();
        forgeTxt.text = "熔炉"; forgeTxt.alignment = TextAnchor.MiddleCenter;
        forgeTxt.fontSize = 14; forgeTxt.color = new Color(0.9f, 0.5f, 0.3f); forgeTxt.font = font;
        forgeTxt.raycastTarget = false;
        forgeBtn.onClick.AddListener(() => { EnsureUIManager(); UIManager.Instance.ShowForge(); });

        // 宠物快捷入口
        var petBtnObj = new GameObject("PetBtn");
        petBtnObj.transform.SetParent(rightPanel.transform, false);
        var petBtnR = petBtnObj.AddComponent<RectTransform>();
        petBtnR.anchorMin = new Vector2(0.04f, 0.94f); petBtnR.anchorMax = new Vector2(0.21f, 0.99f);
        petBtnR.offsetMin = Vector2.zero; petBtnR.offsetMax = Vector2.zero;
        var petBtnImg = petBtnObj.AddComponent<Image>();
        petBtnImg.color = new Color(0.06f, 0.12f, 0.14f, 0.92f);
        var petBtn = petBtnObj.AddComponent<Button>();
        petBtn.transition = Selectable.Transition.None;
        petBtn.targetGraphic = petBtnImg;
        UIHelper.SetupButtonFeedback(petBtn, petBtnImg.color);
        var petBtnLbl = new GameObject("Lbl");
        petBtnLbl.transform.SetParent(petBtnObj.transform, false);
        var petBtnLr = petBtnLbl.AddComponent<RectTransform>();
        petBtnLr.anchorMin = Vector2.zero; petBtnLr.anchorMax = Vector2.one;
        petBtnLr.offsetMin = Vector2.zero; petBtnLr.offsetMax = Vector2.zero;
        var petBtnTxt = petBtnLbl.AddComponent<Text>();
        petBtnTxt.text = "宠物"; petBtnTxt.alignment = TextAnchor.MiddleCenter;
        petBtnTxt.fontSize = 14; petBtnTxt.color = new Color(0.3f, 0.8f, 0.9f); petBtnTxt.font = font;
        petBtnTxt.raycastTarget = false;
        petBtn.onClick.AddListener(() => { ShowPetPanel(); });

        // 赛季通行证快捷入口
        var seasonBtnObj = new GameObject("SeasonBtn");
        seasonBtnObj.transform.SetParent(rightPanel.transform, false);
        var seasonR = seasonBtnObj.AddComponent<RectTransform>();
        seasonR.anchorMin = new Vector2(0.23f, 0.94f); seasonR.anchorMax = new Vector2(0.40f, 0.99f);
        seasonR.offsetMin = Vector2.zero; seasonR.offsetMax = Vector2.zero;
        var seasonImg = seasonBtnObj.AddComponent<Image>();
        seasonImg.color = new Color(0.10f, 0.08f, 0.18f, 0.92f);
        var seasonBtn = seasonBtnObj.AddComponent<Button>();
        seasonBtn.transition = Selectable.Transition.None;
        seasonBtn.targetGraphic = seasonImg;
        UIHelper.SetupButtonFeedback(seasonBtn, seasonImg.color);
        var seasonLbl = new GameObject("Lbl");
        seasonLbl.transform.SetParent(seasonBtnObj.transform, false);
        var seasonLr = seasonLbl.AddComponent<RectTransform>();
        seasonLr.anchorMin = Vector2.zero; seasonLr.anchorMax = Vector2.one;
        seasonLr.offsetMin = Vector2.zero; seasonLr.offsetMax = Vector2.zero;
        var seasonTxt = seasonLbl.AddComponent<Text>();
        seasonTxt.text = "通行证"; seasonTxt.alignment = TextAnchor.MiddleCenter;
        seasonTxt.fontSize = 14; seasonTxt.color = new Color(0.7f, 0.6f, 0.9f); seasonTxt.font = font;
        seasonTxt.raycastTarget = false;
        seasonBtn.onClick.AddListener(() => { EnsureUIManager(); UIManager.Instance.ShowSeasonPass(); });

        // === 角色信息区 (所有Text的raycastTarget=false, 不阻塞按钮) ===
        float infoY = -80f;
        Font infoFont = GameManager.GetUIFont();

        classText = CreateInfoText(rightPanel.transform, "ClassLevel", infoFont, infoY, 36, UIHelper.Accent, TextAnchor.MiddleCenter);
        infoY -= 36;
        hpText = CreateInfoText(rightPanel.transform, "HP", infoFont, infoY, 28, UIHelper.StatHp, TextAnchor.MiddleLeft);
        infoY -= 30;
        atkText = CreateInfoText(rightPanel.transform, "ATK", infoFont, infoY, 28, UIHelper.StatAtk, TextAnchor.MiddleLeft, 0.04f, 0.48f);
        defText = CreateInfoText(rightPanel.transform, "DEF", infoFont, infoY, 28, UIHelper.StatDef, TextAnchor.MiddleLeft, 0.52f, 0.96f);
        infoY -= 30;
        CreateDivider(rightPanel.transform, infoY); infoY -= 12;
        weaponText = CreateInfoText(rightPanel.transform, "Weapon", infoFont, infoY, 26, UIHelper.TextPrimary, TextAnchor.MiddleLeft);
        weaponText.supportRichText = true;
        infoY -= 28;
        armorText = CreateInfoText(rightPanel.transform, "Armor", infoFont, infoY, 26, UIHelper.TextPrimary, TextAnchor.MiddleLeft);
        armorText.supportRichText = true;
        infoY -= 28;
        accessoryText = CreateInfoText(rightPanel.transform, "Accessory", infoFont, infoY, 26, UIHelper.TextPrimary, TextAnchor.MiddleLeft);
        accessoryText.supportRichText = true;
        infoY -= 28;
        CreateDivider(rightPanel.transform, infoY); infoY -= 12;
        backpackText = CreateInfoText(rightPanel.transform, "BackpackInfo", infoFont, infoY, 26, UIHelper.TextSecondary, TextAnchor.MiddleLeft);
        backpackText.supportRichText = true;

        // === 好友列表区 (底部) ===
        BuildFriendListPanel(rightPanel.transform, font);

        panel.SetActive(false);
    }

    private Text CreateInfoText(Transform parent, string name, Font font, float y, float height, Color color, TextAnchor alignment, float xMin = 0.04f, float xMax = 0.96f)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        var r = obj.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(xMin, 1); r.anchorMax = new Vector2(xMax, 1);
        r.pivot = new Vector2(0.5f, 1);
        // 锚定顶部: offsetMax.y = y (距顶部y像素), offsetMin.y = y - height (元素高度)
        r.offsetMin = new Vector2(0, y - height);
        r.offsetMax = new Vector2(0, y);
        var t = obj.AddComponent<Text>();
        t.font = font; t.fontSize = 16; t.color = color;
        t.alignment = alignment;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        return t;
    }

    private void CreateDivider(Transform parent, float y)
    {
        var obj = new GameObject("Divider", typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        var r = obj.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0.04f, 1); r.anchorMax = new Vector2(0.96f, 1);
        r.pivot = new Vector2(0.5f, 1);
        r.sizeDelta = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(0, y);
        var img = obj.AddComponent<Image>();
        img.color = UIHelper.BorderSubtle;
        img.raycastTarget = false;
    }

    /// <summary>无尽模式入口 — 弹窗选择单人或组队</summary>
    private void ShowEndlessModePopup()
    {
        Font font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();

        var overlay = new GameObject("EndlessModePopup");
        overlay.transform.SetParent(canvas.transform, false);
        var oR = overlay.AddComponent<RectTransform>();
        oR.anchorMin = Vector2.zero; oR.anchorMax = Vector2.one;
        oR.offsetMin = Vector2.zero; oR.offsetMax = Vector2.zero;
        overlay.AddComponent<Image>().color = new Color(0, 0, 0, 0.7f);

        var box = UIHelper.MakeGlowCard(overlay.transform, "Box",
            new Vector2(0.3f, 0.3f), new Vector2(0.7f, 0.7f), Vector2.zero, Vector2.zero,
            new Color(0.06f, 0.03f, 0.09f, 0.97f), UIHelper.GlowBottom, UIHelper.Accent);

        // 标题
        var titleObj = new GameObject("Title");
        titleObj.transform.SetParent(box.transform, false);
        var tR = titleObj.AddComponent<RectTransform>();
        tR.anchorMin = new Vector2(0, 0.80f); tR.anchorMax = new Vector2(1, 0.95f);
        tR.offsetMin = Vector2.zero; tR.offsetMax = Vector2.zero;
        var title = titleObj.AddComponent<Text>();
        title.text = "无尽模式"; title.alignment = TextAnchor.MiddleCenter;
        title.fontSize = 24; title.color = UIHelper.Accent; title.font = font;

        // 说明
        var descObj = new GameObject("Desc");
        descObj.transform.SetParent(box.transform, false);
        var dR = descObj.AddComponent<RectTransform>();
        dR.anchorMin = new Vector2(0.05f, 0.55f); dR.anchorMax = new Vector2(0.95f, 0.72f);
        dR.offsetMin = Vector2.zero; dR.offsetMax = Vector2.zero;
        var desc = descObj.AddComponent<Text>();
        desc.text = "选择模式开始挑战\n单人模式: 独自战斗\n组队模式: 邀请好友组队";
        desc.alignment = TextAnchor.MiddleCenter; desc.fontSize = 15;
        desc.color = UIHelper.TextSecondary; desc.font = font;
        desc.supportRichText = true;

        // 关闭按钮
        UIHelper.MakeButton(box.transform, "CloseBtn", "X", font,
            new Vector2(0.88f, 0.88f), new Vector2(0.98f, 0.98f), Vector2.zero, Vector2.zero,
            UIHelper.BtnDanger, 18, () => Destroy(overlay));

        // 单人模式按钮
        UIHelper.MakeButton(box.transform, "SoloBtn", "单人模式", font,
            new Vector2(0.10f, 0.15f), new Vector2(0.48f, 0.45f), Vector2.zero, Vector2.zero,
            UIHelper.BtnConfirm, 20, () =>
            {
                Destroy(overlay);
                EnsureUIManager();
                HubUI.Instance.Hide();
                GameManager.Instance.IsTeamMode = false;
                GameManager.Instance.StartEndlessMode();
            });

        // 组队模式按钮 — 打开组队大厅
        UIHelper.MakeButton(box.transform, "TeamBtn", "组队模式", font,
            new Vector2(0.52f, 0.15f), new Vector2(0.90f, 0.45f), Vector2.zero, Vector2.zero,
            new Color(0.15f, 0.25f, 0.5f, 0.9f), 20, () =>
            {
                Destroy(overlay);
                if (TeamLobbyManager.Instance == null) gameObject.AddComponent<TeamLobbyManager>();
                TeamLobbyManager.Instance.ShowLobby("无尽模式", () =>
                {
                    EnsureUIManager();
                    HubUI.Instance.Hide();
                    GameManager.Instance.IsTeamMode = true;
                    GameManager.Instance.StartEndlessMode();
                });
            });
    }

    private void EnsureUIManager()
    {
        UIHelper.EnsureUIManager();
    }

    // ========================================================
    //  好友列表面板 (右侧底部)
    // ========================================================
    private void BuildFriendListPanel(Transform parent, Font font)
    {
        var friendArea = new GameObject("FriendListArea", typeof(RectTransform));
        friendArea.transform.SetParent(parent, false);
        var frR = friendArea.GetComponent<RectTransform>();
        frR.anchorMin = new Vector2(0.02f, 0.01f); frR.anchorMax = new Vector2(0.98f, 1f);
        frR.offsetMin = new Vector2(-21, 0); frR.offsetMax = new Vector2(-21, -450);

        // 背景
        var frBg = friendArea.AddComponent<Image>();
        frBg.color = new Color(0.05f, 0.03f, 0.08f, 0.8f);
        frBg.raycastTarget = false;
        // 边框
        var frBorder = new GameObject("Border", typeof(RectTransform));
        frBorder.transform.SetParent(friendArea.transform, false);
        var fbR = frBorder.GetComponent<RectTransform>();
        fbR.anchorMin = Vector2.zero; fbR.anchorMax = Vector2.one;
        fbR.offsetMin = new Vector2(-1, -1); fbR.offsetMax = new Vector2(1, 1);
        var fbImg = frBorder.AddComponent<Image>();
        fbImg.color = UIHelper.BorderSubtle; fbImg.raycastTarget = false;
        frBorder.transform.SetAsFirstSibling();

        // 标题行: 好友列表 + 添加好友按钮
        var titleRow = new GameObject("FriendTitleRow");
        titleRow.transform.SetParent(friendArea.transform, false);
        var trR = titleRow.AddComponent<RectTransform>();
        trR.anchorMin = new Vector2(0, 1); trR.anchorMax = new Vector2(1, 1);
        trR.pivot = new Vector2(0.5f, 1);
        trR.anchoredPosition = new Vector2(0, -4);
        trR.sizeDelta = new Vector2(0, 26);

        var titleTxt = new GameObject("Title");
        titleTxt.transform.SetParent(titleRow.transform, false);
        var ttR = titleTxt.AddComponent<RectTransform>();
        ttR.anchorMin = new Vector2(0.04f, 0); ttR.anchorMax = new Vector2(0.5f, 1);
        ttR.offsetMin = Vector2.zero; ttR.offsetMax = Vector2.zero;
        var tTxt = titleTxt.AddComponent<Text>();
        tTxt.text = "好友列表"; tTxt.alignment = TextAnchor.MiddleLeft;
        tTxt.fontSize = 15; tTxt.color = UIHelper.Accent; tTxt.font = font;
        tTxt.raycastTarget = false;

        _friendCountText = new GameObject("Count").AddComponent<Text>();
        _friendCountText.transform.SetParent(titleRow.transform, false);
        var fcR = _friendCountText.GetComponent<RectTransform>();
        fcR.anchorMin = new Vector2(0.5f, 0); fcR.anchorMax = new Vector2(0.75f, 1);
        fcR.offsetMin = Vector2.zero; fcR.offsetMax = Vector2.zero;
        _friendCountText.text = ""; _friendCountText.alignment = TextAnchor.MiddleLeft;
        _friendCountText.fontSize = 13; _friendCountText.color = UIHelper.TextDim;
        _friendCountText.font = font; _friendCountText.raycastTarget = false;

        // + 添加好友 按钮
        UIHelper.MakeButton(titleRow.transform, "AddFriendBtn", "+ 添加好友", font,
            new Vector2(0.75f, 0.05f), new Vector2(0.98f, 0.95f), Vector2.zero, Vector2.zero,
            UIHelper.BtnConfirm, 13, () =>
            {
                EnsureUIManager();
                if (FriendUI.Instance == null) GameManager.Instance.gameObject.AddComponent<FriendUI>();
                FriendUI.Instance.ShowAddFriendDirect();
            });

        // 好友列表容器 (滚动)
        _friendListContainer = new GameObject("FriendItems");
        _friendListContainer.transform.SetParent(friendArea.transform, false);
        var fiR = _friendListContainer.AddComponent<RectTransform>();
        fiR.anchorMin = new Vector2(0.02f, 0.02f); fiR.anchorMax = new Vector2(0.98f, 0.88f);
        fiR.offsetMin = Vector2.zero; fiR.offsetMax = Vector2.zero;
        _friendListContainer.AddComponent<RectMask2D>();
    }

    /// <summary>从服务器获取好友列表并刷新Hub右侧面板</summary>
    private System.Collections.IEnumerator RefreshFriendListHub()
    {
        if (_friendListContainer == null) yield break;

        string token = UnityEngine.PlayerPrefs.GetString("ARPG_AuthToken", "");

        // 有token时从服务器获取
        if (!string.IsNullOrEmpty(token))
        {
            using (var req = UnityEngine.Networking.UnityWebRequest.Get("http://39.107.141.107:5132/api/friend/list"))
            {
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", "Bearer " + token);
                yield return req.SendWebRequest();

                if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var resp = JsonUtility.FromJson<HubFriendListResp>(req.downloadHandler.text);
                        if (resp?.friends != null && resp.friends.Length > 0)
                        {
                            RenderFriendList(resp.friends.Length,
                                (i) => $"{resp.friends[i].username}  Lv.{resp.friends[i].level}",
                                (i) => resp.friends[i].friendId,
                                (i) => resp.friends[i].online);
                            yield break;
                        }
                    }
                    catch { }
                }
            }
        }

        // 无token或服务器失败 — 从本地缓存读取好友列表
        string localJson = UnityEngine.PlayerPrefs.GetString("ARPG_FriendList", "");
        if (!string.IsNullOrEmpty(localJson))
        {
            try
            {
                var localArr = JsonUtility.FromJson<FriendUI.FriendArray>(localJson);
                if (localArr?.friends != null && localArr.friends.Length > 0)
                {
                    RenderFriendList(localArr.friends.Length,
                        (i) => $"{localArr.friends[i].name}  Lv.{localArr.friends[i].level}",
                        (i) => 0,
                        (i) => false); // 本地缓存默认离线
                    yield break;
                }
            }
            catch { }
        }

        // 无任何好友数据 — 显示空状态
        RenderFriendList(0, (i) => "", (i) => 0, (i) => false);
    }

    /// <summary>渲染好友列表到Hub面板</summary>
    private void RenderFriendList(int count, System.Func<int, string> getName, System.Func<int, int> getId, System.Func<int, bool> getOnline = null)
    {
        // 清除旧内容
        for (int i = _friendListContainer.transform.childCount - 1; i >= 0; i--)
            Destroy(_friendListContainer.transform.GetChild(i).gameObject);

        Font font = GameManager.GetUIFont();
        float rowH = 36f, gap = 3f;

        for (int i = 0; i < count && i < 6; i++)
        {
            string fName = getName(i);
            int fId = getId(i);
            bool online = getOnline?.Invoke(i) ?? false;
            float y = -i * (rowH + gap);

            var row = UIHelper.MakeGlowCard(_friendListContainer.transform, $"Friend_{i}",
                new Vector2(0.01f, 1), new Vector2(0.99f, 1),
                new Vector2(0, y - rowH), new Vector2(0, y),
                new Color(0.06f, 0.04f, 0.08f, 0.9f), UIHelper.GlowBottom, UIHelper.BorderSubtle);

            // 整行可点击 → 打开聊天
            var rowBtn = row.AddComponent<Button>();
            rowBtn.transition = Selectable.Transition.None;
            var rowImg = row.GetComponent<Image>();
            UIHelper.SetupButtonFeedback(rowBtn, rowImg.color);
            string chatName = fName;
            rowBtn.onClick.AddListener(() =>
            {
                if (ChatPopupUI.Instance == null) gameObject.AddComponent<ChatPopupUI>();
                ChatPopupUI.Instance.Show(chatName);
            });

            // 在线状态圆点
            var dotObj = new GameObject("StatusDot");
            dotObj.transform.SetParent(row.transform, false);
            var dR = dotObj.AddComponent<RectTransform>();
            dR.anchorMin = new Vector2(0.03f, 0.3f); dR.anchorMax = new Vector2(0.03f, 0.7f);
            dR.pivot = new Vector2(0, 0.5f);
            dR.sizeDelta = new Vector2(10, 10);
            var dotImg = dotObj.AddComponent<Image>();
            dotImg.color = online ? new Color(0.3f, 0.8f, 0.3f, 1f) : new Color(0.5f, 0.3f, 0.3f, 1f);
            dotImg.raycastTarget = false;

            // 名字 + 状态
            var nameObj = new GameObject("Name");
            nameObj.transform.SetParent(row.transform, false);
            var nR = nameObj.AddComponent<RectTransform>();
            nR.anchorMin = new Vector2(0.08f, 0); nR.anchorMax = new Vector2(0.65f, 1);
            nR.offsetMin = Vector2.zero; nR.offsetMax = Vector2.zero;
            var nTxt = nameObj.AddComponent<Text>();
            nTxt.text = $"{fName}  <size=12><color=#{(online ? "55FF55" : "888888")}>{(online ? "在线" : "离线")}</color></size>";
            nTxt.alignment = TextAnchor.MiddleLeft; nTxt.fontSize = 14;
            nTxt.color = UIHelper.TextPrimary; nTxt.font = font;
            nTxt.supportRichText = true; nTxt.raycastTarget = false;
        }

        if (_friendCountText != null)
            _friendCountText.text = count > 0 ? $"({count}/50)" : "";
    }

    [System.Serializable]
    private class HubFriendListResp { public HubFriendItem[] friends; public int count; public int max; }
    [System.Serializable]
    private class HubFriendItem { public int friendId; public string username; public int level; public int classType; public bool online; public string remark; }

    private void UpdateInfo()
    {
        var player = GameManager.Instance?.Player;
        if (player == null) return;
        var data = ClassData.GetClassData(player.HeroClass);

        TopNavBar.Instance?.RefreshCurrency();
        if (classText != null)
            classText.text = $"{data.ClassName}  Lv.{player.Stats.Level}  战力: {player.Stats.GearScore}";
        if (hpText != null)
            hpText.text = $"HP: {player.Stats.Hp}/{player.Stats.TotalMaxHp}";
        if (atkText != null)
            atkText.text = $"ATK: {player.Stats.BuffedAttack}  移速: {player.Stats.TotalMoveSpeed:F1}  暴击: {player.Stats.TotalCritChance*100:F0}%";
        if (defText != null)
            defText.text = $"DEF: {player.Stats.BuffedDefense}  吸血: {player.Stats.LifeSteal*100:F0}%  攻速: {player.Stats.TotalAttackSpeed:F1}x";

        var inv = player.Inventory;
        if (inv != null)
        {
            if (weaponText != null)
            {
                var weapon = inv.Equipped.Find(s => s.SlotType == EquipSlotType.Weapon);
                if (weapon != null && weapon.Item != null)
                {
                    string upgrade = weapon.Item.UpgradeLevel > 0 ? $" <color=#8C55C0>+{weapon.Item.UpgradeLevel}</color>" : "";
                    weaponText.text = $"武器: {weapon.Item.Name}{upgrade}";
                }
                else
                    weaponText.text = "武器: <color=#555>空</color>";
            }
            if (armorText != null)
            {
                var armor = inv.Equipped.Find(s => s.SlotType == EquipSlotType.Armor);
                if (armor != null && armor.Item != null)
                {
                    string upgrade = armor.Item.UpgradeLevel > 0 ? $" <color=#8C55C0>+{armor.Item.UpgradeLevel}</color>" : "";
                    armorText.text = $"护甲: {armor.Item.Name}{upgrade}";
                }
                else
                    armorText.text = "护甲: <color=#555>空</color>";
            }
            if (accessoryText != null)
            {
                var acc = inv.Equipped.Find(s => s.SlotType == EquipSlotType.Accessory);
                if (acc != null && acc.Item != null)
                {
                    string upgrade = acc.Item.UpgradeLevel > 0 ? $" <color=#8C55C0>+{acc.Item.UpgradeLevel}</color>" : "";
                    accessoryText.text = $"饰品: {acc.Item.Name}{upgrade}";
                }
                else
                    accessoryText.text = "饰品: <color=#555>空</color>";
            }
            if (backpackText != null)
                backpackText.text = $"背包: {inv.Backpack.Count}/{EquipmentInventory.MaxBackpackSize}";
        }
    }

    private void ShowOfflineRewardPopup(OfflineRewards.OfflineRewardInfo reward)
    {
        Font font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();

        var popup = new GameObject("OfflineRewardPopup");
        popup.transform.SetParent(canvas.transform, false);
        var pr = popup.AddComponent<RectTransform>();
        pr.anchorMin = new Vector2(0.3f, 0.3f); pr.anchorMax = new Vector2(0.7f, 0.7f);
        pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;

        var bg = popup.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.02f, 0.06f, 0.98f);

        var pc = popup.AddComponent<Canvas>();
        pc.overrideSorting = true; pc.sortingOrder = 20;
        popup.AddComponent<GraphicRaycaster>();

        // Title
        var titleObj = new GameObject("Title");
        titleObj.transform.SetParent(popup.transform, false);
        var tr = titleObj.AddComponent<RectTransform>();
        tr.anchorMin = new Vector2(0, 0.75f); tr.anchorMax = new Vector2(1, 1);
        tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
        var titleTxt = titleObj.AddComponent<Text>();
        titleTxt.text = "离线收益"; titleTxt.alignment = TextAnchor.MiddleCenter;
        titleTxt.fontSize = 28; titleTxt.color = UIHelper.Accent; titleTxt.font = font;

        // Info
        var infoObj = new GameObject("Info");
        infoObj.transform.SetParent(popup.transform, false);
        var ir = infoObj.AddComponent<RectTransform>();
        ir.anchorMin = new Vector2(0.1f, 0.35f); ir.anchorMax = new Vector2(0.9f, 0.72f);
        ir.offsetMin = Vector2.zero; ir.offsetMax = Vector2.zero;
        var infoTxt = infoObj.AddComponent<Text>();
        infoTxt.alignment = TextAnchor.MiddleCenter;
        infoTxt.fontSize = 20; infoTxt.color = UIHelper.TextPrimary; infoTxt.font = font;
        infoTxt.supportRichText = true;
        infoTxt.text = $"离线时长: <color=#88CCFF>{reward.hours:F1}小时</color>\n" +
                       $"每小时收益: <color=#FFD700>{reward.goldPerHour}金币</color>\n" +
                       $"<size=24><color=#FFD700>+{reward.goldReward} 金币</color></size>";

        // Claim button
        var btnObj = new GameObject("ClaimBtn");
        btnObj.transform.SetParent(popup.transform, false);
        var br = btnObj.AddComponent<RectTransform>();
        br.anchorMin = new Vector2(0.25f, 0.12f); br.anchorMax = new Vector2(0.75f, 0.30f);
        br.offsetMin = Vector2.zero; br.offsetMax = Vector2.zero;
        var btnImg = btnObj.AddComponent<Image>();
        btnImg.color = UIHelper.BtnConfirm;
        var btn = btnObj.AddComponent<Button>();
        var lbl = new GameObject("Lbl");
        lbl.transform.SetParent(btnObj.transform, false);
        var lr = lbl.AddComponent<RectTransform>();
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
        lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;
        var lblTxt = lbl.AddComponent<Text>();
        lblTxt.text = "领取"; lblTxt.alignment = TextAnchor.MiddleCenter;
        lblTxt.fontSize = 24; lblTxt.color = UIHelper.TextPrimary; lblTxt.font = font;
        lblTxt.raycastTarget = false;

        btn.onClick.AddListener(() =>
        {
            var player = GameManager.Instance?.Player;
            if (player != null) player.Stats.AddGold(reward.goldReward);
            OfflineRewards.ClaimReward();
            Destroy(popup);
        });
    }

    private void OnDestroy()
    {
        if (_petPanel != null) Destroy(_petPanel);
        if (panel != null) Destroy(panel);
        if (Instance == this) Instance = null;
    }

    // === Pet Panel ===
    private GameObject _petPanel;

    public void ShowPetPanel()
    {
        if (_petPanel != null) { Destroy(_petPanel); _petPanel = null; }

        Font font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();

        _petPanel = new GameObject("PetPanel");
        _petPanel.transform.SetParent(canvas.transform, false);
        RectTransform rt = _petPanel.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        Image bg = _petPanel.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.05f, 0.08f, 1f);

        // Nav buttons (same as all other panels) — back closes pet panel
        UIHelper.MakeNavButtons(_petPanel.transform, font, onBack: () => { Destroy(_petPanel); _petPanel = null; });

        // Title
        var titleObj = new GameObject("PetTitle");
        titleObj.transform.SetParent(_petPanel.transform, false);
        var tr = titleObj.AddComponent<RectTransform>();
        tr.anchorMin = new Vector2(0.5f, 1); tr.anchorMax = new Vector2(0.5f, 1);
        tr.pivot = new Vector2(0.5f, 1); tr.anchoredPosition = new Vector2(0, -65);
        tr.sizeDelta = new Vector2(400, 40);
        var titleText = titleObj.AddComponent<Text>();
        titleText.text = "宠物";
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.fontSize = 28; titleText.color = new Color(0.3f, 0.8f, 0.9f);
        titleText.font = font;
        titleText.raycastTarget = false;

        // Scroll area for pet cards
        var scrollObj = new GameObject("PetScroll");
        scrollObj.transform.SetParent(_petPanel.transform, false);
        var scrollRt = scrollObj.AddComponent<RectTransform>();
        scrollRt.anchorMin = new Vector2(0.1f, 0.05f); scrollRt.anchorMax = new Vector2(0.9f, 0.85f);
        scrollRt.offsetMin = Vector2.zero; scrollRt.offsetMax = Vector2.zero;

        // Build a pet card for each type
        var defs = PetDataManager.PetDefs;
        float cardH = 100f;
        float gap = 10f;
        string[] emoji = { "", "", "", "" };
        Color[] colors = {
            new Color(0.3f, 0.8f, 1f),
            new Color(1f, 0.4f, 0.1f),
            new Color(0.3f, 0.9f, 0.3f),
            new Color(0.7f, 0.2f, 0.9f)
        };

        for (int i = 0; i < defs.Length; i++)
        {
            var def = defs[i];
            int idx = i;
            float y = -i * (cardH + gap) - 10f;

            // Card container
            var cardObj = new GameObject($"PetCard_{def.Type}");
            cardObj.transform.SetParent(scrollObj.transform, false);
            var cardRt = cardObj.AddComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0, 1); cardRt.anchorMax = new Vector2(1, 1);
            cardRt.pivot = new Vector2(0.5f, 1);
            cardRt.anchoredPosition = new Vector2(0, y);
            cardRt.sizeDelta = new Vector2(0, cardH);
            var cardImg = cardObj.AddComponent<Image>();
            bool owned = PetDataManager.IsOwned(def.Type);
            cardImg.color = owned ? new Color(0.1f, 0.15f, 0.12f, 0.9f) : new Color(0.1f, 0.08f, 0.06f, 0.9f);

            // Left color bar
            var barObj = new GameObject("ColorBar");
            barObj.transform.SetParent(cardObj.transform, false);
            var barRt = barObj.AddComponent<RectTransform>();
            barRt.anchorMin = new Vector2(0, 0); barRt.anchorMax = new Vector2(0, 1);
            barRt.pivot = new Vector2(0, 0.5f);
            barRt.sizeDelta = new Vector2(5, 0);
            var barImg = barObj.AddComponent<Image>();
            barImg.color = colors[i];

            // Name + status (left side, takes 65% width)
            var nameObj = new GameObject("Name");
            nameObj.transform.SetParent(cardObj.transform, false);
            var nameRt = nameObj.AddComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0.02f, 0.1f); nameRt.anchorMax = new Vector2(0.62f, 0.95f);
            nameRt.offsetMin = Vector2.zero; nameRt.offsetMax = Vector2.zero;
            var nameText = nameObj.AddComponent<Text>();
            nameText.font = font; nameText.supportRichText = true;
            nameText.alignment = TextAnchor.LowerLeft; nameText.fontSize = 20;

            int level = PetDataManager.GetLevel(def.Type);
            bool evolved = PetDataManager.IsEvolved(def.Type);
            string display = PetDataManager.GetDisplayName(def.Type);

            if (!owned)
                nameText.text = $"{emoji[i]} {display}\n<size=14><color=#888>{def.EvolveDesc}</color></size>";
            else
            {
                bool isActive = GameManager.Instance.ActivePetType == def.Type;
                nameText.text = $"{emoji[i]} {display} <size=16><color=#FFD700>Lv.{level}/{def.MaxLevel}</color></size>" +
                    (evolved ? " <color=#FF88FF>★进化</color>" : "") +
                    (isActive ? " <color=#44FF44>[携带]</color>" : "") +
                    $"\n<size=13><color=#aaa>{def.EvolveDesc}</color></size>";
            }
            nameText.color = colors[i];

            // Right side: two buttons side-by-side in bottom row
            // Carry button — right-left (50% of right area)
            if (owned)
            {
                var carryObj = new GameObject("CarryBtn");
                carryObj.transform.SetParent(cardObj.transform, false);
                var carryRt = carryObj.AddComponent<RectTransform>();
                carryRt.anchorMin = new Vector2(0.64f, 0.1f); carryRt.anchorMax = new Vector2(0.80f, 0.5f);
                carryRt.offsetMin = Vector2.zero; carryRt.offsetMax = Vector2.zero;
                var carryImg = carryObj.AddComponent<Image>();
                bool isActive = GameManager.Instance.ActivePetType == def.Type;
                carryImg.color = isActive ? new Color(0.2f, 0.7f, 0.3f, 0.9f) : new Color(0.15f, 0.25f, 0.2f, 0.8f);
                var carryBtn = carryObj.AddComponent<Button>();
                var carryLbl = new GameObject("Label");
                carryLbl.transform.SetParent(carryObj.transform, false);
                var carryLblRt = carryLbl.AddComponent<RectTransform>();
                carryLblRt.anchorMin = Vector2.zero; carryLblRt.anchorMax = Vector2.one;
                carryLblRt.offsetMin = Vector2.zero; carryLblRt.offsetMax = Vector2.zero;
                var carryLblText = carryLbl.AddComponent<Text>();
                carryLblText.font = font; carryLblText.alignment = TextAnchor.MiddleCenter;
                carryLblText.fontSize = 14; carryLblText.color = Color.white;
                carryLblText.text = isActive ? "✓ 携带中\n<size=12>点击取消</size>" : "携带";
                carryBtn.onClick.AddListener(() =>
                {
                    if (isActive)
                    {
                        // 取消携带
                        PetDataManager.ClearActivePet();
                        if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("宠物取消", $"{emoji[idx]} {display} 已取消携带");
                    }
                    else
                    {
                        // 切换携带
                        GameManager.Instance.ActivePetType = def.Type;
                        PetDataManager.SaveActivePet(def.Type);
                        if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("宠物切换", $"{emoji[idx]} {display} 已设为携带宠物！");
                    }
                    Destroy(_petPanel); _petPanel = null; ShowPetPanel();
                });
            }

            // Action button (buy/upgrade/evolve) — right-right (50% of right area)
            var btnObj = new GameObject("ActionBtn");
            btnObj.transform.SetParent(cardObj.transform, false);
            var btnRt = btnObj.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.82f, 0.1f); btnRt.anchorMax = new Vector2(0.98f, 0.5f);
            btnRt.offsetMin = Vector2.zero; btnRt.offsetMax = Vector2.zero;
            var btnImg = btnObj.AddComponent<Image>();
            var btn = btnObj.AddComponent<Button>();
            var btnColors = btn.colors;
            btnColors.fadeDuration = 0.05f;

            // Label inside button
            var lblObj = new GameObject("Label");
            lblObj.transform.SetParent(btnObj.transform, false);
            var lblRt = lblObj.AddComponent<RectTransform>();
            lblRt.anchorMin = Vector2.zero; lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = Vector2.zero; lblRt.offsetMax = Vector2.zero;
            var lblText = lblObj.AddComponent<Text>();
            lblText.font = font; lblText.alignment = TextAnchor.MiddleCenter;
            lblText.fontSize = 14; lblText.color = Color.white; lblText.supportRichText = true;

            if (!owned)
            {
                // Not owned — 直接购买
                btnImg.color = new Color(0.15f, 0.35f, 0.15f, 0.9f);
                lblText.text = $"购买\n<size=12>◆ {def.Price}金</size>";
                btn.onClick.AddListener(() =>
                {
                    if (PetDataManager.Buy(def.Type))
                    {
                        if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("购买成功", $"{emoji[idx]} {display} 已加入宠物栏！");
                        Destroy(_petPanel); _petPanel = null; ShowPetPanel();
                    }
                    else if (GameUI.Instance != null)
                        GameUI.Instance.ShowItemPickupToast("购买失败", "金币不足");
                });
            }
            else if (level < def.MaxLevel)
            {
                // Upgrade
                int cost = def.UpgradeCost * level;
                btnImg.color = cost <= (GameManager.Instance?.Player?.Stats?.Gold ?? 0)
                    ? new Color(0.15f, 0.4f, 0.7f, 0.9f) : new Color(0.2f, 0.2f, 0.2f, 0.7f);
                lblText.text = $"升级 Lv{level}→{level+1}\n<size=13>◆ {cost}金</size>";
                btn.onClick.AddListener(() =>
                {
                    if (PetDataManager.Upgrade(def.Type))
                    {
                        if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("升级成功", $"{emoji[idx]} {display} Lv.{level+1}！");
                        Destroy(_petPanel); _petPanel = null; ShowPetPanel();
                    }
                    else if (GameUI.Instance != null)
                        GameUI.Instance.ShowItemPickupToast("升级失败", "金币不足");
                });
            }
            else if (!evolved && level >= def.EvolveLevel)
            {
                // Evolve
                btnImg.color = new Color(0.6f, 0.2f, 0.8f, 0.9f);
                lblText.text = $"<color=#FF88FF>进化! →{def.EvolveName}</color>\n<size=13>◆ 2000金</size>";
                btn.onClick.AddListener(() =>
                {
                    if (PetDataManager.Evolve(def.Type))
                    {
                        if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("进化成功!", $"{emoji[idx]} 进化为 {def.EvolveName}！");
                        Destroy(_petPanel); _petPanel = null; ShowPetPanel();
                    }
                    else if (GameUI.Instance != null)
                        GameUI.Instance.ShowItemPickupToast("进化失败", "金币不足或等级不够");
                });
            }
            else if (evolved)
            {
                // Maxed
                btnImg.color = new Color(0.15f, 0.15f, 0.18f, 0.7f);
                lblText.text = "★ 已满级";
                btn.interactable = false;
            }
            else
            {
                // Need higher level to evolve
                btnImg.color = new Color(0.15f, 0.15f, 0.18f, 0.7f);
                lblText.text = $"满级\n<size=12>进化需Lv{def.EvolveLevel}</size>";
                btn.interactable = false;
            }

            // If not owned, check if we can afford
            if (!owned && def.Price > (GameManager.Instance?.Player?.Stats?.Gold ?? 0))
                btn.interactable = false;
            if (owned && level < def.MaxLevel && def.UpgradeCost * level > (GameManager.Instance?.Player?.Stats?.Gold ?? 0))
                btn.interactable = false;
            if (owned && !evolved && level >= def.MaxLevel && level >= def.EvolveLevel && 2000 > (GameManager.Instance?.Player?.Stats?.Gold ?? 0))
                btn.interactable = false;
        }

        // Update gold display
        RefreshPetGold();
    }

    private void RefreshPetGold()
    {
        TopNavBar.Instance?.RefreshCurrency();
    }
}
