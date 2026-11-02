using System.Collections;
using UnityEngine;
using Mirror;

namespace ArpgGameServer
{
    /// <summary>
    /// 服务器启动器 — 读取命令行参数,启动GameNetworkManager
    /// 用法: ArpgGameServer.x86_64 -batchmode -nographics -port 7777 -webApiUrl http://localhost:5132
    ///       [-nodeId cn-bj-01] [-publicHost 10.0.0.1] [-nodeKey dev-node-key]
    /// 阶段11: 启动时向 WebAPI 注册节点(绑定开线), 每10秒心跳(负载=连接数), 退出时优雅注销。
    /// 心跳断绝 → WebAPI 摘除分线并把会话转 Detached; 节点重启重新注册即恢复。
    /// </summary>
    public class ServerBootstrapper : MonoBehaviour
    {
        [Header("服务器配置")]
        public int port = 7777;
        public string webApiUrl = "http://localhost:5132";
        public int maxRooms = 50;

        [Header("阶段11 节点身份 (Docker动态开线)")]
        public string nodeId = "";          // 空=设备唯一ID; 集群部署用 -nodeId 指定
        public string publicHost = "127.0.0.1"; // 客户端可直连地址 (-publicHost)
        public string nodeKey = "dev-node-key"; // 与 WebAPI appsettings Node:Key 一致 (-nodeKey)
        public int heartbeatIntervalSeconds = 10;

        /// <summary>单例 — 供快照上传器等读取 webApiUrl</summary>
        public static ServerBootstrapper Instance { get; private set; }

        private bool _registered;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

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
                if (args[i] == "-nodeId")
                    nodeId = args[i + 1];
                if (args[i] == "-publicHost")
                    publicHost = args[i + 1];
                if (args[i] == "-nodeKey")
                    nodeKey = args[i + 1];
            }
            if (string.IsNullOrEmpty(nodeId))
                nodeId = SystemInfo.deviceUniqueIdentifier;

            Debug.Log($"[GameServer] Port={port}, WebApiUrl={webApiUrl}, MaxRooms={maxRooms}, NodeId={nodeId}, PublicHost={publicHost}");

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

            // 阶段11: 注册节点 + 心跳循环 (注册失败不影响本节点开服, 只是没有分线分配)
            StartCoroutine(RegisterAndHeartbeatLoop());
        }

        private IEnumerator RegisterAndHeartbeatLoop()
        {
            // 注册 (WebAPI不可用时重试, 期间不接受分线分配)
            while (!_registered)
            {
                var done = false;
                yield return WebApiClient.Instance.RegisterNode(nodeId, publicHost, port, maxRooms, 1, nodeKey,
                    (ok, lineId) =>
                    {
                        done = true;
                        _registered = ok;
                        if (ok) Debug.Log($"[GameServer] 节点注册成功, 绑定分线 lineId={lineId}");
                    });
                if (done && _registered) break;
                Debug.LogWarning($"[GameServer] 节点注册失败, 10秒后重试 (webApiUrl={webApiUrl})");
                yield return new WaitForSeconds(10f);
            }

            // 心跳循环: load = 当前Mirror连接数
            while (true)
            {
                int load = Mirror.NetworkServer.connections.Count;
                yield return WebApiClient.Instance.HeartbeatNode(nodeId, load, true, nodeKey);
                yield return new WaitForSeconds(heartbeatIntervalSeconds);
            }
        }

        private void OnApplicationQuit()
        {
            // 优雅注销: WebAPI 立即摘线; 宕机场景由 WebAPI 心跳超时兜底
            if (_registered && WebApiClient.Instance != null)
                StartCoroutine(WebApiClient.Instance.DeregisterNode(nodeId, nodeKey));
        }
    }
}
