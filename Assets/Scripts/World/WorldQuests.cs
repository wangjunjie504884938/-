using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 世界讨伐任务 — 每个野外区(1-15)一名任务发布人，发布可重复的击杀任务
/// 状态机: 未接取 → 进行中 → 可交付 → 交付回未接取(可重复)
/// 持久化: ARPG_World_Quests (JSON，全局不分存档槽)
/// </summary>
public static class WorldQuests
{
    public const int StateNone = 0;
    public const int StateInProgress = 1;
    public const int StateTurnIn = 2;

    [Serializable]
    public class QuestState
    {
        public int zone;
        public int state;
        public int kills;
        public int doneCount;
    }

    [Serializable]
    private class SaveBlob { public List<QuestState> list = new List<QuestState>(); }

    private const string Key = "ARPG_World_Quests";
    private static readonly Dictionary<int, QuestState> _quests = new Dictionary<int, QuestState>();
    private static bool _loaded;

    public static bool HasNpc(int zoneIndex) => zoneIndex > 0 && zoneIndex < WorldData.Zones.Length;

    /// <summary>发布人位置：传送点西南角(不挡光柱，玩家先见NPC后见光柱)</summary>
    public static Vector2 NpcPos(int zoneIndex) => WorldData.Get(zoneIndex).WaypointPos + new Vector2(-6f, -6f);

    public static int Target(int zoneIndex) => 3 + WorldData.Get(zoneIndex).Tier;
    public static int GoldReward(int zoneIndex) => (30 + WorldData.Get(zoneIndex).Tier * 20) * WorldData.WorldTier;
    public static int XpReward(int zoneIndex) => (20 + WorldData.Get(zoneIndex).Tier * 12) * WorldData.WorldTier;

    public static QuestState Get(int zoneIndex)
    {
        Load();
        return _quests.TryGetValue(zoneIndex, out var q) ? q : null;
    }

    public static void Accept(int zoneIndex)
    {
        Load();
        var q = GetOrCreate(zoneIndex);
        if (q.state != StateNone) return;
        q.state = StateInProgress;
        q.kills = 0;
        Save();
    }

    /// <summary>区域击杀计入进行中的讨伐任务 (WorldManager刷怪列表回收时调用)</summary>
    public static void OnZoneKills(int zoneIndex, int kills)
    {
        Load();
        if (!_quests.TryGetValue(zoneIndex, out var q) || q.state != StateInProgress) return;
        q.kills = Mathf.Min(q.kills + kills, Target(zoneIndex));
        if (q.kills >= Target(zoneIndex))
        {
            q.state = StateTurnIn;
            GameUI.Instance?.ShowItemPickupToast("任务目标达成", $"回 {WorldData.Get(zoneIndex).Name} 发布人处交付");
        }
        Save();
    }

    public static void TurnIn(int zoneIndex)
    {
        Load();
        if (!_quests.TryGetValue(zoneIndex, out var q) || q.state != StateTurnIn) return;
        var player = GameManager.Instance?.Player;
        if (player == null) return;

        int gold = GoldReward(zoneIndex);
        int xp = XpReward(zoneIndex);
        player.Stats.AddGold(gold);
        player.GainXp(xp);
        q.doneCount++;
        q.state = StateNone;
        q.kills = 0;
        Save();
        GameUI.Instance?.ShowItemPickupToast("任务完成", $"{WorldData.Get(zoneIndex).Name}讨伐 — 金币×{gold} 经验×{xp}");
    }

    private static QuestState GetOrCreate(int zoneIndex)
    {
        if (_quests.TryGetValue(zoneIndex, out var q)) return q;
        q = new QuestState { zone = zoneIndex, state = StateNone };
        _quests[zoneIndex] = q;
        return q;
    }

    private static void Load()
    {
        if (_loaded) return;
        _loaded = true;
        string json = PlayerPrefs.GetString(Key, "");
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            var blob = JsonUtility.FromJson<SaveBlob>(json);
            if (blob?.list == null) return;
            foreach (var q in blob.list)
                if (q != null && HasNpc(q.zone))
                    _quests[q.zone] = q;
        }
        catch (Exception e)
        {
            Debug.LogError($"[WorldQuests] 存档解析失败: {e.Message}");
        }
    }

    private static void Save()
    {
        var blob = new SaveBlob { list = new List<QuestState>(_quests.Values) };
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(blob));
        PlayerPrefs.Save();
    }
}
