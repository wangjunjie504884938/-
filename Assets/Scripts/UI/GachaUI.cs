using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Random = UnityEngine.Random;

/// <summary>
/// 抽卡系统 — 沉浸式布局（参照原神祈愿）
/// 大结果展示区 + 底部一行4按钮
/// </summary>
public class GachaUI : MonoBehaviour
{
    public static GachaUI Instance { get; private set; }

    private GameObject panel;
    private Text pityText;
    private RectTransform resultContainer;
    private Coroutine showRoutine;
    private Font _font;

    private static string ApiBase => (CloudSaveManager.Instance?.ServerUrl ?? "http://localhost:5132") + "/api";
    private const int NormalCost = 500;
    private const int PremiumCost = 2000;
    private const int ClassPoolCost = 1000;
    private const int PityThreshold = 50;
    private const float TenPullDiscount = 0.9f; // 十连9折

    private static readonly string[] RarityNames = { "普通", "稀有", "史诗", "传说" };
    private static readonly Color[] RarityColors =
    {
        new Color(0.45f, 0.45f, 0.48f, 1f),
        new Color(0.15f, 0.50f, 0.85f, 1f),
        new Color(0.55f, 0.20f, 0.75f, 1f),
        new Color(0.75f, 0.50f, 0.10f, 1f)
    };
    private static readonly string[] RarityIcons = { "●", "◆", "★", "" };

    [Serializable]
    private class GachaResult { public int rarity; }

    /// <summary>抽卡结果+装备信息</summary>
    private class GachaDrop
    {
        public int rarity;
        public string itemName;
        public string itemStats;
        public EquipSlotType slotType;
    }
    [Serializable]
    private class GachaResponse
    {
        public bool success;
        public int newGold;
        public int pityCounter;
        public List<GachaResult> results;
    }

    [Serializable]
    private class PityResponse { public int pityCounter; public int pityThreshold; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Show()
    {
        if (panel == null) BuildUI();
        UIHelper.SetupTopBar("装备抽卡", () => { Hide(); UIHelper.EnsureUIManager(); UIManager.Instance.ShowHub(); });
        UpdateInfo();
        panel.SetActive(true);
        if (showRoutine != null) StopCoroutine(showRoutine);
        showRoutine = StartCoroutine(UIHelper.FadeIn(panel, 0.2f));
        StartCoroutine(LoadPity());
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
        _font = GameManager.GetUIFont();
        Font font = _font;
        Canvas canvas = GameManager.EnsureCanvas();

        panel = UiPrefabLoader.TryLoad("GachaPanel", canvas);
        if (panel == null)
        panel = new GameObject("GachaPanel");
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

        // === 结果展示区 — 大面板 ===
        var resultArea = UIHelper.MakeGlowCard(panel.transform, "ResultArea",
            new Vector2(0.05f, 0.22f), new Vector2(0.95f, 0.88f),
            new Vector2(-20, 48.97547f), new Vector2(20, -48.97553f),
            UIHelper.GlowTop,
            UIHelper.GlowBottom,
            UIHelper.Accent);
        // Border 精确偏移
        var resultBorder = resultArea.transform.Find("Border") as RectTransform;
        if (resultBorder != null)
        {
            resultBorder.offsetMin = new Vector2(-30, -3.684475f);
            resultBorder.offsetMax = new Vector2(30, 54.85105f);
        }
        var sub = UIHelper.MakeSubtitle(resultArea.transform, "抽卡结果", font, 57.53601f, 16);

        var rcObj = new GameObject("ResultContainer");
        rcObj.transform.SetParent(resultArea.transform, false);
        resultContainer = rcObj.AddComponent<RectTransform>();
        resultContainer.anchorMin = new Vector2(0.05f, 0.02f);
        resultContainer.anchorMax = new Vector2(0.95f, 0.88f);
        resultContainer.offsetMin = Vector2.zero; resultContainer.offsetMax = Vector2.zero;

        ShowPlaceholder();

