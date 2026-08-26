using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 被动技能树面板 — 左侧滚动列表 + 右侧详情
/// </summary>
public class PassiveTreeUI : MonoBehaviour
{
    public static PassiveTreeUI Instance { get; private set; }

    private GameObject panel;
    private Coroutine showRoutine;
    private Text _detailText;
    private ScrollRect _scrollRect;
    private Text _pointsText;

    private static readonly Color NodeMinor = new Color(0.15f, 0.15f, 0.18f, 0.95f);
    private static readonly Color NodeNotable = new Color(0.20f, 0.12f, 0.25f, 0.95f);
    private static readonly Color NodeKeystone = new Color(0.25f, 0.08f, 0.08f, 0.95f);
    private static readonly Color NodeAllocated = new Color(0.15f, 0.30f, 0.15f, 0.95f);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Show()
    {
        if (panel != null) { Destroy(panel); panel = null; }
        BuildUI();
        UIHelper.SetupTopBar("被动天赋", () => { Hide(); UIHelper.EnsureUIManager(); UIManager.Instance.ShowHub(); });
        panel.SetActive(true);
        if (showRoutine != null) StopCoroutine(showRoutine);
        showRoutine = StartCoroutine(UIHelper.FadeIn(panel, 0.2f));
    }

    public void Hide()
    {
        if (panel != null)
        {
            if (showRoutine != null) StopCoroutine(showRoutine);
            showRoutine = StartCoroutine(UIHelper.FadeOut(panel, 0.15f));
        }
    }

