using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace ArpgGameServer
{
    /// <summary>
    /// 快照上传器 (规划阶段06) — 游戏服务器 → WebAPI 快照持久化。
    /// 有界队列(500) + 失败重试3次(指数退避) + 同快照去重; 不阻塞游戏主线程逻辑。
    /// </summary>
    public class SnapshotUploader : MonoBehaviour
    {
        public static SnapshotUploader Instance { get; private set; }

        private readonly ConcurrentQueue<(string json, int retries)> _queue = new();
        private Coroutine _loop;
        private string _apiBase;

        private const int MaxQueue = 500;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void Configure(string webApiUrl)
        {
            _apiBase = webApiUrl?.TrimEnd('/') ?? "http://localhost:5132";
        }

        /// <summary>入队 (满则丢弃最旧并警告 — 有界队列不允许无界增长)</summary>
        public void Enqueue(RoomSnapshotData snap)
        {
            if (_queue.Count >= MaxQueue)
            {
                Debug.LogWarning("[Snapshot] 队列满, 丢弃最旧快照");
                _queue.TryDequeue(out _);
            }
            _queue.Enqueue((snap.ToJson(), 0));
            if (_loop == null) _loop = StartCoroutine(UploadLoop());
        }

        private IEnumerator UploadLoop()
        {
            while (true)
            {
                if (_queue.TryDequeue(out var job))
                {
                    yield return PostSnapshot(job.json, job.retries);
                }
                else yield return new WaitForSeconds(0.5f);
            }
        }

        private IEnumerator PostSnapshot(string json, int retries)
        {
            using var req = new UnityWebRequest($"{_apiBase}/api/dungeon/snapshot/save", "POST");
            req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 5;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success && retries < 3)
            {
                Debug.LogWarning($"[Snapshot] 上传失败(重试{retries + 1}): {req.error}");
                yield return new WaitForSeconds(1f * (retries + 1)); // 指数退避
                _queue.Enqueue((json, retries + 1));
            }
        }

        /// <summary>从 GameNetworkManager 传入的 webApiUrl 初始化 (ServerBootstrapper args)</summary>
        public static void Bootstrap(string webApiUrl)
        {
            if (Instance == null)
            {
                var go = new GameObject("SnapshotUploader");
                DontDestroyOnLoad(go);
                go.AddComponent<SnapshotUploader>();
            }
            Instance?.Configure(webApiUrl);
        }
    }
}
