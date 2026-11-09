using UnityEngine;

/// <summary>
/// Equipment dismantle system — converts unwanted gear into upgrade fragments.
/// Fragments are stored in PlayerProgressData and used for future crafting/upgrading.
/// </summary>
public static class DismantleSystem
{
    /// <summary>Fragment yield per rarity tier.</summary>
    public static readonly int[] FragmentYield = { 1, 3, 8, 20 }; // Common, Rare, Epic, Legendary

    /// <summary>Current fragment count (persisted in PlayerProgressData).</summary>
    public static int Fragments
    {
        get => GameManager.Instance?.Player?.Stats?.Progress?.DismantleFragments ?? 0;
        set
        {
            var p = GameManager.Instance?.Player?.Stats?.Progress;
            if (p != null) p.DismantleFragments = value;
        }
    }

    /// <summary>Returns fragment yield for an item based on rarity and upgrade level.</summary>
    public static int GetFragmentYield(EquipmentItem item)
    {
        if (item == null) return 0;
        int baseYield = FragmentYield[(int)item.Rarity];
        // +1 fragment per upgrade level
        return baseYield + item.UpgradeLevel;
    }

    /// <summary>寿元币产出: 分解史诗+1 / 传说+3 (经济规范: 分解特定装备产出寿元)</summary>
    public static int GetLifeYield(EquipmentItem item)
    {
        if (item == null) return 0;
        return (int)item.Rarity switch { 3 => 3, 2 => 1, _ => 0 };
    }

    /// <summary>Dismantle a single item from backpack. Returns fragments gained.</summary>
    public static int DismantleFromBackpack(EquipmentInventory inv, int index)
    {
        if (inv == null || index < 0 || index >= inv.Backpack.Count) return 0;
        var item = inv.Backpack[index];
        int yield = GetFragmentYield(item);
        int life = GetLifeYield(item);
        inv.Backpack.RemoveAt(index);
        Fragments += yield;
        if (life > 0) CurrencyWallet.AddLifeOrPlane("Life", life);
        inv.RecalculateStats();
        return yield;
    }

    /// <summary>Dismantle multiple items from backpack. Returns total fragments gained.</summary>
    public static int DismantleBatch(EquipmentInventory inv, System.Collections.Generic.List<int> indices)
    {
        if (inv == null || indices == null || indices.Count == 0) return 0;
        // Deduplicate + sort descending so removal doesn't shift indices
        var unique = new System.Collections.Generic.HashSet<int>(indices);
        var sorted = new System.Collections.Generic.List<int>(unique);
        sorted.Sort((a, b) => b.CompareTo(a));
        int total = 0;
        int totalLife = 0;
        foreach (var idx in sorted)
        {
            if (idx < 0 || idx >= inv.Backpack.Count) continue;
            total += GetFragmentYield(inv.Backpack[idx]);
            totalLife += GetLifeYield(inv.Backpack[idx]);
            inv.Backpack.RemoveAt(idx);
        }
        Fragments += total;
        if (totalLife > 0) CurrencyWallet.AddLifeOrPlane("Life", totalLife);
        inv.RecalculateStats();
        return total;
    }
}
