using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// 每日任务 + 签到面板 — 暗青ARPG风格
/// 渐变卡片、发光边框、状态格子、悬停效果
/// </summary>
public partial class DailyTaskUI : MonoBehaviour
{
    public static DailyTaskUI Instance { get; private set; }

    private GameObject panel;
    private Coroutine showRoutine;
    private List<DailyTaskInfo> taskList = new();
    private SignInData signInData;
    private int _pendingClaimTaskId;
    private bool _serverProgressFailed; // 标记服务器不可用，后续只走本地

    // 配色 — 青蓝主色调(暗)

    // 渐变 Sprite 缓存键
    private static Sprite _signedGradient;
    private static Sprite _todayGradient;
    private static Sprite _futureGradient;

    private static string ApiBase => (CloudSaveManager.Instance?.ServerUrl ?? "http://localhost:5132") + "/api";

    private static readonly string[] TaskTypeNames = { "击杀敌人", "通关副本", "消耗金币", "强化装备", "获得经验" };
    private static readonly string[] TaskTypeIcons = { "", "", "", "", "" };

    [Serializable]
    public class DailyTaskInfo
    {
        public int id;
        public int taskType;
        public int targetValue;
        public int currentValue;
        public int rewardGold;
        public bool claimed;
    }

    [Serializable]
    public class TaskListResponse { public List<DailyTaskInfo> tasks; }

    [Serializable]
    public class SignInData
    {
        public int signedDays;
        public bool canSign;
        public int todayReward;
        public int[] rewards;
    }

    [Serializable]
    public class SignInResponse
    {
        public int signedDays;
        public bool canSign;
        public int todayReward;
        public int[] rewards;
    }

    [Serializable]
    public class SignInResultResponse
    {
        public bool success;
        public int rewardGold;
        public int signedDays;
    }

    [Serializable]
    public class ClaimResultResponse
    {
        public bool success;
        public int rewardGold;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Show()
    {
        if (panel == null) BuildUI();
        UIHelper.SetupTopBar("每日任务", onBack: HandleNavBack);
        panel.SetActive(true);
        if (showRoutine != null) StopCoroutine(showRoutine);
        showRoutine = StartCoroutine(UIHelper.FadeIn(panel, 0.2f));
        StartCoroutine(LoadData());
    }

    public void Hide()
    {
        if (panel != null)
        {
            if (showRoutine != null) StopCoroutine(showRoutine);
            showRoutine = StartCoroutine(UIHelper.FadeOut(panel, 0.15f));
        }
    }

    private static Sprite GetSignedGradient()
    {
        if (_signedGradient == null)
            _signedGradient = UIHelper.CreateGradientTexture(64, 64,
                new Color(0.06f, 0.14f, 0.09f, 0.92f),
                new Color(0.03f, 0.07f, 0.05f, 0.92f));
        return _signedGradient;
    }

    private static Sprite GetTodayGradient()
    {
        if (_todayGradient == null)
            _todayGradient = UIHelper.CreateGradientTexture(64, 64,
                new Color(0.05f, 0.10f, 0.15f, 0.92f),
                new Color(0.02f, 0.05f, 0.08f, 0.92f));
        return _todayGradient;
    }

    private static Sprite GetFutureGradient()
    {
        if (_futureGradient == null)
            _futureGradient = UIHelper.CreateGradientTexture(64, 64,
                UIHelper.GlowTop,
                UIHelper.GlowBottom);
        return _futureGradient;
    }

