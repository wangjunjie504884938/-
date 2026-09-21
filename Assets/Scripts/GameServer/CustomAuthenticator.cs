using UnityEngine;
using Mirror;
using System.Collections;
using ArpgShared;

namespace ArpgGameServer
{
    /// <summary>
    /// 自定义认证器 — 客户端连接时通过Web API验证JWT Token
    /// </summary>
    public class CustomAuthenticator : NetworkAuthenticator
    {
        public string webApiUrl = "http://39.107.141.107:5132";

        // 待验证的连接和Token
        private readonly System.Collections.Concurrent.ConcurrentDictionary<int, string> _pendingAuth = new();

        public override void OnStartServer()
        {
            NetworkServer.RegisterHandler<AuthRequestMessage>(OnAuthRequest, false);
        }

        public override void OnStartClient()
        {
            NetworkClient.RegisterHandler<AuthResponseMessage>(OnAuthResponse, false);
        }

        public override void OnServerAuthenticate(NetworkConnectionToClient conn)
        {
            // 等待客户端发送AuthRequestMessage
        }

        public override void OnClientAuthenticate()
        {
            // Token在PlayerPrefs中以AES加密存储，必须解密后再发送（Crypto实现在ArpgShared，与CloudSaveManager共用）
            var token = ArpgShared.AuthTokenCrypto.GetStoredToken();

            NetworkClient.Send(new AuthRequestMessage { token = token });
        }

        /// <summary>处理客户端认证请求 — 通过Web API验证JWT</summary>
        private void OnAuthRequest(NetworkConnectionToClient conn, AuthRequestMessage msg)
        {
            if (string.IsNullOrEmpty(msg.token))
            {
                RejectConnection(conn, "Token为空");
                return;
            }

            // 记录待验证连接
            _pendingAuth[conn.connectionId] = msg.token;

            // 通过WebApiClient验证Token（异步协程）
            var webClient = GetWebApiClient();
            if (webClient != null)
            {
                StartCoroutine(ValidateAndRespond(conn, msg.token, webClient));
            }
            else
            {
                // WebApiClient不可用时降级为格式检查
                FormatCheckFallback(conn, msg.token);
            }
        }

        private System.Collections.IEnumerator ValidateAndRespond(NetworkConnectionToClient conn, string token, WebApiClient webClient)
        {
            bool isValid = false;
            bool done = false;

            yield return webClient.ValidateToken(token, result =>
            {
                isValid = result;
                done = true;
            });

            // 等待回调完成（最多5秒）
            float timeout = 5f;
            while (!done && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            if (!done)
            {
                // 验证超时 — 降级为格式检查
                FormatCheckFallback(conn, token);
                yield break;
            }

            if (isValid)
            {
                AcceptConnection(conn);
                Debug.Log($"[Auth] Web API验证成功: connId={conn.connectionId}");
            }
            else
            {
                RejectConnection(conn, "Token验证失败(Web API)");
                _pendingAuth.TryRemove(conn.connectionId, out _);
            }
        }

        private void FormatCheckFallback(NetworkConnectionToClient conn, string token)
        {
            if (token.Length > 20 && token.StartsWith("eyJ"))
            {
                AcceptConnection(conn);
                Debug.Log($"[Auth] 格式检查通过(降级): connId={conn.connectionId}");
            }
            else
            {
                RejectConnection(conn, "Token格式无效");
            }
        }

        private void AcceptConnection(NetworkConnectionToClient conn)
        {
            _pendingAuth.TryRemove(conn.connectionId, out _);
            conn.Send(new AuthResponseMessage { success = true });
            conn.isAuthenticated = true;
            ServerAccept(conn);
        }

        private void RejectConnection(NetworkConnectionToClient conn, string reason)
        {
            _pendingAuth.TryRemove(conn.connectionId, out _);
            conn.Send(new AuthResponseMessage { success = false, error = reason });
            conn.isAuthenticated = false;
            conn.Disconnect();
            Debug.LogWarning($"[Auth] 认证失败: connId={conn.connectionId}, 原因={reason}");
        }

        private WebApiClient GetWebApiClient()
        {
            if (WebApiClient.Instance != null) return WebApiClient.Instance;

            // 自动创建
            var go = new GameObject("WebApiClient");
            var client = go.AddComponent<WebApiClient>();
            client.Initialize(webApiUrl);
            return client;
        }

        /// <summary>客户端处理认证响应</summary>
        private void OnAuthResponse(AuthResponseMessage msg)
        {
            if (msg.success)
            {
                ClientAccept();
                Debug.Log("[Auth] 服务器认证成功");
            }
            else
            {
                Debug.LogError($"[Auth] 服务器认证失败: {msg.error}");
                NetworkClient.Disconnect();
            }
        }
    }
}
