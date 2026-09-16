using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Edit Mode 直接生成空面板prefab框架 — 不需要Play Mode
/// 只生成背景+标题+关闭按钮的静态框架，动态内容仍由代码填充
/// </summary>
public static class UiPrefabFrameworkGenerator
{
    private const string OutputDir = "Assets/Resources/UI";

    // 20个面板的框架定义
    private static readonly (string name, string title, string subtitle)[] Panels = new (string, string, string)[]
    {
        ("HubPanel", "大本营", ""),
        ("GuildPanel", "公会", ""),
        ("FriendPanel", "好友", ""),
        ("MailPanel", "邮件", ""),
        ("EquipPanel", "装备", ""),
        ("ShopPanel", "商店", ""),
        ("DailyTaskPanel", "每日任务", ""),
        ("SeasonPassPanel", "通行证", ""),
        ("AchievementPanel", "成就", ""),
        ("ForgePanel", "熔铸", ""),
        ("SettingsPanel", "设置", ""),
        ("StageSelectPanel", "选择关卡", ""),
        ("GachaPanel", "抽卡", ""),
        ("AsyncPvpPanel", "竞技场", ""),
        ("SkillTreePanel", "技能树", ""),
        ("PassiveTreePanel", "被动天赋", ""),
        ("RuneEquipPanel", "符文镶嵌", ""),
        ("BossCodexPanel", "Boss图鉴", ""),
        ("FragmentShopPanel", "碎片商店", ""),
        ("DailyChallengePanel", "每日挑战", ""),
    };

    [MenuItem("Tools/UI/Generate Panel Frameworks (Edit Mode)")]
    public static void GenerateAll()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("UI Framework Generator", "请在非Play Mode下运行（Edit Mode）！", "确定");
            return;
        }

        if (!System.IO.Directory.Exists(OutputDir))
            System.IO.Directory.CreateDirectory(OutputDir);

        var canvasGo = GameObject.Find("Canvas");
        if (canvasGo == null)
        {
            // 创建一个临时Canvas用于生成
            canvasGo = new GameObject("TempCanvas");
            canvasGo.AddComponent<Canvas>();
            _tempCanvas = canvasGo;
        }

        int generated = 0, skipped = 0;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("UI Panel Framework Generation:");

        foreach (var (name, title, subtitle) in Panels)
        {
            string path = $"{OutputDir}/{name}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                sb.AppendLine($"  SKIP (exists): {name}");
                skipped++;
                continue;
            }

            var panel = CreatePanelFramework(canvasGo.transform, name, title);
            PrefabUtility.SaveAsPrefabAsset(panel, path);
            Object.DestroyImmediate(panel);
            sb.AppendLine($"  OK: {name}");
            generated++;
        }

        // 清理临时Canvas
        if (_tempCanvas != null)
        {
            Object.DestroyImmediate(_tempCanvas);
            _tempCanvas = null;
        }

        AssetDatabase.SaveAssets();
        sb.AppendLine($"\nTotal: {generated} generated, {skipped} skipped");
        Debug.Log(sb.ToString());
        EditorUtility.DisplayDialog("UI Framework Generator", sb.ToString(), "确定");
    }

    private static GameObject _tempCanvas;

    private static GameObject CreatePanelFramework(Transform parent, string name, string title)
    {
        // Panel root
        var panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        var rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        panel.AddComponent<CanvasGroup>();

        // Background
        var bg = new GameObject("BG");
        bg.transform.SetParent(panel.transform, false);
        var bgRt = bg.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.05f, 0.06f, 0.09f, 0.97f);
        bgImg.raycastTarget = true;

        // Title (top center)
        if (!string.IsNullOrEmpty(title))
        {
            var titleObj = new GameObject("Title");
            titleObj.transform.SetParent(panel.transform, false);
            var tRt = titleObj.AddComponent<RectTransform>();
            tRt.anchorMin = new Vector2(0.5f, 1); tRt.anchorMax = new Vector2(0.5f, 1);
            tRt.pivot = new Vector2(0.5f, 1); tRt.anchoredPosition = new Vector2(0, -30);
            tRt.sizeDelta = new Vector2(400, 40);
            var tTxt = titleObj.AddComponent<Text>();
            tTxt.text = title; tTxt.alignment = TextAnchor.MiddleCenter;
            tTxt.fontSize = 28; tTxt.color = Color.white;
            tTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        // Close button (top right)
        var closeObj = new GameObject("CloseBtn");
        closeObj.transform.SetParent(panel.transform, false);
        var cRt = closeObj.AddComponent<RectTransform>();
        cRt.anchorMin = new Vector2(1, 1); cRt.anchorMax = new Vector2(1, 1);
        cRt.pivot = new Vector2(1, 1); cRt.anchoredPosition = new Vector2(-15, -15);
        cRt.sizeDelta = new Vector2(40, 40);
        var cImg = closeObj.AddComponent<Image>();
        cImg.color = new Color(0.7f, 0.2f, 0.2f, 0.8f);
        var cBtn = closeObj.AddComponent<Button>();
        var cLbl = new GameObject("Label");
        cLbl.transform.SetParent(closeObj.transform, false);
        var cLRt = cLbl.AddComponent<RectTransform>();
        cLRt.anchorMin = Vector2.zero; cLRt.anchorMax = Vector2.one;
        cLRt.offsetMin = Vector2.zero; cLRt.offsetMax = Vector2.zero;
        var cLTxt = cLbl.AddComponent<Text>();
        cLTxt.text = "X"; cLTxt.alignment = TextAnchor.MiddleCenter;
        cLTxt.fontSize = 20; cLTxt.color = Color.white;
        cLTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Content placeholder (marks where dynamic content goes)
        var content = new GameObject("Content");
        content.transform.SetParent(panel.transform, false);
        var ctRt = content.AddComponent<RectTransform>();
        ctRt.anchorMin = new Vector2(0.05f, 0.05f); ctRt.anchorMax = new Vector2(0.95f, 0.88f);
        ctRt.offsetMin = Vector2.zero; ctRt.offsetMax = Vector2.zero;

        return panel;
    }
}
