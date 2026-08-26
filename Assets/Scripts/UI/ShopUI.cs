using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 装备商店 — 暗色ARPG风格
/// </summary>
public class ShopUI : MonoBehaviour
{
    public static ShopUI Instance { get; private set; }

    private GameObject panel;
    private List<GameObject> shopItemButtons = new List<GameObject>();
    private List<EquipmentItem> currentShopItems = new List<EquipmentItem>();
    private List<EquipmentItem> shopPool = new List<EquipmentItem>();
    private Text pageText;
    private Coroutine showRoutine;
    private GameObject confirmDialog;
    private int pendingBuyIndex = -1;

    // 符文商店
    private int currentTab = 0; // 0=装备, 1=符文
    private GameObject runeListArea;
    private GameObject equipListArea;

    private const int ShopItemCount = 8;
    private const int ShopPoolSize = 40;
    private int currentPage = 0;
    private int shopPageCount = 1;

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
        UIHelper.SetupTopBar("商场", () => { Hide(); UIHelper.EnsureUIManager(); UIManager.Instance.ShowHub(); });
        if (shopPool.Count == 0) GenerateShopPool();
        RefreshShop();
        panel.SetActive(true);
        if (showRoutine != null) StopCoroutine(showRoutine);
        showRoutine = StartCoroutine(UIHelper.FadeIn(panel, 0.2f));
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
        Font font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();

        panel = new GameObject("ShopPanel");
        panel.transform.SetParent(canvas.transform, false);
        RectTransform pr = panel.AddComponent<RectTransform>();
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one;
        pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;
        panel.AddComponent<CanvasGroup>();

        // 暗色背景
        var bg = new GameObject("BG");
        bg.transform.SetParent(panel.transform, false);
        var bgr = bg.AddComponent<RectTransform>();
        bgr.anchorMin = Vector2.zero; bgr.anchorMax = Vector2.one;
        bgr.offsetMin = Vector2.zero; bgr.offsetMax = Vector2.zero;
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = UIHelper.BgDark;
        bgImg.raycastTarget = true;

        // 标签栏：装备 / 符文
        var tabRow = new GameObject("TabRow");
        tabRow.transform.SetParent(panel.transform, false);
        var tabR = tabRow.AddComponent<RectTransform>();
        tabR.anchorMin = new Vector2(0.1f, 1); tabR.anchorMax = new Vector2(0.9f, 1);
        tabR.pivot = new Vector2(0.5f, 1);
        tabR.anchoredPosition = new Vector2(0, -100);
        tabR.sizeDelta = new Vector2(0, 40);

        for (int t = 0; t < 2; t++)
        {
            int tabIdx = t;
            var tabObj = new GameObject($"Tab_{t}");
            tabObj.transform.SetParent(tabRow.transform, false);
            var tR = tabObj.AddComponent<RectTransform>();
            float w = 0.5f;
            tR.anchorMin = new Vector2(w * t, 0); tR.anchorMax = new Vector2(w * (t + 1), 1);
            tR.offsetMin = new Vector2(2, 0); tR.offsetMax = new Vector2(-2, 0);
            var tImg = tabObj.AddComponent<Image>();
            tImg.color = t == 0 ? new Color(0.12f, 0.08f, 0.18f, 0.95f) : new Color(0.04f, 0.03f, 0.06f, 0.6f);
            var tBtn = tabObj.AddComponent<Button>();
            tBtn.transition = Selectable.Transition.None;
            tBtn.onClick.AddListener(() => { currentTab = tabIdx; SwitchTab(); });
            var tLbl = new GameObject("Lbl");
            tLbl.transform.SetParent(tabObj.transform, false);
            var tlR = tLbl.AddComponent<RectTransform>();
            tlR.anchorMin = Vector2.zero; tlR.anchorMax = Vector2.one; tlR.offsetMin = Vector2.zero; tlR.offsetMax = Vector2.zero;
            var tTxt = tLbl.AddComponent<Text>();
            tTxt.text = t == 0 ? "装备" : "符文";
            tTxt.alignment = TextAnchor.MiddleCenter; tTxt.fontSize = 18; tTxt.font = font;
            tTxt.color = t == 0 ? UIHelper.TextPrimary : UIHelper.TextDim;
        }

