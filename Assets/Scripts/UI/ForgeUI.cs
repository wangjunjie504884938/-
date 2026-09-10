using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 装备熔炉 — 点击空槽弹出装备选择面板(分页)，选3件同稀有度合成
/// </summary>
public class ForgeUI : MonoBehaviour
{
    public static ForgeUI Instance { get; private set; }

    private GameObject panel;
    private GameObject _pickPopup;
    private Coroutine showRoutine;
    private readonly List<int> selectedIndices = new();
    private int _pickPage;
    private const int ItemsPerPage = 8;
    private Button _forgeBtn;
    private Text _forgeBtnTxt;
    private Text _resultTxt;

    private static readonly string[] RarityNames = { "普通", "稀有", "史诗", "传说" };
    private static readonly Color[] RarityColors = { UIHelper.RarityCommon, UIHelper.RarityRare, UIHelper.RarityEpic, UIHelper.RarityLegendary };

    private void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; }

    public void Show()
    {
        if (panel != null) { Destroy(panel); panel = null; }
        selectedIndices.Clear();
        BuildUI();
        UIHelper.SetupTopBar("装备熔炉", () => { Hide(); UIHelper.EnsureUIManager(); UIManager.Instance.ShowHub(); });
        panel.SetActive(true);
        if (showRoutine != null) StopCoroutine(showRoutine);
        showRoutine = StartCoroutine(UIHelper.FadeIn(panel, 0.2f));
    }

    public void Hide()
    {
        if (panel != null) { if (showRoutine != null) StopCoroutine(showRoutine); showRoutine = StartCoroutine(UIHelper.FadeOut(panel, 0.15f)); }
    }

    private void BuildUI()
    {
        Font font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();

        panel = new GameObject("ForgePanel");
        panel.transform.SetParent(canvas.transform, false);
        RectTransform pr = panel.AddComponent<RectTransform>();
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one; pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;
        panel.AddComponent<CanvasGroup>();

        var bg = new GameObject("BG"); bg.transform.SetParent(panel.transform, false);
        var bgr = bg.AddComponent<RectTransform>(); bgr.anchorMin = Vector2.zero; bgr.anchorMax = Vector2.one; bgr.offsetMin = Vector2.zero; bgr.offsetMax = Vector2.zero;
        bg.AddComponent<Image>().color = UIHelper.BgDark;

        UIHelper.MakeSubtitle(panel.transform, "3件同稀有度 → 1件更高稀有度", font, 110, 16);

        // 说明
        var infoObj = new GameObject("Info"); infoObj.transform.SetParent(panel.transform, false);
        var ir = infoObj.AddComponent<RectTransform>(); ir.anchorMin = new Vector2(0.1f, 0.80f); ir.anchorMax = new Vector2(0.9f, 0.88f); ir.offsetMin = Vector2.zero; ir.offsetMax = Vector2.zero;
        var infoTxt = infoObj.AddComponent<Text>();
        infoTxt.text = "点击下方空槽选择装备 · 普通×3→稀有 · 稀有×3→史诗 · 史诗×3→传说";
        infoTxt.alignment = TextAnchor.MiddleCenter; infoTxt.fontSize = 15; infoTxt.color = UIHelper.TextSecondary; infoTxt.font = font; infoTxt.supportRichText = true; infoTxt.raycastTarget = false;

        // 3个选择槽
        for (int i = 0; i < 3; i++)
        {
            int slotIdx = i;
            var slotObj = new GameObject($"Slot_{i}"); slotObj.transform.SetParent(panel.transform, false);
            var sr = slotObj.AddComponent<RectTransform>();
            float x = -160 + i * 160;
            sr.anchorMin = new Vector2(0.5f, 0.55f); sr.anchorMax = new Vector2(0.5f, 0.55f);
            sr.pivot = new Vector2(0.5f, 0.5f); sr.anchoredPosition = new Vector2(x, 0); sr.sizeDelta = new Vector2(150, 120);
            var slotImg = slotObj.AddComponent<Image>(); slotImg.color = new Color(0.06f, 0.04f, 0.10f, 0.92f);
            var slotBtn = slotObj.AddComponent<Button>();
            slotBtn.onClick.AddListener(() => OpenPickPopup(slotIdx, font));

            var slotTxt = new GameObject("Lbl"); slotTxt.transform.SetParent(slotObj.transform, false);
            var lr = slotTxt.AddComponent<RectTransform>(); lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;
            var t = slotTxt.AddComponent<Text>(); t.text = "＋ 选择\n装备"; t.alignment = TextAnchor.MiddleCenter; t.fontSize = 18; t.color = UIHelper.TextDim; t.font = font; t.raycastTarget = false;
        }

        // 结果预览
        var resultObj = new GameObject("Result"); resultObj.transform.SetParent(panel.transform, false);
        var rr = resultObj.AddComponent<RectTransform>(); rr.anchorMin = new Vector2(0.25f, 0.22f); rr.anchorMax = new Vector2(0.75f, 0.35f); rr.offsetMin = Vector2.zero; rr.offsetMax = Vector2.zero;
        resultObj.AddComponent<Image>().color = new Color(0.08f, 0.06f, 0.14f, 0.92f);
        _resultTxt = new GameObject("Lbl").AddComponent<Text>();
        _resultTxt.transform.SetParent(resultObj.transform, false);
        var rtr = _resultTxt.GetComponent<RectTransform>(); rtr.anchorMin = Vector2.zero; rtr.anchorMax = Vector2.one; rtr.offsetMin = Vector2.zero; rtr.offsetMax = Vector2.zero;
        _resultTxt.text = "→ 放入3件装备预览结果"; _resultTxt.alignment = TextAnchor.MiddleCenter; _resultTxt.fontSize = 18; _resultTxt.color = UIHelper.TextDim; _resultTxt.font = font; _resultTxt.raycastTarget = false;

        // 熔铸按钮
        var btnObj = new GameObject("ForgeBtn"); btnObj.transform.SetParent(panel.transform, false);
        var br = btnObj.AddComponent<RectTransform>(); br.anchorMin = new Vector2(0.35f, 0.08f); br.anchorMax = new Vector2(0.65f, 0.18f); br.offsetMin = Vector2.zero; br.offsetMax = Vector2.zero;
        btnObj.AddComponent<Image>().color = UIHelper.CardBg;
        _forgeBtn = btnObj.AddComponent<Button>(); _forgeBtn.interactable = false;
        _forgeBtnTxt = new GameObject("Lbl").AddComponent<Text>();
        _forgeBtnTxt.transform.SetParent(btnObj.transform, false);
        var blr = _forgeBtnTxt.GetComponent<RectTransform>(); blr.anchorMin = Vector2.zero; blr.anchorMax = Vector2.one; blr.offsetMin = Vector2.zero; blr.offsetMax = Vector2.zero;
        _forgeBtnTxt.text = "熔 铸"; _forgeBtnTxt.alignment = TextAnchor.MiddleCenter; _forgeBtnTxt.fontSize = 22; _forgeBtnTxt.color = UIHelper.TextDim; _forgeBtnTxt.font = font; _forgeBtnTxt.raycastTarget = false;

        // 熔铸历史记录
        var histObj = new GameObject("History"); histObj.transform.SetParent(panel.transform, false);
        var histR = histObj.AddComponent<RectTransform>(); histR.anchorMin = new Vector2(0.05f, 0.03f); histR.anchorMax = new Vector2(0.95f, 0.06f); histR.offsetMin = Vector2.zero; histR.offsetMax = Vector2.zero;
        var histTxt = histObj.AddComponent<Text>();
        string history = string.Join("  |  ", ForgeData.GetHistory());
        histTxt.text = string.IsNullOrEmpty(history) ? "熔铸记录: 暂无" : history.Replace("\n", "  |  ");
        histTxt.alignment = TextAnchor.MiddleCenter; histTxt.fontSize = 12; histTxt.color = UIHelper.TextDim; histTxt.font = font; histTxt.raycastTarget = false;
        _forgeBtn.onClick.AddListener(TryForge);

        panel.SetActive(false);
    }

    // === 装备选择弹窗 ===
    private void OpenPickPopup(int slotIdx, Font font)
    {
        // 如果该槽已有装备，先取消选择
        if (slotIdx < selectedIndices.Count)
        {
            selectedIndices.RemoveAt(slotIdx);
            UpdateSlots();
            return;
        }
        if (selectedIndices.Count >= 3) return;

        _pickPage = 0;
        ShowPickPopup(font);
    }

    private void ShowPickPopup(Font font)
    {
        if (_pickPopup != null) Destroy(_pickPopup);

        _pickPopup = new GameObject("PickPopup");
        _pickPopup.transform.SetParent(panel.transform, false);
        var pr = _pickPopup.AddComponent<RectTransform>();
        pr.anchorMin = new Vector2(0.1f, 0.1f); pr.anchorMax = new Vector2(0.9f, 0.75f); pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;
        _pickPopup.AddComponent<Image>().color = new Color(0.04f, 0.03f, 0.07f, 0.98f);
        var pc = _pickPopup.AddComponent<Canvas>(); pc.overrideSorting = true; pc.sortingOrder = 10;
        _pickPopup.AddComponent<GraphicRaycaster>();

        // 标题
        var title = AddText(_pickPopup.transform, "Title", 0.05f, 0.92f, 0.95f, 0.99f, "选择装备", 22, UIHelper.Accent, font);

        // 关闭按钮
        var closeBtn = AddPanel(_pickPopup.transform, "Close", 0.85f, 0.92f, 0.95f, 0.99f, UIHelper.BtnDanger);
        var closeBtnComp = closeBtn.AddComponent<Button>();
        closeBtnComp.onClick.AddListener(() => { Destroy(_pickPopup); _pickPopup = null; });
        AddText(closeBtn.transform, "X", 0, 0, 1, 1, "✕", 18, Color.white, font);

        // 装备列表区域
        var listArea = AddPanel(_pickPopup.transform, "ListArea", 0.03f, 0.15f, 0.97f, 0.88f, new Color(0.06f, 0.04f, 0.10f, 0.5f));
        var listTransform = listArea.transform;

        // 分页
        var inv = GameManager.Instance?.Player?.Inventory;
        if (inv == null) return;

        // 已选中的不显示
        var available = new List<(int origIdx, EquipmentItem item)>();
        for (int i = 0; i < inv.Backpack.Count; i++)
        {
            if (selectedIndices.Contains(i)) continue;
            // 只显示与已选相同稀有度的
            if (selectedIndices.Count > 0)
            {
                var first = inv.Backpack[selectedIndices[0]];
                if (first.Rarity != inv.Backpack[i].Rarity) continue;
            }
            available.Add((i, inv.Backpack[i]));
        }

        int totalPages = Mathf.Max(1, Mathf.CeilToInt((float)available.Count / ItemsPerPage));
        _pickPage = Mathf.Clamp(_pickPage, 0, totalPages - 1);
        int start = _pickPage * ItemsPerPage;
        int end = Mathf.Min(start + ItemsPerPage, available.Count);
        int count = end - start;

        float rowH = 55f, gap = 4f;
        float totalH = count * rowH + (count - 1) * gap;
        float startY = totalH / 2f - rowH / 2f;

        for (int i = start; i < end; i++)
        {
            int slot = i - start;
            float y = startY - slot * (rowH + gap);
            var (origIdx, item) = available[i];

            var row = AddPanel(listTransform, $"Row_{i}", 0.01f, 0.5f, 0.99f, 0.5f, new Color(0.06f, 0.04f, 0.08f, 0.92f));
            var rowR = row.GetComponent<RectTransform>();
            rowR.anchoredPosition = new Vector2(0, y); rowR.sizeDelta = new Vector2(0, rowH);

            var nameTxt = AddText(row.transform, "Name", 0.02f, 0, 0.65f, 1, $"{RarityNames[(int)item.Rarity]} {item.Name}", 15, RarityColors[(int)item.Rarity], font);
            nameTxt.alignment = TextAnchor.MiddleLeft;
            var statTxt = AddText(row.transform, "Stat", 0.65f, 0, 0.95f, 1, item.GetStatSummary(), 12, UIHelper.TextDim, font);
            statTxt.alignment = TextAnchor.MiddleRight;

            var btn = row.AddComponent<Button>();
            int idx = origIdx;
            btn.onClick.AddListener(() => { selectedIndices.Add(idx); Destroy(_pickPopup); _pickPopup = null; UpdateSlots(); });
        }

        // 翻页
        var pageRow = AddPanel(_pickPopup.transform, "PageRow", 0.1f, 0.02f, 0.9f, 0.12f, new Color(0.04f, 0.03f, 0.07f, 0.5f));
        UIHelper.MakeButton(pageRow.transform, "Prev", "◀ 上一页", font, new Vector2(0, 0), new Vector2(0.3f, 1), Vector2.zero, Vector2.zero, new Color(0.08f, 0.08f, 0.12f, 0.9f), 16, () => { if (_pickPage > 0) { _pickPage--; ShowPickPopup(font); } });
        AddText(pageRow.transform, "Page", 0.3f, 0, 0.7f, 1, $"{_pickPage + 1} / {totalPages}", 16, UIHelper.TextSecondary, font);
        UIHelper.MakeButton(pageRow.transform, "Next", "下一页 ▶", font, new Vector2(0.7f, 0), new Vector2(1f, 1), Vector2.zero, Vector2.zero, new Color(0.08f, 0.08f, 0.12f, 0.9f), 16, () => { if (_pickPage < totalPages - 1) { _pickPage++; ShowPickPopup(font); } });
    }

    private void UpdateSlots()
    {
        var inv = GameManager.Instance?.Player?.Inventory;
        if (inv == null) return;

        for (int i = 0; i < 3; i++)
        {
            var slot = panel.transform.Find($"Slot_{i}");
            if (slot == null) continue;
            var txt = slot.Find("Lbl")?.GetComponent<Text>();
            var img = slot.GetComponent<Image>();

            if (i < selectedIndices.Count)
            {
                var item = inv.Backpack[selectedIndices[i]];
                if (txt != null) { txt.text = $"{RarityNames[(int)item.Rarity]}\n{item.Name}"; txt.color = RarityColors[(int)item.Rarity]; txt.fontSize = 14; }
                if (img != null) img.color = new Color(0.08f, 0.12f, 0.06f, 0.95f);
            }
            else
            {
                if (txt != null) { txt.text = "＋ 选择\n装备"; txt.color = UIHelper.TextDim; txt.fontSize = 18; }
                if (img != null) img.color = new Color(0.06f, 0.04f, 0.10f, 0.92f);
            }
        }

        if (selectedIndices.Count == 3)
        {
            var item = inv.Backpack[selectedIndices[0]];
            ItemRarity resultRarity = item.Rarity switch { ItemRarity.Common => ItemRarity.Rare, ItemRarity.Rare => ItemRarity.Epic, _ => ItemRarity.Legendary };
            _resultTxt.text = $"→ <color=#{ColorUtility.ToHtmlStringRGBA(RarityColors[(int)resultRarity])}>{RarityNames[(int)resultRarity]}装备</color>";
            _resultTxt.color = UIHelper.TextPrimary;
            _forgeBtn.interactable = true;
            _forgeBtnTxt.text = "熔 铸! (◆500金)"; _forgeBtnTxt.color = UIHelper.Accent;
        }
        else
        {
            _resultTxt.text = $"→ 已选 {selectedIndices.Count}/3"; _resultTxt.color = UIHelper.TextDim;
            _forgeBtn.interactable = false;
            _forgeBtnTxt.text = "熔 铸"; _forgeBtnTxt.color = UIHelper.TextDim;
        }
    }

    private void TryForge()
    {
        var inv = GameManager.Instance?.Player?.Inventory;
        if (inv == null || selectedIndices.Count != 3) return;

        var item = inv.Backpack[selectedIndices[0]];
        ItemRarity resultRarity = item.Rarity switch { ItemRarity.Common => ItemRarity.Rare, ItemRarity.Rare => ItemRarity.Epic, _ => ItemRarity.Legendary };

        // 服务器权威校验: 扣除熔炼费用
        if (CloudSaveManager.HasInstance && CloudSaveManager.Instance.IsLoggedIn)
        {
            if (!CloudSaveManager.Instance.SpendGoldServer(500, "forge"))
            {
                if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("熔炼失败", "金币不足(需要500金)");
                return;
            }
        }
        else
        {
            // 离线模式: 本地扣金币
            if (!GameManager.Instance.Player.Stats.SpendGold(500))
            {
                if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("熔炼失败", "金币不足(需要500金)");
                return;
            }
        }

        int dungeonLevel = GameManager.Instance.DungeonLevel;
        EquipSlotType resultSlot = (EquipSlotType)Random.Range(0, 3);
        var newItem = EquipmentItem.Generate(resultSlot, resultRarity, dungeonLevel);

        var sorted = new List<int>(selectedIndices); sorted.Sort((a, b) => b.CompareTo(a));
        foreach (var idx in sorted) inv.Backpack.RemoveAt(idx);

        inv.AddToBackpack(newItem);
        inv.RecalculateStats();
        GameManager.Instance.Player.Stats.Save();

        if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("熔铸成功!", $"{RarityNames[(int)resultRarity]} {newItem.Name}");

        AddHistoryLog(newItem, resultRarity);
        Show();
    }

    private static void AddHistoryLog(EquipmentItem newItem, ItemRarity resultRarity)
    {
        string log = $"{System.DateTime.Now:HH:mm} | {RarityNames[(int)resultRarity]} {newItem.Name}";
        ForgeData.AppendHistory(log);
    }

    private GameObject AddPanel(Transform parent, string name, float x1, float y1, float x2, float y2, Color c)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        var r = go.AddComponent<RectTransform>(); r.anchorMin = new Vector2(x1, y1); r.anchorMax = new Vector2(x2, y2); r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        go.AddComponent<Image>().color = c; return go;
    }

    private Text AddText(Transform parent, string name, float x1, float y1, float x2, float y2, string text, int size, Color c, Font f)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        var r = go.AddComponent<RectTransform>(); r.anchorMin = new Vector2(x1, y1); r.anchorMax = new Vector2(x2, y2); r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        var t = go.AddComponent<Text>(); t.text = text; t.alignment = TextAnchor.MiddleCenter; t.fontSize = size; t.color = c; t.font = f; t.raycastTarget = false; return t;
    }

    private void OnDestroy() { if (panel != null) Destroy(panel); if (Instance == this) Instance = null; }
}
