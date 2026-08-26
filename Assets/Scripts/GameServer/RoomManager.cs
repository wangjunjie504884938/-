using System.Collections.Concurrent;
using System.Collections.Generic;
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
            Debug.Log($"[Room] 创建房间 {roomId}, 副本={dungeonId}, 人数={memberIds.Length}");

            // 初始化副本怪物
            SpawnDungeonMonsters(room);

            return room;
        }

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
            Debug.Log($"[Room] 玩家 {player.playerName} 加入房间 {roomId}");
            return true;
        }

        /// <summary>从房间移除玩家</summary>
        public void RemovePlayerFromRoom(NetworkedPlayer player)
        {
            if (player.CurrentRoom == null) return;
            player.CurrentRoom.Players.Remove(player);

            // 如果房间空了,销毁
            if (player.CurrentRoom.Players.Count == 0)
            {
                DestroyRoom(player.CurrentRoom.RoomId);
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
            }

            // Boss
            var bossObj = new GameObject("Boss");
            bossObj.transform.position = new Vector2(0, 8f);
            var boss = bossObj.AddComponent<NetworkedMonster>();
            boss.Initialize(999, 3, 500 + room.DungeonId * 100, new Vector2(0, 8f), room.DungeonId + 1);
            room.Monsters.Add(boss);
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
        public string RoomId;
        public int DungeonId;
        public int Difficulty;
        public int MaxMembers;
        public System.DateTime CreatedAt;
        public List<NetworkedPlayer> Players = new();
        public List<NetworkedMonster> Monsters = new();
        public bool IsSettled = false;
    }
}
