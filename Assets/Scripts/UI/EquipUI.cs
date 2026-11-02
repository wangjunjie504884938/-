using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 背包 — 水墨暗色新版（背包页签，外壳共用 ShopBagShell）
/// 确认对话框见 EquipUI.Dialogs.cs (partial)
/// </summary>
public partial class EquipUI : MonoBehaviour
{
    public static EquipUI Instance { get; private set; }

    private GameObject panel;
    private GameObject confirmDialog;   // Dialogs partial 使用
    private Coroutine showRoutine;
    private ShopBagShell.Refs shell;

    private GameObject gridRoot;
    private Text pageText;
    private Text countText;
    private Text sortLbl, filterLbl;

    private const int CellsPerPage = 16;
    private int bpPage;
    private readonly HashSet<int> selectedBpIndices = new(); // Dialogs partial 引用
    private int sortMode = 0;   // 0=默认, 1=品质降序, 2=品质升序
    private int filterMode = 0; // 0=全部, 1=武器, 2=护甲, 3=饰品

    private static readonly string[] SlotIcons = { "武器", "防具", "饰品" };
    private static readonly string[] SlotTags = { "[武器]", "[护甲]", "[饰品]" };
    private static string SlotPrefix(EquipSlotType type) => $"<size=14><color=#aaa>{SlotIcons[(int)type]}{SlotTags[(int)type]}</color></size> ";

    private static readonly string[] SortLabels = { "排序:默认", "排序:品质↓", "排序:品质↑" };
    private static readonly string[] FilterLabels = { "筛选:全部", "筛选:武器", "筛选:护甲", "筛选:饰品" };

    // 背包网格（相对 GridRoot 中心 (20,-18)，格 104px，4 行；GridRoot=面板内缩 左30 右30 顶20 底20）
    private static readonly float[] ColX = { 48f, 163f, 278f, 393f };
    private static readonly float[] RowY = { 180f, 57f, -66f, -189f };
    private const float CellSize = 104f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Show()
    {
        if (panel == null) BuildUI();
        ShopBagShell.CloseSlotMenu();
        TopNavBar.Instance?.HideBar();
        bpPage = 0;
        panel.SetActive(true);
        if (showRoutine != null) StopCoroutine(showRoutine);
        showRoutine = StartCoroutine(UIHelper.FadeIn(panel, 0.2f));
        Canvas.ForceUpdateCanvases();
        RefreshDisplay();
    }

    public void Hide()
    {
        ShopBagShell.CloseSlotMenu();
        HideConfirmDialog();
        if (panel != null)
        {
            if (showRoutine != null) StopCoroutine(showRoutine);
            showRoutine = StartCoroutine(UIHelper.FadeOut(panel, 0.15f));
        }
    }

