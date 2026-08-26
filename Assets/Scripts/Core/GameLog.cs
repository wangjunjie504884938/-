using UnityEngine;

/// <summary>
/// 统一日志管理器 — 条件编译控制，Release构建自动排除所有日志
/// 使用 GameLog.Log() 替代 Debug.Log()
/// </summary>
public static class GameLog
{
    [System.Diagnostics.Conditional("DEBUG_LOG")]
    public static void Log(object message)
    {
        Debug.Log(message);
    }

    [System.Diagnostics.Conditional("DEBUG_LOG")]
    public static void Log(object message, Object context)
    {
        Debug.Log(message, context);
    }

    [System.Diagnostics.Conditional("DEBUG_LOG")]
    public static void LogWarning(object message)
    {
        Debug.LogWarning(message);
    }

    [System.Diagnostics.Conditional("DEBUG_LOG")]
    public static void LogWarning(object message, Object context)
    {
        Debug.LogWarning(message, context);
    }

    // Error 和 Exception 始终输出（不受条件编译控制）
    public static void LogError(object message)
    {
        Debug.LogError(message);
    }

    public static void LogError(object message, Object context)
    {
        Debug.LogError(message, context);
    }
}
