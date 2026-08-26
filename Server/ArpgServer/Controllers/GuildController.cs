using System.Security.Claims;
using ArpgServer.Data;
using ArpgServer.Models;
using ArpgServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArpgServer.Controllers;

/// <summary>
/// 公会系统控制器 — 公会创建/列表/申请/邀请/成员管理
/// 路由: /api/guild/*
/// 所有接口需要 JWT 认证
/// </summary>
[ApiController]
[Route("api/guild")]
[Authorize]
public class GuildController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly RedisCacheService _cache;

    public GuildController(AppDbContext db, RedisCacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    private int GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.Parse(claim!);
    }

    private string GetUsername()
    {
        return User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
    }

    // ========================================================
    //  创建公会
    // ========================================================

    /// <summary>
    /// 创建公会(会长自动加入)
    /// POST /api/guild/create
    /// Body: { "name": "公会名", "announce": "公告" }
    /// </summary>
    [HttpPost("create")]
    public async Task<IActionResult> CreateGuild([FromBody] CreateGuildRequest req)
    {
        int userId = GetUserId();

        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { error = "公会名称不能为空" });

        if (req.Name.Length > 32)
            return BadRequest(new { error = "公会名称最长32个字符" });

        // 检查是否已加入公会
        var existingMember = await _db.GuildMembers.FirstOrDefaultAsync(m => m.UserId == userId);
        if (existingMember != null)
            return Ok(new { success = false, error = "你已加入公会，请先退出" });

        // 检查公会名称是否被占用
        var existingGuild = await _db.Guilds.FirstOrDefaultAsync(g => g.Name == req.Name);
        if (existingGuild != null)
            return Ok(new { success = false, error = "公会名称已被占用" });

        // 检查是否已创建过公会(LeaderUserId唯一索引)
        var existingLeader = await _db.Guilds.FirstOrDefaultAsync(g => g.LeaderUserId == userId);
        if (existingLeader != null)
            return Ok(new { success = false, error = "你已创建过公会" });

        // 创建公会
        var guild = new Guild
        {
            Name = req.Name,
            LeaderUserId = userId,
            Announce = string.IsNullOrWhiteSpace(req.Announce) ? "欢迎加入公会！" : req.Announce,
        };
        _db.Guilds.Add(guild);
        await _db.SaveChangesAsync();

        // 会长自动加入
        _db.GuildMembers.Add(new GuildMember
        {
            GuildId = guild.Id,
            UserId = userId,
            Rank = 0, // 会长
        });
        await _db.SaveChangesAsync();

        // 删除缓存
        if (_cache.IsAvailable)
            await _cache.RemoveAsync($"guild:my:{userId}");

        return Ok(new
        {
            success = true,
            guild = new
            {
                id = guild.Id,
                name = guild.Name,
                leaderUserId = guild.LeaderUserId,
                level = guild.Level,
                announce = guild.Announce,
                maxMembers = guild.MaxMembers,
                createdAt = guild.CreatedAt,
            },
        });
    }

    // ========================================================
    //  公会列表(分页)
    // ========================================================

    /// <summary>
    /// 获取公会列表(分页)
    /// GET /api/guild/list?page=1&pageSize=20
    /// </summary>
    [HttpGet("list")]
    public async Task<ActionResult<object>> GetGuildList([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var query = _db.Guilds.OrderByDescending(g => g.Level).ThenByDescending(g => g.CreatedAt);

        var total = await query.CountAsync();
        var guilds = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // 批量获取成员数
        var guildIds = guilds.Select(g => g.Id).ToList();
        var memberCounts = await _db.GuildMembers
            .Where(m => guildIds.Contains(m.GuildId))
            .GroupBy(m => m.GuildId)
            .Select(g => new { GuildId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.GuildId, x => x.Count);

        // 批量获取会长名
        var leaderIds = guilds.Select(g => g.LeaderUserId).Distinct().ToList();
        var leaders = await _db.Users
            .Where(u => leaderIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        var result = guilds.Select(g => new
        {
            id = g.Id,
            name = g.Name,
            leaderUserId = g.LeaderUserId,
            leaderName = leaders.TryGetValue(g.LeaderUserId, out var u) ? u.Username : "Unknown",
            level = g.Level,
            memberCount = memberCounts.TryGetValue(g.Id, out var c) ? c : 0,
            maxMembers = g.MaxMembers,
            announce = g.Announce,
            createdAt = g.CreatedAt,
        }).ToList();

        return Ok(new { guilds = result, total, page, pageSize });
    }

    // ========================================================
    //  我的公会信息+成员列表
    // ========================================================

    /// <summary>
    /// 获取我的公会信息+成员列表
    /// GET /api/guild/my
    /// </summary>
    [HttpGet("my")]
    public async Task<ActionResult<object>> GetMyGuild()
    {
        int userId = GetUserId();

        // Redis缓存优先
        string cacheKey = $"guild:my:{userId}";
        if (_cache.IsAvailable)
        {
            var cached = await _cache.GetAsync<string>(cacheKey);
            if (!string.IsNullOrEmpty(cached))
                return Ok(System.Text.Json.JsonSerializer.Deserialize<object>(cached));
        }

        var member = await _db.GuildMembers.FirstOrDefaultAsync(m => m.UserId == userId);
        if (member == null)
            return Ok(new { guild = (object?)null, message = "你还没有加入公会" });

        var guild = await _db.Guilds.FirstOrDefaultAsync(g => g.Id == member.GuildId);
        if (guild == null)
            return Ok(new { guild = (object?)null, message = "公会不存在" });

        // 获取所有成员
        var members = await _db.GuildMembers
            .Where(m => m.GuildId == guild.Id)
            .OrderBy(m => m.Rank)
            .ThenBy(m => m.JoinedAt)
            .ToListAsync();

        // 关联用户名
        var userIds = members.Select(m => m.UserId).Distinct().ToList();
        var users = await _db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id);

        var memberList = members.Select(m => new
        {
            userId = m.UserId,
            username = users.TryGetValue(m.UserId, out var u) ? u.Username : "Unknown",
            rank = m.Rank,
            rankName = m.Rank switch
            {
                0 => "会长",
                1 => "副会长",
                2 => "长老",
                _ => "成员",
            },
            contribution = m.Contribution,
            joinedAt = m.JoinedAt,
        }).ToList();

        var result = new
        {
            guild = new
            {
                id = guild.Id,
                name = guild.Name,
                leaderUserId = guild.LeaderUserId,
                level = guild.Level,
                exp = guild.Exp,
                announce = guild.Announce,
                maxMembers = guild.MaxMembers,
                memberCount = members.Count,
                createdAt = guild.CreatedAt,
            },
            myRank = member.Rank,
            members = memberList,
        };

        // 写入缓存
        if (_cache.IsAvailable)
            await _cache.SetAsync(cacheKey, System.Text.Json.JsonSerializer.Serialize(result), TimeSpan.FromMinutes(10));

        return Ok(result);
    }

    // ========================================================
    //  申请加入公会
    // ========================================================

    /// <summary>
    /// 申请加入公会(Type=1, ToUserId=公会会长)
    /// POST /api/guild/join/{guildId}
    /// Body: { "message": "请让我加入" }
    /// </summary>
    [HttpPost("join/{guildId}")]
    public async Task<IActionResult> JoinGuild(int guildId, [FromBody] JoinGuildRequest? req)
    {
        int userId = GetUserId();

        // 检查是否已加入公会
        var existingMember = await _db.GuildMembers.FirstOrDefaultAsync(m => m.UserId == userId);
        if (existingMember != null)
            return Ok(new { success = false, error = "你已加入公会，请先退出" });

        // 检查公会是否存在
        var guild = await _db.Guilds.FirstOrDefaultAsync(g => g.Id == guildId);
        if (guild == null)
            return NotFound(new { error = "公会不存在" });

        // 检查是否已是成员
        var alreadyMember = await _db.GuildMembers.FirstOrDefaultAsync(m =>
            m.GuildId == guildId && m.UserId == userId);
        if (alreadyMember != null)
            return Ok(new { success = false, error = "你已经是该公会成员" });

        // 检查是否已有待处理申请
        var pendingReq = await _db.GuildRequests.FirstOrDefaultAsync(r =>
            r.GuildId == guildId && r.FromUserId == userId && r.Type == 1 && r.Status == 0);
        if (pendingReq != null)
            return Ok(new { success = false, error = "已发送过申请，等待公会审批" });

        // 检查公会是否已满
        int memberCount = await _db.GuildMembers.CountAsync(m => m.GuildId == guildId);
        if (memberCount >= guild.MaxMembers)
            return Ok(new { success = false, error = "公会成员已满" });

        // 创建申请(Type=1, ToUserId=公会会长)
        _db.GuildRequests.Add(new GuildRequest
        {
            GuildId = guildId,
            FromUserId = userId,
            ToUserId = guild.LeaderUserId,
            Type = 1, // 申请加入
            Status = 0,
            Message = req?.Message ?? "",
        });
        await _db.SaveChangesAsync();

        // 删除缓存
        if (_cache.IsAvailable)
            await _cache.RemoveAsync($"guild:requests:{guildId}");

        return Ok(new { success = true });
    }

    // ========================================================
    //  邀请玩家加入公会
    // ========================================================

    /// <summary>
    /// 邀请玩家加入公会(Type=0, ToUserId=目标玩家)
    /// POST /api/guild/invite/{userId}
    /// Body: { "message": "来我们公会吧" }
    /// </summary>
    [HttpPost("invite/{userId}")]
    public async Task<IActionResult> InvitePlayer(int userId, [FromBody] InviteGuildRequest? req)
    {
        int myUserId = GetUserId();

        // 检查自己是否在公会中
        var myMember = await _db.GuildMembers.FirstOrDefaultAsync(m => m.UserId == myUserId);
        if (myMember == null)
            return Ok(new { success = false, error = "你还没有加入公会" });

        // 会长(0)、副会长(1)、长老(2)可以邀请
        if (myMember.Rank > 2)
            return Ok(new { success = false, error = "只有会长、副会长或长老可以邀请玩家" });

        var guild = await _db.Guilds.FirstOrDefaultAsync(g => g.Id == myMember.GuildId);
        if (guild == null)
            return NotFound(new { error = "公会不存在" });

        // 检查目标用户是否存在
        var targetUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (targetUser == null)
            return NotFound(new { error = "目标用户不存在" });

        // 不能邀请自己
        if (userId == myUserId)
            return BadRequest(new { error = "不能邀请自己" });

        // 检查目标是否已加入公会
        var existingMember = await _db.GuildMembers.FirstOrDefaultAsync(m => m.UserId == userId);
        if (existingMember != null)
            return Ok(new { success = false, error = "该玩家已加入公会" });

        // 检查是否已有待处理邀请
        var pendingInvite = await _db.GuildRequests.FirstOrDefaultAsync(r =>
            r.GuildId == guild.Id && r.ToUserId == userId && r.Type == 0 && r.Status == 0);
        if (pendingInvite != null)
            return Ok(new { success = false, error = "已发送过邀请，等待玩家确认" });

        // 检查公会是否已满
        int memberCount = await _db.GuildMembers.CountAsync(m => m.GuildId == guild.Id);
        if (memberCount >= guild.MaxMembers)
            return Ok(new { success = false, error = "公会成员已满" });

        // 创建邀请(Type=0)
        _db.GuildRequests.Add(new GuildRequest
        {
            GuildId = guild.Id,
            FromUserId = myUserId,
            ToUserId = userId,
            Type = 0, // 邀请
            Status = 0,
            Message = req?.Message ?? "",
        });
        await _db.SaveChangesAsync();

        // 删除缓存
        if (_cache.IsAvailable)
            await _cache.RemoveAsync($"guild:invites:{userId}");

        return Ok(new { success = true });
    }

    // ========================================================
    //  获取我收到的公会邀请
    // ========================================================

    /// <summary>
    /// 获取我收到的公会邀请
    /// GET /api/guild/invites
    /// </summary>
    [HttpGet("invites")]
    public async Task<ActionResult<object>> GetMyInvites()
    {
        int userId = GetUserId();

        // 24小时自动过期
        var expiryTime = DateTime.UtcNow.AddHours(-24);
        var expiredRequests = await _db.GuildRequests
            .Where(r => r.ToUserId == userId && r.Type == 0 && r.Status == 0 && r.CreatedAt < expiryTime)
            .ToListAsync();
        if (expiredRequests.Count > 0)
        {
            foreach (var r in expiredRequests)
            {
                r.Status = 3; // 过期
                r.UpdatedAt = DateTime.UtcNow;
            }
            await _db.SaveChangesAsync();
            if (_cache.IsAvailable)
                await _cache.RemoveAsync($"guild:invites:{userId}");
        }

        var invites = await _db.GuildRequests
            .Where(r => r.ToUserId == userId && r.Type == 0 && r.Status == 0)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        // 关联公会名和邀请人名
        var guildIds = invites.Select(r => r.GuildId).Distinct().ToList();
        var fromUserIds = invites.Select(r => r.FromUserId).Distinct().ToList();

        var guilds = await _db.Guilds
            .Where(g => guildIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id);
        var users = await _db.Users
            .Where(u => fromUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        var result = invites.Select(r => new
        {
            id = r.Id,
            guildId = r.GuildId,
            guildName = guilds.TryGetValue(r.GuildId, out var g) ? g.Name : "Unknown",
            guildLevel = guilds.TryGetValue(r.GuildId, out var g2) ? g2.Level : 0,
            fromUserId = r.FromUserId,
            fromName = users.TryGetValue(r.FromUserId, out var u) ? u.Username : "Unknown",
            message = r.Message,
            createdAt = r.CreatedAt,
        }).ToList();

        return Ok(new { invites = result, count = result.Count });
    }

    // ========================================================
    //  获取未处理邀请数量(红点用)
    // ========================================================

    /// <summary>
    /// 获取未处理邀请数量(红点用)
    /// GET /api/guild/invite/count
    /// </summary>
    [HttpGet("invite/count")]
    public async Task<ActionResult<object>> GetInviteCount()
    {
        int userId = GetUserId();

        string cacheKey = $"guild:invite:count:{userId}";
        if (_cache.IsAvailable)
        {
            var cached = await _cache.GetAsync<string>(cacheKey);
            if (!string.IsNullOrEmpty(cached) && int.TryParse(cached, out int cachedCount))
                return Ok(new { count = cachedCount });
        }

        // 过期24小时的视为已过期
        var expiryTime = DateTime.UtcNow.AddHours(-24);
        int count = await _db.GuildRequests
            .CountAsync(r => r.ToUserId == userId && r.Type == 0 && r.Status == 0 && r.CreatedAt >= expiryTime);

        if (_cache.IsAvailable)
            await _cache.SetAsync(cacheKey, count.ToString(), TimeSpan.FromMinutes(5));

        return Ok(new { count });
    }

    // ========================================================
    //  获取公会的加入申请(会长/副会长可见)
    // ========================================================

    /// <summary>
    /// 获取我公会的加入申请(会长/副会长可见)
    /// GET /api/guild/requests
    /// </summary>
    [HttpGet("requests")]
    public async Task<ActionResult<object>> GetGuildRequests()
    {
        int userId = GetUserId();

        // 检查用户是否在公会中
        var myMember = await _db.GuildMembers.FirstOrDefaultAsync(m => m.UserId == userId);
        if (myMember == null)
            return Ok(new { requests = Array.Empty<object>(), count = 0, message = "你还没有加入公会" });

        // 会长(0)、副会长(1)、长老(2)可以查看
        if (myMember.Rank > 2)
            return Ok(new { requests = Array.Empty<object>(), count = 0, message = "只有会长、副会长或长老可以查看申请" });

        // 24小时自动过期
        var expiryTime = DateTime.UtcNow.AddHours(-24);
        var expiredRequests = await _db.GuildRequests
            .Where(r => r.GuildId == myMember.GuildId && r.Type == 1 && r.Status == 0 && r.CreatedAt < expiryTime)
            .ToListAsync();
        if (expiredRequests.Count > 0)
        {
            foreach (var r in expiredRequests) { r.Status = 3; r.UpdatedAt = DateTime.UtcNow; }
            await _db.SaveChangesAsync();
        }

        var requests = await _db.GuildRequests
            .Where(r => r.GuildId == myMember.GuildId && r.Type == 1 && r.Status == 0)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        // 关联用户名
        var fromUserIds = requests.Select(r => r.FromUserId).Distinct().ToList();
        var users = await _db.Users
            .Where(u => fromUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        var result = requests.Select(r => new
        {
            id = r.Id,
            fromUserId = r.FromUserId,
            fromName = users.TryGetValue(r.FromUserId, out var u) ? u.Username : "Unknown",
            message = r.Message,
            createdAt = r.CreatedAt,
        }).ToList();

        return Ok(new { requests = result, count = result.Count });
    }

    // ========================================================
    //  同意/拒绝 邀请或申请
    // ========================================================

    /// <summary>
    /// 同意/拒绝邀请或申请
    /// POST /api/guild/respond/{requestId}
    /// Body: { "accept": true/false }
    /// 
    /// Type=0(邀请): 玩家响应自己收到的邀请，同意则加入公会(rank=3)
    /// Type=1(申请): 会长/副会长响应加入申请，同意则申请者加入公会(rank=3)
    /// </summary>
    [HttpPost("respond/{requestId}")]
    public async Task<IActionResult> RespondToRequest(int requestId, [FromBody] RespondGuildRequest req)
    {
        int userId = GetUserId();

        var guildReq = await _db.GuildRequests.FirstOrDefaultAsync(r => r.Id == requestId && r.Status == 0);
        if (guildReq == null)
            return NotFound(new { error = "申请/邀请不存在或已处理" });

        int newMemberUserId; // 将要加入公会的用户

        if (guildReq.Type == 0)
        {
            // 邀请 — 当前用户(被邀请者)响应
            if (guildReq.ToUserId != userId)
                return Forbid();

            newMemberUserId = userId;

            // 检查是否已加入公会
            var existingMember = await _db.GuildMembers.FirstOrDefaultAsync(m => m.UserId == userId);
            if (existingMember != null)
                return Ok(new { success = false, error = "你已加入公会" });
        }
        else
        {
            // 申请 — 会长/副会长/长老响应
            var myMember = await _db.GuildMembers.FirstOrDefaultAsync(m => m.UserId == userId);
            if (myMember == null || myMember.GuildId != guildReq.GuildId || myMember.Rank > 2)
                return Forbid();

            newMemberUserId = guildReq.FromUserId;

            // 检查申请者是否已加入公会
            var existingMember = await _db.GuildMembers.FirstOrDefaultAsync(m => m.UserId == guildReq.FromUserId);
            if (existingMember != null)
                return Ok(new { success = false, error = "该玩家已加入公会" });
        }

        // 更新申请状态
        guildReq.Status = req.Accept ? 1 : 2;
        guildReq.UpdatedAt = DateTime.UtcNow;

        if (req.Accept)
        {
            // 检查公会是否已满
            var guild = await _db.Guilds.FirstOrDefaultAsync(g => g.Id == guildReq.GuildId);
            if (guild == null)
                return NotFound(new { error = "公会不存在" });

            int memberCount = await _db.GuildMembers.CountAsync(m => m.GuildId == guildReq.GuildId);
            if (memberCount >= guild.MaxMembers)
                return Ok(new { success = false, error = "公会成员已满" });

            // 添加成员(rank=3 普通成员)
            _db.GuildMembers.Add(new GuildMember
            {
                GuildId = guildReq.GuildId,
                UserId = newMemberUserId,
                Rank = 3,
            });
        }

        await _db.SaveChangesAsync();

        // 删除缓存
        if (_cache.IsAvailable)
        {
            await _cache.RemoveAsync($"guild:my:{userId}");
            await _cache.RemoveAsync($"guild:my:{newMemberUserId}");
            await _cache.RemoveAsync($"guild:invites:{newMemberUserId}");
            await _cache.RemoveAsync($"guild:requests:{guildReq.GuildId}");
        }

        return Ok(new { success = true, accepted = req.Accept });
    }

    // ========================================================
    //  离开公会
    // ========================================================

    /// <summary>
    /// 离开公会
    /// POST /api/guild/leave
    /// </summary>
    [HttpPost("leave")]
    public async Task<IActionResult> LeaveGuild()
    {
        int userId = GetUserId();

        var member = await _db.GuildMembers.FirstOrDefaultAsync(m => m.UserId == userId);
        if (member == null)
            return NotFound(new { error = "你还没有加入公会" });

        // 会长不能直接离开
        if (member.Rank == 0)
            return Ok(new { success = false, error = "会长不能直接离开，请先解散公会或转让会长" });

        int guildId = member.GuildId;

        _db.GuildMembers.Remove(member);
        await _db.SaveChangesAsync();

        // 删除缓存
        if (_cache.IsAvailable)
            await _cache.RemoveAsync($"guild:my:{userId}");

        return Ok(new { success = true });
    }

    // ========================================================
    //  解散公会(仅会长)
    // ========================================================

    /// <summary>
    /// 解散公会(仅会长)
    /// POST /api/guild/disband
    /// </summary>
    [HttpPost("disband")]
    public async Task<IActionResult> DisbandGuild()
    {
        int userId = GetUserId();

        var member = await _db.GuildMembers.FirstOrDefaultAsync(m => m.UserId == userId);
        if (member == null)
            return NotFound(new { error = "你还没有加入公会" });

        if (member.Rank != 0)
            return Forbid();

        var guild = await _db.Guilds.FirstOrDefaultAsync(g => g.Id == member.GuildId);
        if (guild == null)
            return NotFound(new { error = "公会不存在" });

        int guildId = guild.Id;

        // 获取所有成员的userId用于缓存清理
        var memberUserIds = await _db.GuildMembers
            .Where(m => m.GuildId == guildId)
            .Select(m => m.UserId)
            .ToListAsync();

        // 删除所有成员、申请、公会
        var members = await _db.GuildMembers.Where(m => m.GuildId == guildId).ToListAsync();
        var requests = await _db.GuildRequests.Where(r => r.GuildId == guildId).ToListAsync();

        _db.GuildMembers.RemoveRange(members);
        _db.GuildRequests.RemoveRange(requests);
        _db.Guilds.Remove(guild);
        await _db.SaveChangesAsync();

        // 删除缓存
        if (_cache.IsAvailable)
        {
            foreach (var uid in memberUserIds)
                await _cache.RemoveAsync($"guild:my:{uid}");
            await _cache.RemoveAsync($"guild:requests:{guildId}");
        }

        return Ok(new { success = true });
    }
}

// ========== DTO ==========

public class CreateGuildRequest
{
    public string Name { get; set; } = "";
    public string? Announce { get; set; }
}

public class JoinGuildRequest
{
    public string? Message { get; set; }
}

public class InviteGuildRequest
{
    public string? Message { get; set; }
}

public class RespondGuildRequest
{
    public bool Accept { get; set; }
}
