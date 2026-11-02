using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Mirror;
using ArpgShared;

namespace ArpgGameServer
{
    /// <summary>
    /// 房间管理器 — 管理游戏房间的创建/销毁/查询
    /// 房间数据存储在内存中,通过字典管理
    /// </summary>
    public class RoomManager : MonoBehaviour
    {
        public static RoomManager Instance { get; private set; }

        private readonly ConcurrentDictionary<string, GameRoom> _rooms = new();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

                // 启动房间超时检查循环
                StartCoroutine(RoomTimeoutLoop());

                // 快照上传器 (规划阶段06) — 由 ServerBootstrapper.Configure
                SnapshotUploader.Bootstrap(ServerBootstrapper.Instance != null ? ServerBootstrapper.Instance.webApiUrl : "http://localhost:5132");
            }

        /// <summary>房间超时检查 — 每5秒检查一次</summary>
        private System.Collections.IEnumerator RoomTimeoutLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(5f);

                foreach (var kvp in _rooms)
                {
                    var room = kvp.Value;
                    if (room.IsClosed) continue;

                    // 快照上传 (规划阶段06) — Fighting 状态按 Tier 间隔捕获; 全员断线时停止(保留最后快照)
                    if (room.State == GameRoom.RoomState.Fighting && SnapshotUploader.Instance != null)
                    {
                        var sinceSnap = (System.DateTime.UtcNow - room.LastSnapshotUtc).TotalSeconds;
                        if (sinceSnap >= room.SnapshotIntervalSeconds)
                        {
                            var snap = RoomSnapshotBuilder.Capture(room, room.SnapshotTier, room.RiftLayer, room.RandomSeed, (float)(System.DateTime.UtcNow - room.CreatedAt).TotalSeconds);
                            SnapshotUploader.Instance.Enqueue(snap);
                            room.LastSnapshotUtc = System.DateTime.UtcNow;
                        }

                        // 断线保留: 全员断开超过5分钟 → 强制结算 (规则: 保留期内可回, 超时按规则结算)
                        if (room.Players.Count == 0 && room.AllDisconnectedAtUtc.HasValue
                            && (System.DateTime.UtcNow - room.AllDisconnectedAtUtc.Value).TotalSeconds >= 300)
                        {
                            Debug.LogWarning($"[Room:{room.RoomId}] 全员断线超5分钟, 保留最后快照并结算关闭");
                            room.TransitionTo(GameRoom.RoomState.Settled);
                            DestroyRoom(room.RoomId);
                            continue;
                        }
                    }

                    if (room.CheckTimeout())
                    {
                        Debug.LogWarning($"[Room:{room.RoomId}] 超时，当前状态: {room.State}");
                        if (room.State == GameRoom.RoomState.Waiting)
                        {
                            // 等待超时 — 关闭房间
                            room.TransitionTo(GameRoom.RoomState.Closed);
                            DestroyRoom(room.RoomId);
                        }
                        else if (room.State == GameRoom.RoomState.Fighting)
                        {
                            // 战斗超时 — 强制结算
                            room.TransitionTo(GameRoom.RoomState.Settled);
                            StartCoroutine(SettlementGenerator.Instance.GenerateAndPush(room));
                        }
                    }
                }
            }
        }

        /// <summary>创建房间</summary>
        public GameRoom CreateRoom(string roomId, int dungeonId, int[] memberIds, int difficulty, int maxMembers)
        {
            var room = new GameRoom
            {
                RoomId = roomId,
                DungeonId = dungeonId,
                Difficulty = difficulty,
                MaxMembers = maxMembers,
                CreatedAt = System.DateTime.UtcNow,
                Players = new List<NetworkedPlayer>(),
                Monsters = new List<NetworkedMonster>(),
            };

            _rooms[roomId] = room;
            Debug.Log($"[Room] 创建房间 {roomId}, 副本={dungeonId}, 人数={memberIds.Length}, 状态={room.State}");

            // 初始化副本怪物
            SpawnDungeonMonsters(room);

            return room;        }

        /// <summary>销毁房间</summary>
        public void DestroyRoom(string roomId)
        {
            if (_rooms.TryRemove(roomId, out var room))
            {
                // 销毁所有怪物
                foreach (var monster in room.Monsters)
                {
                    if (monster != null) Destroy(monster.gameObject);
                }
                Debug.Log($"[Room] 销毁房间 {roomId}");
            }
        }

        /// <summary>清空所有房间</summary>
        public void ClearAllRooms()
        {
            foreach (var kvp in _rooms)
            {
                foreach (var monster in kvp.Value.Monsters)
                {
                    if (monster != null) Destroy(monster.gameObject);
                }
            }
            _rooms.Clear();
            Debug.Log("[Room] 已清空所有房间");
        }

        /// <summary>添加玩家到房间</summary>
        public bool AddPlayerToRoom(string roomId, NetworkedPlayer player)
        {
            if (!_rooms.TryGetValue(roomId, out var room)) return false;
            if (room.Players.Count >= room.MaxMembers) return false;

            room.Players.Add(player);
            player.CurrentRoom = room;
            room.AllDisconnectedAtUtc = null; // 有人回来了, 清除断线保留计时
            room.GridMove(player.playerId, player.serverPosition); // AOI 网格同步
            Debug.Log($"[Room] 玩家 {player.playerName} 加入房间 {roomId}");
            return true;
        }

        /// <summary>从房间移除玩家</summary>
        public void RemovePlayerFromRoom(NetworkedPlayer player)
        {
            if (player.CurrentRoom == null) return;
            player.CurrentRoom.Players.Remove(player);

            // 断线保留 (规划阶段06): 移除后房间还有其他成员 → 队友继续战斗;
            // 房间空了 → 记录断线时间, 保留最后快照5分钟 (期间重连可恢复), 超时由循环结算关闭
            if (player.CurrentRoom.Players.Count == 0)
            {
                player.CurrentRoom.AllDisconnectedAtUtc = System.DateTime.UtcNow;
                Debug.LogWarning($"[Room:{player.CurrentRoom.RoomId}] 全员断线, 快照保留5分钟 (重连可恢复)");
            }
            player.CurrentRoom = null;
        }

        /// <summary>生成副本怪物</summary>
        private void SpawnDungeonMonsters(GameRoom room)
        {
            int monsterCount = 10 + room.DungeonId * 3;
            for (int i = 0; i < monsterCount; i++)
            {
                var pos = new Vector2(Random.Range(-8f, 8f), Random.Range(-5f, 5f));
                var monsterObj = new GameObject($"Monster_{i}");
                monsterObj.transform.position = pos;
                var monster = monsterObj.AddComponent<NetworkedMonster>();
                monster.Initialize(i, Random.Range(0, 3), 30 + room.DungeonId * 5, pos, room.DungeonId + 1);
                room.Monsters.Add(monster);
                room.GridMove(i, pos); // AOI 网格同步 (规划阶段07)
            }

            // Boss
            var bossObj = new GameObject("Boss");
            bossObj.transform.position = new Vector2(0, 8f);
            var boss = bossObj.AddComponent<NetworkedMonster>();
            boss.Initialize(999, 3, 500 + room.DungeonId * 100, new Vector2(0, 8f), room.DungeonId + 1);
            room.Monsters.Add(boss);
            room.GridMove(999, new Vector2(0, 8f)); // AOI 网格同步
        }

        /// <summary>构建状态快照</summary>
        public StateSnapshotMessage BuildSnapshot()
        {
            var snapshot = new StateSnapshotMessage
            {
                timestamp = (float)System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000f,
            };

            var allPlayers = new List<PlayerState>();
            var allMonsters = new List<MonsterState>();

            foreach (var room in _rooms.Values)
            {
                foreach (var p in room.Players)
                {
                    if (p != null)
                        allPlayers.Add(p.GetState());
                }
                foreach (var m in room.Monsters)
                {
                    if (m != null && !m.IsDead)
                        allMonsters.Add(m.GetState());
                }
            }

            snapshot.players = allPlayers.ToArray();
            snapshot.monsters = allMonsters.ToArray();

            return snapshot;
        }

        /// <summary>检查Boss是否死亡(触发结算)</summary>
        public GameRoom CheckBossDefeated()
        {
            foreach (var room in _rooms.Values)
            {
                var boss = room.Monsters.Find(m => m.monsterType == 3);
                if (boss != null && boss.IsDead && !room.IsSettled)
                {
                    room.IsSettled = true;
                    return room;
                }
            }
            return null;
        }
    }

    /// <summary>游戏房间</summary>
    public class GameRoom
    {
        // ====== 房间状态机 ======
        public enum RoomState { Waiting, Loading, Fighting, Settled, Closed }

        public string RoomId;
        public int DungeonId;
        public int Difficulty;
        public int MaxMembers;
        public System.DateTime CreatedAt;
        public List<NetworkedPlayer> Players = new();
        public List<NetworkedMonster> Monsters = new();
        public bool IsSettled = false;

        // ====== 快照字段 (规划阶段06: 地牢实例与快照) ======
        public int SnapshotTier;                                          // 0=P0普通(5s) 1=P1精英Boss(3s) 2=P2高难(1s)
        public System.DateTime LastSnapshotUtc = System.DateTime.MinValue; // 上次快照时间
        public System.DateTime? AllDisconnectedAtUtc;                     // 全员断线时刻 (5分钟保留计时)
        public List<string> RewardFlags = new();                          // 已发奖励标记 (幂等结算)
        public int RiftLayer = 1;                                         // 秘境层数
        public int RandomSeed;                                            // 地图随机种子 (重建一致性)

        // ====== AOI 空间网格 (规划阶段07) — 实体兴趣过滤查询 ======
        public readonly Arch.SpatialGrid Grid = new(20f);                 // 格子20米

        /// <summary>实体进网格 (玩家加入/怪物生成后调用)</summary>
        public void GridMove(long id, Vector2 pos) => Grid.Move(id, pos.x, pos.y);

        /// <summary>快照间隔秒 — 按 Tier 分级: P0=5s / P1=3s / P2=1s (规划阶段06)</summary>
        public int SnapshotIntervalSeconds => SnapshotTier switch { 1 => 3, 2 => 1, _ => 5 };

        /// <summary>按观察者构建 AOI 过滤快照 (规划阶段07: AOI外实体不进入消息; 观察者自己永远可见)</summary>
        public StateSnapshotMessage BuildSnapshotFor(long observerPlayerId, Vector2 observerPos, float aoiRadius)
        {
            var visibleIds = Grid.QueryCircle(observerPos.x, observerPos.y, aoiRadius);

            var snapshot = new StateSnapshotMessage
            {
                timestamp = (float)System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000f,
            };
            var players = new List<PlayerState>();
            var monsters = new List<MonsterState>();

            // 玩家: 观察者自己永远可见; 其他玩家需在AOI内
            foreach (var p in Players)
            {
                if (p == null) continue;
                if (p.playerId == observerPlayerId || visibleIds.Contains(p.playerId))
                    players.Add(p.GetState());
            }

            // 怪物: AOI内且存活
            foreach (var m in Monsters)
            {
                if (m == null || m.IsDead) continue;
                if (visibleIds.Contains(m.monsterId))
                    monsters.Add(m.GetState());
            }

            snapshot.players = players.ToArray();
            snapshot.monsters = monsters.ToArray();
            return snapshot;
        }

        public RoomState State { get; private set; } = RoomState.Waiting;
        public System.DateTime StateChangedAt { get; private set; } = System.DateTime.UtcNow;

        // 超时配置
        public const float WaitingTimeoutSeconds = 120f;   // 等待超时2分钟
        public const float FightingTimeoutSeconds = 600f;  // 战斗超时10分钟

        // 合法状态流转表
        private static readonly Dictionary<RoomState, RoomState[]> _validTransitions = new()
        {
            [RoomState.Waiting]  = new[] { RoomState.Loading, RoomState.Closed },
            [RoomState.Loading]  = new[] { RoomState.Fighting, RoomState.Closed },
            [RoomState.Fighting] = new[] { RoomState.Settled, RoomState.Closed },
            [RoomState.Settled]  = new[] { RoomState.Closed },
            [RoomState.Closed]   = System.Array.Empty<RoomState>(),
        };

        /// <summary>流转到新状态（校验合法性，非法流转返回false）</summary>
        public bool TransitionTo(RoomState newState)
        {
            if (!_validTransitions.TryGetValue(State, out var valid))
            {
                Debug.LogWarning($"[Room:{RoomId}] 非法状态流转: {State} → {newState}");
                return false;
            }
            if (!System.Array.Exists(valid, s => s == newState))
            {
                Debug.LogWarning($"[Room:{RoomId}] 不允许的状态流转: {State} → {newState}");
                return false;
            }

            Debug.Log($"[Room:{RoomId}] 状态: {State} → {newState}");
            State = newState;
            StateChangedAt = System.DateTime.UtcNow;

            if (newState == RoomState.Settled)
                IsSettled = true;

            return true;
        }

        /// <summary>检查是否超时（由RoomManager定时调用）</summary>
        public bool CheckTimeout()
        {
            var elapsed = (System.DateTime.UtcNow - StateChangedAt).TotalSeconds;
            return State switch
            {
                RoomState.Waiting  => elapsed > WaitingTimeoutSeconds,
                RoomState.Fighting => elapsed > FightingTimeoutSeconds,
                _ => false,
            };
        }

        /// <summary>所有玩家是否就绪</summary>
        public bool AllPlayersReady => Players.Count > 0 && Players.All(p => p != null);

        /// <summary>房间是否已关闭</summary>
        public bool IsClosed => State == RoomState.Closed;
    }
}
