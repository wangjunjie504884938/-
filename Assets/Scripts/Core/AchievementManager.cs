using UnityEngine;
using System.Collections.Generic;

public enum AchievementType
{
    FirstKill,          // 初次击杀
    Kill100,            // 百人斩
    Kill500,            // 五百斩
    Kill1000,           // 千人斩
    FirstBoss,          // 首次击杀Boss
    ClearStage3,        // 通关第3关
    ClearStage5,        // 通关第5关
    ClearAllStages,     // 全关通关
    Collect3Relics,     // 收集3个遗物
    Collect6Relics,     // 收集6个遗物
    FullHpClear,        // 满血通关
    FirstAffixKill,     // 击杀词缀精英
    DeathlessRun,       // 无死亡通关一关
    Gold1000,           // 金币1000
    Gold5000,           // 金币5000
    Level10,            // 等级10
    Level25,            // 等级25
    MaxRelics,          // 遗物满槽
}

public class AchievementData
{
    public AchievementType Type { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public bool Unlocked { get; set; }

    public int RewardGold { get; private set; }

    private AchievementData() { }

    public static AchievementData Create(AchievementType type)
    {
        var data = new AchievementData { Type = type, Unlocked = false };
        switch (type)
        {
            case AchievementType.FirstKill: data.Name = "初次击杀"; data.Description = "击杀第一个敌人"; data.RewardGold = 50; break;
            case AchievementType.Kill100: data.Name = "百人斩"; data.Description = "累计击杀100个敌人"; data.RewardGold = 200; break;
            case AchievementType.Kill500: data.Name = "五百斩"; data.Description = "累计击杀500个敌人"; data.RewardGold = 500; break;
            case AchievementType.Kill1000: data.Name = "千人斩"; data.Description = "累计击杀1000个敌人"; data.RewardGold = 1000; break;
            case AchievementType.FirstBoss: data.Name = "屠龙者"; data.Description = "首次击杀Boss"; data.RewardGold = 300; break;
            case AchievementType.ClearStage3: data.Name = "探险家"; data.Description = "通关第3关"; data.RewardGold = 200; break;
            case AchievementType.ClearStage5: data.Name = "勇者"; data.Description = "通关第5关"; data.RewardGold = 400; break;
            case AchievementType.ClearAllStages: data.Name = "传说终结"; data.Description = "通关全部8关"; data.RewardGold = 2000; break;
            case AchievementType.Collect3Relics: data.Name = "收藏家"; data.Description = "同时拥有3个遗物"; data.RewardGold = 150; break;
            case AchievementType.Collect6Relics: data.Name = "遗物大师"; data.Description = "同时拥有6个遗物"; data.RewardGold = 300; break;
            case AchievementType.FullHpClear: data.Name = "毫发无伤"; data.Description = "满血通关一个关卡"; data.RewardGold = 250; break;
            case AchievementType.FirstAffixKill: data.Name = "词缀猎人"; data.Description = "击杀一个词缀精英"; data.RewardGold = 100; break;
            case AchievementType.DeathlessRun: data.Name = "不死之身"; data.Description = "无死亡通关一个关卡"; data.RewardGold = 200; break;
            case AchievementType.Gold1000: data.Name = "小康之家"; data.Description = "累计获得1000金币"; data.RewardGold = 100; break;
            case AchievementType.Gold5000: data.Name = "大富翁"; data.Description = "累计获得5000金币"; data.RewardGold = 300; break;
            case AchievementType.Level10: data.Name = "初出茅庐"; data.Description = "达到10级"; data.RewardGold = 200; break;
            case AchievementType.Level25: data.Name = "身经百战"; data.Description = "达到25级"; data.RewardGold = 500; break;
            case AchievementType.MaxRelics: data.Name = "遗物满载"; data.Description = "遗物槽全满"; data.RewardGold = 150; break;
        }
        return data;
    }

    private static readonly AchievementType[] AllTypes = new AchievementType[]
    {
        AchievementType.FirstKill, AchievementType.Kill100, AchievementType.Kill500, AchievementType.Kill1000,
        AchievementType.FirstBoss, AchievementType.ClearStage3, AchievementType.ClearStage5, AchievementType.ClearAllStages,
        AchievementType.Collect3Relics, AchievementType.Collect6Relics, AchievementType.FullHpClear,
        AchievementType.FirstAffixKill, AchievementType.DeathlessRun, AchievementType.Gold1000, AchievementType.Gold5000,
        AchievementType.Level10, AchievementType.Level25, AchievementType.MaxRelics
    };

    public static AchievementData[] CreateAll()
    {
        var result = new AchievementData[AllTypes.Length];
        for (int i = 0; i < AllTypes.Length; i++)
            result[i] = Create(AllTypes[i]);
        return result;
    }
}

public class AchievementManager : MonoBehaviour
{
    public static AchievementManager Instance { get; private set; }

