using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 好友数据访问层 — 从FriendUI提取
/// 集中管理好友/申请/最近玩家/黑名单的本地缓存读写
/// 注意：在线状态(online/status)是易变字段，服务器权威，本地缓存默认0=离线
/// </summary>
public static class FriendData
{
    public const string KeyFriends = "ARPG_FriendList";
    public const string KeyRequests = "ARPG_FriendRequests";
    public const string KeyRecent = "ARPG_RecentPlayers";
    public const string KeyBlacklist = "ARPG_FriendBlacklist";
    public const string KeyTeamId = "ARPG_TeamId";
    public const string KeyTeamLeader = "ARPG_TeamLeader";
    public const string KeyGiftDate = "ARPG_FriendGiftDate";

    [System.Serializable]
    public class FriendArray { public FriendUI.FriendInfo[] friends; }
    [System.Serializable]
    public class RequestArray { public FriendUI.FriendRequestData[] requests; }
    [System.Serializable]
    public class RecentArray { public FriendUI.RecentPlayerData[] players; }
    [System.Serializable]
    public class BlacklistArray { public string[] names; }

    // ====== 好友列表 ======
    public static List<FriendUI.FriendInfo> GetFriends()
    {
        var result = new List<FriendUI.FriendInfo>();
        string json = PlayerPrefs.GetString(KeyFriends, "");
        if (!string.IsNullOrEmpty(json))
        {
            try { var arr = JsonUtility.FromJson<FriendArray>(json); if (arr?.friends != null) result = new List<FriendUI.FriendInfo>(arr.friends); }
            catch { }
        }
        return result;
    }

    /// <summary>保存好友列表 — status强制设为0(离线)，在线状态不持久化</summary>
    public static void SaveFriends(List<FriendUI.FriendInfo> friends)
    {
        var toSave = new List<FriendUI.FriendInfo>(friends);
        for (int i = 0; i < toSave.Count; i++)
        {
            var fd = toSave[i];
            fd.status = 0;
            toSave[i] = fd;
        }
        PlayerPrefs.SetString(KeyFriends, JsonUtility.ToJson(new FriendArray { friends = toSave.ToArray() }));
        PlayerPrefs.Save();
    }

    // ====== 好友申请 ======
    public static List<FriendUI.FriendRequestData> GetRequests()
    {
        var result = new List<FriendUI.FriendRequestData>();
        string json = PlayerPrefs.GetString(KeyRequests, "");
        if (!string.IsNullOrEmpty(json))
        {
            try { var arr = JsonUtility.FromJson<RequestArray>(json); if (arr?.requests != null) result = new List<FriendUI.FriendRequestData>(arr.requests); }
            catch { }
        }
        return result;
    }

    public static void SaveRequests(List<FriendUI.FriendRequestData> requests)
    {
        PlayerPrefs.SetString(KeyRequests, JsonUtility.ToJson(new RequestArray { requests = requests.ToArray() }));
        PlayerPrefs.Save();
    }

    // ====== 最近一起玩 ======
    public static List<FriendUI.RecentPlayerData> GetRecentPlayers()
    {
        var result = new List<FriendUI.RecentPlayerData>();
        string json = PlayerPrefs.GetString(KeyRecent, "");
        if (!string.IsNullOrEmpty(json))
        {
            try { var arr = JsonUtility.FromJson<RecentArray>(json); if (arr?.players != null) result = new List<FriendUI.RecentPlayerData>(arr.players); }
            catch { }
        }
        return result;
    }

    public static void SaveRecentPlayers(List<FriendUI.RecentPlayerData> players)
    {
        PlayerPrefs.SetString(KeyRecent, JsonUtility.ToJson(new RecentArray { players = players.ToArray() }));
        PlayerPrefs.Save();
    }

    // ====== 黑名单 ======
    public static List<string> GetBlacklist()
    {
        var result = new List<string>();
        string json = PlayerPrefs.GetString(KeyBlacklist, "");
        if (!string.IsNullOrEmpty(json))
        {
            try { var arr = JsonUtility.FromJson<BlacklistArray>(json); if (arr?.names != null) result = new List<string>(arr.names); }
            catch { }
        }
        return result;
    }

    public static void SaveBlacklist(List<string> names)
    {
        PlayerPrefs.SetString(KeyBlacklist, JsonUtility.ToJson(new BlacklistArray { names = names.ToArray() }));
        PlayerPrefs.Save();
    }

    // ====== 组队状态 ======
    public static bool InTeam => PlayerPrefs.HasKey(KeyTeamId);
    public static bool IsTeamLeader => PlayerPrefs.GetInt(KeyTeamLeader, 0) == 1;

    public static void CreateTeam(string teamId)
    {
        PlayerPrefs.SetString(KeyTeamId, teamId);
        PlayerPrefs.SetInt(KeyTeamLeader, 1);
        PlayerPrefs.Save();
    }

    public static void JoinTeam(string teamId)
    {
        PlayerPrefs.SetString(KeyTeamId, teamId);
        PlayerPrefs.SetInt(KeyTeamLeader, 0);
        PlayerPrefs.Save();
    }

    public static void LeaveTeam()
    {
        PlayerPrefs.DeleteKey(KeyTeamId);
        PlayerPrefs.DeleteKey(KeyTeamLeader);
        PlayerPrefs.Save();
    }

    public static void DisbandTeam()
    {
        PlayerPrefs.DeleteKey(KeyTeamId);
        PlayerPrefs.DeleteKey(KeyTeamLeader);
        PlayerPrefs.Save();
    }

    // ====== 好友礼物 ======
    /// <summary>今天是否已赠送礼物</summary>
    public static bool GiftSentToday
    {
        get => PlayerPrefs.GetString(KeyGiftDate, "") == System.DateTime.Now.ToString("yyyyMMdd");
    }

    /// <summary>标记今天已赠送礼物</summary>
    public static void MarkGiftSent()
    {
        PlayerPrefs.SetString(KeyGiftDate, System.DateTime.Now.ToString("yyyyMMdd"));
        PlayerPrefs.Save();
    }
}
