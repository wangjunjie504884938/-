using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 性别+起名选择页 (创建新角色第二步, 职业已在上一步选定) — 设计图素材版:
/// 全屏背景 + 左右甲骨文男/女符号(选中发光) + 中央职业立绘(随职业/性别切换, 点击查看属性) + 名称输入 + 确认
/// 确认后 GameManager.OnClassSelected 创建角色进入游戏
/// </summary>
public class GenderSelectUI : MonoBehaviour
{
    public static GenderSelectUI Instance { get; private set; }

    private GameObject panel;
    private Image _maleImg;
    private Image _femaleImg;
    private Image _previewImg;
    private InputField _nameInput;
    private Image _nameBoxBorder;
    private int _gender; // 0=男, 1=女
    private HeroClass _class = HeroClass.Warrior; // 上一步职业选择页确认的职业

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Show()
    {
        Show(HeroClass.Warrior);
    }

    /// <summary>heroClass — 职业选择页(第一步)确认的职业</summary>
    public void Show(HeroClass heroClass)
    {
        _class = heroClass;
        if (panel == null) BuildUI();
        if (TopNavBar.Instance != null) TopNavBar.Instance.HideBar();
        panel.SetActive(true);
        _gender = 0;
        _nameInput.text = "";
        if (_nameBoxBorder != null) _nameBoxBorder.color = new Color(0.3f, 0.25f, 0.1f, 0.6f);
        UpdateGenderVisual();
        StartCoroutine(UIHelper.FadeIn(panel, 0.2f));
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }

    /// <summary>设计图职业立绘路径 (方士无职业立绘, 用通用男/女角色图)</summary>
    private string PortraitPath(bool male)
    {
        return _class switch
        {
            HeroClass.Warrior => male ? ResourcePaths.PortraitWarriorM : ResourcePaths.PortraitWarriorF,
            HeroClass.Priest => male ? ResourcePaths.PortraitPriestM : ResourcePaths.PortraitPriestF,
            _ => male ? ResourcePaths.PortraitGenericM : ResourcePaths.PortraitGenericF,
        };
    }

    private IEnumerator LoadPreview()
    {
        if (_previewImg == null) yield break;
        // 设计图职业立绘
        var designed = Resources.Load<Sprite>(PortraitPath(_gender == 0));
        if (designed != null)
        {
            _previewImg.sprite = designed;
            yield break;
        }
        // 立绘缺失 → 回退职业占位sprite
        yield return CharacterSpriteFactory.LoadClassSpriteAsync(_class, sprite =>
        {
            if (_previewImg != null && sprite != null)
                _previewImg.sprite = sprite;
        });
    }

    private void ToggleGender(int gender)
    {
        _gender = Mathf.Clamp(gender, 0, 1);
        UpdateGenderVisual();
    }

    private void UpdateGenderVisual()
    {
        // 甲骨文灰色字形: 选中提亮(>1 tint), 未选保持原灰
        if (_maleImg != null) _maleImg.color = _gender == 0 ? new Color(1.9f, 1.9f, 1.9f, 1f) : Color.white;
        if (_femaleImg != null) _femaleImg.color = _gender == 1 ? new Color(1.9f, 1.9f, 1.9f, 1f) : Color.white;
        StartCoroutine(LoadPreview()); // 立绘随职业+性别刷新
    }

    private void OnPreviewClicked()
    {
        if (panel == null) return;
        ClassStatsPopup.Show(panel.transform, _class);
    }

    private void OnConfirm()
    {
        string name = _nameInput != null ? _nameInput.text.Trim() : "";
        if (string.IsNullOrEmpty(name))
        {
            if (_nameBoxBorder != null) _nameBoxBorder.color = new Color(0.8f, 0.2f, 0.2f, 0.8f);
            return;
        }
        PlayerProgressData.SaveCharacterGender(_gender);
        PlayerProgressData.SaveCharacterName(name);
        Hide();
        GameManager.Instance.OnClassSelected(_class);
    }

