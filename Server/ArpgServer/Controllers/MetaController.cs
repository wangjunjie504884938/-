using System.Security.Claims;
using ArpgServer.Data;
using ArpgServer.Models;
using ArpgServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArpgServer.Controllers;

/// <summary>
/// 元系统控制器 — 每日任务/签到/排行榜/抽卡/邮件
/// Redis缓存: 排行榜(SortedSet) + 每日任务 + 抽卡保底 + 限流防刷
/// 所有接口需要 JWT 认证
/// </summary>
[ApiController]
[Route("api")]
[Authorize]
public class MetaController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly RedisCacheService _cache;
    private static readonly Random _rng = new();

    // 每日任务模板: (类型, 目标值, 奖励金币)
    private static readonly (int type, int target, int reward)[] DailyTaskTemplates =
    {
        (0, 30, 150),    // 击杀30个敌人 → 150金币
        (0, 60, 300),    // 击杀60个敌人 → 300金币
        (1, 3, 200),     // 通关3次副本 → 200金币
        (1, 5, 350),     // 通关5次副本 → 350金币
        (2, 500, 180),   // 消耗500金币 → 180金币
        (2, 1000, 400),  // 消耗1000金币 → 400金币
        (3, 5, 200),     // 强化5次装备 → 200金币
        (4, 2000, 150),  // 获得2000经验 → 150金币
        (4, 5000, 400),  // 获得5000经验 → 400金币
    };

    // 签到奖励 [天数-1] = 金币
    private static readonly int[] SignInRewards = { 100, 150, 200, 300, 500, 800, 1200 };

    // 抽卡概率 (普通/高级)
    private static readonly float[] NormalGachaRates = { 0.60f, 0.30f, 0.08f, 0.02f }; // 普通/稀有/史诗/传说
    private static readonly float[] PremiumGachaRates = { 0.40f, 0.40f, 0.15f, 0.05f };
    private const int PityThreshold = 50; // 50抽保底传说
    private const int NormalGachaCost = 500;   // 金币
    private const int PremiumGachaCost = 2000; // 金币

    public MetaController(AppDbContext db, RedisCacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    private int GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.Parse(claim!);
    }

    /// <summary>从请求头获取玩家活跃槽位 (默认0)</summary>
    private int GetActiveSlot()
    {
        var slotStr = Request.Headers["X-ActiveSlot"].FirstOrDefault();
        if (int.TryParse(slotStr, out int slot) && slot >= 0 && slot <= 2)
            return slot;
        return 0;
    }

    // ==================== 每日任务 ====================

    /// <summary>
    /// 获取今日每日任务 (自动生成)
    /// GET /api/daily/list
    /// </summary>
    [HttpGet("daily/list")]
    public async Task<ActionResult<object>> GetDailyTasks()
    {
        int userId = GetUserId();
        string today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        // Clean up old daily tasks (older than 7 days) — runs opportunistically
        var cutoff = DateTime.UtcNow.AddDays(-7).ToString("yyyy-MM-dd");
        var oldTasks = await _db.DailyTasks
            .Where(t => t.UserId == userId && string.Compare(t.TaskDate, cutoff) < 0)
            .ToListAsync();
        if (oldTasks.Count > 0)
        {
            _db.DailyTasks.RemoveRange(oldTasks);
            await _db.SaveChangesAsync();
        }

        var tasks = await _db.DailyTasks
            .Where(t => t.UserId == userId && t.TaskDate == today)
            .ToListAsync();

        // 首次访问或新的一天 → 自动生成3个随机任务
        if (tasks.Count == 0)
        {
            var indices = Enumerable.Range(0, DailyTaskTemplates.Length)
                .OrderBy(_ => _rng.Next())
                .Take(5)
                .ToList();

            foreach (var idx in indices)
            {
                var (type, target, reward) = DailyTaskTemplates[idx];
                tasks.Add(new DailyTask
                {
                    UserId = userId,
                    TaskDate = today,
                    TaskType = type,
                    TargetValue = target,
                    CurrentValue = 0,
                    RewardGold = reward,
                    Claimed = false
                });
            }
            _db.DailyTasks.AddRange(tasks);
            await _db.SaveChangesAsync();
        }

        return Ok(new { tasks });
    }

    /// <summary>
    /// 领取每日任务奖励
    /// POST /api/daily/claim?taskId=123
    /// </summary>
    [HttpPost("daily/claim")]
    public async Task<IActionResult> ClaimDailyTask([FromQuery] int taskId)
    {
        int userId = GetUserId();
        var task = await _db.DailyTasks.OrderBy(t => t.Id).FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId);
        if (task == null) return NotFound(new { error = "任务不存在" });
        if (task.Claimed) return BadRequest(new { error = "奖励已领取" });
        if (task.CurrentValue < task.TargetValue) return BadRequest(new { error = "任务未完成" });

        task.Claimed = true;

        // 将奖励金币写入存档
        await AddGoldToSave(userId, task.RewardGold);

        await _db.SaveChangesAsync();
        return Ok(new { success = true, rewardGold = task.RewardGold });
    }

    /// <summary>
    /// 更新每日任务进度 (客户端调用)
    /// POST /api/daily/progress
    /// 请求体: { "taskType": 0, "amount": 5 }
    /// </summary>
    [HttpPost("daily/progress")]
    public async Task<IActionResult> UpdateDailyProgress([FromBody] UpdateProgressRequest req)
    {
        int userId = GetUserId();
        string today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        var tasks = await _db.DailyTasks
            .Where(t => t.UserId == userId && t.TaskDate == today && t.TaskType == req.TaskType && !t.Claimed)
            .ToListAsync();

        foreach (var task in tasks)
        {
            task.CurrentValue = Math.Min(task.TargetValue, task.CurrentValue + req.Amount);
        }
        await _db.SaveChangesAsync();

        return Ok(new { success = true });
    }

    // ==================== 签到 ====================

    /// <summary>
    /// 获取签到状态
    /// GET /api/signin/status
    /// </summary>
    [HttpGet("signin/status")]
    public async Task<ActionResult<object>> GetSignInStatus()
    {
        int userId = GetUserId();
        string today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        // 计算本周周期起始日 (周一)
        var now = DateTime.UtcNow;
        int daysSinceMonday = ((int)now.DayOfWeek + 6) % 7;
        var cycleStart = now.AddDays(-daysSinceMonday).Date;
        string cycleStartStr = cycleStart.ToString("yyyy-MM-dd");

        var record = await _db.SignInRecords
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.CycleStart == cycleStartStr);

        if (record == null)
        {
            record = new SignInRecord
            {
                UserId = userId,
                CycleStart = cycleStartStr,
                SignedDays = 0,
                LastSignDate = ""
            };
            _db.SignInRecords.Add(record);
            await _db.SaveChangesAsync();
        }

        bool canSignToday = record.LastSignDate != today && record.SignedDays < 7;
        int todayReward = record.SignedDays < 7 ? SignInRewards[record.SignedDays] : 0;

        return Ok(new
        {
            signedDays = record.SignedDays,
            canSign = canSignToday,
            todayReward,
            rewards = SignInRewards
        });
    }

    /// <summary>
    /// 执行签到
    /// POST /api/signin/do
    /// </summary>
    [HttpPost("signin/do")]
    public async Task<IActionResult> DoSignIn()
    {
        int userId = GetUserId();
        string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var now = DateTime.UtcNow;
        int daysSinceMonday = ((int)now.DayOfWeek + 6) % 7;
        var cycleStart = now.AddDays(-daysSinceMonday).Date;
        string cycleStartStr = cycleStart.ToString("yyyy-MM-dd");

        var record = await _db.SignInRecords
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.CycleStart == cycleStartStr);

        if (record == null)
        {
            record = new SignInRecord
            {
                UserId = userId,
                CycleStart = cycleStartStr,
                SignedDays = 0,
                LastSignDate = ""
            };
            _db.SignInRecords.Add(record);
        }

        if (record.LastSignDate == today)
            return BadRequest(new { error = "今日已签到" });

        if (record.SignedDays >= 7)
            return BadRequest(new { error = "本周签到已完成" });

        int reward = SignInRewards[record.SignedDays];
        record.SignedDays++;
        record.LastSignDate = today;

        // 将签到奖励金币写入存档
        await AddGoldToSave(userId, reward);

        await _db.SaveChangesAsync();

        return Ok(new { success = true, rewardGold = reward, signedDays = record.SignedDays });
    }

    // ==================== 排行榜 ====================

    /// <summary>
    /// 获取实力排行榜 Top 100 (按等级排序)
    /// GET /api/leaderboard/power
    /// </summary>
    [HttpGet("leaderboard/power")]
    public async Task<ActionResult<object>> GetPowerLeaderboard()
    {
        var entries = await _db.LeaderboardEntries
            .OrderByDescending(l => l.Level)
            .ThenByDescending(l => l.Wave)
            .Take(100)
            .Select(l => new { l.Username, l.ClassType, l.Wave, l.Level, l.CreatedAt })
            .ToListAsync();

        return Ok(new { leaderboard = entries });
    }

    /// <summary>
    /// 获取排行榜 Top 100
    /// GET /api/leaderboard/top
    /// </summary>
    [HttpGet("leaderboard/top")]
    public async Task<ActionResult<object>> GetLeaderboard()
    {
        // Try Redis first
        if (_cache.IsAvailable)
        {
            var redisEntries = await _cache.GetLeaderboardTopN(100);
            if (redisEntries.Count > 0)
            {
                Response.Headers["X-Cache"] = "Redis";
                return Ok(new { leaderboard = redisEntries.Select(l => new { l.Username, l.ClassType, l.Wave, l.Level, l.CreatedAt }) });
            }
        }

        // Fallback to MySQL
        var entries = await _db.LeaderboardEntries
            .OrderByDescending(l => l.Wave)
            .ThenByDescending(l => l.Level)
            .Take(100)
            .Select(l => new { l.Username, l.ClassType, l.Wave, l.Level, l.CreatedAt })
            .ToListAsync();

        return Ok(new { leaderboard = entries });
    }

    /// <summary>
    /// 提交无尽模式成绩
    /// POST /api/leaderboard/submit
    /// 请求体: { "wave": 15, "classType": 0, "level": 25 }
    /// </summary>
    [HttpPost("leaderboard/submit")]
    public async Task<IActionResult> SubmitLeaderboard([FromBody] SubmitLeaderboardRequest req)
    {
        int userId = GetUserId();
        var user = await _db.Users.OrderBy(u => u.Id).FirstOrDefaultAsync(u => u.Id == userId);
        string username = user?.Username ?? "Unknown";

        // 只保留该用户最高纪录 (取Top 1)
        var existing = await _db.LeaderboardEntries
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.Wave)
            .FirstOrDefaultAsync();

        if (existing != null && existing.Wave >= req.Wave)
            return Ok(new { success = true, message = "未超越最高纪录" });

        var entry = new LeaderboardEntry
        {
            UserId = userId,
            Username = username,
            ClassType = req.ClassType,
            Wave = req.Wave,
            Level = req.Level,
            CreatedAt = DateTime.UtcNow
        };
        _db.LeaderboardEntries.Add(entry);

        // 删除旧的低分记录, 避免重复条目
        if (existing != null)
        {
            _db.LeaderboardEntries.Remove(existing);
        }

        await _db.SaveChangesAsync();

        // Also submit to Redis (Sorted Set — O(log N) insertion)
        await _cache.SubmitLeaderboardScore(userId, username, req.Wave, req.Level, req.ClassType);

        // Calculate rank from Redis (fast), fallback to DB
        int rank = 0;
        if (_cache.IsAvailable)
        {
            var redisRank = await _cache.GetPlayerRank(userId);
            rank = redisRank >= 0 ? (int)redisRank + 1 : 0;
        }
        if (rank == 0)
            rank = await _db.LeaderboardEntries.Where(l => l.Wave > req.Wave).CountAsync() + 1;

        return Ok(new { success = true, rank });
    }

    // ==================== 竞技场排行榜 ====================

    /// <summary>
    /// 获取竞技场排行榜
    /// GET /api/arena/leaderboard
    /// </summary>
    [HttpGet("arena/leaderboard")]
    public async Task<ActionResult<object>> GetArenaLeaderboard()
    {
        var entries = await _db.ArenaLeaderboardEntries
            .OrderByDescending(l => l.Wins)
            .ThenByDescending(l => l.GearScore)
            .Take(100)
            .Select(l => new { l.Username, l.ClassType, l.Wins, l.TotalGames, l.GearScore, l.DefenseRating, l.UpdatedAt })
            .ToListAsync();

        return Ok(new { leaderboard = entries });
    }

    /// <summary>
    /// 提交竞技场成绩
    /// POST /api/arena/submit
    /// 请求体: { "won": true, "classType": 0, "gearScore": 3000, "defenseRating": 50 }
    /// </summary>
    [HttpPost("arena/submit")]
    public async Task<IActionResult> SubmitArenaScore([FromBody] SubmitArenaRequest req)
    {
        int userId = GetUserId();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        string username = user?.Username ?? "Unknown";

        var existing = await _db.ArenaLeaderboardEntries
            .FirstOrDefaultAsync(l => l.UserId == userId);

        if (existing == null)
        {
            existing = new ArenaLeaderboardEntry
            {
                UserId = userId,
                Username = username,
                ClassType = req.ClassType,
                Wins = req.Won ? 1 : 0,
                TotalGames = 1,
                GearScore = req.GearScore,
                DefenseRating = req.DefenseRating,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.ArenaLeaderboardEntries.Add(existing);
        }
        else
        {
            existing.TotalGames++;
            if (req.Won) existing.Wins++;
            existing.GearScore = Math.Max(existing.GearScore, req.GearScore);
            existing.DefenseRating = req.DefenseRating;
            existing.ClassType = req.ClassType;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        int rank = await _db.ArenaLeaderboardEntries
            .Where(l => l.Wins > existing.Wins)
            .CountAsync() + 1;

        return Ok(new { success = true, rank, wins = existing.Wins, totalGames = existing.TotalGames });
    }

    // ==================== 抽卡 ====================

    /// <summary>
    /// 执行抽卡
    /// POST /api/gacha/draw
    /// 请求体: { "gachaType": 0, "count": 1 }
    /// gachaType: 0=普通(500金币) 1=高级(2000金币)
    /// </summary>
    [HttpPost("gacha/draw")]
    public async Task<IActionResult> DrawGacha([FromBody] DrawGachaRequest req)
    {
        int userId = GetUserId();

        // 限流防刷: 每分钟最多10次抽卡
        if (await _cache.IsRateLimited(userId, "gacha", 10, TimeSpan.FromMinutes(1)))
            return BadRequest(new { error = "操作过于频繁, 请稍后再试" });

        int cost = req.GachaType == 1 ? PremiumGachaCost : NormalGachaCost;
        int totalCost = cost * req.Count;

        // 从存档中扣除金币 (原子操作)
        var (deductOk, newGold) = await DeductGoldFromSave(userId, totalCost);
        if (!deductOk)
            return BadRequest(new { error = "金币不足" });

        // 获取保底计数 (优先从Redis, 降级到DB)
        int pityCounter;
        if (_cache.IsAvailable)
        {
            pityCounter = await _cache.GetPityCounter(userId, 1);
        }
        else
        {
            var pity = await _db.GachaPities
                .OrderBy(g => g.Id)
                .FirstOrDefaultAsync(g => g.UserId == userId && g.GachaType == 1);
            pityCounter = pity?.PityCounter ?? 0;
        }

        var results = new List<object>();
        var rates = req.GachaType == 1 ? PremiumGachaRates : NormalGachaRates;

        for (int i = 0; i < req.Count; i++)
        {
            int rarity;
            if (req.GachaType == 1)
            {
                pityCounter++;
                // 保底检查
                if (pityCounter >= PityThreshold)
                {
                    rarity = 3; // 传说
                    pityCounter = 0;
                }
                else
                {
                    rarity = RollRarity(rates);
                }
            }
            else
            {
                rarity = RollRarity(rates);
            }

            // 记录抽卡历史 (仅记录, 不用于查询保底)
            var record = new GachaRecord
            {
                UserId = userId,
                GachaType = req.GachaType,
                Rarity = rarity,
                PityCounter = req.GachaType == 1 ? pityCounter : 0,
                CreatedAt = DateTime.UtcNow
            };
            _db.GachaRecords.Add(record);

            results.Add(new { rarity });
        }

        // 更新保底计数 (优先Redis, 同时写DB)
        if (req.GachaType == 1)
        {
            if (_cache.IsAvailable)
            {
                // Redis: atomic increment per draw
                if (pityCounter == 0) // was reset
                    await _cache.ResetPityCounter(userId, 1);
                else
                    await _cache.SetAsync($"GachaPity:{userId}:1", pityCounter, TimeSpan.FromDays(30));
            }

            // Also update DB (fallback/backup)
            var pity = await _db.GachaPities.OrderBy(g => g.Id).FirstOrDefaultAsync(g => g.UserId == userId && g.GachaType == 1);
            if (pity == null)
            {
                _db.GachaPities.Add(new GachaPity { UserId = userId, GachaType = 1, PityCounter = pityCounter });
            }
            else
            {
                pity.PityCounter = pityCounter;
            }
        }

        await _db.SaveChangesAsync();

        return Ok(new { success = true, results, newGold, pityCounter = req.GachaType == 1 ? pityCounter : 0 });
    }

    /// <summary>
    /// 获取抽卡保底信息
    /// GET /api/gacha/pity
    /// </summary>
    [HttpGet("gacha/pity")]
    public async Task<ActionResult<object>> GetGachaPity()
    {
        int userId = GetUserId();

        // Try Redis first
        int pityCounter;
        if (_cache.IsAvailable)
        {
            pityCounter = await _cache.GetPityCounter(userId, 1);
            if (pityCounter > 0)
            {
                Response.Headers["X-Cache"] = "Redis";
                return Ok(new { pityCounter, pityThreshold = PityThreshold });
            }
        }

        // Fallback to DB
        var pity = await _db.GachaPities
            .OrderBy(g => g.Id)
            .FirstOrDefaultAsync(g => g.UserId == userId && g.GachaType == 1);
        pityCounter = pity?.PityCounter ?? 0;

        // Warm Redis cache
        if (_cache.IsAvailable && pityCounter > 0)
            await _cache.SetAsync($"GachaPity:{userId}:1", pityCounter, TimeSpan.FromDays(30));

        return Ok(new { pityCounter, pityThreshold = PityThreshold });
    }

    // ==================== 邮件 ====================

    /// <summary>
    /// 获取邮件列表
    /// GET /api/mail/list
    /// </summary>
    [HttpGet("mail/list")]
    public async Task<ActionResult<object>> GetMailList()
    {
        int userId = GetUserId();
        var now = DateTime.UtcNow;

        var mails = await _db.Mails
            .Where(m => m.UserId == userId && (m.ExpireAt == null || m.ExpireAt > now))
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new
            {
                m.Id,
                m.Title,
                m.Content,
                m.AttachmentGold,
                m.AttachmentItems,
                m.IsRead,
                m.Claimed,
                m.CreatedAt
            })
            .ToListAsync();

        return Ok(new { mails });
    }

    /// <summary>
    /// 标记邮件已读
    /// POST /api/mail/read?mailId=123
    /// </summary>
    [HttpPost("mail/read")]
    public async Task<IActionResult> ReadMail([FromQuery] int mailId)
    {
        int userId = GetUserId();
        var mail = await _db.Mails.OrderBy(m => m.Id).FirstOrDefaultAsync(m => m.Id == mailId && m.UserId == userId);
        if (mail == null) return NotFound(new { error = "邮件不存在" });

        mail.IsRead = true;
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    /// <summary>
    /// 领取邮件附件
    /// POST /api/mail/claim?mailId=123
    /// </summary>
    [HttpPost("mail/claim")]
    public async Task<IActionResult> ClaimMail([FromQuery] int mailId)
    {
        int userId = GetUserId();
        var mail = await _db.Mails.OrderBy(m => m.Id).FirstOrDefaultAsync(m => m.Id == mailId && m.UserId == userId);
        if (mail == null) return NotFound(new { error = "邮件不存在" });
        if (mail.Claimed) return BadRequest(new { error = "附件已领取" });

        mail.Claimed = true;
        mail.IsRead = true;

        // 将附件金币加到存档 (原子操作)
        if (mail.AttachmentGold > 0)
        {
            await AddGoldToSave(userId, mail.AttachmentGold);
        }

        await _db.SaveChangesAsync();
        return Ok(new { success = true, attachmentGold = mail.AttachmentGold });
    }

    /// <summary>
    /// 发送邮件给自己 (客户端本地邮件同步到服务器)
    /// POST /api/mail/send
    /// 请求体: { "title": "...", "content": "...", "attachmentGold": 0, "attachmentItems": "..." }
    /// </summary>
    [HttpPost("mail/send")]
    public async Task<IActionResult> SendMailToSelf([FromBody] SendMailRequest req)
    {
        int userId = GetUserId();

        // 安全限制: 金币只能由服务器端系统发放, 客户端不可附带
        // 但装备物品JSON可以保留 (来自本地背包溢出的装备)
        var mail = new Mail
        {
            UserId = userId,
            Title = string.IsNullOrEmpty(req.Title) ? "系统邮件" : req.Title.Length > 64 ? req.Title.Substring(0, 64) : req.Title,
            Content = string.IsNullOrEmpty(req.Content) ? "" : req.Content.Length > 2000 ? req.Content.Substring(0, 2000) : req.Content,
            AttachmentGold = 0, // 强制为0, 防止刷金币
            AttachmentItems = string.IsNullOrEmpty(req.AttachmentItems) ? "" : req.AttachmentItems.Length > 10000 ? req.AttachmentItems.Substring(0, 10000) : req.AttachmentItems,
            IsRead = false,
            Claimed = string.IsNullOrEmpty(req.AttachmentItems), // 有附件则未领取, 无附件则已领取
            CreatedAt = DateTime.UtcNow,
            ExpireAt = DateTime.UtcNow.AddDays(30)
        };
        _db.Mails.Add(mail);
        await _db.SaveChangesAsync();

        return Ok(new { success = true, mailId = mail.Id });
    }

    // ==================== 工具方法 ====================

    /// <summary>将金币写入玩家存档 (原子操作, 使用 Gold 列)</summary>
    private async Task AddGoldToSave(int userId, int gold)
    {
        // 使用玩家活跃槽位 (从请求头或默认0)
        int activeSlot = GetActiveSlot();
        var save = await _db.PlayerSaves.OrderBy(s => s.Id).FirstOrDefaultAsync(s => s.UserId == userId && s.Slot == activeSlot);
        if (save == null)
        {
            // 回退到slot 0
            save = await _db.PlayerSaves.OrderBy(s => s.Id).FirstOrDefaultAsync(s => s.UserId == userId && s.Slot == 0);
        }
        if (save == null)
        {
            // 用户还没有存档 — 创建初始存档
            save = new PlayerSave
            {
                UserId = userId,
                Slot = activeSlot,
                SaveJson = "{}",
                ClassType = 0,
                Level = 1,
                HighestStageCleared = 0,
                Gold = gold,
                UpdatedAt = DateTime.UtcNow
            };
            _db.PlayerSaves.Add(save);
        }
        else
        {
            save.Gold += gold;
        }

        // 同步金币到 SaveJson (保持一致性)
        SyncGoldToJson(save);
    }

    /// <summary>扣除金币 (原子操作, 返回是否成功)</summary>
    private async Task<(bool success, int newGold)> DeductGoldFromSave(int userId, int amount)
    {
        int activeSlot = GetActiveSlot();
        var save = await _db.PlayerSaves.OrderBy(s => s.Id).FirstOrDefaultAsync(s => s.UserId == userId && s.Slot == activeSlot);
        if (save == null)
            save = await _db.PlayerSaves.OrderBy(s => s.Id).FirstOrDefaultAsync(s => s.UserId == userId && s.Slot == 0);
        if (save == null) return (false, 0);
        if (save.Gold < amount) return (false, save.Gold);

        save.Gold -= amount;
        SyncGoldToJson(save);
        return (true, save.Gold);
    }

    /// <summary>同步 Gold 列到 SaveJson 中的 gold 字段</summary>
    private static void SyncGoldToJson(PlayerSave save)
    {
        try
        {
            var saveDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(save.SaveJson ?? "{}") ?? new();
            saveDict["gold"] = System.Text.Json.JsonSerializer.SerializeToElement(save.Gold);
            save.SaveJson = System.Text.Json.JsonSerializer.Serialize(saveDict);
        }
        catch
        {
            // If JSON is malformed, create minimal structure
            var minimal = new Dictionary<string, System.Text.Json.JsonElement>
            {
                ["gold"] = System.Text.Json.JsonSerializer.SerializeToElement(save.Gold)
            };
            save.SaveJson = System.Text.Json.JsonSerializer.Serialize(minimal);
        }
    }

    private static int RollRarity(float[] rates)
    {
        float roll = (float)_rng.NextDouble();
        float cumulative = 0;
        for (int i = 0; i < rates.Length; i++)
        {
            cumulative += rates[i];
            if (roll < cumulative) return i;
        }
        return 0;
    }
}

// ========== 请求 DTO ==========

public class UpdateProgressRequest
{
    public int TaskType { get; set; }
    public int Amount { get; set; }
}

public class SubmitLeaderboardRequest
{
    public int Wave { get; set; }
    public int ClassType { get; set; }
    public int Level { get; set; }
}

public class SubmitArenaRequest
{
    public bool Won { get; set; }
    public int ClassType { get; set; }
    public int GearScore { get; set; }
    public int DefenseRating { get; set; }
}

public class DrawGachaRequest
{
    public int GachaType { get; set; }
    public int Count { get; set; } = 1;
}

/// <summary>发送邮件请求 (本地邮件同步到服务器)</summary>
public class SendMailRequest
{
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public int AttachmentGold { get; set; }
    public string AttachmentItems { get; set; } = "";
}
