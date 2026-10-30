# 架构审计与性能基线报告 (阶段01交付)

> 规划: Unity_2D_地牢联机服务器架构优化规划_V1.0 · 本报告: 阶段01 项目审计与性能基线
> 分支: arch-v1 · 日期: 2026-10-28 · 审计范围: Assets/Scripts(客户端+服务器) + Server/ArpgServer(ASP.NET Core)

## 1. 运行模式与部署拓扑 (现状)

| 项 | 现状 |
|---|---|
| 引擎 | Tuanjie 2022.3.62t8 (Unity兼容), Built-in RP |
| 网络库 | Mirror 源码集成于 `Assets/ThirdParty/Mirror-master/Mirror-master/Assets/Mirror/` (6个asmdef, **活的编译源码, 不可删**) |
| Transport | Telepathy (GameServer 7777 端口, ServerBootstrapper.cs:13 batchmode 启动) |
| Web API | ASP.NET Core `Server/ArpgServer` (端口5132), MySQL + Redis(RedisCacheService) |
| 客户端连接 | CloudSaveManager.ServerUrl 默认 localhost:5132; ServerRegionManager 可切远程 39.107.141.107:5132 |
| 联机流程 | WebAPI建房(RoomManager/HttpRoomServer 7778) → Mirror直连(7777) → 服务器权威结算 → WebAPI提交 |
| 部署 | 本地 Server/publish/ArpgServer.exe; 远程 39.107.141.107:5132 (旧版, 需用户SSH部署) |

**联机脚本清单** (阶段01 ☐定位完成):
- 服务端宿主: `GameServer/GameNetworkManager.cs`(2.8KB) `ServerBootstrapper.cs`(1.7KB) `RoomManager.cs`(10.3KB) `SettlementGenerator.cs`(4.5KB)
- 网络实体: `NetworkedPlayer.cs`(8.9KB) `NetworkedMonster.cs`(5.1KB)
- 客户端: `Network/ClientNetworkManager.cs`(9.8KB) `RemotePlayerRenderer.cs` `RemoteMonsterRenderer.cs`
- 共享: `Shared/NetworkMessages.cs`(2.2KB) `PlayerState.cs` `MonsterState.cs` `CombatCalculator.cs` `AuthTokenCrypto.cs`
- 认证: `CustomAuthenticator.cs`(5.3KB, JWT→WebApiClient.ValidateToken)

## 2. RPC / 消息入口盘点 (阶段01 ☐完成)

共 **16 处**, 全部集中在两个网络实体 (无散落):
- `NetworkedPlayer.cs` ×9 (Command/ClientRpc/SyncVar)
- `NetworkedMonster.cs` ×7

消息发送入口: `ClientNetworkManager.cs` (Connect/发送移动技能/接收快照/断线重连3次/CleanStaleRemotePlayers)。
风险注记: Mirror 直连仅在组队副本激活; 单机主流程(95%玩法)不经过 Mirror, 走 WebAPI + 本地模拟。

## 3. 货币/资源修改入口盘点 (阶段01 ☐完成 — 阶段03/05的收敛目标)

**39 个文件、约 90 处**修改入口 (AddGold/SpendGold/AddLife/AddPlane/AddLifeOrPlane/TrySpendLife/TrySpendPlane/Fragments写):
- 钱包: `CurrencyWallet.cs` ×7 (寿元/位面, 本地PlayerPrefs权威 — 阶段03需上服务器账本)
- 金币(Stats.Gold, 服务器权威): Stats ×6 / GameManager ×4 / CombatDirector ×3 / DungeonRewardFlow ×2 / LocalMailSystem ×3 / GachaUI ×2 / 各UI ×~15
- 碎片: DismantleSystem ×5 / RuneManager ×3 / FragmentShopUI ×4 / PlayerProgressData ×3
- 服务器扣费: MetaController.DeductGoldFromSave(抽卡, 已改位面币客户端扣) / AddGoldToSave(签到/任务/邮件附件)
- 拾取: LootPickup ×1 / RelicPickup ×1 / MistSystem ×2(资源箱)

