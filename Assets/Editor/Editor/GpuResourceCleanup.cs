using UnityEditor;
using UnityEngine;

/// <summary>
/// 域重载后自动清理未使用的GPU资源，缓解Tuanjie引擎Resource ID泄漏
/// </summary>
[InitializeOnLoad]
public static class GpuResourceCleanup
{
    static GpuResourceCleanup()
    {
        EditorApplication.delayCall += () =>
        {
            Resources.UnloadUnusedAssets();
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
        };
    }

    /// <summary>手动触发清理（可绑定菜单快捷键）</summary>
    [MenuItem("Tools/GPU 资源清理")]
    public static void ManualCleanup()
    {
        var before = System.GC.GetTotalMemory(false);
        Resources.UnloadUnusedAssets();
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        var after = System.GC.GetTotalMemory(true);
        Debug.Log($"[GPU清理] GC: {before / 1024}KB → {after / 1024}KB");
    }
}
