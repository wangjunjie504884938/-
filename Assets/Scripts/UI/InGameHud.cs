using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

/// <summary>
/// 局内HUD (设计图版) — 左上头像/名字/等级/HP条, 顶中三货币, 左侧任务栏(可折叠),
/// 左下虚拟摇杆, 底中三个功能钮(红点), 右下技能轮盘(冷却fillAmount+倒计时), 右上小地图占位。
/// Show()时隐藏旧GameUI HUD避免重叠; 所有切图位于 Resources/Sprites/Hud/。
/// 层级(自底向上): 摇杆 → 任务栏 → 左上信息 → 货币 → 小地图 → 底中按钮 → 技能轮盘 → 红点(各自按钮内最后)。
/// </summary>
public class InGameHud : MonoBehaviour
{
    public static InGameHud Instance { get; private set; }

    private const float SC = 0.75f;   // 设计图2560宽 → 画布1920 的缩放

    private GameObject panel;
    private Text _hpText;
    private Image _hpFill;
    private Text _levelText;
    private Text _nameText;
    private Text[] _currencyTexts = new Text[3];
    private Image[] _currencyPlus = new Image[3];
    private GameObject[] _redDots = new GameObject[3];
    private Button[] _roundBtns = new Button[3];
    private RectTransform _questSlider;
    private RectTransform _questArrow;
    private Text _mainQuestText;
    private Text _dailyQuestText;
    private bool _questFolded;

    // 技能槽: 0=普攻上方(增伤2/螺旋) 1=右上(增益1/盾) 2=右(增伤1/轮) 3=回复(增益2/叶)
    private Image[] _skillCool = new Image[4];
    private Text[] _skillCount = new Text[4];
    private GameObject[] _skillReady = new GameObject[4];
    private float[] _cdRemain = new float[4];
    private float[] _cdTotal = new float[4];
    private Text _mapNameText;
    private RectTransform _minimapRect;
    private string _lastMapName;
    private bool _lastMapNameWorld;
    private RectTransform _hudNavBtns;
    private RectTransform _currenciesRow;
    private float _questRefreshTimer;

    /// <summary>摇杆输出向量 (长度0..1, 未拖动为0)</summary>
    public Vector2 JoystickMove { get; private set; }

