using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Animated title screen for "卫冕战争" with dynamic background:
/// - Gradient animated background
/// - Floating particle system (embers / stars)
/// - Pulsing title glow
/// - Animated sword icons
/// - Scroll fog layers
/// </summary>
public class TitleScreen : MonoBehaviour
{
    public static TitleScreen Instance { get; private set; }

    private GameObject panel;
    private bool started;

    // Animation references
    private List<ParticleData> particles = new List<ParticleData>();
    private List<FogLayerData> fogLayers = new List<FogLayerData>();
    private GameObject titleTextObj;
    private GameObject leftSwordIcon;
    private GameObject rightSwordIcon;
    private Image bannerImg;
    private float animTime;

    private struct ParticleData
    {
        public RectTransform rect;
        public float speed;
        public float phase;
        public float amplitude;
        public float baseAlpha;
    }

    private struct FogLayerData
    {
        public RectTransform rect;
        public float speed;
        public float startX;
        public float width;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Show()
    {
        started = false;
        if (panel == null) BuildUI();
        panel.SetActive(true);
        animTime = 0f;

    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }

    private void Update()
    {
        if (panel == null || !panel.activeSelf) return;

        animTime += Time.deltaTime;

        // Animate particles (float upward with sinusoidal drift)
        for (int i = 0; i < particles.Count; i++)
        {
            var p = particles[i];
            Vector2 pos = p.rect.anchoredPosition;
            pos.y += p.speed * Time.deltaTime * 60f;
            pos.x += Mathf.Sin(animTime * 1.5f + p.phase) * p.amplitude * Time.deltaTime * 30f;

            // Reset when off top
            if (pos.y > 900f)
            {
                pos.y = -50f;
                pos.x = Random.Range(-400f, 400f);
            }
            p.rect.anchoredPosition = pos;

            // Fade in/out
            float alpha = p.baseAlpha;
            if (pos.y < 50f) alpha *= pos.y / 50f;
            if (pos.y > 750f) alpha *= (900f - pos.y) / 150f;
            var img = p.rect.GetComponent<Image>();
            if (img != null)
            {
                var c = img.color;
                c.a = alpha;
                img.color = c;
            }
        }

        // Animate fog scroll
        for (int i = 0; i < fogLayers.Count; i++)
        {
            var f = fogLayers[i];
            Vector2 pos = f.rect.anchoredPosition;
            pos.x += f.speed * Time.deltaTime * 30f;
            if (f.speed > 0 && pos.x > f.width * 0.5f)
                pos.x = -f.width * 0.5f;
            else if (f.speed < 0 && pos.x < -f.width * 0.5f)
                pos.x = f.width * 0.5f;
            f.rect.anchoredPosition = pos;
        }

        // Pulse title glow
        if (titleTextObj != null)
        {
            var txt = titleTextObj.GetComponent<Text>();
            if (txt != null)
            {
                float pulse = 0.85f + Mathf.Sin(animTime * 2f) * 0.15f;
                txt.color = new Color(1f * pulse, 0.85f * pulse, 0.2f, 1f);
            }
        }

        // Animate banner border shimmer
        if (bannerImg != null)
        {
            float shimmer = 0.7f + Mathf.Sin(animTime * 1.5f) * 0.3f;
            bannerImg.color = new Color(0.8f * shimmer, 0.6f * shimmer, 0.1f, 0.9f);
        }

        // Animate sword icons (bob up/down + slow rotate)
        AnimateSwordIcon(leftSwordIcon, animTime, -1);
        AnimateSwordIcon(rightSwordIcon, animTime + 0.5f, 1);
    }

    private void AnimateSwordIcon(GameObject swordObj, float time, int side)
    {
        if (swordObj == null) return;
        float bob = Mathf.Sin(time * 1.8f) * 8f;
        float tilt = Mathf.Sin(time * 1.2f + side) * 5f;
        var rect = swordObj.GetComponent<RectTransform>();
        if (rect != null)
        {
            Vector2 basePos = side < 0 ? new Vector2(-180, 50) : new Vector2(180, 50);
            rect.anchoredPosition = basePos + new Vector2(0, bob);
            rect.localEulerAngles = new Vector3(0, 0, tilt * side);
        }
    }

