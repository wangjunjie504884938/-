using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 设置面板 — 音量、振动等
/// </summary>
public class SettingsUI : MonoBehaviour
{
    public static SettingsUI Instance { get; private set; }

    private GameObject panel;
    private Slider sfxSlider;
    private Slider bgmSlider;
    private Toggle vibrateToggle;
    private bool openedFromNav; // true if opened from dungeon nav gear button

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Show()
    {
        if (panel == null) BuildUI();
        panel.SetActive(true);
    }

    public void ShowFromNav()
    {
        openedFromNav = true;
        if (!GameManager.Instance.IsPaused) GameManager.Instance.TogglePause();
        Show();
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
        if (openedFromNav)
        {
            openedFromNav = false;
            if (GameManager.Instance.IsPaused) GameManager.Instance.TogglePause();
        }
    }

    private void BuildUI()
    {
        Font font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();

        panel = new GameObject("SettingsPanel");
        panel.transform.SetParent(canvas.transform, false);
        RectTransform pr = panel.AddComponent<RectTransform>();
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one;
        pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;

        // 半透明背景
        var bgImg = panel.AddComponent<Image>();
        bgImg.color = new Color(0, 0, 0, 0.6f);

        // Ensure settings panel renders above pause panel
        var panelCanvas = panel.AddComponent<Canvas>();
        panelCanvas.overrideSorting = true;
        panelCanvas.sortingOrder = 10;
        panel.AddComponent<GraphicRaycaster>();

        // 内容卡片
        float cardW = 420f, cardH = 420f;
        var card = UIHelper.MakeCard(panel.transform, "SettingsCard",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-cardW / 2f, -cardH / 2f), new Vector2(cardW / 2f, cardH / 2f));
        card.GetComponent<Image>().color = UIHelper.BgPanel;

        // 标题 (覆盖层自带居中标题，不使用全局 TopNavBar)
        UIHelper.MakeTitle(card.transform, "设置", font, 28);

        // ✕ 关闭按钮 (右上角)
        var closeBtn = new GameObject("CloseBtn");
        closeBtn.transform.SetParent(card.transform, false);
        var clR = closeBtn.AddComponent<RectTransform>();
        clR.anchorMin = new Vector2(1, 1); clR.anchorMax = new Vector2(1, 1);
        clR.pivot = new Vector2(1, 1); clR.anchoredPosition = new Vector2(-8, -8);
        clR.sizeDelta = new Vector2(36, 36);
        var clBg = closeBtn.AddComponent<Image>();
        clBg.color = UIHelper.BackBtnBg;
        var clButton = closeBtn.AddComponent<Button>();
        clButton.onClick.AddListener(() => Hide());
        var clLbl = new GameObject("Lbl");
        clLbl.transform.SetParent(closeBtn.transform, false);
        var clLblR = clLbl.AddComponent<RectTransform>();
        clLblR.anchorMin = Vector2.zero; clLblR.anchorMax = Vector2.one;
        clLblR.offsetMin = Vector2.zero; clLblR.offsetMax = Vector2.zero;
        var clTxt = clLbl.AddComponent<Text>();
        clTxt.text = "✕"; clTxt.alignment = TextAnchor.MiddleCenter;
        clTxt.fontSize = 20; clTxt.color = UIHelper.TextPrimary; clTxt.font = font;

        // 滚动区域 — 标题下方到底部
        var scrollObj = new GameObject("ScrollArea");
        scrollObj.transform.SetParent(card.transform, false);
        var srRect = scrollObj.AddComponent<RectTransform>();
        srRect.anchorMin = new Vector2(0, 0); srRect.anchorMax = new Vector2(1, 1);
        srRect.offsetMin = new Vector2(8, 8); srRect.offsetMax = new Vector2(-8, -90);
        var scrollRect = scrollObj.AddComponent<ScrollRect>();
        scrollRect.vertical = true; scrollRect.horizontal = false;
        scrollRect.scrollSensitivity = 20f;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.elasticity = 0.1f;

        // Viewport — RectMask2D 裁剪
        var vpObj = new GameObject("Viewport", typeof(RectTransform));
        vpObj.transform.SetParent(scrollObj.transform, false);
        var vpR = vpObj.GetComponent<RectTransform>();
        vpR.anchorMin = Vector2.zero; vpR.anchorMax = Vector2.one;
        vpR.offsetMin = Vector2.zero; vpR.offsetMax = Vector2.zero;
        vpObj.AddComponent<RectMask2D>();
        scrollRect.viewport = vpR;

