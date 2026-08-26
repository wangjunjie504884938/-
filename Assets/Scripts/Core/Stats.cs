using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Backward-compatible facade that delegates to PlayerProgressData (persistent) 
/// and PlayerRuntimeStats (live combat state). All external code continues to 
/// use Stats.* without changes.
/// </summary>
[System.Serializable]
public class Stats
{
    public PlayerProgressData Progress;
    public PlayerRuntimeStats Runtime;

    // --- Field aliases for backward compatibility ---
    public HeroClass Class { get => Progress.Class; set => Progress.Class = value; }
    public int Level { get => Progress.Level; set => Progress.Level = value; }
    public int MaxHp { get => Progress.BaseMaxHp; set => Progress.BaseMaxHp = value; }
    public int Hp { get => Runtime.Hp; set => Runtime.Hp = value; }
    public int Attack { get => Progress.BaseAttack; set => Progress.BaseAttack = value; }
    public int Defense { get => Progress.BaseDefense; set => Progress.BaseDefense = value; }
    public float MoveSpeed { get => Progress.BaseMoveSpeed; set => Progress.BaseMoveSpeed = value; }
    public int Xp { get => Progress.Xp; set => Progress.Xp = value; }
    public int XpToNextLevel { get => Progress.XpToNextLevel; set => Progress.XpToNextLevel = value; }
    public float HpRegen { get => Runtime.HpRegen; set => Runtime.HpRegen = value; }
    public float CritChance { get => Progress.BaseCritChance; set => Progress.BaseCritChance = value; }
    public float XpMultiplier { get => Runtime.XpMultiplier; set => Runtime.XpMultiplier = value; }
    public float LifeSteal { get => Runtime.LifeSteal; set => Runtime.LifeSteal = value; }
    public float AttackRange { get => Progress.BaseAttackRange; set => Progress.BaseAttackRange = value; }
    public float AttackSpeed { get => Runtime.AttackSpeed; set => Runtime.AttackSpeed = value; }
    public float AttackCooldown { get => Progress.BaseAttackCooldown; set => Progress.BaseAttackCooldown = value; }
    public int Gold { get => Progress.Gold; set => Progress.Gold = value; }
    public int SkillPoints { get => Progress.SkillPoints; set => Progress.SkillPoints = value; }
    public int HighestStageCleared { get => Progress.HighestStageCleared; set => Progress.HighestStageCleared = value; }

    public int BonusAttack { get => Runtime.BonusAttack; set => Runtime.BonusAttack = value; }
    public int BonusDefense { get => Runtime.BonusDefense; set => Runtime.BonusDefense = value; }
    public int BonusMaxHp { get => Runtime.BonusMaxHp; set => Runtime.BonusMaxHp = value; }
    public float BonusMoveSpeed { get => Runtime.BonusMoveSpeed; set => Runtime.BonusMoveSpeed = value; }
    public float BonusCritChance { get => Runtime.BonusCritChance; set => Runtime.BonusCritChance = value; }
    public float BonusLifeSteal { get => Runtime.BonusLifeSteal; set => Runtime.BonusLifeSteal = value; }
    public float BonusAttackRange { get => Runtime.BonusAttackRange; set => Runtime.BonusAttackRange = value; }
    public float BonusAttackSpeed { get => Runtime.BonusAttackSpeed; set => Runtime.BonusAttackSpeed = value; }

    public float BuffAttackMult { get => Runtime.BuffAttackMult; set => Runtime.BuffAttackMult = value; }
    public float BuffDefenseMult { get => Runtime.BuffDefenseMult; set => Runtime.BuffDefenseMult = value; }
    public float BuffEndTime { get => Runtime.BuffEndTime; set => Runtime.BuffEndTime = value; }
    public int ShieldHp { get => Runtime.ShieldHp; set => Runtime.ShieldHp = value; }
    public float ShieldEndTime { get => Runtime.ShieldEndTime; set => Runtime.ShieldEndTime = value; }

    // --- Derived stat aliases ---
    public int TotalAttack => Runtime.TotalAttack;
    public int TotalDefense => Runtime.TotalDefense;
    public int TotalMaxHp => Runtime.TotalMaxHp;
    public float TotalMoveSpeed => Runtime.TotalMoveSpeed;
    public float TotalCritChance => Runtime.TotalCritChance;
    public float TotalLifeSteal => Runtime.TotalLifeSteal;
    public float TotalAttackRange => Runtime.TotalAttackRange;
    public float GetSkillRange(float coeff) => Runtime.GetSkillRange(coeff);
    public float TotalAttackSpeed => Runtime.TotalAttackSpeed;
    public float EffectiveAttackCooldown => Runtime.EffectiveAttackCooldown;
    public int BuffedAttack => Runtime.BuffedAttack;
    public int BuffedDefense => Runtime.BuffedDefense;