    private void BuildUI()
    {
        Font font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();

        panel = UiPrefabLoader.TryLoad("DailyTaskPanel", canvas);
        if (panel == null)
        panel = new GameObject("DailyTaskPanel");
        panel.transform.SetParent(canvas.transform, false);
        RectTransform pr = panel.AddComponent<RectTransform>();
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one;
        pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;
        panel.AddComponent<CanvasGroup>();

        // 暗色背景(无金色顶光)
        var bg = new GameObject("BG");
        bg.transform.SetParent(panel.transform, false);
        var bgr = bg.AddComponent<RectTransform>();
        bgr.anchorMin = Vector2.zero; bgr.anchorMax = Vector2.one;
        bgr.offsetMin = Vector2.zero; bgr.offsetMax = Vector2.zero;
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = UIHelper.BgDark;
        bgImg.raycastTarget = true;

        // === 签到区 === (扩大高度给按钮更多空间)
        var signInArea = UIHelper.MakeGlowCard(panel.transform, "SignInArea",
            new Vector2(0.06f, 1f), new Vector2(0.94f, 1f),
            new Vector2(0, -300), new Vector2(0, -110),
            UIHelper.GlowTop,
            UIHelper.GlowBottom,
            UIHelper.Accent);
        UIHelper.MakeSubtitle(signInArea.transform, "每日签到", font, 12, 20);

        // 7天签到格子 (上移, 给按钮留空间)
        float cellW = 115f, cellH = 85f, gap = 10f;
        float totalW = 7 * cellW + 6 * gap;
        float startX = -totalW / 2f;

        for (int i = 0; i < 7; i++)
        {
            float x = startX + i * (cellW + gap);
            var cell = new GameObject($"SignInDay_{i}");
            cell.transform.SetParent(signInArea.transform, false);
            var cr = cell.AddComponent<RectTransform>();
            cr.anchorMin = new Vector2(0.5f, 0.5f); cr.anchorMax = new Vector2(0.5f, 0.5f);
            cr.pivot = new Vector2(0.5f, 0.5f);
            cr.anchoredPosition = new Vector2(x, 10);
            cr.sizeDelta = new Vector2(cellW, cellH);
            var cellImg = cell.AddComponent<Image>();
            cellImg.sprite = GetFutureGradient();
            cellImg.type = Image.Type.Simple;

            // 格子边框
            var cellBorder = new GameObject("Border");
            cellBorder.transform.SetParent(cell.transform, false);
            var cbr = cellBorder.AddComponent<RectTransform>();
            cbr.anchorMin = Vector2.zero; cbr.anchorMax = Vector2.one;
            cbr.offsetMin = new Vector2(-1, -1); cbr.offsetMax = new Vector2(1, 1);
            var cbi = cellBorder.AddComponent<Image>();
            cbi.color = UIHelper.BorderSubtle;
            cbi.raycastTarget = false;
            cellBorder.transform.SetAsFirstSibling();

            // 天标签
            var dayLbl = new GameObject("Day");
            dayLbl.transform.SetParent(cell.transform, false);
            var dr = dayLbl.AddComponent<RectTransform>();
            dr.anchorMin = new Vector2(0, 0.72f); dr.anchorMax = new Vector2(1, 1);
            dr.offsetMin = new Vector2(4, 0); dr.offsetMax = new Vector2(-4, 0);
            var dayTxt = dayLbl.AddComponent<Text>();
            dayTxt.text = $"第{i + 1}天";
            dayTxt.alignment = TextAnchor.MiddleCenter;
            dayTxt.fontSize = 15; dayTxt.color = UIHelper.TextSecondary; dayTxt.font = font;

            // 奖励
            var rewardLbl = new GameObject("Reward");
            rewardLbl.transform.SetParent(cell.transform, false);
            var rr2 = rewardLbl.AddComponent<RectTransform>();
            rr2.anchorMin = new Vector2(0, 0.28f); rr2.anchorMax = new Vector2(1, 0.72f);
            rr2.offsetMin = Vector2.zero; rr2.offsetMax = Vector2.zero;
            var rewardTxt = rewardLbl.AddComponent<Text>();
            rewardTxt.text = "???"; rewardTxt.alignment = TextAnchor.MiddleCenter;
            rewardTxt.fontSize = 20; rewardTxt.color = UIHelper.Accent; rewardTxt.font = font;

            // 状态标记
            var statusLbl = new GameObject("Status");
            statusLbl.transform.SetParent(cell.transform, false);
            var sr2 = statusLbl.AddComponent<RectTransform>();
            sr2.anchorMin = new Vector2(0, 0); sr2.anchorMax = new Vector2(1, 0.28f);
            sr2.offsetMin = Vector2.zero; sr2.offsetMax = Vector2.zero;
            var statusTxt = statusLbl.AddComponent<Text>();
            statusTxt.text = ""; statusTxt.alignment = TextAnchor.MiddleCenter;
            statusTxt.fontSize = 14; statusTxt.color = Color.clear; statusTxt.font = font;
        }

        // 签到按钮 (签到区底部)
        var signBtn = UIHelper.MakeGlowCard(signInArea.transform, "SignInBtn",
            new Vector2(0.28f, 0f), new Vector2(0.72f, 0f),
            new Vector2(0, 4), new Vector2(0, 48),
            new Color(0.04f, 0.08f, 0.12f, 0.92f),
            new Color(0.02f, 0.04f, 0.06f, 0.92f),
            UIHelper.Accent);
        var signBtnComp = signBtn.AddComponent<Button>();
        signBtnComp.transition = Selectable.Transition.None;
        signBtnComp.onClick.AddListener(OnSignInButton);

        var signLbl = new GameObject("Lbl");
        signLbl.transform.SetParent(signBtn.transform, false);
        var sbr = signLbl.AddComponent<RectTransform>();
        sbr.anchorMin = Vector2.zero; sbr.anchorMax = Vector2.one;
        sbr.offsetMin = Vector2.zero; sbr.offsetMax = Vector2.zero;
        var signTxt = signLbl.AddComponent<Text>();
        signTxt.alignment = TextAnchor.MiddleCenter;
        signTxt.fontSize = 24; signTxt.color = UIHelper.TextPrimary; signTxt.font = font;
        signTxt.text = "签到";

        // === 任务区 === (扩大到填满底部)
        var taskArea = UIHelper.MakeGlowCard(panel.transform, "TaskArea",
            new Vector2(0.06f, 0f), new Vector2(0.94f, 0.65f),
            new Vector2(0, 15), new Vector2(0, 0),
            new Color(0.04f, 0.03f, 0.06f, 0.92f),
            UIHelper.GlowBottom,
            UIHelper.Accent);
        UIHelper.MakeSubtitle(taskArea.transform, "每日任务", font, 12, 20);

        // 任务卡片 (5个, 固定间距)
        float taskCardH = 72f;
        float taskGap = 8f;
        for (int i = 0; i < 5; i++)
        {
            float cardTop = -48 - i * (taskCardH + taskGap);
            float cardBottom = cardTop - taskCardH;
            var card = UIHelper.MakeGlowCard(taskArea.transform, $"TaskCard_{i}",
                new Vector2(0.04f, 1f), new Vector2(0.96f, 1f),
                new Vector2(0, cardBottom), new Vector2(0, cardTop),
                new Color(0.05f, 0.04f, 0.08f, 0.92f),
                UIHelper.GlowBottom,
                UIHelper.Accent);
            card.AddComponent<CardHoverEffect>();

            // 任务图标
            var iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(card.transform, false);
            var ir = iconObj.AddComponent<RectTransform>();
            ir.anchorMin = new Vector2(0, 0); ir.anchorMax = new Vector2(0, 1);
            ir.pivot = new Vector2(0, 0.5f);
            ir.anchoredPosition = new Vector2(15, 0);
            ir.sizeDelta = new Vector2(50, 0);
            var iconTxt = iconObj.AddComponent<Text>();
            iconTxt.alignment = TextAnchor.MiddleCenter; iconTxt.fontSize = 28;
            iconTxt.color = UIHelper.Accent; iconTxt.font = font;

            // 任务描述
            var descObj = new GameObject("Desc");
            descObj.transform.SetParent(card.transform, false);
            var dr2 = descObj.AddComponent<RectTransform>();
            dr2.anchorMin = new Vector2(0.1f, 0); dr2.anchorMax = new Vector2(0.6f, 1);
            dr2.offsetMin = Vector2.zero; dr2.offsetMax = Vector2.zero;
            var descTxt = descObj.AddComponent<Text>();
            descTxt.alignment = TextAnchor.MiddleLeft; descTxt.fontSize = 18;
            descTxt.color = UIHelper.TextPrimary; descTxt.font = font;

            // 奖励
            var rewardObj = new GameObject("Reward");
            rewardObj.transform.SetParent(card.transform, false);
            var rvr = rewardObj.AddComponent<RectTransform>();
            rvr.anchorMin = new Vector2(0.6f, 0); rvr.anchorMax = new Vector2(0.82f, 1);
            rvr.offsetMin = Vector2.zero; rvr.offsetMax = Vector2.zero;
            var rewardTxt = rewardObj.AddComponent<Text>();
            rewardTxt.alignment = TextAnchor.MiddleCenter; rewardTxt.fontSize = 18;
            rewardTxt.color = UIHelper.Accent; rewardTxt.font = font;

            // 领取按钮
            var claimBtnObj = new GameObject("ClaimBtn");
            claimBtnObj.transform.SetParent(card.transform, false);
            var cbr = claimBtnObj.AddComponent<RectTransform>();
            cbr.anchorMin = new Vector2(0.84f, 0.1f); cbr.anchorMax = new Vector2(0.98f, 0.9f);
            cbr.offsetMin = Vector2.zero; cbr.offsetMax = Vector2.zero;
            var claimImg = claimBtnObj.AddComponent<Image>();
            claimImg.color = UIHelper.BtnConfirm;
            var claimBtn = claimBtnObj.AddComponent<Button>();
            var claimLbl = new GameObject("Lbl");
            claimLbl.transform.SetParent(claimBtnObj.transform, false);
            var clr = claimLbl.AddComponent<RectTransform>();
            clr.anchorMin = Vector2.zero; clr.anchorMax = Vector2.one;
            clr.offsetMin = Vector2.zero; clr.offsetMax = Vector2.zero;
            var claimTxt = claimLbl.AddComponent<Text>();
            claimTxt.alignment = TextAnchor.MiddleCenter; claimTxt.fontSize = 16;
            claimTxt.color = UIHelper.TextPrimary; claimTxt.font = font;
        }

        panel.SetActive(false);
    }

