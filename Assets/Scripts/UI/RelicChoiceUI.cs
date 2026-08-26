using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Roguelite 遗物选择弹窗 — 暂停游戏, 显示3个遗物供玩家选择
/// </summary>
public class RelicChoiceUI : MonoBehaviour
{
    public static RelicChoiceUI Instance { get; private set; }

    private GameObject panel;
    private List<RelicData> choices;
    private System.Action<RelicData> onSelected;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (panel != null) Destroy(panel);
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// 显示遗物选择弹窗
    /// </summary>
    public void Show(List<RelicData> relicChoices, System.Action<RelicData> callback)
    {
        if (relicChoices == null || relicChoices.Count == 0)
        {
            callback?.Invoke(null);
            return;
        }

        // If only 1 choice, skip UI
        if (relicChoices.Count == 1)
        {
            callback?.Invoke(relicChoices[0]);
            return;
        }

        choices = relicChoices;
        onSelected = callback;

        if (panel == null) BuildUI();
        panel.SetActive(true);

        // Pause game during selection
        Time.timeScale = 0f;
    }

    private void BuildUI()
    {
        Font font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();

        panel = new GameObject("RelicChoicePanel");
        panel.transform.SetParent(canvas.transform, false);
        RectTransform pr = panel.AddComponent<RectTransform>();
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one;
        pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;

        // Dark overlay
        var bg = new GameObject("BG");
        bg.transform.SetParent(panel.transform, false);
        var bgr = bg.AddComponent<RectTransform>();
        bgr.anchorMin = Vector2.zero; bgr.anchorMax = Vector2.one;
        bgr.offsetMin = Vector2.zero; bgr.offsetMax = Vector2.zero;
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.02f, 0.01f, 0.04f, 0.92f);

        // Title
        var titleObj = new GameObject("Title");
        titleObj.transform.SetParent(panel.transform, false);
        var tr = titleObj.AddComponent<RectTransform>();
        tr.anchorMin = new Vector2(0.5f, 1); tr.anchorMax = new Vector2(0.5f, 1);
        tr.pivot = new Vector2(0.5f, 1); tr.anchoredPosition = new Vector2(0, -60);
        tr.sizeDelta = new Vector2(600, 50);
        var titleTxt = titleObj.AddComponent<Text>();
        titleTxt.text = "选择一件遗物";
        titleTxt.alignment = TextAnchor.MiddleCenter;
        titleTxt.fontSize = 32; titleTxt.color = UIHelper.Accent; titleTxt.font = font;

        // Cards container
        var container = new GameObject("Cards");
        container.transform.SetParent(panel.transform, false);
        var cr = container.AddComponent<RectTransform>();
        cr.anchorMin = new Vector2(0.5f, 0.5f); cr.anchorMax = new Vector2(0.5f, 0.5f);
        cr.pivot = new Vector2(0.5f, 0.5f); cr.anchoredPosition = Vector2.zero;

        float cardW = 280f, cardH = 400f, gap = 40f;
        float totalW = 3 * cardW + 2 * gap;
        float startX = -totalW / 2f + cardW / 2f;

        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            float x = startX + i * (cardW + gap);