        // === 概率信息条 ===
        var probObj = new GameObject("ProbInfo");
        probObj.transform.SetParent(panel.transform, false);
        var pbr = probObj.AddComponent<RectTransform>();
        pbr.anchorMin = new Vector2(0.5f, 0.5f); pbr.anchorMax = new Vector2(0.5f, 0.5f);
        pbr.pivot = new Vector2(0.5f, 0.5f);
        pbr.anchoredPosition = new Vector2(0, 397f);
        pbr.sizeDelta = new Vector2(1447.2f, 49.6373f);
        var probTxt = probObj.AddComponent<Text>();
        probTxt.text = "<color=#7755AA>普通:</color> 60/30/8/2%    <color=#9955CC>高级:</color> 40/40/15/5%(50保底)    <color=#55AAFF>职业:</color> 50/30/15/5%(职业装备偏向)";
        probTxt.alignment = TextAnchor.MiddleCenter;
        probTxt.fontSize = 13; probTxt.color = UIHelper.TextSecondary; probTxt.font = font;
        probTxt.supportRichText = true;

        // === 底部6按钮 (2行x3列) ===
        // Row 1 (y=0.10-0.21)
        // 普通单抽
        var btnNormalSingle = UIHelper.MakeGlowCard(panel.transform, "BtnNormalSingle",
            new Vector2(0.04f, 0.10f), new Vector2(0.31f, 0.21f),
            Vector2.zero, Vector2.zero,
            new Color(0.04f, 0.08f, 0.06f, 0.92f),
            new Color(0.02f, 0.04f, 0.03f, 0.92f),
            new Color(0.2f, 0.5f, 0.2f, 0.8f));
        var nsBtn = btnNormalSingle.AddComponent<Button>();
        nsBtn.transition = Selectable.Transition.None;
        nsBtn.targetGraphic = btnNormalSingle.GetComponent<Image>();
        UIHelper.SetupButtonFeedback(nsBtn, new Color(0.04f, 0.08f, 0.06f, 0.92f));
        nsBtn.onClick.AddListener(() => StartCoroutine(DrawGacha(0, 1)));
        MakeBtnLabel(btnNormalSingle.transform, "单抽", $"◆{NormalCost}", font, new Color(0.3f, 0.7f, 0.3f));

        // 普通十连
        var btnNormalTen = UIHelper.MakeGlowCard(panel.transform, "BtnNormalTen",
            new Vector2(0.35f, 0.10f), new Vector2(0.62f, 0.21f),
            Vector2.zero, Vector2.zero,
            new Color(0.04f, 0.08f, 0.06f, 0.92f),
            new Color(0.02f, 0.04f, 0.03f, 0.92f),
            new Color(0.2f, 0.5f, 0.2f, 0.8f));
        var ntBtn = btnNormalTen.AddComponent<Button>();
        ntBtn.transition = Selectable.Transition.None;
        ntBtn.targetGraphic = btnNormalTen.GetComponent<Image>();
        UIHelper.SetupButtonFeedback(ntBtn, new Color(0.04f, 0.08f, 0.06f, 0.92f));
        ntBtn.onClick.AddListener(() => StartCoroutine(DrawGacha(0, 10)));
        MakeBtnLabel(btnNormalTen.transform, "十连", $"◆{Mathf.RoundToInt(NormalCost * 10 * TenPullDiscount)} 9折", font, new Color(0.3f, 0.7f, 0.3f));

        // 职业单抽
        var btnClassSingle = UIHelper.MakeGlowCard(panel.transform, "BtnClassSingle",
            new Vector2(0.66f, 0.10f), new Vector2(0.93f, 0.21f),
            Vector2.zero, Vector2.zero,
            new Color(0.04f, 0.06f, 0.10f, 0.92f),
            new Color(0.02f, 0.03f, 0.05f, 0.92f),
            new Color(0.2f, 0.5f, 0.8f, 0.8f));
        var csBtn = btnClassSingle.AddComponent<Button>();
        csBtn.transition = Selectable.Transition.None;
        csBtn.targetGraphic = btnClassSingle.GetComponent<Image>();
        UIHelper.SetupButtonFeedback(csBtn, new Color(0.04f, 0.06f, 0.10f, 0.92f));
        csBtn.onClick.AddListener(() => StartCoroutine(DrawGacha(2, 1)));
        MakeBtnLabel(btnClassSingle.transform, "职业单抽", $"◆{ClassPoolCost}", font, new Color(0.3f, 0.6f, 0.9f));

