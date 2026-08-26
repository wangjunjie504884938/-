using UnityEngine;

/// <summary>
/// Globally cached 1x1 white sprite used by all projectiles and VFX.
/// Eliminates repeated new Texture2D + Sprite.Create calls at runtime.
/// </summary>
public static class SpriteCache
{
    private static Sprite _whitePixel;
    private static Texture2D _whiteTex;

    public static Sprite WhitePixel
    {
        get
        {
            if (_whitePixel != null) return _whitePixel;
            _whiteTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _whiteTex.SetPixel(0, 0, Color.white);
            _whiteTex.Apply(false, false);
            _whiteTex.name = "WhitePixel";
            _whitePixel = Sprite.Create(_whiteTex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            _whitePixel.name = "WhitePixel";
            return _whitePixel;
        }
    }

    /// <summary>
    /// Assign the cached white pixel sprite to a SpriteRenderer, tinted with color.
    /// </summary>
    public static void SetWhitePixel(this SpriteRenderer sr, Color tint, float scale)
    {
        sr.sprite = WhitePixel;
        sr.color = tint;
        sr.transform.localScale = new Vector3(scale, scale, 1f);
    }
}
