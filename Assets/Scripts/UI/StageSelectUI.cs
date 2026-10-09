using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class StageSelectUI : MonoBehaviour
{
    public static StageSelectUI Instance { get; private set; }

    private GameObject panel;
    private List<GameObject> stageButtons = new List<GameObject>();
    private List<Image> stageImages = new List<Image>();
    private float animTime;
    private Coroutine showRoutine;

    // 秘境无限爬层: 窗口化层卡片 (2列x6行, 翻页浏览)
    private int _windowStart = 1;       // 窗口第一个可见层(1-based)
    private const int VisibleRows = 12;
    private readonly int[] _cardLayers = new int[12];
    private Text _bestLayerText;

    // 暗青蓝配色

    // Stage definitions
    public static readonly (string name, string desc, int difficulty, int goldReward, Color theme)[] Stages =
    {
        ("幽暗森林", "哥布林与野狼出没", 1, 50, new Color(0.2f, 0.5f, 0.2f)),
        ("废弃矿洞", "骷髅与蝙蝠的巢穴", 2, 80, new Color(0.4f, 0.35f, 0.3f)),
        ("冰霜峡谷", "寒冰元素和雪怪", 3, 120, new Color(0.5f, 0.7f, 0.9f)),
        ("火焰神殿", "烈焰恶魔和火元素", 4, 180, new Color(0.8f, 0.3f, 0.1f)),
        ("亡灵墓穴", "亡灵领主的领地", 5, 250, new Color(0.3f, 0.2f, 0.4f)),
        ("龙巢深处", "远古巨龙的巢穴", 6, 400, new Color(0.7f, 0.5f, 0.1f)),
        ("混沌虚空", "来自虚空的怪物", 7, 600, new Color(0.5f, 0.1f, 0.6f)),
        ("魔王殿堂", "最终BOSS", 8, 1000, new Color(0.8f, 0f, 0f))
    };

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Update()
    {
        animTime += Time.deltaTime;

        // 可挑战层呼吸 + 锁定层红字脉冲
        int cleared = RuntimePlayerData.Instance?.HighestStage ?? -1;
        for (int i = 0; i < stageButtons.Count && i < VisibleRows; i++)
        {
            int layerIndex = _cardLayers[i] - 1;
            var img = stageImages[i];
            if (img == null) continue;

            if (layerIndex == cleared + 1)
            {
                float pulse = 0.3f + Mathf.Sin(animTime * 3f) * 0.15f;
                var t = Stages[GameManager.RiftLayerToTheme(_cardLayers[i] - 1)].theme;
                img.color = new Color(t.r * pulse, t.g * pulse, t.b * pulse, 0.95f);
            }
            else if (layerIndex > cleared + 1)
            {
                var lockLabel = FindChildByName(stageButtons[i], "LockIcon");
                if (lockLabel != null)
                {
                    var lockText = lockLabel.GetComponent<Text>();
                    if (lockText != null)
                    {
                        float lockPulse = 0.5f + Mathf.Sin(animTime * 2f) * 0.2f;
                        lockText.color = new Color(0.8f, 0.2f, 0.2f, lockPulse);
                    }
                }
            }
        }
    }

    public void Show()
    {
        if (panel == null) BuildUI();
        UIHelper.SetupTopBar("秘境挑战", () => { Hide(); UIHelper.EnsureUIManager(); UIManager.Instance.ShowHub(); });
        int challenge = (RuntimePlayerData.Instance?.HighestStage ?? -1) + 2; // 1-based可挑战层
        _windowStart = Mathf.Max(1, challenge - 3);
        UpdateLayerCards();
        panel.SetActive(true);
        if (showRoutine != null) StopCoroutine(showRoutine);
        showRoutine = StartCoroutine(UIHelper.FadeIn(panel, 0.2f));
        animTime = 0f;
    }

    public void Hide()
    {
        if (panel != null)
        {
            if (showRoutine != null) StopCoroutine(showRoutine);
            showRoutine = StartCoroutine(UIHelper.FadeOut(panel, 0.15f));
        }
    }

    private GameObject FindChildByName(GameObject parent, string name)
    {
        for (int c = 0; c < parent.transform.childCount; c++)
        {
            var child = parent.transform.GetChild(c);
            if (child.name == name) return child.gameObject;
        }
        return null;
    }

    private void BuildUI()
    {
        Font font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();

        panel = UiPrefabLoader.TryLoad("StageSelectPanel", canvas);
        if (panel == null)
        panel = new GameObject("StageSelectPanel");
        panel.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero; panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero; panelRect.offsetMax = Vector2.zero;
        Image bg = panel.AddComponent<Image>();
        bg.color = UIHelper.BgDark;
        bg.raycastTarget = true;

        // 秘境最高层数标题
        var bestObj = new GameObject("BestLayer");
        bestObj.transform.SetParent(panel.transform, false);
        var bestRect = bestObj.AddComponent<RectTransform>();
        bestRect.anchorMin = new Vector2(0.5f, 1); bestRect.anchorMax = new Vector2(0.5f, 1);
        bestRect.pivot = new Vector2(0.5f, 1); bestRect.anchoredPosition = new Vector2(0, -66);
        bestRect.sizeDelta = new Vector2(600, 32);
        _bestLayerText = bestObj.AddComponent<Text>();
        _bestLayerText.alignment = TextAnchor.MiddleCenter;
        _bestLayerText.fontSize = 20; _bestLayerText.color = UIHelper.Accent; _bestLayerText.font = font;

        // 层卡片窗口 — 2列x6行 (无限层数, 翻页浏览)
        float cardW = 270f, cardH = 86f;
        float xLeft = -175f, xRight = 175f;
        float ySpacing = 98f;
        float yStart = 130f;

        for (int i = 0; i < VisibleRows; i++)
        {
            int row = i / 2;
            int col = i % 2;
            float xPos = col == 0 ? xLeft : xRight;
            float yPos = yStart - row * ySpacing;

            GameObject btnObj = new GameObject("LayerCard_" + i);
            btnObj.transform.SetParent(panel.transform, false);
            RectTransform btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0.5f); btnRect.anchorMax = new Vector2(0.5f, 0.5f);
            btnRect.pivot = new Vector2(0.5f, 0.5f);
            btnRect.anchoredPosition = new Vector2(xPos, yPos);
            btnRect.sizeDelta = new Vector3(cardW, cardH);
            Image btnImg = btnObj.AddComponent<Image>();
            btnImg.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);

            // 层数 (左)
            GameObject numObj = new GameObject("Num");
            numObj.transform.SetParent(btnObj.transform, false);
            RectTransform numRect = numObj.AddComponent<RectTransform>();
            numRect.anchorMin = new Vector2(0, 0.5f); numRect.anchorMax = new Vector2(0.32f, 1);
            numRect.offsetMin = new Vector2(8, 0); numRect.offsetMax = Vector2.zero;
            Text numText = numObj.AddComponent<Text>();
            numText.alignment = TextAnchor.MiddleLeft;
            numText.fontSize = 20; numText.color = Color.white; numText.font = font;

            // 主题名 (右)
            GameObject nameObj = new GameObject("Name");
            nameObj.transform.SetParent(btnObj.transform, false);
            RectTransform nameRect = nameObj.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.34f, 0.5f); nameRect.anchorMax = new Vector2(1, 1);
            nameRect.offsetMin = Vector2.zero; nameRect.offsetMax = new Vector2(-6, 0);
            Text nameText = nameObj.AddComponent<Text>();
            nameText.alignment = TextAnchor.MiddleRight;
            nameText.fontSize = 17; nameText.color = Color.white; nameText.font = font;

            // 状态行 (通关/可挑战/未解锁 + 奖励)
            GameObject statusObj = new GameObject("Status");
            statusObj.transform.SetParent(btnObj.transform, false);
            RectTransform statusRect = statusObj.AddComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0, 0.06f); statusRect.anchorMax = new Vector2(1, 0.48f);
            statusRect.offsetMin = new Vector2(8, 0); statusRect.offsetMax = new Vector2(-6, 0);
            Text statusText = statusObj.AddComponent<Text>();
            statusText.alignment = TextAnchor.MiddleLeft;
            statusText.fontSize = 14; statusText.color = Color.white; statusText.font = font;

            // Boss层标记 (右上角)
            GameObject bossObj = new GameObject("BossBadge");
            bossObj.transform.SetParent(btnObj.transform, false);
            RectTransform bossRect = bossObj.AddComponent<RectTransform>();
            bossRect.anchorMin = new Vector2(1, 1); bossRect.anchorMax = new Vector2(1, 1);
            bossRect.pivot = new Vector2(1, 1); bossRect.anchoredPosition = new Vector2(-4, -4);
            bossRect.sizeDelta = new Vector2(58, 20);
            Text bossText = bossObj.AddComponent<Text>();
            bossText.text = "BOSS层"; bossText.alignment = TextAnchor.MiddleCenter;
            bossText.fontSize = 12; bossText.color = new Color(1f, 0.35f, 0.2f); bossText.font = font;
            bossObj.SetActive(false);

            // 锁定遮罩
            GameObject lockObj = new GameObject("LockIcon");
            lockObj.transform.SetParent(btnObj.transform, false);
            RectTransform lockRect = lockObj.AddComponent<RectTransform>();
            lockRect.anchorMin = Vector2.zero; lockRect.anchorMax = Vector2.one;
            lockRect.offsetMin = Vector2.zero; lockRect.offsetMax = Vector2.zero;
            Text lockText = lockObj.AddComponent<Text>();
            lockText.text = "未解锁"; lockText.alignment = TextAnchor.MiddleCenter;
            lockText.fontSize = 20; lockText.color = new Color(0.8f, 0.2f, 0.2f, 0.6f);
            lockText.font = font;
            lockObj.SetActive(false);

            Button btn = btnObj.AddComponent<Button>();
            int cardIdx = i;
            btn.onClick.AddListener(() => OnCardClicked(cardIdx));
            stageButtons.Add(btnObj);
            stageImages.Add(btnImg);
        }

        // 翻页按钮
        UIHelper.MakeButton(panel.transform, "PrevBtn", "◀ 上一页", font,
            new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(-280, 18), new Vector2(-80, 66),
            new Color(0.15f, 0.25f, 0.5f, 0.9f), 18, () => PageShift(-VisibleRows));
        UIHelper.MakeButton(panel.transform, "NextBtn", "下一页 ▶", font,
            new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(80, 18), new Vector2(280, 66),
            new Color(0.15f, 0.25f, 0.5f, 0.9f), 18, () => PageShift(VisibleRows));

        panel.SetActive(false);
    }

    private void UpdateLayerCards()
    {
        int cleared = RuntimePlayerData.Instance?.HighestStage ?? -1; // 0-based已通关最高层
        if (_bestLayerText != null)
            _bestLayerText.text = cleared >= 0 ? $"最高通关层数: {cleared + 1}" : "尚未通关 — 从第1层开始挑战";

        for (int i = 0; i < stageButtons.Count && i < VisibleRows; i++)
        {
            int layer = _windowStart + i;
            _cardLayers[i] = layer;
            int layerIndex = layer - 1;
            var theme = Stages[GameManager.RiftLayerToTheme(layer - 1)];

            var numText = FindChildByName(stageButtons[i], "Num")?.GetComponent<Text>();
            var nameText = FindChildByName(stageButtons[i], "Name")?.GetComponent<Text>();
            var statusText = FindChildByName(stageButtons[i], "Status")?.GetComponent<Text>();
            var bossBadge = FindChildByName(stageButtons[i], "BossBadge");
            var lockObj = FindChildByName(stageButtons[i], "LockIcon");
            var img = i < stageImages.Count ? stageImages[i] : null;
            var btn = stageButtons[i] != null ? stageButtons[i].GetComponent<Button>() : null;

            if (numText != null) { numText.text = $"第{layer}层"; numText.color = theme.theme; }
            if (nameText != null) nameText.text = theme.name;
            if (bossBadge != null) bossBadge.SetActive(layer % 10 == 0);

            string goldText = $"奖励{GameManager.GetLayerGoldReward(layer)}金";
            if (statusText != null)
            {
                if (layerIndex <= cleared) { statusText.text = $"已通关 · {goldText}"; statusText.color = new Color(0.2f, 1f, 0.3f); }
                else if (layerIndex == cleared + 1) { statusText.text = $"可挑战 · {goldText}"; statusText.color = new Color(1f, 0.9f, 0.2f); }
                else { statusText.text = "未解锁"; statusText.color = new Color(0.6f, 0.6f, 0.65f, 0.8f); }
            }
            if (lockObj != null) lockObj.SetActive(layerIndex > cleared + 1);
            if (btn != null) btn.interactable = layerIndex <= cleared + 1;
            if (img != null)
            {
                if (layerIndex == cleared + 1)
                    img.color = new Color(0.2f, 0.2f, 0.2f, 0.9f); // 呼吸色由Update驱动
                else if (layerIndex <= cleared)
                    img.color = new Color(theme.theme.r * 0.3f, theme.theme.g * 0.3f, theme.theme.b * 0.3f, 0.9f);
                else
                    img.color = new Color(0.1f, 0.1f, 0.1f, 0.7f);
            }
        }
    }

    private void PageShift(int delta)
    {
        int challenge = (RuntimePlayerData.Instance?.HighestStage ?? -1) + 2;
        int maxStart = Mathf.Max(1, challenge);
        _windowStart = Mathf.Clamp(_windowStart + delta, 1, maxStart);
        UpdateLayerCards();
    }

    /// <summary>秘境层显示名 — 主题按8层循环</summary>
    public static string GetLayerName(int layer1Based)
    {
        var t = Stages[GameManager.RiftLayerToTheme(layer1Based - 1)];
        return $"第{layer1Based}层·{t.name}";
    }

    private void OnCardClicked(int cardIdx)
    {
        if (cardIdx < 0 || cardIdx >= VisibleRows) return;
        OnStageSelected(_cardLayers[cardIdx] - 1); // 0-based层索引
    }

    private void OnStageSelected(int stageIndex)
    {
        // Don't allow selecting locked stages (秘境层数: 首通解锁下一层)
        int cleared = RuntimePlayerData.Instance?.HighestStage ?? -1;
        if (stageIndex > cleared + 1) return;

        // 弹出选择：单人 / 组队
        StartCoroutine(ShowModeSelectPopup(stageIndex));
    }

    /// <summary>模式选择弹窗：单人开始 / 组队大厅</summary>
    private IEnumerator ShowModeSelectPopup(int stageIndex)
    {
        Font font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();

        var overlay = new GameObject("ModeSelectPopup");
        overlay.transform.SetParent(canvas.transform, false);
        var oR = overlay.AddComponent<RectTransform>();
        oR.anchorMin = Vector2.zero; oR.anchorMax = Vector2.one; oR.offsetMin = Vector2.zero; oR.offsetMax = Vector2.zero;
        overlay.AddComponent<Image>().color = new Color(0, 0, 0, 0.7f);
        var oCG = overlay.AddComponent<CanvasGroup>();

        var box = UIHelper.MakeGlowCard(overlay.transform, "Box",
            new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.75f), Vector2.zero, Vector2.zero,
            new Color(0.06f, 0.05f, 0.10f, 0.98f), UIHelper.GlowTop, UIHelper.Accent);

        // 标题
        var titleObj = new GameObject("Title"); titleObj.transform.SetParent(box.transform, false);
        var tR = titleObj.AddComponent<RectTransform>(); tR.anchorMin = new Vector2(0, 0.75f); tR.anchorMax = new Vector2(1, 0.95f); tR.offsetMin = Vector2.zero; tR.offsetMax = Vector2.zero;
        var title = titleObj.AddComponent<Text>();
        title.text = GetLayerName(stageIndex + 1); title.alignment = TextAnchor.MiddleCenter; title.fontSize = 22; title.color = UIHelper.Accent; title.font = font;

        // 关闭
        UIHelper.MakeButton(box.transform, "CloseBtn", "✕", font,
            new Vector2(0.85f, 0.80f), new Vector2(0.98f, 0.98f), Vector2.zero, Vector2.zero,
            UIHelper.BtnDanger, 16, () => Destroy(overlay));

        // 单人按钮
        UIHelper.MakeButton(box.transform, "SoloBtn", "单人开始", font,
            new Vector2(0.1f, 0.40f), new Vector2(0.9f, 0.60f), Vector2.zero, Vector2.zero,
            UIHelper.BtnConfirm, 20, () =>
            {
                Destroy(overlay);
                Hide();
                GameManager.Instance.IsTeamMode = false;
                GameManager.Instance.StartDungeon(stageIndex);
            });

        // 组队按钮
        UIHelper.MakeButton(box.transform, "TeamBtn", "组队大厅", font,
            new Vector2(0.1f, 0.10f), new Vector2(0.9f, 0.30f), Vector2.zero, Vector2.zero,
            new Color(0.15f, 0.25f, 0.5f, 0.9f), 20, () =>
            {
                Destroy(overlay);
                if (TeamLobbyManager.Instance == null) GameManager.Instance.gameObject.AddComponent<TeamLobbyManager>();
                TeamLobbyManager.Instance.ShowLobby(Stages[stageIndex].name, () =>
                {
                    Hide();
                    if (CloudSaveManager.HasInstance && CloudSaveManager.Instance.IsLoggedIn)
                    {
                        StageSelectUI.Instance?.StartCoroutine(StageSelectUI.Instance.RequestTeamDungeon(stageIndex));
                    }
                    else
                    {
                        GameManager.Instance.IsTeamMode = true;
                        GameManager.Instance.StartDungeon(stageIndex);
                    }
                });
            });

        float t = 0;
        while (t < 0.2f) { t += Time.unscaledDeltaTime; oCG.alpha = Mathf.Clamp01(t / 0.2f); yield return null; }
        oCG.alpha = 1f;
        yield return null;
    }

    public bool IsVisible() => panel != null && panel.activeSelf;

    /// <summary>请求Web API创建房间并连接Game Server</summary>
    public IEnumerator RequestTeamDungeon(int stageIndex)
    {
        string token = CloudSaveManager.Instance.Token;
        string url = $"{CloudSaveManager.Instance.ServerUrl}/api/team/start?dungeonId={stageIndex}";

        using (var req = new UnityEngine.Networking.UnityWebRequest(url, "POST"))
        {
            req.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            req.SetRequestHeader("Authorization", $"Bearer {token}");
            req.timeout = 10;
            yield return req.SendWebRequest();

            if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                var resp = JsonUtility.FromJson<TeamStartResponse>(req.downloadHandler.text);
                if (resp.success && resp.port > 0)
                {
                                GameLog.Log($"[Team] 房间创建成功: roomId={resp.roomId}, port={resp.port}");
                    // 连接Game Server
                    if (ClientNetworkManager.Instance != null)
                        ClientNetworkManager.Instance.Connect(resp.roomId, resp.port);
                }
                else if (resp.fallback)
                {
                                GameLog.Log("[Team] Game Server不可用, 使用AI队友模式");
                }
            }
            else
            {
                GameLog.LogWarning($"[Team] 创建房间失败: {req.error}, 使用AI队友模式");
            }
        }

        // 无论是否连接Game Server, 都进入副本(AI队友模式作为fallback)
        GameManager.Instance.IsTeamMode = true;
        GameManager.Instance.StartDungeon(stageIndex);
    }

    [System.Serializable]
    public class TeamStartResponse
    {
        public bool success;
        public bool fallback;
        public string roomId;
        public int port;
        public string message;
    }

    /// <summary>清空Transform的所有子物体</summary>
    private void ClearChildren(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--)
            Destroy(t.GetChild(i).gameObject);
    }

    private void OnDestroy()
    {
        if (panel != null) Destroy(panel);
        if (Instance == this) Instance = null;
    }
}
