using System.ComponentModel.DataAnnotations;

namespace ArpgServer.Models;

/// <summary>
/// 每日任务表 — 每用户每天刷新3个任务
/// </summary>
public class DailyTask
{
    public int Id { get; set; }
    public int UserId { get; set; }
    /// <summary>任务日期(yyyy-MM-dd)</summary>
    public string TaskDate { get; set; } = "";
    /// <summary>任务类型: 0=击杀N个敌人 1=通关N次副本 2=消耗N金币 3=强化N次装备 4=获得N经验</summary>
    public int TaskType { get; set; }
    /// <summary>目标值</summary>
    public int TargetValue { get; set; }
    /// <summary>当前进度</summary>
    public int CurrentValue { get; set; }
    /// <summary>奖励金币</summary>
    public int RewardGold { get; set; }
    /// <summary>是否已领取</summary>
    public bool Claimed { get; set; }
}

/// <summary>
/// 签到记录表 — 每7天一个周期
/// </summary>
public class SignInRecord
{
    public int Id { get; set; }
    public int UserId { get; set; }
    /// <summary>签到周期起始日期(yyyy-MM-dd)</summary>
    public string CycleStart { get; set; } = "";
    /// <summary>本周已签到天数(0-7)</summary>
    public int SignedDays { get; set; }
    /// <summary>最后签到日期</summary>
    public string LastSignDate { get; set; } = "";
}

/// <summary>
/// 排行榜表 — 无尽模式成绩
/// </summary>
public class LeaderboardEntry
{
    public int Id { get; set; }
    public int UserId { get; set; }
    /// <summary>用户名(冗余存储,避免JOIN)</summary>
    public string Username { get; set; } = "";
    /// <summary>职业</summary>
    public int ClassType { get; set; }
    /// <summary>无尽模式波数</summary>
    public int Wave { get; set; }
    /// <summary>角色等级</summary>
    public int Level { get; set; }
    /// <summary>提交时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 竞技场排行榜表 — 竞技场对战成绩
/// </summary>
public class ArenaLeaderboardEntry
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = "";
    public int ClassType { get; set; }
    /// <summary>竞技场胜场数</summary>
    public int Wins { get; set; }
    /// <summary>竞技场总场次</summary>
    public int TotalGames { get; set; }
    /// <summary>战力值</summary>
    public int GearScore { get; set; }
    /// <summary>防守分</summary>
    public int DefenseRating { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 抽卡记录表 — 记录用户抽卡历史
/// </summary>
public class GachaRecord
{
    public int Id { get; set; }
    public int UserId { get; set; }
    /// <summary>抽卡类型: 0=普通(金币) 1=高级(钻石)</summary>
    public int GachaType { get; set; }
    /// <summary>本次抽到的稀有度</summary>
    public int Rarity { get; set; }
    /// <summary>高级抽卡累计次数(保底计数) — 仅高级保留, 普通为0</summary>
    public int PityCounter { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 抽卡保底计数表 — 每用户每类型一行, 原子更新保底计数
/// </summary>
public class GachaPity
{
    public int Id { get; set; }
    public int UserId { get; set; }
    /// <summary>抽卡类型(0=普通 1=高级)</summary>
    public int GachaType { get; set; }
    /// <summary>当前保底累计次数</summary>
    public int PityCounter { get; set; }
}

/// <summary>
/// 邮件表 — 系统邮件/奖励/补偿
/// </summary>
public class Mail
{
    public int Id { get; set; }
    public int UserId { get; set; }
    /// <summary>邮件标题</summary>
    public string Title { get; set; } = "";
    /// <summary>邮件内容</summary>
    public string Content { get; set; } = "";
    /// <summary>附件金币</summary>
    public int AttachmentGold { get; set; }
    /// <summary>附件物品JSON</summary>
    public string AttachmentItems { get; set; } = "";
    /// <summary>是否已读</summary>
    public bool IsRead { get; set; }
    /// <summary>是否已领取附件</summary>
    public bool Claimed { get; set; }
    /// <summary>发送时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>过期时间(null=永不过期)</summary>
    public DateTime? ExpireAt { get; set; }
}

// ========== 好友系统 ==========

/// <summary>
/// 好友表 — 玩家间好友关系
/// </summary>
public class Friend
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int FriendId { get; set; }
    /// <summary>状态: 0=好友, 1=拉黑</summary>
    public int Status { get; set; } = 0;
    /// <summary>备注名</summary>
    public string Remark { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 好友申请表
/// </summary>
public class FriendRequest
{
    public int Id { get; set; }
    public int FromUserId { get; set; }
    public int ToUserId { get; set; }
    /// <summary>申请留言</summary>
    public string Message { get; set; } = "";
    /// <summary>状态: 0=待处理, 1=已接受, 2=已拒绝</summary>
    public int Status { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 最近一起玩的玩家记录
/// </summary>
public class RecentPlayer
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int OtherUserId { get; set; }
    /// <summary>组队次数</summary>
    public int PlayCount { get; set; } = 1;
    public DateTime LastPlayedAt { get; set; } = DateTime.UtcNow;
}

// ========== 公会系统 ==========

/// <summary>
/// 公会表
/// </summary>
public class Guild
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int LeaderUserId { get; set; }
    public int Level { get; set; } = 1;
    public int Exp { get; set; } = 0;
    public string Announce { get; set; } = "欢迎加入公会！";
    public int MaxMembers { get; set; } = 30;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 公会成员表
/// </summary>
public class GuildMember
{
    public int Id { get; set; }
    public int GuildId { get; set; }
    public int UserId { get; set; }
    public int Rank { get; set; } = 3; // 0=会长 1=副会长 2=长老 3=成员
    public int Contribution { get; set; } = 0;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 公会申请/邀请表 — Type=0为邀请(会长邀请玩家), Type=1为申请(玩家申请加入)
/// </summary>
public class GuildRequest
{
    public int Id { get; set; }
    public int GuildId { get; set; }
    public int FromUserId { get; set; }
    public int ToUserId { get; set; }
    public int Type { get; set; } = 0; // 0=邀请 1=申请加入
    public int Status { get; set; } = 0; // 0=待处理 1=已同意 2=已拒绝
    public string Message { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
