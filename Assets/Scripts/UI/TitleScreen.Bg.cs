using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TitleScreen partial — procedural sprite generation (gradient fallback bg / floating particles)
/// </summary>
public partial class TitleScreen
{
    // ======================== GRADIENT FALLBACK ========================

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
}