    private void BuildUI()
    {
        Font font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();

        panel = new GameObject("TitleScreenPanel");
        panel.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // === Animated gradient background ===
        GameObject bgObj = new GameObject("AnimatedBG");
        bgObj.transform.SetParent(panel.transform, false);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.sprite = CreateGradientBG(256, 256);
        bgImg.color = Color.white;
        bgImg.raycastTarget = false;

        // === Fog layers (scrolling atmospheric fog) ===
        CreateFogLayer(panel, new Color(0.12f, 0.08f, 0.18f, 0.25f), 1200f, 0.3f);  // slow left
        CreateFogLayer(panel, new Color(0.08f, 0.06f, 0.15f, 0.2f), 1400f, -0.2f);  // slow right
        CreateFogLayer(panel, new Color(0.15f, 0.10f, 0.20f, 0.15f), 1000f, 0.5f);  // medium left

        // === Floating particle system (embers / stars) ===
        CreateParticles(panel, 35);

        // === Title Banner Background ===
        GameObject bannerObj = new GameObject("TitleBanner");
        bannerObj.transform.SetParent(panel.transform, false);
        RectTransform bannerRect = bannerObj.AddComponent<RectTransform>();
        bannerRect.anchorMin = new Vector2(0.5f, 0.65f);
        bannerRect.anchorMax = new Vector2(0.5f, 0.65f);
        bannerRect.pivot = new Vector2(0.5f, 0.5f);
        bannerRect.anchoredPosition = Vector2.zero;
        bannerRect.sizeDelta = new Vector2(700, 200);

        // Banner glow (pulsing outer glow)
        GameObject bannerGlow = new GameObject("BannerGlow");
        bannerGlow.transform.SetParent(bannerObj.transform, false);
        RectTransform glowRect = bannerGlow.AddComponent<RectTransform>();
        glowRect.anchorMin = Vector2.zero;
        glowRect.anchorMax = Vector2.one;
        glowRect.offsetMin = new Vector2(-20, -15);
        glowRect.offsetMax = new Vector2(20, 15);
        Image glowImg = bannerGlow.AddComponent<Image>();
        glowImg.sprite = CreateRadialGlow(128, new Color(0.8f, 0.6f, 0.1f, 0.3f));
        glowImg.color = Color.white;

        // Banner background
        Image bannerBgImg = bannerObj.AddComponent<Image>();
        bannerBgImg.color = new Color(0.08f, 0.05f, 0.15f, 0.85f);

        // Decorative border around banner (animated shimmer)
        GameObject borderObj = new GameObject("BannerBorder");
        borderObj.transform.SetParent(bannerObj.transform, false);
        RectTransform borderRect = borderObj.AddComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = new Vector2(-3, -3);
        borderRect.offsetMax = new Vector2(3, 3);
        bannerImg = borderObj.AddComponent<Image>();
        bannerImg.color = new Color(0.8f, 0.6f, 0.1f, 0.9f);

        // Corner ornaments on banner
        CreateCornerOrnament(borderObj, new Vector2(-8, -8), new Vector2(20, 20));
        CreateCornerOrnament(borderObj, new Vector2(8, -8), new Vector2(20, 20));
        CreateCornerOrnament(borderObj, new Vector2(-8, 8), new Vector2(20, 20));
        CreateCornerOrnament(borderObj, new Vector2(8, 8), new Vector2(20, 20));

        // Inner border
        GameObject innerObj = new GameObject("BannerInner");
        innerObj.transform.SetParent(borderObj.transform, false);
        RectTransform innerRect = innerObj.AddComponent<RectTransform>();
        innerRect.anchorMin = Vector2.zero;
        innerRect.anchorMax = Vector2.one;
        innerRect.offsetMin = new Vector2(3, 3);
        innerRect.offsetMax = new Vector2(-3, -3);
        Image innerImg = innerObj.AddComponent<Image>();
        innerImg.color = new Color(0.05f, 0.03f, 0.12f, 1f);

        // === Title Text "卫冕战争" ===
        titleTextObj = new GameObject("GameTitle");
        titleTextObj.transform.SetParent(bannerObj.transform, false);
        RectTransform titleRect = titleTextObj.AddComponent<RectTransform>();
        titleRect.anchorMin = Vector2.zero;
        titleRect.anchorMax = Vector2.one;
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;

        // Shadow text (offset for depth)
        GameObject titleShadow = new GameObject("TitleShadow");
        titleShadow.transform.SetParent(titleTextObj.transform, false);
        RectTransform shadowRect = titleShadow.AddComponent<RectTransform>();
        shadowRect.anchorMin = Vector2.zero;
        shadowRect.anchorMax = Vector2.one;
        shadowRect.offsetMin = new Vector2(2, -2);
        shadowRect.offsetMax = new Vector2(2, -2);
        Text shadowText = titleShadow.AddComponent<Text>();
        shadowText.text = "卫冕战争";
        shadowText.alignment = TextAnchor.MiddleCenter;
        shadowText.fontSize = 72;
        shadowText.color = new Color(0.3f, 0.15f, 0f, 0.7f);
        shadowText.font = font;

        Text titleText = titleTextObj.AddComponent<Text>();
        titleText.text = "卫冕战争";
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.fontSize = 72;
        titleText.color = new Color(1f, 0.85f, 0.2f);
        titleText.font = font;

        // Subtitle
        GameObject subObj = new GameObject("Subtitle");
        subObj.transform.SetParent(panel.transform, false);
        RectTransform subRect = subObj.AddComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0.5f, 0.5f);
        subRect.anchorMax = new Vector2(0.5f, 0.5f);
        subRect.pivot = new Vector2(0.5f, 0.5f);
        subRect.anchoredPosition = new Vector2(0, 50);
        subRect.sizeDelta = new Vector2(500, 40);
        Text subText = subObj.AddComponent<Text>();
        subText.text = "—— 2D 地下城 ARPG ——";
        subText.alignment = TextAnchor.MiddleCenter;
        subText.fontSize = 22;
        subText.color = new Color(0.7f, 0.7f, 0.8f, 0.8f);
        subText.font = font;

