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

        // Animate: pulse the "NEW" badge and locked borders
        int cleared = RuntimePlayerData.Instance?.HighestStage ?? -1;
        for (int i = 0; i < stageButtons.Count && i < Stages.Length; i++)
        {
            // Pulse the next-unlocked stage border
            if (i == cleared + 1)
            {
                var img = stageImages[i];
                if (img != null)
                {
                    float pulse = 0.3f + Mathf.Sin(animTime * 3f) * 0.15f;
                    var t = Stages[i].theme;
                    img.color = new Color(t.r * pulse, t.g * pulse, t.b * pulse, 0.95f);
                }
            }

            // Pulse lock icons on locked stages
            if (i > cleared + 1)
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
        UIHelper.SetupTopBar("选择副本", () => { Hide(); UIHelper.EnsureUIManager(); UIManager.Instance.ShowHub(); });
        UpdateButtons();
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

        panel = new GameObject("StageSelectPanel");
        panel.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero; panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero; panelRect.offsetMax = Vector2.zero;
        Image bg = panel.AddComponent<Image>();
        bg.color = UIHelper.BgDark;
        bg.raycastTarget = true;

        // Stage grid — 蛇形路径排列 (居中, 导航栏下方)
        float cardW = 200f, cardH = 170f;
        float xLeft = -150f, xRight = 150f;
        float ySpacing = 190f;
        float yStart = 200f;

        for (int i = 0; i < Stages.Length; i++)
        {
            int idx = i;
            var stage = Stages[i];

            // 蛇形排列: row determines left/right
            int row = i / 2;
            bool goingRight = (row % 2 == 0);
            int col = goingRight ? (i % 2) : (1 - i % 2);
            float xPos = col == 0 ? xLeft : xRight;
            float yPos = yStart - row * ySpacing;

            GameObject btnObj = new GameObject("Stage_" + i);
            btnObj.transform.SetParent(panel.transform, false);
            RectTransform btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0.5f); btnRect.anchorMax = new Vector2(0.5f, 0.5f);
            btnRect.pivot = new Vector2(0.5f, 0.5f);
            btnRect.anchoredPosition = new Vector2(xPos, yPos);
            btnRect.sizeDelta = new Vector3(cardW, cardH);
            Image btnImg = btnObj.AddComponent<Image>();
            btnImg.color = new Color(stage.theme.r * 0.3f, stage.theme.g * 0.3f, stage.theme.b * 0.3f, 0.9f);

            // Stage number
            GameObject numObj = new GameObject("Num");
            numObj.transform.SetParent(btnObj.transform, false);
            RectTransform numRect = numObj.AddComponent<RectTransform>();
            numRect.anchorMin = new Vector2(0.5f, 1); numRect.anchorMax = new Vector2(0.5f, 1);
            numRect.pivot = new Vector2(0.5f, 1); numRect.anchoredPosition = new Vector2(0, -10);
            numRect.sizeDelta = new Vector2(200, 30);
            Text numText = numObj.AddComponent<Text>();
            numText.text = $"第{i + 1}层"; numText.alignment = TextAnchor.MiddleCenter;
            numText.fontSize = 18; numText.color = stage.theme; numText.font = font;

            // Stage name
            GameObject nameObj = new GameObject("Name");
            nameObj.transform.SetParent(btnObj.transform, false);
            RectTransform nameRect = nameObj.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0.45f); nameRect.anchorMax = new Vector2(1, 0.7f);
            nameRect.offsetMin = new Vector2(5, 0); nameRect.offsetMax = new Vector2(-5, 0);
            Text nameText = nameObj.AddComponent<Text>();
            nameText.text = stage.name; nameText.alignment = TextAnchor.MiddleCenter;
            nameText.fontSize = 20; nameText.color = Color.white; nameText.font = font;

            // Reward
            GameObject rewardObj = new GameObject("Reward");
            rewardObj.transform.SetParent(btnObj.transform, false);
            RectTransform rewardRect = rewardObj.AddComponent<RectTransform>();
            rewardRect.anchorMin = new Vector2(0, 0.25f); rewardRect.anchorMax = new Vector2(1, 0.45f);
            rewardRect.offsetMin = new Vector2(5, 0); rewardRect.offsetMax = new Vector2(-5, 0);
            Text rewardText = rewardObj.AddComponent<Text>();
            rewardText.text = $"奖励: {stage.goldReward}金"; rewardText.alignment = TextAnchor.MiddleCenter;
            rewardText.fontSize = 16; rewardText.color = UIHelper.Accent; rewardText.font = font;

            // Difficulty stars
            GameObject diffObj = new GameObject("Diff");
            diffObj.transform.SetParent(btnObj.transform, false);
            RectTransform diffRect = diffObj.AddComponent<RectTransform>();
            diffRect.anchorMin = new Vector2(0, 0.1f); diffRect.anchorMax = new Vector2(1, 0.25f);
            diffRect.offsetMin = new Vector2(5, 0); diffRect.offsetMax = new Vector2(-5, 0);
            Text diffText = diffObj.AddComponent<Text>();
            diffText.text = new string('★', stage.difficulty); diffText.alignment = TextAnchor.MiddleCenter;
            diffText.fontSize = 18; diffText.color = new Color(1f, 0.5f, 0.1f); diffText.font = font;

            // Status icon slot
            GameObject statusObj = new GameObject("StatusIcon");
            statusObj.transform.SetParent(btnObj.transform, false);
            RectTransform statusRect = statusObj.AddComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0.5f, 1); statusRect.anchorMax = new Vector2(0.5f, 1);
            statusRect.pivot = new Vector2(0.5f, 1); statusRect.anchoredPosition = new Vector2(65, -8);
            statusRect.sizeDelta = new Vector2(28, 28);
            Text statusText = statusObj.AddComponent<Text>();
            statusText.alignment = TextAnchor.MiddleCenter;
            statusText.fontSize = 20; statusText.font = font;

            // Lock icon slot
            GameObject lockObj = new GameObject("LockIcon");
            lockObj.transform.SetParent(btnObj.transform, false);
            RectTransform lockRect = lockObj.AddComponent<RectTransform>();
            lockRect.anchorMin = Vector2.zero; lockRect.anchorMax = Vector2.one;
            lockRect.offsetMin = Vector2.zero; lockRect.offsetMax = Vector2.zero;
            Text lockText = lockObj.AddComponent<Text>();
            lockText.alignment = TextAnchor.MiddleCenter;
            lockText.fontSize = 48; lockText.color = new Color(0.8f, 0.2f, 0.2f, 0.5f);
            lockText.font = font;
            lockText.gameObject.SetActive(false);

            // NEW badge slot
            GameObject newObj = new GameObject("NewBadge");
            newObj.transform.SetParent(btnObj.transform, false);
            RectTransform newRect = newObj.AddComponent<RectTransform>();
            newRect.anchorMin = new Vector2(1, 1); newRect.anchorMax = new Vector2(1, 1);
            newRect.pivot = new Vector2(1, 1); newRect.anchoredPosition = new Vector2(-5, -5);
            newRect.sizeDelta = new Vector2(40, 20);
            Text newText = newObj.AddComponent<Text>();
            newText.text = "NEW"; newText.alignment = TextAnchor.MiddleCenter;
            newText.fontSize = 12; newText.color = new Color(1f, 0.9f, 0.2f); newText.font = font;
            newText.gameObject.SetActive(false);

            Button btn = btnObj.AddComponent<Button>();
            btn.onClick.AddListener(() => OnStageSelected(idx));
            stageButtons.Add(btnObj);
            stageImages.Add(btnImg);
        }

        // 连接路径线
        for (int i = 0; i < Stages.Length - 1; i++)
        {
            int row = i / 2;
            bool goingRight = (row % 2 == 0);
            int col1 = goingRight ? (i % 2) : (1 - i % 2);
            float x1 = col1 == 0 ? xLeft : xRight;
            float y1 = yStart - row * ySpacing;

            int nextRow = (i + 1) / 2;
            bool nextGoingRight = (nextRow % 2 == 0);
            int col2 = nextGoingRight ? ((i + 1) % 2) : (1 - (i + 1) % 2);
            float x2 = col2 == 0 ? xLeft : xRight;
            float y2 = yStart - nextRow * ySpacing;

            // 水平线 (同行)
            if (row == nextRow)
            {
                float midX = (x1 + x2) / 2f;
                var line = new GameObject($"Path_{i}");
                line.transform.SetParent(panel.transform, false);
                var lr = line.AddComponent<RectTransform>();
                lr.anchorMin = new Vector2(0.5f, 0.5f); lr.anchorMax = new Vector2(0.5f, 0.5f);
                lr.pivot = new Vector2(0.5f, 0.5f);
                lr.anchoredPosition = new Vector2(midX, y1);
                lr.sizeDelta = new Vector2(Mathf.Abs(x2 - x1) - cardW, 4);
                var lineImg = line.AddComponent<Image>();
                lineImg.color = new Color(0.25f, 0.60f, 0.85f, 0.5f);
                lineImg.raycastTarget = false;
            }
            else
            {
                // 垂直弯曲线 (跨行)
                var line = new GameObject($"Path_{i}");
                line.transform.SetParent(panel.transform, false);
                var lr = line.AddComponent<RectTransform>();
                lr.anchorMin = new Vector2(0.5f, 0.5f); lr.anchorMax = new Vector2(0.5f, 0.5f);
                lr.pivot = new Vector2(0.5f, 0.5f);
                lr.anchoredPosition = new Vector2(x1, (y1 + y2) / 2f);
                lr.sizeDelta = new Vector2(4, Mathf.Abs(y2 - y1) - cardH);
                var lineImg = line.AddComponent<Image>();
                lineImg.color = new Color(0.25f, 0.60f, 0.85f, 0.5f);
                lineImg.raycastTarget = false;
            }
        }

        panel.SetActive(false);
    }

    private void UpdateButtons()
    {
        int cleared = RuntimePlayerData.Instance?.HighestStage ?? -1;
        for (int i = 0; i < stageButtons.Count && i < Stages.Length; i++)
        {
            var btn = stageButtons[i].GetComponent<Button>();
            var img = stageImages[i];
            var statusText = FindChildByName(stageButtons[i], "StatusIcon")?.GetComponent<Text>();
            var lockObj = FindChildByName(stageButtons[i], "LockIcon");
            var newObj = FindChildByName(stageButtons[i], "NewBadge");

            if (i <= cleared)
            {
                // Cleared stage — show theme color + checkmark
                btn.interactable = true;
                var t = Stages[i].theme;
                img.color = new Color(t.r * 0.3f, t.g * 0.3f, t.b * 0.3f, 0.9f);
                if (statusText != null) { statusText.text = ""; statusText.color = new Color(0.2f, 1f, 0.3f); }
                if (lockObj != null) lockObj.SetActive(false);
                if (newObj != null) newObj.SetActive(false);
            }
            else if (i == cleared + 1)
            {
                // Next available stage — dark with NEW badge
                btn.interactable = true;
                img.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
                if (statusText != null) { statusText.text = ""; statusText.color = Color.white; }
                if (lockObj != null) lockObj.SetActive(false);
                if (newObj != null) newObj.SetActive(true);
            }
            else
            {
                // Locked stage — very dark with lock icon
                btn.interactable = false;
                img.color = new Color(0.1f, 0.1f, 0.1f, 0.7f);
                if (statusText != null) { statusText.text = ""; statusText.color = Color.grey; }
                if (lockObj != null) { lockObj.SetActive(true); var lt = lockObj.GetComponent<Text>(); if (lt != null) lt.text = ""; }
                if (newObj != null) newObj.SetActive(false);
            }
        }
    }

    private void OnStageSelected(int stageIndex)
    {
        // Don't allow selecting locked stages
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
        title.text = $"{Stages[stageIndex].name}"; title.alignment = TextAnchor.MiddleCenter; title.fontSize = 22; title.color = UIHelper.Accent; title.font = font;

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
