using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 场景对象注册表 — 替代FindObjectsOfType, 提高性能
/// 临时对象在OnEnable注册, OnDisable注销
/// </summary>
public static class SceneRegistry
{
    private static readonly HashSet<EnemyController> _enemies = new();
    private static readonly HashSet<LootPickup> _lootPickups = new();
    private static readonly HashSet<RelicPickup> _relicPickups = new();
    private static readonly HashSet<EnemyProjectile> _enemyProjectiles = new();
    private static readonly HashSet<PlayerProjectile> _playerProjectiles = new();
    private static readonly HashSet<PetCompanion> _pets = new();
    private static readonly HashSet<BossDropAnimation> _bossDrops = new();

    public static void Register(EnemyController obj) => _enemies.Add(obj);
    public static void Unregister(EnemyController obj) => _enemies.Remove(obj);
    public static void Register(LootPickup obj) => _lootPickups.Add(obj);
    public static void Unregister(LootPickup obj) => _lootPickups.Remove(obj);
    public static void Register(RelicPickup obj) => _relicPickups.Add(obj);
    public static void Unregister(RelicPickup obj) => _relicPickups.Remove(obj);
    public static void Register(EnemyProjectile obj) => _enemyProjectiles.Add(obj);
    public static void Unregister(EnemyProjectile obj) => _enemyProjectiles.Remove(obj);
    public static void Register(PlayerProjectile obj) => _playerProjectiles.Add(obj);
    public static void Unregister(PlayerProjectile obj) => _playerProjectiles.Remove(obj);
    public static void Register(PetCompanion obj) => _pets.Add(obj);
    public static void Unregister(PetCompanion obj) => _pets.Remove(obj);
    public static void Register(BossDropAnimation obj) => _bossDrops.Add(obj);
    public static void Unregister(BossDropAnimation obj) => _bossDrops.Remove(obj);

    private static readonly List<PetCompanion> _petResultBuffer = new List<PetCompanion>();
    public static List<PetCompanion> GetPets()
    {
        _petResultBuffer.Clear();
        _petResultBuffer.AddRange(_pets);
        return _petResultBuffer;
    }

    /// <summary>清理所有临时对象 (通关/死亡/返回大厅时调用)</summary>
    public static void ClearAll()
    {
        DestroyAll(_enemies);
        DestroyAll(_lootPickups);
        DestroyAll(_relicPickups);
        DestroyAll(_enemyProjectiles);
        DestroyAll(_playerProjectiles);
        DestroyAll(_pets);
        DestroyAll(_bossDrops);
    }

    /// <summary>清理投射物和掉落物 (保留敌人)</summary>
    public static void ClearProjectilesAndLoot()
    {
        DestroyAll(_lootPickups);
        DestroyAll(_relicPickups);
        DestroyAll(_enemyProjectiles);
        DestroyAll(_playerProjectiles);
        DestroyAll(_bossDrops);
    }

    private static void DestroyAll<T>(HashSet<T> set) where T : Component
    {
        if (set.Count == 0) return;
        var items = new T[set.Count];
        set.CopyTo(items);
        set.Clear();
        // 延迟销毁，确保渲染线程完成当前帧的GPU资源释放
        foreach (var item in items)
            if (item != null) Object.Destroy(item.gameObject);
    }
}