            var card = new GameObject($"RelicCard_{i}");
            card.transform.SetParent(container.transform, false);
            var cardRect = card.AddComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f); cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = new Vector2(x, 0);
            cardRect.sizeDelta = new Vector2(cardW, cardH);

            var cardImg = card.AddComponent<Image>();
            Color rarityBg = i < choices.Count ? GetRarityBg(choices[i].Rarity) : new Color(0.05f, 0.04f, 0.08f, 0.95f);
            cardImg.color = rarityBg;

            // Border
            var border = new GameObject("Border");
            border.transform.SetParent(card.transform, false);
            var br = border.AddComponent<RectTransform>();
            br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one;
            br.offsetMin = new Vector2(-2, -2); br.offsetMax = new Vector2(2, 2);
            var bImg = border.AddComponent<Image>();
            bImg.color = i < choices.Count ? GetRarityColor(choices[i].Rarity) : UIHelper.BorderSubtle;
            bImg.raycastTarget = false;
            border.transform.SetAsFirstSibling();

            // Relic icon (colored circle)
            var iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(card.transform, false);
            var ir = iconObj.AddComponent<RectTransform>();
            ir.anchorMin = new Vector2(0.5f, 0.75f); ir.anchorMax = new Vector2(0.5f, 0.75f);
            ir.pivot = new Vector2(0.5f, 0.5f); ir.anchoredPosition = Vector2.zero;
            ir.sizeDelta = new Vector2(100, 100);
            var iconImg = iconObj.AddComponent<Image>();
            iconImg.color = i < choices.Count ? choices[i].IconColor : Color.clear;
            iconImg.raycastTarget = false;

            // Relic name
            var nameObj = new GameObject("Name");
            nameObj.transform.SetParent(card.transform, false);
            var nr = nameObj.AddComponent<RectTransform>();
            nr.anchorMin = new Vector2(0, 0.35f); nr.anchorMax = new Vector2(1, 0.55f);
            nr.offsetMin = new Vector2(8, 0); nr.offsetMax = new Vector2(-8, 0);
            var nameTxt = nameObj.AddComponent<Text>();
            nameTxt.alignment = TextAnchor.MiddleCenter;
            nameTxt.fontSize = 22; nameTxt.color = UIHelper.TextPrimary; nameTxt.font = font;
            nameTxt.text = i < choices.Count ? choices[i].Name : "";

            // Rarity label
            var rarObj = new GameObject("Rarity");
            rarObj.transform.SetParent(card.transform, false);
            var rr = rarObj.AddComponent<RectTransform>();
            rr.anchorMin = new Vector2(0, 0.28f); rr.anchorMax = new Vector2(1, 0.35f);
            rr.offsetMin = Vector2.zero; rr.offsetMax = Vector2.zero;
            var rarTxt = rarObj.AddComponent<Text>();
            rarTxt.alignment = TextAnchor.MiddleCenter;
            rarTxt.fontSize = 14; rarTxt.font = font;
            rarTxt.text = i < choices.Count ? GetRarityLabel(choices[i].Rarity) : "";

            // Description
            var descObj = new GameObject("Desc");
            descObj.transform.SetParent(card.transform, false);
            var dr = descObj.AddComponent<RectTransform>();
            dr.anchorMin = new Vector2(0, 0.05f); dr.anchorMax = new Vector2(1, 0.28f);
            dr.offsetMin = new Vector2(10, 0); dr.offsetMax = new Vector2(-10, 0);
            var descTxt = descObj.AddComponent<Text>();
            descTxt.alignment = TextAnchor.UpperCenter;
            descTxt.fontSize = 15; descTxt.color = UIHelper.TextSecondary; descTxt.font = font;
            descTxt.text = i < choices.Count ? choices[i].Description : "";

            // Button
            if (i < choices.Count)
            {
                var btn = card.AddComponent<Button>();
                btn.targetGraphic = cardImg;
                btn.onClick.AddListener(() => OnRelicSelected(idx));
            }
        }

        panel.SetActive(false);
    }

    private void OnRelicSelected(int index)
    {
        if (index < 0 || index >= choices.Count) return;

        var selected = choices[index];
        panel.SetActive(false);

        // Resume game
        Time.timeScale = 1f;

        onSelected?.Invoke(selected);
        onSelected = null;
    }

    private static Color GetRarityColor(RelicRarity rarity)
    {
        return rarity switch
        {
            RelicRarity.Legendary => new Color(0.8f, 0.55f, 0.15f, 0.9f),
            RelicRarity.Rare => new Color(0.2f, 0.45f, 0.8f, 0.7f),
            _ => new Color(0.4f, 0.4f, 0.45f, 0.5f),
        };
    }

    private static Color GetRarityBg(RelicRarity rarity)
    {
        return rarity switch
        {
            RelicRarity.Legendary => new Color(0.12f, 0.08f, 0.03f, 0.95f),
            RelicRarity.Rare => new Color(0.05f, 0.08f, 0.14f, 0.95f),
            _ => new Color(0.05f, 0.04f, 0.07f, 0.95f),
        };
    }

    private static string GetRarityLabel(RelicRarity rarity)
    {
        return rarity switch
        {
            RelicRarity.Legendary => "★ 传说",
            RelicRarity.Rare => "◆ 稀有",
            _ => "● 普通",
        };
    }
}
