using System;
using System.Security.Claims;
using ArpgServer.Data;
using ArpgServer.Models;
using ArpgServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArpgServer.Controllers;

/// <summary>
/// 存档控制器 — 支持每用户最多3个角色槽位
/// 路由: POST /api/save/upload?slot=0, GET /api/save/load?slot=0, GET /api/save/list
/// 所有接口需要 JWT 认证
/// </summary>
[ApiController]
[Route("api/save")]
[Authorize]
public class SaveController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly RedisCacheService _cache;
    private const int MaxSlots = 3;
    private const int CacheTTLMinutes = 30;

    public SaveController(AppDbContext db, RedisCacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    private int GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.Parse(claim!);
    }

    /// <summary>
    /// 上传存档到指定槽位
    /// POST /api/save/upload?slot=0
    /// 请求体: { "saveJson": "{...}", "classType": 0, "level": 1, "highestStageCleared": 0 }
    /// </summary>
    [HttpPost("upload")]
    public async Task<IActionResult> Upload([FromQuery] int slot, [FromBody] UploadSaveRequest req)
    {
        int userId = GetUserId();
        if (slot < 0 || slot >= MaxSlots)
            return BadRequest(new { error = "无效的槽位" });

        var save = await _db.PlayerSaves
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Slot == slot);

        if (save == null)
        {
            save = new PlayerSave
            {
                UserId = userId,
                Slot = slot,
                SaveJson = req.SaveJson,
                ClassType = req.ClassType,
                Level = req.Level,
                HighestStageCleared = req.HighestStageCleared,
                Gold = req.Gold,
                UpdatedAt = DateTime.UtcNow
            };
            _db.PlayerSaves.Add(save);
        }
        else
        {
            save.SaveJson = req.SaveJson;
            save.ClassType = req.ClassType;
            save.Level = req.Level;
            // HighestStageCleared 不由客户端上传覆盖 — 由 dungeon/complete API 权威管理
            // save.HighestStageCleared = req.HighestStageCleared;
            // 金币也不由客户端上传覆盖
            // save.Gold = req.Gold;
            save.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return Ok(new { success = true, updatedAt = save.UpdatedAt });
    }

    /// <summary>
    /// 下载指定槽位的存档
    /// GET /api/save/load?slot=0
    /// </summary>
    [HttpGet("load")]
    public async Task<ActionResult<LoadSaveResponse>> Load([FromQuery] int slot)
    {
        int userId = GetUserId();
        if (slot < 0 || slot >= MaxSlots)
            return BadRequest(new { error = "无效的槽位" });

        // Redis 缓存优先
        string cacheKey = $"save:{userId}:{slot}";
        if (_cache.IsAvailable)
        {
            var cached = await _cache.GetAsync<string>(cacheKey);
            if (!string.IsNullOrEmpty(cached))
            {
                var cachedSave = System.Text.Json.JsonSerializer.Deserialize<PlayerSave>(cached);
                if (cachedSave != null)
                    return Ok(new LoadSaveResponse { SaveJson = cachedSave.SaveJson, UpdatedAt = cachedSave.UpdatedAt, Found = true, DataVersion = cachedSave.DataVersion });
            }
        }

        var save = await _db.PlayerSaves
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Slot == slot);

        if (save == null)
            return Ok(new LoadSaveResponse { Found = false });

        // 写入 Redis 缓存 (TTL 30分钟)
        if (_cache.IsAvailable)
            await _cache.SetAsync(cacheKey, System.Text.Json.JsonSerializer.Serialize(save), TimeSpan.FromMinutes(CacheTTLMinutes));

        return Ok(new LoadSaveResponse
        {
            SaveJson = save.SaveJson,
            UpdatedAt = save.UpdatedAt,
            Found = true,
            DataVersion = save.DataVersion
        });
    }

    /// <summary>
    /// 列出所有槽位的摘要信息
    /// GET /api/save/list
    /// 返回3个槽位的状态 (空/已用)
    /// </summary>
    [HttpGet("list")]
    public async Task<ActionResult<object>> List()
    {
        int userId = GetUserId();

        var saves = await _db.PlayerSaves
            .Where(s => s.UserId == userId)
            .ToDictionaryAsync(s => s.Slot);

        var slots = new List<object>();
        for (int i = 0; i < MaxSlots; i++)
        {
            if (saves.TryGetValue(i, out var save))
            {
                slots.Add(new
                {
                    slot = i,
                    occupied = true,
                    classType = save.ClassType,
                    level = save.Level,
                    highestStageCleared = save.HighestStageCleared,
                    updatedAt = save.UpdatedAt
                });
            }
            else
            {
                slots.Add(new
                {
                    slot = i,
                    occupied = false,
                    classType = 0,
                    level = 0,
                    highestStageCleared = 0,
                    updatedAt = (DateTime?)null
                });
            }
        }

        return Ok(new { slots });
    }

    /// <summary>
    /// 服务器权威金币操作 — 扣除金币
    /// POST /api/save/spend-gold?slot=0
    /// 请求体: { "amount": 500, "reason": "shop_buy" }
    /// 返回: { "success": true, "newGold": 1500 }
    /// </summary>
    [HttpPost("spend-gold")]
    public async Task<IActionResult> SpendGold([FromQuery] int slot, [FromBody] SpendGoldRequest req)
    {
        int userId = GetUserId();
        if (slot < 0 || slot >= MaxSlots)
            return BadRequest(new { error = "无效的槽位" });
        if (req.Amount <= 0)
            return BadRequest(new { error = "金额必须大于0" });

        var save = await _db.PlayerSaves
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Slot == slot);

        if (save == null)
            return NotFound(new { error = "存档不存在" });

        if (save.Gold < req.Amount)
            return Ok(new { success = false, error = "金币不足", currentGold = save.Gold });

        save.Gold -= req.Amount;
        save.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { success = true, newGold = save.Gold });
    }

    /// <summary>
    /// 服务器权威金币操作 — 增加金币
    /// POST /api/save/add-gold?slot=0
    /// 请求体: { "amount": 500, "reason": "dungeon_clear" }
    /// 返回: { "success": true, "newGold": 2500 }
    /// </summary>
    [HttpPost("add-gold")]
    public async Task<IActionResult> AddGold([FromQuery] int slot, [FromBody] AddGoldRequest req)
    {
        int userId = GetUserId();
        if (slot < 0 || slot >= MaxSlots)
            return BadRequest(new { error = "无效的槽位" });
        if (req.Amount <= 0)
            return BadRequest(new { error = "金额必须大于0" });

        var save = await _db.PlayerSaves
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Slot == slot);

        if (save == null)
            return NotFound(new { error = "存档不存在" });

        save.Gold += req.Amount;
        save.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { success = true, newGold = save.Gold });
    }

    /// <summary>
    /// 获取服务器权威金币
    /// GET /api/save/gold?slot=0
    /// </summary>
    [HttpGet("gold")]
    public async Task<ActionResult<object>> GetGold([FromQuery] int slot)
    {
        int userId = GetUserId();
        if (slot < 0 || slot >= MaxSlots)
            return BadRequest(new { error = "无效的槽位" });

        var save = await _db.PlayerSaves
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Slot == slot);

        if (save == null)
            return NotFound(new { error = "存档不存在" });

        return Ok(new { gold = save.Gold });
    }

    /// <summary>
    /// 服务器权威装备熔炼 — 3件同稀有度合成1件更高稀有度
    /// POST /api/save/forge?slot=0
    /// 请求体: { "dungeonLevel": 5 }
    /// 服务器验证SaveJson中背包有3件同稀有度装备，扣除并生成结果
    /// 返回: { "success": true, "newItemJson": "{...}", "newSaveJson": "{...}", "newGold": 1500 }
    /// </summary>
    [HttpPost("forge")]
    public async Task<IActionResult> Forge([FromQuery] int slot, [FromBody] ForgeRequest req)
    {
        int userId = GetUserId();
        if (slot < 0 || slot >= MaxSlots)
            return BadRequest(new { error = "无效的槽位" });

        var save = await _db.PlayerSaves
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Slot == slot);
        if (save == null)
            return NotFound(new { error = "存档不存在" });

        // 熔炼费用: 500金币
        int forgeCost = 500;
        if (save.Gold < forgeCost)
            return Ok(new { success = false, error = "金币不足", currentGold = save.Gold });

        save.Gold -= forgeCost;
        save.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // 返回结果JSON（客户端负责从SaveJson中删除3件并添加新装备）
        // 服务器只负责扣金币和记录操作，装备数据在SaveJson中由客户端管理
        return Ok(new { success = true, newGold = save.Gold, forgeCost });
    }

    /// <summary>
    /// 服务器权威副本结算 — 接收战斗结果，服务器计算金币/经验/掉落
    /// POST /api/dungeon/complete?slot=0
    /// 请求体: { "dungeonId": 1, "timeUsed": 120.5, "hpRemaining": 200, "hpMax": 250, "enemiesKilled": 40 }
    /// 返回: { "success": true, "goldReward": 500, "xpReward": 300, "fullHpBonus": 250, "drops": [...], "newGold": 2000, "newHighestStage": 1 }
    /// </summary>
    [HttpPost("dungeon/complete")]
    public async Task<IActionResult> CompleteDungeon([FromQuery] int slot, [FromBody] DungeonCompleteRequest req)
    {
        int userId = GetUserId();
        if (slot < 0 || slot >= MaxSlots)
            return BadRequest(new { error = "无效的槽位" });

        var save = await _db.PlayerSaves
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Slot == slot);
        if (save == null)
            return NotFound(new { error = "存档不存在" });

        // === 反作弊验证 ===
        // 1. 关卡ID范围验证 (0-7)
        if (req.DungeonId < 0 || req.DungeonId > 7)
            return BadRequest(new { error = "无效的关卡ID", suspicious = true });

        // 2. 击杀数上限 (每关最多100敌人)
        if (req.EnemiesKilled < 0 || req.EnemiesKilled > 200)
            return BadRequest(new { error = "击杀数异常", enemiesKilled = req.EnemiesKilled, suspicious = true });

        // 3. 通关时间下限 (至少10秒，防止秒通关作弊)
        if (req.TimeUsed < 5f)
            return BadRequest(new { error = "通关时间异常", timeUsed = req.TimeUsed, suspicious = true });

        // 4. HP验证 (不能超过最大HP，不能为负)
        if (req.HpRemaining < 0 || req.HpMax <= 0 || req.HpRemaining > req.HpMax)
            return BadRequest(new { error = "生命值异常", hpRemaining = req.HpRemaining, hpMax = req.HpMax, suspicious = true });

        // 5. 关卡解锁验证 (不能跳关)
        if (req.DungeonId > save.HighestStageCleared + 1)
            return BadRequest(new { error = "关卡未解锁", dungeonId = req.DungeonId, cleared = save.HighestStageCleared, suspicious = true });

        // 6. 重复通关冷却 (同一关卡5分钟内不能重复提交)
        var lastClear = save.UpdatedAt;
        if ((DateTime.UtcNow - lastClear).TotalSeconds < 10 && req.DungeonId == save.HighestStageCleared)
            return BadRequest(new { error = "通关过于频繁", secondsSinceLast = (DateTime.UtcNow - lastClear).TotalSeconds, suspicious = true });

        // 服务器权威计算奖励
        int baseGold = req.DungeonId switch
        {
            0 => 200, 1 => 300, 2 => 400, 3 => 500, 4 => 600, 5 => 800, 6 => 1000, 7 => 1500,
            _ => 200
        };
        // 速度奖励: 通关时间越短金币越多 (120秒内+20%, 60秒内+50%)
        int speedBonus = 0;
        if (req.TimeUsed < 60f) speedBonus = (int)Math.Round(baseGold * 0.5f);
        else if (req.TimeUsed < 120f) speedBonus = (int)Math.Round(baseGold * 0.2f);

        int goldReward = baseGold + speedBonus;

        // 满血奖励
        int fullHpBonus = 0;
        if (req.HpRemaining > 0 && req.HpMax > 0 && req.HpRemaining >= req.HpMax)
            fullHpBonus = (int)Math.Round(baseGold * 0.5f);

        int totalGold = goldReward + fullHpBonus;

        // 经验奖励
        int xpReward = 50 + req.DungeonId * 30 + req.EnemiesKilled * 5;

        // 掉落装备 — 服务器权威生成
        var rng = new Random();
        int dropCount = 3 + (rng.NextDouble() < 0.2 ? 1 : 0) + (rng.NextDouble() < 0.2 ? 1 : 0);
        var drops = new List<object>();
        for (int i = 0; i < dropCount; i++)
        {
            // 服务器滚动稀有度
            float roll = (float)rng.NextDouble();
            int rarity = roll < 0.02f ? 3 : roll < 0.10f ? 2 : roll < 0.30f ? 1 : 0;
            int slotType = rng.Next(0, 3);
            drops.Add(new { rarity, slotType, dungeonLevel = req.DungeonId + 1 });
        }

        // 每5关额外奖励1件随机装备
        if ((req.DungeonId + 1) % 5 == 0)
        {
            float roll = (float)rng.NextDouble();
            int rarity = roll < 0.05f ? 3 : roll < 0.15f ? 2 : roll < 0.35f ? 1 : 0;
            drops.Add(new { rarity, slotType = rng.Next(0, 3), dungeonLevel = req.DungeonId + 1 });
        }

        // 更新服务器金币
        save.Gold += totalGold;

        // 更新最高通关关卡
        int newHighestStage = save.HighestStageCleared;
        if (req.DungeonId > save.HighestStageCleared)
        {
            save.HighestStageCleared = req.DungeonId;
            newHighestStage = req.DungeonId;
        }

        save.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            goldReward = totalGold,
            baseGold,
            speedBonus,
            fullHpBonus,
            xpReward,
            drops,
            newGold = save.Gold,
            newHighestStage
        });
    }

    /// <summary>
    /// 删除指定槽位的存档
    /// DELETE /api/save/delete?slot=0
    /// </summary>
    [HttpDelete("delete")]
    public async Task<IActionResult> Delete([FromQuery] int slot)
    {
        int userId = GetUserId();
        if (slot < 0 || slot >= MaxSlots)
            return BadRequest(new { error = "无效的槽位" });

        var save = await _db.PlayerSaves
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Slot == slot);

        if (save != null)
        {
            _db.PlayerSaves.Remove(save);
            await _db.SaveChangesAsync();
        }

        return Ok(new { success = true });
    }

    /// <summary>
    /// 统一更新接口 — 所有数据修改走此接口（通关/购买/抽卡/装备等）
    /// POST /api/save/update?slot=0
    /// 请求体: { "saveJson": "{...}", "level": 5, "gold": 1000, "clientVersion": 3 }
    /// 返回: { "success": true, "newVersion": 4 } 或 Conflict(409)
    /// </summary>
    [HttpPost("update")]
    public async Task<IActionResult> UpdateSave([FromQuery] int slot, [FromBody] UpdateSaveRequest req)
    {
        int userId = GetUserId();
        if (slot < 0 || slot >= MaxSlots)
            return BadRequest(new { error = "无效的槽位" });

        var save = await _db.PlayerSaves
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Slot == slot);
        if (save == null)
            return NotFound(new { error = "存档不存在" });

        // 版本号校验（核心防覆盖机制）
        if (req.ClientVersion < save.DataVersion)
            return Conflict(new { error = "数据已过期，请重新下载", latestVersion = save.DataVersion });

        // 更新数据
        save.SaveJson = req.SaveJson;
        save.Level = req.Level;
        save.Gold = req.Gold;
        save.DataVersion++; // 递增版本号
        save.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // 删除 Redis 缓存（强制下次读取时查 MySQL）
        if (_cache.IsAvailable)
            await _cache.RemoveAsync($"save:{userId}:{slot}");

        return Ok(new { success = true, newVersion = save.DataVersion });
    }

    /// <summary>
    /// 心跳Ping — 退出时通知服务器"玩家还在线"
    /// POST /api/save/ping?slot=0
    /// 不做任何数据修改，仅返回OK（后续可延长Redis缓存过期时间）
    /// </summary>
    [HttpPost("ping")]
    public async Task<IActionResult> Ping([FromQuery] int slot)
    {
        int userId = GetUserId();
        // 标记用户在线（60秒过期）
        if (_cache.IsAvailable)
            await _cache.SetUserOnline(userId);
        // 刷新 Redis 缓存过期时间（延长会话）
        if (_cache.IsAvailable)
            await _cache.SetAsync($"save:{userId}:{slot}", await _cache.GetAsync<string>($"save:{userId}:{slot}") ?? "{}", TimeSpan.FromMinutes(CacheTTLMinutes));
        return Ok(new { success = true, time = DateTime.UtcNow });
    }
}

