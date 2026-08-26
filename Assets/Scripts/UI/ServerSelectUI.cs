using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 服务器选择页面 — 单区服居中大卡片
/// </summary>
public class ServerSelectUI : MonoBehaviour
{
    public static ServerSelectUI Instance { get; private set; }
    private GameObject _panel;
    private Font _font;
    private bool _shown;
    private Text _statusLabel;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Show(Font font)
    {
        _font = font;
        if (_panel == null) BuildUI();
        _panel.SetActive(true);
        _shown = true;
    }

    public void Hide() { if (_panel != null) _panel.SetActive(false); _shown = false; }
    public bool IsVisible => _shown;

    private void BuildUI()
    {
        Canvas canvas = GameManager.EnsureCanvas();
        _panel = new GameObject("ServerSelectPanel");
        _panel.transform.SetParent(canvas.transform, false);
        var pr = _panel.AddComponent<RectTransform>();
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one;
        pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;
        _panel.AddComponent<CanvasGroup>();

        // 暗色背景
        var bg = new GameObject("BG");
        bg.transform.SetParent(_panel.transform, false);
        var bgr = bg.AddComponent<RectTransform>();
        bgr.anchorMin = Vector2.zero; bgr.anchorMax = Vector2.one;
        bgr.offsetMin = Vector2.zero; bgr.offsetMax = Vector2.zero;
        bg.AddComponent<Image>().color = new Color(0.02f, 0.015f, 0.04f, 0.98f);

        // 装饰光线
        var glow = new GameObject("Glow");
        glow.transform.SetParent(_panel.transform, false);
        var gr = glow.AddComponent<RectTransform>();
        gr.anchorMin = new Vector2(0.5f, 0.5f); gr.anchorMax = new Vector2(0.5f, 0.5f);
        gr.pivot = new Vector2(0.5f, 0.5f); gr.sizeDelta = new Vector2(800, 500);
        var glowImg = glow.AddComponent<Image>();
        glowImg.sprite = SpriteCache.WhitePixel;
        glowImg.color = new Color(0.15f, 0.08f, 0.25f, 0.15f);
        glowImg.raycastTarget = false;

        // 标题
        var title = new GameObject("Title");
        title.transform.SetParent(_panel.transform, false);
        var tr = title.AddComponent<RectTransform>();
        tr.anchorMin = new Vector2(0.5f, 1); tr.anchorMax = new Vector2(0.5f, 1);
        tr.pivot = new Vector2(0.5f, 1); tr.anchoredPosition = new Vector2(0, -60);
        tr.sizeDelta = new Vector2(600, 50);
        var titleTxt = title.AddComponent<Text>();
        titleTxt.text = "选 择 服 务 器";
        titleTxt.alignment = TextAnchor.MiddleCenter;
        titleTxt.fontSize = 32; titleTxt.color = new Color(0.7f, 0.5f, 0.9f, 1f); titleTxt.font = _font;

        // 副标题
        var sub = new GameObject("Subtitle");
        sub.transform.SetParent(_panel.transform, false);
        var sr = sub.AddComponent<RectTransform>();
        sr.anchorMin = new Vector2(0.5f, 1); sr.anchorMax = new Vector2(0.5f, 1);
        sr.pivot = new Vector2(0.5f, 1); sr.anchoredPosition = new Vector2(0, -105);
        sr.sizeDelta = new Vector2(500, 25);
        var subTxt = sub.AddComponent<Text>();
        subTxt.text = "— 选择你的冒险区域 —";
        subTxt.alignment = TextAnchor.MiddleCenter;
        subTxt.fontSize = 16; subTxt.color = new Color(0.4f, 0.35f, 0.5f, 1f); subTxt.font = _font;

        // 状态提示
        var statusObj = new GameObject("StatusLabel");
        statusObj.transform.SetParent(_panel.transform, false);
        var str = statusObj.AddComponent<RectTransform>();
        str.anchorMin = new Vector2(0.5f, 0); str.anchorMax = new Vector2(0.5f, 0);
        str.pivot = new Vector2(0.5f, 0); str.anchoredPosition = new Vector2(0, 40);
        str.sizeDelta = new Vector2(600, 30);
        _statusLabel = statusObj.AddComponent<Text>();
        _statusLabel.text = ""; _statusLabel.alignment = TextAnchor.MiddleCenter;
        _statusLabel.fontSize = 18; _statusLabel.color = new Color(1f, 0.6f, 0.3f, 1f); _statusLabel.font = _font;

        // 创建卡片
        var servers = ServerRegionManager.Servers;
        string lastId = ServerRegionManager.GetLastServerId();
        for (int i = 0; i < servers.Count; i++)
        {
            int idx = i;
            var srv = servers[i];
            CreateCard(_panel.transform, srv, lastId, idx);
        }

        _panel.SetActive(false);
    }

