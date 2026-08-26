using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Chibi pixel-art sprite generator — mimics Deltarune/Undertale overworld style.
/// Small canvas (24×32), big head, short body, flat cel-shading, complementary colors.
/// Run via: new SpriteGenerator().GenerateAll()
/// </summary>
public class SpriteGenerator
{
    const int CW = 24, CH = 32;   // character canvas — small & chibi
    const int EW = 24, EH = 32;   // enemy canvas
    const int BW = 48, BH = 56;   // boss canvas (2x character)
    const int AW = 32;             // arc size

    static Color C(float r, float g, float b, float a = 1f) => new Color(r, g, b, a);

    Color[] px; int w, h;
    void Init(int cw, int ch) { w = cw; h = ch; px = new Color[cw * ch]; }

    void P(int x, int y, Color c) { if ((uint)x < (uint)w && (uint)y < (uint)h) px[y * w + x] = c; }
    void R(int x, int y, int rw, int rh, Color c) { for (int dy = 0; dy < rh; dy++) for (int dx = 0; dx < rw; dx++) P(x + dx, y + dy, c); }

    void Outline(Color col)
    {
        var src = (Color[])px.Clone();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (src[y * w + x].a > 0.01f) continue;
                bool has = false;
                for (int dy = -1; dy <= 1 && !has; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    { int nx = x + dx, ny = y + dy; if ((uint)nx < (uint)w && (uint)ny < (uint)h && src[ny * w + nx].a > 0.01f) { has = true; break; } }
                if (has) px[y * w + x] = col;
            }
    }

