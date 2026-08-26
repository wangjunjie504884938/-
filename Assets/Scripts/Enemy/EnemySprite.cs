using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Per-stage boss visual config with rich color palette for layered rendering.
/// </summary>
public class BossVisualConfig
{
    public string Name;
    public Color Body;       // main body fill
    public Color Dark;       // darker shade for depth / outline
    public Color Light;      // highlight / accent
    public Color Glow;       // emissive glow color
    public float Scale;

    public static readonly BossVisualConfig[] Configs = new BossVisualConfig[]
    {
        // 0: 树精长老 — forest spirit
        new BossVisualConfig { Name = "树精长老",
            Body = new Color(0.22f, 0.55f, 0.18f), Dark = new Color(0.12f, 0.32f, 0.08f),
            Light = new Color(0.55f, 0.35f, 0.12f), Glow = new Color(0.3f, 0.9f, 0.2f, 0.6f), Scale = 3f },
        // 1: 矿洞巨魔 — cave brute
        new BossVisualConfig { Name = "矿洞巨魔",
            Body = new Color(0.42f, 0.33f, 0.22f), Dark = new Color(0.25f, 0.18f, 0.1f),
            Light = new Color(0.65f, 0.5f, 0.28f), Glow = new Color(0.8f, 0.6f, 0.2f, 0.5f), Scale = 3.5f },
        // 2: 冰霜女妖 — frost banshee
        new BossVisualConfig { Name = "冰霜女妖",
            Body = new Color(0.55f, 0.78f, 0.92f), Dark = new Color(0.3f, 0.45f, 0.65f),
            Light = new Color(0.85f, 0.95f, 1f), Glow = new Color(0.5f, 0.8f, 1f, 0.7f), Scale = 2.8f },
        // 3: 炎魔将军 — flame warlord
        new BossVisualConfig { Name = "炎魔将军",
            Body = new Color(0.75f, 0.18f, 0.05f), Dark = new Color(0.4f, 0.08f, 0.02f),
            Light = new Color(1f, 0.65f, 0.1f), Glow = new Color(1f, 0.4f, 0.05f, 0.7f), Scale = 3.2f },
        // 4: 亡灵领主 — undead lord
        new BossVisualConfig { Name = "亡灵领主",
            Body = new Color(0.3f, 0.22f, 0.45f), Dark = new Color(0.15f, 0.1f, 0.25f),
            Light = new Color(0.15f, 0.85f, 0.3f), Glow = new Color(0.1f, 1f, 0.3f, 0.6f), Scale = 3f },
        // 5: 远古幼龙 — ancient whelp
        new BossVisualConfig { Name = "远古幼龙",
            Body = new Color(0.28f, 0.42f, 0.6f), Dark = new Color(0.15f, 0.22f, 0.35f),
            Light = new Color(0.7f, 0.78f, 0.2f), Glow = new Color(0.5f, 0.8f, 1f, 0.5f), Scale = 3.5f },
        // 6: 虚空使者 — void herald
        new BossVisualConfig { Name = "虚空使者",
            Body = new Color(0.18f, 0.05f, 0.3f), Dark = new Color(0.08f, 0.02f, 0.15f),
            Light = new Color(0.65f, 0.2f, 0.9f), Glow = new Color(0.75f, 0.3f, 1f, 0.7f), Scale = 3f },
        // 7: 魔王·深渊 — abyss demon king
        new BossVisualConfig { Name = "魔王·深渊",
            Body = new Color(0.18f, 0.02f, 0.22f), Dark = new Color(0.08f, 0f, 0.1f),
            Light = new Color(0.9f, 0.12f, 0.12f), Glow = new Color(1f, 0.15f, 0.15f, 0.8f), Scale = 4f },
    };

    public static BossVisualConfig Get(int stageIndex) => Configs[Mathf.Clamp(stageIndex, 0, Configs.Length - 1)];
}

/// <summary>
/// Idle bob + glow pulse animation for boss visuals.
/// </summary>
public class BossVisualAnimator : MonoBehaviour
{
    private float baseY;
    private float phaseOffset;
    private Transform glowObj;
    private SpriteRenderer glowSr;
    private Color glowBase;

