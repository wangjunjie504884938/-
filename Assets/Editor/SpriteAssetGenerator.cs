using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Editor tool that generates PNG sprite assets from the same pixel patterns
/// used by runtime code (DungeonVisuals, CharacterSprite, EnemySprite, SpriteCache).
/// Run via menu: Tools > Generate Sprite Assets
/// </summary>
public static class SpriteAssetGenerator
{
    private const string ROOT = "Assets/UI/Sprites";

    [MenuItem("Tools/Generate Sprite Assets")]
    public static void GenerateAll()
    {
        GenerateProjectileSprites();
        GenerateDungeonSprites();
        GenerateCharacterSprites();
        GenerateEnemySprites();
        AssetDatabase.Refresh();
        SetTextureImportSettings();
        Debug.Log("SpriteAssetGenerator: All sprites generated and imported.");
    }

    // ===================== HELPERS =====================

    private static void SavePNG(Texture2D tex, string subPath)
    {
        string dir = Path.GetDirectoryName(subPath);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        byte[] png = tex.EncodeToPNG();
        File.WriteAllBytes(subPath, png);
    }

    private static void FillRect(Color[] px, int w, int x, int y, int rw, int rh, Color c)
    {
        for (int dy = 0; dy < rh; dy++)
            for (int dx = 0; dx < rw; dx++)
            {
                int px2 = x + dx, py = y + dy;
                if (px2 >= 0 && px2 < w && py >= 0 && py < px.Length / w)
                    px[py * w + px2] = c;
            }
    }

    private static void FillPixel(Color[] px, int w, int x, int y, Color c)
    {
        if (x >= 0 && x < w && y >= 0 && y < px.Length / w)
            px[y * w + x] = c;
    }

    private static void FillEllipse(Color[] px, int w, int cx, int cy, int rw, int rh, Color c)
    {
        for (int dy = -rh; dy <= rh; dy++)
            for (int dx = -rw; dx <= rw; dx++)
            {
                float nx = (float)dx / rw, ny = (float)dy / rh;
                if (nx * nx + ny * ny <= 1f)
                {
                    int px2 = cx + dx, py = cy + dy;
                    if (px2 >= 0 && px2 < w && py >= 0 && py < px.Length / w)
                        px[py * w + px2] = c;
                }
            }
    }

    private static void DrawOutlinedRect(Color[] px, int w, int x, int y, int rw, int rh, Color fill, Color hi, Color outline)
    {
        FillRect(px, w, x - 2, y - 2, rw + 4, rh + 4, outline);
        FillRect(px, w, x, y, rw, rh, fill);
        if (hi != outline)
        {
            FillRect(px, w, x + 1, y + 1, rw - 2, 2, hi);
            FillRect(px, w, x + 1, y + 1, 2, rh - 2, hi);
        }
    }

    private static void DrawOutlinedEllipse(Color[] px, int w, int cx, int cy, int rw, int rh, Color fill, Color hi, Color outline)
    {
        FillEllipse(px, w, cx, cy, rw + 2, rh + 2, outline);
        FillEllipse(px, w, cx, cy, rw, rh, fill);
        if (hi != outline)
            FillEllipse(px, w, cx - 1, cy - 1, rw - 2, rh - 2, hi);
    }

    // ===================== PROJECTILE SPRITES =====================

    private static void GenerateProjectileSprites()
    {
        string dir = ROOT + "/Projectile";

        // WhitePixel (1x1)
        Texture2D whiteTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        whiteTex.SetPixel(0, 0, Color.white);
        whiteTex.Apply();
        SavePNG(whiteTex, dir + "/WhitePixel.png");

        // Circle projectile 8x8 (soft edge for visual quality)
        int cs = 8;
        Texture2D circleTex = new Texture2D(cs, cs, TextureFormat.RGBA32, false);
        Color[] cpx = new Color[cs * cs];
        float fc = cs / 2f;
        for (int y = 0; y < cs; y++)
            for (int x = 0; x < cs; x++)
            {
                float d = Mathf.Sqrt(Mathf.Pow(x - fc + 0.5f, 2) + Mathf.Pow(y - fc + 0.5f, 2)) / fc;
                cpx[y * cs + x] = new Color(1f, 1f, 1f, Mathf.Max(0f, 1f - d));
            }
        circleTex.SetPixels(cpx); circleTex.Apply();
        SavePNG(circleTex, dir + "/CircleProjectile.png");

        Object.DestroyImmediate(whiteTex);
        Object.DestroyImmediate(circleTex);
    }

    // ===================== DUNGEON SPRITES =====================

    private static void GenerateDungeonSprites()
    {
        string dir = ROOT + "/Dungeon";

        // --- Floor tiles (5 themes) ---
        for (int theme = 0; theme < 5; theme++)
            GenerateFloorTheme(dir, theme);

        // --- Wall (brick pattern) ---
        GenerateWall(dir);

        // --- Wall shadow ---
        GenerateWallShadow(dir);

        // --- Obstacles ---
        GenerateObstacles(dir);

        // --- Decorations ---
        GenerateDecorations(dir);

        // --- Atmospheric ---
        GenerateAtmospheric(dir);
    }

    private static void GenerateFloorTheme(string dir, int theme)
    {
        int tileW = 64, tileH = 64, gridCount = 4;
        int texW = tileW * gridCount, texH = tileH * gridCount;
        Texture2D tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
        Color[] px = new Color[texW * texH];

        Color stone1, stone2, stone3, grout, groutHi;
        if (theme == 1) { stone1 = new Color(0.32f, 0.38f, 0.28f); stone2 = new Color(0.28f, 0.34f, 0.24f); stone3 = new Color(0.30f, 0.36f, 0.26f); grout = new Color(0.18f, 0.22f, 0.14f); groutHi = new Color(0.22f, 0.26f, 0.18f); }
        else if (theme == 2) { stone1 = new Color(0.38f, 0.42f, 0.50f); stone2 = new Color(0.34f, 0.38f, 0.46f); stone3 = new Color(0.36f, 0.40f, 0.48f); grout = new Color(0.22f, 0.26f, 0.32f); groutHi = new Color(0.26f, 0.30f, 0.36f); }
        else if (theme == 3) { stone1 = new Color(0.42f, 0.32f, 0.26f); stone2 = new Color(0.38f, 0.28f, 0.22f); stone3 = new Color(0.40f, 0.30f, 0.24f); grout = new Color(0.20f, 0.12f, 0.08f); groutHi = new Color(0.24f, 0.16f, 0.12f); }
        else if (theme == 4) { stone1 = new Color(0.36f, 0.30f, 0.42f); stone2 = new Color(0.32f, 0.26f, 0.38f); stone3 = new Color(0.34f, 0.28f, 0.40f); grout = new Color(0.18f, 0.12f, 0.24f); groutHi = new Color(0.22f, 0.16f, 0.28f); }
        else { stone1 = new Color(0.35f, 0.33f, 0.37f); stone2 = new Color(0.30f, 0.28f, 0.32f); stone3 = new Color(0.32f, 0.30f, 0.34f); grout = new Color(0.20f, 0.18f, 0.22f); groutHi = new Color(0.25f, 0.23f, 0.27f); }

        Color stain = new Color(0.25f, 0.22f, 0.24f);
        Color highlight = new Color(0.40f, 0.38f, 0.42f);
        System.Random rng = new System.Random(42);

        for (int gy = 0; gy < gridCount; gy++)
            for (int gx = 0; gx < gridCount; gx++)
            {
                Color baseColor = (gx + gy) % 3 == 0 ? stone2 : ((gx + gy) % 3 == 1 ? stone3 : stone1);
                for (int y = 0; y < tileH; y++)
                    for (int x = 0; x < tileW; x++)
                    {
                        int px2 = gx * tileW + x, py = gy * tileH + y;
                        Color c = baseColor;
                        if (x <= 2 || y <= 2) { c = (x == 0 || y == 0) ? grout : groutHi; }
                        else
                        {
                            float noise = (float)(rng.NextDouble() - 0.5) * 0.04f;
                            c = new Color(Mathf.Clamp01(c.r + noise), Mathf.Clamp01(c.g + noise), Mathf.Clamp01(c.b + noise));
                            if (rng.Next(100) < 6) c = stain;
                            else if (rng.Next(100) < 3) c = highlight;
                        }
                        if (x == 3 && y > 2) c = new Color(c.r * 0.95f, c.g * 0.95f, c.b * 0.95f);
                        if (y == 3 && x > 2) c = new Color(c.r * 0.95f, c.g * 0.95f, c.b * 0.95f);
                        px[py * texW + px2] = c;
                    }
            }
        tex.SetPixels(px); tex.filterMode = FilterMode.Point; tex.wrapMode = TextureWrapMode.Repeat; tex.Apply();

        string[] themeNames = { "Stone", "Forest", "Ice", "Fire", "Shadow" };
        SavePNG(tex, dir + "/Floor_" + themeNames[theme] + ".png");
        Object.DestroyImmediate(tex);
    }

