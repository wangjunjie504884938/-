using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// 运行时玩家数据 — 内存中唯一真实来源
/// 实现 IPlayerDataProvider 接口，所有UI通过接口读取
/// 数据只能通过 RefreshFromServer 或 RefreshFromCache 修改
/// </summary>
public class RuntimePlayerData : MonoBehaviour, IPlayerDataProvider
{
    public static RuntimePlayerData Instance { get; private set; }

    [Header("玩家进度数据")]
    [SerializeField] private int _highestStage = -1;
    [SerializeField] private bool[] _clearFlags = new bool[8];
    [SerializeField] private bool _isLoaded = false;
    [SerializeField] private bool _isOfflineMode = false;
    [SerializeField] private DateTime _lastSyncTime = DateTime.MinValue;
    [SerializeField] private int _dataVersion = 1;

    public int HighestStage => _highestStage;
    public bool[] ClearFlags => _clearFlags;
    public bool IsLoaded => _isLoaded;
    public bool IsOfflineMode => _isOfflineMode;
    public DateTime LastSyncTime => _lastSyncTime;
    public int DataVersion => _dataVersion;
    public int Gold => GameManager.Instance?.Player?.Stats?.Gold ?? 0;
    public int Level => GameManager.Instance?.Player?.Stats?.Level ?? 1;
    public bool IsEndlessUnlocked => _highestStage >= 5;

    public event Action OnDataChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>从服务器数据刷新（唯一服务器入口）</summary>
    /// <param name="dataVersion">服务器端DataVersion，传-1(默认)表示不修改当前版本号</param>
    public void RefreshFromServer(int highestStage, bool[] clearFlags, int dataVersion = -1)
    {
        _highestStage = highestStage;
        if (dataVersion >= 0)
            _dataVersion = dataVersion;
        if (clearFlags != null && clearFlags.Length >= 8)
            _clearFlags = clearFlags;
        else
        {
            _clearFlags = new bool[8];
            for (int i = 0; i <= highestStage && i < 8; i++)
                _clearFlags[i] = true;
        }
        _isLoaded = true;
        _isOfflineMode = false;
        _lastSyncTime = DateTime.Now;

        // 同步写入本地缓存
        LocalSettingsManager.CacheStageProgress(highestStage, _clearFlags);

        OnDataChanged?.Invoke();
    }

    /// <summary>从本地缓存刷新（仅离线兜底）</summary>
    public void RefreshFromCache(int highestStage)
    {
        _highestStage = highestStage;
        _clearFlags = new bool[8];
        for (int i = 0; i <= highestStage && i < 8; i++)
            _clearFlags[i] = true;
        _isLoaded = false;
        _isOfflineMode = true;

        OnDataChanged?.Invoke();
    }

    /// <summary>更新版本号（服务器返回新版本号后调用）</summary>
    public void SetDataVersion(int version)
    {
        _dataVersion = version;
    }
    public bool CanEnterStage(int dungeonId)
    {
        if (dungeonId <= 0) return true;
        return dungeonId <= _highestStage + 1;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
