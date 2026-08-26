using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// UI面板基类 — 统一 Show/Hide/Fade 逻辑和协程管理
/// 子类只需实现 BuildContent() 和可选的 OnShow()/OnHide()
/// </summary>
public abstract class UIPanelBase : MonoBehaviour
{
    protected GameObject panel;
    protected CanvasGroup panelCanvasGroup;
    protected System.Action _onBack;
    private Coroutine _showRoutine;

    /// <summary>面板是否可见</summary>
    public bool IsVisible => panel != null && panel.activeSelf;

    /// <summary>子类实现：构建面板内容（首次调用时）</summary>
    protected abstract void BuildContent();

    /// <summary>子类可选：每次显示时刷新数据</summary>
    protected virtual void OnShow() { }

    /// <summary>子类可选：每次隐藏时清理</summary>
    protected virtual void OnHide() { }

    /// <summary>子类必须重写：刷新面板数据（替代OnEnable中直接访问GameManager）</summary>
    public virtual void RefreshData() { }

    /// <summary>子类可选：面板标题文本</summary>
    protected virtual string PanelTitle => "";

    public virtual void Show()
    {
        if (panel == null)
        {
            BuildContent();
            panelCanvasGroup = panel.GetComponent<CanvasGroup>();
            if (panelCanvasGroup == null)
                panelCanvasGroup = panel.AddComponent<CanvasGroup>();
        }
        if (!string.IsNullOrEmpty(PanelTitle)) UIHelper.SetupTopBar(PanelTitle, _onBack);
        OnShow();
        RefreshData(); // 打开面板前先刷新数据，确保显示最新
        panel.SetActive(true);

        // 入场动画前，预加载UI面板引用资源（占位：实际加载逻辑待GameConfigSO实现）
        // GameConfigSO.Instance?.uiPanelReferences?.PreloadPanel(GetType().Name);

        if (_showRoutine != null) StopCoroutine(_showRoutine);
        _showRoutine = StartCoroutine(UIHelper.FadeIn(panel, GameConstants.FadeInDuration));
    }

    public virtual void Hide()
    {
        if (panel == null) return;
        OnHide();

        if (_showRoutine != null) StopCoroutine(_showRoutine);
        _showRoutine = StartCoroutine(FadeOutAndUnload());
    }

    /// <summary>
    /// 淡出动画 → 禁用面板 → 异步卸载未使用资源
    /// 协程运行期间 CanvasGroup.interactable 始终为 false，防止用户交互
    /// </summary>
    private IEnumerator FadeOutAndUnload()
    {
        // 锁定交互
        if (panelCanvasGroup != null)
            panelCanvasGroup.interactable = false;

        // 播放淡出动画
        yield return UIHelper.FadeOut(panel, GameConstants.FadeOutDuration);

        // 异步卸载未使用的UI纹理，防止显存泄漏
        // 卸载期间面板不可交互（interactable已锁定）
        var unloadOp = Resources.UnloadUnusedAssets();
        while (!unloadOp.isDone)
        {
            // 协程运行期间确保交互锁定
            if (panelCanvasGroup != null)
                panelCanvasGroup.interactable = false;
            yield return null;
        }

        // 卸载完成，恢复正常状态
        if (panelCanvasGroup != null)
            panelCanvasGroup.interactable = true;
    }

    /// <summary>创建标准面板容器（背景+导航栏+标题）</summary>
    protected GameObject CreateStandardPanel(string name, Font font, System.Action onBack = null)
    {
        Canvas canvas = GameManager.EnsureCanvas();
        panel = new GameObject(name);
        panel.transform.SetParent(canvas.transform, false);
        var pr = panel.AddComponent<RectTransform>();
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one;
        pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;

        var bg = new GameObject("BG");
        bg.transform.SetParent(panel.transform, false);
        var bgr = bg.AddComponent<RectTransform>();
        bgr.anchorMin = Vector2.zero; bgr.anchorMax = Vector2.one;
        bgr.offsetMin = Vector2.zero; bgr.offsetMax = Vector2.zero;
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = UIHelper.BgDark;
        bgImg.raycastTarget = true;

        _onBack = onBack;

        return panel;
    }

    protected virtual void OnDestroy()
    {
        if (panel != null) Destroy(panel);
    }
}
