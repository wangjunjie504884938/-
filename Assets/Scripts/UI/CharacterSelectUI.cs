using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 职业选择页 (创建新角色第一步) — 设计图素材版:
/// 全屏背景(支持滑动手势切换职业) + 中央立绘(头顶职业名) + 底部Hint(点击查看属性 ClassStatsPopup) + 确认
/// 确认后进入性别+起名页 (GenderSelectUI, 第二步)
/// </summary>
public class CharacterSelectUI : MonoBehaviour
{
    public static CharacterSelectUI Instance { get; private set; }

    private static readonly HeroClass[] ClassOrder = { HeroClass.Warrior, HeroClass.Mage, HeroClass.Priest };
    private static readonly string[] ClassNames = { "勇士", "方士", "祝由" };
    private const float SlideDist = 260f;   // 滑动动画位移(像素)
    private const float SlideDur = 0.12f;   // 滑动动画时长(秒)

    private GameObject panel;
    private int _classIdx;
    private Image _previewImg;
    private Text _classNameText;
    private RectTransform _content;
    private bool _sliding;

    private int PrevIdx => (_classIdx + ClassOrder.Length - 1) % ClassOrder.Length;
    private int NextIdx => (_classIdx + 1) % ClassOrder.Length;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Show()
    {
        if (panel == null) BuildUI();
        // 选择角色页面不显示金币碎片
        if (TopNavBar.Instance != null) TopNavBar.Instance.HideBar();
        panel.SetActive(true);
        StartCoroutine(UIHelper.FadeIn(panel, 0.2f));
        RefreshClassVisual();
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }

    private void ToggleClass(int idx)
    {
        _classIdx = Mathf.Clamp(idx, 0, ClassOrder.Length - 1);
        RefreshClassVisual();
    }

    /// <summary>
    /// 滑动切换到指定职业: 内容先跳到侧位(fromRight=内容从右侧进入)并换职业, 再滑回原位
    /// 单段 jump-swap-tween, _sliding 防抖
    /// </summary>
    private void SlideTo(int idx, bool fromRight)
    {
        if (_sliding || idx == _classIdx) return;
        // 属性面板打开时不滑动 (避免误切换面板背后的职业)
        var popup = panel != null ? panel.transform.Find("ClassStatsPopup") : null;
        if (popup != null && popup.gameObject.activeSelf) return;
        if (_content == null) { ToggleClass(idx); return; }
        StartCoroutine(SlideCo(idx, fromRight));
    }

    private IEnumerator SlideCo(int idx, bool fromRight)
    {
        _sliding = true;
        var startPos = new Vector2(fromRight ? SlideDist : -SlideDist, 0f);
        _content.anchoredPosition = startPos;
        ToggleClass(idx);
        float t = 0f;
        while (t < SlideDur)
        {
            t += Time.unscaledDeltaTime;
            if (_content != null)
                _content.anchoredPosition = Vector2.Lerp(startPos, Vector2.zero, Mathf.Clamp01(t / SlideDur));
            yield return null;
        }
        if (_content != null) _content.anchoredPosition = Vector2.zero;
        _sliding = false;
    }

    private void RefreshClassVisual()
    {
        if (_classNameText != null) _classNameText.text = ClassNames[_classIdx];
        if (_previewImg != null)
        {
            _previewImg.sprite = null;
            StartCoroutine(LoadPreviewSprite(ClassOrder[_classIdx], _previewImg));
        }
    }

    private void OnConfirmBtn()
    {
        Hide();
        GameManager.Instance.ShowGenderSelect(ClassOrder[_classIdx]);
    }

    private void OnBackClicked()
    {
        Hide();
        // 选角组件可能已被ShowHub清理 — 缺失时重建(与boot同一挂载点)
        var slot = CharacterSlotUI.Instance;
        if (slot == null) slot = GameManager.Instance.gameObject.AddComponent<CharacterSlotUI>();
        slot.Show();
    }

    private void OnHintClicked()
    {
        if (panel == null) return;
        ClassStatsPopup.Show(panel.transform, ClassOrder[_classIdx]);
    }

