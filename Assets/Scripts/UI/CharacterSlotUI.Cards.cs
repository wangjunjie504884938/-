using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// CharacterSlotUI partial — Slot card creation and visuals
/// </summary>
public partial class CharacterSlotUI
{
    // ========== 卡片创建 ==========

    private GameObject CreateSlotCard(Transform parent, int slotIndex, CloudSaveManager.SlotSummary summary, bool occupied, Font font)
    {
        GameObject card = new GameObject($"Slot_{slotIndex}");
        card.transform.SetParent(parent, false);
        RectTransform cardRect = card.AddComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 1);
        cardRect.anchorMax = new Vector2(0.5f, 1);
        cardRect.pivot = new Vector2(0.5f, 1);
        cardRect.anchoredPosition = new Vector2(-310 + slotIndex * 310, -210);
        cardRect.sizeDelta = new Vector2(280, 400);
        cardRect.localScale = Vector3.zero;
        _cardRects.Add(cardRect);

        HeroClass cardClass = occupied ? (HeroClass)summary.classType : HeroClass.Warrior;
        ClassData cd = ClassData.GetClassData(cardClass);
        Color primaryColor = cd != null ? cd.PrimaryColor : new Color(0.5f, 0.5f, 0.5f);
        _cardColors.Add(primaryColor);
        _cardOccupied.Add(occupied);

        // Card glow
        GameObject glowObj = new GameObject("Glow");
        glowObj.transform.SetParent(card.transform, false);
        RectTransform glowRect = glowObj.AddComponent<RectTransform>();
        glowRect.anchorMin = Vector2.zero; glowRect.anchorMax = Vector2.one;
        glowRect.offsetMin = new Vector2(-8, -8); glowRect.offsetMax = new Vector2(8, 8);
        Image glowImg = glowObj.AddComponent<Image>();
        glowImg.color = occupied
            ? new Color(primaryColor.r, primaryColor.g, primaryColor.b, 0.15f)
            : new Color(0.2f, 0.2f, 0.25f, 0.05f);
        glowImg.raycastTarget = false;
        _cardGlows.Add(glowImg);

        // Card background
        Image cardImg = card.AddComponent<Image>();
        cardImg.color = new Color(primaryColor.r * 0.12f, primaryColor.g * 0.12f, primaryColor.b * 0.12f, 0.95f);
        _cardBgs.Add(cardImg);

        // Border
        GameObject borderObj = new GameObject("Border");
        borderObj.transform.SetParent(card.transform, false);
        RectTransform bRect = borderObj.AddComponent<RectTransform>();
        bRect.anchorMin = Vector2.zero; bRect.anchorMax = Vector2.one;
        bRect.offsetMin = new Vector2(-2, -2); bRect.offsetMax = new Vector2(2, 2);
        Image borderImg = borderObj.AddComponent<Image>();
        borderImg.color = occupied ? primaryColor * 0.6f : new Color(0.3f, 0.3f, 0.35f, 0.5f);
        borderImg.raycastTarget = false;
        _cardBorders.Add(borderImg);

        // Inner background
        GameObject innerObj = new GameObject("Inner");
        innerObj.transform.SetParent(borderObj.transform, false);
        RectTransform iRect = innerObj.AddComponent<RectTransform>();
        iRect.anchorMin = Vector2.zero; iRect.anchorMax = Vector2.one;
        iRect.offsetMin = new Vector2(2, 2); iRect.offsetMax = new Vector2(-2, -2);
        Image innerImg = innerObj.AddComponent<Image>();
        innerImg.color = new Color(0.04f, 0.03f, 0.07f, 0.98f);
        innerImg.raycastTarget = false;

        // Slot number badge
        CreateSlotBadge(card.transform, slotIndex, font, occupied ? primaryColor : new Color(0.4f, 0.4f, 0.45f));

