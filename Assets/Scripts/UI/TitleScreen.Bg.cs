using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TitleScreen partial — procedural sprite generation for animated background (gradient/glow/fog/particles/ornaments)
/// </summary>
public partial class TitleScreen
{
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

}