    private IEnumerator LoadPreviewSprite(HeroClass heroClass, Image targetImg)
    {
        if (targetImg == null) yield break;
        // 设计图职业立绘 (性别未选默认男版; 方士无职业立绘用通用图), 缺失回退职业占位sprite
        var designed = Resources.Load<Sprite>(PortraitPath(heroClass));
        if (designed != null)
        {
            targetImg.sprite = designed;
            yield break;
        }
        yield return CharacterSpriteFactory.LoadClassSpriteAsync(heroClass, sprite =>
        {
            if (targetImg != null && sprite != null)
                targetImg.sprite = sprite;
        });
    }

    /// <summary>设计图职业立绘路径 (与性别页 PortraitPath 同一映射)</summary>
    private static string PortraitPath(HeroClass heroClass)
    {
        return heroClass switch
        {
            HeroClass.Warrior => ResourcePaths.PortraitWarriorM,
            HeroClass.Priest => ResourcePaths.PortraitPriestM,
            _ => ResourcePaths.PortraitGenericM,
        };
    }

    private void BuildUI()
    {
        Font font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();

        panel = new GameObject("CharacterSelectPanel");
        panel.transform.SetParent(canvas.transform, false);
        var pr = panel.AddComponent<RectTransform>();
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one;
        pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;
        panel.AddComponent<CanvasGroup>();

        // 滑动手势挂页面根节点 (任意子元素起拖都会沿层级向上找到它)
        var swipe = panel.AddComponent<SwipeHandler>();
        swipe.OnSwipeLeft = () => SlideTo(NextIdx, true);    // 手指左滑 → 下一个(内容从右侧滑入)
        swipe.OnSwipeRight = () => SlideTo(PrevIdx, false);  // 手指右滑 → 上一个(内容从左侧滑入)

        // 全屏设计图背景 (可接收Raycast, 供空白区域起滑)
        var bgObj = new GameObject("BG");
        bgObj.transform.SetParent(panel.transform, false);
        var bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero; bgRect.offsetMax = Vector2.zero;
        var bgImg = bgObj.AddComponent<Image>();
        bgImg.sprite = Resources.Load<Sprite>(ResourcePaths.CreateBg);
        bgImg.raycastTarget = true;

        // 可滑动内容容器 (立绘/提示/确认)
        var contentObj = new GameObject("Content");
        contentObj.transform.SetParent(panel.transform, false);
        _content = contentObj.AddComponent<RectTransform>();
        _content.anchorMin = Vector2.zero; _content.anchorMax = Vector2.one;
        _content.offsetMin = Vector2.zero; _content.offsetMax = Vector2.zero;

        // 中央职业立绘 (点击查看属性)
        var previewObj = new GameObject("Preview");
        previewObj.transform.SetParent(contentObj.transform, false);
        var pvRect = previewObj.AddComponent<RectTransform>();
        pvRect.anchorMin = new Vector2(0.5f, 0.5f); pvRect.anchorMax = new Vector2(0.5f, 0.5f);
        pvRect.pivot = new Vector2(0.5f, 0.5f);
        pvRect.anchoredPosition = new Vector2(0, -40);
        pvRect.sizeDelta = new Vector2(420, 740);
        _previewImg = previewObj.AddComponent<Image>();
        _previewImg.preserveAspect = true;

        // 人物头顶职业名 (随滑动内容一起切换)
        var classNameObj = new GameObject("ClassName");
        classNameObj.transform.SetParent(contentObj.transform, false);
        var cnRect = classNameObj.AddComponent<RectTransform>();
        cnRect.anchorMin = new Vector2(0.5f, 0.5f); cnRect.anchorMax = new Vector2(0.5f, 0.5f);
        cnRect.pivot = new Vector2(0.5f, 0.5f);
        cnRect.anchoredPosition = new Vector2(0, 370);
        cnRect.sizeDelta = new Vector2(400, 60);
        _classNameText = classNameObj.AddComponent<Text>();
        _classNameText.alignment = TextAnchor.MiddleCenter;
        _classNameText.fontSize = 44; _classNameText.color = new Color(0.95f, 0.88f, 0.6f); _classNameText.font = font;
        _classNameText.raycastTarget = false;

        // 底部提示
        var hintObj = new GameObject("Hint");
        hintObj.transform.SetParent(contentObj.transform, false);
        var hintRect = hintObj.AddComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0.5f, 0.5f); hintRect.anchorMax = new Vector2(0.5f, 0.5f);
        hintRect.pivot = new Vector2(0.5f, 0.5f);
        hintRect.anchoredPosition = new Vector2(-140, -462);
        hintRect.sizeDelta = new Vector2(340, 30);
        var hintText = hintObj.AddComponent<Text>();
        hintText.text = "点击查看属性"; hintText.alignment = TextAnchor.MiddleCenter;
        hintText.fontSize = 18; hintText.color = new Color(0.78f, 0.78f, 0.88f, 0.85f); hintText.font = font;
        hintText.raycastTarget = true;
        // 属性弹窗触发点 (点Hint文字, 立绘区域保持纯滑动)
        var hintBtn = hintObj.AddComponent<Button>();
        hintBtn.targetGraphic = hintText;
        hintBtn.transition = Selectable.Transition.None;
        UIHelper.SetupButtonFeedback(hintBtn, hintText.color);
        hintBtn.onClick.AddListener(OnHintClicked);

