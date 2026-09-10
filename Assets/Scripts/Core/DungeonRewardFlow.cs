using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 副本结算流程 — 从GameManager提取
/// 处理服务器同步、奖励发放、掉落生成、结算UI
/// </summary>
public class DungeonRewardFlow : MonoBehaviour
{
    public static DungeonRewardFlow Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private GameManager GM => GameManager.Instance;

    [System.Serializable]
    public class DungeonResultBody
    {
        public int dungeonId;
        public float timeUsed;
        public int hpRemaining;
        public int hpMax;
        public int enemiesKilled;
    }

    [System.Serializable]
    public class DungeonResultDrop
    {
        public int rarity;
        public int slotType;
        public int dungeonLevel;
    }

    [System.Serializable]
    public class DungeonResultResponse
    {
        public bool success;
        public int goldReward;
        public int baseGold;
        public int speedBonus;
        public int fullHpBonus;
        public int xpReward;
        public int newGold;
        public int newHighestStage;
        public List<DungeonResultDrop> drops;
    }

    /// <summary>完整副本完成流程: 服务器同步 → 清理战场 → 显示结算</summary>
    public IEnumerator CompleteDungeonFlow(float timeUsed, int hpRemaining, int hpMax, int enemiesKilled, int stageIndex)
    {
        yield return StartCoroutine(SubmitDungeonResult(stageIndex, timeUsed, hpRemaining, hpMax, enemiesKilled));

        yield return null;

        SceneRegistry.ClearAll();
        VFXPool.ClearAll();
        EnemyPool.ClearAll();

        if (DungeonVisuals.Instance != null)
        {
            var dvObj = DungeonVisuals.Instance.gameObject;
            DungeonVisuals.Instance = null;
            dvObj.SetActive(false);
            Destroy(dvObj);
        }
    }

    private IEnumerator SubmitDungeonResult(int dungeonId, float timeUsed, int hpRemaining, int hpMax, int enemiesKilled)
    {
        string token = CloudSaveManager.Instance?.Token ?? "";
        int slot = CloudSaveManager.Instance?.ActiveSlot ?? 0;
        string apiBase = (CloudSaveManager.Instance?.ServerUrl ?? "http://localhost:5132") + "/api";

        if (!string.IsNullOrEmpty(token))
        {
            var body = new DungeonResultBody
            {
                dungeonId = dungeonId,
                timeUsed = timeUsed,
                hpRemaining = hpRemaining,
                hpMax = hpMax,
                enemiesKilled = enemiesKilled
            };
            string json = JsonUtility.ToJson(body);

            using (var req = new UnityWebRequest($"{apiBase}/save/dungeon/complete?slot={slot}", "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {token}");
                req.timeout = 10;
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    var resp = JsonUtility.FromJson<DungeonResultResponse>(req.downloadHandler.text);
                    if (resp != null && resp.success)
                    {
                        if (RuntimePlayerData.Instance != null && resp.newHighestStage >= 0)
                        {
                            RuntimePlayerData.Instance.RefreshFromServer(resp.newHighestStage, null);
                            LocalSettingsManager.CacheStageProgress(resp.newHighestStage, null);
                        }
                        ApplyDungeonRewards(resp.goldReward, resp.xpReward, resp.newGold, resp.newHighestStage, resp.drops, dungeonId);
                        yield break;
                    }
                }
                GameLog.LogWarning("[Dungeon] 服务器结算失败, 使用本地结算");
            }
        }

        // 本地fallback结算
        int goldReward = 0;
        if (StageSelectUI.Stages != null && dungeonId >= 0 && dungeonId < StageSelectUI.Stages.Length)
            goldReward = StageSelectUI.Stages[dungeonId].goldReward;

        int fullHpBonus = (hpRemaining >= hpMax) ? Mathf.RoundToInt(goldReward * 0.5f) : 0;
        int totalGold = goldReward + fullHpBonus;
        int xpReward = 50 + dungeonId * 30 + enemiesKilled * 5;

        var localDrops = new List<DungeonResultDrop>();
        int dropCount = 3;
        for (int i = 0; i < dropCount; i++)
        {
            float roll = Random.Range(0f, 1f);
            int rarity = roll < 0.02f ? 3 : roll < 0.10f ? 2 : roll < 0.30f ? 1 : 0;
            localDrops.Add(new DungeonResultDrop { rarity = rarity, slotType = Random.Range(0, 3), dungeonLevel = dungeonId + 1 });
        }

        if (RuntimePlayerData.Instance != null && dungeonId > RuntimePlayerData.Instance.HighestStage)
        {
            RuntimePlayerData.Instance.RefreshFromServer(dungeonId, null);
            LocalSettingsManager.CacheStageProgress(dungeonId, null);
        }

        ApplyDungeonRewards(totalGold, xpReward, -1, -1, localDrops, dungeonId);
    }

    public void ApplyDungeonRewards(int goldReward, int xpReward, int serverGold, int newHighestStage, List<DungeonResultDrop> drops, int dungeonId)
    {
        var player = GM?.Player;
        if (player != null)
        {
            if (serverGold > 0)
            {
                int currentGold = player.Stats.Gold;
                if (serverGold > currentGold)
                    player.Stats.AddGold(serverGold - currentGold);
            }

            player.RunGoldEarned += goldReward;
            player.GainXp(xpReward);

            if (AchievementManager.Instance != null)
            {
                bool fullHp = player.Stats.Hp == player.Stats.TotalMaxHp;
                AchievementManager.Instance.RecordStageClear(dungeonId, fullHp);
            }

            if (drops != null)
            {
                foreach (var drop in drops)
                {
                    var item = EquipmentItem.Generate((EquipSlotType)drop.slotType, (ItemRarity)drop.rarity, drop.dungeonLevel);
                    player.RunLootItems.Add(item);
                    if (!player.Inventory.AddToBackpack(item))
                        LocalMailSystem.SendItemMail($"副本掉落: {item.Name}", $"背包已满，装备已发送至邮箱。\n{item.GetStatSummary()}", item);
                }
            }

            player.Stats.Save();

            if (CloudSaveManager.HasInstance && CloudSaveManager.Instance.IsLoggedIn)
                CloudSaveManager.Instance.StartCoroutine(CloudSaveManager.Instance.UploadSaveCo());
        }

        DailyTaskUI.ReportProgress(1, 1);
        SeasonPass.AddXP(xpReward);
        if (DailyChallenge.ActiveModifier.HasValue && !DailyChallenge.IsCompleted())
        {
            DailyChallenge.MarkCompleted();
            DailyChallenge.EndChallenge();
            if (player != null) player.Stats.AddGold(DailyChallenge.RewardGold);
        }
        else if (DailyChallenge.ActiveModifier.HasValue)
        {
            DailyChallenge.EndChallenge();
        }

        if (DialogueSystem.Instance != null)
            DialogueSystem.Instance.ShowStageEndDialogue(dungeonId);

        if (dungeonId >= 7)
            GameUI.Instance?.ShowVictoryScreen(goldReward);
        else
            GameUI.Instance?.ShowDungeonComplete(goldReward);
    }
}
