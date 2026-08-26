using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 离线挂机收益 — 玩家离线后回来领取金币奖励
/// 基于最高通关关卡计算每小时收益，上限8小时
/// </summary>
public static class OfflineRewards
{
    private const string LastOnlineKey = "ARPG_LastOnlineTime";
    private const string RewardClaimedKey = "ARPG_OfflineRewardClaimed";
    private const float MaxHours = 8f;
    private const float MinHours = 0.5f; // 至少离线半小时才有奖励

    /// <summary>记录离线时间（应用退出/返回标题时调用）</summary>
    public static void RecordLogout()
    {
        PlayerPrefs.SetString(LastOnlineKey, System.DateTime.Now.ToString("o"));
        PlayerPrefs.Save();
    }

    /// <summary>计算离线奖励信息</summary>
    public static OfflineRewardInfo CalculateReward()
    {
        if (!PlayerPrefs.HasKey(LastOnlineKey))
            return new OfflineRewardInfo { goldReward = 0, hours = 0, eligible = false };

        // 每天只能领一次
        string today = System.DateTime.Now.ToString("yyyyMMdd");
        if (PlayerPrefs.GetString(RewardClaimedKey, "") == today)
            return new OfflineRewardInfo { goldReward = 0, hours = 0, eligible = false };

        var lastOnlineStr = PlayerPrefs.GetString(LastOnlineKey, "");
        if (!System.DateTime.TryParse(lastOnlineStr, out var lastOnline))
            return new OfflineRewardInfo { goldReward = 0, hours = 0, eligible = false };

        float hours = (float)(System.DateTime.Now - lastOnline).TotalHours;
        if (hours < MinHours)
            return new OfflineRewardInfo { goldReward = 0, hours = hours, eligible = false };

        hours = Mathf.Min(hours, MaxHours);

        // 每小时收益 = (最高关卡+1) * 50
        int stage = RuntimePlayerData.Instance?.HighestStage ?? 0;
        int goldPerHour = (stage + 1) * 50;
        int goldReward = Mathf.RoundToInt(goldPerHour * hours);

        return new OfflineRewardInfo
        {
            goldReward = goldReward,
            hours = hours,
            goldPerHour = goldPerHour,
            eligible = true
        };
    }

    /// <summary>领取奖励</summary>
    public static void ClaimReward()
    {
        string today = System.DateTime.Now.ToString("yyyyMMdd");
        PlayerPrefs.SetString(RewardClaimedKey, today);
        PlayerPrefs.Save();
    }

    public struct OfflineRewardInfo
    {
        public int goldReward;
        public float hours;
        public int goldPerHour;
        public bool eligible;
    }
}
