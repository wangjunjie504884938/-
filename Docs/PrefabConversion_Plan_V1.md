# Prefab 转换清单与目标目录结构 (V1.1)

> 目标：把当前靠代码运行时 `new GameObject()` 生成的游戏内容，抽成可编辑 prefab，
> 使策划/美术能在 Inspector 面板直接调整参数。
>
> 原则：只抽「单个可复用模板」；程序化生成逻辑（地牢布局、迷雾地图、VFX 对象池）保留在代码里。
> 扫描基线：全脚本 861 处 `new GameObject()`，89 个 .cs 文件。

## 现状盘点

| 资源 | 数量 | 说明 |
|------|------|------|
| 角色职业 | 3 | Warrior / Mage / Priest（Idle/Run/Attack/Hurt/Dead + Animator + 武器弧光） |
| 敌人类型 | 8 | Melee / Ranged / Elite / Boss / Bomber / Charger / Healer / Shielder |
| 敌人贴图 | 11 | Melee_Goblin / Ranged_Mage / Elite_Knight / Boss_0~7 / Boss_DemonLord |
| 地牢地板 | 5 | Stone / Forest / Ice / Fire / Shadow |
| 障碍物 | 6 | Pillar / Tree / Rock / IceSpike / LavaPool / Tombstone |
| 装饰件 | 10 | Torch / Flame / Cobweb / Debris / Mushroom / Vine / Crystal_Ice / Crystal_Shadow / Icicle / LavaCrack |
| 环境特效 | 6 | LightRay / Vignette / Fog_A / Fog_B / DustParticle / EmberParticle |
| 投射物 | 2 | CircleProjectile / WhitePixel |
| UI 面板 | ~45 | 其中 21 个有占位 prefab（禁用），24+ 个纯代码 |
| 遗物 | 2 | 掉落物 RelicPickup + 选择面板 RelicChoiceUI |

## 目标目录结构

```
Assets/Prefabs/
├── Characters/
│   ├── Player_Warrior / Player_Mage / Player_Priest
│   ├── Teammate
│   └── Pet
├── Enemies/
│   ├── Melee_Goblin / Ranged_Mage / Elite_Knight
│   ├── Bomber / Charger / Healer / Shielder / Minion
│   └── Bosses/  (Boss_0 ~ Boss_7, Boss_DemonLord)
├── Loot/
│   └── RelicPickup
├── Environment/
│   ├── Dungeon/
│   │   ├── Floors/      (5)
│   │   ├── Walls/       (Wall_Brick, WallShadow)
│   │   ├── Obstacles/   (6)
│   │   ├── Decorations/ (10)
│   │   └── Effects/     (LightRay, Vignette, Fog_A/B, 粒子)
│   └── World/  (Home, QuestNpc, Waypoint, ZoneFloor)
├── VFX/
│   ├── Projectiles/   (CircleProjectile, WhitePixel)
│   ├── Skills/        (Warrior_Slash/Whirlwind/WarCry, Mage_IceNova, Priest_HealAura/DivineShield)
│   └── TelegraphCircle / DivineShield / ScreenFlash / BuffAura
├── Network/  (RemotePlayer, RemoteMonster)
└── UI/  (迁移自 Resources/UI，补全内容后启用)
```

## 完整转换清单

### ① 角色类（7 项）
- 玩家 ×3（Warrior / Mage / Priest）— SpriteRenderer + Animator + 血条 + 武器弧光子物体
- 队友 Teammate（TeammateAI）
- 宠物 Pet（PetCompanion：PetSprite / PetHPBar / 光环）
- 生成源：GameManager.cs:964、PetTeammateManager.cs:25/54

### ② 敌人类（21 项）
- 基础怪 ×8：Melee_Goblin / Ranged_Mage / Elite_Knight / Bomber / Charger / Healer / Shielder / Minion
- Boss ×9：Boss_0 ~ Boss_7、Boss_DemonLord
- 远程渲染体 ×2：RemotePlayer / RemoteMonster
- 词缀光环 AffixAura
- 生成源：CombatDirector.cs:330、RoomManager.cs:173/182、WorldManager.cs:401、EnemyCombatController.cs:285、ClientNetworkManager.cs:232/250

### ③ Loot 遗物类（2 项）
- RelicPickup 掉落物（CombatDirector.cs:526、RelicPickup.cs）
- RelicChoiceUI 三选一面板（RelicChoiceUI.cs）

### ④ 环境类（约 30 项）
- 地板 5 + 墙 2 + 障碍 6 + 装饰 10 + 特效 6
- 世界件：Home / QuestNpc / Waypoint（Beam+BaseDisc）/ ZoneFloor
- 生成源：DungeonVisuals.cs、WorldVisuals.cs、MistSystem.cs（雾/篝火/地图节点）

