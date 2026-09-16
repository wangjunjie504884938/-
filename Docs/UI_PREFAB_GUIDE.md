# P1-C UI Prefab化操作指南

## 目标
把代码生成的UI面板（`BuildUI()` 中 `new GameObject(...)`）转为 `.prefab` 资产，
运行时只需 `Instantiate(prefab)`，不再每次用代码拼UI。

## 操作方式（二选一）

### 方式A：半自动脚本生成（推荐，AI可代做）
写一个 Editor 脚本，运行一次项目代码的 `BuildUI()` 生成面板，
然后把生成的 GameObject 用 `PrefabUtility.SaveAsPrefabAsset()` 保存为 prefab。
后续 UI 代码改为加载 prefab。

### 方式B：纯手工（Unity Editor 里拖拽）
运行游戏 → 面板生成后停止 → 在 Hierarchy 中复制该面板 → 粘贴到 Project 窗口变成 prefab → 停止运行后删除场景实例。

---

## 单个面板的转换流程（以 HubUI 为例）

### 第1步：生成 prefab（Editor脚本执行一次）
```csharp
// Assets/Editor/GenerateUiPrefabs.cs
[MenuItem("Tools/Generate UI Prefabs")]
static void Generate()
{
    // 1. 确保 Canvas 存在
    var canvas = GameObject.Find("Canvas");
    if (canvas == null) return;

    // 2. 调用 HubUI 的 BuildUI 生成面板
    var hubUI = canvas.AddComponent<HubUI>();
    hubUI.Show();  // 触发 BuildUI()

    // 3. 找到生成的面板并保存为 prefab
    var panel = GameObject.Find("HubPanel");  // HubUI的panel名称
    if (panel != null)
    {
        PrefabUtility.SaveAsPrefabAsset(panel, "Assets/Resources/UI/HubPanel.prefab");
        Object.DestroyImmediate(panel);
    }

    Object.DestroyImmediate(hubUI);
    AssetDatabase.SaveAssets();
}
```

### 第2步：修改 UI 代码改为加载 prefab
```csharp
// HubUI.cs 修改后
private void BuildUI()
{
    // 优先从prefab加载
    var prefab = Resources.Load<GameObject>("UI/HubPanel");
    if (prefab != null)
    {
        panel = Object.Instantiate(prefab, GameManager.EnsureCanvas().transform, false);
        // 绑定引用（按钮点击等仍需代码）
        BindReferences();
        return;
    }

    // prefab不存在时回退到代码生成（开发模式）
    LegacyBuildUI();
}
```

### 第3步：清理
- 旧的 `BuildUI()` 改名为 `LegacyBuildUI()` 保留作回退
- 新增 `BindReferences()` 方法把 `GetComponentInChildren` 的引用绑定从"构建时"改为"加载后"

---

## 每个面板的注意事项

| 面板 | panel名称 | 特殊注意 |
|---|---|---|
| HubUI | HubPanel | 好友列表+宠物面板是子面板，分别保存 |
| GameUI | HUD面板 | 已有GameHUD.prefab，检查是否覆盖 |
| TitleScreen | TitlePanel | 动画背景粒子需要运行时逻辑，prefab只保存静态部分 |
| GuildUI | GuildPanel | 4个标签页内容动态生成，prefab只保存框架 |
| FriendUI | FriendPanel | 同上，列表项用item pool |
| MailUI | MailPanel | 列表项动态 |

## 关键原则
1. **prefab保存静态结构**（背景/按钮/标题/布局）
2. **动态内容仍用代码**（列表项、数据绑定、点击回调）
3. **列表项用对象池**（不要Instantiate/Destroy）
4. **保留LegacyBuildUI回退**，prefab缺失时不崩溃
