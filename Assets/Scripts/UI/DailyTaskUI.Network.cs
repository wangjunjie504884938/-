using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// DailyTaskUI partial — Server sync (task list + sign-in data)
/// </summary>
public partial class DailyTaskUI
{
    private IEnumerator LoadData()
    {
        string token = GetToken();
        if (string.IsNullOrEmpty(token)) yield break;

        bool taskDone = false, signInDone = false;

        StartCoroutine(LoadTasks(token, () => taskDone = true));
        StartCoroutine(LoadSignIn(token, () => signInDone = true));

        yield return new WaitUntil(() => taskDone && signInDone);

        // 同步本地积累的离线任务进度到服务器
        if (!_serverProgressFailed && !string.IsNullOrEmpty(token))
            yield return SyncLocalProgressToServer();

        RefreshUI();
    }

    /// <summary>将本地积累的任务进度同步到服务器</summary>
    private IEnumerator SyncLocalProgressToServer()
    {
        string token = GetToken();
        if (string.IsNullOrEmpty(token)) yield break;

        string dateKey = System.DateTime.Now.ToString("yyyyMMdd");
        string prefix = $"ARPG_Daily_{dateKey}_";

        // 检查本地是否有积累的进度
        bool hasLocalProgress = false;
        for (int i = 0; i < taskList.Count; i++)
        {
            if (PlayerPrefs.HasKey(prefix + $"task{i}_progress"))
            {
                hasLocalProgress = true;
                break;
            }
        }
        if (!hasLocalProgress) yield break;

        // 比较本地进度和服务器进度, 上报差异
        foreach (var task in taskList)
        {
            // Local progress is stored by taskType index (matching GenerateLocalTasks templates order)
            int localProgress = PlayerPrefs.GetInt(prefix + $"task{task.taskType}_progress", 0);
            if (localProgress > task.currentValue)
            {
                int diff = localProgress - task.currentValue;
                // 上报差异到服务器
                var body = new UpdateProgressBody { taskType = task.taskType, amount = diff };
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
                    if (req.result != UnityWebRequest.Result.Success)
                    {
                                    GameLog.Log("[DailyTask] 离线进度同步失败, 本地保留");
                        _serverProgressFailed = true;
                        yield break;
                    }
                }
            }
        }

