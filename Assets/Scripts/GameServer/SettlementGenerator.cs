using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ArpgShared;

namespace ArpgGameServer
{
    /// <summary>
    /// 结算生成器 — Boss死亡后计算每个玩家的奖励并推送给Web API
    /// </summary>
    public class SettlementGenerator : MonoBehaviour
    {
        public static SettlementGenerator Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <summary>生成结算数据并推送给Web API</summary>
        public IEnumerator GenerateAndPush(GameRoom room)
        {
            Debug.Log($"[Settlement] 开始结算房间 {room.RoomId}");

            float timeUsed = (float)(System.DateTime.UtcNow - room.CreatedAt).TotalSeconds;

            // 计算总奖励
            var (baseGold, speedBonus, fullHpBonus) = CombatCalculator.CalculateGoldReward(
                room.DungeonId, timeUsed,
                room.Players.Count > 0 ? room.Players[0].currentHp : 0,
                room.Players.Count > 0 ? room.Players[0].maxHp : 1);

            int totalGold = baseGold + speedBonus + fullHpBonus;
            int totalXp = CombatCalculator.CalculateXpReward(room.DungeonId, 40);

            // 生成掉落
            var drops = new List<DropInfo>();
            int dropCount = 3;
            for (int i = 0; i < dropCount; i++)
            {
                drops.Add(new DropInfo
                {
                    rarity = CombatCalculator.RollRarity(),
                    slotType = Random.Range(0, 3),
                    dungeonLevel = room.DungeonId + 1
                });
            }

            // 构建结算数据
            var settlement = new SettlementData
            {
                roomId = room.RoomId,
                dungeonId = room.DungeonId,
                victory = true,
                timeUsed = timeUsed,
                drops = drops,
            };

            // 为每个玩家生成奖励
            foreach (var player in room.Players)
            {
                if (player == null) continue;
                settlement.playerRewards.Add(new PlayerReward
                {
                    playerId = player.playerId,
                    username = player.playerName,
                    goldReward = totalGold / Mathf.Max(1, room.Players.Count) + player.kills * 5,
                    xpReward = totalXp / Mathf.Max(1, room.Players.Count) + player.damageDealt / 100,
                    kills = player.kills,
                    damageDealt = player.damageDealt,
                    damageTaken = player.damageTaken,
                });
            }

            // 分配奖励
            CombatCalculator.DistributeRewards(settlement, totalGold, totalXp, room.Players.Count);

            // 推送给Web API
            yield return StartCoroutine(WebApiClient.Instance.PushSettlement(settlement));

            // 广播结算消息给所有客户端
            BroadcastSettlement(room, settlement);

            Debug.Log($"[Settlement] 房间 {room.RoomId} 结算完成, 总金币={totalGold}, 总经验={totalXp}");
        }

        /// <summary>广播结算消息给房间内所有客户端</summary>
        private void BroadcastSettlement(GameRoom room, SettlementData data)
        {
            // 通过Mirror广播结算消息
            foreach (var player in room.Players)
            {
                if (player == null || player.connectionToClient == null) continue;
                var msg = new SettlementMessage
                {
                    victory = data.victory,
                    goldReward = data.playerRewards.Find(r => r.playerId == player.playerId)?.goldReward ?? 0,
                    xpReward = data.playerRewards.Find(r => r.playerId == player.playerId)?.xpReward ?? 0,
                    drops = data.drops.ToArray(),
                    newHighestStage = data.victory ? data.dungeonId : -1,
                };
                // player.connectionToClient.Send(msg);
            }

            // 延迟销毁房间
            StartCoroutine(DestroyRoomAfterDelay(room.RoomId, 5f));
        }

        private IEnumerator DestroyRoomAfterDelay(string roomId, float delay)
        {
            yield return new WaitForSeconds(delay);
            RoomManager.Instance?.DestroyRoom(roomId);
        }
    }
}
