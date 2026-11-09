using UnityEngine;
using System;

/// <summary>
/// 元游戏货币钱包 — 寿元币/位面币 (本地持久化 PlayerPrefs, 模拟充值到账)。
/// 与角色金币(Stats.Gold, 服务器权威)和碎片(DismantleSystem.Fragments)互相独立。
/// OnChanged 在余额变动时触发, UI 层订阅刷新。
/// </summary>
public static class CurrencyWallet
{
    private const string KeyLife = "ARPG_LifeCoins";
    private const string KeyPlane = "ARPG_PlaneCoins";

    /// <summary>余额变动通知 (UI订阅刷新)</summary>
    public static event Action OnChanged;

    public static int LifeCoins => PlayerPrefs.GetInt(KeyLife, 0);
    public static int PlaneCoins => PlayerPrefs.GetInt(KeyPlane, 0);

    public static void AddLife(int amount)
    {
        if (amount <= 0) return;
        PlayerPrefs.SetInt(KeyLife, LifeCoins + amount);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    public static void AddPlane(int amount)
    {
        if (amount <= 0) return;
        PlayerPrefs.SetInt(KeyPlane, PlaneCoins + amount);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    /// <summary>按名称加币 (kind = "Life" / "Plane", 充值礼包用)</summary>
    public static void AddLifeOrPlane(string kind, int amount)
    {
        if (kind == "Life") AddLife(amount);
        else if (kind == "Plane") AddPlane(amount);
    }

    /// <summary>扣除, 余额不足返回 false</summary>
    public static bool TrySpendLife(int amount)
    {
        if (amount <= 0 || LifeCoins < amount) return false;
        PlayerPrefs.SetInt(KeyLife, LifeCoins - amount);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
        return true;
    }

    public static bool TrySpendPlane(int amount)
    {
        if (amount <= 0 || PlaneCoins < amount) return false;
        PlayerPrefs.SetInt(KeyPlane, PlaneCoins - amount);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
        return true;
    }
}
