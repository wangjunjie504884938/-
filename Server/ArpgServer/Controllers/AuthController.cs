using ArpgServer.Data;
using ArpgServer.Models;
using ArpgServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ArpgServer.Controllers;

/// <summary>
/// 认证控制器 — 注册 & 登录 & Token验证
/// 路由: POST /api/auth/register, POST /api/auth/login, GET /api/auth/validate
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtService _jwt;

    public AuthController(AppDbContext db, JwtService jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    /// <summary>
    /// 注册新用户
    /// POST /api/auth/register
    /// 请求体: { "username": "xxx", "password": "xxxxxx" }
    /// 成功返回: { "token": "eyJ...", "username": "xxx" }
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] AuthRequest req)
    {
        // 检查用户名是否已存在
        if (await _db.Users.AnyAsync(u => u.Username == req.Username))
            return Conflict(new { error = "用户名已存在" });

        // 创建用户 (密码用 BCrypt 哈希存储，不存明文)
        var user = new AppUser
        {
            Username = req.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // 自动为新用户创建初始存档
        _db.PlayerSaves.Add(new PlayerSave
        {
            UserId = user.Id,
            Slot = 0,
            SaveJson = "{}",
            ClassType = 0,
            Level = 1,
            HighestStageCleared = 0,
            Gold = 0,
            DataVersion = 1,
            UpdatedAt = DateTime.UtcNow
        });

        // 自动添加到排行榜
        _db.LeaderboardEntries.Add(new LeaderboardEntry
        {
            UserId = user.Id,
            Username = user.Username,
            ClassType = 0,
            Wave = 0,
            Level = 1,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        return Ok(new AuthResponse
        {
            Token = _jwt.GenerateToken(user.Id, user.Username),
            Username = user.Username
        });
    }

    /// <summary>
    /// 登录
    /// POST /api/auth/login
    /// 请求体: { "username": "xxx", "password": "xxxxxx" }
    /// 成功返回: { "token": "eyJ...", "username": "xxx" }
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] AuthRequest req)
    {
        var user = await _db.Users.OrderBy(u => u.Id).FirstOrDefaultAsync(u => u.Username == req.Username);
        if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { error = "用户名或密码错误" });

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new AuthResponse
        {
            Token = _jwt.GenerateToken(user.Id, user.Username),
            Username = user.Username
        });
    }

    /// <summary>
    /// 验证Token有效性 (Game Server的CustomAuthenticator调用)
    /// GET /api/auth/validate
    /// Header: Authorization: Bearer {token}
    /// 有效返回200: { "valid": true, "userId": 1, "username": "xxx" }
    /// 无效/缺失由JWT中间件自动返回401
    /// </summary>
    [Authorize]
    [HttpGet("validate")]
    public IActionResult Validate()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(claim) || !int.TryParse(claim, out var userId))
            return Unauthorized(new { error = "Token无效" });

        var username = User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
        return Ok(new { valid = true, userId, username });
    }
}