### ⑤ 特效类（约 12 项）
- 投射物 2：CircleProjectile / WhitePixel
- 职业技能特效 6：Warrior_Slash / Warrior_Whirlwind / Warrior_WarCry / Mage_IceNova / Priest_HealAura / Priest_DivineShield
- 通用 4：DivineShield / TelegraphCircle / ScreenFlash / BuffAura
- 生成源：VFXHelper.cs、VFXHelper.Skills.cs、DivineShieldVFX.cs、BossPattern.cs

### ⑥ UI 类（约 45 项）

**A. 已有占位 prefab（21 个，需补全内容 + 启用 EnablePrefabs）**
Achievement / AsyncPvp / BossCodex / BossHealth / DailyChallenge / DailyTask / Equip / Forge / FragmentShop / Friend / Gacha / Guild / Hub / Mail / PassiveTree / RuneEquip / SeasonPass / Settings / Shop / SkillTree / StageSelect

**B. 纯代码面板（24+ 个，需新建 prefab）**
ArenaBattle、BossDropAnimation、CharacterDetail、CharacterSelect、CharacterSlot（Cards/Confirm）、ChatPopup、ClassStatsPopup、CloudAuth、Dialogue、GenderSelect、GuildInviteBanner、InfoUI、InGameHud、Leaderboard、LoadingScreen、NotificationCenter、Recharge、RelicChoice、ServerSelect、ShopBagShell、TeamLobby、TitleScreen（+Bg）、TopNavBar、HUD（GameUI/GameUI.Toasts/GameManager.Hud）、WorldQuest、WaypointUI

### ⑦ 明确「不转 prefab」的框架/系统对象
这些 `new GameObject` 是运行时单例/框架，转 prefab 会破坏架构，保持代码生成：
- 单例/框架：SingletonBehaviour、SceneController、GameManager
- 对象池/管理器：VFXManager、VFXPool、EnemyPool
- 场景系统对象：Main Camera、EventSystem、Canvas、Grid
- 网络/测试：SnapshotUploader、WebApiClient（CustomAuthenticator）、ServerBotManager（bot）、Phase09LodProbe（测试探针）