        // === Decorative Sword Icons (animated) ===
        leftSwordIcon = CreateDecorativeIcon(panel, new Vector2(-180, 50), new Color(0.8f, 0.6f, 0.1f), 3f);
        rightSwordIcon = CreateDecorativeIcon(panel, new Vector2(180, 50), new Color(0.8f, 0.6f, 0.1f), 3f);

        // === Decorative diamond separators ===
        CreateDiamondSeparator(panel, new Vector2(-250, 50));
        CreateDiamondSeparator(panel, new Vector2(250, 50));

        // === Start Button (with glow effect) ===
        GameObject startObj = new GameObject("StartBtn");
        startObj.transform.SetParent(panel.transform, false);
        RectTransform startRect = startObj.AddComponent<RectTransform>();
        startRect.anchorMin = new Vector2(0.5f, 0.3f);
        startRect.anchorMax = new Vector2(0.5f, 0.3f);
        startRect.pivot = new Vector2(0.5f, 0.5f);
        startRect.anchoredPosition = Vector2.zero;
        startRect.sizeDelta = new Vector2(300, 65);

        // Button glow
        GameObject btnGlow = new GameObject("BtnGlow");
        btnGlow.transform.SetParent(startObj.transform, false);
        RectTransform btnGlowRect = btnGlow.AddComponent<RectTransform>();
        btnGlowRect.anchorMin = Vector2.zero;
        btnGlowRect.anchorMax = Vector2.one;
        btnGlowRect.offsetMin = new Vector2(-12, -12);
        btnGlowRect.offsetMax = new Vector2(12, 12);
        Image btnGlowImg = btnGlow.AddComponent<Image>();
        btnGlowImg.sprite = CreateRadialGlow(64, new Color(0.9f, 0.3f, 0.1f, 0.35f));
        btnGlowImg.color = Color.white;

        Image startImg = startObj.AddComponent<Image>();
        startImg.color = new Color(0.6f, 0.15f, 0.1f, 0.9f);

        // Button border
        GameObject btnBorderObj = new GameObject("BtnBorder");
        btnBorderObj.transform.SetParent(startObj.transform, false);
        RectTransform btnBorderRect = btnBorderObj.AddComponent<RectTransform>();
        btnBorderRect.anchorMin = Vector2.zero;
        btnBorderRect.anchorMax = Vector2.one;
        btnBorderRect.offsetMin = new Vector2(-2, -2);
        btnBorderRect.offsetMax = new Vector2(2, 2);
        Image btnBorderImg = btnBorderObj.AddComponent<Image>();
        btnBorderImg.color = new Color(1f, 0.8f, 0.2f, 0.9f);

        // Button inner
        GameObject btnInnerObj = new GameObject("BtnInner");
        btnInnerObj.transform.SetParent(btnBorderObj.transform, false);
        RectTransform btnInnerRect = btnInnerObj.AddComponent<RectTransform>();
        btnInnerRect.anchorMin = Vector2.zero;
        btnInnerRect.anchorMax = Vector2.one;
        btnInnerRect.offsetMin = new Vector2(2, 2);
        btnInnerRect.offsetMax = new Vector2(-2, -2);
        Image btnInnerImg = btnInnerObj.AddComponent<Image>();
        btnInnerImg.color = new Color(0.5f, 0.1f, 0.08f, 0.95f);

        // Button text
        GameObject btnLabel = new GameObject("Label");
        btnLabel.transform.SetParent(startObj.transform, false);
        RectTransform lblRect = btnLabel.AddComponent<RectTransform>();
        lblRect.anchorMin = Vector2.zero;
        lblRect.anchorMax = Vector2.one;
        lblRect.offsetMin = Vector2.zero;
        lblRect.offsetMax = Vector2.zero;
        Text lblText = btnLabel.AddComponent<Text>();
        lblText.text = "开始游戏";
        lblText.alignment = TextAnchor.MiddleCenter;
        lblText.fontSize = 32;
        lblText.color = new Color(1f, 0.9f, 0.6f);
        lblText.font = font;

        Button startBtn = startObj.AddComponent<Button>();
        startBtn.onClick.AddListener(OnStartGame);

        // === Version Text ===
        GameObject verObj = new GameObject("Version");
        verObj.transform.SetParent(panel.transform, false);
        RectTransform verRect = verObj.AddComponent<RectTransform>();
        verRect.anchorMin = new Vector2(1, 0);
        verRect.anchorMax = new Vector2(1, 0);
        verRect.pivot = new Vector2(1, 0);
        verRect.anchoredPosition = new Vector2(-15, 15);
        verRect.sizeDelta = new Vector2(200, 25);
        Text verText = verObj.AddComponent<Text>();
        verText.text = "v0.7.0 Alpha";
        verText.alignment = TextAnchor.MiddleRight;
        verText.fontSize = 14;
        verText.color = new Color(0.4f, 0.4f, 0.5f, 0.6f);
        verText.font = font;

