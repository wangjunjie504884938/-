# 项目优化审计清单

日期：2026-08-13

## 当前结论

- 编译检查通过：`Assembly-CSharp.csproj` 构建结果为 `0` 错误、`122` 警告。
- Build Settings 当前只启用 `Assets/Scenes/SampleScene.scene`，客户端构建入口比之前更干净。
- 主要问题不是“项目无法编译”，而是：示例资源污染、UI 代码生成残留、运行时全局查找、`PlayerPrefs` 过多、核心脚本过大、联机权威边界不清。
- 继续遵守当前方向：后续 UI/美术不要再靠代码生成，优先做成 prefab / 场景对象 / ScriptableObject 配置。

## P0：优先处理

### 1. 清理 Mirror 导入包污染

现状：

- `Assets/Import/Mirror-master.zip` 约 15.08 MB。
- `Assets/Import/Mirror-master/Mirror-master/Assets/Mirror/Examples`、`Tests`、`Hosting/Edgegap` 等示例/测试资源占体积，并产生大量警告。
- 粗扫缺失脚本引用时，Mirror profiling/example prefab 是主要噪音来源。

建议：

- 保留运行所需的 Mirror 核心、Transport、Authenticator。
- 将 Mirror 的 `Examples`、`Tests`、原始 zip 移出 `Assets`，放到项目外归档目录。
- 若要长期维护，改为 UPM/package 管理 Mirror，而不是整包源码 zip + examples 直接进 `Assets`。

涉及目录：

- `Assets/Import/Mirror-master.zip`
- `Assets/Import/Mirror-master/Mirror-master/Assets/Mirror/Examples`
- `Assets/Import/Mirror-master/Mirror-master/Assets/Mirror/Tests`

### 2. 检查主项目 prefab/scene 的脚本引用

现状：

- 主项目过滤 Mirror 后仍扫到 `GameHUD.prefab`、`BossHealthPanel.prefab`、`GameServer.unity` 的脚本 GUID。
- `GameHUD.prefab` 和 Boss HP prefab 中大量 GUID 是 Unity 内置 UI 组件，不能简单判定为 Missing Script。
- `GameServer.unity` 中有多条自定义/包脚本引用，需要在 Unity Inspector 里确认没有 Missing Script。

建议：

- 在 Unity 打开：
  - `Assets/Prefabs/GameHUD.prefab`
  - `Assets/Resources/UI/BossHealthPanel.prefab`
  - `Assets/Scenes/GameServer.unity`
- 用 Inspector 搜索 `Missing (Mono Script)`。
- 如果 `GameServer.unity` 有 Missing Script，先修引用，再继续联机改造。

### 3. 停止扩散代码生成 UI

现状：

- `new GameObject(` 在脚本中约 803 处，集中在 UI 层。
- 高频文件：
  - `Assets/Scripts/UI/GuildUI.cs`
  - `Assets/Scripts/UI/HubUI.cs`
  - `Assets/Scripts/UI/EquipUI.cs`
  - `Assets/Scripts/Core/GameManager.cs`
  - `Assets/Scripts/UI/TitleScreen.cs`
  - `Assets/Scripts/UI/GameUI.cs`

建议：

- 新功能 UI 一律走 prefab。
- 旧 UI 迁移顺序：
  1. `GameUI` / `GameHUD`
  2. `TitleScreen`
  3. `HubUI`
  4. `GuildUI`
  5. `EquipUI` / `ShopUI` / `DailyTaskUI`
- 每个 UI 脚本拆成：
  - `View`：只持有 prefab 引用、按钮、文本、列表容器。
  - `Presenter/Controller`：绑定数据、处理点击。
  - `Service`：请求云端/联机/存档。

## P1：代码结构优化

### 1. 拆分超大脚本

当前超过 500 行的重点脚本：

- `Assets/Scripts/Core/GameManager.cs`：1698 行。
- `Assets/Scripts/Combat/VFXHelper.cs`：1588 行。
- `Assets/Scripts/UI/GuildUI.cs`：1461 行。
- `Assets/Scripts/UI/HubUI.cs`：1083 行。
- `Assets/Scripts/Player/PlayerCombatController.cs`：1002 行。
- `Assets/Scripts/UI/GameUI.cs`：965 行。
- `Assets/Scripts/UI/TitleScreen.cs`：922 行。
- `Assets/Scripts/UI/EquipUI.cs`：909 行。

建议拆法：

- `GameManager`
  - 保留：启动流程、场景状态切换。
  - 拆出：`DungeonRunController`、`RewardController`、`PlayerSpawnController`、`GameFlowStateMachine`。
- `VFXHelper`
  - 拆成：`DamageNumberVFX`、`SkillVFX`、`ProjectileVFX`、`ImpactVFX`、`VFXFactory`。
  - 可复用对象全部接入 `VFXPool`。
- `PlayerCombatController`
  - 拆成：`PlayerAttackExecutor`、`SkillCooldowns`、`PlayerDamageReceiver`、`PlayerCombatViewBridge`。
