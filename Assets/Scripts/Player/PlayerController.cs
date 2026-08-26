using UnityEngine;
using System.Collections.Generic;

public class PlayerController : MonoBehaviour
{
    [Header("Class")]
    public HeroClass HeroClass = HeroClass.Warrior;
    public ClassData ClassData;

    [Header("Stats")]
    public Stats Stats;
    public EquipmentInventory Inventory;

    [Header("Skills")]
    public List<SkillData> Skills = new List<SkillData>();

    [Header("Combat")]
    public float AutoAttackRange = 1.5f;

    // Sub-controllers
    public PlayerInputController InputCtrl { get; private set; }
    public PlayerCombatController Combat { get; private set; }
    public PlayerSkillController SkillCtrl { get; private set; }
    public PlayerStateMachine StateMachine { get; private set; }
    public CharacterSprite CharSprite => charSprite;
    public SpriteRenderer MainSpriteRenderer => mainSpriteRenderer;

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private CharacterSprite charSprite;
    private SpriteRenderer mainSpriteRenderer;

    // Combo (owned by PlayerController for external access)
    public int ComboCount { get; set; }

    // Run loot tracking — items/relics/gold collected this dungeon run
    public List<EquipmentItem> RunLootItems { get; private set; } = new List<EquipmentItem>();
    public List<RelicData> RunLootRelics { get; private set; } = new List<RelicData>();
    public int RunGoldEarned { get; set; }
    public int RunXpEarned { get; set; }

    // Per-run mutation pool — randomly drawn at dungeon start for rogue differentiation
    public List<SkillMutation> RunMutationPool { get; private set; } = new List<SkillMutation>();

    public void RollRunMutationPool(int count = -1)
    {
        RunMutationPool = MutationPool.RollRunPool(HeroClass, count);
    }

    public void ResetRunLoot()
    {
        RunLootItems.Clear();
        RunLootRelics.Clear();
        RunGoldEarned = 0;
        RunXpEarned = 0;
        // Reset mutations on all skills for the new run
        foreach (var skill in Skills)
            skill.ResetMutations();
    }

    // Death state
    public bool IsDead { get; set; }

    // Combat pass-through properties (for backward compatibility with GameUI etc.)
    public float CurrentAttackCooldown => Stats.EffectiveAttackCooldown;
    public int MaxHp => Stats.TotalMaxHp;
    public int CurrentHp => Stats.Hp;
    public float Skill1CooldownRemaining => SkillCtrl != null ? SkillCtrl.GetCooldownRemaining(PlayerSkillSlot.Skill1) : 0f;
    public float Skill2CooldownRemaining => SkillCtrl != null ? SkillCtrl.GetCooldownRemaining(PlayerSkillSlot.Skill2) : 0f;
    public float Skill3CooldownRemaining => SkillCtrl != null ? SkillCtrl.GetCooldownRemaining(PlayerSkillSlot.Skill3) : 0f;
    public float Skill1CooldownMax => SkillCtrl != null ? SkillCtrl.GetMaxCooldown(PlayerSkillSlot.Skill1) : 1f;
    public float Skill2CooldownMax => SkillCtrl != null ? SkillCtrl.GetMaxCooldown(PlayerSkillSlot.Skill2) : 1f;
    public float Skill3CooldownMax => SkillCtrl != null ? SkillCtrl.GetMaxCooldown(PlayerSkillSlot.Skill3) : 1f;
    public float DashCooldownRemaining => SkillCtrl != null ? SkillCtrl.GetCooldownRemaining(PlayerSkillSlot.Dash) : 0f;
    public float DashCooldownMax => SkillCtrl != null ? SkillCtrl.GetMaxCooldown(PlayerSkillSlot.Dash) : 2f;

    public void InitializeClass(HeroClass heroClass)
    {
        HeroClass = heroClass;
        ClassData = ClassData.GetClassData(heroClass);
        Stats = new Stats(heroClass);
        AutoAttackRange = ClassData.AutoAttackRange;
        Skills = SkillData.GetClassSkills(heroClass);
        Inventory = new EquipmentInventory();

        if (SkillCtrl != null)
            SkillCtrl.SetSkills(Skills.ToArray());

        // Setup character sprite after class is known (not in Awake where HeroClass is still default Warrior)
        if (charSprite != null)
            charSprite.Setup(transform, HeroClass);
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.clear;
            mainSpriteRenderer = spriteRenderer;
        }

        charSprite = gameObject.AddComponent<CharacterSprite>();

        if (Stats == null)
            InitializeClass(HeroClass);