    private static void GenerateWall(string dir)
    {
        int w = 48, h = 48;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] px = new Color[w * h];
        Color brick1 = new Color(0.28f, 0.24f, 0.32f), brick2 = new Color(0.24f, 0.20f, 0.28f), brick3 = new Color(0.30f, 0.26f, 0.34f);
        Color mortar = new Color(0.16f, 0.13f, 0.20f), mortarHi = new Color(0.22f, 0.19f, 0.26f);
        Color brickHi = new Color(0.36f, 0.32f, 0.40f), brickDk = new Color(0.18f, 0.15f, 0.22f);
        System.Random rng = new System.Random(99);
        int brickH = 10, brickW1 = 20, brickW2 = 16;

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int row = y / brickH; bool offset = (row % 2 == 1);
                int bw = (row % 2 == 0) ? brickW1 : brickW2;
                int localX = offset ? (x + bw / 2) % bw : x % bw;
                int localY = y % brickH;
                Color c;
                if (localY <= 1 || localX <= 1) c = (localY == 0) ? mortarHi : mortar;
                else
                {
                    int seed = (row * 7 + (x / bw) * 13) % 5;
                    c = seed switch { 0 => brick1, 1 => brick2, 2 => brick3, 3 => brick2, _ => brick1 };
                    if (localY == 2 && localX > 1 && localX < 8) c = brickHi;
                    if (localY >= brickH - 3 && localX > bw - 6) c = brickDk;
                    float n = (float)(rng.NextDouble() - 0.5) * 0.03f;
                    c = new Color(Mathf.Clamp01(c.r + n), Mathf.Clamp01(c.g + n), Mathf.Clamp01(c.b + n));
                }
                px[y * w + x] = c;
            }
        tex.SetPixels(px); tex.filterMode = FilterMode.Point; tex.wrapMode = TextureWrapMode.Repeat; tex.Apply();
        SavePNG(tex, dir + "/Wall_Brick.png");
        Object.DestroyImmediate(tex);
    }

    private static void GenerateWallShadow(string dir)
    {
        int sw = 64, sh = 8;
        Texture2D tex = new Texture2D(sw, sh, TextureFormat.RGBA32, false);
        Color[] sp = new Color[sw * sh];
        for (int y = 0; y < sh; y++)
        {
            float a = Mathf.SmoothStep(0.5f, 0f, y / (float)sh);
            for (int x = 0; x < sw; x++) sp[y * sw + x] = new Color(0, 0, 0, a);
        }
        tex.SetPixels(sp); tex.filterMode = FilterMode.Point; tex.wrapMode = TextureWrapMode.Repeat; tex.Apply();
        SavePNG(tex, dir + "/WallShadow.png");
        Object.DestroyImmediate(tex);
    }

    private static void GenerateObstacles(string dir)
    {
        // Pillar
        SaveObstacle(dir, "Pillar", (px, w) =>
        {
            FillRect(px, w, 5, 0, 6, 3, new Color(0.40f, 0.37f, 0.44f));
            FillRect(px, w, 6, 3, 4, 10, new Color(0.35f, 0.32f, 0.38f));
            FillRect(px, w, 7, 4, 2, 8, new Color(0.42f, 0.38f, 0.45f));
            FillRect(px, w, 5, 13, 6, 3, new Color(0.40f, 0.37f, 0.44f));
        });

        // Tree
        SaveObstacle(dir, "Tree", (px, w) =>
        {
            FillRect(px, w, 6, 0, 4, 6, new Color(0.38f, 0.25f, 0.12f));
            FillEllipse(px, w, 8, 10, 6, 5, new Color(0.25f, 0.50f, 0.20f));
            FillEllipse(px, w, 7, 9, 3, 3, new Color(0.32f, 0.58f, 0.26f));
        });

        // Rock
        SaveObstacle(dir, "Rock", (px, w) =>
        {
            FillEllipse(px, w, 8, 7, 6, 5, new Color(0.32f, 0.30f, 0.35f));
            FillEllipse(px, w, 7, 6, 3, 3, new Color(0.38f, 0.36f, 0.40f));
        });

        // IceSpike
        SaveObstacle(dir, "IceSpike", (px, w) =>
        {
            FillRect(px, w, 5, 0, 2, 8, new Color(0.68f, 0.82f, 0.95f, 0.8f));
            FillRect(px, w, 3, 2, 6, 4, new Color(0.55f, 0.72f, 0.88f, 0.7f));
            FillRect(px, w, 4, 6, 3, 2, new Color(0.85f, 0.92f, 1f, 0.9f));
        });

        // LavaPool
        SaveObstacle(dir, "LavaPool", (px, w) =>
        {
            FillEllipse(px, w, 8, 6, 7, 5, new Color(0.85f, 0.20f, 0.02f));
            FillEllipse(px, w, 8, 6, 4, 3, new Color(1f, 0.45f, 0.05f));
            FillEllipse(px, w, 8, 6, 2, 1, new Color(1f, 0.72f, 0.15f));
        });

        // Tombstone
        SaveObstacle(dir, "Tombstone", (px, w) =>
        {
            FillRect(px, w, 4, 0, 8, 8, new Color(0.50f, 0.48f, 0.45f));
            FillRect(px, w, 5, 1, 6, 6, new Color(0.56f, 0.54f, 0.52f));
            FillRect(px, w, 7, 4, 2, 3, new Color(0.30f, 0.28f, 0.26f));
        });
    }

    private delegate void DrawObstacle(Color[] px, int w);

    private static void SaveObstacle(string dir, string name, DrawObstacle draw)
    {
        int s = 16;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        Color[] px = new Color[s * s];
        System.Array.Clear(px, 0, px.Length);
        draw(px, s);
        tex.SetPixels(px); tex.filterMode = FilterMode.Point; tex.Apply();
        SavePNG(tex, dir + "/Obstacle_" + name + ".png");
        Object.DestroyImmediate(tex);
    }

    private static void GenerateDecorations(string dir)
    {
        // Torch
        {
            int tw = 8, th = 12;
            Texture2D tex = new Texture2D(tw, th, TextureFormat.RGBA32, false);
            Color[] tpx = new Color[tw * th]; System.Array.Clear(tpx, 0, tpx.Length);
            FillRect(tpx, tw, 0, 2, 3, 8, new Color(0.45f, 0.35f, 0.20f));
            FillRect(tpx, tw, 3, 0, 2, 8, new Color(0.55f, 0.38f, 0.18f));
            FillPixel(tpx, tw, 4, 10, new Color(1f, 0.75f, 0.15f));
            FillPixel(tpx, tw, 4, 11, new Color(1f, 0.45f, 0.08f));
            tex.SetPixels(tpx); tex.filterMode = FilterMode.Point; tex.Apply();
            SavePNG(tex, dir + "/Deco_Torch.png"); Object.DestroyImmediate(tex);
        }

        // Flame (8x8 soft circle)
        {
            int fs = 8;
            Texture2D tex = new Texture2D(fs, fs, TextureFormat.RGBA32, false);
            Color[] fpx = new Color[fs * fs]; float fc = fs / 2f;
            for (int y = 0; y < fs; y++)
                for (int x = 0; x < fs; x++)
                {
                    float d = Mathf.Sqrt(Mathf.Pow(x - fc + 0.5f, 2) + Mathf.Pow(y - fc + 0.5f, 2)) / fc;
                    fpx[y * fs + x] = new Color(1f, 0.6f, 0.1f, Mathf.Max(0f, 1f - d) * 0.7f);
                }
            tex.SetPixels(fpx); tex.filterMode = FilterMode.Bilinear; tex.Apply();
            SavePNG(tex, dir + "/Deco_Flame.png"); Object.DestroyImmediate(tex);
        }

        // Cobweb (8x8 X-pattern)
        {
            int s = 8;
            Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            Color[] p = new Color[s * s]; System.Array.Clear(p, 0, p.Length);
            Color web = new Color(0.85f, 0.82f, 0.80f, 0.35f);
            for (int i = 0; i < s; i++) { FillPixel(p, s, i, i, web); FillPixel(p, s, s - 1 - i, i, web); }
            tex.SetPixels(p); tex.filterMode = FilterMode.Point; tex.Apply();
            SavePNG(tex, dir + "/Deco_Cobweb.png"); Object.DestroyImmediate(tex);
        }

        // Debris (6x4)
        {
            int w2 = 6, h2 = 4;
            Texture2D tex = new Texture2D(w2, h2, TextureFormat.RGBA32, false);
            Color[] p = new Color[w2 * h2]; System.Array.Clear(p, 0, p.Length);
            FillPixel(p, w2, 1, 1, new Color(0.30f, 0.27f, 0.33f));
            FillPixel(p, w2, 2, 1, new Color(0.22f, 0.19f, 0.25f));
            tex.SetPixels(p); tex.filterMode = FilterMode.Point; tex.Apply();
            SavePNG(tex, dir + "/Deco_Debris.png"); Object.DestroyImmediate(tex);
        }

        // Mushroom (6x6)
        {
            int s = 6;
            Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            Color[] p = new Color[s * s]; System.Array.Clear(p, 0, p.Length);
            FillRect(p, s, 2, 0, 2, 3, new Color(0.88f, 0.85f, 0.78f));
            FillRect(p, s, 1, 3, 4, 2, new Color(0.72f, 0.25f, 0.18f));
            tex.SetPixels(p); tex.filterMode = FilterMode.Point; tex.Apply();
            SavePNG(tex, dir + "/Deco_Mushroom.png"); Object.DestroyImmediate(tex);
        }

        // Vine (4x16)
        {
            int w2 = 4, h2 = 16;
            Texture2D tex = new Texture2D(w2, h2, TextureFormat.RGBA32, false);
            Color[] p = new Color[w2 * h2]; System.Array.Clear(p, 0, p.Length);
            FillRect(p, w2, 1, 0, 2, 16, new Color(0.35f, 0.22f, 0.10f));
            FillRect(p, w2, 0, 2, 2, 2, new Color(0.28f, 0.52f, 0.22f));
            FillRect(p, w2, 2, 6, 2, 2, new Color(0.28f, 0.52f, 0.22f));
            tex.SetPixels(p); tex.filterMode = FilterMode.Point; tex.Apply();
            SavePNG(tex, dir + "/Deco_Vine.png"); Object.DestroyImmediate(tex);
        }

        // Crystal (6x10) - ice variant
        {
            int w2 = 6, h2 = 10;
            Texture2D tex = new Texture2D(w2, h2, TextureFormat.RGBA32, false);
            Color[] p = new Color[w2 * h2]; System.Array.Clear(p, 0, p.Length);
            Color crystal = new Color(0.45f, 0.62f, 0.88f);
            Color crystalHi = new Color(0.62f, 0.78f, 0.95f);
            FillRect(p, w2, 2, 0, 2, 8, crystal);
            FillRect(p, w2, 1, 2, 4, 6, crystal);
            FillPixel(p, w2, 2, 8, crystalHi);
            tex.SetPixels(p); tex.filterMode = FilterMode.Point; tex.Apply();
            SavePNG(tex, dir + "/Deco_Crystal_Ice.png"); Object.DestroyImmediate(tex);
        }

        // Crystal (6x10) - shadow variant
        {
            int w2 = 6, h2 = 10;
            Texture2D tex = new Texture2D(w2, h2, TextureFormat.RGBA32, false);
            Color[] p = new Color[w2 * h2]; System.Array.Clear(p, 0, p.Length);
            Color crystal = new Color(0.45f, 0.18f, 0.62f);
            Color crystalHi = new Color(0.62f, 0.32f, 0.82f);
            FillRect(p, w2, 2, 0, 2, 8, crystal);
            FillRect(p, w2, 1, 2, 4, 6, crystal);
            FillPixel(p, w2, 2, 8, crystalHi);
            tex.SetPixels(p); tex.filterMode = FilterMode.Point; tex.Apply();
            SavePNG(tex, dir + "/Deco_Crystal_Shadow.png"); Object.DestroyImmediate(tex);
        }

        // Icicle (3x8)
        {
            int w2 = 3, h2 = 8;
            Texture2D tex = new Texture2D(w2, h2, TextureFormat.RGBA32, false);
            Color[] p = new Color[w2 * h2]; System.Array.Clear(p, 0, p.Length);
            FillRect(p, w2, 0, 6, 3, 2, new Color(0.68f, 0.82f, 0.95f, 0.8f));
            FillRect(p, w2, 1, 3, 1, 3, new Color(0.68f, 0.82f, 0.95f, 0.8f));
            tex.SetPixels(p); tex.filterMode = FilterMode.Point; tex.Apply();
            SavePNG(tex, dir + "/Deco_Icicle.png"); Object.DestroyImmediate(tex);
        }

        // LavaCrack (8x4)
        {
            int w2 = 8, h2 = 4;
            Texture2D tex = new Texture2D(w2, h2, TextureFormat.RGBA32, false);
            Color[] p = new Color[w2 * h2]; System.Array.Clear(p, 0, p.Length);
            FillRect(p, w2, 2, 1, 2, 2, new Color(1f, 0.45f, 0.05f));
            FillRect(p, w2, 4, 2, 2, 1, new Color(1f, 0.45f, 0.05f));
            tex.SetPixels(p); tex.filterMode = FilterMode.Point; tex.Apply();
            SavePNG(tex, dir + "/Deco_LavaCrack.png"); Object.DestroyImmediate(tex);
        }
    }

    private static void GenerateAtmospheric(string dir)
    {
        // Vignette (64x64)
        {
            int s = 64;
            Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            Color[] px = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float nx = (x / (float)s - 0.5f) * 2f, ny = (y / (float)s - 0.5f) * 2f;
                    float a = Mathf.SmoothStep(0.5f, 1.3f, Mathf.Sqrt(nx * nx + ny * ny)) - 0.5f;
                    px[y * s + x] = new Color(0, 0, 0, Mathf.Clamp01(a) * 0.55f);
                }
            tex.SetPixels(px); tex.Apply();
            SavePNG(tex, dir + "/Vignette.png"); Object.DestroyImmediate(tex);
        }

        // Dust particle (4x4, white)
        {
            int ps = 4;
            Texture2D tex = new Texture2D(ps, ps, TextureFormat.RGBA32, false);
            Color[] px = new Color[ps * ps]; float c = ps / 2f;
            for (int y = 0; y < ps; y++)
                for (int x = 0; x < ps; x++)
                {
                    float d = Mathf.Sqrt(Mathf.Pow(x - c + 0.5f, 2) + Mathf.Pow(y - c + 0.5f, 2)) / c;
                    float a = Mathf.Max(0f, 1f - d);
                    px[y * ps + x] = new Color(0.8f, 0.75f, 0.7f, a * 0.25f);
                }
            tex.SetPixels(px); tex.filterMode = FilterMode.Bilinear; tex.Apply();
            SavePNG(tex, dir + "/DustParticle.png"); Object.DestroyImmediate(tex);
        }

        // Ember particle (4x4)
        {
            int ps = 4;
            Texture2D tex = new Texture2D(ps, ps, TextureFormat.RGBA32, false);
            Color[] px = new Color[ps * ps]; float c = ps / 2f;
            for (int y = 0; y < ps; y++)
                for (int x = 0; x < ps; x++)
                {
                    float d = Mathf.Sqrt(Mathf.Pow(x - c + 0.5f, 2) + Mathf.Pow(y - c + 0.5f, 2)) / c;
                    float a = Mathf.Max(0f, 1f - d);
                    px[y * ps + x] = new Color(1f, 0.5f, 0.1f, a * 0.4f);
                }
            tex.SetPixels(px); tex.filterMode = FilterMode.Bilinear; tex.Apply();
            SavePNG(tex, dir + "/EmberParticle.png"); Object.DestroyImmediate(tex);
        }

        // Fog (128x32) - two variants
        for (int i = 0; i < 2; i++)
        {
            int tw = 128, th = 32;
            Texture2D tex = new Texture2D(tw, th, TextureFormat.RGBA32, false);
            Color[] px = new Color[tw * th];
            System.Random rng = new System.Random(i * 31 + 7);
            Color fc = i == 0 ? new Color(0.15f, 0.12f, 0.2f, 0.15f) : new Color(0.1f, 0.08f, 0.15f, 0.1f);
            for (int y = 0; y < th; y++)
            {
                float vf = Mathf.Sin((float)y / th * Mathf.PI);
                for (int x = 0; x < tw; x++)
                    px[y * tw + x] = new Color(fc.r, fc.g, fc.b, vf * (float)(rng.NextDouble() * 0.4 + 0.6) * fc.a);
            }
            tex.SetPixels(px); tex.filterMode = FilterMode.Bilinear; tex.wrapMode = TextureWrapMode.Repeat; tex.Apply();
            SavePNG(tex, dir + "/Fog_" + (i == 0 ? "A" : "B") + ".png"); Object.DestroyImmediate(tex);
        }

        // LightRay (8x64)
        {
            int rw = 8, rh = 64;
            Texture2D tex = new Texture2D(rw, rh, TextureFormat.RGBA32, false);
            Color[] px = new Color[rw * rh];
            Color rc = new Color(1f, 0.95f, 0.8f, 0.08f);
            for (int y = 0; y < rh; y++)
            {
                float ny = (float)y / rh;
                float vf = Mathf.Sin(ny * Mathf.PI) * Mathf.Max(0f, 1f - Mathf.Abs(ny - 0.5f) * 1.5f);
                for (int x = 0; x < rw; x++)
                {
                    float hf = Mathf.Max(0f, 1f - Mathf.Abs((float)x / rw - 0.5f) * 2f);
                    px[y * rw + x] = new Color(rc.r, rc.g, rc.b, vf * hf * rc.a);
                }
            }
            tex.SetPixels(px); tex.filterMode = FilterMode.Bilinear; tex.Apply();
            SavePNG(tex, dir + "/LightRay.png"); Object.DestroyImmediate(tex);
        }
    }

    // ===================== CHARACTER SPRITES =====================

    private static void GenerateCharacterSprites()
    {
        string dir = ROOT + "/Character";
        Color Outline = new Color(0.12f, 0.08f, 0.08f);

        // Warrior, Mage, Priest - 4 directions, 4 frames each
        string[] classNames = { "Warrior", "Mage", "Priest" };
        HeroClass[] classEnums = { HeroClass.Warrior, HeroClass.Mage, HeroClass.Priest };
        string[] dirNames = { "Down", "Left", "Up", "Right" };

        for (int ci = 0; ci < 3; ci++)
        {
            for (int di = 0; di < 4; di++)
            {
                for (int fi = 0; fi < 4; fi++)
                {
                    int w = 64, h = 64;
                    Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                    Color[] px = new Color[w * h];
                    System.Array.Clear(px, 0, px.Length);

                    // Draw based on class
                    switch (ci)
                    {
                        case 0: DrawWarriorFrame(px, w, h, di, fi); break;
                        case 1: DrawMageFrame(px, w, h, di, fi); break;
                        case 2: DrawPriestFrame(px, w, h, di, fi); break;
                    }

                    // Mirror for right direction
                    if (di == 3) // Right = mirrored Left
                    {
                        Color[] mirrored = new Color[w * h];
                        for (int y = 0; y < h; y++)
                            for (int x = 0; x < w; x++)
                                mirrored[y * w + (w - 1 - x)] = px[y * w + x];
                        tex.SetPixels(mirrored);
                    }
                    else
                    {
                        tex.SetPixels(px);
                    }
                    tex.filterMode = FilterMode.Point; tex.Apply();

                    string fname = string.Format("{0}/{1}_{2}_{3}.png", dir, classNames[ci], dirNames[di], fi);
                    SavePNG(tex, fname);
                    Object.DestroyImmediate(tex);
                }
            }

            // Weapon swing arc
            GenerateWeaponArc(dir, classNames[ci], ci);
        }
    }

    private static void GenerateWeaponArc(string dir, string className, int classIdx)
    {
        int arcW = 48, arcH = 48;
        Texture2D arcTex = new Texture2D(arcW, arcH, TextureFormat.RGBA32, false);
        Color[] arcPx = new Color[arcW * arcH];
        System.Array.Clear(arcPx, 0, arcPx.Length);

        Color slashColor, slashGlow;
        switch (classIdx)
        {
            case 0: slashColor = new Color(1f, 0.85f, 0.4f, 0.9f); slashGlow = new Color(1f, 0.95f, 0.7f, 0.5f); break;
            case 1: slashColor = new Color(0.4f, 0.6f, 1f, 0.9f); slashGlow = new Color(0.6f, 0.8f, 1f, 0.5f); break;
            default: slashColor = new Color(1f, 1f, 0.5f, 0.9f); slashGlow = new Color(1f, 1f, 0.8f, 0.5f); break;
        }

        float cx = arcW / 2f, cy = arcH / 2f;
        float innerR = 12f, outerR = 22f;
        for (int y = 0; y < arcH; y++)
            for (int x = 0; x < arcW; x++)
            {
                float dx = x - cx + 0.5f, dy = y - cy + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                if (dist >= innerR && dist <= outerR && angle >= -50f && angle <= 50f)
                {
                    float t = (dist - innerR) / (outerR - innerR);
                    float a = Mathf.Lerp(1f, 0.3f, t);
                    float angleFade = 1f - Mathf.Abs(angle) / 60f;
                    arcPx[y * arcW + x] = new Color(slashColor.r, slashColor.g, slashColor.b, a * angleFade * slashColor.a);
                }
                else if (dist >= innerR - 3f && dist <= outerR + 3f && angle >= -55f && angle <= 55f)
                {
                    float angleFade = 1f - Mathf.Abs(angle) / 65f;
                    arcPx[y * arcW + x] = new Color(slashGlow.r, slashGlow.g, slashGlow.b, 0.2f * angleFade);
                }
            }

        arcTex.SetPixels(arcPx); arcTex.filterMode = FilterMode.Bilinear; arcTex.Apply();
        SavePNG(arcTex, dir + "/" + className + "_WeaponArc.png");
        Object.DestroyImmediate(arcTex);
    }

    // ===== Warrior =====
    private static void DrawWarriorFrame(Color[] px, int w, int h, int dir, int frame)
    {
        Color Outline = new Color(0.12f, 0.08f, 0.08f);
        Color armor = new Color(0.58f, 0.55f, 0.52f), armorHi = new Color(0.72f, 0.69f, 0.65f);
        Color armorDk = new Color(0.42f, 0.39f, 0.36f), armorSh = new Color(0.32f, 0.29f, 0.27f);
        Color cape = new Color(0.78f, 0.18f, 0.14f), capeDk = new Color(0.58f, 0.12f, 0.09f), capeHi = new Color(0.88f, 0.28f, 0.22f);
        Color skin = new Color(0.95f, 0.80f, 0.65f), skinSh = new Color(0.82f, 0.65f, 0.50f);
        Color hair = new Color(0.55f, 0.38f, 0.18f);
        Color helm = new Color(0.65f, 0.60f, 0.56f), helmDk = new Color(0.45f, 0.40f, 0.37f);
        Color trim = new Color(0.88f, 0.75f, 0.28f), trimHi = new Color(0.95f, 0.85f, 0.45f);
        Color blade = new Color(0.80f, 0.84f, 0.90f), bladeHi = new Color(0.94f, 0.96f, 1f);
        Color handle = new Color(0.48f, 0.30f, 0.14f);
        Color boot = new Color(0.38f, 0.25f, 0.14f), bootHi = new Color(0.48f, 0.32f, 0.18f);
        Color shadow = new Color(0.12f, 0.10f, 0.10f, 0.35f);
        int bobY = (frame == 1 || frame == 3) ? 1 : 0;

        if (dir == 0) // DOWN
        {
            FillEllipse(px, w, 32, 6, 18, 5, shadow);
            DrawOutlinedRect(px, w, 22, 8 + bobY, 20, 26, cape, capeDk, Outline);
            FillRect(px, w, 24, 10 + bobY, 16, 22, cape);
            FillRect(px, w, 24, 10 + bobY, 8, 12, capeHi);
            FillRect(px, w, 26, 18 + bobY, 12, 8, capeDk);
            DrawOutlinedRect(px, w, 21, 8 + bobY, 8, 6, boot, bootHi, Outline);
            FillRect(px, w, 23, 10 + bobY, 4, 3, bootHi);
            DrawOutlinedRect(px, w, 35, 8 + bobY, 8, 6, boot, bootHi, Outline);
            FillRect(px, w, 37, 10 + bobY, 4, 3, bootHi);
            DrawOutlinedRect(px, w, 22, 14 + bobY, 7, 8, armorDk, armorSh, Outline);
            FillRect(px, w, 24, 16 + bobY, 3, 4, armor);
            DrawOutlinedRect(px, w, 35, 14 + bobY, 7, 8, armorDk, armorSh, Outline);
            FillRect(px, w, 37, 16 + bobY, 3, 4, armor);
            DrawOutlinedRect(px, w, 20, 22 + bobY, 24, 4, trim, trimHi, Outline);
            FillRect(px, w, 30, 22 + bobY, 4, 4, trimHi);
            FillPixel(px, w, 31, 24 + bobY, Outline);
            DrawOutlinedRect(px, w, 20, 26 + bobY, 24, 14, armor, armorDk, Outline);
            FillRect(px, w, 23, 28 + bobY, 18, 10, armorHi);
            FillRect(px, w, 22, 32 + bobY, 6, 4, armorDk);
            FillRect(px, w, 34, 32 + bobY, 6, 4, armorDk);
            DrawOutlinedEllipse(px, w, 20, 34 + bobY, 7, 6, armor, armorHi, Outline);
            DrawOutlinedEllipse(px, w, 44, 34 + bobY, 7, 6, armor, armorHi, Outline);
            FillRect(px, w, 16, 36 + bobY, 8, 2, trim);
            FillRect(px, w, 40, 36 + bobY, 8, 2, trim);
            FillRect(px, w, 28, 38 + bobY, 8, 4, skin);
            DrawOutlinedEllipse(px, w, 32, 44 + bobY, 10, 10, skin, skinSh, Outline);
            DrawOutlinedEllipse(px, w, 32, 47 + bobY, 11, 8, helm, helmDk, Outline);
            FillRect(px, w, 24, 48 + bobY, 16, 5, helm);
            FillRect(px, w, 26, 49 + bobY, 12, 3, helmDk);
            FillRect(px, w, 25, 48 + bobY, 14, 2, Outline);
            FillPixel(px, w, 28, 48 + bobY, new Color(0.9f, 0.8f, 0.3f));
            FillPixel(px, w, 35, 48 + bobY, new Color(0.9f, 0.8f, 0.3f));
            DrawOutlinedEllipse(px, w, 32, 56 + bobY, 5, 6, cape, capeHi, Outline);
            FillRect(px, w, 30, 52 + bobY, 4, 6, cape);
            FillRect(px, w, 28, 55 + bobY, 8, 4, capeHi);
            if (frame == 2) { DrawOutlinedRect(px, w, 48, 12 + bobY, 4, 24, blade, bladeHi, Outline); FillRect(px, w, 49, 16 + bobY, 2, 16, bladeHi); DrawOutlinedRect(px, w, 46, 36 + bobY, 8, 3, trim, trimHi, Outline); DrawOutlinedRect(px, w, 49, 39 + bobY, 2, 6, handle, Outline, Outline); }
            else { DrawOutlinedRect(px, w, 47, 18 + bobY, 3, 16, blade, bladeHi, Outline); FillRect(px, w, 48, 22 + bobY, 1, 10, bladeHi); FillRect(px, w, 46, 34 + bobY, 5, 2, trim); DrawOutlinedRect(px, w, 48, 36 + bobY, 2, 6, handle, Outline, Outline); }
        }
        else if (dir == 2) // UP
        {
            FillEllipse(px, w, 32, 6, 18, 5, shadow);
            DrawOutlinedRect(px, w, 20, 8 + bobY, 24, 28, cape, capeDk, Outline);
            FillRect(px, w, 22, 10 + bobY, 20, 24, cape);
            FillRect(px, w, 22, 10 + bobY, 10, 8, capeHi);
            FillRect(px, w, 24, 22 + bobY, 16, 10, capeDk);
            DrawOutlinedRect(px, w, 23, 8 + bobY, 7, 5, boot, bootHi, Outline);
            DrawOutlinedRect(px, w, 34, 8 + bobY, 7, 5, boot, bootHi, Outline);
            DrawOutlinedRect(px, w, 24, 13 + bobY, 7, 8, armorDk, armorSh, Outline);
            DrawOutlinedRect(px, w, 33, 13 + bobY, 7, 8, armorDk, armorSh, Outline);
            DrawOutlinedRect(px, w, 20, 22 + bobY, 24, 4, trim, trimHi, Outline);
            DrawOutlinedRect(px, w, 20, 26 + bobY, 24, 14, armorDk, armorSh, Outline);
            FillRect(px, w, 23, 28 + bobY, 18, 8, armor);
            DrawOutlinedEllipse(px, w, 20, 34 + bobY, 7, 6, armor, armorDk, Outline);
            DrawOutlinedEllipse(px, w, 44, 34 + bobY, 7, 6, armor, armorDk, Outline);
            DrawOutlinedEllipse(px, w, 32, 44 + bobY, 10, 10, helm, helmDk, Outline);
            FillRect(px, w, 24, 46 + bobY, 16, 6, helm);
            FillRect(px, w, 26, 47 + bobY, 12, 4, helmDk);
            FillRect(px, w, 26, 44 + bobY, 12, 3, hair);
            DrawOutlinedEllipse(px, w, 32, 56 + bobY, 5, 6, cape, capeHi, Outline);
            FillRect(px, w, 30, 52 + bobY, 4, 6, cape);
            FillRect(px, w, 28, 55 + bobY, 8, 4, capeHi);
            DrawOutlinedRect(px, w, 15, 16 + bobY, 3, 20, blade, bladeHi, Outline);
            FillRect(px, w, 16, 20 + bobY, 1, 12, bladeHi);
            FillRect(px, w, 14, 36 + bobY, 5, 2, trim);
            DrawOutlinedRect(px, w, 15, 38 + bobY, 2, 6, handle, Outline, Outline);
        }
        else // LEFT
        {
            FillEllipse(px, w, 32, 6, 16, 5, shadow);
            DrawOutlinedRect(px, w, 28, 8 + bobY, 18, 26, cape, capeDk, Outline);
            FillRect(px, w, 30, 10 + bobY, 14, 22, cape);
            FillRect(px, w, 30, 12 + bobY, 8, 10, capeHi);
            FillRect(px, w, 32, 22 + bobY, 10, 8, capeDk);
            int legOffset1 = (frame == 1) ? 2 : (frame == 3) ? -2 : 0;
            int legOffset2 = (frame == 1) ? -2 : (frame == 3) ? 2 : 0;
            DrawOutlinedRect(px, w, 26 + legOffset1, 8 + bobY, 8, 5, boot, bootHi, Outline);
            DrawOutlinedRect(px, w, 22 + legOffset2, 8 + bobY, 8, 5, boot, bootHi, Outline);
            DrawOutlinedRect(px, w, 27 + legOffset1, 13 + bobY, 7, 8, armorDk, armorSh, Outline);
            DrawOutlinedRect(px, w, 23 + legOffset2, 13 + bobY, 7, 8, armorDk, armorSh, Outline);
            DrawOutlinedRect(px, w, 21, 22 + bobY, 16, 4, trim, trimHi, Outline);
            FillRect(px, w, 22, 23 + bobY, 4, 2, trimHi);
            DrawOutlinedRect(px, w, 21, 26 + bobY, 16, 14, armor, armorDk, Outline);
            FillRect(px, w, 24, 28 + bobY, 10, 8, armorHi);
            DrawOutlinedEllipse(px, w, 22, 34 + bobY, 7, 6, armor, armorHi, Outline);
            FillRect(px, w, 18, 36 + bobY, 8, 2, trim);
            FillRect(px, w, 28, 38 + bobY, 6, 3, skin);
            DrawOutlinedEllipse(px, w, 30, 44 + bobY, 9, 10, skin, skinSh, Outline);
            DrawOutlinedEllipse(px, w, 30, 47 + bobY, 10, 8, helm, helmDk, Outline);
            FillRect(px, w, 22, 48 + bobY, 16, 5, helm);
            FillRect(px, w, 24, 49 + bobY, 12, 3, helmDk);
            FillRect(px, w, 24, 48 + bobY, 5, 2, Outline);
            FillPixel(px, w, 26, 48 + bobY, new Color(0.9f, 0.8f, 0.3f));
            FillPixel(px, w, 22, 46 + bobY, skinSh);
            DrawOutlinedEllipse(px, w, 30, 56 + bobY, 5, 6, cape, capeHi, Outline);
            FillRect(px, w, 28, 52 + bobY, 4, 6, cape);
            FillRect(px, w, 26, 55 + bobY, 8, 4, capeHi);
            if (frame == 2) { DrawOutlinedRect(px, w, 10, 14 + bobY, 4, 28, blade, bladeHi, Outline); FillRect(px, w, 11, 18 + bobY, 2, 18, bladeHi); DrawOutlinedRect(px, w, 8, 42 + bobY, 8, 3, trim, trimHi, Outline); DrawOutlinedRect(px, w, 11, 45 + bobY, 2, 6, handle, Outline, Outline); }
            else { DrawOutlinedRect(px, w, 16, 22 + bobY, 3, 18, blade, bladeHi, Outline); FillRect(px, w, 17, 26 + bobY, 1, 10, bladeHi); FillRect(px, w, 15, 40 + bobY, 5, 2, trim); DrawOutlinedRect(px, w, 17, 42 + bobY, 2, 6, handle, Outline, Outline); }
        }
    }

    // ===== Mage =====
    private static void DrawMageFrame(Color[] px, int w, int h, int dir, int frame)
    {
        Color Outline = new Color(0.12f, 0.08f, 0.08f);
        Color robe = new Color(0.24f, 0.30f, 0.65f), robeHi = new Color(0.36f, 0.44f, 0.82f);
        Color robeDk = new Color(0.16f, 0.20f, 0.50f), robeSh = new Color(0.10f, 0.14f, 0.38f);
        Color skin = new Color(0.95f, 0.80f, 0.65f), skinSh = new Color(0.82f, 0.65f, 0.50f);
        Color hat = new Color(0.22f, 0.28f, 0.62f), hatDk = new Color(0.15f, 0.19f, 0.45f);
        Color trim = new Color(0.68f, 0.48f, 0.98f), trimHi = new Color(0.82f, 0.65f, 1f);
        Color star = new Color(0.92f, 0.78f, 1f), starGlow = new Color(0.70f, 0.50f, 1f, 0.5f);
        Color orb = new Color(0.38f, 0.58f, 1f), orbHi = new Color(0.68f, 0.82f, 1f), orbGlow = new Color(0.30f, 0.50f, 1f, 0.3f);
        Color wood = new Color(0.52f, 0.34f, 0.16f), woodDk = new Color(0.40f, 0.24f, 0.12f);
        Color eye = new Color(0.28f, 0.48f, 0.88f);
        Color hair = new Color(0.72f, 0.58f, 0.32f);
        Color beard = new Color(0.68f, 0.58f, 0.38f), beardDk = new Color(0.52f, 0.42f, 0.25f);
        Color shadow = new Color(0.10f, 0.08f, 0.15f, 0.35f);
        int bobY = (frame == 1 || frame == 3) ? 1 : 0;

        if (dir == 0) // DOWN
        {
            FillEllipse(px, w, 32, 6, 18, 5, shadow);
            DrawOutlinedRect(px, w, 18, 8 + bobY, 28, 28, robe, robeDk, Outline);
            FillRect(px, w, 20, 10 + bobY, 24, 24, robeHi);
            FillRect(px, w, 22, 12 + bobY, 12, 8, robeHi);
            FillRect(px, w, 20, 26 + bobY, 28, 6, robeDk);
            FillRect(px, w, 26, 30 + bobY, 12, 4, robeSh);
            DrawOutlinedRect(px, w, 18, 8 + bobY, 28, 4, trim, trimHi, Outline);
            FillRect(px, w, 20, 9 + bobY, 24, 2, trimHi);
            FillRect(px, w, 30, 18 + bobY, 4, 1, star);
            FillRect(px, w, 31, 17 + bobY, 2, 3, star);
            FillEllipse(px, w, 32, 18 + bobY, 5, 5, starGlow);
            DrawOutlinedRect(px, w, 14, 24 + bobY, 8, 8, robeHi, robe, Outline);
            DrawOutlinedRect(px, w, 42, 24 + bobY, 8, 8, robeHi, robe, Outline);
            FillRect(px, w, 16, 26 + bobY, 4, 4, robeHi);
            FillRect(px, w, 44, 26 + bobY, 4, 4, robeHi);
            DrawOutlinedEllipse(px, w, 17, 22 + bobY, 4, 4, skin, skinSh, Outline);
            DrawOutlinedEllipse(px, w, 47, 22 + bobY, 4, 4, skin, skinSh, Outline);
            FillRect(px, w, 28, 36 + bobY, 8, 4, skin);
            DrawOutlinedEllipse(px, w, 32, 42 + bobY, 10, 10, skin, skinSh, Outline);
            FillEllipse(px, w, 27, 43 + bobY, 3, 2, Color.white);
            FillEllipse(px, w, 37, 43 + bobY, 3, 2, Color.white);
            FillPixel(px, w, 28, 43 + bobY, eye);
            FillPixel(px, w, 38, 43 + bobY, eye);
            DrawOutlinedRect(px, w, 26, 38 + bobY, 12, 6, beard, beardDk, Outline);
            FillRect(px, w, 28, 40 + bobY, 8, 3, beardDk);
            DrawOutlinedRect(px, w, 20, 48 + bobY, 24, 5, hat, hatDk, Outline);
            FillRect(px, w, 22, 49 + bobY, 20, 3, hatDk);
            DrawOutlinedRect(px, w, 26, 53 + bobY, 12, 6, hat, hatDk, Outline);
            DrawOutlinedRect(px, w, 29, 59 + bobY, 6, 4, hat, hatDk, Outline);
            FillRect(px, w, 31, 62 + bobY, 2, 2, hatDk);
            FillRect(px, w, 20, 52 + bobY, 24, 2, trim);
            FillRect(px, w, 30, 55 + bobY, 4, 1, star);
            FillRect(px, w, 31, 54 + bobY, 2, 3, star);
            DrawOutlinedRect(px, w, 49, 8 + bobY, 3, 30, wood, woodDk, Outline);
            FillRect(px, w, 50, 12 + bobY, 1, 20, woodDk);
            DrawOutlinedEllipse(px, w, 50, 6 + bobY, 6, 6, orb, orbHi, Outline);
            FillEllipse(px, w, 50, 6 + bobY, 8, 8, orbGlow);
            FillPixel(px, w, 49, 4 + bobY, orbHi);
            FillPixel(px, w, 51, 3 + bobY, orbHi);
        }
        else if (dir == 2) // UP
        {
            FillEllipse(px, w, 32, 6, 18, 5, shadow);
            DrawOutlinedRect(px, w, 18, 8 + bobY, 28, 28, robeDk, robeSh, Outline);
            FillRect(px, w, 20, 10 + bobY, 24, 24, robe);
            FillRect(px, w, 20, 26 + bobY, 28, 6, robeSh);
            DrawOutlinedRect(px, w, 18, 8 + bobY, 28, 4, trim, trimHi, Outline);
            DrawOutlinedRect(px, w, 14, 24 + bobY, 8, 8, robe, robeDk, Outline);
            DrawOutlinedRect(px, w, 42, 24 + bobY, 8, 8, robe, robeDk, Outline);
            DrawOutlinedEllipse(px, w, 32, 42 + bobY, 10, 10, hair, Outline, Outline);
            FillEllipse(px, w, 32, 42 + bobY, 9, 9, hair);
            DrawOutlinedRect(px, w, 20, 48 + bobY, 24, 5, hat, hatDk, Outline);
            DrawOutlinedRect(px, w, 26, 53 + bobY, 12, 6, hat, hatDk, Outline);
            DrawOutlinedRect(px, w, 29, 59 + bobY, 6, 4, hat, hatDk, Outline);
            FillRect(px, w, 20, 52 + bobY, 24, 2, trim);
            DrawOutlinedRect(px, w, 12, 8 + bobY, 3, 30, wood, woodDk, Outline);
            DrawOutlinedEllipse(px, w, 13, 6 + bobY, 6, 6, orb, orbHi, Outline);
            FillEllipse(px, w, 13, 6 + bobY, 8, 8, orbGlow);
        }
        else // LEFT
        {
            FillEllipse(px, w, 30, 6, 16, 5, shadow);
            DrawOutlinedRect(px, w, 20, 8 + bobY, 22, 28, robe, robeDk, Outline);
            FillRect(px, w, 22, 10 + bobY, 18, 24, robeHi);
            FillRect(px, w, 22, 26 + bobY, 22, 6, robeDk);
            DrawOutlinedRect(px, w, 20, 8 + bobY, 22, 4, trim, trimHi, Outline);
            FillRect(px, w, 28, 18 + bobY, 3, 1, star);
            FillRect(px, w, 29, 17 + bobY, 1, 3, star);
            FillEllipse(px, w, 29, 18 + bobY, 4, 4, starGlow);
            DrawOutlinedRect(px, w, 16, 24 + bobY, 8, 8, robeHi, robe, Outline);
            DrawOutlinedEllipse(px, w, 17, 22 + bobY, 4, 4, skin, skinSh, Outline);
            FillRect(px, w, 26, 36 + bobY, 6, 3, skin);
            DrawOutlinedEllipse(px, w, 29, 42 + bobY, 9, 10, skin, skinSh, Outline);
            FillEllipse(px, w, 25, 43 + bobY, 3, 2, Color.white);
            FillPixel(px, w, 26, 43 + bobY, eye);
            DrawOutlinedRect(px, w, 24, 38 + bobY, 8, 6, beard, beardDk, Outline);
            DrawOutlinedRect(px, w, 18, 48 + bobY, 20, 5, hat, hatDk, Outline);
            DrawOutlinedRect(px, w, 23, 53 + bobY, 10, 6, hat, hatDk, Outline);
            DrawOutlinedRect(px, w, 26, 59 + bobY, 5, 4, hat, hatDk, Outline);
            FillRect(px, w, 18, 52 + bobY, 20, 2, trim);
            DrawOutlinedRect(px, w, 10, 8 + bobY, 3, 30, wood, woodDk, Outline);
            DrawOutlinedEllipse(px, w, 11, 6 + bobY, 6, 6, orb, orbHi, Outline);
            FillEllipse(px, w, 11, 6 + bobY, 8, 8, orbGlow);
            FillPixel(px, w, 10, 4 + bobY, orbHi);
            FillPixel(px, w, 12, 3 + bobY, orbHi);
        }
    }

    // ===== Priest =====
    private static void DrawPriestFrame(Color[] px, int w, int h, int dir, int frame)
    {
        Color Outline = new Color(0.12f, 0.08f, 0.08f);
        Color robe = new Color(0.94f, 0.92f, 0.87f), robeHi = new Color(0.98f, 0.96f, 0.93f);
        Color robeDk = new Color(0.84f, 0.80f, 0.74f), robeSh = new Color(0.74f, 0.70f, 0.64f);
        Color gold = new Color(0.88f, 0.75f, 0.24f), goldHi = new Color(0.96f, 0.86f, 0.42f), goldDk = new Color(0.68f, 0.55f, 0.14f);
        Color skin = new Color(0.95f, 0.80f, 0.65f), skinSh = new Color(0.82f, 0.65f, 0.50f);
        Color hair = new Color(0.90f, 0.84f, 0.58f), hairDk = new Color(0.75f, 0.68f, 0.42f);
        Color eye = new Color(0.32f, 0.52f, 0.78f);
        Color holy = new Color(1f, 1f, 0.62f), halo = new Color(1f, 0.98f, 0.58f, 0.6f), haloGlow = new Color(1f, 0.95f, 0.5f, 0.2f);
        Color book = new Color(0.68f, 0.48f, 0.18f), bookDk = new Color(0.52f, 0.35f, 0.10f), bookPage = new Color(0.96f, 0.94f, 0.90f);
        Color cross = new Color(1f, 1f, 0.72f), crossGlow = new Color(1f, 1f, 0.5f, 0.3f);
        Color shadow = new Color(0.10f, 0.08f, 0.08f, 0.35f);
        int bobY = (frame == 1 || frame == 3) ? 1 : 0;

        if (dir == 0) // DOWN
        {
            FillEllipse(px, w, 32, 6, 18, 5, shadow);
            DrawOutlinedRect(px, w, 18, 8 + bobY, 28, 28, robe, robeDk, Outline);
            FillRect(px, w, 20, 10 + bobY, 24, 24, robeHi);
            FillRect(px, w, 20, 28 + bobY, 28, 6, robeDk);
            FillRect(px, w, 26, 32 + bobY, 12, 4, robeSh);
            DrawOutlinedRect(px, w, 18, 8 + bobY, 28, 4, gold, goldHi, Outline);
            DrawOutlinedRect(px, w, 18, 26 + bobY, 28, 3, gold, goldHi, Outline);
            FillRect(px, w, 30, 16 + bobY, 4, 1, cross);
            FillRect(px, w, 31, 15 + bobY, 2, 3, cross);
            FillEllipse(px, w, 32, 16 + bobY, 6, 6, crossGlow);
            DrawOutlinedRect(px, w, 12, 24 + bobY, 8, 8, robeHi, robe, Outline);
            DrawOutlinedRect(px, w, 44, 24 + bobY, 8, 8, robeHi, robe, Outline);
            FillRect(px, w, 12, 24 + bobY, 8, 2, gold);
            FillRect(px, w, 44, 24 + bobY, 8, 2, gold);
            DrawOutlinedEllipse(px, w, 15, 22 + bobY, 4, 4, skin, skinSh, Outline);
            DrawOutlinedEllipse(px, w, 49, 22 + bobY, 4, 4, skin, skinSh, Outline);
            DrawOutlinedRect(px, w, 10, 18 + bobY, 8, 10, book, bookDk, Outline);
            FillRect(px, w, 12, 20 + bobY, 4, 6, bookPage);
            FillRect(px, w, 13, 22 + bobY, 2, 2, cross);
            FillRect(px, w, 28, 36 + bobY, 8, 3, skin);
            DrawOutlinedEllipse(px, w, 32, 42 + bobY, 10, 10, skin, skinSh, Outline);
            FillEllipse(px, w, 27, 43 + bobY, 3, 2, Color.white);
            FillEllipse(px, w, 37, 43 + bobY, 3, 2, Color.white);
            FillPixel(px, w, 28, 43 + bobY, eye);
            FillPixel(px, w, 38, 43 + bobY, eye);
            FillRect(px, w, 29, 40 + bobY, 6, 1, skinSh);
            DrawOutlinedRect(px, w, 24, 46 + bobY, 16, 5, hair, hairDk, Outline);
            FillRect(px, w, 26, 47 + bobY, 12, 3, hairDk);
            DrawOutlinedEllipse(px, w, 32, 55 + bobY, 12, 4, halo, holy, Outline);
            FillEllipse(px, w, 32, 55 + bobY, 16, 8, haloGlow);
            FillPixel(px, w, 24, 57 + bobY, holy);
            FillPixel(px, w, 40, 57 + bobY, holy);
        }
        else if (dir == 2) // UP
        {
            FillEllipse(px, w, 32, 6, 18, 5, shadow);
            DrawOutlinedRect(px, w, 18, 8 + bobY, 28, 28, robeDk, robeSh, Outline);
            FillRect(px, w, 20, 10 + bobY, 24, 24, robe);
            FillRect(px, w, 18, 8 + bobY, 28, 4, gold);
            FillRect(px, w, 18, 26 + bobY, 28, 3, gold);
            DrawOutlinedRect(px, w, 12, 24 + bobY, 8, 8, robe, robeDk, Outline);
            DrawOutlinedRect(px, w, 44, 24 + bobY, 8, 8, robe, robeDk, Outline);
            DrawOutlinedEllipse(px, w, 32, 42 + bobY, 10, 10, hair, hairDk, Outline);
            FillEllipse(px, w, 32, 42 + bobY, 9, 9, hairDk);
            DrawOutlinedEllipse(px, w, 32, 55 + bobY, 12, 4, halo, holy, Outline);
            FillEllipse(px, w, 32, 55 + bobY, 16, 8, haloGlow);
            DrawOutlinedRect(px, w, 10, 18 + bobY, 8, 10, book, bookDk, Outline);
            FillPixel(px, w, 12, 20 + bobY, cross);
        }
        else // LEFT
        {
            FillEllipse(px, w, 30, 6, 16, 5, shadow);
            DrawOutlinedRect(px, w, 20, 8 + bobY, 22, 28, robe, robeDk, Outline);
            FillRect(px, w, 22, 10 + bobY, 18, 24, robeHi);
            FillRect(px, w, 20, 28 + bobY, 22, 6, robeDk);
            FillRect(px, w, 20, 8 + bobY, 22, 4, gold);
            FillRect(px, w, 20, 26 + bobY, 22, 3, gold);
            FillRect(px, w, 28, 16 + bobY, 3, 1, cross);
            FillRect(px, w, 29, 15 + bobY, 1, 3, cross);
            DrawOutlinedRect(px, w, 16, 24 + bobY, 8, 8, robeHi, robe, Outline);
            FillRect(px, w, 16, 24 + bobY, 8, 2, gold);
            DrawOutlinedEllipse(px, w, 17, 22 + bobY, 4, 4, skin, skinSh, Outline);
            DrawOutlinedRect(px, w, 10, 18 + bobY, 8, 10, book, bookDk, Outline);
            FillRect(px, w, 12, 20 + bobY, 4, 6, bookPage);
            FillRect(px, w, 13, 22 + bobY, 2, 2, cross);
            FillRect(px, w, 26, 36 + bobY, 6, 3, skin);
            DrawOutlinedEllipse(px, w, 29, 42 + bobY, 9, 10, skin, skinSh, Outline);
            FillEllipse(px, w, 25, 43 + bobY, 3, 2, Color.white);
            FillPixel(px, w, 26, 43 + bobY, eye);
            FillRect(px, w, 26, 40 + bobY, 4, 1, skinSh);
            DrawOutlinedRect(px, w, 22, 46 + bobY, 14, 5, hair, hairDk, Outline);
            DrawOutlinedEllipse(px, w, 29, 55 + bobY, 11, 4, halo, holy, Outline);
            FillEllipse(px, w, 29, 55 + bobY, 14, 7, haloGlow);
        }
    }

    // ===================== ENEMY SPRITES =====================

    private static void GenerateEnemySprites()
    {
        string dir = ROOT + "/Enemy";

        // Melee Goblin (16x20)
        SaveEnemySprite(dir, "Melee_Goblin", 16, 20, (px, w, h) =>
        {
            Color Outline = new Color(0.12f, 0.08f, 0.08f);
            Color skin = new Color(0.45f, 0.72f, 0.35f), skinHi = new Color(0.58f, 0.82f, 0.48f), skinDk = new Color(0.32f, 0.55f, 0.22f);
            Color vest = new Color(0.65f, 0.18f, 0.18f), vestDk = new Color(0.48f, 0.12f, 0.12f), vestHi = new Color(0.78f, 0.28f, 0.22f);
            Color eye = new Color(1f, 0.35f, 0.05f), eyeGlow = new Color(1f, 0.4f, 0f, 0.4f);
            Color club = new Color(0.52f, 0.35f, 0.15f), clubDk = new Color(0.38f, 0.22f, 0.08f);
            Color ear = new Color(0.40f, 0.65f, 0.30f);
            Color mouth = new Color(0.35f, 0.10f, 0.10f), tooth = new Color(0.90f, 0.88f, 0.80f);

            FillRect(px, w, 4, 5, 8, 8, Outline); FillRect(px, w, 5, 6, 6, 6, vest); FillRect(px, w, 6, 7, 4, 3, vestHi);
            FillRect(px, w, 5, 1, 3, 4, Outline); FillRect(px, w, 6, 2, 2, 3, vestDk);
            FillRect(px, w, 9, 1, 3, 4, Outline); FillRect(px, w, 10, 2, 2, 3, vestDk);
            FillRect(px, w, 5, 1, 3, 2, skinDk); FillRect(px, w, 9, 1, 3, 2, skinDk);
            FillRect(px, w, 2, 7, 3, 4, Outline); FillRect(px, w, 3, 8, 2, 3, skin); FillPixel(px, w, 3, 7, skinHi);
            FillRect(px, w, 12, 7, 3, 4, Outline); FillRect(px, w, 13, 8, 2, 3, skin);
            FillEllipse(px, w, 8, 14, 6, 5, Outline); FillEllipse(px, w, 8, 14, 5, 4, skin); FillEllipse(px, w, 7, 15, 3, 2, skinHi);
            FillRect(px, w, 2, 14, 3, 3, Outline); FillRect(px, w, 3, 15, 2, 2, ear);
            FillRect(px, w, 12, 14, 3, 3, Outline); FillRect(px, w, 13, 15, 2, 2, ear);
            FillEllipse(px, w, 6, 16, 2, 2, eyeGlow); FillPixel(px, w, 6, 16, eye);
            FillEllipse(px, w, 10, 16, 2, 2, eyeGlow); FillPixel(px, w, 10, 16, eye);
            FillRect(px, w, 6, 13, 4, 2, mouth); FillPixel(px, w, 6, 14, tooth); FillPixel(px, w, 9, 14, tooth);
            FillRect(px, w, 14, 4, 2, 8, Outline); FillRect(px, w, 15, 5, 1, 6, club);
            FillRect(px, w, 14, 3, 3, 3, Outline); FillRect(px, w, 15, 4, 2, 2, clubDk); FillPixel(px, w, 15, 3, club);
        });

        // Ranged Mage (16x20)
        SaveEnemySprite(dir, "Ranged_Mage", 16, 20, (px, w, h) =>
        {
            Color Outline = new Color(0.12f, 0.08f, 0.08f);
            Color robe = new Color(0.72f, 0.42f, 0.12f), robeHi = new Color(0.82f, 0.55f, 0.22f), robeDk = new Color(0.55f, 0.32f, 0.08f);
            Color hood = new Color(0.52f, 0.32f, 0.08f), hoodDk = new Color(0.40f, 0.22f, 0.05f);
            Color face = new Color(0.55f, 0.45f, 0.35f);
            Color eye = new Color(1f, 0.82f, 0.22f), eyeGlow = new Color(1f, 0.8f, 0.2f, 0.4f);
            Color orb = new Color(0.32f, 0.55f, 1f), orbHi = new Color(0.55f, 0.75f, 1f), orbGlow = new Color(0.25f, 0.45f, 1f, 0.3f);
            Color staff = new Color(0.52f, 0.35f, 0.15f), staffDk = new Color(0.40f, 0.25f, 0.10f);

            FillRect(px, w, 3, 1, 10, 3, Outline); FillRect(px, w, 4, 2, 8, 2, robeDk);
            FillRect(px, w, 4, 4, 8, 10, Outline); FillRect(px, w, 5, 5, 6, 8, robe); FillRect(px, w, 6, 6, 4, 4, robeHi);
            FillRect(px, w, 5, 10, 2, 3, robeDk); FillRect(px, w, 9, 10, 2, 3, robeDk);
            FillRect(px, w, 1, 8, 4, 5, Outline); FillRect(px, w, 2, 9, 3, 4, robe); FillPixel(px, w, 2, 7, face);
            FillRect(px, w, 12, 8, 4, 5, Outline); FillRect(px, w, 13, 9, 3, 4, robe);
            FillEllipse(px, w, 8, 16, 5, 4, Outline); FillEllipse(px, w, 8, 16, 4, 3, hood);
            FillRect(px, w, 5, 14, 6, 3, hoodDk); FillRect(px, w, 5, 14, 6, 2, face);
            FillEllipse(px, w, 6, 16, 2, 2, eyeGlow); FillPixel(px, w, 6, 16, eye);
            FillEllipse(px, w, 10, 16, 2, 2, eyeGlow); FillPixel(px, w, 10, 16, eye);
            FillRect(px, w, 14, 3, 2, 10, Outline); FillRect(px, w, 15, 4, 1, 8, staff);
            FillRect(px, w, 14, 2, 1, 2, staffDk);
            FillEllipse(px, w, 14, 2, 3, 3, Outline); FillEllipse(px, w, 14, 2, 2, 2, orb);
            FillEllipse(px, w, 14, 2, 4, 4, orbGlow); FillPixel(px, w, 13, 1, orbHi); FillPixel(px, w, 15, 0, orbHi);
        });

        // Elite Knight (20x24)
        SaveEnemySprite(dir, "Elite_Knight", 20, 24, (px, w, h) =>
        {
            Color Outline = new Color(0.12f, 0.08f, 0.08f);
            Color armor = new Color(0.92f, 0.78f, 0.12f), armorHi = new Color(1f, 0.88f, 0.28f);
            Color armorDk = new Color(0.72f, 0.58f, 0.08f), armorSh = new Color(0.55f, 0.42f, 0.05f);
            Color skin = new Color(0.82f, 0.68f, 0.52f), skinDk = new Color(0.68f, 0.52f, 0.38f);
            Color eye = new Color(1f, 0.05f, 0.05f), eyeGlow = new Color(1f, 0f, 0f, 0.4f);
            Color helmet = new Color(0.78f, 0.62f, 0.08f), helmetDk = new Color(0.58f, 0.45f, 0.05f);
            Color blade = new Color(0.85f, 0.88f, 0.92f), bladeHi = new Color(0.95f, 0.97f, 1f);
            Color handle = new Color(0.55f, 0.38f, 0.18f);
            Color crest = new Color(0.85f, 0.12f, 0.08f), crestHi = new Color(1f, 0.22f, 0.15f);

            FillRect(px, w, 5, 6, 10, 10, Outline); FillRect(px, w, 6, 7, 8, 8, armor);
            FillRect(px, w, 7, 8, 4, 4, armorHi); FillRect(px, w, 6, 12, 8, 3, armorDk);
            FillEllipse(px, w, 5, 12, 4, 4, Outline); FillEllipse(px, w, 5, 12, 3, 3, armor); FillPixel(px, w, 4, 11, armorHi);
            FillEllipse(px, w, 15, 12, 4, 4, Outline); FillEllipse(px, w, 15, 12, 3, 3, armor); FillPixel(px, w, 16, 11, armorHi);
            FillRect(px, w, 5, 1, 4, 5, Outline); FillRect(px, w, 6, 2, 3, 4, armorDk); FillRect(px, w, 6, 3, 2, 2, armor);
            FillRect(px, w, 11, 1, 4, 5, Outline); FillRect(px, w, 12, 2, 3, 4, armorDk); FillRect(px, w, 12, 3, 2, 2, armor);
            FillRect(px, w, 5, 0, 4, 2, armorSh); FillRect(px, w, 11, 0, 4, 2, armorSh);
            FillRect(px, w, 2, 8, 4, 5, Outline); FillRect(px, w, 3, 9, 3, 4, armorDk);
            FillRect(px, w, 14, 8, 4, 5, Outline); FillRect(px, w, 15, 9, 3, 4, armorDk);
            FillEllipse(px, w, 10, 18, 6, 5, Outline); FillEllipse(px, w, 10, 18, 5, 4, helmet);
            FillRect(px, w, 6, 17, 8, 3, helmetDk); FillRect(px, w, 6, 18, 8, 1, Outline);
            FillEllipse(px, w, 8, 18, 2, 1, eyeGlow); FillPixel(px, w, 8, 18, eye);
            FillEllipse(px, w, 12, 18, 2, 1, eyeGlow); FillPixel(px, w, 12, 18, eye);
            FillRect(px, w, 9, 22, 2, 2, Outline); FillRect(px, w, 10, 23, 1, 1, crest);
            FillRect(px, w, 8, 21, 4, 2, crest); FillRect(px, w, 9, 22, 2, 1, crestHi);
            FillRect(px, w, 17, 2, 2, 12, Outline); FillRect(px, w, 18, 3, 1, 10, blade);
            FillPixel(px, w, 18, 5, bladeHi); FillRect(px, w, 16, 14, 4, 2, armor);
            FillPixel(px, w, 17, 15, armorHi);
            FillRect(px, w, 17, 16, 2, 4, Outline); FillRect(px, w, 18, 17, 1, 3, handle);
        });

        // Boss Demon Lord (24x28)
        SaveEnemySprite(dir, "Boss_DemonLord", 24, 28, (px, w, h) =>
        {
            Color Outline = new Color(0.12f, 0.08f, 0.08f);
            Color body = new Color(0.52f, 0.05f, 0.62f), bodyHi = new Color(0.65f, 0.15f, 0.75f);
            Color bodyDk = new Color(0.38f, 0.02f, 0.45f), bodySh = new Color(0.25f, 0.01f, 0.32f);
            Color skin = new Color(0.62f, 0.12f, 0.55f), skinHi = new Color(0.75f, 0.22f, 0.65f);
            Color eye = new Color(1f, 0.08f, 0.05f), eyeGlow = new Color(1f, 0.1f, 0f, 0.5f);
            Color horn = new Color(0.32f, 0.18f, 0.08f), hornHi = new Color(0.42f, 0.25f, 0.12f);
            Color wing = new Color(0.38f, 0.02f, 0.45f), wingVein = new Color(0.55f, 0.12f, 0.62f);
            Color chest = new Color(1f, 0.22f, 0.18f), chestGlow = new Color(1f, 0.3f, 0.2f, 0.4f);
            Color claw = new Color(0.95f, 0.92f, 0.85f);
            Color mouth = new Color(0.20f, 0.02f, 0.12f), fang = new Color(0.92f, 0.90f, 0.82f);

            FillRect(px, w, 0, 10, 5, 8, Outline); FillRect(px, w, 1, 11, 4, 6, wing);
            FillRect(px, w, 2, 13, 2, 4, wingVein); FillRect(px, w, 1, 11, 1, 2, skin);
            FillRect(px, w, 19, 10, 5, 8, Outline); FillRect(px, w, 20, 11, 4, 6, wing);
            FillRect(px, w, 21, 13, 2, 4, wingVein); FillRect(px, w, 23, 11, 1, 2, skin);
            FillRect(px, w, 6, 4, 12, 10, Outline); FillRect(px, w, 7, 5, 10, 8, body);
            FillRect(px, w, 8, 6, 5, 4, bodyHi); FillRect(px, w, 7, 10, 10, 3, bodyDk);
            FillEllipse(px, w, 12, 9, 3, 3, Outline); FillEllipse(px, w, 12, 9, 2, 2, chest);
            FillEllipse(px, w, 12, 9, 4, 4, chestGlow);
            FillRect(px, w, 2, 6, 5, 5, Outline); FillRect(px, w, 3, 7, 4, 4, skin);
            FillPixel(px, w, 4, 8, skinHi);
            FillRect(px, w, 17, 6, 5, 5, Outline); FillRect(px, w, 18, 7, 4, 4, skin);
            FillPixel(px, w, 2, 5, Outline); FillPixel(px, w, 3, 5, claw); FillPixel(px, w, 4, 5, claw);
            FillPixel(px, w, 22, 5, claw); FillPixel(px, w, 21, 5, claw);
            FillRect(px, w, 7, 0, 4, 5, Outline); FillRect(px, w, 8, 1, 3, 4, bodyDk);
            FillRect(px, w, 8, 2, 2, 2, body);
            FillRect(px, w, 13, 0, 4, 5, Outline); FillRect(px, w, 14, 1, 3, 4, bodyDk);
            FillRect(px, w, 14, 2, 2, 2, body);
            FillRect(px, w, 7, 0, 4, 1, bodySh); FillRect(px, w, 13, 0, 4, 1, bodySh);
            FillEllipse(px, w, 12, 18, 7, 6, Outline); FillEllipse(px, w, 12, 18, 6, 5, skin);
            FillEllipse(px, w, 11, 19, 3, 3, skinHi);
            FillRect(px, w, 3, 20, 3, 4, Outline); FillRect(px, w, 4, 21, 2, 3, horn);
            FillPixel(px, w, 4, 20, hornHi);
            FillRect(px, w, 2, 24, 2, 4, Outline); FillRect(px, w, 3, 25, 1, 3, horn);
            FillRect(px, w, 18, 20, 3, 4, Outline); FillRect(px, w, 19, 21, 2, 3, horn);
            FillPixel(px, w, 20, 20, hornHi);
            FillRect(px, w, 20, 24, 2, 4, Outline); FillRect(px, w, 21, 25, 1, 3, horn);
            FillEllipse(px, w, 9, 19, 3, 2, eyeGlow); FillRect(px, w, 8, 19, 3, 1, eye);
            FillEllipse(px, w, 15, 19, 3, 2, eyeGlow); FillRect(px, w, 14, 19, 3, 1, eye);
            FillRect(px, w, 9, 15, 6, 2, mouth);
            FillPixel(px, w, 9, 14, fang); FillPixel(px, w, 14, 14, fang);
            FillPixel(px, w, 10, 16, fang); FillPixel(px, w, 13, 16, fang);
        });
    }

    private delegate void DrawEnemy(Color[] px, int w, int h);

    private static void SaveEnemySprite(string dir, string name, int w, int h, DrawEnemy draw)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] px = new Color[w * h];
        System.Array.Clear(px, 0, px.Length);
        draw(px, w, h);
        tex.SetPixels(px); tex.filterMode = FilterMode.Point; tex.Apply();
        SavePNG(tex, dir + "/" + name + ".png");
        Object.DestroyImmediate(tex);
    }

    // ===================== IMPORT SETTINGS =====================

    private static void SetTextureImportSettings()
    {
        string[] spriteDirs = {
            ROOT + "/Projectile",
            ROOT + "/Dungeon",
            ROOT + "/Character",
            ROOT + "/Enemy"
        };

        foreach (string dir in spriteDirs)
        {
            string absDir = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, dir);
            if (!System.IO.Directory.Exists(absDir)) continue;
            string[] files = System.IO.Directory.GetFiles(absDir, "*.png");
            foreach (string file in files)
            {
                string assetPath = "Assets" + file.Substring(Application.dataPath.Length);
                TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.spritePixelsPerUnit = GetPPU(assetPath);
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
            }
        }
    }

    private static float GetPPU(string assetPath)
    {
        if (assetPath.Contains("/Projectile/")) return 1f;
        if (assetPath.Contains("/Dungeon/"))
        {
            if (assetPath.Contains("Floor_")) return 64f;
            if (assetPath.Contains("Wall_Brick")) return 48f;
            if (assetPath.Contains("WallShadow")) return 8f;
            if (assetPath.Contains("Obstacle_")) return 16f;
            if (assetPath.Contains("Deco_")) return 8f;
            return 64f;
        }
        if (assetPath.Contains("/Character/")) return 64f;
        if (assetPath.Contains("/Enemy/")) return 16f;
        return 32f;
    }
}