- 大 UI 脚本
  - 每个面板先 prefab 化，再拆 View/Presenter/Service。

### 2. 减少运行时全局查找

扫描结果：

- `FindObjectOfType`：17 处。
- `FindObjectsOfType`：5 处。
- `GameObject.Find`：3 处。
- `GetComponentsInChildren`：8 处。

重点位置：

- `Assets/Scripts/Core/GameManager.cs`
- `Assets/Scripts/UI/GameUI.cs`
- `Assets/Scripts/UI/UIManager.cs`
- `Assets/Scripts/Dungeon/DungeonVisuals.cs`
- `Assets/Scripts/Player/PlayerInputController.cs`

建议：

- 保留启动初始化阶段少量查找可以接受。
- 战斗中、进副本、刷怪、重建 HUD 时不要靠全局查找。
- 用 `SceneRegistry`、显式引用、事件注册替代。

### 3. 日志系统统一

现状：

- `GameLog.cs` 已存在。
- `Assets/Scripts/GameServer` 下仍有多处 `Debug.Log` / `Debug.LogWarning` / `Debug.LogError`。

建议：

- 服务器/联机脚本统一接 `GameLog`。
- 加日志等级和开关，Release/移动端默认关闭普通日志。
- 网络消息日志只保留关键错误、房间状态变化、认证失败、断线原因。

## P2：资源和运行时性能

### 1. `Resources` 目录收缩

现状：

- 最大资源：`Assets/Resources/Sprites/Dungeon/Map_Stage0.png` 约 5.78 MB。
- 角色生成图若干约 0.44-0.54 MB。
- `Resources.Load` 约 16 处。

建议：

- 短期：集中管理 `Resources` 路径，不再新增散落字符串路径。
- 中期：大图、角色图、UI prefab 改 Addressables 或显式引用。
- `Map_Stage0.png` 检查导入设置：Max Size、压缩格式、Read/Write、MipMap。

### 2. `PlayerPrefs` 分层

现状：

- `PlayerPrefs` 命中约 485 处。
- 主要集中在：
  - `Assets/Scripts/Cloud/SaveDataDTO.cs`
  - `Assets/Scripts/Core/PlayerProgressData.cs`
  - `Assets/Scripts/UI/GuildUI.cs`
  - `Assets/Scripts/Cloud/CloudSaveManager.cs`
  - `Assets/Scripts/UI/FriendUI.cs`

建议：

- 本地设置：音量、画质、语言，继续可用 `PlayerPrefs`。
- 玩家进度：统一进 `LocalSaveRepository`。
- 联机权威数据：金币、装备、抽卡、邮件、公会、好友、排行，不能以客户端 `PlayerPrefs` 为准。
- UI 不直接读写 `PlayerPrefs`，只调用数据服务。

### 3. 运行时对象创建收敛

现状：

- `new GameObject` 大量集中于 UI 和 VFX。
- UI 方向应转 prefab。
- VFX 方向应走池化。

建议：

- UI：prefab 实例化一次，列表项用 item pool。
- VFX：伤害数字、弹道、命中特效、飘字统一对象池。
- 地牢装饰：能复用的 sprite/material 不重复创建。

## 联机改造前置清单

P0：

- 确认 `GameServer.unity` 没有 Missing Script。
- 清理 Mirror 示例和 zip，减少包污染。
- 把 `NetworkMessages.cs`、`PlayerState.cs`、`MonsterState.cs` 固定为共享协议层，禁止 UI 直接依赖服务端对象。

P1：

- `GameManager` 拆出本地单机流程和联机流程。
- `PlayerCombatController` 拆出纯计算逻辑，服务端复用 `Shared/CombatCalculator.cs`。
- `CloudSaveManager` 只负责云存档，不再承担金币/装备/邮件/公会的权威判断。

P2：

- 做客户端预测/插值：`RemotePlayerRenderer`、`RemoteMonsterRenderer` 只表现，不参与判定。
- 做房间状态机：匹配、加载、战斗中、结算、断线重连。
- 做网络日志与回放数据，方便查联机 bug。

## 本轮验证记录

- 构建命令：`D:\unity\2022.3.62t8\Editor\Data\NetCoreRuntime\dotnet.exe build Assembly-CSharp.csproj /nologo`
- 结果：`0` 错误，`122` 警告。
- 主要警告：
  - Mirror examples/test 相关未赋值字段。
  - `System.IO.Compression`、`System.Net.Http` 版本冲突，来自 Tuanjie/Codely/Mirror/工具程序集引用。
  - DTO/响应类字段 `CS0649`，多数是 JSON 反序列化字段，不一定是 bug。
  - 少量真正可清理字段：`CombatDirector.hasElite`、`FriendUI._teamPage`、`GameManager._pendingDungeonInit`、`GameManager._pendingStageIndex`、`CombatDirector._batchKillVfxCount`、`CombatDirector._killFrameReset`。

