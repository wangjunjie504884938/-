using UnityEngine;

/// <summary>
/// Scene management helper — extracted from GameManager for separation of concerns.
/// Handles dungeon visuals creation and boss equipment drops.
/// </summary>
public static class SceneController
{
    /// <summary>Creates dungeon visuals for the current stage if none exist.</summary>
    public static void CreateDungeonVisuals(int stageIndex)
    {
        if (DungeonVisuals.Instance == null)
        {
            GameObject dvObj = new GameObject("DungeonVisuals");
            var dv = dvObj.AddComponent<DungeonVisuals>();
            dv.BuildMap(stageIndex);
        }
    }

    /// <summary>Drops boss equipment with DNF-style animation. Shared by dungeon complete and endless wave complete.</summary>
    public static void DropBossEquipment(int dungeonLevel, Vector3 dropPos)
    {
        var player = GameManager.Instance?.Player;
        if (player == null) return;

        int dropCount = 3;
        for (int i = 0; i < 3; i++)
        {
            if (Random.Range(0f, 1f) < 0.2f) dropCount++;
        }

        for (int i = 0; i < dropCount; i++)
        {
            ItemRarity bossRarity = ItemData.RollBossRarity(dungeonLevel);
            EquipSlotType bossSlot = Random.Range(0, 3) switch
            {
                0 => EquipSlotType.Weapon,
                1 => EquipSlotType.Armor,
                _ => EquipSlotType.Accessory
            };
            var bossItem = EquipmentItem.Generate(bossSlot, bossRarity, dungeonLevel);
            player.RunLootItems.Add(bossItem);

            Vector3 offset = new Vector3((i - dropCount / 2f) * 1.5f, 0, 0);
            BossDropAnimation.ShowDrop(dropPos + offset, bossItem);

            if (!player.Inventory.AddToBackpack(bossItem))
            {
                LocalMailSystem.SendItemMail(
                    $"Boss掉落: {bossItem.Name}",
                    $"背包已满，装备已发送至邮箱。\n{bossItem.GetStatSummary()}",
                    bossItem);
            }
        }
    }
}
