using UnityEngine;
using Mirror;

namespace ArpgGameServer
{
    /// <summary>
    /// 自定义认证器 — 客户端连接时验证JWT Token
    /// </summary>
    public class CustomAuthenticator : NetworkAuthenticator
    {
        public string webApiUrl = "http://localhost:5132";

        public override void OnStartServer()
        {
            // 注册认证消息
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
            // 客户端发送Token
            var token = PlayerPrefs.GetString("ARPG_CloudToken", "");

            NetworkClient.Send(new AuthRequestMessage { token = token });
        }

        /// <summary>处理客户端认证请求</summary>
        private void OnAuthRequest(NetworkConnectionToClient conn, AuthRequestMessage msg)
        {
            if (string.IsNullOrEmpty(msg.token))
            {
                conn.Send(new AuthResponseMessage { success = false, error = "Token为空" });
                conn.isAuthenticated = false;
                return;
            }

            // TODO: 向Web API验证Token有效性
            // 简化: 直接检查Token格式
            if (msg.token.Length > 20)
            {
                conn.Send(new AuthResponseMessage { success = true });
                ServerAccept(conn);
                Debug.Log($"[Auth] 认证成功: connId={conn.connectionId}");
            }
            else
            {
                conn.Send(new AuthResponseMessage { success = false, error = "Token无效" });
                conn.isAuthenticated = false;
                conn.Disconnect();
                Debug.LogWarning($"[Auth] 认证失败: connId={conn.connectionId}");
            }
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

    /// <summary>认证请求消息</summary>
    public struct AuthRequestMessage : NetworkMessage
    {
        public string token;
    }

    /// <summary>认证响应消息</summary>
    public struct AuthResponseMessage : NetworkMessage
    {
        public bool success;
        public string error;
    }
}