    public void Init(Transform glowTransform, Color glowColor)
    {
        baseY = transform.localPosition.y;
        phaseOffset = Random.Range(0f, 6.28f);
        glowObj = glowTransform;
        glowSr = glowTransform.GetComponent<SpriteRenderer>();
        glowBase = glowColor;
    }

    private void Update()
    {
        float t = Time.time + phaseOffset;
        // Gentle float bob
        Vector3 p = transform.localPosition;
        p.y = baseY + Mathf.Sin(t * 1.5f) * 0.15f;
        transform.localPosition = p;
        // Glow pulse
        if (glowSr != null)
        {
            Color c = glowBase;
            c.a = glowBase.a * (0.6f + 0.4f * Mathf.Sin(t * 2f));
            glowSr.color = c;
        }
    }
}

public static class EnemySprite
{
    public static void ApplySprite(GameObject enemyObj, EnemyType type, int stageIndex = 0)
    {
        var mainSr = enemyObj.GetComponent<SpriteRenderer>();
        if (mainSr != null) mainSr.color = Color.clear;

        string assetName = type switch
        {
            EnemyType.Melee => "Melee_Goblin",
            EnemyType.Ranged => "Ranged_Mage",
            EnemyType.Elite => "Elite_Knight",
            EnemyType.Boss => $"Boss_{stageIndex}",
            EnemyType.Bomber => "Melee_Goblin",
            EnemyType.Charger => "Melee_Goblin",
            EnemyType.Healer => "Ranged_Mage",
            EnemyType.Shielder => "Melee_Goblin",
            _ => "Melee_Goblin"
        };

        Sprite loaded = Resources.Load<Sprite>(ResourcePaths.EnemyBase + assetName);

        float scale = type switch
        {
            EnemyType.Melee => 1.5f,
            EnemyType.Ranged => 1.5f,
            EnemyType.Elite => 1.8f,
            EnemyType.Boss => 3.0f,
            EnemyType.Bomber => 1.3f,
            EnemyType.Charger => 1.6f,
            EnemyType.Healer => 1.4f,
            EnemyType.Shielder => 1.5f,
            _ => 1.5f
        };

        if (type == EnemyType.Boss)
        {
            var cfg = BossVisualConfig.Get(stageIndex);
            if (loaded == null) loaded = SpriteCache.WhitePixel;
            BuildBossVisual(enemyObj, loaded, cfg);
            return;
        }

        if (loaded == null) loaded = SpriteCache.WhitePixel;

        GameObject child = new GameObject("EnemyVisual");
        child.transform.SetParent(enemyObj.transform, false);
        child.transform.localPosition = Vector3.zero;

        var sr = child.AddComponent<SpriteRenderer>();
        sr.sprite = loaded;
        sr.sortingOrder = 1;

        // Apply type-specific tint for special enemies
        Color tint = type switch
        {
            EnemyType.Bomber => new Color(1f, 0.5f, 0.1f),
            EnemyType.Charger => new Color(0.9f, 0.3f, 0.3f),
            EnemyType.Healer => new Color(0.3f, 0.9f, 0.5f),
            EnemyType.Shielder => new Color(0.3f, 0.5f, 1f),
            _ => Color.white
        };
        if (tint != Color.white) sr.color = tint;

        child.transform.localScale = new Vector3(scale, scale, 1f);
    }

    // ─── Boss builders ────────────────────────────────────────────────

