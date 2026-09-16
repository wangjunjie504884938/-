using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 碎片兑换商店 — 使用分解装备获得的碎片兑换物品
/// 商品: 随机装备箱(4档稀有度)、金币包(3档)、技能点、随机符文
/// </summary>
public class FragmentShopUI : MonoBehaviour
{
    public static FragmentShopUI Instance { get; private set; }

    private GameObject panel;
    private Text fragmentText;
    private Coroutine showRoutine;

    private static readonly Color FragColor = new Color(0.4f, 0.8f, 1f, 1f);

    // === 商品定义 ===
    private struct ShopItem
    {
        public string name;
        public string desc;
        public string icon;
        public int fragCost;
        public Color iconColor;
    }

    private static readonly ShopItem[] Items =
    {
        // 装备箱
        new ShopItem { name = "普通装备箱", desc = "随机一件普通装备", icon = "", fragCost = 5, iconColor = UIHelper.RarityCommon },
        new ShopItem { name = "稀有装备箱", desc = "随机一件稀有装备", icon = "", fragCost = 15, iconColor = UIHelper.RarityRare },
        new ShopItem { name = "史诗装备箱", desc = "随机一件史诗装备", icon = "", fragCost = 40, iconColor = UIHelper.RarityEpic },
        new ShopItem { name = "传说装备箱", desc = "随机一件传说装备", icon = "", fragCost = 100, iconColor = UIHelper.RarityLegendary },
        // 金币包
        new ShopItem { name = "金币包(100)", desc = "获得100金币", icon = "", fragCost = 3, iconColor = new Color(0.85f, 0.7f, 0.2f) },
        new ShopItem { name = "金币包(500)", desc = "获得500金币", icon = "", fragCost = 12, iconColor = new Color(0.85f, 0.7f, 0.2f) },
        new ShopItem { name = "金币包(2000)", desc = "获得2000金币", icon = "", fragCost = 40, iconColor = new Color(0.85f, 0.7f, 0.2f) },
        // 技能点
        new ShopItem { name = "技能点", desc = "获得1点技能点", icon = "", fragCost = 30, iconColor = new Color(0.3f, 0.5f, 0.9f) },
        // 药水补给
        new ShopItem { name = "药水补给", desc = "恢复全部药水(5瓶)", icon = "", fragCost = 10, iconColor = new Color(0.3f, 0.8f, 0.4f) },
    };

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Show()
    {
        if (panel == null) BuildUI();
        UIHelper.SetupTopBar("碎片商店", () => { Hide(); UIHelper.EnsureUIManager(); UIManager.Instance.ShowHub(); });
        RefreshInfo();
        panel.SetActive(true);
        if (showRoutine != null) StopCoroutine(showRoutine);
        showRoutine = StartCoroutine(UIHelper.FadeIn(panel, 0.2f));
    }

