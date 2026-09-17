using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using ArpgShared;

/// <summary>
/// 客户端网络管理器 — 连接Game Server/发送移动和技能消息/接收状态快照
/// 接入Mirror NetworkClient，支持断线自动重连3次
/// </summary>
public partial class ClientNetworkManager : MonoBehaviour
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

    [Header("重连配置")]
    public int maxReconnectAttempts = 3;
    public float reconnectDelay = 2f;

    [Header("远程实体渲染")]
    private readonly Dictionary<int, RemotePlayerRenderer> _remotePlayers = new();
    private readonly Dictionary<int, RemoteMonsterRenderer> _remoteMonsters = new();

    // 房间信息
    private string _currentRoomId;
    private int _reconnectAttempts;
    private bool _intentionalDisconnect;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        RegisterMessageHandlers();
    }

    /// <summary>注册Mirror消息处理器</summary>
    private void RegisterMessageHandlers()
    {
        NetworkClient.RegisterHandler<ArpgShared.StateSnapshotMessage>(OnSnapshotMessage);
        NetworkClient.RegisterHandler<ArpgShared.SettlementMessage>(OnSettlementMessage);
    }

    /// <summary>连接到Game Server</summary>
    public void Connect(string roomId, int port)
    {
        if (isConnected)
        {
            GameLog.Log("[Client] 已连接，跳过重复连接");
            return;
        }

        gameServerPort = port;
        _currentRoomId = roomId;
        _reconnectAttempts = 0;
        _intentionalDisconnect = false;

        string serverAddress = $"{gameServerHost}:{gameServerPort}";
        GameLog.Log($"[Client] 连接Game Server: {serverAddress}, Room={roomId}");

        NetworkClient.Connect(serverAddress);
    }

    /// <summary>断开连接（主动）</summary>
    public void Disconnect()
    {
        _intentionalDisconnect = true;
        isConnected = false;

        if (NetworkClient.isConnected)
            NetworkClient.Disconnect();

        // 清理远程实体
        ClearRemoteEntities();

        GameLog.Log("[Client] 断开Game Server连接（主动）");
    }

    /// <summary>清理远程实体</summary>
    private void ClearRemoteEntities()
    {
        foreach (var kvp in _remotePlayers)
            if (kvp.Value != null) Destroy(kvp.Value.gameObject);
        foreach (var kvp in _remoteMonsters)
            if (kvp.Value != null) Destroy(kvp.Value.gameObject);
        _remotePlayers.Clear();
        _remoteMonsters.Clear();
    }

    /// <summary>Mirror连接成功回调（由NetworkManager的ClientConnect触发）</summary>
    public void OnClientConnected()
    {
        isConnected = true;
        _reconnectAttempts = 0;
        GameLog.Log($"[Client] 已连接Game Server: {_currentRoomId}");
    }

    /// <summary>Mirror连接断开回调 — 自动重连</summary>
    public void OnClientDisconnected()
    {
        isConnected = false;

        if (_intentionalDisconnect)
        {
            GameLog.Log("[Client] 主动断开，不重连");
            return;
        }

        // 自动重连
        if (_reconnectAttempts < maxReconnectAttempts)
        {
            _reconnectAttempts++;
            GameLog.LogWarning($"[Client] 断线，{_reconnectAttempts}/{maxReconnectAttempts}次重连（{reconnectDelay}秒后）...");
            StartCoroutine(ReconnectAfterDelay());
        }
        else
        {
            GameLog.LogError($"[Client] 重连{maxReconnectAttempts}次失败，放弃连接");
            ClearRemoteEntities();
        }
    }

    private IEnumerator ReconnectAfterDelay()
    {
        yield return new WaitForSeconds(reconnectDelay);

        if (_intentionalDisconnect || isConnected) yield break;

        string serverAddress = $"{gameServerHost}:{gameServerPort}";
        GameLog.Log($"[Client] 重连到 {serverAddress}...");
        NetworkClient.Connect(serverAddress);
    }

    // ====== 发送消息 ======

    /// <summary>发送移动消息</summary>
    public void SendMove(Vector2 position, Vector2 velocity)
    {
        if (!isConnected || !NetworkClient.isConnected) return;
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

        NetworkClient.Send(msg);
    }

    /// <summary>发送技能释放消息</summary>
    public void SendSkillCast(int skillId, Vector2 targetPos)
    {
        if (!isConnected || !NetworkClient.isConnected) return;

        var msg = new ArpgShared.SkillCastMessage
        {
            playerId = CloudSaveManager.Instance?.ActiveSlot ?? 0,
            skillId = skillId,
            targetPosition = targetPos,
            timestamp = Time.time
        };

        NetworkClient.Send(msg);
    }

    // ====== 接收消息 ======

    /// <summary>Mirror消息处理 — 状态快照</summary>
    private void OnSnapshotMessage(ArpgShared.StateSnapshotMessage snapshot)
    {
        OnSnapshotReceived(snapshot);
    }

    /// <summary>Mirror消息处理 — 结算</summary>
    private void OnSettlementMessage(ArpgShared.SettlementMessage msg)
    {
        OnSettlementReceived(msg);
    }

    /// <summary>处理服务器状态快照</summary>
    public void OnSnapshotReceived(ArpgShared.StateSnapshotMessage snapshot)
    {
        lastSnapshot = snapshot;

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

                if (!_remotePlayers.TryGetValue(ps.playerId, out var renderer))
                {
                    var obj = new GameObject($"RemotePlayer_{ps.playerId}");
                    renderer = obj.AddComponent<RemotePlayerRenderer>();
                    renderer.Initialize(ps.playerId, ps.username, ps.classType, ps.level);
                    _remotePlayers[ps.playerId] = renderer;
                }
                renderer.UpdateFromState(ps);
            }

            // 清理已离开的远程玩家
            CleanStaleRemotePlayers(snapshot.players);
        }

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

    /// <summary>清理快照中不存在的远程玩家</summary>
    private void CleanStaleRemotePlayers(ArpgShared.PlayerState[] currentPlayers)
    {
        var staleIds = new List<int>();
        foreach (var kvp in _remotePlayers)
        {
            bool found = false;
            foreach (var ps in currentPlayers)
            {
                if (ps.playerId == kvp.Key) { found = true; break; }
            }
            if (!found) staleIds.Add(kvp.Key);
        }
        foreach (var id in staleIds)
        {
            if (_remotePlayers.Remove(id) && _remotePlayers.TryGetValue(id, out var renderer) && renderer != null)
                Destroy(renderer.gameObject);
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
