using UnityEngine;
using UnityEngine.UI;
using System;

/// <summary>
/// 充值中心 (设计图版风格) — 寿元币/位面币礼包模拟充值。
/// 左列寿元币礼包 3 档, 右列位面币礼包 3 档, 顶部三货币余额实时展示。
/// 入口: HUD 顶中三个充值加号 (InGameHud → UIManager.ShowRecharge)。
/// 模拟充值不产生真实扣费; 余额本地持久化 (CurrencyWallet)。
/// </summary>
public class RechargeUI : UIPanelBase
{
    public static RechargeUI Instance { get; private set; }

    private static readonly Color ColText = new Color(0.93f, 0.91f, 0.96f);
    private static readonly Color ColGold = new Color(1f, 0.85f, 0.35f);
    private static readonly Color ColLife = new Color(0.45f, 0.95f, 0.55f);
    private static readonly Color ColPlane = new Color(1f, 0.75f, 0.35f);
    private static readonly Color ColDim = new Color(0.60f, 0.57f, 0.66f, 0.85f);
    private static readonly Vector2 AC = new Vector2(0.5f, 0.5f);

    private Text _lifeBalance;
    private Text _planeBalance;
    private Text _goldBalance;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        CurrencyWallet.OnChanged += RefreshBalances;
    }

    protected override void OnDestroy()
    {
        CurrencyWallet.OnChanged -= RefreshBalances;
        if (Instance == this) Instance = null;
        base.OnDestroy();
    }

    protected override void OnHide()
    {
        CurrencyWallet.OnChanged -= RefreshBalances;
    }

    protected override void OnShow()
    {
        CurrencyWallet.OnChanged -= RefreshBalances;   // 防止重复Show叠加订阅
        CurrencyWallet.OnChanged += RefreshBalances;
    }

    // =================== 构建 ===================

    protected override void BuildContent()
    {
        Canvas canvas = GameManager.EnsureCanvas();

        panel = new GameObject("RechargePanel");
        panel.transform.SetParent(canvas.transform, false);
        var pr = panel.AddComponent<RectTransform>();
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one;
        pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;

        // 全屏暗色底 (点击空白关闭)
        var backdrop = NewRect("Backdrop", panel.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        backdrop.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        var bdBtn = backdrop.AddComponent<Button>();
        bdBtn.transition = Selectable.Transition.None;
        bdBtn.onClick.AddListener(ClosePanel);

        // 主框 (水墨风暗底 + 描边, 同商城/信息窗口风格)
        var box = UIHelper.MakeGlowCard(panel.transform, "Box",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
            new Color(0.06f, 0.05f, 0.10f, 0.98f), UIHelper.GlowTop, UIHelper.Accent);
        var boxRect = box.GetComponent<RectTransform>();
        boxRect.sizeDelta = new Vector2(960, 680);

        // 标题
        var title = MakeText(box.transform, "TitleTxt", "充 值 中 心", 34, UIHelper.Accent, TextAnchor.MiddleCenter);
        Place(title.rectTransform, 0, 285, 400, 44);

        // 关闭按钮 (✕)
        UIHelper.MakeButton(box.transform, "CloseBtn", "✕", GameManager.GetUIFont(),
            new Vector2(0.88f, 0.88f), new Vector2(0.98f, 0.98f), Vector2.zero, Vector2.zero,
            UIHelper.BtnDanger, 22, ClosePanel);

        // 三货币余额行
        MakeBalanceRow(box.transform, "游戏币", -310, 215, ColGold, out _goldBalance);
        MakeBalanceRow(box.transform, "寿元币", 0, 215, ColLife, out _lifeBalance);
        MakeBalanceRow(box.transform, "位面币", 310, 215, ColPlane, out _planeBalance);

        // 分栏标题
        var lifeHead = MakeText(box.transform, "LifeHeaderTxt", "— 寿元币礼包 —", 26, ColLife, TextAnchor.MiddleCenter);
        Place(lifeHead.rectTransform, -230, 150, 400, 34);
        var planeHead = MakeText(box.transform, "PlaneHeaderTxt", "— 位面币礼包 —", 26, ColPlane, TextAnchor.MiddleCenter);
        Place(planeHead.rectTransform, 230, 150, 400, 34);

        // 礼包卡片: 左列寿元 3 档, 右列位面 3 档
        BuildPack(box.transform, "Life", "寿元币", 100, -230, 60, ColLife);
        BuildPack(box.transform, "Life", "寿元币", 500, -230, -35, ColLife);
        BuildPack(box.transform, "Life", "寿元币", 1200, -230, -130, ColLife);
        BuildPack(box.transform, "Plane", "位面币", 60, 230, 60, ColPlane);
        BuildPack(box.transform, "Plane", "位面币", 300, 230, -35, ColPlane);
        BuildPack(box.transform, "Plane", "位面币", 680, 230, -130, ColPlane);

        // 说明
        var note = MakeText(box.transform, "NoteTxt", "模拟充值 · 不产生真实扣费 · 余额本地保存", 17, ColDim, TextAnchor.MiddleCenter);
        Place(note.rectTransform, 0, -230, 700, 24);

        // 特权: 自动拾取 (位面币, 永久) — 经济规范: 位面币消耗/特权
        BuildPrivCard(box.transform);

        RefreshBalances();
    }

    /// <summary>自动拾取特权卡片 (100位面, 购买后永久 8米磁吸)</summary>
    private void BuildPrivCard(Transform parent)
    {
        var card = NewRect("Priv_AutoLoot", parent, AC, AC, new Vector2(0, -290), new Vector2(700, 52));
        var cardImg = card.AddComponent<Image>();
        cardImg.color = new Color(0.10f, 0.08f, 0.15f, 0.95f);

        var privTxt = MakeText(card.transform, "PrivTxt", "", 18, ColText, TextAnchor.MiddleLeft);
        PlacePivotLeft(privTxt.rectTransform, 16, 0, 470, 30);

        var btnObj = NewRect("PrivBuyBtn", card.transform, AC, AC, new Vector2(285, 0), new Vector2(100, 36));
        var btnImg = btnObj.AddComponent<Image>();
        btnImg.color = UIHelper.BtnConfirm;
        var btn = btnObj.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        var btnTxt = MakeText(btnObj.transform, "BuyTxt", "", 16, Color.white, TextAnchor.MiddleCenter);
        btnTxt.rectTransform.anchorMin = Vector2.zero; btnTxt.rectTransform.anchorMax = Vector2.one;
        btnTxt.rectTransform.offsetMin = Vector2.zero; btnTxt.rectTransform.offsetMax = Vector2.zero;

        void RefreshPriv()
        {
            bool owned = Privileges.AutoLoot;
            privTxt.text = owned ? "自动拾取特权: 已拥有 (掉落物8米磁吸, 永久)" : "自动拾取特权: 掉落物 8 米磁吸, 永久生效";
            btnImg.color = owned ? UIHelper.CardBg : UIHelper.BtnConfirm;
            btn.interactable = !owned;
            btnTxt.text = owned ? "已拥有" : $"{Privileges.AutoLootPrice}位面";
        }

        btn.onClick.AddListener(() =>
        {
            if (Privileges.AutoLoot) return;
            if (CurrencyWallet.TrySpendPlane(Privileges.AutoLootPrice))
            {
                Privileges.AutoLoot = true;
                GameUI.Instance?.ShowItemPickupToast("特权生效", "自动拾取: 掉落物 8 米磁吸");
            }
            else GameUI.Instance?.ShowItemPickupToast("位面币不足", $"需要 {Privileges.AutoLootPrice} 位面币");
            RefreshPriv();
        });
        RefreshPriv();
    }

    private void BuildPack(Transform parent, string kind, string coinName, int amount, float x, float y, Color coinColor)
    {
        var card = NewRect($"Pack_{kind}_{amount}", parent, AC, AC, new Vector2(x, y), new Vector2(210, 78));
        var cardImg = card.AddComponent<Image>();
        cardImg.color = new Color(0.10f, 0.08f, 0.15f, 0.95f);

        var amountTxt = MakeText(card.transform, "AmountTxt", $"+{amount} {coinName}", 22, coinColor, TextAnchor.MiddleCenter);
        Place(amountTxt.rectTransform, 0, 20, 190, 30);

        // 每礼包两支付按钮: 微信支付/支付宝支付 (模拟到账)
        MakePayBtn(card.transform, "WeChatBtn", "微信支付", -52, kind, amount, new Color(0.16f, 0.66f, 0.36f));
        MakePayBtn(card.transform, "AliPayBtn", "支付宝支付", 52, kind, amount, new Color(0.25f, 0.60f, 0.92f));
    }

    /// <summary>礼包支付按钮 — 模拟到账: 点击即 CurrencyWallet.AddLifeOrPlane</summary>
    private void MakePayBtn(Transform parent, string objName, string label, float x, string kind, int amount, Color bg)
    {
        var btnObj = NewRect(objName, parent, AC, AC, new Vector2(x, -17), new Vector2(94, 30));
        var img = btnObj.AddComponent<Image>();
        img.color = bg;
        var btn = btnObj.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        UIHelper.SetupButtonFeedback(btn, bg);
        int amt = amount;
        btn.onClick.AddListener(() => CurrencyWallet.AddLifeOrPlane(kind, amt));
        var t = MakeText(btnObj.transform, "PayTxt", label, 15, Color.white, TextAnchor.MiddleCenter);
        t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one;
        t.rectTransform.offsetMin = Vector2.zero; t.rectTransform.offsetMax = Vector2.zero;
    }

    private void RefreshBalances()
    {
        if (_lifeBalance != null) _lifeBalance.text = CurrencyWallet.LifeCoins.ToString();
        if (_planeBalance != null) _planeBalance.text = CurrencyWallet.PlaneCoins.ToString();
        if (_goldBalance != null)
        {
            var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            _goldBalance.text = player != null ? player.Stats.Gold.ToString() : "0";
        }
    }

    private void MakeBalanceRow(Transform parent, string label, float x, float y, Color color, out Text valueText)
    {
        var lbl = MakeText(parent, "BalLabel_" + label, label + ":", 22, ColDim, TextAnchor.MiddleRight);
        Place(lbl.rectTransform, x - 12, y, 130, 30);
        var val = MakeText(parent, "BalValue_" + label, "0", 26, color, TextAnchor.MiddleLeft);
        Place(val.rectTransform, x + 124, y, 150, 32);
        valueText = val;
    }

    private void ClosePanel()
    {
        Hide();
        UIHelper.ReturnFromPanel();
    }

    // =================== 工具 ===================

    private static RectTransform NewRect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    private static Text MakeText(Transform parent, string objName, string content, int fontSize, Color color, TextAnchor anchor)
    {
        var go = new GameObject(objName);
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.text = content;
        t.font = GameManager.GetUIFont();
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = anchor;
        t.raycastTarget = false;
        return t;
    }

    private static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = AC; rt.anchorMax = AC; rt.pivot = AC;
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private static void PlacePivotLeft(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = AC; rt.anchorMax = AC; rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }
}
