using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 装备商店 — 水墨暗色新版（商城页签，外壳共用 ShopBagShell）
/// </summary>
public class ShopUI : MonoBehaviour
{
    public static ShopUI Instance { get; private set; }

    private GameObject panel;
    private Coroutine showRoutine;
    private GameObject confirmDialog;
    private int pendingBuyIndex = -1;
    private ShopBagShell.Refs shell;
    private readonly List<EquipmentItem> shopPool = new List<EquipmentItem>();

    private int currentMode = 0; // 0=装备 1=符文
    private GameObject gridRoot;
    private GameObject buyBtn;
    private GameObject modeEquipLine, modeRuneLine;
    private GameObject prevBtn, nextBtn, pageTextObj;
    private Text pageText;

    private int selectedCell = -1;
    private readonly List<int> cellItemIndex = new List<int>();
    private readonly List<GameObject> cellGos = new List<GameObject>();

    private const int CellsPerPage = 16;
    private const int ShopPoolSize = 40;
    private int currentPage = 0;
    private int shopPageCount = 1;

    // 商品网格（相对 GridRoot 中心 (35,-23)，格 90px；GridRoot=面板内缩 左30 右30）
    private static readonly float[] ColX = { 48f, 163f, 278f, 393f };
    private static readonly float[] RowY = { 200f, 77f, -46f, -169f };
    private const float CellSize = 90f;

    private static readonly string[] SlotTags = { "武器", "防具", "饰品" };
    private static string SlotLabel(EquipSlotType type) => SlotTags[(int)type];

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
        if (shopPool.Count == 0) GenerateShopPool();
        currentMode = 0;
        RefreshShop();
        RefreshModeVisibility();
        ShopBagShell.RefreshEquipSlots(shell);
        panel.SetActive(true);
        if (showRoutine != null) StopCoroutine(showRoutine);
        showRoutine = StartCoroutine(UIHelper.FadeIn(panel, 0.2f));
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

        panel = new GameObject("ShopPanel");
        panel.transform.SetParent(canvas.transform, false);
        RectTransform pr = panel.AddComponent<RectTransform>();
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one;
        pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;
        panel.AddComponent<CanvasGroup>();

        shell = ShopBagShell.Build(panel, font, true,
            onClose: () => { Hide(); UIHelper.ReturnFromPanel(); },
            openOtherTab: () =>
            {
                ShopBagShell.CloseSlotMenu();
                HideConfirmDialog();
                UIHelper.EnsureUIManager();
                UIManager.Instance.ShowEquip();
            });
        shell.OnEquippedChanged = RefreshShop;

        // 装备/符文 子页签
        var modeEquip = ShopBagShell.MakeMenuBtn(panel.transform, "ShopModeEquipBtn", "装备", font,
            new Vector2(65, 240), new Vector2(55, 25), new Color(0, 0, 0, 0), null, 18);
        modeEquip.GetComponent<Image>().raycastTarget = true;
        modeEquip.GetComponent<Button>().onClick.AddListener(() => { if (currentMode != 0) { currentMode = 0; RefreshModeVisibility(); } });
        var lineE = ShopBagShell.MakeSprite(modeEquip.transform, "ShopModeEquipLine", null, new Vector2(0, -10), new Vector2(40, 3));
        lineE.GetComponent<Image>().color = UIHelper.Accent;

        var modeRune = ShopBagShell.MakeMenuBtn(panel.transform, "ShopModeRuneBtn", "符文", font,
            new Vector2(120, 240), new Vector2(55, 25), new Color(0, 0, 0, 0), null, 18);
        modeRune.GetComponent<Image>().raycastTarget = true;
        modeRune.GetComponent<Button>().onClick.AddListener(() => { if (currentMode != 1) { currentMode = 1; RefreshModeVisibility(); } });
        var lineR = ShopBagShell.MakeSprite(modeRune.transform, "ShopModeRuneLine", null, new Vector2(0, -10), new Vector2(40, 3));
        lineR.GetComponent<Image>().color = UIHelper.Accent;
        modeEquipLine = lineE;
        modeRuneLine = lineR;