    private void OnBackClicked()
    {
        Hide();
        // 职业选择页只是被隐藏保留 — 组件可能已被销毁, 缺失时重建(与职业页返回同一挂载点)
        var charSelect = CharacterSelectUI.Instance;
        if (charSelect == null) charSelect = GameManager.Instance.gameObject.AddComponent<CharacterSelectUI>();
        charSelect.Show();
    }

    private void BuildUI()
    {
        Font font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();

        panel = new GameObject("GenderSelectPanel");
        panel.transform.SetParent(canvas.transform, false);
        var pr = panel.AddComponent<RectTransform>();
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one;
        pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;
        panel.AddComponent<CanvasGroup>();

        // 全屏设计图背景
        var bgObj = new GameObject("BG");
        bgObj.transform.SetParent(panel.transform, false);
        var bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero; bgRect.offsetMax = Vector2.zero;
        var bgImg = bgObj.AddComponent<Image>();
        bgImg.sprite = Resources.Load<Sprite>(ResourcePaths.CreateBg);
        bgImg.raycastTarget = false;

        // 中央职业立绘 (点击查看属性)
        var previewObj = new GameObject("Preview");
        previewObj.transform.SetParent(panel.transform, false);
        var pvRect = previewObj.AddComponent<RectTransform>();
        pvRect.anchorMin = new Vector2(0.5f, 0.5f); pvRect.anchorMax = new Vector2(0.5f, 0.5f);
        pvRect.pivot = new Vector2(0.5f, 0.5f);
        pvRect.anchoredPosition = new Vector2(0, -30);
        pvRect.sizeDelta = new Vector2(400, 700);
        _previewImg = previewObj.AddComponent<Image>();
        _previewImg.preserveAspect = true;
        var pvBtn = previewObj.AddComponent<Button>();
        pvBtn.targetGraphic = _previewImg;
        pvBtn.onClick.AddListener(OnPreviewClicked);

        // 左右甲骨文性别符号 (源图统一, 选中由tint提亮, 点击切换)
        _maleImg = CreateGenderSide(panel.transform, "MaleSide", -810, ResourcePaths.GenderMale, () => ToggleGender(0));
        _femaleImg = CreateGenderSide(panel.transform, "FemaleSide", 810, ResourcePaths.GenderFemale, () => ToggleGender(1));

        // 名称框 (设计图素材 + InputField)
        var nameBoxObj = new GameObject("NameBox");
        nameBoxObj.transform.SetParent(panel.transform, false);
        var nbRect = nameBoxObj.AddComponent<RectTransform>();
        nbRect.anchorMin = new Vector2(0.5f, 0.5f); nbRect.anchorMax = new Vector2(0.5f, 0.5f);
        nbRect.pivot = new Vector2(0.5f, 0.5f);
        nbRect.anchoredPosition = new Vector2(-100, -415);
        nbRect.sizeDelta = new Vector2(360, 79);
        var nbImg = nameBoxObj.AddComponent<Image>();
        nbImg.sprite = Resources.Load<Sprite>(ResourcePaths.NameBox);
        nbImg.type = Image.Type.Sliced;
        _nameBoxBorder = nbImg;

        var inputObj = new GameObject("NameInput");
        inputObj.transform.SetParent(nameBoxObj.transform, false);
        var inRect = inputObj.AddComponent<RectTransform>();
        inRect.anchorMin = Vector2.zero; inRect.anchorMax = Vector2.one;
        inRect.offsetMin = new Vector2(24, 8); inRect.offsetMax = new Vector2(-24, -8);
        var inTextObj = new GameObject("Text");
        inTextObj.transform.SetParent(inputObj.transform, false);
        var inTRect = inTextObj.AddComponent<RectTransform>();
        inTRect.anchorMin = Vector2.zero; inTRect.anchorMax = Vector2.one;
        inTRect.offsetMin = new Vector2(8, 0); inTRect.offsetMax = new Vector2(-8, 0);
        var inText = inTextObj.AddComponent<Text>();
        inText.text = ""; inText.alignment = TextAnchor.MiddleLeft;
        inText.fontSize = 26; inText.color = Color.white; inText.font = font;
        var phObj = new GameObject("Placeholder");
        phObj.transform.SetParent(inputObj.transform, false);
        var phRect = phObj.AddComponent<RectTransform>();
        phRect.anchorMin = Vector2.zero; phRect.anchorMax = Vector2.one;
        phRect.offsetMin = new Vector2(8, 0); phRect.offsetMax = new Vector2(-8, 0);
        var phText = phObj.AddComponent<Text>();
        phText.text = "输入名称"; phText.alignment = TextAnchor.MiddleLeft;
        phText.fontSize = 24; phText.color = new Color(0.55f, 0.55f, 0.6f); phText.font = font;
        phText.fontStyle = FontStyle.Italic;
        _nameInput = inputObj.AddComponent<InputField>();
        _nameInput.textComponent = inText;
        _nameInput.placeholder = phText;
        _nameInput.characterLimit = 12;
        _nameInput.contentType = InputField.ContentType.Standard;

        // 确认按钮 (设计图素材 + 确认文字)
        var confirmObj = new GameObject("ConfirmBtn");
        confirmObj.transform.SetParent(panel.transform, false);
        var cfRect = confirmObj.AddComponent<RectTransform>();
        cfRect.anchorMin = new Vector2(0.5f, 0.5f); cfRect.anchorMax = new Vector2(0.5f, 0.5f);
        cfRect.pivot = new Vector2(0.5f, 0.5f);
        cfRect.anchoredPosition = new Vector2(175, -413);
        cfRect.sizeDelta = new Vector2(150, 62);
        var cfImg = confirmObj.AddComponent<Image>();
        cfImg.sprite = Resources.Load<Sprite>(ResourcePaths.ConfirmBtn);
        var cfBtn = confirmObj.AddComponent<Button>();
        cfBtn.targetGraphic = cfImg;
        cfBtn.onClick.AddListener(OnConfirm);
        var cfLblObj = new GameObject("Label");
        cfLblObj.transform.SetParent(confirmObj.transform, false);
        var cfLblRect = cfLblObj.AddComponent<RectTransform>();
        cfLblRect.anchorMin = Vector2.zero; cfLblRect.anchorMax = Vector2.one;
        cfLblRect.offsetMin = Vector2.zero; cfLblRect.offsetMax = Vector2.zero;
        var cfLbl = cfLblObj.AddComponent<Text>();
        cfLbl.text = "确认"; cfLbl.alignment = TextAnchor.MiddleCenter;
        cfLbl.fontSize = 24; cfLbl.color = new Color(0.95f, 0.98f, 1f); cfLbl.font = font;
        cfLbl.raycastTarget = false;

        // 返回按钮 (左上角, 回职业选择页)
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

    /// <summary>甲骨文性别符号侧按钮 (源图统一, 选中态由UpdateGenderVisual的tint区分, 点击切换)</summary>
    private Image CreateGenderSide(Transform parent, string name, float xPos, string spritePath, System.Action onToggle)
    {
        var sideObj = new GameObject(name);
        sideObj.transform.SetParent(parent, false);
        var sRect = sideObj.AddComponent<RectTransform>();
        sRect.anchorMin = new Vector2(0.5f, 0.5f); sRect.anchorMax = new Vector2(0.5f, 0.5f);
        sRect.pivot = new Vector2(0.5f, 0.5f);
        sRect.anchoredPosition = new Vector2(xPos, -30);
        sRect.sizeDelta = new Vector2(167, 300);
        var sImg = sideObj.AddComponent<Image>();
        sImg.sprite = Resources.Load<Sprite>(spritePath);
        sImg.preserveAspect = true;
        var sBtn = sideObj.AddComponent<Button>();
        sBtn.targetGraphic = sImg;
        sBtn.onClick.AddListener(() => onToggle());
        return sImg;
    }

    private void OnDestroy()
    {
        if (panel != null) Destroy(panel);
        if (Instance == this) Instance = null;
    }
}
