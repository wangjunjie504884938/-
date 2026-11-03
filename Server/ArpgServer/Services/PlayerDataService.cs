using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using ArpgServer.Data;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace ArpgServer.Services;

/// <summary>
/// 玩家数据加载服务 (规划阶段03) — LoadPlayerAsync:
/// Redis 命中 → 校验版本返回; 未命中 → MySQL 读取 → 回填 Redis。
/// 每键 SemaphoreSlim 防止同一玩家并发冷加载造成缓存覆盖。
/// </summary>
public class PlayerDataService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);

    private readonly RedisCacheService _redis;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public PlayerDataService(RedisCacheService redis, IServiceScopeFactory scopeFactory)
    {
        _redis = redis;
        _scopeFactory = scopeFactory;
    }

    public class PlayerSnapshot
    {
        public int SaveId;
        public int UserId;
        public int Slot;
        public string SaveJson = "{}";
        public int DataVersion;
        public int ClassType;
        public int Level;
        public int HighestStageCleared;
        public int Gold;
    }

    // 键命名 (规划4.2): player:{userId}:data:{slot} — {userId} hash tag 集群安全 (纯缓存, 30分钟TTL)
    private static string CacheKey(int userId, int slot) => $"player:{{{userId}}}:data:{slot}";
    private SemaphoreSlim KeyLock(string key) => _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

    /// <summary>加载玩家存档快照 (Redis命中 → MySQL回填; 并发冷加载只查一次库)</summary>
    public async Task<PlayerSnapshot?> LoadPlayerAsync(int userId, int slot)
    {
        var key = CacheKey(userId, slot);

        var cached = await ReadCache(key);
        if (cached != null) return cached;

        var gate = KeyLock(key);
        await gate.WaitAsync();
        try
        {
            cached = await ReadCache(key);          // 双检: 等锁期间可能已被回填
            if (cached != null) return cached;

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var save = await db.PlayerSaves.AsNoTracking()
                .OrderBy(s => s.Id)
                .FirstOrDefaultAsync(s => s.UserId == userId && s.Slot == slot);
            if (save == null) return null;

            var snap = new PlayerSnapshot
            {
                SaveId = save.Id,
                UserId = save.UserId,
                Slot = save.Slot,
                SaveJson = save.SaveJson,
                DataVersion = save.DataVersion,
                ClassType = save.ClassType,
                Level = save.Level,
                HighestStageCleared = save.HighestStageCleared,
                Gold = save.Gold,
            };
            await WriteCache(key, snap);            // 回填 Redis
            return snap;
        }
        finally { gate.Release(); }
    }

    private async Task<PlayerSnapshot?> ReadCache(string key)
        => await _redis.GetAsync<PlayerSnapshot>(key);

    private Task WriteCache(string key, PlayerSnapshot snap)
        => _redis.SetAsync(key, snap, CacheTtl);
}

/// <summary>
/// 存档写队列 (规划阶段03) — 有界队列 + 单消费者(同一玩家写入有序) + DataVersion 乐观锁。
/// 旧 DataVersion 的写入会被丢弃 (不覆盖新版本), 丢弃记日志供对账。
/// </summary>
public class SaveQueueService : BackgroundService
{
    public record SaveJob(int SaveId, int UserId, int Slot, string SaveJson, int ExpectedVersion, string Source);

    private readonly Channel<SaveJob> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RedisCacheService _redis;
    private readonly ILogger<SaveQueueService> _logger;
    private readonly TimeSpan _saveTickSeconds;

    public SaveQueueService(IConfiguration config, IServiceScopeFactory scopeFactory,
        RedisCacheService redis, ILogger<SaveQueueService> logger)
    {
        _scopeFactory = scopeFactory;
        _redis = redis;
        _logger = logger;
        _saveTickSeconds = TimeSpan.FromSeconds(config.GetValue("Arch:SaveTickSeconds", 300));
        _channel = Channel.CreateBounded<SaveJob>(new BoundedChannelOptions(
            config.GetValue("Arch:SaveQueueCapacity", 1000))
        { FullMode = BoundedChannelFullMode.Wait });
    }

    /// <summary>入队 (队列满时返回false — 调用方降级为同步写)</summary>
    public bool Enqueue(SaveJob job) => _channel.Writer.TryWrite(job);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[SaveQueue] 启动, SaveTick={Tick}s", _saveTickSeconds.TotalSeconds);
        using var tick = new PeriodicTimer(_saveTickSeconds);
        var consumer = ConsumeLoop(stoppingToken);
        while (!stoppingToken.IsCancellationRequested && await tick.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            // SaveTick 心跳: 队列积压监控 (规划: 有界队列不允许无界增长)
            if (_channel.Reader.Count > 100)
                _logger.LogWarning("[SaveQueue] 队列积压: {Count}", _channel.Reader.Count);
        }
        _channel.Writer.TryComplete();
        try { await consumer.ConfigureAwait(false); } catch (OperationCanceledException) { }
    }

    private async Task ConsumeLoop(CancellationToken ct)
    {
        await foreach (var job in _channel.Reader.ReadAllAsync(ct))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                // 乐观锁: ExpectedVersion 不匹配 = 旧数据, 丢弃 (0行受影响)
                var rows = await db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE PlayerSaves SET SaveJson = {job.SaveJson}, DataVersion = DataVersion + 1, UpdatedAt = UTC_TIMESTAMP(3) WHERE Id = {job.SaveId} AND DataVersion = {job.ExpectedVersion}");
                if (rows == 0)
                    _logger.LogWarning("[SaveQueue] 版本冲突丢弃: saveId={Id} expected={V} source={S}", job.SaveId, job.ExpectedVersion, job.Source);
                else
                {
                    var newVersion = job.ExpectedVersion + 1;
                    // 失效Redis缓存 (键: player:{userId}:data:{slot}), 下次LoadPlayerAsync回填新版本
                    await _redis.RemoveAsync($"player:{{{job.UserId}}}:data:{job.Slot}");
                    // 规划4.2: save_version 键 = 玩家当前持久化版本标记
                    try { await _redis.Database.StringSetAsync($"player:{{{job.UserId}}}:save_version", newVersion.ToString()); }
                    catch { /* save_version为辅助标记, 失败不影响存档 */ }
                    // 规划4.1: player_data 统一blob表双写 (data_blob=SaveJson; 读取过渡期仍以PlayerSave列权威)
                    try
                    {
                        await db.Database.ExecuteSqlInterpolatedAsync(
                            $@"INSERT INTO player_data (`player_id`, `slot`, `data_blob`, `data_version`, `updated_at`)
                               VALUES ({job.UserId}, {job.Slot}, {job.SaveJson}, {newVersion}, UTC_TIMESTAMP(3))
                               ON DUPLICATE KEY UPDATE `data_blob`={job.SaveJson}, `data_version`={newVersion}, `updated_at`=UTC_TIMESTAMP(3)");
                    }
                    catch (Exception ex2)
                    {
                        _logger.LogWarning("[SaveQueue] player_data双写失败(不影响PlayerSave权威): saveId={Id} {Msg}", job.SaveId, ex2.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SaveQueue] 写入失败 saveId={Id}", job.SaveId);
            }
        }
    }
}