**结论**: 货币写入口高度分散 — 阶段03 Lua原子账本需先建 CurrencyOperation 幂等层, 再逐个迁移写入点。

## 4. Resources.Load / API 端点 / Update 盘点

- **Resources.Load**: 21 个文件 (CharacterSpriteFactory×5, LoadingScreen×5, TitleScreen×4 …) — 阶段02/Addressables 迁移清单来源
- **UnityWebRequest (DB/API)**: 23 个文件, CloudSaveManager×17 / MailUI×10 / DailyTaskUI×8 / InfoUI×8 …
- **服务器 API 端点**: Auth 3 / Friend 7 / Guild 12 / GameConfig 2 / Meta(日常+签到+排行+抽卡+邮件) 20+ / Save(upload/load/list/delete/ping/update) / Team — 共 **40+ 端点**
- **Update 方法**: 28 个 (阶段08 GC 治理时的扫描范围)

## 5. 性能基线 (ProfilerRecorder 实测, 本机 30秒×2 场景)

| 场景 | FPS | 主线程均值 | 主线程峰值 | GC分配/帧 | GC累计(30s) |
|---|---|---|---|---|---|
| 大厅(Hub) | 116.8 | 1.78ms | 940ms(启动 hitch) | 13.6KB | 46.5MB |
| 秘境1层(战斗) | 132.9 | 1.47ms | 43.6ms | 14.1KB | 55.0MB |

- cpuAvg 单位为纳秒(计数器原生), 已换算毫秒。
- 目标对齐(规划阶段12): CPU<60% ✓(当前远低于)、GC 接近0 ✗(13-14KB/帧 → 阶段08治理)、带宽未测(需 Mirror 联机压测, 阶段07)。
- 副本 FPS 高于大厅属正常(大厅 UI 重绘多)。

## 6. ThirdParty 审计 (阶段01 ☐完成)

- `Assets/ThirdParty/Mirror-master/Mirror-master/Assets/Mirror/` = **唯一 Mirror 源码, 全项目 6 个 asmdef + MIRROR 宏来源 + 8 个业务文件 using Mirror → 必须保留** (2026-10-26 已核实, 勿删)。
- 其余 ThirdParty (Readme.asset 等) 为模板残留, 清理需走独立提交(阶段02后)。

## 7. 关键风险对照 (规划§5 → 现状)

| 规划风险 | 现状结论 |
|---|---|
| 货币写入口分散(90处) | ✓ 确认, 阶段03建 CurrencyOperation+Lua账本 后逐个迁移 |
| 位面币客户端权威 | ✓ CurrencyWallet=PlayerPrefs 本地权威; 服务器无此列 — 阶段03需 PlayerSave 加列+迁移 |
| 抽卡扣费已迁移 | ✓ 2026-10-28 已改位面币(客户端钱包), 服务器不再扣金币 |
| 存档 SaveJson 大JSON | ✓ SaveController 单JSON列, 阶段03 拆 player_data 表 |
| 无分线/AOI/快照 | ✓ 均未实现 — 阶段04/06/07 全新开发 |
| 死亡惩罚已实现 | ✓ 10%金币(上限1000) |

## 8. 阶段02+ 预备事项

1. 玩家数据合并目录: Stats.Gold / CurrencyWallet / Progress.DismantleFragments / PassiveNodeIds → 统一 PlayerData 模型(阶段02)
2. 断线决策已定: 队友继续战斗, 角色倒地可救起; 不允许切线/跨服迁移
3. 快照分级 P0-P3 已定(规划§8表)
4. 在线人数/线容量未定 → 阶段04 前必须给出(影响 ShardManager)
5. 本地 Redis/MySQL 可用性待验证(阶段03前置)
