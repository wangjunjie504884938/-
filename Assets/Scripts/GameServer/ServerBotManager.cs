using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Mirror;
using Arch;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

namespace ArpgGameServer
{
    /// <summary>
    /// 服务器内Bot压测 (规划阶段12) — 进程内直接驱动真实服务器逻辑, 不走真实网络栈:
    /// N个bot玩家(带NetworkIdentity的服务器对象, SyncVar脏位真实受压) 随机移动(经ServerAuthority验证+AOI网格)
    /// + 每2秒攻击最近怪(LOS射线/伤害计算/对象池/怪物AI) + 15Hz对每个bot调BuildSnapshotFor采样(AOI构建耗时+序列化体积=带宽口径)。
    /// 采样: 每5秒输出 帧耗时avg/max(目标<16.7ms=CPU<60%口径) / 快照ms / 快照字节×15Hz×N=带宽估算(目标<10Mbps) / GC增量(目标接近0) / AOI实体数。
    /// 启动: 专用服务器命令行 -loadbots 50 ; 或运行时调 StartBots(n)/StopBots()。
    /// 怪物耗损: 每20秒清理死亡怪物并按批次ID补生成, 保持持续满负载。
    /// </summary>
    public class ServerBotManager : MonoBehaviour
    {
        public static ServerBotManager Instance { get; private set; }

        private readonly List<NetworkedPlayer> _bots = new();
        private Vector2[] _botTargets = System.Array.Empty<Vector2>();
        private float[] _nextAttack = System.Array.Empty<float>();
        private GameRoom _room;
        private int _botCount;
        private int _initialMonsters;
        private float _tickAccum;
        private float _sampleTimer;
        private float _respawnTimer;
        private int _respawnBatch = 1;
        private bool _running;

        // 采样累计窗口 (5秒)
        private float _frameAccum;
        private float _frameMax;
        private int _frameCount;
        private double _snapMsAccum;
        private long _snapBytesAccum;
        private int _snapCount;
        private long _gcWindowStart = GC.GetTotalMemory(false);

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-loadbots" && int.TryParse(args[i + 1], out int n) && n > 0)
                    _botCount = n;
        }

        private void Start()
        {
            if (_botCount > 0) StartCoroutine(StartWhenServerReady(_botCount));
        }

        private IEnumerator StartWhenServerReady(int n)
        {
            while (!NetworkServer.active) yield return new WaitForSeconds(1f);
            yield return new WaitForSeconds(1f); // 等RoomManager等组件就绪
            StartBots(n);
        }

        /// <summary>启动bot压测 (创建房间+生成怪物+N个bot玩家)</summary>
        public void StartBots(int n)
        {
            if (_running || RoomManager.Instance == null) return;
            _botCount = n;
            _botTargets = new Vector2[n];
            _nextAttack = new float[n];

            _room = RoomManager.Instance.CreateRoom($"botload-{System.DateTime.UtcNow:HHmmss}", 3, new int[0], 1, n);
            if (_room == null) { Debug.LogError("[BotLoad] 创建房间失败"); return; }
            // 状态机合法流转: Waiting → Loading → Fighting (直跳Fighting会被拒绝, 120s后房间超时结算)
            _room.TransitionTo(GameRoom.RoomState.Loading);
            _room.TransitionTo(GameRoom.RoomState.Fighting);
            _initialMonsters = _room.Monsters.Count;

            for (int i = 0; i < n; i++)
            {
                var go = new GameObject($"bot-{i:D3}");
                var pos = new Vector2(Random.Range(-8f, 8f), Random.Range(-5f, 5f));
                go.transform.position = pos;
                go.AddComponent<NetworkIdentity>(); // Spawn必需 — SyncVar脏位追踪真实受压
                var player = go.AddComponent<NetworkedPlayer>();
                player.Initialize(9000 + i, $"bot-{i:D3}", i % 4, 10, 3);
                NetworkServer.Spawn(go); // 服务器对象, 无owner连接 (SyncVar脏位追踪真实受压)
                RoomManager.Instance.AddPlayerToRoom(_room.RoomId, player);
                _bots.Add(player);
                _botTargets[i] = pos;
            }

            _running = true;
            _sampleTimer = 0f;
            _respawnTimer = 0f;
            _gcWindowStart = GC.GetTotalMemory(false);
            Debug.Log($"[BotLoad] 启动: {n}bot × 1线(怪物{_initialMonsters}) — 帧耗时目标<16.7ms, 快照带宽目标<10Mbps");
        }

        /// <summary>停止并输出汇总报告</summary>
        public void StopBots()
        {
            if (!_running) return;
            _running = false;
            foreach (var bot in _bots)
                if (bot != null) Destroy(bot.gameObject);
            _bots.Clear();
            if (_room != null) RoomManager.Instance.DestroyRoom(_room.RoomId);
            _room = null;
            Debug.Log("[BotLoad] 已停止");
        }

        private void Update()
        {
            if (!_running || _room == null) return;

            // ---- 15Hz bot行为节拍 ----
            _tickAccum += Time.unscaledDeltaTime;
            var tickInterval = 1f / 15f;
            while (_tickAccum >= tickInterval)
            {
                _tickAccum -= tickInterval;
                DriveBotsOnce(tickInterval);
            }

            // ---- 每20秒: 清理死亡怪物并补生成 (持续满负载) ----
            _respawnTimer += Time.unscaledDeltaTime;
            if (_respawnTimer >= 20f)
            {
                _respawnTimer = 0f;
                RespawnDeadMonsters();
            }

            // ---- 每5秒: 采样报告 ----
            _sampleTimer += Time.unscaledDeltaTime;
            if (_sampleTimer >= 5f)
            {
                EmitSample(_sampleTimer);
                _sampleTimer = 0f;
            }
        }

