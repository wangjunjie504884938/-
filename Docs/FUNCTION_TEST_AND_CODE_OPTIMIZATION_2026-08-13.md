# 功能测试与代码优化规划

日期：2026-08-13

## 本轮测试结果

### 已验证

- 项目结构可读取，主入口场景当前为 `Assets/Scenes/SampleScene.scene`。
- 已存在冒烟测试入口：`Assets/Editor/CodexSmokeTest.cs`。
- 当前脚本扫描完成，已定位主要优化热点。
- 发现一个实际阻塞项：生成的 `EncryptionTransportEditor.csproj` 引用了不存在的 Mirror 源文件。

### 未完整验证

- 未能完成 Unity PlayMode 全流程实测。
- 原因：当前已有 `Tuanjie.exe` 进程在运行，但没有可捕获窗口标题；新的 batchmode 命令秒退且没有生成新日志。
- 因此不能把“标题页 -> 选职业 -> 进副本 -> 清怪 -> 结算”说成已经完整跑通。

## 当前阻塞项

### P0：Mirror 工程引用损坏

编译时出现：

```text
CSC : error CS2001: 未能找到源文件
Assets\Import\Mirror-master\Mirror-master\Assets\Mirror\Transports\Encryption\Editor\EncryptionTransportInspector.cs
```

实际检查：

- 文件不存在。
- `Assets/Import/Mirror-master/Mirror-master/Assets/Mirror/Transports/Encryption` 目录也不存在。
- 但生成的 `EncryptionTransportEditor.csproj` 仍引用它。

判断：

- 这是 Mirror 导入包或 Unity 生成工程缓存不一致导致的，不建议直接手改 `.csproj`。

处理建议：

1. 在 Unity 里执行重新生成 C# 工程文件。
2. 如果仍存在，清理 `Library` 后让 Unity 重导。
3. 如果仍存在，说明 Mirror 导入残留不完整，需要重导 Mirror 或删除残留 asmdef/meta。
4. 后续建议不要把整个 `Mirror-master.zip`、Examples、Tests 留在 `Assets` 内。

## 功能测试计划

### P0：先恢复可测试环境

- 关闭或确认当前正在运行的 Tuanjie 编辑器实例。
- 重新打开项目，确认 Console 无红错。
- 重新生成 `.csproj`。
- 再跑 `CodexSmokeTest.Run`。

### P1：主流程冒烟测试

目标流程：

1. 打开 `SampleScene.scene`。
2. 进入标题界面。
3. 点击开始新游戏。
4. 选择 Warrior。
5. 进入 Hub。
6. 进入第 0 关副本。
7. 自动清怪到结算。
8. 回到非副本状态。

期望结果：

- 无 Exception / Error。
- `GameManager.Instance` 存在。
- `TitleScreen.Instance`、`CharacterSelectUI.Instance`、`HubUI.Instance`、`GameUI.Instance` 按流程出现。
- `Player` 正常生成。
- `IsInDungeon` 能进入并退出。

### P2：扩展功能测试

- 装备 UI：打开、穿戴、卸下、属性刷新。
- 抽卡 UI：请求、结果显示、保底计数。
- 邮件 UI：列表、领取附件。
- 好友/公会：接口失败时 UI 不黑屏、不锁死。
- 云存档：登录失败/断网/冲突版本处理。
- 联机：启动 `GameServer.unity`、客户端连接、房间进入、玩家/怪物同步。

## 代码可优化点

### P0：先处理会阻塞测试和构建的问题

- Mirror 残留引用：`EncryptionTransportEditor.csproj` 指向不存在文件。
- Mirror examples/tests/zip 污染：会增加编译警告、导入时间、误报 Missing Script。
- `GameServer.unity` 需要 Inspector 检查 Missing Script。

### P1：UI 不再代码生成

扫描结果：

- `new GameObject(`：约 800 处。
- 主要集中在 UI 脚本。

优先拆：

- `Assets/Scripts/UI/GameUI.cs`
- `Assets/Scripts/UI/TitleScreen.cs`
- `Assets/Scripts/UI/HubUI.cs`
- `Assets/Scripts/UI/GuildUI.cs`
- `Assets/Scripts/UI/EquipUI.cs`

目标结构：

- prefab 存 UI 层级和美术。
- View 脚本只绑定控件。
- Presenter/Controller 负责点击和刷新。
- Service 负责网络/存档。

### P1：拆超大脚本

当前最大脚本：

- `Assets/Scripts/Core/GameManager.cs`：1698 行。
- `Assets/Scripts/Combat/VFXHelper.cs`：1588 行。
- `Assets/Scripts/UI/GuildUI.cs`：1461 行。
- `Assets/Scripts/UI/HubUI.cs`：1083 行。
- `Assets/Scripts/Player/PlayerCombatController.cs`：1002 行。

建议拆分：

- `GameManager` 拆出 `GameFlowController`、`DungeonRunController`、`RewardController`、`PlayerSpawnController`。
- `VFXHelper` 拆出 `DamageNumberVFX`、`SkillVFX`、`ProjectileVFX`、`ImpactVFX`。
- `PlayerCombatController` 拆出攻击执行、技能冷却、受击、表现桥接。
- 大 UI 拆成 View/Presenter/Service。

### P1：存档与联机数据分层

扫描结果：

- `PlayerPrefs.`：约 441 处。

优化方向：

- `PlayerPrefs` 只保留本机设置：音量、画质、语言。
- 玩家进度放 `LocalSaveRepository`。
- 金币、装备、抽卡、邮件、公会、好友、排行，联机版必须以后端/服务端为准。
- UI 层不直接读写 `PlayerPrefs`。

### P2：减少运行时查找和同步加载

扫描结果：

- `FindObjectOfType`：17 处。
- `FindObjectsOfType`：5 处。
- `GameObject.Find`：3 处。
- `Resources.Load`：16 处。

优化方向：

- 启动阶段少量查找可以保留。
- 战斗中、刷怪、切 UI、进副本时不要全局查找。
- 用 `SceneRegistry`、显式引用、事件注册替代。
- 大资源从 `Resources` 迁到 Addressables 或 prefab 显式引用。

### P2：日志降噪

扫描结果：

- `Debug.Log`：34 处。
- `Debug.LogWarning`：3 处。
- `Debug.LogError`：7 处。

建议：

- 服务器和联机脚本统一走 `GameLog`。
- Release 默认关闭普通日志。
- 网络消息日志只保留认证、断线、房间状态、严重错误。

## 推荐下一步

### 第一步

先修 Mirror 工程引用问题，让项目重新达到稳定编译和可批处理测试状态。

### 第二步

跑通 `CodexSmokeTest` 主流程，并把报告写到 `Logs/CodexSmokeTestReport.txt`。

### 第三步

从 `GameUI` 和 `TitleScreen` 开始做 prefab 化，停止 UI 代码生成继续扩大。

### 第四步

拆 `GameManager`，把单机流程和联机流程边界分清，为后续联机版打底。