        // Row 2 (y=0.01-0.09)
        // 高级单抽
        var btnPremSingle = UIHelper.MakeGlowCard(panel.transform, "BtnPremSingle",
            new Vector2(0.04f, 0.01f), new Vector2(0.31f, 0.09f),
            Vector2.zero, Vector2.zero,
            new Color(0.06f, 0.04f, 0.08f, 0.92f),
            new Color(0.03f, 0.02f, 0.04f, 0.92f),
            new Color(0.5f, 0.2f, 0.6f, 0.8f));
        var psBtn = btnPremSingle.AddComponent<Button>();
        psBtn.transition = Selectable.Transition.None;
        psBtn.targetGraphic = btnPremSingle.GetComponent<Image>();
        UIHelper.SetupButtonFeedback(psBtn, new Color(0.06f, 0.04f, 0.08f, 0.92f));
        psBtn.onClick.AddListener(() => StartCoroutine(DrawGacha(1, 1)));
        MakeBtnLabel(btnPremSingle.transform, "高级单抽", $"◆{PremiumCost}", font, new Color(0.7f, 0.4f, 0.8f));

        // 高级十连
        var btnPremTen = UIHelper.MakeGlowCard(panel.transform, "BtnPremTen",
            new Vector2(0.35f, 0.01f), new Vector2(0.62f, 0.09f),
            Vector2.zero, Vector2.zero,
            new Color(0.06f, 0.04f, 0.08f, 0.92f),
            new Color(0.03f, 0.02f, 0.04f, 0.92f),
            new Color(0.5f, 0.2f, 0.6f, 0.8f));
        var ptBtn = btnPremTen.AddComponent<Button>();
        ptBtn.transition = Selectable.Transition.None;
        ptBtn.targetGraphic = btnPremTen.GetComponent<Image>();
        UIHelper.SetupButtonFeedback(ptBtn, new Color(0.06f, 0.04f, 0.08f, 0.92f));
        ptBtn.onClick.AddListener(() => StartCoroutine(DrawGacha(1, 10)));
        MakeBtnLabel(btnPremTen.transform, "高级十连", $"◆{Mathf.RoundToInt(PremiumCost * 10 * TenPullDiscount)} 9折", font, new Color(0.7f, 0.4f, 0.8f));

        // 职业十连
        var btnClassTen = UIHelper.MakeGlowCard(panel.transform, "BtnClassTen",
            new Vector2(0.66f, 0.01f), new Vector2(0.93f, 0.09f),
            Vector2.zero, Vector2.zero,
            new Color(0.04f, 0.06f, 0.10f, 0.92f),
            new Color(0.02f, 0.03f, 0.05f, 0.92f),
            new Color(0.2f, 0.5f, 0.8f, 0.8f));
        var ctBtn = btnClassTen.AddComponent<Button>();
        ctBtn.transition = Selectable.Transition.None;
        ctBtn.targetGraphic = btnClassTen.GetComponent<Image>();
        UIHelper.SetupButtonFeedback(ctBtn, new Color(0.04f, 0.06f, 0.10f, 0.92f));
        ctBtn.onClick.AddListener(() => StartCoroutine(DrawGacha(2, 10)));
        MakeBtnLabel(btnClassTen.transform, "职业十连", $"◆{Mathf.RoundToInt(ClassPoolCost * 10 * TenPullDiscount)} 9折", font, new Color(0.3f, 0.6f, 0.9f));

        // 保底计数 (底部小字)
        var pityObj = new GameObject("PityText");
        pityObj.transform.SetParent(panel.transform, false);
        var ptr = pityObj.AddComponent<RectTransform>();
        ptr.anchorMin = new Vector2(0.3f, 0.01f); ptr.anchorMax = new Vector2(0.7f, 0.04f);
        ptr.offsetMin = Vector2.zero; ptr.offsetMax = Vector2.zero;
        pityText = pityObj.AddComponent<Text>();
        pityText.alignment = TextAnchor.MiddleCenter;
        pityText.fontSize = 14; pityText.color = UIHelper.TextDim; pityText.font = font;

        panel.SetActive(false);
    }