        // === Cloud Login Button (bottom-left) ===
        CreateCloudLoginButton(panel.transform, font);

        // === Server Select Button (bottom-center) ===
        CreateServerSelectButton(panel.transform, font);

        panel.SetActive(false);
    }

    // ======================== ANIMATED BACKGROUND ========================

    private Sprite CreateGradientBG(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h);
        Color[] px = new Color[w * h];

        // Deep dark purple gradient (top darker, bottom slightly lighter)
        Color top = new Color(0.02f, 0.01f, 0.06f);
        Color mid = new Color(0.04f, 0.02f, 0.10f);
        Color bot = new Color(0.06f, 0.03f, 0.12f);

        for (int y = 0; y < h; y++)
        {
            float t = (float)y / h;
            Color c = t < 0.5f
                ? Color.Lerp(top, mid, t * 2f)
                : Color.Lerp(mid, bot, (t - 0.5f) * 2f);

            // Add subtle horizontal variation
            for (int x = 0; x < w; x++)
            {
                float nx = (float)x / w;
                float vignette = 1f - Mathf.Pow(Mathf.Abs(nx - 0.5f) * 2f, 2f) * 0.3f;
                px[y * w + x] = c * vignette;
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
    }

    private Sprite CreateRadialGlow(int size, Color color)
    {
        Texture2D tex = new Texture2D(size, size);
        Color[] px = new Color[size * size];
        float center = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Max(0f, 1f - dist);
                alpha = Mathf.Pow(alpha, 1.5f) * color.a;
                px[y * size + x] = new Color(color.r, color.g, color.b, alpha);
            }
        }

        tex.SetPixels(px);
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    // ======================== FOG LAYERS ========================

    private void CreateFogLayer(GameObject parent, Color color, float width, float speed)
    {
        int texW = 256, texH = 64;
        Texture2D tex = new Texture2D(texW, texH);
        Color[] px = new Color[texW * texH];

        System.Random rng = new System.Random(speed.GetHashCode());

        for (int y = 0; y < texH; y++)
        {
            float ny = (float)y / texH;
            float vertFade = Mathf.Sin(ny * Mathf.PI);
            for (int x = 0; x < texW; x++)
            {
                float nx = (float)x / texW;
                float noise = (float)(rng.NextDouble() * 0.3 + 0.7);
                float alpha = vertFade * noise * 0.4f;
                px[y * texW + x] = new Color(color.r, color.g, color.b, alpha);
            }
        }

        tex.SetPixels(px);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.Apply();

        GameObject fogObj = new GameObject("FogLayer");
        fogObj.transform.SetParent(parent.transform, false);
        RectTransform fogRect = fogObj.AddComponent<RectTransform>();
        fogRect.anchorMin = new Vector2(0.5f, 0.5f);
        fogRect.anchorMax = new Vector2(0.5f, 0.5f);
        fogRect.pivot = new Vector2(0.5f, 0.5f);
        fogRect.anchoredPosition = Vector2.zero;
        fogRect.sizeDelta = new Vector2(width, 200f);

        Image fogImg = fogObj.AddComponent<Image>();
        fogImg.sprite = Sprite.Create(tex, new Rect(0, 0, texW, texH), new Vector2(0.5f, 0.5f), texW);
        fogImg.color = color;

        fogLayers.Add(new FogLayerData
        {
            rect = fogRect,
            speed = speed,
            startX = 0f,
            width = width
        });
    }

    // ======================== PARTICLES ========================

