using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ArpgServer.Data;
using ArpgServer.Services;

var builder = WebApplication.CreateBuilder(args);

// ========== 1. EF Core + MySQL ==========
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseMySql(
        builder.Configuration.GetConnectionString("Default"),
        ServerVersion.Parse("8.0.0")
    ));

// ========== 1b. Redis 缓存 (游戏配置缓存) ==========
builder.Services.AddSingleton<RedisCacheService>();

// ========== 2. JWT 认证配置 ==========
builder.Services.AddSingleton<JwtService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });
builder.Services.AddAuthorization();

// ========== 3. CORS — 允许 Unity 客户端跨域请求 ==========
builder.Services.AddCors(opt =>
{
    opt.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHttpClient(); // 用于TeamController向Game Server发HTTP请求

var app = builder.Build();

// ========== 4. 自动建库建表 ==========
// EnsureCreated 只能创建全新数据库; 已有数据库不会新增表
// 迁移检测: 检查 PlayerSave.Slot 列 + Users 表备注是否存在
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    bool needsRecreate = false;

    try
    {
        _ = db.GameClassStats.OrderBy(g => g.Id).FirstOrDefault();

        // 检查 Slot 列 (多角色槽位升级)
        try { _ = db.PlayerSaves.Where(s => s.Slot == 0).OrderBy(s => s.Id).FirstOrDefault(); }
        catch { needsRecreate = true; }

        // 检查元系统表 (每日任务/签到/排行榜/抽卡/邮件)
        if (!needsRecreate)
        {
            try { _ = db.DailyTasks.OrderBy(d => d.Id).FirstOrDefault(); }
            catch { needsRecreate = true; }
        }

        // 检查表备注 (中文备注升级)
        if (!needsRecreate)
        {
            try
            {
                var conn = db.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open)
                    conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT TABLE_COMMENT FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Users' LIMIT 1";
                if (string.IsNullOrEmpty(cmd.ExecuteScalar()?.ToString()))
                    needsRecreate = true;
            }
            catch { }
        }
    }
    catch { needsRecreate = true; }

    if (needsRecreate)
    {
        Console.WriteLine("数据库结构变更, 正在补充表...");
        db.Database.EnsureCreated();
    }
    else
    {
        db.Database.EnsureCreated();
    }

    // 好友系统表: EnsureCreated不会给已有数据库新增表, 手动创建
    try
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) conn.Open();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS `Friends` (
    `Id` INT AUTO_INCREMENT PRIMARY KEY,
    `UserId` INT NOT NULL,
    `FriendId` INT NOT NULL,
    `Status` INT DEFAULT 0,
    `Remark` VARCHAR(50) DEFAULT '',
    `CreatedAt` DATETIME DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY `UX_Friends_UserId_FriendId` (`UserId`, `FriendId`),
    INDEX `IX_Friends_UserId` (`UserId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS `FriendRequests` (
    `Id` INT AUTO_INCREMENT PRIMARY KEY,
    `FromUserId` INT NOT NULL,
    `ToUserId` INT NOT NULL,
    `Message` VARCHAR(100) DEFAULT '',
    `Status` INT DEFAULT 0,
    `CreatedAt` DATETIME DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt` DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY `UX_FriendRequests_From_To` (`FromUserId`, `ToUserId`),
    INDEX `IX_FriendRequests_To_Status` (`ToUserId`, `Status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS `RecentPlayers` (
    `Id` INT AUTO_INCREMENT PRIMARY KEY,
    `UserId` INT NOT NULL,
    `OtherUserId` INT NOT NULL,
    `PlayCount` INT DEFAULT 1,
    `LastPlayedAt` DATETIME DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY `UX_Recent_UserId_Other` (`UserId`, `OtherUserId`),
    INDEX `IX_Recent_UserId_Last` (`UserId`, `LastPlayedAt`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
        cmd.ExecuteNonQuery();

        // 公会系统表
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS `Guilds` (
    `Id` INT AUTO_INCREMENT PRIMARY KEY,
    `Name` VARCHAR(32) NOT NULL,
    `LeaderUserId` INT NOT NULL,
    `Level` INT DEFAULT 1,
    `Exp` INT DEFAULT 0,
    `Announce` VARCHAR(200) DEFAULT '欢迎加入公会！',
    `MaxMembers` INT DEFAULT 30,
    `CreatedAt` DATETIME DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY `UX_Guilds_Leader` (`LeaderUserId`),
    INDEX `IX_Guilds_Name` (`Name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS `GuildMembers` (
    `Id` INT AUTO_INCREMENT PRIMARY KEY,
    `GuildId` INT NOT NULL,
    `UserId` INT NOT NULL,
    `Rank` INT DEFAULT 3,
    `Contribution` INT DEFAULT 0,
    `JoinedAt` DATETIME DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY `UX_GuildMembers_Guild_User` (`GuildId`, `UserId`),
    INDEX `IX_GuildMembers_UserId` (`UserId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS `GuildRequests` (
    `Id` INT AUTO_INCREMENT PRIMARY KEY,
    `GuildId` INT NOT NULL,
    `FromUserId` INT NOT NULL,
    `ToUserId` INT NOT NULL,
    `Type` INT DEFAULT 0,
    `Status` INT DEFAULT 0,
    `Message` VARCHAR(100) DEFAULT '',
    `CreatedAt` DATETIME DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt` DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX `IX_GuildRequests_To_Status` (`ToUserId`, `Status`),
    INDEX `IX_GuildRequests_Guild_Status` (`GuildId`, `Status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
        cmd.ExecuteNonQuery();

        Console.WriteLine("[DB] 好友/公会系统表已创建/验证");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[DB] 好友表创建失败(可能已存在): {ex.Message}");
    }
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// 固定端口为5132 (必须在MapControllers之前)
app.Urls.Add("http://0.0.0.0:5132");

app.MapControllers();

app.Run();
