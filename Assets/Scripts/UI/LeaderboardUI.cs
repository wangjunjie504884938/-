using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// 排行榜 — 三标签 + 滚动列表(前100) + 实时数据
/// </summary>
public class LeaderboardUI : MonoBehaviour
{
    public static LeaderboardUI Instance { get; private set; }

    private GameObject panel;
    private Transform listContainer;
    private Coroutine showRoutine;
    private Coroutine _loadRoutine;
    private readonly List<LeaderboardEntryData> entries = new();
    private int _tab = 0;
    private Text _loadingText;
    private readonly GameObject[] _tabObjs = new GameObject[3];
    private readonly Image[] _tabBgs = new Image[3];
    private readonly Text[] _tabLabels = new Text[3];
    private RectTransform _tabIndicator;
    private Coroutine _indicatorRoutine;

    private static string ApiBase => (CloudSaveManager.Instance?.ServerUrl ?? "http://localhost:5132") + "/api";

    private static readonly string[] ClassNames = { "战士", "法师", "牧师" };
    private static readonly string[] TabNames = { "实力排行", "竞技场", "∞ 无尽模式" };
    private static readonly Color[] TabColors =
    {
        new Color(0.8f, 0.5f, 0.2f, 1f),
        new Color(0.6f, 0.3f, 0.8f, 1f),
        new Color(0.3f, 0.5f, 0.9f, 1f),
    };

    [Serializable]
    public class LeaderboardEntryData
    {
        public string username = "";
        public int classType;
        public int wave;
        public int level;
        public int wins;
        public int totalGames;
        public int gearScore;
        public int defenseRating;
    }

    [Serializable]
    private class LeaderboardResponse { public List<LeaderboardEntryData> leaderboard; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Show()
    {
        if (panel != null && panel.activeSelf)
        {
            UIHelper.SetupTopBar("排行榜", () => { Hide(); UIHelper.EnsureUIManager(); UIManager.Instance.ShowHub(); });
            if (_loadRoutine != null) StopCoroutine(_loadRoutine);
            _loadRoutine = StartCoroutine(Co_LoadData());
            return;
        }
        if (panel != null) { Destroy(panel); panel = null; }
        BuildUI();
        UIHelper.SetupTopBar("排行榜", () => { Hide(); UIHelper.EnsureUIManager(); UIManager.Instance.ShowHub(); });
        panel.SetActive(true);
        if (showRoutine != null) StopCoroutine(showRoutine);
        showRoutine = StartCoroutine(UIHelper.FadeIn(panel, 0.2f));
        if (_loadRoutine != null) StopCoroutine(_loadRoutine);
        _loadRoutine = StartCoroutine(Co_LoadData());
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

        panel = new GameObject("LeaderboardPanel", typeof(RectTransform));
        panel.transform.SetParent(canvas.transform, false);
        var pr = panel.GetComponent<RectTransform>();
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one;
        pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;
        panel.AddComponent<CanvasGroup>();

        // 背景
        var bg = new GameObject("BG", typeof(RectTransform));
        bg.transform.SetParent(panel.transform, false);
        var bgr = bg.GetComponent<RectTransform>();
        bgr.anchorMin = Vector2.zero; bgr.anchorMax = Vector2.one;
        bgr.offsetMin = Vector2.zero; bgr.offsetMax = Vector2.zero;
        bg.AddComponent<Image>().color = UIHelper.BgDark;

        // 三标签 — 渐变背景 + 滑动指示器
        var tabRow = new GameObject("TabRow", typeof(RectTransform));
        tabRow.transform.SetParent(panel.transform, false);
        var tabR = tabRow.GetComponent<RectTransform>();
        tabR.anchorMin = new Vector2(0.05f, 1); tabR.anchorMax = new Vector2(0.95f, 1);
        tabR.pivot = new Vector2(0.5f, 1); tabR.anchoredPosition = new Vector2(0, -128); tabR.sizeDelta = new Vector2(0, 48);

