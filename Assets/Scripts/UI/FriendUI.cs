using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 好友&组队面板 — 好友列表/申请/最近一起玩/组队
/// 纯客户端实现，数据存储在 PlayerPrefs
/// </summary>
public class FriendUI : MonoBehaviour
{
    public static FriendUI Instance { get; private set; }

    private GameObject panel;
    private Coroutine showRoutine;
    private Coroutine _refreshRoutine;
    private const float RefreshInterval = 15f;
    private int _currentTab = 0;
    private GameObject _tabContent;
    private Font _font;
    private int _friendPage = 0;
    private const int FriendsPerPage = 10;
    private const int MaxFriends = 50;
    private List<FriendData> _liveFriends = new List<FriendData>();

    // ====== Keys ======
    private const string KeyFriends = "ARPG_FriendList";
    private const string KeyRequests = "ARPG_FriendRequests";
    private const string KeyRecent = "ARPG_RecentPlayers";
    private const string KeyGiftDate = "ARPG_FriendGiftDate";
    private const string KeyGiftSent = "ARPG_FriendGiftSent";
    private const string KeyBlacklist = "ARPG_FriendBlacklist";
    private const string KeyTeamId = "ARPG_TeamId";
    private const string KeyTeamLeader = "ARPG_TeamLeader";

    private static readonly string[] ClassNames = { "战士", "法师", "牧师" };
    private static readonly Color[] ClassColors =
    {
        new Color(0.7f, 0.2f, 0.2f, 1f),
        new Color(0.2f, 0.35f, 0.8f, 1f),
        new Color(0.8f, 0.75f, 0.4f, 1f),
    };

    [System.Serializable]
    public struct FriendData
    {
        public string name;
        public int cls;
        public int level;
        public int gearScore;
        public string remark;
        public int status; // 0=离线 1=在线 2=战斗中 3=挂机
        public long lastActive;
    }

    [System.Serializable]
    public struct FriendRequestData
    {
        public string fromName;
        public int fromLevel;
        public int fromClass;
        public string message;
    }

    [System.Serializable]
    public struct RecentPlayerData
    {
        public string name;
        public int cls;
        public int level;
        public int playCount;
    }

    [System.Serializable]
    public class FriendArray { public FriendData[] friends; }
    [System.Serializable]
    private class RequestArray { public FriendRequestData[] requests; }
    [System.Serializable]
    private class RecentArray { public RecentPlayerData[] players; }
    [System.Serializable]
    private class BlacklistArray { public string[] names; }

