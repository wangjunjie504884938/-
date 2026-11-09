using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 信息窗口 (设计图版) — 综合(频道聊天)/好友(好友列表+私聊)双Tab, 水墨底框。
/// 综合Tab: 左列频道框列表(世界频道仅村/城/镇区域解锁) + 右侧聊天记录(文本/语音回放/表情包气泡
///          +系统提示框+好友申请同意/拒绝) + 语音输入中覆盖层 + 点击头像下拉菜单。
/// 好友Tab: 左列搜索+好友在线/离线框列表(选中框, 男女标识, 组队按钮, 服务器实时数据) + 右侧私聊记录。
/// 底部输入条: 语音按钮(按住录音松开发送)/输入文本框/表情按钮(贴纸面板)/发送按钮。
/// 语音WAV存persistentDataPath本地回放。
/// 切图位于 Resources/Sprites/Chat/。局内HUD人像按钮(UIManager.ShowInfo)入口, Hub好友行直达好友聊天。
/// </summary>
public class InfoUI : UIPanelBase
{
    public static InfoUI Instance { get; private set; }

    private const float SC = 0.75f;              // 设计图2560宽 → 画布1920 (与InGameHud/CharacterDetailUI一致)
    private const int MaxFriends = 50;
    private const string WorldTip = "世界频道只在村，城，镇中开放使用";
    private const string ServerUrl = "http://39.107.141.107:5132";
    private const string ColPinkHex = "E8598A";

    private static Sprite S(string name) => Resources.Load<Sprite>($"Sprites/Chat/{name}");
    private static Sprite SClose() => Resources.Load<Sprite>("Sprites/CharacterDetail/close_btn");

    // --- 配色 (设计图采样) ---
    private static readonly Color ColText = new Color(0.91f, 0.90f, 0.88f);
    private static readonly Color ColTextDim = new Color(0.50f, 0.48f, 0.55f);
    private static readonly Color ColOnline = new Color(0.91f, 0.63f, 0.24f);
    private static readonly Color ColOffline = new Color(0.62f, 0.62f, 0.66f);
    private static readonly Color ColGold = new Color(0.85f, 0.70f, 0.24f);
    private static readonly Color ColPink = new Color(0.91f, 0.35f, 0.54f);
    private static readonly Color ColBlue = new Color(0.35f, 0.54f, 0.91f);
    private static readonly Color ColTabSelBg = new Color(0.29f, 0.29f, 0.33f, 0.9f);

    private static readonly Vector2 AC = new Vector2(0.5f, 0.5f);   // 中心锚点
    private static readonly string[] ChannelNames = { "世界频道", "组队频道", "系统频道", "当前频道" };

    private int _tab;                    // 0=综合 1=好友
    private int _channel = 2;            // 默认系统频道 (设计图白色选中态)
    private string _selectedFriend;
    private int _selectedIdx = -1;       // 行索引 (演示列表同名行, 选中框只亮一行)
    private string _searchText = "";
    private int _rowIdx;
    private int _menuItemIdx;

    private readonly Image[] _tabBg = new Image[2];
    private readonly Text[] _tabTxt = new Text[2];
    private GameObject _pageChat;
    private GameObject _inputBarRoot;
    private readonly Text[] _channelTxt = new Text[4];
    private Image _worldLockImg;
    private GameObject _pageFriend;
    private InputField _searchInput;
    private RectTransform _friendContent;
    private RectTransform _chatArea;
    private RectTransform _chatContent;
    private ScrollRect _chatScroll;
    private InputField _input;
    private GameObject _voiceOverlay;
    private GameObject _avatarMenu;
    private GameObject _avatarMenuBlocker;
    private Text _voiceDurText;
    private AudioSource _audioSrc;
    private bool _recording;
    private AudioClip _recordClip;
    private string _recordDevice;
    private float _recordStart;
    private GameObject _emojiPanel;
    private bool _worldUnlocked;
    private float _lockTimer;
    private bool _fetching;
    private List<FriendUI.FriendInfo> _liveFriends = new List<FriendUI.FriendInfo>();
    private List<FriendUI.FriendRequestData> _liveRequests = new List<FriendUI.FriendRequestData>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    protected override void OnDestroy()
    {
        if (Instance == this) Instance = null;
        base.OnDestroy();
    }

    protected override void OnHide()
    {
        StopVoiceRecord(false);   // 面板隐藏时丢弃未发送录音
        CloseAvatarMenu();
        CloseEmojiPanel();
        if (_voiceOverlay != null) _voiceOverlay.SetActive(false);
    }

    // =================== 构建 ===================

