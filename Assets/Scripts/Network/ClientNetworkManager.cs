using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

/// <summary>
/// 客户端网络管理器 — 连接Game Server/发送移动和技能消息/接收状态快照
/// 与Mirror的NetworkManager配合使用
/// </summary>
public class ClientNetworkManager : MonoBehaviour
{
    public static ClientNetworkManager Instance { get; private set; }

    [Header("Game Server配置")]
    public string gameServerHost = "39.107.141.107";
    public int gameServerPort = 7777;
    public bool isConnected = false;

    [Header("状态快照")]
    public ArpgShared.StateSnapshotMessage lastSnapshot;

    [Header("客户端预测")]
    public Vector2 predictedPosition;
    public Vector2 predictedVelocity;
    public float lastSendTime;
    public float sendInterval = 0.1f; // 10次/秒

    [Header("远程实体渲染")]
    private readonly Dictionary<int, RemotePlayerRenderer> _remotePlayers = new();
    private readonly Dictionary<int, RemoteMonsterRenderer> _remoteMonsters = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>连接到Game Server</summary>
    public void Connect(string roomId, int port)
    {
        gameServerPort = port;
        // 这里需要Mirror的NetworkClient.Connect
        // NetworkClient.Connect($"{gameServerHost}:{gameServerPort}");
                    GameLog.Log($"[Client] 连接Game Server: {gameServerHost}:{gameServerPort}, Room={roomId}");
        isConnected = true;
    }

    /// <summary>断开连接</summary>
    public void Disconnect()
    {
        // NetworkClient.Disconnect();
        isConnected = false;

        // 清理远程实体
        foreach (var kvp in _remotePlayers)
            if (kvp.Value != null) Destroy(kvp.Value.gameObject);
        foreach (var kvp in _remoteMonsters)
            if (kvp.Value != null) Destroy(kvp.Value.gameObject);
        _remotePlayers.Clear();
        _remoteMonsters.Clear();

                    GameLog.Log("[Client] 断开Game Server连接");
    }

    /// <summary>发送移动消息 (每帧调用,实际发送频率由sendInterval控制)</summary>
    public void SendMove(Vector2 position, Vector2 velocity)
    {
        if (!isConnected) return;
        if (Time.time - lastSendTime < sendInterval) return;

        lastSendTime = Time.time;
        predictedPosition = position;
        predictedVelocity = velocity;

        var msg = new ArpgShared.MoveMessage
        {
            playerId = CloudSaveManager.Instance?.ActiveSlot ?? 0,
            position = position,
            velocity = velocity,
            timestamp = Time.time
        };

        // NetworkClient.Send(msg);
    }

    /// <summary>发送技能释放消息</summary>
    public void SendSkillCast(int skillId, Vector2 targetPos)
    {
        if (!isConnected) return;

        var msg = new ArpgShared.SkillCastMessage
        {
            playerId = CloudSaveManager.Instance?.ActiveSlot ?? 0,
            skillId = skillId,
            targetPosition = targetPos,
            timestamp = Time.time
        };

        // NetworkClient.Send(msg);
    }

    /// <summary>处理服务器状态快照</summary>
    public void OnSnapshotReceived(ArpgShared.StateSnapshotMessage snapshot)
    {
        lastSnapshot = snapshot;

        // 渲染远程玩家
        if (snapshot.players != null)
        {
            int myId = CloudSaveManager.Instance?.ActiveSlot ?? 0;
            foreach (var ps in snapshot.players)
            {
                if (ps.playerId == myId)
                {
                    // 自己: 预测校正
                    float dist = Vector2.Distance(predictedPosition, ps.position);
                    if (dist > 1.5f)
                    {
                        var player = GameManager.Instance?.Player;
                        if (player != null)
                        {
                            player.transform.position = ps.position;
                            predictedPosition = ps.position;
                        }
                    }
                    continue;
                }

                // 远程玩家
                if (!_remotePlayers.TryGetValue(ps.playerId, out var renderer))
                {
                    var obj = new GameObject($"RemotePlayer_{ps.playerId}");
                    renderer = obj.AddComponent<RemotePlayerRenderer>();
                    renderer.Initialize(ps.playerId, ps.username, ps.classType, ps.level);
                    _remotePlayers[ps.playerId] = renderer;
                }
                renderer.UpdateFromState(ps);
            }
        }

        // 渲染远程怪物
        if (snapshot.monsters != null)
        {
            foreach (var ms in snapshot.monsters)
            {
                if (!_remoteMonsters.TryGetValue(ms.monsterId, out var renderer))
                {
                    var obj = new GameObject($"RemoteMonster_{ms.monsterId}");
                    renderer = obj.AddComponent<RemoteMonsterRenderer>();
                    renderer.Initialize(ms.monsterId, ms.monsterType, ms.position);
                    _remoteMonsters[ms.monsterId] = renderer;
                }
                renderer.UpdateFromState(ms);
            }
        }
    }

    /// <summary>处理结算消息</summary>
    public void OnSettlementReceived(ArpgShared.SettlementMessage msg)
    {
        if (msg.victory)
        {
            var player = GameManager.Instance?.Player;
            if (player != null)
            {
                player.Stats.AddGold(msg.goldReward);
                player.GainXp(msg.xpReward);
            }
            RuntimePlayerData.Instance?.RefreshFromServer(msg.newHighestStage, null);
        }
        Disconnect();
                    GameLog.Log($"[Client] 收到结算: victory={msg.victory}, gold={msg.goldReward}, xp={msg.xpReward}");
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
