using UnityEngine;
using System;

/// <summary>
/// 玩家数据提供者接口 — 定义数据源契约
/// 所有UI通过此接口读取数据，禁止直接访问PlayerPrefs或GameManager
/// </summary>
public interface IPlayerDataProvider
{
    int HighestStage { get; }
    int Gold { get; }
    int Level { get; }
    bool IsEndlessUnlocked { get; }
    bool IsLoaded { get; }
    bool IsOfflineMode { get; }
    DateTime LastSyncTime { get; }

    /// <summary>数据变更时触发，UI监听此事件刷新</summary>
    event Action OnDataChanged;
}
