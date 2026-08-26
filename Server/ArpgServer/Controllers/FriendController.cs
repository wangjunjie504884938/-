using System.Security.Claims;
using ArpgServer.Data;
using ArpgServer.Models;
using ArpgServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArpgServer.Controllers;

/// <summary>
/// 好友系统控制器 — 好友列表/申请/删除/拉黑/最近一起玩
/// 路由: /api/friend/*
/// 所有接口需要 JWT 认证
/// </summary>
[ApiController]
[Route("api/friend")]
[Authorize]
public class FriendController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly RedisCacheService _cache;
    private const int MaxFriends = 50;

    public FriendController(AppDbContext db, RedisCacheService cache)
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
    //  好友列表
    // ========================================================

    /// <summary>
    /// 获取好友列表
    /// GET /api/friend/list
    /// </summary>
    [HttpGet("list")]
    public async Task<ActionResult<object>> GetFriendList()
    {
        int userId = GetUserId();

        // Redis缓存优先（不含online字段，online每次实时查询）
        string cacheKey = $"friend:list:v3:{userId}";
        List<CachedFriend>? cachedFriends = null;
        if (_cache.IsAvailable)
            cachedFriends = await _cache.GetAsync<List<CachedFriend>>(cacheKey);

        List<CachedFriend> friendData;
        if (cachedFriends != null && cachedFriends.Count > 0)
        {
            friendData = cachedFriends;
        }
        else
        {
            var friends = await _db.Friends
                .Where(f => f.UserId == userId && f.Status == 0)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();

            var friendUserIds = friends.Select(f => f.FriendId).Distinct().ToList();
            var friendUsers = await _db.Users
                .Where(u => friendUserIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id);
            var friendSaves = await _db.PlayerSaves
                .Where(s => friendUserIds.Contains(s.UserId))
                .GroupBy(s => s.UserId)
                .ToDictionaryAsync(g => g.Key, g => g.OrderByDescending(s => s.Level).First());

            friendData = friends.Select(f => new CachedFriend
            {
                friendId = f.FriendId,
                username = friendUsers.TryGetValue(f.FriendId, out var u) ? u.Username : "Unknown",
                level = friendSaves.TryGetValue(f.FriendId, out var s) ? s.Level : 0,
                classType = friendSaves.TryGetValue(f.FriendId, out var s2) ? s2.ClassType : 0,
                remark = f.Remark,
                createdAt = f.CreatedAt,
            }).ToList();

            // 缓存好友基础数据（不含online），5分钟过期
            if (_cache.IsAvailable)
                await _cache.SetAsync(cacheKey, friendData, TimeSpan.FromMinutes(5));
        }

        // 实时查询每个好友的在线状态
        var result = friendData.Select(f => new
        {
            friendId = f.friendId,
            username = f.username,
            level = f.level,
            classType = f.classType,
            online = _cache.IsAvailable && _cache.IsUserOnline(f.friendId).GetAwaiter().GetResult(),
            remark = f.remark,
            createdAt = f.createdAt,
        }).ToList();

        return Ok(new { friends = result, count = result.Count, max = MaxFriends });
    }

    // ========================================================
    //  发送好友申请
    // ========================================================

    /// <summary>
    /// 发送好友申请
    /// POST /api/friend/request
    /// Body: { "toUserId": 123, "message": "一起玩吧" }
    /// </summary>
    [HttpPost("request")]
    public async Task<IActionResult> SendRequest([FromBody] SendFriendRequest req)
    {
        int userId = GetUserId();

        // 如果未提供ToUserId，尝试通过用户名查找
        if (req.ToUserId == 0 && !string.IsNullOrWhiteSpace(req.TargetName))
        {
            var userByName = await _db.Users.FirstOrDefaultAsync(u => u.Username == req.TargetName);
            if (userByName == null)
                return NotFound(new { error = "用户不存在" });
            req.ToUserId = userByName.Id;
        }

        if (req.ToUserId == 0)
            return BadRequest(new { error = "请指定目标用户" });

        if (req.ToUserId == userId)
            return BadRequest(new { error = "不能添加自己为好友" });

        // 检查目标用户是否存在
        var targetUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == req.ToUserId);
        if (targetUser == null)
            return NotFound(new { error = "用户不存在" });

        // 检查是否已经是好友
        var existing = await _db.Friends.FirstOrDefaultAsync(f =>
            f.UserId == userId && f.FriendId == req.ToUserId && f.Status == 0);
        if (existing != null)
            return Ok(new { success = false, error = "已经是好友了" });

        // 检查好友上限
        int friendCount = await _db.Friends.CountAsync(f => f.UserId == userId && f.Status == 0);
        if (friendCount >= MaxFriends)
            return Ok(new { success = false, error = $"好友已达上限({MaxFriends})" });

        // 检查是否已有待处理申请
        var pendingReq = await _db.FriendRequests.FirstOrDefaultAsync(r =>
            r.FromUserId == userId && r.ToUserId == req.ToUserId && r.Status == 0);
        if (pendingReq != null)
            return Ok(new { success = false, error = "已发送过申请，等待对方确认" });

        // 创建申请
        _db.FriendRequests.Add(new FriendRequest
        {
            FromUserId = userId,
            ToUserId = req.ToUserId,
            Message = req.Message ?? "",
            Status = 0,
        });
        await _db.SaveChangesAsync();

        // 删除Redis缓存
        if (_cache.IsAvailable)
            await _cache.RemoveAsync($"friend:requests:{req.ToUserId}");

        return Ok(new { success = true });
    }

    // ========================================================
    //  接受好友申请
    // ========================================================

    /// <summary>
    /// 接受好友申请
    /// POST /api/friend/accept
    /// Body: { "requestId": 123 }
    /// </summary>
    [HttpPost("accept")]
    public async Task<IActionResult> AcceptRequest([FromBody] AcceptFriendRequest req)
    {
        int userId = GetUserId();

        var friendReq = await _db.FriendRequests.FirstOrDefaultAsync(r =>
            r.Id == req.RequestId && r.ToUserId == userId && r.Status == 0);
        if (friendReq == null)
            return NotFound(new { error = "申请不存在或已处理" });

        friendReq.Status = 1;
        friendReq.UpdatedAt = DateTime.UtcNow;

        // 双向添加好友
        _db.Friends.Add(new Friend { UserId = userId, FriendId = friendReq.FromUserId, Status = 0 });
        _db.Friends.Add(new Friend { UserId = friendReq.FromUserId, FriendId = userId, Status = 0 });

        await _db.SaveChangesAsync();

        // 删除缓存
        if (_cache.IsAvailable)
        {
            await _cache.RemoveAsync($"friend:list:v3:{userId}");
            await _cache.RemoveAsync($"friend:list:v3:{friendReq.FromUserId}");
            await _cache.RemoveAsync($"friend:requests:{userId}");
        }

        return Ok(new { success = true });
    }

    // ========================================================
    //  拒绝好友申请
    // ========================================================

    /// <summary>
    /// 拒绝好友申请
    /// POST /api/friend/reject
    /// Body: { "requestId": 123 }
    /// </summary>
    [HttpPost("reject")]
    public async Task<IActionResult> RejectRequest([FromBody] AcceptFriendRequest req)
    {
        int userId = GetUserId();

        var friendReq = await _db.FriendRequests.FirstOrDefaultAsync(r =>
            r.Id == req.RequestId && r.ToUserId == userId && r.Status == 0);
        if (friendReq == null)
            return NotFound(new { error = "申请不存在或已处理" });

        friendReq.Status = 2;
        friendReq.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        if (_cache.IsAvailable)
            await _cache.RemoveAsync($"friend:requests:{userId}");

        return Ok(new { success = true });
    }

    // ========================================================
    //  获取待处理申请
    // ========================================================

    /// <summary>
    /// 获取待处理的好友申请
    /// GET /api/friend/requests
    /// </summary>
    [HttpGet("requests")]
    public async Task<ActionResult<object>> GetRequests()
    {
        int userId = GetUserId();

        var requests = await _db.FriendRequests
            .Where(r => r.ToUserId == userId && r.Status == 0)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        // 关联用户名
        var fromUserIds = requests.Select(r => r.FromUserId).Distinct().ToList();
        var users = await _db.Users.Where(u => fromUserIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id);

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
    //  删除好友
    // ========================================================

    /// <summary>
    /// 删除好友（单向）
    /// DELETE /api/friend/delete?friendId=123
    /// </summary>
    [HttpDelete("delete")]
    public async Task<IActionResult> DeleteFriend([FromQuery] int friendId)
    {
        int userId = GetUserId();

        var friend = await _db.Friends.FirstOrDefaultAsync(f =>
            f.UserId == userId && f.FriendId == friendId && f.Status == 0);
        if (friend == null)
            return NotFound(new { error = "好友不存在" });

        _db.Friends.Remove(friend);
        await _db.SaveChangesAsync();

        if (_cache.IsAvailable)
            await _cache.RemoveAsync($"friend:list:v3:{userId}");

        return Ok(new { success = true });
    }

    // ========================================================
    //  拉黑好友
    // ========================================================

    /// <summary>
    /// 拉黑好友
    /// POST /api/friend/block
    /// Body: { "friendId": 123 }
    /// </summary>
    [HttpPost("block")]
    public async Task<IActionResult> BlockFriend([FromBody] BlockFriendRequest req)
    {
        int userId = GetUserId();

        var friend = await _db.Friends.FirstOrDefaultAsync(f =>
            f.UserId == userId && f.FriendId == req.FriendId);
        if (friend == null)
            return NotFound(new { error = "好友不存在" });

        friend.Status = 1; // 拉黑
        await _db.SaveChangesAsync();

        if (_cache.IsAvailable)
            await _cache.RemoveAsync($"friend:list:v3:{userId}");

        return Ok(new { success = true });
    }

    // ========================================================
    //  最近一起玩
    // ========================================================

    /// <summary>
    /// 获取最近一起玩的玩家
    /// GET /api/friend/recent
    /// </summary>
    [HttpGet("recent")]
    public async Task<ActionResult<object>> GetRecentPlayers()
    {
        int userId = GetUserId();

        var recent = await _db.RecentPlayers
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.LastPlayedAt)
            .Take(50)
            .ToListAsync();

        var otherIds = recent.Select(r => r.OtherUserId).Distinct().ToList();
        var users = await _db.Users.Where(u => otherIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id);

        var result = recent.Select(r => new
        {
            userId = r.OtherUserId,
            username = users.TryGetValue(r.OtherUserId, out var u) ? u.Username : "Unknown",
            playCount = r.PlayCount,
            lastPlayedAt = r.LastPlayedAt,
        }).ToList();

        return Ok(new { players = result });
    }

    /// <summary>
    /// 记录一起玩（内部调用，组队结束时触发）
    /// POST /api/friend/recent/add
    /// Body: { "otherUserId": 123 }
    /// </summary>
    [HttpPost("recent/add")]
    public async Task<IActionResult> AddRecentPlayer([FromBody] AddRecentRequest req)
    {
        int userId = GetUserId();

        var existing = await _db.RecentPlayers.FirstOrDefaultAsync(r =>
            r.UserId == userId && r.OtherUserId == req.OtherUserId);

        if (existing != null)
        {
            existing.PlayCount++;
            existing.LastPlayedAt = DateTime.UtcNow;
        }
        else
        {
            _db.RecentPlayers.Add(new RecentPlayer
            {
                UserId = userId,
                OtherUserId = req.OtherUserId,
                PlayCount = 1,
                LastPlayedAt = DateTime.UtcNow,
            });
        }

        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }
}

// ========== DTO ==========

public class CachedFriend
{
    public int friendId { get; set; }
    public string username { get; set; } = "Unknown";
    public int level { get; set; }
    public int classType { get; set; }
    public string remark { get; set; } = "";
    public DateTime createdAt { get; set; }
}

public class SendFriendRequest
{
    public int ToUserId { get; set; }
    public string? TargetName { get; set; }
    public string? Message { get; set; }
}

public class AcceptFriendRequest
{
    public int RequestId { get; set; }
}

public class BlockFriendRequest
{
    public int FriendId { get; set; }
}

public class AddRecentRequest
{
    public int OtherUserId { get; set; }
}
