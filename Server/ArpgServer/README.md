# ArpgServer — 玩家存档云端同步服务

## 技术栈
- ASP.NET Core Web API (.NET 10)
- MySQL 8.0+ (或 MariaDB 10.5+)
- JWT Bearer Token 认证
- EF Core (Pomelo MySQL 驱动)
- BCrypt 密码哈希

## 环境准备

### 1. 安装 MySQL
- 下载 MySQL Community Server: https://dev.mysql.com/downloads/mysql/
- 安装时设置 root 密码
- 或使用 MariaDB: https://mariadb.org/download/

### 2. 配置连接字符串
打开 `appsettings.json`, 修改 `ConnectionStrings:Default`:
```
Server=127.0.0.1;Port=3306;Database=ArpgServer;Uid=root;Pwd=你的密码;Charset=utf8mb4;
```

### 3. 还原 NuGet 包
```bash
cd Server/ArpgServer
dotnet restore
```

### 4. 启动服务器
```bash
dotnet run
```
- 首次启动会自动创建数据库和表 (EnsureCreated)
- 默认地址: http://localhost:5000
- 开发模式下可访问 http://localhost:5000/openapi 查看 API 文档

## API 接口

### 注册
```
POST /api/auth/register
Body: { "username": "player1", "password": "123456" }
返回: { "token": "eyJ...", "username": "player1" }
```

### 登录
```
POST /api/auth/login
Body: { "username": "player1", "password": "123456" }
返回: { "token": "eyJ...", "username": "player1" }
```

### 上传存档
```
POST /api/save/upload
Header: Authorization: Bearer {token}
Body: { "saveJson": "{...完整存档JSON...}" }
返回: { "success": true, "updatedAt": "2026-06-22T..." }
```

### 下载存档
```
GET /api/save/load
Header: Authorization: Bearer {token}
返回: { "saveJson": "{...}", "updatedAt": "...", "found": true }
```

### 检查是否有存档
```
GET /api/save/exists
Header: Authorization: Bearer {token}
返回: { "exists": true }
```

## 文件结构
```
Server/ArpgServer/
├── Program.cs              # 入口 + DI + JWT + MySQL 配置
├── appsettings.json        # MySQL 连接字符串 + JWT 密钥
├── Models/
│   └── AppUser.cs          # 数据模型 (User + PlayerSave + DTO)
├── Data/
│   └── AppDbContext.cs     # EF Core 数据库上下文
├── Services/
│   └── JwtService.cs       # JWT Token 生成
└── Controllers/
    ├── AuthController.cs   # 注册 + 登录 API
    └── SaveController.cs   # 存档上传 + 下载 API
```

## 数据库表结构
```
Users
  Id              INT PRIMARY KEY AUTO_INCREMENT
  Username        VARCHAR(32) UNIQUE NOT NULL
  PasswordHash    NVARCHAR(72) NOT NULL     -- BCrypt 哈希
  CreatedAt       DATETIME
  LastLoginAt     DATETIME NULL

PlayerSaves
  Id              INT PRIMARY KEY AUTO_INCREMENT
  UserId          INT UNIQUE NOT NULL        -- FK → Users.Id
  SaveJson        LONGTEXT                   -- 完整存档 JSON
  UpdatedAt       DATETIME
```
