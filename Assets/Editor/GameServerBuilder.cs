using UnityEditor.Build.Reporting;
using UnityEditor;
using UnityEngine;
using System.Linq;

/// <summary>
/// Game Server构建脚本 — 构建Linux Headless可执行文件
/// 菜单: Build > Build Game Server (Linux Headless)
/// </summary>
public class GameServerBuilder
{
    private const string ScenePath = "Assets/Scenes/GameServer.unity";
    private const string OutputDir = "Builds/GameServer";

    [MenuItem("Build/Build Game Server (Linux Headless)")]
    public static void BuildLinuxHeadless()
    {
        // 确保场景在Build Settings中
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == ScenePath))
        {
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // 构建选项: Linux x86_64 Headless (使用Server子目标替代过时的EnableHeadlessMode)
        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = $"{OutputDir}/ArpgGameServer",
            target = BuildTarget.StandaloneLinux64,
            subtarget = (int)StandaloneBuildSubtarget.Server,
            options = BuildOptions.Development
        };

        Debug.Log("[Build] 开始构建Game Server (Linux Headless)...");
        var report = BuildPipeline.BuildPlayer(options);

        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Log($"[Build] 构建成功! 输出: {OutputDir}/ArpgGameServer");
            EditorUtility.DisplayDialog("构建成功", $"Game Server已构建到 {OutputDir}/ArpgGameServer", "确定");
        }
        else
        {
            Debug.LogError($"[Build] 构建失败: {report.summary.result}");
            EditorUtility.DisplayDialog("构建失败", $"错误: {report.summary.result}", "确定");
        }
    }

    [MenuItem("Build/Build Game Server (Windows)")]
    public static void BuildWindows()
    {
        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = $"{OutputDir}_Win/ArpgGameServer.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        };

        Debug.Log("[Build] 开始构建Game Server (Windows)...");
        var report = BuildPipeline.BuildPlayer(options);

        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            Debug.Log($"[Build] 构建成功! 输出: {OutputDir}_Win/ArpgGameServer.exe");
        else
            Debug.LogError($"[Build] 构建失败: {report.summary.result}");
    }
}
