using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

/// <summary>
/// 角色详情面板 (设计图版) — 1200x900底框+四页签(属性/体系/副职禁用/天赋禁用, 选中黑底白字)+右上关闭按钮(紫色X切图骑角)。
/// 体系页(用户给定的实测坐标表): 左侧角色立绘+右侧经脉节点图——4大圆(-100/330, 190/-180)
///         +8外环小节点(金木水火土风雷冰, 八边形)+5内环(精智耐攻敏, 五边形), 水墨素材装饰线DecoLines(150,-30) 550x450,
///         点击节点+1点(消耗SkillPoints), 按住下滑锁定/上滑解锁, 阴阳±10步进(面板右缘), 点数框/重塑/提示在图底部。
/// 属性页: 名字/职业/等级/分割线/基础属性双列(生命上限/攻击力/防御/智力/敏捷/耐力值/精神)。
/// 切图位于 Resources/Sprites/CharacterDetail/。经 HUD 角色按钮 (UIManager.ShowCharacterDetail) 打开。
/// </summary>
public class CharacterDetailUI : UIPanelBase
{
    public static CharacterDetailUI Instance { get; private set; }

    private const float ReshapeCostGold = 1000f;   // 重塑消耗金币
    private const int StepAmount = 10;             // 阴阳步进

    private static Sprite S(string name) => Resources.Load<Sprite>($"Sprites/CharacterDetail/{name}");

    // --- 配色 (取自设计图) ---
    private static readonly Color ColText = new Color(0.93f, 0.91f, 0.96f);
    private static readonly Color ColOrange = new Color(0.91f, 0.64f, 0.24f);
    private static readonly Color ColNotLearned = new Color(0.48f, 0.45f, 0.54f);
    private static readonly Color ColTabOff = new Color(0.62f, 0.59f, 0.68f);
    private static readonly Color ColTabDisabled = new Color(0.35f, 0.33f, 0.40f);
    private static readonly Color ColTabSelBg = new Color(0.06f, 0.065f, 0.09f);   // 选中页签黑底(设计图采样37,37,37)
    private static readonly Color ColHint = new Color(0.60f, 0.57f, 0.66f, 0.85f);
    private static readonly Color ColReshape = new Color(1f, 0.70f, 0.13f);

    private int _page;                 // 0=属性 1=体系
    private GameObject _pageAttr;
    private GameObject _pageSys;
    private readonly Image[] _tabBg = new Image[2];
    private readonly Text[] _tabTxt = new Text[2];

    // 体系页节点布局(用户实测坐标表, 面板1200x900坐标系): 4大圆 + 8外环(八边形) + 5内环(五边形)
    private struct NodeLayout { public int Id; public float X, Y; public int Tier; }   // Tier: 0=大 1=小
    private static readonly NodeLayout[] SysLayout =
    {
        new NodeLayout{ Id = CharacterSystemData.Space,   X = -100, Y =  190, Tier = 0 },
        new NodeLayout{ Id = CharacterSystemData.Law,     X =  330, Y =  190, Tier = 0 },
        new NodeLayout{ Id = CharacterSystemData.Life,    X = -100, Y = -180, Tier = 0 },
        new NodeLayout{ Id = CharacterSystemData.YinYang, X =  330, Y = -180, Tier = 0 },   // 坐标表原文-330与法则(330)/八边形右列矛盾, 按右侧对称修正
        // 外环八边形: 上/左上/右上/左/右/左下/右下/下
        new NodeLayout{ Id = CharacterSystemData.Jin,     X =  113, Y =  126, Tier = 1 },
        new NodeLayout{ Id = CharacterSystemData.Bing,    X =  -18, Y =   87, Tier = 1 },
        new NodeLayout{ Id = CharacterSystemData.Mu,      X =  263, Y =   87, Tier = 1 },
        new NodeLayout{ Id = CharacterSystemData.Lei,     X =  -60, Y =  -15, Tier = 1 },
        new NodeLayout{ Id = CharacterSystemData.Shui,    X =  290, Y =  -16, Tier = 1 },
        new NodeLayout{ Id = CharacterSystemData.Feng,    X =    0, Y = -120, Tier = 1 },
        new NodeLayout{ Id = CharacterSystemData.Huo,     X =  246, Y = -120, Tier = 1 },
        new NodeLayout{ Id = CharacterSystemData.Tu,      X =  115, Y = -150, Tier = 1 },
        // 内环五边形: 顶/左/右/左下/右下
        new NodeLayout{ Id = CharacterSystemData.Jing,    X =  115, Y =   60, Tier = 1 },
        new NodeLayout{ Id = CharacterSystemData.Zhi,     X =   30, Y =    5, Tier = 1 },
        new NodeLayout{ Id = CharacterSystemData.Nai,     X =  200, Y =    5, Tier = 1 },
        new NodeLayout{ Id = CharacterSystemData.Gong,    X =   70, Y =  -65, Tier = 1 },
        new NodeLayout{ Id = CharacterSystemData.Min,     X =  170, Y =  -65, Tier = 1 },
    };

