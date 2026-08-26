using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 商业级 UI 工具类 — 提供渐变背景、卡片样式、动画过渡等复用功能
/// </summary>
public static class UIHelper
{
    // ====== 配色方案 (暗紫虚空ARPG风格) ======
    public static readonly Color BgDark = new Color(0.04f, 0.02f, 0.06f, 0.98f);
    public static readonly Color BgPanel = new Color(0.06f, 0.03f, 0.09f, 0.98f);
    public static readonly Color CardBg = new Color(0.07f, 0.04f, 0.11f, 0.92f);
    public static readonly Color CardHover = new Color(0.11f, 0.06f, 0.16f, 0.92f);
    public static readonly Color Accent = new Color(0.55f, 0.30f, 0.75f, 1f);       // 暗紫
    public static readonly Color AccentDim = new Color(0.30f, 0.15f, 0.45f, 0.5f);
    public static readonly Color TextPrimary = new Color(0.96f, 0.94f, 0.98f, 1f);
    public static readonly Color TextSecondary = new Color(0.62f, 0.56f, 0.70f, 1f);
    public static readonly Color TextDim = new Color(0.40f, 0.34f, 0.48f, 1f);
    public static readonly Color BorderSubtle = new Color(0.18f, 0.12f, 0.26f, 0.6f);
    public static readonly Color BtnPrimary = new Color(0.10f, 0.06f, 0.18f, 0.95f);
    public static readonly Color BtnConfirm = new Color(0.08f, 0.22f, 0.12f, 0.95f);
    public static readonly Color BtnDanger = new Color(0.40f, 0.10f, 0.10f, 0.95f);
    public static readonly Color BtnGold = new Color(0.10f, 0.06f, 0.18f, 0.95f);
    public static readonly Color BackBtnBg = new Color(0.10f, 0.06f, 0.14f, 0.85f);

    // ====== MakeGlowCard 标准渐变色 ======
    public static readonly Color GlowTop = new Color(0.06f, 0.03f, 0.09f, 0.92f);

    /// <summary>
    /// 为按钮添加防连点保护 — isProcessing=true时点击无效
    /// </summary>
    public static void SetupAntiSpam(Button btn, float cooldownSeconds = 0.5f)
    {
        btn.interactable = true;
        bool isProcessing = false;
        btn.onClick.AddListener(() =>
        {
            if (isProcessing) return;
            isProcessing = true;
            btn.interactable = false;
            // 延迟恢复，防止连点
            var runner = btn.GetComponent<VFXRunner>();
            if (runner == null) runner = btn.gameObject.AddComponent<VFXRunner>();
            runner.StartCoroutine(ResetButton(btn, () => isProcessing = false, cooldownSeconds));
        });
    }