    private void BuildUI()
    {
        Font font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();

        panel = new GameObject("PassiveTreePanel");
        panel.transform.SetParent(canvas.transform, false);
        RectTransform pr = panel.AddComponent<RectTransform>();
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one;
        pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;
        panel.AddComponent<CanvasGroup>();

        var bg = new GameObject("BG");
        bg.transform.SetParent(panel.transform, false);
        var bgr = bg.AddComponent<RectTransform>();
        bgr.anchorMin = Vector2.zero; bgr.anchorMax = Vector2.one;
        bgr.offsetMin = Vector2.zero; bgr.offsetMax = Vector2.zero;
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = UIHelper.BgDark;
        bgImg.raycastTarget = true;

        var player = GameManager.Instance?.Player;
        if (player == null) { panel.SetActive(false); return; }

        int points = player.Stats.SkillPoints;
        _pointsText = UIHelper.MakeSubtitle(panel.transform, $"可用技能点: {points}", font, 110, 16);

        // === 精确布局 ===
        float scrollTop = -236f, scrollH = 600f;
        float detailTop = -240f, detailH = 600f;
        float hintTop = -976f, hintH = 84f;

        // === 左侧: 滚动列表 ===
        var scrollObj = new GameObject("ScrollArea");
        scrollObj.transform.SetParent(panel.transform, false);
        var sr = scrollObj.AddComponent<RectTransform>();
        sr.anchorMin = new Vector2(0.04f, 1); sr.anchorMax = new Vector2(0.58f, 1);
        sr.pivot = new Vector2(0.5f, 1);
        sr.offsetMin = new Vector2(-29, scrollTop - scrollH);
        sr.offsetMax = new Vector2(29, scrollTop);
        var scrollBg = scrollObj.AddComponent<Image>();
        scrollBg.color = new Color(0.04f, 0.03f, 0.06f, 0.5f);
        var scrollRect = scrollObj.AddComponent<ScrollRect>();
        scrollRect.vertical = true; scrollRect.horizontal = false;
        scrollRect.scrollSensitivity = 20f;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.elasticity = 0.1f;
        _scrollRect = scrollRect;

        // Viewport — RectMask2D 裁剪
        var viewportObj = new GameObject("Viewport", typeof(RectTransform));
        viewportObj.transform.SetParent(scrollObj.transform, false);
        var vpR = viewportObj.GetComponent<RectTransform>();
        vpR.anchorMin = Vector2.zero; vpR.anchorMax = Vector2.one;
        vpR.offsetMin = Vector2.zero; vpR.offsetMax = Vector2.zero;
        viewportObj.AddComponent<RectMask2D>();
        scrollRect.viewport = vpR;

        var contentObj = new GameObject("Content");
        contentObj.transform.SetParent(viewportObj.transform, false);
        var cr = contentObj.AddComponent<RectTransform>();
        cr.anchorMin = new Vector2(0, 1); cr.anchorMax = new Vector2(1, 1);
        cr.pivot = new Vector2(0.5f, 1);
        cr.offsetMin = Vector2.zero; cr.offsetMax = Vector2.zero;
        var vlg = contentObj.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 4; vlg.padding = new RectOffset(6, 6, 6, 6);
        vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
        vlg.childControlHeight = true; vlg.childForceExpandHeight = false;
        var csf = contentObj.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.content = cr;

        // === 右侧: 详情面板 ===
        var detailArea = new GameObject("DetailArea", typeof(RectTransform));
        detailArea.transform.SetParent(panel.transform, false);
        var daR = detailArea.GetComponent<RectTransform>();
        daR.anchorMin = new Vector2(0.60f, 1); daR.anchorMax = new Vector2(0.96f, 1);
        daR.pivot = new Vector2(0.5f, 1);
        daR.offsetMin = new Vector2(0, detailTop - detailH);
        daR.offsetMax = new Vector2(0, detailTop);
        var daImg = detailArea.AddComponent<Image>();
        daImg.color = new Color(0.10f, 0.07f, 0.15f, 1f);
        // 边框
        var daBorder = new GameObject("Border", typeof(RectTransform));
        daBorder.transform.SetParent(detailArea.transform, false);
        var dabR = daBorder.GetComponent<RectTransform>();
        dabR.anchorMin = Vector2.zero; dabR.anchorMax = Vector2.one;
        dabR.offsetMin = new Vector2(-1, -1); dabR.offsetMax = new Vector2(1, 1);
        var dabImg = daBorder.AddComponent<Image>();
        dabImg.color = new Color(0.45f, 0.25f, 0.60f, 0.9f);
        dabImg.raycastTarget = false;
        daBorder.transform.SetAsFirstSibling();
        // 顶部线
        var daLine = new GameObject("TopLine", typeof(RectTransform));
        daLine.transform.SetParent(detailArea.transform, false);
        var dalR = daLine.GetComponent<RectTransform>();
        dalR.anchorMin = new Vector2(0.1f, 1); dalR.anchorMax = new Vector2(0.9f, 1);
        dalR.pivot = new Vector2(0.5f, 1);
        dalR.sizeDelta = new Vector2(0, 2);
        var dalImg = daLine.AddComponent<Image>();
        dalImg.color = UIHelper.Accent;
        dalImg.raycastTarget = false;

        var detailTextObj = new GameObject("DetailText");
        detailTextObj.transform.SetParent(detailArea.transform, false);
        var dtr = detailTextObj.AddComponent<RectTransform>();
        dtr.anchorMin = new Vector2(0.06f, 0.04f); dtr.anchorMax = new Vector2(0.94f, 0.96f);
        dtr.offsetMin = Vector2.zero; dtr.offsetMax = Vector2.zero;
        _detailText = detailTextObj.AddComponent<Text>();
        _detailText.alignment = TextAnchor.UpperLeft;
        _detailText.fontSize = 18; _detailText.color = UIHelper.TextPrimary; _detailText.font = font;
        _detailText.supportRichText = true;
        _detailText.raycastTarget = false;
        _detailText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _detailText.verticalOverflow = VerticalWrapMode.Overflow;
        _detailText.text = "点击左侧节点查看详情";

        // === 节点列表 ===
        var allocated = new HashSet<int>(player.Stats.Progress.PassiveNodeIds);
        var nodes = PassiveTree.Nodes;

        foreach (var kv in nodes)
        {
            var node = kv.Value;
            bool isAllocated = allocated.Contains(node.Id);

            var nodeColor = isAllocated ? NodeAllocated : node.Type switch
            {
                PassiveNodeType.Keystone => NodeKeystone,
                PassiveNodeType.Notable => NodeNotable,
                _ => NodeMinor
            };

            var card = new GameObject($"Node_{node.Id}", typeof(RectTransform));
            card.transform.SetParent(contentObj.transform, false);
            var cardImg = card.AddComponent<Image>();
            cardImg.color = nodeColor;
            // 边框
            var border = new GameObject("Border", typeof(RectTransform));
            border.transform.SetParent(card.transform, false);
            var br = border.GetComponent<RectTransform>();
            br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one;
            br.offsetMin = new Vector2(-1, -1); br.offsetMax = new Vector2(1, 1);
            var bImg = border.AddComponent<Image>();
            bImg.color = isAllocated ? new Color(0.2f, 0.6f, 0.2f, 0.6f) : UIHelper.BorderSubtle;
            bImg.raycastTarget = false;
            border.transform.SetAsFirstSibling();
            // 底部线
            var glowLine = new GameObject("GlowLine", typeof(RectTransform));
            glowLine.transform.SetParent(card.transform, false);
            var glR = glowLine.GetComponent<RectTransform>();
            glR.anchorMin = new Vector2(0.1f, 0); glR.anchorMax = new Vector2(0.9f, 0);
            glR.pivot = new Vector2(0.5f, 0);
            glR.sizeDelta = new Vector2(0, 2);
            var glImg = glowLine.AddComponent<Image>();
            glImg.color = isAllocated ? new Color(0.3f, 0.8f, 0.3f, 0.8f) : new Color(0.3f, 0.15f, 0.45f, 0.5f);
            glImg.raycastTarget = false;

            var le = card.AddComponent<LayoutElement>();
            le.preferredHeight = 52;
            le.minWidth = 200;

            var btn = card.AddComponent<Button>();
            btn.interactable = true;
            int nodeId = node.Id;
            btn.onClick.AddListener(() => OnNodeClick(nodeId));

            // 节点类型图标
            var typeIcon = new GameObject("TypeIcon");
            typeIcon.transform.SetParent(card.transform, false);
            var tir = typeIcon.AddComponent<RectTransform>();
            tir.anchorMin = new Vector2(0, 0.5f); tir.anchorMax = new Vector2(0, 0.5f);
            tir.pivot = new Vector2(0.5f, 0.5f);
            tir.anchoredPosition = new Vector2(25, 0);
            tir.sizeDelta = new Vector2(30, 30);
            var typeTxt = typeIcon.AddComponent<Text>();
            typeTxt.text = node.Type switch
            {
                PassiveNodeType.Keystone => "★",
                PassiveNodeType.Notable => "◆",
                _ => "•"
            };
            typeTxt.alignment = TextAnchor.MiddleCenter;
            typeTxt.fontSize = 20;
            typeTxt.color = isAllocated ? new Color(0.5f, 1f, 0.5f) : UIHelper.Accent;
            typeTxt.font = font;
            typeTxt.raycastTarget = false;

            // 名称
            var nameObj = new GameObject("Name");
            nameObj.transform.SetParent(card.transform, false);
            var nr = nameObj.AddComponent<RectTransform>();
            nr.anchorMin = new Vector2(0.12f, 0); nr.anchorMax = new Vector2(1, 1);
            nr.offsetMin = Vector2.zero; nr.offsetMax = Vector2.zero;
            var nameTxt = nameObj.AddComponent<Text>();
            nameTxt.text = (isAllocated ? "✓ " : "") + node.Name;
            nameTxt.alignment = TextAnchor.MiddleLeft;
            nameTxt.fontSize = 17;
            nameTxt.color = isAllocated ? new Color(0.6f, 1f, 0.6f) : UIHelper.TextPrimary;
            nameTxt.font = font;
            nameTxt.raycastTarget = false;
        }

        // === 底部提示 ===
        var hintObj = new GameObject("Hint");
        hintObj.transform.SetParent(panel.transform, false);
        var hr2 = hintObj.AddComponent<RectTransform>();
        hr2.anchorMin = new Vector2(0.04f, 1); hr2.anchorMax = new Vector2(0.96f, 1);
        hr2.pivot = new Vector2(0.5f, 1);
        hr2.offsetMin = new Vector2(0, hintTop - hintH);
        hr2.offsetMax = new Vector2(0, hintTop);
        var hintBg = hintObj.AddComponent<Image>();
        hintBg.color = new Color(0.06f, 0.03f, 0.09f, 0.8f);
        var hintTxtObj = new GameObject("Txt", typeof(RectTransform));
        hintTxtObj.transform.SetParent(hintObj.transform, false);
        var htr = hintTxtObj.GetComponent<RectTransform>();
        htr.anchorMin = Vector2.zero; htr.anchorMax = Vector2.one;
        htr.offsetMin = Vector2.zero; htr.offsetMax = Vector2.zero;
        var hintTxt = hintTxtObj.AddComponent<Text>();
        hintTxt.text = "升级获得技能点 → 点击节点分配 → 相邻节点才能解锁";
        hintTxt.alignment = TextAnchor.MiddleCenter;
        hintTxt.fontSize = 14; hintTxt.color = UIHelper.TextSecondary; hintTxt.font = font;
        hintTxt.raycastTarget = false;

        panel.SetActive(false);
    }