    protected override void BuildContent()
    {
        Canvas canvas = GameManager.EnsureCanvas();

        panel = new GameObject("InfoPanel");
        panel.transform.SetParent(canvas.transform, false);
        var pr = panel.AddComponent<RectTransform>();
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one;
        pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;

        // 全屏暗色底 (点击空白关闭)
        var backdrop = NewRect("Backdrop", panel.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var bdImg = backdrop.AddComponent<Image>();
        bdImg.color = new Color(0f, 0f, 0f, 0.55f);
        bdImg.raycastTarget = true;
        var bdBtn = backdrop.AddComponent<Button>();
        bdBtn.transition = Selectable.Transition.None;
        bdBtn.onClick.AddListener(ClosePanel);

        // 底框 (设计图素材1585x1123 ×0.75)
        var frame = NewRect("Frame", panel.transform, AC, AC, Vector2.zero, new Vector2(1189, 842));
        var frameImg = frame.AddComponent<Image>();
        frameImg.sprite = S("ChatBg");
        frameImg.raycastTarget = true;

        BuildTabs(frame);
        BuildCloseButton(frame);
        BuildDivider(frame);
        BuildChatArea(frame);
        BuildInputBar(frame);

        _pageChat = NewRect("PageChat", frame.transform, AC, AC, Vector2.zero, Vector2.zero).gameObject;
        BuildChannels(_pageChat.transform);
        _pageFriend = NewRect("PageFriend", frame.transform, AC, AC, Vector2.zero, Vector2.zero).gameObject;
        BuildFriendPage(_pageFriend.transform);

        SwitchTab(0);
        SwitchChannel(_channel);
    }

    private void BuildTabs(RectTransform frame)
    {
        string[] labels = { "综合", "好友" };
        float[] xs = { -412, -238 };
        for (int i = 0; i < 2; i++)
        {
            var tab = NewRect("Tab" + labels[i], frame.transform, AC, AC, new Vector2(xs[i], 304), new Vector2(140, 56));
            var img = tab.AddComponent<Image>();
            img.color = Color.clear;
            img.raycastTarget = true;
            var btn = tab.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            int page = i;
            btn.onClick.AddListener(() => SwitchTab(page));
            var txt = MakeText(tab.transform, "TabTxt", labels[i], 30, Color.white, TextAnchor.MiddleCenter);
            txt.rectTransform.anchorMin = Vector2.zero; txt.rectTransform.anchorMax = Vector2.one;
            txt.rectTransform.offsetMin = Vector2.zero; txt.rectTransform.offsetMax = Vector2.zero;
            _tabBg[i] = img;
            _tabTxt[i] = txt;
        }
    }

    private void SwitchTab(int tab)
    {
        _tab = tab;
        if (_pageChat != null) _pageChat.SetActive(tab == 0);
        if (_pageFriend != null) _pageFriend.SetActive(tab == 1);
        for (int i = 0; i < 2; i++)
        {
            bool sel = i == _tab;
            if (_tabBg[i] != null) _tabBg[i].color = sel ? ColTabSelBg : Color.clear;
            if (_tabTxt[i] != null) _tabTxt[i].color = sel ? ColText : ColTextDim;
        }
        if (tab == 1 && _friendContent != null) BuildFriendRows();
        if (_chatContent != null) RebuildChat();
    }

    private void BuildCloseButton(RectTransform frame)
    {
        // 紫色X骑右上角 (与角色详情同款切图)
        var go = NewRect("CloseBtn", frame.transform, AC, AC, new Vector2(513, 350), new Vector2(56, 56));
        var img = go.AddComponent<Image>();
        img.sprite = SClose();
        if (img.sprite == null) img.sprite = S("CloseBtn");
        img.raycastTarget = true;
        var btn = go.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(ClosePanel);
    }

    private void BuildDivider(RectTransform frame)
    {
        var div = NewRect("Divider", frame.transform, AC, AC, new Vector2(-18, 265), new Vector2(1058, 2));
        var img = div.AddComponent<Image>();
        img.color = new Color(0.23f, 0.21f, 0.26f, 0.9f);
        img.raycastTarget = false;
    }

    // =================== 综合Tab: 频道列表 ===================

    private void BuildChannels(Transform parent)
    {
        float[] ys = { 214, 129, 44, -41 };
        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            var row = NewRect("Channel_" + ChannelNames[i], parent, AC, AC, new Vector2(-416, ys[i]), new Vector2(248, 77));
            var img = row.AddComponent<Image>();
            img.sprite = S("ChannelFrame");
            img.raycastTarget = true;
            var btn = row.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => SwitchChannel(idx));
            var txt = MakeText(row.transform, "ChannelTxt", ChannelNames[i], 30, ColTextDim, TextAnchor.MiddleCenter);
            txt.rectTransform.anchorMin = Vector2.zero; txt.rectTransform.anchorMax = Vector2.one;
            txt.rectTransform.offsetMin = new Vector2(20, 0); txt.rectTransform.offsetMax = new Vector2(-46, 0);
            _channelTxt[i] = txt;

            if (i == 0)
            {
                // 世界频道锁 (设计图: 行右侧暗色锁; 仅在世界村/城/镇区域解锁, 图标换"解锁")
                _worldLockImg = Img(row.transform, "LockIcon", "Lock", new Vector2(100, -14), new Vector2(20, 29));
            }
        }
    }

    private void SwitchChannel(int idx)
    {
        if (idx == 0 && !_worldUnlocked) { Toast(WorldTip, ""); return; }   // 未到村/城/镇: 世界频道不可打开
        _channel = idx;
        for (int i = 0; i < 4; i++)
        {
            bool sel = i == _channel;
            if (_channelTxt[i] != null) _channelTxt[i].color = sel ? ColText : ColTextDim;
        }
        if (_chatContent != null) RebuildChat();
    }

    // =================== 好友Tab: 搜索+好友列表 ===================

    private void BuildFriendPage(Transform parent)
    {
        var searchObj = NewRect("SearchInput", parent, AC, AC, new Vector2(-450, 235), new Vector2(172, 31));
        var sImg = searchObj.AddComponent<Image>();
        sImg.sprite = S("SearchBox");
        _searchInput = searchObj.AddComponent<InputField>();
        _searchInput.lineType = InputField.LineType.SingleLine;
        var st = MakeText(searchObj.transform, "SearchText", "", 15, ColText, TextAnchor.MiddleLeft);
        st.rectTransform.anchorMin = Vector2.zero; st.rectTransform.anchorMax = Vector2.one;
        st.rectTransform.offsetMin = new Vector2(10, 0); st.rectTransform.offsetMax = new Vector2(-8, 0);
        var sph = MakeText(searchObj.transform, "SearchPlaceholder", "输入名字搜索", 15, ColTextDim, TextAnchor.MiddleLeft);
        sph.rectTransform.anchorMin = Vector2.zero; sph.rectTransform.anchorMax = Vector2.one;
        sph.rectTransform.offsetMin = new Vector2(10, 0); sph.rectTransform.offsetMax = new Vector2(-8, 0);
        _searchInput.textComponent = st;
        _searchInput.placeholder = sph;
        _searchInput.onValueChanged.AddListener(text => { _searchText = text ?? ""; BuildFriendRows(); });

        var searchBtnObj = NewRect("SearchBtn", parent, AC, AC, new Vector2(-318, 235), new Vector2(54, 30));
        var sbImg = searchBtnObj.AddComponent<Image>();
        sbImg.sprite = S("SearchFrame");
        sbImg.raycastTarget = true;
        var sbBtn = searchBtnObj.AddComponent<Button>();
        sbBtn.transition = Selectable.Transition.None;
        sbBtn.onClick.AddListener(BuildFriendRows);
        var sbTxt = MakeText(searchBtnObj.transform, "SearchBtnTxt", "搜索", 16, ColText, TextAnchor.MiddleCenter);
        sbTxt.rectTransform.anchorMin = Vector2.zero; sbTxt.rectTransform.anchorMax = Vector2.one;
        sbTxt.rectTransform.offsetMin = Vector2.zero; sbTxt.rectTransform.offsetMax = Vector2.zero;

        // 好友行滚动容器 (设计图区域 y200..-273)
        var scrollObj = NewRect("FriendScroll", parent, AC, AC, new Vector2(-416, -36), new Vector2(262, 473));
        var fsr = scrollObj.AddComponent<ScrollRect>();
        fsr.movementType = ScrollRect.MovementType.Clamped;
        fsr.scrollSensitivity = 20f;
        fsr.horizontal = false; fsr.vertical = true;
        var viewport = NewStretch("Viewport", scrollObj.transform);
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = new GameObject("FriendContent");
        content.transform.SetParent(viewport, false);
        var cr = content.AddComponent<RectTransform>();
        cr.anchorMin = new Vector2(0, 1); cr.anchorMax = new Vector2(1, 1);
        cr.pivot = new Vector2(0.5f, 1);
        cr.offsetMin = Vector2.zero; cr.offsetMax = Vector2.zero;
        var layout = content.AddComponent<VerticalLayoutGroup>();
        layout.childControlWidth = false; layout.childControlHeight = false;
        layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.spacing = 6;
        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fsr.content = cr;
        fsr.viewport = viewport;
        _friendContent = cr;
    }

    private struct FriendRow { public string Name; public int Level; public bool Online; }

    private void BuildFriendRows()
    {
        if (_friendContent == null) return;
        for (int i = _friendContent.childCount - 1; i >= 0; i--)
            Destroy(_friendContent.GetChild(i).gameObject);

        // 实时数据: 服务器拉取的列表优先, 断网回退本地缓存 (无任何假数据)
        var rows = new List<FriendRow>();
        var friends = _liveFriends.Count > 0 ? _liveFriends : FriendData.GetFriends();
        foreach (var f in friends)
        {
            if (!PassSearch(f.name)) continue;
            rows.Add(new FriendRow { Name = f.name, Level = f.level, Online = f.status == 1 });
        }

        for (int i = 0; i < rows.Count; i++)
        {
            // 外部入口(OpenFriendChat)未指定行索引时, 自动定位到首个同名行
            if (rows[i].Name == _selectedFriend && _selectedIdx < 0) _selectedIdx = i;
            BuildFriendRow(rows[i], i);
        }

        if (rows.Count == 0)
        {
            var empty = MakeText(_friendContent, "EmptyTxt", string.IsNullOrEmpty(_searchText) ? "暂无好友" : "未找到该好友", 18, ColTextDim, TextAnchor.MiddleCenter);
            Place(empty.rectTransform, 0, -20, 246, 30);
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(_friendContent);
    }

    private bool PassSearch(string name)
        => string.IsNullOrEmpty(_searchText) || (name != null && name.Contains(_searchText));

    private void BuildFriendRow(FriendRow f, int idx)
    {
        bool selected = f.Name == _selectedFriend && idx == _selectedIdx;
        var row = new GameObject("FriendRow_" + idx);
        row.transform.SetParent(_friendContent, false);
        var rr = row.AddComponent<RectTransform>();
        rr.sizeDelta = new Vector2(248, 77);

        if (selected)
        {
            var selImg = NewRect("SelFrame", row.transform, AC, AC, Vector2.zero, new Vector2(286, 115));
            var si = selImg.AddComponent<Image>();
            si.sprite = S("FriendSelFrame");
            si.raycastTarget = false;
        }

        var bg = NewRect("RowBg", row.transform, AC, AC, Vector2.zero, new Vector2(248, 77));
        var bgImg = bg.AddComponent<Image>();
        bgImg.sprite = S(f.Online ? "FriendOnlineFrame" : "FriendOfflineFrame");
        bgImg.raycastTarget = true;

        var avatar = Img(bg.transform, "Avatar", "SystemAvatar", new Vector2(-82, 0), new Vector2(57, 56));
        if (!f.Online) avatar.color = new Color(0.55f, 0.55f, 0.55f, 1f);

        var nameTxt = MakeText(bg.transform, "NameTxt", f.Name, 19, f.Online ? ColText : ColTextDim, TextAnchor.MiddleLeft);
        PlacePivotLeft(nameTxt.rectTransform, -41, 18, 120, 22);

        // 性别标识: 数据层暂无性别字段, 按名字哈希稳定推导 (装饰性)
        bool female = f.Name != null && Mathf.Abs(f.Name.GetHashCode()) % 2 == 0;
        var gender = Img(bg.transform, "GenderIcon", female ? "FemaleIcon" : "MaleIcon", new Vector2(84, 16), new Vector2(11, 20));
        gender.color = female ? ColPink : ColBlue;

        var levelTxt = MakeText(bg.transform, "LevelTxt", f.Level.ToString(), 15, ColGold, TextAnchor.MiddleLeft);
        PlacePivotLeft(levelTxt.rectTransform, -41, -4, 60, 18);

        var badge = Img(bg.transform, "StatusBadge", "OnlineOfflineFrame", new Vector2(30, -22), new Vector2(46, 24));
        var statusTxt = MakeText(bg.transform, "StatusTxt", f.Online ? "在线" : "离线", 15, f.Online ? ColOnline : ColOffline, TextAnchor.MiddleCenter);
        Place(statusTxt.rectTransform, 30, -22, 46, 24);

        var team = Img(bg.transform, "TeamBtn", "TeamBtn", new Vector2(98, -14), new Vector2(55, 32), true);
        // 组队按钮素材为无字金刷, 设计图上文字为叠加文本
        var teamTxt = MakeText(team.transform, "TeamBtnTxt", "组队", 15, Color.white, TextAnchor.MiddleCenter);
        teamTxt.rectTransform.anchorMin = Vector2.zero; teamTxt.rectTransform.anchorMax = Vector2.one;
        teamTxt.rectTransform.offsetMin = Vector2.zero; teamTxt.rectTransform.offsetMax = Vector2.zero;
        var teamBtn = team.gameObject.AddComponent<Button>();
        teamBtn.transition = Selectable.Transition.None;
        teamBtn.targetGraphic = team;
        string teamName = f.Name;
        teamBtn.onClick.AddListener(() => Toast("组队邀请", $"已向 {teamName} 发送组队邀请"));

        var rowBtn = bg.AddComponent<Button>();
        rowBtn.transition = Selectable.Transition.None;
        rowBtn.onClick.AddListener(() => SelectFriend(teamName, idx));
    }

    private void SelectFriend(string name) => SelectFriend(name, -1);

    private void SelectFriend(string name, int idx)
    {
        _selectedFriend = name;
        _selectedIdx = idx;
        BuildFriendRows();
        RebuildChat();
    }

    /// <summary>外部入口: 打开好友Tab并定位到指定好友 (Hub好友行传入"名字  Lv.X"格式)</summary>
    public void OpenFriendChat(string rawName)
    {
        string name = rawName ?? "";
        int idx = name.IndexOf("  Lv.");
        if (idx > 0) name = name.Substring(0, idx);
        _selectedFriend = name;
        _searchText = "";
        if (_searchInput != null) _searchInput.text = "";
        SwitchTab(1);
        BuildFriendRows();
        RebuildChat();
    }

    // =================== 聊天区 ===================

    private void BuildChatArea(RectTransform frame)
    {
        var area = NewRect("ChatArea", frame.transform, AC, AC, new Vector2(117, -9), new Vector2(786, 528));
        var areaImg = area.AddComponent<Image>();
        areaImg.color = new Color(0f, 0f, 0f, 0.20f);
        areaImg.raycastTarget = false;
        _chatArea = area;

        var scrollObj = NewStretch("ChatScroll", area);
        _chatScroll = scrollObj.AddComponent<ScrollRect>();
        _chatScroll.movementType = ScrollRect.MovementType.Clamped;
        _chatScroll.scrollSensitivity = 30f;
        _chatScroll.horizontal = false; _chatScroll.vertical = true;

        var viewport = NewStretch("Viewport", scrollObj.transform);
        viewport.gameObject.AddComponent<RectMask2D>();

        var content = new GameObject("Content");
        content.transform.SetParent(viewport, false);
        var cr = content.AddComponent<RectTransform>();
        cr.anchorMin = new Vector2(0, 1); cr.anchorMax = new Vector2(1, 1);
        cr.pivot = new Vector2(0.5f, 1);
        cr.offsetMin = Vector2.zero; cr.offsetMax = Vector2.zero;
        var layout = content.AddComponent<VerticalLayoutGroup>();
        layout.childControlWidth = false; layout.childControlHeight = false;
        layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.spacing = 8;
        layout.padding = new RectOffset(8, 8, 8, 8);
        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _chatScroll.content = cr;
        _chatScroll.viewport = viewport;
        _chatContent = cr;

        var voice = Img(area, "VoiceOverlay", "VoiceInputting", new Vector2(0, -117), new Vector2(308, 122));
        _voiceOverlay = voice.gameObject;
        _voiceOverlay.SetActive(false);
        _voiceDurText = MakeText(voice.transform, "VoiceDurText", "0.0″", 22, Color.white, TextAnchor.MiddleCenter);
        Place(_voiceDurText.rectTransform, 0, -30, 120, 28);
    }

    private void BuildInputBar(RectTransform frame)
    {
        _inputBarRoot = NewRect("InputBarRoot", frame.transform, AC, AC, Vector2.zero, Vector2.zero).gameObject;
        // 语音按钮 (按住录音, 松开发送)
        var mic = Img(_inputBarRoot.transform, "VoiceBtn", "VoiceBtn", new Vector2(-208, -307), new Vector2(40, 40), true);
        var micBtn = mic.gameObject.AddComponent<Button>();
        micBtn.transition = Selectable.Transition.None;
        micBtn.targetGraphic = mic;
        var hold = mic.gameObject.AddComponent<PressHold>();
        hold.OnHold = on => { if (on) StartVoiceRecord(); else StopVoiceRecord(true); };

        // 输入框
        var inputObj = NewRect("ChatInput", _inputBarRoot.transform, AC, AC, new Vector2(56, -307), new Vector2(472, 35));
        var inImg = inputObj.AddComponent<Image>();
        inImg.sprite = S("InputBox");
        _input = inputObj.AddComponent<InputField>();
        _input.lineType = InputField.LineType.SingleLine;
        var itxt = MakeText(inputObj.transform, "InputText", "", 18, ColText, TextAnchor.MiddleLeft);
        itxt.rectTransform.anchorMin = Vector2.zero; itxt.rectTransform.anchorMax = Vector2.one;
        itxt.rectTransform.offsetMin = new Vector2(16, 2); itxt.rectTransform.offsetMax = new Vector2(-12, -2);
        var ph = MakeText(inputObj.transform, "Placeholder", "请点击输入", 18, new Color(0.45f, 0.44f, 0.5f, 1f), TextAnchor.MiddleLeft);
        ph.rectTransform.anchorMin = Vector2.zero; ph.rectTransform.anchorMax = Vector2.one;
        ph.rectTransform.offsetMin = new Vector2(16, 2); ph.rectTransform.offsetMax = new Vector2(-12, -2);
        _input.textComponent = itxt;
        _input.placeholder = ph;
        _input.onEndEdit.AddListener(text => { if (Input.GetKeyDown(KeyCode.Return)) OnSend(); });

        // 表情按钮
        var emoji = Img(_inputBarRoot.transform, "EmojiBtn", "EmojiBtn", new Vector2(321, -307), new Vector2(40, 40), true);
        var emojiBtn = emoji.gameObject.AddComponent<Button>();
        emojiBtn.transition = Selectable.Transition.None;
        emojiBtn.targetGraphic = emoji;
        emojiBtn.onClick.AddListener(ToggleEmojiPanel);

        // 发送按钮
        var send = Img(_inputBarRoot.transform, "SendBtn", "SendBtn", new Vector2(438, -307), new Vector2(133, 52), true);
        var sendBtn = send.gameObject.AddComponent<Button>();
        sendBtn.transition = Selectable.Transition.None;
        sendBtn.targetGraphic = send;
        sendBtn.onClick.AddListener(OnSend);
        // 发送按键素材为无字笔刷, 设计图上白字为叠加文本
        var sendTxt = MakeText(send.transform, "SendBtnTxt", "发送", 24, Color.white, TextAnchor.MiddleCenter);
        sendTxt.rectTransform.anchorMin = Vector2.zero; sendTxt.rectTransform.anchorMax = Vector2.one;
        sendTxt.rectTransform.offsetMin = Vector2.zero; sendTxt.rectTransform.offsetMax = Vector2.zero;
    }

    private void OnSend()
    {
        if (_tab == 0 && _channel == 0 && !_worldUnlocked) { Toast(WorldTip, ""); return; }
        if (_input == null || string.IsNullOrEmpty(_input.text)) return;
        string msg = _input.text.Trim();
        if (string.IsNullOrEmpty(msg)) return;
        if (_tab == 1)
        {
            if (string.IsNullOrEmpty(_selectedFriend)) { Toast("请先选择好友", ""); return; }
            EnsureChatData().SendMessage(_selectedFriend, msg);
        }
        else
        {
            EnsureChatData().SendChannelMessage(ChannelNames[_channel], msg);
        }
        _input.text = "";
        RebuildChat();
    }

    private void RebuildChat()
    {
        if (_chatContent == null) return;
        for (int i = _chatContent.childCount - 1; i >= 0; i--)
            Destroy(_chatContent.GetChild(i).gameObject);
        CloseAvatarMenu();
        _rowIdx = 0;

        var data = EnsureChatData();
        if (_tab == 1)
        {
            if (string.IsNullOrEmpty(_selectedFriend))
            {
                AddHintRow("请选择好友查看聊天");
            }
            else
            {
                var history = data.GetHistory(_selectedFriend);
                if (history.Count == 0) AddHintRow($"开始与 {_selectedFriend} 的对话...");
                foreach (var line in history) AppendChatLine(line, false);
            }
        }
        else if (_channel == 0 && !_worldUnlocked)
        {
            // 世界频道锁定 (非村/城/镇区域): 只显示开放范围提示, 不可发言
            BuildTipRow(WorldTip);
        }
        else
        {
            bool any = false;
            if (_channel == 2)
            {
                var requests = _liveRequests.Count > 0 ? _liveRequests : FriendData.GetRequests();
                foreach (var req in requests) { BuildRequestRow(req); any = true; }
            }
            var history = data.GetChannelHistory(ChannelNames[_channel]);
            foreach (var line in history) { AppendChatLine(line, true); any = true; }
            if (!any) AddHintRow("暂无消息");
        }

        Canvas.ForceUpdateCanvases();
        if (_chatScroll != null) _chatScroll.verticalNormalizedPosition = 0f;
        Canvas.ForceUpdateCanvases();
    }

    /// <summary>解析一行聊天记录: "[系统] x" / "[HH:mm] 我: x" / "[HH:mm] 名字: x"</summary>
    private void AppendChatLine(string line, bool allowSystem)
    {
        if (string.IsNullOrEmpty(line)) return;
        if (allowSystem && line.StartsWith("[系统]"))
        {
            BuildTipRow(line.Substring(4).Trim());
            return;
        }
        int close = line.IndexOf(']');
        if (close < 1) return;
        string rest = line.Substring(close + 1).TrimStart();
        if (rest.StartsWith("我: ")) BuildOwnRow(rest.Substring(3));
        else
        {
            int sep = rest.IndexOf(": ");
            if (sep > 0) BuildOpponentRow(rest.Substring(0, sep), rest.Substring(sep + 2));
            else BuildOwnRow(rest);
        }
    }

    private RectTransform NewRow(string name, float height)
    {
        var row = new GameObject(name + "_" + _rowIdx);
        _rowIdx++;
        row.transform.SetParent(_chatContent, false);
        var r = row.AddComponent<RectTransform>();
        r.sizeDelta = new Vector2(770, height);
        return r;
    }

    private void AddHintRow(string text)
    {
        var row = NewRow("HintRow", 40);
        var txt = MakeText(row, "HintTxt", text, 18, ColTextDim, TextAnchor.MiddleCenter);
        Place(txt.rectTransform, 0, 0, 500, 30);
    }

    private void BuildTipRow(string text)
    {
        var row = NewRow("TipRow", 58);
        var tip = NewRect("TipBox", row, AC, AC, Vector2.zero, new Vector2(770, 58));
        var img = tip.AddComponent<Image>();
        img.sprite = S("SystemTipBox");
        img.raycastTarget = false;
        var txt = MakeText(tip.transform, "TipTxt", text, 19, ColText, TextAnchor.MiddleCenter);
        txt.rectTransform.anchorMin = Vector2.zero; txt.rectTransform.anchorMax = Vector2.one;
        txt.rectTransform.offsetMin = new Vector2(30, 0); txt.rectTransform.offsetMax = new Vector2(-30, 0);
    }

    private void BuildOwnRow(string content)
    {
        var row = NewRow("OwnRow", 64);

        var nameTxt = MakeText(row, "OwnNameTxt", MyName(), 14, ColTextDim, TextAnchor.MiddleRight);
        PlacePivotRight(nameTxt.rectTransform, 323, 20, 300, 16);

        var avatar = Img(row, "OwnAvatar", "SystemAvatar", new Vector2(345, -2), new Vector2(50, 50));

        if (content.StartsWith("[表情]")) { BuildStickerImg(row, true, content.Substring(4)); return; }
        if (content.StartsWith("[语音:")) { BuildVoiceBubble(row, true, content); return; }

        float bw = Mathf.Clamp(content.Length * 17f + 30f, 90f, 143f);
        var bubble = new GameObject("OwnBubble");
        bubble.transform.SetParent(row, false);
        var br = bubble.AddComponent<RectTransform>();
        br.anchorMin = AC; br.anchorMax = AC; br.pivot = new Vector2(1f, 0.5f);
        br.anchoredPosition = new Vector2(322, -8);
        br.sizeDelta = new Vector2(bw, 33);
        var bi = bubble.AddComponent<Image>();
        bi.sprite = S("PlayerBubble");
        bi.raycastTarget = false;

        var bt = MakeText(bubble.transform, "BubbleTxt", content, 17, ColText, TextAnchor.MiddleCenter);
        bt.rectTransform.anchorMin = Vector2.zero; bt.rectTransform.anchorMax = Vector2.one;
        bt.rectTransform.offsetMin = new Vector2(10, 0); bt.rectTransform.offsetMax = new Vector2(-10, 0);
    }

    private void BuildOpponentRow(string sender, string content)
    {
        var row = NewRow("OppRow", 64);

        var nameTxt = MakeText(row, "OppNameTxt", sender, 14, ColTextDim, TextAnchor.MiddleLeft);
        PlacePivotLeft(nameTxt.rectTransform, -323, 20, 300, 16);

        var avatar = Img(row, "OppAvatar", "SystemAvatar", new Vector2(-345, -2), new Vector2(50, 50), true);
        var avatarBtn = avatar.gameObject.AddComponent<Button>();
        avatarBtn.transition = Selectable.Transition.None;
        string target = sender;
        avatarBtn.onClick.AddListener(() => ShowAvatarMenu(avatar.rectTransform, target));

        if (content.StartsWith("[表情]")) { BuildStickerImg(row, false, content.Substring(4)); return; }
        if (content.StartsWith("[语音:")) { BuildVoiceBubble(row, false, content); return; }

        float bw = Mathf.Clamp(content.Length * 17f + 30f, 90f, 314f);
        var bubble = new GameObject("OppBubble");
        bubble.transform.SetParent(row, false);
        var br = bubble.AddComponent<RectTransform>();
        br.anchorMin = AC; br.anchorMax = AC; br.pivot = new Vector2(0f, 0.5f);
        br.anchoredPosition = new Vector2(-323, -8);
        br.sizeDelta = new Vector2(bw, 33);
        var bi = bubble.AddComponent<Image>();
        bi.sprite = S("OpponentBubble");
        bi.raycastTarget = false;

        var bt = MakeText(bubble.transform, "BubbleTxt", content, 17, ColText, TextAnchor.MiddleCenter);
        bt.rectTransform.anchorMin = Vector2.zero; bt.rectTransform.anchorMax = Vector2.one;
        bt.rectTransform.offsetMin = new Vector2(10, 0); bt.rectTransform.offsetMax = new Vector2(-10, 0);
    }

    /// <summary>表情消息: 贴纸图直接展示, 不套气泡框</summary>
    private void BuildStickerImg(RectTransform row, bool own, string stickerName)
    {
        var sprite = Sticker(stickerName);
        if (sprite == null) return;
        var r = NewRect("StickerImg", row, AC, own ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f),
            new Vector2(own ? 322 : -323, -8), new Vector2(56, 56));
        var img = r.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
    }

    /// <summary>语音消息气泡: [语音:文件名:时长秒], 点击回放本地录音</summary>
    private void BuildVoiceBubble(RectTransform row, bool own, string content)
    {
        int p1 = content.IndexOf(':');
        int p2 = content.LastIndexOf(':');
        string file = p2 > p1 ? content.Substring(p1 + 1, p2 - p1 - 1) : "";
        string sec = p2 > p1 ? content.Substring(p2 + 1).TrimEnd(']') : "";

        float bw = 110;
        var bubble = new GameObject(own ? "OwnBubble" : "OppBubble");
        bubble.transform.SetParent(row, false);
        var br = bubble.AddComponent<RectTransform>();
        br.anchorMin = AC; br.anchorMax = AC;
        br.pivot = own ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
        br.anchoredPosition = new Vector2(own ? 322 : -323, -8);
        br.sizeDelta = new Vector2(bw, 33);
        var bi = bubble.AddComponent<Image>();
        bi.sprite = S(own ? "PlayerBubble" : "OpponentBubble");
        bi.raycastTarget = true;

        var vt = MakeText(bubble.transform, "VoiceTxt", sec, 17, ColText, TextAnchor.MiddleLeft);
        PlacePivotLeft(vt.rectTransform, 12, 0, 60, 22);
        var sym = Img(bubble.transform, "VoiceIcon", "VoiceSymbol", new Vector2(bw - 24, 0), new Vector2(15, 20));

        var btn = bubble.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        string f = file;
        btn.onClick.AddListener(() => PlayVoice(f));
    }

    private void BuildRequestRow(FriendUI.FriendRequestData req)
    {
        var row = NewRow("ReqRow", 56);
        var avatar = Img(row, "ReqAvatar", "SystemAvatar", new Vector2(-350, 0), new Vector2(44, 44));
        var txt = MakeText(row, "ReqTxt", "", 15, ColText, TextAnchor.MiddleLeft);
        txt.supportRichText = true;
        txt.text = $"<color=#{ColPinkHex}>{req.fromName}</color> 申请添加好友";
        PlacePivotLeft(txt.rectTransform, -322, 8, 380, 20);

        // ✓/✗ 紧随申请文字之后 (设计图: 文字结束+约15px间距)
        float textEnd = -322 + (req.fromName.Length + 7) * 15f;
        var agree = Img(row, "AgreeBtnImg", "AgreeBtn", new Vector2(textEnd + 20, 0), new Vector2(29, 27), true);
        var agreeBtn = agree.gameObject.AddComponent<Button>();
        agreeBtn.transition = Selectable.Transition.None;
        agreeBtn.targetGraphic = agree;
        string fromName = req.fromName;
        int reqId = req.requestId;
        agreeBtn.onClick.AddListener(() => AcceptRequest(fromName, reqId));

        var refuse = Img(row, "RefuseBtnImg", "RefuseBtn", new Vector2(textEnd + 62, 0), new Vector2(29, 27), true);
        var refuseBtn = refuse.gameObject.AddComponent<Button>();
        refuseBtn.transition = Selectable.Transition.None;
        refuseBtn.targetGraphic = refuse;
        refuseBtn.onClick.AddListener(() => RejectRequest(fromName, reqId));
    }

    private void AcceptRequest(string fromName, int requestId) => StartCoroutine(AcceptRequestCo(fromName, requestId));

    private System.Collections.IEnumerator AcceptRequestCo(string fromName, int requestId)
    {
        string token = CloudSaveManager.GetAuthToken();
        bool serverOk = false;
        if (!string.IsNullOrEmpty(token) && requestId > 0)
        {
            using (var req = new UnityWebRequest($"{ServerUrl}/api/friend/accept", "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes($"{{\"requestId\":{requestId}}}"));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", "Bearer " + token);
                req.timeout = 5;
                yield return req.SendWebRequest();
                serverOk = req.result == UnityWebRequest.Result.Success;
            }
        }

        if (serverOk)
        {
            // 服务器权威: 接受后重拉好友+申请
            Toast("已添加好友", fromName);
            StartCoroutine(FetchServerData());
        }
        else
        {
            // 离线fallback: 本地处理
            var requests = FriendData.GetRequests();
            requests.RemoveAll(r => r.fromName == fromName);
            FriendData.SaveRequests(requests);
            var friends = FriendData.GetFriends();
            if (friends.Count < MaxFriends && friends.FindIndex(f => f.name == fromName) < 0)
            {
                friends.Add(new FriendUI.FriendInfo
                {
                    name = fromName, cls = 0, level = 1, gearScore = 0, status = 1,
                    lastActive = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                });
                FriendData.SaveFriends(friends);
            }
            Toast("已添加好友", fromName);
            BuildFriendRows();
            RebuildChat();
        }
    }

    private void RejectRequest(string fromName, int requestId) => StartCoroutine(RejectRequestCo(fromName, requestId));

    private System.Collections.IEnumerator RejectRequestCo(string fromName, int requestId)
    {
        string token = CloudSaveManager.GetAuthToken();
        bool serverOk = false;
        if (!string.IsNullOrEmpty(token) && requestId > 0)
        {
            using (var req = new UnityWebRequest($"{ServerUrl}/api/friend/reject", "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes($"{{\"requestId\":{requestId}}}"));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", "Bearer " + token);
                req.timeout = 5;
                yield return req.SendWebRequest();
                serverOk = req.result == UnityWebRequest.Result.Success;
            }
        }

        if (serverOk)
        {
            StartCoroutine(FetchServerData());
        }
        else
        {
            var requests = FriendData.GetRequests();
            requests.RemoveAll(r => (requestId > 0 && r.requestId == requestId) || r.fromName == fromName);
            FriendData.SaveRequests(requests);
            RebuildChat();
        }
    }

    // =================== 头像下拉菜单 ===================

    private void ShowAvatarMenu(RectTransform avatarRect, string playerName)
    {
        CloseAvatarMenu();
        _menuItemIdx = 0;

        _avatarMenuBlocker = NewStretch("AvatarMenuBlocker", _chatArea).gameObject;
        var bImg = _avatarMenuBlocker.AddComponent<Image>();
        bImg.color = Color.clear;
        bImg.raycastTarget = true;
        var bBtn = _avatarMenuBlocker.AddComponent<Button>();
        bBtn.transition = Selectable.Transition.None;
        bBtn.onClick.AddListener(CloseAvatarMenu);

        _avatarMenu = NewRect("AvatarMenu", _chatArea, AC, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(100, 60)).gameObject;
        var amr = (RectTransform)_avatarMenu.transform;
        Vector2 local = _chatArea.InverseTransformPoint(avatarRect.position);
        amr.anchoredPosition = local + new Vector2(34, 4);

        MakeMenuItem("添加好友", () => { AddFriendFromChat(playerName); CloseAvatarMenu(); });
        MakeMenuItem("邀请组队", () => { Toast("组队邀请", $"已向 {playerName} 发送组队邀请"); CloseAvatarMenu(); });
    }

    private void MakeMenuItem(string label, System.Action onClick)
    {
        var item = NewRect("MenuItem_" + label, _avatarMenu.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0, -15 - _menuItemIdx * 30), new Vector2(100, 29));
        _menuItemIdx++;
        var img = item.AddComponent<Image>();
        img.sprite = S("AvatarMenuFrame");
        img.raycastTarget = true;
        var btn = item.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(() => onClick());
        var txt = MakeText(item.transform, "ItemTxt", label, 15, ColText, TextAnchor.MiddleCenter);
        txt.rectTransform.anchorMin = Vector2.zero; txt.rectTransform.anchorMax = Vector2.one;
        txt.rectTransform.offsetMin = Vector2.zero; txt.rectTransform.offsetMax = Vector2.zero;
    }

    private void CloseAvatarMenu()
    {
        if (_avatarMenu != null) Destroy(_avatarMenu);
        _avatarMenu = null;
        if (_avatarMenuBlocker != null) Destroy(_avatarMenuBlocker);
        _avatarMenuBlocker = null;
    }

    private void AddFriendFromChat(string playerName)
    {
        var friends = FriendData.GetFriends();
        if (friends.FindIndex(f => f.name == playerName) >= 0) { Toast("已是好友", playerName); return; }
        if (friends.Count >= MaxFriends) { Toast("好友已满", $"上限{MaxFriends}人"); return; }
        friends.Add(new FriendUI.FriendInfo
        {
            name = playerName, cls = 0, level = 1, gearScore = 0, status = 1,
            lastActive = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });
        FriendData.SaveFriends(friends);
        Toast("已添加好友", playerName);
        if (_tab == 1) BuildFriendRows();
    }

    // =================== 语音消息 (录音/回放) ===================

    private bool HasVoiceTarget()
        => _tab == 1 ? !string.IsNullOrEmpty(_selectedFriend) : !(_channel == 0 && !_worldUnlocked);

    private void StartVoiceRecord()
    {
        if (_recording) return;
        if (!HasVoiceTarget()) { Toast(_tab == 1 ? "请先选择好友" : WorldTip, ""); return; }
        if (Microphone.devices == null || Microphone.devices.Length == 0) { Toast("未检测到麦克风", ""); return; }
        _recordDevice = Microphone.devices[0];
        _recordClip = Microphone.Start(_recordDevice, false, 60, ChatDataManager.VoiceFrequency);
        _recordStart = Time.unscaledTime;
        _recording = true;
        if (_voiceOverlay != null) _voiceOverlay.SetActive(true);
        if (_voiceDurText != null) _voiceDurText.text = "0.0″";
    }

    private void StopVoiceRecord(bool send)
    {
        if (!_recording) return;
        _recording = false;
        float elapsed = Time.unscaledTime - _recordStart;
        if (_voiceOverlay != null) _voiceOverlay.SetActive(false);

        AudioClip clip = _recordClip;
        _recordClip = null;
        if (clip == null) return;

        int pos = 0;
        if (Microphone.IsRecording(_recordDevice))
        {
            pos = Microphone.GetPosition(_recordDevice);
            Microphone.End(_recordDevice);
        }
        float dur = pos > 0 ? Mathf.Min(pos / (float)ChatDataManager.VoiceFrequency, elapsed) : Mathf.Min(elapsed, 60f);
        if (!send) return;
        if (dur < 0.5f) { Toast("说话时间太短", ""); return; }

        int count = Mathf.Clamp((int)(dur * ChatDataManager.VoiceFrequency), 1, clip.samples);
        var samples = new float[count];
        clip.GetData(samples, 0);
        string file = ChatDataManager.SaveVoiceWav(samples, ChatDataManager.VoiceFrequency);
        string msg = $"[语音:{file}:{dur.ToString("F1")}秒]";
        if (_tab == 1) EnsureChatData().SendMessage(_selectedFriend, msg);
        else EnsureChatData().SendChannelMessage(ChannelNames[_channel], msg);
        RebuildChat();
    }

    private void PlayVoice(string filename)
    {
        var clip = ChatDataManager.LoadVoiceClip(filename);
        if (clip == null) { Toast("语音已失效", ""); return; }
        if (_audioSrc == null) _audioSrc = gameObject.AddComponent<AudioSource>();
        _audioSrc.spatialBlend = 0f;
        _audioSrc.PlayOneShot(clip);
    }

    // =================== 表情包 ===================

    private static readonly string[] StickerNames = { "微笑", "大笑", "哭泣", "生气", "惊讶", "爱心", "无语", "睡觉" };
    private static readonly Dictionary<string, Sprite> _stickerCache = new Dictionary<string, Sprite>();

    private static Sprite Sticker(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (_stickerCache.TryGetValue(name, out var cached) && cached != null) return cached;
        if (System.Array.IndexOf(StickerNames, name) < 0) return null;
        var s = CreateStickerSprite(name);
        _stickerCache[name] = s;
        return s;
    }

    /// <summary>程序化卡通表情 (抗锯齿圆脸+五官, 96x96)</summary>
    private static Sprite CreateStickerSprite(string name)
    {
        const int S = 96;
        var px = new Color[S * S];
        float cx = S / 2f - 0.5f, cy = S / 2f - 0.5f;
        Color face = name == "生气" ? new Color(1f, 0.60f, 0.40f) : new Color(1f, 0.84f, 0.35f);
        Color dark = new Color(0.32f, 0.22f, 0.14f);
        Color red = new Color(0.90f, 0.28f, 0.30f);
        Color blue = new Color(0.35f, 0.62f, 0.95f);

        void Blend(int x, int y, Color c, float a)
        {
            if (x < 0 || y < 0 || x >= S || y >= S) return;
            px[y * S + x] = Color.Lerp(px[y * S + x], c, Mathf.Clamp01(a));
        }
        void Circle(float x, float y, float r, Color c)
        {
            for (int iy = Mathf.FloorToInt(y - r) - 1; iy <= y + r + 1; iy++)
                for (int ix = Mathf.FloorToInt(x - r) - 1; ix <= x + r + 1; ix++)
                {
                    float d = Vector2.Distance(new Vector2(ix, iy), new Vector2(x, y));
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    if (a > 0) Blend(ix, iy, c, a);
                }
        }
        void Line(float x1, float y1, float x2, float y2, float r, Color c)
        {
            int steps = Mathf.CeilToInt(Vector2.Distance(new Vector2(x1, y1), new Vector2(x2, y2)) * 2f) + 1;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Circle(Mathf.Lerp(x1, x2, t), Mathf.Lerp(y1, y2, t), r, c);
            }
        }
        void Arc(float x, float y, float r, float a0, float a1, float th, Color c)
        {
            int steps = Mathf.CeilToInt(Mathf.Abs(a1 - a0) / 6f);
            for (int i = 0; i <= steps; i++)
            {
                float ang = Mathf.Lerp(a0, a1, i / (float)steps) * Mathf.Deg2Rad;
                Circle(x + Mathf.Cos(ang) * r, y + Mathf.Sin(ang) * r, th, c);
            }
        }

        float ex = 14f, ey = 8f;
        if (name == "爱心")
        {
            Circle(cx - 13, cy + 12, 21, red);
            Circle(cx + 13, cy + 12, 21, red);
            for (int iy = -20; iy <= 10; iy++)
            {
                float w = 2f + (iy + 20) / 30f * 26f;
                for (int ix = -Mathf.CeilToInt(w / 2f); ix <= w / 2f; ix++)
                    Circle(cx + ix, cy + iy, 2.2f, red);
            }
        }
        else
        {
            Circle(cx, cy, 41, new Color(0.25f, 0.18f, 0.10f, 0.9f));   // 描边
            Circle(cx, cy, 39, face);
            switch (name)
            {
                case "微笑":
                    Circle(cx - ex, cy + ey, 4.5f, dark);
                    Circle(cx + ex, cy + ey, 4.5f, dark);
                    Arc(cx, cy - 2f, 19f, 200f, 340f, 3f, dark);
                    break;
                case "大笑":
                    Arc(cx - ex, cy + 12f, 8f, 20f, 160f, 3f, dark);
                    Arc(cx + ex, cy + 12f, 8f, 20f, 160f, 3f, dark);
                    Circle(cx, cy - 12f, 13f, dark);
                    Circle(cx, cy - 8f, 9f, red);
                    break;
                case "哭泣":
                    Circle(cx - ex, cy + ey, 4.5f, dark);
                    Circle(cx + ex, cy + ey, 4.5f, dark);
                    Circle(cx - ex, cy - 12f, 4.5f, blue);
                    Arc(cx, cy - 16f, 14f, 20f, 160f, 3f, dark);
                    break;
                case "生气":
                    Line(cx - 22f, cy + 18f, cx - 7f, cy + 13f, 3f, dark);
                    Line(cx + 22f, cy + 18f, cx + 7f, cy + 13f, 3f, dark);
                    Circle(cx - ex, cy + 4f, 4.5f, dark);
                    Circle(cx + ex, cy + 4f, 4.5f, dark);
                    Arc(cx, cy - 18f, 14f, 20f, 160f, 3f, dark);
                    break;
                case "惊讶":
                    Circle(cx - ex, cy + ey, 8f, Color.white);
                    Circle(cx + ex, cy + ey, 8f, Color.white);
                    Circle(cx - ex, cy + ey, 3.5f, dark);
                    Circle(cx + ex, cy + ey, 3.5f, dark);
                    Circle(cx, cy - 16f, 8f, dark);
                    break;
                case "无语":
                    Line(cx - 20f, cy + ey, cx - 8f, cy + ey, 3f, dark);
                    Line(cx + 8f, cy + ey, cx + 20f, cy + ey, 3f, dark);
                    Line(cx - 12f, cy - 16f, cx + 12f, cy - 16f, 3f, dark);
                    break;
                case "睡觉":
                    Arc(cx - ex, cy + ey, 7f, 180f, 360f, 3f, dark);
                    Arc(cx + ex, cy + ey, 7f, 180f, 360f, 3f, dark);
                    Line(cx - 10f, cy - 16f, cx + 10f, cy - 16f, 3f, dark);
                    Line(cx + 20f, cy + 30f, cx + 32f, cy + 30f, 2.5f, dark);
                    Line(cx + 32f, cy + 30f, cx + 20f, cy + 16f, 2.5f, dark);
                    Line(cx + 20f, cy + 16f, cx + 32f, cy + 16f, 2.5f, dark);
                    Line(cx + 36f, cy + 44f, cx + 44f, cy + 44f, 2f, dark);
                    Line(cx + 44f, cy + 44f, cx + 36f, cy + 34f, 2f, dark);
                    Line(cx + 36f, cy + 34f, cx + 44f, cy + 34f, 2f, dark);
                    break;
            }
        }

        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.SetPixels(px);
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
    }

    private void ToggleEmojiPanel()
    {
        if (_emojiPanel != null) { CloseEmojiPanel(); return; }
        var panel = NewRect("EmojiPanel", _chatArea, AC, AC, new Vector2(150, -150), new Vector2(330, 180));
        var bg = panel.AddComponent<Image>();
        bg.color = new Color(0.07f, 0.05f, 0.11f, 0.97f);
        _emojiPanel = panel.gameObject;

        for (int i = 0; i < StickerNames.Length; i++)
        {
            string sn = StickerNames[i];
            float col = i % 4, row = i / 4;
            var cell = NewRect("Emoji_" + sn, panel.transform, AC, AC,
                new Vector2(-123 + col * 82, 42 - row * 84), new Vector2(64, 64));
            var img = cell.AddComponent<Image>();
            img.sprite = Sticker(sn);
            img.raycastTarget = true;
            var btn = cell.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => SendSticker(sn));
        }
    }

    private void SendSticker(string name)
    {
        CloseEmojiPanel();
        if (_tab == 1)
        {
            if (string.IsNullOrEmpty(_selectedFriend)) { Toast("请先选择好友", ""); return; }
            EnsureChatData().SendMessage(_selectedFriend, "[表情]" + name);
        }
        else
        {
            if (_channel == 0 && !_worldUnlocked) { Toast(WorldTip, ""); return; }
            EnsureChatData().SendChannelMessage(ChannelNames[_channel], "[表情]" + name);
        }
        RebuildChat();
    }

    private void CloseEmojiPanel()
    {
        if (_emojiPanel != null) Destroy(_emojiPanel);
        _emojiPanel = null;
    }

    // =================== 世界频道区域锁 (仅村/城/镇开放) ===================

    private bool IsWorldChatUnlocked()
    {
        var gm = GameManager.Instance;
        if (gm == null || !gm.InWorld) return false;
        var player = gm.Player;
        if (player == null) return false;
        var zone = WorldData.GetAt(player.transform.position.x, player.transform.position.y);
        if (zone == null || string.IsNullOrEmpty(zone.Name)) return false;
        return zone.Name.Contains("村") || zone.Name.Contains("镇") || zone.Name.Contains("城");
    }

    private void UpdateLockState()
    {
        bool unlocked = IsWorldChatUnlocked();
        if (unlocked == _worldUnlocked) return;
        _worldUnlocked = unlocked;
        if (_worldLockImg != null)
        {
            _worldLockImg.sprite = S(unlocked ? "Unlock" : "Lock");
            _worldLockImg.rectTransform.sizeDelta = unlocked ? new Vector2(20, 32) : new Vector2(20, 29);
        }
        if (_channel == 0 && _chatContent != null) RebuildChat();
    }

    private void Update()
    {
        if (_recording && _voiceDurText != null)
            _voiceDurText.text = (Time.unscaledTime - _recordStart).ToString("F1") + "″";
        _lockTimer += Time.unscaledDeltaTime;
        if (_lockTimer >= 0.5f) { _lockTimer = 0f; UpdateLockState(); }
    }

    // =================== 好友/申请 服务器实时数据 ===================

    private System.Collections.IEnumerator FetchServerData()
    {
        string token = CloudSaveManager.GetAuthToken();
        if (string.IsNullOrEmpty(token) || _fetching) yield break;
        _fetching = true;

        // 好友列表 (在线状态服务器权威; 缓存剥离status)
        using (var req = UnityWebRequest.Get($"{ServerUrl}/api/friend/list"))
        {
            req.SetRequestHeader("Authorization", "Bearer " + token);
            req.timeout = 5;
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var resp = JsonUtility.FromJson<FriendListResp>(req.downloadHandler.text);
                    if (resp?.friends != null)
                    {
                        var live = new List<FriendUI.FriendInfo>();
                        foreach (var f in resp.friends)
                        {
                            live.Add(new FriendUI.FriendInfo
                            {
                                name = f.username ?? $"ID:{f.friendId}",
                                cls = Mathf.Clamp(f.classType, 0, 2),
                                level = f.level > 0 ? f.level : 1,
                                status = f.online ? 1 : 0,
                            });
                        }
                        _liveFriends = live;
                        var toSave = new List<FriendUI.FriendInfo>(live);
                        for (int i = 0; i < toSave.Count; i++) { var fd = toSave[i]; fd.status = 0; toSave[i] = fd; }
                        FriendData.SaveFriends(toSave);
                    }
                }
                catch { }
            }
        }

        // 待处理好友申请
        using (var req = UnityWebRequest.Get($"{ServerUrl}/api/friend/requests"))
        {
            req.SetRequestHeader("Authorization", "Bearer " + token);
            req.timeout = 5;
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var resp = JsonUtility.FromJson<FriendReqListResp>(req.downloadHandler.text);
                    if (resp?.requests != null)
                    {
                        var live = new List<FriendUI.FriendRequestData>();
                        foreach (var r in resp.requests)
                        {
                            live.Add(new FriendUI.FriendRequestData
                            {
                                requestId = r.id,
                                fromName = r.fromName ?? "",
                                fromLevel = 0,
                                fromClass = 0,
                                message = r.message ?? "",
                            });
                        }
                        _liveRequests = live;
                        FriendData.SaveRequests(live);
                    }
                }
                catch { }
            }
        }

        _fetching = false;
        BuildFriendRows();
        RebuildChat();
    }

    [System.Serializable]
    private class FriendListResp { public FriendListRespItem[] friends; public int count; public int max; }
    [System.Serializable]
    private class FriendListRespItem { public int friendId; public string username; public int level; public int classType; public bool online; public string remark; }
    [System.Serializable]
    private class FriendReqListResp { public FriendReqItem[] requests; public int count; }
    [System.Serializable]
    private class FriendReqItem { public int id; public int fromUserId; public string fromName; public string message; }

    public override void RefreshData()
    {
        for (int i = 0; i < 4; i++)
            if (_channelTxt[i] != null) _channelTxt[i].color = i == _channel ? ColText : ColTextDim;
        UpdateLockState();
        if (_tab == 1 && _friendContent != null) BuildFriendRows();
        if (_chatContent != null) RebuildChat();
        StartCoroutine(FetchServerData());
    }

    private void ClosePanel()
    {
        Hide();
        UIHelper.ReturnFromPanel();
    }

    // =================== 工具 ===================

    private ChatDataManager EnsureChatData()
    {
        if (ChatDataManager.Instance == null)
            GameManager.Instance.gameObject.AddComponent<ChatDataManager>();
        return ChatDataManager.Instance;
    }

    private static void Toast(string title, string sub) => GameUI.Instance?.ShowItemPickupToast(title, sub);

    private static string MyName()
    {
        string name = PlayerProgressData.GetSavedCharacterName();
        if (string.IsNullOrEmpty(name)) name = CloudSaveManager.Instance?.CurrentUsername ?? "冒险者";
        return name;
    }

    private static RectTransform NewRect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var r = go.AddComponent<RectTransform>();
        r.anchorMin = anchor; r.anchorMax = anchor; r.pivot = pivot;
        r.anchoredPosition = pos; r.sizeDelta = size;
        return r;
    }

    private static RectTransform NewStretch(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var r = go.AddComponent<RectTransform>();
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        return r;
    }

    private static Text MakeText(Transform parent, string objName, string content, int fontSize, Color color, TextAnchor anchor)
    {
        var go = new GameObject(objName);
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.text = content;
        t.font = GameManager.GetUIFont();
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = anchor;
        t.raycastTarget = false;
        return t;
    }

    private static Image Img(Transform parent, string objName, string spriteName, Vector2 pos, Vector2 size, bool raycast = false)
    {
        var r = NewRect(objName, parent, AC, AC, pos, size);
        var img = r.AddComponent<Image>();
        img.sprite = S(spriteName);
        img.preserveAspect = true;
        img.raycastTarget = raycast;
        return img;
    }

    private static void Place(RectTransform r, float x, float y, float w, float h)
    {
        r.anchorMin = AC; r.anchorMax = AC; r.pivot = AC;
        r.anchoredPosition = new Vector2(x, y);
        r.sizeDelta = new Vector2(w, h);
    }

    private static void PlacePivotLeft(RectTransform r, float x, float y, float w, float h)
    {
        r.anchorMin = AC; r.anchorMax = AC; r.pivot = new Vector2(0f, 0.5f);
        r.anchoredPosition = new Vector2(x, y);
        r.sizeDelta = new Vector2(w, h);
    }

    private static void PlacePivotRight(RectTransform r, float x, float y, float w, float h)
    {
        r.anchorMin = AC; r.anchorMax = AC; r.pivot = new Vector2(1f, 0.5f);
        r.anchoredPosition = new Vector2(x, y);
        r.sizeDelta = new Vector2(w, h);
    }

    /// <summary>按住状态按钮 (语音输入)</summary>
    private class PressHold : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public System.Action<bool> OnHold;
        public void OnPointerDown(PointerEventData e) => OnHold?.Invoke(true);
        public void OnPointerUp(PointerEventData e) => OnHold?.Invoke(false);
        public void OnPointerExit(PointerEventData e) => OnHold?.Invoke(false);
    }
}
