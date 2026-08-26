using UnityEngine;
using UnityEngine.UI;
using System;

/// <summary>
/// Handles all player input (keyboard, touch buttons, joystick).
/// Input polling is centralized here; other controllers consume the cached values.
/// </summary>
public class PlayerInputController : MonoBehaviour
{
    public Vector2 MoveInput { get; private set; }
    public bool DashPressed { get; private set; }
    public bool Skill1Pressed { get; private set; }
    public bool PotionPressed { get; private set; }
    public bool Skill2Pressed { get; private set; }
    public bool Skill3Pressed { get; private set; }
    public bool AttackPressed { get; private set; }

    // Button references (wired by GameManager during UI build)
    public GameObject Skill1BtnObj { get; set; }
    public GameObject Skill2BtnObj { get; set; }
    public GameObject Skill3BtnObj { get; set; }
    public GameObject DashBtnObj { get; set; }
    public GameObject AttackBtnObj { get; set; }

    private bool _useKeyboard = true;
    private PlayerSkillController _skillController;
    private VirtualJoystick _joystick;

    public bool UseKeyboard
    {
        get => _useKeyboard;
        set => _useKeyboard = value;
    }

    private bool _buttonsWired;

    public void Initialize(PlayerSkillController skillController)
    {
        _skillController = skillController;
    }

    /// <summary>
    /// Called by GameManager after all UI button GameObjects have been assigned.
    /// Wires skill/dash button callbacks via SkillButton.OnPressedCallback.
    /// </summary>
    public void WireButtonListeners()
    {
        if (_buttonsWired) return;

        WireSkillButton(DashBtnObj, OnDashButton);
        WireSkillButton(Skill1BtnObj, OnSkill1Button);
        WireSkillButton(Skill2BtnObj, OnSkill2Button);
        WireSkillButton(Skill3BtnObj, OnSkill3Button);

        _buttonsWired = true;
    }

    private void WireSkillButton(GameObject btnObj, Action callback)
    {
        if (btnObj == null) return;
        var skillBtn = btnObj.GetComponent<SkillButton>();
        if (skillBtn != null)
            skillBtn.OnPressedCallback = callback;
    }

    private void Update()
    {
        Tick();
    }

    public void Tick()
    {
        // Reset single-frame flags
        DashPressed = false;
        Skill1Pressed = false;
        Skill2Pressed = false;
        Skill3Pressed = false;
        PotionPressed = false;

        // Movement input — lazy-find joystick (created after player in build order)
        if (_joystick == null && Time.frameCount % 30 == 0)
            _joystick = FindObjectOfType<VirtualJoystick>();

        if (_useKeyboard)
        {
            Vector2 keyboard = new Vector2(
                Input.GetAxisRaw("Horizontal"),
                Input.GetAxisRaw("Vertical")
            );
            if (keyboard.sqrMagnitude > 0.01f)
                MoveInput = keyboard.normalized;
            else if (_joystick != null)
                MoveInput = _joystick.InputVector;
            else
                MoveInput = Vector2.zero;
        }
        else if (_joystick != null)
        {
            MoveInput = _joystick.InputVector;
        }
        else
        {
            MoveInput = Vector2.zero;
        }

        // Combat inputs
        if (Input.GetKeyDown(InputMapping.Dash) || Input.GetKeyDown(InputMapping.AltDash))
            DashPressed = true;

        if (Input.GetKeyDown(InputMapping.Skill1))
            Skill1Pressed = true;

        if (Input.GetKeyDown(InputMapping.Skill2))
            Skill2Pressed = true;

        if (Input.GetKeyDown(InputMapping.Skill3))
            Skill3Pressed = true;

        if (Input.GetKeyDown(InputMapping.Potion))
            PotionPressed = true;

        // Attack: keyboard uses GetKey, touch uses AttackButton.Instance.IsPressed
        bool keyboardAttack = Input.GetKey(InputMapping.Attack) || Input.GetKey(InputMapping.AltAttack);
        bool touchAttack = AttackButton.Instance != null && AttackButton.Instance.IsPressed;
        AttackPressed = keyboardAttack || touchAttack;
    }

    // Touch button callbacks (called from SkillButton.OnPressedCallback)
    public void OnDashButton()
    {
        DashPressed = true;
    }

    public void OnSkill1Button()
    {
        Skill1Pressed = true;
    }

    public void OnSkill2Button()
    {
        Skill2Pressed = true;
    }

    public void OnSkill3Button()
    {
        Skill3Pressed = true;
    }
}
