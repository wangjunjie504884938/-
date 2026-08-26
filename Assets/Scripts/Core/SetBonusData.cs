using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Equipment set bonus system. Items with the same SetId grant bonus stats when 2+ pieces equipped.
/// </summary>
public static class SetBonusData
{
    // Set IDs: 0 = no set, 1-6 = defined sets
    public struct SetDef
    {
        public int Id;
        public string Name;
        public int AttackBonus2; public int DefenseBonus2; public int HpBonus2; public float SpeedBonus2;
        public int AttackBonus3; public int DefenseBonus3; public int HpBonus3;
        public float SpeedBonus3; public float CritBonus3;
    }

    public static readonly SetDef[] Sets = new SetDef[]
    {
        new SetDef { Id = 1, Name = "战士之魂", AttackBonus2 = 5, DefenseBonus2 = 3, HpBonus3 = 30, AttackBonus3 = 10, DefenseBonus3 = 5, SpeedBonus3 = 0.5f, CritBonus3 = 0.05f },
        new SetDef { Id = 2, Name = "法师之辉", AttackBonus2 = 4, HpBonus2 = 20, AttackBonus3 = 8, HpBonus3 = 40, DefenseBonus3 = 3, CritBonus3 = 0.08f },
        new SetDef { Id = 3, Name = "牧师之佑", DefenseBonus2 = 4, HpBonus2 = 30, DefenseBonus3 = 8, HpBonus3 = 50, AttackBonus3 = 5, SpeedBonus3 = 0.3f },
        new SetDef { Id = 4, Name = "龙裔", AttackBonus2 = 6, DefenseBonus2 = 4, AttackBonus3 = 12, DefenseBonus3 = 6, HpBonus3 = 40, CritBonus3 = 0.06f },
        new SetDef { Id = 5, Name = "暗影", AttackBonus2 = 5, SpeedBonus2 = 0.5f, AttackBonus3 = 8, SpeedBonus3 = 0.8f, CritBonus3 = 0.1f, HpBonus3 = 20 },
        new SetDef { Id = 6, Name = "神谕", HpBonus2 = 40, DefenseBonus2 = 5, HpBonus3 = 80, DefenseBonus3 = 10, AttackBonus3 = 6, CritBonus3 = 0.05f },
    };

    private static readonly Dictionary<int, int> _counts = new Dictionary<int, int>();

    public static SetDef? GetSet(int setId)
    {
        if (setId <= 0) return null;
        foreach (var s in Sets) if (s.Id == setId) return s;
        return null;
    }

    public static string GetSetName(int setId) => GetSet(setId)?.Name ?? "";

    /// <summary>Returns active set bonuses as a display string.</summary>
    public static string GetActiveBonusDisplay(List<EquipmentSlot> equipped)
    {
        _counts.Clear();
        foreach (var slot in equipped)
        {
            if (slot.Item != null && slot.Item.SetId > 0)
            {
                _counts.TryGetValue(slot.Item.SetId, out int c);
                _counts[slot.Item.SetId] = c + 1;
            }
        }

        var sb = new System.Text.StringBuilder();
        foreach (var kv in _counts)
        {
            var set = GetSet(kv.Key);
            if (!set.HasValue) continue;
            int pieces = kv.Value;
            if (pieces >= 2)
            {
                sb.AppendLine($"<color=#FFD700>{set.Value.Name} ({pieces}/3)</color>");
                var parts2 = new List<string>();
                if (set.Value.AttackBonus2 > 0) parts2.Add($"攻+{set.Value.AttackBonus2}");
                if (set.Value.DefenseBonus2 > 0) parts2.Add($"防+{set.Value.DefenseBonus2}");
                if (set.Value.HpBonus2 > 0) parts2.Add($"生命+{set.Value.HpBonus2}");
                if (set.Value.SpeedBonus2 > 0) parts2.Add($"移速+{set.Value.SpeedBonus2}");
                if (parts2.Count > 0)
                    sb.AppendLine($"  <color=#88FF88>2件: {string.Join(" ", parts2)}</color>");
                if (pieces >= 3)
                {
                    var parts3 = new List<string>();
                    if (set.Value.AttackBonus3 > 0) parts3.Add($"攻+{set.Value.AttackBonus3}");
                    if (set.Value.DefenseBonus3 > 0) parts3.Add($"防+{set.Value.DefenseBonus3}");
                    if (set.Value.HpBonus3 > 0) parts3.Add($"生命+{set.Value.HpBonus3}");
                    if (set.Value.SpeedBonus3 > 0) parts3.Add($"移速+{set.Value.SpeedBonus3}");
                    if (set.Value.CritBonus3 > 0) parts3.Add($"暴击+{set.Value.CritBonus3 * 100:F0}%");
                    if (parts3.Count > 0)
                        sb.AppendLine($"  <color=#FF88FF>3件: {string.Join(" ", parts3)}</color>");
                }
            }
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>Applies set bonuses to runtime stats. Call after equipment bonuses are calculated.</summary>
    public static void ApplySetBonuses(List<EquipmentSlot> equipped, PlayerRuntimeStats stats)
    {
        _counts.Clear();
        foreach (var slot in equipped)
        {
            if (slot.Item != null && slot.Item.SetId > 0)
            {
                _counts.TryGetValue(slot.Item.SetId, out int c);
                _counts[slot.Item.SetId] = c + 1;
            }
        }

        foreach (var kv in _counts)
        {
            var set = GetSet(kv.Key);
            if (!set.HasValue) continue;
            int pieces = kv.Value;
            if (pieces >= 2)
            {
                stats.BonusAttack += set.Value.AttackBonus2;
                stats.BonusDefense += set.Value.DefenseBonus2;
                stats.BonusMaxHp += set.Value.HpBonus2;
                stats.BonusMoveSpeed += set.Value.SpeedBonus2;
            }
            if (pieces >= 3)
            {
                stats.BonusAttack += set.Value.AttackBonus3;
                stats.BonusDefense += set.Value.DefenseBonus3;
                stats.BonusMaxHp += set.Value.HpBonus3;
                stats.BonusMoveSpeed += set.Value.SpeedBonus3;
                stats.BonusCritChance += set.Value.CritBonus3;
            }
        }
    }
}
