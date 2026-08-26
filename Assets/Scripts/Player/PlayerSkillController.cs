using UnityEngine;

/// <summary>
/// 纯CD制技能控制器 — 每个技能有独立冷却, 升级可减CD
/// </summary>
public class PlayerSkillController
{
    private SkillData[] _skills = new SkillData[3];
    private PlayerController _player;
    private PlayerCombatController _combat;

    // 每个技能的CD计时器
    private readonly float[] _cooldownTimers = new float[3];
    private float _dashCooldownTimer;
    private const float DashCD = 1.5f;

    // 基础CD (当SkillData没有CooldownPerLevel时使用)
    private static readonly float[] BaseCooldowns = { 4f, 6f, 10f };

    public PlayerSkillController(PlayerController player)
    {
        _player = player;
        _combat = player?.Combat;
    }

    public void SetCombat(PlayerCombatController combat) => _combat = combat;

    public void SetSkills(SkillData[] skills)
    {
        if (skills == null) return;
        for (int i = 0; i < Mathf.Min(skills.Length, 3); i++)
            _skills[i] = skills[i];
    }

    public void Tick(float deltaTime)
    {
        for (int i = 0; i < 3; i++)
            if (_cooldownTimers[i] > 0f)
                _cooldownTimers[i] -= deltaTime;

        if (_dashCooldownTimer > 0f)
            _dashCooldownTimer -= deltaTime;
    }

    private float GetSkillMaxCD(int index)
    {
        if (index < 0 || index >= _skills.Length || _skills[index] == null)
            return BaseCooldowns[Mathf.Min(index, 2)];
        // 使用SkillData的CurrentCooldown (支持升级减CD)
        return _skills[index].CurrentCooldown > 0 ? _skills[index].CurrentCooldown : BaseCooldowns[index];
    }

    public float GetCooldownRemaining(PlayerSkillSlot slot)
    {
        if (slot == PlayerSkillSlot.Dash)
            return Mathf.Max(0f, _dashCooldownTimer);

        int idx = GetSlotIndex(slot);
        if (idx < 0 || idx >= 3) return 0f;
        return Mathf.Max(0f, _cooldownTimers[idx]);
    }

    public float GetMaxCooldown(PlayerSkillSlot slot)
    {
        if (slot == PlayerSkillSlot.Dash)
            return DashCD;

        int idx = GetSlotIndex(slot);
        if (idx < 0) return 1f;
        return GetSkillMaxCD(idx);
    }

    public bool IsReady(PlayerSkillSlot slot)
    {
        if (slot == PlayerSkillSlot.Dash)
            return _dashCooldownTimer <= 0f;

        int index = GetSlotIndex(slot);
        if (index < 0 || index >= _skills.Length || _skills[index] == null) return false;
        return _cooldownTimers[index] <= 0f;
    }

    // 兼容旧接口 — 返回0表示不需要精神值
    public float GetSpiritCost(PlayerSkillSlot slot) => 0f;
    public float GetSpiritRemaining() => 0f;
    public float GetMaxSpirit() => 1f;

    public bool TryUseSkill(PlayerSkillSlot slot)
    {
        if (!IsReady(slot)) return false;

        switch (slot)
        {
            case PlayerSkillSlot.Skill1:
            case PlayerSkillSlot.Skill2:
            case PlayerSkillSlot.Skill3:
                int index = GetSlotIndex(slot);
                if (index < 0 || index >= _skills.Length || _skills[index] == null) return false;
                _cooldownTimers[index] = GetSkillMaxCD(index);
                _combat?.ExecuteSkill(_skills[index], (SkillSlot)index);
                return true;

            case PlayerSkillSlot.Dash:
                _dashCooldownTimer = DashCD;
                _combat?.ExecuteDash();
                return true;

            default:
                return false;
        }
    }

    private int GetSlotIndex(PlayerSkillSlot slot) => slot switch
    {
        PlayerSkillSlot.Skill1 => 0,
        PlayerSkillSlot.Skill2 => 1,
        PlayerSkillSlot.Skill3 => 2,
        _ => -1
    };
}
