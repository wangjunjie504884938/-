using UnityEngine;
using System;
using System.IO;
using System.IO.Compression;

/// <summary>
/// 存档数据传输对象 — 客户端与服务器之间的存档数据格式
/// 将当前 PlayerPrefs 中的所有零散数据合并为一个 JSON 对象
/// 上传时 FromCurrentState() 构建, 下载时 ApplyToPlayer() 应用
/// v2: 增加版本号+符文数据+增量保存支持
/// </summary>
[Serializable]
public class SaveDataDTO
{
    // ====== 版本管理 ======
    public int saveVersion = 2;       // 存档格式版本号（用于迁移）
    public const int CurrentVersion = 2;

    // ====== 基础属性 ======
    public string characterName;          // 角色名称
    public int classType;              // 职业 (HeroClass enum 转 int)
    public int level;                  // 等级
    public int xp;                    // 当前经验
    public int xpToNextLevel;         // 升级所需经验
    public int gold;                  // 金币
    public int skillPoints;           // 可用技能点
    public int highestStageCleared;   // 最高通关关卡

    // ====== 战斗属性 (基础值) ======
    // NOTE: hp (current HP) is intentionally NOT synced to cloud.
    // It is saved locally only (PlayerPrefs) to prevent save-scumming across devices.
    public int baseMaxHp;             // 基础最大生命
    public int baseAttack;            // 基础攻击力
    public int baseDefense;           // 基础防御力
    public float baseMoveSpeed;       // 基础移动速度
    public float baseCritChance;      // 基础暴击率
    public float baseAttackRange;     // 基础攻击范围
    public float baseAttackCooldown;  // 基础攻击间隔
    public float baseHpRegen;         // 基础生命回复

    // ====== 序列化数据 (使用现有格式) ======
    public string skills;             // 技能+符文 CSV (格式同 PlayerProgressData.SaveSkills)
    public string equipment;          // 装备 JSON (格式同 EquipmentInventory.ToJson)
    public string achievements;       // 成就 CSV (格式同 AchievementManager.Serialize)
    public string passiveTree;        // 被动天赋 CSV
    public int dismantleFragments;    // 碎片数量
    public string petData;            // 宠物数据 JSON (owned/level/evolved/activeType)
    public string runeData;          // v2新增：符文数据 JSON

    // ====== 元数据 ======
    public string lastSavedAt;        // 最后保存时间 (服务器写入)

