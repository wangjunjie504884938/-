using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 聊天数据管理器 — 管理聊天记录的内存缓存，减少PlayerPrefs读写
/// 后续可扩展为服务器同步
/// </summary>
public class ChatDataManager : MonoBehaviour
{
    public static ChatDataManager Instance { get; private set; }

    // 每个好友的最新消息缓存
    private Dictionary<string, List<string>> _chatHistory = new Dictionary<string, List<string>>();

    // 服务器同步URL
    private const string ServerUrl = "http://39.107.141.107:5132";

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>获取聊天记录（优先内存，其次PlayerPrefs）</summary>
    public List<string> GetHistory(string friendName)
    {
        if (_chatHistory.TryGetValue(friendName, out var cached))
            return new List<string>(cached);

        // 从PlayerPrefs加载
        var list = new List<string>();
        string key = $"ARPG_Chat_{friendName}";
        string json = PlayerPrefs.GetString(key, "");
        if (!string.IsNullOrEmpty(json))
        {
            list = new List<string>(json.Split('\n'));
            if (list.Count == 1 && string.IsNullOrEmpty(list[0]))
                list.Clear();
        }
        _chatHistory[friendName] = list;
        return new List<string>(list);
    }

    /// <summary>发送消息</summary>
    public void SendMessage(string friendName, string message)
    {
        if (string.IsNullOrEmpty(message)) return;

        var history = GetHistory(friendName);
        string time = System.DateTime.Now.ToString("HH:mm");
        history.Add($"[{time}] 我: {message}");
        if (history.Count > 100)
            history.RemoveRange(0, history.Count - 100);
        _chatHistory[friendName] = history;

        // 保存到PlayerPrefs
        PlayerPrefs.SetString($"ARPG_Chat_{friendName}", string.Join("\n", history));
        PlayerPrefs.Save();

        // 异步上传到服务器（如果已登录）
        string token = PlayerPrefs.GetString("ARPG_AuthToken", "");
        if (!string.IsNullOrEmpty(token))
            StartCoroutine(UploadMessageToServer(friendName, message, time));
    }

    /// <summary>清除聊天记录</summary>
    public void ClearHistory(string friendName)
    {
        _chatHistory.Remove(friendName);
        PlayerPrefs.DeleteKey($"ARPG_Chat_{friendName}");
        PlayerPrefs.Save();
    }

    /// <summary>上传消息到服务器（联机版）</summary>
    private IEnumerator UploadMessageToServer(string friendName, string message, string time)
    {
        string token = PlayerPrefs.GetString("ARPG_AuthToken", "");
        if (string.IsNullOrEmpty(token)) yield break;

        // 查找好友ID（从本地缓存）
        string friendJson = PlayerPrefs.GetString("ARPG_FriendList", "");
        int friendId = -1;
        if (!string.IsNullOrEmpty(friendJson))
        {
            try
            {
                var friends = JsonUtility.FromJson<FriendArray>(friendJson);
                if (friends?.friends != null)
                {
                    foreach (var f in friends.friends)
                    {
                        if (f.name == friendName) { friendId = 0; break; }
                    }
                }
            }
            catch { }
        }
        if (friendId < 0) yield break;

        // POST /api/chat/send
        string json = JsonUtility.ToJson(new ChatMessage
        {
            toUserId = friendId,
            content = message,
            timestamp = time
        });

        using (var req = new UnityEngine.Networking.UnityWebRequest($"{ServerUrl}/api/chat/send", "POST"))
        {
            req.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + token);
            req.timeout = 5;
            yield return req.SendWebRequest();
        }
    }

    /// <summary>从服务器拉取新消息</summary>
    public IEnumerator FetchNewMessages(string friendName)
    {
        string token = PlayerPrefs.GetString("ARPG_AuthToken", "");
        if (string.IsNullOrEmpty(token)) yield break;

        using (var req = UnityEngine.Networking.UnityWebRequest.Get($"{ServerUrl}/api/chat/history?friendName={friendName}"))
        {
            req.SetRequestHeader("Authorization", "Bearer " + token);
            req.timeout = 5;
            yield return req.SendWebRequest();

            if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                try
                {
                    var resp = JsonUtility.FromJson<ChatHistoryResp>(req.downloadHandler.text);
                    if (resp?.messages != null && resp.messages.Length > 0)
                    {
                        var history = GetHistory(friendName);
                        foreach (var msg in resp.messages)
                        {
                            string line = $"[{msg.timestamp}] {msg.senderName}: {msg.content}";
                            if (!history.Contains(line))
                                history.Add(line);
                        }
                        if (history.Count > 100)
                            history.RemoveRange(0, history.Count - 100);
                        _chatHistory[friendName] = history;
                        PlayerPrefs.SetString($"ARPG_Chat_{friendName}", string.Join("\n", history));
                        PlayerPrefs.Save();
                    }
                }
                catch { }
            }
        }
    }

    [System.Serializable]
    private class FriendArray { public FriendUI.FriendData[] friends; }
    [System.Serializable]
    private class ChatMessage { public int toUserId; public string content; public string timestamp; }
    [System.Serializable]
    private class ChatHistoryResp { public ChatMessageData[] messages; }
    [System.Serializable]
    private class ChatMessageData { public string senderName; public string content; public string timestamp; }
}
