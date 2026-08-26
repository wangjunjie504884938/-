using UnityEngine;
using System.Collections;

/// <summary>
/// Character animation driven by Unity Animator.
/// Uses trimmed sprite sheets (6 frames: Idle/Run1/Run2/Attack/Hurt/Dead) per class.
/// Procedural weapon swing, dash, hurt, death effects preserved on top of animator.
/// </summary>
public class CharacterSprite : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private HeroClass heroClass;

    // Attack
    private float attackTimer;
    private const float AttackDuration = 0.3f;
    private bool isAttacking;
    public bool IsAttacking => isAttacking;

    private int comboIndex;
    private float lastAttackEndTime;
    private const float ComboResetTime = 1f;
    public int ComboIndex => comboIndex;

    // Skill
    private float skillTimer;
    private const float SkillDuration = 0.4f;
    private bool isSkillAttacking;
    public bool IsSkillAttacking => isSkillAttacking;

    // Weapon swing visual
    private GameObject weaponSwingObj;
    private SpriteRenderer weaponSwingSr;
    private Vector2 attackDirection;

    // Hurt
    private float hurtTimer;
    private const float HurtDuration = 0.25f;
    private bool isHurting;
    private Vector2 hurtDirection;

    // Death
    private bool isDying;
    public bool IsDying => isDying;

    // Dash
    private float dashTimer;
    private const float DashDuration = 0.15f;
    private bool isDashing;
    private Vector2 dashDirection;
    public bool IsDashingAnim => isDashing;

    // Animator parameter IDs (cached)
    private static readonly int ParamIsMoving = Animator.StringToHash("IsMoving");
    private static readonly int ParamAttack = Animator.StringToHash("Attack");
    private static readonly int ParamHurt = Animator.StringToHash("Hurt");
    private static readonly int ParamDie = Animator.StringToHash("Die");
    private static readonly int ParamIsDashing = Animator.StringToHash("IsDashing");

    public void Setup(Transform parent, HeroClass heroClass)
    {
        this.heroClass = heroClass;

        // Clean up previous setup
        foreach (Transform child in parent)
        {
            if (child.name == "CharSprite" || child.name == "WeaponSwing" || child.name == "Char3D")
                Destroy(child.gameObject);
        }

        foreach (var sr in parent.GetComponentsInChildren<SpriteRenderer>())
            sr.enabled = true;

        var srObj = new GameObject("CharSprite");
        srObj.transform.SetParent(parent, false);
        srObj.transform.localPosition = Vector3.zero;
        spriteRenderer = srObj.AddComponent<SpriteRenderer>();
        spriteRenderer.sortingOrder = 5;

        animator = srObj.AddComponent<Animator>();
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        LoadAnimatorController();

        // Trimmed sprites at PPU=1000; scale 3.0 gives ~1.9 world units (visible against ortho-10 camera)
        parent.localScale = new Vector3(3f, 3f, 1f);

        CreateWeaponSwing(parent);
    }

    private void LoadAnimatorController()
    {
        string controllerName = heroClass switch
        {
            HeroClass.Warrior => "WarriorAnimator",
            HeroClass.Mage => "MageAnimator",
            _ => "PriestAnimator"
        };
        var controller = Resources.Load<RuntimeAnimatorController>(ResourcePaths.CharacterBase + controllerName);
        if (controller != null)
            animator.runtimeAnimatorController = controller;
        else
            GameLog.LogError($"[CharacterSprite] Failed to load animator controller: {controllerName}");
    }

    private void CreateWeaponSwing(Transform parent)
    {
        weaponSwingObj = new GameObject("WeaponSwing");
        weaponSwingObj.transform.SetParent(parent, false);
        weaponSwingObj.transform.localPosition = Vector3.zero;
        weaponSwingObj.SetActive(false);

        string className = heroClass switch
        {
            HeroClass.Warrior => "Warrior",
            HeroClass.Mage => "Mage",
            _ => "Priest"
        };
        Sprite arcSprite = Resources.Load<Sprite>(ResourcePaths.CharacterBase + className + "_WeaponArc");
        if (arcSprite == null) arcSprite = SpriteCache.WhitePixel;

        weaponSwingSr = weaponSwingObj.AddComponent<SpriteRenderer>();
        weaponSwingSr.sprite = arcSprite;
        weaponSwingSr.sortingOrder = 10;
        // Scale up weapon arc to match new character scale (old 32px sprites → 3.0x parent)
        weaponSwingObj.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
    }

    /// <summary>Set facing from direction vector. Returns true if horizontal (flipX applies).</summary>
    private void ApplyFacing(Vector2 dir)
    {
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            spriteRenderer.flipX = dir.x < 0;
        // Vertical facing: no flip (character faces forward in idle/run/attack sprites)
    }

    public bool IsFacingLeft => spriteRenderer != null && spriteRenderer.flipX;

    public void SetFacing(bool flipX)
    {
        if (isAttacking || isSkillAttacking) return;
        if (spriteRenderer != null) spriteRenderer.flipX = flipX;
    }

    public void SetFacing(Vector2 dir)
    {
        if (isAttacking || isSkillAttacking) return;
        if (dir.sqrMagnitude < 0.01f) return;
        ApplyFacing(dir);
    }

    public void SetDirection(Vector2 moveInput)
    {
        if (isAttacking) return;
        if (moveInput.sqrMagnitude < 0.01f) return;
        ApplyFacing(moveInput);
    }

    public void TriggerAttack(Vector2 direction)
    {
        isAttacking = true;
        attackTimer = 0f;
        attackDirection = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector2.right;
        ApplyFacing(direction);

        if (Time.time - lastAttackEndTime < ComboResetTime)
            comboIndex = (comboIndex + 1) % 3;
        else
            comboIndex = 0;

        if (animator != null) animator.CrossFade(ParamAttack, 0.05f);
        ShowWeaponSwing();
    }

    public void TriggerSkill(Vector2 direction)
    {
        if (isAttacking || isSkillAttacking) return;
        isSkillAttacking = true;
        skillTimer = 0f;
        attackDirection = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector2.right;
        ApplyFacing(direction);
        ShowWeaponSwing();
    }

    public void TriggerSkillCast(Vector2 direction) => TriggerSkill(direction);
    public void TriggerMeleeSkill(Vector2 direction) => TriggerSkill(direction);

    public void TriggerHurt(Vector2 direction)
    {
        if (isDying) return;
        isHurting = true;
        hurtTimer = 0f;
        hurtDirection = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector2.up;
        if (animator != null) animator.CrossFade(ParamHurt, 0.05f);
    }

    public void TriggerDash(Vector2 direction)
    {
        if (isDying) return;
        isDashing = true;
        dashTimer = 0f;
        dashDirection = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector2.right;
        ApplyFacing(direction);
        if (animator != null) animator.SetBool(ParamIsDashing, true);
    }

    public void TriggerCritStagger(Vector2 direction)
    {
        if (isDying) return;
        isHurting = true;
        hurtTimer = 0f;
        hurtDirection = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector2.up;
        if (animator != null) animator.CrossFade(ParamHurt, 0.05f);
    }

    public void TriggerFlash() { }

    public void PlayDeath()
    {
        if (isDying) return;
        isDying = true;
        isDashing = false;
        dashTimer = 0f;
        if (spriteRenderer != null)
            spriteRenderer.transform.localScale = Vector3.one;
        if (animator != null) animator.CrossFade(ParamDie, 0.05f);
        StartCoroutine(DeathAnimation());
    }

    /// <summary>Reset death visual state for retry/respawn</summary>
    public void ResetDeath()
    {
        isDying = false;
        if (spriteRenderer != null)
        {
            spriteRenderer.transform.localRotation = Quaternion.identity;
            spriteRenderer.transform.localPosition = Vector3.zero;
            spriteRenderer.transform.localScale = Vector3.one;
        }
        // 主SpriteRenderer（在Player GameObject上的占位）保持Color.clear
        SpriteRenderer mainSr = GetComponent<SpriteRenderer>();
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
        {
            if (sr == null || sr == mainSr) continue; // 跳过主占位SR
            Color c = sr.color;
            c.a = 1f;
            sr.color = c;
        }
    }

    /// <summary>场景切换后重新初始化 — 完全重建CharSprite子物体</summary>
    public void ResetCharacterSprite()
    {
        // 完全重新Setup，确保Animator和SpriteRenderer都正确
        Setup(transform, heroClass);

        // 强制播放Idle
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.Play("Idle", 0, 0f);
            animator.Update(0f);
        }

        // 主占位SR保持透明
        SpriteRenderer mainSr = GetComponent<SpriteRenderer>();
        if (mainSr != null)
            mainSr.color = Color.clear;

        // 重置动画状态
        isAttacking = false;
        isSkillAttacking = false;
        isHurting = false;
        isDying = false;
        isDashing = false;
        attackTimer = 0f;
        skillTimer = 0f;
        hurtTimer = 0f;
        dashTimer = 0f;
        comboIndex = 0;

        GameLog.Log($"[CharacterSprite] ResetCharacterSprite done. SR.sprite={spriteRenderer?.sprite} animator={animator != null} controller={animator?.runtimeAnimatorController?.name}");
    }

    private void ShowWeaponSwing()
    {
        if (weaponSwingObj == null) return;
        weaponSwingObj.SetActive(true);
        if (weaponSwingSr != null)
        {
            Color c = weaponSwingSr.color;
            c.a = 1f;
            weaponSwingSr.color = c;
        }
    }

    private IEnumerator DeathAnimation()
    {
        // Animator plays Dead clip (lying-down sprite); coroutine only adds fade + sink
        float duration = 0.6f;
        float timer = 0f;
        var renderers = GetComponentsInChildren<SpriteRenderer>();

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);
            foreach (var sr in renderers)
            {
                if (sr == null) continue;
                Color c = sr.color;
                c.a = 1f - t;
                sr.color = c;
            }
            Vector3 pos = spriteRenderer.transform.localPosition;
            pos.y = -t * 0.2f;
            spriteRenderer.transform.localPosition = pos;
            yield return null;
        }
    }

    public void UpdateAnimation(float moveMagnitude)
    {
        bool isMoving = moveMagnitude > 0.1f;

        if (animator != null && !isDying)
            animator.SetBool(ParamIsMoving, isMoving && !isAttacking && !isSkillAttacking && !isDashing);

        // Attack timer + weapon swing
        if (isAttacking)
        {
            attackTimer += Time.deltaTime;
            float t = attackTimer / AttackDuration;

            if (weaponSwingObj != null && weaponSwingObj.activeSelf)
            {
                float swingAngle = Mathf.Lerp(-60f, 60f, t);
                float scaleT = t < 0.3f ? t / 0.3f : 1f - (t - 0.3f) / 0.7f;
                float swingScale = Mathf.Lerp(0.8f, 1.8f, scaleT);
                float baseAngle = Mathf.Atan2(attackDirection.y, attackDirection.x) * Mathf.Rad2Deg;
                weaponSwingObj.transform.localEulerAngles = new Vector3(0f, 0f, baseAngle + swingAngle);
                weaponSwingObj.transform.localScale = new Vector3(swingScale, swingScale, 1f);
                if (weaponSwingSr != null)
                {
                    Color c = weaponSwingSr.color;
                    c.a = Mathf.Clamp01(t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f);
                    weaponSwingSr.color = c;
                }
            }

            if (attackTimer >= AttackDuration)
            {
                isAttacking = false;
                attackTimer = 0f;
                lastAttackEndTime = Time.time;
                if (weaponSwingObj != null) weaponSwingObj.SetActive(false);
            }
        }

        // Skill timer + weapon spin
        if (isSkillAttacking)
        {
            skillTimer += Time.deltaTime;
            float t = skillTimer / SkillDuration;

            if (weaponSwingObj != null && weaponSwingObj.activeSelf)
            {
                float spinAngle = t * 720f;
                float scaleT = t < 0.2f ? t / 0.2f : 1f - (t - 0.2f) / 0.8f;
                float skillScale = Mathf.Lerp(1.2f, 2.4f, scaleT);
                weaponSwingObj.transform.localEulerAngles = new Vector3(0f, 0f, spinAngle);
                weaponSwingObj.transform.localScale = new Vector3(skillScale, skillScale, 1f);
                if (weaponSwingSr != null)
                {
                    Color c = weaponSwingSr.color;
                    c.a = Mathf.Clamp01(t < 0.8f ? 1f : 1f - (t - 0.8f) / 0.2f);
                    weaponSwingSr.color = c;
                }
            }

            if (skillTimer >= SkillDuration)
            {
                isSkillAttacking = false;
                skillTimer = 0f;
                if (weaponSwingObj != null) weaponSwingObj.SetActive(false);
            }
        }

        // Procedural offsets on top of animator-driven sprite
        if (!isDying)
        {
            Vector3 offset = Vector3.zero;
            float rot = 0f;

            if (isHurting)
            {
                hurtTimer += Time.deltaTime;
                float t = hurtTimer / HurtDuration;
                float recoil = (1f - t) * 0.25f;
                offset = new Vector3(-hurtDirection.x * recoil, -hurtDirection.y * recoil, 0f);
                float shake = (1f - t) * 0.04f;
                offset += new Vector3(Random.Range(-shake, shake), Random.Range(-shake, shake), 0f);
                rot = (1f - t) * 5f * Mathf.Sin(t * 40f);
                if (hurtTimer >= HurtDuration)
                {
                    isHurting = false;
                    hurtTimer = 0f;
                }
            }
            else if (isDashing)
            {
                dashTimer += Time.deltaTime;
                float t = dashTimer / DashDuration;
                float lean = Mathf.Sin(t * Mathf.PI) * 0.3f;
                offset = new Vector3(dashDirection.x * lean, dashDirection.y * lean, 0f);
                rot = dashDirection.x * Mathf.Sin(t * Mathf.PI) * 15f;
                float squash = 1f + Mathf.Sin(t * Mathf.PI) * 0.15f;
                spriteRenderer.transform.localScale = new Vector3(squash, 2f / squash, 1f);
                if (dashTimer >= DashDuration)
                {
                    isDashing = false;
                    dashTimer = 0f;
                    spriteRenderer.transform.localScale = Vector3.one;
                    if (animator != null) animator.SetBool(ParamIsDashing, false);
                }
            }
            else if (isAttacking)
            {
                float t = attackTimer / AttackDuration;
                float lunge = Mathf.Sin(t * Mathf.PI) * 0.1f;
                offset = new Vector3(attackDirection.x * lunge, attackDirection.y * lunge, 0f);
            }
            else if (isSkillAttacking)
            {
                float t = skillTimer / SkillDuration;
                float lunge = Mathf.Sin(t * Mathf.PI) * 0.15f;
                offset = new Vector3(attackDirection.x * lunge, attackDirection.y * lunge, 0f);
            }
            else if (isMoving)
            {
                offset.y = Mathf.Sin(Time.time * 10f) * 0.04f;
                offset.x = Mathf.Sin(Time.time * 5f) * 0.012f;
                rot = Mathf.Sin(Time.time * 10f) * 1.5f;
            }
            else
            {
                offset.y = Mathf.Sin(Time.time * 2f) * 0.015f;
                rot = Mathf.Sin(Time.time * 2f) * 0.5f;
            }

            spriteRenderer.transform.localPosition = offset;
            spriteRenderer.transform.localEulerAngles = new Vector3(0f, 0f, rot);
        }
    }
}
