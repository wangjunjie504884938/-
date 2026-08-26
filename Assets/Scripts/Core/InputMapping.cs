using UnityEngine;

/// <summary>
/// Centralized input key mappings. Edit this file to change all key bindings.
/// </summary>
public static class InputMapping
{
    // Movement
    public const KeyCode MoveUp = KeyCode.W;
    public const KeyCode MoveDown = KeyCode.S;
    public const KeyCode MoveLeft = KeyCode.A;
    public const KeyCode MoveRight = KeyCode.D;

    // Combat
    public const KeyCode Attack = KeyCode.Space;
    public const KeyCode AltAttack = KeyCode.J;

    // Dash
    public const KeyCode Dash = KeyCode.LeftShift;
    public const KeyCode AltDash = KeyCode.K;

    // Skills
    public const KeyCode Skill1 = KeyCode.Q;
    public const KeyCode Skill2 = KeyCode.E;
    public const KeyCode Skill3 = KeyCode.R;

    // Potion
    public const KeyCode Potion = KeyCode.H;

    // UI Navigation
    public const KeyCode Confirm = KeyCode.Return;
    public const KeyCode Cancel = KeyCode.Escape;

    // Pause
    public const KeyCode Pause = KeyCode.P;
}
