using System;
using System.Collections.Generic;

namespace Arch
{
    /// <summary>
    /// 运行时状态 (内存) — 与持久化 PlayerData 分离; 进快照时投影为 MemberState (规划阶段02: 模板与运行时状态分离)。
    /// </summary>
    public class PlayerRuntimeState
    {
        public long PlayerId;
        public float X, Y;
        public long Hp;
        public bool InCombat;
        public bool Downed;
        public int Potions;
        public List<string> ActiveBuffs = new List<string>();
        public float SurvivalTime;      // 本局生存时间 (寿元结算)
    }

    /// <summary>
    /// 静态配置模板根 — 只读数据 (加载后不得修改, 校验通过才可用)。
    /// 首批: 日常任务模板 (与 DailyTaskUI 现行模板对齐); 后续迁入: 商品/怪物/掉落/强化费用。
    /// </summary>
    public class StaticConfig
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        public List<TaskTemplate> DailyTasks = new List<TaskTemplate>();
        public List<ItemTemplate> Items = new List<ItemTemplate>();
    }

    public class TaskTemplate
    {
        public int TaskType;    // 0=击杀敌人 1=通关副本 2=消耗金币 3=强化装备 4=获得经验
        public int TargetValue;
        public int RewardGold;
    }

    public class ItemTemplate
    {
        public int TemplateId;
        public int SlotType;
        public int Rarity;
        public string Name;
        public string StatsJson;
    }

    /// <summary>静态配置校验 (规划阶段02验收: ID唯一性/字段完整性/引用关系; 重复ID或缺失明确报错)</summary>
    public static class ConfigValidator
    {
        public static List<string> Validate(StaticConfig cfg)
        {
            var errors = new List<string>();
            if (cfg == null) { errors.Add("配置为null"); return errors; }
            if (cfg.Version <= 0) errors.Add("配置版本号非法: " + cfg.Version);

            var seenTaskTypes = new HashSet<int>();
            foreach (var t in cfg.DailyTasks)
            {
                if (t.TaskType < 0 || t.TaskType > 4) errors.Add($"日常任务类型越界: {t.TaskType}");
                if (!seenTaskTypes.Add(t.TaskType)) errors.Add($"日常任务类型重复: {t.TaskType}");
                if (t.TargetValue <= 0) errors.Add($"任务 {t.TaskType} 目标值非法: {t.TargetValue}");
                if (t.RewardGold < 0) errors.Add($"任务 {t.TaskType} 奖励为负: {t.RewardGold}");
            }

            var seenItems = new HashSet<int>();
            foreach (var it in cfg.Items)
            {
                if (!seenItems.Add(it.TemplateId)) errors.Add($"物品模板ID重复: {it.TemplateId}");
                if (string.IsNullOrEmpty(it.Name)) errors.Add($"物品 {it.TemplateId} 缺少名称");
                if (it.SlotType < 0 || it.SlotType > 2) errors.Add($"物品 {it.TemplateId} 槽位越界: {it.SlotType}");
                if (it.Rarity < 0 || it.Rarity > 3) errors.Add($"物品 {it.TemplateId} 品质越界: {it.Rarity}");
            }
            return errors;
        }
    }
}
