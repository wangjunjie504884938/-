using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 全局顶部导航栏 — 独立组件，挂在Canvas上持久存在。
/// 左侧: 返回按钮 + 设置按钮 | 中间: 动态页面标题 | 右侧: 金币+碎片
/// 各面板Show()时调用 Setup(title, onBack) 即可，无需自行创建标题和导航按钮。
/// </summary>
public class TopNavBar : MonoBehaviour
{
    public static TopNavBar Instance { get; private set; }

    private GameObject _bar;
    private Text _titleText;
    private readonly Text[] _navCurrencyTexts = new Text[3];
    private GameObject _backBtn;
    private System.Action _backAction;

    private const float BarHeight = 56f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        CurrencyWallet.OnChanged += RefreshCurrency;   // 寿元/位面余额变动自动刷新
    }

    private void BuildBar()
    {
        Font font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();

        _bar = new GameObject("TopNavBar");
        _bar.transform.SetParent(canvas.transform, false);
        var barR = _bar.AddComponent<RectTransform>();
        barR.anchorMin = new Vector2(0, 1); barR.anchorMax = new Vector2(1, 1);
        barR.pivot = new Vector2(0.5f, 1);
        barR.sizeDelta = new Vector2(0, BarHeight);
        barR.anchoredPosition = Vector2.zero;

        // 背景
        var bgImg = _bar.AddComponent<Image>();
        bgImg.color = UIHelper.BgPanel;
        bgImg.raycastTarget = true;

        // 底部细线
        var line = new GameObject("BottomLine");
        line.transform.SetParent(_bar.transform, false);
        var lineR = line.AddComponent<RectTransform>();
        lineR.anchorMin = new Vector2(0, 0); lineR.anchorMax = new Vector2(1, 0);
        lineR.pivot = new Vector2(0.5f, 0);
        lineR.sizeDelta = new Vector2(0, 1);
        var lineImg = line.AddComponent<Image>();
        lineImg.color = UIHelper.BorderSubtle;
        lineImg.raycastTarget = false;

        Color navBorder = new Color(0.25f, 0.12f, 0.40f, 0.6f);
        Color navFill = new Color(0.06f, 0.04f, 0.09f, 0.85f);
        float btnSize = 40f;
        float gap = 6f;
        float startX = 12f;
        float posY = -(BarHeight - btnSize) / 2f;

        // 返回按钮
        _backBtn = new GameObject("BackBtn");
        _backBtn.transform.SetParent(_bar.transform, false);
        var backR = _backBtn.AddComponent<RectTransform>();
        backR.anchorMin = new Vector2(0, 0.5f); backR.anchorMax = new Vector2(0, 0.5f);
        backR.pivot = new Vector2(0, 0.5f);
        backR.anchoredPosition = new Vector2(startX, posY);
        backR.sizeDelta = new Vector2(btnSize, btnSize);
        var backBg = _backBtn.AddComponent<Image>();
        backBg.color = navFill;
        var backBorder = new GameObject("Border");
        backBorder.transform.SetParent(_backBtn.transform, false);
        var bbR = backBorder.AddComponent<RectTransform>();
        bbR.anchorMin = Vector2.zero; bbR.anchorMax = Vector2.one;
        bbR.offsetMin = new Vector2(-1, -1); bbR.offsetMax = new Vector2(1, 1);
        var bbImg = backBorder.AddComponent<Image>();
        bbImg.color = navBorder; bbImg.raycastTarget = false;
        backBorder.transform.SetAsFirstSibling();
        var backBtnComp = _backBtn.AddComponent<Button>();
        backBtnComp.transition = Selectable.Transition.None;
        UIHelper.SetupButtonFeedback(backBtnComp, navFill);
        backBtnComp.onClick.AddListener(() => _backAction?.Invoke());
        var backLbl = new GameObject("Lbl");
        backLbl.transform.SetParent(_backBtn.transform, false);
        var blR = backLbl.AddComponent<RectTransform>();
        blR.anchorMin = Vector2.zero; blR.anchorMax = Vector2.one;
        blR.offsetMin = Vector2.zero; blR.offsetMax = Vector2.zero;
        var backTxt = backLbl.AddComponent<Text>();
        backTxt.text = "←"; backTxt.alignment = TextAnchor.MiddleCenter;
        backTxt.fontSize = 22; backTxt.color = UIHelper.TextPrimary; backTxt.font = font;
        backTxt.raycastTarget = false;
        _backBtn.SetActive(false);

        // 设置按钮
        float gearX = startX + btnSize + gap;
        var gearObj = new GameObject("GearBtn");
        gearObj.transform.SetParent(_bar.transform, false);
        var gearR = gearObj.AddComponent<RectTransform>();
        gearR.anchorMin = new Vector2(0, 0.5f); gearR.anchorMax = new Vector2(0, 0.5f);
        gearR.pivot = new Vector2(0, 0.5f);
        gearR.anchoredPosition = new Vector2(gearX, posY);
        gearR.sizeDelta = new Vector2(btnSize, btnSize);
        var gearBg = gearObj.AddComponent<Image>();
        gearBg.color = navFill;
        var gearBorder = new GameObject("Border");
        gearBorder.transform.SetParent(gearObj.transform, false);
        var gbR = gearBorder.AddComponent<RectTransform>();
        gbR.anchorMin = Vector2.zero; gbR.anchorMax = Vector2.one;
        gbR.offsetMin = new Vector2(-1, -1); gbR.offsetMax = new Vector2(1, 1);
        var gbImg = gearBorder.AddComponent<Image>();
        gbImg.color = navBorder; gbImg.raycastTarget = false;
        gearBorder.transform.SetAsFirstSibling();
        var gearBtnComp = gearObj.AddComponent<Button>();
        gearBtnComp.transition = Selectable.Transition.None;
        UIHelper.SetupButtonFeedback(gearBtnComp, navFill);
        gearBtnComp.onClick.AddListener(() => { UIHelper.EnsureUIManager(); UIManager.Instance.ShowSettings(); });
        var gearIcon = new GameObject("GearIcon");
        gearIcon.transform.SetParent(gearObj.transform, false);
        var giR = gearIcon.AddComponent<RectTransform>();
        giR.anchorMin = Vector2.zero; giR.anchorMax = Vector2.one;
        giR.offsetMin = new Vector2(7, 7); giR.offsetMax = new Vector2(-7, -7);
        var giImg = gearIcon.AddComponent<Image>();
        giImg.sprite = UIHelper.CreateGearSprite();
        giImg.color = UIHelper.TextPrimary;

        // 标题 (居中)
        var titleObj = new GameObject("Title");
        titleObj.transform.SetParent(_bar.transform, false);
        var titleR = titleObj.AddComponent<RectTransform>();
        titleR.anchorMin = new Vector2(0.25f, 0); titleR.anchorMax = new Vector2(0.75f, 1);
        titleR.offsetMin = Vector2.zero; titleR.offsetMax = Vector2.zero;
        _titleText = titleObj.AddComponent<Text>();
        _titleText.alignment = TextAnchor.MiddleCenter;
        _titleText.fontSize = 24; _titleText.color = UIHelper.Accent; _titleText.font = font;
        _titleText.raycastTarget = false;

        // 寿元币/位面币/游戏币 图标+数量槽 (右侧) + 充值加号 — 样式对齐HUD货币槽 (用户要求加到导航上)
        var row = new GameObject("CurrencyRow");
        row.transform.SetParent(_bar.transform, false);
        var rowR = row.AddComponent<RectTransform>();
        rowR.anchorMin = new Vector2(1, 0.5f); rowR.anchorMax = new Vector2(1, 0.5f);
        rowR.pivot = new Vector2(1f, 0.5f);
        rowR.anchoredPosition = new Vector2(-48, 0);
        rowR.sizeDelta = new Vector2(540, 44);

        string[][] curDefs =
        {
            new[] { "游戏币", "HudCoinGold" },
            new[] { "寿元币", "HudCoinLife" },
            new[] { "位面币", "HudCoinPlane" },
        };
        float[] groupCx = { -180f, 0f, 180f };
        for (int i = 0; i < 3; i++)
        {
            var g = new GameObject("NavCur_" + curDefs[i][0]);
            g.transform.SetParent(row.transform, false);
            var gR = g.AddComponent<RectTransform>();
            gR.anchorMin = new Vector2(0.5f, 0.5f); gR.anchorMax = new Vector2(0.5f, 0.5f);
            gR.anchoredPosition = new Vector2(groupCx[i], 0);
            gR.sizeDelta = new Vector2(170, 44);

            var icon = new GameObject("Icon");
            icon.transform.SetParent(g.transform, false);
            var iR = icon.AddComponent<RectTransform>();
            iR.anchorMin = new Vector2(0f, 0.5f); iR.anchorMax = new Vector2(0f, 0.5f);
            iR.anchoredPosition = new Vector2(20, 0);
            iR.sizeDelta = new Vector2(38, 38);
            var iImg = icon.AddComponent<Image>();
            iImg.sprite = HudSprite(curDefs[i][1]);
            iImg.raycastTarget = false;

            var boxObj = new GameObject("AmountBox");
            boxObj.transform.SetParent(g.transform, false);
            var bR = boxObj.AddComponent<RectTransform>();
            bR.anchorMin = new Vector2(0f, 0.5f); bR.anchorMax = new Vector2(0f, 0.5f);
            bR.anchoredPosition = new Vector2(115, 0);
            bR.sizeDelta = new Vector2(110, 34);
            var bImg = boxObj.AddComponent<Image>();
            bImg.sprite = HudSprite("HudAmountBox");
            bImg.raycastTarget = false;

            var tObj = new GameObject("AmountText");
            tObj.transform.SetParent(boxObj.transform, false);
            var tR = tObj.AddComponent<RectTransform>();
            tR.anchorMin = Vector2.zero; tR.anchorMax = Vector2.one;
            tR.offsetMin = new Vector2(4, 0); tR.offsetMax = new Vector2(-4, 0);
            var t = tObj.AddComponent<Text>();
            t.alignment = TextAnchor.MiddleCenter;
            t.fontSize = 19; t.color = Color.white; t.font = font;
            t.raycastTarget = false;
            _navCurrencyTexts[i] = t;
        }

        // 充值加号 (货币行尾 → 充值中心)
        var plusObj = new GameObject("RechargePlus");
        plusObj.transform.SetParent(_bar.transform, false);
        var plusR = plusObj.AddComponent<RectTransform>();
        plusR.anchorMin = new Vector2(1, 0.5f); plusR.anchorMax = new Vector2(1, 0.5f);
        plusR.anchoredPosition = new Vector2(-22, 0);
        plusR.sizeDelta = new Vector2(36, 40);
        var plusImg = plusObj.AddComponent<Image>();
        plusImg.sprite = HudSprite("HudPlus");
        var plusBtn = plusObj.AddComponent<Button>();
        plusBtn.transition = Selectable.Transition.None;
        UIHelper.SetupButtonFeedback(plusBtn, Color.white);
        plusBtn.onClick.AddListener(() => UIManager.Instance.ShowRecharge());

        _bar.SetActive(false);
    }

    private static Sprite HudSprite(string name) => Resources.Load<Sprite>($"Sprites/Hud/{name}");

    /// <summary>设置导航栏: 标题 + 返回回调</summary>
    public void Setup(string title, System.Action onBack = null)
    {
        if (_bar == null) BuildBar();   // 惰性构建 — 四货币重构后BuildBar调用点丢失导致导航栏整体消失
        if (_bar == null) return;
        _bar.SetActive(true);
        _bar.transform.SetAsLastSibling();

        if (_titleText != null)
            _titleText.text = title;

        _backAction = onBack;
        if (_backBtn != null)
            _backBtn.SetActive(onBack != null);

        // 通知中心红点
        if (NotificationCenter.Instance != null)
            NotificationCenter.Instance.SetupBadge(_bar.transform);

        RefreshCurrency();
    }

    /// <summary>仅设置标题(不改变返回按钮)</summary>
    public void SetTitle(string title)
    {
        if (_titleText != null)
            _titleText.text = title;
    }

    /// <summary>刷新导航货币槽: 游戏币/寿元币/位面币</summary>
    public void RefreshCurrency()
    {
        int gold = GameManager.Instance?.Player?.Stats?.Gold ?? 0;
        if (_navCurrencyTexts[0] != null) _navCurrencyTexts[0].text = UIStringBuilderPool.FormatNumber(gold);
        if (_navCurrencyTexts[1] != null) _navCurrencyTexts[1].text = CurrencyWallet.LifeCoins.ToString();
        if (_navCurrencyTexts[2] != null) _navCurrencyTexts[2].text = CurrencyWallet.PlaneCoins.ToString();
    }

    /// <summary>隐藏导航栏(标题画面/选角等)</summary>
    public void HideBar()
    {
        if (_bar != null) _bar.SetActive(false);
    }

    private void OnDestroy()
    {
        CurrencyWallet.OnChanged -= RefreshCurrency;
        if (Instance == this) Instance = null;
    }
}