    private AchievementData[] achievements;
    private HashSet<AchievementType> unlockedSet = new HashSet<AchievementType>();

    // Persistent stats for tracking
    public int TotalKills { get; private set; }
    public int TotalGoldEarned { get; private set; }
    public bool DiedThisRun { get; set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        achievements = AchievementData.CreateAll();
    }

    public void RecordKill(bool hadAffix)
    {
        TotalKills++;
        TryUnlock(AchievementType.FirstKill);
        if (TotalKills >= 100) TryUnlock(AchievementType.Kill100);
        if (TotalKills >= 500) TryUnlock(AchievementType.Kill500);
        if (TotalKills >= 1000) TryUnlock(AchievementType.Kill1000);
        if (hadAffix) TryUnlock(AchievementType.FirstAffixKill);
    }

    public void RecordBossKill() => TryUnlock(AchievementType.FirstBoss);

    public void RecordGoldEarned(int amount)
    {
        TotalGoldEarned += amount;
        if (TotalGoldEarned >= 1000) TryUnlock(AchievementType.Gold1000);
        if (TotalGoldEarned >= 5000) TryUnlock(AchievementType.Gold5000);
    }

    public void RecordLevel(int level)
    {
        if (level >= 10) TryUnlock(AchievementType.Level10);
        if (level >= 25) TryUnlock(AchievementType.Level25);
    }

    public void RecordStageClear(int stageIndex, bool fullHp)
    {
        if (stageIndex >= 2) TryUnlock(AchievementType.ClearStage3);
        if (stageIndex >= 4) TryUnlock(AchievementType.ClearStage5);
        if (stageIndex >= 7) TryUnlock(AchievementType.ClearAllStages);
        if (fullHp) TryUnlock(AchievementType.FullHpClear);
        if (!DiedThisRun) TryUnlock(AchievementType.DeathlessRun);
    }

    public void RecordRelicCount(int count)
    {
        if (count >= 3) TryUnlock(AchievementType.Collect3Relics);
        if (count >= 6) TryUnlock(AchievementType.Collect6Relics);
        if (count >= RelicManager.MaxRelics) TryUnlock(AchievementType.MaxRelics);
    }

    public void TryUnlock(AchievementType type)
    {
        if (unlockedSet.Contains(type)) return;
        unlockedSet.Add(type);

        for (int i = 0; i < achievements.Length; i++)
        {
            if (achievements[i].Type == type && !achievements[i].Unlocked)
            {
                achievements[i].Unlocked = true;
                ShowAchievementToast(achievements[i]);

                // 发放成就奖励金币
                if (achievements[i].RewardGold > 0)
                {
                    var player = GameManager.Instance?.Player;
                    if (player != null)
                    {
                        player.Stats.AddGold(achievements[i].RewardGold);
                                    GameLog.Log($"[成就] 奖励 {achievements[i].RewardGold} 金币");
                    }
                }

                // 立即保存 (防止解锁后未保存)
                if (GameManager.Instance?.Player != null)
                    GameManager.Instance.Player.Stats.Save();
                break;
            }
        }
    }

    private void ShowAchievementToast(AchievementData data)
    {
                    GameLog.Log($"[成就解锁] {data.Name} — {data.Description}");

        if (GameUI.Instance != null)
            GameUI.Instance.ShowAchievementToast(data.Name, data.Description);
    }

    public int UnlockedCount => unlockedSet.Count;
    public int TotalCount => achievements.Length;
    public AchievementData[] GetAllAchievements() => achievements;

    public void ResetRunState()
    {
        DiedThisRun = false;
    }

    public string Serialize()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(TotalKills).Append(',').Append(TotalGoldEarned).Append(',');
        foreach (var t in unlockedSet) sb.Append((int)t).Append(';');
        return sb.ToString();
    }

    public void Deserialize(string data)
    {
        if (string.IsNullOrEmpty(data)) return;
        string[] parts = data.Split(',');
        if (parts.Length >= 2)
        {
            int.TryParse(parts[0], out int kills); TotalKills = kills;
            int.TryParse(parts[1], out int gold); TotalGoldEarned = gold;
        }
        if (parts.Length >= 3)
        {
            string[] ids = parts[2].Split(';');
            foreach (var id in ids)
            {
                if (int.TryParse(id, out int val))
                {
                    unlockedSet.Add((AchievementType)val);
                    for (int i = 0; i < achievements.Length; i++)
                        if (achievements[i].Type == (AchievementType)val) achievements[i].Unlocked = true;
                }
            }
        }
    }
}