    private static void BuildBossVisual(GameObject enemyObj, Sprite baseSprite, BossVisualConfig c)
    {
        float s = c.Scale;
        GameObject root = NewChild(enemyObj, "BossVisual");

        // Shadow (elliptical, soft)
        var shadow = AddPart(root, "Shadow", new Vector3(0, -s * 0.42f, 0),
            new Vector3(s * 0.65f, s * 0.14f, 1), new Color(0, 0, 0, 0.2f), -1);

        // Outer glow (large, soft, behind body)
        Transform glowT = AddPart(root, "Glow", Vector3.zero,
            new Vector3(s * 1.4f, s * 1.4f, 1), c.Glow, -1).transform;

        // Base sprite layer (the generated boss sprite as main body)
        if (baseSprite != null && baseSprite != SpriteCache.WhitePixel)
        {
            GameObject baseObj = new GameObject("BossBase");
            baseObj.transform.SetParent(root.transform, false);
            baseObj.transform.localPosition = Vector3.zero;
            var baseSr = baseObj.AddComponent<SpriteRenderer>();
            baseSr.sprite = baseSprite;
            baseSr.color = Color.white;
            baseSr.sortingOrder = 1;
            // Scale the 48×56 sprite to match the boss scale
            float baseScale = s / 2.5f;
            baseObj.transform.localScale = new Vector3(baseScale, baseScale, 1f);
        }

        // Glowing eyes (shared by all bosses)
        float eyeY = s * 0.22f;
        float eyeSpacing = s * 0.12f;
        AddPart(root, "EyeL", new Vector3(-eyeSpacing, eyeY, 0),
            new Vector3(s * 0.07f, s * 0.07f, 1), Color.white, 4);
        AddPart(root, "EyeR", new Vector3(eyeSpacing, eyeY, 0),
            new Vector3(s * 0.07f, s * 0.07f, 1), Color.white, 4);
        // Eye glow
        AddPart(root, "EyeGlowL", new Vector3(-eyeSpacing, eyeY, 0),
            new Vector3(s * 0.15f, s * 0.15f, 1), c.Glow, 3);
        AddPart(root, "EyeGlowR", new Vector3(eyeSpacing, eyeY, 0),
            new Vector3(s * 0.15f, s * 0.15f, 1), c.Glow, 3);

        // Idle bob animator
        var anim = root.AddComponent<BossVisualAnimator>();
        anim.Init(glowT, c.Glow);
    }

    private static void BuildByShape(GameObject root, BossVisualConfig c, float s)
    {
        string n = c.Name;
        if (n == "树精长老") BuildTreant(root, c, s);
        else if (n == "矿洞巨魔") BuildTroll(root, c, s);
        else if (n == "冰霜女妖") BuildBanshee(root, c, s);
        else if (n == "炎魔将军") BuildFlameWarlord(root, c, s);
        else if (n == "亡灵领主") BuildUndeadLord(root, c, s);
        else if (n == "远古幼龙") BuildDragon(root, c, s);
        else if (n == "虚空使者") BuildVoidHerald(root, c, s);
        else if (n == "魔王·深渊") BuildAbyssKing(root, c, s);
        else BuildHumanoid(root, c, s);
    }

    // ─── 树精长老: thick trunk + canopy + roots ──────────────
    private static void BuildTreant(GameObject root, BossVisualConfig c, float s)
    {
        // Trunk (tall oval)
        AddPart(root, "Trunk", new Vector3(0, 0, 0),
            new Vector3(s * 0.5f, s * 0.75f, 1), c.Body, 2);
        // Trunk dark edge
        AddPart(root, "TrunkEdge", new Vector3(0, 0, 0),
            new Vector3(s * 0.54f, s * 0.79f, 1), c.Dark, 1);
        // Canopy (wide circle on top)
        AddPart(root, "Canopy", new Vector3(0, s * 0.45f, 0),
            new Vector3(s * 0.7f, s * 0.5f, 1), c.Body * 1.15f, 2);
        // Canopy highlight
        AddPart(root, "CanopyHi", new Vector3(0, s * 0.5f, 0),
            new Vector3(s * 0.45f, s * 0.3f, 1), c.Light, 3);
        // Branch arms
        AddPart(root, "LBranch", new Vector3(-s * 0.32f, s * 0.15f, 0),
            new Vector3(s * 0.28f, s * 0.1f, 1), c.Dark, 2);
        AddPart(root, "RBranch", new Vector3(s * 0.32f, s * 0.15f, 0),
            new Vector3(s * 0.28f, s * 0.1f, 1), c.Dark, 2);
        // Root feet
        AddPart(root, "LRoot", new Vector3(-s * 0.18f, -s * 0.38f, 0),
            new Vector3(s * 0.16f, s * 0.08f, 1), c.Dark, 2);
        AddPart(root, "RRoot", new Vector3(s * 0.18f, -s * 0.38f, 0),
            new Vector3(s * 0.16f, s * 0.08f, 1), c.Dark, 2);
        // Vine details
        AddPart(root, "Vine1", new Vector3(-s * 0.1f, -s * 0.15f, 0),
            new Vector3(s * 0.04f, s * 0.25f, 1), c.Light, 3);
        AddPart(root, "Vine2", new Vector3(s * 0.12f, -s * 0.1f, 0),
            new Vector3(s * 0.03f, s * 0.2f, 1), c.Light, 3);
    }

