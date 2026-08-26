using UnityEngine;

/// <summary>
/// 全局游戏设置 — 管理可持久化的用户偏好选项
/// </summary>
public static class GameSettings
{
    private const string KeyDamageNumbers = "ARPG_Setting_DamageNumbers";
    private const string KeyScreenShake = "ARPG_Setting_ScreenShake";
    private const string KeyAutoLoot = "ARPG_Setting_AutoLoot";
    private const string KeyFPSDisplay = "ARPG_Setting_FPSDisplay";
    private const string KeyQualityLevel = "ARPG_Setting_QualityLevel";
    private const string KeyResolutionScale = "ARPG_Setting_ResolutionScale";

    public static bool DamageNumbers
    {
        get => PlayerPrefs.GetInt(KeyDamageNumbers, 1) == 1;
        set => PlayerPrefs.SetInt(KeyDamageNumbers, value ? 1 : 0);
    }

    public static bool ScreenShake
    {
        get => PlayerPrefs.GetInt(KeyScreenShake, 1) == 1;
        set => PlayerPrefs.SetInt(KeyScreenShake, value ? 1 : 0);
    }

    public static bool AutoLoot
    {
        get => PlayerPrefs.GetInt(KeyAutoLoot, 1) == 1;
        set => PlayerPrefs.SetInt(KeyAutoLoot, value ? 1 : 0);
    }

    public static bool FPSDisplay
    {
        get => PlayerPrefs.GetInt(KeyFPSDisplay, 0) == 1;
        set => PlayerPrefs.SetInt(KeyFPSDisplay, value ? 1 : 0);
    }

    /// <summary>性能档位: 0=低, 1=中, 2=高</summary>
    public static int QualityLevel
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(KeyQualityLevel, 1), 0, 2);
        set => PlayerPrefs.SetInt(KeyQualityLevel, Mathf.Clamp(value, 0, 2));
    }

    /// <summary>分辨率缩放: 0.5~1.0</summary>
    public static float ResolutionScale
    {
        get => PlayerPrefs.GetFloat(KeyResolutionScale, 1.0f);
        set => PlayerPrefs.SetFloat(KeyResolutionScale, Mathf.Clamp(value, 0.5f, 1.0f));
    }

    /// <summary>根据性能档位返回最大同时敌人数</summary>
    public static int MaxConcurrentEnemies => QualityLevel switch
    {
        0 => 8,
        1 => 15,
        _ => 25
    };

    /// <summary>根据性能档位返回VFX池预热数量</summary>
    public static int VfxPrewarmCount => QualityLevel switch
    {
        0 => 2,
        1 => 4,
        _ => 8
    };

    /// <summary>根据性能档位返回是否启用受击粒子</summary>
    public static bool EnableHitParticles => QualityLevel >= 1;

    /// <summary>根据性能档位返回是否启用冲击波</summary>
    public static bool EnableAreaPulse => QualityLevel >= 1;

    public static void Save() => PlayerPrefs.Save();
}
