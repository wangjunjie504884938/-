using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 云端存档管理器 — 支持每用户最多3个角色槽位
///
/// API:
///   - Login / Register / Logout
///   - UploadSave(slot) / DownloadSave(slot) / ListSlots
///   - ActiveSlot: 当前选中的角色槽位
/// </summary>
public class CloudSaveManager : MonoBehaviour
{
    public static CloudSaveManager Instance { get; private set; }

    [Header("服务器配置")]
    public string ServerUrl = "http://localhost:5132";
    public float TimeoutSeconds = 10f;

    public const int MaxSlots = 3;

    // ====== 状态 ======
    public bool IsLoggedIn { get; private set; }
    public static bool HasInstance => Instance != null;
    public string CurrentUsername { get; private set; }
    /// <summary>返回解密后的JWT token (供其他UI类使用)</summary>
    public string Token => _token;
    /// <summary>当前活跃角色槽位 (0-2)</summary>
    public int ActiveSlot { get; set; } = 0;

    private const string TokenKey = "ARPG_CloudToken";
    private const string UsernameKey = "ARPG_CloudUser";
    private const string ActiveSlotKey = "ARPG_ActiveSlot";

    private string _token;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 解密加载Token
        string encryptedToken = PlayerPrefs.GetString(TokenKey, "");
        _token = DecryptToken(encryptedToken);
        CurrentUsername = PlayerPrefs.GetString(UsernameKey, "");
        IsLoggedIn = !string.IsNullOrEmpty(_token);
        ActiveSlot = PlayerPrefs.GetInt(ActiveSlotKey, 0);
    }

    public void SetActiveSlot(int slot)
    {
        ActiveSlot = Mathf.Clamp(slot, 0, MaxSlots - 1);
        PlayerPrefs.SetInt(ActiveSlotKey, ActiveSlot);
        PlayerPrefs.Save();
    }

    // ========================================================
    //  注册
    // ========================================================

    public void Register(string username, string password,
        Action onSuccess, Action<string> onFail)
    {
        StartCoroutine(Co_Register(username, password, onSuccess, onFail));
    }

    private IEnumerator Co_Register(string username, string password,
        Action onSuccess, Action<string> onFail)
    {
        var body = new AuthRequest { username = username, password = password };
        string json = JsonUtility.ToJson(body);

        using (var req = new UnityWebRequest($"{ServerUrl}/api/auth/register", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = (int)TimeoutSeconds;
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var resp = JsonUtility.FromJson<AuthResponse>(req.downloadHandler.text);
                SaveToken(resp.token, resp.username);
                onSuccess?.Invoke();
            }
            else
            {
                onFail?.Invoke(ExtractError(req));
            }
        }
    }

    // ========================================================
    //  登录
    // ========================================================

    public void Login(string username, string password,
        Action onSuccess, Action<string> onFail)
    {
        StartCoroutine(Co_Login(username, password, onSuccess, onFail));
    }

    private IEnumerator Co_Login(string username, string password,
        Action onSuccess, Action<string> onFail)
    {
        var body = new AuthRequest { username = username, password = password };
        string json = JsonUtility.ToJson(body);

        using (var req = new UnityWebRequest($"{ServerUrl}/api/auth/login", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = (int)TimeoutSeconds;
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var resp = JsonUtility.FromJson<AuthResponse>(req.downloadHandler.text);
                SaveToken(resp.token, resp.username);
                onSuccess?.Invoke();
            }
            else
            {
                onFail?.Invoke(ExtractError(req));
            }
        }
    }

    // ========================================================
    //  上传存档 (带槽位)
    // ========================================================

    public void UploadSave(int slot, Action onSuccess, Action<string> onFail)
    {
        if (!IsLoggedIn) { onFail?.Invoke("未登录"); return; }
        StartCoroutine(Co_UploadSave(slot, onSuccess, onFail));
    }

    /// <summary>自动上传当前槽位 (无回调, 由 Stats.Save() 调用)</summary>
    public IEnumerator UploadSaveCo()
    {
        yield return StartCoroutine(Co_UploadSave(ActiveSlot, null, null));
    }

    private IEnumerator Co_UploadSave(int slot, Action onSuccess, Action<string> onFail)
    {
        var dto = SaveDataDTO.FromPlayerPrefs();
        if (dto == null || !PlayerPrefs.HasKey($"ARPG_S{slot}_Class"))
        { onFail?.Invoke("无法获取当前游戏数据"); yield break; }

        string json = JsonUtility.ToJson(dto);
        // 构建带摘要信息的上传请求
        var uploadBody = new UploadSaveBody
        {
            saveJson = json,
            classType = dto.classType,
            level = dto.level,
            highestStageCleared = dto.highestStageCleared,
            gold = dto.gold,
            characterName = dto.characterName
        };
        string bodyJson = JsonUtility.ToJson(uploadBody);

        using (var req = new UnityWebRequest($"{ServerUrl}/api/save/upload?slot={slot}", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(bodyJson);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", $"Bearer {_token}");
            req.timeout = (int)TimeoutSeconds;
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                            GameLog.Log($"[CloudSave] 槽位{slot}存档上传成功");
                onSuccess?.Invoke();
            }
            else
            {
                if (CheckTokenExpired(req)) { onFail?.Invoke("登录已过期"); yield break; }
                onFail?.Invoke(ExtractError(req));
            }
        }
    }

    // ========================================================
    //  下载存档 (带槽位)
    // ========================================================

    public void DownloadSave(int slot, Action<bool> onDone)
    {
        if (!IsLoggedIn) { onDone?.Invoke(false); return; }
        StartCoroutine(Co_DownloadSave(slot, onDone));
    }

    private IEnumerator Co_DownloadSave(int slot, Action<bool> onDone)
    {
        using (var req = UnityWebRequest.Get($"{ServerUrl}/api/save/load?slot={slot}"))
        {
            req.SetRequestHeader("Authorization", $"Bearer {_token}");
            req.timeout = (int)TimeoutSeconds;
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var resp = JsonUtility.FromJson<LoadSaveResponseData>(req.downloadHandler.text);
                if (resp != null && resp.found)
                {
                    var dto = JsonUtility.FromJson<SaveDataDTO>(resp.saveJson);
                    if (dto != null)
                    {
                        dto.ApplyToLocal();
                        // 服务器权威: 更新RuntimePlayerData的通关记录
                        // 取服务器SaveJson值和当前RuntimePlayerData值的较大者
                        // 防止旧SaveJson覆盖SubmitDungeonResult已更新的通关记录
                        if (RuntimePlayerData.Instance != null)
                        {
                            int serverStage = dto.highestStageCleared;
                            int currentStage = RuntimePlayerData.Instance.HighestStage;
                            int effectiveStage = Mathf.Max(serverStage, currentStage);
                            RuntimePlayerData.Instance.RefreshFromServer(effectiveStage, null);
                            // 同步服务器端DataVersion，防止后续UpdatePlayerData版本冲突
                            RuntimePlayerData.Instance.SetDataVersion(resp.dataVersion);
                        }
                                    GameLog.Log($"[CloudSave] 槽位{slot}存档下载成功, highestStage={dto.highestStageCleared}");
                        onDone?.Invoke(true);
                        yield break;
                    }
                }
                            GameLog.Log($"[CloudSave] 槽位{slot}无云端存档");
                onDone?.Invoke(false);
            }
            else
            {
                if (CheckTokenExpired(req)) { onDone?.Invoke(false); yield break; }
                GameLog.LogWarning($"[CloudSave] 下载失败: {ExtractError(req)}");
                onDone?.Invoke(false);
            }
        }
    }

    // ========================================================
    //  列出所有槽位
    // ========================================================

    public void ListSlots(Action<SlotSummary[]> onDone)
    {
        if (!IsLoggedIn) { onDone?.Invoke(null); return; }
        StartCoroutine(Co_ListSlots(onDone));
    }

    private IEnumerator Co_ListSlots(Action<SlotSummary[]> onDone)
    {
        using (var req = UnityWebRequest.Get($"{ServerUrl}/api/save/list"))
        {
            req.SetRequestHeader("Authorization", $"Bearer {_token}");
            req.timeout = (int)TimeoutSeconds;
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var resp = JsonUtility.FromJson<SlotListResponse>(req.downloadHandler.text);
                if (resp != null && resp.slots != null)
                {
                    onDone?.Invoke(resp.slots);
                    yield break;
                }
            }
            if (CheckTokenExpired(req)) { onDone?.Invoke(null); yield break; }
            GameLog.LogWarning($"[CloudSave] 获取槽位列表失败: {ExtractError(req)}");
            onDone?.Invoke(null);
        }
    }

    // ========================================================
    //  删除存档
    // ========================================================

    public void DeleteSave(int slot, Action onSuccess, Action<string> onFail)
    {
        if (!IsLoggedIn) { onFail?.Invoke("未登录"); return; }
        StartCoroutine(Co_DeleteSave(slot, onSuccess, onFail));
    }

    private IEnumerator Co_DeleteSave(int slot, Action onSuccess, Action<string> onFail)
    {
        using (var req = UnityWebRequest.Delete($"{ServerUrl}/api/save/delete?slot={slot}"))
        {
            req.SetRequestHeader("Authorization", $"Bearer {_token}");
            req.timeout = (int)TimeoutSeconds;
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                            GameLog.Log($"[CloudSave] 槽位{slot}存档已删除");
                onSuccess?.Invoke();
            }
            else
            {
                onFail?.Invoke(ExtractError(req));
            }
        }
    }

    // ========================================================
    //  退出登录
    // ========================================================

    public void Logout()
    {
        _token = "";
        CurrentUsername = "";
        IsLoggedIn = false;
        PlayerPrefs.DeleteKey(TokenKey);
        PlayerPrefs.DeleteKey(UsernameKey);
        PlayerPrefs.Save();
    }

    // ========================================================
    //  Ping (退出时通知服务器，延长Redis缓存)
    // ========================================================

    /// <summary>发送心跳Ping，不等待响应，发完即走。404时静默忽略（服务器未部署新版本）</summary>
    public void SendPing()
    {
        if (!IsLoggedIn || string.IsNullOrEmpty(_token)) return;
        try
        {
            using (var req = UnityWebRequest.PostWwwForm($"{ServerUrl}/api/save/ping?slot={ActiveSlot}", ""))
            {
                req.SetRequestHeader("Authorization", $"Bearer {_token}");
                req.timeout = 2;
                req.SendWebRequest();
                // 不等待响应，发完即走。404静默忽略（服务器旧版本无此端点）
            }
        }
        catch { /* 忽略错误，Ping失败不影响退出 */ }
    }

    /// <summary>同步本地邮件到服务器 (跨设备支持)</summary>
    public IEnumerator SyncLocalMailsToServerCo()
    {
        if (!IsLoggedIn) yield break;

        var localMails = LocalMailSystem.GetUnsyncedMails();
        if (localMails.Count == 0) yield break;

        string apiBase = ServerUrl + "/api/mail";
        foreach (var mail in localMails)
        {
            var body = new SendMailBody
            {
                title = mail.title,
                content = mail.content,
                attachmentGold = mail.gold,
                attachmentItems = mail.itemJson ?? ""
            };
            string json = JsonUtility.ToJson(body);

            using (var req = new UnityWebRequest($"{apiBase}/send", "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {_token}");
                req.timeout = 5;
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    LocalMailSystem.MarkSynced(mail.id);
                                GameLog.Log($"[CloudSave] 本地邮件已同步到服务器: {mail.title}");
                }
                else
                {
                    GameLog.LogWarning($"[CloudSave] 邮件同步失败: {req.error}");
                    yield break; // 停止同步, 下次重试
                }
            }
        }
    }

    [Serializable]
    public class SendMailBody
    {
        public string title;
        public string content;
        public int attachmentGold;
        public string attachmentItems;
    }

    // ========================================================
    //  内部工具
    // ========================================================

    // AES加密密钥 (与服务器端共享)
    private static readonly byte[] AesKey = System.Text.Encoding.UTF8.GetBytes("ArpgGuardian2026!!"); // 16字节
    private static readonly byte[] AesIv = System.Text.Encoding.UTF8.GetBytes("InitVector16Bytes");

    private static string EncryptToken(string plainToken)
    {
        if (string.IsNullOrEmpty(plainToken)) return "";
        try
        {
            using (var aes = System.Security.Cryptography.Aes.Create())
            {
                aes.Key = AesKey;
                aes.IV = AesIv;
                var encryptor = aes.CreateEncryptor();
                var bytes = System.Text.Encoding.UTF8.GetBytes(plainToken);
                var encrypted = encryptor.TransformFinalBlock(bytes, 0, bytes.Length);
                return System.Convert.ToBase64String(encrypted);
            }
        }
        catch { return plainToken; }
    }

    private static string DecryptToken(string storedToken)
    {
        if (string.IsNullOrEmpty(storedToken)) return "";

        // 兼容旧版明文token: JWT以"eyJ"开头, 加密token是Base64
        if (storedToken.StartsWith("eyJ"))
        {
            // 旧版明文token, 自动迁移加密
            PlayerPrefs.SetString(TokenKey, EncryptToken(storedToken));
            PlayerPrefs.Save();
            return storedToken;
        }

        try
        {
            using (var aes = System.Security.Cryptography.Aes.Create())
            {
                aes.Key = AesKey;
                aes.IV = AesIv;
                var decryptor = aes.CreateDecryptor();
                var bytes = System.Convert.FromBase64String(storedToken);
                var decrypted = decryptor.TransformFinalBlock(bytes, 0, bytes.Length);
                return System.Text.Encoding.UTF8.GetString(decrypted);
            }
        }
        catch { return ""; }
    }

    private void SaveToken(string token, string username)
    {
        // 检测账号切换：如果登录的用户名和之前不同，清理本地存档
        string oldUsername = PlayerPrefs.GetString(UsernameKey, "");
        if (!string.IsNullOrEmpty(oldUsername) && oldUsername != username)
        {
                        GameLog.Log($"[CloudSave] 账号切换: {oldUsername} → {username}, 清理本地存档");
            ClearLocalSaveData();
        }

        _token = token;
        CurrentUsername = username;
        IsLoggedIn = true;
        // 加密存储Token, 防止明文泄露
        PlayerPrefs.SetString(TokenKey, EncryptToken(token));
        PlayerPrefs.SetString(UsernameKey, username);
        PlayerPrefs.Save();
                    GameLog.Log($"[CloudSave] 登录成功: {username}");
    }

    /// <summary>清理本地存档数据（账号切换时调用）</summary>
    private void ClearLocalSaveData()
    {
        string[] knownKeys = {
            "Class", "Level", "Xp", "Gold", "SkillPoints", "HighestStage",
            "Skills", "Equipment", "Hp", "Achievements",
            "BaseMaxHp", "BaseAttack", "BaseDefense",
            "BaseMoveSpeed", "BaseCritChance", "BaseAttackRange",
            "BaseAttackCooldown", "BaseHpRegen",
            "CharacterName", "PassiveTree", "DismantleFragments",
        };
        string[] petNames = { "Spirit", "Flame", "Guardian", "Shadow" };

        // 清理所有3个槽位的本地存档
        for (int slot = 0; slot < 3; slot++)
        {
            string prefix = $"ARPG_S{slot}_";
            foreach (var key in knownKeys)
                PlayerPrefs.DeleteKey(prefix + key);

            // 宠物数据 (枚举名)
            PlayerPrefs.DeleteKey(prefix + "Pet_ActiveType");
            foreach (var petName in petNames)
            {
                PlayerPrefs.DeleteKey(prefix + "Pet_" + petName + "_Owned");
                PlayerPrefs.DeleteKey(prefix + "Pet_" + petName + "_Level");
                PlayerPrefs.DeleteKey(prefix + "Pet_" + petName + "_Evolved");
            }
            // 旧格式数字key兜底
            for (int i = 0; i < 4; i++)
            {
                PlayerPrefs.DeleteKey(prefix + "Pet_" + i + "_Owned");
                PlayerPrefs.DeleteKey(prefix + "Pet_" + i + "_Level");
                PlayerPrefs.DeleteKey(prefix + "Pet_" + i + "_Evolved");
            }
        }
        // 兼容: 清理旧格式宠物数据 (无槽位前缀)
        PlayerPrefs.DeleteKey("ARPG_Pet_ActiveType");
        for (int i = 0; i < 4; i++)
        {
            PlayerPrefs.DeleteKey($"ARPG_Pet_{i}_Owned");
            PlayerPrefs.DeleteKey($"ARPG_Pet_{i}_Level");
            PlayerPrefs.DeleteKey($"ARPG_Pet_{i}_Evolved");
        }

        // 清除账号级别数据 (公会/PVP/签到/抽卡/离线/锻造/对话/每日/通行证)
        string[] accountKeys = {
            "ARPG_GuildName", "ARPG_GuildLevel", "ARPG_GuildExp",
            "ARPG_PvpWins", "ARPG_PvpLosses", "ARPG_PvpDefRating",
            "ARPG_GhostUploaded",
            "ARPG_SignIn_LastDate", "ARPG_SignIn_Days",
            "ARPG_LastOnlineTime", "ARPG_OfflineRewardClaimed",
            "ARPG_ForgeHistory",
            // 通行证数据
            "ARPG_Season_Id", "ARPG_Season_XP", "ARPG_Season_Level",
            "ARPG_Season_Premium", "ARPG_Season_FreeClaimed", "ARPG_Season_PremiumClaimed",
            "ARPG_Season_WeekStart",
        };
        foreach (var key in accountKeys)
            PlayerPrefs.DeleteKey(key);

        // 通行证领取记录
        for (int i = 1; i <= 30; i++)
        {
            PlayerPrefs.DeleteKey($"ARPG_Season_FreeClaimed_{i}");
            PlayerPrefs.DeleteKey($"ARPG_Season_PremiumClaimed_{i}");
        }

        for (int i = 0; i < 50; i++)
        {
            PlayerPrefs.DeleteKey($"ARPG_DialogueStart_{i}");
            PlayerPrefs.DeleteKey($"ARPG_DialogueEnd_{i}");
        }

        for (int d = 0; d < 30; d++)
        {
            var date = System.DateTime.Now.AddDays(-d).ToString("yyyyMMdd");
            for (int t = 0; t < 10; t++)
            {
                PlayerPrefs.DeleteKey($"ARPG_Daily_{date}_task{t}_progress");
                PlayerPrefs.DeleteKey($"ARPG_Daily_{date}_task{t}_claimed");
            }
            PlayerPrefs.DeleteKey($"ARPG_DailyChallenge_{date}");
        }

        PlayerPrefs.Save();
                    GameLog.Log("[CloudSave] 本地存档已清理");
    }

    private string ExtractError(UnityWebRequest req)
    {
        if (string.IsNullOrEmpty(req.downloadHandler?.text))
            return req.error;
        try
        {
            var err = JsonUtility.FromJson<ServerError>(req.downloadHandler.text);
            return err?.error ?? req.error;
        }
        catch
        {
            return req.error;
        }
    }

    /// <summary>检查响应是否为401未授权, 如果是则自动登出并触发会话过期事件</summary>
    private bool CheckTokenExpired(UnityWebRequest req)
    {
        if (req.responseCode == 401)
        {
            GameLog.LogWarning("[CloudSave] Token已过期或无效, 自动登出");
            OnSessionExpired?.Invoke();
            return true;
        }
        return false;
    }

    /// <summary>会话过期事件 (Token失效时触发, UI层可监听并提示重新登录)</summary>
    public event Action OnSessionExpired;

    // ====== JSON 类 ======
    [Serializable]
    public class AuthRequest { public string username; public string password; }

    [Serializable]
    public class AuthResponse { public string token; public string username; }

    [Serializable]
    public class UploadSaveBody
    {
        public string saveJson;
        public int classType;
        public int level;
        public int highestStageCleared;
        public int gold;
        public string characterName;
    }

    [Serializable]
    public class LoadSaveResponseData
    {
        public string saveJson;
        public bool found;
        public string updatedAt;
        public int dataVersion;
    }

    [Serializable]
    public class ServerError { public string error; }

    [Serializable]
    public class SlotSummary
    {
        public int slot;
        public bool occupied;
        public int classType;
        public int level;
        public int highestStageCleared;
        public string characterName;
        public string updatedAt;
    }

    [Serializable]
    public class SlotListResponse
    {
        public SlotSummary[] slots;
    }

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
