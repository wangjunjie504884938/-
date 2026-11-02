using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace ArpgGameServer
{
    /// <summary>
    /// 地牢快照数据 (规划阶段06) — 纯数据可序列化, 禁止 GameObject/Transform (只取 SyncVar 权威值)。
    /// 分级: P0普通5s / P1精英Boss 3s / P2高难1s (Tier=0/1/2)。
    /// </summary>
    public class RoomSnapshotData
    {
        public string instanceId;
        public int configVersion = 1;
        public int templateId;          // 地图主题
        public int riftLayer;           // 秘境层数 (1-based)
        public int randomSeed;
        public int wave;                // 当前波次
        public float survivalTime;      // 已生存时间 (寿元结算)
        public string state;            // Fighting
        public int tier;                // 0=P0 1=P1 2=P2

        public List<MemberEntry> members = new();
        public List<MonsterSnapshotEntry> monsters = new();
        public List<string> rewardFlags = new();

        public string ToJson() => JsonConvert.SerializeObject(this);
        public static RoomSnapshotData FromJson(string json) => JsonConvert.DeserializeObject<RoomSnapshotData>(json);
    }

    public class MemberEntry
    {
        public long playerId;
        public float x, y;
        public int hp, maxHp;
        public bool downed;
    }

    public class MonsterSnapshotEntry
    {
        public int spawnId;
        public int monsterType;
        public int hp, maxHp;
        public float x, y;
    }

    /// <summary>
    /// 快照捕获 — 从 GameRoom 提取纯数据 (规划阶段06: 只序列化 SyncVar 权威值)。
    /// </summary>
    public static class RoomSnapshotBuilder
    {
        public static RoomSnapshotData Capture(GameRoom room, int tier, int riftLayer, int randomSeed, float survivalTime)
        {
            var snap = new RoomSnapshotData
            {
                instanceId = room.RoomId,
                templateId = room.DungeonId,
                riftLayer = riftLayer,
                wave = 1,
                survivalTime = survivalTime,
                state = room.State.ToString(),
                tier = tier,
            };

            foreach (var p in room.Players)
            {
                if (p == null) continue;
                snap.members.Add(new MemberEntry
                {
                    playerId = p.playerId,
                    x = p.serverPosition.x,
                    y = p.serverPosition.y,
                    hp = p.currentHp,
                    maxHp = p.maxHp,
                    downed = p.isDead,
                });
            }

            foreach (var m in room.Monsters)
            {
                if (m == null) continue;
                snap.monsters.Add(new MonsterSnapshotEntry
                {
                    spawnId = m.monsterId,
                    monsterType = m.monsterType,
                    hp = m.currentHp,
                    maxHp = m.maxHp,
                    x = m.serverPosition.x,
                    y = m.serverPosition.y,
                });
            }
            return snap;
        }
    }
}
