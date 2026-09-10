using UnityEngine;

/// <summary>
/// PVP统计数据访问层 — 从AsyncPvpUI提取
/// 客户端本地胜负记录（非服务器权威）
/// </summary>
public static class PvpStatsData
{
    private const string KeyWins = "ARPG_PvpWins";
    private const string KeyLosses = "ARPG_PvpLosses";
    private const string KeyDefRating = "ARPG_PvpDefRating";

    public static int Wins => PlayerPrefs.GetInt(KeyWins, 0);
    public static int Losses => PlayerPrefs.GetInt(KeyLosses, 0);
    public static int DefRating => PlayerPrefs.GetInt(KeyDefRating, 50);

    public static void RecordResult(bool won)
    {
        if (won)
            PlayerPrefs.SetInt(KeyWins, Wins + 1);
        else
            PlayerPrefs.SetInt(KeyLosses, Losses + 1);
        PlayerPrefs.Save();
    }

    public static void SetDefRating(int rating)
    {
        PlayerPrefs.SetInt(KeyDefRating, Mathf.Max(50, rating));
        PlayerPrefs.Save();
    }
}
