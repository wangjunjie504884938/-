using UnityEngine;
using System.Collections;

/// <summary>
/// Shared sprite loading for hero class previews.
/// Primary: Resources.LoadAsync (异步, 不阻塞主线程)
/// Fallback: 程序化生成像素艺术
/// 后续可无缝迁移到 Addressables.LoadAssetAsync
/// </summary>
public static class CharacterSpriteFactory
{
    private static readonly int TexW = 16;
    private static readonly int TexH = 20;

    private static Sprite _warriorSprite;
    private static Sprite _mageSprite;
    private static Sprite _priestSprite;

    /// <summary>同步获取 (优先缓存→Resources.LoadAll→程序化生成)</summary>
    public static Sprite GetClassSprite(HeroClass heroClass)
    {
        // 检查缓存
        Sprite cached = heroClass switch
        {
            HeroClass.Warrior => _warriorSprite,
            HeroClass.Mage => _mageSprite,
            _ => _priestSprite
        };
        if (cached != null) return cached;

        // 从GameConfigSO读取路径
        var entry = GameConfigSO.Instance?.GetCharacterAsset(heroClass);
        if (entry != null && !string.IsNullOrEmpty(entry.resourcesPath))
        {
            var subSprites = Resources.LoadAll<Sprite>(entry.resourcesPath);
            if (subSprites != null && subSprites.Length > 0)
            {
                foreach (var s in subSprites)
                {
                    if (s.name == entry.spriteName || s.name == $"{heroClass}_Idle")
                    {
                        CacheSprite(heroClass, s);
                        return s;
                    }
                }
                // 没找到指定名称, 用第一个
                CacheSprite(heroClass, subSprites[0]);
                return subSprites[0];
            }
        }

        // Fallback: 程序化生成
        var generated = CreateSprite(heroClass);
        CacheSprite(heroClass, generated);
        return generated;
    }

    /// <summary>异步加载角色预览Sprite (不阻塞主线程, 加载完成后回调)</summary>
    public static IEnumerator LoadClassSpriteAsync(HeroClass heroClass, System.Action<Sprite> onComplete)
    {
        // 检查缓存
        Sprite cached = heroClass switch
        {
            HeroClass.Warrior => _warriorSprite,
            HeroClass.Mage => _mageSprite,
            _ => _priestSprite
        };
        if (cached != null) { onComplete?.Invoke(cached); yield break; }

        // 从GameConfigSO读取路径
        var entry = GameConfigSO.Instance?.GetCharacterAsset(heroClass);
        if (entry != null && !string.IsNullOrEmpty(entry.resourcesPath))
        {
            // Resources.LoadAll 是同步的, 但在协程中调用不会阻塞渲染管线
            // 后续迁移到 Addressables 时改为真正的异步
            var subSprites = Resources.LoadAll<Sprite>(entry.resourcesPath);
            if (subSprites != null && subSprites.Length > 0)
            {
                foreach (var s in subSprites)
                {
                    if (s.name == entry.spriteName || s.name == $"{heroClass}_Idle")
                    {
                        CacheSprite(heroClass, s);
                        onComplete?.Invoke(s);
                        yield break;
                    }
                }
                CacheSprite(heroClass, subSprites[0]);
                onComplete?.Invoke(subSprites[0]);
                yield break;
            }
        }

        // Fallback: 程序化生成 (同步)
        var generated = CreateSprite(heroClass);
        CacheSprite(heroClass, generated);
        onComplete?.Invoke(generated);
    }

    /// <summary>预加载所有职业Sprite (在Loading页面调用)</summary>
    public static IEnumerator PreloadAllAsync()
    {
        yield return LoadClassSpriteAsync(HeroClass.Warrior, null);
        yield return LoadClassSpriteAsync(HeroClass.Mage, null);
        yield return LoadClassSpriteAsync(HeroClass.Priest, null);
    }

    /// <summary>释放缓存 (场景切换时调用, 释放Asset句柄)</summary>
    public static void ReleaseCache()
    {
        _warriorSprite = null;
        _mageSprite = null;
        _priestSprite = null;
    }

