using ArpgServer.Data;
using ArpgServer.Models;
using ArpgServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArpgServer.Controllers;

/// <summary>
/// 游戏配置控制器 — Redis缓存游戏配置
/// 路由: GET /api/config/all (无需JWT认证)
/// 工作流程: Redis缓存 → 未命中则查MySQL → 写入Redis (30分钟过期)
/// </summary>
[ApiController]
[Route("api/config")]
public class GameConfigController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly RedisCacheService _cache;
    private const string CacheKey = "GameConfig:All";
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(30);

    public GameConfigController(AppDbContext db, RedisCacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    /// <summary>
    /// 获取全部游戏配置 (Redis缓存)
    /// GET /api/config/all
    /// </summary>
    [HttpGet("all")]
    public async Task<ActionResult<GameConfigResponse>> GetAll()
    {
        // Try Redis cache first
        if (_cache.IsAvailable)
        {
            var cached = await _cache.GetAsync<GameConfigResponse>(CacheKey);
            if (cached != null)
            {
                Response.Headers["X-Cache"] = "HIT";
                return Ok(cached);
            }
        }

        // Cache miss — query MySQL
        var response = new GameConfigResponse
        {
            Classes = await _db.GameClassStats.OrderBy(c => c.ClassId).ToListAsync(),
            Skills = await _db.GameSkillData.OrderBy(s => s.ClassId).ThenBy(s => s.SkillSlot).ToListAsync(),
            Enemies = await _db.GameEnemyBase.OrderBy(e => e.EnemyType).ToListAsync(),
            Affixes = await _db.GameEliteAffix.OrderBy(a => a.AffixType).ToListAsync(),
            Runes = await _db.GameRuneData.OrderBy(r => r.RuneType).ToListAsync(),
            Relics = await _db.GameRelicData.OrderBy(r => r.RelicType).ToListAsync(),
            Equipment = await _db.GameEquipmentConfig.OrderBy(e => e.Rarity).ToListAsync(),
            Global = await _db.GameGlobalConfig
                .Select(g => new GameConfigGlobalItem { Key = g.ConfigKey, Value = g.ConfigValue })
                .ToListAsync()
        };

        // Write to Redis cache
        if (_cache.IsAvailable)
        {
            await _cache.SetAsync(CacheKey, response, CacheExpiry);
        }

        Response.Headers["X-Cache"] = "MISS";
        return Ok(response);
    }

    /// <summary>
    /// 清除配置缓存 (管理员接口)
    /// POST /api/config/clear-cache
    /// </summary>
    [HttpPost("clear-cache")]
    public async Task<IActionResult> ClearCache()
    {
        if (_cache.IsAvailable)
        {
            await _cache.RemoveAsync(CacheKey);
            return Ok(new { success = true, message = "配置缓存已清除" });
        }
        return Ok(new { success = false, message = "Redis不可用, 无需清除" });
    }
}
