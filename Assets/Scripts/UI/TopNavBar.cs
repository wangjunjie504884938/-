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
    private Text _currencyText;
    private GameObject _backBtn;
    private System.Action _backAction;

    private const float BarHeight = 56f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildBar();
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

        // 金币+碎片 (右侧)
        var currencyObj = new GameObject("Currency");
        currencyObj.transform.SetParent(_bar.transform, false);
        var curR = currencyObj.AddComponent<RectTransform>();
        curR.anchorMin = new Vector2(0.5f, 0); curR.anchorMax = new Vector2(1, 1);
        curR.offsetMin = new Vector2(0, 0); curR.offsetMax = new Vector2(-12, 0);
        _currencyText = currencyObj.AddComponent<Text>();
        _currencyText.alignment = TextAnchor.MiddleRight;
        _currencyText.fontSize = 18; _currencyText.color = UIHelper.Accent; _currencyText.font = font;
        _currencyText.raycastTarget = false;
        _currencyText.supportRichText = true;

        _bar.SetActive(false);
    }

    /// <summary>设置导航栏: 标题 + 返回回调</summary>
    public void Setup(string title, System.Action onBack = null)
    {
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

    /// <summary>刷新金币+碎片显示</summary>
    public void RefreshCurrency()
    {
        if (_currencyText == null) return;
        int gold = GameManager.Instance?.Player?.Stats?.Gold ?? 0;
        int frags = DismantleSystem.Fragments;
        _currencyText.text = $"<color=#FFCC44>金币</color> {UIStringBuilderPool.FormatNumber(gold)}    <color=#88CCFF>碎片</color> {UIStringBuilderPool.FormatNumber(frags)}";
    }

    /// <summary>隐藏导航栏(标题画面/选角等)</summary>
    public void HideBar()
    {
        if (_bar != null) _bar.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
