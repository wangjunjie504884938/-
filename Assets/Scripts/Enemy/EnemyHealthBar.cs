using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Floating health bar above enemy. Auto-generated at runtime, no prefab needed.
/// Color shifts from green → yellow based on HP percentage.
/// </summary>
public class EnemyHealthBar : MonoBehaviour
{
    private static readonly HashSet<EnemyHealthBar> _activeBars = new HashSet<EnemyHealthBar>();

    /// <summary>清理所有残留的血条（场景切换/重启时调用）</summary>
    public static void CleanupAll()
    {
        if (_activeBars.Count == 0) return;
        var buffer = new EnemyHealthBar[_activeBars.Count];
        _activeBars.CopyTo(buffer);
        _activeBars.Clear();
        foreach (var bar in buffer)
            if (bar != null) bar.Cleanup();
    }

    private GameObject barObj;
    private SpriteRenderer bgRenderer;
    private SpriteRenderer fillRenderer;
    private Transform target;
    private float maxHp;
    private float currentHp;
    private float barWidth = 1.0f;
    private float barHeight = 0.10f;
    private float yOffset = 1.0f;

    private bool _destroyed;

    public void Initialize(Transform enemy, int hp)
    {
        target = enemy;
        maxHp = hp;
        currentHp = hp;
        _activeBars.Add(this);

        // 血条挂到敌人身上，敌人回收时自动销毁
        barObj = new GameObject("HPBar");
        barObj.transform.SetParent(enemy, false);
        barObj.transform.localPosition = new Vector3(0f, yOffset, 0f);
        bgRenderer = barObj.AddComponent<SpriteRenderer>();
        bgRenderer.sprite = SpriteCache.WhitePixel;
        bgRenderer.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);
        bgRenderer.sortingOrder = 15;
        barObj.transform.localScale = new Vector3(barWidth + 0.04f, barHeight + 0.04f, 1f);

        // Fill bar
        GameObject fillObj = new GameObject("HPFill");
        fillObj.transform.SetParent(barObj.transform, false);
        fillObj.transform.localPosition = Vector3.zero;
        fillRenderer = fillObj.AddComponent<SpriteRenderer>();
        fillRenderer.sprite = SpriteCache.WhitePixel;
        fillRenderer.color = Color.green;
        fillRenderer.sortingOrder = 16;
        fillObj.transform.localScale = new Vector3(barWidth / (barWidth + 0.04f), barHeight / (barHeight + 0.04f), 1f);
    }

    public void UpdateHp(int hp)
    {
        currentHp = hp;
        float pct = maxHp > 0 ? (float)currentHp / maxHp : 0f;

        // Hide bar when at full HP
        if (barObj != null)
            barObj.SetActive(pct < 0.99f);

        // Scale the fill
        if (fillRenderer != null)
        {
            float parentWidth = barWidth + 0.04f;
            float parentHeight = barHeight + 0.04f;
            fillRenderer.transform.localScale = new Vector3(
                (barWidth * pct) / parentWidth,
                barHeight / parentHeight,
                1f
            );

            // Color: green → yellow (不再变红)
            if (pct > 0.5f)
                fillRenderer.color = Color.Lerp(Color.yellow, Color.green, (pct - 0.5f) * 2f);
            else
                fillRenderer.color = Color.Lerp(new Color(0.8f, 0.6f, 0.1f), Color.yellow, pct * 2f);

            // Offset fill to left-align
            float offset = (barWidth * (pct - 1f)) * 0.5f / parentWidth;
            fillRenderer.transform.localPosition = new Vector3(offset, 0f, 0f);
        }
    }

    private void LateUpdate()
    {
        if (_destroyed) return;
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            Cleanup();
            return;
        }
        // barObj是敌人的子物体，位置自动跟随，无需手动更新
    }

    private void OnDestroy()
    {
        Cleanup();
    }

    public void Cleanup()
    {
        if (_destroyed) return;
        _destroyed = true;
        _activeBars.Remove(this);
        if (barObj != null)
        {
            barObj.SetActive(false);
            Destroy(barObj);
            barObj = null;
        }
        bgRenderer = null;
        fillRenderer = null;
    }
}
