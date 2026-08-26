using UnityEngine;

/// <summary>
/// 地面掉落物 — Boss掉落已改为直接入背包, 此类仅保留兼容性。
/// </summary>
public class LootPickup : MonoBehaviour
{
    private void OnEnable() => SceneRegistry.Register(this);
    private void OnDestroy() => SceneRegistry.Unregister(this);

    public ItemType Type { get; private set; }
    public ItemRarity Rarity { get; private set; }
    public string ItemName { get; private set; }
    public int GoldValue { get; private set; }

    private bool pickedUp;
    private Vector3 basePosition;
    private bool basePositionSet;

    public void SetRuntimeItem(ItemType type, ItemRarity rarity, int dungeonLevel)
    {
        Type = type;
        Rarity = rarity;

        float levelScale = 1f + (dungeonLevel - 1) * 0.2f;
        GoldValue = Mathf.RoundToInt(rarity switch
        {
            ItemRarity.Legendary => 60f * levelScale,
            ItemRarity.Epic => 30f * levelScale,
            ItemRarity.Rare => 15f * levelScale,
            _ => 5f * levelScale
        });

        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = ItemData.GetRarityColor(rarity);
    }

    private void Update()
    {
        if (pickedUp) return;

        if (!basePositionSet)
        {
            basePosition = transform.position;
            basePositionSet = true;
        }

        float bob = Mathf.Sin(Time.unscaledTime * 3f + GetInstanceID()) * 0.05f;
        transform.position = basePosition + Vector3.up * bob;

        var player = GameManager.Instance?.Player;
        if (player != null)
        {
            float dist = Vector2.Distance(transform.position, player.transform.position);
            float pickupRange = GameSettings.AutoLoot ? 3.5f : 2.0f;
            if (dist <= pickupRange)
                TryPickup(player);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (pickedUp) return;
        if (other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null)
                TryPickup(player);
        }
    }

    private void TryPickup(PlayerController player)
    {
        if (pickedUp) return;
        pickedUp = true;
        AudioManager.Instance?.PlayPickup();

        int dungeonLevel = GameManager.Instance != null ? GameManager.Instance.DungeonLevel : 1;
        Color rarityColor = ItemData.GetRarityColor(Rarity);
        string rarityHex = ColorUtility.ToHtmlStringRGBA(rarityColor);

        player.Stats.AddGold(GoldValue);
        player.RunGoldEarned += GoldValue;
        VFXHelper.SpawnDamageNumber(player.transform.position, GoldValue, false, new Color(1f, 0.85f, 0.2f));

        switch (Type)
        {
            case ItemType.Weapon:
            case ItemType.Armor:
            case ItemType.Accessory:
                var slotType = Type switch
                {
                    ItemType.Weapon => EquipSlotType.Weapon,
                    ItemType.Armor => EquipSlotType.Armor,
                    _ => EquipSlotType.Accessory
                };
                var item = EquipmentItem.Generate(slotType, Rarity, dungeonLevel);
                player.RunLootItems.Add(item);
                if (!player.Inventory.AddToBackpack(item))
                {
                    LocalMailSystem.SendItemMail(
                        $"副本掉落: {item.Name}",
                        $"背包已满，装备已发送至邮箱。\n{item.GetStatSummary()}",
                        item);
                    // 背包满才用toast提示（重要信息）
                    GameUI.Instance?.ShowItemPickupToast("背包已满", $"{item.Name} 已发送至邮箱");
                }
                else
                {
                    // 战斗中拾取装备只显示简短伤害数字，不用大toast框遮挡屏幕
                    VFXHelper.SpawnDamageNumber(player.transform.position + Vector3.up, 0, false, new Color(1f, 0.85f, 0.2f));
                }
                break;
            case ItemType.Potion:
                int healAmt = Mathf.RoundToInt(player.Stats.TotalMaxHp * 0.25f);
                player.Heal(healAmt);
                GameUI.Instance?.ShowItemPickupToast("生命药水", $"恢复 {healAmt} 生命值");
                break;
        }

        Destroy(gameObject);
    }
}