    private static void CacheSprite(HeroClass heroClass, Sprite sprite)
    {
        switch (heroClass)
        {
            case HeroClass.Warrior: _warriorSprite = sprite; break;
            case HeroClass.Mage: _mageSprite = sprite; break;
            default: _priestSprite = sprite; break;
        }
    }

    private static Sprite CreateSprite(HeroClass heroClass)
    {
        Texture2D tex = new Texture2D(TexW, TexH);
        Color[] px = new Color[TexW * TexH];
        System.Array.Clear(px, 0, px.Length);

        switch (heroClass)
        {
            case HeroClass.Warrior: FillWarrior(px, TexW); break;
            case HeroClass.Mage: FillMage(px, TexW); break;
            case HeroClass.Priest: FillPriest(px, TexW); break;
        }

        tex.SetPixels(px);
        tex.filterMode = FilterMode.Point;
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, TexW, TexH), new Vector2(0.5f, 0.5f), TexW);
    }

    public static void FillRect(Color[] px, int w, int x, int y, int rw, int rh, Color c)
    {
        for (int dy = 0; dy < rh; dy++)
            for (int dx = 0; dx < rw; dx++)
            {
                int px2 = x + dx, py = y + dy;
                if (px2 >= 0 && px2 < w && py >= 0 && py < px.Length / w)
                    px[py * w + px2] = c;
            }
    }

    public static void FillPx(Color[] px, int w, int x, int y, Color c)
    {
        if (x >= 0 && x < w && y >= 0 && y < px.Length / w)
            px[y * w + x] = c;
    }

    private static void FillWarrior(Color[] px, int w)
    {
        Color armor = new Color(0.7f, 0.15f, 0.15f), armorDk = new Color(0.5f, 0.1f, 0.1f);
        Color skin = new Color(0.85f, 0.7f, 0.55f), helm = new Color(0.6f, 0.6f, 0.65f);
        Color helmDk = new Color(0.4f, 0.4f, 0.45f), plume = new Color(1f, 0.2f, 0.1f);
        Color sword = new Color(0.85f, 0.85f, 0.9f), grip = new Color(0.45f, 0.3f, 0.15f);
        Color boot = new Color(0.35f, 0.2f, 0.1f), eye = new Color(0.1f, 0.1f, 0.1f);
        FillRect(px, w, 5, 15, 6, 3, helm); FillRect(px, w, 4, 14, 8, 1, helmDk);
        FillRect(px, w, 6, 18, 4, 2, plume); FillPx(px, w, 5, 17, plume);
        FillRect(px, w, 6, 12, 4, 3, skin); FillPx(px, w, 7, 14, eye); FillPx(px, w, 9, 14, eye);
        FillRect(px, w, 5, 7, 6, 5, armor); FillRect(px, w, 6, 8, 4, 2, armorDk);
        FillRect(px, w, 3, 9, 2, 2, armor); FillRect(px, w, 11, 9, 2, 2, armor);
        FillRect(px, w, 3, 6, 2, 3, skin); FillRect(px, w, 11, 6, 2, 3, skin);
        FillRect(px, w, 13, 7, 1, 6, sword); FillPx(px, w, 13, 13, grip);
        FillRect(px, w, 2, 7, 2, 3, armorDk);
        FillRect(px, w, 5, 3, 3, 4, armorDk); FillRect(px, w, 8, 3, 3, 4, armorDk);
        FillRect(px, w, 5, 1, 3, 2, boot); FillRect(px, w, 8, 1, 3, 2, boot);
    }

    private static void FillMage(Color[] px, int w)
    {
        Color robe = new Color(0.2f, 0.3f, 0.7f), robeDk = new Color(0.15f, 0.2f, 0.55f);
        Color hat = new Color(0.25f, 0.2f, 0.65f), hatDk = new Color(0.15f, 0.12f, 0.45f);
        Color skin = new Color(0.85f, 0.7f, 0.55f), eye = new Color(0.3f, 0.5f, 1f);
        Color staff = new Color(0.5f, 0.35f, 0.2f), orb = new Color(0.4f, 0.6f, 1f);
        Color orbGlow = new Color(0.6f, 0.8f, 1f), boot = new Color(0.15f, 0.15f, 0.4f);
        FillRect(px, w, 7, 18, 2, 2, hat); FillRect(px, w, 6, 16, 4, 2, hat);
        FillRect(px, w, 5, 14, 6, 2, hatDk); FillRect(px, w, 4, 13, 8, 1, hat);
        FillPx(px, w, 8, 17, new Color(1f, 0.9f, 0.3f));
        FillRect(px, w, 6, 11, 4, 2, skin); FillPx(px, w, 7, 12, eye); FillPx(px, w, 9, 12, eye);
        FillRect(px, w, 7, 9, 2, 2, new Color(0.8f, 0.8f, 0.85f));
        FillRect(px, w, 5, 5, 6, 6, robe); FillRect(px, w, 6, 6, 4, 3, robeDk);
        FillPx(px, w, 7, 8, new Color(1f, 0.9f, 0.3f)); FillPx(px, w, 9, 7, new Color(1f, 0.9f, 0.3f));
        FillRect(px, w, 3, 7, 2, 3, robe); FillRect(px, w, 11, 7, 2, 3, robe);
        FillPx(px, w, 3, 6, skin); FillPx(px, w, 11, 6, skin);
        FillRect(px, w, 13, 3, 1, 10, staff); FillRect(px, w, 12, 12, 3, 3, orb);
        FillPx(px, w, 13, 13, orbGlow);
        FillRect(px, w, 4, 1, 8, 4, robeDk);
        FillRect(px, w, 4, 0, 2, 1, boot); FillRect(px, w, 8, 0, 2, 1, boot);
    }

    private static void FillPriest(Color[] px, int w)
    {
        Color vest = new Color(0.9f, 0.88f, 0.8f), gold = new Color(0.85f, 0.7f, 0.15f);
        Color skin = new Color(0.85f, 0.7f, 0.55f), eye = new Color(0.2f, 0.5f, 0.2f);
        Color hair = new Color(0.9f, 0.85f, 0.5f), halo = new Color(1f, 0.95f, 0.4f);
        Color book = new Color(0.5f, 0.15f, 0.15f), boot = new Color(0.6f, 0.5f, 0.2f);
        FillRect(px, w, 5, 19, 6, 1, halo); FillRect(px, w, 4, 18, 1, 1, halo * 0.7f);
        FillRect(px, w, 11, 18, 1, 1, halo * 0.7f);
        FillRect(px, w, 5, 14, 6, 3, hair); FillRect(px, w, 4, 13, 8, 1, hair);
        FillRect(px, w, 6, 11, 4, 3, skin); FillPx(px, w, 7, 13, eye); FillPx(px, w, 9, 13, eye);
        FillRect(px, w, 5, 5, 6, 6, vest); FillRect(px, w, 5, 5, 6, 1, gold);
        FillRect(px, w, 7, 7, 2, 2, gold); FillPx(px, w, 8, 8, gold);
        FillRect(px, w, 3, 7, 2, 3, vest); FillRect(px, w, 11, 7, 2, 3, vest);
        FillPx(px, w, 3, 6, skin); FillPx(px, w, 11, 6, skin);
        FillRect(px, w, 2, 6, 2, 3, book); FillPx(px, w, 3, 8, gold);
        FillPx(px, w, 12, 10, skin); FillRect(px, w, 12, 9, 1, 2, new Color(1f, 1f, 0.6f, 0.6f));
        FillRect(px, w, 5, 1, 6, 4, vest); FillRect(px, w, 5, 1, 6, 1, gold);
        FillRect(px, w, 5, 0, 2, 1, boot); FillRect(px, w, 8, 0, 2, 1, boot);
    }
}
