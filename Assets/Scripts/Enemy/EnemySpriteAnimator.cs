using UnityEngine;
using System.Collections;

/// <summary>
/// Procedural animation for enemy sprites — attack lunge, cast pullback,
/// slam jump, hit squash, idle bob. Applied to the EnemyVisual child transform.
/// </summary>
public class EnemySpriteAnimator : MonoBehaviour
{
    private Transform animRoot;
    private Vector3 basePos;
    private Vector3 baseScale;

    // Animation state
    private float animTimer;
    private float animDuration;
    private enum AnimState { None, MeleeLunge, RangedCast, SlamJump, HitSquash }
    private AnimState state = AnimState.None;
    private Vector2 attackDir;

    public void Init(Transform root)
    {
        animRoot = root;
        basePos = root.localPosition;
        baseScale = root.localScale;
    }

    public void PlayMeleeLunge(Vector2 dir)
    {
        state = AnimState.MeleeLunge;
        animTimer = 0f;
        animDuration = 0.2f;
        attackDir = dir;
    }

    public void PlayRangedCast(Vector2 dir)
    {
        state = AnimState.RangedCast;
        animTimer = 0f;
        animDuration = 0.25f;
        attackDir = dir;
    }

    public void PlaySlamJump()
    {
        state = AnimState.SlamJump;
        animTimer = 0f;
        animDuration = 0.4f;
    }

    public void PlayHitSquash()
    {
        state = AnimState.HitSquash;
        animTimer = 0f;
        animDuration = 0.12f;
    }

    private void Update()
    {
        if (animRoot == null || state == AnimState.None) return;

        animTimer += Time.deltaTime;
        float t = Mathf.Clamp01(animTimer / animDuration);

        switch (state)
        {
            case AnimState.MeleeLunge:
                {
                    // Lunge forward then recoil back
                    float lunge = Mathf.Sin(t * Mathf.PI) * 0.3f;
                    float squash = Mathf.Sin(t * Mathf.PI) * 0.15f;
                    animRoot.localPosition = basePos + new Vector3(attackDir.x * lunge, attackDir.y * lunge, 0f);
                    animRoot.localScale = new Vector3(
                        baseScale.x * (1f + squash * Mathf.Abs(attackDir.x)),
                        baseScale.y * (1f - squash * 0.5f + squash * 0.5f * Mathf.Abs(attackDir.y)),
                        baseScale.z);
                    if (t >= 1f) ResetAnim();
                }
                break;

            case AnimState.RangedCast:
                {
                    // Pull back then thrust forward
                    float pullback = t < 0.4f
                        ? -(t / 0.4f) * 0.2f
                        : Mathf.Lerp(-0.2f, 0.1f, (t - 0.4f) / 0.6f);
                    animRoot.localPosition = basePos + new Vector3(-attackDir.x * pullback, -attackDir.y * pullback, 0f);
                    float scalePulse = t < 0.4f ? 1f - (t / 0.4f) * 0.1f : 1f + Mathf.Sin((t - 0.4f) / 0.6f * Mathf.PI) * 0.08f;
                    animRoot.localScale = baseScale * scalePulse;
                    if (t >= 1f) ResetAnim();
                }
                break;

            case AnimState.SlamJump:
                {
                    // Jump up then crash down
                    float jump = t < 0.5f
                        ? Mathf.Sin((t / 0.5f) * Mathf.PI * 0.5f) * 0.5f
                        : Mathf.Lerp(0.5f, 0f, EaseInCubic((t - 0.5f) / 0.5f));
                    animRoot.localPosition = basePos + new Vector3(0f, jump, 0f);
                    if (t < 0.5f)
                    {
                        float stretch = (t / 0.5f) * 0.2f;
                        animRoot.localScale = new Vector3(baseScale.x * (1f - stretch * 0.3f), baseScale.y * (1f + stretch), baseScale.z);
                    }
                    else
                    {
                        float squash = (1f - (t - 0.5f) / 0.5f) * 0.25f;
                        animRoot.localScale = new Vector3(baseScale.x * (1f + squash), baseScale.y * (1f - squash * 0.6f), baseScale.z);
                    }
                    if (t >= 1f) ResetAnim();
                }
                break;

            case AnimState.HitSquash:
                {
                    // Quick squash + wobble
                    float squash = (1f - t) * 0.2f;
                    animRoot.localScale = new Vector3(
                        baseScale.x * (1f + squash),
                        baseScale.y * (1f - squash * 0.6f),
                        baseScale.z);
                    animRoot.localPosition = basePos + new Vector3(Mathf.Sin(t * 30f) * 0.03f * (1f - t), 0f, 0f);
                    if (t >= 1f) ResetAnim();
                }
                break;
        }
    }

    private void ResetAnim()
    {
        state = AnimState.None;
        if (animRoot != null)
        {
            animRoot.localPosition = basePos;
            animRoot.localScale = baseScale;
        }
    }

    private static float EaseInCubic(float t) => t * t * t;
}