    private void BuildUI()
    {
        Font font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();

        // === Root Panel ===
        panel = new GameObject("EquipPanel");
        panel.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero; panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero; panelRect.offsetMax = Vector2.zero;
        panel.AddComponent<CanvasGroup>();

        shell = ShopBagShell.Build(panel, font, false,
            onClose: () =>
            {
                var st = GameManager.Instance?.Player?.Stats;
                if (st != null) st.Save();
                Hide();
                UIHelper.ReturnFromPanel();
            },
            openOtherTab: () =>
            {
                ShopBagShell.CloseSlotMenu();
                HideConfirmDialog();
                UIHelper.EnsureUIManager();
                UIManager.Instance.ShowShop();
            });
        shell.OnEquippedChanged = RefreshDisplay;

        // === 工具行（网格上方 y=246）===
        Color toolBg = new Color(0.10f, 0.07f, 0.16f, 0.92f);
        var sortBtn = MakeToolBtn(panel.transform, "BagSortBtn", SortLabels[sortMode], font,
            new Vector2(73, 235), new Vector2(80, 25), toolBg, 14, () =>
            {
                sortMode = (sortMode + 1) % 3; bpPage = 0;
                if (sortLbl != null) sortLbl.text = SortLabels[sortMode];
                RefreshDisplay();
            });
        sortLbl = sortBtn.GetComponentInChildren<Text>();

        var filterBtn = MakeToolBtn(panel.transform, "BagFilterBtn", FilterLabels[filterMode], font,
            new Vector2(153, 235), new Vector2(80, 25), toolBg, 14, () =>
            {
                filterMode = (filterMode + 1) % 4; bpPage = 0;
                if (filterLbl != null) filterLbl.text = FilterLabels[filterMode];
                RefreshDisplay();
            });
        filterLbl = filterBtn.GetComponentInChildren<Text>();

        MakeToolBtn(panel.transform, "BagDismantleCommonBtn", "分解普通", font,
            new Vector2(233, 235), new Vector2(80, 25), toolBg, 14, DismantleAllCommon);
        MakeToolBtn(panel.transform, "BagEquipBestBtn", "装备最优", font,
            new Vector2(313, 235), new Vector2(80, 25), toolBg, 14, EquipBestItems);
        MakeToolBtn(panel.transform, "BagExpandBtn", "扩展背包", font,
            new Vector2(-100, 235), new Vector2(110, 25), toolBg, 14, ExpandBackpack);

        var prevBtn = MakeToolBtn(panel.transform, "BagPrevBtn", "◀", font,
            new Vector2(420, 235), new Vector2(25, 25), toolBg, 15, () => { if (bpPage > 0) { bpPage--; RefreshDisplay(); } });
        var nextBtn = MakeToolBtn(panel.transform, "BagNextBtn", "▶", font,
            new Vector2(487, 235), new Vector2(25, 25), toolBg, 15, () => { bpPage++; RefreshDisplay(); });

        var pageObj = new GameObject("BagPageText");
        pageObj.transform.SetParent(panel.transform, false);
        var pR = pageObj.AddComponent<RectTransform>();
        pR.anchorMin = new Vector2(0.5f, 0.5f); pR.anchorMax = new Vector2(0.5f, 0.5f);
        pR.anchoredPosition = new Vector2(455, 235);
        pR.sizeDelta = new Vector2(45, 25);
        pageText = pageObj.AddComponent<Text>();
        pageText.alignment = TextAnchor.MiddleCenter;
        pageText.fontSize = 14; pageText.color = UIHelper.TextSecondary; pageText.font = font;
        pageText.raycastTarget = false;

        var cntObj = new GameObject("BagCountText");
        cntObj.transform.SetParent(panel.transform, false);
        var cR = cntObj.AddComponent<RectTransform>();
        cR.anchorMin = new Vector2(0.5f, 0.5f); cR.anchorMax = new Vector2(0.5f, 0.5f);
        cR.anchoredPosition = new Vector2(-20, 235);
        cR.sizeDelta = new Vector2(60, 25);
        countText = cntObj.AddComponent<Text>();
        countText.alignment = TextAnchor.MiddleCenter;
        countText.fontSize = 14; countText.color = UIHelper.TextDim; countText.font = font;
        countText.raycastTarget = false;

        // === 网格根（面板 1100x800 内缩 左30 右30 顶20 底20 → 1040x760，整体右移15下移5）===
        gridRoot = new GameObject("BagGridRoot");
        gridRoot.transform.SetParent(panel.transform, false);
        var gr = gridRoot.AddComponent<RectTransform>();
        gr.anchorMin = new Vector2(0.5f, 0.5f); gr.anchorMax = new Vector2(0.5f, 0.5f);
        gr.anchoredPosition = new Vector2(35, -23);
        gr.sizeDelta = new Vector2(1040, 760);

        panel.SetActive(false);
    }

    private GameObject MakeToolBtn(Transform parent, string name, string text, Font font,
        Vector2 center, Vector2 size, Color bg, int fontSize, System.Action onClick)
    {
        var go = ShopBagShell.MakeSprite(parent, name, null, center, size, raycast: true);
        go.GetComponent<Image>().color = bg;
        var b = go.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.onClick.AddListener(() => onClick());
        ShopBagShell.MakeText(go.transform, name + "Label", text, fontSize, UIHelper.TextSecondary, font, Vector2.zero, size);
        return go;
    }

