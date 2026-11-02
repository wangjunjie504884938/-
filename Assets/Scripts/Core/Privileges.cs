using UnityEngine;

/// <summary>
/// 位面币永久特权 — 购买后 PlayerPrefs 持久化 (经济规范: 位面币消耗/特权)
/// </summary>
public static class Privileges
{
    private const string KeyAutoLoot = "ARPG_Priv_AutoLoot";

    /// <summary>自动拾取特权: 掉落物 8 米磁吸 (未购买走 GameSettings.AutoLoot 的 3.5 米基础档)</summary>
    public static bool AutoLoot
    {
        get => PlayerPrefs.GetInt(KeyAutoLoot, 0) == 1;
        set => PlayerPrefs.SetInt(KeyAutoLoot, value ? 1 : 0);
    }

    public const int AutoLootPrice = 100; // 位面币
}