    private void MakeBtnLabel(Transform parent, string title, string cost, Font font, Color accentColor)
    {
        var titleObj = new GameObject("Title");
        titleObj.transform.SetParent(parent, false);
        var tr = titleObj.AddComponent<RectTransform>();
        tr.anchorMin = new Vector2(0, 0.5f); tr.anchorMax = new Vector2(1, 1);
        tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
        var titleTxt = titleObj.AddComponent<Text>();
        titleTxt.text = title; titleTxt.alignment = TextAnchor.MiddleCenter;
        titleTxt.fontSize = 18; titleTxt.color = UIHelper.TextPrimary; titleTxt.font = font;
        titleTxt.raycastTarget = false;

        var costObj = new GameObject("Cost");
        costObj.transform.SetParent(parent, false);
        var cr = costObj.AddComponent<RectTransform>();
        cr.anchorMin = new Vector2(0, 0); cr.anchorMax = new Vector2(1, 0.5f);
        cr.offsetMin = Vector2.zero; cr.offsetMax = Vector2.zero;
        var costTxt = costObj.AddComponent<Text>();
        costTxt.text = cost; costTxt.alignment = TextAnchor.MiddleCenter;
        costTxt.fontSize = 15; costTxt.color = accentColor; costTxt.font = font;
        costTxt.raycastTarget = false;
    }

    private void ShowPlaceholder()
    {
        ClearResults();
        var ph = new GameObject("Placeholder");
        ph.transform.SetParent(resultContainer, false);
        var phr = ph.AddComponent<RectTransform>();
        phr.anchorMin = new Vector2(0.3f, 0.35f); phr.anchorMax = new Vector2(0.7f, 0.65f);
        phr.offsetMin = Vector2.zero; phr.offsetMax = Vector2.zero;
        var phTxt = ph.AddComponent<Text>();
        phTxt.text = "点击下方按钮抽取装备";
        phTxt.alignment = TextAnchor.MiddleCenter;
        phTxt.fontSize = 22; phTxt.color = UIHelper.TextDim; phTxt.font = _font;
    }

    private void ClearResults()
    {
        for (int i = resultContainer.childCount - 1; i >= 0; i--)
            Destroy(resultContainer.GetChild(i).gameObject);
    }

    // ====== 弹窗系统 ======
    private GameObject _popup;
    private Coroutine _popupRoutine;

    private void ClosePopup()
    {
        if (_popupRoutine != null) { StopCoroutine(_popupRoutine); _popupRoutine = null; }
        if (_popup != null) { Destroy(_popup); _popup = null; }
    }

    private IEnumerator PopupFadeRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (_popup != null)
        {
            var cg = _popup.GetComponent<CanvasGroup>();
            if (cg == null) cg = _popup.AddComponent<CanvasGroup>();
            float t = 0f;
            while (t < 0.3f)
            {
                t += Time.unscaledDeltaTime;
                cg.alpha = 1f - (t / 0.3f);
                yield return null;
            }
            Destroy(_popup);
            _popup = null;
        }
        _popupRoutine = null;
    }

    private void ShowGachaPopup(string content, Color contentColor, float duration = 3f)
    {
        ClosePopup();
        Canvas canvas = GameManager.EnsureCanvas();
        _popup = new GameObject("GachaPopup");
        _popup.transform.SetParent(canvas.transform, false);
        var pr = _popup.AddComponent<RectTransform>();
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one;
        pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;
        _popup.AddComponent<CanvasGroup>();

        // 半透明遮罩
        var dim = new GameObject("Dim");
        dim.transform.SetParent(_popup.transform, false);
        var dr = dim.AddComponent<RectTransform>();
        dr.anchorMin = Vector2.zero; dr.anchorMax = Vector2.one;
        dr.offsetMin = Vector2.zero; dr.offsetMax = Vector2.zero;
        var dimImg = dim.AddComponent<Image>();
        dimImg.color = new Color(0, 0, 0, 0.6f);

        // 居中卡片 — 更大以容纳装备详情
        var card = UIHelper.MakeGlowCard(_popup.transform, "PopupCard",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-340, -260), new Vector2(340, 260),
            UIHelper.GlowTop, UIHelper.GlowBottom, contentColor);

        // 内容文字
        var txtObj = new GameObject("Content");
        txtObj.transform.SetParent(card.transform, false);
        var tr = txtObj.AddComponent<RectTransform>();
        tr.anchorMin = new Vector2(0.05f, 0.05f); tr.anchorMax = new Vector2(0.95f, 0.95f);
        tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
        var txt = txtObj.AddComponent<Text>();
        txt.text = content;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontSize = 20; txt.color = contentColor; txt.font = _font;
        txt.supportRichText = true;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow = VerticalWrapMode.Overflow;

        // 点击关闭
        var btn = _popup.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(ClosePopup);

