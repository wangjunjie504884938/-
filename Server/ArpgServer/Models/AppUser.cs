using System.ComponentModel.DataAnnotations;

namespace ArpgServer.Models;

/// <summary>
/// 用户表 — 存储账号信息
/// </summary>
public class AppUser
{
    public int Id { get; set; }

    [Required]
    [MaxLength(32)]
    public string Username { get; set; } = string.Empty;   // 用户名 (唯一)

    [Required]
    public string PasswordHash { get; set; } = string.Empty; // BCrypt 哈希后的密码

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;  // 注册时间
    public DateTime? LastLoginAt { get; set; }                  // 最后登录时间

    // 一对多：一个用户最多3个角色存档
    public List<PlayerSave> Saves { get; set; } = new();
}

/// <summary>
/// 玩家存档表 — 每用户最多3个角色存档
/// 唯一约束: (UserId, Slot)
/// </summary>
public class PlayerSave
{
    public int Id { get; set; }
    public int UserId { get; set; }       // 外键 → AppUser.Id
    public int Slot { get; set; }         // 角色槽位 0-2

    /// <summary>
    /// 全量存档 JSON (包含等级/金币/装备/技能/成就等所有数据)
    /// </summary>
    public string SaveJson { get; set; } = "{}";

    // 摘要列 — 用于角色选择列表, 避免解析 JSON
    public int ClassType { get; set; }    // 职业 (0=战士, 1=法师, 2=牧师)
    public int Level { get; set; }        // 等级
    public int HighestStageCleared { get; set; } // 最高通关关卡

    /// <summary>服务端权威金币 — 用于原子操作(任务/签到/抽卡/邮件)</summary>
    public int Gold { get; set; }

    /// <summary>数据版本号 — 每次修改递增，用于乐观锁防覆盖</summary>
    public int DataVersion { get; set; } = 1;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

// ========== 请求/响应 DTO ==========

/// <summary>注册/登录请求</summary>
public class AuthRequest
{
    [Required]
    [MaxLength(32)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    [MaxLength(64)]
    public string Password { get; set; } = string.Empty;
}

/// <summary>注册/登录响应 — 返回 JWT Token</summary>
public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
}

// UploadSaveRequest 和 LoadSaveResponse 已移至 SaveController.cs (ArpgServer.Controllers 命名空间)
// 此处保留的旧定义已删除以避免歧义