    // ==== Server sync — moved to DailyTaskUI.Network.cs (partial class) ====

    private void RefreshUI()
    {
        if (panel == null) return;

        // 更新签到格子
        for (int i = 0; i < 7; i++)
        {
            var cell = panel.transform.Find("SignInArea/SignInDay_" + i);
            if (cell == null) continue;
            var rewardTxt = cell.Find("Reward")?.GetComponent<Text>();
            var statusTxt = cell.Find("Status")?.GetComponent<Text>();
            var cellImg = cell.GetComponent<Image>();
            var cellBorder = cell.Find("Border")?.GetComponent<Image>();

            if (signInData?.rewards != null && i < signInData.rewards.Length)
            {
                if (rewardTxt != null) rewardTxt.text = $"◆{signInData.rewards[i]}";
            }

            if (signInData != null && i < signInData.signedDays)
            {
                // 已签到
                cellImg.sprite = GetSignedGradient();
                if (rewardTxt != null) rewardTxt.color = new Color(0.8f, 1f, 0.7f, 1f);
                if (statusTxt != null) { statusTxt.text = "✓"; statusTxt.color = new Color(0.5f, 1f, 0.4f, 1f); }
                if (cellBorder != null) cellBorder.color = new Color(0.2f, 0.5f, 0.15f, 0.8f);
            }
            else if (signInData != null && i == signInData.signedDays && signInData.canSign)
            {
                // 今天可签
                cellImg.sprite = GetTodayGradient();
                if (rewardTxt != null) rewardTxt.color = UIHelper.Accent;
                if (statusTxt != null) { statusTxt.text = "● 今日"; statusTxt.color = UIHelper.Accent; }
                if (cellBorder != null) cellBorder.color = new Color(0.25f, 0.60f, 0.85f, 0.9f);
            }
            else
            {
                // 未来
                cellImg.sprite = GetFutureGradient();
                if (rewardTxt != null) rewardTxt.color = UIHelper.TextDim;
                if (statusTxt != null) { statusTxt.text = ""; statusTxt.color = Color.clear; }
                if (cellBorder != null) cellBorder.color = UIHelper.BorderSubtle;
            }
        }

        // 签到按钮
        var signBtn = panel.transform.Find("SignInArea/SignInBtn")?.GetComponent<Button>();
        if (signBtn != null)
        {
            var signLbl = signBtn.transform.Find("Lbl")?.GetComponent<Text>();
            if (signInData != null && !signInData.canSign)
            {
                signBtn.interactable = false;
                if (signLbl != null) signLbl.text = "今日已签到";
            }
            else
            {
                signBtn.interactable = true;
                if (signLbl != null) signLbl.text = $"签到 (◆{signInData?.todayReward ?? 0})";
            }
        }

        // 更新任务卡片
        for (int i = 0; i < 5; i++)
        {
            var card = panel.transform.Find($"TaskArea/TaskCard_{i}");
            if (card == null) continue;
            var iconTxt = card.Find("Icon")?.GetComponent<Text>();
            var descTxt = card.Find("Desc")?.GetComponent<Text>();
            var rewardTxt = card.Find("Reward")?.GetComponent<Text>();
            var claimBtn = card.Find("ClaimBtn")?.GetComponent<Button>();
            var claimLbl = claimBtn?.transform.Find("Lbl")?.GetComponent<Text>();
            var claimImg = claimBtn?.GetComponent<Image>();

            if (i < taskList.Count)
            {
                var task = taskList[i];
                card.gameObject.SetActive(true);
                int typeIdx = Mathf.Clamp(task.taskType, 0, TaskTypeNames.Length - 1);
                if (iconTxt != null) iconTxt.text = TaskTypeIcons[typeIdx];
                if (descTxt != null)
                    descTxt.text = $"{TaskTypeNames[typeIdx]}  ({task.currentValue}/{task.targetValue})";
                if (rewardTxt != null) rewardTxt.text = $"◆{task.rewardGold}";

                if (claimBtn != null)
                {
                    if (task.claimed)
                    {
                        claimBtn.interactable = false;
                        if (claimLbl != null) claimLbl.text = "已领取";
                        if (claimImg != null) claimImg.color = UIHelper.CardBg;
                    }
                    else if (task.currentValue >= task.targetValue)
                    {
                        claimBtn.interactable = true;
                        if (claimLbl != null) claimLbl.text = "领取";
                        if (claimImg != null) claimImg.color = UIHelper.BtnConfirm;
                        _pendingClaimTaskId = task.id;
                        claimBtn.onClick.RemoveAllListeners();
                        claimBtn.onClick.AddListener(HandleClaimClick);
                    }
                    else
                    {
                        claimBtn.interactable = false;
                        if (claimLbl != null) claimLbl.text = "未完成";
                        if (claimImg != null) claimImg.color = UIHelper.CardBg;
                    }
                }
            }
            else
            {
                card.gameObject.SetActive(false);
            }
        }
    }