        // Tab栏背景条
        var tabBg = new GameObject("TabBarBg", typeof(RectTransform));
        tabBg.transform.SetParent(tabRow.transform, false);
        var tbbR = tabBg.GetComponent<RectTransform>();
        tbbR.anchorMin = Vector2.zero; tbbR.anchorMax = Vector2.one;
        tbbR.offsetMin = Vector2.zero; tbbR.offsetMax = Vector2.zero;
        tabBg.AddComponent<Image>().color = new Color(0.03f, 0.02f, 0.05f, 0.7f);
        // 底部分隔线
        var tabSep = new GameObject("TabSeparator", typeof(RectTransform));
        tabSep.transform.SetParent(tabRow.transform, false);
        var tsR = tabSep.GetComponent<RectTransform>();
        tsR.anchorMin = new Vector2(0, 0); tsR.anchorMax = new Vector2(1, 0);
        tsR.pivot = new Vector2(0.5f, 0);
        tsR.sizeDelta = new Vector2(0, 1);
        tsR.anchoredPosition = Vector2.zero;
        tabSep.AddComponent<Image>().color = UIHelper.BorderSubtle;

        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            var btnObj = new GameObject($"Tab_{i}", typeof(RectTransform));
            btnObj.transform.SetParent(tabRow.transform, false);
            var br = btnObj.GetComponent<RectTransform>();
            float x1 = i * 0.3333f, x2 = (i + 1) * 0.3333f;
            br.anchorMin = new Vector2(x1, 0); br.anchorMax = new Vector2(x2, 1);
            br.offsetMin = new Vector2(2, 0); br.offsetMax = new Vector2(-2, 0);
            var tabImg = btnObj.AddComponent<Image>();
            _tabObjs[i] = btnObj;
            _tabBgs[i] = tabImg;

            var btn = btnObj.AddComponent<Button>();
            btn.transition = Selectable.Transition.None; // 自己处理颜色，不依赖Tuanjie ColorTint
            btn.onClick.AddListener(() => SwitchTab(idx));

            var lbl = new GameObject("Lbl", typeof(RectTransform));
            lbl.transform.SetParent(btnObj.transform, false);
            var lr = lbl.GetComponent<RectTransform>();
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
            lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;
            var t = lbl.AddComponent<Text>();
            t.text = TabNames[i];
            t.alignment = TextAnchor.MiddleCenter;
            t.fontSize = 17;
            t.font = font;
            t.raycastTarget = false;
            _tabLabels[i] = t;

            AddTabHover(btnObj, idx);
        }

        // 滑动指示器（发光下划线）
        var indicator = new GameObject("TabIndicator", typeof(RectTransform));
        indicator.transform.SetParent(tabRow.transform, false);
        _tabIndicator = indicator.GetComponent<RectTransform>();
        _tabIndicator.anchorMin = new Vector2(0, 0);
        _tabIndicator.anchorMax = new Vector2(0, 0);
        _tabIndicator.pivot = new Vector2(0.5f, 0.5f);
        _tabIndicator.sizeDelta = new Vector2(0, 3);
        var indImg = indicator.AddComponent<Image>();
        indImg.color = TabColors[_tab];
        indImg.raycastTarget = false;

        // 初始视觉状态
        UpdateTabVisuals(true);

        // 列表容器 — 简单背景，不用MakeGlowCard(避免子对象遮挡滚动)
        var listArea = new GameObject("ListArea", typeof(RectTransform));
        listArea.transform.SetParent(panel.transform, false);
        var laR = listArea.GetComponent<RectTransform>();
        laR.anchorMin = new Vector2(0.05f, 0.04f); laR.anchorMax = new Vector2(0.95f, 0.82f);
        laR.offsetMin = Vector2.zero; laR.offsetMax = Vector2.zero;
        var laImg = listArea.AddComponent<Image>();
        laImg.color = new Color(0.04f, 0.03f, 0.06f, 0.5f);

        var scrollObj = new GameObject("ScrollArea", typeof(RectTransform));
        scrollObj.transform.SetParent(listArea.transform, false);
        var sr = scrollObj.GetComponent<RectTransform>();
        sr.anchorMin = new Vector2(0.02f, 0.02f); sr.anchorMax = new Vector2(0.98f, 0.98f);
        sr.offsetMin = Vector2.zero; sr.offsetMax = Vector2.zero;
        var scrollRect = scrollObj.AddComponent<ScrollRect>();
        scrollRect.vertical = true; scrollRect.horizontal = false;
        scrollRect.scrollSensitivity = 20f;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.elasticity = 0.1f;

