using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// EquipUI partial — Confirmation dialogs (upgrade/sell/dismantle/replace)
/// </summary>
public partial class EquipUI
{
    // ================================================================
    //  Confirm Dialogs
    // ================================================================
    private void ShowUpgradeConfirm(int index)
    {
        var inv = GameManager.Instance?.Player?.Inventory;
        if (inv == null || index >= inv.Backpack.Count) return;

        var item = inv.Backpack[index];
        if (item.UpgradeLevel >= EquipmentItem.MaxUpgradeLevel) return;
        int cost = item.GetUpgradeCost();
        int playerGold = GameManager.Instance?.Player?.Stats?.Gold ?? 0;
        bool canAfford = playerGold >= cost;

        HideConfirmDialog();

        Font font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();

        confirmDialog = CreateDialogBase(canvas, "强化装备", 350);

        var box = confirmDialog.transform.Find("DialogBox").gameObject;
        GameObject infoObj = new GameObject("ItemInfo");
        infoObj.transform.SetParent(box.transform, false);
        RectTransform iRect = infoObj.AddComponent<RectTransform>();
        iRect.anchorMin = new Vector2(0.08f, 0.3f); iRect.anchorMax = new Vector2(0.92f, 0.8f);
        iRect.offsetMin = Vector2.zero; iRect.offsetMax = Vector2.zero;
        Text iText = infoObj.AddComponent<Text>();
        string upgradeTag = item.UpgradeLevel > 0 ? $" <color=#8C55C0>+{item.UpgradeLevel}</color>" : "";
        iText.text = $"{SlotPrefix(item.SlotType)}{item.Name}{upgradeTag}\n<size=15><color=#aaa>{item.GetStatSummary()}</color></size>\n\n<color=#88FF88>→ 强化 {item.UpgradeLevel} → {item.UpgradeLevel + 1}  属性提升15%</color>\n<color=#8C55C0>◆ {cost} 金币</color>"
            + (canAfford ? "" : $"\n<color=#FF4444>(不足! 你有{playerGold}金)</color>");
        iText.alignment = TextAnchor.MiddleCenter;
        iText.fontSize = 18; iText.color = ItemData.GetRarityColor(item.Rarity);
        iText.font = font; iText.supportRichText = true;

        AddDialogButtons(box, font, canAfford ? "确认强化" : "金币不足",
            canAfford ? (UnityEngine.Events.UnityAction)(() => {
                HideConfirmDialog();
                if (GameManager.Instance.Player.Stats.SpendGold(cost))
                {
                    item.Upgrade();
                    inv.RecalculateStats();
                    GameManager.Instance.Player.Stats.Save();
                    RefreshDisplay();
                }
            }) : null);
    }

    private void ShowSellConfirm(int index)
    {
        var inv = GameManager.Instance?.Player?.Inventory;
        if (inv == null || index >= inv.Backpack.Count) return;

        var item = inv.Backpack[index];
        int sellPrice = item.SellPrice;

        HideConfirmDialog();

        Font font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();

        confirmDialog = CreateDialogBase(canvas, "确认出售");

        var box = confirmDialog.transform.Find("DialogBox").gameObject;
        GameObject infoObj = new GameObject("ItemInfo");
        infoObj.transform.SetParent(box.transform, false);
        RectTransform iRect = infoObj.AddComponent<RectTransform>();
        iRect.anchorMin = new Vector2(0.1f, 0.35f); iRect.anchorMax = new Vector2(0.9f, 0.8f);
        iRect.offsetMin = Vector2.zero; iRect.offsetMax = Vector2.zero;
        Text iText = infoObj.AddComponent<Text>();
        iText.text = $"{SlotPrefix(item.SlotType)}{item.Name}\n<size=16><color=#aaa>{item.GetStatSummary()}</color></size>\n\n<color=#8C55C0>出售获得 ◆ {sellPrice} 金币</color>";
        iText.alignment = TextAnchor.MiddleCenter;
        iText.fontSize = 20; iText.color = ItemData.GetRarityColor(item.Rarity);
        iText.font = font; iText.supportRichText = true;

        AddDialogButtons(box, font, "确认出售",
            () => { HideConfirmDialog(); inv.SellFromBackpack(index); GameManager.Instance.Player.Stats.Save(); RefreshDisplay(); });
    }

