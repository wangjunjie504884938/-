using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// UI Prefab生成工具 — 运行各面板的BuildUI生成GameObject，保存为prefab资产
/// 菜单: Tools/UI/Generate All UI Prefabs
/// </summary>
public static class UiPrefabGenerator
{
    private const string OutputDir = "Assets/Resources/UI";

    // 面板名称列表（按优先级排列）
    private static readonly (string uiType, string panelName)[] Panels = new (string, string)[]
    {
        ("HubUI", "HubPanel"),
        ("GuildUI", "GuildPanel"),
        ("FriendUI", "FriendPanel"),
        ("MailUI", "MailPanel"),
        ("EquipUI", "EquipPanel"),
        ("ShopUI", "ShopPanel"),
        ("DailyTaskUI", "DailyTaskPanel"),
        ("SeasonPassUI", "SeasonPassPanel"),
        ("AchievementUI", "AchievementPanel"),
        ("ForgeUI", "ForgePanel"),
        ("SettingsUI", "SettingsPanel"),
        ("StageSelectUI", "StageSelectPanel"),
        ("GachaUI", "GachaPanel"),
        ("AsyncPvpUI", "AsyncPvpPanel"),
        ("SkillTreeUI", "SkillTreePanel"),
        ("PassiveTreeUI", "PassiveTreePanel"),
        ("RuneEquipUI", "RuneEquipPanel"),
        ("BossCodexUI", "BossCodexPanel"),
        ("FragmentShopUI", "FragmentShopPanel"),
        ("DailyChallengeUI", "DailyChallengePanel"),
    };

    [MenuItem("Tools/UI/Generate All UI Prefabs")]
    public static void GenerateAll()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("UI Prefab Generator",
                "请在Play Mode中运行此工具！\n\n1. 点击Play按钮\n2. 再执行 Tools/UI/Generate All UI Prefabs", "确定");
            return;
        }

        if (!System.IO.Directory.Exists(OutputDir))
            System.IO.Directory.CreateDirectory(OutputDir);

        var canvas = GameObject.Find("Canvas");
        if (canvas == null)
        {
            Debug.LogError("[UiPrefabGen] Canvas not found!");
            return;
        }

        int generated = 0, skipped = 0;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("UI Prefab Generation Results:");

        foreach (var (uiType, panelName) in Panels)
        {
            var panelGo = GameObject.Find(panelName);
            if (panelGo == null)
            {
                sb.AppendLine($"  SKIP (panel not found): {panelName}");
                skipped++;
                continue;
            }

            // 检查是否已经有prefab
            string prefabPath = $"{OutputDir}/{panelName}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                sb.AppendLine($"  SKIP (already exists): {panelName}");
                skipped++;
                continue;
            }

            // 保存为prefab
            var prefab = PrefabUtility.SaveAsPrefabAsset(panelGo, prefabPath);
            if (prefab != null)
            {
                sb.AppendLine($"  OK: {panelName} → {prefabPath}");
                generated++;
            }
            else
            {
                sb.AppendLine($"  FAIL: {panelName}");
            }
        }

        sb.AppendLine($"\nTotal: {generated} generated, {skipped} skipped");
        Debug.Log(sb.ToString());
        EditorUtility.DisplayDialog("UI Prefab Generator", sb.ToString(), "确定");
    }

    [MenuItem("Tools/UI/Generate Single Panel Prefab")]
    public static void GenerateSelected()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("UI Prefab Generator", "请在Play Mode中运行！", "确定");
            return;
        }

        var go = Selection.activeGameObject;
        if (go == null || !go.name.EndsWith("Panel"))
        {
            EditorUtility.DisplayDialog("UI Prefab Generator", "请先在Hierarchy中选中一个 *Panel 对象", "确定");
            return;
        }

        if (!System.IO.Directory.Exists(OutputDir))
            System.IO.Directory.CreateDirectory(OutputDir);

        string path = $"{OutputDir}/{go.name}.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        if (prefab != null)
            Debug.Log($"[UiPrefabGen] Saved: {path}");
    }
}
