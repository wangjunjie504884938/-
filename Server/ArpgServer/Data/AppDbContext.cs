using Microsoft.EntityFrameworkCore;
using ArpgServer.Models;

namespace ArpgServer.Data;

/// <summary>
/// EF Core 数据库上下文
/// 管理用户表、存档表 + 8张游戏配置表
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // 用户 & 存档
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<PlayerSave> PlayerSaves => Set<PlayerSave>();

    // 游戏配置表
    public DbSet<GameClassStats> GameClassStats => Set<GameClassStats>();
    public DbSet<GameSkillData> GameSkillData => Set<GameSkillData>();
    public DbSet<GameEnemyBase> GameEnemyBase => Set<GameEnemyBase>();
    public DbSet<GameEliteAffix> GameEliteAffix => Set<GameEliteAffix>();
    public DbSet<GameRuneData> GameRuneData => Set<GameRuneData>();
    public DbSet<GameRelicData> GameRelicData => Set<GameRelicData>();
    public DbSet<GameEquipmentConfig> GameEquipmentConfig => Set<GameEquipmentConfig>();
    public DbSet<GameGlobalConfig> GameGlobalConfig => Set<GameGlobalConfig>();

    // 元系统表 (每日任务/签到/排行榜/抽卡/邮件)
    public DbSet<DailyTask> DailyTasks => Set<DailyTask>();
    public DbSet<SignInRecord> SignInRecords => Set<SignInRecord>();
    public DbSet<LeaderboardEntry> LeaderboardEntries => Set<LeaderboardEntry>();
    public DbSet<ArenaLeaderboardEntry> ArenaLeaderboardEntries => Set<ArenaLeaderboardEntry>();
    public DbSet<GachaRecord> GachaRecords => Set<GachaRecord>();
    public DbSet<GachaPity> GachaPities => Set<GachaPity>();

    // 好友系统
    public DbSet<Friend> Friends => Set<Friend>();
    public DbSet<FriendRequest> FriendRequests => Set<FriendRequest>();
    public DbSet<RecentPlayer> RecentPlayers => Set<RecentPlayer>();
    public DbSet<Mail> Mails => Set<Mail>();

    // 公会系统
    public DbSet<Guild> Guilds => Set<Guild>();
    public DbSet<GuildMember> GuildMembers => Set<GuildMember>();
    public DbSet<GuildRequest> GuildRequests => Set<GuildRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ========== 用户表 ==========
        modelBuilder.Entity<AppUser>(e =>
        {
            e.ToTable(t => t.HasComment("用户表 — 存储账号信息"));
            e.HasIndex(u => u.Username).IsUnique();
            e.Property(u => u.Username).HasMaxLength(32);
            e.HasMany(u => u.Saves)
                .WithOne()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Property(u => u.Id).HasComment("主键ID");
            e.Property(u => u.Username).HasComment("用户名(唯一)");
            e.Property(u => u.PasswordHash).HasComment("密码哈希(BCrypt加密)");
            e.Property(u => u.CreatedAt).HasComment("注册时间");
            e.Property(u => u.LastLoginAt).HasComment("最后登录时间");
        });

        // ========== 玩家存档表 ==========
        modelBuilder.Entity<PlayerSave>(e =>
        {
            e.ToTable(t => t.HasComment("玩家存档表 — 每用户最多3个角色存档"));
            e.HasIndex(s => new { s.UserId, s.Slot }).IsUnique();
            e.Property(s => s.Id).HasComment("主键ID");
            e.Property(s => s.UserId).HasComment("用户ID(外键→Users.Id)");
            e.Property(s => s.Slot).HasComment("角色槽位(0-2)");
            e.Property(s => s.SaveJson).HasComment("完整存档JSON(等级/金币/装备/技能/成就等)");
            e.Property(s => s.ClassType).HasComment("职业(0=战士 1=法师 2=牧师)");
            e.Property(s => s.Level).HasComment("角色等级");
            e.Property(s => s.HighestStageCleared).HasComment("最高通关关卡");
            e.Property(s => s.Gold).HasComment("服务端权威金币(任务/签到/抽卡/邮件原子操作)");
            e.Property(s => s.DataVersion).HasComment("数据版本号(乐观锁防覆盖,每次修改+1)");
            e.Property(s => s.UpdatedAt).HasComment("最后更新时间");
        });

        // ========== 职业属性表 ==========
        modelBuilder.Entity<GameClassStats>(e =>
        {
            e.ToTable(t => t.HasComment("职业属性表 — 各职业基础数值"));
            e.HasIndex(c => c.ClassId).IsUnique();
            e.Property(c => c.Id).HasComment("主键ID");
            e.Property(c => c.ClassId).HasComment("职业ID(0=战士 1=法师 2=牧师)");
            e.Property(c => c.ClassName).HasComment("职业名称");
            e.Property(c => c.Description).HasComment("职业描述");
            e.Property(c => c.Skill1Name).HasComment("技能1名称");
            e.Property(c => c.Skill2Name).HasComment("技能2名称");
            e.Property(c => c.Skill3Name).HasComment("技能3名称");
            e.Property(c => c.BaseHp).HasComment("基础生命值");
            e.Property(c => c.BaseAttack).HasComment("基础攻击力");
            e.Property(c => c.BaseDefense).HasComment("基础防御力");
            e.Property(c => c.BaseMoveSpeed).HasComment("基础移动速度");
            e.Property(c => c.BaseAttackRange).HasComment("基础攻击范围");
            e.Property(c => c.BaseAttackCooldown).HasComment("基础攻击冷却(秒)");
            e.Property(c => c.BaseCritChance).HasComment("基础暴击率");
            e.Property(c => c.BaseHpRegen).HasComment("基础生命恢复/秒");
            e.Property(c => c.HpPerLevel).HasComment("每级生命增长");
            e.Property(c => c.AttackPerLevel).HasComment("每级攻击增长");
            e.Property(c => c.DefensePerLevel).HasComment("每级防御增长");
            e.Property(c => c.IsRanged).HasComment("是否远程职业");
            e.Property(c => c.AutoAttackRange).HasComment("普攻射程");
            e.Property(c => c.PrimaryR).HasComment("主色调R");
            e.Property(c => c.PrimaryG).HasComment("主色调G");
            e.Property(c => c.PrimaryB).HasComment("主色调B");
            e.Property(c => c.PrimaryA).HasComment("主色调A");
            e.Property(c => c.SecondaryR).HasComment("副色调R");
            e.Property(c => c.SecondaryG).HasComment("副色调G");
            e.Property(c => c.SecondaryB).HasComment("副色调B");
            e.Property(c => c.SecondaryA).HasComment("副色调A");
            e.Property(c => c.Skill1R).HasComment("技能1颜色R");
            e.Property(c => c.Skill1G).HasComment("技能1颜色G");
            e.Property(c => c.Skill1B).HasComment("技能1颜色B");
            e.Property(c => c.Skill1A).HasComment("技能1颜色A");
            e.Property(c => c.Skill2R).HasComment("技能2颜色R");
            e.Property(c => c.Skill2G).HasComment("技能2颜色G");
            e.Property(c => c.Skill2B).HasComment("技能2颜色B");
            e.Property(c => c.Skill2A).HasComment("技能2颜色A");
            e.Property(c => c.Skill3R).HasComment("技能3颜色R");
            e.Property(c => c.Skill3G).HasComment("技能3颜色G");
            e.Property(c => c.Skill3B).HasComment("技能3颜色B");
            e.Property(c => c.Skill3A).HasComment("技能3颜色A");
        });

        // ========== 技能数据表 ==========
        modelBuilder.Entity<GameSkillData>(e =>
        {
            e.ToTable(t => t.HasComment("技能数据表 — 各职业技能详细信息"));
            e.HasIndex(s => new { s.ClassId, s.SkillSlot }).IsUnique();
            e.Property(s => s.Id).HasComment("主键ID");
            e.Property(s => s.ClassId).HasComment("职业ID(0=战士 1=法师 2=牧师)");
            e.Property(s => s.SkillSlot).HasComment("技能槽位(0=技能1 1=技能2 2=技能3)");
            e.Property(s => s.Name).HasComment("技能名称");
            e.Property(s => s.Description).HasComment("技能描述");
            e.Property(s => s.Cooldown).HasComment("冷却时间(秒)");
            e.Property(s => s.Range).HasComment("技能范围");
            e.Property(s => s.DamageMultiplier).HasComment("伤害倍率");
            e.Property(s => s.CooldownPerLevel).HasComment("每级冷却(CSV格式)");
            e.Property(s => s.DamageMultiplierPerLevel).HasComment("每级伤害倍率(CSV格式)");
            e.Property(s => s.RangePerLevel).HasComment("每级范围(CSV格式)");
            e.Property(s => s.EvolutionName).HasComment("进化名称");
            e.Property(s => s.EvolutionDescription).HasComment("进化描述");
            e.Property(s => s.EvolutionDamageMultiplier).HasComment("进化伤害倍率");
            e.Property(s => s.EvolutionProjectileBonus).HasComment("进化额外投射物数量");
        });

        // ========== 敌人基础属性表 ==========
        modelBuilder.Entity<GameEnemyBase>(e =>
        {
            e.ToTable(t => t.HasComment("敌人基础属性表 — 敌人数值与缩放公式"));
            e.HasIndex(en => en.EnemyType).IsUnique();
            e.Property(en => en.Id).HasComment("主键ID");
            e.Property(en => en.EnemyType).HasComment("敌人类型(0=近战 1=远程 2=精英 3=Boss)");
            e.Property(en => en.BaseHp).HasComment("基础生命值");
            e.Property(en => en.BaseAttack).HasComment("基础攻击力");
            e.Property(en => en.BaseDefense).HasComment("基础防御力");
            e.Property(en => en.BaseMoveSpeed).HasComment("基础移动速度");
            e.Property(en => en.BaseXpReward).HasComment("基础经验奖励");
            e.Property(en => en.AttackRange).HasComment("攻击范围");
            e.Property(en => en.AttackCooldown).HasComment("攻击冷却(秒)");
            e.Property(en => en.ScaleEarly).HasComment("早期关卡缩放系数");
            e.Property(en => en.ScaleEarlyStep).HasComment("早期每级缩放增量");
            e.Property(en => en.ScaleMid).HasComment("中期关卡缩放系数");
            e.Property(en => en.ScaleMidStep).HasComment("中期每级缩放增量");
            e.Property(en => en.ScaleLate).HasComment("晚期关卡缩放系数");
            e.Property(en => en.ScaleLateStep).HasComment("晚期每级缩放增量");
            e.Property(en => en.MoveSpeedEarlyMult).HasComment("早期移速倍率");
            e.Property(en => en.MoveSpeedMidStep).HasComment("中期移速每级增量");
            e.Property(en => en.MoveSpeedLateBase).HasComment("晚期移速基础值");
            e.Property(en => en.MoveSpeedLateStep).HasComment("晚期移速每级增量");
            e.Property(en => en.EnemyAttackMult).HasComment("敌人攻击力倍率");
            e.Property(en => en.BossHpMult).HasComment("Boss生命倍率");
            e.Property(en => en.BossAtkMult).HasComment("Boss攻击倍率");
            e.Property(en => en.BossDefBonusBase).HasComment("Boss防御加成基础值");
            e.Property(en => en.BossXpMult).HasComment("Boss经验倍率");
            e.Property(en => en.EliteHpMult).HasComment("精英生命倍率");
            e.Property(en => en.EliteAtkMult).HasComment("精英攻击倍率");
            e.Property(en => en.EliteXpMult).HasComment("精英经验倍率");
            e.Property(en => en.AttackCdScaleStartLevel).HasComment("攻速缩放起始等级");
            e.Property(en => en.AttackCdScalePerLevel).HasComment("每级攻速缩放值");
            e.Property(en => en.AttackCdScaleMin).HasComment("攻速缩放下限");
            e.Property(en => en.MinionBaseHp).HasComment("召唤物基础生命值");
            e.Property(en => en.MinionBaseAttack).HasComment("召唤物基础攻击力");
            e.Property(en => en.MinionBaseMoveSpeed).HasComment("召唤物基础移动速度");
            e.Property(en => en.MinionBaseXpReward).HasComment("召唤物基础经验奖励");
        });

        // ========== 精英词缀表 ==========
        modelBuilder.Entity<GameEliteAffix>(e =>
        {
            e.ToTable(t => t.HasComment("精英词缀表 — 精英怪特殊属性"));
            e.HasIndex(a => a.AffixType).IsUnique();
            e.Property(a => a.Id).HasComment("主键ID");
            e.Property(a => a.AffixType).HasComment("词缀类型(0-7)");
            e.Property(a => a.Name).HasComment("词缀名称");
            e.Property(a => a.Description).HasComment("词缀描述");
            e.Property(a => a.AuraRadius).HasComment("光环范围(烈焰)");
            e.Property(a => a.AuraDamage).HasComment("光环伤害(烈焰)");
            e.Property(a => a.TeleportCooldown).HasComment("传送冷却(秒)");
            e.Property(a => a.TeleportRange).HasComment("传送范围");
            e.Property(a => a.ShieldHp).HasComment("护盾生命值");
            e.Property(a => a.ShieldRegenDelay).HasComment("护盾恢复延迟(秒)");
            e.Property(a => a.SpawnInterval).HasComment("召唤间隔(秒)");
            e.Property(a => a.SpawnCount).HasComment("召唤数量");
            e.Property(a => a.HasteSpeedMult).HasComment("疾风移速倍率");
            e.Property(a => a.HasteAttackMult).HasComment("疾风攻速倍率(冷却缩减)");
            e.Property(a => a.ReflectPct).HasComment("反弹伤害百分比");
            e.Property(a => a.RegenPerSecond).HasComment("每秒恢复生命值");
            e.Property(a => a.ChainBuffRadius).HasComment("连锁强化范围");
            e.Property(a => a.ChainBuffDuration).HasComment("连锁强化持续时间(秒)");
            e.Property(a => a.AffixScaleBase).HasComment("词缀缩放基础值");
            e.Property(a => a.AffixScalePerLevel).HasComment("词缀每级缩放增量");
            e.Property(a => a.ColorR).HasComment("颜色R分量");
            e.Property(a => a.ColorG).HasComment("颜色G分量");
            e.Property(a => a.ColorB).HasComment("颜色B分量");
            e.Property(a => a.ColorA).HasComment("颜色A(透明度)分量");
        });

        // ========== 符文数据表 ==========
        modelBuilder.Entity<GameRuneData>(e =>
        {
            e.ToTable(t => t.HasComment("符文数据表 — 技能强化符文"));
            e.HasIndex(r => r.RuneType).IsUnique();
            e.Property(r => r.Id).HasComment("主键ID");
            e.Property(r => r.RuneType).HasComment("符文类型(0-7)");
            e.Property(r => r.Name).HasComment("符文名称");
            e.Property(r => r.Description).HasComment("符文描述");
            e.Property(r => r.DamageMultBase).HasComment("伤害倍率基础值");
            e.Property(r => r.DamageMultPerLevel).HasComment("每级伤害倍率增量");
            e.Property(r => r.DamageMultMin).HasComment("伤害倍率下限");
            e.Property(r => r.ProjectileBonus).HasComment("额外投射物数量(散射)");
            e.Property(r => r.PierceBase).HasComment("穿透基础值(穿透)");
            e.Property(r => r.PiercePerLevel).HasComment("每级穿透增量(穿透)");
            e.Property(r => r.ChainBase).HasComment("弹射基础值(连锁)");
            e.Property(r => r.ChainPerLevel).HasComment("每级弹射增量(连锁)");
            e.Property(r => r.AoeRangeMultBase).HasComment("AOE范围倍率基础值(范围扩大)");
            e.Property(r => r.AoeRangeMultPerLevel).HasComment("每级AOE范围增量(范围扩大)");
            e.Property(r => r.LifeStealBase).HasComment("吸血基础值(吸血)");
            e.Property(r => r.LifeStealPerLevel).HasComment("每级吸血增量(吸血)");
            e.Property(r => r.SlowPctBase).HasComment("减速基础值(减速)");
            e.Property(r => r.SlowPctPerLevel).HasComment("每级减速增量(减速)");
            e.Property(r => r.SlowDurationBase).HasComment("减速基础时长(秒)");
            e.Property(r => r.SlowDurationPerLevel).HasComment("每级减速时长增量(秒)");
            e.Property(r => r.BurnDpsBase).HasComment("燃烧DPS基础值(燃烧)");
            e.Property(r => r.BurnDpsPerLevel).HasComment("每级燃烧DPS增量(燃烧)");
            e.Property(r => r.BurnDurationBase).HasComment("燃烧基础时长(秒)");
            e.Property(r => r.BurnDurationPerLevel).HasComment("每级燃烧时长增量(秒)");
            e.Property(r => r.ColorR).HasComment("颜色R分量");
            e.Property(r => r.ColorG).HasComment("颜色G分量");
            e.Property(r => r.ColorB).HasComment("颜色B分量");
        });

        // ========== 遗物数据表 ==========
        modelBuilder.Entity<GameRelicData>(e =>
        {
            e.ToTable(t => t.HasComment("遗物数据表 — 随机掉落遗物"));
            e.HasIndex(r => r.RelicType).IsUnique();
            e.Property(r => r.Id).HasComment("主键ID");
            e.Property(r => r.RelicType).HasComment("遗物类型(0-9)");
            e.Property(r => r.Name).HasComment("遗物名称");
            e.Property(r => r.Description).HasComment("遗物描述");
            e.Property(r => r.Rarity).HasComment("稀有度(0=普通 1=稀有 2=传说)");
            e.Property(r => r.SplitCount).HasComment("分裂数量(分裂之矢)");
            e.Property(r => r.KillHealAmount).HasComment("击杀恢复生命(生命汲取)");
            e.Property(r => r.CritBurstRadius).HasComment("暴击爆炸范围(暴击风暴)");
            e.Property(r => r.SpeedBoostDuration).HasComment("移速加成持续秒(杀意涌动)");
            e.Property(r => r.SpeedBoostMult).HasComment("移速加成倍率(杀意涌动)");
            e.Property(r => r.BloodMagicDamageBonus).HasComment("血祭伤害加成(血之祭祀)");
            e.Property(r => r.BloodMagicHpCostPct).HasComment("血祭HP消耗百分比(血之祭祀)");
            e.Property(r => r.ChainTargets).HasComment("连锁目标数(雷霆链)");
            e.Property(r => r.ThornsPct).HasComment("反伤百分比(荆棘之盾)");
            e.Property(r => r.DoubleStrikeChance).HasComment("双重打击概率(双刃)");
            e.Property(r => r.DodgeHealChance).HasComment("闪避恢复概率(回光返照)");
            e.Property(r => r.DodgeHealAmount).HasComment("闪避恢复量(回光返照)");
            e.Property(r => r.XpBonus).HasComment("经验加成(智慧之心)");
            e.Property(r => r.ColorR).HasComment("颜色R分量");
            e.Property(r => r.ColorG).HasComment("颜色G分量");
            e.Property(r => r.ColorB).HasComment("颜色B分量");
            e.Property(r => r.RollLegendaryBase).HasComment("传说掉率基础值");
            e.Property(r => r.RollLegendaryPerStage).HasComment("每关传说掉率增量");
            e.Property(r => r.RollRareBase).HasComment("稀有掉率基础值");
            e.Property(r => r.RollRarePerStage).HasComment("每关稀有掉率增量");
        });

        // ========== 装备配置表 ==========
        modelBuilder.Entity<GameEquipmentConfig>(e =>
        {
            e.ToTable(t => t.HasComment("装备配置表 — 各稀有度装备数值"));
            e.HasIndex(eq => eq.Rarity).IsUnique();
            e.Property(eq => eq.Id).HasComment("主键ID");
            e.Property(eq => eq.Rarity).HasComment("稀有度(0=普通 1=稀有 2=史诗 3=传说)");
            e.Property(eq => eq.TierMultiplier).HasComment("品质倍率");
            e.Property(eq => eq.SellPriceBase).HasComment("基础售价");
            e.Property(eq => eq.UpgradeBaseCost).HasComment("基础强化费用");
            e.Property(eq => eq.UpgradeBoostPct).HasComment("强化加成百分比");
            e.Property(eq => eq.SellPriceUpgradeMult).HasComment("强化售价倍率");
        });

        // ========== 全局配置表 ==========
        modelBuilder.Entity<GameGlobalConfig>(e =>
        {
            e.ToTable(t => t.HasComment("全局配置表 — 键值对配置"));
            e.HasIndex(g => g.ConfigKey).IsUnique();
            e.Property(g => g.ConfigKey).HasMaxLength(64);
            e.Property(g => g.Id).HasComment("主键ID");
            e.Property(g => g.ConfigKey).HasComment("配置键名");
            e.Property(g => g.ConfigValue).HasComment("配置值");
            e.Property(g => g.Description).HasComment("配置描述");
        });

        // ========== 每日任务表 ==========
        modelBuilder.Entity<DailyTask>(e =>
        {
            e.ToTable(t => t.HasComment("每日任务表 — 每用户每天刷新5个任务"));
            e.HasIndex(d => new { d.UserId, d.TaskDate });
            e.HasIndex(d => new { d.UserId, d.TaskDate, d.TaskType });
            e.Property(d => d.Id).HasComment("主键ID");
            e.Property(d => d.UserId).HasComment("用户ID(外键→Users.Id)");
            e.Property(d => d.TaskDate).HasComment("任务日期(yyyy-MM-dd)");
            e.Property(d => d.TaskType).HasComment("任务类型(0=击杀 1=通关 2=消耗金币 3=强化装备 4=获得经验)");
            e.Property(d => d.TargetValue).HasComment("目标值");
            e.Property(d => d.CurrentValue).HasComment("当前进度");
            e.Property(d => d.RewardGold).HasComment("奖励金币");
            e.Property(d => d.Claimed).HasComment("是否已领取");
        });

        // ========== 签到记录表 ==========
        modelBuilder.Entity<SignInRecord>(e =>
        {
            e.ToTable(t => t.HasComment("签到记录表 — 每7天一个周期"));
            e.HasIndex(s => new { s.UserId, s.CycleStart }).IsUnique();
            e.Property(s => s.Id).HasComment("主键ID");
            e.Property(s => s.UserId).HasComment("用户ID(外键→Users.Id)");
            e.Property(s => s.CycleStart).HasComment("签到周期起始日期(yyyy-MM-dd)");
            e.Property(s => s.SignedDays).HasComment("本周已签到天数(0-7)");
            e.Property(s => s.LastSignDate).HasComment("最后签到日期");
        });

        // ========== 排行榜表 ==========
        modelBuilder.Entity<LeaderboardEntry>(e =>
        {
            e.ToTable(t => t.HasComment("排行榜表 — 无尽模式成绩"));
            e.HasIndex(l => l.Wave);
            e.HasIndex(l => l.UserId).IsUnique();
            e.Property(l => l.Id).HasComment("主键ID");
            e.Property(l => l.UserId).HasComment("用户ID(外键→Users.Id)");
            e.Property(l => l.Username).HasComment("用户名(冗余存储)");
            e.Property(l => l.ClassType).HasComment("职业(0=战士 1=法师 2=牧师)");
            e.Property(l => l.Wave).HasComment("无尽模式波数");
            e.Property(l => l.Level).HasComment("角色等级");
            e.Property(l => l.CreatedAt).HasComment("提交时间");
        });

        // ========== 竞技场排行榜表 ==========
        modelBuilder.Entity<ArenaLeaderboardEntry>(e =>
        {
            e.ToTable(t => t.HasComment("竞技场排行榜表 — 竞技场对战成绩"));
            e.HasIndex(l => l.Wins);
            e.HasIndex(l => l.UserId).IsUnique();
            e.Property(l => l.Id).HasComment("主键ID");
            e.Property(l => l.UserId).HasComment("用户ID");
            e.Property(l => l.Username).HasComment("用户名");
            e.Property(l => l.ClassType).HasComment("职业");
            e.Property(l => l.Wins).HasComment("胜场数");
            e.Property(l => l.TotalGames).HasComment("总场次");
            e.Property(l => l.GearScore).HasComment("战力值");
            e.Property(l => l.DefenseRating).HasComment("防守分");
        });

        // ========== 抽卡记录表 ==========
        modelBuilder.Entity<GachaRecord>(e =>
        {
            e.ToTable(t => t.HasComment("抽卡记录表 — 记录用户抽卡历史和保底计数"));
            e.HasIndex(g => new { g.UserId, g.GachaType, g.Id });
            e.Property(g => g.Id).HasComment("主键ID");
            e.Property(g => g.UserId).HasComment("用户ID(外键→Users.Id)");
            e.Property(g => g.GachaType).HasComment("抽卡类型(0=普通 1=高级)");
            e.Property(g => g.Rarity).HasComment("本次抽到的稀有度");
            e.Property(g => g.PityCounter).HasComment("高级抽卡累计次数(保底计数)");
            e.Property(g => g.CreatedAt).HasComment("抽卡时间");
        });

        // ========== 邮件表 ==========
        modelBuilder.Entity<Mail>(e =>
        {
            e.ToTable(t => t.HasComment("邮件表 — 系统邮件/奖励/补偿"));
            e.HasIndex(m => new { m.UserId, m.IsRead });
            e.HasIndex(m => new { m.UserId, m.CreatedAt });
            e.Property(m => m.Id).HasComment("主键ID");
            e.Property(m => m.UserId).HasComment("用户ID(外键→Users.Id)");
            e.Property(m => m.Title).HasComment("邮件标题");
            e.Property(m => m.Content).HasComment("邮件内容");
            e.Property(m => m.AttachmentGold).HasComment("附件金币");
            e.Property(m => m.AttachmentItems).HasComment("附件物品JSON");
            e.Property(m => m.IsRead).HasComment("是否已读");
            e.Property(m => m.Claimed).HasComment("是否已领取附件");
            e.Property(m => m.CreatedAt).HasComment("发送时间");
            e.Property(m => m.ExpireAt).HasComment("过期时间(null=永不过期)");
        });

        // ========== 抽卡保底表 ==========
        modelBuilder.Entity<GachaPity>(e =>
        {
            e.ToTable(t => t.HasComment("抽卡保底计数表 — 每用户每类型一行"));
            e.HasIndex(p => new { p.UserId, p.GachaType }).IsUnique();
            e.Property(p => p.Id).HasComment("主键ID");
            e.Property(p => p.UserId).HasComment("用户ID(外键→Users.Id)");
            e.Property(p => p.GachaType).HasComment("抽卡类型(0=普通 1=高级)");
            e.Property(p => p.PityCounter).HasComment("当前保底累计次数");
        });

        // ========== 好友表 ==========
        modelBuilder.Entity<Friend>(e =>
        {
            e.ToTable(t => t.HasComment("好友表 — 玩家间好友关系"));
            e.HasIndex(f => new { f.UserId, f.FriendId }).IsUnique();
            e.HasIndex(f => f.UserId);
            e.Property(f => f.Status).HasComment("状态: 0=好友, 1=拉黑");
            e.Property(f => f.Remark).HasComment("备注名");
        });

        // ========== 好友申请表 ==========
        modelBuilder.Entity<FriendRequest>(e =>
        {
            e.ToTable(t => t.HasComment("好友申请表"));
            e.HasIndex(r => new { r.FromUserId, r.ToUserId }).IsUnique();
            e.HasIndex(r => new { r.ToUserId, r.Status });
            e.Property(r => r.Message).HasMaxLength(100);
            e.Property(r => r.Status).HasComment("0=待处理 1=已接受 2=已拒绝");
        });

        // ========== 最近一起玩记录表 ==========
        modelBuilder.Entity<RecentPlayer>(e =>
        {
            e.ToTable(t => t.HasComment("最近一起玩记录表"));
            e.HasIndex(r => new { r.UserId, r.OtherUserId }).IsUnique();
            e.HasIndex(r => new { r.UserId, r.LastPlayedAt });
        });

        // ========== 公会表 ==========
        modelBuilder.Entity<Guild>(e =>
        {
            e.ToTable(t => t.HasComment("公会表"));
            e.HasIndex(g => g.LeaderUserId).IsUnique();
            e.HasIndex(g => g.Name);
            e.Property(g => g.Name).HasMaxLength(32);
            e.Property(g => g.Announce).HasMaxLength(200);
        });

        // ========== 公会成员表 ==========
        modelBuilder.Entity<GuildMember>(e =>
        {
            e.ToTable(t => t.HasComment("公会成员表"));
            e.HasIndex(m => new { m.GuildId, m.UserId }).IsUnique();
            e.HasIndex(m => m.UserId);
        });

        // ========== 公会申请/邀请表 ==========
        modelBuilder.Entity<GuildRequest>(e =>
        {
            e.ToTable(t => t.HasComment("公会申请/邀请表"));
            e.HasIndex(r => new { r.ToUserId, r.Status });
            e.HasIndex(r => new { r.GuildId, r.Status });
            e.Property(r => r.Message).HasMaxLength(100);
        });

        // 种子数据
        SeedClassStats(modelBuilder);
        SeedSkillData(modelBuilder);
        SeedEnemyBase(modelBuilder);
        SeedEliteAffix(modelBuilder);
        SeedRuneData(modelBuilder);
        SeedRelicData(modelBuilder);
        SeedEquipmentConfig(modelBuilder);
        SeedGlobalConfig(modelBuilder);
    }

    // ========== 种子数据 ==========

    private static void SeedClassStats(ModelBuilder mb)
    {
        // Warrior
        mb.Entity<GameClassStats>().HasData(new GameClassStats
        {
            Id = 1, ClassId = 0, ClassName = "战士",
            Description = "近战重甲，高生命高防御\n旋风斩/盾击/战吼",
            Skill1Name = "旋风斩", Skill2Name = "盾击", Skill3Name = "战吼",
            BaseHp = 250, BaseAttack = 18, BaseDefense = 8,
            BaseMoveSpeed = 5.5f, BaseAttackRange = 1.8f, BaseAttackCooldown = 0.35f,
            BaseCritChance = 0.08f, BaseHpRegen = 0.5f,
            HpPerLevel = 18, AttackPerLevel = 3, DefensePerLevel = 2,
            IsRanged = false, AutoAttackRange = 2.5f,
            PrimaryR = 0.85f, PrimaryG = 0.2f, PrimaryB = 0.2f, PrimaryA = 1f,
            SecondaryR = 0.7f, SecondaryG = 0.65f, SecondaryB = 0.5f, SecondaryA = 1f,
            Skill1R = 1f, Skill1G = 0.5f, Skill1B = 0.2f, Skill1A = 1f,
            Skill2R = 0.6f, Skill2G = 0.7f, Skill2B = 1f, Skill2A = 1f,
            Skill3R = 1f, Skill3G = 0.9f, Skill3B = 0.3f, Skill3A = 1f,
        });
        // Mage
        mb.Entity<GameClassStats>().HasData(new GameClassStats
        {
            Id = 2, ClassId = 1, ClassName = "法师",
            Description = "远程魔法，高攻击低生命\n火球/冰新星/奥术冲击",
            Skill1Name = "火球术", Skill2Name = "冰新星", Skill3Name = "奥术冲击",
            BaseHp = 120, BaseAttack = 25, BaseDefense = 3,
            BaseMoveSpeed = 5.8f, BaseAttackRange = 6f, BaseAttackCooldown = 0.5f,
            BaseCritChance = 0.12f, BaseHpRegen = 0.2f,
            HpPerLevel = 8, AttackPerLevel = 5, DefensePerLevel = 1,
            IsRanged = true, AutoAttackRange = 6f,
            PrimaryR = 0.3f, PrimaryG = 0.4f, PrimaryB = 0.9f, PrimaryA = 1f,
            SecondaryR = 0.7f, SecondaryG = 0.5f, SecondaryB = 1f, SecondaryA = 1f,
            Skill1R = 1f, Skill1G = 0.4f, Skill1B = 0.1f, Skill1A = 1f,
            Skill2R = 0.4f, Skill2G = 0.8f, Skill2B = 1f, Skill2A = 1f,
            Skill3R = 0.7f, Skill3G = 0.3f, Skill3B = 1f, Skill3A = 1f,
        });
        // Priest
        mb.Entity<GameClassStats>().HasData(new GameClassStats
        {
            Id = 3, ClassId = 2, ClassName = "牧师",
            Description = "治疗辅助，攻守兼备\n圣光弹/治疗术/神圣护盾",
            Skill1Name = "圣光弹", Skill2Name = "治疗术", Skill3Name = "神圣护盾",
            BaseHp = 180, BaseAttack = 15, BaseDefense = 5,
            BaseMoveSpeed = 5.6f, BaseAttackRange = 5f, BaseAttackCooldown = 0.45f,
            BaseCritChance = 0.06f, BaseHpRegen = 1.0f,
            HpPerLevel = 12, AttackPerLevel = 3, DefensePerLevel = 1,
            IsRanged = true, AutoAttackRange = 5f,
            PrimaryR = 1f, PrimaryG = 0.95f, PrimaryB = 0.6f, PrimaryA = 1f,
            SecondaryR = 0.95f, SecondaryG = 0.9f, SecondaryB = 0.7f, SecondaryA = 1f,
            Skill1R = 1f, Skill1G = 1f, Skill1B = 0.5f, Skill1A = 1f,
            Skill2R = 0.3f, Skill2G = 1f, Skill2B = 0.5f, Skill2A = 1f,
            Skill3R = 0.8f, Skill3G = 0.9f, Skill3B = 1f, Skill3A = 1f,
        });
    }

    private static void SeedSkillData(ModelBuilder mb)
    {
        // Warrior: 旋风斩 / 盾击 / 战吼
        mb.Entity<GameSkillData>().HasData(new GameSkillData
        {
            Id = 1, ClassId = 0, SkillSlot = 0, Name = "旋风斩", Description = "旋转斩击周围所有敌人",
            Cooldown = 5f, Range = 2.5f, DamageMultiplier = 1.5f,
            CooldownPerLevel = "5,4.5,4,3.5,3", DamageMultiplierPerLevel = "1.5,1.7,1.9,2.2,2.5",
            RangePerLevel = "2.5,2.7,2.9,3.2,3.5",
            EvolutionName = "龙卷风", EvolutionDescription = "范围+50%，吸引敌人",
            EvolutionDamageMultiplier = 3f, EvolutionProjectileBonus = 0
        });
        mb.Entity<GameSkillData>().HasData(new GameSkillData
        {
            Id = 2, ClassId = 0, SkillSlot = 1, Name = "盾击", Description = "盾牌猛击前方敌人并击退",
            Cooldown = 3f, Range = 2f, DamageMultiplier = 1.2f,
            CooldownPerLevel = "3,2.7,2.4,2.1,1.8", DamageMultiplierPerLevel = "1.2,1.4,1.6,1.9,2.2",
            RangePerLevel = "2,2.2,2.4,2.7,3",
            EvolutionName = "破甲猛击", EvolutionDescription = "无视敌人防御",
            EvolutionDamageMultiplier = 2.8f, EvolutionProjectileBonus = 0
        });
        mb.Entity<GameSkillData>().HasData(new GameSkillData
        {
            Id = 3, ClassId = 0, SkillSlot = 2, Name = "战吼", Description = "提升攻击力和防御力8秒",
            Cooldown = 10f, Range = 0f, DamageMultiplier = 0f,
            CooldownPerLevel = "10,9,8,7,6", DamageMultiplierPerLevel = "0.2,0.25,0.3,0.4,0.5",
            RangePerLevel = "0,0,0,0,0",
            EvolutionName = "狂暴", EvolutionDescription = "持续+4秒，额外+20%攻速",
            EvolutionDamageMultiplier = 0.6f, EvolutionProjectileBonus = 0
        });
        // Mage: 火球术 / 冰新星 / 奥术冲击
        mb.Entity<GameSkillData>().HasData(new GameSkillData
        {
            Id = 4, ClassId = 1, SkillSlot = 0, Name = "火球术", Description = "发射爆炸火球，范围伤害",
            Cooldown = 3f, Range = 8f, DamageMultiplier = 2f,
            CooldownPerLevel = "3,2.7,2.4,2.1,1.8", DamageMultiplierPerLevel = "2,2.3,2.6,3,3.5",
            RangePerLevel = "8,8.5,9,9.5,10",
            EvolutionName = "流星火雨", EvolutionDescription = "3个火球同时落下",
            EvolutionDamageMultiplier = 4f, EvolutionProjectileBonus = 2
        });
        mb.Entity<GameSkillData>().HasData(new GameSkillData
        {
            Id = 5, ClassId = 1, SkillSlot = 1, Name = "冰新星", Description = "以自身为中心释放冰霜新星",
            Cooldown = 6f, Range = 3f, DamageMultiplier = 1.8f,
            CooldownPerLevel = "6,5.5,5,4.5,4", DamageMultiplierPerLevel = "1.8,2,2.3,2.6,3",
            RangePerLevel = "3,3.3,3.6,4,4.5",
            EvolutionName = "暴风雪", EvolutionDescription = "范围+50%，冰冻敌人2秒",
            EvolutionDamageMultiplier = 3.5f, EvolutionProjectileBonus = 0
        });
        mb.Entity<GameSkillData>().HasData(new GameSkillData
        {
            Id = 6, ClassId = 1, SkillSlot = 2, Name = "奥术冲击", Description = "蓄力发射强力奥术光束",
            Cooldown = 8f, Range = 10f, DamageMultiplier = 3f,
            CooldownPerLevel = "8,7.5,7,6.5,6", DamageMultiplierPerLevel = "3,3.5,4,4.5,5",
            RangePerLevel = "10,10,11,11,12",
            EvolutionName = "虚空射线", EvolutionDescription = "穿透全屏，击中所有敌人",
            EvolutionDamageMultiplier = 5.5f, EvolutionProjectileBonus = 0
        });
        // Priest: 圣光弹 / 治疗术 / 神圣护盾
        mb.Entity<GameSkillData>().HasData(new GameSkillData
        {
            Id = 7, ClassId = 2, SkillSlot = 0, Name = "圣光弹", Description = "发射追踪圣光弹攻击敌人",
            Cooldown = 2.5f, Range = 7f, DamageMultiplier = 1.5f,
            CooldownPerLevel = "2.5,2.2,2,1.8,1.5", DamageMultiplierPerLevel = "1.5,1.7,1.9,2.2,2.5",
            RangePerLevel = "7,7.5,8,8.5,9",
            EvolutionName = "圣光审判", EvolutionDescription = "穿透+爆炸范围伤害",
            EvolutionDamageMultiplier = 3f, EvolutionProjectileBonus = 0
        });
        mb.Entity<GameSkillData>().HasData(new GameSkillData
        {
            Id = 8, ClassId = 2, SkillSlot = 1, Name = "治疗术", Description = "恢复自身大量生命值",
            Cooldown = 5f, Range = 0f, DamageMultiplier = 0f,
            CooldownPerLevel = "5,4.5,4,3.5,3", DamageMultiplierPerLevel = "0.3,0.35,0.4,0.5,0.6",
            RangePerLevel = "0,0,0,0,0",
            EvolutionName = "群体治疗", EvolutionDescription = "治疗量+50%，同时获得护盾",
            EvolutionDamageMultiplier = 0.9f, EvolutionProjectileBonus = 0
        });
        mb.Entity<GameSkillData>().HasData(new GameSkillData
        {
            Id = 9, ClassId = 2, SkillSlot = 2, Name = "神圣护盾", Description = "获得护盾吸收伤害5秒",
            Cooldown = 12f, Range = 0f, DamageMultiplier = 0f,
            CooldownPerLevel = "12,11,10,9,8", DamageMultiplierPerLevel = "0.2,0.25,0.3,0.4,0.5",
            RangePerLevel = "0,0,0,0,0",
            EvolutionName = "神圣之翼", EvolutionDescription = "护盾+反击伤害",
            EvolutionDamageMultiplier = 0.6f, EvolutionProjectileBonus = 0
        });
    }

    private static void SeedEnemyBase(ModelBuilder mb)
    {
        mb.Entity<GameEnemyBase>().HasData(new GameEnemyBase
        {
            Id = 1, EnemyType = 0, // Melee
            BaseHp = 30, BaseAttack = 8, BaseDefense = 2, BaseMoveSpeed = 2f, BaseXpReward = 20,
            AttackRange = 2f, AttackCooldown = 1.5f,
            ScaleEarly = 0.7f, ScaleEarlyStep = 0.15f,
            ScaleMid = 1.0f, ScaleMidStep = 0.2f,
            ScaleLate = 1.6f, ScaleLateStep = 0.35f,
            MoveSpeedEarlyMult = 0.8f, MoveSpeedMidStep = 0.15f,
            MoveSpeedLateBase = 0.45f, MoveSpeedLateStep = 0.2f,
            EnemyAttackMult = 1.5f,
            BossHpMult = 3f, BossAtkMult = 1.5f, BossDefBonusBase = 1, BossXpMult = 5f,
            EliteHpMult = 2f, EliteAtkMult = 1.3f, EliteXpMult = 3f,
            AttackCdScaleStartLevel = 4, AttackCdScalePerLevel = 0.08f, AttackCdScaleMin = 0.4f,
            MinionBaseHp = 10, MinionBaseAttack = 4, MinionBaseMoveSpeed = 3f, MinionBaseXpReward = 5
        });
    }

    private static void SeedEliteAffix(ModelBuilder mb)
    {
        mb.Entity<GameEliteAffix>().HasData(new GameEliteAffix
        {
            Id = 1, AffixType = 0, Name = "烈焰", Description = "周围的敌人会被灼烧",
            AuraRadius = 2.5f, AuraDamage = 5,
            AffixScaleBase = 1f, AffixScalePerLevel = 0.1f,
            ColorR = 1f, ColorG = 0.4f, ColorB = 0.1f, ColorA = 0.5f
        });
        mb.Entity<GameEliteAffix>().HasData(new GameEliteAffix
        {
            Id = 2, AffixType = 1, Name = "传送", Description = "可以瞬间移动到玩家身边",
            TeleportCooldown = 5f, TeleportRange = 6f,
            AffixScaleBase = 1f, AffixScalePerLevel = 0.1f,
            ColorR = 0.6f, ColorG = 0.3f, ColorB = 1f, ColorA = 0.5f
        });
        mb.Entity<GameEliteAffix>().HasData(new GameEliteAffix
        {
            Id = 3, AffixType = 2, Name = "护盾", Description = "拥有可以吸收伤害的护盾",
            ShieldHp = 30, ShieldRegenDelay = 8f,
            AffixScaleBase = 1f, AffixScalePerLevel = 0.1f,
            ColorR = 0.3f, ColorG = 0.7f, ColorB = 1f, ColorA = 0.5f
        });
        mb.Entity<GameEliteAffix>().HasData(new GameEliteAffix
        {
            Id = 4, AffixType = 3, Name = "召唤", Description = "定期召唤小怪助战",
            SpawnInterval = 6f, SpawnCount = 2,
            AffixScaleBase = 1f, AffixScalePerLevel = 0.1f,
            ColorR = 0.4f, ColorG = 0.9f, ColorB = 0.3f, ColorA = 0.5f
        });
        mb.Entity<GameEliteAffix>().HasData(new GameEliteAffix
        {
            Id = 5, AffixType = 4, Name = "疾风", Description = "移动和攻击速度大幅提升",
            HasteSpeedMult = 1.6f, HasteAttackMult = 0.6f,
            AffixScaleBase = 1f, AffixScalePerLevel = 0.1f,
            ColorR = 0.9f, ColorG = 0.9f, ColorB = 0.2f, ColorA = 0.5f
        });
        mb.Entity<GameEliteAffix>().HasData(new GameEliteAffix
        {
            Id = 6, AffixType = 5, Name = "反弹", Description = "将部分伤害反弹给攻击者",
            ReflectPct = 0.3f,
            AffixScaleBase = 1f, AffixScalePerLevel = 0.1f,
            ColorR = 0.9f, ColorG = 0.5f, ColorB = 0.9f, ColorA = 0.5f
        });
        mb.Entity<GameEliteAffix>().HasData(new GameEliteAffix
        {
            Id = 7, AffixType = 6, Name = "再生", Description = "持续恢复生命值",
            RegenPerSecond = 3,
            AffixScaleBase = 1f, AffixScalePerLevel = 0.1f,
            ColorR = 0.2f, ColorG = 1f, ColorB = 0.5f, ColorA = 0.5f
        });
        mb.Entity<GameEliteAffix>().HasData(new GameEliteAffix
        {
            Id = 8, AffixType = 7, Name = "连锁", Description = "死亡时强化周围的敌人",
            ChainBuffRadius = 5f, ChainBuffDuration = 10f,
            AffixScaleBase = 1f, AffixScalePerLevel = 0.1f,
            ColorR = 1f, ColorG = 0.6f, ColorB = 0.2f, ColorA = 0.5f
        });
    }

    private static void SeedRuneData(ModelBuilder mb)
    {
        mb.Entity<GameRuneData>().HasData(new GameRuneData
        {
            Id = 1, RuneType = 0, Name = "散射", Description = "投射物+2，伤害降低",
            DamageMultBase = 0.7f, DamageMultPerLevel = 0.05f, DamageMultMin = 0.4f,
            ProjectileBonus = 2,
            ColorR = 0.4f, ColorG = 0.8f, ColorB = 1f
        });
        mb.Entity<GameRuneData>().HasData(new GameRuneData
        {
            Id = 2, RuneType = 1, Name = "穿透", Description = "投射物穿透敌人",
            DamageMultBase = 0.8f, DamageMultPerLevel = 0.05f, DamageMultMin = 0.5f,
            PierceBase = 0, PiercePerLevel = 1,
            ColorR = 0.9f, ColorG = 0.9f, ColorB = 0.3f
        });
        mb.Entity<GameRuneData>().HasData(new GameRuneData
        {
            Id = 3, RuneType = 2, Name = "连锁", Description = "命中后弹射到附近敌人",
            DamageMultBase = 0.6f, DamageMultPerLevel = 0.05f, DamageMultMin = 0.3f,
            ChainBase = 1, ChainPerLevel = 1,
            ColorR = 0.5f, ColorG = 0.3f, ColorB = 1f
        });
        mb.Entity<GameRuneData>().HasData(new GameRuneData
        {
            Id = 4, RuneType = 3, Name = "燃烧", Description = "点燃地面，持续伤害",
            DamageMultBase = 0.85f, DamageMultPerLevel = 0f, DamageMultMin = 0.85f,
            BurnDpsBase = 0.15f, BurnDpsPerLevel = 0.05f,
            BurnDurationBase = 2f, BurnDurationPerLevel = 0.5f,
            ColorR = 1f, ColorG = 0.4f, ColorB = 0.1f
        });
        mb.Entity<GameRuneData>().HasData(new GameRuneData
        {
            Id = 5, RuneType = 4, Name = "范围扩大", Description = "AOE范围大幅增加",
            DamageMultBase = 0.85f, DamageMultPerLevel = 0.03f, DamageMultMin = 0.6f,
            AoeRangeMultBase = 1.3f, AoeRangeMultPerLevel = 0.1f,
            ColorR = 0.3f, ColorG = 1f, ColorB = 0.5f
        });
        mb.Entity<GameRuneData>().HasData(new GameRuneData
        {
            Id = 6, RuneType = 5, Name = "元素转换", Description = "伤害转为元素，附带元素效果",
            DamageMultBase = 1f, DamageMultPerLevel = 0f, DamageMultMin = 1f,
            ColorR = 0.6f, ColorG = 0.5f, ColorB = 1f
        });
        mb.Entity<GameRuneData>().HasData(new GameRuneData
        {
            Id = 7, RuneType = 6, Name = "吸血", Description = "伤害转化为生命",
            DamageMultBase = 0.75f, DamageMultPerLevel = 0.05f, DamageMultMin = 0.4f,
            LifeStealBase = 0.1f, LifeStealPerLevel = 0.03f,
            ColorR = 0.9f, ColorG = 0.1f, ColorB = 0.3f
        });
        mb.Entity<GameRuneData>().HasData(new GameRuneData
        {
            Id = 8, RuneType = 7, Name = "减速", Description = "命中敌人大幅减速",
            DamageMultBase = 0.9f, DamageMultPerLevel = 0.03f, DamageMultMin = 0.7f,
            SlowPctBase = 0.3f, SlowPctPerLevel = 0.05f,
            SlowDurationBase = 1.5f, SlowDurationPerLevel = 0.5f,
            ColorR = 0.2f, ColorG = 0.6f, ColorB = 0.9f
        });
    }

    private static void SeedRelicData(ModelBuilder mb)
    {
        mb.Entity<GameRelicData>().HasData(new GameRelicData
        {
            Id = 1, RelicType = 0, Name = "分裂之矢", Description = "投射物命中时分裂出2个小投射物",
            Rarity = 1, SplitCount = 2,
            ColorR = 0.3f, ColorG = 0.9f, ColorB = 1f,
            RollLegendaryBase = 0.05f, RollLegendaryPerStage = 0.02f,
            RollRareBase = 0.25f, RollRarePerStage = 0.03f
        });
        mb.Entity<GameRelicData>().HasData(new GameRelicData
        {
            Id = 2, RelicType = 1, Name = "生命汲取", Description = "击杀敌人恢复15点生命",
            Rarity = 0, KillHealAmount = 15,
            ColorR = 0.2f, ColorG = 1f, ColorB = 0.3f,
            RollLegendaryBase = 0.05f, RollLegendaryPerStage = 0.02f,
            RollRareBase = 0.25f, RollRarePerStage = 0.03f
        });
        mb.Entity<GameRelicData>().HasData(new GameRelicData
        {
            Id = 3, RelicType = 2, Name = "暴击风暴", Description = "暴击时产生范围爆炸(2.5m)",
            Rarity = 1, CritBurstRadius = 2.5f,
            ColorR = 1f, ColorG = 0.9f, ColorB = 0.1f,
            RollLegendaryBase = 0.05f, RollLegendaryPerStage = 0.02f,
            RollRareBase = 0.25f, RollRarePerStage = 0.03f
        });
        mb.Entity<GameRelicData>().HasData(new GameRelicData
        {
            Id = 4, RelicType = 3, Name = "杀意涌动", Description = "击杀后2秒内移动速度+50%",
            Rarity = 0, SpeedBoostDuration = 2f, SpeedBoostMult = 1.5f,
            ColorR = 0.9f, ColorG = 0.5f, ColorB = 1f,
            RollLegendaryBase = 0.05f, RollLegendaryPerStage = 0.02f,
            RollRareBase = 0.25f, RollRarePerStage = 0.03f
        });
        mb.Entity<GameRelicData>().HasData(new GameRelicData
        {
            Id = 5, RelicType = 4, Name = "血之祭祀", Description = "技能5%HP消耗换40%伤害加成",
            Rarity = 2, BloodMagicDamageBonus = 1.4f, BloodMagicHpCostPct = 0.05f,
            ColorR = 0.9f, ColorG = 0.1f, ColorB = 0.1f,
            RollLegendaryBase = 0.05f, RollLegendaryPerStage = 0.02f,
            RollRareBase = 0.25f, RollRarePerStage = 0.03f
        });
        mb.Entity<GameRelicData>().HasData(new GameRelicData
        {
            Id = 6, RelicType = 5, Name = "雷霆链", Description = "命中时闪电弹射到附近2个敌人",
            Rarity = 1, ChainTargets = 2,
            ColorR = 0.5f, ColorG = 0.7f, ColorB = 1f,
            RollLegendaryBase = 0.05f, RollLegendaryPerStage = 0.02f,
            RollRareBase = 0.25f, RollRarePerStage = 0.03f
        });
        mb.Entity<GameRelicData>().HasData(new GameRelicData
        {
            Id = 7, RelicType = 6, Name = "荆棘之盾", Description = "受到伤害时反弹30%给攻击者",
            Rarity = 0, ThornsPct = 0.3f,
            ColorR = 0.6f, ColorG = 0.8f, ColorB = 0.2f,
            RollLegendaryBase = 0.05f, RollLegendaryPerStage = 0.02f,
            RollRareBase = 0.25f, RollRarePerStage = 0.03f
        });
        mb.Entity<GameRelicData>().HasData(new GameRelicData
        {
            Id = 8, RelicType = 7, Name = "双刃", Description = "20%概率普攻双重打击",
            Rarity = 0, DoubleStrikeChance = 0.2f,
            ColorR = 1f, ColorG = 0.6f, ColorB = 0.2f,
            RollLegendaryBase = 0.05f, RollLegendaryPerStage = 0.02f,
            RollRareBase = 0.25f, RollRarePerStage = 0.03f
        });
        mb.Entity<GameRelicData>().HasData(new GameRelicData
        {
            Id = 9, RelicType = 8, Name = "回光返照", Description = "受伤时10%概率恢复20HP",
            Rarity = 0, DodgeHealChance = 0.1f, DodgeHealAmount = 20,
            ColorR = 0.3f, ColorG = 1f, ColorB = 0.9f,
            RollLegendaryBase = 0.05f, RollLegendaryPerStage = 0.02f,
            RollRareBase = 0.25f, RollRarePerStage = 0.03f
        });
        mb.Entity<GameRelicData>().HasData(new GameRelicData
        {
            Id = 10, RelicType = 9, Name = "智慧之心", Description = "经验获取+30%",
            Rarity = 0, XpBonus = 0.3f,
            ColorR = 0.7f, ColorG = 0.5f, ColorB = 1f,
            RollLegendaryBase = 0.05f, RollLegendaryPerStage = 0.02f,
            RollRareBase = 0.25f, RollRarePerStage = 0.03f
        });
    }

    private static void SeedEquipmentConfig(ModelBuilder mb)
    {
        mb.Entity<GameEquipmentConfig>().HasData(new GameEquipmentConfig
        {
            Id = 1, Rarity = 0, TierMultiplier = 1f, SellPriceBase = 20, UpgradeBaseCost = 30,
            UpgradeBoostPct = 0.15f, SellPriceUpgradeMult = 1.2f
        });
        mb.Entity<GameEquipmentConfig>().HasData(new GameEquipmentConfig
        {
            Id = 2, Rarity = 1, TierMultiplier = 1.5f, SellPriceBase = 60, UpgradeBaseCost = 80,
            UpgradeBoostPct = 0.15f, SellPriceUpgradeMult = 1.2f
        });
        mb.Entity<GameEquipmentConfig>().HasData(new GameEquipmentConfig
        {
            Id = 3, Rarity = 2, TierMultiplier = 2.5f, SellPriceBase = 180, UpgradeBaseCost = 200,
            UpgradeBoostPct = 0.15f, SellPriceUpgradeMult = 1.2f
        });
        mb.Entity<GameEquipmentConfig>().HasData(new GameEquipmentConfig
        {
            Id = 4, Rarity = 3, TierMultiplier = 4f, SellPriceBase = 500, UpgradeBaseCost = 500,
            UpgradeBoostPct = 0.15f, SellPriceUpgradeMult = 1.2f
        });
    }

    private static void SeedGlobalConfig(ModelBuilder mb)
    {
        var globals = new (string Key, string Value, string Desc)[]
        {
            ("CritMultiplier", "2.0", "暴击伤害倍率"),
            ("InvincibilityTime", "0.5", "受伤后无敌时间(秒)"),
            ("ComboTimeout", "2.0", "连击超时时间(秒)"),
            ("DashCooldown", "1.2", "冲刺冷却(秒)"),
            ("DashSpeed", "28.0", "冲刺速度"),
            ("DashDuration", "0.12", "冲刺持续时间(秒)"),
            ("XpFormulaBase", "50", "经验公式基础值"),
            ("XpFormulaPerLevel", "30", "经验公式每级增量"),
            ("AutoAttackProjectileSpeed", "8.0", "普攻投射物速度"),
            ("SkillProjectileSpeedDefault", "10.0", "技能投射物默认速度"),
            ("SkillProjectileSpeedPriest", "9.0", "牧师技能投射物速度"),
            ("SkillProjectileSpeedSkill3", "12.0", "Skill3投射物速度"),
            ("BuffDuration", "8.0", "战吼Buff持续时间(秒)"),
            ("BuffDurationEvolved", "12.0", "进化战吼Buff持续时间(秒)"),
            ("ShieldDuration", "5.0", "护盾持续时间(秒)"),
            ("ShieldDurationEvolved", "7.0", "进化护盾持续时间(秒)"),
            ("BurnTickInterval", "0.5", "燃烧伤害tick间隔(秒)"),
            ("MaxBackpackSize", "20", "背包最大容量"),
            ("MaxUpgradeLevel", "10", "装备最大强化等级"),
            ("EquipmentLevelScaleBase", "1.0", "装备等级缩放基础值"),
            ("EquipmentLevelScalePerLevel", "0.15", "装备等级缩放每级增量"),
            ("DropChanceBoss", "1.0", "Boss掉落概率"),
            ("DropChanceElite", "0.6", "精英掉落概率"),
            ("DropChanceNormal", "0.35", "普通敌人掉落概率"),
            ("RarityLegendaryBase", "0.02", "传说掉率基础值"),
            ("RarityLegendaryPerLevel", "0.005", "传说掉率每级增量"),
            ("RarityEpicBase", "0.08", "史诗掉率基础值"),
            ("RarityEpicPerLevel", "0.01", "史诗掉率每级增量"),
            ("RarityRareBase", "0.2", "稀有掉率基础值"),
            ("RarityRarePerLevel", "0.02", "稀有掉率每级增量"),
            ("ShopRareChanceBase", "0.25", "商店稀有概率基础值"),
            ("ShopRareChancePerLevel", "0.02", "商店稀有概率每级增量"),
        };

        for (int i = 0; i < globals.Length; i++)
        {
            mb.Entity<GameGlobalConfig>().HasData(new GameGlobalConfig
            {
                Id = i + 1,
                ConfigKey = globals[i].Key,
                ConfigValue = globals[i].Value,
                Description = globals[i].Desc
            });
        }
    }
}