    /// <summary>
    /// 综合战力值 — 加权计算所有战斗属性，用于排行榜和匹配
    /// </summary>
    public int GearScore
    {
        get
        {
            int score = 0;
            score += BuffedAttack * 10;
            score += BuffedDefense * 8;
            score += TotalMaxHp * 2;
            score += Mathf.RoundToInt(TotalCritChance * 500f);
            score += Mathf.RoundToInt(TotalLifeSteal * 800f);
            score += Mathf.RoundToInt(TotalAttackSpeed * 200f);
            score += Mathf.RoundToInt(TotalMoveSpeed * 30f);
            score += Mathf.RoundToInt(TotalAttackRange * 50f);
            score += Progress.Level * 100;
            return score;
        }
    }

    public Stats(HeroClass heroClass)
    {
        Progress = new PlayerProgressData(heroClass);
        Runtime = new PlayerRuntimeStats(Progress);
    }

    public Stats(int level, int maxHp, int attack, int defense, float moveSpeed)
    {
        Progress = new PlayerProgressData(HeroClass.Warrior);
        Progress.Level = level;
        Progress.BaseMaxHp = maxHp;
        Progress.BaseAttack = attack;
        Progress.BaseDefense = defense;
        Progress.BaseMoveSpeed = moveSpeed;
        Progress.BaseCritChance = 0.08f;
        Progress.BaseAttackRange = 1.0f;
        Progress.BaseAttackCooldown = 0.6f;
        Progress.XpToNextLevel = 50 + level * 30;
        Runtime = new PlayerRuntimeStats(Progress);
        Runtime.AttackSpeed = 1f;
        Runtime.XpMultiplier = 1f;
    }

    public void AddXp(int amount)
    {
        int oldLevel = Progress.Level;
        Progress.AddXp(amount, Runtime.XpMultiplier);
        if (Progress.Level > oldLevel)
            Runtime.Hp = TotalMaxHp;
    }

    public int TakeDamage(int rawDamage) => Runtime.TakeDamage(rawDamage);

    public void RegenerateHp() => Runtime.RegenerateHp();

    public void UpdateBuffs() => Runtime.UpdateBuffs();

    public void AddGold(int amount)
    {
        // 优先服务器权威操作
        if (CloudSaveManager.HasInstance && CloudSaveManager.Instance.IsLoggedIn)
        {
            if (CloudSaveManager.Instance.AddGoldServer(amount, "gameplay"))
                return; // 服务器已更新Gold并同步到本地
        }
        // 服务器不可用时本地操作
        Progress.AddGold(amount);
    }

    public bool SpendGold(int amount)
    {
        // 优先服务器权威操作
        if (CloudSaveManager.HasInstance && CloudSaveManager.Instance.IsLoggedIn)
        {
            if (CloudSaveManager.Instance.SpendGoldServer(amount, "gameplay"))
                return true; // 服务器扣除成功并同步到本地
            return false; // 服务器拒绝(金币不足)
        }
        // 服务器不可用时本地操作
        return Progress.SpendGold(amount);
    }

    public bool SpendSkillPoint() => Progress.SpendSkillPoint();

    public void RecalculateEquipmentBonuses(List<EquipmentSlot> equipped)
        => Runtime.RecalculateEquipmentBonuses(equipped);

    /// <summary>重新应用被动树效果 (装备变更后调用)</summary>
    public void RecalculatePassiveTree()
    {
        PassiveTreeFlags.Reset();
        if (Progress.PassiveNodeIds != null && Progress.PassiveNodeIds.Count > 0)
        {
            var allocatedSet = new HashSet<int>(Progress.PassiveNodeIds);
            PassiveTree.ApplyEffects(Progress, Runtime, allocatedSet);
        }
    }

    // --- Save/Load delegates ---
    private float _lastUploadTime;
    private const float MinUploadInterval = 5f; // 最少间隔5秒才上传一次
    private const int MaxPlayerPrefsSize = 500000; // 500KB 上限

    // ====== 增量保存：脏标记 + 节流 ======
    private bool _isDirty = false;
    private float _lastDirtyTime = 0f;
    private const float DirtySaveInterval = 3f; // 脏数据3秒后自动保存