    private void RefreshDisplay()
    {
        ShopBagShell.ClearChildren(gridRoot);
        Font font = GameManager.GetUIFont();
        var inv = GameManager.Instance?.Player?.Inventory;
        if (inv == null || gridRoot == null) return;

        // 过滤 + 排序
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
        int totalPages = Mathf.Max(1, Mathf.CeilToInt(totalItems / (float)CellsPerPage));
        bpPage = Mathf.Clamp(bpPage, 0, totalPages - 1);
        int startIdx = bpPage * CellsPerPage;

        for (int i = 0; i < CellsPerPage; i++)
        {
            int col = i % 4, row = i / 4;
            var center = new Vector2(ColX[col], RowY[row]);
            int abs = startIdx + i;

            if (abs < totalItems)
            {
                int origIdx = displayItems[abs].origIdx;
                var item = displayItems[abs].item;

                var cell = ShopBagShell.MakeSprite(gridRoot.transform, "BagCell_" + i,
                    ShopBagShell.FrameName(item.Rarity), center, new Vector2(CellSize, CellSize));
                cell.GetComponent<Image>().raycastTarget = true;
                var btn = cell.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                int idx = origIdx;
                btn.onClick.AddListener(() => ShowItemMenu(idx));

                string lvlTag = item.UpgradeLevel > 0 ? $" <color=#C088FF>+{item.UpgradeLevel}</color>" : "";
                var nameT = ShopBagShell.MakeText(cell.transform, "BagCellName_" + i, ShopBagShell.StripRarityPrefix(item.Name) + lvlTag, 15, Color.white, font,
                    new Vector2(0, 4), new Vector2(96, 54), TextAnchor.UpperCenter, outline: true);
                nameT.supportRichText = true;

                int cmp = CompareToEquipped(item);
                if (cmp != 0)
                    ShopBagShell.MakeSprite(cell.transform, "BagCellHint_" + i, cmp > 0 ? "UpHint" : "DownHint",
                        new Vector2(34, -34), new Vector2(30, 38));
            }
            else
            {
                var cell = ShopBagShell.MakeSprite(gridRoot.transform, "BagCell_" + i, "SlotEmpty_Bag",
                    center, new Vector2(CellSize, CellSize));
                cell.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.55f);
            }
        }