    // 体系页控件
    private readonly Text[] _nodeName = new Text[CharacterSystemData.NodeCount];
    private readonly Text[] _nodeStatus = new Text[CharacterSystemData.NodeCount];
    private readonly GameObject[] _nodeLock = new GameObject[CharacterSystemData.NodeCount];
    private readonly Text[] _nodeStatusSub = new Text[CharacterSystemData.NodeCount]; // 阴阳节点第二行
    private Text _pointsText;
    private Text _yinValue;
    private Text _yangValue;

    // 属性页控件
    private Text _attrName;
    private Text _attrClass;
    private Text _attrSubClass;
    private Text _attrLevel;
    private readonly Text[] _attrValues = new Text[7];

    // 重塑确认框
    private GameObject _reshapeDialog;
    private Text _dialogMsg;
    private Coroutine _msgResetRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    protected override void OnDestroy()
    {
        if (Instance == this) Instance = null;
        base.OnDestroy();
    }

    protected override string PanelTitle => "";   // 设计图无顶部导航栏, 用右上X关闭

    // =================== 构建 ===================

    protected override void BuildContent()
    {
        Canvas canvas = GameManager.EnsureCanvas();

        panel = new GameObject("CharacterDetailPanel");
        panel.transform.SetParent(canvas.transform, false);
        var pr = panel.AddComponent<RectTransform>();
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one;
        pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;

        // 全屏暗色底 (点击空白关闭)
        var backdrop = NewRect("Backdrop", panel.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var bdImg = backdrop.AddComponent<Image>();
        bdImg.color = new Color(0f, 0f, 0f, 0.55f);
        bdImg.raycastTarget = true;
        var bdBtn = backdrop.AddComponent<Button>();
        bdBtn.transition = Selectable.Transition.None;
        bdBtn.onClick.AddListener(ClosePanel);

        // 底框 (用户指定 1200x900, 居中)
        var frame = NewRect("Frame", panel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(1200, 900));
        var frameImg = frame.AddComponent<Image>();
        frameImg.sprite = S("panel_bg");
        frameImg.raycastTarget = true;

        BuildTabs(frame.gameObject);
        BuildCloseButton(frame.gameObject);
        BuildCharDisplay(frame.gameObject);
        BuildAttrPage(frame.gameObject);
        BuildSysPage(frame.gameObject);
        BuildReshapeDialog(panel);

        SwitchPage(1);   // 设计图默认展示体系页
    }

    private void BuildTabs(GameObject frame)
    {
        string[] labels = { "属性", "体系", "副职", "天赋" };
        float[] xs = { -450, -300, -150, 0 };
        for (int i = 0; i < 4; i++)
        {
            var tab = NewRect("Tab" + labels[i], frame.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(xs[i], 350), new Vector2(140, 60));
            var img = tab.AddComponent<Image>();
            img.raycastTarget = i < 2;                  // 副职/天赋未开放
            img.color = i < 2 ? Color.white : Color.clear;

            var txt = MakeText(tab.transform, "TabTxt", labels[i], 34, Color.white, TextAnchor.MiddleCenter);
            txt.rectTransform.anchorMin = Vector2.zero; txt.rectTransform.anchorMax = Vector2.one;
            txt.rectTransform.offsetMin = Vector2.zero; txt.rectTransform.offsetMax = Vector2.zero;

            if (i < 2)
            {
                int page = i;
                var btn = tab.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => SwitchPage(page));
                _tabBg[i] = img;
                _tabTxt[i] = txt;
            }
            else
            {
                txt.color = ColTabDisabled;
            }
        }
    }

