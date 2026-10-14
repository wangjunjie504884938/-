using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 统一加载遮罩页 — 设计图视觉(全屏背景 + 读条底/填充/边框 + 骑条小人) + 阶段文案/攻略tips + 进度
/// 全屏raycast遮罩: 加载期间吞掉所有点击防误操作
/// 30秒看门狗: 加载协程异常中断时强制隐藏, 防止遮罩永久卡屏
/// 各加载场景(冷启动/进秘境/复活/世界传送)分帧驱动: Show → SetProgress → Hide
/// </summary>
public class LoadingScreen : MonoBehaviour
{
    public static LoadingScreen Instance { get; private set; }
    public static bool IsActive => Instance != null && Instance._panel != null && Instance._panel.activeSelf;

    private GameObject _panel;
    private Image _fillImg;
    private Text _stageText;
    private Text _tipText;
    private RectTransform _walkerRect;
    private float _shownAtRealtime;
    private float _targetProgress;      // 调用方设定值(瞬间跳变)
    private float _displayProgress;     // 实际显示值(按最短时长匀速追赶, 小人才有走动感)
    private float _minDuration = 1.6f;  // 最短展示时长(秒)
    private bool _hideRequested;

    private const float WatchdogSeconds = 30f;
    private const float WalkMinX = -670f;
    private const float WalkMaxX = 670f;

    private static readonly string[] Tips =
    {
        "暴击可以打断敌人的攻击动作",
        "秘境层数越高，装备稀有度越高",
        "每5层会遇到Boss，掉落专属装备",
        "碎片可以在商店升级符文",
        "组队时AI队友会自动跟随作战",
        "装备熔炼可以获得强化材料",
        "血瓶会缓慢恢复，省着点用",
        "地图资源点会掉落血瓶和碎片",
    };

    /// <summary>显示加载遮罩并重置进度(自动换一条随机tips)。minDuration=最短展示秒数, 分帧加载快于此值时进度平滑补足</summary>
    public static void Show(string stageText, float minDuration = 1.6f)
    {
        Ensure();
        if (Instance._panel == null) Instance.Build();
        Instance._panel.SetActive(true);
        Instance._shownAtRealtime = Time.unscaledTime;
        Instance._minDuration = minDuration;
        Instance._hideRequested = false;
        Instance._targetProgress = 0f;
        Instance._displayProgress = 0f;
        Instance.ApplyProgress(0f);
        SetStage(stageText);
        SetTip(Tips[Random.Range(0, Tips.Length)]);
    }

    public static void SetStage(string stageText)
    {
        Ensure();
        if (Instance._stageText != null) Instance._stageText.text = stageText;
    }

    public static void SetTip(string tipText)
    {
        Ensure();
        if (Instance._tipText != null) Instance._tipText.text = tipText;
    }

    /// <summary>目标进度0~1: 显示值由Update平滑追赶, 驱动读条填充+小人骑填充头</summary>
    public static void SetProgress(float p)
    {
        Ensure();
        Instance._targetProgress = Mathf.Clamp01(p);
    }

    /// <summary>请求隐藏 — 实际隐藏等显示进度走到1, 保证小人走完读条</summary>
    public static void Hide()
    {
        if (Instance == null) return;
        Instance._hideRequested = true;
    }

    private static void Ensure()
    {
        if (Instance != null) return;
        var canvas = GameManager.EnsureCanvas();
        Instance = canvas.GetComponent<LoadingScreen>() ?? canvas.gameObject.AddComponent<LoadingScreen>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (_panel == null || !_panel.activeSelf) return;

        // 显示进度匀速追赶目标进度 (整条走完耗时≈minDuration)
        _displayProgress = Mathf.MoveTowards(
            _displayProgress, _targetProgress,
            Time.unscaledDeltaTime / Mathf.Max(0.2f, _minDuration));
        ApplyProgress(_displayProgress);

        // 隐藏条件: 调用方已请求 + 小人走完 + 最短时长已过
        if (_hideRequested && _displayProgress >= 1f
            && Time.unscaledTime - _shownAtRealtime >= _minDuration)
        {
            _panel.SetActive(false);
            return;
        }

        // 看门狗: 协程异常中断时强制隐藏防卡遮罩
        if (Time.unscaledTime - _shownAtRealtime > WatchdogSeconds)
        {
            Debug.LogError($"[LoadingScreen] 看门狗超时{WatchdogSeconds}s强制隐藏 — 检查加载协程是否异常中断");
            _panel.SetActive(false);
        }
    }

    private void ApplyProgress(float p)
    {
        if (_fillImg != null) _fillImg.fillAmount = p;
        if (_walkerRect != null)
        {
            var pos = _walkerRect.anchoredPosition;
            pos.x = Mathf.Lerp(WalkMinX, WalkMaxX, p);
            _walkerRect.anchoredPosition = pos;
        }
    }