    private void OnNodeClick(int nodeId)
    {
        var player = GameManager.Instance?.Player;
        if (player == null) return;

        var nodes = PassiveTree.Nodes;
        if (!nodes.TryGetValue(nodeId, out var node)) return;

        var allocated = new HashSet<int>(player.Stats.Progress.PassiveNodeIds);
        bool isAllocated = allocated.Contains(nodeId);

        string status = isAllocated ? "<color=#55FF55>已分配</color>" : "未分配";
        _detailText.text = $"<size=22><color=#{ColorUtility.ToHtmlStringRGBA(GetNodeTypeColor(node.Type))}>{node.Name}</color></size>\n\n" +
                           $"类型: {node.Type}\n状态: {status}\n\n" +
                           $"<color=#AAA>{node.Description}</color>\n\n" +
                           $"<color=#FFF>{node.GetEffectText()}</color>";

        if (isAllocated) return;

        bool canAllocate = PassiveTree.CanAllocate(nodeId, player.HeroClass, allocated);
        if (!canAllocate)
        {
            _detailText.text += "\n\n<color=#FF6644>需要先分配相邻节点</color>";
            return;
        }

        if (player.Stats.SkillPoints <= 0)
        {
            _detailText.text += "\n\n<color=#FF6644>技能点不足</color>";
            return;
        }

        // Allocate
        player.Stats.Progress.PassiveNodeIds.Add(nodeId);
        player.Stats.SpendSkillPoint();
        player.Stats.RecalculatePassiveTree();
        player.Stats.Save();

        // 局部刷新：不重建面板，保持滚动位置
        RefreshNodeVisual(nodeId);
        if (_pointsText != null)
            _pointsText.text = $"可用技能点: {player.Stats.SkillPoints}";
        // 更新详情面板
        _detailText.text = $"<size=22><color=#{ColorUtility.ToHtmlStringRGBA(GetNodeTypeColor(node.Type))}>{node.Name}</color></size>\n\n" +
                           $"类型: {node.Type}\n状态: <color=#55FF55>已分配</color>\n\n" +
                           $"<color=#AAA>{node.Description}</color>\n\n" +
                           $"<color=#FFF>{node.GetEffectText()}</color>";
    }

