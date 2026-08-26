using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 游戏配置管理器 — 从服务器拉取所有游戏属性, 缓存到本地
///
/// 使用方式:
///   GameConfigManager.Instance.GetClassData(HeroClass.Warrior)
///   GameConfigManager.Instance.GetGlobal("CritMultiplier", 2f)
///   GameConfigManager.Instance.IsLoaded
///
/// 工作流程:
///   1. GameManager.Awake() → 启动 LoadConfigCo()
///   2. 从 GET /api/config/all 拉取配置
///   3. 成功 → 缓存到 PlayerPrefs + 内存
///   4. 失败 → 从 PlayerPrefs 恢复上次缓存
///   5. 都没有 → 使用硬编码默认值 (保证离线可玩)
/// </summary>
public class GameConfigManager : MonoBehaviour
{
    public static GameConfigManager Instance { get; private set; }

    [Header("服务器配置")]
    public string ServerUrl = "http://localhost:5132";
    public float TimeoutSeconds = 10f;

    /// <summary>配置是否已加载 (服务器或缓存)</summary>
    public bool IsLoaded { get; private set; }

    /// <summary>是否从服务器实时拉取 (非缓存)</summary>
    public bool IsFromServer { get; private set; }

    /// <summary>完整配置数据</summary>
    public GameConfigAll Config { get; private set; }

    private const string CacheKey = "ARPG_GameConfig_Json";
    private const string CacheVersionKey = "ARPG_GameConfig_Version";
    private const int CurrentCacheVersion = 2; // Bump this when breaking changes require cache invalidation

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Check cache version — invalidate stale cache
        int cachedVersion = PlayerPrefs.GetInt(CacheVersionKey, 0);
        if (cachedVersion != CurrentCacheVersion)
        {
            PlayerPrefs.DeleteKey(CacheKey);
            PlayerPrefs.SetInt(CacheVersionKey, CurrentCacheVersion);
            PlayerPrefs.Save();
                        GameLog.Log($"[GameConfig] Cache version mismatch ({cachedVersion}→{CurrentCacheVersion}), cleared stale cache");
        }

        // 尝试从 PlayerPrefs 恢复缓存
        LoadFromCache();
    }

    /// <summary>
    /// 从服务器拉取配置 (协程)
    /// </summary>
    public IEnumerator LoadConfigCo(Action onSuccess = null, Action<string> onFail = null)
    {
        string url = $"{ServerUrl}/api/config/all";

        using (var req = UnityWebRequest.Get(url))
        {
            req.timeout = (int)TimeoutSeconds;
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                ParseAndCache(req.downloadHandler.text);
                IsFromServer = true;
                            GameLog.Log($"[GameConfig] 配置加载成功 (服务器) — 职业数:{Config.classes?.Count} 技能数:{Config.skills?.Count}");
                onSuccess?.Invoke();
            }
            else
            {
                GameLog.LogWarning($"[GameConfig] 服务器拉取失败: {req.error}, 使用缓存配置");
                IsFromServer = false;
                onFail?.Invoke(req.error);
            }
        }
    }

    /// <summary>从 PlayerPrefs 恢复缓存</summary>
    private void LoadFromCache()
    {
        string json = PlayerPrefs.GetString(CacheKey, "");
        if (!string.IsNullOrEmpty(json))
        {
            ParseAndCache(json);
            IsFromServer = false;
                        GameLog.Log("[GameConfig] 从本地缓存恢复配置");
        }
    }

    private void ParseAndCache(string json)
    {
        Config = JsonUtility.FromJson<GameConfigAll>(json);
        if (Config != null)
        {
            Config.BuildGlobalDict();
            IsLoaded = true;
            PlayerPrefs.SetString(CacheKey, json);
            PlayerPrefs.Save();
        }
    }

    // ========================================================
    //  全局参数访问
    // ========================================================

    public float GetGlobal(string key, float defaultValue)
    {
        if (Config?.globalDict != null && Config.globalDict.TryGetValue(key, out var val))
        {
            if (float.TryParse(val, out float result)) return result;
        }
        return defaultValue;
    }

    public int GetGlobalInt(string key, int defaultValue)
    {
        if (Config?.globalDict != null && Config.globalDict.TryGetValue(key, out var val))
        {
            if (int.TryParse(val, out int result)) return result;
        }
        return defaultValue;
    }

    public bool GetGlobalBool(string key, bool defaultValue)
    {
        if (Config?.globalDict != null && Config.globalDict.TryGetValue(key, out var val))
        {
            if (bool.TryParse(val, out bool result)) return result;
        }
        return defaultValue;
    }

    // ========================================================
    //  职业属性访问
    // ========================================================

    public GameConfigClassDTO GetClassData(int classId)
    {
        if (Config?.classes != null)
            foreach (var c in Config.classes)
                if (c.classId == classId) return c;
        return null;
    }

    public GameConfigClassDTO GetClassData(HeroClass heroClass)
    {
        return GetClassData((int)heroClass);
    }

    // ========================================================
    //  技能数据访问
    // ========================================================

    public List<GameConfigSkillDTO> GetClassSkills(int classId)
    {
        var result = new List<GameConfigSkillDTO>();
        if (Config?.skills != null)
            foreach (var s in Config.skills)
                if (s.classId == classId) result.Add(s);
        return result;
    }

    public List<GameConfigSkillDTO> GetClassSkills(HeroClass heroClass)
    {
        return GetClassSkills((int)heroClass);
    }

    /// <summary>将CSV字符串解析为float数组</summary>
    public static float[] ParseCsvFloats(string csv)
    {
        if (string.IsNullOrEmpty(csv)) return null;
        var parts = csv.Split(',');
        var result = new float[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            if (!float.TryParse(parts[i], out result[i]))
                return null;
        }
        return result;
    }

    // ========================================================
    //  敌人属性访问
    // ========================================================

    public GameConfigEnemyDTO GetEnemyData(int enemyType)
    {
        if (Config?.enemies != null)
            foreach (var e in Config.enemies)
                if (e.enemyType == enemyType) return e;
        return Config?.enemies?.Count > 0 ? Config.enemies[0] : null;
    }

    // ========================================================
    //  精英词缀访问
    // ========================================================

    public GameConfigAffixDTO GetAffixData(int affixType)
    {
        if (Config?.affixes != null)
            foreach (var a in Config.affixes)
                if (a.affixType == affixType) return a;
        return null;
    }

    // ========================================================
    //  符文数据访问
    // ========================================================

    public GameConfigRuneDTO GetRuneData(int runeType)
    {
        if (Config?.runes != null)
            foreach (var r in Config.runes)
                if (r.runeType == runeType) return r;
        return null;
    }

    // ========================================================
    //  遗物数据访问
    // ========================================================

    public GameConfigRelicDTO GetRelicData(int relicType)
    {
        if (Config?.relics != null)
            foreach (var r in Config.relics)
                if (r.relicType == relicType) return r;
        return null;
    }

    public List<GameConfigRelicDTO> GetAllRelics()
    {
        return Config?.relics ?? new List<GameConfigRelicDTO>();
    }

    // ========================================================
    //  装备配置访问
    // ========================================================

    public GameConfigEquipmentDTO GetEquipmentConfig(int rarity)
    {
        if (Config?.equipment != null)
            foreach (var e in Config.equipment)
                if (e.rarity == rarity) return e;
        return null;
    }

    // ========================================================
    //  颜色辅助方法
    // ========================================================

    public static Color MakeColor(float r, float g, float b, float a = 1f)
        => new Color(r, g, b, a);
}
