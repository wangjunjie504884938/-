using StackExchange.Redis;
using System.Text.Json;
using System.Threading.Tasks;
using ArpgServer.Models;

namespace ArpgServer.Services;

/// <summary>
/// Redis 缓存服务 — 游戏配置/排行榜/每日任务/抽卡保底/限流/在线状态
/// 所有操作都有降级: Redis不可用时自动使用MySQL直查
/// </summary>
public class RedisCacheService
{
    private readonly ConnectionMultiplexer? _redis;
    private readonly IDatabase? _db;
    private readonly string _instanceName;
    private bool _available;

    public bool IsAvailable => _available;

    public RedisCacheService(IConfiguration config)
    {
        var connStr = config["Redis:ConnectionString"] ?? "localhost:6379";
        _instanceName = config["Redis:InstanceName"] ?? "Arpg:";

        try
        {
            _redis = ConnectionMultiplexer.Connect(connStr);
            _db = _redis.GetDatabase();
            _available = true;
            Console.WriteLine($"[Redis] 连接成功: {connStr}");
        }
        catch (Exception ex)
        {
            _available = false;
            Console.WriteLine($"[Redis] 连接失败, 使用数据库直查: {ex.Message}");
        }
    }

    // ========================================================
    //  通用缓存操作
    // ========================================================

    /// <summary>获取缓存 (JSON反序列化)</summary>
    public async Task<T?> GetAsync<T>(string key)
    {
        if (!_available) return default;
        try
        {
            var value = await _db.StringGetAsync(_instanceName + key);
            if (value.IsNullOrEmpty) return default;
            return JsonSerializer.Deserialize<T>(value.ToString());
        }
        catch { return default; }
    }

