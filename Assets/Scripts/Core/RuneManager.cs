using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 符文管理器 — 管理符文购买/升级/镶嵌/取下
/// 数据持久化在PlayerPrefs(JSON)
/// </summary>
public class RuneManager : MonoBehaviour
{
    public static RuneManager Instance { get; private set; }

    private const string SaveKey = "ARPG_RuneData";

    // 碎片统一使用DismantleSystem.Fragments（走PlayerProgressData有槽位前缀）
    private void SetFragments(int value) => DismantleSystem.Fragments = value;

    // 玩家拥有的符文数据
    [System.Serializable]
    public class PlayerRuneEntry
    {
        public int runeType;     // RuneType enum value
        public int level;       // 当前等级
        public bool owned;      // 是否已购买
    }

    [System.Serializable]
    private class RuneSaveData
    {
        public List<PlayerRuneEntry> runes = new List<PlayerRuneEntry>();
    }

    private Dictionary<RuneType, PlayerRuneEntry> _runeData = new Dictionary<RuneType, PlayerRuneEntry>();
    private RuneSaveData _saveData = new RuneSaveData();

    // 符文配置缓存
    private static readonly Dictionary<RuneType, RuneData> _configCache = new Dictionary<RuneType, RuneData>();

    // 购买价格
    private static readonly Dictionary<RuneType, (int gold, int fragments)> _prices = new Dictionary<RuneType, (int, int)>
    {
        { RuneType.Scatter,       (500, 20) },
        { RuneType.Pierce,         (500, 20) },
        { RuneType.Chain,          (800, 30) },
        { RuneType.Burn,           (800, 30) },
        { RuneType.AreaExpand,     (600, 25) },
        { RuneType.ElementConvert, (1000, 40) },
        { RuneType.Vampiric,      (1000, 40) },
        { RuneType.Slowing,        (600, 25) },
    };

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadAll();
    }

    // ====== 数据加载/保存 ======

    private void LoadAll()
    {
        string json = PlayerPrefs.GetString(SaveKey, "");
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                _saveData = JsonUtility.FromJson<RuneSaveData>(json) ?? new RuneSaveData();
                _runeData.Clear();
                foreach (var entry in _saveData.runes)
                    _runeData[(RuneType)entry.runeType] = entry;
            }
            catch { _saveData = new RuneSaveData(); }
        }

        // 确保所有符文类型都有条目
        foreach (RuneType type in System.Enum.GetValues(typeof(RuneType)))
        {
            if (!_runeData.ContainsKey(type))
            {
                var entry = new PlayerRuneEntry { runeType = (int)type, level = 0, owned = false };
                _runeData[type] = entry;
                _saveData.runes.Add(entry);
            }
        }
    }

    private void SaveAll()
    {
        _saveData.runes = _runeData.Values.ToList();
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(_saveData));
        PlayerPrefs.Save();
        // 标记存档数据已变更，触发增量保存
        GameManager.Instance?.Player?.Stats?.MarkDirty();
    }

    // ====== 购买 ======

    public bool PurchaseRune(RuneType type)
    {
        if (!IsOwned(type))
        {
            var (goldCost, fragCost) = GetPrice(type);
            int currentGold = GameManager.Instance?.Player?.Stats?.Gold ?? 0;
            int currentFrags = GetFragments();
            if (currentGold < goldCost || currentFrags < fragCost)
                return false;

            // 扣费
            if (GameManager.Instance?.Player != null)
                GameManager.Instance.Player.Stats.SpendGold(goldCost);
            SetFragments(currentFrags - fragCost);

            // 设置拥有
            var entry = _runeData[type];
            entry.owned = true;
            entry.level = 1;
            _runeData[type] = entry;
            SaveAll();
            return true;
        }
        return false;
    }

    // ====== 升级 ======

    public bool UpgradeRune(RuneType type)
    {
        if (!IsOwned(type)) return false;
        var entry = _runeData[type];
        if (entry.level >= 5) return false; // 最大5级

        int cost = GetUpgradeCost(type, entry.level);
        int currentFrags = GetFragments();
        if (currentFrags < cost) return false;

        SetFragments(currentFrags - cost);
        entry.level++;
        _runeData[type] = entry;
        SaveAll();
        return true;
    }

    // ====== 镶嵌/取下 ======

    public bool EquipRune(int skillIndex, int slotIndex, RuneType type)
    {
        if (!IsOwned(type)) return false;
        var player = GameManager.Instance?.Player;
        if (player == null || skillIndex < 0 || skillIndex >= player.Skills.Count) return false;

        var skill = player.Skills[skillIndex];
        if (slotIndex < 0 || slotIndex >= SkillData.MaxRuneSlots) return false;

        // 检查该符文是否已镶嵌
        if (skill.HasRune(type))
            return false;

        // 如果槽位已满，先取下最后一个
        if (skill.EquippedRunes.Count >= SkillData.MaxRuneSlots)
            skill.UnequipRune(skill.EquippedRunes[skill.EquippedRunes.Count - 1].Type);

        var rune = RuneData.Create(type, _runeData[type].level);
        return skill.EquipRune(rune);
    }

    public void UnequipRune(int skillIndex, int slotIndex)
    {
        var player = GameManager.Instance?.Player;
        if (player == null || skillIndex < 0 || skillIndex >= player.Skills.Count) return;
        var skill = player.Skills[skillIndex];
        if (slotIndex < 0 || slotIndex >= skill.EquippedRunes.Count) return;
        skill.UnequipRune(skill.EquippedRunes[slotIndex].Type);
    }

    // ====== 查询 ======

    public bool IsOwned(RuneType type) => _runeData.TryGetValue(type, out var e) && e.owned;
    public int GetLevel(RuneType type) => _runeData.TryGetValue(type, out var e) ? e.level : 0;
    public (int gold, int fragments) GetPrice(RuneType type) => _prices.TryGetValue(type, out var p) ? p : (500, 20);

    public int GetUpgradeCost(RuneType type, int currentLevel)
    {
        return 20 + currentLevel * 15; // 碎片消耗：1级→35, 2级→50, 3级→65, 4级→80
    }

    public int GetFragments() => DismantleSystem.Fragments;
    public void AddFragments(int amount)
    {
        DismantleSystem.Fragments += amount;
    }

    /// <summary>获取已拥有的符文列表</summary>
    public List<(RuneType type, int level)> GetOwnedRunes()
    {
        var result = new List<(RuneType, int)>();
        foreach (var kvp in _runeData)
        {
            if (kvp.Value.owned)
                result.Add((kvp.Key, kvp.Value.level));
        }
        return result;
    }
}