        // 商品列表区域
        var listArea = new GameObject("ListArea");
        listArea.transform.SetParent(panel.transform, false);
        var lr = listArea.AddComponent<RectTransform>();
        lr.anchorMin = new Vector2(0.05f, 0); lr.anchorMax = new Vector2(0.95f, 1);
        lr.offsetMin = new Vector2(0, 80); lr.offsetMax = new Vector2(0, -150);

        // 翻页控件
        var pageRow = new GameObject("PageRow");
        pageRow.transform.SetParent(panel.transform, false);
        var pgr = pageRow.AddComponent<RectTransform>();
        pgr.anchorMin = new Vector2(0, 0); pgr.anchorMax = new Vector2(1, 0);
        pgr.pivot = new Vector2(0.5f, 0);
        pgr.anchoredPosition = new Vector2(0, 25);
        pgr.sizeDelta = new Vector2(0, 40);

        UIHelper.MakeButton(pageRow.transform, "PrevBtn", "◀ 上一页", font,
            new Vector2(0, 0), new Vector2(0.25f, 1), new Vector2(10, 2), new Vector2(-3, -2),
            new Color(0.06f, 0.06f, 0.08f, 0.9f), 16, () => { if (currentPage > 0) { currentPage--; RefreshShop(); } });

        var ptObj = new GameObject("PageText");
        ptObj.transform.SetParent(pageRow.transform, false);
        var ptr = ptObj.AddComponent<RectTransform>();
        ptr.anchorMin = new Vector2(0.25f, 0); ptr.anchorMax = new Vector2(0.55f, 1);
        ptr.offsetMin = Vector2.zero; ptr.offsetMax = Vector2.zero;
        pageText = ptObj.AddComponent<Text>();
        pageText.alignment = TextAnchor.MiddleCenter;
        pageText.fontSize = 18; pageText.color = UIHelper.TextSecondary; pageText.font = font;

        UIHelper.MakeButton(pageRow.transform, "NextBtn", "下一页 ▶", font,
            new Vector2(0.55f, 0), new Vector2(0.8f, 1), new Vector2(3, 2), new Vector2(-3, -2),
            new Color(0.06f, 0.06f, 0.08f, 0.9f), 16, () => { if (currentPage < shopPageCount - 1) { currentPage++; RefreshShop(); } });

        // 刷新商品按钮
        UIHelper.MakeButton(pageRow.transform, "RefreshBtn", "刷新", font,
            new Vector2(0.8f, 0), new Vector2(1, 1), new Vector2(3, 2), new Vector2(-10, -2),
            new Color(0.15f, 0.1f, 0.2f, 0.9f), 16, () => { GenerateShopPool(); RefreshShop(); TopNavBar.Instance?.RefreshCurrency(); });

        // 符文列表区域（初始隐藏）
        runeListArea = new GameObject("RuneListArea");
        runeListArea.transform.SetParent(panel.transform, false);
        var rlr = runeListArea.AddComponent<RectTransform>();
        rlr.anchorMin = new Vector2(0.05f, 0); rlr.anchorMax = new Vector2(0.95f, 1);
        rlr.offsetMin = new Vector2(0, 80); rlr.offsetMax = new Vector2(0, -160);
        runeListArea.AddComponent<Image>().color = new Color(0.03f, 0.02f, 0.05f, 0.3f);
        runeListArea.SetActive(false);

        // 装备列表区域引用
        equipListArea = listArea;