    private void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; }

    public void Show()
    {
        if (panel != null) { Destroy(panel); panel = null; }
        _currentTab = 0;
        _friendPage = 0;
        _liveFriends.Clear();
        BuildUI();
        UIHelper.SetupTopBar("好友", () => { Hide(); UIHelper.EnsureUIManager(); UIManager.Instance.ShowHub(); });
        panel.SetActive(true);
        if (showRoutine != null) StopCoroutine(showRoutine);
        showRoutine = StartCoroutine(UIHelper.FadeIn(panel, 0.2f));
        StartCoroutine(FetchFriendsFromServer());
        if (_refreshRoutine != null) StopCoroutine(_refreshRoutine);
        _refreshRoutine = StartCoroutine(RefreshLoop());
    }

    private IEnumerator RefreshLoop()
    {
        while (panel != null && panel.activeSelf)
        {
            yield return new WaitForSeconds(RefreshInterval);
            if (panel != null && panel.activeSelf && _currentTab == 0)
                StartCoroutine(FetchFriendsFromServer());
        }
    }

    /// <summary>直接弹出添加好友窗口(从大本营调用)</summary>
    public void ShowAddFriendDirect()
    {
        StartCoroutine(ShowAddFriendPopup());
    }

    private const string ServerUrl = "http://39.107.141.107:5132";

    /// <summary>从服务器获取好友列表并缓存到PlayerPrefs</summary>
    private IEnumerator FetchFriendsFromServer()
    {
        string token = PlayerPrefs.GetString("ARPG_AuthToken", "");
        if (string.IsNullOrEmpty(token)) yield break;

        using (var req = UnityEngine.Networking.UnityWebRequest.Get($"{ServerUrl}/api/friend/list"))
        {
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + token);
            yield return req.SendWebRequest();

            if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                try
                {
                    var resp = JsonUtility.FromJson<ServerFriendListResp>(req.downloadHandler.text);
                    if (resp?.friends != null)
                    {
                        var friends = new List<FriendData>();
                        foreach (var sf in resp.friends)
                        {
                            friends.Add(new FriendData
                            {
                                name = sf.username ?? $"ID:{sf.friendId}",
                                cls = Mathf.Clamp(sf.classType, 0, 2),
                                level = sf.level > 0 ? sf.level : 1,
                                gearScore = 0,
                                remark = sf.remark ?? "",
                                status = sf.online ? 1 : 0,
                                lastActive = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                            });
                        }
                        // 缓存到PlayerPrefs时status强制设为0(离线)，在线状态不持久化
                        _liveFriends = friends;
                        var toSave = new List<FriendData>(friends);
                        for (int i = 0; i < toSave.Count; i++) { var fd = toSave[i]; fd.status = 0; toSave[i] = fd; }
                        SaveFriends(toSave);
                        if (_currentTab == 0 && panel != null && panel.activeSelf)
                            RefreshTabLast();
                    }
                }
                catch { }
            }
        }
    }

    [System.Serializable]
    private class ServerFriendListResp { public ServerFriendItem[] friends; public int count; public int max; }
    [System.Serializable]
    private class ServerFriendItem { public int friendId; public string username; public int level; public int classType; public bool online; public string remark; }

    public void Hide()
    {
        if (panel != null)
        {
            if (showRoutine != null) StopCoroutine(showRoutine);
            if (_refreshRoutine != null) StopCoroutine(_refreshRoutine);
            showRoutine = StartCoroutine(UIHelper.FadeOut(panel, 0.15f));
        }
    }

    // ====== 数据 ======
    private List<FriendData> GetFriends()
    {
        var result = new List<FriendData>();
        string json = PlayerPrefs.GetString(KeyFriends, "");
        if (!string.IsNullOrEmpty(json))
        {
            try { var arr = JsonUtility.FromJson<FriendArray>(json); if (arr?.friends != null) result = new List<FriendData>(arr.friends); }
            catch { }
        }
        return result;
    }

    private void SaveFriends(List<FriendData> friends)
    {
        PlayerPrefs.SetString(KeyFriends, JsonUtility.ToJson(new FriendArray { friends = friends.ToArray() }));
        PlayerPrefs.Save();
    }

    private List<FriendRequestData> GetRequests()
    {
        var result = new List<FriendRequestData>();
        string json = PlayerPrefs.GetString(KeyRequests, "");
        if (!string.IsNullOrEmpty(json))
        {
            try { var arr = JsonUtility.FromJson<RequestArray>(json); if (arr?.requests != null) result = new List<FriendRequestData>(arr.requests); }
            catch { }
        }
        return result;
    }

    private void SaveRequests(List<FriendRequestData> requests)
    {
        PlayerPrefs.SetString(KeyRequests, JsonUtility.ToJson(new RequestArray { requests = requests.ToArray() }));
        PlayerPrefs.Save();
    }

    private List<RecentPlayerData> GetRecentPlayers()
    {
        var result = new List<RecentPlayerData>();
        string json = PlayerPrefs.GetString(KeyRecent, "");
        if (!string.IsNullOrEmpty(json))
        {
            try { var arr = JsonUtility.FromJson<RecentArray>(json); if (arr?.players != null) result = new List<RecentPlayerData>(arr.players); }
            catch { }
        }
        return result;
    }

    private void SaveRecent(List<RecentPlayerData> players)
    {
        PlayerPrefs.SetString(KeyRecent, JsonUtility.ToJson(new RecentArray { players = players.ToArray() }));
        PlayerPrefs.Save();
    }

    private List<string> GetBlacklist()
    {
        var result = new List<string>();
        string json = PlayerPrefs.GetString(KeyBlacklist, "");
        if (!string.IsNullOrEmpty(json))
        {
            try { var arr = JsonUtility.FromJson<BlacklistArray>(json); if (arr?.names != null) result = new List<string>(arr.names); }
            catch { }
        }
        return result;
    }

    private void SaveBlacklist(List<string> names)
    {
        PlayerPrefs.SetString(KeyBlacklist, JsonUtility.ToJson(new BlacklistArray { names = names.ToArray() }));
        PlayerPrefs.Save();
    }

    private static string StatusText(int status) => status switch
    {
        1 => "<color=#55FF55>●在线</color>", 2 => "<color=#FF6644>●战斗中</color>",
        3 => "<color=#FFAA44>●挂机</color>", _ => "<color=#666>●离线</color>"
    };

    // ========================================================
    //  BuildUI
    // ========================================================
    private void BuildUI()
    {
        _font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();

        panel = new GameObject("FriendPanel");
        panel.transform.SetParent(canvas.transform, false);
        var pr = panel.AddComponent<RectTransform>();
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one; pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;
        panel.AddComponent<CanvasGroup>();

        var bg = new GameObject("BG"); bg.transform.SetParent(panel.transform, false);
        var bgr = bg.AddComponent<RectTransform>(); bgr.anchorMin = Vector2.zero; bgr.anchorMax = Vector2.one; bgr.offsetMin = Vector2.zero; bgr.offsetMax = Vector2.zero;
        bg.AddComponent<Image>().color = UIHelper.BgDark;

        UIHelper.MakeSubtitle(panel.transform, $"好友 {GetFriends().Count}/{MaxFriends}    申请 {GetRequests().Count}", _font, 100, 16);

        BuildTabbedView();
        panel.SetActive(false);
    }

    private void BuildTabbedView()
    {
        string[] tabNames = { "好友列表", "申请", "最近一起玩", "组队" };

        var tabRow = new GameObject("TabRow");
        tabRow.transform.SetParent(panel.transform, false);
        var tabRowRt = tabRow.AddComponent<RectTransform>();
        tabRowRt.anchorMin = new Vector2(0.1f, 1); tabRowRt.anchorMax = new Vector2(0.9f, 1);
        tabRowRt.pivot = new Vector2(0.5f, 1);
        tabRowRt.offsetMin = new Vector2(0, -50); tabRowRt.offsetMax = new Vector2(0, -10);

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
        }

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
        for (int i = _tabContent.transform.childCount - 1; i >= 0; i--)
            Destroy(_tabContent.transform.GetChild(i).gameObject);

        switch (_currentTab)
        {
            case 0: BuildFriendListTab(); break;
            case 1: BuildRequestsTab(); break;
            case 2: BuildRecentTab(); break;
            case 3: BuildTeamTab(); break;
        }
    }

    // ========================================================
    //  Tab 0: 好友列表
    // ========================================================
    private void BuildFriendListTab()
    {
        var friends = _liveFriends.Count > 0 ? _liveFriends : GetFriends();
        int total = friends.Count;
        int start = _friendPage * FriendsPerPage;
        int end = Mathf.Min(start + FriendsPerPage, total);
        float rowH = 50f, gap = 4f;

        // 添加好友按钮 (顶部)
        UIHelper.MakeButton(_tabContent.transform, "AddBtn", "添加好友", _font,
            new Vector2(0, 1), new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(0, 0),
            UIHelper.BtnConfirm, 16, () => StartCoroutine(ShowAddFriendPopup()));

        // 每日礼物按钮
        string today = System.DateTime.Now.ToString("yyyyMMdd");
        bool giftSent = PlayerPrefs.GetString(KeyGiftDate, "") == today;
        UIHelper.MakeButton(_tabContent.transform, "GiftBtn", giftSent ? "已领取" : "领取友情点", _font,
            new Vector2(0.5f, 1), new Vector2(1f, 1), new Vector2(0, -50), new Vector2(0, 0),
            giftSent ? new Color(0.3f, 0.3f, 0.3f, 0.8f) : UIHelper.BtnPrimary, 16, () =>
            {
                if (giftSent) return;
                PlayerPrefs.SetString(KeyGiftDate, today);
                PlayerPrefs.Save();
                var player = GameManager.Instance?.Player;
                if (player != null) player.Stats.AddGold(friends.Count * 50);
                if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("友情点", $"+{friends.Count * 50}金币");
                RefreshTabLast();
            });

        // 好友列表
        for (int i = start; i < end; i++)
        {
            int idx = i;
            var f = friends[i];
            float y = -60f - (i - start) * (rowH + gap);

            var row = UIHelper.MakeGlowCard(_tabContent.transform, $"Friend_{i}",
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, y - rowH), new Vector2(0, y),
                new Color(0.06f, 0.04f, 0.08f, 0.92f), UIHelper.GlowBottom, UIHelper.BorderSubtle);

            var txtObj = new GameObject("Lbl"); txtObj.transform.SetParent(row.transform, false);
            var txtR = txtObj.AddComponent<RectTransform>(); txtR.anchorMin = Vector2.zero; txtR.anchorMax = new Vector2(0.65f, 1); txtR.offsetMin = new Vector2(10, 4); txtR.offsetMax = new Vector2(-4, -4);
            var txt = txtObj.AddComponent<Text>();
            int clsIdx = Mathf.Clamp(f.cls, 0, 2);
            string classColor = ColorUtility.ToHtmlStringRGBA(ClassColors[clsIdx]);
            string remark = string.IsNullOrEmpty(f.remark) ? "" : $"({f.remark})";
            txt.text = $"{StatusText(f.status)} <color=#{classColor}>{ClassNames[clsIdx]}</color> {f.name}{remark}  Lv.{f.level}  战力:{f.gearScore}";
            txt.alignment = TextAnchor.MiddleLeft; txt.fontSize = 14; txt.color = UIHelper.TextPrimary; txt.font = _font; txt.supportRichText = true; txt.raycastTarget = false;

            // 私聊+组队+删除按钮
            if (f.status == 1)
            {
                UIHelper.MakeButton(row.transform, "TeamBtn", "组队", _font,
                    new Vector2(0.68f, 0.1f), new Vector2(0.80f, 0.9f), Vector2.zero, Vector2.zero,
                    UIHelper.BtnConfirm, 12, () =>
                    {
                        if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("组队邀请", $"已向 {f.name} 发送组队邀请");
                    });
            }

            UIHelper.MakeButton(row.transform, "DelBtn", "删除", _font,
                new Vector2(0.81f, 0.1f), new Vector2(0.93f, 0.9f), Vector2.zero, Vector2.zero,
                UIHelper.BtnDanger, 12, () =>
                {
                    friends.RemoveAt(idx);
                    SaveFriends(friends);
                    RefreshTabLast();
                });

            UIHelper.MakeButton(row.transform, "BlockBtn", "拉黑", _font,
                new Vector2(0.93f, 0.1f), new Vector2(0.99f, 0.9f), Vector2.zero, Vector2.zero,
                new Color(0.3f, 0.15f, 0.15f, 0.9f), 10, () =>
                {
                    var bl = GetBlacklist();
                    if (!bl.Contains(f.name)) { bl.Add(f.name); SaveBlacklist(bl); }
                    friends.RemoveAt(idx);
                    SaveFriends(friends);
                    if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("已拉黑", f.name);
                    RefreshTabLast();
                });
        }

        // 分页
        int totalPages = Mathf.Max(1, Mathf.CeilToInt((float)total / FriendsPerPage));
        var pageRow = new GameObject("PageRow"); pageRow.transform.SetParent(_tabContent.transform, false);
        var pgr = pageRow.AddComponent<RectTransform>();
        pgr.anchorMin = new Vector2(0.5f, 0); pgr.anchorMax = new Vector2(0.5f, 0);
        pgr.pivot = new Vector2(0.5f, 0);
        pgr.offsetMin = new Vector2(-150, 5); pgr.offsetMax = new Vector2(150, 55);

        UIHelper.MakeButton(pageRow.transform, "PrevBtn", "◀ 上一页", _font,
            new Vector2(0, 0), new Vector2(0.4f, 1), Vector2.zero, Vector2.zero,
            new Color(0.08f, 0.08f, 0.12f, 0.9f), 13, () => { if (_friendPage > 0) { _friendPage--; RefreshTabLast(); } });

        var ptObj = new GameObject("PageText"); ptObj.transform.SetParent(pageRow.transform, false);
        var ptr = ptObj.AddComponent<RectTransform>(); ptr.anchorMin = new Vector2(0.4f, 0); ptr.anchorMax = new Vector2(0.6f, 1); ptr.offsetMin = Vector2.zero; ptr.offsetMax = Vector2.zero;
        var ptTxt = ptObj.AddComponent<Text>(); ptTxt.alignment = TextAnchor.MiddleCenter; ptTxt.fontSize = 14; ptTxt.color = UIHelper.TextSecondary; ptTxt.font = _font;
        ptTxt.text = $"{_friendPage + 1} / {totalPages}";

        UIHelper.MakeButton(pageRow.transform, "NextBtn", "下一页", _font,
            new Vector2(0.6f, 0), new Vector2(1f, 1), Vector2.zero, Vector2.zero,
            new Color(0.08f, 0.08f, 0.12f, 0.9f), 13, () => { if (_friendPage < totalPages - 1) { _friendPage++; RefreshTabLast(); } });

        // 底部 + 添加好友按钮
        UIHelper.MakeButton(_tabContent.transform, "AddFriendBottom", "+ 添加好友", _font,
            new Vector2(0.3f, 0), new Vector2(0.7f, 0), new Vector2(0, 60), new Vector2(0, 100),
            UIHelper.BtnConfirm, 18, () => StartCoroutine(ShowAddFriendPopup()));
    }

    private IEnumerator ShowAddFriendPopup()
    {
        Canvas canvas = GameManager.EnsureCanvas();
        var overlay = new GameObject("AddFriendPopup");
        overlay.transform.SetParent(canvas.transform, false);
        var oR = overlay.AddComponent<RectTransform>();
        oR.anchorMin = Vector2.zero; oR.anchorMax = Vector2.one; oR.offsetMin = Vector2.zero; oR.offsetMax = Vector2.zero;
        overlay.AddComponent<Image>().color = new Color(0, 0, 0, 0.7f);
        var oCG = overlay.AddComponent<CanvasGroup>();

        var box = UIHelper.MakeGlowCard(overlay.transform, "Box",
            new Vector2(0.2f, 0.3f), new Vector2(0.8f, 0.7f), Vector2.zero, Vector2.zero,
            new Color(0.06f, 0.05f, 0.10f, 0.98f), UIHelper.GlowTop, UIHelper.Accent);

        var titleObj = new GameObject("Title"); titleObj.transform.SetParent(box.transform, false);
        var tR = titleObj.AddComponent<RectTransform>(); tR.anchorMin = new Vector2(0, 0.75f); tR.anchorMax = new Vector2(0.85f, 1f); tR.offsetMin = Vector2.zero; tR.offsetMax = Vector2.zero;
        var title = titleObj.AddComponent<Text>(); title.text = "添加好友"; title.alignment = TextAnchor.MiddleLeft; title.fontSize = 20; title.color = UIHelper.Accent; title.font = _font;

        UIHelper.MakeButton(box.transform, "CloseBtn", "X", _font,
            new Vector2(0.88f, 0.80f), new Vector2(0.98f, 0.98f), Vector2.zero, Vector2.zero,
            UIHelper.BtnDanger, 16, () => Destroy(overlay));

        // 输入框 + 搜索按钮
        var inputLabel = new GameObject("InputLabel"); inputLabel.transform.SetParent(box.transform, false);
        var ilR = inputLabel.AddComponent<RectTransform>();
        ilR.anchorMin = new Vector2(0.05f, 0.55f); ilR.anchorMax = new Vector2(0.95f, 0.70f);
        ilR.offsetMin = Vector2.zero; ilR.offsetMax = Vector2.zero;
        var ilTxt = inputLabel.AddComponent<Text>();
        ilTxt.text = "输入对方用户名发送好友申请"; ilTxt.alignment = TextAnchor.MiddleLeft;
        ilTxt.fontSize = 14; ilTxt.color = UIHelper.TextSecondary; ilTxt.font = _font;

        var inputObj = new GameObject("InputField"); inputObj.transform.SetParent(box.transform, false);
        var inR = inputObj.AddComponent<RectTransform>();
        inR.anchorMin = new Vector2(0.05f, 0.38f); inR.anchorMax = new Vector2(0.70f, 0.53f);
        inR.offsetMin = Vector2.zero; inR.offsetMax = Vector2.zero;
        var inBg = inputObj.AddComponent<Image>(); inBg.color = new Color(0.08f, 0.06f, 0.12f, 0.9f);
        var inputField = inputObj.AddComponent<InputField>();
        var inputText = new GameObject("Text"); inputText.transform.SetParent(inputObj.transform, false);
        var itR = inputText.AddComponent<RectTransform>();
        itR.anchorMin = new Vector2(0, 0); itR.anchorMax = new Vector2(1, 1);
        itR.offsetMin = new Vector2(8, 2); itR.offsetMax = new Vector2(-8, -2);
        var iTxt = inputText.AddComponent<Text>();
        iTxt.alignment = TextAnchor.MiddleLeft; iTxt.fontSize = 16;
        iTxt.color = UIHelper.TextPrimary; iTxt.font = _font;
        var placeholder = new GameObject("Placeholder"); placeholder.transform.SetParent(inputObj.transform, false);
        var phR = placeholder.AddComponent<RectTransform>();
        phR.anchorMin = new Vector2(0, 0); phR.anchorMax = new Vector2(1, 1);
        phR.offsetMin = new Vector2(8, 2); phR.offsetMax = new Vector2(-8, -2);
        var phTxt = placeholder.AddComponent<Text>();
        phTxt.text = "用户名..."; phTxt.alignment = TextAnchor.MiddleLeft; phTxt.fontSize = 16;
        phTxt.color = new Color(0.4f, 0.4f, 0.4f, 1f); phTxt.fontStyle = FontStyle.Italic; phTxt.font = _font;
        inputField.textComponent = iTxt; inputField.placeholder = phTxt;

        UIHelper.MakeButton(box.transform, "SendBtn", "发送申请", _font,
            new Vector2(0.72f, 0.38f), new Vector2(0.95f, 0.53f), Vector2.zero, Vector2.zero,
            UIHelper.BtnConfirm, 14, () =>
            {
                string targetName = inputField.text?.Trim() ?? "";
                if (string.IsNullOrEmpty(targetName))
                {
                    if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("请输入用户名", "");
                    return;
                }
                // 通过服务器API发送好友申请
                StartCoroutine(SendFriendRequestByName(targetName, overlay));
            });

        float t = 0;
        while (t < 0.2f) { t += Time.unscaledDeltaTime; oCG.alpha = Mathf.Clamp01(t / 0.2f); yield return null; }
        oCG.alpha = 1f;
        yield return null;
    }

    /// <summary>通过用户名发送好友申请(调用服务器API)</summary>
    private IEnumerator SendFriendRequestByName(string targetName, GameObject overlay)
    {
        string url = "http://39.107.141.107:5132/api/friend/request";
        string json = $"{{\"toUserId\":0,\"targetName\":\"{targetName}\",\"message\":\"我想和你成为好友\"}}";
        string token = PlayerPrefs.GetString("ARPG_AuthToken", "");
        if (string.IsNullOrEmpty(token))
        {
            if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("请先登录", "");
            yield break;
        }

        using (var req = new UnityEngine.Networking.UnityWebRequest(url, "POST"))
        {
            req.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + token);
            yield return req.SendWebRequest();

            if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("申请已发送", $"等待 {targetName} 确认");
                if (overlay != null) Destroy(overlay);
            }
            else
            {
                string errMsg = "发送失败";
                if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("好友申请失败", errMsg);
            }
        }
    }

    // ========================================================
    //  Tab 1: 好友申请
    // ========================================================
    private void BuildRequestsTab()
    {
        var requests = GetRequests();

        if (requests.Count == 0)
        {
            var empty = new GameObject("Empty"); empty.transform.SetParent(_tabContent.transform, false);
            var eR = empty.AddComponent<RectTransform>(); eR.anchorMin = new Vector2(0.2f, 0.4f); eR.anchorMax = new Vector2(0.8f, 0.6f); eR.offsetMin = Vector2.zero; eR.offsetMax = Vector2.zero;
            var eTxt = empty.AddComponent<Text>(); eTxt.text = "暂无好友申请"; eTxt.alignment = TextAnchor.MiddleCenter; eTxt.fontSize = 18; eTxt.color = UIHelper.TextDim; eTxt.font = _font;
            return;
        }

        for (int i = 0; i < requests.Count; i++)
        {
            int idx = i;
            var req = requests[i];
            float y = -10f - i * 60f;

            var row = UIHelper.MakeGlowCard(_tabContent.transform, $"Req_{i}",
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, y - 50), new Vector2(0, y),
                new Color(0.06f, 0.04f, 0.08f, 0.92f), UIHelper.GlowBottom, UIHelper.BorderSubtle);

            var txtObj = new GameObject("Lbl"); txtObj.transform.SetParent(row.transform, false);
            var txtR = txtObj.AddComponent<RectTransform>(); txtR.anchorMin = new Vector2(0.03f, 0); txtR.anchorMax = new Vector2(0.55f, 1); txtR.offsetMin = new Vector2(8, 4); txtR.offsetMax = new Vector2(-4, -4);
            var txt = txtObj.AddComponent<Text>();
            int clsIdx = Mathf.Clamp(req.fromClass, 0, 2);
            string cc = ColorUtility.ToHtmlStringRGBA(ClassColors[clsIdx]);
            txt.text = $"<color=#{cc}>{ClassNames[clsIdx]}</color> {req.fromName}  Lv.{req.fromLevel}\n\"{req.message}\"";
            txt.alignment = TextAnchor.MiddleLeft; txt.fontSize = 14; txt.color = UIHelper.TextPrimary; txt.font = _font; txt.supportRichText = true; txt.raycastTarget = false;

            UIHelper.MakeButton(row.transform, "AcceptBtn", "接受", _font,
                new Vector2(0.58f, 0.15f), new Vector2(0.75f, 0.85f), Vector2.zero, Vector2.zero,
                UIHelper.BtnConfirm, 14, () =>
                {
                    var friends = GetFriends();
                    if (friends.Count >= MaxFriends)
                    {
                        if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("好友已满", $"上限{MaxFriends}人");
                        return;
                    }
                    friends.Add(new FriendData { name = req.fromName, cls = req.fromClass, level = req.fromLevel, gearScore = Random.Range(800, 3000), status = 1, lastActive = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() });
                    SaveFriends(friends);
                    requests.RemoveAt(idx);
                    SaveRequests(requests);
                    if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("已添加", $"{req.fromName} 已成为好友");
                    RefreshTabLast();
                });

            UIHelper.MakeButton(row.transform, "RejectBtn", "拒绝", _font,
                new Vector2(0.77f, 0.15f), new Vector2(0.94f, 0.85f), Vector2.zero, Vector2.zero,
                UIHelper.BtnDanger, 14, () =>
                {
                    requests.RemoveAt(idx);
                    SaveRequests(requests);
                    RefreshTabLast();
                });
        }
    }

    // ========================================================
    //  Tab 2: 最近一起玩
    // ========================================================
    private void BuildRecentTab()
    {
        var recent = GetRecentPlayers();

        for (int i = 0; i < recent.Count; i++)
        {
            int idx = i;
            var p = recent[i];
            float y = -10f - i * 55f;

            var row = UIHelper.MakeGlowCard(_tabContent.transform, $"Recent_{i}",
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, y - 45), new Vector2(0, y),
                new Color(0.06f, 0.04f, 0.08f, 0.92f), UIHelper.GlowBottom, UIHelper.BorderSubtle);

            var txtObj = new GameObject("Lbl"); txtObj.transform.SetParent(row.transform, false);
            var txtR = txtObj.AddComponent<RectTransform>(); txtR.anchorMin = Vector2.zero; txtR.anchorMax = new Vector2(0.6f, 1); txtR.offsetMin = new Vector2(10, 4); txtR.offsetMax = new Vector2(-4, -4);
            var txt = txtObj.AddComponent<Text>();
            int clsIdx = Mathf.Clamp(p.cls, 0, 2);
            string cc = ColorUtility.ToHtmlStringRGBA(ClassColors[clsIdx]);
            txt.text = $"<color=#{cc}>{ClassNames[clsIdx]}</color> {p.name}  Lv.{p.level}  一起玩:{p.playCount}次";
            txt.alignment = TextAnchor.MiddleLeft; txt.fontSize = 14; txt.color = UIHelper.TextPrimary; txt.font = _font; txt.supportRichText = true; txt.raycastTarget = false;

            UIHelper.MakeButton(row.transform, "AddBtn", "加好友", _font,
                new Vector2(0.62f, 0.15f), new Vector2(0.80f, 0.85f), Vector2.zero, Vector2.zero,
                UIHelper.BtnConfirm, 12, () =>
                {
                    var friends = GetFriends();
                    if (friends.Count >= MaxFriends) { if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("好友已满", ""); return; }
                    if (friends.FindIndex(f => f.name == p.name) < 0)
                    {
                        friends.Add(new FriendData { name = p.name, cls = p.cls, level = p.level, gearScore = Random.Range(800, 3000), status = 1, lastActive = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() });
                        SaveFriends(friends);
                        if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("已添加", $"{p.name} 已成为好友");
                    }
                });

            UIHelper.MakeButton(row.transform, "InviteBtn", "邀请组队", _font,
                new Vector2(0.82f, 0.15f), new Vector2(0.98f, 0.85f), Vector2.zero, Vector2.zero,
                UIHelper.BtnPrimary, 12, () =>
                {
                    if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("组队邀请", $"已向 {p.name} 发送邀请");
                });
        }
    }

    // ========================================================
    //  Tab 3: 组队
    // ========================================================
    private void BuildTeamTab()
    {
        bool inTeam = PlayerPrefs.HasKey(KeyTeamId);

        if (!inTeam)
        {
            // 创建队伍
            var createCard = UIHelper.MakeGlowCard(_tabContent.transform, "CreateCard",
                new Vector2(0.1f, 0.55f), new Vector2(0.9f, 0.80f), Vector2.zero, Vector2.zero,
                UIHelper.GlowTop, UIHelper.GlowBottom, UIHelper.Accent);

            var title = new GameObject("Title"); title.transform.SetParent(createCard.transform, false);
            var tR = title.AddComponent<RectTransform>(); tR.anchorMin = new Vector2(0.1f, 0.60f); tR.anchorMax = new Vector2(0.9f, 0.95f); tR.offsetMin = Vector2.zero; tR.offsetMax = Vector2.zero;
            var tTxt = title.AddComponent<Text>(); tTxt.text = "创建队伍"; tTxt.alignment = TextAnchor.MiddleCenter; tTxt.fontSize = 22; tTxt.color = UIHelper.Accent; tTxt.font = _font;

            var desc = new GameObject("Desc"); desc.transform.SetParent(createCard.transform, false);
            var dR = desc.AddComponent<RectTransform>(); dR.anchorMin = new Vector2(0.1f, 0.30f); dR.anchorMax = new Vector2(0.9f, 0.55f); dR.offsetMin = Vector2.zero; dR.offsetMax = Vector2.zero;
            var dTxt = desc.AddComponent<Text>(); dTxt.text = "创建队伍，邀请好友一起冒险\n最多5人组队"; dTxt.alignment = TextAnchor.MiddleCenter; dTxt.fontSize = 14; dTxt.color = UIHelper.TextSecondary; dTxt.font = _font;

            UIHelper.MakeButton(createCard.transform, "CreateBtn", "创建队伍", _font,
                new Vector2(0.3f, 0.08f), new Vector2(0.7f, 0.25f), Vector2.zero, Vector2.zero,
                UIHelper.BtnConfirm, 18, () =>
                {
                    string teamId = System.Guid.NewGuid().ToString("N").Substring(0, 8);
                    PlayerPrefs.SetString(KeyTeamId, teamId);
                    PlayerPrefs.SetInt(KeyTeamLeader, 1); // 自己是队长
                    PlayerPrefs.Save();
                    if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("队伍已创建", $"队伍ID: {teamId}");
                    RefreshTabLast();
                });

            // 公开队伍列表
            var listTitle = new GameObject("ListTitle"); listTitle.transform.SetParent(_tabContent.transform, false);
            var ltR = listTitle.AddComponent<RectTransform>(); ltR.anchorMin = new Vector2(0, 0.45f); ltR.anchorMax = new Vector2(1, 0.52f); ltR.offsetMin = Vector2.zero; ltR.offsetMax = Vector2.zero;
            var ltTxt = listTitle.AddComponent<Text>(); ltTxt.text = "公开队伍"; ltTxt.alignment = TextAnchor.MiddleCenter; ltTxt.fontSize = 16; ltTxt.color = UIHelper.TextSecondary; ltTxt.font = _font;

            // Mock队伍列表
            string[] teamNames = { "速刷副本队", "Boss开荒队", "日常任务队", "竞技场车队" };
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                string tn = teamNames[Random.Range(0, teamNames.Length)];
                int members = Random.Range(1, 4);
                int maxM = 5;
                int leaderLv = Random.Range(15, 28);
                float y = 0.40f - i * 0.12f;

                var row = UIHelper.MakeGlowCard(_tabContent.transform, $"Team_{i}",
                    new Vector2(0.05f, y), new Vector2(0.95f, y + 0.10f), Vector2.zero, Vector2.zero,
                    new Color(0.06f, 0.04f, 0.08f, 0.92f), UIHelper.GlowBottom, UIHelper.BorderSubtle);

                var lblObj = new GameObject("Lbl"); lblObj.transform.SetParent(row.transform, false);
                var lr = lblObj.AddComponent<RectTransform>(); lr.anchorMin = new Vector2(0.03f, 0); lr.anchorMax = new Vector2(0.65f, 1); lr.offsetMin = new Vector2(8, 4); lr.offsetMax = new Vector2(-4, -4);
                var lbl = lblObj.AddComponent<Text>();
                lbl.text = $"{tn}  ({members}/{maxM}人)  队长Lv.{leaderLv}";
                lbl.alignment = TextAnchor.MiddleLeft; lbl.fontSize = 14; lbl.color = UIHelper.TextPrimary; lbl.font = _font; lbl.raycastTarget = false;

                UIHelper.MakeButton(row.transform, "JoinBtn", "加入", _font,
                    new Vector2(0.68f, 0.15f), new Vector2(0.96f, 0.85f), Vector2.zero, Vector2.zero,
                    UIHelper.BtnConfirm, 12, () =>
                    {
                        PlayerPrefs.SetString(KeyTeamId, $"join_{idx}");
                        PlayerPrefs.SetInt(KeyTeamLeader, 0);
                        PlayerPrefs.Save();
                        if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("已加入队伍", tn);
                        RefreshTabLast();
                    });
            }
        }
        else
        {
            bool isLeader = PlayerPrefs.GetInt(KeyTeamLeader, 0) == 1;

            // 队伍信息
            var teamCard = UIHelper.MakeGlowCard(_tabContent.transform, "TeamCard",
                new Vector2(0.05f, 0.50f), new Vector2(0.95f, 0.98f), Vector2.zero, Vector2.zero,
                new Color(0.06f, 0.04f, 0.08f, 0.92f), UIHelper.GlowTop, UIHelper.Accent);

            var title = new GameObject("Title"); title.transform.SetParent(teamCard.transform, false);
            var tR = title.AddComponent<RectTransform>(); tR.anchorMin = new Vector2(0.05f, 0.75f); tR.anchorMax = new Vector2(0.95f, 0.95f); tR.offsetMin = Vector2.zero; tR.offsetMax = Vector2.zero;
            var tTxt = title.AddComponent<Text>();
            tTxt.text = $"队伍 ({(isLeader ? "队长" : "成员")})";
            tTxt.alignment = TextAnchor.MiddleCenter; tTxt.fontSize = 20; tTxt.color = UIHelper.Accent; tTxt.font = _font;

            // 队员列表 (Mock + 玩家)
            var player = GameManager.Instance?.Player;
            string myName = PlayerProgressData.GetSavedCharacterName();
            if (string.IsNullOrEmpty(myName)) myName = CloudSaveManager.Instance?.CurrentUsername ?? "冒险者";
            int myCls = (int)(player?.HeroClass ?? HeroClass.Warrior);
            int myLv = player?.Stats?.Level ?? 1;

            var memberNames = new string[] { myName, "冰霜法师", "神圣牧师", "铁壁战士" };
            int[] memberCls = { myCls, 1, 2, 0 };
            int[] memberLv = { myLv, Random.Range(15, 28), Random.Range(15, 28), Random.Range(15, 28) };
            bool[] memberReady = { true, true, false, true };
            int memberCount = isLeader ? 4 : Random.Range(2, 5);

            for (int i = 0; i < memberCount; i++)
            {
                float y = 0.70f - i * 0.16f;
                var row = UIHelper.MakeGlowCard(teamCard.transform, $"Member_{i}",
                    new Vector2(0.05f, y), new Vector2(0.95f, y + 0.14f), Vector2.zero, Vector2.zero,
                    new Color(0.05f, 0.03f, 0.07f, 0.9f), UIHelper.GlowBottom, UIHelper.BorderSubtle);

                var lblObj = new GameObject("Lbl"); lblObj.transform.SetParent(row.transform, false);
                var lr = lblObj.AddComponent<RectTransform>(); lr.anchorMin = new Vector2(0.05f, 0); lr.anchorMax = new Vector2(0.7f, 1); lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;
                var lbl = lblObj.AddComponent<Text>();
                int clsIdx = Mathf.Clamp(memberCls[i], 0, 2);
                string cc = ColorUtility.ToHtmlStringRGBA(ClassColors[clsIdx]);
                string leaderIcon = (i == 0) ? "" : "";
                string readyText = memberReady[i] ? "<color=#55FF55>✓ 准备就绪</color>" : "<color=#FF6644>✗ 未准备</color>";
                lbl.text = $"{leaderIcon}<color=#{cc}>{ClassNames[clsIdx]}</color> {memberNames[i]}  Lv.{memberLv[i]}  {readyText}";
                lbl.alignment = TextAnchor.MiddleLeft; lbl.fontSize = 14; lbl.color = UIHelper.TextPrimary; lbl.font = _font; lbl.supportRichText = true; lbl.raycastTarget = false;

                if (isLeader && i > 0)
                {
                    UIHelper.MakeButton(row.transform, "KickBtn", "踢出", _font,
                        new Vector2(0.75f, 0.15f), new Vector2(0.95f, 0.85f), Vector2.zero, Vector2.zero,
                        UIHelper.BtnDanger, 11, () =>
                        {
                            if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("已踢出", memberNames[i]);
                        });
                }
            }

            // 底部操作
            if (isLeader)
            {
                UIHelper.MakeButton(_tabContent.transform, "InviteBtn", "邀请好友", _font,
                    new Vector2(0.1f, 0.05f), new Vector2(0.35f, 0.15f), Vector2.zero, Vector2.zero,
                    UIHelper.BtnConfirm, 14, () =>
                    {
                        var friends = GetFriends();
                        var online = friends.FindAll(f => f.status == 1);
                        if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("邀请发送", $"已邀请{online.Count}位在线好友");
                    });

                UIHelper.MakeButton(_tabContent.transform, "TransferBtn", "转让队长", _font,
                    new Vector2(0.38f, 0.05f), new Vector2(0.63f, 0.15f), Vector2.zero, Vector2.zero,
                    new Color(0.2f, 0.3f, 0.6f, 0.9f), 14, () =>
                    {
                        PlayerPrefs.SetInt(KeyTeamLeader, 0);
                        PlayerPrefs.Save();
                        if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("转让成功", "队长已转让");
                        RefreshTabLast();
                    });

                UIHelper.MakeButton(_tabContent.transform, "DisbandBtn", "解散队伍", _font,
                    new Vector2(0.66f, 0.05f), new Vector2(0.95f, 0.15f), Vector2.zero, Vector2.zero,
                    UIHelper.BtnDanger, 14, () =>
                    {
                        PlayerPrefs.DeleteKey(KeyTeamId);
                        PlayerPrefs.DeleteKey(KeyTeamLeader);
                        PlayerPrefs.Save();
                        if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("队伍已解散", "");
                        RefreshTabLast();
                    });
            }
            else
            {
                UIHelper.MakeButton(_tabContent.transform, "LeaveBtn", "退出队伍", _font,
                    new Vector2(0.3f, 0.05f), new Vector2(0.7f, 0.15f), Vector2.zero, Vector2.zero,
                    UIHelper.BtnDanger, 16, () =>
                    {
                        PlayerPrefs.DeleteKey(KeyTeamId);
                        PlayerPrefs.DeleteKey(KeyTeamLeader);
                        PlayerPrefs.Save();
                        if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("已退出队伍", "");
                        RefreshTabLast();
                    });
            }
        }
    }

    // ========================================================
    //  辅助
    // ========================================================
    private void RefreshTabLast()
    {
        if (_tabContent == null) return;
        for (int i = _tabContent.transform.childCount - 1; i >= 0; i--)
            Destroy(_tabContent.transform.GetChild(i).gameObject);
        switch (_currentTab)
        {
            case 0: BuildFriendListTab(); break;
            case 1: BuildRequestsTab(); break;
            case 2: BuildRecentTab(); break;
            case 3: BuildTeamTab(); break;
        }
    }

    private void OnDestroy() { if (panel != null) Destroy(panel); if (Instance == this) Instance = null; }
}