        var contentObj = new GameObject("Content", typeof(RectTransform));
        contentObj.transform.SetParent(scrollObj.transform, false);
        var cr = contentObj.GetComponent<RectTransform>();
        cr.anchorMin = new Vector2(0, 1); cr.anchorMax = new Vector2(1, 1);
        cr.pivot = new Vector2(0.5f, 1);
        var vlg = contentObj.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 4; vlg.padding = new RectOffset(8, 8, 8, 8);
        vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
        vlg.childControlHeight = true; vlg.childForceExpandHeight = false;
        var csf = contentObj.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.content = cr;
        listContainer = contentObj.transform;

        // 加载提示(单独放在列表区外面)
        var loadingObj = new GameObject("LoadingText", typeof(RectTransform));
        loadingObj.transform.SetParent(listArea.transform, false);
        var lr2 = loadingObj.GetComponent<RectTransform>();
        lr2.anchorMin = new Vector2(0, 0.4f); lr2.anchorMax = new Vector2(1, 0.6f);
        lr2.offsetMin = Vector2.zero; lr2.offsetMax = Vector2.zero;
        _loadingText = loadingObj.AddComponent<Text>();
        _loadingText.text = "加载中...";
        _loadingText.alignment = TextAnchor.MiddleCenter;
        _loadingText.fontSize = 22; _loadingText.color = UIHelper.TextDim; _loadingText.font = font;
        _loadingText.raycastTarget = false;

        panel.SetActive(false);
    }

    private void SwitchTab(int idx)
    {
        if (idx == _tab && panel != null && panel.activeSelf) return;
        _tab = idx;
        UpdateTabVisuals(false);
        if (_loadRoutine != null) StopCoroutine(_loadRoutine);
        _loadRoutine = StartCoroutine(Co_LoadData());
    }

    private void UpdateTabVisuals(bool instant)
    {
        for (int i = 0; i < 3; i++)
        {
            bool active = i == _tab;
            if (_tabBgs[i] != null)
            {
                if (active)
                {
                    Color dark = new Color(TabColors[i].r * 0.35f, TabColors[i].g * 0.35f, TabColors[i].b * 0.35f, 0.9f);
                    _tabBgs[i].sprite = UIHelper.CreateGradientTexture(64, 64, TabColors[i], dark);
                    _tabBgs[i].color = Color.white;
                }
                else
                {
                    _tabBgs[i].sprite = null;
                    _tabBgs[i].color = new Color(0.04f, 0.03f, 0.06f, 0.5f);
                }
            }
            if (_tabLabels[i] != null)
            {
                _tabLabels[i].color = active ? Color.white : UIHelper.TextDim;
                _tabLabels[i].fontSize = active ? 18 : 16;
            }
        }

        if (_tabIndicator == null) return;

        float parentWidth = ((RectTransform)_tabIndicator.parent).rect.width;
        if (parentWidth <= 0)
        {
            StartCoroutine(Co_DeferredIndicator());
            return;
        }

        float tabWidth = parentWidth / 3f;
        float targetX = (_tab + 0.5f) * tabWidth;
        float indicatorWidth = tabWidth * 0.65f;

        if (instant)
        {
            _tabIndicator.anchoredPosition = new Vector2(targetX, 2f);
            _tabIndicator.sizeDelta = new Vector2(indicatorWidth, 3);
            var indImg = _tabIndicator.GetComponent<Image>();
            if (indImg != null) indImg.color = TabColors[_tab];
        }
        else
        {
            if (_indicatorRoutine != null) StopCoroutine(_indicatorRoutine);
            _indicatorRoutine = StartCoroutine(Co_SlideIndicator(targetX, indicatorWidth));
        }
    }

