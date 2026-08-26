using UnityEngine;

public class PlayerStateMachine
{
    private PlayerState _currentState;
    private float _invincibilityEndTime;
    private float _dashEndTime;
    private float _castEndTime;

    public PlayerState CurrentState => _currentState;
    public bool IsInvincible => Time.time < _invincibilityEndTime;
    public bool IsDashing => Time.time < _dashEndTime;
    public bool IsCasting => Time.time < _castEndTime;

    public void SetState(PlayerState newState)
    {
        if (newState == PlayerState.Dead)
            _currentState = PlayerState.Dead;
        else if (_currentState == PlayerState.Dead)
            return;

        _currentState = newState;
    }

    public void EnterInvincibility(float duration)
    {
        _invincibilityEndTime = Time.time + duration;
    }

    public void EnterDash(float duration)
    {
        _dashEndTime = Time.time + duration;
        SetState(PlayerState.Dashing);
    }

    public void EnterCast(float duration)
    {
        _castEndTime = Time.time + duration;
        SetState(PlayerState.CastingSkill);
    }

    public void Tick()
    {
        if (_currentState == PlayerState.Dead)
            return;

        if (IsDashing)
            SetState(PlayerState.Dashing);
        else if (IsCasting)
            SetState(PlayerState.CastingSkill);
        else if (IsInvincible)
            SetState(PlayerState.Invincible);
        else
            SetState(PlayerState.Idle);
    }

    public void Reset()
    {
        _currentState = PlayerState.Idle;
        _invincibilityEndTime = 0f;
        _dashEndTime = 0f;
        _castEndTime = 0f;
    }
}