        // 商品网格根（面板 1100x800 内缩 左30 右30 → 1040x800，整体右移15下移5）
        gridRoot = new GameObject("ShopGridRoot");
        gridRoot.transform.SetParent(panel.transform, false);
        var gr = gridRoot.AddComponent<RectTransform>();
        gr.anchorMin = new Vector2(0.5f, 0.5f); gr.anchorMax = new Vector2(0.5f, 0.5f);
        gr.anchoredPosition = new Vector2(35, -23);
        gr.sizeDelta = new Vector2(1040, 800);

        // 购买按钮
        buyBtn = ShopBagShell.MakeSprite(panel.transform, "ShopBuyBtn", "BuyBtn", new Vector2(244, -294), new Vector2(167, 55), raycast: true);
        var buyBt = buyBtn.AddComponent<Button>();
        buyBt.transition = Selectable.Transition.None;
        buyBt.onClick.AddListener(OnBuyClicked);

        // 分页（装备模式）
        prevBtn = ShopBagShell.MakeSprite(panel.transform, "ShopPrevBtn", null, new Vector2(370, 235), new Vector2(30, 28), raycast: true);
        prevBtn.GetComponent<Image>().color = new Color(0.10f, 0.07f, 0.16f, 0.9f);
        var pb = prevBtn.AddComponent<Button>();
        pb.transition = Selectable.Transition.None;
        pb.onClick.AddListener(() => { if (currentPage > 0) { currentPage--; RefreshShop(); } });
        ShopBagShell.MakeText(prevBtn.transform, "ShopPrevLbl", "◀", 18, UIHelper.TextSecondary, font, Vector2.zero, new Vector2(30, 28));

        pageTextObj = new GameObject("ShopPageText");
        pageTextObj.transform.SetParent(panel.transform, false);
        var ptr = pageTextObj.AddComponent<RectTransform>();
        ptr.anchorMin = new Vector2(0.5f, 0.5f); ptr.anchorMax = new Vector2(0.5f, 0.5f);
        ptr.anchoredPosition = new Vector2(412, 235);
        ptr.sizeDelta = new Vector2(56, 28);
        pageText = pageTextObj.AddComponent<Text>();
        pageText.alignment = TextAnchor.MiddleCenter;
        pageText.fontSize = 18; pageText.color = UIHelper.TextSecondary; pageText.font = font;
        pageText.raycastTarget = false;

        nextBtn = ShopBagShell.MakeSprite(panel.transform, "ShopNextBtn", null, new Vector2(452, 235), new Vector2(30, 28), raycast: true);
        nextBtn.GetComponent<Image>().color = new Color(0.10f, 0.07f, 0.16f, 0.9f);
        var nb = nextBtn.AddComponent<Button>();
        nb.transition = Selectable.Transition.None;
        nb.onClick.AddListener(() => { if (currentPage < shopPageCount - 1) { currentPage++; RefreshShop(); } });
        ShopBagShell.MakeText(nextBtn.transform, "ShopNextLbl", "▶", 18, UIHelper.TextSecondary, font, Vector2.zero, new Vector2(30, 28));

