using UnityEngine;
using Mirror;

namespace ArpgGameServer
{
    /// <summary>
    /// 游戏网络管理器 — 管理服务器启动/房间管理/玩家连接
    /// 继承Mirror的NetworkManager
    /// </summary>
    public class GameNetworkManager : NetworkManager
    {
        public int maxRooms = 50;

        public override void OnStartServer()
        {
            base.OnStartServer();
            Debug.Log("[Network] 服务器已启动");

            // 注册消息处理器
            NetworkServer.RegisterHandler<ArpgShared.MoveMessage>(OnMoveMessage);
            NetworkServer.RegisterHandler<ArpgShared.SkillCastMessage>(OnSkillCastMessage);

                // 启动状态快照广播
                StartCoroutine(SnapshotLoop());
        }

        public override void OnStopServer()
        {
            Debug.Log("[Network] 服务器已停止");
            RoomManager.Instance?.ClearAllRooms();
            base.OnStopServer();
        }

        public override void OnServerConnect(NetworkConnectionToClient conn)
        {
            base.OnServerConnect(conn);
            Debug.Log($"[Network] 玩家连接: connId={conn.connectionId}");
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            Debug.Log($"[Network] 玩家断开: connId={conn.connectionId}");

            // 从房间中移除
            var player = conn.identity?.GetComponent<NetworkedPlayer>();
            if (player != null)
            {
                RoomManager.Instance?.RemovePlayerFromRoom(player);
            }

            base.OnServerDisconnect(conn);
        }

        /// <summary>处理移动消息</summary>
        private void OnMoveMessage(NetworkConnectionToClient conn, ArpgShared.MoveMessage msg)
        {
            var player = conn.identity?.GetComponent<NetworkedPlayer>();
            if (player != null)
            {
                player.OnServerMove(msg);
            }
        }

        /// <summary>处理技能释放消息</summary>
        private void OnSkillCastMessage(NetworkConnectionToClient conn, ArpgShared.SkillCastMessage msg)
        {
            var player = conn.identity?.GetComponent<NetworkedPlayer>();
            if (player != null)
            {
                player.OnServerSkillCast(msg);
            }
        }

        /// <summary>状态快照广播 — 每秒10次(100ms间隔)</summary>
        private System.Collections.IEnumerator SnapshotLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(0.1f);

                if (RoomManager.Instance != null)
                {
                    var snapshot = RoomManager.Instance.BuildSnapshot();
                    NetworkServer.SendToAll(snapshot);
                }
            }
        }
    }
}