    private void RefreshNodeVisual(int nodeId)
    {
        if (_scrollRect == null || _scrollRect.content == null) return;
        string targetName = $"Node_{nodeId}";
        for (int i = 0; i < _scrollRect.content.childCount; i++)
        {
            var child = _scrollRect.content.GetChild(i);
            if (child.name != targetName) continue;

            var img = child.GetComponent<Image>();
            var nameTxt = child.Find("Name")?.GetComponent<Text>();
            var typeTxt = child.Find("TypeIcon")?.GetComponent<Text>();
            var border = child.Find("Border")?.GetComponent<Image>();
            var glow = child.Find("GlowLine")?.GetComponent<Image>();
            var le = child.GetComponent<LayoutElement>();

            if (img == null) continue;

            // 更新为已分配样式
            img.color = NodeAllocated;
            if (nameTxt != null)
            {
                nameTxt.text = "✓ " + nameTxt.text.Replace("✓ ", "");
                nameTxt.color = new Color(0.6f, 1f, 0.6f, 1f);
            }
            if (typeTxt != null)
                typeTxt.color = new Color(0.5f, 1f, 0.5f, 1f);
            if (border != null)
                border.color = new Color(0.2f, 0.6f, 0.2f, 0.6f);
            if (glow != null)
                glow.color = new Color(0.3f, 0.8f, 0.3f, 0.8f);
            break;
        }
    }

    private static Color GetNodeTypeColor(PassiveNodeType type) => type switch
    {
        PassiveNodeType.Keystone => new Color(0.8f, 0.3f, 0.3f),
        PassiveNodeType.Notable => new Color(0.6f, 0.4f, 0.8f),
        _ => new Color(0.5f, 0.5f, 0.55f)
    };

    private void OnDestroy()
    {
        if (panel != null) Destroy(panel);
        if (Instance == this) Instance = null;
    }
}