        if (pageText != null) pageText.text = $"{bpPage + 1}/{totalPages}";
        if (countText != null) countText.text = $"{inv.Backpack.Count}/{EquipmentInventory.MaxBackpackSize}";
        ShopBagShell.RefreshEquipSlots(shell);
    }

    private int CompareToEquipped(EquipmentItem item)
    {
        var inv = GameManager.Instance != null ? GameManager.Instance.Player?.Inventory : null;
        if (inv == null) return 0;
        var slot = inv.Equipped[(int)item.SlotType];
        if (slot == null || slot.IsEmpty) return 1;
        var eq = slot.Item;
        if (item.Rarity != eq.Rarity) return item.Rarity.CompareTo(eq.Rarity);
        int a = item.AttackBonus + item.DefenseBonus + item.HpBonus / 5;
        int b = eq.AttackBonus + eq.DefenseBonus + eq.HpBonus / 5;
        return a.CompareTo(b);
    }

    // ================================================================
    //  背包格点击 — 操作菜单（装备/替换 | 强化 | 取消 / 卖出 | 分解）
    // ================================================================
    private void ShowItemMenu(int origIdx)
    {
        var inv = GameManager.Instance?.Player?.Inventory;
        if (inv == null || origIdx < 0 || origIdx >= inv.Backpack.Count) return;
        var item = inv.Backpack[origIdx];
        HideConfirmDialog();

        Font font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();

        confirmDialog = CreateDialogBase(canvas, "背包操作", 380);
        var box = confirmDialog.transform.Find("DialogBox").gameObject;

        var infoObj = new GameObject("MenuItemInfo");
        infoObj.transform.SetParent(box.transform, false);
        var iRect = infoObj.AddComponent<RectTransform>();
        iRect.anchorMin = new Vector2(0.06f, 0.52f); iRect.anchorMax = new Vector2(0.94f, 0.82f);
        iRect.offsetMin = Vector2.zero; iRect.offsetMax = Vector2.zero;
        var iTxt = infoObj.AddComponent<Text>();
        string affixStr = item.GetAffixDisplayString();
        iTxt.text = $"{SlotPrefix(item.SlotType)}{ShopBagShell.StripRarityPrefix(item.Name)}{(item.UpgradeLevel > 0 ? $" <color=#8C55C0>+{item.UpgradeLevel}</color>" : "")}\n<size=14><color=#aaa>{item.GetStatSummary()}</color></size>" + (string.IsNullOrEmpty(affixStr) ? "" : $"\n<size=12><color=#88CCFF>{affixStr}</color></size>");
        iTxt.alignment = TextAnchor.MiddleCenter;
        iTxt.fontSize = 17; iTxt.color = ItemData.GetRarityColor(item.Rarity);
        iTxt.font = font; iTxt.supportRichText = true;

        var equippedSlot = inv.Equipped.Find(s => s.SlotType == item.SlotType);
        bool slotOccupied = equippedSlot != null && !equippedSlot.IsEmpty;
        bool canUpgrade = item.UpgradeLevel < EquipmentItem.MaxUpgradeLevel;

        // 行1: 装备/替换 + 强化 + 取消
        ShopBagShell.MakeMenuBtn(box.transform, "MenuEquipBtn", slotOccupied ? "替 换" : "装 备", font,
            new Vector2(-150, -78), new Vector2(130, 46),
            slotOccupied ? UIHelper.BtnReplace : UIHelper.BtnEquip, () =>
            {
                HideConfirmDialog();
                if (slotOccupied)
                {
                    ShowReplaceConfirm(origIdx);
                }
                else
                {
                    selectedBpIndices.Clear();
                    inv.Equip(inv.Backpack[origIdx]);
                    GameManager.Instance.Player.Stats.Save();
                    RefreshDisplay();
                }
            });
        ShopBagShell.MakeMenuBtn(box.transform, "MenuUpgradeBtn", "强 化", font,
            new Vector2(0, -78), new Vector2(130, 46),
            canUpgrade ? UIHelper.BtnUpgrade : new Color(0.20f, 0.20f, 0.20f, 0.6f),
            canUpgrade ? (System.Action)(() => { HideConfirmDialog(); ShowUpgradeConfirm(origIdx); }) : null);
        ShopBagShell.MakeMenuBtn(box.transform, "MenuCancelBtn", "取 消", font,
            new Vector2(150, -78), new Vector2(130, 46),
            new Color(0.10f, 0.07f, 0.14f, 0.9f), () => HideConfirmDialog());

        // 行2: 卖出 + 分解
        ShopBagShell.MakeMenuBtn(box.transform, "MenuSellBtn", $"卖出 {item.SellPrice}金", font,
            new Vector2(-75, -140), new Vector2(150, 46),
            UIHelper.BtnSell, () => { HideConfirmDialog(); ShowSellConfirm(origIdx); });
        int dYield = DismantleSystem.GetFragmentYield(item);
        int dLife = DismantleSystem.GetLifeYield(item);
        string dLabel = dLife > 0 ? $"分解 +{dYield} +{dLife}寿元" : $"分解 +{dYield}";
        ShopBagShell.MakeMenuBtn(box.transform, "MenuDismantleBtn", dLabel, font,
            new Vector2(75, -140), new Vector2(150, 46),
            new Color(0.15f, 0.35f, 0.5f, 0.9f), () => { HideConfirmDialog(); ShowDismantleConfirm(origIdx); });
    }

    // ================================================================
    //  Quick Actions: Dismantle All Common + Equip Best
    // ================================================================
    /// <summary>背包扩展: 位面币购买+10格 (上限+50) — 经济规范: 位面币消耗/仓库扩展</summary>
    private void ExpandBackpack()
    {
        int times = PlayerPrefs.GetInt("ARPG_BagExpand", 0);
        if (times >= EquipmentInventory.BagExpandMaxTimes)
        {
            if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("提示", "背包已扩展至上限 (+50格)");
            return;
        }
        if (!CurrencyWallet.TrySpendPlane(EquipmentInventory.BagExpandPrice))
        {
            if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("位面币不足", $"扩展背包需要 {EquipmentInventory.BagExpandPrice} 位面币");
            return;
        }
        PlayerPrefs.SetInt("ARPG_BagExpand", times + 1);
        PlayerPrefs.Save();
        if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("背包扩展", $"背包容量 +10 (当前 {EquipmentInventory.MaxBackpackSize} 格)");
        RefreshDisplay();
    }

    private void DismantleAllCommon()
    {
        var inv = GameManager.Instance?.Player?.Inventory;
        if (inv == null) return;

        var indices = new List<int>();
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
    //  Helper methods
    // ================================================================
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

    // ==== Confirm dialogs — moved to EquipUI.Dialogs.cs (partial class) ====
    public bool IsVisible() => panel != null && panel.activeSelf;

    private void OnDestroy()
    {
        ShopBagShell.CloseSlotMenu();
        HideConfirmDialog();
        if (panel != null) Destroy(panel);
        if (Instance == this) Instance = null;
    }
}
