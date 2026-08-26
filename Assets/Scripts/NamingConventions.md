# 命名规范

## 类和接口
- **PascalCase**: `PlayerController`, `GameManager`, `UIEventManager`
- **接口加 I 前缀**: `IPoolable`, `IEventEmitter`

## 方法
- **PascalCase**: `GetUIFont()`, `SetState()`, `TryPickup()`
- **异步方法加 Async 后缀**: `LoadSaveDataAsync()`

## 属性
- **PascalCase**: `TotalAttack`, `BuffedAttack`, `IsInvincible`
- **布尔属性用 Is/Has/Can 前缀**: `IsReady`, `HasBuff`, `CanAttack`

## 字段
- **私有字段用 camelCase 加下划线前缀**: `_player`, `_isDashing`
- **公共字段用 PascalCase（仅用于常量和只读数据）**: `Instance`, `PoolSize`

## 常量
- **PascalCase**: `MaxPoolSize`, `InvincibilityTime`

## 局部变量
- **camelCase**: `player`, `damage`, `currentHp`

## 参数
- **camelCase**: `damage`, `target`, `skillIndex`

## 枚举
- **PascalCase**: `PlayerState`, `HeroClass`, `ItemRarity`
- **枚举值 PascalCase**: `Idle`, `Dashing`, `Invincible`

## 命名空间
- **PascalCase**: `Player`, `Combat`, `UI`, `Core`

## 文件名
- **PascalCase**: `PlayerController.cs`, `GameManager.cs`
- **对应主要类名**: `PlayerController` → `PlayerController.cs`

## 后缀规范
- **UI 组件加 UI 后缀**: `GameUI`, `ShopUI`, `EquipUI`
- **管理器加 Manager 后缀**: `GameManager`, `UIManager`, `VFXPoolManager`
- **控制器加 Controller 后缀**: `PlayerController`, `EnemyController`
- **数据类加 Data/Model 后缀**: `PlayerProgressData`, `ItemData`
- **工具类加 Helper 后缀**: `VFXHelper`, `UIHelper`, `StringBuilderPool`（静态工具类）

## 避免的命名
- 避免缩写：用 `Character` 不用 `Char`
- 避免匈牙利记法：不用 `intHp`, `strName`
- 避免下划线前缀（私有字段除外）：不用 `_player` 作为公共成员
- 避免单字母变量（循环计数器除外）：不用 `a`, `b`, `x`