using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// 数据同步服务 — 负责所有数据同步操作（下载、上传、缓存读写）
/// 服务器是权威来源，本地缓存仅离线兜底
/// </summary>
public class DataSyncService : MonoBehaviour
{
    public static DataSyncService Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>启动时：从服务器同步，失败则用本地缓存</summary>
    public IEnumerator SyncFromServerOrCache(Action<bool, string> onComplete)
    {
        if (!CloudSaveManager.HasInstance || !CloudSaveManager.Instance.IsLoggedIn)
        {
            // 离线模式
            int cached = LocalSettingsManager.GetCachedStage();
            RuntimePlayerData.Instance?.RefreshFromCache(cached);
            onComplete?.Invoke(true, "离线模式");
            yield break;
        }

        bool done = false;
        bool success = false;

        CloudSaveManager.Instance.DownloadSave(CloudSaveManager.Instance.ActiveSlot, result =>
        {
            success = result;
            done = true;
        });

        // 最多等10秒（用unscaledDeltaTime防止暂停时卡死）
        float timer = 0;
        while (!done && timer < 10f)
        {
            timer += Time.unscaledDeltaTime;
            yield return null;
        }

        if (success && RuntimePlayerData.Instance != null && RuntimePlayerData.Instance.IsLoaded)
        {
            onComplete?.Invoke(true, null);
        }
        else
        {
            // 服务器失败，用本地缓存
            int cached = LocalSettingsManager.GetCachedStage();
            RuntimePlayerData.Instance?.RefreshFromCache(cached);
            onComplete?.Invoke(false, "服务器连接失败，使用本地缓存");
        }
    }

    /// <summary>通关时：提交结果到服务器，返回奖励</summary>
    public IEnumerator SubmitDungeonResult(int dungeonId, float timeUsed, int hpRemaining, int hpMax, int enemiesKilled, Action<bool> onComplete)
    {
        string token = CloudSaveManager.Instance?.Token ?? "";
        int slot = CloudSaveManager.Instance?.ActiveSlot ?? 0;
        string apiBase = (CloudSaveManager.Instance?.ServerUrl ?? "http://localhost:5132") + "/api";

        var body = new DungeonResultBody
        {
            dungeonId = dungeonId,
            timeUsed = timeUsed,
            hpRemaining = hpRemaining,
            hpMax = hpMax,
            enemiesKilled = enemiesKilled
        };
        string json = JsonUtility.ToJson(body);

        using (var req = new UnityEngine.Networking.UnityWebRequest($"{apiBase}/save/dungeon/complete?slot={slot}", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", $"Bearer {token}");
            req.timeout = 10;
            yield return req.SendWebRequest();

            if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                var resp = JsonUtility.FromJson<DungeonResultResponse>(req.downloadHandler.text);
                if (resp != null && resp.success)
                {
                    // 服务器权威更新RuntimePlayerData
                    if (RuntimePlayerData.Instance != null && resp.newHighestStage >= 0)
                        RuntimePlayerData.Instance.RefreshFromServer(resp.newHighestStage, null);

                    // 上传最新存档（更新SaveJson中的Level/Gold等）
                    if (CloudSaveManager.HasInstance && CloudSaveManager.Instance.IsLoggedIn)
                        CloudSaveManager.Instance.StartCoroutine(CloudSaveManager.Instance.UploadSaveCo());

                    onComplete?.Invoke(true);
                    yield break;
                }
            }
        }

        // 服务器失败
        onComplete?.Invoke(false);
    }

    /// <summary>写入本地缓存</summary>
    public void WriteLocalCache(int stage)
    {
        LocalSettingsManager.CacheStageProgress(stage, null);
    }

    /// <summary>读取本地缓存</summary>
    public int ReadLocalCache()
    {
        return LocalSettingsManager.GetCachedStage();
    }

    [System.Serializable]
    public class DungeonResultBody
    {
        public int dungeonId;
        public float timeUsed;
        public int hpRemaining;
        public int hpMax;
        public int enemiesKilled;
    }

    [System.Serializable]
    public class DungeonResultResponse
    {
        public bool success;
        public int goldReward;
        public int baseGold;
        public int speedBonus;
        public int fullHpBonus;
        public int xpReward;
        public int newGold;
        public int newHighestStage;
        public System.Collections.Generic.List<DungeonResultDrop> drops;
    }

    [System.Serializable]
    public class DungeonResultDrop
    {
        public int rarity;
        public int slotType;
        public int dungeonLevel;
    }
}
