using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Arch;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 架构阶段02编辑器工具:
/// - Arch/数据模型自检: JSON往返/货币溢出/配置校验 (报告落 Docs/ArchSelfTest.md)
/// - Arch/任务模板CSV→JSON: Excel另存CSV后导入, 导出 StaticConfig JSON (含版本号+校验)
/// </summary>
public static class ArchTools
{
    [MenuItem("Arch/数据模型自检", priority = 0)]
    public static void RunSelfTest()
    {
        var report = ArchSelfTest.Run();
        Debug.Log(report);
        var dir = "Docs";
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "ArchSelfTest.md"), "# Arch 数据模型自检\n\n```\n" + report + "\n```\n", Encoding.UTF8);
        EditorUtility.DisplayDialog("Arch 数据模型自检", report, "确定");
    }

    [MenuItem("Arch/任务模板CSV→JSON", priority = 1)]
    public static void ExportTaskCsvToJson()
    {
        var path = EditorUtility.OpenFilePanel("选择任务模板 CSV (Excel另存为CSV)", "", "csv");
        if (string.IsNullOrEmpty(path)) return;

        var cfg = new StaticConfig { Version = StaticConfig.CurrentVersion };
        var lines = File.ReadAllLines(path, Encoding.UTF8).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
        if (lines.Length < 2)
        {
            EditorUtility.DisplayDialog("Arch 导出", "CSV 至少需要 表头+1行数据", "确定");
            return;
        }

        // 表头: taskType,targetValue,rewardGold
        for (int i = 1; i < lines.Length; i++)
        {
            var cols = lines[i].Split(',');
            if (cols.Length < 3) continue;
            if (!int.TryParse(cols[0].Trim(), out var type)) { Debug.LogError($"[Arch] 第{i + 1}行 taskType 非法: {lines[i]}"); continue; }
            if (!int.TryParse(cols[1].Trim(), out var target)) { Debug.LogError($"[Arch] 第{i + 1}行 targetValue 非法: {lines[i]}"); continue; }
            if (!int.TryParse(cols[2].Trim(), out var reward)) { Debug.LogError($"[Arch] 第{i + 1}行 rewardGold 非法: {lines[i]}"); continue; }
            cfg.DailyTasks.Add(new TaskTemplate { TaskType = type, TargetValue = target, RewardGold = reward });
        }

        var errors = ConfigValidator.Validate(cfg);
        if (errors.Count > 0)
        {
            EditorUtility.DisplayDialog("Arch 导出失败", "配置校验未通过:\n" + string.Join("\n", errors.ToArray()), "确定");
            return;
        }

        var outPath = Path.Combine(Path.GetDirectoryName(path) ?? "", "daily_tasks.json");
        File.WriteAllText(outPath, JsonConvert.SerializeObject(cfg, Formatting.Indented), Encoding.UTF8);
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Arch 导出成功", $"已导出 {cfg.DailyTasks.Count} 条任务模板到:\n{outPath}", "确定");
    }
}
