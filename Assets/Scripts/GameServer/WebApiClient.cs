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
    }
}