        _popupRoutine = StartCoroutine(PopupFadeRoutine(duration));
    }

    private void ShowSingleResult(GachaDrop drop)
    {
        // 保留结果到结果区
        ClearResults();
        var obj = new GameObject("ResultItem");
        obj.transform.SetParent(resultContainer, false);
        var r = obj.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(0, 0); r.anchorMax = new Vector2(1, 1);
        r.offsetMin = new Vector2(20, 20); r.offsetMax = new Vector2(-20, -20);

        var txt = obj.AddComponent<Text>();
        string slotLabel = drop.slotType switch
        {
            EquipSlotType.Weapon => "武器",
            EquipSlotType.Armor => "护甲",
            EquipSlotType.Accessory => "饰品",
            _ => ""
        };
        string colorHex = ColorUtility.ToHtmlStringRGBA(RarityColors[drop.rarity]);
        txt.text = $"<size=36>{RarityIcons[drop.rarity]} {slotLabel}</size>\n\n" +
                   $"<size=30><color=#{colorHex}>{drop.itemName}</color></size>\n\n" +
                   $"<size=18>{drop.itemStats}</size>";
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontSize = 20; txt.color = UIHelper.TextPrimary; txt.font = _font;
        txt.supportRichText = true;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow = VerticalWrapMode.Overflow;

        // 同时弹出通知
        ShowGachaPopup(
            $"{RarityIcons[drop.rarity]} {slotLabel}\n<size=28><color=#{colorHex}>{drop.itemName}</color></size>\n<size=16>{drop.itemStats}</size>",
            RarityColors[drop.rarity], 3f);
    }

    private void ShowTenResults(List<GachaDrop> drops)
    {
        // 保留结果到结果区
        ClearResults();
        int bestRarity = 0;
        foreach (var d in drops) if (d.rarity > bestRarity) bestRarity = d.rarity;

        var obj = new GameObject("ResultList");
        obj.transform.SetParent(resultContainer, false);
        var r = obj.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(0, 0); r.anchorMax = new Vector2(1, 1);
        r.offsetMin = new Vector2(20, 20); r.offsetMax = new Vector2(-20, -20);

        var txt = obj.AddComponent<Text>();
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < drops.Count; i++)
        {
            var d = drops[i];
            string colorHex = ColorUtility.ToHtmlStringRGBA(RarityColors[d.rarity]);
            string slotIcon = d.slotType switch
            {
                EquipSlotType.Weapon => "",
                EquipSlotType.Armor => "",
                EquipSlotType.Accessory => "",
                _ => ""
            };
            sb.Append($"<color=#{colorHex}>{RarityIcons[d.rarity]}{slotIcon} {d.itemName}</color>  ");
            if ((i + 1) % 2 == 0) sb.AppendLine();
        }
        // Best item details
        var best = drops.Find(d => d.rarity == bestRarity);
        if (best != null)
        {
            string bestColorHex = ColorUtility.ToHtmlStringRGBA(RarityColors[bestRarity]);
            sb.AppendLine($"\n\n<color=#{bestColorHex}>★ 最佳: {best.itemName} ({RarityNames[bestRarity]})</color>");
            sb.Append($"<size=14>{best.itemStats}</size>");
        }
        txt.text = sb.ToString();
        txt.alignment = TextAnchor.UpperCenter;
        txt.fontSize = 18; txt.color = UIHelper.TextPrimary; txt.font = _font;
        txt.supportRichText = true;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow = VerticalWrapMode.Overflow;

        // 同时弹出通知
        var popupSb = new System.Text.StringBuilder();
        for (int i = 0; i < drops.Count; i++)
        {
            var d = drops[i];
            string colorHex = ColorUtility.ToHtmlStringRGBA(RarityColors[d.rarity]);
            string slotIcon = d.slotType switch
            {
                EquipSlotType.Weapon => "",
                EquipSlotType.Armor => "",
                EquipSlotType.Accessory => "",
                _ => ""
            };
            popupSb.Append($"<color=#{colorHex}>{RarityIcons[d.rarity]}{slotIcon}{d.itemName}</color>  ");
            if ((i + 1) % 2 == 0) popupSb.AppendLine();
        }
        if (best != null)
        {
            string bestColorHex = ColorUtility.ToHtmlStringRGBA(RarityColors[bestRarity]);
            popupSb.AppendLine($"\n<color=#{bestColorHex}>★ 最佳: {best.itemName} ({RarityNames[bestRarity]})</color>\n<size=14>{best.itemStats}</size>");
        }
        ShowGachaPopup(popupSb.ToString(), RarityColors[bestRarity], 5f);
    }

    private IEnumerator LoadPity()
    {
        string token = CloudSaveManager.Instance?.Token ?? "";

        // 保底计数纯服务器权威，不读本地PlayerPrefs
        if (!string.IsNullOrEmpty(token))
        {
            using (var req = UnityWebRequest.Get($"{ApiBase}/gacha/pity"))
            {
                req.SetRequestHeader("Authorization", $"Bearer {token}");
                req.timeout = 5;
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    var resp = JsonUtility.FromJson<PityResponse>(req.downloadHandler.text);
                    if (resp != null)
                    {
                        if (pityText != null)
                            pityText.text = $"高级保底: {resp.pityCounter}/{resp.pityThreshold}";
                        yield break;
                    }
                }
            }
        }

        // 服务器不可用: 高级抽卡不可用，保底显示为问号
        if (pityText != null)
            pityText.text = $"高级保底: 服务器未连接";
    }

    private IEnumerator DrawGacha(int gachaType, int count)
    {
        string token = CloudSaveManager.Instance?.Token ?? "";
        int cost = gachaType switch { 1 => PremiumCost, 2 => ClassPoolCost, _ => NormalCost };
        int totalCost = cost * count;
        if (count >= 10) totalCost = Mathf.RoundToInt(totalCost * TenPullDiscount);

        // Check gold
        var player = GameManager.Instance?.Player;
        if (player == null) yield break;
        if (player.Stats.Gold < totalCost)
        {
            ShowErrorText("金币不足!");
            yield break;
        }

        // 高级抽卡(保底池)必须走服务器，禁止本地fallback防止SL作弊
        if (gachaType == 1 && string.IsNullOrEmpty(token))
        {
            ShowErrorText("需要登录服务器才能使用高级抽卡!");
            yield break;
        }

        // 普通池和职业池: 优先服务器，服务器不可用时本地fallback
        // 高级池: 必须服务器
        if (!string.IsNullOrEmpty(token) && gachaType != 2)
        {
            var body = new DrawBody { gachaType = gachaType, count = count };
            string json = JsonUtility.ToJson(body);

            using (var req = new UnityWebRequest($"{ApiBase}/gacha/draw", "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {token}");
                req.timeout = 5;
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    var resp = JsonUtility.FromJson<GachaResponse>(req.downloadHandler.text);
                    if (resp?.success == true && resp.results != null && resp.results.Count > 0)
                    {
                        var drops = new List<GachaDrop>();
                        foreach (var r in resp.results)
                            drops.Add(GiveGachaItem(r.rarity, GameManager.Instance.DungeonLevel));

                        if (drops.Count == 1)
                            ShowSingleResult(drops[0]);
                        else
                            ShowTenResults(drops);

                        player.Stats.Gold = resp.newGold;
                        UpdateInfo();
                        player.Stats.Save();

                        if (gachaType == 1 && pityText != null)
                            pityText.text = $"高级保底: {resp.pityCounter}/{PityThreshold}";
                        yield break;
                    }
                }

                // 高级抽卡服务器失败: 不允许本地fallback
                if (gachaType == 1)
                {
                    ShowErrorText("服务器连接失败，高级抽卡不可用");
                    yield break;
                }
            }
        }

        // === LOCAL FALLBACK (仅普通池/职业池，服务器不可用时) ===
        player.Stats.SpendGold(totalCost);

        float[] rates = gachaType switch
        {
            2 => new float[] { 0.50f, 0.30f, 0.15f, 0.05f },  // Class pool
            _ => new float[] { 0.60f, 0.30f, 0.08f, 0.02f },  // Normal
        };

        var localDrops = new List<GachaDrop>();

        for (int i = 0; i < count; i++)
        {
            int rarity = LocalRollRarity(rates);
            bool useClassBias = gachaType == 2;
            localDrops.Add(GiveGachaItem(rarity, GameManager.Instance.DungeonLevel, useClassBias));
        }

        // 十连保底: 至少1个稀有(Rare+)以上
        if (count >= 10)
        {
            bool hasRarePlus = false;
            foreach (var d in localDrops)
            {
                if (d != null && d.rarity >= 1) { hasRarePlus = true; break; }
            }
            if (!hasRarePlus && localDrops.Count > 0 && localDrops[0] != null)
            {
                // Upgrade first drop to Rare
                localDrops[0] = GiveGachaItem(1, GameManager.Instance.DungeonLevel);
            }
        }

        if (count == 1)
            ShowSingleResult(localDrops[0]);
        else
            ShowTenResults(localDrops);

        UpdateInfo();
        player.Stats.Save();
    }

    /// <summary>本地随机稀有度</summary>
    private int LocalRollRarity(float[] rates)
    {
        float roll = Random.Range(0f, 1f);
        float cumulative = 0f;
        for (int i = 0; i < rates.Length; i++)
        {
            cumulative += rates[i];
            if (roll < cumulative) return i;
        }
        return 0;
    }

    /// <summary>根据稀有度生成装备并放入背包, 返回装备信息</summary>
    private GachaDrop GiveGachaItem(int rarity, int dungeonLevel, bool useClassBias = false)
    {
        var player = GameManager.Instance?.Player;
        if (player == null) return null;

        var itemRarity = (ItemRarity)rarity;
        EquipSlotType slot;

        if (useClassBias)
        {
            // 60% chance to get class-favored slot
            EquipSlotType preferredSlot = player.HeroClass switch
            {
                HeroClass.Warrior => EquipSlotType.Weapon,
                HeroClass.Mage => EquipSlotType.Accessory,
                HeroClass.Priest => EquipSlotType.Armor,
                _ => EquipSlotType.Weapon,
            };
            slot = Random.Range(0f, 1f) < 0.6f ? preferredSlot : (EquipSlotType)Random.Range(0, 3);
        }
        else
        {
            slot = Random.Range(0, 3) switch
            {
                0 => EquipSlotType.Weapon,
                1 => EquipSlotType.Armor,
                _ => EquipSlotType.Accessory
            };
        }

        var item = EquipmentItem.Generate(slot, itemRarity, dungeonLevel);

        bool added = player.Inventory.AddToBackpack(item);
        if (!added)
        {
            // 背包已满 — 发送到邮箱, 不自动出售
            LocalMailSystem.SendItemMail(
                $"抽卡获得: {item.Name}",
                $"背包已满，装备已发送至邮箱。\n{item.GetStatSummary()}{(string.IsNullOrEmpty(item.GetAffixDisplayString()) ? "" : " " + item.GetAffixDisplayString())}",
                item);
                        GameLog.Log($"[Gacha] 背包已满, 装备{item.Name}已发送至邮箱");
        }

        var sb = new System.Text.StringBuilder();
        if (item.AttackBonus > 0) sb.Append($"ATK+{item.AttackBonus} ");
        if (item.DefenseBonus > 0) sb.Append($"DEF+{item.DefenseBonus} ");
        if (item.HpBonus > 0) sb.Append($"HP+{item.HpBonus} ");
        if (item.CritBonus > 0) sb.Append($"暴击+{item.CritBonus * 100:F0}% ");
        if (item.SpeedBonus > 0) sb.Append($"移速+{item.SpeedBonus:F1} ");
        if (item.LifeStealBonus > 0) sb.Append($"吸血+{item.LifeStealBonus * 100:F0}% ");
        if (item.AttackSpeedBonus > 0) sb.Append($"攻速+{item.AttackSpeedBonus * 100:F0}% ");
        if (item.RangeBonus > 0) sb.Append($"范围+{item.RangeBonus:F1} ");
        string stats = sb.ToString().Trim();
        if (!string.IsNullOrEmpty(item.GetAffixDisplayString()))
            stats += " " + item.GetAffixDisplayString();

        return new GachaDrop
        {
            rarity = rarity,
            itemName = item.Name,
            itemStats = stats.Trim(),
            slotType = item.SlotType
        };
    }

    private void ShowErrorText(string msg)
    {
        ShowGachaPopup(msg, new Color(0.9f, 0.3f, 0.3f, 1f), 2f);
    }

    [Serializable]
    private class DrawBody { public int gachaType; public int count; }

    private void UpdateInfo()
    {
        TopNavBar.Instance?.RefreshCurrency();
    }

    private void OnDestroy()
    {
        if (panel != null) Destroy(panel);
        if (Instance == this) Instance = null;
    }
}
