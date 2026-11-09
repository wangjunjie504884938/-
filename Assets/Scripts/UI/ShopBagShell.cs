using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 商城/背包共用外壳 — 水墨底框 + 商城/背包页签 + 紫X关闭 + 左栏(战斗搭配下拉/角色立绘/9装备槽)
/// 局部坐标 = 1920x1080 设计坐标 - 屏幕中心(960,540)，+y 向上
/// </summary>
public static class ShopBagShell
{
    private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

    public class Refs
    {
        public Font Font;
        public int LoadoutIndex;
        public Text LoadoutLabel;
        public GameObject LoadoutList;
        public Text[] LoadoutItemLabels;
        public GameObject[] EquipSlots;
        public Image[] SlotFrames;
        public Text[] SlotNames;
        public int[] SlotData;
        public System.Action OnEquippedChanged;
    }

    public static Sprite Load(string name)
    {
        if (_cache.TryGetValue(name, out var s) && s != null) return s;
        s = Resources.Load<Sprite>("Sprites/ShopBag/" + name);
        if (s == null) s = Resources.Load<Sprite>("Sprites/CharacterDetail/" + name); // 复用角色详情页素材(如close_btn)
        _cache[name] = s;
        return s;
    }

    /// <summary>稀有度 → 等级物品框素材名</summary>
    public static string FrameName(ItemRarity r)
    {
        switch (r)
        {
            case ItemRarity.Rare: return "Rarity_Blue";
            case ItemRarity.Epic: return "Rarity_Purple";
            case ItemRarity.Legendary: return "Rarity_Gold";
            default: return "Rarity_White";
        }
    }

    public static GameObject MakeSprite(Transform parent, string objName, string spriteName,
        Vector2 localCenter, Vector2 size, bool raycast = false, Color? tint = null)
    {
        var go = new GameObject(objName);
        go.transform.SetParent(parent, false);
        var r = go.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(0.5f, 0.5f); r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = localCenter;
        r.sizeDelta = size;
        var img = go.AddComponent<Image>();
        if (!string.IsNullOrEmpty(spriteName))
        {
            img.sprite = Load(spriteName);
            if (img.sprite == null) img.color = Color.clear; // 素材缺失时防空白方块
        }
        if (tint.HasValue) img.color = tint.Value;
        img.raycastTarget = raycast;
        return go;
    }

    public static Text MakeText(Transform parent, string objName, string content, int fontSize, Color color,
        Font font, Vector2 localCenter, Vector2 size, TextAnchor alignment = TextAnchor.MiddleCenter, bool outline = false)
    {
        var go = new GameObject(objName);
        go.transform.SetParent(parent, false);
        var r = go.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(0.5f, 0.5f); r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = localCenter;
        r.sizeDelta = size;
        var t = go.AddComponent<Text>();
        t.text = content;
        t.alignment = alignment;
        t.fontSize = fontSize;
        t.color = color;
        t.font = font;
        t.raycastTarget = false;
        if (outline)
        {
            var ol = go.AddComponent<Outline>();
            ol.effectColor = new Color(0f, 0f, 0f, 0.85f);
            ol.effectDistance = new Vector2(2f, -2f);
        }
        return t;
    }

    /// <summary>剥掉装备名里的品质前缀（传说·/史诗·/稀有·），品质由稀有度框颜色表达</summary>
    public static string StripRarityPrefix(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        if (name.StartsWith("传说·")) return name.Substring(3);
        if (name.StartsWith("史诗·")) return name.Substring(3);
        if (name.StartsWith("稀有·")) return name.Substring(3);
        return name;
    }

    public static GameObject MakeMenuBtn(Transform parent, string objName, string label, Font font,
        Vector2 center, Vector2 size, Color bg, System.Action onClick, int fontSize = 17)
    {
        var go = MakeSprite(parent, objName, null, center, size, raycast: true);
        go.GetComponent<Image>().color = bg;
        var b = go.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        if (onClick != null) b.onClick.AddListener(() => onClick());
        MakeText(go.transform, objName + "Label", label, fontSize, UIHelper.TextPrimary, font, Vector2.zero, size);
        return go;
    }

    public static void ClearChildren(GameObject root)
    {
        if (root == null) return;
        var list = new List<GameObject>();
        foreach (Transform c in root.transform) list.Add(c.gameObject);
        foreach (var go in list) if (go != null) Object.Destroy(go);
    }

