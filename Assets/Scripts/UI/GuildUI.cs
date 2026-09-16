using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 公会面板 — 创建/加入公会、成员管理、信息、Boss挑战、商店
/// 纯客户端实现，数据存储在 PlayerPrefs
/// </summary>
public partial class GuildUI : MonoBehaviour
{
    public static GuildUI Instance { get; private set; }

    private GameObject panel;
    private Coroutine showRoutine;
    private int _currentTab = 0;
    private int _memberPage;
    private const int MembersPerPage = 10;
    private Text _pageText;
    private GameObject _tabContent;
    private Font _font;

    // ====== 常量 ======
    private static readonly string[] ClassNames = { "战士", "法师", "牧师" };
    private static readonly Color[] ClassColors =
    {
        new Color(0.7f, 0.2f, 0.2f, 1f),
        new Color(0.2f, 0.35f, 0.8f, 1f),
        new Color(0.8f, 0.75f, 0.4f, 1f)
    };
    private static readonly string[] RankNames = { "会长", "副会长", "长老", "成员" };
    private static readonly Color[] RankColors =
    {
        new Color(1f, 0.85f, 0.2f, 1f),   // 会长 - 金色
        new Color(0.8f, 0.5f, 1f, 1f),    // 副会长 - 紫色
        new Color(0.3f, 0.9f, 1f, 1f),    // 长老 - 青色
        new Color(0.7f, 0.7f, 0.7f, 1f),  // 成员 - 灰色
    };
    // 权限矩阵 — rank: 0=会长 1=副会长 2=长老 3=成员
    // 会长:   解散/转让/任命副会长/修改公会名/踢副会长以下/审批/公告/仓库/公会战
    // 副会长: 任命长老/踢长老和成员/审批/公告/仓库取物/发起公会战
    // 长老:   踢成员/审批/修改公告/取仓库物品
    // 成员:   捐献/商店购买/参战/查看信息
    private static bool CanDisband => PlayerRank == 0;           // 解散公会: 仅会长
    private static bool CanTransferMaster => PlayerRank == 0;    // 转让会长: 仅会长
    private static bool CanAppointVice => PlayerRank == 0;        // 任命/罢免副会长: 仅会长
    private static bool CanEditName => PlayerRank == 0;          // 修改公会名: 仅会长
    private static bool CanAppointElder => PlayerRank <= 1;      // 任命/罢免长老: 会长+副会长
    private static bool CanKick(int targetRank) => PlayerRank < targetRank && PlayerRank <= (targetRank == 1 ? 0 : 2); // 踢人: 会长踢副会长以下, 副会长踢长老和成员, 长老踢成员
    private static bool CanApprove => PlayerRank <= 2;            // 审批申请: 会长+副会长+长老
    private static bool CanEditAnnounce => PlayerRank <= 2;      // 修改公告: 会长+副会长+长老
    private static bool CanWithdraw => PlayerRank <= 2;           // 取仓库物品: 会长+副会长+长老
    private static bool CanStartWar => PlayerRank <= 1;           // 发起公会战: 会长+副会长
    private static bool CanShop => true;                         // 商店购买: 全员
    private static bool CanDonate => true;                       // 捐献: 全员
    private static bool CanJoinWar => true;                       // 参战: 全员
    private static bool CanViewMembers => true;                  // 查看信息: 全员
    private static float DailyBonus => PlayerRank switch { 0 => 0.20f, 1 => 0.15f, 2 => 0.10f, _ => 0f }; // 签到加成

    // ====== PlayerPrefs Keys — moved to GuildData.cs (data access layer) ======

    // ====== 商店配置 ======
    private static readonly int[] DonateTiers = { 100, 500, 1000 };
    private static readonly int[] DonateContribution = { 10, 60, 150 };
    private static readonly int[] DonateGuildExp = { 20, 100, 250 };
    private const int MaxDailyDonations = 3;

    // ====== Boss配置 ======
    private static readonly string[] BossNames = { "暗影巨龙", "熔岩泰坦", "冰霜女王", "深渊魔王", "风暴之神" };
    private const int BossBaseHP = 50000;

