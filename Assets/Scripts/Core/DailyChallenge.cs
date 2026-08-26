using UnityEngine;

/// <summary>
/// 每日挑战 — 每天生成一个全局词缀修饰，完成获得金币奖励
/// 修饰符基于日期种子确定，所有玩家当天获得相同修饰符
/// </summary>
public static class DailyChallenge
{
    public enum ModifierType
    {
        DoubleDamage,     // 双倍伤害（玩家和敌人）
        NoDash,           // 禁用冲刺
        SpeedDemon,       // 极速模式（所有人移速+50%）
        GlassCannon,      // 玻璃大炮（攻击+100%，防御-80%）
        Vampire,          // 吸血模式（全员吸血20%）
        IceWorld,         // 冰冻世界（敌人移速-40%）
        Horde,            // 兽潮（敌人数量+50%）
        MirrorDamage,     // 镜像伤害（受到伤害反弹50%给攻击者）
    }

    public struct ModifierInfo
    {
        public ModifierType Type;
        public string Name;
        public string Desc;
        public Color ThemeColor;
    }

    private static readonly ModifierInfo[] Modifiers =
    {
        new ModifierInfo { Type = ModifierType.DoubleDamage, Name = "狂暴之战", Desc = "所有伤害翻倍，战斗更加凶险!", ThemeColor = new Color(0.9f, 0.2f, 0.1f) },
        new ModifierInfo { Type = ModifierType.NoDash, Name = "禁冲刺挑战", Desc = "无法使用冲刺闪避，靠走位生存!", ThemeColor = new Color(0.6f, 0.3f, 0.1f) },
        new ModifierInfo { Type = ModifierType.SpeedDemon, Name = "极速模式", Desc = "所有人移速+50%，节奏飞快!", ThemeColor = new Color(0.2f, 0.8f, 0.9f) },
        new ModifierInfo { Type = ModifierType.GlassCannon, Name = "玻璃大炮", Desc = "攻击+100%，防御-80%!", ThemeColor = new Color(0.8f, 0.4f, 0.9f) },
        new ModifierInfo { Type = ModifierType.Vampire, Name = "吸血之夜", Desc = "全员获得20%吸血效果!", ThemeColor = new Color(0.7f, 0.1f, 0.3f) },
        new ModifierInfo { Type = ModifierType.IceWorld, Name = "冰冻世界", Desc = "敌人移速-40%，行动迟缓!", ThemeColor = new Color(0.3f, 0.7f, 1f) },
        new ModifierInfo { Type = ModifierType.Horde, Name = "兽潮来袭", Desc = "敌人数量+50%!", ThemeColor = new Color(0.6f, 0.5f, 0.2f) },
        new ModifierInfo { Type = ModifierType.MirrorDamage, Name = "镜像反伤", Desc = "受到伤害时反弹50%给攻击者!", ThemeColor = new Color(0.5f, 0.8f, 0.5f) },
    };

    /// <summary>获取今天的修饰符（基于日期种子）</summary>
    public static ModifierInfo GetTodayModifier()
    {
        var today = System.DateTime.Now;
        int seed = today.Year * 10000 + today.Month * 100 + today.Day;
        var rng = new System.Random(seed);
        int index = rng.Next(Modifiers.Length);
        return Modifiers[index];
    }

    /// <summary>今日挑战是否已完成</summary>
    public static bool IsCompleted()
    {
        string key = $"ARPG_DailyChallenge_{System.DateTime.Now:yyyyMMdd}";
        return PlayerPrefs.GetInt(key, 0) == 1;
    }

    /// <summary>标记今日挑战完成</summary>
    public static void MarkCompleted()
    {
        string key = $"ARPG_DailyChallenge_{System.DateTime.Now:yyyyMMdd}";
        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();
    }

    /// <summary>每日挑战奖励</summary>
    public const int RewardGold = 500;

    /// <summary>获取当前激活的修饰符（地牢中实时读取）</summary>
    public static ModifierType? ActiveModifier { get; set; }

    /// <summary>开始每日挑战</summary>
    public static void StartChallenge()
    {
        ActiveModifier = GetTodayModifier().Type;
    }

    /// <summary>结束挑战（清除修饰符）</summary>
    public static void EndChallenge()
    {
        ActiveModifier = null;
    }
}