    // ====== 9 装备槽定义（局部坐标）====== 左列5 + 右列4
    private static readonly string[] SlotArt = {
        "SlotEmpty_Helmet", "SlotEmpty_Necklace", "SlotEmpty_Chest", "SlotEmpty_Weapon", "SlotEmpty_Pants",
        "SlotEmpty_Shoulder", "SlotEmpty_Ring", "SlotEmpty_Accessory", "SlotEmpty_Bag"
    };
    private static readonly float[] SlotX = { -396f, -396f, -396f, -396f, -396f, -70f, -70f, -70f, -70f };
    private static readonly float[] SlotY = { 200f, 88f, -24f, -136f, -248f, 200f, 88f, -24f, -136f };
    // 数据槽映射: -1=装饰, 0=Weapon, 1=Armor, 2=Accessory（项链↔饰品, 上衣↔护甲, 武器↔武器）
    private static readonly int[] SlotDataMap = { -1, 2, 1, 0, -1, -1, -1, -1, -1 };

    public static Refs Build(GameObject panel, Font font, bool shopSelected,
        System.Action onClose, System.Action openOtherTab)
    {
        var refs = new Refs
        {
            Font = font,
            LoadoutIndex = PlayerPrefs.GetInt("ShopBagLoadout", 0),
            SlotData = (int[])SlotDataMap.Clone()
        };

        // 全屏微暗遮罩(阻挡面板外点击)
        var dim = new GameObject("ShellDimBG");
        dim.transform.SetParent(panel.transform, false);
        var dimR = dim.AddComponent<RectTransform>();
        dimR.anchorMin = Vector2.zero; dimR.anchorMax = Vector2.one;
        dimR.offsetMin = Vector2.zero; dimR.offsetMax = Vector2.zero;
        var dimImg = dim.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.25f);
        dimImg.raycastTarget = true;

        // 水墨底框
        var frame = MakeSprite(panel.transform, "ShellFrame", "PanelBg", new Vector2(20, -18), new Vector2(1100, 800));
        frame.GetComponent<Image>().raycastTarget = true;

        // === 页签 ===
        Vector2 selCenter = shopSelected ? new Vector2(200, 290) : new Vector2(355, 290);
        Vector2 otherCenter = shopSelected ? new Vector2(355, 290) : new Vector2(200, 290);
        MakeSprite(panel.transform, "ShellTabFrame", "TitleSelFrame", selCenter, new Vector2(150, 40));
        MakeText(panel.transform, "ShellTabSelLabel", shopSelected ? "商城" : "背包", 30, UIHelper.TextPrimary, font,
            selCenter, new Vector2(150, 40));

        var otherBtn = MakeSprite(panel.transform, "ShellTabOtherBtn", null, otherCenter, new Vector2(150, 40), raycast: true, tint: Color.clear);
        var otherBt = otherBtn.AddComponent<Button>();
        otherBt.transition = Selectable.Transition.None;
        otherBt.onClick.AddListener(() => openOtherTab?.Invoke());
        MakeText(otherBtn.transform, "ShellTabOtherLabel", shopSelected ? "背包" : "商城", 30, UIHelper.TextDim, font,
            Vector2.zero, new Vector2(150, 40));

        // === 紫色 X 关闭（复用角色详情页 close_btn 素材，缺失时回退文字 ✕）===
        var closeBtn = MakeSprite(panel.transform, "ShellCloseBtn", "close_btn", new Vector2(532, 382), new Vector2(200, 200), raycast: true);
        var closeBt = closeBtn.AddComponent<Button>();
        closeBt.transition = Selectable.Transition.None;
        closeBt.onClick.AddListener(() => onClose?.Invoke());
        var closeGlyph = MakeText(closeBtn.transform, "ShellCloseGlyph", "✕", 54, new Color(0.80f, 0.56f, 0.98f, 1f), font,
            Vector2.zero, new Vector2(90, 90));
        if (Load("close_btn") != null) closeGlyph.gameObject.SetActive(false);

        // === 战斗搭配下拉 ===
        var box = MakeSprite(panel.transform, "ShellLoadoutBox", "LoadoutFrame", new Vector2(-400, 290), new Vector2(143, 41), raycast: true);
        var boxBtn = box.AddComponent<Button>();
        boxBtn.transition = Selectable.Transition.None;
        refs.LoadoutLabel = MakeText(box.transform, "ShellLoadoutLabel", "", 17, UIHelper.TextPrimary, font,
            Vector2.zero, new Vector2(140, 30));

