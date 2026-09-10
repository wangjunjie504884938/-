using UnityEngine;

/// <summary>
/// 每日任务/签到本地进度数据层 — 从DailyTaskUI提取
/// 客户端本地进度缓存（服务器权威，本地仅缓存）
/// </summary>
public static class DailyTaskData
{
    public static string DateKey => System.DateTime.Now.ToString("yyyyMMdd");

    public static string TaskProgressKey(int taskType) => $"ARPG_Daily_{DateKey}_task{taskType}_progress";
    public static string TaskClaimedKey(int taskType) => $"ARPG_Daily_{DateKey}_task{taskType}_claimed";

    private const string KeySignInLastDate = "ARPG_SignIn_LastDate";
    private const string KeySignInDays = "ARPG_SignIn_Days";

    /// <summary>标记任务已领取（本地缓存）</summary>
    public static void MarkTaskClaimed(int taskType)
    {
        PlayerPrefs.SetInt(TaskClaimedKey(taskType), 1);
        PlayerPrefs.Save();
    }

    /// <summary>任务是否已领取</summary>
    public static bool IsTaskClaimed(int taskType)
    {
        return PlayerPrefs.GetInt(TaskClaimedKey(taskType), 0) == 1;
    }

    /// <summary>保存任务进度（本地缓存）</summary>
    public static void SaveTaskProgress(int taskType, int progress)
    {
        PlayerPrefs.SetInt(TaskProgressKey(taskType), progress);
        PlayerPrefs.Save();
    }

    /// <summary>读取任务进度</summary>
    public static int GetTaskProgress(int taskType)
    {
        return PlayerPrefs.GetInt(TaskProgressKey(taskType), 0);
    }
}