        panel.SetActive(false);
    }

    private void RefreshModeVisibility()
    {
        bool equipMode = currentMode == 0;
        if (gridRoot != null) gridRoot.SetActive(true);
        if (buyBtn != null) buyBtn.SetActive(true);
        if (modeEquipLine != null) modeEquipLine.SetActive(equipMode);
        if (modeRuneLine != null) modeRuneLine.SetActive(!equipMode);
        if (prevBtn != null) prevBtn.SetActive(equipMode);
        if (nextBtn != null) nextBtn.SetActive(equipMode);
        if (pageTextObj != null) pageTextObj.SetActive(equipMode);
        if (equipMode) RefreshShop();
        else RefreshRuneGrid();
    }

    private void GenerateShopPool()
    {
        shopPool.Clear();
        int dungeonLevel = (RuntimePlayerData.Instance?.HighestStage ?? 0) + 1;
        for (int i = 0; i < ShopPoolSize; i++)
            shopPool.Add(EquipmentItem.GenerateShopItem(dungeonLevel));
        currentPage = 0;
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

    private void RefreshShop()
    {
        ShopBagShell.ClearChildren(gridRoot);
        cellGos.Clear();
        cellItemIndex.Clear();
        selectedCell = -1;

        Font font = GameManager.GetUIFont();
        shopPageCount = Mathf.Max(1, Mathf.CeilToInt(shopPool.Count / (float)CellsPerPage));
        currentPage = Mathf.Clamp(currentPage, 0, shopPageCount - 1);
        int start = currentPage * CellsPerPage;

        for (int i = 0; i < CellsPerPage; i++)
        {
            int col = i % 4, row = i / 4;
            var center = new Vector2(ColX[col], RowY[row]);
            int abs = start + i;

            if (abs < shopPool.Count)
            {
                var item = shopPool[abs];

                var cell = ShopBagShell.MakeSprite(gridRoot.transform, "ShopCell_" + i,
                    ShopBagShell.FrameName(item.Rarity), center, new Vector2(CellSize, CellSize));
                cell.GetComponent<Image>().raycastTarget = true;
                var btn = cell.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                int cellIdx = i;
                btn.onClick.AddListener(() => SelectCell(cellIdx));

                string lvlTag = item.UpgradeLevel > 0 ? $" <color=#C088FF>+{item.UpgradeLevel}</color>" : "";
                var nameT = ShopBagShell.MakeText(cell.transform, "ShopCellName_" + i, ShopBagShell.StripRarityPrefix(item.Name) + lvlTag, 15, Color.white, font,
                    new Vector2(0, 10), new Vector2(84, 38), TextAnchor.UpperCenter, outline: true);
                nameT.supportRichText = true;

                var priceT = ShopBagShell.MakeText(cell.transform, "ShopCellPrice_" + i,
                    UIStringBuilderPool.FormatNumber(item.SellPrice * 2), 15, new Color(1f, 0.86f, 0.40f, 1f), font,
                    new Vector2(0, -30), new Vector2(84, 22), TextAnchor.MiddleCenter, outline: true);

                int cmp = CompareToEquipped(item);
                if (cmp != 0)
                    ShopBagShell.MakeSprite(cell.transform, "ShopCellHint_" + i, cmp > 0 ? "UpHint" : "DownHint",
                        new Vector2(29, -29), new Vector2(26, 33));

                cellGos.Add(cell);
                cellItemIndex.Add(abs);
            }
            else
            {
                var cell = ShopBagShell.MakeSprite(gridRoot.transform, "ShopCell_" + i, "Rarity_None",
                    center, new Vector2(CellSize, CellSize));
                cell.GetComponent<Image>().color = new Color(0.75f, 0.72f, 0.8f, 0.45f);
                cellGos.Add(cell);
                cellItemIndex.Add(-1);
            }
        }

        if (pageText != null) pageText.text = $"{currentPage + 1}/{shopPageCount}";
    }

    private void SelectCell(int i)
    {
        selectedCell = i;
        for (int c = 0; c < cellGos.Count; c++)
            if (cellGos[c] != null)
                cellGos[c].transform.localScale = (c == selectedCell) ? new Vector3(1.1f, 1.1f, 1f) : Vector3.one;
    }

    private void OnBuyClicked()
    {
        if (selectedCell < 0 || selectedCell >= cellItemIndex.Count || cellItemIndex[selectedCell] < 0)
        {
            if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("提示", "请先选择要购买的商品");
            return;
        }
        if (currentMode == 0)
        {
            ShowBuyConfirm(cellItemIndex[selectedCell]);
            return;
        }
        // 符文模式：未拥有→购买确认；已拥有未满级→升级确认；满级→提示
        var allRunes = RuneData.GetAllRunes();
        int ri = cellItemIndex[selectedCell];
        if (ri < 0 || ri >= allRunes.Count) return;
        var rune = allRunes[ri];
        bool owned = RuneManager.Instance != null && RuneManager.Instance.IsOwned(rune.Type);
        int level = RuneManager.Instance != null ? RuneManager.Instance.GetLevel(rune.Type) : 0;
        if (!owned)
            ShowRuneConfirm(rune, false, level);
        else if (level < 5)
            ShowRuneConfirm(rune, true, level);
        else if (GameUI.Instance != null)
            GameUI.Instance.ShowItemPickupToast("提示", $"{rune.Name} 已满级");
    }

    /// <summary>符文格子网格 — 与装备商品页同样的格子形式</summary>
    private void RefreshRuneGrid()
    {
        ShopBagShell.ClearChildren(gridRoot);
        cellGos.Clear();
        cellItemIndex.Clear();
        selectedCell = -1;

        Font font = GameManager.GetUIFont();
        var allRunes = RuneData.GetAllRunes();

        for (int i = 0; i < CellsPerPage; i++)
        {
            int col = i % 4, row = i / 4;
            var center = new Vector2(ColX[col], RowY[row]);

            if (i < allRunes.Count)
            {
                var rune = allRunes[i];
                bool owned = RuneManager.Instance != null && RuneManager.Instance.IsOwned(rune.Type);
                int level = RuneManager.Instance != null ? RuneManager.Instance.GetLevel(rune.Type) : 0;

                var cell = ShopBagShell.MakeSprite(gridRoot.transform, "RuneCell_" + i, "Rarity_None",
                    center, new Vector2(CellSize, CellSize));
                var cellImg = cell.GetComponent<Image>();
                cellImg.color = Color.Lerp(Color.white, rune.RuneColor, 0.55f); // 框染符文色
                cellImg.raycastTarget = true;
                var btn = cell.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                int cellIdx = i;
                btn.onClick.AddListener(() => SelectCell(cellIdx));

                string status = owned ? $" <color=#55FF55>Lv.{level}</color>" : "";
                var nameT = ShopBagShell.MakeText(cell.transform, "RuneCellName_" + i, rune.Name + status, 15, Color.white, font,
                    new Vector2(0, 10), new Vector2(84, 38), TextAnchor.UpperCenter, outline: true);
                nameT.supportRichText = true;

                string priceStr;
                if (!owned)
                {
                    var price = RuneManager.Instance?.GetPrice(rune.Type) ?? (500, 20);
                    priceStr = $"<color=#FFCC44>{UIStringBuilderPool.FormatNumber(price.gold)}金</color>\n<size=12><color=#88CCFF>碎片{UIStringBuilderPool.FormatNumber(price.fragments)}</color></size>";
                }
                else if (level < 5)
                {
                    var upCost = RuneManager.Instance?.GetUpgradeCost(rune.Type, level) ?? 20;
                    priceStr = $"<size=13><color=#88CCFF>升级碎片 {UIStringBuilderPool.FormatNumber(upCost)}</color></size>";
                }
                else
                {
                    priceStr = "<size=13><color=#CCCCCC>已满级</color></size>";
                }
                var priceT = ShopBagShell.MakeText(cell.transform, "RuneCellPrice_" + i, priceStr, 14, Color.white, font,
                    new Vector2(0, -28), new Vector2(84, 32), TextAnchor.MiddleCenter, outline: true);
                priceT.supportRichText = true;

                cellGos.Add(cell);
                cellItemIndex.Add(i);
            }
            else
            {
                var cell = ShopBagShell.MakeSprite(gridRoot.transform, "RuneCell_" + i, "Rarity_None",
                    center, new Vector2(CellSize, CellSize));
                cell.GetComponent<Image>().color = new Color(0.75f, 0.72f, 0.8f, 0.45f);
                cellGos.Add(cell);
                cellItemIndex.Add(-1);
            }
        }

        if (pageText != null) pageText.text = "1/1";
    }

    /// <summary>符文购买/升级确认框</summary>
    private void ShowRuneConfirm(RuneData rune, bool isUpgrade, int level)
    {
        if (confirmDialog != null) Destroy(confirmDialog);

        Font font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();

        int gold = GameManager.Instance?.Player?.Stats?.Gold ?? 0;
        int frags = DismantleSystem.Fragments;
        bool canAfford;
        string costLine;
        if (isUpgrade)
        {
            int upCost = RuneManager.Instance?.GetUpgradeCost(rune.Type, level) ?? 20;
            canAfford = frags >= upCost;
            costLine = $"<color=#88CCFF>升级消耗 {upCost} 碎片</color>" + (canAfford ? "" : $"\n<color=#FF4444>(碎片不足! 你有{frags})</color>");
        }
        else
        {
            var price = RuneManager.Instance?.GetPrice(rune.Type) ?? (500, 20);
            canAfford = gold >= price.gold && frags >= price.fragments;
            costLine = $"<color=#FFCC44>金币 {UIStringBuilderPool.FormatNumber(price.gold)}</color>  <color=#88CCFF>碎片 {UIStringBuilderPool.FormatNumber(price.fragments)}</color>"
                + (canAfford ? "" : $"\n<color=#FF4444>(余额不足! 你有{gold}金/{frags}碎片)</color>");
        }

        confirmDialog = new GameObject("BuyConfirm");
        confirmDialog.transform.SetParent(canvas.transform, false);
        var dr = confirmDialog.AddComponent<RectTransform>();
        dr.anchorMin = Vector2.zero; dr.anchorMax = Vector2.one;
        dr.offsetMin = Vector2.zero; dr.offsetMax = Vector2.zero;
        var db = confirmDialog.AddComponent<Image>();
        db.color = new Color(0, 0, 0, 0.7f);

        var box = UIHelper.MakeGlowCard(confirmDialog.transform, "DialogBox",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-240, -180), new Vector2(240, 180),
            new Color(0.06f, 0.03f, 0.09f, 0.95f),
            UIHelper.GlowBottom,
            UIHelper.Accent);

        var dtObj = new GameObject("DialogTitle");
        dtObj.transform.SetParent(box.transform, false);
        var dtr = dtObj.AddComponent<RectTransform>();
        dtr.anchorMin = new Vector2(0, 1); dtr.anchorMax = new Vector2(1, 1);
        dtr.pivot = new Vector2(0.5f, 1); dtr.anchoredPosition = new Vector2(0, -18);
        dtr.sizeDelta = new Vector2(0, 32);
        var dtTxt = dtObj.AddComponent<Text>();
        dtTxt.text = isUpgrade ? "升级符文" : "购买符文"; dtTxt.alignment = TextAnchor.MiddleCenter;
        dtTxt.fontSize = 24; dtTxt.color = UIHelper.Accent; dtTxt.font = font;

        var infoObj = new GameObject("ItemInfo");
        infoObj.transform.SetParent(box.transform, false);
        var ir = infoObj.AddComponent<RectTransform>();
        ir.anchorMin = new Vector2(0.08f, 0.3f); ir.anchorMax = new Vector2(0.92f, 0.8f);
        ir.offsetMin = Vector2.zero; ir.offsetMax = Vector2.zero;
        var it = infoObj.AddComponent<Text>();
        string color = ColorUtility.ToHtmlStringRGBA(rune.RuneColor);
        it.text = $"<color=#{color}>{rune.Name}</color>" + (isUpgrade ? $" <color=#55FF55>Lv.{level} → Lv.{level + 1}</color>" : "") + $"\n<size=15><color=#aaa>{rune.Description}</color></size>\n\n{costLine}";
        it.alignment = TextAnchor.MiddleCenter;
        it.fontSize = 18; it.color = UIHelper.TextPrimary;
        it.font = font; it.supportRichText = true;

        var okBtn = UIHelper.MakeGlowCard(box.transform, "OkBtn",
            new Vector2(0.05f, 0.04f), new Vector2(0.47f, 0.22f),
            Vector2.zero, Vector2.zero,
            canAfford ? new Color(0.05f, 0.10f, 0.08f, 0.92f) : new Color(0.06f, 0.06f, 0.08f, 0.6f),
            canAfford ? new Color(0.02f, 0.05f, 0.04f, 0.92f) : new Color(0.04f, 0.02f, 0.06f, 0.6f),
            UIHelper.Accent);
        var okBtnComp = okBtn.AddComponent<Button>();
        okBtnComp.transition = Selectable.Transition.None;
        if (canAfford)
        {
            okBtnComp.onClick.AddListener(() =>
            {
                bool ok = isUpgrade ? RuneManager.Instance.UpgradeRune(rune.Type) : RuneManager.Instance.PurchaseRune(rune.Type);
                if (ok)
                {
                    if (GameUI.Instance != null)
                        GameUI.Instance.ShowItemPickupToast(isUpgrade ? "升级成功" : "购买成功", $"{rune.Name}" + (isUpgrade ? $" → Lv.{level + 1}" : " 已获得!"));
                    HideConfirmDialog();
                    RefreshRuneGrid();
                }
                else
                {
                    if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("失败", "金币/碎片不足");
                }
            });
        }
        var okLbl = new GameObject("Lbl");
        okLbl.transform.SetParent(okBtn.transform, false);
        var okr = okLbl.AddComponent<RectTransform>();
        okr.anchorMin = Vector2.zero; okr.anchorMax = Vector2.one;
        okr.offsetMin = Vector2.zero; okr.offsetMax = Vector2.zero;
        var okTxt = okLbl.AddComponent<Text>();
        okTxt.text = !canAfford ? "余额不足" : (isUpgrade ? "确认升级" : "确认购买");
        okTxt.alignment = TextAnchor.MiddleCenter; okTxt.fontSize = 20;
        okTxt.color = UIHelper.TextPrimary; okTxt.font = font;

        var cancelBtn = UIHelper.MakeGlowCard(box.transform, "CancelBtn",
            new Vector2(0.53f, 0.04f), new Vector2(0.95f, 0.22f),
            Vector2.zero, Vector2.zero,
            new Color(0.10f, 0.05f, 0.05f, 0.92f),
            new Color(0.05f, 0.02f, 0.02f, 0.92f),
            new Color(0.6f, 0.2f, 0.2f, 1f));
        var cancelBtnComp = cancelBtn.AddComponent<Button>();
        cancelBtnComp.transition = Selectable.Transition.None;
        cancelBtnComp.onClick.AddListener(() => HideConfirmDialog());
        var cancelLbl = new GameObject("Lbl");
        cancelLbl.transform.SetParent(cancelBtn.transform, false);
        var clr2 = cancelLbl.AddComponent<RectTransform>();
        clr2.anchorMin = Vector2.zero; clr2.anchorMax = Vector2.one;
        clr2.offsetMin = Vector2.zero; clr2.offsetMax = Vector2.zero;
        var cancelTxt = cancelLbl.AddComponent<Text>();
        cancelTxt.text = "取 消"; cancelTxt.alignment = TextAnchor.MiddleCenter;
        cancelTxt.fontSize = 20; cancelTxt.color = UIHelper.TextPrimary; cancelTxt.font = font;
    }

    private void ShowBuyConfirm(int absIndex)
    {
        if (absIndex < 0 || absIndex >= shopPool.Count) return;
        pendingBuyIndex = absIndex;
        var item = shopPool[absIndex];
        int price = item.SellPrice * 2;
        if (confirmDialog != null) Destroy(confirmDialog);

        Font font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();

        // 遮罩
        confirmDialog = new GameObject("BuyConfirm");
        confirmDialog.transform.SetParent(canvas.transform, false);
        var dr = confirmDialog.AddComponent<RectTransform>();
        dr.anchorMin = Vector2.zero; dr.anchorMax = Vector2.one;
        dr.offsetMin = Vector2.zero; dr.offsetMax = Vector2.zero;
        var db = confirmDialog.AddComponent<Image>();
        db.color = new Color(0, 0, 0, 0.7f);

        // 对话框卡片
        var box = UIHelper.MakeGlowCard(confirmDialog.transform, "DialogBox",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-240, -180), new Vector2(240, 180),
            new Color(0.06f, 0.03f, 0.09f, 0.95f),
            UIHelper.GlowBottom,
            UIHelper.Accent);

        // 标题
        var dtObj = new GameObject("DialogTitle");
        dtObj.transform.SetParent(box.transform, false);
        var dtr = dtObj.AddComponent<RectTransform>();
        dtr.anchorMin = new Vector2(0, 1); dtr.anchorMax = new Vector2(1, 1);
        dtr.pivot = new Vector2(0.5f, 1); dtr.anchoredPosition = new Vector2(0, -18);
        dtr.sizeDelta = new Vector2(0, 32);
        var dtTxt = dtObj.AddComponent<Text>();
        dtTxt.text = "确认购买"; dtTxt.alignment = TextAnchor.MiddleCenter;
        dtTxt.fontSize = 24; dtTxt.color = UIHelper.Accent; dtTxt.font = font;

        // 物品信息
        var infoObj = new GameObject("ItemInfo");
        infoObj.transform.SetParent(box.transform, false);
        var ir = infoObj.AddComponent<RectTransform>();
        ir.anchorMin = new Vector2(0.08f, 0.3f); ir.anchorMax = new Vector2(0.92f, 0.8f);
        ir.offsetMin = Vector2.zero; ir.offsetMax = Vector2.zero;
        var it = infoObj.AddComponent<Text>();
        int playerGold = GameManager.Instance?.Player?.Stats?.Gold ?? 0;
        bool canAfford = playerGold >= price;
        it.text = $"{SlotLabel(item.SlotType)} {ShopBagShell.StripRarityPrefix(item.Name)}\n<size=15><color=#aaa>{item.GetStatSummary()}</color></size>\n\n<color=#8C55C0>价格 {UIStringBuilderPool.FormatNumber(price)} 金币</color>"
            + (canAfford ? "" : $"\n<color=#FF4444>(不足! 你有{playerGold}金)</color>");
        it.alignment = TextAnchor.MiddleCenter;
        it.fontSize = 18; it.color = ItemData.GetRarityColor(item.Rarity);
        it.font = font; it.supportRichText = true;

        // 按钮行
        var okBtn = UIHelper.MakeGlowCard(box.transform, "OkBtn",
            new Vector2(0.05f, 0.04f), new Vector2(0.47f, 0.22f),
            Vector2.zero, Vector2.zero,
            canAfford ? new Color(0.05f, 0.10f, 0.08f, 0.92f) : new Color(0.06f, 0.06f, 0.08f, 0.6f),
            canAfford ? new Color(0.02f, 0.05f, 0.04f, 0.92f) : new Color(0.04f, 0.02f, 0.06f, 0.6f),
            UIHelper.Accent);
        var okBtnComp = okBtn.AddComponent<Button>();
        okBtnComp.transition = Selectable.Transition.None;
        if (canAfford) okBtnComp.onClick.AddListener(() => { ConfirmBuy(); HideConfirmDialog(); });
        var okLbl = new GameObject("Lbl");
        okLbl.transform.SetParent(okBtn.transform, false);
        var okr = okLbl.AddComponent<RectTransform>();
        okr.anchorMin = Vector2.zero; okr.anchorMax = Vector2.one;
        okr.offsetMin = Vector2.zero; okr.offsetMax = Vector2.zero;
        var okTxt = okLbl.AddComponent<Text>();
        okTxt.text = canAfford ? "确认购买" : "金币不足";
        okTxt.alignment = TextAnchor.MiddleCenter; okTxt.fontSize = 20;
        okTxt.color = UIHelper.TextPrimary; okTxt.font = font;

        var cancelBtn = UIHelper.MakeGlowCard(box.transform, "CancelBtn",
            new Vector2(0.53f, 0.04f), new Vector2(0.95f, 0.22f),
            Vector2.zero, Vector2.zero,
            new Color(0.10f, 0.05f, 0.05f, 0.92f),
            new Color(0.05f, 0.02f, 0.02f, 0.92f),
            new Color(0.6f, 0.2f, 0.2f, 1f));
        var cancelBtnComp = cancelBtn.AddComponent<Button>();
        cancelBtnComp.transition = Selectable.Transition.None;
        cancelBtnComp.onClick.AddListener(() => HideConfirmDialog());
        var cancelLbl = new GameObject("Lbl");
        cancelLbl.transform.SetParent(cancelBtn.transform, false);
        var clr2 = cancelLbl.AddComponent<RectTransform>();
        clr2.anchorMin = Vector2.zero; clr2.anchorMax = Vector2.one;
        clr2.offsetMin = Vector2.zero; clr2.offsetMax = Vector2.zero;
        var cancelTxt = cancelLbl.AddComponent<Text>();
        cancelTxt.text = "取 消"; cancelTxt.alignment = TextAnchor.MiddleCenter;
        cancelTxt.fontSize = 20; cancelTxt.color = UIHelper.TextPrimary; cancelTxt.font = font;
    }

    private void HideConfirmDialog()
    {
        if (confirmDialog != null) { Destroy(confirmDialog); confirmDialog = null; }
        pendingBuyIndex = -1;
    }

    private void ConfirmBuy()
    {
        if (pendingBuyIndex < 0) return;
        BuyItem(pendingBuyIndex);
        pendingBuyIndex = -1;
    }

    private void BuyItem(int absIndex)
    {
        if (absIndex < 0 || absIndex >= shopPool.Count) return;
        var player = GameManager.Instance?.Player;
        if (player == null) return;

        var item = shopPool[absIndex];
        int price = item.SellPrice * 2;

        // 检查背包是否已满
        if (player.Inventory.Backpack.Count >= EquipmentInventory.MaxBackpackSize)
        {
            // 背包满 — 发送到邮箱
            if (player.Stats.SpendGold(price))
            {
                LocalMailSystem.SendItemMail($"商店购买: {item.Name}", $"背包已满，装备已发送至邮箱。\n{item.GetStatSummary()}", item);
                shopPool.RemoveAt(absIndex);
                int dungeonLevel = (RuntimePlayerData.Instance?.HighestStage ?? 0) + 1;
                shopPool.Add(EquipmentItem.GenerateShopItem(dungeonLevel));
                RefreshShop();
                player.Stats.Save();
            }
            return;
        }

        if (player.Stats.SpendGold(price))
        {
            player.Inventory.AddToBackpack(item);
            shopPool.RemoveAt(absIndex);
            int dl = (RuntimePlayerData.Instance?.HighestStage ?? 0) + 1;
            shopPool.Add(EquipmentItem.GenerateShopItem(dl));
            RefreshShop();
            player.Stats.Save();
        }
    }

    private void OnDestroy()
    {
        ShopBagShell.CloseSlotMenu();
        HideConfirmDialog();
        if (panel != null) Destroy(panel);
        if (Instance == this) Instance = null;
    }

    public bool IsVisible() => panel != null && panel.activeSelf;
}
