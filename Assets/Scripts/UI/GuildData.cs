using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 公会数据访问层 — 从GuildUI提取
/// 集中管理公会相关的PlayerPrefs读写，UI层不直接操作存储
/// </summary>
public static class GuildData
{
    /// <summary>公会成员数据结构</summary>
    [System.Serializable]
    public struct GuildMember
    {
        public string name;
        public int cls;
        public int level;
        public int contribution;
        public int rank;
        public bool online;
    }

    [System.Serializable]
    private class MemberArray { public GuildMember[] members; }

    // ====== PlayerPrefs Keys ======
    public const string KeyGuildName = "ARPG_GuildName";
    public const string KeyGuildLevel = "ARPG_GuildLevel";
    public const string KeyGuildExp = "ARPG_GuildExp";
    public const string KeyGuildAnnounce = "ARPG_GuildAnnounce";
    public const string KeyContribution = "ARPG_GuildContribution";
    public const string KeyPlayerRank = "ARPG_GuildPlayerRank";
    public const string KeyDonateDate = "ARPG_GuildDonateDate";
    public const string KeyDonateCount = "ARPG_GuildDonateCount";
    public const string KeyBossDate = "ARPG_GuildBossDate";
    public const string KeyBossHP = "ARPG_GuildBossHP";
    public const string KeyBossMaxHP = "ARPG_GuildBossMaxHP";
    public const string KeyBossDamage = "ARPG_GuildBossDamage";
    public const string KeyBossDefeated = "ARPG_GuildBossDefeated";
    public const string KeyBossLevel = "ARPG_GuildBossLevel";
    public const string KeyGuildMembers = "ARPG_GuildMembers";
    public const string KeyShopPurchased = "ARPG_GuildShopPurchased";

    // ====== 只读属性 ======
    public static bool HasGuild => PlayerPrefs.HasKey(KeyGuildName);
    public static string GuildName => PlayerPrefs.GetString(KeyGuildName, "");
    public static int GuildLevel => PlayerPrefs.GetInt(KeyGuildLevel, 1);
    public static int GuildExp => PlayerPrefs.GetInt(KeyGuildExp, 0);
    public static int Contribution => PlayerPrefs.GetInt(KeyContribution, 0);
    /// <summary>玩家在公会中的职位 (0=会长 1=副会长 2=长老 3=成员)</summary>
    public static int PlayerRank => PlayerPrefs.GetInt(KeyPlayerRank, 0);
    public static string GuildAnnounce => PlayerPrefs.GetString(KeyGuildAnnounce, "欢迎来到公会！一起努力变强吧！");
    public static int GuildMaxMembers => 10 + GuildLevel * 5;
    public static int GuildExpToNext => 1000 + GuildLevel * 500;

    // ====== 写操作 ======
    public static void SetGuild(string name, int level, int rank)
    {
        PlayerPrefs.SetString(KeyGuildName, name);
        PlayerPrefs.SetInt(KeyGuildLevel, level);
        PlayerPrefs.SetInt(KeyPlayerRank, rank);
        PlayerPrefs.Save();
    }

    public static void LeaveGuild()
    {
        PlayerPrefs.DeleteKey(KeyGuildName);
        PlayerPrefs.DeleteKey(KeyGuildLevel);
        PlayerPrefs.DeleteKey(KeyGuildExp);
        PlayerPrefs.DeleteKey(KeyGuildAnnounce);
        PlayerPrefs.DeleteKey(KeyContribution);
        PlayerPrefs.DeleteKey(KeyPlayerRank);
        PlayerPrefs.DeleteKey(KeyGuildMembers);
        PlayerPrefs.DeleteKey(KeyBossDate);
        PlayerPrefs.DeleteKey(KeyBossHP);
        PlayerPrefs.DeleteKey(KeyBossMaxHP);
        PlayerPrefs.DeleteKey(KeyBossDamage);
        PlayerPrefs.DeleteKey(KeyBossDefeated);
        PlayerPrefs.DeleteKey(KeyBossLevel);
        PlayerPrefs.DeleteKey(KeyShopPurchased);
        PlayerPrefs.Save();
    }

    public static void AddGuildExp(int exp)
    {
        int newExp = GuildExp + exp;
        int newLevel = GuildLevel;
        while (newExp >= 1000 + newLevel * 500)
        {
            newExp -= 1000 + newLevel * 500;
            newLevel++;
        }
        PlayerPrefs.SetInt(KeyGuildExp, newExp);
        PlayerPrefs.SetInt(KeyGuildLevel, newLevel);
        PlayerPrefs.Save();
    }

    public static void AddContribution(int amount)
    {
        PlayerPrefs.SetInt(KeyContribution, Contribution + amount);
        PlayerPrefs.Save();
    }

    // ====== 成员管理 ======
    public static List<GuildMember> GetMembers()
    {
        var result = new List<GuildMember>();
        string json = PlayerPrefs.GetString(KeyGuildMembers, "");
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                var arr = JsonUtility.FromJson<MemberArray>(json);
                if (arr != null && arr.members != null)
                    result = new List<GuildMember>(arr.members);
            }
            catch { }
        }
        return result;
    }

    public static void SaveMembers(List<GuildMember> members)
    {
        var arr = new MemberArray { members = members.ToArray() };
        PlayerPrefs.SetString(KeyGuildMembers, JsonUtility.ToJson(arr));
        PlayerPrefs.Save();
    }
}
