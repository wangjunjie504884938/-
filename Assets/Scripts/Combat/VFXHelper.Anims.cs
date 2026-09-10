using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// VFXHelper partial — Animation coroutines for VFX objects.
/// All coroutines return objects to VFXPool when done.
/// </summary>
public static partial class VFXHelper
{
    private static IEnumerator SlashAnim(GameObject obj, SpriteRenderer sr)
    {
        float duration = 0.2f;
        float t = 0f;
        Vector3 startScale = obj.transform.localScale;
        while (t < duration)
        {
            t += Time.deltaTime;
            float progress = t / duration;
            obj.transform.localScale = new Vector3(
                startScale.x * (1f + progress * 0.5f),
                startScale.y * (1f - progress * 0.5f),
                1f
            );
            Color c = sr.color;
            c.a = 0.8f * (1f - progress);
            sr.color = c;
            yield return null;
        }
        VFXPool.Return("slash", obj);
    }

    private static IEnumerator SlashTrailAnim(GameObject obj, SpriteRenderer sr, float delay)
    {
        yield return new WaitForSeconds(delay);
        float duration = 0.15f;
        float t = 0f;
        Vector3 startScale = obj.transform.localScale;
        while (t < duration)
        {
            t += Time.deltaTime;
            float progress = t / duration;
            obj.transform.localScale = new Vector3(
                startScale.x * (1f + progress * 0.3f),
                startScale.y * (1f - progress * 0.7f),
                1f
            );
            Color c = sr.color;
            c.a = 0.6f * (1f - progress);
            sr.color = c;
            yield return null;
        }
        VFXPool.Return("slash", obj);
    }

    private static IEnumerator BeamAnim(GameObject obj, SpriteRenderer sr, float duration)
    {
        float t = 0f;
        Vector3 startScale = obj.transform.localScale;
        while (t < duration)
        {
            t += Time.deltaTime;
            float progress = t / duration;
            obj.transform.localScale = new Vector3(
                startScale.x,
                startScale.y * (1f - progress * 0.8f),
                1f
            );
            Color c = sr.color;
            c.a = 0.8f * (1f - progress);
            sr.color = c;
            yield return null;
        }
        VFXPool.Return("beam", obj);
    }

    private static IEnumerator MistAnim(GameObject obj, SpriteRenderer sr, float duration)
    {
        float t = 0f;
        Vector3 startScale = obj.transform.localScale;
        while (t < duration)
        {
            t += Time.deltaTime;
            float progress = t / duration;
            obj.transform.localScale = startScale * (1f + progress * 2f);
            Color c = sr.color;
            c.a = 0.3f * (1f - progress);
            sr.color = c;
            yield return null;
        }
        VFXPool.Return("mist", obj);
    }

    private static IEnumerator DamageNumberAnim(GameObject obj, SpriteRenderer sr, bool isCrit)
    {
        float duration = isCrit ? 1.2f : 0.8f;
        float t = 0f;
        Vector3 pos = obj.transform.position;
        float speed = 2f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            pos.y += speed * Time.unscaledDeltaTime;
            obj.transform.position = pos;

            if (isCrit)
            {
                float scaleT = t / 0.15f;
                if (scaleT < 1f)
                {
                    float s = Mathf.Lerp(0.1f, 1.3f, scaleT * scaleT);
                    obj.transform.localScale = new Vector3(s, s, 1f);
                }
                else if (t < 0.25f)
                {
                    float settleT = (t - 0.15f) / 0.1f;
                    float s = Mathf.Lerp(1.3f, 0.8f, settleT);
                    obj.transform.localScale = new Vector3(s, s, 1f);
                }
                else
                {
                    obj.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
                }
            }
            else
            {
                if (t < 0.1f)
                {
                    float s = Mathf.Lerp(0.1f, 0.5f, t / 0.1f);
                    obj.transform.localScale = new Vector3(s, s, 1f);
                }
            }

            float progress = t / duration;
            Color c = sr.color;
            c.a = 1f - progress;
            sr.color = c;
            yield return null;
        }
        if (sr != null && sr.sprite != null && sr.sprite != SpriteCache.WhitePixel)
        {
            bool isCached = false;
            foreach (var kv in _numberSpriteCache)
            {
                if (kv.Value == sr.sprite) { isCached = true; break; }
            }
            if (!isCached)
            {
                Object.Destroy(sr.sprite.texture);
                Object.Destroy(sr.sprite);
            }
            sr.sprite = SpriteCache.WhitePixel;
        }
        VFXPool.Return("dmgnum", obj);
    }

    private static IEnumerator AreaPulseAnim(GameObject obj, SpriteRenderer sr, float targetRadius)
    {
        float duration = 0.3f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float progress = t / duration;
            float scale = Mathf.Lerp(0.1f, targetRadius * 2f, progress);
            obj.transform.localScale = new Vector3(scale, scale, 1f);
            Color c = sr.color;
            c.a = 0.3f * (1f - progress);
            sr.color = c;
            yield return null;
        }
        VFXPool.Return("areapulse", obj);
    }

    private static IEnumerator DeathFlashAnim(GameObject obj, SpriteRenderer sr)
    {
        float duration = 0.2f;
        float t = 0f;
        Vector3 startScale = obj.transform.localScale;
        while (t < duration)
        {
            t += Time.deltaTime;
            float progress = t / duration;
            obj.transform.localScale = startScale * (1f + progress * 1.5f);
            Color c = sr.color;
            c.a = 0.6f * (1f - progress);
            sr.color = c;
            yield return null;
        }
        VFXPool.Return("deathflash", obj);
    }

    private static IEnumerator ParticleAnim(GameObject obj, SpriteRenderer sr, float lifetime)
    {
        float t = 0f;
        while (t < lifetime)
        {
            t += Time.deltaTime;
            Color c = sr.color;
            c.a = 1f - (t / lifetime);
            sr.color = c;
            yield return null;
        }
        VFXPool.Return("particle", obj);
    }
}