        // Create sub-controllers
        InputCtrl = gameObject.AddComponent<PlayerInputController>();
        Combat = gameObject.AddComponent<PlayerCombatController>();
        SkillCtrl = new PlayerSkillController(this);
        StateMachine = new PlayerStateMachine();
        Combat.Initialize(this, InputCtrl, SkillCtrl, StateMachine, rb, spriteRenderer, charSprite);
    }

    private void Update()
    {
        if (IsDead)
        {
            if (Combat.IsAutoBattle) Combat.IsAutoBattle = false;
            return;
        }

        Stats.RegenerateHp();
        Stats.UpdateBuffs();
        Stats.CheckDirtySave();
        HeartbeatPing();

        if (Combat.IsAutoBattle)
        {
            // Auto-battle: skip manual input, animation driven by FixedUpdate
            Combat.TickUpdate();
            if (charSprite != null)
            {
                charSprite.UpdateAnimation(Combat.AutoMoveDir.magnitude);
                if (Combat.AutoMoveDir.sqrMagnitude > 0.01f)
                    charSprite.SetDirection(Combat.AutoMoveDir);
            }
            return;
        }

        if (charSprite != null)
        {
            charSprite.SetDirection(InputCtrl.MoveInput);
            charSprite.UpdateAnimation(InputCtrl.MoveInput.magnitude);
        }

        Combat.TickUpdate();
    }

    private void FixedUpdate()
    {
        if (IsDead) return;

        if (Combat.IsAutoBattle && !Combat.IsDashing)
        {
            Combat.TickFixedUpdate(Combat.AutoMoveDir);
            if (charSprite != null && Combat.AutoMoveDir.sqrMagnitude > 0.01f)
            {
                if (Combat.AutoMoveDir.x < -0.1f)
                    charSprite.SetFacing(true);
                else if (Combat.AutoMoveDir.x > 0.1f)
                    charSprite.SetFacing(false);
            }
            return;
        }

        Combat.TickFixedUpdate(InputCtrl.MoveInput);

        // 联网模式: 发送位置同步到Game Server
        if (ClientNetworkManager.Instance != null && ClientNetworkManager.Instance.isConnected)
        {
            ClientNetworkManager.Instance.SendMove(transform.position, InputCtrl.MoveInput);
        }

        // Facing — only need to drive CharSprite (main renderer is color.clear)
        if (!Combat.IsDashing && charSprite != null)
        {
            if (InputCtrl.MoveInput.x < -0.1f)
                charSprite.SetFacing(true);
            else if (InputCtrl.MoveInput.x > 0.1f)
                charSprite.SetFacing(false);
        }
    }

    // === PUBLIC API (delegates to Combat) ===

    public void TakeDamage(int rawDamage) => Combat.TakeDamage(rawDamage);

    public void Heal(int amount)
    {
        if (IsDead) return;
        Stats.Hp = Mathf.Min(Stats.TotalMaxHp, Stats.Hp + amount);
        VFXHelper.SpawnHealEffect(transform.position, amount);
    }

    public void GainXp(int amount)
    {
        int bonusXp = ComboCount > 3 ? Mathf.RoundToInt(amount * (1f + ComboCount * 0.05f)) : amount;
        RunXpEarned += bonusXp;
        int oldLevel = Stats.Level;
        Stats.AddXp(bonusXp);
        if (Stats.Level > oldLevel)
        {
            // Stats + SkillPoints already grown in PlayerProgressData.LevelUp()
            Stats.Hp = Stats.TotalMaxHp; // Full heal on level up

            // Achievement tracking
            if (AchievementManager.Instance != null)
                AchievementManager.Instance.RecordLevel(Stats.Level);

                        GameLog.Log($"Level Up! Now level {Stats.Level}");
            VFXHelper.SpawnLevelUpEffect(transform.position);
            AudioManager.Instance?.PlayLevelUp();
            CameraFollow.SlowMotion(0.2f, 0.15f);
        }
    }

    public void RegisterKill()
    {
        ComboCount++;
    }

    public void Die()
    {
        IsDead = true;
        rb.velocity = Vector2.zero;
        if (Combat != null) Combat.StopAllCoroutines();
        VFXHelper.SpawnPlayerDeathEffect(transform.position, ClassData?.PrimaryColor ?? Color.white);
        CameraFollow.SlowMotion(0.1f, 0.3f);
        if (charSprite != null)
            charSprite.PlayDeath();
        else
        {
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
                sr.color = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        }
        GameManager.Instance.OnPlayerDeath();
    }

    private float _lastHeartbeat;
    private const float HeartbeatInterval = 30f;

    private void HeartbeatPing()
    {
        if (Time.time - _lastHeartbeat < HeartbeatInterval) return;
        _lastHeartbeat = Time.time;
        CloudSaveManager.Instance?.SendPing();
    }
}
