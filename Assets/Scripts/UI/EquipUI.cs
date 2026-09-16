using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public partial class EquipUI : MonoBehaviour
{
    public static EquipUI Instance { get; private set; }

    private GameObject panel;
    private GameObject confirmDialog;
    private Coroutine showRoutine;

    // 暗青蓝配色

    private RectTransform leftContainer;
    private RectTransform rightContainer;

    private const int ItemsPerPage = 10;
    private int bpPage;
    private readonly HashSet<int> selectedBpIndices = new();
    private int sortMode = 0; // 0=默认, 1=品质降序, 2=品质升序
    private int filterMode = 0; // 0=全部, 1=武器, 2=护甲, 3=饰品

    private static readonly string[] SlotIcons = { "武器", "防具", "饰品" };
    private static readonly string[] SlotTags = { "[武器]", "[护甲]", "[饰品]" };

    private static string SlotPrefix(EquipSlotType type) => $"<size=14><color=#aaa>{SlotIcons[(int)type]}{SlotTags[(int)type]}</color></size> ";

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Show()
    {
        if (panel == null) BuildUI();
        UIHelper.SetupTopBar("背包", () =>
        {
            if (GameManager.Instance?.Player?.Stats != null) GameManager.Instance.Player.Stats.Save();
            Hide();
            UIHelper.EnsureUIManager();
            UIManager.Instance.ShowHub();
        });
        bpPage = 0;
        selectedBpIndices.Clear();
        panel.SetActive(true);
        if (showRoutine != null) StopCoroutine(showRoutine);
        showRoutine = StartCoroutine(UIHelper.FadeIn(panel, 0.2f));
        Canvas.ForceUpdateCanvases();
        RefreshDisplay();
    }

    public void Hide()
    {
        HideConfirmDialog();
        if (panel != null)
        {
            if (showRoutine != null) StopCoroutine(showRoutine);
            showRoutine = StartCoroutine(UIHelper.FadeOut(panel, 0.15f));
        }
    }

    private void BuildUI()
    {
        Canvas canvas = GameManager.EnsureCanvas();

        // === Root Panel ===
        panel = UiPrefabLoader.TryLoad("EquipPanel", canvas);
        if (panel == null)
        panel = new GameObject("EquipPanel");
        panel.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero; panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero; panelRect.offsetMax = Vector2.zero;
        Image bg = panel.AddComponent<Image>();
        bg.color = UIHelper.BgDark;
        bg.raycastTarget = false;

        // === Top nav bar ===

        // === Left Container (Equipped) — 38% width, between header & footer ===
        GameObject leftPanel = new GameObject("LeftContainer");
        leftPanel.transform.SetParent(panel.transform, false);
        leftContainer = leftPanel.AddComponent<RectTransform>();
        leftContainer.anchorMin = new Vector2(0, 0); leftContainer.anchorMax = new Vector2(0.38f, 1);
        leftContainer.offsetMin = new Vector2(10, 10);
        leftContainer.offsetMax = new Vector2(-5, -64);

        // === Right Container (Backpack) — 62% width, between header & footer ===
        GameObject rightPanel = new GameObject("RightContainer");
        rightPanel.transform.SetParent(panel.transform, false);
        rightContainer = rightPanel.AddComponent<RectTransform>();
        rightContainer.anchorMin = new Vector2(0.38f, 0); rightContainer.anchorMax = new Vector2(1, 1);
        rightContainer.offsetMin = new Vector2(5, 10);
        rightContainer.offsetMax = new Vector2(-10, -64);

        panel.SetActive(false);
    }

    private void RefreshDisplay()
    {
        ClearChildren(leftContainer);
        ClearChildren(rightContainer);

        Font font = GameManager.GetUIFont();
        var inv = GameManager.Instance?.Player?.Inventory;
        if (inv == null) return;

        TopNavBar.Instance?.RefreshCurrency();

        BuildEquippedSection(font, inv);
        BuildBackpackSection(font, inv);
    }

    private void ClearChildren(RectTransform container)
    {
        var list = new List<GameObject>();
        foreach (Transform child in container) list.Add(child.gameObject);
        foreach (var go in list) Destroy(go);
    }

    // ================================================================
    //  Left: Equipped Items
    // ================================================================
    private void BuildEquippedSection(Font font, EquipmentInventory inv)
    {
        float h = leftContainer.rect.height;
        if (h <= 0) h = 945f;

        float pad = 8f;
        float labelH = 28f;
        float gap = 6f;
        float maxSlotH = 130f;
        float naturalH = (h - labelH - pad * 2 - gap * 2) / 3f;
        float slotH = Mathf.Min(naturalH, maxSlotH);

        // Section label
        MakeSectionLabel(leftContainer.gameObject, "EqLabel", "已装备",
            UIHelper.Accent, font, 20, pad);

        string[] slotNames = { "武 器", "护 甲", "饰 品" };
        string[] slotIcons = { "武器", "防具", "饰品" };

        for (int i = 0; i < inv.Equipped.Count; i++)
        {
            int idx = i;
            var slot = inv.Equipped[i];
            float yOff = pad + labelH + i * (slotH + gap);

            // Slot background
            GameObject slotObj = new GameObject("EquipSlot_" + i);
            slotObj.transform.SetParent(leftContainer, false);
            RectTransform sRect = slotObj.AddComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0, 1); sRect.anchorMax = new Vector2(1, 1);
            sRect.pivot = new Vector2(0, 1);
            sRect.anchoredPosition = new Vector2(pad, -yOff);
            sRect.sizeDelta = new Vector2(-pad * 2, slotH);
            Image sImg = slotObj.AddComponent<Image>();
            sImg.color = UIHelper.CardBg;

            // Slot type header
            GameObject headerObj = new GameObject("Header");
            headerObj.transform.SetParent(slotObj.transform, false);
            RectTransform hdrRect = headerObj.AddComponent<RectTransform>();
            hdrRect.anchorMin = new Vector2(0, 0.7f); hdrRect.anchorMax = new Vector2(1, 1);
            hdrRect.offsetMin = new Vector2(10, 0); hdrRect.offsetMax = new Vector2(-10, -3);
            Text hdrText = headerObj.AddComponent<Text>();
            hdrText.text = $"{slotIcons[i]} {slotNames[i]}";
            hdrText.alignment = TextAnchor.MiddleLeft;
            hdrText.fontSize = 20; hdrText.color = new Color(0.7f, 0.7f, 0.8f); hdrText.font = font;

            if (slot.Item != null)
            {
                MakeRarityBar(slotObj, ItemData.GetRarityColor(slot.Item.Rarity));

                // Item info
                GameObject infoObj = new GameObject("ItemInfo");
                infoObj.transform.SetParent(slotObj.transform, false);
                RectTransform iRect = infoObj.AddComponent<RectTransform>();
                iRect.anchorMin = new Vector2(0, 0); iRect.anchorMax = new Vector2(0.65f, 0.7f);
                iRect.offsetMin = new Vector2(15, 5); iRect.offsetMax = new Vector2(-5, -2);
                Text iText = infoObj.AddComponent<Text>();
                string eqUpgradeTag = slot.Item.UpgradeLevel > 0 ? $" <color=#8C55C0>+{slot.Item.UpgradeLevel}</color>" : "";
                string affixStr = slot.Item.GetAffixDisplayString();
                iText.text = $"{slot.Item.Name}{eqUpgradeTag}\n<size=15>{slot.Item.GetStatSummary()}</size>" + (string.IsNullOrEmpty(affixStr) ? "" : $"\n<size=13><color=#88CCFF>{affixStr}</color></size>");
                iText.alignment = TextAnchor.MiddleLeft;
                iText.fontSize = 19; iText.color = ItemData.GetRarityColor(slot.Item.Rarity);
                iText.font = font; iText.supportRichText = true;

                // Unequip button
                GameObject ueObj = new GameObject("UnequipBtn");
                ueObj.transform.SetParent(slotObj.transform, false);
                RectTransform ueRect = ueObj.AddComponent<RectTransform>();
                ueRect.anchorMin = new Vector2(0.65f, 0.40f); ueRect.anchorMax = new Vector2(0.95f, 0.65f);
                ueRect.offsetMin = new Vector2(2, 0); ueRect.offsetMax = new Vector2(-4, 0);
                Image ueImg = ueObj.AddComponent<Image>();
                ueImg.color = UIHelper.BtnUnequip;
                Button ueBtn = ueObj.AddComponent<Button>();
                ueBtn.onClick.AddListener(() =>
                {
                    if (inv.Backpack.Count >= EquipmentInventory.MaxBackpackSize)
                    {
                        if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("提示", "背包已满，无法卸下装备");
                        return;
                    }
                    inv.Unequip(inv.Equipped[idx].SlotType);
                    GameManager.Instance.Player.Stats.Save();
                    RefreshDisplay();
                });
                MakeLabel(ueObj, "卸 下", 20, Color.white, font);

                // Upgrade button
                var item = slot.Item;
                bool canUpgrade = item.UpgradeLevel < EquipmentItem.MaxUpgradeLevel;
                GameObject upObj = new GameObject("UpgradeBtn");
                upObj.transform.SetParent(slotObj.transform, false);
                RectTransform upRect = upObj.AddComponent<RectTransform>();
                upRect.anchorMin = new Vector2(0.65f, 0.10f); upRect.anchorMax = new Vector2(0.95f, 0.35f);
                upRect.offsetMin = Vector2.zero; upRect.offsetMax = Vector2.zero;
                Image upImg = upObj.AddComponent<Image>();
                upImg.color = canUpgrade ? UIHelper.BtnUpgrade : UIHelper.CardBg;
                Button upBtn = upObj.AddComponent<Button>();
                upBtn.interactable = canUpgrade;
                upBtn.onClick.AddListener(() => TryUpgradeItem(inv.Equipped[idx].Item, inv));
                int goldCost = item.GetUpgradeCost();
                int fragCost = DismantleSystem.FragmentYield[(int)item.Rarity] / 2 + 1;
                string upLabel = canUpgrade ? $"+{item.UpgradeLevel + 1}" : "MAX";
                MakeLabel(upObj, canUpgrade ? $"升级 {upLabel}\n<color=#88CCFF>{fragCost} {goldCost}</color>" : "已满级", 14, Color.white, font);
            }
            else
            {
                // Empty slot
                GameObject emptyObj = new GameObject("Empty");
                emptyObj.transform.SetParent(slotObj.transform, false);
                RectTransform eRect = emptyObj.AddComponent<RectTransform>();
                eRect.anchorMin = new Vector2(0, 0); eRect.anchorMax = new Vector2(1, 0.7f);
                eRect.offsetMin = new Vector2(15, 5); eRect.offsetMax = new Vector2(-10, -2);
                Text eText = emptyObj.AddComponent<Text>();
                eText.text = "空"; eText.alignment = TextAnchor.MiddleLeft;
                eText.fontSize = 19; eText.color = new Color(0.35f, 0.35f, 0.4f); eText.font = font;
            }
        }

        // === Set Bonuses Display ===
        string setBonusText = SetBonusData.GetActiveBonusDisplay(inv.Equipped);
        if (!string.IsNullOrEmpty(setBonusText))
        {
            GameObject setObj = new GameObject("SetBonusDisplay");
            setObj.transform.SetParent(leftContainer, false);
            RectTransform setRect = setObj.AddComponent<RectTransform>();
            setRect.anchorMin = new Vector2(0, 0); setRect.anchorMax = new Vector2(1, 0);
            setRect.pivot = new Vector2(0.5f, 0);
            setRect.anchoredPosition = new Vector2(0, 10);
            setRect.sizeDelta = new Vector2(-20, 60);
            Text setText = setObj.AddComponent<Text>();
            setText.text = setBonusText;
            setText.alignment = TextAnchor.UpperLeft;
            setText.fontSize = 14; setText.color = Color.white;
            setText.font = font; setText.supportRichText = true;
        }
    }

    // ================================================================
    //  Right: Backpack Items + Pagination
    // ================================================================
    private void BuildBackpackSection(Font font, EquipmentInventory inv)
    {
        float h = rightContainer.rect.height;
        if (h <= 0) h = 945f;

        float pad = 8f;
        float labelH = 28f;
        float sortRowH = 32f;
        float pageRowH = 36f;
        float itemGap = 3f;
        float itemAreaH = h - labelH - sortRowH - pageRowH - pad * 3;
        float itemH = (itemAreaH - (ItemsPerPage - 1) * itemGap) / ItemsPerPage;

        // Section label
        GameObject bpLabel = new GameObject("BpLabel");
        bpLabel.transform.SetParent(rightContainer, false);
        RectTransform bpLRect = bpLabel.AddComponent<RectTransform>();
        bpLRect.anchorMin = new Vector2(0, 1); bpLRect.anchorMax = new Vector2(1, 1);
        bpLRect.pivot = new Vector2(0.5f, 1);
        bpLRect.anchoredPosition = new Vector2(0, -pad);
        bpLRect.sizeDelta = new Vector2(0, labelH);
        Text bpLText = bpLabel.AddComponent<Text>();
        string sortLabel = sortMode switch { 1 => "品质↓", 2 => "品质↑", _ => "默认" };
        string filterLabel = filterMode switch { 1 => "武器", 2 => "护甲", 3 => "饰品", _ => "全部" };
        bpLText.text = $"背 包 ({inv.Backpack.Count}/{EquipmentInventory.MaxBackpackSize}) | 排序:{sortLabel} 筛选:{filterLabel}";
        bpLText.alignment = TextAnchor.MiddleCenter;
        bpLText.fontSize = 18; bpLText.color = Color.white; bpLText.font = font;

        // 排序/筛选按钮行
        var btnRow = new GameObject("SortFilterRow");
        btnRow.transform.SetParent(rightContainer, false);
        var btnRowRT = btnRow.AddComponent<RectTransform>();
        btnRowRT.anchorMin = new Vector2(0, 1); btnRowRT.anchorMax = new Vector2(1, 1);
        btnRowRT.pivot = new Vector2(0.5f, 1);
        btnRowRT.anchoredPosition = new Vector2(0, -pad - labelH);
        btnRowRT.sizeDelta = new Vector2(0, 28);

        // 排序按钮
        var sortBtnObj = new GameObject("SortBtn");
        sortBtnObj.transform.SetParent(btnRow.transform, false);
        var sortRT = sortBtnObj.AddComponent<RectTransform>();
        sortRT.anchorMin = new Vector2(0.02f, 0); sortRT.anchorMax = new Vector2(0.25f, 1);
        sortRT.offsetMin = Vector2.zero; sortRT.offsetMax = Vector2.zero;
        var sortImg = sortBtnObj.AddComponent<Image>();
        sortImg.color = new Color(0.12f, 0.10f, 0.18f, 0.9f);
        var sortBtn = sortBtnObj.AddComponent<Button>();
        sortBtn.onClick.AddListener(() => { sortMode = (sortMode + 1) % 3; bpPage = 0; selectedBpIndices.Clear(); RefreshDisplay(); });
        MakeLabel(sortBtnObj, "排序", 14, Color.white, font);

        // 筛选按钮
        var filterBtnObj = new GameObject("FilterBtn");
        filterBtnObj.transform.SetParent(btnRow.transform, false);
        var filterRT = filterBtnObj.AddComponent<RectTransform>();
        filterRT.anchorMin = new Vector2(0.27f, 0); filterRT.anchorMax = new Vector2(0.50f, 1);
        filterRT.offsetMin = Vector2.zero; filterRT.offsetMax = Vector2.zero;
        var filterImg = filterBtnObj.AddComponent<Image>();
        filterImg.color = new Color(0.10f, 0.12f, 0.16f, 0.9f);
        var filterBtn = filterBtnObj.AddComponent<Button>();
        filterBtn.onClick.AddListener(() => { filterMode = (filterMode + 1) % 4; bpPage = 0; selectedBpIndices.Clear(); RefreshDisplay(); });
        MakeLabel(filterBtnObj, "筛选", 14, Color.white, font);

        // 一键分解普通
        var dismantleBtnObj = new GameObject("DismantleCommonBtn");
        dismantleBtnObj.transform.SetParent(btnRow.transform, false);
        var dismantleRT = dismantleBtnObj.AddComponent<RectTransform>();
        dismantleRT.anchorMin = new Vector2(0.52f, 0); dismantleRT.anchorMax = new Vector2(0.75f, 1);
        dismantleRT.offsetMin = Vector2.zero; dismantleRT.offsetMax = Vector2.zero;
        var dismantleImg = dismantleBtnObj.AddComponent<Image>();
        dismantleImg.color = new Color(0.18f, 0.12f, 0.06f, 0.9f);
        var dismantleBtn = dismantleBtnObj.AddComponent<Button>();
        dismantleBtn.onClick.AddListener(DismantleAllCommon);
        MakeLabel(dismantleBtnObj, "分解普通", 13, Color.white, font);

        // 一键装备最优
        var bestBtnObj = new GameObject("EquipBestBtn");
        bestBtnObj.transform.SetParent(btnRow.transform, false);
        var bestRT = bestBtnObj.AddComponent<RectTransform>();
        bestRT.anchorMin = new Vector2(0.77f, 0); bestRT.anchorMax = new Vector2(1f, 1);
        bestRT.offsetMin = Vector2.zero; bestRT.offsetMax = Vector2.zero;
        var bestImg = bestBtnObj.AddComponent<Image>();
        bestImg.color = new Color(0.06f, 0.14f, 0.10f, 0.9f);
        var bestBtn = bestBtnObj.AddComponent<Button>();
        bestBtn.onClick.AddListener(EquipBestItems);
        MakeLabel(bestBtnObj, "装备最优", 13, Color.white, font);

        // Build filtered+sorted item list
        var displayItems = new List<(int origIdx, EquipmentItem item)>();
        for (int i = 0; i < inv.Backpack.Count; i++)
        {
            var it = inv.Backpack[i];
            if (filterMode > 0 && (int)it.SlotType != filterMode - 1) continue;
            displayItems.Add((i, it));
        }
        if (sortMode == 1) displayItems.Sort((a, b) => b.item.Rarity.CompareTo(a.item.Rarity));
        else if (sortMode == 2) displayItems.Sort((a, b) => a.item.Rarity.CompareTo(b.item.Rarity));

        int totalItems = displayItems.Count;
        int totalPages = Mathf.Max(1, Mathf.CeilToInt(totalItems / (float)ItemsPerPage));
        bpPage = Mathf.Clamp(bpPage, 0, totalPages - 1);

        // Item rows
        int startIdx = bpPage * ItemsPerPage;
        int endIdx = Mathf.Min(startIdx + ItemsPerPage, totalItems);

        for (int i = startIdx; i < endIdx; i++)
        {
            int origIdx = displayItems[i].origIdx;
            var item = displayItems[i].item;
            int idx = origIdx;
            float yOff = pad + labelH + sortRowH + (i - startIdx) * (itemH + itemGap);

            GameObject bpObj = new GameObject("Backpack_" + i);
            bpObj.transform.SetParent(rightContainer, false);
            RectTransform bRect = bpObj.AddComponent<RectTransform>();
            bRect.anchorMin = new Vector2(0, 1); bRect.anchorMax = new Vector2(1, 1);
            bRect.pivot = new Vector2(0, 1);
            bRect.anchoredPosition = new Vector2(pad, -yOff);
            bRect.sizeDelta = new Vector2(-pad * 2, itemH);
            Image bImg = bpObj.AddComponent<Image>();
            bImg.color = UIHelper.CardBg;

            MakeRarityBar(bpObj, ItemData.GetRarityColor(item.Rarity));

            // 多选复选框 — 使用原始背包索引idx保持一致性
            bool isSelected = selectedBpIndices.Contains(idx);
            GameObject chkObj = new GameObject("CheckBox");
            chkObj.transform.SetParent(bpObj.transform, false);
            RectTransform chkRect = chkObj.AddComponent<RectTransform>();
            chkRect.anchorMin = new Vector2(0, 0.08f); chkRect.anchorMax = new Vector2(0.05f, 0.92f);
            chkRect.offsetMin = new Vector2(2, 0); chkRect.offsetMax = new Vector2(-2, 0);
            Image chkImg = chkObj.AddComponent<Image>();
            chkImg.color = isSelected ? new Color(0.2f, 0.5f, 0.2f, 0.9f) : new Color(0.1f, 0.1f, 0.12f, 0.7f);
            Button chkBtn = chkObj.AddComponent<Button>();
            chkBtn.onClick.AddListener(() => { if (selectedBpIndices.Contains(idx)) selectedBpIndices.Remove(idx); else selectedBpIndices.Add(idx); RefreshDisplay(); });
            MakeLabel(chkObj, isSelected ? "✓" : "", 16, Color.white, font);

            // Item info
            GameObject infoObj = new GameObject("Info");
            infoObj.transform.SetParent(bpObj.transform, false);
            RectTransform iRect = infoObj.AddComponent<RectTransform>();
            iRect.anchorMin = new Vector2(0.05f, 0); iRect.anchorMax = new Vector2(0.36f, 1);
            iRect.offsetMin = new Vector2(14, 3); iRect.offsetMax = new Vector2(-5, -3);
            Text iText = infoObj.AddComponent<Text>();
            string upgradeTag = item.UpgradeLevel > 0 ? $" <size=13><color=#8C55C0>+{item.UpgradeLevel}</color></size>" : "";
            string affixStr = item.GetAffixDisplayString();
            iText.text = $"{SlotPrefix(item.SlotType)}{item.Name}{upgradeTag}\n<size=14><color=#aaa>{item.GetStatSummary()}</color></size>" + (string.IsNullOrEmpty(affixStr) ? "" : $"\n<size=12><color=#88CCFF>{affixStr}</color></size>");
            iText.alignment = TextAnchor.MiddleLeft; iText.fontSize = 16;
            iText.color = ItemData.GetRarityColor(item.Rarity);
            iText.font = font; iText.supportRichText = true;

            // Slot occupied check
            var equippedSlot = inv.Equipped.Find(s => s.SlotType == item.SlotType);
            bool slotOccupied = equippedSlot != null && !equippedSlot.IsEmpty;

            // Upgrade button
            bool canUpgrade = item.UpgradeLevel < EquipmentItem.MaxUpgradeLevel;
            GameObject upgObj = new GameObject("UpgradeBtn");
            upgObj.transform.SetParent(bpObj.transform, false);
            RectTransform upgRect = upgObj.AddComponent<RectTransform>();
            upgRect.anchorMin = new Vector2(0.36f, 0.08f); upgRect.anchorMax = new Vector2(0.48f, 0.92f);
            upgRect.offsetMin = new Vector2(2, 0); upgRect.offsetMax = new Vector2(-2, 0);
            Image upgImg = upgObj.AddComponent<Image>();
            upgImg.color = canUpgrade ? UIHelper.BtnUpgrade : new Color(0.10f, 0.06f, 0.14f, 0.5f);
            Button upgBtn = upgObj.AddComponent<Button>();
            upgBtn.interactable = canUpgrade;
            if (canUpgrade)
                upgBtn.onClick.AddListener(() => ShowUpgradeConfirm(idx));
            MakeLabel(upgObj, canUpgrade ? $"强化{item.UpgradeLevel}→{item.UpgradeLevel + 1}" : "满级", 12, Color.white, font);

            // Equip / Replace button
            GameObject eqObj = new GameObject("EquipBtn");
            eqObj.transform.SetParent(bpObj.transform, false);
            RectTransform eqRect = eqObj.AddComponent<RectTransform>();
            eqRect.anchorMin = new Vector2(0.48f, 0.08f); eqRect.anchorMax = new Vector2(0.62f, 0.92f);
            eqRect.offsetMin = new Vector2(2, 0); eqRect.offsetMax = new Vector2(-2, 0);
            Image eqImg = eqObj.AddComponent<Image>();
            eqImg.color = slotOccupied ? UIHelper.BtnReplace : UIHelper.BtnEquip;
            Button eqBtn = eqObj.AddComponent<Button>();
            if (slotOccupied)
                eqBtn.onClick.AddListener(() => ShowReplaceConfirm(idx));
            else
                eqBtn.onClick.AddListener(() => { selectedBpIndices.Clear(); inv.Equip(inv.Backpack[idx]); GameManager.Instance.Player.Stats.Save(); RefreshDisplay(); });
            MakeLabel(eqObj, slotOccupied ? "替换" : "装备", 13, Color.white, font);

            // Sell button
            GameObject sellObj = new GameObject("SellBtn");
            sellObj.transform.SetParent(bpObj.transform, false);
            RectTransform sellRect = sellObj.AddComponent<RectTransform>();
            sellRect.anchorMin = new Vector2(0.62f, 0.08f); sellRect.anchorMax = new Vector2(0.74f, 0.92f);
            sellRect.offsetMin = new Vector2(2, 0); sellRect.offsetMax = new Vector2(-2, 0);
            Image sellImg = sellObj.AddComponent<Image>();
            sellImg.color = UIHelper.BtnSell;
            Button sellBtn = sellObj.AddComponent<Button>();
            sellBtn.onClick.AddListener(() => ShowSellConfirm(idx));
            MakeLabel(sellObj, "卖出", 13, Color.white, font);

            // Dismantle button
            int dismantleYield = DismantleSystem.GetFragmentYield(item);
            GameObject dmdObj = new GameObject("DismantleBtn");
            dmdObj.transform.SetParent(bpObj.transform, false);
            RectTransform dmdRect = dmdObj.AddComponent<RectTransform>();
            dmdRect.anchorMin = new Vector2(0.74f, 0.08f); dmdRect.anchorMax = new Vector2(0.86f, 0.92f);
            dmdRect.offsetMin = new Vector2(2, 0); dmdRect.offsetMax = new Vector2(-2, 0);
            Image dmdImg = dmdObj.AddComponent<Image>();
            dmdImg.color = new Color(0.15f, 0.35f, 0.5f, 0.9f);
            Button dmdBtn = dmdObj.AddComponent<Button>();
            dmdBtn.onClick.AddListener(() => ShowDismantleConfirm(idx));
            MakeLabel(dmdObj, $"分解+{dismantleYield}", 11, new Color(0.6f, 0.85f, 1f), font);

            // Price label
            GameObject priceObj = new GameObject("PriceLabel");
            priceObj.transform.SetParent(bpObj.transform, false);
            RectTransform priceRect = priceObj.AddComponent<RectTransform>();
            priceRect.anchorMin = new Vector2(0.86f, 0.08f); priceRect.anchorMax = new Vector2(1f, 0.92f);
            priceRect.offsetMin = new Vector2(2, 0); priceRect.offsetMax = new Vector2(-6, 0);
            Text priceText = priceObj.AddComponent<Text>();
            priceText.text = $"{item.SellPrice}金"; priceText.alignment = TextAnchor.MiddleCenter;
            priceText.fontSize = 14; priceText.color = UIHelper.Accent; priceText.font = font;
        }

        // 批量出售 + Pagination — 合并到底部行
        BuildPaginationWithBulkSell(rightContainer.gameObject, font, totalPages, inv);

    }

    private void BuildPaginationWithBulkSell(GameObject parent, Font font, int totalPages, EquipmentInventory inv)
    {
        float pad = 8f;
        float pageRowH = 36f;

        GameObject pageRow = new GameObject("PageRow");
        pageRow.transform.SetParent(parent.transform, false);
        RectTransform prRect = pageRow.AddComponent<RectTransform>();
        prRect.anchorMin = new Vector2(0, 0); prRect.anchorMax = new Vector2(1, 0);
        prRect.pivot = new Vector2(0, 0);
        prRect.anchoredPosition = new Vector2(pad, pad);
        prRect.sizeDelta = new Vector2(-pad * 2, pageRowH);

        // Prev page
        GameObject prevObj = new GameObject("PrevPageBtn");
        prevObj.transform.SetParent(pageRow.transform, false);
        RectTransform prevRect = prevObj.AddComponent<RectTransform>();
        prevRect.anchorMin = new Vector2(0, 0); prevRect.anchorMax = new Vector2(0.15f, 1);
        prevRect.offsetMin = new Vector2(0, 3); prevRect.offsetMax = new Vector2(-3, -3);
        Image prevImg = prevObj.AddComponent<Image>();
        prevImg.color = bpPage > 0 ? new Color(0.14f, 0.08f, 0.20f, 0.9f) : new Color(0.10f, 0.06f, 0.14f, 0.5f);
        Button prevBtn = prevObj.AddComponent<Button>();
        prevBtn.interactable = bpPage > 0;
        prevBtn.onClick.AddListener(() => { bpPage--; RefreshDisplay(); });
        MakeLabel(prevObj, "◀", 17, Color.white, font);

        // Page text
        GameObject ptObj = new GameObject("PageText");
        ptObj.transform.SetParent(pageRow.transform, false);
        RectTransform ptRect = ptObj.AddComponent<RectTransform>();
        ptRect.anchorMin = new Vector2(0.15f, 0); ptRect.anchorMax = new Vector2(0.35f, 1);
        ptRect.offsetMin = new Vector2(3, 3); ptRect.offsetMax = new Vector2(-3, -3);
        Text ptText = ptObj.AddComponent<Text>();
        ptText.text = $"{bpPage + 1}/{totalPages}";
        ptText.alignment = TextAnchor.MiddleCenter;
        ptText.fontSize = 17; ptText.color = new Color(0.8f, 0.8f, 0.85f); ptText.font = font;

        // 批量出售按钮
        int totalSellPrice = 0;
        foreach (var selIdx in selectedBpIndices)
        {
            if (selIdx >= 0 && selIdx < inv.Backpack.Count)
                totalSellPrice += inv.Backpack[selIdx].SellPrice;
        }
        GameObject bulkSellObj = new GameObject("BulkSellBtn");
        bulkSellObj.transform.SetParent(pageRow.transform, false);
        RectTransform bsRect = bulkSellObj.AddComponent<RectTransform>();
        bsRect.anchorMin = new Vector2(0.36f, 0); bsRect.anchorMax = new Vector2(0.66f, 1);
        bsRect.offsetMin = new Vector2(3, 3); bsRect.offsetMax = new Vector2(-3, -3);
        Image bsImg = bulkSellObj.AddComponent<Image>();
        bsImg.color = selectedBpIndices.Count > 0 ? UIHelper.BtnSell : new Color(0.1f, 0.06f, 0.14f, 0.5f);
        Button bsBtn = bulkSellObj.AddComponent<Button>();
        bsBtn.interactable = selectedBpIndices.Count > 0;
        bsBtn.onClick.AddListener(BulkSellSelected);
        MakeLabel(bulkSellObj, selectedBpIndices.Count > 0 ? $"出售({selectedBpIndices.Count})◆{totalSellPrice}" : "未选中", 13, Color.white, font);

        // Bulk Dismantle button
        int totalFragments = 0;
        foreach (var selIdx in selectedBpIndices)
        {
            if (selIdx >= 0 && selIdx < inv.Backpack.Count)
                totalFragments += DismantleSystem.GetFragmentYield(inv.Backpack[selIdx]);
        }
        GameObject bulkDmdObj = new GameObject("BulkDismantleBtn");
        bulkDmdObj.transform.SetParent(pageRow.transform, false);
        RectTransform bdRect = bulkDmdObj.AddComponent<RectTransform>();
        bdRect.anchorMin = new Vector2(0.67f, 0); bdRect.anchorMax = new Vector2(0.84f, 1);
        bdRect.offsetMin = new Vector2(3, 3); bdRect.offsetMax = new Vector2(-3, -3);
        Image bdImg = bulkDmdObj.AddComponent<Image>();
        bdImg.color = selectedBpIndices.Count > 0 ? new Color(0.15f, 0.35f, 0.5f, 0.9f) : new Color(0.1f, 0.06f, 0.14f, 0.5f);
        Button bdBtn = bulkDmdObj.AddComponent<Button>();
        bdBtn.interactable = selectedBpIndices.Count > 0;
        bdBtn.onClick.AddListener(BulkDismantleSelected);
        MakeLabel(bulkDmdObj, selectedBpIndices.Count > 0 ? $"分解({selectedBpIndices.Count})◆{totalFragments}" : "未选中", 13, new Color(0.6f, 0.85f, 1f), font);

        // Next page
        GameObject nextObj = new GameObject("NextPageBtn");
        nextObj.transform.SetParent(pageRow.transform, false);
        RectTransform nextRect = nextObj.AddComponent<RectTransform>();
        nextRect.anchorMin = new Vector2(0.85f, 0); nextRect.anchorMax = new Vector2(1f, 1);
        nextRect.offsetMin = new Vector2(3, 3); nextRect.offsetMax = new Vector2(0, -3);
        Image nextImg = nextObj.AddComponent<Image>();
        nextImg.color = bpPage < totalPages - 1 ? new Color(0.14f, 0.08f, 0.20f, 0.9f) : new Color(0.10f, 0.06f, 0.14f, 0.5f);
        Button nextBtn = nextObj.AddComponent<Button>();
        nextBtn.interactable = bpPage < totalPages - 1;
        nextBtn.onClick.AddListener(() => { bpPage++; RefreshDisplay(); });
        MakeLabel(nextObj, "▶", 17, Color.white, font);
    }

    // ================================================================
    //  Helper methods
    // ================================================================
    private void MakeSectionLabel(GameObject parent, string name, string text, Color color, Font font, int fontSize, float topPad)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent.transform, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(0.5f, 1);
        rect.anchoredPosition = new Vector2(0, -topPad);
        rect.sizeDelta = new Vector2(0, 28);
        Text t = obj.AddComponent<Text>();
        t.text = text; t.alignment = TextAnchor.MiddleCenter;
        t.fontSize = fontSize; t.color = color; t.font = font;
    }

    private void MakeRarityBar(GameObject parent, Color color)
    {
        GameObject obj = new GameObject("RarityBar");
        obj.transform.SetParent(parent.transform, false);
        RectTransform r = obj.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(0, 0); r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0, 0.5f);
        r.sizeDelta = new Vector2(4, 0);
        Image img = obj.AddComponent<Image>();
        img.color = color;
    }

    private Text MakeLabel(GameObject parent, string text, int fontSize, Color color, Font font)
    {
        GameObject label = new GameObject("L");
        label.transform.SetParent(parent.transform, false);
        RectTransform rect = label.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        Text t = label.AddComponent<Text>();
        t.text = text; t.alignment = TextAnchor.MiddleCenter;
        t.fontSize = fontSize; t.color = color; t.font = font;
        t.supportRichText = true;
        return t;
    }

    // ================================================================
    //  Equipment Upgrade
    // ================================================================
    private void TryUpgradeItem(EquipmentItem item, EquipmentInventory inv)
    {
        if (item == null || item.UpgradeLevel >= EquipmentItem.MaxUpgradeLevel) return;

        int goldCost = item.GetUpgradeCost();
        int fragCost = DismantleSystem.FragmentYield[(int)item.Rarity] / 2 + 1;

        var player = GameManager.Instance?.Player;
        if (player == null) return;

        if (player.Stats.Gold < goldCost)
        {
            if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("提示", "金币不足");
            return;
        }
        if (DismantleSystem.Fragments < fragCost)
        {
            if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("提示", $"碎片不足, 需要{fragCost}个");
            return;
        }

        // Consume resources
        player.Stats.SpendGold(goldCost);
        DismantleSystem.Fragments -= fragCost;

        // Upgrade item
        item.Upgrade();
        inv.RecalculateStats();
        player.Stats.Save();

        if (GameUI.Instance != null)
            GameUI.Instance.ShowItemPickupToast("升级成功", $"{item.Name} +{item.UpgradeLevel}");

        RefreshDisplay();
    }

    // ================================================================
    //  Quick Actions: Dismantle All Common + Equip Best
    // ================================================================
    private void DismantleAllCommon()
    {
        var inv = GameManager.Instance?.Player?.Inventory;
        if (inv == null) return;

        var indices = new System.Collections.Generic.List<int>();
        for (int i = inv.Backpack.Count - 1; i >= 0; i--)
        {
            if (inv.Backpack[i].Rarity == ItemRarity.Common)
                indices.Add(i);
        }

        if (indices.Count == 0)
        {
            if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("提示", "没有普通装备可分解");
            return;
        }

        int totalFrag = DismantleSystem.DismantleBatch(inv, indices);
        if (GameManager.Instance?.Player != null)
            GameManager.Instance.Player.Stats.Save();

        if (GameUI.Instance != null)
            GameUI.Instance.ShowItemPickupToast("批量分解", $"分解{indices.Count}件普通装备\n获得{totalFrag}碎片");

        RefreshDisplay();
    }

    private void EquipBestItems()
    {
        var inv = GameManager.Instance?.Player?.Inventory;
        if (inv == null) return;

        // Find best item per slot type (highest rarity, then highest total stat bonus)
        EquipmentItem bestWeapon = null, bestArmor = null, bestAccessory = null;

        for (int i = inv.Backpack.Count - 1; i >= 0; i--)
        {
            var item = inv.Backpack[i];
            switch (item.SlotType)
            {
                case EquipSlotType.Weapon:
                    if (bestWeapon == null || CompareItems(item, bestWeapon) > 0) bestWeapon = item;
                    break;
                case EquipSlotType.Armor:
                    if (bestArmor == null || CompareItems(item, bestArmor) > 0) bestArmor = item;
                    break;
                case EquipSlotType.Accessory:
                    if (bestAccessory == null || CompareItems(item, bestAccessory) > 0) bestAccessory = item;
                    break;
            }
        }

        int equipped = 0;
        if (bestWeapon != null && IsBetterThanEquipped(inv, bestWeapon)) { inv.Equip(bestWeapon); equipped++; }
        if (bestArmor != null && IsBetterThanEquipped(inv, bestArmor)) { inv.Equip(bestArmor); equipped++; }
        if (bestAccessory != null && IsBetterThanEquipped(inv, bestAccessory)) { inv.Equip(bestAccessory); equipped++; }

        if (GameManager.Instance?.Player != null)
        {
            GameManager.Instance.Player.Stats.Save();
            inv.RecalculateStats();
        }

        if (GameUI.Instance != null)
            GameUI.Instance.ShowItemPickupToast("装备最优", equipped > 0 ? $"已装备{equipped}件更优装备" : "已是最优装备");

        RefreshDisplay();
    }

    private static int CompareItems(EquipmentItem a, EquipmentItem b)
    {
        if (a.Rarity != b.Rarity) return a.Rarity.CompareTo(b.Rarity);
        int aStat = a.AttackBonus + a.DefenseBonus + a.HpBonus / 5;
        int bStat = b.AttackBonus + b.DefenseBonus + b.HpBonus / 5;
        return aStat.CompareTo(bStat);
    }

    private static bool IsBetterThanEquipped(EquipmentInventory inv, EquipmentItem candidate)
    {
        var slot = inv.Equipped[(int)candidate.SlotType];
        if (slot == null || slot.IsEmpty) return true;
        return CompareItems(candidate, slot.Item) > 0;
    }

    // ================================================================
    //  Bulk Sell
    // ================================================================
    private void BulkSellSelected()
    {
        var inv = GameManager.Instance?.Player?.Inventory;
        if (inv == null || selectedBpIndices.Count == 0) return;

        int totalGold = 0;
        int sold = 0;
        // Sort descending so removal doesn't shift indices
        var sorted = new List<int>(selectedBpIndices);
        sorted.Sort((a, b) => b.CompareTo(a));
        foreach (var idx in sorted)
        {
            if (idx < 0 || idx >= inv.Backpack.Count) continue;
            totalGold += inv.Backpack[idx].SellPrice;
            inv.Backpack.RemoveAt(idx);
            sold++;
        }

        if (sold > 0 && GameManager.Instance?.Player != null)
        {
            GameManager.Instance.Player.Stats.AddGold(totalGold);
            GameManager.Instance.Player.Stats.Save();
            inv.RecalculateStats();
                        GameLog.Log($"[EquipUI] 批量出售 {sold} 件装备, 获得 {totalGold} 金币");
        }

        selectedBpIndices.Clear();
        RefreshDisplay();
    }

    // ================================================================
    //  Bulk Dismantle
    // ================================================================
    private void BulkDismantleSelected()
    {
        var inv = GameManager.Instance?.Player?.Inventory;
        if (inv == null || selectedBpIndices.Count == 0) return;

        int totalFragments = DismantleSystem.DismantleBatch(inv, new List<int>(selectedBpIndices));
        if (totalFragments > 0 && GameManager.Instance?.Player != null)
        {
            GameManager.Instance.Player.Stats.Save();
            if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("批量分解", $"获得 {totalFragments} 分解碎片");
        }
        selectedBpIndices.Clear();
        RefreshDisplay();
    }

    // ==== Confirm dialogs — moved to EquipUI.Dialogs.cs (partial class) ====
    public bool IsVisible() => panel != null && panel.activeSelf;

    private void OnDestroy()
    {
        HideConfirmDialog();
        if (panel != null) Destroy(panel);
        if (Instance == this) Instance = null;
    }
}