    public void Hide()
    {
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

        panel = UiPrefabLoader.TryLoad("FragmentShopPanel", canvas);
        if (panel == null)
        panel = new GameObject("FragmentShopPanel");
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

        // 碎片余额(右上)
        var fragObj = new GameObject("FragText");
        fragObj.transform.SetParent(panel.transform, false);
        var fr = fragObj.AddComponent<RectTransform>();
        fr.anchorMin = new Vector2(1, 1); fr.anchorMax = new Vector2(1, 1);
        fr.pivot = new Vector2(1, 1);
        fr.anchoredPosition = new Vector2(-20, -8);
        fr.sizeDelta = new Vector2(250, 40);
        fragmentText = fragObj.AddComponent<Text>();
        fragmentText.alignment = TextAnchor.MiddleRight;
        fragmentText.fontSize = 22; fragmentText.color = FragColor; fragmentText.font = font;

        // 说明文字
        UIHelper.MakeSubtitle(panel.transform, "分解装备获得碎片 · 兑换稀有资源", font, 100, 16);

        // === 商品网格 (3列) ===
        int cols = 3;
        float cardW = 250f, cardH = 130f, gapX = 14f, gapY = 14f;
        float colSpacing = cardW + gapX;
        float startX = -((cols - 1) * colSpacing) / 2f;

        for (int i = 0; i < Items.Length; i++)
        {
            int col = i % cols;
            int row = i / cols;
            float xPos = startX + col * colSpacing;
            float yPos = -361 - row * (cardH + gapY);

            var item = Items[i];
            var card = UIHelper.MakeGlowCard(panel.transform, $"Item_{i}",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, Vector2.zero,
                new Color(0.05f, 0.04f, 0.08f, 0.92f),
                UIHelper.GlowBottom,
                item.iconColor);
            var cardRt = card.GetComponent<RectTransform>();
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.anchoredPosition = new Vector2(xPos, yPos);
            cardRt.sizeDelta = new Vector2(cardW, cardH);
            card.AddComponent<CardHoverEffect>();

            // 图标 — 和Boss图鉴一样的定位方式
            var iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(card.transform, false);
            var ir = iconObj.AddComponent<RectTransform>();
            ir.anchorMin = new Vector2(0, 0.5f); ir.anchorMax = new Vector2(0, 0.5f);
            ir.pivot = new Vector2(0.5f, 0.5f);
            ir.anchoredPosition = new Vector2(40, 5);
            ir.sizeDelta = new Vector2(55, 55);
            var iconTxt = iconObj.AddComponent<Text>();
            iconTxt.text = item.icon; iconTxt.alignment = TextAnchor.MiddleCenter;
            iconTxt.fontSize = 32; iconTxt.color = item.iconColor; iconTxt.font = font;
            iconTxt.raycastTarget = false;

            // 名称 — 和Boss图鉴一样的anchor
            var nameObj = new GameObject("Name");
            nameObj.transform.SetParent(card.transform, false);
            var nr = nameObj.AddComponent<RectTransform>();
            nr.anchorMin = new Vector2(0.18f, 0.55f); nr.anchorMax = new Vector2(0.98f, 0.92f);
            nr.offsetMin = Vector2.zero; nr.offsetMax = Vector2.zero;
            var nameTxt = nameObj.AddComponent<Text>();
            nameTxt.text = item.name; nameTxt.alignment = TextAnchor.MiddleLeft;
            nameTxt.fontSize = 20; nameTxt.color = UIHelper.TextPrimary; nameTxt.font = font;
            nameTxt.raycastTarget = false;

            // 描述
            var descObj = new GameObject("Desc");
            descObj.transform.SetParent(card.transform, false);
            var dr2 = descObj.AddComponent<RectTransform>();
            dr2.anchorMin = new Vector2(0.22f, 0.30f); dr2.anchorMax = new Vector2(0.98f, 0.55f);
            dr2.offsetMin = Vector2.zero; dr2.offsetMax = Vector2.zero;
            var descTxt = descObj.AddComponent<Text>();
            descTxt.text = item.desc; descTxt.alignment = TextAnchor.MiddleLeft;
            descTxt.fontSize = 13; descTxt.color = UIHelper.TextSecondary; descTxt.font = font;
            descTxt.raycastTarget = false;

            // 价格标签
            var costObj = new GameObject("Cost");
            costObj.transform.SetParent(card.transform, false);
            var cr2 = costObj.AddComponent<RectTransform>();
            cr2.anchorMin = new Vector2(0.22f, 0.08f); cr2.anchorMax = new Vector2(0.6f, 0.30f);
            cr2.offsetMin = Vector2.zero; cr2.offsetMax = Vector2.zero;
            var costTxt = costObj.AddComponent<Text>();
            costTxt.text = $"{item.fragCost}"; costTxt.alignment = TextAnchor.MiddleLeft;
            costTxt.fontSize = 16; costTxt.color = FragColor; costTxt.font = font;
            costTxt.raycastTarget = false;

            // 购买按钮
            var buyBtnObj = new GameObject("BuyBtn");
            buyBtnObj.transform.SetParent(card.transform, false);
            var br = buyBtnObj.AddComponent<RectTransform>();
            br.anchorMin = new Vector2(0.62f, 0.1f); br.anchorMax = new Vector2(0.96f, 0.35f);
            br.offsetMin = Vector2.zero; br.offsetMax = Vector2.zero;
            var buyImg = buyBtnObj.AddComponent<Image>();
            buyImg.color = UIHelper.BtnConfirm;
            var buyBtn = buyBtnObj.AddComponent<Button>();
            var buyLbl = new GameObject("Lbl");
            buyLbl.transform.SetParent(buyBtnObj.transform, false);
            var blr = buyLbl.AddComponent<RectTransform>();
            blr.anchorMin = Vector2.zero; blr.anchorMax = Vector2.one;
            blr.offsetMin = Vector2.zero; blr.offsetMax = Vector2.zero;
            var buyTxt = buyLbl.AddComponent<Text>();
            buyTxt.text = "兑换"; buyTxt.alignment = TextAnchor.MiddleCenter;
            buyTxt.fontSize = 16; buyTxt.color = UIHelper.TextPrimary; buyTxt.font = font;
            buyTxt.raycastTarget = false;

            int idx = i;
            buyBtn.onClick.AddListener(() => OnBuy(idx));
        }

        // 底部提示
        var hintObj = new GameObject("Hint");
        hintObj.transform.SetParent(panel.transform, false);
        var hr2 = hintObj.AddComponent<RectTransform>();
        hr2.anchorMin = new Vector2(0, 0.02f); hr2.anchorMax = new Vector2(1, 0.08f);
        hr2.offsetMin = Vector2.zero; hr2.offsetMax = Vector2.zero;
        var hintTxt = hintObj.AddComponent<Text>();
        hintTxt.text = "碎片可通过分解背包中的装备获得 (背包 → 选择装备 → 分解)";
        hintTxt.alignment = TextAnchor.MiddleCenter;
        hintTxt.fontSize = 14; hintTxt.color = UIHelper.TextDim; hintTxt.font = font;
        hintTxt.raycastTarget = false;

        panel.SetActive(false);
    }