        private void DriveBotsOnce(float dt)
        {
            for (int i = 0; i < _bots.Count; i++)
            {
                var bot = _bots[i];
                if (bot == null || bot.isDead) continue;

                // 到达目标点则换新目标 (房间范围内随机游走)
                var pos = bot.serverPosition;
                if ((pos - _botTargets[i]).sqrMagnitude < 0.5f)
                    _botTargets[i] = new Vector2(Random.Range(-9f, 9f), Random.Range(-6f, 6f));

                // 朝目标移动一步 (速度限制内 — 经ServerAuthority验证)
                var dir = (_botTargets[i] - pos);
                var len = dir.magnitude;
                if (len > 0.01f)
                {
                    var step = Mathf.Min(len, bot.moveSpeed * dt);
                    var next = pos + dir / len * step;
                    bot.OnServerMove(new ArpgShared.MoveMessage
                    {
                        playerId = bot.playerId,
                        position = next,
                        velocity = dir / len * bot.moveSpeed,
                        timestamp = Time.time,
                    });
                }

                // 每2秒攻击一次最近怪 (LOS/伤害/对象池路径; 服务器自动选最近目标)
                if (Time.time >= _nextAttack[i])
                {
                    _nextAttack[i] = Time.time + 2f;
                    bot.OnServerSkillCast(new ArpgShared.SkillCastMessage
                    {
                        playerId = bot.playerId,
                        skillId = 0,
                        targetPosition = bot.serverPosition,
                        timestamp = Time.time,
                    });
                }
            }

            // ---- 15Hz 快照构建采样 (每bot一次, 量出AOI构建耗时与序列化体积) ----
            var sw = Stopwatch.StartNew();
            long bytes = 0;
            var entities = 0;
            var snapCount = 0;
            foreach (var bot in _bots)
            {
                if (bot == null || bot.CurrentRoom == null) continue;
                var snapshot = bot.CurrentRoom.BuildSnapshotFor(bot.playerId, bot.serverPosition, GameNetworkManager.AoiRadius);
                snapCount++;
                entities += snapshot.players.Length + snapshot.monsters.Length;
                using (var w = NetworkWriterPool.Get())
                {
                    w.Write(snapshot);
                    bytes += w.Position;
                }
            }
            sw.Stop();
            _snapMsAccum += sw.Elapsed.TotalMilliseconds;
            _snapBytesAccum += bytes;
            _snapCount += snapCount > 0 ? snapCount : 1;
            _aoiEntitiesAccum += entities;
            _aoiSnapCount += snapCount;
        }

        private float _aoiEntitiesAccum;
        private int _aoiSnapCount;

        private void EmitSample(float windowSeconds)
        {
            var frameAvg = _frameCount > 0 ? _frameAccum / _frameCount : 0f;
            var snapMsAvg = _snapCount > 0 ? _snapMsAccum / _snapCount : 0;
            var bytesPerBot = _snapCount > 0 ? _snapBytesAccum / _snapCount : 0;
            var bandwidthMbps = bytesPerBot * 15.0 * _bots.Count * 8 / 1_000_000.0;
            var aoiAvg = _aoiSnapCount > 0 ? (float)_aoiEntitiesAccum / _aoiSnapCount : 0;
            var gcNow = GC.GetTotalMemory(false);
            var gcDeltaKb = (gcNow - _gcWindowStart) / 1024.0;

            Debug.Log(
                $"[BotLoad] bots={_bots.Count} 帧={frameAvg * 1000f:F1}ms(峰{_frameMax * 1000f:F1}) " +
                $"快照={snapMsAvg:F2}ms/{bytesPerBot}B×15Hz 带宽≈{bandwidthMbps:F2}Mbps AOI实体={aoiAvg:F1} " +
                $"GC窗口增量={gcDeltaKb:F0}KB 怪物={AliveMonsters()}");

            // 口径复位
            _frameAccum = 0f; _frameMax = 0f; _frameCount = 0;
            _snapMsAccum = 0; _snapBytesAccum = 0; _snapCount = 0;
            _aoiEntitiesAccum = 0; _aoiSnapCount = 0;
            _gcWindowStart = gcNow;
        }

        private int AliveMonsters()
        {
            var alive = 0;
            foreach (var m in _room.Monsters) if (m != null && !m.IsDead) alive++;
            return alive;
        }

        private void RespawnDeadMonsters()
        {
            var removed = _room.Monsters.RemoveAll(m => m == null || m.IsDead);
            if (removed <= 0) return;
            RoomManager.Instance.SpawnDungeonMonstersPublic(_room, _respawnBatch * 100000);
            _respawnBatch++;
            Debug.Log($"[BotLoad] 清理死亡怪物{removed}只, 补生成批次{_respawnBatch - 1}");
        }

        private void LateUpdate()
        {
            if (!_running) return;
            _frameAccum += Time.unscaledDeltaTime;
            if (Time.unscaledDeltaTime > _frameMax) _frameMax = Time.unscaledDeltaTime;
            _frameCount++;
        }
    }
}
