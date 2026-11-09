using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 职业属性面板 (静态弹层) — 职业选择页与性别+起名页共用:
/// 点击中央立绘弹出; 属性面板底框 + 六边形雷达(功/防/体/智/精/耐) + 职业/武器/护甲/特征 + 关闭
/// 以 parent 下名为 "ClassStatsPopup" 的子节点复用, 关闭仅 SetActive(false)
/// </summary>
public static class ClassStatsPopup
{
    private static readonly HeroClass[] ClassOrder = { HeroClass.Warrior, HeroClass.Mage, HeroClass.Priest };
    public static readonly string[] FullNames = { "战士", "法师", "牧师" };
    private static readonly int[][] HexStats =   // 功/防/体/智/精/耐 (0-10)
    {
        new[] { 8, 8, 9, 3, 3, 7 },  // 勇士
        new[] { 9, 4, 4, 9, 7, 4 },  // 方士
        new[] { 5, 5, 6, 7, 9, 6 },  // 祝由
    };
    private static readonly string[] Weapons = { "剑刃", "法杖", "权杖" };
    private static readonly string[] Armors = { "重甲", "布甲", "法袍" };
    private static readonly string[] Traits = { "高防近战·稳定输出", "远程爆发·范围伤害", "治疗辅助·持久续航" };
    private static readonly string[] StatAxis = { "功", "防", "体", "智", "精", "耐" };

    public static int IdxOf(HeroClass heroClass)
    {
        for (int i = 0; i < ClassOrder.Length; i++)
            if (ClassOrder[i] == heroClass) return i;
        return 0;
    }

    /// <summary>在 parent 下弹出(或刷新)职业属性面板覆盖层</summary>
    public static void Show(Transform parent, HeroClass heroClass)
    {
        var existing = parent.Find("ClassStatsPopup");
        if (existing != null)
        {
            existing.SetAsLastSibling();
            existing.gameObject.SetActive(true);
            Refresh(existing, heroClass);
            return;
        }

        Font font = GameManager.GetUIFont();

        var popObj = new GameObject("ClassStatsPopup");
        popObj.transform.SetParent(parent, false);
        var popRect = popObj.AddComponent<RectTransform>();
        popRect.anchorMin = Vector2.zero; popRect.anchorMax = Vector2.one;
        popRect.offsetMin = Vector2.zero; popRect.offsetMax = Vector2.zero;
        var dimImg = popObj.AddComponent<Image>();
        dimImg.color = new Color(0, 0, 0, 0.55f);

        // 面板底框 (设计图素材)
        var card = new GameObject("StatsCard");
        card.transform.SetParent(popObj.transform, false);
        var cardRect = card.AddComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f); cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = new Vector2(0, 30);
        cardRect.sizeDelta = new Vector2(920, 540);
        var cardImg = card.AddComponent<Image>();
        cardImg.sprite = Resources.Load<Sprite>(ResourcePaths.StatsPanelBg);
        cardImg.type = Image.Type.Sliced;
        cardImg.raycastTarget = true;

        // 标题
        var titleObj = new GameObject("Title");
        titleObj.transform.SetParent(card.transform, false);
        var tRect = titleObj.AddComponent<RectTransform>();
        tRect.anchorMin = new Vector2(0.5f, 1); tRect.anchorMax = new Vector2(0.5f, 1);
        tRect.pivot = new Vector2(0.5f, 1); tRect.anchoredPosition = new Vector2(0, -14);
        tRect.sizeDelta = new Vector2(400, 50);
        var titleText = titleObj.AddComponent<Text>();
        titleText.text = "属性面板"; titleText.alignment = TextAnchor.MiddleCenter;
        titleText.fontSize = 32; titleText.color = Color.white; titleText.font = font;
        titleText.raycastTarget = false;

        // 关闭按钮 (设计图素材, 自带X)
        var closeObj = new GameObject("CloseBtn");
        closeObj.transform.SetParent(card.transform, false);
        var cRect = closeObj.AddComponent<RectTransform>();
        cRect.anchorMin = new Vector2(1, 1); cRect.anchorMax = new Vector2(1, 1);
        cRect.pivot = new Vector2(1, 1);
        cRect.anchoredPosition = new Vector2(-18, -18);
        cRect.sizeDelta = new Vector2(64, 58);
        var cImg = closeObj.AddComponent<Image>();
        cImg.sprite = Resources.Load<Sprite>(ResourcePaths.CloseBtn);
        cImg.preserveAspect = true;
        var cBtn = closeObj.AddComponent<Button>();
        cBtn.targetGraphic = cImg;
        cBtn.onClick.AddListener(() => popObj.SetActive(false));