    // ─── 矿洞巨魔: hulking body + horns + belly ──────────────
    private static void BuildTroll(GameObject root, BossVisualConfig c, float s)
    {
        // Body (wide squat)
        AddPart(root, "Body", new Vector3(0, -s * 0.05f, 0),
            new Vector3(s * 0.65f, s * 0.55f, 1), c.Body, 2);
        // Dark outline
        AddPart(root, "BodyEdge", new Vector3(0, -s * 0.05f, 0),
            new Vector3(s * 0.69f, s * 0.59f, 1), c.Dark, 1);
        // Head (small on top)
        AddPart(root, "Head", new Vector3(0, s * 0.32f, 0),
            new Vector3(s * 0.3f, s * 0.28f, 1), c.Body * 1.1f, 3);
        // Horns
        AddPart(root, "HornL", new Vector3(-s * 0.15f, s * 0.48f, 0),
            new Vector3(s * 0.08f, s * 0.22f, 1), c.Light, 4);
        AddPart(root, "HornR", new Vector3(s * 0.15f, s * 0.48f, 0),
            new Vector3(s * 0.08f, s * 0.22f, 1), c.Light, 4);
        // Belly
        AddPart(root, "Belly", new Vector3(0, -s * 0.18f, 0),
            new Vector3(s * 0.35f, s * 0.25f, 1), c.Light, 3);
        // Fists
        AddPart(root, "LFist", new Vector3(-s * 0.38f, -s * 0.15f, 0),
            new Vector3(s * 0.18f, s * 0.16f, 1), c.Dark, 3);
        AddPart(root, "RFist", new Vector3(s * 0.38f, -s * 0.15f, 0),
            new Vector3(s * 0.18f, s * 0.16f, 1), c.Dark, 3);
        // Belly scar
        AddPart(root, "Scar", new Vector3(s * 0.05f, -s * 0.12f, 0),
            new Vector3(s * 0.12f, s * 0.03f, 1), c.Dark * 1.5f, 4);
    }

    // ─── 冰霜女妖: slender floating + flowing robes + halo ──────
    private static void BuildBanshee(GameObject root, BossVisualConfig c, float s)
    {
        // Core body (slender oval)
        AddPart(root, "Body", new Vector3(0, s * 0.05f, 0),
            new Vector3(s * 0.3f, s * 0.55f, 1), c.Body, 2);
        // Dark edge
        AddPart(root, "BodyEdge", new Vector3(0, s * 0.05f, 0),
            new Vector3(s * 0.34f, s * 0.59f, 1), c.Dark, 1);
        // Flowing robe (wider at bottom)
        AddPart(root, "Robe", new Vector3(0, -s * 0.25f, 0),
            new Vector3(s * 0.5f, s * 0.35f, 1), c.Body * 0.85f, 2);
        // Robe shimmer
        AddPart(root, "RobeHi", new Vector3(-s * 0.08f, -s * 0.2f, 0),
            new Vector3(s * 0.2f, s * 0.15f, 1), c.Light, 3);
        // Halo
        AddPart(root, "Halo", new Vector3(0, s * 0.55f, 0),
            new Vector3(s * 0.35f, s * 0.1f, 1), c.Glow, 3);
        // Inner halo
        AddPart(root, "HaloInner", new Vector3(0, s * 0.55f, 0),
            new Vector3(s * 0.25f, s * 0.05f, 1), Color.white, 4);
        // Sleeves
        AddPart(root, "LSleeve", new Vector3(-s * 0.25f, -s * 0.05f, 0),
            new Vector3(s * 0.15f, s * 0.25f, 1), c.Dark, 2);
        AddPart(root, "RSleeve", new Vector3(s * 0.25f, -s * 0.05f, 0),
            new Vector3(s * 0.15f, s * 0.25f, 1), c.Dark, 2);
        // Frost particles (small dots)
        for (int i = 0; i < 4; i++)
        {
            float angle = i * 1.57f + 0.4f;
            AddPart(root, $"Frost{i}", new Vector3(Mathf.Cos(angle) * s * 0.4f, s * 0.1f + Mathf.Sin(angle) * s * 0.3f, 0),
                new Vector3(s * 0.06f, s * 0.06f, 1), c.Glow, 4);
        }
    }

