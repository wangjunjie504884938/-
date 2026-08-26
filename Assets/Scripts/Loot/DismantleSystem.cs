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

    /// <summary>Dismantle a single item from backpack. Returns fragments gained.</summary>
    public static int DismantleFromBackpack(EquipmentInventory inv, int index)
    {
        if (inv == null || index < 0 || index >= inv.Backpack.Count) return 0;
        var item = inv.Backpack[index];
        int yield = GetFragmentYield(item);
        inv.Backpack.RemoveAt(index);
        Fragments += yield;
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
        foreach (var idx in sorted)
        {
            if (idx < 0 || idx >= inv.Backpack.Count) continue;
            total += GetFragmentYield(inv.Backpack[idx]);
            inv.Backpack.RemoveAt(idx);
        }
        Fragments += total;
        inv.RecalculateStats();
        return total;
    }
}
