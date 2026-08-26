using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class PlayerProgressData
{
    public HeroClass Class;
    public int Level;
    public int BaseMaxHp;
    public int BaseAttack;
    public int BaseDefense;
    public float BaseMoveSpeed;
    public float BaseCritChance;
    public float BaseAttackRange;
    public float BaseAttackCooldown;
    public float BaseHpRegen;
    public int Xp;
    public int XpToNextLevel;
    public int Gold;
    public int SkillPoints;
    public int DismantleFragments;
    public int HighestStageCleared;

    // Passive tree allocated node IDs (PoE2-style)
    public List<int> PassiveNodeIds = new List<int>();

    public PlayerProgressData(HeroClass heroClass)
    {
        Class = heroClass;
        Level = 1;
        var data = ClassData.GetClassData(heroClass);
        if (data == null) return;

        BaseMaxHp = data.BaseHp;
        BaseAttack = data.BaseAttack;
        BaseDefense = data.BaseDefense;
        BaseMoveSpeed = data.BaseMoveSpeed;
        BaseAttackRange = data.BaseAttackRange;
        BaseAttackCooldown = data.BaseAttackCooldown;
        BaseCritChance = data.BaseCritChance;
        BaseHpRegen = data.BaseHpRegen;
        Xp = 0;
        XpToNextLevel = CalculateXpNeeded(Level);
        Gold = 0;
        SkillPoints = 0;
        HighestStageCleared = -1; // -1=未通关
    }

    public void AddXp(int amount, float multiplier)
    {
        Xp += Mathf.RoundToInt(amount * multiplier);
        while (Xp >= XpToNextLevel)
        {
            Xp -= XpToNextLevel;
            LevelUp();
        }
    }

    private void LevelUp()
    {
        Level++;
        var data = ClassData.GetClassData(Class);
        if (data != null)
        {
            BaseMaxHp += data.HpPerLevel;
            BaseAttack += data.AttackPerLevel;
            BaseDefense += data.DefensePerLevel;
        }
        else
        {
            BaseMaxHp += 10;
            BaseAttack += 2;
            BaseDefense += 1;
        }
        SkillPoints++;
        XpToNextLevel = CalculateXpNeeded(Level);
    }

    private int CalculateXpNeeded(int level)
    {
        int baseXp = 50;
        int perLevel = 30;
        if (GameConfigManager.Instance != null && GameConfigManager.Instance.IsLoaded)
        {
            baseXp = GameConfigManager.Instance.GetGlobalInt("XpFormulaBase", 50);
            perLevel = GameConfigManager.Instance.GetGlobalInt("XpFormulaPerLevel", 30);
        }
        return baseXp + level * perLevel;
    }

    public void AddGold(int amount)
    {
        Gold += amount;
        if (AchievementManager.Instance != null)
            AchievementManager.Instance.RecordGoldEarned(amount);
    }

    public bool SpendGold(int amount)
    {
        if (Gold < amount) return false;
        Gold -= amount;
        return true;
    }

    public bool SpendSkillPoint()
    {
        if (SkillPoints <= 0) return false;
        SkillPoints--;
        return true;
    }

    // --- Save/Load (slot-aware) ---
    private const string BasePrefix = "ARPG_";

    /// <summary>当前槽位的 PlayerPrefs key 前缀</summary>
    private static int _activeSlot = 0;
    public static int ActiveSlot => _activeSlot;

    /// <summary>设置活跃槽位 (0-2), 影响所有后续 Save/Load 操作</summary>
    public static void SetActiveSlot(int slot)
    {
        _activeSlot = Mathf.Clamp(slot, 0, 2);
    }

    /// <summary>当前槽位的完整 key 前缀, 如 "ARPG_S0_"</summary>
    private static string KeyPrefix => $"{BasePrefix}S{_activeSlot}_";

    public void Save()
    {
        PlayerPrefs.SetInt(KeyPrefix + "Class", (int)Class);
        PlayerPrefs.SetInt(KeyPrefix + "Level", Level);
        PlayerPrefs.SetInt(KeyPrefix + "Xp", Xp);
        PlayerPrefs.SetInt(KeyPrefix + "Gold", Gold);
        PlayerPrefs.SetInt(KeyPrefix + "SkillPoints", SkillPoints);
        PlayerPrefs.SetInt(KeyPrefix + "DismantleFragments", DismantleFragments);
        // HighestStage 不再写入 PlayerPrefs — 服务器权威，由 RuntimePlayerData 管理
        PlayerPrefs.SetInt(KeyPrefix + "BaseMaxHp", BaseMaxHp);
        PlayerPrefs.SetInt(KeyPrefix + "BaseAttack", BaseAttack);
        PlayerPrefs.SetInt(KeyPrefix + "BaseDefense", BaseDefense);
        PlayerPrefs.SetFloat(KeyPrefix + "BaseMoveSpeed", BaseMoveSpeed);
        PlayerPrefs.SetFloat(KeyPrefix + "BaseCritChance", BaseCritChance);
        PlayerPrefs.SetFloat(KeyPrefix + "BaseAttackRange", BaseAttackRange);
        PlayerPrefs.SetFloat(KeyPrefix + "BaseAttackCooldown", BaseAttackCooldown);
        PlayerPrefs.SetFloat(KeyPrefix + "BaseHpRegen", BaseHpRegen);
        SaveSkills();
        SavePassiveTree();
        SaveHp();
        PlayerPrefs.Save();
        // 云端上传由 Stats.Save() 统一处理, 这里不重复上传
    }

    public static void SaveCharacterName(string name)
    {
        PlayerPrefs.SetString(KeyPrefix + "CharacterName", name ?? "");
        PlayerPrefs.Save();
    }

    public static string GetSavedCharacterName()
    {
        return PlayerPrefs.GetString(KeyPrefix + "CharacterName", "");
    }

    private void SaveHp()
    {
        var player = GameManager.Instance?.Player;
        if (player != null)
            PlayerPrefs.SetInt(KeyPrefix + "Hp", player.Stats.Hp);
    }

    private void SavePassiveTree()
    {
        if (PassiveNodeIds == null || PassiveNodeIds.Count == 0)
        {
            PlayerPrefs.DeleteKey(KeyPrefix + "PassiveTree");
            return;
        }
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < PassiveNodeIds.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(PassiveNodeIds[i]);
        }
        PlayerPrefs.SetString(KeyPrefix + "PassiveTree", sb.ToString());
    }

    private void LoadPassiveTree()
    {
        PassiveNodeIds = new List<int>();
        string data = PlayerPrefs.GetString(KeyPrefix + "PassiveTree", "");
        if (string.IsNullOrEmpty(data)) return;
        foreach (var part in data.Split(','))
        {
            if (int.TryParse(part, out int id))
                PassiveNodeIds.Add(id);
        }
    }

    private void SaveSkills()
    {
        var player = GameManager.Instance?.Player;
        if (player == null || player.Skills == null) return;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < player.Skills.Count; i++)
        {
            if (i > 0) sb.Append(';');
            var sk = player.Skills[i];
            sb.Append($"{(int)sk.Slot},{sk.CurrentLevel}");
            if (sk.EquippedRunes != null && sk.EquippedRunes.Count > 0)
            {
                sb.Append(':');
                for (int r = 0; r < sk.EquippedRunes.Count; r++)
                {
                    if (r > 0) sb.Append('|');
                    sb.Append($"{(int)sk.EquippedRunes[r].Type},{sk.EquippedRunes[r].Level}");
                }
            }
        }
        PlayerPrefs.SetString(KeyPrefix + "Skills", sb.ToString());
    }

    public void LoadSavedSkills()
    {
        var player = GameManager.Instance?.Player;
        if (player == null || player.Skills == null) return;
        string data = PlayerPrefs.GetString(KeyPrefix + "Skills", "");
        if (string.IsNullOrEmpty(data)) return;

        var entries = data.Split(';');
        foreach (var entry in entries)
        {
            var mainParts = entry.Split(':');
            var skillParts = mainParts[0].Split(',');
            if (skillParts.Length < 2) continue;
            if (!int.TryParse(skillParts[0], out int slotIdx)) continue;
            if (!int.TryParse(skillParts[1], out int skillLevel)) continue;
            if (slotIdx < 0 || slotIdx >= player.Skills.Count) continue;

            player.Skills[slotIdx].CurrentLevel = skillLevel;

            if (mainParts.Length >= 2)
            {
                var runeEntries = mainParts[1].Split('|');
                foreach (var runeEntry in runeEntries)
                {
                    var runeParts = runeEntry.Split(',');
                    if (runeParts.Length >= 2 && int.TryParse(runeParts[0], out int runeType) && int.TryParse(runeParts[1], out int runeLvl))
                    {
                        player.Skills[slotIdx].EquipRune(RuneData.Create((RuneType)runeType, runeLvl));
                    }
                }
            }
        }
    }

    public static bool HasSaveData() => PlayerPrefs.HasKey(KeyPrefix + "Class");

    public static HeroClass GetSavedClass() => (HeroClass)PlayerPrefs.GetInt(KeyPrefix + "Class", 0);

    public void LoadSaved()
    {
        if (!HasSaveData()) return;
        Class = (HeroClass)PlayerPrefs.GetInt(KeyPrefix + "Class", 0);
        Level = PlayerPrefs.GetInt(KeyPrefix + "Level", 1);
        Xp = PlayerPrefs.GetInt(KeyPrefix + "Xp", 0);
        Gold = PlayerPrefs.GetInt(KeyPrefix + "Gold", 0);
        SkillPoints = PlayerPrefs.GetInt(KeyPrefix + "SkillPoints", 0);
        DismantleFragments = PlayerPrefs.GetInt(KeyPrefix + "DismantleFragments", 0);
        // HighestStage 不再从 PlayerPrefs 读取 — 由 RuntimePlayerData/服务器提供
        XpToNextLevel = CalculateXpNeeded(Level);
        LoadPassiveTree();
        // Restore base combat stats if saved; otherwise recalculate from class data + level
        if (PlayerPrefs.HasKey(KeyPrefix + "BaseMaxHp"))
        {
            BaseMaxHp = PlayerPrefs.GetInt(KeyPrefix + "BaseMaxHp");
            BaseAttack = PlayerPrefs.GetInt(KeyPrefix + "BaseAttack");
            BaseDefense = PlayerPrefs.GetInt(KeyPrefix + "BaseDefense");
            BaseMoveSpeed = PlayerPrefs.GetFloat(KeyPrefix + "BaseMoveSpeed");
            BaseCritChance = PlayerPrefs.GetFloat(KeyPrefix + "BaseCritChance");
            BaseAttackRange = PlayerPrefs.GetFloat(KeyPrefix + "BaseAttackRange");
            BaseAttackCooldown = PlayerPrefs.GetFloat(KeyPrefix + "BaseAttackCooldown");
            BaseHpRegen = PlayerPrefs.GetFloat(KeyPrefix + "BaseHpRegen");
        }
        else
        {
            // Legacy save without base stats: recalculate from class data by replaying level-ups
            var data = ClassData.GetClassData(Class);
            if (data != null)
            {
                BaseMaxHp = data.BaseHp;
                BaseAttack = data.BaseAttack;
                BaseDefense = data.BaseDefense;
                BaseMoveSpeed = data.BaseMoveSpeed;
                BaseAttackRange = data.BaseAttackRange;
                BaseAttackCooldown = data.BaseAttackCooldown;
                BaseCritChance = data.BaseCritChance;
                BaseHpRegen = data.BaseHpRegen;
                for (int lv = 1; lv < Level; lv++)
                {
                    BaseMaxHp += data.HpPerLevel;
                    BaseAttack += data.AttackPerLevel;
                    BaseDefense += data.DefensePerLevel;
                }
            }
        }
    }

    public int GetSavedHp()
    {
        return PlayerPrefs.GetInt(KeyPrefix + "Hp", -1);
    }

    public static void ClearSave()
    {
        string prefix = KeyPrefix;
        // 清除当前槽位的所有 PlayerPrefs key (按前缀匹配)
        // 已知的全部 key 显式删除
        string[] knownKeys = {
            "Class", "Level", "Xp", "Gold", "SkillPoints", "HighestStage",
            "Skills", "Equipment", "Hp", "Achievements",
            "BaseMaxHp", "BaseAttack", "BaseDefense",
            "BaseMoveSpeed", "BaseCritChance", "BaseAttackRange",
            "BaseAttackCooldown", "BaseHpRegen",
            "CharacterName", "PassiveTree", "DismantleFragments",
        };
        foreach (var key in knownKeys)
            PlayerPrefs.DeleteKey(prefix + key);

        // 宠物数据 (key用枚举名: Spirit/Flame/Guardian/Shadow)
        PlayerPrefs.DeleteKey(prefix + "Pet_ActiveType");
        string[] petNames = { "Spirit", "Flame", "Guardian", "Shadow" };
        foreach (var petName in petNames)
        {
            PlayerPrefs.DeleteKey(prefix + "Pet_" + petName + "_Owned");
            PlayerPrefs.DeleteKey(prefix + "Pet_" + petName + "_Level");
            PlayerPrefs.DeleteKey(prefix + "Pet_" + petName + "_Evolved");
        }

        // 兜底: 清除残留的旧格式数字key
        for (int i = 0; i < 4; i++)
        {
            PlayerPrefs.DeleteKey(prefix + "Pet_" + i + "_Owned");
            PlayerPrefs.DeleteKey(prefix + "Pet_" + i + "_Level");
            PlayerPrefs.DeleteKey(prefix + "Pet_" + i + "_Evolved");
        }

        // 清除账号级别的非槽位数据 (公会/PVP/签到/日常/对话/锻造/抽卡/离线奖励/通行证)
        string[] accountKeys = {
            "ARPG_GuildName", "ARPG_GuildLevel", "ARPG_GuildExp",
            "ARPG_PvpWins", "ARPG_PvpLosses", "ARPG_PvpDefRating",
            "ARPG_GhostUploaded",
            "ARPG_SignIn_LastDate", "ARPG_SignIn_Days",
            "ARPG_LastOnlineTime", "ARPG_OfflineRewardClaimed",
            "ARPG_ForgeHistory",
            // 通行证数据
            "ARPG_Season_Id", "ARPG_Season_XP", "ARPG_Season_Level",
            "ARPG_Season_Premium", "ARPG_Season_FreeClaimed", "ARPG_Season_PremiumClaimed",
            "ARPG_Season_WeekStart",
        };
        foreach (var key in accountKeys)
            PlayerPrefs.DeleteKey(key);

        // 通行证领取记录 (key格式: ARPG_Season_FreeClaimed_{level} / ARPG_Season_PremiumClaimed_{level})
        for (int i = 1; i <= 30; i++)
        {
            PlayerPrefs.DeleteKey($"ARPG_Season_FreeClaimed_{i}");
            PlayerPrefs.DeleteKey($"ARPG_Season_PremiumClaimed_{i}");
        }

        // 清除对话进度 (key格式: ARPG_DialogueStart_{i} / ARPG_DialogueEnd_{i})
        for (int i = 0; i < 50; i++)
        {
            PlayerPrefs.DeleteKey($"ARPG_DialogueStart_{i}");
            PlayerPrefs.DeleteKey($"ARPG_DialogueEnd_{i}");
        }

        // 清除每日任务进度 (key格式: ARPG_Daily_{dateKey}_task{type}_progress / _claimed)
        // 每日任务key带日期, 无法穷举, 但可以用日期范围清理最近30天
        for (int d = 0; d < 30; d++)
        {
            var date = System.DateTime.Now.AddDays(-d).ToString("yyyyMMdd");
            for (int t = 0; t < 10; t++)
            {
                PlayerPrefs.DeleteKey($"ARPG_Daily_{date}_task{t}_progress");
                PlayerPrefs.DeleteKey($"ARPG_Daily_{date}_task{t}_claimed");
            }
            PlayerPrefs.DeleteKey($"ARPG_DailyChallenge_{date}");
        }

        // 清除新手引导提示记录
        PlayerPrefs.DeleteKey("ARPG_Tip_newplayer");
        PlayerPrefs.DeleteKey("ARPG_Tip_dungeon");

        // 清除本地缓存和运行时数据
        LocalSettingsManager.ClearCache();
        if (RuntimePlayerData.Instance != null)
            RuntimePlayerData.Instance.RefreshFromCache(-1);

        PlayerPrefs.Save();
    }
}
