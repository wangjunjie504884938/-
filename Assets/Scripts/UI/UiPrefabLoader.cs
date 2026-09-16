using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI Prefab加载辅助 — 面板优先从prefab实例化，不存在时回退到代码生成
/// </summary>
public static class UiPrefabLoader
{
    private const string PrefabDir = "UI/";

    /// <summary>
    /// 尝试从Resources/UI加载prefab并实例化到canvas下
    /// 返回null表示prefab不存在（调用方应回退到LegacyBuildUI）
    /// </summary>
    public static GameObject TryLoad(string panelName, Canvas canvas)
    {
        var prefab = Resources.Load<GameObject>(PrefabDir + panelName);
        if (prefab == null) return null;

        var instance = Object.Instantiate(prefab, canvas.transform, false);
        instance.name = panelName;
        return instance;
    }
}
