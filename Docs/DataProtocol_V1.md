# 数据协议 V1 (阶段02交付)

> 规划: Unity_2D_地牢联机服务器架构优化规划_V1.0 · 本文档 = 数据协议文档 (阶段02验收物)
> 序列化: Newtonsoft.Json (com.unity.nuget.newtonsoft-json 3.2.2) · 货币: **long** · 静态配置只读

## 1. 数据模型总览

| 类型 | 目录 | 用途 | 持久化 |
|---|---|---|---|
| `Arch.PlayerData` | Arch/PlayerData.cs | 玩家持久化账本 (货币/成长/背包/被动) | MySQL `player_data.data_blob` + Redis 热缓存 |
| `Arch.PlayerRuntimeState` | Arch/PlayerRuntimeState.cs | 内存运行时态 (进快照投影为 MemberState) | 不持久化 |
| `Arch.DungeonSnapshot` | Arch/DungeonSnapshot.cs | 地牢实例快照 (分级 P0-P3) | Redis 热态 + MySQL 灾备 |
| `Arch.LineData` | Arch/LineData.cs | 分线 (容量/健康/接收/版本) | server_registry 表 |
| `Arch.CurrencyOperation` | Arch/CurrencyOperation.cs | 货币操作幂等流水 | currency_ledger 表 |
| `Arch.StaticConfig` | Arch/StaticConfig.cs | 静态配置 (只读, 加载后校验) | JSON 文件 (CSV→JSON 导出) |

## 2. 货币规则 (阶段02 强约束)

1. 货币一律 **long**: `Gold`(游戏币) / `LifeCoins`(寿元币) / `PlaneCoins`(位面币) / `Fragments`(碎片)
2. 非负校验: 反序列化/迁移后 `PlayerData.Normalize` 统一钳位
3. 上限: 金币 999,999,999,999；寿元/位面/碎片 999,999,999 — 封顶时 `CurrencyMath.TryAdd` 返回 false(未足额入账)
4. 溢出: `TryMul(count, unit, max)` 批量扣费防 `int*long` 溢出
5. 所有变更必须生成 `CurrencyOperation` (OperationId 幂等) — 阶段03 接 Lua 原子账本

## 3. 版本迁移与兼容策略

- `PlayerData.Version` 整数递增; `Migrate()` 逐级升级 (v0→v1→…)
- **高版本数据拒绝加载** (客户端过旧, 明确报错)
- 迁移后必须过 `Normalize()` (非负/封顶/空集合)
- 兼容策略: 只加字段不改语义; 字段废弃保留一个版本后清除

## 4. 静态配置 (只读)

- 首批: `DailyTasks` (TaskType 0击杀/1通关/2耗金/3强化/4经验 — 与 DailyTaskUI 现行模板对齐) + `Items`
- CSV→JSON 导出: Excel 另存 CSV → 编辑器菜单 **Arch/任务模板CSV→JSON** → 校验通过后落 `daily_tasks.json`
- 启动校验: `ConfigValidator.Validate` — ID 唯一性/字段完整性/类型越界, 报错即拒绝使用

## 5. 快照分级 (用户已定)

| 等级 | 副本类型 | 快照间隔 | 崩溃恢复 | 持久化策略 |
|---|---|---|---|---|
| P0 | 普通小怪 | 5s | ≤5s | Redis 快照+关键事件 |
| P1 | 精英/普通Boss | 3s | ≤3s | Redis 快照+关键战斗事件 |
| P2 | 高难Boss/长流程 | 1s | ≤1s | 高频快照+事件日志 |
| P3 | 货币/掉落/结算 | 不依赖快照 | 不可重复/丢失 | 操作ID幂等+持久化流水+对账 |

`DungeonSnapshot.SnapshotTier` 0/1/2 对应 P0/P1/P2; 货币结算永远走 P3 幂等通道。

## 6. 已定决策 (阶段04/06 引用)

- 断线: 队友继续战斗, 角色肉体倒地可救起 (`MemberState.Downed`)
- 账本: Redis=实时账本, MySQL=异步落库
- 不允许切线/跨服地牢迁移
- 单线容量默认 50 (占位, 阶段04前必须确认最终值)
