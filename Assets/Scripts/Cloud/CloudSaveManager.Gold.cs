using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;



/// <summary>

/// CloudSaveManager partial — Gold authority API + versioned player data update

/// </summary>

public partial class CloudSaveManager

{

    // ===== 金币权威API =====



    [Serializable]

    private class GoldOpBody { public int amount; public string reason; }



    [Serializable]

    private class GoldOpResponse { public bool success; public int newGold; public string error; public int currentGold; }



    /// <summary>服务器权威扣除金币 (阻塞式, 返回是否成功)</summary>

    public bool SpendGoldServer(int amount, string reason)

    {

        if (!IsLoggedIn) return false;

        var body = new GoldOpBody { amount = amount, reason = reason };

        string json = JsonUtility.ToJson(body);

        int slot = ActiveSlot;



        using (var req = new UnityWebRequest($"{ServerUrl}/api/save/spend-gold?slot={slot}", "POST"))

        {

            req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));

            req.downloadHandler = new DownloadHandlerBuffer();

            req.SetRequestHeader("Content-Type", "application/json");

            req.SetRequestHeader("Authorization", $"Bearer {_token}");

            req.timeout = (int)TimeoutSeconds;

            req.SendWebRequest();

            while (!req.isDone) System.Threading.Thread.Sleep(10);



            if (req.result == UnityWebRequest.Result.Success)

            {

                var resp = JsonUtility.FromJson<GoldOpResponse>(req.downloadHandler.text);

                if (resp.success && GameManager.Instance?.Player != null)

                    GameManager.Instance.Player.Stats.Gold = resp.newGold;

                return resp.success;

            }

        }

        return false;

    }



    /// <summary>服务器权威增加金币 (阻塞式)</summary>

    public bool AddGoldServer(int amount, string reason)

    {

        if (!IsLoggedIn) return false;

        var body = new GoldOpBody { amount = amount, reason = reason };

        string json = JsonUtility.ToJson(body);

        int slot = ActiveSlot;



        using (var req = new UnityWebRequest($"{ServerUrl}/api/save/add-gold?slot={slot}", "POST"))

        {

            req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));

            req.downloadHandler = new DownloadHandlerBuffer();

            req.SetRequestHeader("Content-Type", "application/json");

            req.SetRequestHeader("Authorization", $"Bearer {_token}");

            req.timeout = (int)TimeoutSeconds;

            req.SendWebRequest();

            while (!req.isDone) System.Threading.Thread.Sleep(10);



            if (req.result == UnityWebRequest.Result.Success)

            {

                var resp = JsonUtility.FromJson<GoldOpResponse>(req.downloadHandler.text);

                if (resp.success && GameManager.Instance?.Player != null)

                    GameManager.Instance.Player.Stats.Gold = resp.newGold;

                return resp.success;

            }

        }

        return false;

    }



    // ===== 统一更新接口（第二步：版本号机制） =====



    /// <summary>统一数据更新 — 携带版本号，服务器校验，最多重试3次</summary>

    public IEnumerator UpdatePlayerData(Action<bool> onComplete)

    {

        if (!IsLoggedIn) { onComplete?.Invoke(false); yield break; }



        int maxRetries = 3;

        for (int attempt = 0; attempt <= maxRetries; attempt++)

        {

            var dto = SaveDataDTO.FromPlayerPrefs();

            if (dto == null) { onComplete?.Invoke(false); yield break; }



            int clientVersion = RuntimePlayerData.Instance?.DataVersion ?? 1;

            var body = new UpdateRequestBody

            {

                saveJson = JsonUtility.ToJson(dto),

                level = dto.level,

                gold = dto.gold,

                clientVersion = clientVersion

            };

            string json = JsonUtility.ToJson(body);



            using (var req = new UnityWebRequest($"{ServerUrl}/api/save/update?slot={ActiveSlot}", "POST"))

            {

                byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

                req.uploadHandler = new UploadHandlerRaw(bodyRaw);

                req.downloadHandler = new DownloadHandlerBuffer();

                req.SetRequestHeader("Content-Type", "application/json");

                req.SetRequestHeader("Authorization", $"Bearer {_token}");

                req.timeout = 10;

                yield return req.SendWebRequest();



                if (req.result == UnityWebRequest.Result.Success)

                {

                    var resp = JsonUtility.FromJson<UpdateResponseData>(req.downloadHandler.text);

                    if (resp != null && resp.success)

                    {

                        RuntimePlayerData.Instance?.SetDataVersion(resp.newVersion);

                        onComplete?.Invoke(true);

                        yield break;

                    }

                }

                else if (req.responseCode == 409)

                {

                    // 版本冲突 — 解析服务器返回的最新版本号，重新下载，重试

                    if (attempt < maxRetries)

                    {

                        // 先从409响应体读取latestVersion，立即更新DataVersion

                        try

                        {

                            var conflict = JsonUtility.FromJson<ConflictResponseData>(req.downloadHandler.text);

                            if (conflict != null && conflict.latestVersion > 0)

                                RuntimePlayerData.Instance?.SetDataVersion(conflict.latestVersion);

                        }

                        catch { /* 解析失败不影响重试 */ }

                        GameLog.LogWarning($"[CloudSave] 版本冲突(当前v{clientVersion}→服务器v{RuntimePlayerData.Instance?.DataVersion})，第{attempt+1}次重试");

                        yield return StartCoroutine(Co_DownloadSave(ActiveSlot, _ => { }));

                        continue; // 重试

                    }

                    GameLog.LogError("[CloudSave] 版本冲突重试3次仍失败");

                    onComplete?.Invoke(false);

                    yield break;

                }



                // 404 = 服务器未部署新版本，回退到旧的上传接口

                if (req.responseCode == 404)

                {

                    GameLog.LogWarning("[CloudSave] /api/save/update 不存在(404)，回退到 /upload");

                    yield return StartCoroutine(Co_UploadSave(ActiveSlot, null, null));

                    onComplete?.Invoke(true);

                    yield break;

                }



                GameLog.LogWarning($"[CloudSave] 更新失败: {req.error}");

                onComplete?.Invoke(false);

                yield break;

            }

        }

    }



    [System.Serializable]

    private class UpdateRequestBody

    {

        public string saveJson;

        public int level;

        public int gold;

        public int clientVersion;

    }



    [System.Serializable]

    private class UpdateResponseData

    {

        public bool success;

        public int newVersion;

    }



    [System.Serializable]

    private class ConflictResponseData

    {

        public string error;

        public int latestVersion;

    }
}