    private void BuildCloseButton(GameObject frame)
    {
        // 关闭按钮: 紫色X切图, 用户指定 (530,390) 200x200 骑右上角
        var go = NewRect("CloseBtn", frame.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(530, 390), new Vector2(200, 200));
        var img = go.AddComponent<Image>();
        img.sprite = S("close_btn");   // 素材已提亮处理(紫盘RGB*1.7/alpha*1.8)
        img.raycastTarget = true;
        var btn = go.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(ClosePanel);
    }

    private void BuildCharDisplay(GameObject frame)
    {
        // 角色立绘 (设计图体系页/属性页均在左侧, 体系页节点图已右移避让)
        var img = NewRect("CharDisplay", frame.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-395, -42), new Vector2(352, 608)).AddComponent<Image>();
        img.sprite = S("char_display");
        img.preserveAspect = true;
        img.raycastTarget = false;
    }

    // =================== 属性页 ===================

    private void BuildAttrPage(GameObject frame)
    {
        _pageAttr = NewRect("PageAttr", frame.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero).gameObject;

        _attrName = MakeText(_pageAttr.transform, "AttrNameTxt", "", 34, ColText, TextAnchor.MiddleLeft);
        PlacePivotLeft(_attrName.rectTransform, -131, 230, 420, 44);

        _attrClass = MakeText(_pageAttr.transform, "AttrClassTxt", "", 28, ColText, TextAnchor.MiddleLeft);
        PlacePivotLeft(_attrClass.rectTransform, -131, 171, 240, 40);

        _attrSubClass = MakeText(_pageAttr.transform, "AttrSubClassTxt", "副职:  无", 28, ColText, TextAnchor.MiddleLeft);
        PlacePivotLeft(_attrSubClass.rectTransform, 93, 171, 240, 40);

        _attrLevel = MakeText(_pageAttr.transform, "AttrLevelTxt", "", 28, ColText, TextAnchor.MiddleLeft);
        PlacePivotLeft(_attrLevel.rectTransform, -131, 123, 300, 40);

        // 分割线
        var div = NewRect("Divider", _pageAttr.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(160, 83), new Vector2(583, 6));
        var divImg = div.AddComponent<Image>();
        divImg.sprite = S("divider");
        divImg.raycastTarget = false;

        var baseHeader = MakeText(_pageAttr.transform, "BaseHeaderTxt", "基础属性:", 28, ColOrange, TextAnchor.MiddleLeft);
        PlacePivotLeft(baseHeader.rectTransform, -131, 46, 240, 40);

        // 双列属性行: 左列 生命上限/攻击力/防御/智力, 右列 敏捷/耐力值/精神
        string[] leftNames = { "生命上限:", "攻击力:", "防御:", "智力:" };
        string[] rightNames = { "敏捷:", "耐力值:", "精神:" };
        float[] rowYs = { -21, -85, -149, -213 };
        for (int i = 0; i < 4; i++)
        {
            BuildStatRow(_pageAttr, leftNames[i], -126, rowYs[i], i);
            if (i < 3) BuildStatRow(_pageAttr, rightNames[i], 157, rowYs[i], 4 + i);
        }
    }

    private void BuildStatRow(GameObject page, string label, float iconX, float y, int valueIdx)
    {
        string key = label.TrimEnd(':');   // 按属性名生成唯一子物体名, 避免同级同名

        // 图标占位 (白色小圆)
        var icon = NewRect("Icon_" + key, page.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(iconX, y), new Vector2(26, 26));
        var iconImg = icon.AddComponent<Image>();
        iconImg.sprite = CreateCircleSprite();
        iconImg.color = new Color(0.82f, 0.79f, 0.87f, 0.95f);
        iconImg.raycastTarget = false;

        var lbl = MakeText(page.transform, "RowLabel_" + key, label, 26, ColText, TextAnchor.MiddleLeft);
        PlacePivotLeft(lbl.rectTransform, iconX + 17, y, 200, 34);

        var val = MakeText(page.transform, "RowValue_" + key, "", 26, ColText, TextAnchor.MiddleLeft);
        PlacePivotLeft(val.rectTransform, iconX + 160, y, 160, 34);
        _attrValues[valueIdx] = val;
    }

    // =================== 体系页 ===================

    private void BuildSysPage(GameObject frame)
    {
        _pageSys = NewRect("PageSys", frame.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(50, 0), Vector2.zero).gameObject;

        // 体系装饰线: 设计图水墨素材(外矩形+八边形+五边形三层双笔墨线)整体拉伸,
        // 用户指定矩形(150,-30) 550x450, 先建保证节点盖在线上
        var deco = NewRect("DecoLines", _pageSys.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(150, -30), new Vector2(550, 450));
        var decoImg = deco.AddComponent<Image>();
        decoImg.sprite = S("deco_lines");
        decoImg.raycastTarget = false;

        // 17节点 — 4大圆 + 8元素外环(八边形) + 5核心内环(五边形), 坐标见 SysLayout
        for (int i = 0; i < SysLayout.Length; i++)
            BuildNode(CharacterSystemData.Nodes[SysLayout[i].Id], new Vector2(SysLayout[i].X, SysLayout[i].Y), SysLayout[i].Tier);

        // 阴阳 ±10 步进 (用户坐标: 右缘, 阴上阳下)
        BuildStepper("Yin", "阴", new Vector2(425, -65));
        BuildStepper("Yang", "阳", new Vector2(425, -110));

        // 点数框 (设计图: 节点图底部偏左, 重塑按钮左下方)
        var ptsBox = NewRect("PointsBox", _pageSys.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-130, -310), new Vector2(215, 50));
        var ptsImg = ptsBox.AddComponent<Image>();
        ptsImg.sprite = S("points_box");
        ptsImg.raycastTarget = false;
        _pointsText = MakeText(ptsBox.transform, "PointsTxt", "点数: 0", 22, ColText, TextAnchor.MiddleCenter);
        _pointsText.rectTransform.anchorMin = Vector2.zero; _pointsText.rectTransform.anchorMax = Vector2.one;
        _pointsText.rectTransform.offsetMin = Vector2.zero; _pointsText.rectTransform.offsetMax = Vector2.zero;

        // 重塑按钮 (设计图: 节点图底部中央, 点数框右上方, 白色确认按钮着橙色; 下移避让土节点数值)
        var reshape = NewRect("ReshapeBtn", _pageSys.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(115, -250), new Vector2(115, 40));
        var rImg = reshape.AddComponent<Image>();
        rImg.sprite = S("btn_confirm_white");
        rImg.color = ColReshape;
        var rBtn = reshape.AddComponent<Button>();
        rBtn.transition = Selectable.Transition.None;
        rBtn.onClick.AddListener(OpenReshapeDialog);
        var rTxt = MakeText(reshape.transform, "ReshapeTxt", "重塑", 22, Color.black, TextAnchor.MiddleCenter);
        rTxt.rectTransform.anchorMin = Vector2.zero; rTxt.rectTransform.anchorMax = Vector2.one;
        rTxt.rectTransform.offsetMin = Vector2.zero; rTxt.rectTransform.offsetMax = Vector2.zero;

        // 重塑按钮下方提示 (保持按钮下方42px, 与点数框不重叠)
        var hint = MakeText(_pageSys.transform, "HintTxt", "点住体系下滑上锁点数，上滑解锁", 17, new Color(0.66f, 0.63f, 0.72f, 0.95f), TextAnchor.MiddleCenter);
        Place(hint.rectTransform, 115, -292, 330, 22);
    }

    private void BuildNode(CharacterSystemData.NodeDef def, Vector2 localPos, int tier)
    {
        // 尺寸用户指定: 大145x110, 小65x70
        Vector2 size = tier == 0 ? new Vector2(145, 110) : new Vector2(65, 70);
        var node = NewRect("Node_" + def.Name, _pageSys.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            localPos, size);
        var img = node.AddComponent<Image>();
        img.sprite = S(def.Big ? "node_big" : "node_small");
        img.raycastTarget = true;
        img.preserveAspect = true;

        var btn = node.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        int id = def.Id;
        btn.onClick.AddListener(() => AllocateOne(id));
        var drag = node.AddComponent<SystemNodeGesture>();
        drag.onLockToggle = () => OnGestureLock(id, true);
        drag.onUnlockToggle = () => OnGestureLock(id, false);

        // 名字 (节点内铺满居中)
        if (tier == 0)
        {
            var nameTxt = MakeText(node.transform, "NameTxt", def.Name, 26, def.Orange ? ColOrange : ColText, TextAnchor.MiddleCenter);
            nameTxt.rectTransform.anchorMin = Vector2.zero; nameTxt.rectTransform.anchorMax = Vector2.one;
            nameTxt.rectTransform.offsetMin = Vector2.zero; nameTxt.rectTransform.offsetMax = Vector2.zero;
            _nodeName[id] = nameTxt;
        }
        else
        {
            var nameTxt = MakeText(node.transform, "NameTxt", def.Name, 18, def.Orange ? ColOrange : ColText, TextAnchor.MiddleCenter);
            nameTxt.rectTransform.anchorMin = Vector2.zero; nameTxt.rectTransform.anchorMax = Vector2.one;
            nameTxt.rectTransform.offsetMin = Vector2.zero; nameTxt.rectTransform.offsetMax = Vector2.zero;
            _nodeName[id] = nameTxt;
        }

        // 状态 (节点下方, 设计图样式): 数值 / 未习得 / 阴阳双行
        float statusY = tier == 0 ? -75 : -47;
        var status = MakeText(node.transform, "StatusTxt", "", tier == 0 ? 20 : 16, ColText, TextAnchor.MiddleCenter);
        Place(status.rectTransform, 0, statusY, tier == 0 ? 150 : 70, tier == 0 ? 26 : 18);
        _nodeStatus[id] = status;

        // 阴阳节点第二行
        if (def.Id == CharacterSystemData.YinYang)
        {
            var sub = MakeText(node.transform, "StatusSubTxt", "", 17, ColText, TextAnchor.MiddleCenter);
            Place(sub.rectTransform, 0, statusY - 26, 150, 22);
            _nodeStatusSub[id] = sub;
        }

        // 锁图标 (状态文字右侧)
        Vector2 lockPos = tier == 0 ? new Vector2(58, statusY) : new Vector2(34, statusY);
        var lockGo = CreateLockIcon(node.transform, lockPos);
        lockGo.transform.localScale = tier == 0 ? Vector3.one : new Vector3(0.75f, 0.75f, 1f);
        lockGo.SetActive(false);
        _nodeLock[id] = lockGo;
    }

    private void BuildStepper(string objName, string label, Vector2 pos)
    {
        var row = NewRect("Stepper" + objName, _pageSys.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            pos, new Vector2(180, 40));

        var lbl = MakeText(row.transform, "LabelTxt", label, 26, ColText, TextAnchor.MiddleCenter);
        Place(lbl.rectTransform, -70, 0, 40, 36);

        var box = NewRect("Box", row.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(20, 0), new Vector2(120, 38));
        var boxImg = box.AddComponent<Image>();
        boxImg.sprite = S("stat_stepper");
        boxImg.raycastTarget = false;

        var step = MakeText(box.transform, "ValueTxt", "0", 24, ColText, TextAnchor.MiddleCenter);
        step.rectTransform.anchorMin = Vector2.zero; step.rectTransform.anchorMax = Vector2.one;
        step.rectTransform.offsetMin = Vector2.zero; step.rectTransform.offsetMax = Vector2.zero;

        // 半透明按钮区域覆盖 - 与 + (左40px/右40px, 中间留步进数字)
        var minus = NewRect("Minus", box.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(40, 38));
        var minusImg = minus.AddComponent<Image>();
        minusImg.color = Color.clear;
        var minusBtn = minus.AddComponent<Button>();
        minusBtn.transition = Selectable.Transition.None;
        minusBtn.onClick.AddListener(() => MovePool(objName == "Yin", -StepAmount));

        var plus = NewRect("Plus", box.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(40, 38));
        var plusImg = plus.AddComponent<Image>();
        plusImg.color = Color.clear;
        var plusBtn = plus.AddComponent<Button>();
        plusBtn.transition = Selectable.Transition.None;
        plusBtn.onClick.AddListener(() => MovePool(objName == "Yin", +StepAmount));

        // 可见的 -/+ 字形 (用户要求"阴 - 0 +"样式; 点击仍走上方透明按钮区域)
        var minusTxt = MakeText(box.transform, "MinusTxt", "-", 26, ColText, TextAnchor.MiddleCenter);
        Place(minusTxt.rectTransform, -40, 0, 40, 38);
        var plusTxt = MakeText(box.transform, "PlusTxt", "+", 26, ColText, TextAnchor.MiddleCenter);
        Place(plusTxt.rectTransform, 40, 0, 40, 38);

        if (objName == "Yin") _yinValue = step;
        else _yangValue = step;
    }

    // =================== 重塑确认框 ===================

    private void BuildReshapeDialog(GameObject root)
    {
        _reshapeDialog = new GameObject("ReshapeDialog");
        _reshapeDialog.transform.SetParent(root.transform, false);
        var r = _reshapeDialog.AddComponent<RectTransform>();
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        _reshapeDialog.SetActive(false);

        var dim = NewRect("Dim", _reshapeDialog.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var dimImg = dim.AddComponent<Image>();
        dimImg.color = Color.clear;   // 设计图无暗化遮罩, 透明仅作点击空白取消的射线区
        var dimBtn = dim.AddComponent<Button>();
        dimBtn.transition = Selectable.Transition.None;
        dimBtn.onClick.AddListener(CloseReshapeDialog);   // 点击空白取消

        // 提示框 (1011x437 * 0.75 ≈ 758x328)
        var box = NewRect("DialogBox", _reshapeDialog.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, 40), new Vector2(758, 328));
        var boxImg = box.AddComponent<Image>();
        boxImg.sprite = S("dialog_bg");
        boxImg.raycastTarget = true;

        _dialogMsg = MakeText(box.transform, "MsgTxt", ReshapeMessage(), 26, ColText, TextAnchor.UpperCenter);
        Place(_dialogMsg.rectTransform, 0, 52, 640, 150);

        var confirm = NewRect("ConfirmBtn", box.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, -70), new Vector2(150, 60));
        var cImg = confirm.AddComponent<Image>();
        cImg.sprite = S("btn_confirm");
        var cBtn = confirm.AddComponent<Button>();
        cBtn.transition = Selectable.Transition.None;
        cBtn.onClick.AddListener(OnReshapeConfirmed);
        var cTxt = MakeText(confirm.transform, "ConfirmTxt", "确认", 30, Color.white, TextAnchor.MiddleCenter);
        cTxt.rectTransform.anchorMin = Vector2.zero; cTxt.rectTransform.anchorMax = Vector2.one;
        cTxt.rectTransform.offsetMin = Vector2.zero; cTxt.rectTransform.offsetMax = Vector2.zero;

        // 确认按钮下方灰色小字 (设计图: "点击空白处取消")
        var dlgHint = MakeText(box.transform, "CancelHintTxt", "点击空白处取消", 16, ColHint, TextAnchor.MiddleCenter);
        Place(dlgHint.rectTransform, 0, -112, 300, 18);
    }

    private static string ReshapeMessage()
    {
        return $"需要消耗{ReshapeCostGold:0}金币，重新分配所有点数，上锁点数不会被重置，是否消耗{ReshapeCostGold:0}金币重塑体系？";
    }

    private void OpenReshapeDialog()
    {
        if (_dialogMsg != null) _dialogMsg.text = ReshapeMessage();
        if (_reshapeDialog != null) _reshapeDialog.SetActive(true);
    }

    private void CloseReshapeDialog()
    {
        if (_reshapeDialog != null) _reshapeDialog.SetActive(false);
    }

    private void OnReshapeConfirmed()
    {
        var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
        if (player == null) { CloseReshapeDialog(); return; }

        if (!player.Stats.SpendGold((int)ReshapeCostGold))
        {
            // 金币不足: 消息短暂变红提示
            if (_dialogMsg != null)
            {
                _dialogMsg.text = "金币不足！";
                _dialogMsg.color = new Color(0.95f, 0.35f, 0.30f);
                if (_msgResetRoutine != null) StopCoroutine(_msgResetRoutine);
                _msgResetRoutine = StartCoroutine(ResetDialogMsgLater());
            }
            return;
        }

        int refund = CharacterSystemData.Reshape();
        player.Stats.SkillPoints += refund;
        CharacterSystemData.ApplyEffects(player.Stats.Runtime);
        player.Stats.MarkDirty();
        CloseReshapeDialog();
        RefreshData();
    }

    private IEnumerator ResetDialogMsgLater()
    {
        yield return new WaitForSecondsRealtime(1.5f);
        if (_dialogMsg != null)
        {
            _dialogMsg.text = ReshapeMessage();
            _dialogMsg.color = ColText;
        }
        _msgResetRoutine = null;
    }

    // =================== 交互 ===================

    private void SwitchPage(int page)
    {
        _page = page;
        if (_pageAttr != null) _pageAttr.SetActive(page == 0);
        if (_pageSys != null) _pageSys.SetActive(page == 1);
        for (int i = 0; i < 2; i++)
        {
            bool selected = i == page;
            if (_tabBg[i] != null) _tabBg[i].color = selected ? ColTabSelBg : Color.clear;
            if (_tabTxt[i] != null) _tabTxt[i].color = selected ? Color.white : ColTabOff;
        }
    }

    private void AllocateOne(int nodeId)
    {
        var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
        if (player == null) return;
        int pool = player.Stats.SkillPoints;
        if (pool <= 0) return;
        int granted = CharacterSystemData.Allocate(nodeId, 1, pool);
        if (granted <= 0) return;
        player.Stats.SkillPoints = pool - granted;
        AfterDataChanged(player);
    }

    private void MovePool(bool isYin, int delta)
    {
        var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
        if (player == null) return;
        int pool = player.Stats.SkillPoints;
        int moved = isYin ? CharacterSystemData.MoveYin(delta, pool) : CharacterSystemData.MoveYang(delta, pool);
        if (moved == 0) return;
        player.Stats.SkillPoints = pool - moved;
        AfterDataChanged(player);
    }

    private void OnGestureLock(int nodeId, bool locked)
    {
        var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
        if (player == null) return;
        if (CharacterSystemData.GetPoints(nodeId) <= 0) return;   // 未习得节点不可锁
        if (CharacterSystemData.IsLocked(nodeId) == locked) return;
        CharacterSystemData.SetLocked(nodeId, locked);
        player.Stats.MarkDirty();
        RefreshSysPage();
    }

    private void AfterDataChanged(PlayerController player)
    {
        CharacterSystemData.ApplyEffects(player.Stats.Runtime);
        player.Stats.MarkDirty();
        RefreshSysPage();
    }

    private void ClosePanel()
    {
        Hide();
        UIHelper.ReturnFromPanel();
    }

    // =================== 刷新 ===================

    public override void RefreshData()
    {
        // 槽位切换后缓存可能过期
        if (!CharacterSystemData.IsLoadedForActiveSlot)
            CharacterSystemData.Load($"ARPG_S{PlayerProgressData.ActiveSlot}_");

        RefreshAttrPage();
        RefreshSysPage();
        SwitchPage(_page);
    }

    private void RefreshAttrPage()
    {
        var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
        string name = PlayerProgressData.GetSavedCharacterName();
        if (string.IsNullOrEmpty(name)) name = "冒险者";
        if (_attrName != null) _attrName.text = name;

        string className = "未知";
        if (player != null)
        {
            var cd = ClassData.GetClassData(player.Stats.Class);
            if (cd != null) className = cd.ClassName;
        }
        if (_attrClass != null) _attrClass.text = $"职业:  {className}";
        if (_attrSubClass != null) _attrSubClass.text = "副职:  无";
        if (_attrLevel != null) _attrLevel.text = $"等级: {(player != null ? player.Stats.Level : 0)}";

        int zhi = CharacterSystemData.GetPoints(CharacterSystemData.Zhi);
        int min = CharacterSystemData.GetPoints(CharacterSystemData.Min);
        int nai = CharacterSystemData.GetPoints(CharacterSystemData.Nai);
        int jing = CharacterSystemData.GetPoints(CharacterSystemData.Jing);
        int[] vals =
        {
            player != null ? player.Stats.TotalMaxHp : 0,
            player != null ? player.Stats.BuffedAttack : 0,
            player != null ? player.Stats.BuffedDefense : 0,
            zhi, min, nai, jing,
        };
        for (int i = 0; i < _attrValues.Length; i++)
            if (_attrValues[i] != null) _attrValues[i].text = vals[i].ToString();
    }

    private void RefreshSysPage()
    {
        var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
        if (_pointsText != null)
            _pointsText.text = $"点数: {(player != null ? player.Stats.SkillPoints : 0)}";

        for (int i = 0; i < CharacterSystemData.NodeCount; i++)
        {
            var def = CharacterSystemData.Nodes[i];
            int pts = CharacterSystemData.GetPoints(i);
            bool locked = CharacterSystemData.IsLocked(i);

            if (_nodeStatus[i] != null)
            {
                if (def.Id == CharacterSystemData.YinYang)
                {
                    _nodeStatus[i].text = $"阴: {CharacterSystemData.Yin}";
                    if (_nodeStatusSub[i] != null) _nodeStatusSub[i].text = $"阳: {CharacterSystemData.Yang}";
                }
                else
                {
                    _nodeStatus[i].text = pts > 0 ? pts.ToString() : "未习得";
                    _nodeStatus[i].color = pts > 0 ? ColText : ColNotLearned;
                }
            }
            if (_nodeLock[i] != null) _nodeLock[i].SetActive(locked);
        }

        if (_yinValue != null) _yinValue.text = CharacterSystemData.Yin.ToString();
        if (_yangValue != null) _yangValue.text = CharacterSystemData.Yang.ToString();
    }

    protected override void OnHide()
    {
        // 面板隐藏时确保确认框复位
        CloseReshapeDialog();
    }

    // =================== 工具 ===================

    private static RectTransform NewRect(string name, Transform parent, Vector2 anchor, Vector2 pivot,
        Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var r = go.AddComponent<RectTransform>();
        r.anchorMin = anchor; r.anchorMax = anchor; r.pivot = pivot;
        r.anchoredPosition = pos; r.sizeDelta = size;
        return r;
    }

    private static Text MakeText(Transform parent, string objName, string content, int fontSize, Color color, TextAnchor anchor)
    {
        var go = new GameObject(objName);
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.text = content;
        t.font = GameManager.GetUIFont();
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = anchor;
        t.raycastTarget = false;
        return t;
    }

    private static void Place(RectTransform r, float x, float y, float w, float h)
    {
        r.anchorMin = new Vector2(0.5f, 0.5f); r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = new Vector2(x, y);
        r.sizeDelta = new Vector2(w, h);
    }

    private static void PlacePivotLeft(RectTransform r, float x, float y, float w, float h)
    {
        r.anchorMin = new Vector2(0.5f, 0.5f); r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0f, 0.5f);
        r.anchoredPosition = new Vector2(x, y);
        r.sizeDelta = new Vector2(w, h);
    }

    private static Sprite _circleSprite;
    private static Sprite CreateCircleSprite()
    {
        if (_circleSprite != null) return _circleSprite;
        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color[size * size];
        float c = (size - 1) / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                px[y * size + x] = d <= c ? Color.white : Color.clear;
            }
        tex.SetPixels(px);
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();
        _circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return _circleSprite;
    }

    private GameObject CreateLockIcon(Transform parent, Vector2 pos)
    {
        var root = NewRect("Lock", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(16, 20));
        Color c = new Color(0.95f, 0.93f, 0.98f);
        // 锁扣 (∩ 三段)
        var l = NewRect("L", root.transform, new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(0, 0), new Vector2(2, 8));
        l.AddComponent<Image>().color = c;
        var rt = NewRect("T", root.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, 0), new Vector2(10, 2));
        rt.AddComponent<Image>().color = c;
        var rr = NewRect("R", root.transform, new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(0, 0), new Vector2(2, 8));
        rr.AddComponent<Image>().color = c;
        // 锁体
        var body = NewRect("Body", root.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -10), new Vector2(14, 10));
        body.AddComponent<Image>().color = c;
        return root.gameObject;
    }

    /// <summary>节点手势: 按住下滑→锁定, 上滑→解锁 (点击加点由Button处理)</summary>
    private class SystemNodeGesture : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        public System.Action onLockToggle;
        public System.Action onUnlockToggle;
        private bool _fired;
        private Vector2 _downPos;

        public void OnPointerDown(PointerEventData e)
        {
            _fired = false;
            _downPos = e.position;
        }

        public void OnDrag(PointerEventData e)
        {
            if (_fired) return;
            float dy = e.position.y - _downPos.y;
            if (dy < -60f) { _fired = true; onLockToggle?.Invoke(); }
            else if (dy > 60f) { _fired = true; onUnlockToggle?.Invoke(); }
        }

        public void OnPointerUp(PointerEventData e) { _fired = false; }
    }
}
