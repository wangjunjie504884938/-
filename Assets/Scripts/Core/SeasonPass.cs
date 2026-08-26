using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 纯客户端赛季通行证 — 每周刷新赛季，免费轨道+高级轨道，本地存储
/// </summary>
public static class SeasonPass
{
    private const string KeySeason = "ARPG_Season_Id";
    private const string KeyXP = "ARPG_Season_XP";
    private const string KeyLevel = "ARPG_Season_Level";
    private const string KeyPremium = "ARPG_Season_Premium";
    private const string KeyFreeClaimed = "ARPG_Season_FreeClaimed";
    private const string KeyPremiumClaimed = "ARPG_Season_PremiumClaimed";
    private const string KeyWeekStart = "ARPG_Season_WeekStart";
    public const int MaxLevel = 30;
    private const int XPPerLevel = 100;

    /// <summary>当前赛季ID（每周更新）</summary>
    public static int SeasonId
    {
        get => PlayerPrefs.GetInt(KeySeason, 1);
        private set => PlayerPrefs.SetInt(KeySeason, value);
    }

    /// <summary>赛季经验</summary>
    public static int XP
    {
        get => PlayerPrefs.GetInt(KeyXP, 0);
        private set => PlayerPrefs.SetInt(KeyXP, value);
    }

    /// <summary>赛季等级</summary>
    public static int Level
    {
        get => PlayerPrefs.GetInt(KeyLevel, 1);
        private set => PlayerPrefs.SetInt(KeyLevel, Mathf.Clamp(value, 1, MaxLevel));
    }

    /// <summary>是否已购买高级通行证</summary>
    public static bool IsPremium
    {
        get => PlayerPrefs.GetInt(KeyPremium, 0) == 1;
        set { PlayerPrefs.SetInt(KeyPremium, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    /// <summary>本周是否已开始</summary>
    public static bool IsNewWeek()
    {
        string lastWeek = PlayerPrefs.GetString(KeyWeekStart, "");
        string thisWeek = System.DateTime.Now.ToString("yyyyWW");
        return lastWeek != thisWeek;
    }

    /// <summary>刷新赛季（每周调用）</summary>
    public static void CheckNewSeason()
    {
        if (!IsNewWeek()) return;
        string thisWeek = System.DateTime.Now.ToString("yyyyWW");
        PlayerPrefs.SetString(KeyWeekStart, thisWeek);
        SeasonId++;
        XP = 0;
        Level = 1;
        PlayerPrefs.DeleteKey(KeyFreeClaimed);
        PlayerPrefs.DeleteKey(KeyPremiumClaimed);
        PlayerPrefs.Save();
    }

    /// <summary>添加赛季经验</summary>
    public static void AddXP(int amount)
    {
        XP += amount;
        while (XP >= XPPerLevel && Level < MaxLevel)
        {
            XP -= XPPerLevel;
            Level++;
        }
        if (Level >= MaxLevel) XP = 0;
        PlayerPrefs.Save();
    }

    /// <summary>免费轨道奖励</summary>
    public static int GetFreeReward(int level) => level * 50;

    /// <summary>高级轨道奖励</summary>
    public static int GetPremiumReward(int level) => level * 100 + level * 5;

    /// <summary>是否已领取免费奖励</summary>
    public static bool IsFreeClaimed(int level) => PlayerPrefs.GetInt($"{KeyFreeClaimed}_{level}", 0) == 1;

    /// <summary>是否已领取高级奖励</summary>
    public static bool IsPremiumClaimed(int level) => PlayerPrefs.GetInt($"{KeyPremiumClaimed}_{level}", 0) == 1;

    /// <summary>领取免费奖励</summary>
    public static bool ClaimFree(int level)
    {
        if (level > Level || IsFreeClaimed(level)) return false;
        PlayerPrefs.SetInt($"{KeyFreeClaimed}_{level}", 1);
        var player = GameManager.Instance?.Player;
        if (player != null) player.Stats.AddGold(GetFreeReward(level));
        PlayerPrefs.Save();
        return true;
    }

    /// <summary>领取高级奖励</summary>
    public static bool ClaimPremium(int level)
    {
        if (!IsPremium || level > Level || IsPremiumClaimed(level)) return false;
        PlayerPrefs.SetInt($"{KeyPremiumClaimed}_{level}", 1);
        var player = GameManager.Instance?.Player;
        if (player != null) player.Stats.AddGold(GetPremiumReward(level));
        PlayerPrefs.Save();
        return true;
    }

    /// <summary>一键领取所有可领奖励</summary>
    public static int ClaimAll()
    {
        int total = 0;
        for (int i = 1; i <= Level; i++)
        {
            if (!IsFreeClaimed(i)) { ClaimFree(i); total += GetFreeReward(i); }
            if (IsPremium && !IsPremiumClaimed(i)) { ClaimPremium(i); total += GetPremiumReward(i); }
        }
        return total;
    }

    /// <summary>购买高级通行证（消耗金币）</summary>
    public static bool PurchasePremium(int cost = 5000)
    {
        if (IsPremium) return false;
        var player = GameManager.Instance?.Player;
        if (player == null || !player.Stats.SpendGold(cost)) return false;
        IsPremium = true;
        return true;
    }
}
