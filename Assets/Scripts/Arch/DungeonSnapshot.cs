using System;
using System.Collections.Generic;

namespace Arch
{
    /// <summary>
    /// 地牢实例快照 — 纯数据, 禁止序列化 GameObject/Transform/Unity运行时对象 (规划阶段06)。
    /// 分级快照策略 (用户已定):
    ///   P0 普通副本   快照间隔5s  崩溃回退≤5s   Redis快照+关键事件记录
    ///   P1 精英/Boss  快照间隔3s  崩溃回退≤3s   Redis快照+关键战斗事件
    ///   P2 高难/长流程 快照间隔1s  崩溃回退≤1s   高频快照+事件日志
    ///   P3 货币/掉落/奖励结算       不依赖周期快照  操作ID幂等+持久化流水+恢复对账
    /// </summary>
    public class DungeonSnapshot
    {
        public string InstanceId;          // 唯一实例ID (guid)
        public int SnapshotTier;           // 0=P0 1=P1 2=P2 (P3不落快照)
        public int ConfigVersion;          // 生成时的静态配置版本 (断线重连校验)
        public int TemplateId;             // 地图主题 (0-10)
        public int RiftLayer;              // 秘境层数 (1-based)
        public int RandomSeed;             // 地图/障碍随机种子 (重建一致性)
        public float SurvivalTime;         // 已生存时间 (寿元结算用)
        public List<MemberState> Members = new List<MemberState>();
        public List<MonsterEntry> Monsters = new List<MonsterEntry>();
        public List<string> RewardFlags = new List<string>(); // 已发奖励标记 (幂等: 不重复发奖)
        public int Wave;                   // 当前波次
        public DateTime SavedAtUtc = DateTime.UtcNow;

        public const int RetentionSeconds = 300; // 断线保留5分钟
    }

    /// <summary>成员状态 — 断线玩家按规则保留; 队友继续战斗时持续更新 (用户决策)</summary>
    public class MemberState
    {
        public long PlayerId;
        public float X, Y;
        public long Hp;
        public bool Downed;                // 断线后肉体倒地 (可救起 — 用户决策)
    }

    /// <summary>怪物状态 — 只存差异 (SpawnId定位 + 血量/阶段/仇恨目标)</summary>
    public class MonsterEntry
    {
        public int SpawnId;
        public long Hp;
        public int Phase;
        public long AggroPlayerId;
    }
}