        // Content — VerticalLayoutGroup + ContentSizeFitter
        var contentObj = new GameObject("Content");
        contentObj.transform.SetParent(vpObj.transform, false);
        var cr = contentObj.AddComponent<RectTransform>();
        cr.anchorMin = new Vector2(0, 1); cr.anchorMax = new Vector2(1, 1);
        cr.pivot = new Vector2(0.5f, 1);
        cr.offsetMin = Vector2.zero; cr.offsetMax = Vector2.zero;
        var vlg = contentObj.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8; vlg.padding = new RectOffset(8, 8, 8, 8);
        vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
        vlg.childControlHeight = true; vlg.childForceExpandHeight = false;
        var csf = contentObj.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.content = cr;

        Transform contentParent = contentObj.transform;

        // 音效音量
        MakeSliderRow(contentParent, "SFX", "音效", font, out sfxSlider);

        // 背景音乐音量
        MakeSliderRow(contentParent, "BGM", "音乐", font, out bgmSlider);

        // 振动开关
        MakeToggleRow(contentParent, "Vibrate", "振动", font, true, v => { /* 振动 */ });
        vibrateToggle = contentParent.Find("VibrateRow/Toggle")?.GetComponent<Toggle>();

        // 伤害数字开关
        MakeToggleRow(contentParent, "DmgNum", "伤害数字", font, GameSettings.DamageNumbers, v => GameSettings.DamageNumbers = v);

        // 屏幕震动开关
        MakeToggleRow(contentParent, "Shake", "屏幕震动", font, GameSettings.ScreenShake, v => GameSettings.ScreenShake = v);

        // 自动拾取开关
        MakeToggleRow(contentParent, "AutoLoot", "自动拾取", font, GameSettings.AutoLoot, v => GameSettings.AutoLoot = v);

        // FPS显示开关
        MakeToggleRow(contentParent, "FPS", "显示帧率", font, GameSettings.FPSDisplay, v => GameSettings.FPSDisplay = v);

