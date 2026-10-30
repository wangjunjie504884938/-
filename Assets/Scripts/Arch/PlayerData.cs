using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Arch
{
    /// <summary>
    /// 玩家持久化数据 V1 — 服务器权威账本结构 (阶段02定义, 后续阶段逐步接线)。
    /// 规则: 货币一律 long; 非负; 上限封顶; 版本化迁移; 静态配置不进此结构。
    /// </summary>
    public class PlayerData
    {
        public const int CurrentVersion = 1;

        [JsonProperty("version")] public int Version = CurrentVersion;
        [JsonProperty("playerId")] public long PlayerId;

        // ---- 货币 (long, 非负, 封顶) ----
        [JsonProperty("gold")] public long Gold;           // 游戏币 (基础软通货)
        [JsonProperty("lifeCoins")] public long LifeCoins; // 寿元币 (核心玩法代币)
        [JsonProperty("planeCoins")] public long PlaneCoins; // 位面币 (高级硬通货)
        [JsonProperty("fragments")] public long Fragments; // 装备碎片

        // ---- 成长 ----
        [JsonProperty("level")] public int Level = 1;
        [JsonProperty("xp")] public long Xp;
        [JsonProperty("skillPoints")] public int SkillPoints;
        [JsonProperty("highestStageCleared")] public int HighestStageCleared = -1;
        [JsonProperty("riftHighestCleared")] public int RiftHighestCleared = -1;
        [JsonProperty("bagExpandTimes")] public int BagExpandTimes;
        [JsonProperty("passiveNodeIds")] public List<string> PassiveNodeIds = new List<string>();

        // ---- 背包 (模板引用+实例数据, 纯数据可序列化) ----
        [JsonProperty("backpack")] public List<ItemEntry> Backpack = new List<ItemEntry>();

        [JsonProperty("updatedAt")] public DateTime UpdatedAtUtc = DateTime.UtcNow;

        /// <summary>版本迁移: 旧版本数据补默认值/映射后升至 CurrentVersion。高于当前版本 = 拒绝(客户端过旧)。</summary>
        public static PlayerData Migrate(PlayerData d)
        {
            if (d == null) return null;
            if (d.Version > CurrentVersion)
                throw new InvalidOperationException($"存档版本 {d.Version} 高于当前支持的 {CurrentVersion}, 拒绝加载");
            // v0→v1 迁移占位: 历史字段改名/拆分在此追加 (不修改静态配置)
            d.Version = CurrentVersion;
            Normalize(d);
            return d;
        }

        /// <summary>非负/上限/空集合规整 (迁移与反序列化后都要过一遍)</summary>
        public static void Normalize(PlayerData d)
        {
            if (d == null) return;
            d.Gold = CurrencyMath.Clamp(d.Gold, 0, CurrencyMath.MaxGold);
            d.LifeCoins = CurrencyMath.Clamp(d.LifeCoins, 0, CurrencyMath.MaxLifeCoins);
            d.PlaneCoins = CurrencyMath.Clamp(d.PlaneCoins, 0, CurrencyMath.MaxPlaneCoins);
            d.Fragments = CurrencyMath.Clamp(d.Fragments, 0, CurrencyMath.MaxFragments);
            if (d.Level < 1) d.Level = 1;
            if (d.Xp < 0) d.Xp = 0;
            if (d.SkillPoints < 0) d.SkillPoints = 0;
            if (d.HighestStageCleared < -1) d.HighestStageCleared = -1;
            if (d.RiftHighestCleared < -1) d.RiftHighestCleared = -1;
            if (d.BagExpandTimes < 0) d.BagExpandTimes = 0;
            if (d.Backpack == null) d.Backpack = new List<ItemEntry>();
            if (d.PassiveNodeIds == null) d.PassiveNodeIds = new List<string>();
            d.UpdatedAtUtc = DateTime.UtcNow;
        }
    }

    /// <summary>背包物品条目 — 纯数据 (模板Id + 实例差异), 不携带行为</summary>
    public class ItemEntry
    {
        public int TemplateId;
        public int SlotType;      // 0=武器 1=护甲 2=饰品
        public int Rarity;        // 0=普通 1=稀有 2=史诗 3=传说
        public string Name;
        public int UpgradeLevel;
        public string StatsJson;  // 随机词缀等实例数据 (JSON转义存储)
    }
}