## 代码改造点（转换后需回填引用）
- `GameManager.cs:964`（Player）→ Characters/Player_*.prefab
- `PetTeammateManager.cs:25/54`（Pet/Teammate）→ Characters/Pet、Teammate
- `CombatDirector.cs:330/526`（怪物/遗物）、`RoomManager.cs:173/182`、`WorldManager.cs:401`、`EnemyCombatController.cs:285`（Minion）→ Enemies/*、Loot/*
- `ClientNetworkManager.cs:232/250`（远程玩家/怪物）→ Network/*
- `DungeonVisuals.cs` / `WorldVisuals.cs` / `MistSystem.cs`（环境件）→ Environment/*
- `VFXHelper*.cs` / `DivineShieldVFX.cs` / `BossPattern.cs`（特效）→ VFX/*
- `UiPrefabLoader.EnablePrefabs` → true（待 UI prefab 补全后）

## ⑧ 已有的 Editor 自动化工具链（应复用，而非新造）

| 工具 | 菜单 | 作用 |
|------|------|------|
| UiPrefabGenerator.cs | Tools/UI/Generate All UI Prefabs | Play Mode 跑各面板 BuildUI，烘焙成完整 prefab |
| UiPrefabFrameworkGenerator.cs | Tools/UI/Generate Panel Frameworks | Edit Mode 生成空占位框架（21 个占位 prefab 的来源） |
| HudPrefabBuilder.cs | Tools/Build HUD Prefab | 烘焙 HUD 为 prefab，目标 Assets/Prefabs/（与本计划目录一致） |
| SpriteGenerator.cs | — | 程序化生成 chibi 像素美术 |
| SpriteAssetGenerator.cs | Tools/Generate Sprite Assets | 运行时像素图导出为 PNG 资产 |
| GameServerBuilder.cs | Build/Build Game Server | 构建 Linux Headless 服务器 |
| ArchTools.cs | Arch/数据模型自检 | 架构自检、CSV→JSON |
| ProjectBridge.cs | — | DSH Unity MCP 桥接 |
| GpuResourceCleanup.cs | — | GPU 资源清理 |

**关键洞察**：作者已搭好「代码 BuildUI → Editor 烘焙为 prefab」的工具骨架，只是未完成（21 个 prefab 是 framework 占位版，完整版需 Play Mode 跑 UiPrefabGenerator）。
转换方案应复用/扩展这套工具，把 UI 烘焙思路推广到角色/敌人/环境/特效。

## ⑨ ScriptableObject 配置现状（另一处待落盘）
- Data/ScriptableObjects/ 为空（仅 .gitkeep）
- GameConfigSO 走 Resources.Load("GameConfig")，找不到即 CreateInstance 运行时新建空实例
- 建议：转换时同步把 GameConfigSO 等配置落盘为 .asset 资产，便于面板编辑

## 转换顺序建议
1. 角色（玩家 3 + 队友 + 宠物）
2. 敌人（基础怪 8 + Boss 9 + 远程渲染 2）
3. Loot 遗物（掉落物 + 选择面板）
4. 环境（地牢 + 世界）
5. 特效（投射物 + 技能 + 通用）
6. UI（先补全 21 占位并启用，再转纯代码面板）

---

# ⚠️ 注意事项（转换全程必须遵守）

## A. 视觉/样式风险

1. **程序化占位图不会「变好看」**
   `CharacterSpriteFactory.FillWarrior/FillMage/FillPriest`、`SpriteGenerator` 是像素画 fallback，说明部分角色/敌人美术尚未到位。转 prefab 后若仍走 fallback，视觉与现在完全一致（占位）；要变好看是美术补图，不是 prefab 转换能解决的。

2. **运行时动态换贴图逻辑必须保留，不能静态化**
   - `CharacterSprite.cs`：按职业/朝向换动画帧、换武器弧光
   - `EnemySprite.cs`：按 EnemyType 换 sprite、Boss 图、词缀光环颜色
   - 正确做法：prefab 只存「默认视觉」，动态切换仍由代码控制，否则会贴图错位/丢失。

3. **引用同一批 sprite，样式才一致**
   prefab 里的 SpriteRenderer 必须引用与代码 `Resources.Load` 相同的贴图（同路径/同 guid），否则转换前后视觉有细微差别。

## B. 功能/引用风险

4. **单例引用必须回填，漏一处即 NRE**
   `GameManager.Instance.Player`、`RoomManager`、网络生成处等，现在 `new` 完立刻塞进单例。改 prefab 后生成处必须回填引用，否则空引用报错（面板空白/怪不动/断线）。

5. **Animator 控制器引用要一致**
   `CharacterSprite.cs` 现在用 `Resources.Load<RuntimeAnimatorController>` 运行时加载 Warrior/Mage/Priest Animator。转 prefab 后改成序列化引用，路径/guid 必须一致，否则动画丢失。

6. **序列化值 vs 硬编码值冲突**
   `EnemyController` 字段有默认值（BaseHp=30 等），代码里又可能运行时覆盖。转换前必须明确「哪些值面板调、哪些值代码控」，否则改了面板不生效，或被代码覆盖。

7. **对象池要兼容**
   `EnemyPool` / `VFXPool` 现在 `new GameObject`。改 prefab 后要用 `Instantiate(prefab)` 接进池子（拿/还引用的是 prefab，不是裸对象），否则池失效、重复实例化。

## C. 网络/架构风险

8. **网络对象必须挂 NetworkIdentity**
   RemotePlayer / RemoteMonster（`ClientNetworkManager` 生成）是 Mirror 网络对象。转 prefab 后必须挂 `NetworkIdentity` 并注册到 `spawnPrefabs`，否则客户端无法同步、生成失败。

9. **不该转的对象不要碰**
   单例/框架（SingletonBehaviour/SceneController/GameManager）、对象池管理器（VFXManager/EnemyPool）、场景系统对象（Main Camera/EventSystem/Canvas/Grid）、网络/测试（SnapshotUploader/WebApiClient/ServerBotManager/Phase09LodProbe）保持代码生成。

10. **不过度转换**
    程序化生成逻辑（地牢布局、迷雾地图、VFX 池）保留代码，只抽「单个可复用模板」。把布局逻辑也塞进 prefab 会失去运行时随机性。

## D. 工具/流程注意

11. **UiPrefabGenerator 必须在 Play Mode 运行**
    面板 BuildUI 依赖运行时状态（单例、数据），Edit Mode 下会空引用。`UiPrefabFrameworkGenerator`（空框架）才可在 Edit Mode 跑。

12. **meta/guid 稳定性**
    每个 prefab 必须保留 .meta 文件（guid 稳定），引用才不会断；git 提交时 .prefab 与 .meta 成对提交。

13. **每步验证，不要一次全转**
    - 转换前：读原脚本字段定义 + 运行时初始化，记录原值
    - 转换后回读：查对象值/组件/引用，对照转换前
    - Play 验证：进图、打怪、放技能、切职业，跑主路径 + 失败分支
    - 查 Console 无报错，等编译完成，不清日志掩盖错误

14. **git 兜底，逐类独立提交**
    每一类转换一个 commit，出问题 `git revert` 即时回滚。分支当前为 `arch-v1`。

## E. 顺带待办（非 prefab 范畴，但同属素材化）

15. **音效缺失**：`Assets/Audio/` 全空，无任何音频文件。
16. **配置资产未落盘**：`GameConfigSO` 等 ScriptableObject 运行时 CreateInstance，`Data/ScriptableObjects/` 为空，建议转换时同步落盘 .asset。