    // ====== 商店配置 ======
    private static readonly (string name, int cost, string type, int value, int rarity)[] ShopItems =
    {
        ("稀有装备箱", 50, "equipment", 1, 1),
        ("史诗装备箱", 200, "equipment", 1, 2),
        ("传说装备箱", 500, "equipment", 1, 3),
        ("金币 x1000", 80, "gold", 1000, 0),
        ("金币 x5000", 350, "gold", 5000, 0),
        ("技能点 x1", 300, "skillpoint", 1, 0),
        ("碎片 x100", 120, "fragment", 100, 0),
    };

    private void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; }

    // ====== HTTP Helpers — moved to GuildUI.Network.cs (partial class) ======

    public void Show()
    {
        if (GuildDataManager.Instance == null) gameObject.AddComponent<GuildDataManager>();
        if (panel != null) { Destroy(panel); panel = null; }
        _memberPage = 0;
        _currentTab = 0;
        BuildUI();
        UIHelper.SetupTopBar("公会", () => { Hide(); UIHelper.EnsureUIManager(); UIManager.Instance.ShowHub(); });
        panel.SetActive(true);
        StartCoroutine(CheckGuildInvites());
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

    // ====== 数据访问 — 委托到 GuildData.cs ======
    private static bool HasGuild => GuildData.HasGuild;
    private static string GuildName => GuildData.GuildName;
    private static int GuildLevel => GuildData.GuildLevel;
    private static int GuildExp => GuildData.GuildExp;
    private static int Contribution => GuildData.Contribution;
    /// <summary>玩家在公会中的职位 (0=会长 1=副会长 2=长老 3=成员)</summary>
    private static int PlayerRank => GuildData.PlayerRank;
    private static string GuildAnnounce => GuildData.GuildAnnounce;
    private static int GuildMaxMembers => GuildData.GuildMaxMembers;
    private static int GuildExpToNext => GuildData.GuildExpToNext;

    private void BuildUI()
    {
        _font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();

        panel = UiPrefabLoader.TryLoad("GuildPanel", canvas);
        if (panel == null)
        panel = new GameObject("GuildPanel");
        panel.transform.SetParent(canvas.transform, false);
        RectTransform pr = panel.AddComponent<RectTransform>();
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one; pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;
        panel.AddComponent<CanvasGroup>();

        var bg = new GameObject("BG"); bg.transform.SetParent(panel.transform, false);
        var bgr = bg.AddComponent<RectTransform>(); bgr.anchorMin = Vector2.zero; bgr.anchorMax = Vector2.one; bgr.offsetMin = Vector2.zero; bgr.offsetMax = Vector2.zero;
        bg.AddComponent<Image>().color = UIHelper.BgDark;

        UIHelper.MakeSubtitle(panel.transform, HasGuild ? $"{GuildName}  Lv.{GuildLevel}" : "创建或加入公会", _font, 100, 16);

        if (HasGuild)
            BuildTabbedView();
        else
            BuildNoGuildView();

        panel.SetActive(false);
    }

    // ========================================================
    //  无公会视图
    // ========================================================
    private void BuildNoGuildView()
    {
        // 创建公会卡片
        var createArea = UIHelper.MakeGlowCard(panel.transform, "CreateArea",
            new Vector2(0.15f, 0.55f), new Vector2(0.85f, 0.80f),
            Vector2.zero, Vector2.zero,
            UIHelper.GlowTop, UIHelper.GlowBottom, UIHelper.Accent);

        var createTitle = new GameObject("Title"); createTitle.transform.SetParent(createArea.transform, false);
        var ctr = createTitle.AddComponent<RectTransform>(); ctr.anchorMin = new Vector2(0.1f, 0.70f); ctr.anchorMax = new Vector2(0.9f, 0.95f); ctr.offsetMin = Vector2.zero; ctr.offsetMax = Vector2.zero;
        var ctTxt = createTitle.AddComponent<Text>(); ctTxt.text = "创建公会"; ctTxt.alignment = TextAnchor.MiddleCenter; ctTxt.fontSize = 22; ctTxt.color = UIHelper.Accent; ctTxt.font = _font;

        var costTxt = new GameObject("Cost"); costTxt.transform.SetParent(createArea.transform, false);
        var costR = costTxt.AddComponent<RectTransform>(); costR.anchorMin = new Vector2(0.1f, 0.40f); costR.anchorMax = new Vector2(0.9f, 0.65f); costR.offsetMin = Vector2.zero; costR.offsetMax = Vector2.zero;
        var costText = costTxt.AddComponent<Text>(); costText.text = "费用: 2000  名称随机\n成为会长，管理一切"; costText.alignment = TextAnchor.MiddleCenter; costText.fontSize = 14; costText.color = UIHelper.TextSecondary; costText.font = _font; costText.supportRichText = true;

        var createBtn = UIHelper.MakeButton(createArea.transform, "CreateBtn", "创建公会", _font,
            new Vector2(0.25f, 0.08f), new Vector2(0.75f, 0.35f), Vector2.zero, Vector2.zero,
            UIHelper.BtnConfirm, 20, () => CreateGuild());

        // 加入公会卡片
        var joinArea = UIHelper.MakeGlowCard(panel.transform, "JoinArea",
            new Vector2(0.15f, 0.30f), new Vector2(0.85f, 0.52f),
            Vector2.zero, Vector2.zero,
            new Color(0.05f, 0.04f, 0.08f, 0.6f), UIHelper.GlowBottom, UIHelper.BorderSubtle);

        var joinTitle = new GameObject("Title"); joinTitle.transform.SetParent(joinArea.transform, false);
        var jtr = joinTitle.AddComponent<RectTransform>(); jtr.anchorMin = new Vector2(0.1f, 0.70f); jtr.anchorMax = new Vector2(0.9f, 0.95f); jtr.offsetMin = Vector2.zero; jtr.offsetMax = Vector2.zero;
        var jtTxt = joinTitle.AddComponent<Text>(); jtTxt.text = "加入公会"; jtTxt.alignment = TextAnchor.MiddleCenter; jtTxt.fontSize = 22; jtTxt.color = UIHelper.Accent; jtTxt.font = _font;

        var joinDesc = new GameObject("Desc"); joinDesc.transform.SetParent(joinArea.transform, false);
        var jdR = joinDesc.AddComponent<RectTransform>(); jdR.anchorMin = new Vector2(0.1f, 0.40f); jdR.anchorMax = new Vector2(0.9f, 0.65f); jdR.offsetMin = Vector2.zero; jdR.offsetMax = Vector2.zero;
        var jdTxt = joinDesc.AddComponent<Text>(); jdTxt.text = "查看公会信息\n选择加入"; jdTxt.alignment = TextAnchor.MiddleCenter; jdTxt.fontSize = 14; jdTxt.color = UIHelper.TextSecondary; jdTxt.font = _font;

        var joinBtn = UIHelper.MakeButton(joinArea.transform, "JoinBtn", "加入公会", _font,
            new Vector2(0.25f, 0.08f), new Vector2(0.75f, 0.35f), Vector2.zero, Vector2.zero,
            UIHelper.BtnPrimary, 20, () => StartCoroutine(ShowJoinGuildPopup()));
    }

    /// <summary>创建公会</summary>
    private void CreateGuild()
    {
        // 扣金币：优先通过Player.Stats.SpendGold（服务器权威），Player为null时直接从PlayerPrefs扣
        bool goldDeducted = false;
        var player = GameManager.Instance?.Player;
        if (player != null)
        {
            goldDeducted = player.Stats.SpendGold(2000);
        }
        else
        {
            // Player未创建时，直接从PlayerPrefs扣金币
            string goldKey = $"ARPG_S{PlayerProgressData.ActiveSlot}_Gold";
            int currentGold = PlayerPrefs.GetInt(goldKey, 0);
            if (currentGold >= 2000)
            {
                PlayerPrefs.SetInt(goldKey, currentGold - 2000);
                PlayerPrefs.Save();
                goldDeducted = true;
            }
        }
        if (!goldDeducted)
        {
            if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("金币不足", "创建公会需要2000金币");
            return;
        }
        string[] prefixes = { "暗影", "烈焰", "冰霜", "风暴", "圣光", "深渊", "苍穹", "龙裔" };
        string[] suffixes = { "军团", "联盟", "战盟", "骑士团", "守望者", "先锋" };
        string guildName = prefixes[Random.Range(0, prefixes.Length)] + suffixes[Random.Range(0, suffixes.Length)];
        PlayerPrefs.SetString(GuildData.KeyGuildName, guildName);
        PlayerPrefs.SetInt(GuildData.KeyGuildLevel, 1);
        PlayerPrefs.SetInt(GuildData.KeyGuildExp, 0);
        PlayerPrefs.SetInt(GuildData.KeyContribution, 0);
        PlayerPrefs.SetInt(GuildData.KeyPlayerRank, 0); // 会长
        PlayerPrefs.SetString(GuildData.KeyGuildAnnounce, $"欢迎加入「{guildName}」！一起努力变强吧！");
        // 创建公会时只添加会长(自己)，成员通过邀请加入
        var members = new List<GuildData.GuildMember>();
        SaveMembers(members);
        PlayerPrefs.Save();
        if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("公会创建成功", $"欢迎加入「{guildName}」! 你是会长");
        Show();
    }

    /// <summary>加入公会弹窗 — 可滚动公会列表</summary>
    private IEnumerator ShowJoinGuildPopup()
    {
        Canvas canvas = GameManager.EnsureCanvas();

        // 全屏遮罩
        var overlay = new GameObject("JoinGuildPopup");
        overlay.transform.SetParent(canvas.transform, false);
        var oR = overlay.AddComponent<RectTransform>();
        oR.anchorMin = Vector2.zero; oR.anchorMax = Vector2.one; oR.offsetMin = Vector2.zero; oR.offsetMax = Vector2.zero;
        var oImg = overlay.AddComponent<Image>(); oImg.color = new Color(0, 0, 0, 0.7f);
        var oCG = overlay.AddComponent<CanvasGroup>();

        // 弹窗容器
        var box = UIHelper.MakeGlowCard(overlay.transform, "Box",
            new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f), Vector2.zero, Vector2.zero,
            new Color(0.06f, 0.05f, 0.10f, 0.98f), UIHelper.GlowBottom, UIHelper.Accent);

        // 标题
        var titleObj = new GameObject("Title"); titleObj.transform.SetParent(box.transform, false);
        var tR = titleObj.AddComponent<RectTransform>(); tR.anchorMin = new Vector2(0, 0.92f); tR.anchorMax = new Vector2(0.85f, 1f); tR.offsetMin = Vector2.zero; tR.offsetMax = Vector2.zero;
        var title = titleObj.AddComponent<Text>(); title.text = "加入公会"; title.alignment = TextAnchor.MiddleLeft; title.fontSize = 22; title.color = UIHelper.Accent; title.font = _font;

        // 关闭按钮
        var closeBtn = UIHelper.MakeButton(box.transform, "CloseBtn", "X", _font,
            new Vector2(0.88f, 0.93f), new Vector2(0.98f, 0.99f), Vector2.zero, Vector2.zero,
            UIHelper.BtnDanger, 18, () => Destroy(overlay));

        // 滚动列表容器
        var scrollObj = new GameObject("ScrollArea");
        scrollObj.transform.SetParent(box.transform, false);
        var saR = scrollObj.AddComponent<RectTransform>();
        saR.anchorMin = new Vector2(0.05f, 0.06f); saR.anchorMax = new Vector2(0.95f, 0.90f); saR.offsetMin = Vector2.zero; saR.offsetMax = Vector2.zero;
        var saImg = scrollObj.AddComponent<Image>(); saImg.color = new Color(0.03f, 0.02f, 0.05f, 0.5f);

        var scroll = scrollObj.AddComponent<ScrollRect>();
        scroll.scrollSensitivity = 20f;

        // Viewport (RectMask2D裁剪)
        var vpObj = new GameObject("Viewport");
        vpObj.transform.SetParent(scrollObj.transform, false);
        var vpR = vpObj.AddComponent<RectTransform>();
        vpR.anchorMin = Vector2.zero; vpR.anchorMax = Vector2.one; vpR.offsetMin = Vector2.zero; vpR.offsetMax = Vector2.zero;
        vpObj.AddComponent<RectMask2D>();
        var vpImg = vpObj.AddComponent<Image>(); vpImg.color = Color.clear;

        // Content
        var contentObj = new GameObject("Content");
        contentObj.transform.SetParent(vpObj.transform, false);
        var cR = contentObj.AddComponent<RectTransform>();
        cR.anchorMin = new Vector2(0, 1); cR.anchorMax = Vector2.one; cR.pivot = new Vector2(0.5f, 1);
        cR.offsetMin = Vector2.zero; cR.offsetMax = Vector2.zero;

        scroll.content = cR;
        scroll.viewport = vpR;

        // 加载提示
        var loadingObj = new GameObject("Loading");
        loadingObj.transform.SetParent(vpObj.transform, false);
        var lR = loadingObj.AddComponent<RectTransform>();
        lR.anchorMin = new Vector2(0, 0.4f); lR.anchorMax = new Vector2(1, 0.6f);
        lR.offsetMin = Vector2.zero; lR.offsetMax = Vector2.zero;
        var lTxt = loadingObj.AddComponent<Text>();
        lTxt.text = "加载公会列表..."; lTxt.alignment = TextAnchor.MiddleCenter;
        lTxt.fontSize = 18; lTxt.color = UIHelper.TextDim; lTxt.font = _font;

        // 从服务器获取公会列表
        yield return GetGuildAPI("/api/guild/list?page=1&pageSize=20", (ok, resp) =>
        {
            Destroy(loadingObj);
            if (!ok || string.IsNullOrEmpty(resp))
            {
                lTxt.text = "加载失败，请先登录";
                return;
            }
            try
            {
                var guildResp = JsonUtility.FromJson<ServerGuildListResponse>(resp);
                if (guildResp?.guilds == null || guildResp.guilds.Length == 0)
                {
                    var emptyObj = new GameObject("EmptyHint");
                    emptyObj.transform.SetParent(vpObj.transform, false);
                    var eR = emptyObj.AddComponent<RectTransform>();
                    eR.anchorMin = new Vector2(0, 0.3f); eR.anchorMax = new Vector2(1, 0.7f);
                    eR.offsetMin = Vector2.zero; eR.offsetMax = Vector2.zero;
                    var eTxt = emptyObj.AddComponent<Text>();
                    eTxt.text = "暂无可加入的公会\n\n请创建一个新公会";
                    eTxt.alignment = TextAnchor.MiddleCenter; eTxt.fontSize = 20;
                    eTxt.color = UIHelper.TextDim; eTxt.font = _font;
                    return;
                }

                var guilds = guildResp.guilds;
                float rowH = 100f, gap = 8f;
                cR.sizeDelta = new Vector2(0, guilds.Length * (rowH + gap) + 16f);

                for (int i = 0; i < guilds.Length; i++)
                {
                    var g = guilds[i];
                    int guildId = g.id;
                    float y = -8f - i * (rowH + gap);

                    var row = UIHelper.MakeGlowCard(contentObj.transform, $"Guild_{i}",
                        new Vector2(0, 1), new Vector2(1, 1),
                        new Vector2(8, y - rowH), new Vector2(-8, y),
                        new Color(0.06f, 0.04f, 0.08f, 0.92f), UIHelper.GlowBottom, UIHelper.BorderSubtle);

                    var nameObj = new GameObject("Name"); nameObj.transform.SetParent(row.transform, false);
                    var nR = nameObj.AddComponent<RectTransform>(); nR.anchorMin = new Vector2(0.03f, 0.65f); nR.anchorMax = new Vector2(0.6f, 0.95f); nR.offsetMin = Vector2.zero; nR.offsetMax = Vector2.zero;
                    var nameTxt = nameObj.AddComponent<Text>();
                    nameTxt.text = $"{g.name}  Lv.{g.level}";
                    nameTxt.alignment = TextAnchor.MiddleLeft; nameTxt.fontSize = 18; nameTxt.color = UIHelper.Accent; nameTxt.font = _font; nameTxt.raycastTarget = false;

                    var infoObj = new GameObject("Info"); infoObj.transform.SetParent(row.transform, false);
                    var iR = infoObj.AddComponent<RectTransform>(); iR.anchorMin = new Vector2(0.6f, 0.65f); iR.anchorMax = new Vector2(0.98f, 0.95f); iR.offsetMin = Vector2.zero; iR.offsetMax = Vector2.zero;
                    var infoTxt = infoObj.AddComponent<Text>();
                    infoTxt.text = $"{g.memberCount}/{g.maxMembers}人  会长:{g.leaderName}";
                    infoTxt.alignment = TextAnchor.MiddleRight; infoTxt.fontSize = 14; infoTxt.color = UIHelper.TextSecondary; infoTxt.font = _font; infoTxt.raycastTarget = false;

                    var annObj = new GameObject("Announce"); annObj.transform.SetParent(row.transform, false);
                    var aR = annObj.AddComponent<RectTransform>(); aR.anchorMin = new Vector2(0.03f, 0.10f); aR.anchorMax = new Vector2(0.6f, 0.55f); aR.offsetMin = Vector2.zero; aR.offsetMax = Vector2.zero;
                    var annTxt = annObj.AddComponent<Text>();
                    annTxt.text = g.announce;
                    annTxt.alignment = TextAnchor.MiddleLeft; annTxt.fontSize = 13; annTxt.color = UIHelper.TextDim; annTxt.font = _font; annTxt.raycastTarget = false;

                    UIHelper.MakeButton(row.transform, "JoinBtn", "申请加入", _font,
                        new Vector2(0.65f, 0.15f), new Vector2(0.97f, 0.50f), Vector2.zero, Vector2.zero,
                        UIHelper.BtnConfirm, 16, () =>
                        {
                            StartCoroutine(SendJoinRequest(guildId, row, overlay));
                        });
                }
            }
            catch { }
        });

        // 淡入
        float t = 0;
        while (t < 0.2f)
        {
            t += Time.unscaledDeltaTime;
            oCG.alpha = Mathf.Clamp01(t / 0.2f);
            yield return null;
        }
        oCG.alpha = 1f;
        yield return null;
    }

    /// <summary>加入公会 — 玩家作为普通成员加入</summary>
    private void JoinGuild(string guildName, int guildLevel, int currentMembers, int maxMembers, string leaderName, int leaderLevel, string announce)
    {
        PlayerPrefs.SetString(GuildData.KeyGuildName, guildName);
        PlayerPrefs.SetInt(GuildData.KeyGuildLevel, guildLevel);
        PlayerPrefs.SetInt(GuildData.KeyGuildExp, 0);
        PlayerPrefs.SetInt(GuildData.KeyContribution, 0);
        PlayerPrefs.SetInt(GuildData.KeyPlayerRank, 3); // 成员
        PlayerPrefs.SetString(GuildData.KeyGuildAnnounce, announce);

        // 成员列表初始为空，通过邀请加入
        var members = new List<GuildData.GuildMember>();
        SaveMembers(members);
        PlayerPrefs.Save();
        if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("加入成功", $"已加入「{guildName}」Lv.{guildLevel}");
        Show();
    }

    // ========================================================
    //  Tab 导航视图
    // ========================================================
    private void BuildTabbedView()
    {
        // Tab 栏
        string[] tabNames = { "成员", "信息", "Boss", "商店" };
        var tabRow = new GameObject("TabRow");
        tabRow.transform.SetParent(panel.transform, false);
        var tabRowRt = tabRow.AddComponent<RectTransform>();
        tabRowRt.anchorMin = new Vector2(0.1f, 1); tabRowRt.anchorMax = new Vector2(0.9f, 1);
        tabRowRt.pivot = new Vector2(0.5f, 1);
        tabRowRt.offsetMin = new Vector2(0, -50); tabRowRt.offsetMax = new Vector2(0, -10);

        GameObject[] tabBtns = new GameObject[4];
        Image[] tabBtnImgs = new Image[4];
        Text[] tabBtnLabels = new Text[4];

        for (int t = 0; t < 4; t++)
        {
            int tabIdx = t;
            var tabObj = new GameObject($"Tab_{t}");
            tabObj.transform.SetParent(tabRow.transform, false);
            var tbR = tabObj.AddComponent<RectTransform>();
            float w = 1f / 4f;
            tbR.anchorMin = new Vector2(w * t, 0); tbR.anchorMax = new Vector2(w * (t + 1), 1);
            tbR.offsetMin = new Vector2(2, 0); tbR.offsetMax = new Vector2(-2, 0);
            tbR.pivot = new Vector2(0.5f, 0.5f);

            var tbImg = tabObj.AddComponent<Image>();
            tbImg.color = t == 0 ? new Color(0.12f, 0.08f, 0.18f, 0.95f) : new Color(0.04f, 0.03f, 0.06f, 0.6f);
            tabBtnImgs[t] = tbImg;
            var tbBtn = tabObj.AddComponent<Button>();
            tbBtn.transition = Selectable.Transition.None;
            tbBtn.onClick.AddListener(() => { _currentTab = tabIdx; RefreshTab(tabBtnImgs, tabBtnLabels); });

            var tbLbl = new GameObject("Lbl");
            tbLbl.transform.SetParent(tabObj.transform, false);
            var tbLblR = tbLbl.AddComponent<RectTransform>();
            tbLblR.anchorMin = Vector2.zero; tbLblR.anchorMax = Vector2.one; tbLblR.offsetMin = Vector2.zero; tbLblR.offsetMax = Vector2.zero;
            var tbTxt = tbLbl.AddComponent<Text>();
            tbTxt.text = tabNames[t]; tbTxt.alignment = TextAnchor.MiddleCenter;
            tbTxt.fontSize = 16; tbTxt.font = _font;
            tbTxt.color = t == 0 ? UIHelper.TextPrimary : UIHelper.TextDim;
            tbTxt.raycastTarget = false;
            tabBtnLabels[t] = tbTxt;
            tabBtns[t] = tabObj;
        }

        // Tab 内容容器
        _tabContent = new GameObject("TabContent");
        _tabContent.transform.SetParent(panel.transform, false);
        var tcR = _tabContent.AddComponent<RectTransform>();
        tcR.anchorMin = new Vector2(0.05f, 0.05f); tcR.anchorMax = new Vector2(0.95f, 0.82f);
        tcR.offsetMin = Vector2.zero; tcR.offsetMax = Vector2.zero;

        RefreshTab(tabBtnImgs, tabBtnLabels);
    }

    private void RefreshTab(Image[] tabBtnImgs, Text[] tabBtnLabels)
    {
        for (int t = 0; t < 4; t++)
        {
            bool active = t == _currentTab;
            tabBtnImgs[t].color = active ? new Color(0.12f, 0.08f, 0.18f, 0.95f) : new Color(0.04f, 0.03f, 0.06f, 0.6f);
            tabBtnLabels[t].color = active ? UIHelper.TextPrimary : UIHelper.TextDim;
        }

        // 清空旧内容
        for (int i = _tabContent.transform.childCount - 1; i >= 0; i--)
            Destroy(_tabContent.transform.GetChild(i).gameObject);

        switch (_currentTab)
        {
            case 0: BuildMemberTab(); break;
            case 1: BuildInfoTab(); break;
            case 2: BuildBossTab(); break;
            case 3: BuildShopTab(); break;
        }
    }

    // ==== Member/Info tabs — moved to GuildUI.MemberTabs.cs (partial class) ====

    // ==== Boss/Shop/War tabs — moved to GuildUI.Tabs.cs (partial class) ====
    // ========================================================
    //  成员数据 — moved to GuildData.cs (data access layer)
    // ========================================================

    private List<GuildData.GuildMember> GetMembers() => GuildData.GetMembers();
    private void SaveMembers(List<GuildData.GuildMember> members) => GuildData.SaveMembers(members);

    // ========================================================
    //  辅助
    // ========================================================
    private void RefreshTabLast()
    {
        // 仅刷新Tab内容，不重建整个面板
        if (_tabContent == null) return;
        for (int i = _tabContent.transform.childCount - 1; i >= 0; i--)
            Destroy(_tabContent.transform.GetChild(i).gameObject);
        switch (_currentTab)
        {
            case 0: BuildMemberTab(); break;
            case 1: BuildInfoTab(); break;
            case 2: BuildBossTab(); break;
            case 3: BuildShopTab(); break;
        }
    }

    private void OnDestroy() { if (panel != null) Destroy(panel); if (Instance == this) Instance = null; }
}