    private void CreateCard(Transform parent, ServerRegionManager.ServerInfo srv, string lastId, int idx)
    {
        bool sel = srv.id == lastId;
        bool maint = srv.status == 0;
        float w = 480f, h = 220f;

        var card = new GameObject($"Server_{srv.id}");
        card.transform.SetParent(parent, false);
        var cr = card.AddComponent<RectTransform>();
        cr.anchorMin = new Vector2(0.5f, 0.5f); cr.anchorMax = new Vector2(0.5f, 0.5f);
        cr.pivot = new Vector2(0.5f, 0.5f); cr.anchoredPosition = Vector2.zero;
        cr.sizeDelta = new Vector2(w, h);

        // 卡片背景 — 双层渐变
        var img = card.AddComponent<Image>();
        img.color = maint ? new Color(0.08f, 0.06f, 0.06f, 0.95f)
              : sel ? new Color(0.08f, 0.05f, 0.15f, 0.95f)
                    : new Color(0.04f, 0.03f, 0.07f, 0.92f);

        // 外边框 — 金色发光
        var border = new GameObject("Border");
        border.transform.SetParent(card.transform, false);
        var br = border.AddComponent<RectTransform>();
        br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one;
        br.offsetMin = new Vector2(-3, -3); br.offsetMax = new Vector2(3, 3);
        var bImg = border.AddComponent<Image>();
        bImg.color = maint ? new Color(0.4f, 0.3f, 0.3f, 0.4f)
                 : sel ? new Color(0.6f, 0.4f, 0.1f, 0.8f)
                       : new Color(0.25f, 0.18f, 0.35f, 0.6f);
        bImg.raycastTarget = false;
        border.transform.SetAsFirstSibling();

        // 内边框装饰线
        var inner = new GameObject("InnerLine");
        inner.transform.SetParent(card.transform, false);
        var ir = inner.AddComponent<RectTransform>();
        ir.anchorMin = Vector2.zero; ir.anchorMax = Vector2.one;
        ir.offsetMin = new Vector2(6, 6); ir.offsetMax = new Vector2(-6, -6);
        var iImg = inner.AddComponent<Image>();
        iImg.color = new Color(0.15f, 0.1f, 0.25f, 0.3f);
        iImg.raycastTarget = false;

        // 服务器名 — 居中大字
        var nameObj = new GameObject("Name");
        nameObj.transform.SetParent(card.transform, false);
        var nr = nameObj.AddComponent<RectTransform>();
        nr.anchorMin = new Vector2(0.05f, 0.55f); nr.anchorMax = new Vector2(0.95f, 0.92f);
        nr.offsetMin = Vector2.zero; nr.offsetMax = Vector2.zero;
        var nameTxt = nameObj.AddComponent<Text>();
        nameTxt.text = srv.name;
        nameTxt.alignment = TextAnchor.MiddleCenter;
        nameTxt.fontSize = 36; nameTxt.color = Color.white; nameTxt.font = _font;
        nameTxt.raycastTarget = false;

        // 标签
        if (srv.isNew || srv.isRecommended || maint)
        {
            var tag = new GameObject("Tag");
            tag.transform.SetParent(card.transform, false);
            var tgr = tag.AddComponent<RectTransform>();
            tgr.anchorMin = new Vector2(0.5f, 0.35f); tgr.anchorMax = new Vector2(0.5f, 0.35f);
            tgr.pivot = new Vector2(0.5f, 0.5f); tgr.sizeDelta = new Vector2(80, 26);
            tgr.anchoredPosition = new Vector2(0, 0);
            var tImg = tag.AddComponent<Image>();
            string tt = maint ? "维护中" : srv.isNew ? "新服" : "推荐";
            Color tc = maint ? new Color(0.5f, 0.5f, 0.5f, 0.8f)
                    : srv.isNew ? new Color(0.15f, 0.7f, 0.15f, 0.8f)
                                : new Color(0.8f, 0.6f, 0.1f, 0.8f);
            tImg.color = tc;
            var tlbl = new GameObject("Lbl");
            tlbl.transform.SetParent(tag.transform, false);
            var tlr = tlbl.AddComponent<RectTransform>();
            tlr.anchorMin = Vector2.zero; tlr.anchorMax = Vector2.one;
            tlr.offsetMin = Vector2.zero; tlr.offsetMax = Vector2.zero;
            var tTxt = tlbl.AddComponent<Text>();
            tTxt.text = tt; tTxt.alignment = TextAnchor.MiddleCenter;
            tTxt.fontSize = 14; tTxt.color = Color.white; tTxt.font = _font;
            tTxt.raycastTarget = false;
        }

        // 描述
        var descObj = new GameObject("Desc");
        descObj.transform.SetParent(card.transform, false);
        var dr = descObj.AddComponent<RectTransform>();
        dr.anchorMin = new Vector2(0.05f, 0.18f); dr.anchorMax = new Vector2(0.95f, 0.32f);
        dr.offsetMin = Vector2.zero; dr.offsetMax = Vector2.zero;
        var dTxt = descObj.AddComponent<Text>();
        dTxt.text = srv.description;
        dTxt.alignment = TextAnchor.MiddleCenter;
        dTxt.fontSize = 16; dTxt.color = new Color(0.6f, 0.55f, 0.7f, 1f); dTxt.font = _font;
        dTxt.raycastTarget = false;

        // 底部状态栏
        var bot = new GameObject("BottomBar");
        bot.transform.SetParent(card.transform, false);
        var bbr = bot.AddComponent<RectTransform>();
        bbr.anchorMin = new Vector2(0.05f, 0.03f); bbr.anchorMax = new Vector2(0.95f, 0.15f);
        bbr.offsetMin = Vector2.zero; bbr.offsetMax = Vector2.zero;
        var bTxt = bot.AddComponent<Text>();
        string st = ServerRegionManager.GetStatusText(srv.status);
        Color sc = ServerRegionManager.GetStatusColor(srv.status);
        string lp = sel ? "  ★上次登录" : "";
        bTxt.text = $"<color=#{ColorUtility.ToHtmlStringRGBA(sc)}>● {st}</color>{lp}";
        bTxt.alignment = TextAnchor.MiddleCenter;
        bTxt.fontSize = 15; bTxt.color = new Color(0.7f, 0.65f, 0.8f, 1f); bTxt.font = _font;
        bTxt.supportRichText = true; bTxt.raycastTarget = false;

        // 进入按钮 — 点击卡片直接进入
        var btn = card.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(() => OnServerSelected(idx));
    }

    private void OnServerSelected(int index)
    {
        var srv = ServerRegionManager.Servers[index];
        if (srv.status == 0)
        {
            if (_statusLabel != null) { _statusLabel.text = $"{srv.name} 正在维护中..."; _statusLabel.color = new Color(1f, 0.5f, 0.3f, 1f); }
            return;
        }
        ServerRegionManager.SelectServer(srv.id);
        Hide();
        GameManager.Instance.ShowCharacterSlots();
    }

    private void OnDestroy()
    {
        if (_panel != null) Destroy(_panel);
        if (Instance == this) Instance = null;
    }
}
