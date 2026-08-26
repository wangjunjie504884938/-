using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Visual shield effect with rotating ring dots, glow, and optional evolved runes/wings.
/// Attached to runtime GameObject, follows target, auto-destroys after duration.
/// </summary>
public class DivineShieldVFX : MonoBehaviour
{
    private const int RingDotCount = 16;
    private const int EvolvedRuneCount = 6;

    private Transform _followTarget;
    private float _radius;
    private float _duration;
    private bool _isEvolved;
    private float _elapsed;
    private List<SpriteRenderer> _ringDots;
    private List<SpriteRenderer> _runeSigils;
    private List<float> _runeBaseAngles;

    private GameObject _ringParent;
    private GameObject _runeParent;

    public void Initialize(float radius, float duration, bool isEvolved, Transform followTarget)
    {
        _radius = radius;
        _duration = duration;
        _isEvolved = isEvolved;
        _followTarget = followTarget;
        _elapsed = 0f;

        BuildRing();
        if (isEvolved) BuildEvolvedWings();
    }

    private void BuildRing()
    {
        _ringParent = new GameObject("ShieldRing");
        _ringParent.transform.SetParent(transform, false);
        _ringParent.transform.localPosition = Vector3.zero;

        Color ringColor = new Color(0.8f, 0.9f, 1f, 0.6f);
        _ringDots = new List<SpriteRenderer>(RingDotCount);

        for (int i = 0; i < RingDotCount; i++)
        {
            float angle = (360f / RingDotCount) * i;
            float rad = angle * Mathf.Deg2Rad;
            Vector3 localPos = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * _radius;

            var dot = new GameObject($"Dot_{i}");
            dot.transform.SetParent(_ringParent.transform, false);
            dot.transform.localPosition = localPos;
            dot.transform.localScale = new Vector3(0.18f, 0.18f, 1f);

            var sr = dot.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteCache.WhitePixel;
            sr.color = ringColor;
            sr.sortingOrder = 6;
            _ringDots.Add(sr);
        }

        var glow = new GameObject("ShieldGlow");
        glow.transform.SetParent(_ringParent.transform, false);
        glow.transform.localPosition = Vector3.zero;
        glow.transform.localScale = new Vector3(_radius * 1.6f, _radius * 1.6f, 1f);

        var glowSr = glow.AddComponent<SpriteRenderer>();
        glowSr.sprite = SpriteCache.WhitePixel;
        glowSr.color = new Color(0.8f, 0.9f, 1f, 0.12f);
        glowSr.sortingOrder = 4;
    }

    private void BuildEvolvedWings()
    {
        _runeParent = new GameObject("ShieldRunes");
        _runeParent.transform.SetParent(transform, false);
        _runeParent.transform.localPosition = Vector3.zero;

        Color goldColor = new Color(1f, 0.9f, 0.3f, 0.8f);
        Color wingColor = new Color(1f, 0.95f, 0.5f, 0.5f);
        _runeSigils = new List<SpriteRenderer>(EvolvedRuneCount);
        _runeBaseAngles = new List<float>(EvolvedRuneCount);

        for (int i = 0; i < EvolvedRuneCount; i++)
        {
            float angle = (360f / EvolvedRuneCount) * i;
            float rad = angle * Mathf.Deg2Rad;
            Vector3 localPos = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * (_radius + 0.3f);

            var rune = new GameObject($"Rune_{i}");
            rune.transform.SetParent(_runeParent.transform, false);
            rune.transform.localPosition = localPos;
            rune.transform.localScale = new Vector3(0.25f, 0.25f, 1f);
            rune.transform.rotation = Quaternion.Euler(0, 0, 45f);

            var sr = rune.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteCache.WhitePixel;
            sr.color = goldColor;
            sr.sortingOrder = 7;
            _runeSigils.Add(sr);
            _runeBaseAngles.Add(angle);
        }

        for (int side = 0; side < 2; side++)
        {
            float xDir = side == 0 ? -1f : 1f;
            var wing = new GameObject($"Wing_{side}");
            wing.transform.SetParent(_runeParent.transform, false);
            wing.transform.localPosition = new Vector3(xDir * (_radius + 0.2f), 0.2f, 0);
            wing.transform.localScale = new Vector3(0.6f, 0.12f, 1f);
            wing.transform.rotation = Quaternion.Euler(0, 0, xDir * 25f);

            var sr = wing.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteCache.WhitePixel;
            sr.color = wingColor;
            sr.sortingOrder = 5;
        }
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;

        if (_followTarget != null)
            transform.position = _followTarget.position;

        float progress = _elapsed / _duration;

        if (_ringParent != null)
            _ringParent.transform.Rotate(0, 0, 60f * Time.deltaTime);

        float pulse = 0.5f + 0.5f * Mathf.Sin(_elapsed * 8f);
        float alphaMult = progress > 0.9f ? Mathf.Max(0f, 1f - (progress - 0.9f) / 0.1f) : 1f;

        if (_ringDots != null)
        {
            float baseAlpha = 0.6f * (0.6f + 0.4f * pulse) * alphaMult;
            for (int i = 0; i < _ringDots.Count; i++)
            {
                Color cc = _ringDots[i].color;
                cc.a = baseAlpha;
                _ringDots[i].color = cc;
            }
        }

        if (_isEvolved && _runeParent != null && _runeSigils != null)
        {
            _runeParent.transform.Rotate(0, 0, -40f * Time.deltaTime);
            float runePulse = 0.5f + 0.5f * Mathf.Sin(_elapsed * 12f);
            for (int i = 0; i < _runeSigils.Count; i++)
            {
                Color cc = _runeSigils[i].color;
                cc.a = (0.6f + 0.4f * runePulse) * alphaMult;
                _runeSigils[i].color = cc;
            }
        }

        if (_elapsed >= _duration)
            Destroy(gameObject);
    }

    /// <summary>Force-break the shield VFX early (when shield HP hits 0).</summary>
    public void Break()
    {
        VFXHelper.SpawnShieldBreakEffect(transform.position, _radius, _isEvolved);
        Destroy(gameObject);
    }
}