    // ─── 炎魔将军: armored body + flame crown + pauldrons ────
    private static void BuildFlameWarlord(GameObject root, BossVisualConfig c, float s)
    {
        // Armored torso
        AddPart(root, "Torso", new Vector3(0, 0, 0),
            new Vector3(s * 0.45f, s * 0.6f, 1), c.Body, 2);
        // Dark edge
        AddPart(root, "TorsoEdge", new Vector3(0, 0, 0),
            new Vector3(s * 0.49f, s * 0.64f, 1), c.Dark, 1);
        // Helmet / head
        AddPart(root, "Helm", new Vector3(0, s * 0.38f, 0),
            new Vector3(s * 0.28f, s * 0.22f, 1), c.Dark, 3);
        // Flame crown
        AddPart(root, "Crown", new Vector3(0, s * 0.52f, 0),
            new Vector3(s * 0.3f, s * 0.15f, 1), c.Glow, 4);
        AddPart(root, "CrownTip", new Vector3(0, s * 0.6f, 0),
            new Vector3(s * 0.12f, s * 0.1f, 1), c.Light, 4);
        // Pauldrons
        AddPart(root, "LPauldron", new Vector3(-s * 0.32f, s * 0.2f, 0),
            new Vector3(s * 0.18f, s * 0.14f, 1), c.Light, 3);
        AddPart(root, "RPauldron", new Vector3(s * 0.32f, s * 0.2f, 0),
            new Vector3(s * 0.18f, s * 0.14f, 1), c.Light, 3);
        // Belt
        AddPart(root, "Belt", new Vector3(0, -s * 0.1f, 0),
            new Vector3(s * 0.35f, s * 0.06f, 1), c.Light, 3);
        // Greaves
        AddPart(root, "LGreave", new Vector3(-s * 0.13f, -s * 0.35f, 0),
            new Vector3(s * 0.12f, s * 0.2f, 1), c.Dark, 2);
        AddPart(root, "RGreave", new Vector3(s * 0.13f, -s * 0.35f, 0),
            new Vector3(s * 0.12f, s * 0.2f, 1), c.Dark, 2);
        // Chest emblem glow
        AddPart(root, "Emblem", new Vector3(0, s * 0.08f, 0),
            new Vector3(s * 0.1f, s * 0.1f, 1), c.Glow, 4);
    }

    // ─── 亡灵领主: skeletal frame + green soul fire ─────────
    private static void BuildUndeadLord(GameObject root, BossVisualConfig c, float s)
    {
        // Tattered cloak
        AddPart(root, "Cloak", new Vector3(0, -s * 0.05f, 0),
            new Vector3(s * 0.5f, s * 0.7f, 1), c.Body, 1);
        // Cloak dark edge
        AddPart(root, "CloakEdge", new Vector3(0, -s * 0.05f, 0),
            new Vector3(s * 0.54f, s * 0.74f, 1), c.Dark, 0);
        // Skeletal ribcage (visible through cloak)
        AddPart(root, "Ribs", new Vector3(0, s * 0.05f, 0),
            new Vector3(s * 0.22f, s * 0.3f, 1), new Color(0.75f, 0.72f, 0.65f), 2);
        // Skull
        AddPart(root, "Skull", new Vector3(0, s * 0.38f, 0),
            new Vector3(s * 0.2f, s * 0.2f, 1), new Color(0.8f, 0.77f, 0.7f), 3);
        // Skull shadow
        AddPart(root, "SkullShadow", new Vector3(0, s * 0.35f, 0),
            new Vector3(s * 0.22f, s * 0.15f, 1), c.Dark, 2);
        // Soul fire (green glow from within)
        AddPart(root, "SoulFire", new Vector3(0, s * 0.1f, 0),
            new Vector3(s * 0.18f, s * 0.22f, 1), c.Glow, 4);
        // Crown of bones
        AddPart(root, "BoneCrown", new Vector3(0, s * 0.5f, 0),
            new Vector3(s * 0.22f, s * 0.06f, 1), new Color(0.7f, 0.67f, 0.6f), 4);
        // Tattered edges
        AddPart(root, "TatterL", new Vector3(-s * 0.28f, -s * 0.3f, 0),
            new Vector3(s * 0.1f, s * 0.2f, 1), c.Dark * 0.8f, 2);
        AddPart(root, "TatterR", new Vector3(s * 0.28f, -s * 0.3f, 0),
            new Vector3(s * 0.1f, s * 0.2f, 1), c.Dark * 0.8f, 2);
    }

