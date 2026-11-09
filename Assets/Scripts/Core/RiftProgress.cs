using UnityEngine;

/// <summary>
/// 秘境独立进度存档 — 与旧关卡进度(HighestStageCleared)分离, 秘境从第1层逐层解锁。
/// 本地持久化(PlayerPrefs); 通关第N层时 MarkCleared(N-1) 记录, 只增不减。
/// </summary>
public static class RiftProgress
{
    private const string Key = "ARPG_RiftHighest";

    /// <summary>已通关最高秘境层(0-based, -1=尚未通关任何层)</summary>
    public static int HighestCleared => PlayerPrefs.GetInt(Key, -1);

    /// <summary>能否进入该层(0-based): 只允许 已通关最高层+1, 不能提前进入</summary>
    public static bool CanEnter(int layer0Based) => layer0Based <= HighestCleared + 1;

    /// <summary>记录通关 (只增不减)</summary>
    public static void MarkCleared(int layer0Based)
    {
        if (layer0Based <= HighestCleared) return;
        PlayerPrefs.SetInt(Key, layer0Based);
        PlayerPrefs.Save();
    }
}
