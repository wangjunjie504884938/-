using UnityEngine;
using System.Collections.Generic;

public class RelicPickup : MonoBehaviour
{
    private void OnEnable() => SceneRegistry.Register(this);
    private void OnDestroy() => SceneRegistry.Unregister(this);

    private bool pickedUp;
    private List<RelicData> relicChoices;
    private int stageIndex;
    private float lifetime = 60f; // Auto-destroy after 60s if not picked up

    public void SetRelicChoices(List<RelicData> choices, int stage)
    {
        relicChoices = choices;
        stageIndex = stage;
    }

    private void Update()
    {
        if (pickedUp) return;

        // Countdown lifetime — auto-destroy if not picked up
        lifetime -= Time.deltaTime;
        if (lifetime <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        // Auto-pickup: when player is close enough, trigger pickup
        var player = GameManager.Instance?.Player;
        if (player != null && !player.IsDead)
        {
            float dist = Vector2.Distance(transform.position, player.transform.position);
            if (dist <= 1.5f)
                TryPickup(player);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (pickedUp) return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null || player.IsDead) return;

        TryPickup(player);
    }

    private void TryPickup(PlayerController player)
    {
        if (pickedUp) return;
        pickedUp = true;
        AudioManager.Instance?.PlayPickup();

        // Filter to relics the player doesn't already have
        var available = new List<RelicData>();
        if (relicChoices != null)
        {
            foreach (var choice in relicChoices)
            {
                if (RelicManager.Instance == null || !RelicManager.Instance.HasRelic(choice.Type))
                    available.Add(choice);
            }
        }

        if (available.Count == 0)
        {
            // All relics already owned — give gold compensation
            player.Stats.AddGold(100);
            GameUI.Instance?.ShowItemPickupToast("金币+100", "(遗物已满)");
            Destroy(gameObject);
            return;
        }

        // Use RelicChoiceUI for 3-choice selection
        if (RelicChoiceUI.Instance == null)
        {
            var go = new GameObject("RelicChoiceUI");
            go.AddComponent<RelicChoiceUI>();
        }

        RelicChoiceUI.Instance.Show(available, (selected) =>
        {
            if (selected != null && RelicManager.Instance != null)
            {
                if (RelicManager.Instance.AddRelic(selected))
                {
                    player.RunLootRelics.Add(selected);
                    VFXHelper.SpawnLevelUpEffect(player.transform.position);
                    GameUI.Instance?.ShowItemPickupToast(selected.Name, selected.Description);
                }
            }
            Destroy(gameObject);
        });

        // Safety: if RelicChoiceUI doesn't fire callback within 10s, destroy anyway
        StartCoroutine(SafetyTimeout());
    }

    private System.Collections.IEnumerator SafetyTimeout()
    {
        yield return new WaitForSeconds(10f);
        if (gameObject != null)
        {
            // Force resume game and destroy
            Time.timeScale = 1f;
            Destroy(gameObject);
        }
    }
}