    // ─── 远古幼龙: dragon body + wings + tail + crest ─────────
    private static void BuildDragon(GameObject root, BossVisualConfig c, float s)
    {
        // Main body (elongated oval)
        AddPart(root, "Body", new Vector3(0, 0, 0),
            new Vector3(s * 0.55f, s * 0.4f, 1), c.Body, 2);
        // Body dark underbelly
        AddPart(root, "Belly", new Vector3(0, -s * 0.08f, 0),
            new Vector3(s * 0.4f, s * 0.2f, 1), c.Dark, 2);
        // Head (forward-tilted)
        AddPart(root, "Head", new Vector3(0, s * 0.28f, 0),
            new Vector3(s * 0.25f, s * 0.2f, 1), c.Body * 1.15f, 3);
        // Snout
        AddPart(root, "Snout", new Vector3(0, s * 0.2f, 0),
            new Vector3(s * 0.14f, s * 0.1f, 1), c.Dark, 4);
        // Crest / horns
        AddPart(root, "CrestL", new Vector3(-s * 0.1f, s * 0.4f, 0),
            new Vector3(s * 0.06f, s * 0.15f, 1), c.Light, 4);
        AddPart(root, "CrestR", new Vector3(s * 0.1f, s * 0.4f, 0),
            new Vector3(s * 0.06f, s * 0.15f, 1), c.Light, 4);
        // Wings (large translucent)
        AddPart(root, "WingL", new Vector3(-s * 0.45f, s * 0.12f, 0),
            new Vector3(s * 0.45f, s * 0.35f, 1), c.Light * 0.7f, 1);
        AddPart(root, "WingR", new Vector3(s * 0.45f, s * 0.12f, 0),
            new Vector3(s * 0.45f, s * 0.35f, 1), c.Light * 0.7f, 1);
        // Wing struts
        AddPart(root, "WingStrutL", new Vector3(-s * 0.35f, s * 0.15f, 0),
            new Vector3(s * 0.35f, s * 0.04f, 1), c.Dark, 2);
        AddPart(root, "WingStrutR", new Vector3(s * 0.35f, s * 0.15f, 0),
            new Vector3(s * 0.35f, s * 0.04f, 1), c.Dark, 2);
        // Tail
        AddPart(root, "Tail", new Vector3(0, -s * 0.4f, 0),
            new Vector3(s * 0.1f, s * 0.28f, 1), c.Body * 0.9f, 1);
        // Tail tip
        AddPart(root, "TailTip", new Vector3(0, -s * 0.52f, 0),
            new Vector3(s * 0.14f, s * 0.08f, 1), c.Light, 2);
        // Scale details
        AddPart(root, "Scale1", new Vector3(-s * 0.1f, s * 0.05f, 0),
            new Vector3(s * 0.08f, s * 0.06f, 1), c.Light, 3);
        AddPart(root, "Scale2", new Vector3(s * 0.1f, 0, 0),
            new Vector3(s * 0.08f, s * 0.06f, 1), c.Light, 3);
    }

