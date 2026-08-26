using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class CharacterSelectUI : MonoBehaviour
{
    public static CharacterSelectUI Instance { get; private set; }

    private GameObject panel;
    private HeroClass selectedClass = HeroClass.Warrior;
    private Text classNameText;
    private Text classDescText;
    private Text statText;
    private Text skillText;
    private Image[] classCards = new Image[3];
    private Image[] classIcons = new Image[3];
    private bool confirmed;
    private GameObject _namePopup;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Show()
    {
        confirmed = false;
        if (panel == null) BuildUI();
        // 选择角色页面不显示金币碎片
        if (TopNavBar.Instance != null) TopNavBar.Instance.HideBar();
        panel.SetActive(true);
        StartCoroutine(UIHelper.FadeIn(panel, 0.2f));
        UpdateDisplay();
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }

    public HeroClass? GetSelectedClass()
    {
        return confirmed ? selectedClass : null;
    }

    private void BuildUI()
    {
        Font font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();

        panel = new GameObject("CharacterSelectPanel");
        panel.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero; panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero; panelRect.offsetMax = Vector2.zero;
        panel.AddComponent<CanvasGroup>();
        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.05f, 0.1f, 0.95f);
        bg.raycastTarget = false;

        // 3 class cards with pixel-art character sprites
        string[] classNames = { "战士", "法师", "牧师" };
        HeroClass[] classes = { HeroClass.Warrior, HeroClass.Mage, HeroClass.Priest };

        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            ClassData cd = ClassData.GetClassData(classes[i]);

            // Card background
            GameObject btnObj = new GameObject("ClassCard_" + classNames[i]);
            btnObj.transform.SetParent(panel.transform, false);
            RectTransform btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 1); btnRect.anchorMax = new Vector2(0.5f, 1);
            btnRect.pivot = new Vector2(0.5f, 1);
            btnRect.anchoredPosition = new Vector2(-300 + i * 300, -100);
            btnRect.sizeDelta = new Vector2(260, 340);

            Image btnImg = btnObj.AddComponent<Image>();
            btnImg.color = new Color(cd.PrimaryColor.r * 0.2f, cd.PrimaryColor.g * 0.2f, cd.PrimaryColor.b * 0.2f, 0.95f);
            classCards[i] = btnImg;

            // Card border
            GameObject borderObj = new GameObject("Border");
            borderObj.transform.SetParent(btnObj.transform, false);
            RectTransform bRect = borderObj.AddComponent<RectTransform>();
            bRect.anchorMin = Vector2.zero; bRect.anchorMax = Vector2.one;
            bRect.offsetMin = new Vector2(-2, -2); bRect.offsetMax = new Vector2(2, 2);
            Image borderImg = borderObj.AddComponent<Image>();
            borderImg.color = cd.PrimaryColor * 0.5f;
            borderImg.raycastTarget = false;

            // Inner
            GameObject innerObj = new GameObject("Inner");
            innerObj.transform.SetParent(borderObj.transform, false);
            RectTransform iRect = innerObj.AddComponent<RectTransform>();
            iRect.anchorMin = Vector2.zero; iRect.anchorMax = Vector2.one;
            iRect.offsetMin = new Vector2(2, 2); iRect.offsetMax = new Vector2(-2, -2);
            Image innerImg = innerObj.AddComponent<Image>();
            innerImg.color = new Color(cd.PrimaryColor.r * 0.15f, cd.PrimaryColor.g * 0.15f, cd.PrimaryColor.b * 0.15f, 0.98f);
            innerImg.raycastTarget = false;

            // Character sprite preview (pixel art generated per class)
            GameObject iconObj = new GameObject("CharPreview");
            iconObj.transform.SetParent(btnObj.transform, false);
            RectTransform iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.7f); iconRect.anchorMax = new Vector2(0.5f, 0.7f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(120, 120);
            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.color = Color.white;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            classIcons[i] = iconImg;

            // 异步加载角色预览Sprite (不阻塞UI构建)
            StartCoroutine(LoadPreviewSprite(classes[i], iconImg));

            // Class name
            GameObject nameObj = new GameObject("Name");
            nameObj.transform.SetParent(btnObj.transform, false);
            RectTransform nameRect = nameObj.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0.25f); nameRect.anchorMax = new Vector2(1, 0.35f);
            nameRect.offsetMin = Vector2.zero; nameRect.offsetMax = Vector2.zero;
            Text nameText = nameObj.AddComponent<Text>();
            nameText.text = classNames[i]; nameText.alignment = TextAnchor.MiddleCenter;
            nameText.fontSize = 28; nameText.color = cd.PrimaryColor; nameText.font = font;
            nameText.raycastTarget = false;

            // Short description
            GameObject descObj = new GameObject("ShortDesc");
            descObj.transform.SetParent(btnObj.transform, false);
            RectTransform descRect = descObj.AddComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0, 0.05f); descRect.anchorMax = new Vector2(1, 0.25f);
            descRect.offsetMin = new Vector2(10, 0); descRect.offsetMax = new Vector2(-10, 0);
            Text descText = descObj.AddComponent<Text>();
            descText.text = cd.Description; descText.alignment = TextAnchor.MiddleCenter;
            descText.fontSize = 16; descText.color = new Color(0.75f, 0.75f, 0.8f); descText.font = font;
            descText.lineSpacing = 1.2f;
            descText.raycastTarget = false;

            // Make the entire card clickable
            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.onClick.AddListener(() => { selectedClass = classes[idx]; UpdateDisplay(); });

            // Hover highlight colors
            ColorBlock colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            colors.pressedColor = new Color(0.9f, 0.9f, 0.9f);
            btn.colors = colors;
        }

        // 异步加载角色预览Sprite
        // 加载完成后释放Asset句柄, 仅保留Sprite实例

        BuildDetailPanel(font);
        BuildNavButtons(font);
    }

    private IEnumerator LoadPreviewSprite(HeroClass heroClass, Image targetImg)
    {
        if (targetImg == null) yield break;
        yield return CharacterSpriteFactory.LoadClassSpriteAsync(heroClass, sprite =>
        {
            if (targetImg != null && sprite != null)
                targetImg.sprite = sprite;
        });
    }

    private void BuildDetailPanel(Font font)
    {
        // Detail info panel (bottom)
        GameObject infoObj = new GameObject("InfoPanel");
        infoObj.transform.SetParent(panel.transform, false);
        RectTransform infoRect = infoObj.AddComponent<RectTransform>();
        infoRect.anchorMin = new Vector2(0.5f, 0); infoRect.anchorMax = new Vector2(0.5f, 0);
        infoRect.pivot = new Vector2(0.5f, 0);
        infoRect.anchoredPosition = new Vector2(0, 80); infoRect.sizeDelta = new Vector2(700, 180);
        Image infoBg = infoObj.AddComponent<Image>();
        infoBg.color = new Color(0.08f, 0.08f, 0.14f, 0.95f);

        // Border
        GameObject infoBorderObj = new GameObject("InfoBorder");
        infoBorderObj.transform.SetParent(infoObj.transform, false);
        RectTransform ibRect = infoBorderObj.AddComponent<RectTransform>();
        ibRect.anchorMin = Vector2.zero; ibRect.anchorMax = Vector2.one;
        ibRect.offsetMin = new Vector2(-2, -2); ibRect.offsetMax = new Vector2(2, 2);
        Image ibImg = infoBorderObj.AddComponent<Image>();
        ibImg.color = new Color(0.4f, 0.35f, 0.5f, 0.6f);
        ibImg.raycastTarget = false;

        GameObject infoInnerObj = new GameObject("InfoInner");
        infoInnerObj.transform.SetParent(infoBorderObj.transform, false);
        RectTransform iiRect = infoInnerObj.AddComponent<RectTransform>();
        iiRect.anchorMin = Vector2.zero; iiRect.anchorMax = Vector2.one;
        iiRect.offsetMin = new Vector2(2, 2); iiRect.offsetMax = new Vector2(-2, -2);
        Image iiImg = infoInnerObj.AddComponent<Image>();
        iiImg.color = new Color(0.08f, 0.08f, 0.14f, 0.98f);
        iiImg.raycastTarget = false;

        // Class name
        GameObject cnObj = new GameObject("ClassName");
        cnObj.transform.SetParent(infoObj.transform, false);
        RectTransform cnRect = cnObj.AddComponent<RectTransform>();
        cnRect.anchorMin = new Vector2(0, 1); cnRect.anchorMax = new Vector2(1, 1);
        cnRect.pivot = new Vector2(0.5f, 1);
        cnRect.anchoredPosition = new Vector2(0, -10); cnRect.sizeDelta = new Vector2(680, 35);
        classNameText = cnObj.AddComponent<Text>();
        classNameText.alignment = TextAnchor.MiddleCenter; classNameText.fontSize = 26;
        classNameText.color = Color.white; classNameText.font = font;

        // Stats (left half)
        GameObject statObj = new GameObject("Stats");
        statObj.transform.SetParent(infoObj.transform, false);
        RectTransform stRect = statObj.AddComponent<RectTransform>();
        stRect.anchorMin = new Vector2(0, 0); stRect.anchorMax = new Vector2(0.5f, 1);
        stRect.pivot = new Vector2(0, 1);
        stRect.offsetMin = new Vector2(20, 10); stRect.offsetMax = new Vector2(-5, -45);
        statText = statObj.AddComponent<Text>();
        statText.alignment = TextAnchor.UpperLeft; statText.fontSize = 16;
        statText.color = new Color(0.9f, 0.9f, 0.9f); statText.font = font;
        statText.lineSpacing = 1.3f;

        // Skills (right half)
        GameObject skillObj = new GameObject("Skills");
        skillObj.transform.SetParent(infoObj.transform, false);
        RectTransform skRect = skillObj.AddComponent<RectTransform>();
        skRect.anchorMin = new Vector2(0.5f, 0); skRect.anchorMax = new Vector2(1, 1);
        skRect.pivot = new Vector2(0, 1);
        skRect.offsetMin = new Vector2(5, 10); skRect.offsetMax = new Vector2(-20, -45);
        skillText = skillObj.AddComponent<Text>();
        skillText.alignment = TextAnchor.UpperLeft; skillText.fontSize = 16;
        skillText.color = new Color(0.7f, 0.9f, 1f); skillText.font = font;
        skillText.lineSpacing = 1.3f;

        // Confirm button
        GameObject confirmObj = new GameObject("ConfirmBtn");
        confirmObj.transform.SetParent(panel.transform, false);
        RectTransform confirmRect = confirmObj.AddComponent<RectTransform>();
        confirmRect.anchorMin = new Vector2(0.5f, 0); confirmRect.anchorMax = new Vector2(0.5f, 0);
        confirmRect.pivot = new Vector2(0.5f, 0);
        confirmRect.anchoredPosition = new Vector2(0, 25); confirmRect.sizeDelta = new Vector2(280, 55);

        // Button border
        GameObject cBorderObj = new GameObject("ConfirmBorder");
        cBorderObj.transform.SetParent(confirmObj.transform, false);
        RectTransform cbRect = cBorderObj.AddComponent<RectTransform>();
        cbRect.anchorMin = Vector2.zero; cbRect.anchorMax = Vector2.one;
        cbRect.offsetMin = new Vector2(-2, -2); cbRect.offsetMax = new Vector2(2, 2);
        Image cbImg = cBorderObj.AddComponent<Image>();
        cbImg.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        cbImg.raycastTarget = false;

        GameObject cInnerObj = new GameObject("ConfirmInner");
        cInnerObj.transform.SetParent(cBorderObj.transform, false);
        RectTransform ciRect = cInnerObj.AddComponent<RectTransform>();
        ciRect.anchorMin = Vector2.zero; ciRect.anchorMax = Vector2.one;
        ciRect.offsetMin = new Vector2(2, 2); ciRect.offsetMax = new Vector2(-2, -2);
        Image ciImg = cInnerObj.AddComponent<Image>();
        ciImg.color = new Color(0.2f, 0.55f, 0.2f, 0.95f);

        Button confirmBtn = confirmObj.AddComponent<Button>();
        confirmBtn.targetGraphic = ciImg;
        confirmBtn.onClick.AddListener(OnConfirm);

        GameObject confirmLabel = new GameObject("Label");
        confirmLabel.transform.SetParent(confirmObj.transform, false);
        RectTransform clRect = confirmLabel.AddComponent<RectTransform>();
        clRect.anchorMin = Vector2.zero; clRect.anchorMax = Vector2.one;
        clRect.offsetMin = Vector2.zero; clRect.offsetMax = Vector2.zero;
        Text clText = confirmLabel.AddComponent<Text>();
        clText.text = "确认选择"; clText.alignment = TextAnchor.MiddleCenter;
        clText.fontSize = 26; clText.color = new Color(1f, 0.95f, 0.7f); clText.font = font;
        clText.raycastTarget = false;

        panel.SetActive(false);
    }

    private void BuildNavButtons(Font font)
    {
        // 导航顶栏 (TopNavBar 单例) — SetupTopBar moved to Show()
    }

    private void UpdateDisplay()
    {
        var data = ClassData.GetClassData(selectedClass);
        if (data == null) return;

        if (classNameText != null)
        {
            classNameText.text = data.ClassName;
            classNameText.color = data.PrimaryColor;
        }
        if (statText != null)
        {
            statText.text = $"生命: {data.BaseHp}  攻击: {data.BaseAttack}\n" +
                           $"防御: {data.BaseDefense}  速度: {data.BaseMoveSpeed:F1}\n" +
                           $"暴击: {data.BaseCritChance * 100:F0}%  攻击范围: {data.AutoAttackRange:F1}\n" +
                           $"每级: +{data.HpPerLevel}HP +{data.AttackPerLevel}ATK";
        }
        if (skillText != null)
        {
            skillText.text = $"技能:\n[Q] {data.Skill1Name}\n[E] {data.Skill2Name}\n[R] {data.Skill3Name}";
        }

        // Highlight selected card
        for (int i = 0; i < 3; i++)
        {
            HeroClass cardClass = (HeroClass)i;
            ClassData cd = ClassData.GetClassData(cardClass);
            if (classCards[i] != null)
            {
                if (cardClass == selectedClass)
                {
                    classCards[i].color = new Color(cd.PrimaryColor.r * 0.4f, cd.PrimaryColor.g * 0.4f, cd.PrimaryColor.b * 0.4f, 1f);
                    // Scale up selected card slightly
                    classCards[i].transform.localScale = new Vector3(1.05f, 1.05f, 1f);
                }
                else
                {
                    classCards[i].color = new Color(cd.PrimaryColor.r * 0.15f, cd.PrimaryColor.g * 0.15f, cd.PrimaryColor.b * 0.15f, 0.8f);
                    classCards[i].transform.localScale = Vector3.one;
                }
            }
        }
    }

    private void OnConfirm()
    {
        ShowNamePopup();
    }

    private void ShowNamePopup()
    {
        if (_namePopup != null) { _namePopup.SetActive(true); return; }
        Font font = GameManager.GetUIFont();

        // Full-screen dim overlay
        _namePopup = new GameObject("NamePopup");
        _namePopup.transform.SetParent(panel.transform, false);
        RectTransform popupRect = _namePopup.AddComponent<RectTransform>();
        popupRect.anchorMin = Vector2.zero; popupRect.anchorMax = Vector2.one;
        popupRect.offsetMin = Vector2.zero; popupRect.offsetMax = Vector2.zero;
        Image dimImg = _namePopup.AddComponent<Image>();
        dimImg.color = new Color(0, 0, 0, 0.7f);

        // Card (centered)
        GameObject card = new GameObject("PopupCard");
        card.transform.SetParent(_namePopup.transform, false);
        RectTransform cardRect = card.AddComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f); cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = Vector2.zero; cardRect.sizeDelta = new Vector2(480, 320);
        Image cardBg = card.AddComponent<Image>();
        cardBg.color = new Color(0.08f, 0.08f, 0.14f, 0.98f);

        // Card border
        GameObject borderObj = new GameObject("CardBorder");
        borderObj.transform.SetParent(card.transform, false);
        RectTransform bRect = borderObj.AddComponent<RectTransform>();
        bRect.anchorMin = Vector2.zero; bRect.anchorMax = Vector2.one;
        bRect.offsetMin = new Vector2(-2, -2); bRect.offsetMax = new Vector2(2, 2);
        Image borderImg = borderObj.AddComponent<Image>();
        borderImg.color = new Color(0.4f, 0.35f, 0.5f, 0.6f);
        borderImg.raycastTarget = false;
        borderObj.transform.SetAsFirstSibling();

        // Title
        GameObject titleObj = new GameObject("PopupTitle");
        titleObj.transform.SetParent(card.transform, false);
        RectTransform tRect = titleObj.AddComponent<RectTransform>();
        tRect.anchorMin = new Vector2(0.5f, 1); tRect.anchorMax = new Vector2(0.5f, 1);
        tRect.pivot = new Vector2(0.5f, 1); tRect.anchoredPosition = new Vector2(0, -20);
        tRect.sizeDelta = new Vector2(440, 45);
        Text titleText = titleObj.AddComponent<Text>();
        titleText.text = "为你的角色起个名字";
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.fontSize = 28; titleText.color = new Color(1f, 0.9f, 0.3f); titleText.font = font;
        titleText.raycastTarget = false;

        // Input field
        GameObject inputObj = new GameObject("NameInput");
        inputObj.transform.SetParent(card.transform, false);
        RectTransform inputRect = inputObj.AddComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0.5f, 0.5f); inputRect.anchorMax = new Vector2(0.5f, 0.5f);
        inputRect.pivot = new Vector2(0.5f, 0.5f);
        inputRect.anchoredPosition = new Vector2(0, 10); inputRect.sizeDelta = new Vector2(360, 50);
        Image inputBg = inputObj.AddComponent<Image>();
        inputBg.color = new Color(0.05f, 0.05f, 0.1f, 0.95f);

        // Input border
        GameObject inputBorderObj = new GameObject("InputBorder");
        inputBorderObj.transform.SetParent(inputObj.transform, false);
        RectTransform ibRect = inputBorderObj.AddComponent<RectTransform>();
        ibRect.anchorMin = Vector2.zero; ibRect.anchorMax = Vector2.one;
        ibRect.offsetMin = new Vector2(-1, -1); ibRect.offsetMax = new Vector2(1, 1);
        Image ibImg = inputBorderObj.AddComponent<Image>();
        ibImg.color = new Color(0.4f, 0.35f, 0.5f, 0.6f);
        ibImg.raycastTarget = false;
        ibImg.transform.SetAsFirstSibling();

        // Placeholder
        GameObject phObj = new GameObject("Placeholder");
        phObj.transform.SetParent(inputObj.transform, false);
        RectTransform phRect = phObj.AddComponent<RectTransform>();
        phRect.anchorMin = Vector2.zero; phRect.anchorMax = Vector2.one;
        phRect.offsetMin = new Vector2(10, 0); phRect.offsetMax = new Vector2(-10, 0);
        Text phText = phObj.AddComponent<Text>();
        phText.text = "输入角色名 (1-12字)";
        phText.alignment = TextAnchor.MiddleLeft;
        phText.fontSize = 18; phText.color = new Color(0.4f, 0.4f, 0.45f); phText.font = font;
        phText.fontStyle = FontStyle.Italic;

        // Input text component
        GameObject inputTextObj = new GameObject("Text");
        inputTextObj.transform.SetParent(inputObj.transform, false);
        RectTransform inputTextRect = inputTextObj.AddComponent<RectTransform>();
        inputTextRect.anchorMin = Vector2.zero; inputTextRect.anchorMax = Vector2.one;
        inputTextRect.offsetMin = new Vector2(10, 0); inputTextRect.offsetMax = new Vector2(-10, 0);
        Text inputText = inputTextObj.AddComponent<Text>();
        inputText.text = ""; inputText.alignment = TextAnchor.MiddleLeft;
        inputText.fontSize = 22; inputText.color = Color.white; inputText.font = font;

        InputField nameInput = inputObj.AddComponent<InputField>();
        nameInput.textComponent = inputText;
        nameInput.placeholder = phText;
        nameInput.characterLimit = 12;
        nameInput.contentType = InputField.ContentType.Standard;

        // Confirm button
        GameObject confirmObj = new GameObject("PopupConfirmBtn");
        confirmObj.transform.SetParent(card.transform, false);
        RectTransform cRect = confirmObj.AddComponent<RectTransform>();
        cRect.anchorMin = new Vector2(0.5f, 0); cRect.anchorMax = new Vector2(0.5f, 0);
        cRect.pivot = new Vector2(0.5f, 0);
        cRect.anchoredPosition = new Vector2(0, 30); cRect.sizeDelta = new Vector2(220, 50);

        GameObject cBorderObj = new GameObject("ConfirmBorder");
        cBorderObj.transform.SetParent(confirmObj.transform, false);
        RectTransform cbRect = cBorderObj.AddComponent<RectTransform>();
        cbRect.anchorMin = Vector2.zero; cbRect.anchorMax = Vector2.one;
        cbRect.offsetMin = new Vector2(-2, -2); cbRect.offsetMax = new Vector2(2, 2);
        Image cbImg = cBorderObj.AddComponent<Image>();
        cbImg.color = new Color(0.2f, 0.55f, 0.2f, 0.9f);
        cbImg.raycastTarget = false;

        GameObject cInnerObj = new GameObject("ConfirmInner");
        cInnerObj.transform.SetParent(cBorderObj.transform, false);
        RectTransform ciRect = cInnerObj.AddComponent<RectTransform>();
        ciRect.anchorMin = Vector2.zero; ciRect.anchorMax = Vector2.one;
        ciRect.offsetMin = new Vector2(2, 2); ciRect.offsetMax = new Vector2(-2, -2);
        Image ciImg = cInnerObj.AddComponent<Image>();
        ciImg.color = new Color(0.15f, 0.4f, 0.15f, 0.95f);

        Button confirmBtn = confirmObj.AddComponent<Button>();
        confirmBtn.targetGraphic = ciImg;

        GameObject cLabel = new GameObject("Label");
        cLabel.transform.SetParent(confirmObj.transform, false);
        RectTransform clRect = cLabel.AddComponent<RectTransform>();
        clRect.anchorMin = Vector2.zero; clRect.anchorMax = Vector2.one;
        clRect.offsetMin = Vector2.zero; clRect.offsetMax = Vector2.zero;
        Text clText = cLabel.AddComponent<Text>();
        clText.text = "确认"; clText.alignment = TextAnchor.MiddleCenter;
        clText.fontSize = 24; clText.color = new Color(1f, 0.95f, 0.7f); clText.font = font;
        clText.raycastTarget = false;

        confirmBtn.onClick.AddListener(() => ConfirmName(nameInput, ibImg));
    }

    private void ConfirmName(InputField nameInput, Image inputBorder)
    {
        string name = nameInput != null ? nameInput.text.Trim() : "";
        if (string.IsNullOrEmpty(name))
        {
            // Flash the input border red
            if (inputBorder != null) inputBorder.color = new Color(0.8f, 0.2f, 0.2f, 0.8f);
            return;
        }
        PlayerProgressData.SaveCharacterName(name);
        confirmed = true;
        Hide();
        GameManager.Instance.OnClassSelected(selectedClass);
    }

    private void OnDestroy()
    {
        if (_namePopup != null) Destroy(_namePopup);
        if (panel != null) Destroy(panel);
        if (Instance == this) Instance = null;
    }
}