                    GameLog.Log("[DailyTask] 离线进度已同步到服务器");
    }

    private IEnumerator LoadTasks(string token, Action onDone)
    {
        bool serverAvailable = !string.IsNullOrEmpty(token);
        if (serverAvailable)
        {
            using (var req = UnityWebRequest.Get($"{ApiBase}/daily/list"))
            {
                req.SetRequestHeader("Authorization", $"Bearer {token}");
                req.timeout = 5;
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    var resp = JsonUtility.FromJson<TaskListResponse>(req.downloadHandler.text);
                    if (resp?.tasks != null && resp.tasks.Count > 0)
                    {
                        taskList = resp.tasks;
                                    GameLog.Log($"[DailyTask] 解析到 {taskList.Count} 个任务");
                        onDone?.Invoke();
                        yield break;
                    }
                    else
                    {
                        GameLog.LogWarning("[DailyTask] 服务器返回空任务列表, 使用本地任务");
                    }
                }
                else
                {
                    GameLog.LogWarning($"[DailyTask] 加载任务失败: {req.error}, 使用本地任务");
                }
            }
        }

        // Fallback: generate local daily tasks when server is unavailable
        taskList = GenerateLocalTasks();
                    GameLog.Log($"[DailyTask] 使用本地任务: {taskList.Count} 个");
        onDone?.Invoke();
    }

    /// <summary>生成本地每日任务 (服务器不可用时的回退)</summary>
    private List<DailyTaskInfo> GenerateLocalTasks()
    {
        var tasks = new List<DailyTaskInfo>();
        string dateKey = System.DateTime.Now.ToString("yyyyMMdd");
        string prefix = $"ARPG_Daily_{dateKey}_";

        // 5种任务类型, 每种一个
        var templates = new (int type, int target, int reward)[]
        {
            (0, 50, 100),   // 击杀敌人 50, 奖励100金
            (1, 1, 150),     // 通关副本 1次, 奖励150金
            (2, 500, 200),   // 消耗金币 500, 奖励200金
            (3, 3, 120),     // 强化装备 3次, 奖励120金
            (4, 500, 100),   // 获得经验 500, 奖励100金
        };

        for (int i = 0; i < templates.Length; i++)
        {
            var (type, target, reward) = templates[i];
            int taskId = i + 1;
            int current = PlayerPrefs.GetInt(prefix + $"task{type}_progress", 0);
            bool claimed = PlayerPrefs.GetInt(prefix + $"task{type}_claimed", 0) == 1;

            tasks.Add(new DailyTaskInfo
            {
                id = taskId,
                taskType = type,
                targetValue = target,
                currentValue = current,
                rewardGold = reward,
                claimed = claimed
            });
        }

        return tasks;
    }

    private IEnumerator LoadSignIn(string token, Action onDone)
    {
        bool serverAvailable = !string.IsNullOrEmpty(token);
        if (serverAvailable)
        {
            using (var req = UnityWebRequest.Get($"{ApiBase}/signin/status"))
            {
                req.SetRequestHeader("Authorization", $"Bearer {token}");
                req.timeout = 5;
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    var resp = JsonUtility.FromJson<SignInResponse>(req.downloadHandler.text);
                    if (resp != null)
                    {
                        var serverData = new SignInData
                        {
                            signedDays = resp.signedDays,
                            canSign = resp.canSign,
                            todayReward = resp.todayReward,
                            rewards = resp.rewards
                        };
                        // 合并服务器状态和本地状态 (防止本地已签到被服务器覆盖)
                        signInData = MergeWithLocal(serverData);
                                    GameLog.Log($"[DailyTask] 签到(合并后): signedDays={signInData.signedDays}, canSign={signInData.canSign}");
                        onDone?.Invoke();
                        yield break;
                    }
                }
                else
                {
                    GameLog.LogWarning($"[DailyTask] 加载签到状态失败: {req.error}, 使用本地签到");
                }
            }
        }

        // Fallback: local sign-in data
        signInData = GenerateLocalSignIn();
                    GameLog.Log($"[DailyTask] 使用本地签到: signedDays={signInData.signedDays}, canSign={signInData.canSign}");
        onDone?.Invoke();
    }

    /// <summary>合并服务器签到状态和本地签到状态 (取较新的一方)</summary>
    private SignInData MergeWithLocal(SignInData serverData)
    {
        if (serverData == null) return GenerateLocalSignIn();

        string today = System.DateTime.Now.ToString("yyyyMMdd");
        string localLastSign = PlayerPrefs.GetString("ARPG_SignIn_LastDate", "");
        int localDays = PlayerPrefs.GetInt("ARPG_SignIn_Days", 0);

        // 如果本地今天已签到, 但服务器说可以签 → 以本地为准 (服务器可能没同步)
        if (localLastSign == today && serverData.canSign)
        {
            // 本地已签到, 取本地的天数
            serverData.canSign = false;
            serverData.signedDays = Mathf.Max(serverData.signedDays, localDays);
        }
        // 如果服务器说已签到, 但本地说可以 → 以服务器为准, 同步到本地
        else if (!serverData.canSign && localLastSign != today)
        {
            PlayerPrefs.SetString("ARPG_SignIn_LastDate", today);
            PlayerPrefs.SetInt("ARPG_SignIn_Days", serverData.signedDays);
            PlayerPrefs.Save();
        }

        return serverData;
    }

    /// <summary>生成本地签到数据 (服务器不可用时的回退)</summary>
    private SignInData GenerateLocalSignIn()
    {
        string today = System.DateTime.Now.ToString("yyyyMMdd");
        string lastSignDate = PlayerPrefs.GetString("ARPG_SignIn_LastDate", "");
        int signedDays = PlayerPrefs.GetInt("ARPG_SignIn_Days", 0);

        // If already signed today, can't sign again
        bool canSign = lastSignDate != today;

        // Reset weekly cycle
        if (signedDays >= 7) signedDays = 0;

        int[] rewards = { 100, 150, 200, 300, 500, 800, 1200 };
        int todayReward = rewards[Mathf.Min(signedDays, 6)];

        return new SignInData
        {
            signedDays = signedDays,
            canSign = canSign,
            todayReward = todayReward,
            rewards = rewards
        };
    }
}