    // ─── 虚空使者: floating orb + tendrils + rune ring ───────
    private static void BuildVoidHerald(GameObject root, BossVisualConfig c, float s)
    {
        // Core orb
        AddPart(root, "Orb", new Vector3(0, s * 0.05f, 0),
            new Vector3(s * 0.4f, s * 0.4f, 1), c.Body, 2);
        // Orb highlight
        AddPart(root, "OrbHi", new Vector3(-s * 0.06f, s * 0.12f, 0),
            new Vector3(s * 0.18f, s * 0.15f, 1), c.Light * 0.6f, 3);
        // Rune ring (horizontal ellipse around orb)
        AddPart(root, "RuneRing", new Vector3(0, s * 0.05f, 0),
            new Vector3(s * 0.6f, s * 0.12f, 1), c.Glow, 3);
        // Inner ring
        AddPart(root, "RuneInner", new Vector3(0, s * 0.05f, 0),
            new Vector3(s * 0.5f, s * 0.06f, 1), c.Light, 4);
        // Tendrils (4 dangling below)
        for (int i = 0; i < 4; i++)
        {
            float x = (i - 1.5f) * s * 0.12f;
            AddPart(root, $"Tendril{i}", new Vector3(x, -s * 0.2f - i * s * 0.06f, 0),
                new Vector3(s * 0.04f, s * 0.18f, 1), c.Light * 0.5f, 2);
        }
        // Floating runes (small dots)
        for (int i = 0; i < 3; i++)
        {
            float angle = i * 2.09f + 0.5f;
            AddPart(root, $"Rune{i}", new Vector3(Mathf.Cos(angle) * s * 0.32f, s * 0.05f + Mathf.Sin(angle) * s * 0.15f, 0),
                new Vector3(s * 0.06f, s * 0.06f, 1), c.Glow, 5);
        }
        // Shoulder wisps
        AddPart(root, "WispL", new Vector3(-s * 0.25f, s * 0.15f, 0),
            new Vector3(s * 0.12f, s * 0.12f, 1), c.Glow, 3);
        AddPart(root, "WispR", new Vector3(s * 0.25f, s * 0.15f, 0),
            new Vector3(s * 0.12f, s * 0.12f, 1), c.Glow, 3);
    }

    // ─── 魔王·深渊: massive dark form + crimson accents + wings ─
    private static void BuildAbyssKing(GameObject root, BossVisualConfig c, float s)
    {
        // Dark massive body
        AddPart(root, "Body", new Vector3(0, -s * 0.05f, 0),
            new Vector3(s * 0.6f, s * 0.7f, 1), c.Body, 2);
        // Body edge
        AddPart(root, "BodyEdge", new Vector3(0, -s * 0.05f, 0),
            new Vector3(s * 0.64f, s * 0.74f, 1), c.Dark, 1);
        // Chest plate (crimson)
        AddPart(root, "ChestPlate", new Vector3(0, s * 0.05f, 0),
            new Vector3(s * 0.3f, s * 0.25f, 1), c.Light, 3);
        // Chest core glow
        AddPart(root, "ChestGlow", new Vector3(0, s * 0.05f, 0),
            new Vector3(s * 0.12f, s * 0.12f, 1), c.Glow, 5);
        // Crown / horns
        AddPart(root, "CrownL", new Vector3(-s * 0.15f, s * 0.52f, 0),
            new Vector3(s * 0.07f, s * 0.2f, 1), c.Light, 4);
        AddPart(root, "CrownR", new Vector3(s * 0.15f, s * 0.52f, 0),
            new Vector3(s * 0.07f, s * 0.2f, 1), c.Light, 4);
        AddPart(root, "CrownMid", new Vector3(0, s * 0.55f, 0),
            new Vector3(s * 0.05f, s * 0.18f, 1), c.Glow, 4);
        // Demon wings (translucent dark)
        AddPart(root, "WingL", new Vector3(-s * 0.5f, s * 0.15f, 0),
            new Vector3(s * 0.4f, s * 0.45f, 1), c.Dark * 1.2f, 1);
        AddPart(root, "WingR", new Vector3(s * 0.5f, s * 0.15f, 0),
            new Vector3(s * 0.4f, s * 0.45f, 1), c.Dark * 1.2f, 1);
        // Wing membrane glow
        AddPart(root, "WingGlowL", new Vector3(-s * 0.42f, s * 0.12f, 0),
            new Vector3(s * 0.25f, s * 0.3f, 1), c.Glow * 0.4f, 2);
        AddPart(root, "WingGlowR", new Vector3(s * 0.42f, s * 0.12f, 0),
            new Vector3(s * 0.25f, s * 0.3f, 1), c.Glow * 0.4f, 2);
        // Shoulder spikes
        AddPart(root, "SpkL", new Vector3(-s * 0.34f, s * 0.25f, 0),
            new Vector3(s * 0.08f, s * 0.12f, 1), c.Light, 4);
        AddPart(root, "SpkR", new Vector3(s * 0.34f, s * 0.25f, 0),
            new Vector3(s * 0.08f, s * 0.12f, 1), c.Light, 4);
        // Greaves
        AddPart(root, "LGreave", new Vector3(-s * 0.15f, -s * 0.38f, 0),
            new Vector3(s * 0.13f, s * 0.22f, 1), c.Dark, 2);
        AddPart(root, "RGreave", new Vector3(s * 0.15f, -s * 0.38f, 0),
            new Vector3(s * 0.13f, s * 0.22f, 1), c.Dark, 2);
    }

