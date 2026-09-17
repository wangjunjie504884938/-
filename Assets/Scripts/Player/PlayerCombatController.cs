using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public partial class PlayerCombatController : MonoBehaviour
{
    private static readonly Collider2D[] _overlapBuffer = new Collider2D[64];

    private PlayerController player;
    private PlayerInputController input;
    private PlayerSkillController skillCtrl;
    private PlayerStateMachine stateMachine;
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private CharacterSprite charSprite;

    private float lastAttackTime;
    private float autoAttackTimer;

    private float lastDamageTime;
    private float invincibilityTime = 0.5f;
    private Vector2 _dashDirection; // Stored dash direction (fixes stationary dash)

    private float comboTimer;
    private float comboTimeout = 2f;

    // 从配置读取的战斗参数
    private float critMult = 2f;

    private float passiveHealTimer;

    private int _prevShieldHp;
    private bool _shieldEvolved;
    private DivineShieldVFX _activeShieldVFX;

    // Attack buffering — queue next attack during current swing
    private bool bufferedAttack;
    private float bufferedAttackTime;
    private const float BufferWindow = 0.25f;

    // Hit-stop — brief Time.timeScale freeze on impactful hits
    private bool isHitStopping;
    private float hitStopTimer;
    private const float HitStopDuration = 0.03f;

    public float Skill1CooldownRemaining => skillCtrl != null ? skillCtrl.GetCooldownRemaining(PlayerSkillSlot.Skill1) : 0f;
    public float Skill2CooldownRemaining => skillCtrl != null ? skillCtrl.GetCooldownRemaining(PlayerSkillSlot.Skill2) : 0f;
    public float Skill3CooldownRemaining => skillCtrl != null ? skillCtrl.GetCooldownRemaining(PlayerSkillSlot.Skill3) : 0f;
    public float Skill1CooldownMax => skillCtrl != null ? skillCtrl.GetMaxCooldown(PlayerSkillSlot.Skill1) : 1f;
    public float Skill2CooldownMax => skillCtrl != null ? skillCtrl.GetMaxCooldown(PlayerSkillSlot.Skill2) : 1f;
    public float Skill3CooldownMax => skillCtrl != null ? skillCtrl.GetMaxCooldown(PlayerSkillSlot.Skill3) : 1f;
    public float DashCooldownRemaining => skillCtrl != null ? skillCtrl.GetCooldownRemaining(PlayerSkillSlot.Dash) : 0f;
    public float DashCooldownMax => skillCtrl != null ? skillCtrl.GetMaxCooldown(PlayerSkillSlot.Dash) : 2f;

    public float DashCooldown = 1.2f;
    public float DashSpeed = 28f;
    public float DashDuration = 0.12f;
    public bool IsDashing => stateMachine != null ? stateMachine.IsDashing : false;

    // === Auto-battle ===
    public bool IsAutoBattle { get; set; }
    public Vector2 AutoMoveDir { get; private set; }
    private float _autoThinkTimer;

    public void Initialize(PlayerController pc, PlayerInputController ic, PlayerSkillController sc, PlayerStateMachine sm, Rigidbody2D rb2d, SpriteRenderer sr, CharacterSprite cs)
    {
        player = pc; input = ic; skillCtrl = sc; stateMachine = sm; rb = rb2d; spriteRenderer = sr; charSprite = cs;

        // 从数据库配置读取战斗常量
        if (GameConfigManager.Instance != null && GameConfigManager.Instance.IsLoaded)
        {
            invincibilityTime = GameConfigManager.Instance.GetGlobal("InvincibilityTime", 0.5f);
            comboTimeout = GameConfigManager.Instance.GetGlobal("ComboTimeout", 2f);
            critMult = GameConfigManager.Instance.GetGlobal("CritMultiplier", 2f);
            DashCooldown = GameConfigManager.Instance.GetGlobal("DashCooldown", 1.2f);
            DashSpeed = GameConfigManager.Instance.GetGlobal("DashSpeed", 28f);
            DashDuration = GameConfigManager.Instance.GetGlobal("DashDuration", 0.12f);
        }
    }

    public void TickUpdate()
    {
        if (player.IsDead)
        {
            // Ensure timeScale is restored if player dies during hit-stop
            if (isHitStopping) { isHitStopping = false; Time.timeScale = 1f; }
            return;
        }

        // Hit-stop: freeze everything during impact
        if (isHitStopping)
        {
            hitStopTimer += Time.unscaledDeltaTime;
            if (hitStopTimer >= HitStopDuration)
            {
                isHitStopping = false;
                hitStopTimer = 0f;
                Time.timeScale = 1f;
            }
            return;
        }

        if (player.HeroClass == HeroClass.Priest && player.Stats.HpRegen > 0)
        {
            passiveHealTimer += Time.deltaTime;
            if (passiveHealTimer >= 1f)
            {
                passiveHealTimer -= 1f;
                player.Heal(Mathf.RoundToInt(player.Stats.HpRegen));
            }
        }

        stateMachine.Tick();
        skillCtrl.Tick(Time.deltaTime);
        if (_potionCooldown > 0f) _potionCooldown -= Time.deltaTime;

        // Dash always cancels attacks/skills — priority over everything
        if (input.DashPressed && skillCtrl.IsReady(PlayerSkillSlot.Dash))
        {
            skillCtrl.TryUseSkill(PlayerSkillSlot.Dash);
            return;
        }

        // Potion use (H key) — not during dash
        if (input.PotionPressed && !stateMachine.IsDashing)
        {
            UsePotion();
            return;
        }

        // Skill cancel: can cancel attack during last 40% of swing, or cast near end
        bool canCancelAttack = (charSprite != null && charSprite.IsAttacking && player.ComboCount >= 0)
                               || stateMachine.IsCasting;

        if (input.Skill1Pressed && (stateMachine.CurrentState == PlayerState.Idle || canCancelAttack))
        {
            skillCtrl.TryUseSkill(PlayerSkillSlot.Skill1);
            return;
        }
        if (input.Skill2Pressed && (stateMachine.CurrentState == PlayerState.Idle || canCancelAttack))
        {
            skillCtrl.TryUseSkill(PlayerSkillSlot.Skill2);
            return;
        }
        if (input.Skill3Pressed && (stateMachine.CurrentState == PlayerState.Idle || canCancelAttack))
        {
            skillCtrl.TryUseSkill(PlayerSkillSlot.Skill3);
            return;
        }

        // Attack input — buffer if currently attacking
        if (input.AttackPressed)
        {
            if (Time.time - lastAttackTime >= player.CurrentAttackCooldown)
            {
                EnemyController nearest = FindNearestEnemy(player.AutoAttackRange);
                AutoAttack(nearest);
            }
            else if (charSprite != null && charSprite.IsAttacking)
            {
                // Buffer the attack — execute when current swing ends
                bufferedAttack = true;
                bufferedAttackTime = Time.time;
            }
        }

        // Execute buffered attack after swing completes
        if (bufferedAttack && Time.time - bufferedAttackTime < BufferWindow)
        {
            if (charSprite == null || !charSprite.IsAttacking)
            {
                if (Time.time - lastAttackTime >= player.CurrentAttackCooldown)
                {
                    bufferedAttack = false;
                    EnemyController nearest = FindNearestEnemy(player.AutoAttackRange);
                    AutoAttack(nearest);
                }
            }
        }
        else if (bufferedAttack && Time.time - bufferedAttackTime >= BufferWindow)
        {
            bufferedAttack = false;
        }

        if (player.ComboCount > 0)
        {
            comboTimer -= Time.deltaTime;
            if (comboTimer <= 0f)
                player.ComboCount = 0;
        }

        // === Auto-battle ===
        if (IsAutoBattle && !isHitStopping)
            AutoBattleTick();
    }

    private void AutoBattleTick()
    {
        _autoThinkTimer -= Time.deltaTime;
        if (_autoThinkTimer > 0f) return;
        _autoThinkTimer = 0.1f;

        AutoMoveDir = Vector2.zero;

        // 1. Auto potion: HP < 40%
        float hpPct = player.Stats.TotalMaxHp > 0 ? (float)player.Stats.Hp / player.Stats.TotalMaxHp : 1f;
        if (hpPct < 0.4f && _potionCooldown <= 0f && player.Stats.Runtime.Potions > 0)
        {
            UsePotion();
        }

        // 2. Find nearest enemy
        EnemyController nearest = FindNearestEnemy(player.AutoAttackRange + 2f);
        if (nearest == null)
        {
            // No enemy in extended range — search wider
            nearest = FindNearestEnemy(15f);
            if (nearest == null)
            {
                // No enemies at all — move toward map center to find them
                Vector2 toCenter = (Vector2.zero - (Vector2)player.transform.position);
                AutoMoveDir = toCenter.magnitude > 1f ? toCenter.normalized * 0.5f : Vector2.zero;
                return;
            }
        }

        float dist = Vector2.Distance(player.transform.position, nearest.transform.position);
        Vector2 dir = (nearest.transform.position - player.transform.position).normalized;

        // 3. Auto skills when in range
        bool canCast = stateMachine.CurrentState == PlayerState.Idle ||
                       (charSprite != null && charSprite.IsAttacking) ||
                       stateMachine.IsCasting;

        if (canCast)
        {
            if (skillCtrl.IsReady(PlayerSkillSlot.Skill3) && skillCtrl.TryUseSkill(PlayerSkillSlot.Skill3))
                return;
            if (skillCtrl.IsReady(PlayerSkillSlot.Skill2) && skillCtrl.TryUseSkill(PlayerSkillSlot.Skill2))
                return;
            if (skillCtrl.IsReady(PlayerSkillSlot.Skill1) && skillCtrl.TryUseSkill(PlayerSkillSlot.Skill1))
                return;
        }

        // 4. Auto attack when in melee/attack range
        if (dist <= player.AutoAttackRange)
        {
            AutoMoveDir = Vector2.zero; // Stop and attack
            if (Time.time - lastAttackTime >= player.CurrentAttackCooldown)
            {
                AutoAttack(nearest);
            }
        }
        else
        {
            // 5. Move toward enemy
            AutoMoveDir = dir;
        }
    }

    public void TickFixedUpdate(Vector2 moveInput)
    {
        if (player.IsDead) return;
        if (stateMachine.IsDashing)
        {
            rb.velocity = _dashDirection * DashSpeed;
            if (Random.Range(0f, 1f) < 0.5f)
            {
                VFXHelper.SpawnHitParticles(player.transform.position, new Color(0.7f, 0.7f, 0.7f, 0.5f), 1);
                VFXHelper.SpawnLandingDust(player.transform.position, -_dashDirection);
            }
            return;
        }
        rb.velocity = moveInput * player.Stats.TotalMoveSpeed;
        if (moveInput.magnitude > 0.5f && Random.Range(0f, 1f) < 0.05f)
            VFXHelper.SpawnHitParticles(player.transform.position + new Vector3(0, -0.3f, 0), new Color(0.5f, 0.45f, 0.4f, 0.3f), 1);
    }

    // === Public skill execution API (called by PlayerSkillController) ===

    public void ExecuteSkill(SkillData skill, SkillSlot slot)
    {
        if (stateMachine.CurrentState == PlayerState.Dead || stateMachine.IsDashing)
            return;
        // 不检查IsCasting — 允许技能连发(精神系统已控制频率)

        Vector2 skillDir = spriteRenderer.flipX ? Vector2.left : Vector2.right;
        EnemyController target = FindNearestEnemy(10f);
        if (target != null)
            skillDir = (target.transform.position - player.transform.position).normalized;

        Color vfxColor = skill.RuneColor != Color.white ? skill.RuneColor : player.ClassData.SkillColor1;
        bool isMelee = !player.ClassData.IsRanged && skill.Slot != SkillSlot.Skill3;

        // 联网模式: 发送技能释放消息到Game Server
        if (ClientNetworkManager.Instance != null && ClientNetworkManager.Instance.isConnected)
        {
            int skillId = slot switch { SkillSlot.Skill1 => 0, SkillSlot.Skill2 => 1, SkillSlot.Skill3 => 2, _ => 3 };
            ClientNetworkManager.Instance.SendSkillCast(skillId, skillDir);
        }

        if (charSprite != null)
        {
            if (isMelee)
                charSprite.TriggerMeleeSkill(skillDir);
            else
                charSprite.TriggerSkillCast(skillDir);
        }

        // Charge effect for ranged skills
        if (!isMelee)
            StartCoroutine(VFXHelper.SkillChargeEffect(player.transform.position, vfxColor, 0.2f));

        stateMachine.EnterCast(isMelee ? 0.4f : 0.32f);
        VFXHelper.SpawnScreenFlash(vfxColor, 0.12f);
        CameraFollow.Shake(0.15f, 0.1f);
        ExecuteSkillUnified(skill);
    }

    public void ExecuteDash()
    {
        // PoE2-style dodge roll: i-frames + direction-based + can cancel skill cast
        Vector2 dashDir = input.MoveInput.sqrMagnitude > 0.01f ? input.MoveInput.normalized : (spriteRenderer.flipX ? Vector2.left : Vector2.right);
        _dashDirection = dashDir; // Store for TickFixedUpdate

        // Cancel any active skill cast
        if (stateMachine.IsCasting)
            stateMachine.Reset();

        stateMachine.EnterDash(DashDuration);
        // Extended i-frames — the core defensive tool in PoE2
        float iframeDuration = DashDuration + 0.15f; // brief grace period after dash
        stateMachine.EnterInvincibility(iframeDuration);

        // Dash VFX trail in movement direction
        VFXHelper.SpawnAreaPulse(player.transform.position, 0.5f, new Color(0.5f, 0.7f, 1f, 0.5f));
        VFXHelper.SpawnHitParticles(player.transform.position + (Vector3)(dashDir * -0.3f), new Color(0.4f, 0.6f, 1f, 0.4f), 2);

        AudioManager.Instance?.PlayDash();
        if (charSprite != null)
            charSprite.TriggerDash(dashDir);
    }

    // Access for PlayerSkillController
    public SkillData GetSkill(PlayerSkillSlot slot)
    {
        int idx = slot switch { PlayerSkillSlot.Skill1 => 0, PlayerSkillSlot.Skill2 => 1, PlayerSkillSlot.Skill3 => 2, _ => -1 };
        return idx >= 0 && idx < player.Skills.Count ? player.Skills[idx] : null;
    }

    public PlayerController GetPlayer() => player;

    public Rigidbody2D GetRb() => rb;

    public SpriteRenderer GetSpriteRenderer() => spriteRenderer;

    public CharacterSprite GetCharSprite() => charSprite;

    private void AutoAttack(EnemyController target)
    {
        if (Time.time - lastAttackTime < player.CurrentAttackCooldown) return;
        lastAttackTime = Time.time;

        if (player.ClassData.IsRanged)
        {
            Vector2 dir = target != null
                ? (target.transform.position - player.transform.position).normalized
                : (spriteRenderer.flipX ? Vector2.left : Vector2.right);
            if (charSprite != null) charSprite.TriggerAttack(dir);
            SpawnAutoAttackProjectile(dir, target);
        }
        else
        {
            Vector2 dir;
            if (target != null)
            {
                dir = (target.transform.position - player.transform.position).normalized;
                if (charSprite != null) charSprite.TriggerAttack(dir);
                MeleeAttack(target, dir);
            }
            else
            {
                dir = spriteRenderer.flipX ? Vector2.left : Vector2.right;
                if (charSprite != null) charSprite.TriggerAttack(dir);
                CleaveAttack(dir);
            }
            // Attack lunge — small forward dash on melee attack
            if (rb != null)
            {
                rb.velocity = dir * 12f;
            }
        }
    }

    private void MeleeAttack(EnemyController enemy, Vector2 dir)
    {
        bool isCrit;
        int damage = CalculateDamageWithCrit(out isCrit);

        switch (player.HeroClass)
        {
            case HeroClass.Warrior:
                VFXHelper.SpawnWarriorSlash(player.transform.position, dir, player.Stats.TotalAttackRange);
                break;
            default:
                VFXHelper.SpawnSlashEffect(player.transform.position, dir, player.Stats.TotalAttackRange, player.ClassData.SkillColor1);
                break;
        }

        CameraFollow.Shake(isCrit ? 0.2f : 0.1f, isCrit ? 0.1f : 0.06f);
        AudioManager.Instance?.PlaySlash();
        enemy.TakeDamage(damage, player);

        VFXHelper.SpawnDamageNumber(enemy.transform.position, damage, isCrit);

        VFXHelper.SpawnHitParticles(enemy.transform.position, player.ClassData.SkillColor1, 1);

        if (isCrit) VFXHelper.SpawnImpactFlash(enemy.transform.position, Color.yellow, 0.6f);
        ApplyLifeSteal(damage);

        var enemyRb = enemy.GetComponent<Rigidbody2D>();
        if (enemyRb != null)
            enemyRb.AddForce(dir * (isCrit ? 8f : 4f), ForceMode2D.Impulse);

        if (isCrit)
        {
            AudioManager.Instance?.PlayCrit();
            if (charSprite != null) charSprite.TriggerCritStagger(dir);
        }

        if (isCrit && RelicManager.Instance != null)
            RelicManager.Instance.OnCritDealt(enemy.transform.position, damage);

        if (RelicManager.Instance != null && RelicManager.Instance.ShouldDoubleStrike())
        {
            int doubleDmg = CalculateDamageWithCrit(out _);
            enemy.TakeDamage(doubleDmg, player);
            VFXHelper.SpawnDamageNumber(enemy.transform.position, doubleDmg, false, new Color(1f, 0.6f, 0.2f));
            VFXHelper.SpawnHitParticles(enemy.transform.position, new Color(1f, 0.6f, 0.2f), 2);
        }
    }

    private void CleaveAttack(Vector2 dir)
    {
        Color slashColor = player.ClassData.SkillColor1;
        VFXHelper.SpawnSlashEffect(player.transform.position, dir, player.Stats.TotalAttackRange, slashColor);
        CameraFollow.Shake(0.08f, 0.06f);

        int hitCount = Physics2D.OverlapCircleNonAlloc(
            player.transform.position, player.Stats.TotalAttackRange, _overlapBuffer, LayerMask.GetMask("Enemy"));
        int totalDamage = 0;

        for (int i = 0; i < hitCount; i++)
        {
            EnemyController enemy = _overlapBuffer[i].GetComponent<EnemyController>();
            if (enemy == null || enemy.IsDead) continue;

            bool crit;
            int dmg = CalculateDamageWithCrit(out crit);
            enemy.TakeDamage(dmg, player);
            VFXHelper.SpawnDamageNumber(enemy.transform.position, dmg, crit);
            // 减少AOE命中粒子: 3→1
            VFXHelper.SpawnHitParticles(enemy.transform.position, slashColor, 1);
            totalDamage += dmg;

            if (crit && RelicManager.Instance != null)
                RelicManager.Instance.OnCritDealt(enemy.transform.position, dmg);
        }

        ApplyLifeSteal(totalDamage);
    }

    private void SpawnAutoAttackProjectile(Vector2 dir, EnemyController target)
    {
        Color projColor = player.HeroClass switch
        {
            HeroClass.Mage => new Color(0.4f, 0.5f, 1f),
            HeroClass.Priest => new Color(1f, 1f, 0.6f),
            _ => new Color(0.4f, 0.6f, 1f)
        };

        GameObject projObj = VFXPool.GetPlayerProjectile();
        projObj.transform.position = player.transform.position + (Vector3)(dir * 0.5f);
        projObj.layer = LayerMask.NameToLayer("Player");

        var sr = projObj.GetComponent<SpriteRenderer>();
        sr.sprite = VFXHelper.GetSharedWhiteSprite();
        sr.color = projColor;
        sr.sortingOrder = 8;
        projObj.transform.localScale = new Vector3(0.3f, 0.3f, 1f);

        var rb2d = projObj.GetComponent<Rigidbody2D>();
        float autoAtkSpeed = GameConfigManager.Instance?.GetGlobal("AutoAttackProjectileSpeed", 8f) ?? 8f;
        rb2d.gravityScale = 0f;
        rb2d.velocity = dir * autoAtkSpeed;

        var col = projObj.GetComponent<CircleCollider2D>();
        col.radius = 0.35f;
        col.isTrigger = true;

        var proj = projObj.GetComponent<PlayerProjectile>();
        proj.Initialize(dir, player.Stats.BuffedAttack, player);
        proj.SetColor(projColor);

        // 尾迹已优化
    }

    /// <summary>
    /// 统一技能执行：根据技能类型（投射物/AOE/Buff/治疗/护盾）+ 符文修正
    /// </summary>
    private void ExecuteSkillUnified(SkillData skill)
    {
        float totalDmgMult = skill.CurrentDamageMultiplier * skill.RuneDamageMultiplier;

        // Relic: BloodMagic — sacrifice HP for damage bonus
        if (RelicManager.Instance != null)
            totalDmgMult *= RelicManager.Instance.GetBloodMagicDamageMultiplier();

        // Skill evolution at Level 5
        bool isEvolved = skill.CurrentLevel >= 5 && !string.IsNullOrEmpty(skill.EvolutionName);
        if (isEvolved)
            totalDmgMult = skill.EvolutionDamageMultiplier * skill.RuneDamageMultiplier;

        // 判断技能类型：战士近战 / 法师牧师远程 / Buff / 治疗 / 护盾
        bool isMelee = !player.ClassData.IsRanged && skill.Slot != SkillSlot.Skill3;
        bool isSelfBuff = skill.Slot == SkillSlot.Skill3 && player.HeroClass == HeroClass.Warrior;
        bool isHeal = skill.Slot == SkillSlot.Skill2 && player.HeroClass == HeroClass.Priest;
        bool isShield = skill.Slot == SkillSlot.Skill3 && player.HeroClass == HeroClass.Priest;
        bool isAoe = skill.Slot == SkillSlot.Skill2 && player.HeroClass == HeroClass.Mage;
        bool isProjectile = player.ClassData.IsRanged && skill.Slot != SkillSlot.Skill2 && !isShield;

        // 范围计算：使用统一公式
        float skillRangeMult = 1.0f;
        if (isMelee) skillRangeMult = PlayerRuntimeStats.SkillRangeCoefficient.Single;
        else if (isAoe) skillRangeMult = PlayerRuntimeStats.SkillRangeCoefficient.MediumAOE;
        else if (isSelfBuff) skillRangeMult = PlayerRuntimeStats.SkillRangeCoefficient.HealSupport;
        else if (isHeal) skillRangeMult = PlayerRuntimeStats.SkillRangeCoefficient.HealSupport;
        else if (isProjectile) skillRangeMult = PlayerRuntimeStats.SkillRangeCoefficient.Single;

        // 原始技能范围
        float range = skill.CurrentRange * skill.AoeRangeMultiplier;
        if (isEvolved && skill.EvolutionName == "暴风雪")
            range *= 1.5f;
        else if (isEvolved && skill.EvolutionName == "龙卷风")
            range *= 1.5f;

        // 统一公式计算的地图范围 vs 技能自带范围，取较大值
        float formulaRange = player.Stats.GetSkillRange(skillRangeMult);
        range = Mathf.Max(range, formulaRange);

        Color vfxColor = skill.RuneColor != Color.white ? skill.RuneColor : player.ClassData.SkillColor1;

        if (isMelee) ExecuteMeleeSkill(skill, totalDmgMult, range, vfxColor);
        else if (isShield) ExecuteShieldSkill(skill);
        else if (isHeal) ExecuteHealSkill(skill);
        else if (isSelfBuff) ExecuteBuffSkill(skill);
        else if (isAoe) ExecuteAoeSkill(skill, totalDmgMult, range, vfxColor);
        else if (isProjectile) ExecuteProjectileSkill(skill, totalDmgMult, range, vfxColor);
        else ExecuteProjectileSkill(skill, totalDmgMult, range, vfxColor); // fallback
    }

    private void ExecuteMeleeSkill(SkillData skill, float dmgMult, float range, Color vfxColor)
    {
        bool isEvolved = skill.CurrentLevel >= 5 && !string.IsNullOrEmpty(skill.EvolutionName);

        // 1. 先造成伤害 (确保即使VFX崩溃也能打出伤害)
        int hitCount = Physics2D.OverlapCircleNonAlloc(player.transform.position, range, _overlapBuffer, LayerMask.GetMask("Enemy"));
        int totalDmg = 0;
        // 批量伤害计算：用数组替代List避免GC
        int hitEnemyCount = 0;
        var hitEnemies = new (EnemyController enemy, int dmg, bool crit)[3];
        for (int i = 0; i < hitCount; i++)
        {
            EnemyController enemy = _overlapBuffer[i].GetComponent<EnemyController>();
            if (enemy != null && !enemy.IsDead)
            {
                bool crit;
                int dmg = Mathf.RoundToInt(CalculateDamageWithCrit(out crit) * dmgMult);

                // Evolution: 破甲猛击 ignores defense
                if (isEvolved && skill.EvolutionName == "破甲猛击")
                    dmg = Mathf.RoundToInt(player.Stats.BuffedAttack * dmgMult);

                // Evolution: 龙卷风 pulls enemies toward center
                if (isEvolved && skill.EvolutionName == "龙卷风")
                {
                    var enemyRb = enemy.GetComponent<Rigidbody2D>();
                    if (enemyRb != null)
                    {
                        Vector2 pullDir = (player.transform.position - enemy.transform.position).normalized;
                        enemyRb.AddForce(pullDir * 8f, ForceMode2D.Impulse);
                    }
                }

                // 批量伤害：只扣血，不触发表现
                enemy.TakeDamage(dmg, player);

                // 记录前3个用于表现
                if (hitEnemyCount < 3)
                {
                    hitEnemies[hitEnemyCount] = (enemy, dmg, crit);
                    hitEnemyCount++;
                }

                totalDmg += dmg;
            }
        }

        // 延迟表现：只前3个怪播放VFX
        for (int i = 0; i < hitEnemyCount; i++)
        {
            var (enemy, dmg, crit) = hitEnemies[i];
            VFXHelper.SpawnHitParticles(enemy.transform.position, vfxColor, 1);
            ApplyRuneEffectsToEnemy(skill, enemy);
        }
        ApplyLifeSteal(totalDmg);
        ApplyRuneLifeSteal(skill, totalDmg);
        if (skill.HasBurn) SpawnBurnZones(skill, player.transform.position, range);

        // 3. 技能VFX（精简）
        CameraFollow.Shake(0.1f, 0.06f);
        VFXHelper.SpawnGroundSlam(player.transform.position, range, vfxColor);
        VFXHelper.SpawnWhirlwindEffect(player.transform.position, range, isEvolved ? 0.8f : 0.4f);

        // 4. 变异效果
        ApplyMeleeMutations(skill, range);

        // 5. 连锁闪电变异（近战也支持）
        int chainStacks = skill.GetMutationStacks(MutationPool.ChainLightning);
        if (chainStacks > 0)
        {
            int chainCount = chainStacks + 1;
            var hitIds = new System.Collections.Generic.HashSet<int>();
            // 从前3个怪连锁
            for (int i = 0; i < hitEnemyCount && chainCount > 0; i++)
            {
                var origin = hitEnemies[i];
                hitIds.Add(origin.enemy.GetInstanceID());
                int nearbyCount = Physics2D.OverlapCircleNonAlloc(
                    origin.enemy.transform.position, 2.5f, _overlapBuffer, LayerMask.GetMask("Enemy"));
                int chained = 0;
                for (int j = 0; j < nearbyCount && chained < chainCount; j++)
                {
                    EnemyController chainEnemy = _overlapBuffer[j].GetComponent<EnemyController>();
                    if (chainEnemy == null || chainEnemy.IsDead || hitIds.Contains(chainEnemy.GetInstanceID())) continue;
                    int chainDmg = Mathf.RoundToInt(origin.dmg * 0.5f);
                    chainEnemy.TakeDamage(chainDmg, player);
                    hitIds.Add(chainEnemy.GetInstanceID());
                    VFXHelper.SpawnLightningStrike(chainEnemy.transform.position, 0.2f);
                    chained++;
                }
                chainCount -= chained;
            }
        }
    }

    private void ExecuteProjectileSkill(SkillData skill, float dmgMult, float range, Color vfxColor)
    {
        EnemyController target = FindNearestEnemy(range);
        Vector2 baseDir = target != null ? (target.transform.position - player.transform.position).normalized : (spriteRenderer.flipX ? Vector2.left : Vector2.right);
        bool isEvolved = skill.CurrentLevel >= 5 && !string.IsNullOrEmpty(skill.EvolutionName);

        int projCount = 1 + skill.ProjectileBonus + (isEvolved ? skill.EvolutionProjectileBonus : 0);
        float spreadAngle = projCount > 1 ? 30f : 0f;
        float defaultSpeed = GameConfigManager.Instance?.GetGlobal("SkillProjectileSpeedDefault", 10f) ?? 10f;
        float speed = player.HeroClass == HeroClass.Priest
            ? (GameConfigManager.Instance?.GetGlobal("SkillProjectileSpeedPriest", 9f) ?? 9f)
            : (skill.Slot == SkillSlot.Skill3
                ? (GameConfigManager.Instance?.GetGlobal("SkillProjectileSpeedSkill3", 12f) ?? 12f)
                : defaultSpeed);
        float projSize = skill.Slot == SkillSlot.Skill3 ? 0.6f : (skill.Slot == SkillSlot.Skill1 ? 0.5f : 0.35f);

        // Energy orb charge at cast position
        StartCoroutine(VFXHelper.EnergyOrbEffect(player.transform.position, vfxColor, projSize * 2f, 0.2f));

        for (int i = 0; i < projCount; i++)
        {
            float angleOffset = projCount > 1 ? -spreadAngle / 2f + spreadAngle * i / (projCount - 1) : 0f;
            Vector2 dir = Quaternion.Euler(0, 0, angleOffset) * baseDir;

            GameObject projObj = VFXPool.GetPlayerProjectile();
            projObj.transform.position = player.transform.position + (Vector3)(dir * 0.5f);
            projObj.layer = LayerMask.NameToLayer("Player");

            var sr = projObj.GetComponent<SpriteRenderer>();
            sr.sprite = VFXHelper.GetSharedWhiteSprite();
            sr.color = vfxColor;
            sr.sortingOrder = 8;
            projObj.transform.localScale = new Vector3(projSize, projSize, 1f);

            var rb2d = projObj.GetComponent<Rigidbody2D>();
            rb2d.gravityScale = 0f;
            rb2d.velocity = dir * speed;

            var col = projObj.GetComponent<CircleCollider2D>();
            col.radius = projSize * 0.7f;
            col.isTrigger = true;

            var proj = projObj.GetComponent<PlayerProjectile>();
            int dmg = Mathf.RoundToInt(player.Stats.BuffedAttack * dmgMult);
            proj.Initialize(dir, dmg, player);
            proj.SetColor(vfxColor);

            // Muzzle flash at projectile spawn
            VFXHelper.SpawnProjectileMuzzleFlash(projObj.transform.position, dir, vfxColor);

            // 符文效果
            if (skill.PierceCount > 0) proj.SetPierce(skill.PierceCount);
            if (skill.ChainCount > 0) proj.SetChain(skill.ChainCount);
            if (skill.Slot == SkillSlot.Skill1 && player.HeroClass == HeroClass.Mage) proj.SetExplodeRadius(range * 0.3f);
            if (skill.Slot == SkillSlot.Skill3) proj.SetExplodeRadius(1.5f);
            proj.SetRuneData(skill);

            // Evolution: 圣光审判 (piercing + explode)
            if (isEvolved && skill.EvolutionName == "圣光审判")
            {
                proj.SetPierce(10);
                proj.SetExplodeRadius(2f);
            }
            // Evolution: 虚空射线 (piercing through everything)
            if (isEvolved && skill.EvolutionName == "虚空射线")
            {
                proj.SetPierce(99);
                speed = 15f;
                rb2d.velocity = dir * speed;
            }

            // === 变异效果 ===
            int splitStacks = skill.GetMutationStacks(MutationPool.SplitFireball);
            if (splitStacks > 0)
                proj.SetSplit(splitStacks + 1);

            int frostStacks = skill.GetMutationStacks(MutationPool.FrostFire);
            if (frostStacks > 0)
                proj.SetFrostZone(2f + frostStacks);

            int chainStacks = skill.GetMutationStacks(MutationPool.ChainLightning);
            if (chainStacks > 0)
                proj.SetChain(chainStacks + 1);
        }

        // 投射物尾迹
        if (player.HeroClass == HeroClass.Mage) {} // 尾迹已优化
        else if (player.HeroClass == HeroClass.Priest) {} // 尾迹已优化
    }

    private void ExecuteAoeSkill(SkillData skill, float dmgMult, float range, Color vfxColor)
    {
        bool isEvolved = skill.CurrentLevel >= 5 && !string.IsNullOrEmpty(skill.EvolutionName);

        // 批量伤害计算
        int hitCount = Physics2D.OverlapCircleNonAlloc(player.transform.position, range, _overlapBuffer, LayerMask.GetMask("Enemy"));
        int totalDmg = 0;
        int aoeHitCount = 0;
        var aoeHitEnemies = new (EnemyController enemy, int dmg)[3];
        for (int i = 0; i < hitCount; i++)
        {
            EnemyController enemy = _overlapBuffer[i].GetComponent<EnemyController>();
            if (enemy != null && !enemy.IsDead)
            {
                bool crit;
                int dmg = Mathf.RoundToInt(CalculateDamageWithCrit(out crit) * dmgMult);
                enemy.TakeDamage(dmg, player);
                totalDmg += dmg;

                if (aoeHitCount < 3)
                {
                    aoeHitEnemies[aoeHitCount] = (enemy, dmg);
                    aoeHitCount++;
                }

                ApplyRuneEffectsToEnemy(skill, enemy);

                if (isEvolved && skill.EvolutionName == "暴风雪" && enemy.StatusFx != null)
                    enemy.StatusFx.ApplyEffect(StatusEffectManager.EffectType.Freeze, 2f, 0, 1f, 0.3f);
            }
        }

        // 延迟表现：前3个怪
        for (int i = 0; i < aoeHitCount; i++)
        {
            VFXHelper.SpawnHitParticles(aoeHitEnemies[i].Item1.transform.position, vfxColor, 1);
        }

        ApplyLifeSteal(totalDmg);
        ApplyRuneLifeSteal(skill, totalDmg);
        if (skill.HasBurn) SpawnBurnZones(skill, player.transform.position, range);

        try {
            VFXHelper.SpawnIceNova(player.transform.position, range);
            VFXHelper.SpawnShockwave(player.transform.position, vfxColor, range, 10f);
            CameraFollow.Shake(0.15f, 0.1f);
        } catch { }
    }

    private void ExecuteBuffSkill(SkillData skill)
    {
        bool isEvolved = skill.CurrentLevel >= 5 && !string.IsNullOrEmpty(skill.EvolutionName);
        VFXHelper.SpawnWarCryEffect(player.transform.position, 2f);
        VFXHelper.SpawnLevelUpEffect(player.transform.position);
        CameraFollow.Shake(0.05f, 0.06f);
        float buffAmount = isEvolved ? skill.EvolutionDamageMultiplier : skill.CurrentDamageMultiplier;
        player.Stats.BuffAttackMult = 1f + buffAmount;
        player.Stats.BuffDefenseMult = 1f + buffAmount * 0.5f;
        float buffDuration = isEvolved
            ? GameConfigManager.Instance?.GetGlobal("BuffDurationEvolved", 12f) ?? 12f
            : GameConfigManager.Instance?.GetGlobal("BuffDuration", 8f) ?? 8f;
        player.Stats.BuffEndTime = Time.time + buffDuration;

        Color buffColor = player.ClassData.SkillColor1;
        VFXHelper.SpawnBuffAura(player.transform, buffColor, buffDuration);

        // Evolution: 狂暴 adds attack speed
        if (isEvolved && skill.EvolutionName == "狂暴")
        {
            player.Stats.AttackSpeed += 0.2f;
            // Remove the attack speed buff when the buff expires (simplified: we set a timer)
            player.StartCoroutine(RemoveAttackSpeedBuff(buffDuration, 0.2f));
        }
    }

    private System.Collections.IEnumerator RemoveAttackSpeedBuff(float delay, float amount)
    {
        yield return new WaitForSeconds(delay);
        player.Stats.AttackSpeed -= amount;
    }

    private float _potionCooldown;
    private const float PotionCD = 2f;

    private void UsePotion()
    {
        if (_potionCooldown > 0f) return;
        var runtime = player.Stats.Runtime;
        if (runtime.Potions <= 0) return;
        if (player.Stats.Hp >= player.Stats.TotalMaxHp) return;

        runtime.Potions--;
        int healAmt = Mathf.RoundToInt(player.Stats.TotalMaxHp * 0.25f);
        player.Heal(healAmt);
        _potionCooldown = PotionCD;
        AudioManager.Instance?.PlayHeal();
        VFXHelper.SpawnDamageNumber(player.transform.position, healAmt, false, new Color(0.2f, 1f, 0.3f));
    }

    private void ExecuteHealSkill(SkillData skill)
    {
        bool isEvolved = skill.CurrentLevel >= 5 && !string.IsNullOrEmpty(skill.EvolutionName);
        float healPct = isEvolved ? skill.EvolutionDamageMultiplier : skill.CurrentDamageMultiplier;
        int healAmount = Mathf.RoundToInt(player.Stats.TotalMaxHp * healPct);
        player.Heal(healAmount);
        VFXHelper.SpawnHolyLightBeam(player.transform.position, 1.5f);
        CameraFollow.Shake(0.03f, 0.06f);
        AudioManager.Instance?.PlayHeal();

        // Evolution: 群体治疗 also grants shield
        if (isEvolved && skill.EvolutionName == "群体治疗")
        {
            int shieldAmt = Mathf.RoundToInt(player.Stats.TotalMaxHp * 0.2f);
            player.Stats.ShieldHp = shieldAmt;
            player.Stats.ShieldEndTime = Time.time + 3f;
            DestroyShieldVFX();
            _activeShieldVFX = VFXHelper.SpawnDivineShieldEffect(
                player.transform.position, 1.5f, false, player.transform, 3f);
        }

        // === 变异效果 ===
        ApplyHealMutations(skill, healAmount);
    }

    private void ExecuteShieldSkill(SkillData skill)
    {
        bool isEvolved = skill.CurrentLevel >= 5 && !string.IsNullOrEmpty(skill.EvolutionName);
        float shieldPct = isEvolved ? skill.EvolutionDamageMultiplier : skill.CurrentDamageMultiplier;
        int shieldAmount = Mathf.RoundToInt(player.Stats.TotalMaxHp * shieldPct);
        player.Stats.ShieldHp = shieldAmount;

        float duration = GameConfigManager.Instance?.GetGlobal("ShieldDuration", 5f) ?? 5f;
        if (isEvolved && skill.EvolutionName == "神圣之翼")
            duration = GameConfigManager.Instance?.GetGlobal("ShieldDurationEvolved", 7f) ?? 7f;

        player.Stats.ShieldEndTime = Time.time + duration;
        _shieldEvolved = isEvolved;

        // Destroy any previous shield VFX
        DestroyShieldVFX();

        // Spawn persistent shield that follows the player
        _activeShieldVFX = VFXHelper.SpawnDivineShieldEffect(
            player.transform.position, 1.5f, isEvolved, player.transform, duration);

        CameraFollow.Shake(0.05f, 0.06f);
        AudioManager.Instance?.PlayShield();
    }

    private void DestroyShieldVFX()
    {
        if (_activeShieldVFX != null)
        {
            Destroy(_activeShieldVFX.gameObject);
            _activeShieldVFX = null;
        }
    }

    /// <summary>对敌人施加符文效果（减速、燃烧、DOT等）</summary>
    // === Rune effects & mutations — moved to PlayerCombatController.Runes.cs (partial class) ===

    public EnemyController FindNearestEnemy(float range)
    {
        int hitCount = Physics2D.OverlapCircleNonAlloc(
            player.transform.position, range, _overlapBuffer, LayerMask.GetMask("Enemy"));
        EnemyController nearest = null;
        float nearestSqrDist = float.MaxValue;
        Vector2 playerPos = player.transform.position;
        for (int i = 0; i < hitCount; i++)
        {
            EnemyController enemy = _overlapBuffer[i].GetComponent<EnemyController>();
            if (enemy != null && !enemy.IsDead)
            {
                float sqrDist = (playerPos - (Vector2)enemy.transform.position).sqrMagnitude;
                if (sqrDist < nearestSqrDist)
                {
                    nearestSqrDist = sqrDist;
                    nearest = enemy;
                }
            }
        }
        return nearest;
    }

    private int CalculateDamageWithCrit(out bool isCrit)
    {
        // 使用Shared/CombatCalculator统一公式（客户端预测+服务器权威共用）
        var (damage, crit) = ArpgShared.CombatCalculator.CalculateCritDamage(
            player.Stats.BuffedAttack, player.Stats.TotalCritChance, critMult);
        isCrit = crit;
        return damage;
    }

    private void ApplyLifeSteal(int totalDamage)
    {
        if (player.Stats.TotalLifeSteal > 0 && totalDamage > 0)
        {
            int healAmt = Mathf.Max(1, Mathf.RoundToInt(totalDamage * player.Stats.TotalLifeSteal));
            player.Heal(healAmt);
        }
    }

    public void TakeDamage(int rawDamage)
    {
        if (player.IsDead) return;
        if (Time.time - lastDamageTime < invincibilityTime) return;
        lastDamageTime = Time.time;

        _prevShieldHp = player.Stats.ShieldHp;
        int actualDamage = player.Stats.TakeDamage(rawDamage);

        // Shield broke this hit
        if (_prevShieldHp > 0 && player.Stats.ShieldHp <= 0)
        {
            VFXHelper.SpawnShieldBreakEffect(player.transform.position, 1.5f, _shieldEvolved);
            DestroyShieldVFX();
            CameraFollow.Shake(0.1f, 0.06f);
        }
        else if (actualDamage == 0 && player.Stats.ShieldHp > 0)
        {
            VFXHelper.SpawnAreaPulse(player.transform.position, 0.8f, new Color(0.8f, 0.9f, 1f, 0.5f));
            return;
        }

        // Debug.Log removed for performance
        CameraFollow.Shake(0.15f, 0.1f);
        player.StartCoroutine(FlashRed());
        if (charSprite != null)
            charSprite.TriggerHurt(Vector2.up);

        // Relic: Thorns/DodgeHeal
        if (RelicManager.Instance != null)
        {
            // Find the attacker (nearest enemy) for Thorns
            EnemyController nearestEnemy = FindNearestEnemy(5f);
            RelicManager.Instance.OnPlayerHit(rawDamage, nearestEnemy);
        }

        if (player.Stats.Hp <= 0) player.Die();
    }

    private IEnumerator FlashRed()
    {
        SpriteRenderer[] parts = player.GetComponentsInChildren<SpriteRenderer>();
        Color[] origColors = new Color[parts.Length];
        for (int i = 0; i < parts.Length; i++) { origColors[i] = parts[i].color; if (parts[i] != player.MainSpriteRenderer) parts[i].color = new Color(0.8f, 0.4f, 0.4f); }
        yield return new WaitForSeconds(0.1f);
        for (int i = 0; i < parts.Length; i++) { if (parts[i] != player.MainSpriteRenderer) parts[i].color = origColors[i]; }
    }
}