    private void Update()
    {
        // 今天可签的格子脉动
        if (signInData != null && signInData.canSign && panel != null && panel.activeSelf)
        {
            var cell = panel.transform.Find($"SignInArea/SignInDay_{signInData.signedDays}");
            if (cell != null)
            {
                var border = cell.Find("Border")?.GetComponent<Image>();
                if (border != null)
                {
                    float pulse = Mathf.Sin(Time.unscaledTime * 3f) * 0.3f + 0.7f;
                    border.color = new Color(0.25f, 0.60f, 0.85f, pulse);
                }
            }

            // 签到按钮呼吸
            var signBtn = panel.transform.Find("SignInArea/SignInBtn");
            if (signBtn != null)
            {
                var glow = signBtn.Find("GlowLine")?.GetComponent<Image>();
                if (glow != null)
                {
                    float gp = Mathf.Sin(Time.unscaledTime * 2.5f) * 0.3f + 0.7f;
                    glow.color = new Color(0.25f, 0.60f, 0.85f, gp);
                }
            }
        }
    }

    private IEnumerator ClaimTask(int taskId)
    {
        // Try server first
        string token = GetToken();
        bool serverAvailable = !string.IsNullOrEmpty(token);

        if (serverAvailable)
        {
            using (var req = UnityWebRequest.PostWwwForm($"{ApiBase}/daily/claim?taskId={taskId}", ""))
            {
                req.SetRequestHeader("Authorization", $"Bearer {token}");
                req.timeout = 5;
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    var resp = JsonUtility.FromJson<ClaimResultResponse>(req.downloadHandler.text);
                    if (resp?.success == true)
                    {
                        var player = GameManager.Instance?.Player;
                        if (player != null) player.Stats.AddGold(resp.rewardGold);
                                    GameLog.Log($"[DailyTask] 领取奖励: {resp.rewardGold}金币");
                        yield return LoadData();
                        yield break;
                    }
                }
                else
                {
                    GameLog.LogWarning($"[DailyTask] 服务器领取失败: {req.error}, 使用本地领取");
                }
            }
        }