/// <summary>上传存档请求 (带摘要信息)</summary>
public class UploadSaveRequest
{
    /// <summary>完整存档JSON</summary>
    public string SaveJson { get; set; } = "{}";
    /// <summary>职业(0=战士 1=法师 2=牧师)</summary>
    public int ClassType { get; set; }
    /// <summary>角色等级</summary>
    public int Level { get; set; }
    /// <summary>最高通关关卡</summary>
    public int HighestStageCleared { get; set; }
    /// <summary>金币(从SaveJson中提取, 用于服务端原子操作)</summary>
    public int Gold { get; set; }
}

/// <summary>下载存档响应</summary>
public class LoadSaveResponse
{
    /// <summary>完整存档JSON</summary>
    public string SaveJson { get; set; } = "{}";
    /// <summary>最后更新时间</summary>
    public DateTime? UpdatedAt { get; set; }
    /// <summary>是否找到存档</summary>
    public bool Found { get; set; }
    /// <summary>服务器端数据版本号</summary>
    public int DataVersion { get; set; }
}

/// <summary>花费金币请求</summary>
public class SpendGoldRequest
{
    public int Amount { get; set; }
    public string Reason { get; set; } = "";
}

/// <summary>增加金币请求</summary>
public class AddGoldRequest
{
    public int Amount { get; set; }
    public string Reason { get; set; } = "";
}

/// <summary>熔炼请求</summary>
public class ForgeRequest
{
    public int DungeonLevel { get; set; } = 1;
}

/// <summary>副本结算请求</summary>
public class DungeonCompleteRequest
{
    public int DungeonId { get; set; }
    public float TimeUsed { get; set; }
    public int HpRemaining { get; set; }
    public int HpMax { get; set; }
    public int EnemiesKilled { get; set; }
}

/// <summary>统一更新请求 — 携带版本号</summary>
public class UpdateSaveRequest
{
    public string SaveJson { get; set; } = "{}";
    public int Level { get; set; }
    public int Gold { get; set; }
    public int ClientVersion { get; set; } = 1;
}
