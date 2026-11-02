using System.Collections;
using System.Text;
using UnityEngine;
using ArpgShared;

namespace ArpgGameServer
{
    /// <summary>
    /// Web API客户端 — Game Server向Web API发送HTTP请求
    /// 用于推送结算数据
    /// </summary>
    public class WebApiClient : MonoBehaviour
    {
        public static WebApiClient Instance { get; private set; }

        private string _webApiUrl = "http://localhost:5132";

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <summary>初始化Web API地址</summary>
        public void Initialize(string webApiUrl)
        {
            _webApiUrl = webApiUrl;
            Debug.Log($"[WebApiClient] WebApiUrl={_webApiUrl}");
        }

        /// <summary>推送结算数据给Web API</summary>
        public IEnumerator PushSettlement(SettlementData data)
        {
            string json = JsonUtility.ToJson(data);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

            using (var req = new UnityEngine.Networking.UnityWebRequest($"{_webApiUrl}/api/team/complete", "POST"))
            {
                req.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 10;
                yield return req.SendWebRequest();

                if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    Debug.Log($"[WebApiClient] 结算推送成功: {req.downloadHandler.text}");
                }
                else
                {
                    Debug.LogError($"[WebApiClient] 结算推送失败: {req.error}");
                }
            }
        }

        /// <summary>验证Token有效性</summary>
        public IEnumerator ValidateToken(string token, System.Action<bool> callback)
        {
            using (var req = UnityEngine.Networking.UnityWebRequest.Get($"{_webApiUrl}/api/auth/validate"))
            {
                req.SetRequestHeader("Authorization", $"Bearer {token}");
                req.timeout = 5;
                yield return req.SendWebRequest();

                callback?.Invoke(req.result == UnityEngine.Networking.UnityWebRequest.Result.Success);
            }
        }

        // ========== 阶段11: 节点注册/心跳/注销 (Docker动态开线) ==========

        /// <summary>节点注册 (启动时): WebAPI 分配绑定分线</summary>
        public IEnumerator RegisterNode(string nodeId, string host, int mirrorPort, int capacity, int buildVersion,
            string nodeKey, System.Action<bool, int> callback)
        {
            var body = JsonUtility.ToJson(new NodeRegisterDto
            { nodeId = nodeId, host = host, mirrorPort = mirrorPort, httpPort = 0, capacity = capacity, buildVersion = buildVersion });
            yield return PostNode("api/node/register", body, nodeKey, (ok, text) =>
            {
                int lineId = 0;
                if (ok)
                {
                    var resp = JsonUtility.FromJson<NodeRegisterResp>(text);
                    lineId = resp != null ? resp.lineId : 0;
                }
                callback?.Invoke(ok, lineId);
            });
        }

        /// <summary>节点心跳 (每10秒): 刷新存活/负载; 心跳断绝超时会被 WebAPI 摘除, 恢复心跳即恢复</summary>
        public IEnumerator HeartbeatNode(string nodeId, int load, bool accepting, string nodeKey)
        {
            var body = JsonUtility.ToJson(new NodeHeartbeatDto { nodeId = nodeId, load = load, accepting = accepting });
            yield return PostNode("api/node/heartbeat", body, nodeKey, null);
        }

        /// <summary>节点优雅注销 (正常关闭时)</summary>
        public IEnumerator DeregisterNode(string nodeId, string nodeKey)
        {
            var body = JsonUtility.ToJson(new NodeDeregisterDto { nodeId = nodeId });
            yield return PostNode("api/node/deregister", body, nodeKey, null);
        }

        private IEnumerator PostNode(string path, string json, string nodeKey, System.Action<bool, string> callback)
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            using (var req = new UnityEngine.Networking.UnityWebRequest($"{_webApiUrl}/{path}", "POST"))
            {
                req.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("X-Node-Key", nodeKey);
                req.timeout = 5;
                yield return req.SendWebRequest();

                bool ok = req.result == UnityEngine.Networking.UnityWebRequest.Result.Success;
                if (!ok)
                    Debug.LogWarning($"[WebApiClient] {path} 失败: {req.error} {req.downloadHandler?.text}");
                callback?.Invoke(ok, req.downloadHandler?.text ?? "");
            }
        }

        [System.Serializable]
        private class NodeRegisterDto { public string nodeId; public string host; public int mirrorPort; public int httpPort; public int capacity; public int buildVersion; }
        [System.Serializable]
        private class NodeRegisterResp { public bool success; public string nodeId; public int lineId; public int capacity; }
        [System.Serializable]
        private class NodeHeartbeatDto { public string nodeId; public int load; public bool accepting; }
        [System.Serializable]
        private class NodeDeregisterDto { public string nodeId; }
    }
}