    private void ShowDismantleConfirm(int index)
    {
        var inv = GameManager.Instance?.Player?.Inventory;
        if (inv == null || index >= inv.Backpack.Count) return;

        var item = inv.Backpack[index];
        int yield = DismantleSystem.GetFragmentYield(item);

        HideConfirmDialog();

        Font font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();

        confirmDialog = CreateDialogBase(canvas, "分解装备");

        var box = confirmDialog.transform.Find("DialogBox").gameObject;
        GameObject infoObj = new GameObject("ItemInfo");
        infoObj.transform.SetParent(box.transform, false);
        RectTransform iRect = infoObj.AddComponent<RectTransform>();
        iRect.anchorMin = new Vector2(0.1f, 0.35f); iRect.anchorMax = new Vector2(0.9f, 0.8f);
        iRect.offsetMin = Vector2.zero; iRect.offsetMax = Vector2.zero;
        Text iText = infoObj.AddComponent<Text>();
        iText.text = $"{SlotPrefix(item.SlotType)}{item.Name}\n<size=16><color=#aaa>{item.GetStatSummary()}</color></size>\n\n<color=#60A0FF>分解获得 ◆ {yield} 碎片</color>\n<color=#888>(碎片可用于未来装备强化)</color>";
        iText.alignment = TextAnchor.MiddleCenter;
        iText.fontSize = 20; iText.color = ItemData.GetRarityColor(item.Rarity);
        iText.font = font; iText.supportRichText = true;

        AddDialogButtons(box, font, "确认分解",
            () => { HideConfirmDialog(); DismantleSystem.DismantleFromBackpack(inv, index); GameManager.Instance.Player.Stats.Save(); RefreshDisplay(); });
    }

    private void ShowReplaceConfirm(int backpackIndex)
    {
        var inv = GameManager.Instance?.Player?.Inventory;
        if (inv == null || backpackIndex >= inv.Backpack.Count) return;

        var newItem = inv.Backpack[backpackIndex];
        var equippedSlot = inv.Equipped.Find(s => s.SlotType == newItem.SlotType);
        if (equippedSlot == null || equippedSlot.IsEmpty) return;

        var oldItem = equippedSlot.Item;

        HideConfirmDialog();

        Font font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();

        confirmDialog = CreateDialogBase(canvas, "替换装备", 380);

        var box = confirmDialog.transform.Find("DialogBox").gameObject;

        GameObject newObj = new GameObject("NewItemInfo");
        newObj.transform.SetParent(box.transform, false);
        RectTransform nRect = newObj.AddComponent<RectTransform>();
        nRect.anchorMin = new Vector2(0.08f, 0.52f); nRect.anchorMax = new Vector2(0.92f, 0.82f);
        nRect.offsetMin = Vector2.zero; nRect.offsetMax = Vector2.zero;
        Text nText = newObj.AddComponent<Text>();
        nText.text = $"<color=#88FF88>▶ 装备：</color>{SlotPrefix(newItem.SlotType)}{newItem.Name}\n<size=15><color=#aaa>{newItem.GetStatSummary()}</color></size>";
        nText.alignment = TextAnchor.MiddleLeft;
        nText.fontSize = 18; nText.color = ItemData.GetRarityColor(newItem.Rarity);
        nText.font = font; nText.supportRichText = true;

        GameObject oldObj = new GameObject("OldItemInfo");
        oldObj.transform.SetParent(box.transform, false);
        RectTransform oRect = oldObj.AddComponent<RectTransform>();
        oRect.anchorMin = new Vector2(0.08f, 0.22f); oRect.anchorMax = new Vector2(0.92f, 0.48f);
        oRect.offsetMin = Vector2.zero; oRect.offsetMax = Vector2.zero;
        Text oText = oldObj.AddComponent<Text>();
        oText.text = $"<color=#FF8888>▶ 卸下：</color>{SlotPrefix(oldItem.SlotType)}{oldItem.Name}\n<size=15><color=#aaa>{oldItem.GetStatSummary()}</color></size>";
        oText.alignment = TextAnchor.MiddleLeft;
        oText.fontSize = 18; oText.color = new Color(ItemData.GetRarityColor(oldItem.Rarity).r,
            ItemData.GetRarityColor(oldItem.Rarity).g,
            ItemData.GetRarityColor(oldItem.Rarity).b, 0.7f);
        oText.font = font; oText.supportRichText = true;

        AddDialogButtons(box, font, "确认替换",
            () => { HideConfirmDialog(); selectedBpIndices.Clear(); inv.Equip(inv.Backpack[backpackIndex]); GameManager.Instance.Player.Stats.Save(); RefreshDisplay(); });
    }

