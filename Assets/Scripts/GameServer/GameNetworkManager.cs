using UnityEngine;
using Mirror;
using Arch;

namespace ArpgGameServer
{
    /// <summary>
    /// 游戏网络管理器 — 管理服务器启动/房间管理/玩家连接
    /// 继承Mirror的NetworkManager
    /// </summary>
    public class GameNetworkManager : NetworkManager
    {
        public int maxRooms = 50;

        // ====== RPC 限流 (规划阶段05) ======
        private readonly RpcRateLimiterRegistry _moveLimiter = new(1.0, 30, 5);   // 移动: 30条/秒, 违规5次断开
        private readonly RpcRateLimiterRegistry _skillLimiter = new(1.0, 15, 3);  // 技能: 15条/秒, 违规3次断开

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
            _moveLimiter.RemoveConnection(conn.connectionId);
            _skillLimiter.RemoveConnection(conn.connectionId);

            // 从房间中移除
            var player = conn.identity?.GetComponent<NetworkedPlayer>();
            if (player != null)
            {
                RoomManager.Instance?.RemovePlayerFromRoom(player);
            }

            base.OnServerDisconnect(conn);
        }

        /// <summary>处理移动消息 — 先限流, 超频直接丢弃 (规划阶段05: 高频RPC不无界排队)</summary>
        private void OnMoveMessage(NetworkConnectionToClient conn, ArpgShared.MoveMessage msg)
        {
            var verdict = _moveLimiter.OnMessage(conn.connectionId, "Move", Time.unscaledTimeAsDouble);
            if (verdict == 2)
            {
                Debug.LogWarning($"[AntiCheat] connId={conn.connectionId} 移动消息超频达阈值, 断开连接");
                conn.Disconnect();
                return;
            }
            if (verdict == 1) return; // 超频丢弃

            var player = conn.identity?.GetComponent<NetworkedPlayer>();
            if (player != null)
            {
                player.OnServerMove(msg);
            }
        }

        /// <summary>处理技能释放消息 — 限流 + 转发</summary>
        private void OnSkillCastMessage(NetworkConnectionToClient conn, ArpgShared.SkillCastMessage msg)
        {
            var verdict = _skillLimiter.OnMessage(conn.connectionId, "Skill", Time.unscaledTimeAsDouble);
            if (verdict == 2)
            {
                Debug.LogWarning($"[AntiCheat] connId={conn.connectionId} 技能消息超频达阈值, 断开连接");
                conn.Disconnect();
                return;
            }
            if (verdict == 1) return; // 超频丢弃

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
