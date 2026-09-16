using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Boss图鉴 — 展示各关Boss信息、击杀状态和奖励
/// 击杀状态基于HighestStageCleared判定
/// </summary>
public class BossCodexUI : MonoBehaviour
{
    public static BossCodexUI Instance { get; private set; }

    private GameObject panel;
    private Coroutine showRoutine;

    private static readonly (string name, string stage, string desc, int goldReward, Color theme)[] BossData =
    {
        ("树精长老", "幽暗森林", "森林深处的古老守护者，操控藤蔓与毒孢子", 50, new Color(0.2f, 0.5f, 0.2f)),
        ("矿洞巨魔", "废弃矿洞", "蛮力惊人的洞穴恶霸，擅长冲撞攻击", 80, new Color(0.4f, 0.35f, 0.3f)),
        ("冰霜女妖", "冰霜峡谷", "凛冬之主的眷属，释放冰锥与暴风雪", 120, new Color(0.5f, 0.7f, 0.9f)),
        ("炎魔将军", "火焰神殿", "烈焰军团的统帅，召唤火雨与岩浆", 180, new Color(0.8f, 0.3f, 0.1f)),
        ("亡灵领主", "亡灵墓穴", "统御亡灵大军的黑暗君王", 250, new Color(0.3f, 0.2f, 0.4f)),
        ("远古幼龙", "龙巢深处", "沉睡千年的龙族后裔，吐息毁灭一切", 400, new Color(0.7f, 0.5f, 0.1f)),
        ("虚空使者", "混沌虚空", "来自虚空的异界存在，扭曲时空", 600, new Color(0.5f, 0.1f, 0.6f)),
        ("魔王·深渊", "魔王殿堂", "卫冕大陆的终极威胁", 1000, new Color(0.8f, 0f, 0f)),
    };

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Show()
    {
        if (panel != null) { Destroy(panel); panel = null; }
        BuildUI();
        UIHelper.SetupTopBar("Boss图鉴", () => { Hide(); UIHelper.EnsureUIManager(); UIManager.Instance.ShowHub(); });
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

    private static bool IsBossDefeated(int stageIndex)
    {
        int cleared = RuntimePlayerData.Instance?.HighestStage ?? -1;
        return stageIndex <= cleared;
    }

    private void BuildUI()
    {
        Font font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();

        panel = UiPrefabLoader.TryLoad("BossCodexPanel", canvas);
        if (panel == null)
        panel = new GameObject("BossCodexPanel");
        panel.transform.SetParent(canvas.transform, false);
        RectTransform pr = panel.AddComponent<RectTransform>();
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one;
        pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;
        panel.AddComponent<CanvasGroup>();

        var bg = new GameObject("BG");
        bg.transform.SetParent(panel.transform, false);
        var bgr = bg.AddComponent<RectTransform>();
        bgr.anchorMin = Vector2.zero; bgr.anchorMax = Vector2.one;
        bgr.offsetMin = Vector2.zero; bgr.offsetMax = Vector2.zero;
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = UIHelper.BgDark;
        bgImg.raycastTarget = true;

        int defeated = 0;
        int cleared = RuntimePlayerData.Instance?.HighestStage ?? -1;
        for (int i = 0; i < BossData.Length; i++)
            if (i <= cleared) defeated++;

        UIHelper.MakeSubtitle(panel.transform, $"已击杀 {defeated}/{BossData.Length}", font, 100, 16);

        // 2列居中布局 — 用anchoredPosition+sizeDelta确保位置准确
        float cardW = 340f, cardH = 110f, gapX = 16f, gapY = 14f;
        float col0X = -(cardW / 2f + gapX / 2f); // 左列中心x
        float col1X = (cardW / 2f + gapX / 2f);   // 右列中心x

        for (int i = 0; i < BossData.Length; i++)
        {
            int col = i % 2;
            int row = i / 2;
            float xPos = col == 0 ? col0X : col1X;
            float yPos = -361 - row * (cardH + gapY);

            var boss = BossData[i];
            bool def = IsBossDefeated(i);
            var cardColor = def ? boss.theme : new Color(0.15f, 0.15f, 0.18f, 0.92f);

            // 直接用anchoredPosition定位，不用MakeGlowCard的offset方式
            var card = UIHelper.MakeGlowCard(panel.transform, $"Boss_{i}",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, Vector2.zero,
                new Color(0.05f, 0.04f, 0.08f, 0.92f),
                UIHelper.GlowBottom,
                cardColor);
            var cardRt = card.GetComponent<RectTransform>();
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.anchoredPosition = new Vector2(xPos, yPos);
            cardRt.sizeDelta = new Vector2(cardW, cardH);
            card.AddComponent<CardHoverEffect>();

            var iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(card.transform, false);
            var ir = iconObj.AddComponent<RectTransform>();
            ir.anchorMin = new Vector2(0, 0.5f); ir.anchorMax = new Vector2(0, 0.5f);
            ir.pivot = new Vector2(0.5f, 0.5f);
            ir.anchoredPosition = new Vector2(40, 5);
            ir.sizeDelta = new Vector2(55, 55);
            var iconTxt = iconObj.AddComponent<Text>();
            iconTxt.text = def ? "" : "";
            iconTxt.alignment = TextAnchor.MiddleCenter;
            iconTxt.fontSize = 32;
            iconTxt.color = def ? boss.theme : UIHelper.TextDim;
            iconTxt.font = font;
            iconTxt.raycastTarget = false;

            var nameObj = new GameObject("Name");
            nameObj.transform.SetParent(card.transform, false);
            var nr = nameObj.AddComponent<RectTransform>();
            nr.anchorMin = new Vector2(0.18f, 0.55f); nr.anchorMax = new Vector2(0.98f, 0.92f);
            nr.offsetMin = Vector2.zero; nr.offsetMax = Vector2.zero;
            var nameTxt = nameObj.AddComponent<Text>();
            nameTxt.text = def ? boss.name : "???";
            nameTxt.alignment = TextAnchor.MiddleLeft;
            nameTxt.fontSize = 20; nameTxt.color = def ? UIHelper.TextPrimary : UIHelper.TextDim;
            nameTxt.font = font; nameTxt.raycastTarget = false;

            var descObj = new GameObject("Desc");
            descObj.transform.SetParent(card.transform, false);
            var dr2 = descObj.AddComponent<RectTransform>();
            dr2.anchorMin = new Vector2(0.18f, 0.25f); dr2.anchorMax = new Vector2(0.98f, 0.55f);
            dr2.offsetMin = Vector2.zero; dr2.offsetMax = Vector2.zero;
            var descTxt = descObj.AddComponent<Text>();
            descTxt.text = def ? boss.desc : $"击败 {boss.stage} 的Boss后解锁";
            descTxt.alignment = TextAnchor.MiddleLeft;
            descTxt.fontSize = 13; descTxt.color = UIHelper.TextSecondary; descTxt.font = font;
            descTxt.raycastTarget = false;

            var statusObj = new GameObject("Status");
            statusObj.transform.SetParent(card.transform, false);
            var sr2 = statusObj.AddComponent<RectTransform>();
            sr2.anchorMin = new Vector2(0.18f, 0.05f); sr2.anchorMax = new Vector2(0.98f, 0.25f);
            sr2.offsetMin = Vector2.zero; sr2.offsetMax = Vector2.zero;
            var statusTxt = statusObj.AddComponent<Text>();
            statusTxt.alignment = TextAnchor.MiddleLeft;
            statusTxt.fontSize = 14; statusTxt.font = font; statusTxt.raycastTarget = false;
            if (def)
            {
                statusTxt.text = $"{boss.stage}  {boss.goldReward}金";
                statusTxt.color = boss.theme;
            }
            else
            {
                statusTxt.text = "未解锁";
                statusTxt.color = UIHelper.TextDim;
            }
        }

        panel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (panel != null) Destroy(panel);
        if (Instance == this) Instance = null;
    }
}
