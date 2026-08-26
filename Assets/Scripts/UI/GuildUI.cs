using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 公会面板 — 创建/加入公会、成员管理、信息、Boss挑战、商店
/// 纯客户端实现，数据存储在 PlayerPrefs
/// </summary>
public class GuildUI : MonoBehaviour
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

    // ====== PlayerPrefs Keys ======
    private const string KeyGuildName = "ARPG_GuildName";
    private const string KeyGuildLevel = "ARPG_GuildLevel";
    private const string KeyGuildExp = "ARPG_GuildExp";
    private const string KeyGuildAnnounce = "ARPG_GuildAnnounce";
    private const string KeyContribution = "ARPG_GuildContribution";
    private const string KeyPlayerRank = "ARPG_GuildPlayerRank";
    private const string KeyDonateDate = "ARPG_GuildDonateDate";
    private const string KeyDonateCount = "ARPG_GuildDonateCount";
    private const string KeyBossDate = "ARPG_GuildBossDate";
    private const string KeyBossHP = "ARPG_GuildBossHP";
    private const string KeyBossMaxHP = "ARPG_GuildBossMaxHP";
    private const string KeyBossDamage = "ARPG_GuildBossDamage";
    private const string KeyBossDefeated = "ARPG_GuildBossDefeated";
    private const string KeyBossLevel = "ARPG_GuildBossLevel";
    private const string KeyGuildMembers = "ARPG_GuildMembers";
    private const string KeyShopPurchased = "ARPG_GuildShopPurchased";

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

    // ====== HTTP Helpers ======
    private const string ServerUrl = "http://39.107.141.107:5132";

    private IEnumerator GetGuildAPI(string path, System.Action<bool, string> callback)
    {
        string url = $"{ServerUrl}{path}";
        string token = PlayerPrefs.GetString("ARPG_AuthToken", "");
        using (var req = UnityEngine.Networking.UnityWebRequest.Get(url))
        {
            req.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrEmpty(token)) req.SetRequestHeader("Authorization", "Bearer " + token);
            yield return req.SendWebRequest();
            callback(req.result == UnityEngine.Networking.UnityWebRequest.Result.Success, req.downloadHandler?.text ?? "");
        }
    }

    private IEnumerator PostGuildAPI(string path, string jsonBody, System.Action<bool, string> callback)
    {
        string url = $"{ServerUrl}{path}";
        string token = PlayerPrefs.GetString("ARPG_AuthToken", "");
        using (var req = new UnityEngine.Networking.UnityWebRequest(url, "POST"))
        {
            req.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonBody ?? "{}"));
            req.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrEmpty(token)) req.SetRequestHeader("Authorization", "Bearer " + token);
            yield return req.SendWebRequest();
            callback(req.result == UnityEngine.Networking.UnityWebRequest.Result.Success, req.downloadHandler?.text ?? "");
        }
    }

    /// <summary>检查待处理的公会邀请</summary>
    private IEnumerator CheckGuildInvites()
    {
        yield return GetGuildAPI("/api/guild/invites", (ok, resp) =>
        {
            if (!ok || string.IsNullOrEmpty(resp)) return;
            try
            {
                var obj = JsonUtility.FromJson<GuildInvitesResponse>(resp);
                if (obj?.invites != null && obj.invites.Length > 0)
                    StartCoroutine(ShowGuildInvitesPopup(obj.invites));
            }
            catch { }
        });
    }

    [System.Serializable]
    public class GuildInvitesResponse { public GuildInviteData[] invites; public int count; }
    [System.Serializable]
    public class GuildInviteData { public int id; public int guildId; public string guildName; public int guildLevel; public string fromName; public string message; }

    /// <summary>显示公会邀请弹窗</summary>
    private IEnumerator ShowGuildInvitesPopup(GuildInviteData[] invites)
    {
        Canvas canvas = GameManager.EnsureCanvas();
        var overlay = new GameObject("GuildInviteNotice");
        overlay.transform.SetParent(canvas.transform, false);
        var oR = overlay.AddComponent<RectTransform>();
        oR.anchorMin = Vector2.zero; oR.anchorMax = Vector2.one;
        oR.offsetMin = Vector2.zero; oR.offsetMax = Vector2.zero;
        overlay.AddComponent<Image>().color = new Color(0, 0, 0, 0.7f);

        var box = UIHelper.MakeGlowCard(overlay.transform, "Box",
            new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.8f), Vector2.zero, Vector2.zero,
            new Color(0.06f, 0.03f, 0.09f, 0.95f), UIHelper.GlowBottom, UIHelper.Accent);

        var titleObj = new GameObject("Title"); titleObj.transform.SetParent(box.transform, false);
        var tR = titleObj.AddComponent<RectTransform>();
        tR.anchorMin = new Vector2(0, 0.92f); tR.anchorMax = new Vector2(0.85f, 1f);
        tR.offsetMin = Vector2.zero; tR.offsetMax = Vector2.zero;
        var title = titleObj.AddComponent<Text>();
        title.text = "公会邀请"; title.alignment = TextAnchor.MiddleLeft;
        title.fontSize = 22; title.color = UIHelper.Accent; title.font = _font;

        UIHelper.MakeButton(box.transform, "CloseBtn", "X", _font,
            new Vector2(0.88f, 0.93f), new Vector2(0.98f, 0.99f), Vector2.zero, Vector2.zero,
            UIHelper.BtnDanger, 18, () => Destroy(overlay));

        var scrollObj = new GameObject("Scroll");
        scrollObj.transform.SetParent(box.transform, false);
        var sR = scrollObj.AddComponent<RectTransform>();
        sR.anchorMin = new Vector2(0.05f, 0.06f); sR.anchorMax = new Vector2(0.95f, 0.90f);
        sR.offsetMin = Vector2.zero; sR.offsetMax = Vector2.zero;
        scrollObj.AddComponent<Image>().color = new Color(0.03f, 0.02f, 0.05f, 0.5f);
        scrollObj.AddComponent<RectMask2D>();

        float rowH = 80f, gap = 6f;
        for (int i = 0; i < invites.Length; i++)
        {
            var inv = invites[i];
            int reqId = inv.id;
            string gName = inv.guildName;
            int gLv = inv.guildLevel;
            string fromName = inv.fromName;
            float y = -i * (rowH + gap) - 4f;

            var row = UIHelper.MakeGlowCard(scrollObj.transform, $"Invite_{i}",
                new Vector2(0.02f, 1), new Vector2(0.98f, 1),
                new Vector2(0, y - rowH), new Vector2(0, y),
                new Color(0.06f, 0.04f, 0.08f, 0.9f), UIHelper.GlowBottom, UIHelper.BorderSubtle);

            var infoObj = new GameObject("Info"); infoObj.transform.SetParent(row.transform, false);
            var iR = infoObj.AddComponent<RectTransform>();
            iR.anchorMin = new Vector2(0.03f, 0.1f); iR.anchorMax = new Vector2(0.60f, 0.9f);
            iR.offsetMin = Vector2.zero; iR.offsetMax = Vector2.zero;
            var infoTxt = infoObj.AddComponent<Text>();
            infoTxt.text = $"{gName}  Lv.{gLv}\n邀请人: {fromName}";
            infoTxt.alignment = TextAnchor.MiddleLeft; infoTxt.fontSize = 15;
            infoTxt.color = UIHelper.TextPrimary; infoTxt.font = _font;
            infoTxt.supportRichText = true; infoTxt.raycastTarget = false;

            UIHelper.MakeButton(row.transform, "AcceptBtn", "接受", _font,
                new Vector2(0.62f, 0.15f), new Vector2(0.79f, 0.85f), Vector2.zero, Vector2.zero,
                UIHelper.BtnConfirm, 14, () => { StartCoroutine(RespondGuildRequest(reqId, true, overlay)); });
            UIHelper.MakeButton(row.transform, "DeclineBtn", "拒绝", _font,
                new Vector2(0.81f, 0.15f), new Vector2(0.98f, 0.85f), Vector2.zero, Vector2.zero,
                UIHelper.BtnDanger, 14, () => { StartCoroutine(RespondGuildRequest(reqId, false, overlay)); });
        }
        yield return null;
    }

    /// <summary>响应公会邀请/申请</summary>
    private IEnumerator RespondGuildRequest(int requestId, bool accept, GameObject overlay)
    {
        string body = $"{{\"accept\":{(accept ? "true" : "false")}}}";
        yield return PostGuildAPI($"/api/guild/respond/{requestId}", body, (ok, resp) =>
        {
            if (ok)
            {
                if (GameUI.Instance != null)
                    GameUI.Instance.ShowItemPickupToast(accept ? "已加入公会" : "已拒绝", accept ? "欢迎加入！" : "");
                if (accept)
                {
                    // 重新加载公会数据
                    if (overlay != null) Destroy(overlay);
                    Hide();
                    Show();
                }
                else
                {
                    if (overlay != null) Destroy(overlay);
                }
            }
            else
            {
                if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("操作失败", "");
            }
        });
    }

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

    // ====== 数据访问 ======
    private static bool HasGuild => PlayerPrefs.HasKey(KeyGuildName);
    private static string GuildName => PlayerPrefs.GetString(KeyGuildName, "");
    private static int GuildLevel => PlayerPrefs.GetInt(KeyGuildLevel, 1);
    private static int GuildExp => PlayerPrefs.GetInt(KeyGuildExp, 0);
    private static int Contribution => PlayerPrefs.GetInt(KeyContribution, 0);
    /// <summary>玩家在公会中的职位 (0=会长 1=副会长 2=长老 3=成员)</summary>
    private static int PlayerRank => PlayerPrefs.GetInt(KeyPlayerRank, 0);
    private static string GuildAnnounce => PlayerPrefs.GetString(KeyGuildAnnounce, "欢迎来到公会！一起努力变强吧！");
    private static int GuildMaxMembers => 10 + GuildLevel * 5;
    private static int GuildExpToNext => 1000 + GuildLevel * 500;

    private void BuildUI()
    {
        _font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();

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
        PlayerPrefs.SetString(KeyGuildName, guildName);
        PlayerPrefs.SetInt(KeyGuildLevel, 1);
        PlayerPrefs.SetInt(KeyGuildExp, 0);
        PlayerPrefs.SetInt(KeyContribution, 0);
        PlayerPrefs.SetInt(KeyPlayerRank, 0); // 会长
        PlayerPrefs.SetString(KeyGuildAnnounce, $"欢迎加入「{guildName}」！一起努力变强吧！");
        // 创建公会时只添加会长(自己)，成员通过邀请加入
        var members = new List<GuildMember>();
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
        PlayerPrefs.SetString(KeyGuildName, guildName);
        PlayerPrefs.SetInt(KeyGuildLevel, guildLevel);
        PlayerPrefs.SetInt(KeyGuildExp, 0);
        PlayerPrefs.SetInt(KeyContribution, 0);
        PlayerPrefs.SetInt(KeyPlayerRank, 3); // 成员
        PlayerPrefs.SetString(KeyGuildAnnounce, announce);

        // 成员列表初始为空，通过邀请加入
        var members = new List<GuildMember>();
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

    // ========================================================
    //  Tab 0: 成员管理
    // ========================================================
    private void BuildMemberTab()
    {
        var members = GetMembers();
        // 按职位排序：会长(0)→副会长(1)→长老(2)→成员(3)
        members.Sort((a, b) => a.rank.CompareTo(b.rank));
        var player = GameManager.Instance?.Player;

        // 公会公告
        var announceArea = UIHelper.MakeGlowCard(_tabContent.transform, "Announce",
            new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(0, -50), new Vector2(0, 0),
            new Color(0.06f, 0.04f, 0.08f, 0.5f), UIHelper.GlowBottom, UIHelper.BorderSubtle);
        var annTxt = new GameObject("Txt"); annTxt.transform.SetParent(announceArea.transform, false);
        var annR = annTxt.AddComponent<RectTransform>(); annR.anchorMin = new Vector2(0.05f, 0); annR.anchorMax = new Vector2(0.75f, 1); annR.offsetMin = new Vector2(8, 4); annR.offsetMax = new Vector2(-8, -4);
        var annLabel = annTxt.AddComponent<Text>();
        annLabel.text = $"{GuildAnnounce}";
        annLabel.alignment = TextAnchor.MiddleLeft; annLabel.fontSize = 14; annLabel.color = UIHelper.TextSecondary; annLabel.font = _font; annLabel.supportRichText = true;

        if (CanEditAnnounce)
        {
            UIHelper.MakeButton(announceArea.transform, "EditBtn", "✎", _font,
                new Vector2(0.75f, 0.1f), new Vector2(0.95f, 0.9f), Vector2.zero, Vector2.zero,
                UIHelper.BtnPrimary, 16, () => StartCoroutine(EditAnnouncement()));
        }

        // 成员列表
        int total = members.Count + 1; // +1 for player
        int start = _memberPage * MembersPerPage;
        int end = Mathf.Min(start + MembersPerPage, total);
        int count = end - start;
        float rowH = 50f, gap = 4f;
        float startY = -10f;

        for (int i = start; i < end; i++)
        {
            int slot = i - start;
            float y = startY - slot * (rowH + gap);

            string name; int cls, level, contribution, rank; bool online;
            if (i == 0)
            {
                name = PlayerProgressData.GetSavedCharacterName();
                if (string.IsNullOrEmpty(name)) name = CloudSaveManager.Instance?.CurrentUsername ?? "冒险者";
                cls = (int)(player?.HeroClass ?? HeroClass.Warrior);
                level = player?.Stats?.Level ?? 1;
                contribution = Contribution;
                rank = 0; // 会长
                online = true;
            }
            else
            {
                var m = members[i - 1];
                name = m.name; cls = m.cls; level = m.level;
                contribution = m.contribution; rank = m.rank; online = m.online;
            }

            var row = UIHelper.MakeGlowCard(_tabContent.transform, $"Member_{i}",
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, y - rowH), new Vector2(0, y),
                new Color(0.06f, 0.04f, 0.08f, 0.92f), UIHelper.GlowBottom, UIHelper.BorderSubtle);

            var txtObj = new GameObject("Lbl"); txtObj.transform.SetParent(row.transform, false);
            var txtR = txtObj.AddComponent<RectTransform>(); txtR.anchorMin = Vector2.zero; txtR.anchorMax = new Vector2(0.7f, 1); txtR.offsetMin = new Vector2(10, 4); txtR.offsetMax = new Vector2(-4, -4);
            var txt = txtObj.AddComponent<Text>();
            int clsIdx = Mathf.Clamp(cls, 0, 2);
            string classColor = ColorUtility.ToHtmlStringRGBA(ClassColors[clsIdx]);
            string rankColor = ColorUtility.ToHtmlStringRGBA(RankColors[rank]);
            string statusIcon = online ? "<color=#55FF55>●</color>" : "<color=#555>○</color>";
            txt.text = $"{statusIcon} <color=#{rankColor}>[{RankNames[rank]}]</color> <color=#{classColor}>{ClassNames[clsIdx]}</color> {name}  Lv.{level}  贡献:{contribution}";
            txt.alignment = TextAnchor.MiddleLeft; txt.fontSize = 14; txt.color = UIHelper.TextPrimary; txt.font = _font; txt.supportRichText = true; txt.raycastTarget = false;

            // 踢人按钮（根据权限）
            if (i > 0)
            {
                int targetRank = rank;
                if (CanKick(targetRank))
                {
                    UIHelper.MakeButton(row.transform, "KickBtn", "踢", _font,
                        new Vector2(0.80f, 0.1f), new Vector2(0.96f, 0.9f), Vector2.zero, Vector2.zero,
                        UIHelper.BtnDanger, 12, () =>
                        {
                            members.RemoveAt(i - 1);
                            SaveMembers(members);
                            RefreshTabLast();
                        });
                }
            }
        }

        // 分页
        int totalPages = Mathf.Max(1, Mathf.CeilToInt((float)total / MembersPerPage));
        var pageRow = new GameObject("PageRow"); pageRow.transform.SetParent(_tabContent.transform, false);
        var pgr = pageRow.AddComponent<RectTransform>();
        pgr.anchorMin = new Vector2(0.5f, 0); pgr.anchorMax = new Vector2(0.5f, 0);
        pgr.pivot = new Vector2(0.5f, 0);
        pgr.offsetMin = new Vector2(-150, 5); pgr.offsetMax = new Vector2(150, 55);

        UIHelper.MakeButton(pageRow.transform, "PrevBtn", "◀ 上一页", _font,
            new Vector2(0, 0), new Vector2(0.4f, 1), Vector2.zero, Vector2.zero,
            new Color(0.08f, 0.08f, 0.12f, 0.9f), 13, () => { if (_memberPage > 0) { _memberPage--; RefreshTabLast(); } });

        _pageText = new GameObject("PageText").AddComponent<Text>();
        _pageText.transform.SetParent(pageRow.transform, false);
        var ptr = _pageText.GetComponent<RectTransform>();
        ptr.anchorMin = new Vector2(0.4f, 0); ptr.anchorMax = new Vector2(0.6f, 1); ptr.offsetMin = Vector2.zero; ptr.offsetMax = Vector2.zero;
        _pageText.alignment = TextAnchor.MiddleCenter; _pageText.fontSize = 14; _pageText.color = UIHelper.TextSecondary; _pageText.font = _font;
        _pageText.text = $"{_memberPage + 1} / {totalPages}";

        UIHelper.MakeButton(pageRow.transform, "NextBtn", "下一页 ▶", _font,
            new Vector2(0.6f, 0), new Vector2(1f, 1), Vector2.zero, Vector2.zero,
            new Color(0.08f, 0.08f, 0.12f, 0.9f), 13, () => { if (_memberPage < totalPages - 1) { _memberPage++; RefreshTabLast(); } });

        // 底部按钮行：邀请/任命/转让/退出 — 固定位置
        float btnY = 75, btnH = 70, btnW = 180;
        float[] btnXs = { -381, -191, -1, 189 }; // InviteBtn, AppointBtn, TransferBtn, LeaveBtn offsetMin.x
        int btnIdx = 0;

        // 邀请成员 (会长/副会长/长老可见)
        if (PlayerRank <= 2)
        {
            float x = btnXs[btnIdx];
            var inviteBtn = new GameObject("InviteBtn"); inviteBtn.transform.SetParent(_tabContent.transform, false);
            var ir = inviteBtn.AddComponent<RectTransform>();
            ir.anchorMin = new Vector2(0.5f, 0); ir.anchorMax = new Vector2(0.5f, 0);
            ir.pivot = new Vector2(0.5f, 0);
            ir.offsetMin = new Vector2(x, btnY); ir.offsetMax = new Vector2(x + btnW, btnY + btnH);
            inviteBtn.AddComponent<Image>().color = UIHelper.BtnConfirm;
            var iBtn = inviteBtn.AddComponent<Button>();
            var iLbl = new GameObject("Lbl"); iLbl.transform.SetParent(inviteBtn.transform, false);
            var ilR = iLbl.AddComponent<RectTransform>(); ilR.anchorMin = Vector2.zero; ilR.anchorMax = Vector2.one; ilR.offsetMin = Vector2.zero; ilR.offsetMax = Vector2.zero;
            var iTxt = iLbl.AddComponent<Text>(); iTxt.text = "邀请"; iTxt.alignment = TextAnchor.MiddleCenter; iTxt.fontSize = 14; iTxt.color = UIHelper.TextPrimary; iTxt.font = _font; iTxt.raycastTarget = false;
            iBtn.onClick.AddListener(() => StartCoroutine(ShowGuildInvitePopup(members)));
            btnIdx++;
        }

        // 任命职位（会长+副会长可见）
        if (PlayerRank <= 1)
        {
            float x = btnXs[btnIdx];
            var appointBtn = new GameObject("AppointBtn"); appointBtn.transform.SetParent(_tabContent.transform, false);
            var apR = appointBtn.AddComponent<RectTransform>();
            apR.anchorMin = new Vector2(0.5f, 0); apR.anchorMax = new Vector2(0.5f, 0);
            apR.pivot = new Vector2(0.5f, 0);
            apR.offsetMin = new Vector2(x, btnY); apR.offsetMax = new Vector2(x + btnW, btnY + btnH);
            appointBtn.AddComponent<Image>().color = new Color(0.15f, 0.25f, 0.5f, 0.9f);
            var apBtn = appointBtn.AddComponent<Button>();
            var apLbl = new GameObject("Lbl"); apLbl.transform.SetParent(appointBtn.transform, false);
            var aplR = apLbl.AddComponent<RectTransform>(); aplR.anchorMin = Vector2.zero; aplR.anchorMax = Vector2.one; aplR.offsetMin = Vector2.zero; aplR.offsetMax = Vector2.zero;
            var apTxt = apLbl.AddComponent<Text>(); apTxt.text = "任命"; apTxt.alignment = TextAnchor.MiddleCenter; apTxt.fontSize = 14; apTxt.color = UIHelper.TextPrimary; apTxt.font = _font; apTxt.raycastTarget = false;
            apBtn.onClick.AddListener(() => StartCoroutine(ShowAppointPopup(members)));
            btnIdx++;
        }

        // 转让会长（仅会长）
        if (PlayerRank == 0 && members.Count > 0)
        {
            float x = btnXs[btnIdx];
            var transferBtn = new GameObject("TransferBtn"); transferBtn.transform.SetParent(_tabContent.transform, false);
            var tfR = transferBtn.AddComponent<RectTransform>();
            tfR.anchorMin = new Vector2(0.5f, 0); tfR.anchorMax = new Vector2(0.5f, 0);
            tfR.pivot = new Vector2(0.5f, 0);
            tfR.offsetMin = new Vector2(x, btnY); tfR.offsetMax = new Vector2(x + btnW, btnY + btnH);
            transferBtn.AddComponent<Image>().color = new Color(0.2f, 0.3f, 0.6f, 0.9f);
            var tfBtn = transferBtn.AddComponent<Button>();
            var tfLbl = new GameObject("Lbl"); tfLbl.transform.SetParent(transferBtn.transform, false);
            var tflR = tfLbl.AddComponent<RectTransform>(); tflR.anchorMin = Vector2.zero; tflR.anchorMax = Vector2.one; tflR.offsetMin = Vector2.zero; tflR.offsetMax = Vector2.zero;
            var tfTxt = tfLbl.AddComponent<Text>(); tfTxt.text = "转让"; tfTxt.alignment = TextAnchor.MiddleCenter; tfTxt.fontSize = 14; tfTxt.color = UIHelper.TextPrimary; tfTxt.font = _font; tfTxt.raycastTarget = false;
            tfBtn.onClick.AddListener(() =>
            {
                members[0] = new GuildMember { name = members[0].name, cls = members[0].cls, level = members[0].level, contribution = members[0].contribution, rank = 0, online = members[0].online };
                PlayerPrefs.SetInt(KeyPlayerRank, 3);
                SaveMembers(members);
                if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("转让成功", $"会长已转让给 {members[0].name}");
                RefreshTabLast();
            });
            btnIdx++;
        }

        // 退出/解散
        {
            float x = btnXs[btnIdx];
            var leaveBtn = new GameObject("LeaveBtn"); leaveBtn.transform.SetParent(_tabContent.transform, false);
            var lvR = leaveBtn.AddComponent<RectTransform>();
            lvR.anchorMin = new Vector2(0.5f, 0); lvR.anchorMax = new Vector2(0.5f, 0);
            lvR.pivot = new Vector2(0.5f, 0);
            lvR.offsetMin = new Vector2(x, btnY); lvR.offsetMax = new Vector2(x + btnW, btnY + btnH);
            leaveBtn.AddComponent<Image>().color = UIHelper.BtnDanger;
            var lBtn = leaveBtn.AddComponent<Button>();
            var lLbl = new GameObject("Lbl"); lLbl.transform.SetParent(leaveBtn.transform, false);
            var llR = lLbl.AddComponent<RectTransform>(); llR.anchorMin = Vector2.zero; llR.anchorMax = Vector2.one; llR.offsetMin = Vector2.zero; llR.offsetMax = Vector2.zero;
            var lTxt = lLbl.AddComponent<Text>();
            lTxt.text = PlayerRank == 0 ? "解散公会" : "退出公会";
            lTxt.alignment = TextAnchor.MiddleCenter; lTxt.fontSize = 14; lTxt.color = UIHelper.TextPrimary; lTxt.font = _font; lTxt.raycastTarget = false;
            lBtn.onClick.AddListener(() => {
                PlayerPrefs.DeleteKey(KeyGuildName); PlayerPrefs.DeleteKey(KeyGuildLevel); PlayerPrefs.DeleteKey(KeyGuildExp);
                PlayerPrefs.DeleteKey(KeyGuildAnnounce); PlayerPrefs.DeleteKey(KeyContribution);
                PlayerPrefs.DeleteKey(KeyGuildMembers); PlayerPrefs.DeleteKey(KeyPlayerRank);
                PlayerPrefs.Save();
                if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast(PlayerRank == 0 ? "公会已解散" : "已退出公会", "");
                Show();
            });
        }
    }

    /// <summary>任命弹窗 — 显示成员列表，可设置职位</summary>
    private IEnumerator ShowAppointPopup(List<GuildMember> members)
    {
        Canvas canvas = GameManager.EnsureCanvas();

        // 全屏遮罩
        var overlay = new GameObject("AppointPopup");
        overlay.transform.SetParent(canvas.transform, false);
        var oR = overlay.AddComponent<RectTransform>();
        oR.anchorMin = Vector2.zero; oR.anchorMax = Vector2.one; oR.offsetMin = Vector2.zero; oR.offsetMax = Vector2.zero;
        overlay.AddComponent<Image>().color = new Color(0, 0, 0, 0.7f);
        var oCG = overlay.AddComponent<CanvasGroup>();

        // 弹窗
        var box = UIHelper.MakeGlowCard(overlay.transform, "Box",
            new Vector2(0.15f, 0.1f), new Vector2(0.85f, 0.9f), Vector2.zero, Vector2.zero,
            new Color(0.06f, 0.05f, 0.10f, 0.98f), UIHelper.GlowTop, UIHelper.Accent);

        // 标题
        var titleObj = new GameObject("Title"); titleObj.transform.SetParent(box.transform, false);
        var tR = titleObj.AddComponent<RectTransform>(); tR.anchorMin = new Vector2(0, 0.92f); tR.anchorMax = new Vector2(0.85f, 1f); tR.offsetMin = Vector2.zero; tR.offsetMax = Vector2.zero;
        var title = titleObj.AddComponent<Text>(); title.text = "任命职位"; title.alignment = TextAnchor.MiddleLeft; title.fontSize = 22; title.color = UIHelper.Accent; title.font = _font;

        // 关闭按钮
        UIHelper.MakeButton(box.transform, "CloseBtn", "✕", _font,
            new Vector2(0.88f, 0.93f), new Vector2(0.98f, 0.99f), Vector2.zero, Vector2.zero,
            UIHelper.BtnDanger, 18, () => Destroy(overlay));

        // 滚动列表
        var scrollObj = new GameObject("ScrollArea");
        scrollObj.transform.SetParent(box.transform, false);
        var saR = scrollObj.AddComponent<RectTransform>();
        saR.anchorMin = new Vector2(0.05f, 0.06f); saR.anchorMax = new Vector2(0.95f, 0.90f); saR.offsetMin = Vector2.zero; saR.offsetMax = Vector2.zero;
        scrollObj.AddComponent<Image>().color = new Color(0.03f, 0.02f, 0.05f, 0.5f);
        var scroll = scrollObj.AddComponent<ScrollRect>();
        scroll.scrollSensitivity = 20f;

        var vpObj = new GameObject("Viewport");
        vpObj.transform.SetParent(scrollObj.transform, false);
        var vpR = vpObj.AddComponent<RectTransform>();
        vpR.anchorMin = Vector2.zero; vpR.anchorMax = Vector2.one; vpR.offsetMin = Vector2.zero; vpR.offsetMax = Vector2.zero;
        vpObj.AddComponent<RectMask2D>();
        vpObj.AddComponent<Image>().color = Color.clear;

        var contentObj = new GameObject("Content");
        contentObj.transform.SetParent(vpObj.transform, false);
        var cR = contentObj.AddComponent<RectTransform>();
        cR.anchorMin = new Vector2(0, 1); cR.anchorMax = Vector2.one; cR.pivot = new Vector2(0.5f, 1);
        cR.offsetMin = Vector2.zero; cR.offsetMax = Vector2.zero;

        scroll.content = cR;
        scroll.viewport = vpR;

        // 生成成员行
        int memberCount = members.Count;
        float rowH = 80f, gap = 8f;
        float totalH = memberCount * rowH + (memberCount - 1) * gap + 16f;
        cR.sizeDelta = new Vector2(0, totalH);

        for (int i = 0; i < memberCount; i++)
        {
            int idx = i;
            var m = members[i];
            float y = -8f - i * (rowH + gap);

            var row = UIHelper.MakeGlowCard(contentObj.transform, $"Member_{i}",
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(8, y - rowH), new Vector2(-8, y),
                new Color(0.06f, 0.04f, 0.08f, 0.92f), UIHelper.GlowBottom, UIHelper.BorderSubtle);

            // 成员信息
            var infoObj = new GameObject("Info"); infoObj.transform.SetParent(row.transform, false);
            var iR = infoObj.AddComponent<RectTransform>(); iR.anchorMin = new Vector2(0.03f, 0); iR.anchorMax = new Vector2(0.45f, 1); iR.offsetMin = new Vector2(8, 4); iR.offsetMax = new Vector2(-4, -4);
            var info = infoObj.AddComponent<Text>();
            int clsIdx = Mathf.Clamp(m.cls, 0, 2);
            string classColor = ColorUtility.ToHtmlStringRGBA(ClassColors[clsIdx]);
            string rankColor = ColorUtility.ToHtmlStringRGBA(RankColors[m.rank]);
            info.text = $"<color=#{rankColor}>[{RankNames[m.rank]}]</color> <color=#{classColor}>{ClassNames[clsIdx]}</color> {m.name}  Lv.{m.level}";
            info.alignment = TextAnchor.MiddleLeft; info.fontSize = 15; info.color = UIHelper.TextPrimary; info.font = _font; info.supportRichText = true; info.raycastTarget = false;

            // 当前职位标签
            var curRankObj = new GameObject("CurRank"); curRankObj.transform.SetParent(row.transform, false);
            var crR = curRankObj.AddComponent<RectTransform>(); crR.anchorMin = new Vector2(0.45f, 0.15f); crR.anchorMax = new Vector2(0.62f, 0.85f); crR.offsetMin = Vector2.zero; crR.offsetMax = Vector2.zero;
            var curTxt = curRankObj.AddComponent<Text>();
            curTxt.text = $"当前: {RankNames[m.rank]}";
            curTxt.alignment = TextAnchor.MiddleCenter; curTxt.fontSize = 13; curTxt.color = UIHelper.TextSecondary; curTxt.font = _font; curTxt.raycastTarget = false;

            // 职位选择按钮（根据权限决定可选范围）
            // 会长: 可设置副会长/长老/成员
            // 副会长: 可设置长老/成员
            int minRank = PlayerRank == 0 ? 1 : 2;
            int maxRank = 3;
            for (int r = minRank; r <= maxRank; r++)
            {
                int targetR = r;
                bool canSet = PlayerRank == 0 ? true : (targetR >= 2); // 副会长只能设长老/成员
                if (!canSet) continue;

                float btnW2 = 0.11f;
                float btnX2 = 0.64f + (r - minRank) * (btnW2 + 0.01f);
                var btnColor = targetR == m.rank ? UIHelper.Accent : new Color(0.1f, 0.1f, 0.15f, 0.9f);

                UIHelper.MakeButton(row.transform, $"Rank_{r}", RankNames[targetR], _font,
                    new Vector2(btnX2, 0.15f), new Vector2(btnX2 + btnW2, 0.85f), Vector2.zero, Vector2.zero,
                    btnColor, 12, () =>
                    {
                        if (targetR == m.rank) return;
                        members[idx] = new GuildMember
                        {
                            name = m.name, cls = m.cls, level = m.level,
                            contribution = m.contribution, rank = targetR, online = m.online,
                        };
                        SaveMembers(members);
                        if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("任命成功", $"{m.name} → {RankNames[targetR]}");
                        Destroy(overlay);
                        RefreshTabLast();
                    });
            }
        }

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

    private IEnumerator EditAnnouncement()
    {
        // 简易编辑：弹出输入框
        // 由于Tuanjie引擎InputField可能不稳定，使用循环弹窗
        string[] presets = {
            "欢迎来到公会！一起努力变强吧！",
            "每日捐献+Boss挑战，冲冲冲！",
            "有困难找会长，大家一起帮忙！",
            "本周目标：公会Boss击杀！全员参与！",
            "新人先看公告，有问题问副会长。",
        };
        // 简单选择方式：点击切换
        int idx = 0;
        // 弹一个简单覆盖层
        Canvas canvas = GameManager.EnsureCanvas();
        var overlay = new GameObject("AnnounceEdit");
        overlay.transform.SetParent(canvas.transform, false);
        var or = overlay.AddComponent<RectTransform>();
        or.anchorMin = Vector2.zero; or.anchorMax = Vector2.one; or.offsetMin = Vector2.zero; or.offsetMax = Vector2.zero;
        or.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.7f);
        var cg = overlay.AddComponent<CanvasGroup>();

        var box = UIHelper.MakeGlowCard(overlay.transform, "Box",
            new Vector2(0.2f, 0.3f), new Vector2(0.8f, 0.7f), Vector2.zero, Vector2.zero,
            UIHelper.GlowTop, UIHelper.GlowBottom, UIHelper.Accent);

        var titleObj = new GameObject("Title"); titleObj.transform.SetParent(box.transform, false);
        var ttr = titleObj.AddComponent<RectTransform>(); ttr.anchorMin = new Vector2(0, 0.75f); ttr.anchorMax = new Vector2(1, 1f); ttr.offsetMin = Vector2.zero; ttr.offsetMax = Vector2.zero;
        var title = titleObj.AddComponent<Text>(); title.text = "编辑公会公告"; title.alignment = TextAnchor.MiddleCenter; title.fontSize = 20; title.color = UIHelper.Accent; title.font = _font;

        var displayObj = new GameObject("Display"); displayObj.transform.SetParent(box.transform, false);
        var drR = displayObj.AddComponent<RectTransform>(); drR.anchorMin = new Vector2(0.05f, 0.4f); drR.anchorMax = new Vector2(0.95f, 0.7f); drR.offsetMin = Vector2.zero; drR.offsetMax = Vector2.zero;
        var display = displayObj.AddComponent<Text>(); display.alignment = TextAnchor.MiddleCenter; display.fontSize = 16; display.color = UIHelper.TextPrimary; display.font = _font; display.text = presets[0];

        var prevBtn = UIHelper.MakeButton(box.transform, "Prev", "◀", _font,
            new Vector2(0.05f, 0.1f), new Vector2(0.25f, 0.3f), Vector2.zero, Vector2.zero,
            UIHelper.BtnPrimary, 16, () => { idx = (idx + presets.Length - 1) % presets.Length; display.text = presets[idx]; });

        var okBtn = UIHelper.MakeButton(box.transform, "OK", "确认", _font,
            new Vector2(0.3f, 0.1f), new Vector2(0.7f, 0.3f), Vector2.zero, Vector2.zero,
            UIHelper.BtnConfirm, 16, () => {
                PlayerPrefs.SetString(KeyGuildAnnounce, presets[idx]); PlayerPrefs.Save();
                Destroy(overlay); Show();
            });

        var nextBtn = UIHelper.MakeButton(box.transform, "Next", "▶", _font,
            new Vector2(0.75f, 0.1f), new Vector2(0.95f, 0.3f), Vector2.zero, Vector2.zero,
            UIHelper.BtnPrimary, 16, () => { idx = (idx + 1) % presets.Length; display.text = presets[idx]; });

        yield return null;
    }

    // ========================================================
    //  Tab 1: 信息
    // ========================================================
    private void BuildInfoTab()
    {
        // 公会信息卡片
        var infoCard = UIHelper.MakeGlowCard(_tabContent.transform, "InfoCard",
            new Vector2(0.05f, 0.70f), new Vector2(0.95f, 0.98f), Vector2.zero, Vector2.zero,
            new Color(0.06f, 0.04f, 0.08f, 0.92f), UIHelper.GlowTop, UIHelper.Accent);

        var titleObj = new GameObject("Title"); titleObj.transform.SetParent(infoCard.transform, false);
        var tR = titleObj.AddComponent<RectTransform>(); tR.anchorMin = new Vector2(0.05f, 0.70f); tR.anchorMax = new Vector2(0.95f, 0.95f); tR.offsetMin = Vector2.zero; tR.offsetMax = Vector2.zero;
        var title = titleObj.AddComponent<Text>();
        title.text = $"{GuildName}  Lv.{GuildLevel}";
        title.alignment = TextAnchor.MiddleCenter; title.fontSize = 20; title.color = UIHelper.Accent; title.font = _font;

        var statsObj = new GameObject("Stats"); statsObj.transform.SetParent(infoCard.transform, false);
        var sR = statsObj.AddComponent<RectTransform>(); sR.anchorMin = new Vector2(0.05f, 0.25f); sR.anchorMax = new Vector2(0.95f, 0.65f); sR.offsetMin = Vector2.zero; sR.offsetMax = Vector2.zero;
        var stats = statsObj.AddComponent<Text>();
        var members = GetMembers();
        int onlineCount = 1; // player
        foreach (var m in members) if (m.online) onlineCount++;
        stats.text = $"公会经验: {GuildExp}/{GuildExpToNext}\n成员: {members.Count + 1}/{GuildMaxMembers}  在线: {onlineCount}\n 贡献点: {Contribution}  职位: {RankNames[PlayerRank]}\n每日签到加成: +{DailyBonus*100:F0}%";
        stats.alignment = TextAnchor.MiddleCenter; stats.fontSize = 15; stats.color = UIHelper.TextPrimary; stats.font = _font; stats.supportRichText = true;

        // 公会公告
        var announceCard = UIHelper.MakeGlowCard(_tabContent.transform, "AnnounceCard",
            new Vector2(0.05f, 0.40f), new Vector2(0.95f, 0.68f), Vector2.zero, Vector2.zero,
            new Color(0.06f, 0.04f, 0.08f, 0.92f), UIHelper.GlowBottom, UIHelper.BorderSubtle);

        var annTitle = new GameObject("AnnTitle"); annTitle.transform.SetParent(announceCard.transform, false);
        var aR = annTitle.AddComponent<RectTransform>(); aR.anchorMin = new Vector2(0.05f, 0.65f); aR.anchorMax = new Vector2(0.75f, 0.95f); aR.offsetMin = Vector2.zero; aR.offsetMax = Vector2.zero;
        var annT = annTitle.AddComponent<Text>();
        annT.text = "公会公告"; annT.alignment = TextAnchor.MiddleLeft; annT.fontSize = 16; annT.color = UIHelper.Accent; annT.font = _font;

        if (CanEditAnnounce)
        {
            UIHelper.MakeButton(announceCard.transform, "EditBtn", "✎ 编辑", _font,
                new Vector2(0.72f, 0.65f), new Vector2(0.95f, 0.95f), Vector2.zero, Vector2.zero,
                UIHelper.BtnPrimary, 12, () => StartCoroutine(EditAnnouncement()));
        }

        var annContent = new GameObject("AnnContent"); annContent.transform.SetParent(announceCard.transform, false);
        var acR = annContent.AddComponent<RectTransform>(); acR.anchorMin = new Vector2(0.05f, 0.05f); acR.anchorMax = new Vector2(0.95f, 0.60f); acR.offsetMin = Vector2.zero; acR.offsetMax = Vector2.zero;
        var annTxt = annContent.AddComponent<Text>();
        annTxt.text = GuildAnnounce;
        annTxt.alignment = TextAnchor.MiddleLeft; annTxt.fontSize = 14; annTxt.color = UIHelper.TextSecondary; annTxt.font = _font; annTxt.supportRichText = true;

        // 邀请成员卡片 (会长/副会长/长老可见)
        if (PlayerRank <= 2)
        {
        var inviteCard = UIHelper.MakeGlowCard(_tabContent.transform, "InviteCard",
            new Vector2(0.05f, 0.10f), new Vector2(0.95f, 0.38f), Vector2.zero, Vector2.zero,
            new Color(0.06f, 0.04f, 0.08f, 0.92f), UIHelper.GlowBottom, UIHelper.BorderSubtle);

        var inviteTitle = new GameObject("Title"); inviteTitle.transform.SetParent(inviteCard.transform, false);
        var itR = inviteTitle.AddComponent<RectTransform>(); itR.anchorMin = new Vector2(0.05f, 0.55f); itR.anchorMax = new Vector2(0.95f, 0.90f); itR.offsetMin = Vector2.zero; itR.offsetMax = Vector2.zero;
        var itTxt = inviteTitle.AddComponent<Text>();
        itTxt.text = "邀请新成员"; itTxt.alignment = TextAnchor.MiddleLeft; itTxt.fontSize = 16; itTxt.color = UIHelper.Accent; itTxt.font = _font;

        var inviteDesc = new GameObject("Desc"); inviteDesc.transform.SetParent(inviteCard.transform, false);
        var idR = inviteDesc.AddComponent<RectTransform>(); idR.anchorMin = new Vector2(0.05f, 0.30f); idR.anchorMax = new Vector2(0.55f, 0.50f); idR.offsetMin = Vector2.zero; idR.offsetMax = Vector2.zero;
        var idTxt = inviteDesc.AddComponent<Text>();
        idTxt.text = $"当前: {members.Count + 1}/{GuildMaxMembers}人";
        idTxt.alignment = TextAnchor.MiddleLeft; idTxt.fontSize = 13; idTxt.color = UIHelper.TextSecondary; idTxt.font = _font;

        var inviteBtn = UIHelper.MakeButton(inviteCard.transform, "Btn", "邀请成员", _font,
            new Vector2(0.55f, 0.10f), new Vector2(0.95f, 0.50f), Vector2.zero, Vector2.zero,
            UIHelper.BtnConfirm, 14, () =>
            {
                var m = GetMembers();
                StartCoroutine(ShowGuildInvitePopup(m));
                });
                } // end if PlayerRank <= 2
            }

            // ========================================================
            //  公会邀请弹窗 — 选择好友/最近玩家邀请加入公会
            // ========================================================
            private IEnumerator ShowGuildInvitePopup(List<GuildMember> members)
    {
        Canvas canvas = GameManager.EnsureCanvas();
        var overlay = new GameObject("GuildInvitePopup");
        overlay.transform.SetParent(canvas.transform, false);
        var oR = overlay.AddComponent<RectTransform>();
        oR.anchorMin = Vector2.zero; oR.anchorMax = Vector2.one;
        oR.offsetMin = Vector2.zero; oR.offsetMax = Vector2.zero;
        overlay.AddComponent<Image>().color = new Color(0, 0, 0, 0.7f);
        overlay.AddComponent<CanvasGroup>();

        var box = UIHelper.MakeGlowCard(overlay.transform, "Box",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-360, -360), new Vector2(360, 360),
            new Color(0.06f, 0.03f, 0.09f, 0.95f),
            UIHelper.GlowBottom, UIHelper.Accent);

        var titleObj = new GameObject("Title");
        titleObj.transform.SetParent(box.transform, false);
        var tR = titleObj.AddComponent<RectTransform>();
        tR.anchorMin = new Vector2(0, 1); tR.anchorMax = new Vector2(1, 1);
        tR.pivot = new Vector2(0.5f, 1); tR.anchoredPosition = new Vector2(0, -12);
        tR.sizeDelta = new Vector2(0, 32);
        var titleTxt = titleObj.AddComponent<Text>();
        titleTxt.text = "邀请好友加入公会";
        titleTxt.alignment = TextAnchor.MiddleCenter;
        titleTxt.fontSize = 22; titleTxt.color = UIHelper.Accent; titleTxt.font = _font;

        UIHelper.MakeButton(box.transform, "CloseBtn", "X", _font,
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(-44, -40), new Vector2(-8, -8),
            new Color(0.4f, 0.1f, 0.1f, 0.9f), 18, () => Destroy(overlay));

        var scrollObj = new GameObject("ScrollArea");
        scrollObj.transform.SetParent(box.transform, false);
        var sR = scrollObj.AddComponent<RectTransform>();
        sR.anchorMin = new Vector2(0.05f, 0.12f); sR.anchorMax = new Vector2(0.95f, 0.88f);
        sR.offsetMin = Vector2.zero; sR.offsetMax = Vector2.zero;
        scrollObj.AddComponent<Image>().color = new Color(0.03f, 0.02f, 0.05f, 0.5f);

        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollObj.transform, false);
        var vpR = viewport.AddComponent<RectTransform>();
        vpR.anchorMin = Vector2.zero; vpR.anchorMax = Vector2.one;
        vpR.offsetMin = Vector2.zero; vpR.offsetMax = Vector2.zero;
        viewport.AddComponent<RectMask2D>();

        var content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        var cR = content.AddComponent<RectTransform>();
        cR.anchorMin = new Vector2(0, 1); cR.anchorMax = new Vector2(1, 1);
        cR.pivot = new Vector2(0.5f, 1);
        cR.offsetMin = Vector2.zero; cR.offsetMax = Vector2.zero;

        // 加载提示
        var loadingObj = new GameObject("Loading");
        loadingObj.transform.SetParent(viewport.transform, false);
        var lR = loadingObj.AddComponent<RectTransform>();
        lR.anchorMin = new Vector2(0, 0.4f); lR.anchorMax = new Vector2(1, 0.6f);
        lR.offsetMin = Vector2.zero; lR.offsetMax = Vector2.zero;
        var lTxt = loadingObj.AddComponent<Text>();
        lTxt.text = "加载好友列表..."; lTxt.alignment = TextAnchor.MiddleCenter;
        lTxt.fontSize = 18; lTxt.color = UIHelper.TextDim; lTxt.font = _font;

        // 从服务器获取好友列表
        yield return GetGuildAPI("/api/friend/list", (ok, resp) =>
        {
            Destroy(loadingObj);
            if (!ok || string.IsNullOrEmpty(resp))
            {
                lTxt.text = "加载失败，请先登录";
                return;
            }
            try
            {
                var friendResp = JsonUtility.FromJson<ServerFriendListResponse>(resp);
                if (friendResp?.friends == null || friendResp.friends.Length == 0)
                {
                    var empty = new GameObject("Empty");
                    empty.transform.SetParent(viewport.transform, false);
                    var eR = empty.AddComponent<RectTransform>();
                    eR.anchorMin = new Vector2(0, 0.4f); eR.anchorMax = new Vector2(1, 0.6f);
                    eR.offsetMin = Vector2.zero; eR.offsetMax = Vector2.zero;
                    var eTxt = empty.AddComponent<Text>();
                    eTxt.text = "暂无好友可邀请\n请先添加好友";
                    eTxt.alignment = TextAnchor.MiddleCenter; eTxt.fontSize = 18;
                    eTxt.color = UIHelper.TextDim; eTxt.font = _font;
                    return;
                }

                var friends = friendResp.friends;
                float rowH = 56f, gap = 4f;
                cR.sizeDelta = new Vector2(0, friends.Length * (rowH + gap));

                for (int i = 0; i < friends.Length; i++)
                {
                    var f = friends[i];
                    int friendUserId = f.friendId;
                    float y = -i * (rowH + gap);

                    var row = UIHelper.MakeGlowCard(content.transform, $"Row_{i}",
                        new Vector2(0.02f, 1), new Vector2(0.98f, 1),
                        new Vector2(0, y - rowH), new Vector2(0, y),
                        new Color(0.06f, 0.04f, 0.08f, 0.9f),
                        UIHelper.GlowBottom, UIHelper.BorderSubtle);

                    var nameObj = new GameObject("Name");
                    nameObj.transform.SetParent(row.transform, false);
                    var nR = nameObj.AddComponent<RectTransform>();
                    nR.anchorMin = new Vector2(0.03f, 0); nR.anchorMax = new Vector2(0.65f, 1);
                    nR.offsetMin = Vector2.zero; nR.offsetMax = Vector2.zero;
                    var nTxt = nameObj.AddComponent<Text>();
                    nTxt.text = $"好友  ID:{friendUserId}";
                    nTxt.alignment = TextAnchor.MiddleLeft; nTxt.fontSize = 15;
                    nTxt.color = UIHelper.TextPrimary; nTxt.font = _font;
                    nTxt.raycastTarget = false;

                    var inviteBtnText = "邀请";
                    var btn = UIHelper.MakeButton(row.transform, "InviteBtn", inviteBtnText, _font,
                        new Vector2(0.68f, 0.1f), new Vector2(0.98f, 0.9f),
                        Vector2.zero, Vector2.zero,
                        UIHelper.BtnConfirm, 14, () =>
                        {
                            StartCoroutine(SendGuildInvite(friendUserId, row, overlay));
                        });
                }
            }
            catch { }
        });
        yield return null;
    }

    [System.Serializable]
    private class ServerFriendListResponse { public ServerFriendData[] friends; public int count; public int max; }
    [System.Serializable]
    private class ServerFriendData { public int friendId; public string remark; }

    /// <summary>发送公会邀请到服务器</summary>
    private IEnumerator SendGuildInvite(int targetUserId, GameObject rowObj, GameObject overlay)
    {
        yield return PostGuildAPI($"/api/guild/invite/{targetUserId}", "{}", (ok, resp) =>
        {
            if (ok)
            {
                if (GameUI.Instance != null)
                    GameUI.Instance.ShowItemPickupToast("邀请已发送", "等待对方确认");
                // 禁用按钮防止重复
                var btn = rowObj?.transform.Find("InviteBtn")?.GetComponent<Button>();
                if (btn != null) { btn.interactable = false; }
            }
            else
            {
                if (GameUI.Instance != null)
                {
                    string err = "邀请失败";
                    try { var e = JsonUtility.FromJson<SimpleResponse>(resp); if (!string.IsNullOrEmpty(e.error)) err = e.error; } catch { }
                    GameUI.Instance.ShowItemPickupToast(err, "");
                }
            }
        });
    }

    [System.Serializable]
    private class SimpleResponse { public bool success; public string error; }

    [System.Serializable]
    private class ServerGuildListResponse { public ServerGuildData[] guilds; public int total; public int page; public int pageSize; }
    [System.Serializable]
    private class ServerGuildData { public int id; public string name; public int level; public int memberCount; public int maxMembers; public string leaderName; public string announce; }

    /// <summary>发送加入公会申请</summary>
    private IEnumerator SendJoinRequest(int guildId, GameObject rowObj, GameObject overlay)
    {
        yield return PostGuildAPI($"/api/guild/join/{guildId}", "{}", (ok, resp) =>
        {
            if (ok)
            {
                if (GameUI.Instance != null)
                    GameUI.Instance.ShowItemPickupToast("申请已发送", "等待会长确认");
                var btn = rowObj?.transform.Find("JoinBtn")?.GetComponent<Button>();
                if (btn != null) { btn.interactable = false; }
            }
            else
            {
                if (GameUI.Instance != null)
                {
                    string err = "申请失败";
                    try { var e = JsonUtility.FromJson<SimpleResponse>(resp); if (!string.IsNullOrEmpty(e.error)) err = e.error; } catch { }
                    GameUI.Instance.ShowItemPickupToast(err, "");
                }
            }
        });
    }

    // ========================================================
    //  Tab 2: Boss挑战
    // ========================================================
    private void BuildBossTab()
    {
        // 每日Boss初始化
        string today = System.DateTime.Now.ToString("yyyyMMdd");
        if (GuildDataManager.Instance.NeedsDailyRefresh(today))
        {
            int initLv = Mathf.Max(1, GuildLevel);
            GuildDataManager.Instance.InitDailyBoss(today, initLv);
        }

        int bossHP = GuildDataManager.Instance.BossHP;
        int bossMaxHP = GuildDataManager.Instance.BossMaxHP;
        int myDamage = GuildDataManager.Instance.BossDamage;
        bool defeated = GuildDataManager.Instance.BossDefeated;
        int bossLv = GuildDataManager.Instance.BossLevel;
        string bossName = BossNames[(System.DateTime.Now.DayOfYear) % BossNames.Length];

        // Boss信息
        var bossInfo = new GameObject("BossInfo"); bossInfo.transform.SetParent(_tabContent.transform, false);
        var bir = bossInfo.AddComponent<RectTransform>();
        bir.anchorMin = new Vector2(0, 0.72f); bir.anchorMax = new Vector2(1, 1f); bir.offsetMin = Vector2.zero; bir.offsetMax = Vector2.zero;
        var biTxt = bossInfo.AddComponent<Text>();
        float hpPct = (float)bossHP / bossMaxHP;
        string hpBar = new string('█', (int)(hpPct * 20)) + new string('░', 20 - (int)(hpPct * 20));
        string status = defeated ? "<color=#FF4444>已击杀</color>" : $"<color=#44FF44>{bossHP}</color>/{bossMaxHP}";
        biTxt.text = $"{bossName} Lv.{bossLv}\n{hpBar}\nHP: {status}";
        biTxt.alignment = TextAnchor.MiddleCenter; biTxt.fontSize = 18; biTxt.color = UIHelper.Accent; biTxt.font = _font; biTxt.supportRichText = true;

        // 我的伤害
        var dmgObj = new GameObject("MyDamage"); dmgObj.transform.SetParent(_tabContent.transform, false);
        var drR = dmgObj.AddComponent<RectTransform>();
        drR.anchorMin = new Vector2(0, 0.62f); drR.anchorMax = new Vector2(1, 0.72f); drR.offsetMin = Vector2.zero; drR.offsetMax = Vector2.zero;
        var dmgTxt = dmgObj.AddComponent<Text>();
        dmgTxt.text = $"你的伤害: {myDamage:N0}";
        dmgTxt.alignment = TextAnchor.MiddleCenter; dmgTxt.fontSize = 16; dmgTxt.color = UIHelper.TextPrimary; dmgTxt.font = _font; dmgTxt.supportRichText = true;

        // 挑战按钮
        var challengeBtn = UIHelper.MakeButton(_tabContent.transform, "ChallengeBtn",
            defeated ? "已击杀 (领取奖励)" : "挑战Boss", _font,
            new Vector2(0.2f, 0.48f), new Vector2(0.8f, 0.60f), Vector2.zero, Vector2.zero,
            defeated ? UIHelper.BtnPrimary : new Color(0.5f, 0.1f, 0.1f, 0.95f), 20, () =>
        {
            if (defeated)
            {
                // 领取奖励
                int reward = 100 + bossLv * 50;
                var player = GameManager.Instance?.Player;
                if (player != null)
                {
                    player.Stats.AddGold(reward);
                    SeasonPass.AddXP(30);
                }
                GuildDataManager.Instance.MarkBossRewardClaimed();
                if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("Boss奖励", $"+{reward}  通行证XP+30");
                RefreshTabLast();
            }
            else
            {
                StartCoroutine(GuildBossBattle(bossName, bossLv, bossHP, bossMaxHP));
            }
        });

        // 伤害排行榜 (Mock + 玩家)
        var rankTitle = new GameObject("RankTitle"); rankTitle.transform.SetParent(_tabContent.transform, false);
        var rtR = rankTitle.AddComponent<RectTransform>();
        rtR.anchorMin = new Vector2(0, 0.38f); rtR.anchorMax = new Vector2(1, 0.46f); rtR.offsetMin = Vector2.zero; rtR.offsetMax = Vector2.zero;
        var rtTxt = rankTitle.AddComponent<Text>();
        rtTxt.text = "伤害排行榜";
        rtTxt.alignment = TextAnchor.MiddleCenter; rtTxt.fontSize = 16; rtTxt.color = UIHelper.TextSecondary; rtTxt.font = _font;

        // 生成Mock伤害
        var members = GetMembers();
        var rankList = new List<(string name, int dmg)>();
        rankList.Add(("你", myDamage));
        for (int i = 0; i < Mathf.Min(members.Count, 5); i++)
        {
            int mockDmg = Random.Range(5000, 30000) + members[i].level * 1000;
            rankList.Add((members[i].name, mockDmg));
        }
        rankList.Sort((a, b) => b.dmg.CompareTo(a.dmg));

        for (int i = 0; i < Mathf.Min(rankList.Count, 5); i++)
        {
            float y = 0.35f - i * 0.07f;
            var rowObj = new GameObject($"Rank_{i}"); rowObj.transform.SetParent(_tabContent.transform, false);
            var rrR = rowObj.AddComponent<RectTransform>();
            rrR.anchorMin = new Vector2(0.1f, y); rrR.anchorMax = new Vector2(0.9f, y + 0.06f); rrR.offsetMin = Vector2.zero; rrR.offsetMax = Vector2.zero;
            var rowTxt = rowObj.AddComponent<Text>();
            string medal = i == 0 ? "" : i == 1 ? "" : i == 2 ? "" : $"  {i+1}.";
            rowTxt.text = $"{medal} {rankList[i].name}  —  {rankList[i].dmg:N0}";
            rowTxt.alignment = TextAnchor.MiddleLeft; rowTxt.fontSize = 14; rowTxt.color = i == 0 ? UIHelper.Accent : UIHelper.TextPrimary; rowTxt.font = _font; rowTxt.supportRichText = true;
        }
    }

    private IEnumerator GuildBossBattle(string bossName, int bossLv, int bossHP, int bossMaxHP)
    {
        var player = GameManager.Instance?.Player;
        if (player == null) yield break;

        // 使用ArenaBattleUI进行战斗
        int myPower = player.Stats.GearScore + GuildLevel * 300;
        int myClass = (int)player.HeroClass;
        var bossGhost = new AsyncPvpUI.GhostData
        {
            playerName = bossName,
            classType = Random.Range(0, 3),
            level = bossLv + 10,
            gearScore = bossLv * 2000,
            wins = 999,
            losses = 0,
            defenseRating = 100
        };
        bool won = false;
        yield return ArenaBattleUI.StartBattle(GuildName, myPower, myClass, bossGhost, result => won = result);

        // 计算伤害
        int damage = won ? Random.Range(15000, 35000) + player.Stats.Level * 500 : Random.Range(5000, 15000) + player.Stats.Level * 200;
        bool wasDefeated = GuildDataManager.Instance.BossDefeated;
        GuildDataManager.Instance.DamageBoss(damage);
        bool justDefeated = !wasDefeated && GuildDataManager.Instance.BossDefeated;

        if (justDefeated)
        {
            int contribution = 50 + bossLv * 10;
            PlayerPrefs.SetInt(KeyContribution, Contribution + contribution);
            PlayerPrefs.Save();
            if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("击杀Boss!", $"伤害:{damage:N0}  +{contribution}贡献点\n请领取奖励!");
        }
        else
        {
            int contribution = damage / 1000;
            PlayerPrefs.SetInt(KeyContribution, Contribution + contribution);
            PlayerPrefs.Save();
            if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("Boss挑战", $"造成伤害:{damage:N0}  +{contribution}贡献点");
        }
        RefreshTabLast();
    }

    // ========================================================
    //  Tab 3: 公会商店
    // ========================================================
    private void BuildShopTab()
    {
        // 贡献点显示
        var infoObj = new GameObject("Info"); infoObj.transform.SetParent(_tabContent.transform, false);
        var ir = infoObj.AddComponent<RectTransform>();
        ir.anchorMin = new Vector2(0, 0.88f); ir.anchorMax = new Vector2(1, 1f); ir.offsetMin = Vector2.zero; ir.offsetMax = Vector2.zero;
        var info = infoObj.AddComponent<Text>();
        info.text = $" 贡献点: {Contribution}";
        info.alignment = TextAnchor.MiddleCenter; info.fontSize = 18; info.color = UIHelper.Accent; info.font = _font; info.supportRichText = true;

        // 商店物品列表
        for (int i = 0; i < ShopItems.Length; i++)
        {
            int idx = i;
            float y = 0.85f - (i + 1) * 0.12f;
            var card = UIHelper.MakeGlowCard(_tabContent.transform, $"Shop_{i}",
                new Vector2(0.05f, y), new Vector2(0.95f, y + 0.10f), Vector2.zero, Vector2.zero,
                new Color(0.06f, 0.04f, 0.08f, 0.92f), UIHelper.GlowBottom, UIHelper.BorderSubtle);

            var item = ShopItems[i];
            string rarityColor = item.rarity switch
            {
                3 => "<color=#FF8800>",
                2 => "<color=#AA44FF>",
                1 => "<color=#4488FF>",
                _ => "<color=#CCCCCC>",
            };

            var lblObj = new GameObject("Lbl"); lblObj.transform.SetParent(card.transform, false);
            var lr = lblObj.AddComponent<RectTransform>(); lr.anchorMin = new Vector2(0.05f, 0); lr.anchorMax = new Vector2(0.55f, 1); lr.offsetMin = new Vector2(8, 4); lr.offsetMax = new Vector2(-4, -4);
            var lbl = lblObj.AddComponent<Text>();
            lbl.text = $"{rarityColor}{item.name}</color>  {item.cost}";
            lbl.alignment = TextAnchor.MiddleLeft; lbl.fontSize = 15; lbl.color = UIHelper.TextPrimary; lbl.font = _font; lbl.supportRichText = true; lbl.raycastTarget = false;

            bool purchased = GuildDataManager.Instance.IsShopItemPurchased(idx);
            bool canAfford = Contribution >= item.cost && !purchased;
            var btn = UIHelper.MakeButton(card.transform, "BuyBtn", purchased ? "已兑换" : "兑换", _font,
                new Vector2(0.6f, 0.15f), new Vector2(0.95f, 0.85f), Vector2.zero, Vector2.zero,
                canAfford ? UIHelper.BtnConfirm : new Color(0.3f, 0.3f, 0.3f, 0.8f), 14, () => BuyShopItem(idx));
            var btnComp = btn.GetComponent<Button>();
            btnComp.interactable = canAfford;
        }

        // 每日限购提示
        var hintObj = new GameObject("Hint"); hintObj.transform.SetParent(_tabContent.transform, false);
        var hr = hintObj.AddComponent<RectTransform>();
        hr.anchorMin = new Vector2(0, 0.01f); hr.anchorMax = new Vector2(1, 0.05f); hr.offsetMin = Vector2.zero; hr.offsetMax = Vector2.zero;
        var hint = hintObj.AddComponent<Text>();
        hint.text = "每日重置购买限制 | 通过Boss挑战获取贡献点";
        hint.alignment = TextAnchor.MiddleCenter; hint.fontSize = 12; hint.color = UIHelper.TextDim; hint.font = _font;
    }

    private void BuyShopItem(int idx)
    {
        var item = ShopItems[idx];
        if (Contribution < item.cost)
        {
            if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("贡献点不足", $"需要{item.cost}贡献点");
            return;
        }

        PlayerPrefs.SetInt(KeyContribution, Contribution - item.cost);

        var player = GameManager.Instance?.Player;
        if (player == null) return;

        switch (item.type)
        {
            case "equipment":
                var eqItem = EquipmentItem.GenerateRandom(item.value + 1); // rarity+1 as dungeonLevel
                if (!player.Inventory.AddToBackpack(eqItem))
                    LocalMailSystem.SendItemMail($"公会商店: {eqItem.Name}", $"背包已满，装备已发送至邮箱。\n{eqItem.GetStatSummary()}", eqItem);
                if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("兑换成功", $"{eqItem.Name}");
                break;
            case "gold":
                player.Stats.AddGold(item.value);
                if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("兑换成功", $"+{item.value}金币");
                break;
            case "skillpoint":
                player.Stats.SkillPoints += item.value;
                if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("兑换成功", $"+{item.value}技能点");
                break;
            case "fragment":
                int frag = PlayerPrefs.GetInt($"ARPG_S{PlayerProgressData.ActiveSlot}_DismantleFragments", 0) + item.value;
                PlayerPrefs.SetInt($"ARPG_S{PlayerProgressData.ActiveSlot}_DismantleFragments", frag);
                if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("兑换成功", $"+{item.value}碎片");
                break;
        }
        GuildDataManager.Instance.MarkShopItemPurchased(idx);
        PlayerPrefs.Save();
        RefreshTabLast();
    }

    // ========================================================
    //  公会战 (保留原功能)
    // ========================================================
    private IEnumerator GuildWarBattle(Font font)
    {
        string[] enemyNames = { "黑暗军团", "龙裔先锋", "深渊联盟", "圣光守卫", "风暴骑士团" };
        string enemy = enemyNames[Random.Range(0, enemyNames.Length)];
        int enemyPower = Random.Range(3000, 9000);
        var player = GameManager.Instance?.Player;
        if (player == null) yield break;
        int myPower = player.Stats.GearScore + GuildLevel * 500;
        int myClass = (int)player.HeroClass;
        var enemyGhost = new AsyncPvpUI.GhostData { playerName = enemy, classType = Random.Range(0, 3), level = Random.Range(15, 28), gearScore = enemyPower, wins = Random.Range(20, 80), losses = Random.Range(5, 30), defenseRating = Random.Range(50, 100) };
        bool won = false;
        yield return ArenaBattleUI.StartBattle(GuildName, myPower, myClass, enemyGhost, result => won = result);
        if (won)
        {
            int reward = 200 + GuildLevel * 50; player.Stats.AddGold(reward); SeasonPass.AddXP(50);
            PlayerPrefs.SetInt(KeyGuildExp, GuildExp + 100);
            if (GuildExp + 100 >= GuildExpToNext) { PlayerPrefs.SetInt(KeyGuildLevel, GuildLevel + 1); PlayerPrefs.SetInt(KeyGuildExp, 0); }
            PlayerPrefs.Save();
            if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("公会战胜利!", $"+{reward} 通行证XP+50");
        }
        else
        {
            int consolation = 50; player.Stats.AddGold(consolation); PlayerPrefs.Save();
            if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("公会战失败", $"+{consolation} (参与奖)");
        }
        Show();
    }

    // ========================================================
    //  成员数据 (Mock + PlayerPrefs持久化)
    // ========================================================
    [System.Serializable]
    public struct GuildMember
    {
        public string name;
        public int cls;
        public int level;
        public int contribution;
        public int rank;
        public bool online;
    }

    private List<GuildMember> GetMembers()
    {
        var result = new List<GuildMember>();
        string json = PlayerPrefs.GetString(KeyGuildMembers, "");
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                var arr = JsonUtility.FromJson<MemberArray>(json);
                if (arr != null && arr.members != null)
                    result = new List<GuildMember>(arr.members);
            }
            catch { }
        }
        return result;
    }

    private void SaveMembers(List<GuildMember> members)
    {
        var arr = new MemberArray { members = members.ToArray() };
        PlayerPrefs.SetString(KeyGuildMembers, JsonUtility.ToJson(arr));
        PlayerPrefs.Save();
    }

    [System.Serializable]
    private class MemberArray { public GuildMember[] members; }

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