    /// <summary>普攻按钮按住状态 (PlayerInputController轮询读取)</summary>
    public bool AttackHeld { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDisable()
    {
        // 面板隐藏时清空输入状态, 防止拖拽中断后摇杆/普攻卡死
        JoystickMove = Vector2.zero;
        AttackHeld = false;
    }

    private static Sprite S(string name) => Resources.Load<Sprite>($"Sprites/Hud/{name}");

    public static void Show()
    {
        var gm = GameManager.Instance;
        var go = gm != null ? gm.gameObject : null;
        if (go == null) { Debug.LogError("[InGameHud] 无GameManager挂载点"); return; }
        if (Instance == null) go.AddComponent<InGameHud>();
        if (Instance.panel == null) Instance.BuildUI();
        Instance.panel.SetActive(true);
        RefreshIdentity();
        DailyTaskUI.BootQuiet();
        Instance.RefreshQuestPanel();
    }

    public static void Hide()
    {
        if (Instance == null || Instance.panel == null) return;
        Instance.panel.SetActive(false);
    }

    /// <summary>游戏流程统一入口 — 经 GameUI.SetHUDVisible 漏斗调用</summary>
    public static void SetVisibleFromFlow(bool visible)
    {
        if (visible) Show(); else Hide();
    }

    /// <summary>按当前激活槽位刷新角色名</summary>
    private static void RefreshIdentity()
    {
        var slot = PlayerProgressData.ActiveSlot;
        Instance?.SetName(PlayerPrefs.GetString($"ARPG_S{slot}_CharacterName", "冒险者"));
    }

    // =================== 构建 ===================

    private void BuildUI()
    {
        Font font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();

        panel = new GameObject("InGameHud");
        panel.transform.SetParent(canvas.transform, false);
        var pr = panel.AddComponent<RectTransform>();
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one;
        pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;
        panel.AddComponent<CanvasGroup>().blocksRaycasts = true;

        BuildJoystick(font);
        BuildQuestPanel(font);
        BuildTopLeft(font);
        BuildMapNameText(font);
        BuildCurrencies(font);
        BuildMinimap(font);
        BuildBottomCenter(font);
        BuildSkillWheel(font);
        BuildHudNavButtons(font);
        ApplySafeArea();
        // 置于Canvas最底层: 结算/暂停/装备/商店等一切面板自然显示在HUD之上(含射线优先)
        panel.transform.SetAsFirstSibling();
        panel.SetActive(false);
    }

    private RectTransform NewChild(string name, Transform parent, Vector2 anchor, Vector2 pivot,
        Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var r = go.AddComponent<RectTransform>();
        r.anchorMin = anchor; r.anchorMax = anchor; r.pivot = pivot;
        r.anchoredPosition = pos; r.sizeDelta = size;
        return r;
    }

    private Image Img(Transform parent, string sprite, Vector2 pos, Vector2 size, bool raycast = false)
    {
        var r = NewChild(sprite, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
        var img = r.AddComponent<Image>();
        img.sprite = S(sprite);
        img.preserveAspect = true;
        img.raycastTarget = raycast;
        return img;
    }

    private Text Txt(Transform parent, string txtName, string content, int fontSize, Color color, Vector2 pos, Vector2 size,
        TextAnchor align = TextAnchor.MiddleCenter)
    {
        var r = NewChild(txtName, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
        var t = r.AddComponent<Text>();
        t.text = content; t.alignment = align; t.fontSize = fontSize; t.color = color;
        t.font = GameManager.GetUIFont(); t.raycastTarget = false;
        return t;
    }

    // =================== 左下摇杆 ===================

    public Vector2 GetJoystickMove() => JoystickMove;

    private void BuildJoystick(Font font)
    {
        var root = NewChild("Joystick", panel.transform, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f),
            new Vector2(40 + 115, 40 + 115), new Vector2(230, 230));

        var baseRect = NewChild("Base", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(230, 230));
        var baseImg = baseRect.AddComponent<Image>();
        baseImg.sprite = S("HudJoyBase");
        baseImg.raycastTarget = true;

        var knobRect = NewChild("Knob", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(100, 100));
        var knobImg = knobRect.AddComponent<Image>();
        knobImg.sprite = S("HudJoyKnob");
        knobImg.raycastTarget = false;

        var drag = baseRect.gameObject.AddComponent<JoyDrag>();
        drag.Init(knobRect, 62f, v => JoystickMove = v);
    }

    /// <summary>摇杆拖拽 (挂在Base上; 输出归一化向量)</summary>
    private class JoyDrag : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private RectTransform _knob;
        private float _radius;
        private System.Action<Vector2> _onChanged;
        private bool _dragging;

        public void Init(RectTransform knob, float radius, System.Action<Vector2> onChanged)
        {
            _knob = knob; _radius = radius; _onChanged = onChanged;
        }

        public void OnPointerDown(PointerEventData e) { _dragging = true; UpdateKnob(e); }
        public void OnDrag(PointerEventData e) { if (_dragging) UpdateKnob(e); }
        public void OnPointerUp(PointerEventData e)
        {
            _dragging = false;
            _knob.anchoredPosition = Vector2.zero;
            _onChanged?.Invoke(Vector2.zero);
        }

        private void UpdateKnob(PointerEventData e)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _knob.parent as RectTransform, e.position, e.pressEventCamera, out var local))
            {
                var v = Vector2.ClampMagnitude(local, _radius);
                _knob.anchoredPosition = v;
                _onChanged?.Invoke(v / _radius);
            }
        }
    }

    /// <summary>单次按下触发 (技能槽)</summary>
    private class HudPressButton : MonoBehaviour, IPointerDownHandler
    {
        public System.Action OnDown;
        public void OnPointerDown(PointerEventData e) => OnDown?.Invoke();
    }

    /// <summary>按住状态按钮 (普攻: 按住持续攻击)</summary>
    private class HudHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public System.Action<bool> OnHold;
        public void OnPointerDown(PointerEventData e) => OnHold?.Invoke(true);
        public void OnPointerUp(PointerEventData e) => OnHold?.Invoke(false);
    }

    // =================== 左侧任务栏 ===================

    private void BuildQuestPanel(Font font)
    {
        var root = NewChild("QuestPanel", panel.transform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(158, 20), new Vector2(316, 398));

        // 滑动体 (面板+内容, 折叠时整体左移)
        _questSlider = NewChild("Slider", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(316, 398));
        var panelImg = _questSlider.AddComponent<Image>();
        panelImg.sprite = S("HudQuestPanel");
        panelImg.type = Image.Type.Sliced;
        panelImg.raycastTarget = false;

        Txt(_questSlider, "QuestTitle", "任务栏", 34, new Color(0.95f, 0.88f, 0.6f), new Vector2(-10, 150), new Vector2(220, 50));

        // 任务内容滚动区 (标题以下整块可滑动, RectMask2D裁剪 — Tuanjie无Mask)
        var scrollGo = new GameObject("QuestScroll", typeof(RectTransform), typeof(RectMask2D), typeof(ScrollRect));
        scrollGo.transform.SetParent(_questSlider.transform, false);
        var scrollR = scrollGo.GetComponent<RectTransform>();
        scrollR.anchorMin = Vector2.zero; scrollR.anchorMax = Vector2.one;
        scrollR.offsetMin = new Vector2(8, 10); scrollR.offsetMax = new Vector2(-8, -120);
        var questScroll = scrollGo.GetComponent<ScrollRect>();
        questScroll.horizontal = false;
        questScroll.vertical = true;
        questScroll.movementType = ScrollRect.MovementType.Clamped;
        questScroll.scrollSensitivity = 14;

        var viewport = new GameObject("Viewport", typeof(RectTransform));
        viewport.transform.SetParent(scrollGo.transform, false);
        var vR = viewport.GetComponent<RectTransform>();
        vR.anchorMin = Vector2.zero; vR.anchorMax = Vector2.one;
        vR.offsetMin = Vector2.zero; vR.offsetMax = Vector2.zero;
        var vImg = viewport.AddComponent<Image>();
        vImg.color = Color.clear; // 接收滚轮/拖拽

        var questContent = new GameObject("QuestContent", typeof(RectTransform));
        questContent.transform.SetParent(viewport.transform, false);
        var cR = questContent.GetComponent<RectTransform>();
        cR.anchorMin = new Vector2(0.5f, 1f); cR.anchorMax = new Vector2(0.5f, 1f);
        cR.pivot = new Vector2(0.5f, 1f);
        cR.anchoredPosition = Vector2.zero;
        cR.sizeDelta = new Vector2(300, 420);
        questScroll.viewport = vR;
        questScroll.content = cR;

        // 内容局部坐标 = 用户最终坐标表 (居中锚定, 与Txt默认锚点一致; 滚动查看完整日常列表)
        Txt(questContent.transform, "QuestMainLabel", "主线任务:", 24, new Color(1f, 0.62f, 0.78f), new Vector2(-40, 180), new Vector2(180, 35), TextAnchor.MiddleLeft);
        _mainQuestText = Txt(questContent.transform, "QuestMainDesc", "◇ 找到村长获取任务", 20, Color.white, new Vector2(0, 132), new Vector2(230, 35), TextAnchor.MiddleLeft);
        Txt(questContent.transform, "QuestDailyLabel", "日常任务:", 24, new Color(1f, 0.62f, 0.78f), new Vector2(-40, 65), new Vector2(180, 35), TextAnchor.MiddleLeft);
        _dailyQuestText = Txt(questContent.transform, "QuestDailyDesc", "", 17, Color.white,
            new Vector2(0, -28), new Vector2(230, 120), TextAnchor.UpperLeft);

        // 折叠箭头按钮 (嵌在面板右缘凸舌上, 随面板一起滑动)
        _questArrow = NewChild("FoldArrow", _questSlider, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(130, 0), new Vector2(15, 40));
        var arrowImg = _questArrow.AddComponent<Image>();
        arrowImg.sprite = S("HudQuestArrow");
        var arrowBtn = _questArrow.AddComponent<Button>();
        arrowBtn.targetGraphic = arrowImg;
        arrowBtn.onClick.AddListener(ToggleQuest);
    }

    /// <summary>任务栏折叠/展开 (0.2秒滑出/滑入 + 箭头翻转)</summary>
    public void ToggleQuest()
    {
        _questFolded = !_questFolded;
        StopCoroutine("FoldTween");
        StartCoroutine(FoldTween(_questFolded));
    }

    private IEnumerator FoldTween(bool fold)
    {
        float from = _questSlider.anchoredPosition.x;
        float to = fold ? -(316f - 56f) : 0f;
        float t = 0f;
        while (t < 0.2f)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / 0.2f);
            p = 1f - (1f - p) * (1f - p);   // easeOutQuad
            _questSlider.anchoredPosition = new Vector2(Mathf.Lerp(from, to, p), 0f);
            yield return null;
        }
        _questSlider.anchoredPosition = new Vector2(to, 0f);
        var sc = _questArrow.localScale;
        sc.x = fold ? -1 : 1;
        _questArrow.localScale = sc;
    }

    public void SetQuests(string mainQuest, string[] dailyQuests)
    {
        if (_mainQuestText != null) _mainQuestText.text = "◇ " + mainQuest;
        if (_dailyQuestText != null && dailyQuests != null)
            _dailyQuestText.text = string.Join("\n", System.Array.ConvertAll(dailyQuests, q => "◇ " + q));
    }

    /// <summary>任务栏接真实数据: 主线=秘境进度目标, 日常=DailyTaskUI今日任务 (2秒轮询刷新)</summary>
    private void RefreshQuestPanel()
    {
        int highest = RiftProgress.HighestCleared;
        string main = highest >= 10 ? "秘境已全部通关！" : $"挑战第{highest + 2}层秘境";
        var daily = DailyTaskUI.GetDailyQuestLines();
        SetQuests(main, daily ?? new[] { "暂无日常任务" });
    }

    // =================== 左上头像/名字/等级/HP ===================

    private void BuildTopLeft(Font font)
    {
        var root = NewChild("TopLeft", panel.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0, -60), new Vector2(600, 160));

        var frame = Img(root, "HudAvatarFrame", new Vector2(-240, 25), new Vector2(120, 120));
        frame.raycastTarget = false;
        var avatar = Img(root, "HudAvatarM", new Vector2(-240, 25), new Vector2(100, 100));
        avatar.raycastTarget = false;

        _levelText = Txt(root, "LevelText", "1", 30, Color.white, new Vector2(-210, -15), new Vector2(60, 40));

        _nameText = Txt(root, "NameText", "冒险者", 32, Color.white, new Vector2(-15, 50), new Vector2(320, 50), TextAnchor.MiddleLeft);

        // "HP" 文本 (血条左侧)
        Txt(root, "HpLabel", "HP", 20, new Color(0.95f, 0.35f, 0.30f), new Vector2(-155, 0), new Vector2(45, 25));

        // HP条: 底框(胶囊轨道) + 填充(Filled水平) + 顶框 + 数值
        var bar = NewChild("HpBar", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(60, 0), new Vector2(380, 30));

        var baseImg = NewChild("Base", bar, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(373, 21)).AddComponent<Image>();
        baseImg.sprite = S("HudHpBase");

        var fillRect = NewChild("Fill", bar, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-5, 0), new Vector2(345, 15));
        _hpFill = fillRect.AddComponent<Image>();
        _hpFill.sprite = S("HudHpFill");
        _hpFill.type = Image.Type.Filled;
        _hpFill.fillMethod = Image.FillMethod.Horizontal;
        _hpFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        _hpFill.fillAmount = 1f;
        _hpFill.raycastTarget = false;

        var frameImg = NewChild("Frame", bar, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(375, 23)).AddComponent<Image>();
        frameImg.sprite = S("HudHpFrame");
        frameImg.raycastTarget = false;

        _hpText = Txt(bar, "HpValue", "100/100", 16, Color.white, Vector2.zero, new Vector2(180, 22));
        SetHp(100, 100);
    }

    /// <summary>设置HP (同步填充与数值文本)</summary>
    public void SetHp(long cur, long max)
    {
        if (_hpFill != null) _hpFill.fillAmount = max <= 0 ? 0f : Mathf.Clamp01((float)((double)cur / max));
        if (_hpText != null) _hpText.text = $"{cur}/{max}";
    }

    public void SetName(string name) { if (_nameText != null) _nameText.text = name; }
    public void SetLevel(int lv) { if (_levelText != null) _levelText.text = lv.ToString(); }

    // =================== 顶中地图名/楼层/波次 ===================

    private void BuildMapNameText(Font font)
    {
        var r = NewChild("MapNameText", panel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
            new Vector2(0, -100), new Vector2(500, 30));
        _mapNameText = r.AddComponent<Text>();
        _mapNameText.alignment = TextAnchor.MiddleCenter;
        _mapNameText.fontSize = 22;
        _mapNameText.color = new Color(0.9f, 0.85f, 0.4f, 1f);
        _mapNameText.font = font;
        _mapNameText.raycastTarget = false;
    }

    /// <summary>地图名/楼层/波次刷新 (数据源同旧GameUI; 世界内隐藏)</summary>
    private void UpdateMapName()
    {
        if (_mapNameText == null) return;
        var gm = GameManager.Instance;
        if (gm == null) return;
        bool inWorld = gm.InWorld;
        if (inWorld != _lastMapNameWorld)
        {
            _lastMapNameWorld = inWorld;
            _mapNameText.gameObject.SetActive(!inWorld);
        }
        if (inWorld) return;
        int dl = gm.DungeonLevel;
        int wave = CombatDirector.Instance != null ? CombatDirector.Instance.CurrentStageWave : 0;
        string mapName = gm.CurrentStageIndex switch
        {
            0 => "石墓深渊", 1 => "幽暗森林", 2 => "冰霜洞窟", 3 => "烈焰熔岩",
            4 => "暗影领域", 5 => "龙巢", 6 => "虚空裂隙", 7 => "魔王殿",
            _ => $"第{gm.CurrentStageIndex + 1}关"
        };
        string mode = gm.IsEndlessMode ? " [无尽]" : "";
        string txt = $"{mapName}  {dl}F  波{wave}/{GameManager.WavesPerStage}{mode}";
        if (txt != _lastMapName) { _lastMapName = txt; _mapNameText.text = txt; }
    }

    // =================== 顶中三货币 ===================

    private void BuildCurrencies(Font font)
    {
        var root = NewChild("Currencies", panel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0, 0), new Vector2(820, 60));
        // HUD货币槽与返回/设置同规则: 副本/无尽/世界显示, 大厅由导航栏负责 (SyncHudNavButtons驱动)
        root.gameObject.SetActive(false);
        _currenciesRow = root;

        string[] icons = { "HudCoinGold", "HudCoinLife", "HudCoinPlane" };
        for (int i = 0; i < 3; i++)
        {
            float cx = -270 + i * 270;
            Img(root, icons[i], new Vector2(cx - 95, 0), new Vector2(50, 50));
            Img(root, "HudAmountBox", new Vector2(cx + 15, 0), new Vector2(169, 41));
            _currencyTexts[i] = Txt(root, "CurrencyText" + i, "0", 26, Color.white, new Vector2(cx + 15, 0), new Vector2(140, 34));
            var plus = Img(root, "HudPlus", new Vector2(cx + 118, 2), new Vector2(40, 44), raycast: true);
            _currencyPlus[i] = plus;
            var btn = plus.gameObject.AddComponent<Button>();
            btn.targetGraphic = plus;
            btn.onClick.AddListener(() =>
            {
                UIHelper.EnsureUIManager();
                UIManager.Instance.ShowRecharge();
            });
        }
        SetCurrency(0, 0); SetCurrency(1, CurrencyWallet.LifeCoins); SetCurrency(2, CurrencyWallet.PlaneCoins);
    }

    /// <summary>设置货币数值; 超过99999显示"99999+"并将加号置灰</summary>
    public void SetCurrency(int idx, long amount)
    {
        if (idx < 0 || idx > 2) return;
        bool over = amount > 99999;
        if (_currencyTexts[idx] != null) _currencyTexts[idx].text = over ? "99999+" : amount.ToString();
        if (_currencyPlus[idx] != null)
            _currencyPlus[idx].color = over ? new Color(0.45f, 0.45f, 0.5f, 0.6f) : Color.white;
    }

    // =================== 右上小地图占位 ===================

    private void BuildMinimap(Font font)
    {
        var root = NewChild("Minimap", panel.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(0, -60), new Vector2(340, 340));
        _minimapRect = root;
        var bg = root.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.85f);
        bg.raycastTarget = false;
        // MistSystem迷雾小地图(标题/玩家/敌人/营地标记)经GetMinimapHost嵌入此占位框
    }

    /// <summary>小地图宿主 (MistSystem嵌入用); HUD未显示时返回null走独立右上角回退</summary>
    public static RectTransform GetMinimapHost()
    {
        var inst = Instance;
        if (inst == null || inst.panel == null || !inst.panel.activeSelf) return null;
        return inst._minimapRect;
    }

    // =================== 底中三个功能按钮 ===================

    private void BuildBottomCenter(Font font)
    {
        var root = NewChild("BottomCenter", panel.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0, 30), new Vector2(820, 150));

        string[] icons = { "HudIconBag", "HudIconMail", "HudIconChar" };
        string[] names = { "Bag", "Mail", "Char" };
        for (int i = 0; i < 3; i++)
        {
            float cx = -270 + i * 270;
            var btnRect = NewChild(names[i] + "Btn", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(cx, 0), new Vector2(130, 130));
            var img = btnRect.AddComponent<Image>();
            img.sprite = S(icons[i]);
            img.preserveAspect = true;
            var btn = btnRect.AddComponent<Button>();
            btn.targetGraphic = img;
            int idx = i;
            btn.onClick.AddListener(() => OnFunctionButton(idx));
            _roundBtns[i] = btn;

            // 红点 (默认隐藏, 需要时SetRedDot点亮; 激活时置顶本按钮渲染顺序)
            var dotRect = NewChild("RedDot", btnRect.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(48, 48), new Vector2(26, 27));
            var dot = dotRect.AddComponent<Image>();
            dot.sprite = S("HudRedDot");
            dot.raycastTarget = false;
            dotRect.gameObject.SetActive(false);
            _redDots[i] = dotRect.gameObject;
        }
    }

    /// <summary>底中功能钮: 背包→装备面板 邮件Btn→信息窗口(综合/好友聊天) 信息→角色详情(属性/体系) (战斗流程中返回只收起面板)</summary>
    private void OnFunctionButton(int idx)
    {
        UIHelper.EnsureUIManager();
        switch (idx)
        {
            case 0: UIManager.Instance.ShowEquip(); break;
            case 1: UIManager.Instance.ShowInfo(); break;
            case 2: UIManager.Instance.ShowCharacterDetail(); break;
        }
    }

    /// <summary>红点开关 (激活时红点置顶渲染)</summary>
    public void SetRedDot(int idx, bool on)
    {
        if (idx < 0 || idx > 2 || _redDots[idx] == null) return;
        _redDots[idx].SetActive(on);
        if (on) _redDots[idx].transform.SetAsLastSibling();
    }

    // =================== 左上返回/设置 (副本/无尽/世界显示, 与副本同一套导航; 大厅由导航栏提供) ===================

    /// <summary>HudNavBtns — ← 返回(暂停面板) 与 齿轮(设置), 战斗中左上角入口</summary>
    private void BuildHudNavButtons(Font font)
    {
        var root = NewChild("HudNavBtns", panel.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0, -5), new Vector2(100, 45));
        root.gameObject.SetActive(false);
        _hudNavBtns = root;
        MakeIconBtn(root, "HudPauseBtn", "←", 22, new Vector2(2, 0), () =>
        {
            var gm = GameManager.Instance;
            if (gm != null && !gm.IsPaused) gm.TogglePause();
        });
        MakeIconBtn(root, "HudSettingsBtn", "", 0, new Vector2(50, 0), () =>
        {
            UIHelper.EnsureUIManager();
            UIManager.Instance.ShowSettings();
        }, gear: true);
    }

    /// <summary>每帧同步返回/设置显隐 (副本/无尽/世界显示 — 世界复用Dungeon状态, 与副本同一套导航; 大厅交给导航栏)</summary>
    private void SyncHudNavButtons()
    {
        if (_hudNavBtns == null) return;
        var gm = GameManager.Instance;
        bool show = gm != null
                    && (gm.CurrentState == GameState.Dungeon || gm.CurrentState == GameState.Endless);
        if (_hudNavBtns.gameObject.activeSelf != show)
            _hudNavBtns.gameObject.SetActive(show);
        // HUD货币槽与返回/设置同规则: 副本/无尽/世界显示, 大厅由导航栏货币槽负责
        if (_currenciesRow != null && _currenciesRow.gameObject.activeSelf != show)
            _currenciesRow.gameObject.SetActive(show);
    }

    private void MakeIconBtn(Transform parent, string name, string label, int fontSize, Vector2 pos, System.Action onClick, bool gear = false)
    {
        var btnRect = NewChild(name, parent, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            pos, new Vector2(40, 40));
        var img = btnRect.AddComponent<Image>();
        img.color = new Color(0.06f, 0.04f, 0.09f, 0.85f);
        var border = new GameObject("Border");
        border.transform.SetParent(btnRect.transform, false);
        var bR = border.AddComponent<RectTransform>();
        bR.anchorMin = Vector2.zero; bR.anchorMax = Vector2.one;
        bR.offsetMin = new Vector2(-2, -2); bR.offsetMax = new Vector2(2, 2);
        border.AddComponent<Image>().color = new Color(0.25f, 0.12f, 0.40f, 0.6f);
        if (gear)
        {
            var gi = new GameObject("GearIcon");
            gi.transform.SetParent(btnRect.transform, false);
            var giR = gi.AddComponent<RectTransform>();
            giR.anchorMin = Vector2.zero; giR.anchorMax = Vector2.one;
            giR.offsetMin = new Vector2(7, 7); giR.offsetMax = new Vector2(-7, -7);
            var gearImg = gi.AddComponent<Image>();
            gearImg.sprite = UIHelper.CreateGearSprite();
            gearImg.raycastTarget = false;
        }
        else
        {
            Txt(btnRect.transform, "Lbl", label, fontSize, Color.white, Vector2.zero, new Vector2(40, 40));
        }
        var btn = btnRect.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        UIHelper.SetupButtonFeedback(btn, img.color);
        btn.onClick.AddListener(() => onClick());
    }

    // =================== 右下技能轮盘 ===================

    private void BuildSkillWheel(Font font)
    {
        var root = NewChild("SkillWheel", panel.transform, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f),
            new Vector2(-350, 310), new Vector2(700, 560));

        var deco = Img(root, "HudSkillDeco", new Vector2(200, -160), new Vector2(300, 300));
        deco.raycastTarget = false;

        // 普攻大按钮 (按住持续攻击, 经AttackHeld被PlayerInputController轮询)
        var atkRect = NewChild("AtkBtn", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(240, -180), new Vector2(170, 150));
        var atkImg = atkRect.AddComponent<Image>();
        atkImg.sprite = S("HudAtk");
        var atkHold = atkRect.gameObject.AddComponent<HudHoldButton>();
        atkHold.OnHold = held => AttackHeld = held;

        // 三个环绕技能: 0=左(螺旋) 1=中上(盾) 2=右上(轮) 3=回复(左下, 叶)
        BuildSkillSlot(root, 0, "HudSkillDmg2", new Vector2(75, -140), 80f);
        BuildSkillSlot(root, 1, "HudSkillBuff1", new Vector2(160, -40), 80f);
        BuildSkillSlot(root, 2, "HudSkillDmg1", new Vector2(300, -20), 80f);
        BuildSkillSlot(root, 3, "HudSkillBuff2", new Vector2(70, -250), 80f);
    }

    private void BuildSkillSlot(Transform root, int idx, string iconSprite, Vector2 pos, float size)
    {
        var slot = NewChild("Skill" + idx, root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            pos, new Vector2(size, size));

        var circle = slot.AddComponent<Image>();
        circle.sprite = S("HudSkillCircle");
        circle.raycastTarget = true;   // 接收点击释放技能

        var iconRect = NewChild("Icon", slot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(size * 0.72f, size * 0.72f));
        var icon = iconRect.AddComponent<Image>();
        icon.sprite = S(iconSprite);
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        // 可用框 (就绪时显示)
        var readyRect = NewChild("Ready", slot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(size * 1.3f, size * 1.3f));
        var ready = readyRect.AddComponent<Image>();
        ready.sprite = S("HudSkillReady");
        ready.raycastTarget = false;
        _skillReady[idx] = readyRect.gameObject;

        // 冷却遮罩 (Radial360 顺时针扫过) + 倒计时文本
        var coolRect = NewChild("Cool", slot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(size, size));
        var cool = coolRect.AddComponent<Image>();
        cool.sprite = S("HudSkillCool");
        cool.type = Image.Type.Filled;
        cool.fillMethod = Image.FillMethod.Radial360;
        cool.fillOrigin = (int)Image.Origin360.Top;
        cool.fillClockwise = true;
        cool.fillAmount = 0f;
        cool.raycastTarget = false;
        coolRect.gameObject.SetActive(false);
        _skillCool[idx] = cool;

        var countRect = NewChild("Count", slot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(size, size * 0.5f));
        var count = countRect.AddComponent<Text>();
        count.alignment = TextAnchor.MiddleCenter;
        count.fontSize = (int)(size * 0.34f);
        count.color = Color.white;
        count.font = GameManager.GetUIFont();
        count.raycastTarget = false;
        countRect.gameObject.SetActive(false);
        _skillCount[idx] = count;

        var press = slot.gameObject.AddComponent<HudPressButton>();
        press.OnDown = () => CastSkill(idx);

        _cdRemain[idx] = 0f; _cdTotal[idx] = 1f;
    }

    /// <summary>技能槽按下 → 复用旧SkillButton输入链 (OnSkillXButton置单帧flag, 战斗控制器消费; 0-2=技能1-3, 3=闪避)</summary>
    private void CastSkill(int idx)
    {
        var input = GameManager.Instance != null && GameManager.Instance.Player != null
            ? GameManager.Instance.Player.InputCtrl : null;
        if (input == null) return;
        switch (idx)
        {
            case 0: input.OnSkill1Button(); break;
            case 1: input.OnSkill2Button(); break;
            case 2: input.OnSkill3Button(); break;
            case 3: input.OnDashButton(); break;
        }
    }

    /// <summary>启动技能冷却 (秒); 冷却期间遮罩顺时针收缩+倒计时数字</summary>
    public void StartSkillCooldown(int idx, float seconds)
    {
        if (idx < 0 || idx > 3 || seconds <= 0f) return;
        _cdRemain[idx] = seconds;
        _cdTotal[idx] = seconds;
    }

    /// <summary>清空指定技能冷却</summary>
    public void ClearSkillCooldown(int idx)
    {
        if (idx < 0 || idx > 3) return;
        _cdRemain[idx] = 0f;
    }

    private long _lastHpCur = -1, _lastHpMax = -1;
    private int _lastLevel = -1, _lastGold = -1;
    private int _lastLife = -1, _lastPlane = -1;

    /// <summary>轮询玩家数据, 变化时刷新HUD (HP/等级/金币)</summary>
    private void PollGameData()
    {
        var p = GameManager.Instance != null ? GameManager.Instance.Player : null;
        if (p == null || p.Stats == null) return;
        int cur = p.CurrentHp, max = p.MaxHp;
        if (cur != _lastHpCur || max != _lastHpMax) { _lastHpCur = cur; _lastHpMax = max; SetHp(cur, max); }
        int lv = p.Stats.Level;
        if (lv != _lastLevel) { _lastLevel = lv; SetLevel(lv); }
        int gold = p.Stats.Gold;
        if (gold != _lastGold) { _lastGold = gold; SetCurrency(0, gold); }
        if (CurrencyWallet.LifeCoins != _lastLife) { _lastLife = CurrencyWallet.LifeCoins; SetCurrency(1, _lastLife); }
        if (CurrencyWallet.PlaneCoins != _lastPlane) { _lastPlane = CurrencyWallet.PlaneCoins; SetCurrency(2, _lastPlane); }
    }

    private void Update()
    {
        PollGameData();
        UpdateMapName();
        PollSkillCooldowns();
        SyncHudNavButtons();
        _questRefreshTimer -= Time.unscaledDeltaTime;
        if (_questRefreshTimer <= 0f) { _questRefreshTimer = 2f; RefreshQuestPanel(); }
    }

    /// <summary>技能冷却轮询: 有玩家时以SkillCtrl实时CD为准 (键盘/按钮释放都能反映), 无玩家时退回StartSkillCooldown手动计时</summary>
    private void PollSkillCooldowns()
    {
        var p = GameManager.Instance != null ? GameManager.Instance.Player : null;
        for (int i = 0; i < 4; i++)
        {
            float remain, total;
            if (p != null && p.SkillCtrl != null)
            {
                switch (i)
                {
                    case 0: remain = p.Skill1CooldownRemaining; total = Mathf.Max(0.01f, p.Skill1CooldownMax); break;
                    case 1: remain = p.Skill2CooldownRemaining; total = Mathf.Max(0.01f, p.Skill2CooldownMax); break;
                    case 2: remain = p.Skill3CooldownRemaining; total = Mathf.Max(0.01f, p.Skill3CooldownMax); break;
                    default: remain = p.DashCooldownRemaining; total = Mathf.Max(0.01f, p.DashCooldownMax); break;
                }
            }
            else
            {
                if (_cdRemain[i] > 0f) _cdRemain[i] -= Time.unscaledDeltaTime;
                remain = Mathf.Max(0f, _cdRemain[i]);
                total = Mathf.Max(0.01f, _cdTotal[i]);
            }

            bool onCd = remain > 0f;
            if (_skillReady[i] != null && _skillReady[i].activeSelf == onCd)
                _skillReady[i].SetActive(!onCd);
            if (_skillCool[i] != null)
            {
                if (_skillCool[i].gameObject.activeSelf != onCd) _skillCool[i].gameObject.SetActive(onCd);
                if (onCd) _skillCool[i].fillAmount = remain / total;
            }
            if (_skillCount[i] != null)
            {
                if (_skillCount[i].gameObject.activeSelf != onCd) _skillCount[i].gameObject.SetActive(onCd);
                if (onCd) _skillCount[i].text = Mathf.CeilToInt(remain).ToString();
            }
        }
    }

    // =================== 安全区适配 (刘海屏) ===================

    private void ApplySafeArea()
    {
        var sa = Screen.safeArea;
        if (sa.width >= Screen.width && sa.height >= Screen.height) return;
        var canvas = panel.GetComponentInParent<Canvas>().rootCanvas;
        float scale = canvas != null ? canvas.scaleFactor : 1f;
        var pr = panel.GetComponent<RectTransform>();
        pr.offsetMin = new Vector2(sa.x / scale, sa.y / scale);
        pr.offsetMax = -new Vector2((Screen.width - sa.x - sa.width) / scale, (Screen.height - sa.y - sa.height) / scale);
    }
}
