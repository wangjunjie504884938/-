using UnityEngine;

/// <summary>
/// 本地设置管理器 — 仅作为启动缓存
/// 通关进度只从服务器下载，本地缓存仅用于启动时显示"数据加载中"
/// 严禁用本地缓存调用LoadScene或进入副本
/// 带版本号+时间戳，防止脏缓存覆盖服务器最新数据
/// </summary>
public static class LocalSettingsManager
{
    private const string CacheKey = "ARPG_Cache_StageProgress";
    private const string CacheFlagsKey = "ARPG_Cache_ClearFlags";
    private const string CacheVersionKey = "ARPG_Cache_Version";
    private const string CacheTimestampKey = "ARPG_Cache_Timestamp";

    /// <summary>缓存版本号 — 数据结构变更时递增，旧缓存自动失效</summary>
    private const int CurrentCacheVersion = 1;

    /// <summary>缓存通关进度到本地（仅服务器返回后缓存）</summary>
    public static void CacheStageProgress(int stage, bool[] flags)
    {
        PlayerPrefs.SetInt(CacheKey, stage);
        if (flags != null)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < flags.Length && i < 8; i++)
                sb.Append(flags[i] ? '1' : '0');
            PlayerPrefs.SetString(CacheFlagsKey, sb.ToString());
        }
        // 写入版本号和时间戳
        PlayerPrefs.SetInt(CacheVersionKey, CurrentCacheVersion);
        PlayerPrefs.SetString(CacheTimestampKey, System.DateTime.UtcNow.ToString("o"));
        PlayerPrefs.Save();
    }

    /// <summary>读取缓存的通关进度（仅启动时用，服务器返回后必须覆盖）</summary>
    public static int GetCachedStage()
    {
        // 版本号不匹配则返回默认值，自动失效旧缓存
        if (!IsCacheValid()) return -1;
        return PlayerPrefs.GetInt(CacheKey, -1);
    }

    /// <summary>读取缓存的通关标记</summary>
    public static bool[] GetCachedFlags()
    {
        if (!IsCacheValid()) return new bool[8];
        string s = PlayerPrefs.GetString(CacheFlagsKey, "");
        var flags = new bool[8];
        for (int i = 0; i < s.Length && i < 8; i++)
            flags[i] = s[i] == '1';
        return flags;
    }

    /// <summary>缓存是否有效（版本号匹配且未过期）</summary>
    public static bool IsCacheValid()
    {
        int version = PlayerPrefs.GetInt(CacheVersionKey, 0);
        if (version != CurrentCacheVersion) return false;

        string tsStr = PlayerPrefs.GetString(CacheTimestampKey, "");
        if (string.IsNullOrEmpty(tsStr)) return false;

        // 缓存超过24小时自动失效
        if (System.DateTime.TryParse(tsStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var ts))
        {
            return (System.DateTime.UtcNow - ts).TotalHours < 24;
        }
        return false;
    }

    /// <summary>获取缓存时间戳</summary>
    public static string GetCacheTimestamp()
    {
        return PlayerPrefs.GetString(CacheTimestampKey, "未知");
    }

    /// <summary>清除缓存</summary>
    public static void ClearCache()
    {
        PlayerPrefs.DeleteKey(CacheKey);
        PlayerPrefs.DeleteKey(CacheFlagsKey);
        PlayerPrefs.DeleteKey(CacheVersionKey);
        PlayerPrefs.DeleteKey(CacheTimestampKey);
        PlayerPrefs.Save();
    }
}