    private static System.Collections.IEnumerator ResetButton(Button btn, System.Action reset, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (btn != null) btn.interactable = true;
        reset?.Invoke();
    }
    public static void SetupButtonFeedback(Button btn, Color baseColor, float darken = 0.7f)
    {
        var img = btn.GetComponent<Image>();
        if (img == null) return;

        var trigger = btn.GetComponent<EventTrigger>();
        if (trigger == null) trigger = btn.gameObject.AddComponent<EventTrigger>();

        Color pressedColor = new Color(baseColor.r * darken, baseColor.g * darken, baseColor.b * darken, baseColor.a);

        var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        down.callback.AddListener(_ => img.color = pressedColor);
        trigger.triggers.Add(down);

        var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        up.callback.AddListener(_ => img.color = baseColor);
        trigger.triggers.Add(up);

        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => img.color = baseColor);
        trigger.triggers.Add(exit);
    }
    public static readonly Color GlowBottom = new Color(0.03f, 0.02f, 0.05f, 0.92f);

    // ====== 按钮颜色 ======
    public static readonly Color BtnSell = new Color(0.35f, 0.22f, 0.08f, 0.92f);
    public static readonly Color BtnUpgrade = new Color(0.12f, 0.08f, 0.22f, 0.92f);
    public static readonly Color BtnEquip = new Color(0.06f, 0.18f, 0.10f, 0.92f);
    public static readonly Color BtnUnequip = new Color(0.35f, 0.15f, 0.10f, 0.92f);
    public static readonly Color BtnReplace = new Color(0.12f, 0.08f, 0.22f, 0.92f);

    // ====== 属性显示色 ======
    public static readonly Color StatHp = new Color(0.55f, 0.85f, 0.55f, 1f);
    public static readonly Color StatAtk = new Color(0.90f, 0.65f, 0.40f, 1f);
    public static readonly Color StatDef = new Color(0.50f, 0.55f, 0.85f, 1f);

    // ====== 暗色主题适配稀有度色 ======
    public static readonly Color RarityCommon = new Color(0.55f, 0.55f, 0.58f, 1f);
    public static readonly Color RarityRare = new Color(0.20f, 0.45f, 0.80f, 1f);
    public static readonly Color RarityEpic = new Color(0.55f, 0.25f, 0.75f, 1f);
    public static readonly Color RarityLegendary = new Color(0.80f, 0.55f, 0.15f, 1f);

    // ====== 基础组件 ======

    /// <summary>创建全屏暗色背景(带微妙渐变)</summary>
    public static GameObject MakeBackground(Transform parent)
    {
        var go = new GameObject("BG");
        go.transform.SetParent(parent, false);
        var r = go.AddComponent<RectTransform>();
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;

        // 底层纯色
        var img = go.AddComponent<Image>();
        img.color = BgDark;
        img.raycastTarget = false;

        return go;
    }

    /// <summary>创建卡片容器(圆角感+边框)</summary>
    public static GameObject MakeCard(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var r = go.AddComponent<RectTransform>();
        r.anchorMin = anchorMin; r.anchorMax = anchorMax;
        r.offsetMin = offsetMin; r.offsetMax = offsetMax;

        // 背景
        var bg = go.AddComponent<Image>();
        bg.color = CardBg;

        // 边框
        var border = new GameObject("Border");
        border.transform.SetParent(go.transform, false);
        var br = border.AddComponent<RectTransform>();
        br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one;
        br.offsetMin = new Vector2(-1, -1); br.offsetMax = new Vector2(1, 1);
        var bi = border.AddComponent<Image>();
        bi.color = BorderSubtle;
        bi.raycastTarget = false;
        border.transform.SetAsFirstSibling();

        // 顶部金色细线
        var line = new GameObject("TopLine");
        line.transform.SetParent(go.transform, false);
        var lr = line.AddComponent<RectTransform>();
        lr.anchorMin = new Vector2(0.05f, 1); lr.anchorMax = new Vector2(0.95f, 1);
        lr.pivot = new Vector2(0.5f, 1);
        lr.sizeDelta = new Vector2(0, 1);
        var li = line.AddComponent<Image>();
        li.color = AccentDim;
        li.raycastTarget = false;

        return go;
    }

    /// <summary>创建标题文字(金色+居中)</summary>
    public static Text MakeTitle(Transform parent, string text, Font font, int fontSize = 32)
    {
        var go = new GameObject("Title");
        go.transform.SetParent(parent, false);
        var r = go.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1);
        r.pivot = new Vector2(0.5f, 1);
        r.anchoredPosition = new Vector2(0, -68);
        r.sizeDelta = new Vector2(0, 36);
        var t = go.AddComponent<Text>();
        t.text = text; t.alignment = TextAnchor.MiddleCenter;
        t.fontSize = fontSize; t.color = Accent; t.font = font;
        return t;
    }

    /// <summary>导航顶栏：独占一行(56px)，返回(可选) + 设置</summary>
    public static void MakeNavButtons(Transform parent, Font font, System.Action onBack = null)
    {
        float btnSize = 40f;
        float gap = 6f;
        float barH = 56f;
        float startX = 12f;
        float posY = -(barH - btnSize) / 2f;

        // 顶栏背景条 — 全宽，56px高
        var navBar = new GameObject("_NavBar");
        navBar.transform.SetParent(parent, false);
        var nbr = navBar.AddComponent<RectTransform>();
        nbr.anchorMin = new Vector2(0, 1); nbr.anchorMax = new Vector2(1, 1);
        nbr.pivot = new Vector2(0.5f, 1);
        nbr.sizeDelta = new Vector2(0, barH);
        nbr.anchoredPosition = Vector2.zero;
        var nbImg = navBar.AddComponent<Image>();
        nbImg.color = UIHelper.BgPanel;
        nbImg.raycastTarget = true;

        // 底部细线
        var line = new GameObject("BottomLine");
        line.transform.SetParent(navBar.transform, false);
        var lr = line.AddComponent<RectTransform>();
        lr.anchorMin = new Vector2(0, 0); lr.anchorMax = new Vector2(1, 0);
        lr.pivot = new Vector2(0.5f, 0);
        lr.sizeDelta = new Vector2(0, 1);
        var li = line.AddComponent<Image>();
        li.color = UIHelper.BorderSubtle;
        li.raycastTarget = false;

        Color navBorder = new Color(0.25f, 0.12f, 0.40f, 0.6f);
        Color navFill = new Color(0.06f, 0.04f, 0.09f, 0.85f);

        // ← 返回按钮
        if (onBack != null)
        {
            var backObj = new GameObject("BackBtn");
            backObj.transform.SetParent(navBar.transform, false);
            var br = backObj.AddComponent<RectTransform>();
            br.anchorMin = new Vector2(0, 0.5f); br.anchorMax = new Vector2(0, 0.5f);
            br.pivot = new Vector2(0, 0.5f);
            br.anchoredPosition = new Vector2(startX, posY);
            br.sizeDelta = new Vector2(btnSize, btnSize);
            var bBg = backObj.AddComponent<Image>();
            bBg.color = navFill;
            // Border
            var bBorder = new GameObject("Border");
            bBorder.transform.SetParent(backObj.transform, false);
            var bbR = bBorder.AddComponent<RectTransform>();
            bbR.anchorMin = Vector2.zero; bbR.anchorMax = Vector2.one;
            bbR.offsetMin = new Vector2(-1, -1); bbR.offsetMax = new Vector2(1, 1);
            var bbImg = bBorder.AddComponent<Image>();
            bbImg.color = navBorder;
            bbImg.raycastTarget = false;
            bBorder.transform.SetAsFirstSibling();
            var bBtn = backObj.AddComponent<Button>();
            bBtn.transition = Selectable.Transition.None;
            SetupButtonFeedback(bBtn, navFill);
            bBtn.onClick.AddListener(() => onBack());
            var bLbl = new GameObject("Lbl");
            bLbl.transform.SetParent(backObj.transform, false);
            var blR = bLbl.AddComponent<RectTransform>();
            blR.anchorMin = Vector2.zero; blR.anchorMax = Vector2.one;
            blR.offsetMin = Vector2.zero; blR.offsetMax = Vector2.zero;
            var bTxt = bLbl.AddComponent<Text>();
            bTxt.text = "←"; bTxt.alignment = TextAnchor.MiddleCenter;
            bTxt.fontSize = 22; bTxt.color = TextPrimary; bTxt.font = font;
            bTxt.raycastTarget = false;
        }

        // 设置齿轮按钮
        float gearX = onBack != null ? startX + btnSize + gap : startX;
        var gearObj = new GameObject("GearBtn");
        gearObj.transform.SetParent(navBar.transform, false);
        var gr = gearObj.AddComponent<RectTransform>();
        gr.anchorMin = new Vector2(0, 0.5f); gr.anchorMax = new Vector2(0, 0.5f);
        gr.pivot = new Vector2(0, 0.5f);
        gr.anchoredPosition = new Vector2(gearX, posY);
        gr.sizeDelta = new Vector2(btnSize, btnSize);
        var gBg = gearObj.AddComponent<Image>();
        gBg.color = navFill;
        // Border
        var gBorder = new GameObject("Border");
        gBorder.transform.SetParent(gearObj.transform, false);
        var gbR = gBorder.AddComponent<RectTransform>();
        gbR.anchorMin = Vector2.zero; gbR.anchorMax = Vector2.one;
        gbR.offsetMin = new Vector2(-1, -1); gbR.offsetMax = new Vector2(1, 1);
        var gbImg = gBorder.AddComponent<Image>();
        gbImg.color = navBorder;
        gbImg.raycastTarget = false;
        gBorder.transform.SetAsFirstSibling();
        var gBtn = gearObj.AddComponent<Button>();
        gBtn.transition = Selectable.Transition.None;
        SetupButtonFeedback(gBtn, navFill);
        gBtn.onClick.AddListener(() =>
        {
            EnsureUIManager();
            UIManager.Instance.ShowSettings();
        });
        var gIcon = new GameObject("GearIcon");
        gIcon.transform.SetParent(gearObj.transform, false);
        var giR = gIcon.AddComponent<RectTransform>();
        giR.anchorMin = Vector2.zero; giR.anchorMax = Vector2.one;
        giR.offsetMin = new Vector2(7, 7); giR.offsetMax = new Vector2(-7, -7);
        var giImg = gIcon.AddComponent<Image>();
        giImg.sprite = CreateGearSprite();
        giImg.color = TextPrimary;
    }

    /// <summary>确保 UIManager 单例存在</summary>
    public static void EnsureUIManager()
    {
        if (UIManager.Instance == null)
            GameManager.Instance.gameObject.AddComponent<UIManager>();
    }

    /// <summary>确保 TopNavBar 单例存在并返回</summary>
    public static TopNavBar EnsureTopNavBar()
    {
        if (TopNavBar.Instance == null)
        {
            var go = new GameObject("TopNavBar");
            go.transform.SetParent(GameManager.EnsureCanvas().transform, false);
            go.AddComponent<TopNavBar>();
        }
        return TopNavBar.Instance;
    }

    /// <summary>设置顶部导航栏(替代 MakeTitle + MakeNavButtons)</summary>
    public static void SetupTopBar(string title, System.Action onBack = null)
    {
        EnsureTopNavBar().Setup(title, onBack);
    }

    /// <summary>程序化齿轮纹理(8齿) — cached to avoid repeated allocation</summary>
    private static Sprite _gearSprite;
    public static Sprite CreateGearSprite()
    {
        if (_gearSprite != null) return _gearSprite;

        int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] px = new Color[size * size];
        Color c = Color.white;
        float cx = size / 2f, cy = size / 2f;
        float outerR = 13f, innerR = 8f, holeR = 4f;
        int teeth = 8;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - cx + 0.5f, dy = y - cy + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);
                float toothPhase = (angle + Mathf.PI) / (2f * Mathf.PI) * teeth;
                float toothFrac = toothPhase % 1f;
                bool isTooth = toothFrac < 0.35f || toothFrac > 0.65f;
                bool inOuter = dist <= (isTooth ? outerR : innerR);
                bool inHole = dist <= holeR;
                px[y * size + x] = (inOuter && !inHole) ? c : Color.clear;
            }
        }
        tex.SetPixels(px);
        tex.filterMode = FilterMode.Point;
        tex.Apply();
        _gearSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return _gearSprite;
    }

    /// <summary>创建图标按钮(如齿轮)</summary>
    public static GameObject MakeIconButton(Transform parent, string name, Font font, string icon,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size,
        Color bgColor, Color iconColor, System.Action onClick)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var r = go.AddComponent<RectTransform>();
        r.anchorMin = anchorMin; r.anchorMax = anchorMax;
        r.pivot = new Vector2(1, 1);
        r.anchoredPosition = anchoredPos;
        r.sizeDelta = size;

        var bg = go.AddComponent<Image>();
        bg.color = bgColor;

        var btn = go.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(() => onClick());
        SetupButtonFeedback(btn, bgColor);

        var lbl = new GameObject("Icon");
        lbl.transform.SetParent(go.transform, false);
        var lr = lbl.AddComponent<RectTransform>();
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
        lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;
        var t = lbl.AddComponent<Text>();
        t.text = icon; t.alignment = TextAnchor.MiddleCenter;
        t.fontSize = 24; t.color = iconColor; t.font = font;

        return go;
    }

    /// <summary>创建标准按钮</summary>
    public static GameObject MakeButton(Transform parent, string name, string label, Font font,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
        Color bgColor, int fontSize = 22, System.Action onClick = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var r = go.AddComponent<RectTransform>();
        r.anchorMin = anchorMin; r.anchorMax = anchorMax;
        r.offsetMin = offsetMin; r.offsetMax = offsetMax;

        var bg = go.AddComponent<Image>();
        bg.color = bgColor;

        if (onClick != null)
        {
            var btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => onClick());
            SetupButtonFeedback(btn, bgColor);
        }

        var lbl = new GameObject("Lbl");
        lbl.transform.SetParent(go.transform, false);
        var lr = lbl.AddComponent<RectTransform>();
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
        lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;
        var t = lbl.AddComponent<Text>();
        t.text = label; t.alignment = TextAnchor.MiddleCenter;
        t.fontSize = fontSize; t.color = TextPrimary; t.font = font;

        return go;
    }

    /// <summary>创建分割线</summary>
    public static GameObject MakeDivider(Transform parent, float topOffset)
    {
        var go = new GameObject("Divider");
        go.transform.SetParent(parent, false);
        var r = go.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(0.08f, 1); r.anchorMax = new Vector2(0.92f, 1);
        r.pivot = new Vector2(0.5f, 1);
        r.anchoredPosition = new Vector2(0, -topOffset);
        r.sizeDelta = new Vector2(0, 1);
        var img = go.AddComponent<Image>();
        img.color = BorderSubtle;
        return go;
    }

    /// <summary>创建副标题(如"— 装备商店 —")</summary>
    public static Text MakeSubtitle(Transform parent, string text, Font font, float topOffset, int fontSize = 18)
    {
        var go = new GameObject("Subtitle");
        go.transform.SetParent(parent, false);
        var r = go.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1);
        r.pivot = new Vector2(0.5f, 1);
        r.anchoredPosition = new Vector2(0, -topOffset);
        r.sizeDelta = new Vector2(0, 28);
        var t = go.AddComponent<Text>();
        t.text = text; t.alignment = TextAnchor.MiddleCenter;
        t.fontSize = fontSize; t.color = TextSecondary; t.font = font;
        return t;
    }

    // ====== 动画过渡 ======

    /// <summary>面板淡入(配合CanvasGroup)</summary>
    public static IEnumerator FadeIn(GameObject panel, float duration = 0.2f)
    {
        var cg = panel.GetComponent<CanvasGroup>();
        if (cg == null) cg = panel.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Clamp01(t / duration);
            yield return null;
        }
        cg.alpha = 1f;
        cg.blocksRaycasts = true;
    }

    /// <summary>面板淡出</summary>
    public static IEnumerator FadeOut(GameObject panel, float duration = 0.15f)
    {
        if (panel == null) yield break;
        var cg = panel.GetComponent<CanvasGroup>();
        if (cg == null) cg = panel.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        float t = 0f;
        while (t < duration)
        {
            if (panel == null) yield break;
            t += Time.unscaledDeltaTime;
            cg.alpha = 1f - Mathf.Clamp01(t / duration);
            yield return null;
        }
        if (panel != null)
        {
            cg.alpha = 0f;
            panel.SetActive(false);
            cg.alpha = 1f;
        }
    }

    // ====== 高级视觉组件 ======

    private static readonly Dictionary<int, Sprite> _gradientCache = new Dictionary<int, Sprite>();
    private static readonly Dictionary<int, Sprite> _glowCache = new Dictionary<int, Sprite>();

    /// <summary>创建垂直渐变纹理 (topColor → bottomColor)，带缓存</summary>
    public static Sprite CreateGradientTexture(int width, int height, Color topColor, Color bottomColor)
    {
        int hash = HashCombine(width, height, topColor.GetHashCode(), bottomColor.GetHashCode());
        if (_gradientCache.TryGetValue(hash, out var cached)) return cached;

        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var px = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            float t = (float)y / (height - 1);
            Color c = Color.Lerp(bottomColor, topColor, t);
            for (int x = 0; x < width; x++)
                px[y * width + x] = c;
        }
        tex.SetPixels(px);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), width);
        _gradientCache[hash] = sprite;
        return sprite;
    }

    /// <summary>创建径向光晕 Sprite (中心亮→边缘透明)，带缓存</summary>
    public static Sprite CreateRadialGlowSprite(int size, Color color)
    {
        int hash = HashCombine(size, color.GetHashCode());
        if (_glowCache.TryGetValue(hash, out var cached)) return cached;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color[size * size];
        float cx = size / 2f, cy = size / 2f;
        float maxDist = size / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - cx + 0.5f, dy = y - cy + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - dist / maxDist);
                a = a * a; // 二次衰减
                px[y * size + x] = new Color(color.r, color.g, color.b, a * color.a);
            }
        }
        tex.SetPixels(px);
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        _glowCache[hash] = sprite;
        return sprite;
    }

    private static int HashCombine(params int[] values)
    {
        int hash = 17;
        foreach (var v in values) hash = hash * 31 + v;
        return hash;
    }

    /// <summary>清除所有缓存的纹理 (场景切换/退出时调用)</summary>
    public static void ClearCache()
    {
        foreach (var s in _gradientCache.Values) { if (s != null && s.texture != null) UnityEngine.Object.Destroy(s.texture); }
        _gradientCache.Clear();
        foreach (var s in _glowCache.Values) { if (s != null && s.texture != null) UnityEngine.Object.Destroy(s.texture); }
        _glowCache.Clear();
    }

    /// <summary>创建带渐变背景+发光底线+边框的高级卡片</summary>
    public static GameObject MakeGlowCard(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
        Color topColor, Color bottomColor, Color glowColor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var r = go.AddComponent<RectTransform>();
        r.anchorMin = anchorMin; r.anchorMax = anchorMax;
        r.offsetMin = offsetMin; r.offsetMax = offsetMax;

        // 渐变背景
        var bg = go.AddComponent<Image>();
        bg.sprite = CreateGradientTexture(64, 64, topColor, bottomColor);
        bg.type = Image.Type.Simple;

        // 外边框
        var border = new GameObject("Border");
        border.transform.SetParent(go.transform, false);
        var br = border.AddComponent<RectTransform>();
        br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one;
        br.offsetMin = new Vector2(-1, -1); br.offsetMax = new Vector2(1, 1);
        var bi = border.AddComponent<Image>();
        bi.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0.5f);
        bi.raycastTarget = false;
        border.transform.SetAsFirstSibling();

        // 底部发光线
        var glowLine = new GameObject("GlowLine");
        glowLine.transform.SetParent(go.transform, false);
        var gr = glowLine.AddComponent<RectTransform>();
        gr.anchorMin = new Vector2(0.1f, 0); gr.anchorMax = new Vector2(0.9f, 0);
        gr.pivot = new Vector2(0.5f, 0);
        gr.sizeDelta = new Vector2(0, 2);
        var gi = glowLine.AddComponent<Image>();
        gi.color = glowColor;
        gi.raycastTarget = false;

        // 顶部高光
        var topLine = new GameObject("TopHighlight");
        topLine.transform.SetParent(go.transform, false);
        var tr = topLine.AddComponent<RectTransform>();
        tr.anchorMin = new Vector2(0.1f, 1); tr.anchorMax = new Vector2(0.9f, 1);
        tr.pivot = new Vector2(0.5f, 1);
        tr.sizeDelta = new Vector2(0, 1);
        var ti = topLine.AddComponent<Image>();
        ti.color = new Color(1, 1, 1, 0.15f);
        ti.raycastTarget = false;

        return go;
    }

}