        // 确认按钮 (设计图素材)
        var confirmObj = new GameObject("ConfirmBtn");
        confirmObj.transform.SetParent(contentObj.transform, false);
        var cfRect = confirmObj.AddComponent<RectTransform>();
        cfRect.anchorMin = new Vector2(0.5f, 0.5f); cfRect.anchorMax = new Vector2(0.5f, 0.5f);
        cfRect.pivot = new Vector2(0.5f, 0.5f);
        cfRect.anchoredPosition = new Vector2(160, -460);
        cfRect.sizeDelta = new Vector2(150, 62);
        var cfImg = confirmObj.AddComponent<Image>();
        cfImg.sprite = Resources.Load<Sprite>(ResourcePaths.ConfirmBtn);
        var cfBtn = confirmObj.AddComponent<Button>();
        cfBtn.targetGraphic = cfImg;
        cfBtn.onClick.AddListener(OnConfirmBtn);
        var cfLblObj = new GameObject("Label");
        cfLblObj.transform.SetParent(confirmObj.transform, false);
        var cfLblRect = cfLblObj.AddComponent<RectTransform>();
        cfLblRect.anchorMin = Vector2.zero; cfLblRect.anchorMax = Vector2.one;
        cfLblRect.offsetMin = Vector2.zero; cfLblRect.offsetMax = Vector2.zero;
        var cfLbl = cfLblObj.AddComponent<Text>();
        cfLbl.text = "确认"; cfLbl.alignment = TextAnchor.MiddleCenter;
        cfLbl.fontSize = 24; cfLbl.color = new Color(0.95f, 0.98f, 1f); cfLbl.font = font;
        cfLbl.raycastTarget = false;

        // 返回按钮 (左上角, 回选角槽位页)
        var backObj = new GameObject("BackBtn");
        backObj.transform.SetParent(panel.transform, false);
        var backRect = backObj.AddComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0, 1); backRect.anchorMax = new Vector2(0, 1);
        backRect.pivot = new Vector2(0, 1);
        backRect.anchoredPosition = new Vector2(16, -14);
        backRect.sizeDelta = new Vector2(44, 36);
        var backImg = backObj.AddComponent<Image>();
        backImg.color = new Color(0.08f, 0.06f, 0.12f, 0.9f);
        var backBtn = backObj.AddComponent<Button>();
        backBtn.transition = Selectable.Transition.None;
        UIHelper.SetupButtonFeedback(backBtn, backImg.color);
        backBtn.onClick.AddListener(OnBackClicked);
        var backLblObj = new GameObject("Lbl");
        backLblObj.transform.SetParent(backObj.transform, false);
        var backLblRect = backLblObj.AddComponent<RectTransform>();
        backLblRect.anchorMin = Vector2.zero; backLblRect.anchorMax = Vector2.one;
        backLblRect.offsetMin = Vector2.zero; backLblRect.offsetMax = Vector2.zero;
        var backLbl = backLblObj.AddComponent<Text>();
        backLbl.text = "←"; backLbl.alignment = TextAnchor.MiddleCenter;
        backLbl.fontSize = 22; backLbl.color = UIHelper.Accent; backLbl.font = font;
        backLbl.raycastTarget = false;

        panel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (panel != null) Destroy(panel);
        if (Instance == this) Instance = null;
    }
}
