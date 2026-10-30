using System.Collections.Generic;
using System.Linq;

/// <summary>
/// GameConfigAll 启动校验 (规划阶段02: ID唯一性/字段完整性/引用关系, 明确报错)。
/// 校验失败不阻断离线可玩, 但逐条 GameLog.LogError 明确报告。
/// </summary>
public static class GameConfigValidator
{
    public static List<string> Validate(GameConfigAll cfg)
    {
        var errors = new List<string>();
        if (cfg == null) { errors.Add("配置为null"); return errors; }

        // ---- 职业: classId 唯一 + 基础字段 ----
        var classIds = new HashSet<int>();
        foreach (var c in cfg.classes ?? new List<GameConfigClassDTO>())
        {
            if (!classIds.Add(c.classId)) errors.Add($"职业 classId 重复: {c.classId}");
            if (string.IsNullOrEmpty(c.className)) errors.Add($"职业 {c.classId} 缺少名称");
            if (c.baseHp <= 0) errors.Add($"职业 {c.classId} baseHp 非法: {c.baseHp}");
            if (c.baseAttack < 0) errors.Add($"职业 {c.classId} baseAttack 为负");
        }

        // ---- 技能: (classId, slot) 唯一 + 引用职业必须存在 ----
        var seenSkill = new HashSet<string>();
        foreach (var s in cfg.skills ?? new List<GameConfigSkillDTO>())
        {
            var key = s.classId + ":" + s.skillSlot;
            if (!seenSkill.Add(key)) errors.Add($"技能槽位重复: {key}");
            if (s.cooldown < 0) errors.Add($"技能 {key} 冷却为负");
            if (classIds.Count > 0 && !classIds.Contains(s.classId))
                errors.Add($"技能 {key} 引用了不存在的职业 classId={s.classId}");
        }

        // ---- 怪物: enemyType 唯一 + baseHp ----
        var seenEnemy = new HashSet<int>();
        foreach (var e in cfg.enemies ?? new List<GameConfigEnemyDTO>())
        {
            if (!seenEnemy.Add(e.enemyType)) errors.Add($"怪物 enemyType 重复: {e.enemyType}");
            if (e.baseHp <= 0) errors.Add($"怪物 {e.enemyType} baseHp 非法: {e.baseHp}");
        }

        // ---- 词缀/符文/圣物: 类型唯一 + 名称 ----
        var seenAffix = new HashSet<int>();
        foreach (var a in cfg.affixes ?? new List<GameConfigAffixDTO>())
            if (!seenAffix.Add(a.affixType)) errors.Add($"词缀 affixType 重复: {a.affixType}");

        var seenRune = new HashSet<int>();
        foreach (var r in cfg.runes ?? new List<GameConfigRuneDTO>())
        {
            if (!seenRune.Add(r.runeType)) errors.Add($"符文 runeType 重复: {r.runeType}");
            if (string.IsNullOrEmpty(r.name)) errors.Add($"符文 {r.runeType} 缺少名称");
        }

        var seenRelic = new HashSet<int>();
        foreach (var r in cfg.relics ?? new List<GameConfigRelicDTO>())
        {
            if (!seenRelic.Add(r.relicType)) errors.Add($"圣物 relicType 重复: {r.relicType}");
            if (string.IsNullOrEmpty(r.name)) errors.Add($"圣物 {r.relicType} 缺少名称");
        }

        // ---- 装备档位: rarity 唯一 + 系数 ----
        var seenRarity = new HashSet<int>();
        foreach (var e in cfg.equipment ?? new List<GameConfigEquipmentDTO>())
        {
            if (!seenRarity.Add(e.rarity)) errors.Add($"装备档位 rarity 重复: {e.rarity}");
            if (e.tierMultiplier <= 0) errors.Add($"装备 rarity {e.rarity} tierMultiplier 非法: {e.tierMultiplier}");
        }

        // ---- 全局参数: key 唯一 + value 非空 ----
        var seenGlobal = new HashSet<string>();
        foreach (var g in cfg.global ?? new List<GameConfigGlobalEntry>())
        {
            if (string.IsNullOrEmpty(g.key)) { errors.Add("全局参数存在空 key"); continue; }
            if (!seenGlobal.Add(g.key)) errors.Add($"全局参数 key 重复: {g.key}");
            if (g.value == null) errors.Add($"全局参数 {g.key} value 为null");
        }

        return errors;
    }
}
