using UnityEngine;
using Mirror;

namespace ArpgGameServer
{
    /// <summary>
    /// 服务器启动器 — 读取命令行参数,启动GameNetworkManager
    /// 用法: ArpgGameServer.x86_64 -batchmode -nographics -port 7777 -webApiUrl http://localhost:5132
    /// </summary>
    public class ServerBootstrapper : MonoBehaviour
    {
        [Header("服务器配置")]
        public int port = 7777;
        public string webApiUrl = "http://localhost:5132";
        public int maxRooms = 50;

        private void Awake()
        {
            // 解析命令行参数
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-port" && int.TryParse(args[i + 1], out int p))
                    port = p;
                if (args[i] == "-webApiUrl")
                    webApiUrl = args[i + 1];
                if (args[i] == "-maxRooms" && int.TryParse(args[i + 1], out int mr))
                    maxRooms = mr;
            }

            Debug.Log($"[GameServer] Port={port}, WebApiUrl={webApiUrl}, MaxRooms={maxRooms}");

            // 初始化WebApiClient
            WebApiClient.Instance?.Initialize(webApiUrl);

            // 配置并启动网络管理器
            var manager = GetComponent<GameNetworkManager>();
            if (manager != null)
            {
                manager.StartServer();
                Debug.Log($"[GameServer] 监听端口 {port}");
            }
            else
            {
                Debug.LogError("[GameServer] GameNetworkManager 未找到!");
            }
        }
    }
}