    private void CreateParticles(GameObject parent, int count)
    {
        System.Random rng = new System.Random(77);

        for (int i = 0; i < count; i++)
        {
            // Mix of ember particles and star particles
            bool isEmber = i < count * 0.6f;

            int pSize = isEmber ? 4 : 6;
            Texture2D tex = new Texture2D(pSize, pSize);
            Color[] px = new Color[pSize * pSize];
            float center = pSize / 2f;

            for (int y = 0; y < pSize; y++)
            {
                for (int x = 0; x < pSize; x++)
                {
                    float dx = x - center + 0.5f;
                    float dy = y - center + 0.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy) / center;
                    float alpha = Mathf.Max(0f, 1f - dist);
                    alpha = Mathf.Pow(alpha, 0.8f);

                    if (isEmber)
                    {
                        float warmth = (float)rng.NextDouble();
                        px[y * pSize + x] = new Color(
                            1f,
                            0.4f + warmth * 0.4f,
                            0.1f + warmth * 0.15f,
                            alpha * 0.8f
                        );
                    }
                    else
                    {
                        px[y * pSize + x] = new Color(0.85f, 0.85f, 1f, alpha * 0.5f);
                    }
                }
            }

            tex.SetPixels(px);
            tex.filterMode = FilterMode.Bilinear;
            tex.Apply();

            GameObject pObj = new GameObject("Particle_" + i);
            pObj.transform.SetParent(parent.transform, false);
            RectTransform pRect = pObj.AddComponent<RectTransform>();
            pRect.anchorMin = new Vector2(0.5f, 0.5f);
            pRect.anchorMax = new Vector2(0.5f, 0.5f);
            pRect.pivot = new Vector2(0.5f, 0.5f);

            float scale = isEmber ? Random.Range(2f, 5f) : Random.Range(3f, 7f);
            pRect.sizeDelta = new Vector2(scale, scale);
            pRect.anchoredPosition = new Vector2(Random.Range(-400f, 400f), Random.Range(-50f, 800f));

            Image pImg = pObj.AddComponent<Image>();
            pImg.sprite = Sprite.Create(tex, new Rect(0, 0, pSize, pSize), new Vector2(0.5f, 0.5f), pSize);
            pImg.color = Color.white;

            particles.Add(new ParticleData
            {
                rect = pRect,
                speed = Random.Range(0.3f, 1.2f),
                phase = Random.Range(0f, Mathf.PI * 2f),
                amplitude = Random.Range(0.3f, 1.5f),
                baseAlpha = Random.Range(0.3f, 0.8f)
            });
        }
    }

    // ======================== DECORATIONS ========================

    private void CreateDiamondSeparator(GameObject parent, Vector2 position)
    {
        int s = 8;
        Texture2D tex = new Texture2D(s, s);
        Color[] px = new Color[s * s];
        System.Array.Clear(px, 0, px.Length);

        Color diamond = new Color(0.8f, 0.6f, 0.1f, 0.7f);

        // Diamond shape
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float dx = Mathf.Abs(x - s / 2f + 0.5f) / (s / 2f);
                float dy = Mathf.Abs(y - s / 2f + 0.5f) / (s / 2f);
                if (dx + dy <= 1f)
                    px[y * s + x] = diamond;
            }
        }

        tex.SetPixels(px);
        tex.filterMode = FilterMode.Point;
        tex.Apply();

        GameObject obj = new GameObject("Diamond");
        obj.transform.SetParent(parent.transform, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(10, 10);

        Image img = obj.AddComponent<Image>();
        img.sprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
    }

    private void CreateCornerOrnament(GameObject parent, Vector2 offset, Vector2 size)
    {
        int s = 12;
        Texture2D tex = new Texture2D(s, s);
        Color[] px = new Color[s * s];
        System.Array.Clear(px, 0, px.Length);

        Color gold = new Color(0.85f, 0.65f, 0.15f, 0.9f);
        Color goldDk = new Color(0.6f, 0.4f, 0.08f, 0.7f);

        bool isTop = offset.y > 0;
        bool isLeft = offset.x < 0;

        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                int lx = isLeft ? x : (s - 1 - x);
                int ly = isTop ? (s - 1 - y) : y;

                bool isBorder = (lx == 0 || ly == 0) && (lx < 6 || ly < 6);
                bool isFill = (lx < 2 || ly < 2) && (lx < 5 || ly < 5);

                if (isBorder)
                    px[y * s + x] = gold;
                else if (isFill)
                    px[y * s + x] = goldDk;
            }
        }

        tex.SetPixels(px);
        tex.filterMode = FilterMode.Point;
        tex.Apply();

        GameObject obj = new GameObject("CornerOrnament");
        obj.transform.SetParent(parent.transform, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = offset;
        rect.sizeDelta = size;

        Image img = obj.AddComponent<Image>();
        img.sprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
    }

    private GameObject CreateDecorativeIcon(GameObject parent, Vector2 position, Color color, float scale)
    {
        int w = 8, h = 12;
        Texture2D tex = new Texture2D(w, h);
        Color[] px = new Color[w * h];
        System.Array.Clear(px, 0, px.Length);

        Color blade = new Color(0.85f, 0.85f, 0.9f);
        Color bladeHi = new Color(0.95f, 0.95f, 1f);
        Color guard = color;
        Color grip = new Color(0.5f, 0.3f, 0.1f);
        Color pommel = color * 0.8f;

        // Blade
        FillRect(px, w, 3, 7, 2, 5, blade);
        FillRect(px, w, 3, 9, 1, 3, bladeHi);
        FillPixel(px, w, 4, 11, blade * 0.9f);
        // Guard
        FillRect(px, w, 1, 6, 6, 1, guard);
        FillRect(px, w, 2, 6, 4, 1, new Color(guard.r * 1.2f, guard.g * 1.2f, guard.b * 1.2f));
        // Grip
        FillRect(px, w, 3, 4, 2, 2, grip);
        // Pommel
        FillRect(px, w, 2, 3, 4, 1, pommel);

        tex.SetPixels(px);
        tex.filterMode = FilterMode.Point;
        tex.Apply();

        GameObject iconObj = new GameObject("SwordIcon");
        iconObj.transform.SetParent(parent.transform, false);
        RectTransform iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = position;
        iconRect.sizeDelta = new Vector2(w * scale, h * scale);

        Image iconImg = iconObj.AddComponent<Image>();
        iconImg.sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
        iconImg.color = Color.white;

        return iconObj;
    }

    // ======================== HELPERS ========================

    private static void FillRect(Color[] px, int w, int x, int y, int rectW, int rectH, Color c)
    {
        for (int dy = 0; dy < rectH; dy++)
        {
            for (int dx = 0; dx < rectW; dx++)
            {
                int px2 = x + dx;
                int py = y + dy;
                if (px2 >= 0 && px2 < w && py >= 0 && py < px.Length / w)
                    px[py * w + px2] = c;
            }
        }
    }

    private static void FillPixel(Color[] px, int w, int x, int y, Color c)
    {
        if (x >= 0 && x < w && y >= 0 && y < px.Length / w)
            px[y * w + x] = c;
    }

    // ======================== CLOUD LOGIN ========================

    private void CreateCloudLoginButton(Transform parent, Font font)
    {
        GameObject btnObj = new GameObject("CloudLoginBtn");
        btnObj.transform.SetParent(parent, false);
        RectTransform btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0, 0);
        btnRect.anchorMax = new Vector2(0, 0);
        btnRect.pivot = new Vector2(0, 0);
        btnRect.anchoredPosition = new Vector2(20, 20);
        btnRect.sizeDelta = new Vector2(180, 48);

        // Dark Souls style: dark bg + golden border
        Image btnImg = btnObj.AddComponent<Image>();
        btnImg.color = new Color(0.08f, 0.06f, 0.10f, 0.9f);

        // Golden border
        GameObject borderObj = new GameObject("Border");
        borderObj.transform.SetParent(btnObj.transform, false);
        RectTransform borderRect = borderObj.AddComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero; borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = new Vector2(-2, -2); borderRect.offsetMax = new Vector2(2, 2);
        Image borderImg = borderObj.AddComponent<Image>();
        borderImg.color = new Color(0.62f, 0.47f, 0.08f, 0.85f);

        // Inner
        GameObject innerObj = new GameObject("Inner");
        innerObj.transform.SetParent(borderObj.transform, false);
        RectTransform innerRect = innerObj.AddComponent<RectTransform>();
        innerRect.anchorMin = Vector2.zero; innerRect.anchorMax = Vector2.one;
        innerRect.offsetMin = new Vector2(2, 2); innerRect.offsetMax = new Vector2(-2, -2);
        Image innerImg = innerObj.AddComponent<Image>();
        innerImg.color = new Color(0.05f, 0.04f, 0.08f, 0.95f);

        // Label
        GameObject lblObj = new GameObject("Label");
        lblObj.transform.SetParent(btnObj.transform, false);
        RectTransform lblRect = lblObj.AddComponent<RectTransform>();
        lblRect.anchorMin = Vector2.zero; lblRect.anchorMax = Vector2.one;
        lblRect.offsetMin = Vector2.zero; lblRect.offsetMax = Vector2.zero;
        Text lblText = lblObj.AddComponent<Text>();
        lblText.alignment = TextAnchor.MiddleCenter;
        lblText.fontSize = 20;
        lblText.color = new Color(1f, 0.85f, 0.3f);
        lblText.font = font;

        // Cloud icon prefix
        GameObject iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(btnObj.transform, false);
        RectTransform iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0, 0.5f); iconRect.anchorMax = new Vector2(0, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(20, 0);
        iconRect.sizeDelta = new Vector2(20, 20);
        Text iconText = iconObj.AddComponent<Text>();
        iconText.text = "";
        iconText.alignment = TextAnchor.MiddleCenter;
        iconText.fontSize = 18;
        iconText.color = new Color(1f, 0.85f, 0.3f);
        iconText.font = font;
        iconText.raycastTarget = false;

        // Update label based on login state
        bool loggedIn = CloudSaveManager.Instance != null && CloudSaveManager.Instance.IsLoggedIn;
        string user = loggedIn ? CloudSaveManager.Instance.CurrentUsername : "";
        lblText.text = loggedIn ? $"已登录: {user}" : "云端存档";
        lblText.raycastTarget = false;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        btn.onClick.AddListener(() =>
        {
            var authUI = GetComponent<CloudAuthUI>();
            if (authUI == null)
                authUI = gameObject.AddComponent<CloudAuthUI>();

            authUI.ShowAuthPage(panel.transform, font);
        });
    }

    private void CreateServerSelectButton(Transform parent, Font font)
    {
        // 显示当前服务器 + 切换按钮
        var server = ServerRegionManager.CurrentServer;
        string serverName = server != null ? server.name : "未选择";
        int status = server != null ? server.status : 0;
        string statusText = ServerRegionManager.GetStatusText(status);
        Color statusColor = ServerRegionManager.GetStatusColor(status);

        GameObject btnObj = new GameObject("ServerSelectBtn");
        btnObj.transform.SetParent(parent, false);
        RectTransform r = btnObj.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(0.5f, 0); r.anchorMax = new Vector2(0.5f, 0);
        r.pivot = new Vector2(0.5f, 0); r.anchoredPosition = new Vector2(0, 60);
        r.sizeDelta = new Vector2(300, 35);

        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(0.08f, 0.06f, 0.12f, 0.8f);

        GameObject lbl = new GameObject("Lbl");
        lbl.transform.SetParent(btnObj.transform, false);
        RectTransform lr = lbl.AddComponent<RectTransform>();
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
        lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;
        Text txt = lbl.AddComponent<Text>();
        txt.text = $"服务器: {serverName}  <color=#{ColorUtility.ToHtmlStringRGBA(statusColor)}>[{statusText}]</color>  点击切换";
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontSize = 16; txt.color = new Color(0.8f, 0.75f, 0.9f, 1f); txt.font = font;
        txt.supportRichText = true;
        txt.raycastTarget = false;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() =>
        {
            Hide();
            GameManager.Instance.ShowServerSelect();
        });
    }

    private void OnStartGame()
    {
        if (started) return;
        started = true;
        StartCoroutine(CheckServerAndStart());
    }

    private System.Collections.IEnumerator CheckServerAndStart()
    {
        // 显示加载页面
        GameObject loadingPanel = CreateLoadingPanel();
        loadingPanel.SetActive(true);

        yield return null; // 等一帧让加载页渲染

        // 检查服务器是否连接
        string serverUrl = CloudSaveManager.Instance?.ServerUrl ?? "http://39.107.141.107:5132";
        bool serverOk = false;

        UpdateLoadingText(loadingPanel, "正在连接服务器...");
        var progressFill = loadingPanel.transform.Find("ProgressBar/Fill")?.GetComponent<Image>();

        string testJson = "{\"username\":\"__ping__\",\"password\":\"__ping__\"}";
        using (var req = UnityEngine.Networking.UnityWebRequest.PostWwwForm($"{serverUrl}/api/auth/login", ""))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(testJson);
            req.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 5;
            yield return req.SendWebRequest();
            serverOk = req.result == UnityEngine.Networking.UnityWebRequest.Result.Success
                     || req.result == UnityEngine.Networking.UnityWebRequest.Result.ProtocolError;
        }

        if (!serverOk)
        {
            UpdateLoadingText(loadingPanel, "服务器连接失败");
            if (progressFill != null) progressFill.fillAmount = 0f;
            yield return new WaitForSeconds(1.5f);
            Destroy(loadingPanel);
            started = false;
            yield break;
        }
        UpdateLoadingText(loadingPanel, "服务器连接成功");
        if (progressFill != null) progressFill.fillAmount = 0.15f;
        yield return new WaitForSeconds(0.2f);

        // Step 2: 加载游戏配置 (15-40%)
        UpdateLoadingText(loadingPanel, "正在加载游戏配置...");
        if (progressFill != null) progressFill.fillAmount = 0.20f;
        if (GameConfigManager.Instance != null && !GameConfigManager.Instance.IsLoaded)
        {
            yield return StartCoroutine(GameConfigManager.Instance.LoadConfigCo());
        }
        UpdateLoadingText(loadingPanel, "游戏配置加载完成");
        if (progressFill != null) progressFill.fillAmount = 0.40f;
        yield return new WaitForSeconds(0.2f);

        // Step 3: 加载玩家存档 (40-70%)
        UpdateLoadingText(loadingPanel, "正在加载存档数据...");
        if (progressFill != null) progressFill.fillAmount = 0.50f;
        yield return new WaitForSeconds(0.3f);
        UpdateLoadingText(loadingPanel, "存档加载完成");
        if (progressFill != null) progressFill.fillAmount = 0.70f;
        yield return new WaitForSeconds(0.2f);

        // Step 4: 预加载资源 (70-90%)
        UpdateLoadingText(loadingPanel, "正在预加载资源...");
        if (progressFill != null) progressFill.fillAmount = 0.75f;
        // 异步预加载所有角色预览Sprite (通过GameConfigSO配置路径)
        yield return CharacterSpriteFactory.PreloadAllAsync();
        Resources.LoadAsync<Sprite>(ResourcePaths.MeleeGoblin);
        yield return null;
        UpdateLoadingText(loadingPanel, "资源预加载完成");
        if (progressFill != null) progressFill.fillAmount = 0.90f;
        yield return new WaitForSeconds(0.2f);

        // Step 5: 进入游戏 (90-100%)
        UpdateLoadingText(loadingPanel, "正在进入游戏...");
        if (progressFill != null) progressFill.fillAmount = 0.95f;
        Hide();
        GameManager.Instance.OnTitleStartNewGame();
        UpdateLoadingText(loadingPanel, "欢迎来到卫冕战争!");
        if (progressFill != null) progressFill.fillAmount = 1.0f;
        yield return new WaitForSeconds(0.8f);
        Destroy(loadingPanel);
    }

    private GameObject CreateLoadingPanel()
    {
        Font font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();

        var panel = new GameObject("LoadingPanel");
        panel.transform.SetParent(canvas.transform, false);
        var pr = panel.AddComponent<RectTransform>();
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one;
        pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;

        var bg = panel.AddComponent<Image>();
        bg.color = new Color(0.02f, 0.01f, 0.04f, 0.95f);

        var pc = panel.AddComponent<Canvas>();
        pc.overrideSorting = true; pc.sortingOrder = 999;
        panel.AddComponent<GraphicRaycaster>();

        // 旋转加载圈
        var spinnerObj = new GameObject("Spinner");
        spinnerObj.transform.SetParent(panel.transform, false);
        var sr = spinnerObj.AddComponent<RectTransform>();
        sr.anchorMin = new Vector2(0.5f, 0.6f); sr.anchorMax = new Vector2(0.5f, 0.6f);
        sr.pivot = new Vector2(0.5f, 0.5f); sr.anchoredPosition = Vector2.zero; sr.sizeDelta = new Vector2(60, 60);
        var spinnerImg = spinnerObj.AddComponent<Image>();
        spinnerImg.sprite = SpriteCache.WhitePixel;
        spinnerImg.color = new Color(0.55f, 0.3f, 0.75f, 0.8f);
        spinnerImg.type = Image.Type.Filled;
        spinnerImg.fillMethod = Image.FillMethod.Radial360;
        spinnerImg.fillAmount = 0.3f;
        // 旋转动画
        var rotator = spinnerObj.AddComponent<SpinnerRotator>();

        // 加载文字
        var txtObj = new GameObject("LoadingText");
        txtObj.transform.SetParent(panel.transform, false);
        var tr = txtObj.AddComponent<RectTransform>();
        tr.anchorMin = new Vector2(0.2f, 0.42f); tr.anchorMax = new Vector2(0.8f, 0.50f);
        tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
        var txt = txtObj.AddComponent<Text>();
        txt.text = "正在连接服务器...";
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontSize = 24; txt.color = new Color(0.85f, 0.82f, 0.9f); txt.font = font;
        txt.raycastTarget = false;
        txt.supportRichText = true;

        // 进度点动画
        var dotsObj = new GameObject("Dots");
        dotsObj.transform.SetParent(panel.transform, false);
        var dr = dotsObj.AddComponent<RectTransform>();
        dr.anchorMin = new Vector2(0.3f, 0.32f); dr.anchorMax = new Vector2(0.7f, 0.38f);
        dr.offsetMin = Vector2.zero; dr.offsetMax = Vector2.zero;
        var dotsTxt = dotsObj.AddComponent<Text>();
        dotsTxt.text = "●";
        dotsTxt.alignment = TextAnchor.MiddleCenter;
        dotsTxt.fontSize = 20; dotsTxt.color = new Color(0.55f, 0.3f, 0.75f, 0.6f); dotsTxt.font = font;
        dotsTxt.raycastTarget = false;
        var dotAnim = dotsObj.AddComponent<DotsAnimator>();

        // 进度条
        var pbObj = new GameObject("ProgressBar");
        pbObj.transform.SetParent(panel.transform, false);
        var pbR = pbObj.AddComponent<RectTransform>();
        pbR.anchorMin = new Vector2(0.15f, 0.25f); pbR.anchorMax = new Vector2(0.85f, 0.28f);
        pbR.offsetMin = Vector2.zero; pbR.offsetMax = Vector2.zero;
        var pbBg = pbObj.AddComponent<Image>();
        pbBg.color = new Color(0.08f, 0.06f, 0.12f, 0.9f);

        var fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(pbObj.transform, false);
        var fillR = fillObj.AddComponent<RectTransform>();
        fillR.anchorMin = Vector2.zero; fillR.anchorMax = Vector2.one;
        fillR.offsetMin = Vector2.zero; fillR.offsetMax = Vector2.zero;
        var fillImg = fillObj.AddComponent<Image>();
        fillImg.color = new Color(0.55f, 0.3f, 0.75f, 0.9f);
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillAmount = 0f;

        // Tips文字
        var tipObj = new GameObject("TipText");
        tipObj.transform.SetParent(panel.transform, false);
        var tipR = tipObj.AddComponent<RectTransform>();
        tipR.anchorMin = new Vector2(0.1f, 0.10f); tipR.anchorMax = new Vector2(0.9f, 0.18f);
        tipR.offsetMin = Vector2.zero; tipR.offsetMax = Vector2.zero;
        var tipTxt = tipObj.AddComponent<Text>();
        tipTxt.text = "暴击可以打断敌人的攻击动作";
        tipTxt.alignment = TextAnchor.MiddleCenter;
        tipTxt.fontSize = 14; tipTxt.color = new Color(0.5f, 0.45f, 0.6f); tipTxt.font = font;
        tipTxt.raycastTarget = false;

        return panel;
    }

    private void UpdateLoadingText(GameObject loadingPanel, string text)
    {
        var txt = loadingPanel?.transform.Find("LoadingText")?.GetComponent<Text>();
        if (txt != null) txt.text = text;
    }

    private void OnDestroy()
    {
        if (panel != null) Destroy(panel);
        if (Instance == this) Instance = null;
    }
}

/// <summary>旋转加载圈</summary>
internal class SpinnerRotator : MonoBehaviour
{
    private void Update()
    {
        transform.Rotate(0, 0, -360f * Time.deltaTime);
    }
}

/// <summary>进度点动画 ●→●●→●●●</summary>
internal class DotsAnimator : MonoBehaviour
{
    private float timer;
    private int dotCount;

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer >= 0.4f)
        {
            timer = 0;
            dotCount = (dotCount + 1) % 4;
            var txt = GetComponent<Text>();
            if (txt != null)
                txt.text = dotCount == 0 ? "" : new string('●', dotCount);
        }
    }
}