    /// <summary>
    /// 从当前游戏状态构建存档 DTO
    /// 在 CloudSaveManager.UploadSave() 中调用
    /// </summary>
    public static SaveDataDTO FromCurrentState()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Player == null) return null;

        var stats = gm.Player.Stats;
        var progress = stats.Progress;

        var dto = new SaveDataDTO
        {
            characterName = PlayerPrefs.GetString($"ARPG_S{PlayerProgressData.ActiveSlot}_CharacterName", ""),
            classType = (int)progress.Class,
            level = progress.Level,
            xp = progress.Xp,
            xpToNextLevel = progress.XpToNextLevel,
            gold = progress.Gold,
            skillPoints = progress.SkillPoints,
            highestStageCleared = progress.HighestStageCleared,
            baseMaxHp = progress.BaseMaxHp,
            baseAttack = progress.BaseAttack,
            baseDefense = progress.BaseDefense,
            baseMoveSpeed = progress.BaseMoveSpeed,
            baseCritChance = progress.BaseCritChance,
            baseAttackRange = progress.BaseAttackRange,
            baseAttackCooldown = progress.BaseAttackCooldown,
            baseHpRegen = progress.BaseHpRegen,
            skills = PlayerPrefs.GetString($"ARPG_S{PlayerProgressData.ActiveSlot}_Skills", ""),
            equipment = PlayerPrefs.GetString($"ARPG_S{PlayerProgressData.ActiveSlot}_Equipment", ""),
            achievements = PlayerPrefs.GetString($"ARPG_S{PlayerProgressData.ActiveSlot}_Achievements", ""),
            passiveTree = PlayerPrefs.GetString($"ARPG_S{PlayerProgressData.ActiveSlot}_PassiveTree", ""),
            dismantleFragments = PlayerPrefs.GetInt($"ARPG_S{PlayerProgressData.ActiveSlot}_DismantleFragments", 0),
            petData = SerializePetData(),
            runeData = SerializeRuneData(),
            lastSavedAt = DateTime.UtcNow.ToString("o")
        };

        return dto;
    }

    /// <summary>
    /// 从 PlayerPrefs 快照构建存档 DTO (不依赖运行时 Player 对象)
    /// 用于从角色槽位上传本地缓存数据
    /// </summary>
    public static SaveDataDTO FromPlayerPrefs()
    {
        string prefix = $"ARPG_S{PlayerProgressData.ActiveSlot}_";
        int level = PlayerPrefs.GetInt(prefix + "Level", 1);
        return new SaveDataDTO
        {
            characterName = PlayerPrefs.GetString(prefix + "CharacterName", ""),
            classType = PlayerPrefs.GetInt(prefix + "Class", 0),
            level = level,
            xp = PlayerPrefs.GetInt(prefix + "Xp", 0),
            xpToNextLevel = 50 + level * 30,
            gold = PlayerPrefs.GetInt(prefix + "Gold", 0),
            skillPoints = PlayerPrefs.GetInt(prefix + "SkillPoints", 0),
            // HighestStage: 优先用RuntimePlayerData，未加载时用本地缓存
            highestStageCleared = (RuntimePlayerData.Instance != null && RuntimePlayerData.Instance.HighestStage >= 0)
                ? RuntimePlayerData.Instance.HighestStage
                : LocalSettingsManager.GetCachedStage(),
            baseMaxHp = PlayerPrefs.GetInt(prefix + "BaseMaxHp", 0),
            baseAttack = PlayerPrefs.GetInt(prefix + "BaseAttack", 0),
            baseDefense = PlayerPrefs.GetInt(prefix + "BaseDefense", 0),
            baseMoveSpeed = PlayerPrefs.GetFloat(prefix + "BaseMoveSpeed", 0f),
            baseCritChance = PlayerPrefs.GetFloat(prefix + "BaseCritChance", 0f),
            baseAttackRange = PlayerPrefs.GetFloat(prefix + "BaseAttackRange", 0f),
            baseAttackCooldown = PlayerPrefs.GetFloat(prefix + "BaseAttackCooldown", 0f),
            baseHpRegen = PlayerPrefs.GetFloat(prefix + "BaseHpRegen", 0f),
            skills = PlayerPrefs.GetString(prefix + "Skills", ""),
            equipment = PlayerPrefs.GetString(prefix + "Equipment", ""),
            achievements = PlayerPrefs.GetString(prefix + "Achievements", ""),
            passiveTree = PlayerPrefs.GetString(prefix + "PassiveTree", ""),
            dismantleFragments = PlayerPrefs.GetInt(prefix + "DismantleFragments", 0),
            petData = SerializePetData(),
            runeData = SerializeRuneData(),
            saveVersion = CurrentVersion,
            lastSavedAt = DateTime.UtcNow.ToString("o")
        };
    }

    /// <summary>
    /// 存档迁移 — 旧版本存档自动升级到当前版本
    /// </summary>
    public static SaveDataDTO Migrate(SaveDataDTO oldData)
    {
        if (oldData == null) return null;

        // v0 → v2: 添加符文数据
        if (oldData.saveVersion < 2)
        {
            if (string.IsNullOrEmpty(oldData.runeData))
                oldData.runeData = SerializeRuneData();
            oldData.saveVersion = CurrentVersion;
        }

        // 未来版本迁移示例:
        // if (oldData.saveVersion < 3) { oldData.newField = defaultValue; oldData.saveVersion = 3; }

        return oldData;
    }

    // ====== 二进制压缩工具 ======
    // 使用 GZip 压缩 JSON，减少存档体积约 60-70%

    /// <summary>将 DTO 序列化为压缩二进制（Base64编码便于网络传输）</summary>
    public string ToCompressedBase64()
    {
        string json = JsonUtility.ToJson(this);
        byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);
        using (var ms = new MemoryStream())
        {
            using (var gzip = new GZipStream(ms, System.IO.Compression.CompressionLevel.Optimal))
            {
                gzip.Write(jsonBytes, 0, jsonBytes.Length);
            }
            return Convert.ToBase64String(ms.ToArray());
        }
    }

    /// <summary>从压缩Base64反序列化为 DTO（自动迁移）</summary>
    public static SaveDataDTO FromCompressedBase64(string compressed)
    {
        if (string.IsNullOrEmpty(compressed)) return null;
        try
        {
            byte[] compressedBytes = Convert.FromBase64String(compressed);
            using (var ms = new MemoryStream(compressedBytes))
            using (var gzip = new GZipStream(ms, CompressionMode.Decompress))
            using (var reader = new StreamReader(gzip))
            {
                string json = reader.ReadToEnd();
                var dto = JsonUtility.FromJson<SaveDataDTO>(json);
                return Migrate(dto); // 自动迁移
            }
        }
        catch
        {
            // 回退：尝试直接 JSON 解析
            try { return Migrate(JsonUtility.FromJson<SaveDataDTO>(compressed)); }
            catch { return null; }
        }
    }

    /// <summary>序列化当前符文数据为JSON</summary>
    private static string SerializeRuneData()
    {
        return PlayerPrefs.GetString("ARPG_RuneData", "");
    }
    public void ApplyToLocal()
    {
        string prefix = $"ARPG_S{CloudSaveManager.Instance?.ActiveSlot ?? 0}_";

        // Level/Xp/Gold/SkillPoints: 仅在本地无数据时写入（首次从服务器恢复）
        // 通关后本地保存了新数据，不能被服务器旧SaveJson覆盖
        if (!PlayerPrefs.HasKey(prefix + "Level"))
        {
            PlayerPrefs.SetInt(prefix + "Level", level);
            PlayerPrefs.SetInt(prefix + "Xp", xp);
            PlayerPrefs.SetInt(prefix + "Gold", gold);
            PlayerPrefs.SetInt(prefix + "SkillPoints", skillPoints);
        }
        // Class/CharacterName: 每次写入（不变）
        if (!PlayerPrefs.HasKey(prefix + "CharacterName"))
            PlayerPrefs.SetString(prefix + "CharacterName", characterName ?? "");
        if (!PlayerPrefs.HasKey(prefix + "Class"))
            PlayerPrefs.SetInt(prefix + "Class", classType);
        // BaseStats: 仅首次写入
        if (!PlayerPrefs.HasKey(prefix + "BaseMaxHp"))
        {
            PlayerPrefs.SetInt(prefix + "BaseMaxHp", baseMaxHp);
            PlayerPrefs.SetInt(prefix + "BaseAttack", baseAttack);
            PlayerPrefs.SetInt(prefix + "BaseDefense", baseDefense);
            PlayerPrefs.SetFloat(prefix + "BaseMoveSpeed", baseMoveSpeed);
            PlayerPrefs.SetFloat(prefix + "BaseCritChance", baseCritChance);
            PlayerPrefs.SetFloat(prefix + "BaseAttackRange", baseAttackRange);
            PlayerPrefs.SetFloat(prefix + "BaseAttackCooldown", baseAttackCooldown);
            PlayerPrefs.SetFloat(prefix + "BaseHpRegen", baseHpRegen);
        }
        // Skills/Equipment/Achievements/PassiveTree: 仅首次写入
        if (!PlayerPrefs.HasKey(prefix + "Skills"))
            PlayerPrefs.SetString(prefix + "Skills", skills ?? "");
        if (!PlayerPrefs.HasKey(prefix + "Equipment"))
        {
            PlayerPrefs.SetString(prefix + "Equipment", equipment ?? "");
                        GameLog.Log($"[ApplyToLocal] 首次写入装备: len={equipment?.Length ?? 0}");
        }
        else
        {
                        GameLog.Log($"[ApplyToLocal] 保留本地装备(HasKey=true), 服务器装备len={equipment?.Length ?? 0}");
        }
        if (!PlayerPrefs.HasKey(prefix + "Achievements"))
            PlayerPrefs.SetString(prefix + "Achievements", achievements ?? "");
        if (!PlayerPrefs.HasKey(prefix + "PassiveTree"))
            PlayerPrefs.SetString(prefix + "PassiveTree", passiveTree ?? "");
        // DismantleFragments: 仅首次写入
        if (!PlayerPrefs.HasKey(prefix + "DismantleFragments"))
            PlayerPrefs.SetInt(prefix + "DismantleFragments", dismantleFragments);
        DeserializePetData(petData);
        // v2: 恢复符文数据（总是覆盖，因为符文数据是账号级别的）
        if (!string.IsNullOrEmpty(runeData))
            PlayerPrefs.SetString("ARPG_RuneData", runeData);
        // 迁移后确保版本号最新
        saveVersion = CurrentVersion;
        PlayerPrefs.Save();
    }

    /// <summary>序列化当前槽位宠物数据为JSON</summary>
    private static string SerializePetData()
    {
        int slot = PlayerProgressData.ActiveSlot;
        var sb = new System.Text.StringBuilder();
        sb.Append("{\"slot\":").Append(slot).Append(",\"pets\":[");
        string[] petNames = { "Spirit", "Flame", "Guardian", "Shadow" };
        bool first = true;
        foreach (var name in petNames)
        {
            string p = $"ARPG_S{slot}_Pet_{name}_";
            int owned = PlayerPrefs.GetInt(p + "Owned", 0);
            if (owned == 0) continue;
            if (!first) sb.Append(",");
            sb.Append($"{{\"name\":\"{name}\",\"level\":{PlayerPrefs.GetInt(p+"Level",1)},\"evolved\":{PlayerPrefs.GetInt(p+"Evolved",0)}}}");
            first = false;
        }
        sb.Append("],\"active\":").Append(PlayerPrefs.GetInt($"ARPG_S{slot}_Pet_ActiveType", -1)).Append("}");
        return sb.ToString();
    }

    /// <summary>从JSON恢复宠物数据到PlayerPrefs</summary>
    private void DeserializePetData(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        int slot = CloudSaveManager.Instance?.ActiveSlot ?? 0;
        try
        {
            var data = JsonUtility.FromJson<PetDataJson>(json);
            if (data == null) return;
            foreach (var pet in data.pets ?? System.Array.Empty<PetEntryJson>())
            {
                string p = $"ARPG_S{slot}_Pet_{pet.name}_";
                PlayerPrefs.SetInt(p + "Owned", 1);
                PlayerPrefs.SetInt(p + "Level", pet.level);
                PlayerPrefs.SetInt(p + "Evolved", pet.evolved);
            }
            PlayerPrefs.SetInt($"ARPG_S{slot}_Pet_ActiveType", data.active);
        }
        catch { /* ignore parse errors */ }
    }
}

[Serializable]
public class PetDataJson { public PetEntryJson[] pets; public int active; }

[Serializable]
public class PetEntryJson { public string name; public int level; public int evolved; }