        var list = new GameObject("ShellLoadoutList");
        list.transform.SetParent(panel.transform, false);
        var listR = list.AddComponent<RectTransform>();
        listR.anchorMin = new Vector2(0.5f, 0.5f); listR.anchorMax = new Vector2(0.5f, 0.5f);
        listR.anchoredPosition = new Vector2(-413, 191);
        listR.sizeDelta = new Vector2(130, 140);
        refs.LoadoutList = list;
        refs.LoadoutItemLabels = new Text[3];
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            var item = MakeSprite(list.transform, "ShellLoadoutItem_" + i, null, new Vector2(0, 48 - 48 * i), new Vector2(126, 40), raycast: true);
            item.GetComponent<Image>().color = new Color(0.10f, 0.07f, 0.16f, 0.96f);
            var b = item.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() =>
            {
                var cands = LoadoutCandidates(refs.LoadoutIndex);
                if (idx < cands.Count)
                {
                    refs.LoadoutIndex = cands[idx];
                    PlayerPrefs.SetInt("ShopBagLoadout", refs.LoadoutIndex);
                }
                refs.LoadoutList.SetActive(false);
                UpdateLoadoutLabel(refs);
            });
            refs.LoadoutItemLabels[i] = MakeText(item.transform, "ShellLoadoutItemTxt_" + i, "", 16, UIHelper.TextSecondary, font,
                Vector2.zero, new Vector2(120, 30));
        }
        boxBtn.onClick.AddListener(() =>
        {
            bool show = !list.activeSelf;
            if (show) RefreshLoadoutItems(refs);
            list.SetActive(show);
        });
        UpdateLoadoutLabel(refs);
        list.SetActive(false);

        // === 角色立绘 ===
        MakeSprite(panel.transform, "ShellCharDisplay", "CharDisplay", new Vector2(-233, -2), new Vector2(194, 458));

        // === 装备槽 ===
        refs.EquipSlots = new GameObject[9];
        refs.SlotFrames = new Image[9];
        refs.SlotNames = new Text[9];
        for (int i = 0; i < 9; i++)
        {
            int si = i;
            var slot = MakeSprite(panel.transform, "ShellEquipSlot_" + i, SlotArt[i],
                new Vector2(SlotX[i], SlotY[i]), new Vector2(104, 104), raycast: true);
            var b = slot.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => OnSlotClicked(si, refs));

            var frameGo = new GameObject("ShellSlotFrame_" + i);
            frameGo.transform.SetParent(slot.transform, false);
            var fr = frameGo.AddComponent<RectTransform>();
            fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one;
            fr.offsetMin = Vector2.zero; fr.offsetMax = Vector2.zero;
            var fImg = frameGo.AddComponent<Image>();
            fImg.raycastTarget = false;
            frameGo.SetActive(false);
            refs.EquipSlots[i] = slot;
            refs.SlotFrames[i] = fImg;
            refs.SlotNames[i] = MakeText(slot.transform, "ShellSlotName_" + i, "", 14, UIHelper.TextPrimary, font,
                new Vector2(0, -66), new Vector2(130, 26));
            refs.SlotNames[i].gameObject.SetActive(false);
        }
        RefreshEquipSlots(refs);
        return refs;
    }

    private static List<int> LoadoutCandidates(int current)
    {
        var l = new List<int>();
        for (int i = 0; i < 4; i++) if (i != current) l.Add(i);
        return l;
    }

    private static void RefreshLoadoutItems(Refs refs)
    {
        var cands = LoadoutCandidates(refs.LoadoutIndex);
        for (int i = 0; i < 3 && i < cands.Count; i++)
            refs.LoadoutItemLabels[i].text = "战斗搭配" + (cands[i] + 1);
    }

    private static void UpdateLoadoutLabel(Refs refs)
    {
        if (refs.LoadoutLabel != null)
            refs.LoadoutLabel.text = "战斗搭配" + (refs.LoadoutIndex + 1) + " ▾";
    }

    public static void RefreshEquipSlots(Refs refs)
    {
        if (refs == null || refs.SlotFrames == null) return;
        var inv = GameManager.Instance != null ? GameManager.Instance.Player?.Inventory : null;
        for (int i = 0; i < 9; i++)
        {
            int ds = refs.SlotData[i];
            if (ds < 0) continue;
            var eq = (inv != null && ds < inv.Equipped.Count) ? inv.Equipped[ds] : null;
            var item = eq != null ? eq.Item : null;
            if (item != null)
            {
                refs.SlotFrames[i].sprite = Load(FrameName(item.Rarity));
                refs.SlotFrames[i].color = Color.white;
                refs.SlotFrames[i].gameObject.SetActive(true);
                string lvl = item.UpgradeLevel > 0 ? "+" + item.UpgradeLevel : "";
                refs.SlotNames[i].text = StripRarityPrefix(item.Name) + lvl;
                refs.SlotNames[i].color = ItemData.GetRarityColor(item.Rarity);
                refs.SlotNames[i].gameObject.SetActive(true);
            }
            else
            {
                refs.SlotFrames[i].gameObject.SetActive(false);
                refs.SlotNames[i].gameObject.SetActive(false);
            }
        }
    }

    // ====== 已装备槽点击菜单: 卸下 / 强化 / 取消 ======
    private static GameObject _slotMenu;

    public static void CloseSlotMenu()
    {
        if (_slotMenu != null) { Object.Destroy(_slotMenu); _slotMenu = null; }
    }

    private static void OnSlotClicked(int slotIndex, Refs refs)
    {
        int ds = refs.SlotData[slotIndex];
        if (ds < 0) return;
        var inv = GameManager.Instance != null ? GameManager.Instance.Player?.Inventory : null;
        if (inv == null || ds >= inv.Equipped.Count) return;
        var eq = inv.Equipped[ds];
        if (eq == null || eq.IsEmpty)
        {
            if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("提示", "该栏位暂无装备");
            return;
        }
        var item = eq.Item;
        CloseSlotMenu();

        Font font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();

        _slotMenu = new GameObject("ShopBagSlotMenu");
        _slotMenu.transform.SetParent(canvas.transform, false);
        var dr = _slotMenu.AddComponent<RectTransform>();
        dr.anchorMin = Vector2.zero; dr.anchorMax = Vector2.one;
        dr.offsetMin = Vector2.zero; dr.offsetMax = Vector2.zero;
        var db = _slotMenu.AddComponent<Image>();
        db.color = new Color(0, 0, 0, 0.6f);
        var dbBtn = _slotMenu.AddComponent<Button>();
        dbBtn.transition = Selectable.Transition.None;
        dbBtn.onClick.AddListener(CloseSlotMenu);

        var box = MakeSprite(_slotMenu.transform, "SlotMenuBox", null, Vector2.zero, new Vector2(460, 300));
        box.GetComponent<Image>().color = UIHelper.BgPanel;

        MakeText(box.transform, "SlotMenuTitle", "已装备", 24, UIHelper.Accent, font,
            new Vector2(0, 118), new Vector2(400, 34));
        int cost = item.GetUpgradeCost();
        bool canUp = item.UpgradeLevel < EquipmentItem.MaxUpgradeLevel;
        MakeText(box.transform, "SlotMenuItem",
            $"{StripRarityPrefix(item.Name)}{(item.UpgradeLevel > 0 ? " <color=#C088FF>+" + item.UpgradeLevel + "</color>" : "")}\n<size=15><color=#aaa>{item.GetStatSummary()}</color></size>\n<color=#C088FF>强化费用 {cost} 金币</color>",
            18, ItemData.GetRarityColor(item.Rarity), font, new Vector2(0, 45), new Vector2(420, 100));

        MakeMenuBtn(box.transform, "SlotMenuUnequipBtn", "卸 下", font, new Vector2(-140, -78), new Vector2(120, 48),
            UIHelper.BtnUnequip, () =>
            {
                var invNow = GameManager.Instance != null ? GameManager.Instance.Player?.Inventory : null;
                if (invNow == null) return;
                if (invNow.Backpack.Count >= EquipmentInventory.MaxBackpackSize)
                {
                    if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("提示", "背包已满，无法卸下装备");
                    return;
                }
                invNow.Unequip((EquipSlotType)ds);
                GameManager.Instance.Player.Stats.Save();
                CloseSlotMenu();
                RefreshEquipSlots(refs);
                refs.OnEquippedChanged?.Invoke();
            });
        MakeMenuBtn(box.transform, "SlotMenuUpgradeBtn", canUp ? "强 化" : "已满级", font, new Vector2(0, -78), new Vector2(120, 48),
            canUp ? UIHelper.BtnUpgrade : new Color(0.2f, 0.2f, 0.2f, 0.6f), () =>
            {
                if (!canUp) return;
                var p = GameManager.Instance != null ? GameManager.Instance.Player : null;
                if (p == null) return;
                if (p.Stats.Gold < cost)
                {
                    if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("提示", "金币不足");
                    return;
                }
                p.Stats.SpendGold(cost);
                item.Upgrade();
                inv.RecalculateStats();
                p.Stats.Save();
                if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("强化成功", $"{item.Name} +{item.UpgradeLevel}");
                CloseSlotMenu();
                RefreshEquipSlots(refs);
                refs.OnEquippedChanged?.Invoke();
            });
        MakeMenuBtn(box.transform, "SlotMenuCancelBtn", "取 消", font, new Vector2(140, -78), new Vector2(120, 48),
            new Color(0.10f, 0.07f, 0.14f, 0.9f), CloseSlotMenu);
    }
}