    private GameObject CreateDialogBase(Canvas canvas, string title, float height = 320)
    {
        Font font = GameManager.GetUIFont();

        GameObject dialog = new GameObject("ConfirmDialog");
        dialog.transform.SetParent(canvas.transform, false);
        RectTransform dlgRect = dialog.AddComponent<RectTransform>();
        dlgRect.anchorMin = Vector2.zero; dlgRect.anchorMax = Vector2.one;
        dlgRect.offsetMin = Vector2.zero; dlgRect.offsetMax = Vector2.zero;
        Image dlgBg = dialog.AddComponent<Image>();
        dlgBg.color = new Color(0f, 0f, 0f, 0.7f);

        GameObject boxObj = new GameObject("DialogBox");
        boxObj.transform.SetParent(dialog.transform, false);
        RectTransform boxRect = boxObj.AddComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0.5f, 0.5f); boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        boxRect.pivot = new Vector2(0.5f, 0.5f);
        boxRect.anchoredPosition = Vector2.zero;
        boxRect.sizeDelta = new Vector2(480, height);
        Image boxImg = boxObj.AddComponent<Image>();
        boxImg.color = UIHelper.BgPanel;

        GameObject borderObj = new GameObject("Border");
        borderObj.transform.SetParent(boxObj.transform, false);
        RectTransform bdRect = borderObj.AddComponent<RectTransform>();
        bdRect.anchorMin = Vector2.zero; bdRect.anchorMax = Vector2.one;
        bdRect.offsetMin = new Vector2(-2, -2); bdRect.offsetMax = new Vector2(2, 2);
        Image bdImg = borderObj.AddComponent<Image>();
        bdImg.color = new Color(0.30f, 0.15f, 0.45f, 0.5f);
        borderObj.transform.SetAsFirstSibling();

        GameObject dTitle = new GameObject("DialogTitle");
        dTitle.transform.SetParent(boxObj.transform, false);
        RectTransform dtRect = dTitle.AddComponent<RectTransform>();
        dtRect.anchorMin = new Vector2(0, 1); dtRect.anchorMax = new Vector2(1, 1);
        dtRect.pivot = new Vector2(0.5f, 1); dtRect.anchoredPosition = new Vector2(0, -22);
        dtRect.sizeDelta = new Vector2(0, 36);
        Text dtText = dTitle.AddComponent<Text>();
        dtText.text = title; dtText.alignment = TextAnchor.MiddleCenter;
        dtText.fontSize = 28; dtText.color = UIHelper.Accent; dtText.font = font;

        return dialog;
    }

    private void AddDialogButtons(GameObject box, Font font, string confirmText, UnityEngine.Events.UnityAction onConfirm)
    {
        GameObject btnRow = new GameObject("ButtonRow");
        btnRow.transform.SetParent(box.transform, false);
        RectTransform brRect = btnRow.AddComponent<RectTransform>();
        brRect.anchorMin = new Vector2(0, 0); brRect.anchorMax = new Vector2(1, 0);
        brRect.pivot = new Vector2(0.5f, 0);
        brRect.anchoredPosition = new Vector2(0, 25);
        brRect.sizeDelta = new Vector2(0, 52);

        GameObject okObj = new GameObject("ConfirmBtn");
        okObj.transform.SetParent(btnRow.transform, false);
        RectTransform okRect = okObj.AddComponent<RectTransform>();
        okRect.anchorMin = Vector2.zero; okRect.anchorMax = new Vector2(0.47f, 1);
        okRect.offsetMin = new Vector2(30, 0); okRect.offsetMax = new Vector2(-6, 0);
        Image okImg = okObj.AddComponent<Image>();
        okImg.color = UIHelper.BtnSell;
        Button okBtn = okObj.AddComponent<Button>();
        okBtn.onClick.AddListener(onConfirm);
        MakeLabel(okObj, confirmText, 22, Color.white, font);

        GameObject cancelObj = new GameObject("CancelBtn");
        cancelObj.transform.SetParent(btnRow.transform, false);
        RectTransform cancelRect = cancelObj.AddComponent<RectTransform>();
        cancelRect.anchorMin = new Vector2(0.53f, 0); cancelRect.anchorMax = new Vector2(1, 1);
        cancelRect.offsetMin = new Vector2(6, 0); cancelRect.offsetMax = new Vector2(-30, 0);
        Image cancelImg = cancelObj.AddComponent<Image>();
        cancelImg.color = new Color(0.45f, 0.18f, 0.18f, 0.95f);
        Button cancelBtn = cancelObj.AddComponent<Button>();
        cancelBtn.onClick.AddListener(() => { HideConfirmDialog(); });
        MakeLabel(cancelObj, "取 消", 22, Color.white, font);
    }

    private void HideConfirmDialog()
    {
        if (confirmDialog != null) { Destroy(confirmDialog); confirmDialog = null; }
    }

}
