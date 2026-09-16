using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 角色槽位选择页面 — 显示当前账号的3个角色槽位
/// 点击卡片选中, 底部统一"开始游戏"按钮确认
/// </summary>
public partial class CharacterSlotUI : MonoBehaviour
{
    public static CharacterSlotUI Instance { get; private set; }

    private GameObject panel;
    private CanvasGroup _canvasGroup;
    private Text _statusText;
    private CloudSaveManager.SlotSummary[] _slots;
    private bool _loading;
    private Coroutine _loadTimeoutCo;
    private Coroutine _pulseCo;
    private GameObject _confirmDialog;

    private int _selectedSlot = -1;
    private List<Image> _cardGlows = new List<Image>();
    private List<Image> _cardBorders = new List<Image>();
    private List<Image> _cardBgs = new List<Image>();
    private List<RectTransform> _cardRects = new List<RectTransform>();
    private List<bool> _cardOccupied = new List<bool>();
    private List<Color> _cardColors = new List<Color>();

    private GameObject _startBtn;
    private GameObject _deleteBtn;
    private Text _startBtnLabel;

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
        _selectedSlot = -1;
        StartCoroutine(UIHelper.FadeIn(panel, 0.2f));
        RefreshSlots();
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }

    private void RefreshSlots()
    {
        if (_loading) return;
        _loading = true;
        _selectedSlot = -1;
        UpdateBottomButtons();

        if (_statusText != null)
        {
            _statusText.text = "正在获取角色列表...";
            _statusText.color = UIHelper.Accent;
        }

        if (CloudSaveManager.Instance != null && CloudSaveManager.Instance.IsLoggedIn)
        {
            _loadTimeoutCo = StartCoroutine(LoadTimeout(10f));
            CloudSaveManager.Instance.ListSlots(slots =>
            {
                if (this == null) return;
                if (_loadTimeoutCo != null) { StopCoroutine(_loadTimeoutCo); _loadTimeoutCo = null; }
                _loading = false;

                // Check if server returned any occupied slots
                bool anyOccupied = slots != null && System.Array.Exists(slots, s => s != null && s.occupied);
                if (!anyOccupied)
                {
                    // Server has no save data — fall back to local PlayerPrefs
                    _slots = BuildLocalSlotSummaries();
                    if (_slots != null && _statusText != null)
                    {
                        _statusText.text = "使用本地存档";
                        _statusText.color = new Color(1f, 0.7f, 0.3f);
                    }
                }
                else
                {
                    _slots = slots;
                }
                UpdateSlotCards();
            });
        }
        else
        {
            _loading = false;
            _slots = null;
            UpdateSlotCards();
        }
    }

    private IEnumerator LoadTimeout(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        _loading = false;
        _slots = null;
        if (_statusText != null)
        {
            _statusText.text = "获取角色列表超时，请检查网络连接";
            _statusText.color = new Color(1f, 0.5f, 0.5f);
        }
        UpdateSlotCards();
    }

    private void UpdateSlotCards()
    {
        if (panel == null) return;
        Font font = GameManager.GetUIFont();

        Transform slotsContainer = panel.transform.Find("SlotsContainer");
        if (slotsContainer == null) return;
        for (int i = slotsContainer.childCount - 1; i >= 0; i--)
            Destroy(slotsContainer.GetChild(i).gameObject);

        _cardGlows.Clear();
        _cardBorders.Clear();
        _cardBgs.Clear();
        _cardRects.Clear();
        _cardOccupied.Clear();
        _cardColors.Clear();

        CloudSaveManager.SlotSummary[] effectiveSlots = _slots;
        if (effectiveSlots == null)
        {
            effectiveSlots = BuildLocalSlotSummaries();
            if (effectiveSlots != null && _statusText != null)
            {
                _statusText.text = "无法连接服务器，显示本地存档";
                _statusText.color = new Color(1f, 0.7f, 0.3f);
            }
        }
        else if (_statusText != null)
        {
            _statusText.text = "";
        }

        for (int i = 0; i < 3; i++)
        {
            var summary = effectiveSlots != null && i < effectiveSlots.Length ? effectiveSlots[i] : null;
            bool occupied = summary != null && summary.occupied;
            GameObject card = CreateSlotCard(slotsContainer, i, summary, occupied, font);
            StartCoroutine(AnimateCardEntrance(card, i * 0.1f));
        }

        if (_pulseCo != null) StopCoroutine(_pulseCo);
        _pulseCo = StartCoroutine(PulseEmptySlots());
    }

    private CloudSaveManager.SlotSummary[] BuildLocalSlotSummaries()
    {
        var slots = SlotData.GetLocalSlots();
        var summaries = new CloudSaveManager.SlotSummary[3];
        bool anyOccupied = false;
        for (int i = 0; i < 3; i++)
        {
            if (slots[i].occupied)
            {
                summaries[i] = new CloudSaveManager.SlotSummary
                {
                    slot = i, occupied = true,
                    classType = slots[i].classType,
                    level = slots[i].level,
                    highestStageCleared = slots[i].highestStageCleared,
                    characterName = slots[i].characterName,
                    updatedAt = null
                };
                anyOccupied = true;
            }
            else
            {
                summaries[i] = new CloudSaveManager.SlotSummary
                {
                    slot = i, occupied = false, classType = 0, level = 0,
                    highestStageCleared = 0, updatedAt = null
                };
            }
        }
        return anyOccupied ? summaries : null;
    }

    // ==== Card creation/visuals — moved to CharacterSlotUI.Cards.cs (partial class) ====

    private void OnCardClicked(int slot)
    {
        if (_confirmDialog != null) return;
        _selectedSlot = slot;
        UpdateSelectionVisuals();
        UpdateBottomButtons();
    }

    private void UpdateSelectionVisuals()
    {
        for (int i = 0; i < _cardBgs.Count; i++)
        {
            bool selected = i == _selectedSlot;
            Color c = _cardColors[i];
            Color baseBg = new Color(c.r * 0.12f, c.g * 0.12f, c.b * 0.12f, 0.95f);

            if (selected)
            {
                _cardBgs[i].color = new Color(c.r * 0.25f, c.g * 0.25f, c.b * 0.25f, 1f);
                _cardBorders[i].color = c;
                _cardGlows[i].color = new Color(c.r, c.g, c.b, 0.4f);
                if (_cardRects[i] != null)
                    _cardRects[i].localScale = Vector3.one * 1.03f;
            }
            else
            {
                _cardBgs[i].color = baseBg;
                _cardBorders[i].color = _cardOccupied[i] ? c * 0.6f : new Color(0.3f, 0.3f, 0.35f, 0.5f);
                _cardGlows[i].color = _cardOccupied[i]
                    ? new Color(c.r, c.g, c.b, 0.15f)
                    : new Color(0.2f, 0.2f, 0.25f, 0.05f);
                if (_cardRects[i] != null)
                    _cardRects[i].localScale = Vector3.one;
            }
        }
    }

    private void UpdateBottomButtons()
    {
        if (_startBtn == null) return;
        bool hasSelection = _selectedSlot >= 0;
        _startBtn.SetActive(hasSelection);

        if (hasSelection && _startBtnLabel != null)
        {
            _startBtnLabel.text = _cardOccupied[_selectedSlot] ? "进入游戏" : "创建新角色";
        }

        if (_deleteBtn != null)
            _deleteBtn.SetActive(hasSelection && _selectedSlot < _cardOccupied.Count && _cardOccupied[_selectedSlot]);
    }

    // ========== Hover 效果 ==========

    private void AddHoverEffect(GameObject card, Image cardImg, Image glowImg, Color classColor, bool occupied)
    {
        EventTrigger trigger = card.GetComponent<EventTrigger>();
        if (trigger == null) trigger = card.AddComponent<EventTrigger>();

        Color baseCardColor = cardImg.color;
        Color baseGlowColor = glowImg.color;
        Color hoverGlow = occupied
            ? new Color(classColor.r, classColor.g, classColor.b, 0.35f)
            : new Color(0.4f, 0.45f, 0.55f, 0.2f);

        var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        entry.callback.AddListener(_ =>
        {
            int idx = _cardRects.IndexOf(card.GetComponent<RectTransform>());
            if (idx != _selectedSlot)
            {
                cardImg.color = new Color(
                    Mathf.Min(baseCardColor.r * 1.3f, 1f),
                    Mathf.Min(baseCardColor.g * 1.3f, 1f),
                    Mathf.Min(baseCardColor.b * 1.3f, 1f),
                    baseCardColor.a);
                glowImg.color = hoverGlow;
            }
        });
        trigger.triggers.Add(entry);

        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ =>
        {
            int idx = _cardRects.IndexOf(card.GetComponent<RectTransform>());
            if (idx != _selectedSlot)
            {
                cardImg.color = baseCardColor;
                glowImg.color = baseGlowColor;
            }
        });
        trigger.triggers.Add(exit);
    }

    // ========== 动画 ==========

    private IEnumerator AnimateCardEntrance(GameObject card, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        RectTransform rect = card.GetComponent<RectTransform>();
        if (rect == null) yield break;
        float duration = 0.3f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duration);
            float s = 1f - Mathf.Pow(1f - p, 3f);
            rect.localScale = Vector3.one * s;
            yield return null;
        }
        rect.localScale = Vector3.one;
    }

    private IEnumerator PulseEmptySlots()
    {
        while (panel != null && panel.activeSelf)
        {
            float pulse = 0.4f + Mathf.Sin(Time.unscaledTime * 2f) * 0.2f;
            Transform slotsContainer = panel.transform.Find("SlotsContainer");
            if (slotsContainer != null)
            {
                for (int i = 0; i < slotsContainer.childCount; i++)
                {
                    Transform plus = slotsContainer.GetChild(i).Find("PlusIcon");
                    if (plus != null && i != _selectedSlot)
                    {
                        var txt = plus.GetComponent<Text>();
                        if (txt != null)
                            txt.color = new Color(0.3f, 0.3f, 0.35f, pulse);
                    }
                }
            }
            yield return null;
        }
    }

    // ==== Delete confirm dialog — moved to CharacterSlotUI.Confirm.cs (partial class) ====
    // ========== 开始游戏 ==========

    private void OnStartGame()
    {
        if (_selectedSlot < 0 || _confirmDialog != null) return;

        int slot = _selectedSlot;
        bool occupied = slot < _cardOccupied.Count && _cardOccupied[slot];

        PlayerProgressData.SetActiveSlot(slot);
        if (CloudSaveManager.Instance != null)
            CloudSaveManager.Instance.SetActiveSlot(slot);

        Hide();

        if (occupied)
        {
            bool offlineMode = _slots == null;
            if (!offlineMode && CloudSaveManager.Instance != null && CloudSaveManager.Instance.IsLoggedIn)
            {
                // 显示"加载数据中"遮罩，OnSlotLoaded内部会调用SyncStageFromServer
                ShowLoadingHint();
                GameManager.Instance.OnSlotLoaded();
            }
            else
            {
                // 离线模式
                RuntimePlayerData.Instance?.RefreshFromCache(LocalSettingsManager.GetCachedStage());
                GameManager.Instance.OnSlotLoaded();
            }
        }
        else
        {
            PlayerProgressData.ClearSave();
            GameManager.Instance.ShowCharacterSelect();
        }
    }

    // ========== UI 构建 ==========

    private void BuildUI()
    {
        Font font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();

        panel = UIHelper.MakeBackground(canvas.transform);
        panel.name = "CharacterSlotPanel";
        _canvasGroup = panel.GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = panel.AddComponent<CanvasGroup>();

        UIHelper.MakeSubtitle(panel.transform, "— 选择一个角色开始冒险 —", font, 115, 16);

        // Slots container
        GameObject slotsObj = new GameObject("SlotsContainer");
        slotsObj.transform.SetParent(panel.transform, false);
        RectTransform slotsRect = slotsObj.AddComponent<RectTransform>();
        slotsRect.anchorMin = new Vector2(0.5f, 1); slotsRect.anchorMax = new Vector2(0.5f, 1);
        slotsRect.pivot = new Vector2(0.5f, 1); slotsRect.anchoredPosition = new Vector2(0, -105);
        slotsRect.sizeDelta = new Vector2(950, 420);

        // Status text
        GameObject statusObj = new GameObject("Status");
        statusObj.transform.SetParent(panel.transform, false);
        RectTransform statusRect = statusObj.AddComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0.5f, 0); statusRect.anchorMax = new Vector2(0.5f, 0);
        statusRect.pivot = new Vector2(0.5f, 0); statusRect.anchoredPosition = new Vector2(0, 95);
        statusRect.sizeDelta = new Vector2(500, 30);
        _statusText = statusObj.AddComponent<Text>();
        _statusText.text = ""; _statusText.alignment = TextAnchor.MiddleCenter;
        _statusText.fontSize = 18; _statusText.color = UIHelper.Accent; _statusText.font = font;
        _statusText.raycastTarget = false;

        // Bottom bar: 开始游戏 | 删除角色
        // "开始游戏" button (bottom-center, hidden until selection)
        _startBtn = CreateBottomButton(panel.transform, "StartBtn", "进入游戏", font,
            new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(0, 25), new Vector2(260, 52),
            new Color(0.12f, 0.22f, 0.08f, 0.95f),
            new Color(0.3f, 0.55f, 0.1f, 0.8f),
            () => OnStartGame());
        _startBtnLabel = _startBtn.transform.Find("Label")?.GetComponent<Text>();
        _startBtn.SetActive(false);

        // "删除角色" button (bottom-right, hidden until occupied selection)
        _deleteBtn = CreateBottomButton(panel.transform, "DeleteBtn", "删除角色", font,
            new Vector2(0.88f, 0), new Vector2(0.88f, 0),
            new Vector2(0, 25), new Vector2(160, 48),
            new Color(0.18f, 0.06f, 0.08f, 0.9f),
            new Color(0.4f, 0.15f, 0.1f, 0.6f),
            () => { if (_selectedSlot >= 0) ShowDeleteConfirm(_selectedSlot, font); });
        _deleteBtn.SetActive(false);

        panel.SetActive(false);
    }

    private GameObject CreateBottomButton(Transform parent, string name, string label, Font font,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size,
        Color bgColor, Color borderColor, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btn = new GameObject(name);
        btn.transform.SetParent(parent, false);
        RectTransform rect = btn.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin; rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        GameObject border = new GameObject("Border");
        border.transform.SetParent(btn.transform, false);
        RectTransform bRect = border.AddComponent<RectTransform>();
        bRect.anchorMin = Vector2.zero; bRect.anchorMax = Vector2.one;
        bRect.offsetMin = new Vector2(-2, -2); bRect.offsetMax = new Vector2(2, 2);
        Image bImg = border.AddComponent<Image>();
        bImg.color = borderColor;
        bImg.raycastTarget = false;

        GameObject innerObj = new GameObject("Inner");
        innerObj.transform.SetParent(border.transform, false);
        RectTransform iRect = innerObj.AddComponent<RectTransform>();
        iRect.anchorMin = Vector2.zero; iRect.anchorMax = Vector2.one;
        iRect.offsetMin = Vector2.zero; iRect.offsetMax = Vector2.zero;
        Image iImg = innerObj.AddComponent<Image>();
        iImg.color = new Color(0.06f, 0.05f, 0.10f, 0.95f);
        iImg.raycastTarget = false;

        Image img = btn.AddComponent<Image>();
        img.color = bgColor;
        Button button = btn.AddComponent<Button>();
        button.targetGraphic = img;
        button.onClick.AddListener(onClick);

        GameObject lbl = new GameObject("Label");
        lbl.transform.SetParent(btn.transform, false);
        RectTransform lRect = lbl.AddComponent<RectTransform>();
        lRect.anchorMin = Vector2.zero; lRect.anchorMax = Vector2.one;
        lRect.offsetMin = Vector2.zero; lRect.offsetMax = Vector2.zero;
        Text lText = lbl.AddComponent<Text>();
        lText.text = label; lText.alignment = TextAnchor.MiddleCenter;
        lText.fontSize = 22; lText.color = UIHelper.Accent; lText.font = font;
        lText.raycastTarget = false;

        return btn;
    }

    // ========== 工具方法 ==========

    private Text CreateText(Transform parent, string name, string text, Font font,
        int fontSize, Color color, Vector2 pos, Vector2 size)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1); rect.anchorMax = new Vector2(0.5f, 1);
        rect.pivot = new Vector2(0.5f, 1); rect.anchoredPosition = pos; rect.sizeDelta = size;
        Text t = obj.AddComponent<Text>();
        t.text = text; t.alignment = TextAnchor.MiddleCenter; t.fontSize = fontSize;
        t.color = color; t.font = font; t.raycastTarget = false;
        return t;
    }

    private Text CreateTextInBox(Transform parent, string name, string text, Font font,
        int fontSize, Color color, Vector2 pos, Vector2 size)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1); rect.anchorMax = new Vector2(0.5f, 1);
        rect.pivot = new Vector2(0.5f, 1); rect.anchoredPosition = pos; rect.sizeDelta = size;
        Text t = obj.AddComponent<Text>();
        t.text = text; t.alignment = TextAnchor.MiddleCenter; t.fontSize = fontSize;
        t.color = color; t.font = font; t.lineSpacing = 1.3f; t.raycastTarget = false;
        return t;
    }

    private void CreateActionButton(Transform parent, string label, Font font,
        Vector2 pos, Vector2 size, Color bgColor, Color borderColor, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btn = new GameObject(label + "Btn");
        btn.transform.SetParent(parent, false);
        RectTransform rect = btn.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1); rect.anchorMax = new Vector2(0.5f, 1);
        rect.pivot = new Vector2(0.5f, 1); rect.anchoredPosition = pos; rect.sizeDelta = size;

        GameObject border = new GameObject("Border");
        border.transform.SetParent(btn.transform, false);
        RectTransform bRect = border.AddComponent<RectTransform>();
        bRect.anchorMin = Vector2.zero; bRect.anchorMax = Vector2.one;
        bRect.offsetMin = new Vector2(-1, -1); bRect.offsetMax = new Vector2(1, 1);
        Image bImg = border.AddComponent<Image>();
        bImg.color = borderColor;
        bImg.raycastTarget = false;

        Image img = btn.AddComponent<Image>();
        img.color = bgColor;
        Button button = btn.AddComponent<Button>();
        button.targetGraphic = img;
        button.onClick.AddListener(onClick);

        GameObject lbl = new GameObject("Label");
        lbl.transform.SetParent(btn.transform, false);
        RectTransform lRect = lbl.AddComponent<RectTransform>();
        lRect.anchorMin = Vector2.zero; lRect.anchorMax = Vector2.one;
        lRect.offsetMin = Vector2.zero; lRect.offsetMax = Vector2.zero;
        Text lText = lbl.AddComponent<Text>();
        lText.text = label; lText.alignment = TextAnchor.MiddleCenter;
        lText.fontSize = 18; lText.color = UIHelper.Accent; lText.font = font;
        lText.raycastTarget = false;
    }

    /// <summary>显示"加载中"提示</summary>
    private void ShowLoadingHint()
    {
        if (_statusText != null)
        {
            _statusText.text = "加载数据中...";
            _statusText.color = UIHelper.Accent;
        }
    }

    /// <summary>异步加载角色槽位预览Sprite</summary>
    private IEnumerator LoadSlotSprite(HeroClass heroClass, Image targetImg)
    {
        if (targetImg == null) yield break;
        yield return CharacterSpriteFactory.LoadClassSpriteAsync(heroClass, sprite =>
        {
            if (targetImg != null && sprite != null)
                targetImg.sprite = sprite;
        });
    }

    private void OnDestroy()
    {
        if (_pulseCo != null) StopCoroutine(_pulseCo);
        if (panel != null) Destroy(panel);
        if (Instance == this) Instance = null;
    }
}