    private void RefreshInfo()
    {
        int frags = DismantleSystem.Fragments;
        if (fragmentText != null)
            fragmentText.text = $"碎片: {UIStringBuilderPool.FormatNumber(frags)}";

        // 更新按钮可用状态
        for (int i = 0; i < Items.Length; i++)
        {
            var btn = panel?.transform.Find($"Item_{i}/BuyBtn")?.GetComponent<Button>();
            if (btn != null)
                btn.interactable = frags >= Items[i].fragCost;
        }
    }

    private void OnBuy(int index)
    {
        var item = Items[index];
        int currentFrags = DismantleSystem.Fragments;

        if (currentFrags < item.fragCost)
        {
            VFXHelper.SpawnDamageNumber(Vector3.zero, 0, false, Color.red);
            return;
        }

        // 扣除碎片
        DismantleSystem.Fragments = currentFrags - item.fragCost;

        // 发放奖励
        var player = GameManager.Instance?.Player;
        if (player == null) return;

        switch (index)
        {
            case 0: // 普通装备箱
                GiveEquipment(ItemRarity.Common);
                break;
            case 1: // 稀有装备箱
                GiveEquipment(ItemRarity.Rare);
                break;
            case 2: // 史诗装备箱
                GiveEquipment(ItemRarity.Epic);
                break;
            case 3: // 传说装备箱
                GiveEquipment(ItemRarity.Legendary);
                break;
            case 4: // 金币包100
                player.Stats.AddGold(100);
                break;
            case 5: // 金币包500
                player.Stats.AddGold(500);
                break;
            case 6: // 金币包2000
                player.Stats.AddGold(2000);
                break;
            case 7: // 技能点
                player.Stats.SkillPoints += 1;
                break;
            case 8: // 药水补给
                var runtime = player.Stats.Runtime;
                runtime.Potions = PlayerRuntimeStats.MaxPotions;
                break;
        }

        RefreshInfo();
    }

    private void GiveEquipment(ItemRarity rarity)
    {
        var player = GameManager.Instance?.Player;
        if (player == null) return;

        int dungeonLevel = GameManager.Instance.DungeonLevel;
        var item = EquipmentItem.Generate(
            (EquipSlotType)Random.Range(0, 3), rarity, dungeonLevel);
        player.Inventory.Backpack.Add(item);

                    GameLog.Log($"[FragmentShop] 获得装备: {item.Name} ({rarity})");
    }

    private void OnDestroy()
    {
        if (panel != null) Destroy(panel);
        if (Instance == this) Instance = null;
    }
}