    /// <summary>设置缓存 (带过期时间)</summary>
    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        if (!_available) return;
        try
        {
            var json = JsonSerializer.Serialize(value);
            await _db.StringSetAsync(_instanceName + key, json, expiry ?? TimeSpan.FromMinutes(30));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Redis] 写入缓存失败: {ex.Message}");
        }
    }

    /// <summary>删除缓存</summary>
    public async Task RemoveAsync(string key)
    {
        if (!_available) return;
        try { await _db.KeyDeleteAsync(_instanceName + key); }
        catch { }
    }

    /// <summary>获取或设置 — 不存在则从工厂函数获取并缓存</summary>
    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null)
    {
        var cached = await GetAsync<T>(key);
        if (cached != null) return cached;

        var value = await factory();
        if (value != null)
            await SetAsync(key, value, expiry ?? TimeSpan.FromMinutes(30));
        return value;
    }

    // ========================================================
    //  排行榜 (Sorted Set)
    // ========================================================

    /// <summary>提交排行榜分数</summary>
    public async Task SubmitLeaderboardScore(int userId, string username, int wave, int level, int classType)
    {
        if (!_available) return;
        try
        {
            var key = _instanceName + "Leaderboard";
            // 只保留最高分
            var current = await _db.SortedSetScoreAsync(key, userId);
            if (current == null || wave > current)
            {
                await _db.SortedSetAddAsync(key, userId, wave);
                // 存储用户详情
                await _db.HashSetAsync(_instanceName + $"Leaderboard:User:{userId}", new HashEntry[]
                {
                    new("username", username),
                    new("wave", wave),
                    new("level", level),
                    new("classType", classType),
                    new("updatedAt", DateTime.UtcNow.ToString("o"))
                });
            }
        }
        catch (Exception ex) { Console.WriteLine($"[Redis] 排行榜提交失败: {ex.Message}"); }
    }

    /// <summary>获取排行榜 Top N</summary>
    public async Task<List<LeaderboardEntry>> GetLeaderboardTopN(int count)
    {
        if (!_available) return new List<LeaderboardEntry>();
        try
        {
            var key = _instanceName + "Leaderboard";
            var entries = await _db.SortedSetRangeByRankWithScoresAsync(key, 0, count - 1, Order.Descending);
            var result = new List<LeaderboardEntry>();
            foreach (var entry in entries)
            {
                int userId = (int)entry.Element;
                var hash = await _db.HashGetAllAsync(_instanceName + $"Leaderboard:User:{userId}");
                result.Add(new LeaderboardEntry
                {
                    UserId = userId,
                    Username = hash.FirstOrDefault(h => h.Name == "username").Value!,
                    ClassType = (int)(hash.FirstOrDefault(h => h.Name == "classType").Value != RedisValue.Null ? (long)hash.FirstOrDefault(h => h.Name == "classType").Value : 0),
                    Wave = (int)entry.Score,
                    Level = (int)(hash.FirstOrDefault(h => h.Name == "level").Value != RedisValue.Null ? (long)hash.FirstOrDefault(h => h.Name == "level").Value : 0),
                    CreatedAt = DateTime.TryParse(hash.FirstOrDefault(h => h.Name == "updatedAt").Value, out var dt) ? dt : DateTime.UtcNow
                });
            }
            return result;
        }
        catch { return new List<LeaderboardEntry>(); }
    }

    /// <summary>获取玩家排名</summary>
    public async Task<long> GetPlayerRank(int userId)
    {
        if (!_available) return -1;
        try
        {
            var key = _instanceName + "Leaderboard";
            var rank = await _db.SortedSetRankAsync(key, userId, Order.Descending);
            return rank ?? -1;
        }
        catch { return -1; }
    }

    // ========================================================
    //  每日任务缓存
    // ========================================================

    private string DailyTaskKey(int userId, string date) => $"DailyTask:{userId}:{date}";

    /// <summary>缓存每日任务列表</summary>
    public async Task CacheDailyTasks(int userId, string date, object tasks)
    {
        await SetAsync(DailyTaskKey(userId, date), tasks, TimeSpan.FromHours(25));
    }

    /// <summary>获取缓存的每日任务</summary>
    public async Task<T?> GetCachedDailyTasks<T>(int userId, string date)
    {
        return await GetAsync<T>(DailyTaskKey(userId, date));
    }

    /// <summary>清除每日任务缓存 (任务进度更新后调用)</summary>
    public async Task InvalidateDailyTasks(int userId, string date)
    {
        await RemoveAsync(DailyTaskKey(userId, date));
    }

    // ========================================================
    //  抽卡保底计数 (INCR 原子操作)
    // ========================================================

    /// <summary>递增抽卡保底计数, 返回当前值</summary>
    public async Task<int> IncrementPityCounter(int userId, int gachaType)
    {
        if (!_available) return -1;
        try
        {
            var key = _instanceName + $"GachaPity:{userId}:{gachaType}";
            var count = await _db.StringIncrementAsync(key);
            return (int)count;
        }
        catch { return -1; }
    }

    /// <summary>重置保底计数 (抽到传说后)</summary>
    public async Task ResetPityCounter(int userId, int gachaType)
    {
        if (!_available) return;
        try
        {
            var key = _instanceName + $"GachaPity:{userId}:{gachaType}";
            await _db.KeyDeleteAsync(key);
        }
        catch { }
    }

    /// <summary>获取当前保底计数</summary>
    public async Task<int> GetPityCounter(int userId, int gachaType)
    {
        if (!_available) return 0;
        try
        {
            var key = _instanceName + $"GachaPity:{userId}:{gachaType}";
            var val = await _db.StringGetAsync(key);
            return val.IsNullOrEmpty ? 0 : (int)val;
        }
        catch { return 0; }
    }

    // ========================================================
    //  限流防刷 (滑动窗口)
    // ========================================================

    /// <summary>检查是否超过频率限制, 返回true表示被限制</summary>
    public async Task<bool> IsRateLimited(int userId, string action, int maxCount, TimeSpan window)
    {
        if (!_available) return false;
        try
        {
            var key = _instanceName + $"RateLimit:{userId}:{action}";
            var current = await _db.StringIncrementAsync(key);
            if (current == 1)
                await _db.KeyExpireAsync(key, window);
            return current > maxCount;
        }
        catch { return false; }
    }

    // ========================================================
    //  在线状态
    // ========================================================

    /// <summary>标记用户在线 (带60秒过期)</summary>
    public async Task SetUserOnline(int userId)
    {
        if (!_available) return;
        try
        {
            var key = _instanceName + $"Online:{userId}";
            await _db.StringSetAsync(key, DateTime.UtcNow.ToString("o"), TimeSpan.FromSeconds(60));
        }
        catch { }
    }

    /// <summary>检查用户是否在线</summary>
    public async Task<bool> IsUserOnline(int userId)
    {
        if (!_available) return false;
        try
        {
            var key = _instanceName + $"Online:{userId}";
            return await _db.KeyExistsAsync(key);
        }
        catch { return false; }
    }

    // ========================================================
    //  Token黑名单 (强制下线)
    // ========================================================

    /// <summary>将Token加入黑名单 (登出/强制下线)</summary>
    public async Task BlacklistToken(string token, TimeSpan expiry)
    {
        if (!_available) return;
        try
        {
            await _db.StringSetAsync(_instanceName + $"Blacklist:{token}", "1", expiry);
        }
        catch { }
    }

    /// <summary>检查Token是否在黑名单中</summary>
    public async Task<bool> IsTokenBlacklisted(string token)
    {
        if (!_available) return false;
        try
        {
            return await _db.KeyExistsAsync(_instanceName + $"Blacklist:{token}");
        }
        catch { return false; }
    }
}