        if (occupied)
        {
            GameObject iconObj = new GameObject("CharIcon");
            iconObj.transform.SetParent(card.transform, false);
            RectTransform iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.65f); iconRect.anchorMax = new Vector2(0.5f, 0.65f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(120, 120);
            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.color = Color.white;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            // 异步加载角色预览Sprite
            StartCoroutine(LoadSlotSprite(cardClass, iconImg));

            CreateText(card.transform, "ClassName", cd?.ClassName ?? "未知", font, 28,
                primaryColor, new Vector2(0, -210), new Vector2(260, 36));

            // Character name (from server summary, fall back to PlayerPrefs)
            string charName = !string.IsNullOrEmpty(summary?.characterName)
                ? summary.characterName
                : SlotData.GetCharacterName(slotIndex);
            if (!string.IsNullOrEmpty(charName))
                CreateText(card.transform, "CharName", charName, font, 18,
                    new Color(0.85f, 0.85f, 0.9f), new Vector2(0, -246), new Vector2(260, 40));

            CreateText(card.transform, "Level", $"Lv.{summary.level}", font, 22,
                new Color(0.9f, 0.88f, 0.6f), new Vector2(0, -286), new Vector2(260, 40));

            string stageText = summary.highestStageCleared > 0
                ? $"最高关卡: {summary.highestStageCleared}"
                : "尚未通关";
            CreateText(card.transform, "Stage", stageText, font, 16,
                new Color(0.7f, 0.75f, 0.8f), new Vector2(0, -336), new Vector2(260, 50));
        }
        else
        {
            GameObject plusObj = new GameObject("PlusIcon");
            plusObj.transform.SetParent(card.transform, false);
            RectTransform plusRect = plusObj.AddComponent<RectTransform>();
            plusRect.anchorMin = new Vector2(0.5f, 0.55f); plusRect.anchorMax = new Vector2(0.5f, 0.55f);
            plusRect.pivot = new Vector2(0.5f, 0.5f);
            plusRect.anchoredPosition = Vector2.zero;
            plusRect.sizeDelta = new Vector2(80, 80);
            Text plusText = plusObj.AddComponent<Text>();
            plusText.text = "+";
            plusText.alignment = TextAnchor.MiddleCenter; plusText.fontSize = 60;
            plusText.color = new Color(0.3f, 0.3f, 0.35f, 0.6f); plusText.font = font;
            plusText.raycastTarget = false;

            CreateText(card.transform, "EmptyLabel", "创建角色", font, 22,
                new Color(0.4f, 0.42f, 0.45f), new Vector2(0, -120), new Vector2(260, 32));

            CreateText(card.transform, "EmptyHint", "选择职业开始\n新的冒险", font, 14,
                new Color(0.35f, 0.35f, 0.4f, 0.7f), new Vector2(0, -160), new Vector2(260, 40));
        }

        // Whole-card click → select
        int idx = slotIndex;
        Button cardBtn = card.AddComponent<Button>();
        cardBtn.targetGraphic = cardImg;
        cardBtn.onClick.AddListener(() => OnCardClicked(idx));
        AddHoverEffect(card, cardImg, glowImg, primaryColor, occupied);

        return card;
    }

    private void CreateSlotBadge(Transform parent, int slotIndex, Font font, Color color)
    {
        GameObject badgeObj = new GameObject("SlotBadge");
        badgeObj.transform.SetParent(parent, false);
        RectTransform badgeRect = badgeObj.AddComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(0, 1); badgeRect.anchorMax = new Vector2(0, 1);
        badgeRect.pivot = new Vector2(0, 1);
        badgeRect.anchoredPosition = new Vector2(10, -8);
        badgeRect.sizeDelta = new Vector2(60, 22);
        Image badgeBg = badgeObj.AddComponent<Image>();
        badgeBg.color = new Color(color.r, color.g, color.b, 0.2f);
        badgeBg.raycastTarget = false;

        GameObject badgeBorder = new GameObject("Border");
        badgeBorder.transform.SetParent(badgeObj.transform, false);
        RectTransform bbRect = badgeBorder.AddComponent<RectTransform>();
        bbRect.anchorMin = Vector2.zero; bbRect.anchorMax = Vector2.one;
        bbRect.offsetMin = new Vector2(-1, -1); bbRect.offsetMax = new Vector2(1, 1);
        Image bbImg = badgeBorder.AddComponent<Image>();
        bbImg.color = new Color(color.r, color.g, color.b, 0.5f);
        bbImg.raycastTarget = false;

        GameObject badgeInner = new GameObject("Inner");
        badgeInner.transform.SetParent(badgeBorder.transform, false);
        RectTransform biRect = badgeInner.AddComponent<RectTransform>();
        biRect.anchorMin = Vector2.zero; biRect.anchorMax = Vector2.one;
        biRect.offsetMin = Vector2.zero; biRect.offsetMax = Vector2.zero;
        Image biImg = badgeInner.AddComponent<Image>();
        biImg.color = new Color(0.06f, 0.05f, 0.10f, 0.9f);
        biImg.raycastTarget = false;

        GameObject badgeLabel = new GameObject("Label");
        badgeLabel.transform.SetParent(badgeObj.transform, false);
        RectTransform blRect = badgeLabel.AddComponent<RectTransform>();
        blRect.anchorMin = Vector2.zero; blRect.anchorMax = Vector2.one;
        blRect.offsetMin = Vector2.zero; blRect.offsetMax = Vector2.zero;
        Text blText = badgeLabel.AddComponent<Text>();
        blText.text = $"#{slotIndex + 1}";
        blText.alignment = TextAnchor.MiddleCenter; blText.fontSize = 14;
        blText.color = color; blText.font = font;
        blText.raycastTarget = false;
    }

    // ========== 选中状态 ==========
}