        panel.SetActive(false);
    }

    private void MakeSliderRow(Transform parent, string id, string label, Font font, out Slider slider)
    {
        var row = new GameObject(id + "Row");
        row.transform.SetParent(parent, false);
        var rr = row.AddComponent<RectTransform>();
        rr.anchorMin = new Vector2(0, 1); rr.anchorMax = new Vector2(1, 1);
        rr.pivot = new Vector2(0.5f, 1);
        rr.offsetMin = Vector2.zero; rr.offsetMax = Vector2.zero;
        var le = row.AddComponent<LayoutElement>();
        le.preferredHeight = 50;

        // 标签
        var lbl = new GameObject("Label");
        lbl.transform.SetParent(row.transform, false);
        var lr = lbl.AddComponent<RectTransform>();
        lr.anchorMin = new Vector2(0.1f, 0); lr.anchorMax = new Vector2(0.35f, 1);
        lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;
        var lt = lbl.AddComponent<Text>();
        lt.text = label; lt.alignment = TextAnchor.MiddleLeft;
        lt.fontSize = 20; lt.color = UIHelper.TextPrimary; lt.font = font;

        // 滑动条背景
        var sliderBg = new GameObject("SliderBg");
        sliderBg.transform.SetParent(row.transform, false);
        var sbr = sliderBg.AddComponent<RectTransform>();
        sbr.anchorMin = new Vector2(0.38f, 0.15f); sbr.anchorMax = new Vector2(0.88f, 0.85f);
        sbr.offsetMin = Vector2.zero; sbr.offsetMax = Vector2.zero;
        var sbImg = sliderBg.AddComponent<Image>();
        sbImg.color = new Color(0.15f, 0.15f, 0.22f, 0.9f);

        // 滑动条填充
        var fillArea = new GameObject("FillArea");
        fillArea.transform.SetParent(sliderBg.transform, false);
        var far = fillArea.AddComponent<RectTransform>();
        far.anchorMin = Vector2.zero; far.anchorMax = Vector2.one;
        far.offsetMin = Vector2.zero; far.offsetMax = Vector2.zero;

        var fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(fillArea.transform, false);
        var fr = fillObj.AddComponent<RectTransform>();
        fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one;
        fr.offsetMin = Vector2.zero; fr.offsetMax = Vector2.zero;
        var fImg = fillObj.AddComponent<Image>();
        fImg.color = UIHelper.Accent;

        // 滑动条
        slider = sliderBg.AddComponent<Slider>();
        slider.targetGraphic = sbImg;
        slider.fillRect = fr;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0.8f;
        slider.interactable = true;

        // Wire slider to AudioManager
        if (id == "SFX")
        {
            slider.value = AudioManager.Instance != null ? AudioManager.Instance.SfxVolume : 0.7f;
            slider.onValueChanged.AddListener(v => AudioManager.Instance?.SetSFXVolume(v));
        }
        else if (id == "BGM")
        {
            slider.value = AudioManager.Instance != null ? AudioManager.Instance.BgmVolume : 0.4f;
            slider.onValueChanged.AddListener(v => AudioManager.Instance?.SetBGMVolume(v));
        }
    }

    private void MakeToggleRow(Transform parent, string id, string label, Font font, bool initialValue, System.Action<bool> onChange)
    {
        var row = new GameObject(id + "Row");
        row.transform.SetParent(parent, false);
        var rr = row.AddComponent<RectTransform>();
        rr.anchorMin = new Vector2(0, 1); rr.anchorMax = new Vector2(1, 1);
        rr.pivot = new Vector2(0.5f, 1);
        rr.offsetMin = Vector2.zero; rr.offsetMax = Vector2.zero;
        var le = row.AddComponent<LayoutElement>();
        le.preferredHeight = 50;

        var lbl = new GameObject("Label");
        lbl.transform.SetParent(row.transform, false);
        var lr = lbl.AddComponent<RectTransform>();
        lr.anchorMin = new Vector2(0.1f, 0); lr.anchorMax = new Vector2(0.6f, 1);
        lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;
        var lt = lbl.AddComponent<Text>();
        lt.text = label; lt.alignment = TextAnchor.MiddleLeft;
        lt.fontSize = 20; lt.color = UIHelper.TextPrimary; lt.font = font;

        var toggleObj = new GameObject("Toggle");
        toggleObj.transform.SetParent(row.transform, false);
        var tgr = toggleObj.AddComponent<RectTransform>();
        tgr.anchorMin = new Vector2(0.7f, 0.5f); tgr.anchorMax = new Vector2(0.7f, 0.5f);
        tgr.pivot = new Vector2(0, 0.5f);
        tgr.sizeDelta = new Vector2(80, 36);
        var toggleBg = toggleObj.AddComponent<Image>();
        toggleBg.color = new Color(0.12f, 0.08f, 0.18f, 0.95f);
        // Toggle 边框
        var tBorder = new GameObject("Border");
        tBorder.transform.SetParent(toggleObj.transform, false);
        var tbr = tBorder.AddComponent<RectTransform>();
        tbr.anchorMin = Vector2.zero; tbr.anchorMax = Vector2.one;
        tbr.offsetMin = new Vector2(-1.5f, -1.5f); tbr.offsetMax = new Vector2(1.5f, 1.5f);
        var tbi = tBorder.AddComponent<Image>();
        tbi.color = new Color(0.40f, 0.28f, 0.55f, 0.8f);
        tbi.raycastTarget = false;
        tBorder.transform.SetAsFirstSibling();
        var toggle = toggleObj.AddComponent<Toggle>();
        toggle.targetGraphic = toggleBg;
        toggle.isOn = initialValue;

        var checkObj = new GameObject("Checkmark");
        checkObj.transform.SetParent(toggleObj.transform, false);
        var cr = checkObj.AddComponent<RectTransform>();
        cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one;
        cr.offsetMin = new Vector2(4, 4); cr.offsetMax = new Vector2(-4, -4);
        var checkImg = checkObj.AddComponent<Image>();
        checkImg.color = UIHelper.Accent;
        toggle.graphic = checkImg;

        toggle.onValueChanged.AddListener(v => { onChange(v); GameSettings.Save(); });
    }

    private void OnDestroy()
    {
        if (panel != null) Destroy(panel);
        if (Instance == this) Instance = null;
    }
}
