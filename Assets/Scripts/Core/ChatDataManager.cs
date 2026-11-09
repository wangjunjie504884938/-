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
        string token = CloudSaveManager.GetAuthToken();
        if (!string.IsNullOrEmpty(token))
            StartCoroutine(UploadMessageToServer(friendName, message, time));
    }

    // ====== 频道聊天 (世界/组队/系统/当前) ======

    private Dictionary<string, List<string>> _channelHistory = new Dictionary<string, List<string>>();

    /// <summary>获取频道聊天记录（优先内存，其次PlayerPrefs）</summary>
    public List<string> GetChannelHistory(string channel)
    {
        if (_channelHistory.TryGetValue(channel, out var cached))
            return new List<string>(cached);

        var list = new List<string>();
        string json = PlayerPrefs.GetString($"ARPG_ChatChannel_{channel}", "");
        if (!string.IsNullOrEmpty(json))
        {
            list = new List<string>(json.Split('\n'));
            if (list.Count == 1 && string.IsNullOrEmpty(list[0]))
                list.Clear();
        }
        _channelHistory[channel] = list;
        return new List<string>(list);
    }

    /// <summary>发送频道消息（本地记录，暂无服务器同步）</summary>
    public void SendChannelMessage(string channel, string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        string time = System.DateTime.Now.ToString("HH:mm");
        AppendChannelLine(channel, $"[{time}] 我: {message}");
    }

    /// <summary>追加原始频道行（种子/系统提示用，不加"我:"前缀）</summary>
    public void AppendChannelLine(string channel, string line)
    {
        if (string.IsNullOrEmpty(line)) return;
        var history = GetChannelHistory(channel);
        history.Add(line);
        if (history.Count > 100)
            history.RemoveRange(0, history.Count - 100);
        _channelHistory[channel] = history;
        PlayerPrefs.SetString($"ARPG_ChatChannel_{channel}", string.Join("\n", history));
        PlayerPrefs.Save();
    }

    // ====== 语音消息 (本地录音WAV存取) ======

    public const int VoiceFrequency = 16000;
    private const string VoiceDirName = "chat_voice";

    private static string VoiceDir => System.IO.Path.Combine(Application.persistentDataPath, VoiceDirName);

    /// <summary>保存录音为16bit PCM单声道WAV, 返回文件名</summary>
    public static string SaveVoiceWav(float[] samples, int frequency)
    {
        System.IO.Directory.CreateDirectory(VoiceDir);
        string name = $"voice_{System.DateTime.Now:yyyyMMdd_HHmmss_fff}.wav";
        int dataLen = samples.Length * 2;
        using (var bw = new System.IO.BinaryWriter(System.IO.File.Create(System.IO.Path.Combine(VoiceDir, name))))
        {
            bw.Write(0x46464952);          // RIFF
            bw.Write(36 + dataLen);
            bw.Write(0x45564157);          // WAVE
            bw.Write(0x20746D66);          // "fmt "
            bw.Write(16);
            bw.Write((short)1);            // PCM
            bw.Write((short)1);            // mono
            bw.Write(frequency);
            bw.Write(frequency * 2);       // byte rate
            bw.Write((short)2);            // block align
            bw.Write((short)16);           // bits
            bw.Write(0x61746164);          // "data"
            bw.Write(dataLen);
            foreach (var s in samples)
            {
                short v = (short)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp(s, -1f, 1f) * 32767f), short.MinValue, short.MaxValue);
                bw.Write(v);
            }
        }
        PruneVoiceFiles();
        return name;
    }

    /// <summary>加载语音WAV为AudioClip (解析自己写入的16bit PCM单声道格式)</summary>
    public static AudioClip LoadVoiceClip(string filename)
    {
        if (string.IsNullOrEmpty(filename)) return null;
        string path = System.IO.Path.Combine(VoiceDir, filename);
        if (!System.IO.File.Exists(path)) return null;
        try
        {
            byte[] bytes = System.IO.File.ReadAllBytes(path);
            int pos = 12;   // 跳过 RIFF/WAVE 头, 遍历子块找 data
            while (pos + 8 <= bytes.Length)
            {
                string id = System.Text.Encoding.ASCII.GetString(bytes, pos, 4);
                int size = System.BitConverter.ToInt32(bytes, pos + 4);
                if (id == "data")
                {
                    int count = size / 2;
                    var samples = new float[count];
                    for (int i = 0; i < count; i++)
                        samples[i] = System.BitConverter.ToInt16(bytes, pos + 8 + i * 2) / 32768f;
                    var clip = AudioClip.Create(filename, count, 1, VoiceFrequency, false);
                    clip.SetData(samples, 0);
                    return clip;
                }
                pos += 8 + size + (size & 1);
            }
        }
        catch { }
        return null;
    }

    /// <summary>语音文件超过上限时删除最旧的 (文件名含时间戳可排序)</summary>
    private static void PruneVoiceFiles(int keep = 100)
    {
        try
        {
            var files = System.IO.Directory.GetFiles(VoiceDir, "voice_*.wav");
            if (files.Length <= keep) return;
            System.Array.Sort(files);
            for (int i = 0; i < files.Length - keep; i++)
                System.IO.File.Delete(files[i]);
        }
        catch { }
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
        string token = CloudSaveManager.GetAuthToken();
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
        string token = CloudSaveManager.GetAuthToken();
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
    private class FriendArray { public FriendUI.FriendInfo[] friends; }
    [System.Serializable]
    private class ChatMessage { public int toUserId; public string content; public string timestamp; }
    [System.Serializable]
    private class ChatHistoryResp { public ChatMessageData[] messages; }
    [System.Serializable]
    private class ChatMessageData { public string senderName; public string content; public string timestamp; }
}