    void Save(string path, Color outlineColor)
    {
        Outline(outlineColor);
        Color[] flipped = new Color[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) flipped[(h - 1 - y) * w + x] = px[y * w + x];
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels(flipped); tex.filterMode = FilterMode.Point; tex.Apply();
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null) { imp.filterMode = FilterMode.Point; imp.textureCompression = TextureImporterCompression.Uncompressed; imp.mipmapEnabled = false; imp.spritePixelsPerUnit = 32f; imp.SaveAndReimport(); }
    }

    static readonly Color OL = C(0.06f, 0.03f, 0.02f);

    // ================================================================
    //  WARRIOR — chibi: big head, short body, red+steel complementary
    // ================================================================
    void DrawWarrior(int dir, int frame)
    {
        Init(CW, CH);
        int cx = CW / 2;
        bool side = (dir == 1 || dir == 3);
        int lo = (frame == 1) ? -1 : (frame == 3) ? 1 : 0;

        // Colors — 3 tones each for better depth
        Color skin = C(0.88f, 0.72f, 0.56f), skinHi = C(0.95f, 0.82f, 0.65f), skinDk = C(0.68f, 0.52f, 0.40f);
        Color armor = C(0.72f, 0.16f, 0.14f), armorHi = C(0.88f, 0.30f, 0.26f), armorDk = C(0.48f, 0.08f, 0.06f);
        Color steel = C(0.58f, 0.60f, 0.66f), steelHi = C(0.76f, 0.78f, 0.84f), steelDk = C(0.36f, 0.38f, 0.44f);
        Color gold = C(0.84f, 0.68f, 0.16f), goldHi = C(1f, 0.88f, 0.38f), goldDk = C(0.60f, 0.48f, 0.10f);
        Color boot = C(0.26f, 0.15f, 0.07f), bootHi = C(0.38f, 0.24f, 0.12f);
        Color plume = C(0.92f, 0.22f, 0.12f), plumeHi = C(1f, 0.42f, 0.22f);
        Color blade = C(0.82f, 0.84f, 0.92f), bladeHi = C(0.96f, 0.98f, 1f);
        Color eye = C(0.05f, 0.05f, 0.06f);

        // === Plume (taller, more visible) ===
        R(cx - 1, 1, 3, 2, plumeHi);
        R(cx, 1, 1, 3, plume);

        // === Helmet (rounded dome) ===
        R(cx - 3, 4, 7, 1, steel);
        R(cx - 4, 5, 9, 4, steel);
        R(cx - 4, 5, 9, 1, steelHi);        // top highlight
        R(cx - 4, 8, 9, 1, steelDk);        // bottom rim shadow
        R(cx - 5, 6, 1, 3, steelDk);         // left edge
        R(cx + 4, 6, 1, 3, steelDk);         // right edge
        // Visor bar
        R(cx - 4, 7, 9, 1, C(0.20f, 0.20f, 0.25f));

        // === Face ===
        if (dir == 0) // Down
        {
            R(cx - 3, 9, 7, 4, skin);
            R(cx - 3, 9, 7, 1, skinHi);
            R(cx - 3, 12, 7, 1, skinDk);
            P(cx - 2, 10, eye); P(cx + 2, 10, eye);
            R(cx - 1, 12, 3, 1, skinDk); // mouth
            // Cheek guards
            R(cx - 4, 9, 1, 4, steelDk);
            R(cx + 4, 9, 1, 4, steelDk);
        }
        else if (dir == 2) // Up
        {
            R(cx - 4, 9, 9, 4, steel);
            R(cx - 3, 9, 7, 1, steelHi);
        }
        else // Side
        {
            R(cx - 3, 9, 7, 4, skin);
            R(cx - 3, 9, 7, 1, skinHi);
            int ex = dir == 1 ? cx - 2 : cx + 1;
            P(ex, 10, eye);
            R(cx - 2, 12, 3, 1, skinDk);
            R(cx - 4, 9, 1, 4, steelDk);
        }

        // === Pauldrons (shoulder armor — visible bumps) ===
        P(cx - 5, 13, steelHi); P(cx - 5, 14, steel);
        P(cx + 5, 13, steelHi); P(cx + 5, 14, steel);

        // === Torso (tapered: wider at shoulders, narrower at waist) ===
        R(cx - 4, 13, 9, 3, armor);          // chest (wider)
        R(cx - 4, 13, 9, 1, armorHi);         // top highlight
        R(cx - 3, 16, 7, 4, armorDk);         // waist (narrower, darker)
        R(cx - 3, 16, 1, 4, C(0.35f, 0.05f, 0.04f)); // left shadow
        R(cx + 3, 16, 1, 4, C(0.35f, 0.05f, 0.04f)); // right shadow
        // Chest plate (raised center)
        R(cx - 2, 14, 5, 4, armor);
        R(cx - 2, 14, 5, 1, armorHi);
        R(cx - 2, 17, 5, 1, armorDk);
        // Chest gem (gold)
        R(cx - 1, 15, 3, 2, gold);
        P(cx, 15, goldHi);
        P(cx, 16, goldDk);

        // === Belt ===
        R(cx - 3, 19, 7, 1, steelDk);
        P(cx, 19, gold);

        // === Arms (clearly visible, with gauntlets) ===
        R(cx - 6, 14, 2, 4, armor);
        R(cx - 6, 14, 1, 4, armorDk);
        R(cx + 5, 14, 2, 4, armor);
        R(cx + 6, 14, 1, 4, armorDk);
        // Gauntlets (steel, distinct from arm)
        R(cx - 6, 18, 2, 2, steel);
        R(cx - 6, 18, 2, 1, steelHi);
        R(cx + 5, 18, 2, 2, steel);
        R(cx + 5, 18, 2, 1, steelHi);

        // === Sword (right side, with guard and pommel) ===
        if (dir != 2)
        {
            R(cx + 7, 17, 1, 2, boot);       // handle
            R(cx + 6, 16, 3, 1, gold);        // crossguard
            P(cx + 6, 16, goldHi);
            R(cx + 7, 8, 1, 8, blade);        // blade
            P(cx + 7, 8, bladeHi);           // blade highlight
            P(cx + 7, 7, bladeHi);            // tip
        }

        // === Legs (short, stocky) ===
        R(cx - 3 + lo, 20, 3, 4, armorDk);
        R(cx + 0 - lo, 20, 3, 4, armorDk);
        R(cx - 3 + lo, 20, 3, 1, armor);
        R(cx + 0 - lo, 20, 3, 1, armor);
        // Knee guards
        P(cx - 2 + lo, 22, steel);
        P(cx + 1 - lo, 22, steel);

        // === Boots (distinct, with toe) ===
        R(cx - 3 + lo, 24, 3, 3, boot);
        R(cx + 0 - lo, 24, 3, 3, boot);
        R(cx - 3 + lo, 24, 3, 1, bootHi);    // top highlight
        R(cx + 0 - lo, 24, 3, 1, bootHi);
        R(cx - 3 + lo, 26, 1, 1, C(0.15f, 0.08f, 0.03f)); // toe shadow
        R(cx + 0 - lo, 26, 1, 1, C(0.15f, 0.08f, 0.03f));

        Save($"Assets/Resources/Sprites/Character/Warrior_{DirName(dir)}_{frame}.png", OL);
    }

    // ================================================================
    //  MAGE — chibi: big hat, blue+gold
    // ================================================================
    void DrawMage(int dir, int frame)
    {
        Init(CW, CH);
        int cx = CW / 2;
        int lo = (frame == 1) ? -1 : (frame == 3) ? 1 : 0;

        Color skin = C(0.88f, 0.72f, 0.56f), skinDk = C(0.68f, 0.52f, 0.40f);
        Color robe = C(0.20f, 0.30f, 0.70f), robeDk = C(0.12f, 0.18f, 0.44f);
        Color hat = C(0.18f, 0.14f, 0.50f), hatDk = C(0.10f, 0.08f, 0.32f);
        Color orb = C(0.42f, 0.66f, 1f), star = C(1f, 0.90f, 0.30f);
        Color beard = C(0.78f, 0.74f, 0.70f);
        Color boot = C(0.26f, 0.15f, 0.07f);

        // === Hat (big pointed) ===
        P(cx, 1, hat);
        R(cx - 1, 2, 3, 1, hat);
        R(cx - 2, 3, 5, 1, hat);
        R(cx - 3, 4, 7, 2, hat);
        R(cx - 3, 4, 2, 2, hatDk); // shadow
        R(cx + 2, 4, 2, 2, C(0.24f, 0.20f, 0.58f)); // highlight
        // Brim
        R(cx - 5, 6, 11, 2, hatDk);
        R(cx - 4, 6, 9, 1, hat);
        // Star
        P(cx, 4, star);

        // === Face ===
        if (dir == 0)
        {
            R(cx - 3, 8, 7, 4, skin);
            R(cx - 3, 8, 7, 1, C(0.95f, 0.82f, 0.65f));
            P(cx - 2, 9, C(0.05f, 0.05f, 0.06f)); P(cx + 2, 9, C(0.05f, 0.05f, 0.06f));
            R(cx - 3, 12, 7, 2, beard); // beard
            R(cx - 2, 12, 5, 1, C(0.88f, 0.84f, 0.80f)); // beard highlight
        }
        else if (dir == 2)
        {
            R(cx - 3, 8, 7, 4, hat);
            R(cx - 2, 8, 5, 1, C(0.24f, 0.20f, 0.58f));
        }
        else
        {
            R(cx - 3, 8, 7, 4, skin);
            P(cx + (dir == 1 ? -2 : 1), 9, C(0.05f, 0.05f, 0.06f));
            R(cx - 2, 12, 5, 2, beard);
        }

        // === Robe (short body, flares at bottom) ===
        R(cx - 4, 13, 9, 5, robe);
        R(cx - 4, 13, 9, 1, C(0.30f, 0.42f, 0.84f)); // highlight
        R(cx - 4, 18, 9, 1, robeDk);
        R(cx - 5, 17, 11, 6, robe); // flare
        R(cx - 5, 17, 1, 6, robeDk);
        R(cx + 5, 17, 1, 6, robeDk);
        R(cx - 5, 22, 11, 1, robeDk); // hem
        // Gold trim
        R(cx - 5, 22, 11, 1, star);
        // Runes
        P(cx - 3, 19, star); P(cx + 3, 19, orb);

        // === Arms ===
        R(cx - 6, 14, 2, 5, robe);
        R(cx - 6, 14, 1, 5, robeDk);
        R(cx + 5, 14, 2, 5, robe);
        R(cx + 6, 14, 1, 5, robeDk);

        // === Staff ===
        if (dir != 2)
        {
            R(cx + 7, 8, 1, 14, C(0.44f, 0.30f, 0.18f)); // shaft
            P(cx + 7, 7, orb); P(cx + 6, 6, C(0.62f, 0.86f, 1f)); // orb
        }

        // === Boots ===
        R(cx - 3 + lo, 23, 3, 3, boot);
        R(cx + 0 - lo, 23, 3, 3, boot);

        Save($"Assets/Resources/Sprites/Character/Mage_{DirName(dir)}_{frame}.png", OL);
    }

    // ================================================================
    //  PRIEST — chibi: halo, white+gold
    // ================================================================
    void DrawPriest(int dir, int frame)
    {
        Init(CW, CH);
        int cx = CW / 2;
        int lo = (frame == 1) ? -1 : (frame == 3) ? 1 : 0;

        Color skin = C(0.88f, 0.72f, 0.56f), skinDk = C(0.68f, 0.52f, 0.40f);
        Color vest = C(0.92f, 0.90f, 0.84f), vestDk = C(0.74f, 0.72f, 0.66f);
        Color halo = C(1f, 0.92f, 0.40f);
        Color hair = C(0.78f, 0.70f, 0.38f);
        Color gold = C(0.84f, 0.68f, 0.16f), goldDk = C(0.60f, 0.48f, 0.10f);
        Color boot = C(0.26f, 0.15f, 0.07f);

        // === Halo ===
        R(cx - 4, 4, 9, 1, halo);
        P(cx - 5, 4, new Color(halo.r, halo.g, halo.b, 0.4f));
        P(cx + 5, 4, new Color(halo.r, halo.g, halo.b, 0.4f));

        // === Hair ===
        R(cx - 4, 5, 9, 3, hair);
        R(cx - 4, 5, 9, 1, C(0.90f, 0.82f, 0.50f));

        // === Face ===
        if (dir == 0)
        {
            R(cx - 3, 8, 7, 4, skin);
            R(cx - 3, 8, 7, 1, C(0.95f, 0.82f, 0.65f));
            P(cx - 2, 9, C(0.05f, 0.05f, 0.06f)); P(cx + 2, 9, C(0.05f, 0.05f, 0.06f));
            R(cx - 1, 11, 3, 1, skinDk);
        }
        else if (dir == 2)
        {
            R(cx - 4, 8, 9, 4, hair);
            R(cx - 3, 8, 7, 1, C(0.90f, 0.82f, 0.50f));
        }
        else
        {
            R(cx - 3, 8, 7, 4, skin);
            P(cx + (dir == 1 ? -2 : 1), 9, C(0.05f, 0.05f, 0.06f));
        }

        // === Robe ===
        R(cx - 4, 12, 9, 5, vest);
        R(cx - 4, 12, 9, 1, C(1f, 0.98f, 0.94f));
        R(cx - 4, 16, 9, 1, vestDk);
        R(cx - 5, 16, 11, 6, vest); // flare
        R(cx - 5, 16, 1, 6, vestDk);
        R(cx + 5, 16, 1, 6, vestDk);

        // === Gold cross ===
        R(cx - 1, 13, 3, 5, gold);
        R(cx - 3, 15, 7, 1, gold);
        R(cx - 1, 13, 1, 5, goldDk);

        // === Gold trim ===
        R(cx - 5, 21, 11, 1, gold);

        // === Arms ===
        R(cx - 6, 13, 2, 5, vest);
        R(cx - 6, 13, 1, 5, vestDk);
        R(cx + 5, 13, 2, 5, vest);
        R(cx + 6, 13, 1, 5, vestDk);

        // === Boots ===
        R(cx - 3 + lo, 22, 3, 3, boot);
        R(cx + 0 - lo, 22, 3, 3, boot);

        Save($"Assets/Resources/Sprites/Character/Priest_{DirName(dir)}_{frame}.png", OL);
    }

    // ================================================================
    //  ENEMIES (chibi style)
    // ================================================================
    void DrawGoblin()
    {
        Init(EW, EH);
        int cx = EW / 2;
        Color gSkin = C(0.32f, 0.62f, 0.26f), gDk = C(0.18f, 0.40f, 0.14f);
        Color gHi = C(0.44f, 0.76f, 0.36f);
        Color eye = C(1f, 0.22f, 0.08f);
        Color tooth = C(0.92f, 0.90f, 0.78f);
        Color loin = C(0.52f, 0.20f, 0.14f);
        Color boot = C(0.26f, 0.15f, 0.07f);
        Color club = C(0.36f, 0.24f, 0.14f);

        // Head (big)
        R(cx - 4, 5, 9, 7, gSkin);
        R(cx - 4, 5, 9, 1, gHi);
        R(cx - 4, 11, 9, 1, gDk);
        R(cx - 4, 5, 1, 7, gDk);
        // Ears
        P(cx - 5, 7, gSkin); P(cx - 5, 8, gHi);
        P(cx + 5, 7, gSkin); P(cx + 5, 8, gHi);
        // Eyes
        R(cx - 3, 7, 3, 2, eye); R(cx + 1, 7, 3, 2, eye);
        P(cx - 3, 7, C(1f, 0.5f, 0.2f)); P(cx + 1, 7, C(1f, 0.5f, 0.2f));
        // Mouth + fangs
        R(cx - 3, 10, 7, 1, gDk);
        P(cx - 2, 10, tooth); P(cx + 2, 10, tooth);

        // Body (short)
        R(cx - 4, 12, 9, 6, gDk);
        R(cx - 4, 12, 9, 1, gSkin);
        // Loincloth
        R(cx - 3, 15, 7, 4, loin);

        // Arms
        R(cx - 6, 13, 2, 4, gSkin);
        R(cx + 5, 13, 2, 4, gSkin);

        // Club
        R(cx + 7, 10, 2, 7, club);

        // Legs (short)
        R(cx - 3, 18, 3, 4, gDk);
        R(cx + 0, 18, 3, 4, gDk);

        // Feet
        R(cx - 3, 22, 3, 3, boot);
        R(cx + 0, 22, 3, 3, boot);

        Save("Assets/Resources/Sprites/Enemy/Melee_Goblin.png", C(0.04f, 0.12f, 0.04f));
    }

    void DrawRangedMage()
    {
        Init(EW, EH);
        int cx = EW / 2;
        Color robe = C(0.36f, 0.12f, 0.58f), robeDk = C(0.20f, 0.06f, 0.36f);
        Color hood = C(0.22f, 0.06f, 0.42f), hoodDk = C(0.12f, 0.04f, 0.28f);
        Color eye = C(1f, 0.30f, 1f);
        Color orb = C(0.65f, 0.25f, 1f);
        Color boot = C(0.26f, 0.15f, 0.07f);

        // Hood (pointed)
        P(cx, 2, hood);
        R(cx - 1, 3, 3, 1, hood);
        R(cx - 2, 4, 5, 2, hood);
        R(cx - 3, 6, 7, 3, hood);
        R(cx - 3, 6, 2, 3, hoodDk);
        R(cx - 4, 8, 9, 2, hoodDk); // brim

        // Face (shadowed)
        R(cx - 3, 10, 7, 4, C(0.15f, 0.10f, 0.15f));
        R(cx - 3, 11, 3, 2, eye); R(cx + 1, 11, 3, 2, eye);

        // Robe
        R(cx - 4, 13, 9, 5, robe);
        R(cx - 4, 13, 9, 1, C(0.50f, 0.22f, 0.74f));
        R(cx - 5, 17, 11, 6, robe); // flare
        R(cx - 5, 17, 1, 6, robeDk);
        R(cx + 5, 17, 1, 6, robeDk);
        // Runes
        P(cx - 3, 19, orb); P(cx + 3, 19, orb);

        // Arms
        R(cx - 6, 14, 2, 4, robeDk);
        R(cx + 5, 14, 2, 4, robeDk);

        // Staff
        R(cx + 7, 6, 1, 14, C(0.44f, 0.30f, 0.18f));
        P(cx + 7, 5, orb); P(cx + 6, 4, C(0.82f, 0.48f, 1f));

        // Boots
        R(cx - 3, 23, 3, 3, boot);
        R(cx + 0, 23, 3, 3, boot);

        Save("Assets/Resources/Sprites/Enemy/Ranged_Mage.png", C(0.04f, 0.02f, 0.10f));
    }

    void DrawEliteKnight()
    {
        Init(EW, EH);
        int cx = EW / 2;
        Color armor = C(0.28f, 0.28f, 0.36f), armorDk = C(0.14f, 0.14f, 0.20f);
        Color armorHi = C(0.44f, 0.44f, 0.54f);
        Color cape = C(0.55f, 0.10f, 0.10f);
        Color eye = C(1f, 0.22f, 0.08f);
        Color sword = C(0.78f, 0.80f, 0.88f);
        Color boot = C(0.14f, 0.07f, 0.03f);

        // Cape (behind)
        R(cx - 5, 6, 2, 14, cape);
        R(cx + 4, 6, 2, 14, cape);

        // Helmet
        R(cx - 4, 5, 9, 5, armor);
        R(cx - 3, 4, 7, 1, armor);
        R(cx - 4, 5, 9, 1, armorHi);
        R(cx - 4, 9, 9, 1, armorDk);
        // Spikes
        P(cx - 2, 3, armorHi); P(cx, 2, armorHi); P(cx + 2, 3, armorHi);

        // Face (dark visor)
        R(cx - 3, 10, 7, 3, armor);
        R(cx - 3, 11, 3, 1, eye); R(cx + 1, 11, 3, 1, eye);

        // Body
        R(cx - 4, 12, 9, 7, armor);
        R(cx - 4, 12, 9, 1, armorHi);
        R(cx - 4, 18, 9, 1, armorDk);
        // Gem
        R(cx - 1, 14, 3, 3, cape);
        P(cx, 15, eye);

        // Belt
        R(cx - 4, 18, 9, 1, armorDk);

        // Arms
        R(cx - 6, 13, 2, 5, armor);
        R(cx + 5, 13, 2, 5, armor);

        // Sword
        R(cx + 7, 6, 1, 12, sword);

        // Legs
        R(cx - 3, 19, 3, 5, armor);
        R(cx + 0, 19, 3, 5, armor);

        // Boots
        R(cx - 3, 24, 3, 3, boot);
        R(cx + 0, 24, 3, 3, boot);

        Save("Assets/Resources/Sprites/Enemy/Elite_Knight.png", C(0.03f, 0.03f, 0.06f));
    }

    // ================================================================
    //  WEAPON ARC
    // ================================================================
    void DrawWeaponArc(string className)
    {
        Init(AW, AW);
        Color arc = className switch { "Warrior" => C(0.85f, 0.85f, 0.92f, 0.8f), "Mage" => C(0.40f, 0.60f, 1f, 0.8f), _ => C(1f, 0.95f, 0.50f, 0.8f) };
        Color edge = className switch { "Warrior" => C(0.50f, 0.52f, 0.60f, 0.6f), "Mage" => C(0.20f, 0.35f, 0.70f, 0.6f), _ => C(0.65f, 0.55f, 0.15f, 0.6f) };
        int c = AW / 2;
        for (int y = 0; y < AW; y++)
            for (int x = 0; x < AW; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = Mathf.Atan2(y - c, x - c) * Mathf.Rad2Deg;
                if (d > 9 && d < 16 && a > -170 && a < -10)
                { float t = Mathf.InverseLerp(9, 16, d); float al = (1f - Mathf.Abs(t - 0.5f) * 2f) * 0.8f; P(x, y, new Color(arc.r, arc.g, arc.b, al)); }
                if (d > 8 && d < 9 && a > -170 && a < -10) P(x, y, edge);
            }
        string path = $"Assets/Resources/Sprites/Character/{className}_WeaponArc.png";
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels(px); tex.filterMode = FilterMode.Point; tex.Apply();
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null) { imp.filterMode = FilterMode.Point; imp.textureCompression = TextureImporterCompression.Uncompressed; imp.mipmapEnabled = false; imp.spritePixelsPerUnit = 32f; imp.SaveAndReimport(); }
    }

    // ================================================================
    //  BOSSES (chibi, 48×56)
    // ================================================================
    void DrawBoss(int stage, BossVisualConfig c)
    {
        Init(BW, BH);
        int cx = BW / 2;
        Color body = c.Body, dark = c.Dark, light = c.Light, glow = c.Glow;
        Color outline = C(0.02f, 0.01f, 0.01f);

        switch (stage)
        {
            case 0: // Treant
                R(cx - 6, 8, 13, 30, body);
                R(cx - 6, 8, 13, 2, light * 0.7f);
                R(cx - 6, 36, 13, 2, dark);
                R(cx - 2, 14, 2, 22, dark);
                // Canopy
                R(cx - 10, 8, 21, 12, body);
                R(cx - 8, 6, 17, 4, light * 0.7f);
                // Eyes
                R(cx - 4, 22, 4, 3, glow); R(cx + 1, 22, 4, 3, glow);
                // Roots
                R(cx - 6, 38, 5, 6, dark); R(cx + 1, 38, 5, 6, dark);
                break;
            case 1: // Troll
                R(cx - 8, 14, 17, 22, body);
                R(cx - 8, 14, 17, 2, light * 0.7f);
                // Head
                R(cx - 5, 6, 11, 8, body * 1.1f);
                R(cx - 5, 6, 11, 1, light);
                // Horns
                R(cx - 6, 3, 3, 5, light); R(cx + 4, 3, 3, 5, light);
                // Eyes
                R(cx - 3, 9, 3, 2, glow); R(cx + 1, 9, 3, 2, glow);
                // Legs
                R(cx - 6, 36, 5, 10, dark); R(cx + 1, 36, 5, 10, dark);
                break;
            case 2: // Banshee
                R(cx - 4, 10, 9, 28, body);
                R(cx - 4, 10, 9, 2, light * 0.7f);
                // Halo
                R(cx - 6, 8, 13, 2, glow);
                // Face
                R(cx - 3, 14, 3, 4, glow); R(cx + 1, 14, 3, 4, glow);
                // Robe flare
                R(cx - 7, 30, 15, 8, body * 0.85f);
                break;
            case 3: // Flame Warlord
                R(cx - 6, 12, 13, 24, body);
                R(cx - 6, 12, 13, 2, light * 0.7f);
                // Helmet
                R(cx - 5, 6, 11, 6, dark);
                // Flame crown
                R(cx - 5, 2, 3, 4, glow); R(cx - 1, 1, 3, 5, glow); R(cx + 3, 2, 3, 4, glow);
                // Eyes
                R(cx - 3, 8, 3, 2, glow); R(cx + 1, 8, 3, 2, glow);
                // Legs
                R(cx - 5, 36, 4, 8, dark); R(cx + 1, 36, 4, 8, dark);
                break;
            case 4: // Undead Lord
                R(cx - 6, 10, 13, 28, body);
                R(cx - 6, 10, 13, 2, dark);
                // Skull
                R(cx - 4, 4, 9, 7, C(0.82f, 0.79f, 0.72f));
                R(cx - 4, 4, 9, 1, C(0.92f, 0.89f, 0.82f));
                // Eye sockets
                R(cx - 3, 6, 3, 3, glow); R(cx + 1, 6, 3, 3, glow);
                // Bone crown
                R(cx - 4, 2, 9, 2, C(0.7f, 0.67f, 0.6f));
                break;
            case 5: // Dragon
                R(cx - 7, 14, 15, 18, body);
                R(cx - 7, 14, 15, 2, light * 0.7f);
                // Head
                R(cx - 5, 6, 11, 8, body * 1.1f);
                // Snout
                R(cx - 3, 4, 7, 3, dark);
                // Horns
                R(cx - 4, 2, 2, 5, light); R(cx + 3, 2, 2, 5, light);
                // Eyes
                R(cx - 3, 8, 3, 2, glow); R(cx + 1, 8, 3, 2, glow);
                // Wings
                R(cx - 12, 16, 6, 12, light * 0.5f);
                R(cx + 7, 16, 6, 12, light * 0.5f);
                // Tail
                R(cx - 1, 32, 3, 8, body * 0.9f);
                break;
            case 6: // Void Herald
                Ellipse(cx, 24, 10, 9, body);
                Ellipse(cx - 3, 20, 6, 5, light * 0.5f);
                // Rune ring
                R(cx - 12, 24, 25, 3, glow);
                // Tendrils
                for (int i = 0; i < 5; i++) { int tx = cx - 6 + i * 3; R(tx, 34, 2, 8, light * 0.3f); }
                // Runes
                for (int i = 0; i < 4; i++) { float a = i * 1.57f; P(cx + (int)(Mathf.Cos(a) * 12), 24 + (int)(Mathf.Sin(a) * 8), glow); }
                break;
            case 7: // Abyss King
                R(cx - 8, 10, 17, 28, body);
                R(cx - 8, 10, 17, 2, light * 0.7f);
                // Horns
                for (int y = 0; y < 8; y++) { int hx = 4 - y / 2; P(cx - 9 - hx, 10 - y + 8, light); P(cx + 9 + hx, 10 - y + 8, light); }
                // Crown
                R(cx - 4, 6, 9, 2, C(0.82f, 0.66f, 0.16f));
                // Eyes
                R(cx - 3, 14, 3, 2, glow); R(cx + 1, 14, 3, 2, glow);
                // Chest
                R(cx - 4, 18, 9, 8, light);
                R(cx - 2, 20, 5, 4, glow);
                // Wings
                R(cx - 14, 12, 7, 18, dark * 1.3f);
                R(cx + 8, 12, 7, 18, dark * 1.3f);
                // Legs
                R(cx - 6, 38, 5, 8, dark); R(cx + 1, 38, 5, 8, dark);
                break;
        }

        Save($"Assets/Resources/Sprites/Enemy/Boss_{stage}.png", outline);
    }

    void Ellipse(int cx, int cy, int rx, int ry, Color c)
    {
        for (int y = -ry; y <= ry; y++)
            for (int x = -rx; x <= rx; x++)
                if (x * x * ry * ry + y * y * rx * rx <= rx * rx * ry * ry)
                    P(cx + x, cy + y, c);
    }

    static string DirName(int dir) => dir switch { 0 => "Down", 1 => "Left", 2 => "Up", 3 => "Right", _ => "Down" };

    public void GenerateAll()
    {
        for (int dir = 0; dir < 4; dir++)
            for (int frame = 0; frame < 4; frame++)
            {
                DrawWarrior(dir, frame);
                DrawMage(dir, frame);
                DrawPriest(dir, frame);
            }

        DrawWeaponArc("Warrior");
        DrawWeaponArc("Mage");
        DrawWeaponArc("Priest");

        DrawGoblin();
        DrawRangedMage();
        DrawEliteKnight();

        var configs = BossVisualConfig.Configs;
        for (int i = 0; i < configs.Length; i++)
            DrawBoss(i, configs[i]);

        AssetDatabase.Refresh();
        Debug.Log("Chibi sprites generated! Style: Deltarune-like overworld (24x32 chars, 48x56 bosses)");
    }
}