    /// <summary>标记数据已变更（下次CheckDirtySave自动保存）</summary>
    public void MarkDirty()
    {
        _isDirty = true;
        _lastDirtyTime = Time.time;
    }

    /// <summary>检查脏标记，节流保存（在PlayerController.Update中调用）</summary>
    public void CheckDirtySave()
    {
        if (_isDirty && Time.time - _lastDirtyTime > DirtySaveInterval && Time.time - _lastUploadTime > MinUploadInterval)
        {
            Save();
            _isDirty = false;
        }
    }

    public void Save()
    {
        Progress.Save();
        var inv = GameManager.Instance?.Player?.Inventory;
        if (inv != null)
        {
            string eqJson = inv.ToJson();
            // 超过500KB则使用文件存储
            if (eqJson.Length > MaxPlayerPrefsSize)
            {
                string filePath = System.IO.Path.Combine(Application.persistentDataPath, $"equipment_slot{PlayerProgressData.ActiveSlot}.json");
                System.IO.File.WriteAllText(filePath, eqJson);
                PlayerPrefs.SetString($"ARPG_S{PlayerProgressData.ActiveSlot}_EquipmentPath", filePath);
            }
            else
            {
                PlayerPrefs.SetString($"ARPG_S{PlayerProgressData.ActiveSlot}_Equipment", eqJson);
            }
        }
        if (AchievementManager.Instance != null)
            PlayerPrefs.SetString($"ARPG_S{PlayerProgressData.ActiveSlot}_Achievements", AchievementManager.Instance.Serialize());
        PlayerPrefs.Save(); // 立即写入磁盘
                    GameLog.Log($"[Save] slot={PlayerProgressData.ActiveSlot} level={Progress.Level} gold={Progress.Gold} backpack={inv?.Backpack?.Count ?? -1} equipped={inv?.Equipped?.Count ?? -1}");

        // 节流上传：5秒内不重复上传，使用统一更新接口（带版本号）
        if (CloudSaveManager.Instance != null && CloudSaveManager.Instance.IsLoggedIn && Time.time - _lastUploadTime > MinUploadInterval)
        {
            _lastUploadTime = Time.time;
            CloudSaveManager.Instance.StartCoroutine(CloudSaveManager.Instance.UpdatePlayerData(null));
        }
    }

    public static bool HasSaveData() => PlayerProgressData.HasSaveData();

    public static HeroClass GetSavedClass() => PlayerProgressData.GetSavedClass();

    public void LoadSaved()
    {
        Progress.LoadSaved();
        Runtime.SyncFromProgress();

        // Restore saved HP (or full if no HP saved)
        int savedHp = Progress.GetSavedHp();
        Runtime.Hp = savedHp > 0 ? Mathf.Min(savedHp, TotalMaxHp) : TotalMaxHp;

        var inv = GameManager.Instance?.Player?.Inventory;
        if (inv != null)
        {
            string eqKey = $"ARPG_S{PlayerProgressData.ActiveSlot}_Equipment";
            string eqPathKey = $"ARPG_S{PlayerProgressData.ActiveSlot}_EquipmentPath";
            if (PlayerPrefs.HasKey(eqPathKey))
            {
                // 大文件模式：从文件读取
                string filePath = PlayerPrefs.GetString(eqPathKey);
                if (System.IO.File.Exists(filePath))
                    inv.LoadFromJson(System.IO.File.ReadAllText(filePath));
            }
            else if (PlayerPrefs.HasKey(eqKey))
            {
                inv.LoadFromJson(PlayerPrefs.GetString(eqKey));
            }
        }

                    GameLog.Log($"[LoadSaved] slot={PlayerProgressData.ActiveSlot} level={Progress.Level} backpack={inv?.Backpack?.Count ?? -1} equipped={inv?.Equipped?.Count ?? -1}");

        Progress.LoadSavedSkills();

        if (AchievementManager.Instance != null && PlayerPrefs.HasKey($"ARPG_S{PlayerProgressData.ActiveSlot}_Achievements"))
            AchievementManager.Instance.Deserialize(PlayerPrefs.GetString($"ARPG_S{PlayerProgressData.ActiveSlot}_Achievements"));

        // Apply passive tree effects (PoE2-style)
        PassiveTreeFlags.Reset();
        if (Progress.PassiveNodeIds != null && Progress.PassiveNodeIds.Count > 0)
        {
            var allocatedSet = new HashSet<int>(Progress.PassiveNodeIds);
            PassiveTree.ApplyEffects(Progress, Runtime, allocatedSet);
        }
    }

    public static void ClearSave() => PlayerProgressData.ClearSave();
}
