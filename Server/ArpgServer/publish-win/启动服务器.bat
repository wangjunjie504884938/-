@echo off
chcp 65001 >nul
echo ========================================
echo   卫冕战争 - 游戏服务器
echo ========================================
echo.
echo 首次运行前请确认:
echo   1. 已安装 MySQL 8.0+ (或 MariaDB)
echo   2. 已安装 Redis (可选, 不装也能用)
echo   3. 已修改 appsettings.json 中的数据库连接信息
echo.
echo 正在启动服务器...
echo.
ArpgServer.exe
pause
