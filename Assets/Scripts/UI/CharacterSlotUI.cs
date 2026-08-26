using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 角色槽位选择页面 — 显示当前账号的3个角色槽位
/// 点击卡片选中, 底部统一"开始游戏"按钮确认
/// </summary>
public class CharacterSlotUI : MonoBehaviour
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
        var summaries = new CloudSaveManager.SlotSummary[3];
        bool anyOccupied = false;
        for (int i = 0; i < 3; i++)
        {
            string prefix = $"ARPG_S{i}_";
            if (PlayerPrefs.HasKey(prefix + "Class"))
            {
                summaries[i] = new CloudSaveManager.SlotSummary
                {
                    slot = i, occupied = true,
                    classType = PlayerPrefs.GetInt(prefix + "Class", 0),
                    level = PlayerPrefs.GetInt(prefix + "Level", 1),
                    highestStageCleared = PlayerPrefs.GetInt(prefix + "HighestStage", 0),
                    characterName = PlayerPrefs.GetString(prefix + "CharacterName", ""),
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

    // ========== 卡片创建 ==========

    private GameObject CreateSlotCard(Transform parent, int slotIndex, CloudSaveManager.SlotSummary summary, bool occupied, Font font)
    {
        GameObject card = new GameObject($"Slot_{slotIndex}");
        card.transform.SetParent(parent, false);
        RectTransform cardRect = card.AddComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 1);
        cardRect.anchorMax = new Vector2(0.5f, 1);
        cardRect.pivot = new Vector2(0.5f, 1);
        cardRect.anchoredPosition = new Vector2(-310 + slotIndex * 310, -210);
        cardRect.sizeDelta = new Vector2(280, 400);
        cardRect.localScale = Vector3.zero;
        _cardRects.Add(cardRect);

        HeroClass cardClass = occupied ? (HeroClass)summary.classType : HeroClass.Warrior;
        ClassData cd = ClassData.GetClassData(cardClass);
        Color primaryColor = cd != null ? cd.PrimaryColor : new Color(0.5f, 0.5f, 0.5f);
        _cardColors.Add(primaryColor);
        _cardOccupied.Add(occupied);

        // Card glow
        GameObject glowObj = new GameObject("Glow");
        glowObj.transform.SetParent(card.transform, false);
        RectTransform glowRect = glowObj.AddComponent<RectTransform>();
        glowRect.anchorMin = Vector2.zero; glowRect.anchorMax = Vector2.one;
        glowRect.offsetMin = new Vector2(-8, -8); glowRect.offsetMax = new Vector2(8, 8);
        Image glowImg = glowObj.AddComponent<Image>();
        glowImg.color = occupied
            ? new Color(primaryColor.r, primaryColor.g, primaryColor.b, 0.15f)
            : new Color(0.2f, 0.2f, 0.25f, 0.05f);
        glowImg.raycastTarget = false;
        _cardGlows.Add(glowImg);

        // Card background
        Image cardImg = card.AddComponent<Image>();
        cardImg.color = new Color(primaryColor.r * 0.12f, primaryColor.g * 0.12f, primaryColor.b * 0.12f, 0.95f);
        _cardBgs.Add(cardImg);

        // Border
        GameObject borderObj = new GameObject("Border");
        borderObj.transform.SetParent(card.transform, false);
        RectTransform bRect = borderObj.AddComponent<RectTransform>();
        bRect.anchorMin = Vector2.zero; bRect.anchorMax = Vector2.one;
        bRect.offsetMin = new Vector2(-2, -2); bRect.offsetMax = new Vector2(2, 2);
        Image borderImg = borderObj.AddComponent<Image>();
        borderImg.color = occupied ? primaryColor * 0.6f : new Color(0.3f, 0.3f, 0.35f, 0.5f);
        borderImg.raycastTarget = false;
        _cardBorders.Add(borderImg);

        // Inner background
        GameObject innerObj = new GameObject("Inner");
        innerObj.transform.SetParent(borderObj.transform, false);
        RectTransform iRect = innerObj.AddComponent<RectTransform>();
        iRect.anchorMin = Vector2.zero; iRect.anchorMax = Vector2.one;
        iRect.offsetMin = new Vector2(2, 2); iRect.offsetMax = new Vector2(-2, -2);
        Image innerImg = innerObj.AddComponent<Image>();
        innerImg.color = new Color(0.04f, 0.03f, 0.07f, 0.98f);
        innerImg.raycastTarget = false;

        // Slot number badge
        CreateSlotBadge(card.transform, slotIndex, font, occupied ? primaryColor : new Color(0.4f, 0.4f, 0.45f));

        if (occupied)
        {
            GameObject iconObj = new GameObject("CharIcon");
            iconObj.transform.SetParent(card.transform, false);
            RectTransform iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.65f); iconRect.anchorMax = new Vector2(0.5f, 0.65f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(120, 120);
            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.color = Color.white;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            // 异步加载角色预览Sprite
            StartCoroutine(LoadSlotSprite(cardClass, iconImg));

            CreateText(card.transform, "ClassName", cd?.ClassName ?? "未知", font, 28,
                primaryColor, new Vector2(0, -210), new Vector2(260, 36));

            // Character name (from server summary, fall back to PlayerPrefs)
            string charName = !string.IsNullOrEmpty(summary?.characterName)
                ? summary.characterName
                : PlayerPrefs.GetString($"ARPG_S{slotIndex}_CharacterName", "");
            if (!string.IsNullOrEmpty(charName))
                CreateText(card.transform, "CharName", charName, font, 18,
                    new Color(0.85f, 0.85f, 0.9f), new Vector2(0, -246), new Vector2(260, 40));

            CreateText(card.transform, "Level", $"Lv.{summary.level}", font, 22,
                new Color(0.9f, 0.88f, 0.6f), new Vector2(0, -286), new Vector2(260, 40));

            string stageText = summary.highestStageCleared > 0
                ? $"最高关卡: {summary.highestStageCleared}"
                : "尚未通关";
            CreateText(card.transform, "Stage", stageText, font, 16,
                new Color(0.7f, 0.75f, 0.8f), new Vector2(0, -336), new Vector2(260, 50));
        }
        else
        {
            GameObject plusObj = new GameObject("PlusIcon");
            plusObj.transform.SetParent(card.transform, false);
            RectTransform plusRect = plusObj.AddComponent<RectTransform>();
            plusRect.anchorMin = new Vector2(0.5f, 0.55f); plusRect.anchorMax = new Vector2(0.5f, 0.55f);
            plusRect.pivot = new Vector2(0.5f, 0.5f);
            plusRect.anchoredPosition = Vector2.zero;
            plusRect.sizeDelta = new Vector2(80, 80);
            Text plusText = plusObj.AddComponent<Text>();
            plusText.text = "+";
            plusText.alignment = TextAnchor.MiddleCenter; plusText.fontSize = 60;
            plusText.color = new Color(0.3f, 0.3f, 0.35f, 0.6f); plusText.font = font;
            plusText.raycastTarget = false;

            CreateText(card.transform, "EmptyLabel", "创建角色", font, 22,
                new Color(0.4f, 0.42f, 0.45f), new Vector2(0, -120), new Vector2(260, 32));

            CreateText(card.transform, "EmptyHint", "选择职业开始\n新的冒险", font, 14,
                new Color(0.35f, 0.35f, 0.4f, 0.7f), new Vector2(0, -160), new Vector2(260, 40));
        }

        // Whole-card click → select
        int idx = slotIndex;
        Button cardBtn = card.AddComponent<Button>();
        cardBtn.targetGraphic = cardImg;
        cardBtn.onClick.AddListener(() => OnCardClicked(idx));
        AddHoverEffect(card, cardImg, glowImg, primaryColor, occupied);

        return card;
    }

    private void CreateSlotBadge(Transform parent, int slotIndex, Font font, Color color)
    {
        GameObject badgeObj = new GameObject("SlotBadge");
        badgeObj.transform.SetParent(parent, false);
        RectTransform badgeRect = badgeObj.AddComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(0, 1); badgeRect.anchorMax = new Vector2(0, 1);
        badgeRect.pivot = new Vector2(0, 1);
        badgeRect.anchoredPosition = new Vector2(10, -8);
        badgeRect.sizeDelta = new Vector2(60, 22);
        Image badgeBg = badgeObj.AddComponent<Image>();
        badgeBg.color = new Color(color.r, color.g, color.b, 0.2f);
        badgeBg.raycastTarget = false;

        GameObject badgeBorder = new GameObject("Border");
        badgeBorder.transform.SetParent(badgeObj.transform, false);
        RectTransform bbRect = badgeBorder.AddComponent<RectTransform>();
        bbRect.anchorMin = Vector2.zero; bbRect.anchorMax = Vector2.one;
        bbRect.offsetMin = new Vector2(-1, -1); bbRect.offsetMax = new Vector2(1, 1);
        Image bbImg = badgeBorder.AddComponent<Image>();
        bbImg.color = new Color(color.r, color.g, color.b, 0.5f);
        bbImg.raycastTarget = false;

        GameObject badgeInner = new GameObject("Inner");
        badgeInner.transform.SetParent(badgeBorder.transform, false);
        RectTransform biRect = badgeInner.AddComponent<RectTransform>();
        biRect.anchorMin = Vector2.zero; biRect.anchorMax = Vector2.one;
        biRect.offsetMin = Vector2.zero; biRect.offsetMax = Vector2.zero;
        Image biImg = badgeInner.AddComponent<Image>();
        biImg.color = new Color(0.06f, 0.05f, 0.10f, 0.9f);
        biImg.raycastTarget = false;

        GameObject badgeLabel = new GameObject("Label");
        badgeLabel.transform.SetParent(badgeObj.transform, false);
        RectTransform blRect = badgeLabel.AddComponent<RectTransform>();
        blRect.anchorMin = Vector2.zero; blRect.anchorMax = Vector2.one;
        blRect.offsetMin = Vector2.zero; blRect.offsetMax = Vector2.zero;
        Text blText = badgeLabel.AddComponent<Text>();
        blText.text = $"#{slotIndex + 1}";
        blText.alignment = TextAnchor.MiddleCenter; blText.fontSize = 14;
        blText.color = color; blText.font = font;
        blText.raycastTarget = false;
    }

    // ========== 选中状态 ==========

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

    // ========== 删除确认弹窗 ==========

    private void ShowDeleteConfirm(int slot, Font font)
    {
        if (_confirmDialog != null) Destroy(_confirmDialog);

        _confirmDialog = new GameObject("DeleteConfirmDialog");
        _confirmDialog.transform.SetParent(panel.transform, false);
        RectTransform dialogRect = _confirmDialog.AddComponent<RectTransform>();
        dialogRect.anchorMin = Vector2.zero; dialogRect.anchorMax = Vector2.one;
        dialogRect.offsetMin = Vector2.zero; dialogRect.offsetMax = Vector2.zero;

        Image backdrop = _confirmDialog.AddComponent<Image>();
        backdrop.color = new Color(0, 0, 0, 0.6f);

        GameObject boxObj = new GameObject("DialogBox");
        boxObj.transform.SetParent(_confirmDialog.transform, false);
        RectTransform boxRect = boxObj.AddComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0.5f, 0.5f); boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        boxRect.pivot = new Vector2(0.5f, 0.5f);
        boxRect.sizeDelta = new Vector2(400, 220);

        Image boxBg = boxObj.AddComponent<Image>();
        boxBg.color = new Color(0.08f, 0.06f, 0.10f, 0.98f);

        GameObject borderObj = new GameObject("Border");
        borderObj.transform.SetParent(boxObj.transform, false);
        RectTransform bRect = borderObj.AddComponent<RectTransform>();
        bRect.anchorMin = Vector2.zero; bRect.anchorMax = Vector2.one;
        bRect.offsetMin = new Vector2(-2, -2); bRect.offsetMax = new Vector2(2, 2);
        Image bImg = borderObj.AddComponent<Image>();
        bImg.color = new Color(0.6f, 0.15f, 0.1f, 0.8f);
        bImg.raycastTarget = false;

        GameObject innerObj = new GameObject("Inner");
        innerObj.transform.SetParent(borderObj.transform, false);
        RectTransform iRect = innerObj.AddComponent<RectTransform>();
        iRect.anchorMin = Vector2.zero; iRect.anchorMax = Vector2.one;
        iRect.offsetMin = new Vector2(2, 2); iRect.offsetMax = new Vector2(-2, -2);
        Image iImg = innerObj.AddComponent<Image>();
        iImg.color = new Color(0.08f, 0.06f, 0.10f, 1f);
        iImg.raycastTarget = false;

        CreateTextInBox(boxObj.transform, "Title", "确认删除", font, 24,
            new Color(1f, 0.5f, 0.4f), new Vector2(0, -20), new Vector2(360, 36));
        CreateTextInBox(boxObj.transform, "Warning", "该角色将被永久删除\n此操作无法撤销!", font, 18,
            new Color(0.85f, 0.8f, 0.75f), new Vector2(0, -65), new Vector2(360, 50));

        CreateActionButton(boxObj.transform, "确认删除", font,
            new Vector2(-80, -145), new Vector2(140, 40),
            new Color(0.4f, 0.08f, 0.08f, 0.95f),
            new Color(0.7f, 0.2f, 0.15f, 0.8f),
            () => { CloseConfirmDialog(); OnDeleteSlotConfirmed(slot); });

        CreateActionButton(boxObj.transform, "取消", font,
            new Vector2(80, -145), new Vector2(140, 40),
            new Color(0.15f, 0.15f, 0.20f, 0.95f),
            new Color(0.35f, 0.35f, 0.40f, 0.6f),
            () => CloseConfirmDialog());

        boxObj.transform.localScale = Vector3.zero;
        StartCoroutine(AnimateDialogEntrance(boxObj));
    }

    private void CloseConfirmDialog()
    {
        if (_confirmDialog != null) Destroy(_confirmDialog);
        _confirmDialog = null;
    }

    private void OnDeleteSlotConfirmed(int slot)
    {
        // Always clear local PlayerPrefs data for this slot
        PlayerProgressData.SetActiveSlot(slot);
        PlayerProgressData.ClearSave();

        if (CloudSaveManager.Instance != null && CloudSaveManager.Instance.IsLoggedIn)
        {
            if (_statusText != null)
            {
                _statusText.text = "正在删除角色...";
                _statusText.color = UIHelper.Accent;
            }
            CloudSaveManager.Instance.DeleteSave(slot,
                () =>
                {
                    if (_statusText != null)
                    {
                        _statusText.text = "角色已删除";
                        _statusText.color = new Color(0.3f, 1f, 0.5f);
                    }
                    _selectedSlot = -1;
                    RefreshSlots();
                },
                (err) =>
                {
                    // Server delete failed, but local data already cleared
                    if (_statusText != null)
                    {
                        _statusText.text = $"本地已删除 (服务器同步失败)";
                        _statusText.color = new Color(1f, 0.7f, 0.3f);
                    }
                    _selectedSlot = -1;
                    RefreshSlots();
                });
        }
        else
        {
            if (_statusText != null)
            {
                _statusText.text = "角色已删除";
                _statusText.color = new Color(0.3f, 1f, 0.5f);
            }
            _selectedSlot = -1;
            RefreshSlots();
        }
    }

    private IEnumerator AnimateDialogEntrance(GameObject dialog)
    {
        float duration = 0.2f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duration);
            float s = 1f - Mathf.Pow(1f - p, 3f);
            dialog.transform.localScale = Vector3.one * s;
            yield return null;
        }
        dialog.transform.localScale = Vector3.one;
    }

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