/// <summary>卡片悬停效果 — 缩放+亮度变化</summary>
public class CardHoverEffect : MonoBehaviour
{
    private Vector3 _baseScale = Vector3.one;
    private Image _bgImage;
    private Image _glowOverlay;
    private bool _hovered;
    private float _currentScale = 1f;

    void Start()
    {
        _baseScale = transform.localScale;
        _bgImage = GetComponent<Image>();

        // 添加悬停光晕覆盖层
        var glowObj = new GameObject("HoverGlow");
        glowObj.transform.SetParent(transform, false);
        var gr = glowObj.AddComponent<RectTransform>();
        gr.anchorMin = Vector2.zero; gr.anchorMax = Vector2.one;
        gr.offsetMin = Vector2.zero; gr.offsetMax = Vector2.zero;
        _glowOverlay = glowObj.AddComponent<Image>();
        _glowOverlay.color = new Color(1, 1, 1, 0f);
        _glowOverlay.raycastTarget = false;
        glowObj.transform.SetAsFirstSibling();

        var trigger = gameObject.GetComponent<EventTrigger>();
        if (trigger == null) trigger = gameObject.AddComponent<EventTrigger>();

        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => { _hovered = true; });
        trigger.triggers.Add(enter);

        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => { _hovered = false; });
        trigger.triggers.Add(exit);
    }

    void Update()
    {
        float target = _hovered ? 1.05f : 1f;
        _currentScale = Mathf.Lerp(_currentScale, target, Time.unscaledDeltaTime * 12f);
        transform.localScale = _baseScale * _currentScale;

        if (_glowOverlay != null)
        {
            float targetAlpha = _hovered ? 0.2f : 0f;
            var c = _glowOverlay.color;
            c.a = Mathf.Lerp(c.a, targetAlpha, Time.unscaledDeltaTime * 10f);
            _glowOverlay.color = c;
        }
    }
}
