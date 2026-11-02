using System;
using System.IO;
using System.Text;

namespace ArpgServer.Services
{
    /// <summary>
    /// 按天滚动文件日志 (规划阶段10) — 结构化追踪字段 + 大小限制 + 保留期清理。
    /// 币种/订单/会话等关键事件走此通道, 供对账与交易追踪。
    /// </summary>
    public static class FileLoggerService
    {
        private static readonly object _lock = new object();
        private static string _logDir = Path.Combine(AppContext.BaseDirectory, "logs");
        private const long MaxFileBytes = 20 * 1024 * 1024; // 单文件20MB
        private const int RetainDays = 14;

        /// <summary>结构化追踪行: 时间 | 类别 | 追踪Id | 内容。敏感字段调用方自行脱敏。</summary>
        public static void Log(string category, string traceId, string message)
        {
            try
            {
                lock (_lock)
                {
                    Directory.CreateDirectory(_logDir);
                    var file = Path.Combine(_logDir, $"srv_{DateTime.UtcNow:yyyyMMdd}.log");

                    // 大小限制: 超20MB滚动为 .1 文件
                    if (File.Exists(file) && new FileInfo(file).Length > MaxFileBytes)
                    {
                        var rolled = file + $".{DateTime.UtcNow:HHmmss}.1";
                        if (!File.Exists(rolled)) File.Move(file, rolled);
                    }

                    var line = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}|{category}|{traceId}|{message}";
                    File.AppendAllText(file, line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch { /* 日志失败不抛出 */ }
        }

        /// <summary>保留期清理: 删除超过14天的日志文件 (服务器启动时调用一次)</summary>
        public static void CleanupOldLogs()
        {
            try
            {
                if (!Directory.Exists(_logDir)) return;
                var cutoff = DateTime.UtcNow.AddDays(-RetainDays);
                foreach (var f in Directory.GetFiles(_logDir, "srv_*.log*"))
                    if (File.GetLastWriteTimeUtc(f) < cutoff)
                        File.Delete(f);
            }
            catch { }
        }
    }
}