    // ─── Fallback humanoid ──────────────────────────────────────────
    private static void BuildHumanoid(GameObject root, BossVisualConfig c, float s)
    {
        AddPart(root, "Body", Vector3.zero,
            new Vector3(s * 0.45f, s * 0.6f, 1), c.Body, 2);
        AddPart(root, "BodyEdge", Vector3.zero,
            new Vector3(s * 0.49f, s * 0.64f, 1), c.Dark, 1);
        AddPart(root, "Head", new Vector3(0, s * 0.38f, 0),
            new Vector3(s * 0.25f, s * 0.22f, 1), c.Body * 1.1f, 3);
        AddPart(root, "LArm", new Vector3(-s * 0.3f, s * 0.05f, 0),
            new Vector3(s * 0.12f, s * 0.35f, 1), c.Dark, 2);
        AddPart(root, "RArm", new Vector3(s * 0.3f, s * 0.05f, 0),
            new Vector3(s * 0.12f, s * 0.35f, 1), c.Dark, 2);
    }

    // ─── Helpers ────────────────────────────────────────────────────

    private static GameObject AddPart(GameObject parent, string name, Vector3 pos, Vector3 scale, Color color, int sortOrder)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent.transform, false);
        obj.transform.localPosition = pos;
        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteCache.WhitePixel;
        sr.color = color;
        sr.sortingOrder = sortOrder;
        obj.transform.localScale = scale;
        return obj;
    }

    private static GameObject NewChild(GameObject parent, string name)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent.transform, false);
        obj.transform.localPosition = Vector3.zero;
        return obj;
    }

    /// <summary>Add visual aura indicators for each affix.</summary>
    public static void ApplyAffixAuras(GameObject enemyObj, List<EliteAffix> affixes)
    {
        if (affixes == null || affixes.Count == 0) return;

        Color combined = Color.clear;
        foreach (var affix in affixes)
            combined += affix.AuraColor;
        combined /= affixes.Count;
        combined.a = 0.25f;

        GameObject auraObj = new GameObject("AffixAura");
        auraObj.transform.SetParent(enemyObj.transform, false);
        auraObj.transform.localPosition = Vector3.zero;

        var auraSr = auraObj.AddComponent<SpriteRenderer>();
        auraSr.sprite = SpriteCache.WhitePixel;
        auraSr.color = combined;
        auraSr.sortingOrder = 0;

        float auraScale = enemyObj.GetComponent<EnemyController>()?.Type == EnemyType.Boss ? 4f : 3f;
        auraObj.transform.localScale = new Vector3(auraScale, auraScale, 1f);

        float dotX = -0.3f * (affixes.Count - 1);
        for (int i = 0; i < affixes.Count; i++)
        {
            GameObject dotObj = new GameObject($"AffixDot_{affixes[i].Type}");
            dotObj.transform.SetParent(enemyObj.transform, false);
            float dotY = enemyObj.GetComponent<EnemyController>()?.Type == EnemyType.Boss ? 1.8f : 1.2f;
            dotObj.transform.localPosition = new Vector3(dotX + i * 0.6f, dotY, 0f);

            var dotSr = dotObj.AddComponent<SpriteRenderer>();
            dotSr.sprite = SpriteCache.WhitePixel;
            dotSr.color = affixes[i].AuraColor;
            dotSr.sortingOrder = 5;
            dotObj.transform.localScale = new Vector3(0.4f, 0.4f, 1f);
        }
    }
}
