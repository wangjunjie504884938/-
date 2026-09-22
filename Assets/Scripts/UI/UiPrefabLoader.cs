using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI Prefab加载辅助 — 面板优先从prefab实例化，不存在时回退到代码生成
/// </summary>
public static class UiPrefabLoader
{
    private const string PrefabDir = "UI/";

    /// <summary>
    /// prefab加载总开关 — 当前prefab全部为占位框架(仅BG/Title/Content，无内容)。
    /// 实例化后Legacy构建代码会对其重复AddComponent&lt;RectTransform&gt;失败，
    /// 空引用中断构建导致面板永不激活(页面空白)。prefab完成美术填充后再改回true。
    /// </summary>
    public static bool EnablePrefabs = false;

    /// <summary>
    /// 尝试从Resources/UI加载prefab并实例化到canvas下
    /// 返回null表示prefab不可用（调用方应回退到LegacyBuildUI）
    /// </summary>
    public static GameObject TryLoad(string panelName, Canvas canvas)
    {
        if (!EnablePrefabs) return null;

        var prefab = Resources.Load<GameObject>(PrefabDir + panelName);
        if (prefab == null) return null;

        var instance = Object.Instantiate(prefab, canvas.transform, false);
        instance.name = panelName;
        return instance;
    }
}
