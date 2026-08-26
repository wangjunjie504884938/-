using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Editor utility to bake the HUD layout into a prefab asset.
/// Run via menu: Tools > Build HUD Prefab
/// </summary>
public static class HudPrefabBuilder
{
    [MenuItem("Tools/Build HUD Prefab")]
    public static void Build()
    {
        // Ensure folder
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        Font font = GetUIFont();

        // Root
        GameObject root = new GameObject("GameHUD");
        RectTransform rootRect = root.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero; rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero; rootRect.offsetMax = Vector2.zero;

        // --- HP Bar ---
        GameObject hpBarObj = CreateHpBar(root, font);
        Slider hpSlider = hpBarObj.GetComponent<Slider>();

        // --- Info texts ---
        Text levelText = CreateText(root.transform, "LevelText", new Vector2(10, -95), new Vector2(200, 40), 28, Color.white, font);
        Text xpText = CreateText(root.transform, "XpText", new Vector2(10, -135), new Vector2(250, 35), 22, Color.cyan, font);
        Text dungeonLevelText = CreateText(root.transform, "DungeonLevelText", new Vector2(10, -170), new Vector2(250, 35), 22, Color.yellow, font);
        Text killCountText = CreateText(root.transform, "KillCountText", new Vector2(10, -205), new Vector2(250, 35), 22, Color.white, font);
        Text goldText = CreateText(root.transform, "GoldText", new Vector2(10, -240), new Vector2(250, 40), 26, new Color(1f, 0.85f, 0.2f), font);
        Text enemyCountText = CreateText(root.transform, "EnemyCountText", new Vector2(10, -275), new Vector2(300, 35), 22, new Color(1f, 0.6f, 0.6f), font);

        // --- Stat detail text ---
        Text statDetailText = CreateText(root.transform, "StatDetailText",
            new Vector2(-20, -20), new Vector2(380, 110), 22, new Color(0.8f, 0.8f, 0.8f), font,
            anchorMin: new Vector2(1, 1), anchorMax: new Vector2(1, 1), pivot: new Vector2(1, 1),
            alignment: TextAnchor.UpperRight);

        // --- Shield Bar ---
        GameObject shieldBar = CreateShieldBar(root.transform, font);

        // --- Wave Announcement ---
        Text waveAnnounce = CreateText(root.transform, "WaveAnnounce",
            new Vector2(0, 80), new Vector2(400, 80), 48, new Color(1f, 0.85f, 0.2f), font,
            anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f),
            pivot: new Vector2(0.5f, 0.5f), alignment: TextAnchor.MiddleCenter);
        waveAnnounce.gameObject.SetActive(false);

        // --- Combo Counter ---
        Text comboText = CreateText(root.transform, "ComboCounter",
            new Vector2(0, -200), new Vector2(250, 50), 30, new Color(1f, 0.9f, 0.2f), font,
            anchorMin: new Vector2(0.5f, 1), anchorMax: new Vector2(0.5f, 1),
            pivot: new Vector2(0.5f, 1), alignment: TextAnchor.MiddleCenter);
        comboText.gameObject.SetActive(false);

        // --- Boss Health Panel ---
        GameObject bossPanel = CreateBossPanel(root.transform, font);

        // --- Skill Buttons (placeholders, re-bound at runtime for class data) ---
        Image s1Overlay; Text s1Text; Image s2Overlay; Text s2Text;
        Image s3Overlay; Text s3Text; Image dashOverlay; Text dashText;
        CreateSkillButton(root.transform, "Skill1Btn", "Skill1", new Vector2(-170, 210),
            new Color(0.2f, 0.45f, 0.9f, 0.7f), new Color(0.4f, 0.65f, 1f, 0.9f), font,
            out s1Overlay, out s1Text);
        CreateSkillButton(root.transform, "Skill2Btn", "Skill2", new Vector2(-280, 140),
            new Color(0.85f, 0.4f, 0.1f, 0.7f), new Color(1f, 0.6f, 0.2f, 0.9f), font,
            out s2Overlay, out s2Text);
        CreateSkillButton(root.transform, "Skill3Btn", "Skill3", new Vector2(-280, 280),
            new Color(0.15f, 0.75f, 0.35f, 0.7f), new Color(0.3f, 1f, 0.5f, 0.9f), font,
            out s3Overlay, out s3Text);
        CreateSkillButton(root.transform, "DashBtn", "闪避", new Vector2(-170, 330),
            new Color(0.4f, 0.4f, 0.5f, 0.7f), new Color(0.6f, 0.6f, 0.75f, 0.9f), font,
            out dashOverlay, out dashText);