    private IEnumerator Co_DeferredIndicator()
    {
        yield return null;
        if (_tabIndicator == null) yield break;
        float parentWidth = ((RectTransform)_tabIndicator.parent).rect.width;
        if (parentWidth <= 0) yield break;
        float tabWidth = parentWidth / 3f;
        float targetX = (_tab + 0.5f) * tabWidth;
        _tabIndicator.anchoredPosition = new Vector2(targetX, 2f);
        _tabIndicator.sizeDelta = new Vector2(tabWidth * 0.65f, 3);
        var indImg = _tabIndicator.GetComponent<Image>();
        if (indImg != null) indImg.color = TabColors[_tab];
    }

    private IEnumerator Co_SlideIndicator(float targetX, float targetWidth)
    {
        var indImg = _tabIndicator.GetComponent<Image>();
        Color startColor = indImg != null ? indImg.color : Color.white;
        Color endColor = TabColors[_tab];
        Vector2 startPos = _tabIndicator.anchoredPosition;
        float startWidth = _tabIndicator.sizeDelta.x;
        float t = 0f;
        float duration = 0.25f;
        while (t < duration && _tabIndicator != null)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duration);
            p = 1f - Mathf.Pow(1f - p, 3f); // ease-out cubic
            _tabIndicator.anchoredPosition = new Vector2(Mathf.Lerp(startPos.x, targetX, p), 2f);
            _tabIndicator.sizeDelta = new Vector2(Mathf.Lerp(startWidth, targetWidth, p), 3);
            if (indImg != null) indImg.color = Color.Lerp(startColor, endColor, p);
            yield return null;
        }
        if (_tabIndicator != null)
        {
            _tabIndicator.anchoredPosition = new Vector2(targetX, 2f);
            _tabIndicator.sizeDelta = new Vector2(targetWidth, 3);
            if (indImg != null) indImg.color = endColor;
        }
    }

    private void AddTabHover(GameObject tabObj, int idx)
    {
        var trigger = tabObj.GetComponent<EventTrigger>();
        if (trigger == null) trigger = tabObj.AddComponent<EventTrigger>();

        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ =>
        {
            if (_tab != idx && _tabBgs[idx] != null)
                _tabBgs[idx].color = new Color(0.08f, 0.05f, 0.12f, 0.75f);
        });
        trigger.triggers.Add(enter);

        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ =>
        {
            if (_tab != idx && _tabBgs[idx] != null)
                _tabBgs[idx].color = new Color(0.04f, 0.03f, 0.06f, 0.5f);
        });
        trigger.triggers.Add(exit);
    }

    private IEnumerator Co_LoadData()
    {
        if (_loadingText != null) { _loadingText.text = "加载中..."; _loadingText.gameObject.SetActive(true); }
        // 清空旧行
        for (int i = listContainer.childCount - 1; i >= 0; i--)
            Destroy(listContainer.GetChild(i).gameObject);
        yield return null;

        string token = CloudSaveManager.Instance?.Token ?? "";
        if (string.IsNullOrEmpty(token))
        {
            entries.Clear();
            GenerateMockData();
            PopulateRows();
            yield break;
        }

        using (var req = UnityWebRequest.Get($"{ApiBase}/leaderboard/top"))
        {
            req.SetRequestHeader("Authorization", $"Bearer {token}");
            req.timeout = 10;
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var resp = JsonUtility.FromJson<LeaderboardResponse>(req.downloadHandler.text);
                entries.Clear();
                if (resp?.leaderboard != null)
                    entries.AddRange(resp.leaderboard);
                if (_tab == 0) entries.Sort((a, b) => b.level.CompareTo(a.level));
                else entries.Sort((a, b) => b.wave.CompareTo(a.wave));
            }
            else
            {
                entries.Clear();
                GenerateMockData();
            }
            PopulateRows();
        }
    }

    private void PopulateRows()
    {
        if (_loadingText != null) _loadingText.gameObject.SetActive(false);
        // 清空旧行
        for (int i = listContainer.childCount - 1; i >= 0; i--)
            Destroy(listContainer.GetChild(i).gameObject);

        if (entries.Count == 0)
        {
            if (_loadingText != null) { _loadingText.text = "暂无排行数据"; _loadingText.gameObject.SetActive(true); }
            return;
        }

        Font font = GameManager.GetUIFont();
        string currentUsername = CloudSaveManager.Instance?.CurrentUsername ?? "";
        int maxShow = Mathf.Min(entries.Count, 100);

        // 职业颜色
        var classColors = new Color[]
        {
            new Color(0.85f, 0.55f, 0.25f, 1f),  // 战士 - 橙
            new Color(0.35f, 0.55f, 0.90f, 1f),  // 法师 - 蓝
            new Color(0.40f, 0.80f, 0.45f, 1f),  // 牧师 - 绿
        };

        for (int i = 0; i < maxShow; i++)
        {
            var entry = entries[i];
            int rank = i + 1;
            bool isMe = !string.IsNullOrEmpty(currentUsername) && entry.username == currentUsername;

            // 行容器
            var rowObj = new GameObject($"Row_{i}", typeof(RectTransform));
            rowObj.transform.SetParent(listContainer, false);
            var le = rowObj.AddComponent<LayoutElement>();
            le.preferredHeight = 50;

            var rowImg = rowObj.AddComponent<Image>();
            if (isMe) rowImg.color = new Color(0.10f, 0.16f, 0.08f, 0.95f);
            else if (rank == 1) rowImg.color = new Color(0.16f, 0.13f, 0.04f, 0.9f);
            else if (rank == 2) rowImg.color = new Color(0.10f, 0.10f, 0.13f, 0.9f);
            else if (rank == 3) rowImg.color = new Color(0.13f, 0.08f, 0.05f, 0.9f);
            else rowImg.color = new Color(0.06f, 0.04f, 0.09f, 0.85f);

            int clsIdx = Mathf.Clamp(entry.classType, 0, 2);

            // 左侧色条（排名指示）
            if (rank <= 3)
            {
                var barObj = new GameObject("RankBar", typeof(RectTransform));
                barObj.transform.SetParent(rowObj.transform, false);
                var barR = barObj.GetComponent<RectTransform>();
                barR.anchorMin = new Vector2(0, 0); barR.anchorMax = new Vector2(0, 1);
                barR.pivot = new Vector2(0, 0.5f);
                barR.sizeDelta = new Vector2(4, 0);
                barR.anchoredPosition = Vector2.zero;
                var barImg = barObj.AddComponent<Image>();
                barImg.color = rank switch
                {
                    1 => new Color(0.90f, 0.70f, 0.20f, 1f),
                    2 => new Color(0.60f, 0.62f, 0.65f, 1f),
                    _ => new Color(0.65f, 0.40f, 0.20f, 1f),
                };
                barImg.raycastTarget = false;
            }

            // 排名 — 5%
            Color rankColor = rank switch
            {
                1 => new Color(0.95f, 0.80f, 0.30f, 1f),
                2 => new Color(0.75f, 0.78f, 0.82f, 1f),
                3 => new Color(0.80f, 0.55f, 0.30f, 1f),
                _ => new Color(0.55f, 0.50f, 0.60f, 1f),
            };
            MakeRowText(rowObj.transform, "Rank", $"{rank}", font, 16,
                new Vector2(0.02f, 0), new Vector2(0.10f, 1), rankColor, 22, TextAnchor.MiddleCenter);

            // 玩家名 — 33%
            Color nameColor = isMe ? new Color(0.95f, 1f, 0.85f, 1f) : new Color(0.93f, 0.91f, 0.96f, 1f);
            MakeRowText(rowObj.transform, "Name", entry.username, font, 17,
                new Vector2(0.12f, 0), new Vector2(0.42f, 1), nameColor, 17, TextAnchor.MiddleLeft);

            // 职业 — 15%
            MakeRowText(rowObj.transform, "Class", ClassNames[clsIdx], font, 15,
                new Vector2(0.42f, 0), new Vector2(0.54f, 1), classColors[clsIdx], 15, TextAnchor.MiddleLeft);

            // 属性数据 — 右侧
            string statText = _tab switch
            {
                0 => $"Lv.{entry.level}  {entry.wave}波",
                1 => $"{entry.wins}胜  {entry.gearScore}GS",
                _ => $"{entry.wave}波  Lv.{entry.level}",
            };
            Color statColor = isMe ? new Color(0.80f, 0.95f, 0.70f, 1f) : new Color(0.65f, 0.60f, 0.72f, 1f);
            MakeRowText(rowObj.transform, "Stats", statText, font, 15,
                new Vector2(0.54f, 0), new Vector2(0.96f, 1), statColor, 15, TextAnchor.MiddleRight);

            // YOU 标记
            if (isMe)
            {
                MakeRowText(rowObj.transform, "MeTag", "◀ YOU", font, 13,
                    new Vector2(0.88f, 0), new Vector2(0.99f, 1), new Color(0.60f, 0.90f, 0.50f, 1f), 13, TextAnchor.MiddleRight);
            }
        }
    }

    private static void MakeRowText(Transform parent, string name, string content, Font font, int size,
        Vector2 anchorMin, Vector2 anchorMax, Color color, int fontSize, TextAnchor alignment)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        var r = obj.GetComponent<RectTransform>();
        r.anchorMin = anchorMin; r.anchorMax = anchorMax;
        r.offsetMin = new Vector2(2, 0); r.offsetMax = new Vector2(-2, 0);
        var t = obj.AddComponent<Text>();
        t.text = content;
        t.font = font;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = alignment;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
    }

    private void GenerateMockData()
    {
        entries.Clear();
        var mockNames = new[] { "暗影刺客", "火焰法师", "圣光牧师", "狂战士王", "冰霜女王",
            "雷霆领主", "星辰使者", "深渊魔女", "钢铁堡垒", "月光骑士",
            "风暴之刃", "虚空法师", "神圣审判", "血色战狼", "龙血战士" };
        var rng = new System.Random(System.DateTime.Now.GetHashCode());
        for (int i = 0; i < mockNames.Length; i++)
        {
            entries.Add(new LeaderboardEntryData
            {
                username = mockNames[i],
                classType = rng.Next(0, 3),
                wave = rng.Next(5, 50),
                level = rng.Next(10, 30),
                wins = rng.Next(5, 80),
                gearScore = rng.Next(1000, 8000),
            });
        }
        if (_tab == 0) entries.Sort((a, b) => b.level.CompareTo(a.level));
        else entries.Sort((a, b) => b.wave.CompareTo(a.wave));
    }

    public static void SubmitScore(int wave, int classType, int level)
    {
        if (Instance == null) return;
        Instance.StartCoroutine(Instance.Co_SubmitScore(wave, classType, level));
    }

    private IEnumerator Co_SubmitScore(int wave, int classType, int level)
    {
        string token = CloudSaveManager.Instance?.Token ?? "";
        if (string.IsNullOrEmpty(token)) yield break;
        var body = new SubmitBody { wave = wave, classType = classType, level = level };
        string json = JsonUtility.ToJson(body);
        using (var req = new UnityWebRequest($"{ApiBase}/leaderboard/submit", "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", $"Bearer {token}");
            req.timeout = 10;
            yield return req.SendWebRequest();
        }
    }

    public static void SubmitArenaScore(bool won, int classType, int gearScore, int defenseRating)
    {
        if (Instance == null) return;
        Instance.StartCoroutine(Instance.Co_SubmitArena(won, classType, gearScore, defenseRating));
    }

    private IEnumerator Co_SubmitArena(bool won, int classType, int gearScore, int defenseRating)
    {
        string token = CloudSaveManager.Instance?.Token ?? "";
        if (string.IsNullOrEmpty(token)) yield break;
        var body = new SubmitArenaBody { won = won, classType = classType, gearScore = gearScore, defenseRating = defenseRating };
        string json = JsonUtility.ToJson(body);
        using (var req = new UnityWebRequest($"{ApiBase}/arena/submit", "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", $"Bearer {token}");
            req.timeout = 10;
            yield return req.SendWebRequest();
        }
    }

    [Serializable] private class SubmitBody { public int wave; public int classType; public int level; }
    [Serializable] private class SubmitArenaBody { public bool won; public int classType; public int gearScore; public int defenseRating; }

    private void OnDestroy() { if (panel != null) Destroy(panel); if (Instance == this) Instance = null; }
}