        // Local claim fallback
        if (taskId > 0 && taskId <= taskList.Count)
        {
            var task = taskList[taskId - 1];
            if (!task.claimed && task.currentValue >= task.targetValue)
            {
                var player = GameManager.Instance?.Player;
                if (player != null) player.Stats.AddGold(task.rewardGold);

                task.claimed = true;
                DailyTaskData.MarkTaskClaimed(task.taskType);

                RefreshUI();
                            GameLog.Log($"[DailyTask] 本地领取奖励: {task.rewardGold}金币");
            }
        }
    }

    /// <summary>签到按钮回调</summary>
    public void OnSignInButton()
    {
        StartCoroutine(DoSignIn());
    }

    private void HandleClaimClick()
    {
        StartCoroutine(ClaimTask(_pendingClaimTaskId));
    }

    private void HandleNavBack()
    {
        Hide();
        UIHelper.EnsureUIManager();
        UIManager.Instance.ShowHub();
    }

    private IEnumerator DoSignIn()
    {
        // Try server first
        string token = GetToken();
        bool serverAvailable = !string.IsNullOrEmpty(token);

        if (serverAvailable)
        {
            using (var req = UnityWebRequest.PostWwwForm($"{ApiBase}/signin/do", ""))
            {
                req.SetRequestHeader("Authorization", $"Bearer {token}");
                req.timeout = 5;
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    var resp = JsonUtility.FromJson<SignInResultResponse>(req.downloadHandler.text);
                    if (resp?.success == true)
                    {
                        var player = GameManager.Instance?.Player;
                        if (player != null) player.Stats.AddGold(resp.rewardGold);
                                    GameLog.Log($"[DailyTask] 签到成功: {resp.rewardGold}金币, 已签{resp.signedDays}天");
                        yield return LoadData();
                        yield break;
                    }
                }
                else
                {
                    GameLog.LogWarning($"[DailyTask] 服务器签到失败: {req.error}, 使用本地签到");
                }
            }
        }