        // --- Game Over Panel (placeholder) ---
        GameObject gameOverPanel = CreatePanelPlaceholder(root.transform, "GameOverPanel", font, "游戏结束");

        // --- Upgrade Panel (placeholder) ---
        GameObject upgradePanel = CreatePanelPlaceholder(root.transform, "UpgradePanel", font, "升级");

        // --- Dungeon Complete Panel (placeholder) ---
        GameObject dungeonCompletePanel = CreatePanelPlaceholder(root.transform, "DungeonCompletePanel", font, "副本完成");

        // Save as prefab
        string prefabPath = "Assets/Prefabs/GameHUD.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        Debug.Log($"HUD prefab saved to {prefabPath}");
    }

    private static Font GetUIFont()
    {
        string[] guids = AssetDatabase.FindAssets("t:Font");
        foreach (var g in guids)
        {
            var f = AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(g));
            if (f != null) return f;
        }
        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private static GameObject CreateHpBar(GameObject parent, Font font)
    {
        GameObject obj = new GameObject("HpBar");
        obj.transform.SetParent(parent.transform, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(10, -60);
        rect.sizeDelta = new Vector2(280, 28);
        Image bg = obj.AddComponent<Image>(); bg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(obj.transform, false);
        RectTransform fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero; fillRect.offsetMax = Vector2.zero;
        Image fillImg = fillObj.AddComponent<Image>(); fillImg.color = Color.green;

        Slider slider = obj.AddComponent<Slider>();
        slider.targetGraphic = fillImg; slider.fillRect = fillRect;
        slider.handleRect = null; slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0; slider.maxValue = 100; slider.value = 100; slider.interactable = false;
        return obj;
    }

    private static GameObject CreateShieldBar(Transform parent, Font font)
    {
        GameObject obj = new GameObject("ShieldBar");
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(10, -90);
        rect.sizeDelta = new Vector2(280, 12);
        Image bg = obj.AddComponent<Image>(); bg.color = new Color(0.15f, 0.15f, 0.15f, 0.6f);

        GameObject fillObj = new GameObject("ShieldFill");
        fillObj.transform.SetParent(obj.transform, false);
        RectTransform fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero; fillRect.offsetMax = Vector2.zero;
        Image fillImg = fillObj.AddComponent<Image>(); fillImg.color = new Color(0.6f, 0.8f, 1f, 0.8f);

        obj.SetActive(false);
        return obj;
    }

    private static GameObject CreateBossPanel(Transform parent, Font font)
    {
        GameObject panel = new GameObject("BossHealthPanel");
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1); rect.anchorMax = new Vector2(0.5f, 1);
        rect.pivot = new Vector2(0.5f, 1); rect.anchoredPosition = new Vector2(0, -60);
        rect.sizeDelta = new Vector2(300, 70);
        Image bg = panel.AddComponent<Image>(); bg.color = new Color(0.1f, 0.05f, 0.15f, 0.85f);
        panel.SetActive(false);

        // Boss name
        GameObject nameObj = new GameObject("BossName");
        nameObj.transform.SetParent(panel.transform, false);
        RectTransform nameRect = nameObj.AddComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0, 1); nameRect.anchorMax = new Vector2(1, 1);
        nameRect.pivot = new Vector2(0.5f, 1); nameRect.anchoredPosition = new Vector2(0, -5);
        nameRect.sizeDelta = new Vector2(280, 25);
        Text nameText = nameObj.AddComponent<Text>();
        nameText.alignment = TextAnchor.MiddleCenter; nameText.fontSize = 20;
        nameText.color = new Color(1f, 0.5f, 0.8f); nameText.font = font;

        // Boss HP bar
        GameObject barObj = new GameObject("BossHpBar");
        barObj.transform.SetParent(panel.transform, false);
        RectTransform barRect = barObj.AddComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0, 0); barRect.anchorMax = new Vector2(1, 0.6f);
        barRect.offsetMin = new Vector2(10, 0); barRect.offsetMax = new Vector2(-10, 0);
        Image barBg = barObj.AddComponent<Image>(); barBg.color = new Color(0.2f, 0.1f, 0.1f, 0.9f);

        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(barObj.transform, false);
        RectTransform fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero; fillRect.offsetMax = Vector2.zero;
        Image fillImg = fillObj.AddComponent<Image>(); fillImg.color = new Color(0.8f, 0.2f, 0.8f);

        Slider slider = barObj.AddComponent<Slider>();
        slider.targetGraphic = fillImg; slider.fillRect = fillRect;
        slider.handleRect = null; slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0; slider.maxValue = 100; slider.value = 100; slider.interactable = false;

        // Boss HP text
        GameObject hpTextObj = new GameObject("BossHpText");
        hpTextObj.transform.SetParent(panel.transform, false);
        RectTransform hpRect = hpTextObj.AddComponent<RectTransform>();
        hpRect.anchorMin = Vector2.zero; hpRect.anchorMax = Vector2.one;
        hpRect.offsetMin = Vector2.zero; hpRect.offsetMax = Vector2.zero;
        Text hpText = hpTextObj.AddComponent<Text>();
        hpText.alignment = TextAnchor.MiddleCenter; hpText.fontSize = 16;
        hpText.color = Color.white; hpText.font = font;

        return panel;
    }

    private static void CreateSkillButton(Transform parent, string name, string label, Vector2 pos,
        Color bgColor, Color borderColor, Font font, out Image cooldownOverlay, out Text cooldownText)
    {
        GameObject btn = new GameObject(name);
        btn.transform.SetParent(parent, false);
        RectTransform rect = btn.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1, 0); rect.anchorMax = new Vector2(1, 0);
        rect.pivot = new Vector2(1, 0); rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(80, 80);
        Image bg = btn.AddComponent<Image>(); bg.color = bgColor;

        // Label
        GameObject lbl = new GameObject("Label");
        lbl.transform.SetParent(btn.transform, false);
        RectTransform lRect = lbl.AddComponent<RectTransform>();
        lRect.anchorMin = Vector2.zero; lRect.anchorMax = Vector2.one;
        lRect.offsetMin = Vector2.zero; lRect.offsetMax = Vector2.zero;
        Text txt = lbl.AddComponent<Text>();
        txt.text = label; txt.alignment = TextAnchor.MiddleCenter;
        txt.fontSize = 14; txt.color = Color.white; txt.font = font;
        cooldownText = txt;

        // Cooldown overlay
        GameObject overlay = new GameObject("Cooldown");
        overlay.transform.SetParent(btn.transform, false);
        RectTransform oRect = overlay.AddComponent<RectTransform>();
        oRect.anchorMin = Vector2.zero; oRect.anchorMax = Vector2.one;
        oRect.offsetMin = Vector2.zero; oRect.offsetMax = Vector2.zero;
        Image oImg = overlay.AddComponent<Image>();
        oImg.color = new Color(0, 0, 0, 0.6f);
        overlay.gameObject.SetActive(false);
        cooldownOverlay = oImg;
    }

    private static GameObject CreatePanelPlaceholder(Transform parent, string name, Font font, string title)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        Image bg = panel.AddComponent<Image>(); bg.color = new Color(0, 0, 0, 0.8f);
        panel.SetActive(false);
        return panel;
    }

    private static Text CreateText(Transform parent, string name, Vector2 anchoredPos, Vector2 size,
        int fontSize, Color color, Font font,
        Vector2? anchorMin = null, Vector2? anchorMax = null, Vector2? pivot = null,
        TextAnchor alignment = TextAnchor.UpperLeft)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin ?? new Vector2(0, 1);
        rect.anchorMax = anchorMax ?? new Vector2(0, 1);
        rect.pivot = pivot ?? new Vector2(0, 1);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;
        Text text = obj.AddComponent<Text>();
        text.alignment = alignment; text.fontSize = fontSize;
        text.color = color; text.font = font;
        return text;
    }
}