        panel.SetActive(false);
    }

    /// <summary>切换标签页</summary>
    private void SwitchTab()
    {
        // 更新标签颜色
        var tabRow = panel.transform.Find("TabRow");
        if (tabRow == null) return;
        for (int t = 0; t < 2; t++)
        {
            var tabObj = tabRow.GetChild(t);
            var img = tabObj.GetComponent<Image>();
            var lbl = tabObj.Find("Lbl")?.GetComponent<Text>();
            if (img != null) img.color = t == currentTab ? new Color(0.12f, 0.08f, 0.18f, 0.95f) : new Color(0.04f, 0.03f, 0.06f, 0.6f);
            if (lbl != null) lbl.color = t == currentTab ? UIHelper.TextPrimary : UIHelper.TextDim;
        }

        if (currentTab == 0)
        {
            equipListArea?.SetActive(true);
            runeListArea?.SetActive(false);
            // 显示翻页和刷新
            var pageRow = panel.transform.Find("PageRow");
            if (pageRow != null) pageRow.gameObject.SetActive(true);
            RefreshShop();
        }
        else
        {
            equipListArea?.SetActive(false);
            runeListArea?.SetActive(true);
            var pageRow = panel.transform.Find("PageRow");
            if (pageRow != null) pageRow.gameObject.SetActive(false);
            RefreshRuneShop();
        }
        TopNavBar.Instance?.RefreshCurrency();
    }

    /// <summary>渲染符文商店</summary>
    private void RefreshRuneShop()
    {
        if (runeListArea == null) return;
        // 清除旧内容
        for (int i = runeListArea.transform.childCount - 1; i >= 0; i--)
            Destroy(runeListArea.transform.GetChild(i).gameObject);

        Font font = GameManager.GetUIFont();
        var allRunes = RuneData.GetAllRunes();
        float cardH = 70f, gap = 6f;

        for (int i = 0; i < allRunes.Count; i++)
        {
            var rune = allRunes[i];
            int idx = i;
            float y = -i * (cardH + gap);

            var row = UIHelper.MakeGlowCard(runeListArea.transform, $"Rune_{i}",
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, y - cardH), new Vector2(0, y),
                new Color(0.06f, 0.04f, 0.08f, 0.92f), UIHelper.GlowBottom, UIHelper.BorderSubtle);

            // 图标位（左）
            var iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(row.transform, false);
            var iconR = iconObj.AddComponent<RectTransform>();
            iconR.anchorMin = new Vector2(0.02f, 0.15f); iconR.anchorMax = new Vector2(0.1f, 0.85f);
            iconR.offsetMin = Vector2.zero; iconR.offsetMax = Vector2.zero;
            var iconImg = iconObj.AddComponent<Image>();
            iconImg.color = new Color(rune.RuneColor.r * 0.2f, rune.RuneColor.g * 0.2f, rune.RuneColor.b * 0.2f, 0.8f);
            var iconTxt = new GameObject("IconTxt");
            iconTxt.transform.SetParent(iconObj.transform, false);
            var itR = iconTxt.AddComponent<RectTransform>();
            itR.anchorMin = Vector2.zero; itR.anchorMax = Vector2.one; itR.offsetMin = Vector2.zero; itR.offsetMax = Vector2.zero;
            var itTxt = iconTxt.AddComponent<Text>();
            itTxt.text = rune.Name; itTxt.alignment = TextAnchor.MiddleCenter; itTxt.fontSize = 11;
            itTxt.color = rune.RuneColor; itTxt.font = font; itTxt.raycastTarget = false;

            // 符文名+等级（右上）
            bool owned = RuneManager.Instance != null && RuneManager.Instance.IsOwned(rune.Type);
            int level = RuneManager.Instance != null ? RuneManager.Instance.GetLevel(rune.Type) : 0;

            var nameObj = new GameObject("Name");
            nameObj.transform.SetParent(row.transform, false);
            var nR = nameObj.AddComponent<RectTransform>();
            nR.anchorMin = new Vector2(0.12f, 0.5f); nR.anchorMax = new Vector2(0.55f, 1f);
            nR.offsetMin = new Vector2(4, 2); nR.offsetMax = new Vector2(-4, -2);
            var nameTxt = nameObj.AddComponent<Text>();
            string color = ColorUtility.ToHtmlStringRGBA(rune.RuneColor);
            string status = owned ? $"<color=#55FF55>Lv.{level}</color>" : "<color=#888>未拥有</color>";
            nameTxt.text = $"<color=#{color}>{rune.Name}</color> {status}";
            nameTxt.alignment = TextAnchor.MiddleLeft; nameTxt.fontSize = 16;
            nameTxt.color = UIHelper.TextPrimary; nameTxt.font = font; nameTxt.supportRichText = true; nameTxt.raycastTarget = false;

            // 效果描述（右下）
            var descObj = new GameObject("Desc");
            descObj.transform.SetParent(row.transform, false);
            var dR = descObj.AddComponent<RectTransform>();
            dR.anchorMin = new Vector2(0.12f, 0); dR.anchorMax = new Vector2(0.55f, 0.5f);
            dR.offsetMin = new Vector2(4, 2); dR.offsetMax = new Vector2(-4, -2);
            var descTxt = descObj.AddComponent<Text>();
            descTxt.text = $"<size=13><color=#aaa>{rune.Description}</color></size>";
            descTxt.alignment = TextAnchor.MiddleLeft; descTxt.fontSize = 14;
            descTxt.color = UIHelper.TextSecondary; descTxt.font = font; descTxt.supportRichText = true; descTxt.raycastTarget = false;

            var price = RuneManager.Instance?.GetPrice(rune.Type) ?? (500, 20);
            var upgradeCost = RuneManager.Instance?.GetUpgradeCost(rune.Type, level) ?? 20;

            if (!owned)
            {
                // 购买按钮
                UIHelper.MakeButton(row.transform, "BuyBtn", $"金币 {UIStringBuilderPool.FormatNumber(price.gold)}  碎片 {UIStringBuilderPool.FormatNumber(price.fragments)}", font,
                    new Vector2(0.55f, 0.1f), new Vector2(0.98f, 0.9f), Vector2.zero, Vector2.zero,
                    UIHelper.BtnConfirm, 14, () =>
                    {
                        if (RuneManager.Instance != null && RuneManager.Instance.PurchaseRune(rune.Type))
                        {
                            if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("购买成功", $"{rune.Name} 已获得!");
                            RefreshRuneShop();
                            TopNavBar.Instance?.RefreshCurrency();
                        }
                        else
                        {
                            if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("金币/碎片不足", "");
                        }
                    });
            }
            else if (level < 5)
            {
                // 升级按钮
                UIHelper.MakeButton(row.transform, "UpgradeBtn", $"升级 碎片{UIStringBuilderPool.FormatNumber(upgradeCost)}", font,
                    new Vector2(0.55f, 0.1f), new Vector2(0.98f, 0.9f), Vector2.zero, Vector2.zero,
                    new Color(0.15f, 0.25f, 0.5f, 0.9f), 14, () =>
                    {
                        if (RuneManager.Instance != null && RuneManager.Instance.UpgradeRune(rune.Type))
                        {
                            if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("升级成功", $"{rune.Name} → Lv.{level + 1}");
                            RefreshRuneShop();
                        }
                        else
                        {
                            if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("碎片不足", $"需要{upgradeCost}碎片");
                        }
                    });
            }
            else
            {
                // 满级
                UIHelper.MakeButton(row.transform, "MaxBtn", "已满级", font,
                    new Vector2(0.55f, 0.1f), new Vector2(0.98f, 0.9f), Vector2.zero, Vector2.zero,
                    new Color(0.2f, 0.2f, 0.2f, 0.6f), 14, () => {});
            }
        }
    }

    private void GenerateShopPool()
    {
        shopPool.Clear();
        int dungeonLevel = (RuntimePlayerData.Instance?.HighestStage ?? 0) + 1;
        for (int i = 0; i < ShopPoolSize; i++)
            shopPool.Add(EquipmentItem.GenerateShopItem(dungeonLevel));
        currentPage = 0;
    }

    private void RefreshShop()
    {
        var listArea = panel.transform.Find("ListArea");
        if (listArea != null)
        {
            var remove = new List<GameObject>();
            foreach (Transform child in listArea) remove.Add(child.gameObject);
            foreach (var go in remove) Destroy(go);
        }
        shopItemButtons.Clear();
        currentShopItems.Clear();

        Font font = GameManager.GetUIFont();
        shopPageCount = Mathf.Max(1, Mathf.CeilToInt(shopPool.Count / (float)ShopItemCount));
        currentPage = Mathf.Clamp(currentPage, 0, shopPageCount - 1);
        int startIndex = currentPage * ShopItemCount;
        Transform parent = listArea != null ? listArea : panel.transform;

        int itemsPerPanel = ShopItemCount / 2;
        string[] panelTitles = { "装备列表", "推荐装备" };
        float titleH = 28f, gap = 5f, topPad = 4f;
        // 动态计算行高，填满面板区域
        float panelHeight = (listArea != null ? listArea.GetComponent<RectTransform>().rect.height : 800f);
        float availH = panelHeight - titleH - topPad - gap * (itemsPerPanel - 1);
        float rowH = Mathf.Max(availH / itemsPerPanel, 70f);

        for (int p = 0; p < 2; p++)
        {
            // 面板容器 + 发光边框
            var panelObj = UIHelper.MakeGlowCard(parent, $"Panel_{p}",
                new Vector2(p == 0 ? 0.005f : 0.505f, 0), new Vector2(p == 0 ? 0.495f : 0.995f, 1),
                Vector2.zero, Vector2.zero,
                new Color(0.04f, 0.03f, 0.06f, 0.7f),
                UIHelper.GlowBottom, UIHelper.Accent);

            // 面板标题栏
            var titleObj = new GameObject("PanelTitle");
            titleObj.transform.SetParent(panelObj.transform, false);
            var tR = titleObj.AddComponent<RectTransform>();
            tR.anchorMin = new Vector2(0, 1); tR.anchorMax = new Vector2(1, 1);
            tR.pivot = new Vector2(0.5f, 1);
            tR.anchoredPosition = new Vector2(0, -4);
            tR.sizeDelta = new Vector2(0, 24);
            var titleBg = titleObj.AddComponent<Image>();
            titleBg.color = new Color(0.12f, 0.08f, 0.18f, 0.9f);
            var titleTxt = new GameObject("Txt");
            titleTxt.transform.SetParent(titleObj.transform, false);
            var ttR = titleTxt.AddComponent<RectTransform>();
            ttR.anchorMin = Vector2.zero; ttR.anchorMax = Vector2.one;
            ttR.offsetMin = Vector2.zero; ttR.offsetMax = Vector2.zero;
            var tTxt = titleTxt.AddComponent<Text>();
            tTxt.text = panelTitles[p];
            tTxt.alignment = TextAnchor.MiddleCenter;
            tTxt.fontSize = 16; tTxt.color = UIHelper.Accent; tTxt.font = font;
            tTxt.raycastTarget = false;

            for (int i = 0; i < itemsPerPanel; i++)
            {
                int actualIndex = startIndex + p * itemsPerPanel + i;
                if (actualIndex >= shopPool.Count) break;
                var item = shopPool[actualIndex];
                currentShopItems.Add(item);
                int idx = currentShopItems.Count - 1;
                float y = -(titleH + topPad + i * (rowH + gap));

                var rc = item.Rarity;
                var rcColor = ItemData.GetRarityColor(rc);

                // 行卡片
                var row = UIHelper.MakeGlowCard(panelObj.transform, $"Item_{idx}",
                    new Vector2(0.015f, 1), new Vector2(0.985f, 1),
                    new Vector2(0, y - rowH), new Vector2(0, y),
                    new Color(rcColor.r * 0.06f, rcColor.g * 0.06f, rcColor.b * 0.06f, 0.85f),
                    new Color(rcColor.r * 0.02f, rcColor.g * 0.02f, rcColor.b * 0.02f, 0.85f),
                    rcColor * 0.5f);

                // 左侧稀有度色条
                var barObj = new GameObject("RarityBar");
                barObj.transform.SetParent(row.transform, false);
                var barR = barObj.AddComponent<RectTransform>();
                barR.anchorMin = new Vector2(0, 0); barR.anchorMax = new Vector2(0, 1);
                barR.pivot = new Vector2(0, 0.5f);
                barR.sizeDelta = new Vector2(3, 0);
                var barImg = barObj.AddComponent<Image>();
                barImg.color = rcColor;

                // 图标框 (左 ~12%)
                var iconObj = new GameObject("Icon");
                iconObj.transform.SetParent(row.transform, false);
                var iconR = iconObj.AddComponent<RectTransform>();
                iconR.anchorMin = new Vector2(0.02f, 0.12f); iconR.anchorMax = new Vector2(0.13f, 0.88f);
                iconR.offsetMin = Vector2.zero; iconR.offsetMax = Vector2.zero;
                var iconImg = iconObj.AddComponent<Image>();
                iconImg.color = new Color(rcColor.r * 0.15f, rcColor.g * 0.15f, rcColor.b * 0.15f, 0.6f);
                var iconTxt = new GameObject("IconTxt");
                iconTxt.transform.SetParent(iconObj.transform, false);
                var iR = iconTxt.AddComponent<RectTransform>();
                iR.anchorMin = Vector2.zero; iR.anchorMax = Vector2.one;
                    iR.offsetMin = Vector2.zero; iR.offsetMax = Vector2.zero;
                var iTxt = iconTxt.AddComponent<Text>();
                iTxt.text = SlotLabel(item.SlotType);
                iTxt.alignment = TextAnchor.MiddleCenter; iTxt.fontSize = 13;
                iTxt.color = rcColor; iTxt.font = font; iTxt.raycastTarget = false;

                // 名称+属性 (中间 ~55%)
                var nameObj = new GameObject("Name");
                nameObj.transform.SetParent(row.transform, false);
                var nR = nameObj.AddComponent<RectTransform>();
                nR.anchorMin = new Vector2(0.15f, 0.06f); nR.anchorMax = new Vector2(0.68f, 0.94f);
                nR.offsetMin = Vector2.zero; nR.offsetMax = Vector2.zero;
                var nTxt = nameObj.AddComponent<Text>();
                nTxt.text = $"<size=16>{item.Name}</size>\n<size=13><color=#aaa>{item.GetStatSummary()}</color></size>";
                nTxt.alignment = TextAnchor.MiddleLeft; nTxt.fontSize = 16;
                nTxt.color = rcColor; nTxt.font = font;
                nTxt.supportRichText = true; nTxt.raycastTarget = false;

                // 价格 (右上方)
                var priceObj = new GameObject("Price");
                priceObj.transform.SetParent(row.transform, false);
                var pR = priceObj.AddComponent<RectTransform>();
                pR.anchorMin = new Vector2(0.70f, 0.52f); pR.anchorMax = new Vector2(0.98f, 0.92f);
                pR.offsetMin = Vector2.zero; pR.offsetMax = Vector2.zero;
                var pTxt = priceObj.AddComponent<Text>();
                pTxt.text = UIStringBuilderPool.FormatNumber(item.SellPrice * 2);
                pTxt.alignment = TextAnchor.MiddleCenter; pTxt.fontSize = 15;
                pTxt.color = UIHelper.Accent; pTxt.font = font; pTxt.raycastTarget = false;

                // 购买按钮 (右下方)
                UIHelper.MakeButton(row.transform, "BuyBtn", "购买", font,
                    new Vector2(0.70f, 0.08f), new Vector2(0.98f, 0.48f),
                    Vector2.zero, Vector2.zero,
                    new Color(0.35f, 0.22f, 0.55f, 0.9f), 16,
                    () => ShowBuyConfirm(idx));

                shopItemButtons.Add(row);
            }
        }

        if (pageText != null)
            pageText.text = shopPool.Count == 0 ? "" : $"{currentPage + 1} / {shopPageCount}";
        TopNavBar.Instance?.RefreshCurrency();
    }

    private void ShowBuyConfirm(int index)
    {
        if (index < 0 || index >= currentShopItems.Count) return;
        pendingBuyIndex = index;
        var item = currentShopItems[index];
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
        it.text = $"{SlotLabel(item.SlotType)} {item.Name}\n<size=15><color=#aaa>{item.GetStatSummary()}</color></size>\n\n<color=#8C55C0>价格 {UIStringBuilderPool.FormatNumber(price)} 金币</color>"
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
        if (pendingBuyIndex < 0 || pendingBuyIndex >= currentShopItems.Count) return;
        BuyItem(pendingBuyIndex);
        pendingBuyIndex = -1;
    }

    private void BuyItem(int index)
    {
        if (index < 0 || index >= currentShopItems.Count) return;
        var player = GameManager.Instance?.Player;
        if (player == null) return;

        int actualIndex = currentPage * ShopItemCount + index;
        if (actualIndex < 0 || actualIndex >= shopPool.Count) return;
        var item = shopPool[actualIndex];
        int price = item.SellPrice * 2;

        // 检查背包是否已满
        if (player.Inventory.Backpack.Count >= EquipmentInventory.MaxBackpackSize)
        {
            // 背包满 — 发送到邮箱
            if (player.Stats.SpendGold(price))
            {
                LocalMailSystem.SendItemMail($"商店购买: {item.Name}", $"背包已满，装备已发送至邮箱。\n{item.GetStatSummary()}", item);
                shopPool.RemoveAt(actualIndex);
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
            shopPool.RemoveAt(actualIndex);
            int dl = (RuntimePlayerData.Instance?.HighestStage ?? 0) + 1;
            shopPool.Add(EquipmentItem.GenerateShopItem(dl));
            RefreshShop();
            player.Stats.Save();
        }
    }

    private void OnDestroy()
    {
        HideConfirmDialog();
        if (panel != null) Destroy(panel);
        if (Instance == this) Instance = null;
    }

    public bool IsVisible() => panel != null && panel.activeSelf;
}
