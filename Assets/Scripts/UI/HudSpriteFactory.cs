using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Procedural sprite generation for HUD elements.
/// All sprites are cached statically — created once, reused across frames.
/// </summary>
public static class HudSpriteFactory
{
    // === Cached sprites ===
    private static Sprite _ornateRingSprite;
    private static Sprite _joystickRingSprite;
    private static Sprite _joystickHandleSprite;
    private static Sprite _portraitSprite;
    private static readonly Dictionary<int, Sprite> _darkFillCache = new Dictionary<int, Sprite>();

    /// <summary>Golden metallic ring with beveled edges and cardinal dots — for action buttons.</summary>
    public static Sprite CreateOrnateRingSprite(int size)
    {
        if (_ornateRingSprite != null) return _ornateRingSprite;

        Texture2D tex = new Texture2D(size, size);
        Color[] px = new Color[size * size];
        float center = size / 2f;
        float outerEdge = size / 2f - 0.5f;
        float ringOuter = size / 2f - 2f;
        float ringInner = size / 2f - 6f;
        float shadowEnd = size / 2f - 8f;

        Color brightGold = new Color(0.85f, 0.68f, 0.15f, 1f);
        Color midGold    = new Color(0.62f, 0.47f, 0.08f, 0.95f);
        Color darkGold   = new Color(0.32f, 0.24f, 0.04f, 0.9f);
        Color darkCenter = new Color(0.05f, 0.05f, 0.09f, 0.85f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center + 0.5f;
                float dy = y - center + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist > outerEdge)
                    px[y * size + x] = Color.clear;
                else if (dist >= ringOuter)
                    px[y * size + x] = brightGold;
                else if (dist >= ringInner)
                    px[y * size + x] = midGold;
                else if (dist >= shadowEnd)
                    px[y * size + x] = darkGold;
                else
                    px[y * size + x] = darkCenter;
            }
        }

        // Decorative dots at cardinal points
        float dotDist = (ringOuter + ringInner) / 2f;
        for (int i = 0; i < 4; i++)
        {
            float angle = i * Mathf.PI / 2f;
            float dotCx = center - 0.5f + Mathf.Cos(angle) * dotDist;
            float dotCy = center - 0.5f + Mathf.Sin(angle) * dotDist;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float ddx = x - dotCx;
                    float ddy = y - dotCy;
                    if (ddx * ddx + ddy * ddy <= 1.5f)
                        px[y * size + x] = brightGold;
                }
            }
        }

        tex.SetPixels(px);
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();
        _ornateRingSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return _ornateRingSprite;
    }

    /// <summary>Dark circle fill with subtle radial glow tint — for button centers.</summary>
    public static Sprite CreateDarkFillSprite(int size, Color glowColor)
    {
        int hash = size * 31 + glowColor.GetHashCode();
        if (_darkFillCache.TryGetValue(hash, out var cached)) return cached;

        Texture2D tex = new Texture2D(size, size);
        Color[] px = new Color[size * size];
        float center = size / 2f;
        float radius = size / 2f - 8f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center + 0.5f;
                float dy = y - center + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist <= radius)
                {
                    float t = dist / radius;
                    float glow = (1f - t) * 0.4f;
                    px[y * size + x] = new Color(
                        0.05f + glowColor.r * glow,
                        0.05f + glowColor.g * glow,
                        0.09f + glowColor.b * glow,
                        0.8f);
                }
                else
                    px[y * size + x] = Color.clear;
            }
        }

        tex.SetPixels(px);
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        _darkFillCache[hash] = sprite;
        return sprite;
    }

    /// <summary>Translucent gray ring — for joystick background.</summary>
    public static Sprite CreateJoystickRingSprite(int size)
    {
        if (_joystickRingSprite != null) return _joystickRingSprite;

        Texture2D tex = new Texture2D(size, size);
        Color[] px = new Color[size * size];
        float center = size / 2f;
        float outerR = size / 2f;
        float innerR = size / 2f - 3f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center + 0.5f;
                float dy = y - center + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist <= outerR && dist >= innerR)
                    px[y * size + x] = new Color(0.6f, 0.6f, 0.65f, 0.2f);
                else if (dist < innerR)
                    px[y * size + x] = new Color(0.1f, 0.1f, 0.15f, 0.06f);
                else
                    px[y * size + x] = Color.clear;
            }
        }

        tex.SetPixels(px);
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();
        _joystickRingSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return _joystickRingSprite;
    }

    /// <summary>Bright translucent circle — for joystick handle.</summary>
    public static Sprite CreateJoystickHandleSprite(int size)
    {
        if (_joystickHandleSprite != null) return _joystickHandleSprite;

        Texture2D tex = new Texture2D(size, size);
        Color[] px = new Color[size * size];
        float center = size / 2f;
        float radius = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center + 0.5f;
                float dy = y - center + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist <= radius)
                {
                    float t = 1f - (dist / radius) * 0.4f;
                    px[y * size + x] = new Color(0.7f * t, 0.72f * t, 0.8f * t, 0.3f);
                }
                else
                    px[y * size + x] = Color.clear;
            }
        }

        tex.SetPixels(px);
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();
        _joystickHandleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return _joystickHandleSprite;
    }

    /// <summary>Small portrait icon — golden ring with dark center, for HUD.</summary>
    public static Sprite CreatePortraitSprite(int size)
    {
        if (_portraitSprite != null) return _portraitSprite;

        Texture2D tex = new Texture2D(size, size);
        Color[] px = new Color[size * size];
        float center = size / 2f;
        float outerR = size / 2f;
        float ringInner = size / 2f - 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center + 0.5f;
                float dy = y - center + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist > outerR)
                    px[y * size + x] = Color.clear;
                else if (dist >= ringInner)
                    px[y * size + x] = new Color(0.62f, 0.47f, 0.08f, 0.9f);
                else
                    px[y * size + x] = new Color(0.08f, 0.08f, 0.12f, 0.9f);
            }
        }

        tex.SetPixels(px);
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();
        _portraitSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return _portraitSprite;
    }

    /// <summary>Creates a skill button GameObject with ornate ring, glow center, label, key hint, and cooldown overlay.</summary>
    public static void CreateSkillButton(Transform canvasTransform, GameUI gameUI, string name, string label, Vector2 pos, Color glowColor, Color borderColor, out Image cooldownOverlay, out Text cooldownText, out SkillButton skillBtn)
    {
        Font font = GameManager.GetUIFont();
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(canvasTransform, false);
        RectTransform btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(1, 0); btnRect.anchorMax = new Vector2(1, 0);
        btnRect.pivot = new Vector2(0.5f, 0.5f); btnRect.anchoredPosition = pos; btnRect.sizeDelta = new Vector2(85, 85);

        // Golden ornate ring
        Image btnBorderImg = btnObj.AddComponent<Image>();
        btnBorderImg.sprite = CreateOrnateRingSprite(64);
        btnBorderImg.color = Color.white;

        // Dark center with class-specific glow tint
        GameObject innerObj = new GameObject("InnerFill");
        innerObj.transform.SetParent(btnObj.transform, false);
        RectTransform innerRect = innerObj.AddComponent<RectTransform>();
        innerRect.anchorMin = new Vector2(0.5f, 0.5f); innerRect.anchorMax = new Vector2(0.5f, 0.5f);
        innerRect.pivot = new Vector2(0.5f, 0.5f);
        innerRect.sizeDelta = new Vector2(68, 68);
        Image innerImg = innerObj.AddComponent<Image>();
        innerImg.sprite = CreateDarkFillSprite(64, glowColor);
        innerImg.color = Color.white;

        // Skill label
        GameObject lblObj = new GameObject("Label");
        lblObj.transform.SetParent(btnObj.transform, false);
        RectTransform lblRect = lblObj.AddComponent<RectTransform>();
        lblRect.anchorMin = Vector2.zero; lblRect.anchorMax = Vector2.one;
        lblRect.offsetMin = new Vector2(4, 4); lblRect.offsetMax = new Vector2(4, -4);
        Text lblText = lblObj.AddComponent<Text>();
        lblText.text = label; lblText.alignment = TextAnchor.MiddleCenter;
        lblText.fontSize = 15; lblText.color = new Color(1f, 0.92f, 0.5f); lblText.font = font;

        // Skill key hint
        GameObject keyObj = new GameObject("KeyHint");
        keyObj.transform.SetParent(btnObj.transform, false);
        RectTransform keyRect = keyObj.AddComponent<RectTransform>();
        keyRect.anchorMin = new Vector2(0.5f, 0); keyRect.anchorMax = new Vector2(0.5f, 0);
        keyRect.pivot = new Vector2(0.5f, 0.5f); keyRect.anchoredPosition = new Vector2(0, 10);
        keyRect.sizeDelta = new Vector2(30, 16);
        Text keyText = keyObj.AddComponent<Text>();
        string keyLabel = name switch { "Skill1Btn" => "Q", "Skill2Btn" => "E", "Skill3Btn" => "R", _ => "⇧" };
        keyText.text = keyLabel; keyText.alignment = TextAnchor.MiddleCenter;
        keyText.fontSize = 11; keyText.color = new Color(1f, 1f, 1f, 0.5f); keyText.font = font;

        // Cooldown overlay
        GameObject cdObj = new GameObject("Cooldown");
        cdObj.transform.SetParent(btnObj.transform, false);
        RectTransform cdRect = cdObj.AddComponent<RectTransform>();
        cdRect.anchorMin = Vector2.zero; cdRect.anchorMax = Vector2.one;
        cdRect.offsetMin = Vector2.zero; cdRect.offsetMax = Vector2.zero;
        Image cdImg = cdObj.AddComponent<Image>();
        cdImg.color = new Color(0, 0, 0, 0.65f); cdImg.type = Image.Type.Filled;
        cdImg.fillMethod = Image.FillMethod.Radial360; cdImg.fillOrigin = 0; cdImg.fillAmount = 0f;
        cooldownOverlay = cdImg;

        GameObject cdTextObj = new GameObject("CDText");
        cdTextObj.transform.SetParent(btnObj.transform, false);
        RectTransform cdTextRect = cdTextObj.AddComponent<RectTransform>();
        cdTextRect.anchorMin = Vector2.zero; cdTextRect.anchorMax = Vector2.one;
        cdTextRect.offsetMin = Vector2.zero; cdTextRect.offsetMax = Vector2.zero;
        Text cdTxt = cdTextObj.AddComponent<Text>();
        cdTxt.alignment = TextAnchor.MiddleCenter; cdTxt.fontSize = 20;
        cdTxt.color = Color.white; cdTxt.font = font; cdTxt.gameObject.SetActive(false);
        cooldownText = cdTxt;

        // Touch input component
        skillBtn = btnObj.AddComponent<SkillButton>();
    }

    /// <summary>Creates a text element anchored to the top-left of a parent transform.</summary>
    public static Text CreateText(Transform parent, string name, Vector2 anchoredPos, Vector2 size, int fontSize, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1); rect.anchoredPosition = anchoredPos; rect.sizeDelta = size;
        Text text = obj.AddComponent<Text>();
        text.alignment = TextAnchor.MiddleLeft; text.fontSize = fontSize;
        text.color = color; text.font = GameManager.GetUIFont();
        return text;
    }
}
