using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Procedural audio system — multi-oscillator synthesis with ADSR envelopes,
/// lowpass filters, chord progressions, and drum layers.
/// No audio files needed. Scene-aware BGM switching.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    public enum BGMType { Title, Hub, Battle, Boss, GameOver }

    public float BgmVolume { get; set; } = 0.4f;
    public float SfxVolume { get; set; } = 0.7f;

    private AudioSource bgmSource;
    private List<AudioSource> sfxSources = new List<AudioSource>();
    private const int MaxSfxSources = 8;

    // Cached procedural clips
    private AudioClip _hitClip;
    private AudioClip _slashClip;
    private AudioClip _levelUpClip;
    private AudioClip _healClip;
    private AudioClip _deathClip;
    private AudioClip _pickupClip;
    private AudioClip _dashClip;
    private AudioClip _shieldClip;
    private AudioClip _buttonClip;
    private AudioClip _explosionClip;
    private AudioClip _critClip;

    // Scene-specific BGM clips
    private AudioClip _bgmTitleClip;
    private AudioClip _bgmHubClip;
    private AudioClip _bgmBattleClip;
    private AudioClip _bgmBossClip;
    private AudioClip _bgmGameOverClip;
    private BGMType _currentBGM;

    private const int SampleRate = 44100;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;

        for (int i = 0; i < MaxSfxSources; i++)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            sfxSources.Add(src);
        }

        GenerateAllClips();
    }

    private void Start()
    {
        PlayBGM(BGMType.Title);
    }

    // === BGM ===

    public void PlayBGM(BGMType type = BGMType.Battle)
    {
        if (_currentBGM == type && bgmSource.isPlaying) return;
        _currentBGM = type;

        AudioClip clip = type switch
        {
            BGMType.Title => _bgmTitleClip,
            BGMType.Hub => _bgmHubClip,
            BGMType.Battle => _bgmBattleClip,
            BGMType.Boss => _bgmBossClip,
            BGMType.GameOver => _bgmGameOverClip,
            _ => _bgmBattleClip
        };

        if (clip == null) return;
        bgmSource.clip = clip;
        bgmSource.volume = BgmVolume;
        bgmSource.Play();
    }

    public void StopBGM() => bgmSource.Stop();

    public void SetBGMVolume(float vol)
    {
        BgmVolume = Mathf.Clamp01(vol);
        bgmSource.volume = BgmVolume;
        PlayerPrefs.SetFloat("ARPG_BgmVol", BgmVolume);
    }

    public void SetSFXVolume(float vol)
    {
        SfxVolume = Mathf.Clamp01(vol);
        PlayerPrefs.SetFloat("ARPG_SfxVol", SfxVolume);
    }

    public void LoadSavedVolumes()
    {
        BgmVolume = PlayerPrefs.GetFloat("ARPG_BgmVol", 0.4f);
        SfxVolume = PlayerPrefs.GetFloat("ARPG_SfxVol", 0.7f);
        bgmSource.volume = BgmVolume;
    }

    // === SFX PUBLIC API ===

    public void PlayHit() => PlaySFX(_hitClip);
    public void PlaySlash() => PlaySFX(_slashClip);
    public void PlayLevelUp() => PlaySFX(_levelUpClip);
    public void PlayHeal() => PlaySFX(_healClip);
    public void PlayDeath() => PlaySFX(_deathClip);
    public void PlayPickup() => PlaySFX(_pickupClip);
    public void PlayDash() => PlaySFX(_dashClip);
    public void PlayShield() => PlaySFX(_shieldClip);
    public void PlayButton() => PlaySFX(_buttonClip);
    public void PlayExplosion() => PlaySFX(_explosionClip);
    public void PlayCrit() => PlaySFX(_critClip);

    // === INTERNAL ===

    private void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;
        for (int i = 0; i < sfxSources.Count; i++)
        {
            if (!sfxSources[i].isPlaying)
            {
                sfxSources[i].clip = clip;
                sfxSources[i].volume = SfxVolume;
                sfxSources[i].Play();
                return;
            }
        }
        sfxSources[0].Stop();
        sfxSources[0].clip = clip;
        sfxSources[0].volume = SfxVolume;
        sfxSources[0].Play();
    }

    // === PROCEDURAL CLIP GENERATION ===

    private void GenerateAllClips()
    {
        // SFX — layered synthesis for richer sound
        _hitClip = GenSFX("hit", 220f, 0.10f, harmonics: 3, noiseAmount: 0.15f);
        _slashClip = GenSFX("slash", 450f, 0.12f, harmonics: 2, noiseAmount: 0.5f, filterFreq: 1200f);
        _levelUpClip = GenChordProgression("levelup", new[] { 523f, 659f, 784f, 1047f }, 0.15f, 0.7f);
        _healClip = GenChordProgression("heal", new[] { 660f, 880f, 990f }, 0.13f, 0.5f);
        _deathClip = GenSFX("death", 80f, 0.4f, harmonics: 4, noiseAmount: 0.2f, sweep: -40f);
        _pickupClip = GenChordProgression("pickup", new[] { 880f, 1100f, 1320f }, 0.07f, 0.3f);
        _dashClip = GenSFX("dash", 600f, 0.14f, harmonics: 1, noiseAmount: 0.4f, filterFreq: 1500f, sweep: 400f);
        _shieldClip = GenChordProgression("shield", new[] { 440f, 550f, 660f, 880f }, 0.08f, 0.4f);
        _buttonClip = GenSFX("btn", 700f, 0.06f, harmonics: 1, noiseAmount: 0f);
        _explosionClip = GenSFX("explode", 120f, 0.35f, harmonics: 5, noiseAmount: 0.7f, filterFreq: 300f, sweep: -60f);
        _critClip = GenChordProgression("crit", new[] { 880f, 1320f, 1760f }, 0.04f, 0.25f);

        // BGM — multi-layer compositions
        _bgmTitleClip = GenBGM("bgm_title", 8f, baseFreq: 44f, mood: 0);
        _bgmHubClip = GenBGM("bgm_hub", 6f, baseFreq: 55f, mood: 1);
        _bgmBattleClip = GenBGM("bgm_battle", 4f, baseFreq: 65f, mood: 2);
        _bgmBossClip = GenBGM("bgm_boss", 5f, baseFreq: 49f, mood: 3);
        _bgmGameOverClip = GenBGM("bgm_over", 7f, baseFreq: 33f, mood: 4);
    }

    /// <summary>Rich layered SFX with harmonics, noise, optional frequency sweep and filter.</summary>
    private AudioClip GenSFX(string name, float freq, float duration, int harmonics = 2,
        float noiseAmount = 0f, float filterFreq = 0f, float sweep = 0f)
    {
        int samples = (int)(SampleRate * duration);
        float[] data = new float[samples];
        float rc = filterFreq > 0 ? 1f / (2f * Mathf.PI * filterFreq) : 0f;
        float dt = 1f / SampleRate;
        float filterAlpha = rc > 0 ? dt / (rc + dt) : 1f;
        float prevFiltered = 0f;

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SampleRate;
            float currentFreq = freq + sweep * (t / duration);
            float env = ADSR(t, duration, 0.005f, duration * 0.15f, 0.7f, duration * 0.5f);

            // Base oscillator
            float sample = Mathf.Sin(2f * Mathf.PI * currentFreq * t);

            // Harmonics (each progressively quieter)
            for (int h = 2; h <= harmonics + 1; h++)
            {
                float harmFreq = currentFreq * h;
                float harmAmp = 1f / (h * h);
                sample += Mathf.Sin(2f * Mathf.PI * harmFreq * t) * harmAmp;
            }

            // Noise layer
            if (noiseAmount > 0f)
            {
                float noise = (Random.value * 2f - 1f) * noiseAmount;
                if (filterFreq > 0)
                {
                    prevFiltered += filterAlpha * (noise - prevFiltered);
                    sample += prevFiltered * 0.5f;
                }
                else
                {
                    sample += noise * 0.5f;
                }
            }

            data[i] = sample * env * 0.25f;
        }

        // Normalize
        Normalize(data, 0.5f);

        AudioClip clip = AudioClip.Create(name, samples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>Arpeggiated chord progression — notes played in sequence.</summary>
    private AudioClip GenChordProgression(string name, float[] notes, float noteDuration, float totalDuration)
    {
        int totalSamples = (int)(SampleRate * totalDuration);
        float[] data = new float[totalSamples];

        for (int n = 0; n < notes.Length; n++)
        {
            int start = (int)(n * noteDuration * SampleRate);
            int len = (int)(noteDuration * SampleRate);
            for (int i = 0; i < len && (start + i) < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float env = ADSR(t, noteDuration, 0.01f, noteDuration * 0.1f, 0.6f, noteDuration * 0.6f);
                // Fundamental + 2nd harmonic
                float s = Mathf.Sin(2f * Mathf.PI * notes[n] * t) * 0.5f;
                s += Mathf.Sin(2f * Mathf.PI * notes[n] * 2 * t) * 0.15f;
                data[start + i] += s * env;
            }
        }

        Normalize(data, 0.5f);

        AudioClip clip = AudioClip.Create(name, totalSamples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>
    /// Multi-layer BGM composition: bass drone + chord pad + arpeggio + drum pattern.
    /// mood: 0=calm, 1=light, 2=tense, 3=epic, 4=sad
    /// </summary>
    private AudioClip GenBGM(string name, float duration, float baseFreq, int mood)
    {
        int samples = (int)(SampleRate * duration);
        float[] data = new float[samples];

        // Mood parameters
        float lfoRate = mood switch { 0 => 0.12f, 1 => 0.2f, 2 => 0.4f, 3 => 0.25f, _ => 0.08f };
        float padLevel = mood switch { 0 => 0.12f, 1 => 0.18f, 2 => 0.10f, 3 => 0.20f, _ => 0.08f };
        float arpLevel = mood switch { 0 => 0f, 1 => 0.06f, 2 => 0.04f, 3 => 0.08f, _ => 0f };
        float drumLevel = mood switch { 0 => 0f, 1 => 0.04f, 2 => 0.08f, 3 => 0.10f, _ => 0f };
        float bassLevel = mood switch { 0 => 0.15f, 1 => 0.12f, 2 => 0.20f, 3 => 0.25f, _ => 0.18f };

        // Chord intervals (semitones from root) per mood
        int[] chord = mood switch
        {
            0 => new[] { 0, 7, 12, 16 },      // maj7 — calm
            1 => new[] { 0, 4, 7, 11 },        // dom7 — light
            2 => new[] { 0, 3, 7, 10 },        // min7 — tense
            3 => new[] { 0, 7, 12, 19 },       // power + octave — epic
            _ => new[] { 0, 3, 7, 10 },        // min7 — sad
        };

        // Drum pattern (16 steps per bar)
        float barLen = duration / 4f; // 4 bars
        bool[] kickPattern = mood >= 2 ? new[] { true, false, false, false, true, false, false, true, true, false, false, false, true, false, true, false }.ToBoolArraySafe() : null;
        bool[] snarePattern = mood >= 2 ? new[] { false, false, true, false, false, false, true, false, false, false, true, false, false, false, true, false }.ToBoolArraySafe() : null;

        float fadeTime = 0.5f;

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SampleRate;
            float fade = 1f;
            if (t < fadeTime) fade = t / fadeTime;
            else if (t > duration - fadeTime) fade = (duration - t) / fadeTime;

            // 1. Bass drone with LFO
            float lfo = 1f + Mathf.Sin(2f * Mathf.PI * lfoRate * t) * 0.03f;
            float bass = Mathf.Sin(2f * Mathf.PI * baseFreq * lfo * t) * 0.3f;
            bass += Mathf.Sin(2f * Mathf.PI * baseFreq * 1.5f * lfo * t) * 0.1f;

            // 2. Chord pad — sustained chord tones
            float pad = 0f;
            for (int c = 0; c < chord.Length; c++)
            {
                float freq = baseFreq * 2f * Mathf.Pow(2f, chord[c] / 12f);
                pad += Mathf.Sin(2f * Mathf.PI * freq * t) * (1f / chord.Length);
            }

            // 3. Arpeggio — cycling notes (skip for calm/sad)
            float arp = 0f;
            if (arpLevel > 0)
            {
                float arpRate = mood == 2 ? 4f : 2f;
                int arpIdx = (int)(t * arpRate) % chord.Length;
                float arpFreq = baseFreq * 4f * Mathf.Pow(2f, chord[arpIdx] / 12f);
                float arpEnv = 1f - ((t * arpRate) % 1f);
                arp = Mathf.Sin(2f * Mathf.PI * arpFreq * t) * arpEnv;
            }

            // 4. Drum layer
            float drums = 0f;
            if (drumLevel > 0)
            {
                float stepLen = barLen / 16f;
                float stepPos = (t % barLen) / stepLen;
                int stepIdx = (int)stepPos;
                float stepFrac = stepPos - stepIdx;

                // Kick
                if (kickPattern != null && kickPattern[stepIdx % 16])
                {
                    float kickEnv = Mathf.Exp(-stepFrac * 15f);
                    drums += Mathf.Sin(2f * Mathf.PI * 60f * stepFrac) * kickEnv * 0.5f;
                }
                // Snare
                if (snarePattern != null && snarePattern[stepIdx % 16])
                {
                    float snareEnv = Mathf.Exp(-stepFrac * 20f);
                    drums += (Random.value * 2f - 1f) * snareEnv * 0.3f;
                }
            }

            // Mix all layers
            data[i] = (bass * bassLevel + pad * padLevel + arp * arpLevel + drums * drumLevel) * fade * 0.5f;
        }

        Normalize(data, 0.7f);

        AudioClip clip = AudioClip.Create(name, samples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>Full ADSR envelope.</summary>
    private static float ADSR(float t, float duration, float attack, float decay, float sustain, float releaseStart)
    {
        if (t < attack) return t / attack;
        if (t < attack + decay) return 1f - (1f - sustain) * ((t - attack) / decay);
        if (t < releaseStart) return sustain;
        float relT = (t - releaseStart) / (duration - releaseStart);
        return sustain * (1f - relT);
    }

    /// <summary>Normalize audio data to target peak amplitude.</summary>
    private static void Normalize(float[] data, float targetPeak)
    {
        float peak = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float abs = Mathf.Abs(data[i]);
            if (abs > peak) peak = abs;
        }
        if (peak < 0.0001f) return;
        float scale = targetPeak / peak;
        for (int i = 0; i < data.Length; i++)
            data[i] *= scale;
    }

    // Backward compat
    private static float Envelope(float t, float duration, float attack, float decayStart)
        => ADSR(t, duration, attack, duration * 0.1f, 0.7f, decayStart);
}

/// <summary>Helper extension for bool array safe access.</summary>
internal static class BoolArrayExtensions
{
    public static bool[] ToBoolArraySafe(this bool[] arr) => arr ?? System.Array.Empty<bool>();
}