        // Local sign-in fallback
        if (signInData != null && signInData.canSign)
        {
            int reward = signInData.todayReward;
            int newSignedDays = signInData.signedDays + 1;

            var player = GameManager.Instance?.Player;
            if (player != null) player.Stats.AddGold(reward);

            PlayerPrefs.SetString("ARPG_SignIn_LastDate", System.DateTime.Now.ToString("yyyyMMdd"));
            PlayerPrefs.SetInt("ARPG_SignIn_Days", newSignedDays);
            PlayerPrefs.Save();

            signInData.signedDays = newSignedDays;
            signInData.canSign = false;
            RefreshUI();
                        GameLog.Log($"[DailyTask] 本地签到成功: {reward}金币, 已签{newSignedDays}天");

            // 尝试异步同步到服务器 (不阻塞UI)
            if (!string.IsNullOrEmpty(token))
                StartCoroutine(SyncLocalSignInToServer(newSignedDays));
        }
    }

    /// <summary>上报任务进度 (由 GameManager 调用)</summary>
    public static void ReportProgress(int taskType, int amount)
    {
        if (Instance == null) return;
        Instance.StartCoroutine(Instance.Co_ReportProgress(taskType, amount));
    }

    private IEnumerator Co_ReportProgress(int taskType, int amount)
    {
        string token = GetToken();
        bool serverAvailable = !string.IsNullOrEmpty(token);

        // Always update local first (immediate, no wait)
        UpdateLocalProgress(taskType, amount);

        // Only attempt server upload if token exists AND we haven't recently failed
        if (serverAvailable && !_serverProgressFailed)
        {
            var body = new UpdateProgressBody { taskType = taskType, amount = amount };
            string json = JsonUtility.ToJson(body);

            using (var req = new UnityWebRequest($"{ApiBase}/daily/progress", "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {token}");
                req.timeout = 3;
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    if (panel != null && panel.activeSelf)
                        yield return LoadData();
                    yield break;
                }
                else
                {
                    GameLog.LogWarning($"[DailyTask] 服务器上报失败, 后续使用本地模式");
                    _serverProgressFailed = true; // Stop retrying — use local only for this session
                }
            }
        }

        if (panel != null && panel.activeSelf)
            RefreshUI();
    }

    private void UpdateLocalProgress(int taskType, int amount)
    {
        string dateKey = System.DateTime.Now.ToString("yyyyMMdd");
        string prefix = $"ARPG_Daily_{dateKey}_";

        for (int i = 0; i < taskList.Count; i++)
        {
            if (taskList[i].taskType == taskType)
            {
                taskList[i].currentValue = Mathf.Min(taskList[i].currentValue + amount, taskList[i].targetValue);
                DailyTaskData.SaveTaskProgress(taskList[i].taskType, taskList[i].currentValue);
                break;
            }
        }
    }

    /// <summary>异步同步本地签到到服务器 (不阻塞UI)</summary>
    private IEnumerator SyncLocalSignInToServer(int signedDays)
    {
        string token = GetToken();
        if (string.IsNullOrEmpty(token)) yield break;

        // 尝试服务器签到 — 如果服务器也允许签到, 就同步
        using (var req = UnityWebRequest.PostWwwForm($"{ApiBase}/signin/do", ""))
        {
            req.SetRequestHeader("Authorization", $"Bearer {token}");
            req.timeout = 3;
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var resp = JsonUtility.FromJson<SignInResultResponse>(req.downloadHandler.text);
                if (resp?.success == true)
                {
                    // 服务器签到成功 — 同步服务器返回的天数到本地
                    PlayerPrefs.SetInt("ARPG_SignIn_Days", resp.signedDays);
                    PlayerPrefs.Save();
                                GameLog.Log($"[DailyTask] 本地签到已同步到服务器: 服务器已签{resp.signedDays}天");
                }
            }
            else
            {
                            GameLog.Log("[DailyTask] 服务器签到同步失败, 本地状态保留");
            }
        }
    }

    private static string GetToken()
    {
        return CloudSaveManager.Instance?.Token ?? "";
    }

    [Serializable]
    private class UpdateProgressBody { public int taskType; public int amount; }

    private void OnDestroy()
    {
        if (panel != null) Destroy(panel);
        if (Instance == this) Instance = null;
    }
}