        // 六边形雷达 (动态Mesh): 背景网格 + 数据多边形 + 六轴数值标签
        var hexObj = new GameObject("HexStats");
        hexObj.transform.SetParent(card.transform, false);
        var hRect = hexObj.AddComponent<RectTransform>();
        hRect.anchorMin = new Vector2(0.5f, 0.5f); hRect.anchorMax = new Vector2(0.5f, 0.5f);
        hRect.pivot = new Vector2(0.5f, 0.5f);
        hRect.anchoredPosition = new Vector2(-215, -30);
        hRect.sizeDelta = new Vector2(375, 414);

        var gridGo = new GameObject("Grid");
        gridGo.transform.SetParent(hexObj.transform, false);
        var gRect = gridGo.AddComponent<RectTransform>();
        gRect.anchorMin = Vector2.zero; gRect.anchorMax = Vector2.one;
        gRect.offsetMin = Vector2.zero; gRect.offsetMax = Vector2.zero;
        gridGo.AddComponent<CanvasRenderer>();
        var gridG = gridGo.AddComponent<HexRadarGraphic>();
        gridG.gridMode = true;
        gridG.color = new Color(0.78f, 0.84f, 0.95f, 0.4f);
        gridG.raycastTarget = false;

        var radarGo = new GameObject("Radar");
        radarGo.transform.SetParent(hexObj.transform, false);
        var rRect = radarGo.AddComponent<RectTransform>();
        rRect.anchorMin = Vector2.zero; rRect.anchorMax = Vector2.one;
        rRect.offsetMin = Vector2.zero; rRect.offsetMax = Vector2.zero;
        radarGo.AddComponent<CanvasRenderer>();
        var radar = radarGo.AddComponent<HexRadarGraphic>();
        radar.color = new Color(0.98f, 0.86f, 0.5f, 0.95f);
        radar.raycastTarget = false;

        // 六轴数值: 功(顶) 防(右上) 体(右下) 智(底) 精(左下) 耐(左上)
        Vector2[] axisPos =
        {
            new Vector2(0f, 225f), new Vector2(210f, 112f), new Vector2(210f, -112f),
            new Vector2(0f, -225f), new Vector2(-210f, -112f), new Vector2(-210f, 112f),
        };
        for (int i = 0; i < 6; i++)
        {
            var axisObj = new GameObject("Axis_" + StatAxis[i]);
            axisObj.transform.SetParent(hexObj.transform, false);
            var aRect = axisObj.AddComponent<RectTransform>();
            aRect.anchorMin = new Vector2(0.5f, 0.5f); aRect.anchorMax = new Vector2(0.5f, 0.5f);
            aRect.pivot = new Vector2(0.5f, 0.5f);
            aRect.anchoredPosition = axisPos[i];
            aRect.sizeDelta = new Vector2(90, 34);
            var aText = axisObj.AddComponent<Text>();
            aText.alignment = TextAnchor.MiddleCenter;
            aText.fontSize = 22; aText.color = Color.white; aText.font = font;
            aText.raycastTarget = false;
        }

        // 右侧职业信息列表
        var infoObj = new GameObject("ClassInfo");
        infoObj.transform.SetParent(card.transform, false);
        var iRect = infoObj.AddComponent<RectTransform>();
        iRect.anchorMin = new Vector2(0.5f, 0.5f); iRect.anchorMax = new Vector2(0.5f, 0.5f);
        iRect.pivot = new Vector2(0, 0.5f);
        iRect.anchoredPosition = new Vector2(60, -10);
        iRect.sizeDelta = new Vector2(330, 260);
        var infoText = infoObj.AddComponent<Text>();
        infoText.alignment = TextAnchor.MiddleLeft;
        infoText.fontSize = 24; infoText.color = new Color(0.92f, 0.92f, 1f); infoText.font = font;
        infoText.lineSpacing = 1.5f; infoText.raycastTarget = false;

        Refresh(popObj.transform, heroClass);
    }

    /// <summary>按职业刷新六轴数值与右侧信息</summary>
    public static void Refresh(Transform popup, HeroClass heroClass)
    {
        int idx = IdxOf(heroClass);
        var stats = HexStats[idx];
        var card = popup.Find("StatsCard");
        if (card == null) return;

        var hexObj = card.Find("HexStats");
        if (hexObj != null)
        {
            for (int i = 0; i < 6; i++)
            {
                var aText = hexObj.Find("Axis_" + StatAxis[i])?.GetComponent<Text>();
                if (aText != null) aText.text = $"{StatAxis[i]} {stats[i]}";
            }
            var radarTr = hexObj.Find("Radar");
            var radar = radarTr != null ? radarTr.GetComponent<HexRadarGraphic>() : null;
            if (radar != null)
            {
                var norm = new float[6];
                for (int i = 0; i < 6; i++) norm[i] = stats[i] / 10f;
                radar.SetValues(norm);
            }
        }
        var infoText = card.Find("ClassInfo")?.GetComponent<Text>();
        if (infoText != null)
        {
            infoText.text = $"职业：{FullNames[idx]}\n武器：{Weapons[idx]}\n护甲：{Armors[idx]}\n特征：{Traits[idx]}";
        }
    }
}