    private void Build()
    {
        Font font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();

        _panel = new GameObject("LoadingScreenPanel");
        _panel.transform.SetParent(canvas.transform, false);
        var pr = _panel.AddComponent<RectTransform>();
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one;
        pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;
        var rootImg = _panel.AddComponent<Image>();
        rootImg.color = new Color(0.02f, 0.01f, 0.05f, 1f);
        rootImg.raycastTarget = true;
        var pc = _panel.AddComponent<Canvas>();
        pc.overrideSorting = true; pc.sortingOrder = 950;
        _panel.AddComponent<GraphicRaycaster>();

        // 全屏设计图背景
        var bgObj = new GameObject("BG");
        bgObj.transform.SetParent(_panel.transform, false);
        var bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero; bgRect.offsetMax = Vector2.zero;
        var bgImg = bgObj.AddComponent<Image>();
        bgImg.sprite = Resources.Load<Sprite>(ResourcePaths.LoadingBackground);
        bgImg.color = Color.white;
        bgImg.raycastTarget = false;

        // 进度条簇: 底 + 填充(Filled水平) + 边框 + 骑条小人
        var barRoot = new GameObject("BarRoot");
        barRoot.transform.SetParent(_panel.transform, false);
        var barRect = barRoot.AddComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0.5f, 0.5f);
        barRect.anchorMax = new Vector2(0.5f, 0.5f);
        barRect.pivot = new Vector2(0.5f, 0.5f);
        barRect.anchoredPosition = new Vector2(0, -308);
        barRect.sizeDelta = new Vector2(1380, 56);

        var baseObj = new GameObject("BarBase");
        baseObj.transform.SetParent(barRoot.transform, false);
        var baseRect = baseObj.AddComponent<RectTransform>();
        baseRect.anchorMin = Vector2.zero; baseRect.anchorMax = Vector2.one;
        baseRect.offsetMin = Vector2.zero; baseRect.offsetMax = Vector2.zero;
        var baseImg = baseObj.AddComponent<Image>();
        baseImg.sprite = Resources.Load<Sprite>(ResourcePaths.LoadingBarBase);
        baseImg.raycastTarget = false;

        var fillObj = new GameObject("BarFill");
        fillObj.transform.SetParent(barRoot.transform, false);
        var fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(8, 8); fillRect.offsetMax = new Vector2(-8, -8);
        _fillImg = fillObj.AddComponent<Image>();
        _fillImg.sprite = Resources.Load<Sprite>(ResourcePaths.LoadingBarFill);
        _fillImg.type = Image.Type.Filled;
        _fillImg.fillMethod = Image.FillMethod.Horizontal;
        _fillImg.fillAmount = 0f;
        _fillImg.raycastTarget = false;

        var frameObj = new GameObject("BarFrame");
        frameObj.transform.SetParent(barRoot.transform, false);
        var frameRect = frameObj.AddComponent<RectTransform>();
        frameRect.anchorMin = Vector2.zero; frameRect.anchorMax = Vector2.one;
        frameRect.offsetMin = Vector2.zero; frameRect.offsetMax = Vector2.zero;
        var frameImg = frameObj.AddComponent<Image>();
        frameImg.sprite = Resources.Load<Sprite>(ResourcePaths.LoadingBarFrame);
        frameImg.raycastTarget = false;

        var walkerObj = new GameObject("Walker");
        walkerObj.transform.SetParent(barRoot.transform, false);
        _walkerRect = walkerObj.AddComponent<RectTransform>();
        _walkerRect.anchorMin = new Vector2(0.5f, 0.5f);
        _walkerRect.anchorMax = new Vector2(0.5f, 0.5f);
        _walkerRect.pivot = new Vector2(0.5f, 0f); // 脚底为基准, 站在条顶
        _walkerRect.anchoredPosition = new Vector2(WalkMinX, 30f);
        _walkerRect.sizeDelta = new Vector2(146, 200);
        var walkerImg = walkerObj.AddComponent<Image>();
        walkerImg.sprite = Resources.Load<Sprite>(ResourcePaths.LoadingBarWalker);
        walkerImg.preserveAspect = true;
        walkerImg.raycastTarget = false;

        // 阶段文案
        var stageObj = new GameObject("StageText");
        stageObj.transform.SetParent(_panel.transform, false);
        var stageRect = stageObj.AddComponent<RectTransform>();
        stageRect.anchorMin = new Vector2(0.5f, 0.5f); stageRect.anchorMax = new Vector2(0.5f, 0.5f);
        stageRect.anchoredPosition = new Vector2(0, -398);
        stageRect.sizeDelta = new Vector2(1000, 44);
        _stageText = stageObj.AddComponent<Text>();
        _stageText.alignment = TextAnchor.MiddleCenter;
        _stageText.fontSize = 28;
        _stageText.color = new Color(0.93f, 0.91f, 0.99f);
        _stageText.font = font;
        _stageText.raycastTarget = false;

        // 攻略tips
        var tipObj = new GameObject("TipText");
        tipObj.transform.SetParent(_panel.transform, false);
        var tipRect = tipObj.AddComponent<RectTransform>();
        tipRect.anchorMin = new Vector2(0.5f, 0.5f); tipRect.anchorMax = new Vector2(0.5f, 0.5f);
        tipRect.anchoredPosition = new Vector2(0, -444);
        tipRect.sizeDelta = new Vector2(1400, 34);
        _tipText = tipObj.AddComponent<Text>();
        _tipText.alignment = TextAnchor.MiddleCenter;
        _tipText.fontSize = 18;
        _tipText.color = new Color(0.62f, 0.60f, 0.72f);
        _tipText.font = font;
        _tipText.raycastTarget = false;

        _panel.SetActive(false);
    }
}
