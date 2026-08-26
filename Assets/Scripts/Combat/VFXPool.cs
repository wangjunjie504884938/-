using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

/// <summary>
/// Object pool for VFX GameObjects. Replaces repeated new GameObject()/Destroy()
/// with activate/deactivate recycling to reduce GC pressure and allocation overhead.
/// </summary>
public static class VFXPool
{
    private static readonly Dictionary<string, Queue<GameObject>> _pools = new Dictionary<string, Queue<GameObject>>();
    private static readonly Dictionary<string, int> _poolCounts = new Dictionary<string, int>();
    private const int MaxPoolSize = 128;
    private static int _activeCount;

    /// <summary>
    /// Get a pooled GameObject with the required components, or create a new one.
    /// </summary>
    public static GameObject Get(string poolKey, System.Action<GameObject> configure)
    {
        _activeCount++;
        if (_pools.TryGetValue(poolKey, out var queue) && queue.Count > 0)
        {
            var obj = queue.Dequeue();
            if (obj != null)
            {
                if (_poolCounts.ContainsKey(poolKey)) _poolCounts[poolKey]--;
                // Reset transform and visual state to avoid stale VFX showing on screen
                obj.transform.localScale = Vector3.zero;
                obj.transform.rotation = Quaternion.identity;
                var sr = obj.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = new Color(1, 1, 1, 0); // alpha=0, 避免复用时闪现
                obj.SetActive(true);
                return obj;
            }
            // Object was destroyed — decrement count to prevent drift
            if (_poolCounts.ContainsKey(poolKey)) _poolCounts[poolKey]--;
        }

        var go = new GameObject(poolKey);
        configure(go);
        return go;
    }

    /// <summary>预热对象池 — 在场景加载时预创建对象避免首帧卡顿</summary>
    public static void Prewarm(string poolKey, int count, System.Action<GameObject> configure)
    {
        if (!_pools.ContainsKey(poolKey))
        {
            _pools[poolKey] = new Queue<GameObject>();
            _poolCounts[poolKey] = 0;
        }
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject(poolKey + "_prewarm");
            configure(go);
            go.SetActive(false);
            _pools[poolKey].Enqueue(go);
            _poolCounts[poolKey]++;
        }
    }

    /// <summary>
    /// Return a GameObject to the pool for reuse. Disables it instead of destroying.
    /// </summary>
    public static void Return(string poolKey, GameObject obj)
    {
        if (obj == null) return;
        if (_activeCount > 0) _activeCount--;

        // 重置视觉状态: 隐藏前清零alpha, 避免复用时残留颜色闪现
        var sr = obj.GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = new Color(0, 0, 0, 0);

        obj.SetActive(false);

        if (!_pools.TryGetValue(poolKey, out var queue))
        {
            queue = new Queue<GameObject>();
            _pools[poolKey] = queue;
            _poolCounts[poolKey] = 0;
        }

        if (_poolCounts.GetValueOrDefault(poolKey) < MaxPoolSize)
        {
            queue.Enqueue(obj);
            _poolCounts[poolKey]++;
        }
        else
        {
            Object.Destroy(obj);
        }
    }

    /// <summary>
    /// Configure a particle-style VFX object with SpriteRenderer + Rigidbody2D.
    /// Used by SpawnHitParticles, SpawnDeathExplosion, SpawnLevelUpEffect, etc.
    /// </summary>
    public static void ConfigureParticle(GameObject obj)
    {
        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = VFXHelper.GetSharedWhiteSprite();
        var rb = obj.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0.5f;
    }

    /// <summary>
    /// Get or create a particle VFX object from the pool.
    /// </summary>
    public static GameObject GetParticle()
    {
        return Get("particle", ConfigureParticle);
    }


    /// <summary>
    /// Get or create an area pulse VFX object from the pool.
    /// </summary>
    public static GameObject GetAreaPulse()
    {
        return Get("areapulse", o =>
        {
            var sr = o.AddComponent<SpriteRenderer>();
            sr.sprite = PetCompanion.GetCircleSpriteInternal();
            sr.sortingOrder = 4;
        });
    }

    /// <summary>
    /// Get or create a slash VFX object from the pool.
    /// </summary>
    public static GameObject GetSlash()
    {
        return Get("slash", o =>
        {
            var sr = o.AddComponent<SpriteRenderer>();
            sr.sprite = VFXHelper.GetSharedWhiteSprite();
            sr.sortingOrder = 5;
        });
    }

    /// <summary>
    /// Get or create a beam VFX object from the pool.
    /// </summary>
    public static GameObject GetBeam()
    {
        return Get("beam", o =>
        {
            var sr = o.AddComponent<SpriteRenderer>();
            sr.sprite = VFXHelper.GetSharedWhiteSprite();
            sr.sortingOrder = 6;
        });
    }

    /// <summary>
    /// Get or create a death flash VFX object from the pool.
    /// </summary>
    public static GameObject GetDeathFlash()
    {
        return Get("deathflash", o =>
        {
            var sr = o.AddComponent<SpriteRenderer>();
            sr.sprite = VFXHelper.GetSharedWhiteSprite();
            sr.sortingOrder = 7;
        });
    }

    /// <summary>
    /// <summary>
    /// Get or create a player projectile from the pool.
    /// Configured with SpriteRenderer, Rigidbody2D, CircleCollider2D, PlayerProjectile.
    /// </summary>
    public static GameObject GetPlayerProjectile()
    {
        return Get("player_proj", o =>
        {
            o.layer = LayerMask.NameToLayer("Player");
            var sr = o.AddComponent<SpriteRenderer>();
            sr.sprite = VFXHelper.GetSharedWhiteSprite();
            sr.sortingOrder = 8;
            var rb = o.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            var col = o.AddComponent<CircleCollider2D>();
            col.radius = 0.25f;
            col.isTrigger = true;
            o.AddComponent<PlayerProjectile>();
        });
    }

    /// <summary>
    /// Get or create an enemy projectile from the pool.
    /// Configured with SpriteRenderer, Rigidbody2D, CircleCollider2D, EnemyProjectile.
    /// </summary>
    public static GameObject GetEnemyProjectile()
    {
        return Get("enemy_proj", o =>
        {
            o.layer = 0;
            var sr = o.AddComponent<SpriteRenderer>();
            sr.sprite = VFXHelper.GetSharedWhiteSprite();
            sr.sortingOrder = 6;
            var rb = o.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            var col = o.AddComponent<CircleCollider2D>();
            col.radius = 0.4f;
            col.isTrigger = true;
            o.AddComponent<EnemyProjectile>();
        });
    }

    /// <summary>
    /// Clean up all pooled objects AND any active VFX still in the scene (e.g., on scene load).
    /// </summary>
    public static void ClearAll()
    {
        _activeCount = 0;
        foreach (var queue in _pools.Values)
        {
            while (queue.Count > 0)
            {
                var obj = queue.Dequeue();
                if (obj != null) Object.Destroy(obj);
            }
        }
        _pools.Clear();
        _poolCounts.Clear();

        // Clean up active projectiles via SceneRegistry (much cheaper than FindObjectsOfType)
        SceneRegistry.ClearProjectilesAndLoot();
    }

    /// <summary>预热所有特效池 — 在副本开始时调用</summary>
    public static void PrewarmAll()
    {
        Prewarm("particle", 10, ConfigureParticle);
        Prewarm("dmgnum", 8, o =>
        {
            var sr = o.AddComponent<SpriteRenderer>();
            sr.sprite = VFXHelper.GetSharedWhiteSprite();
            sr.sortingOrder = 10;
        });
        Prewarm("slash", 4, o =>
        {
            var sr = o.AddComponent<SpriteRenderer>();
            sr.sprite = VFXHelper.GetSharedWhiteSprite();
            sr.sortingOrder = 5;
        });
        Prewarm("deathflash", 4, o =>
        {
            var sr = o.AddComponent<SpriteRenderer>();
            sr.sprite = VFXHelper.GetSharedWhiteSprite();
            sr.sortingOrder = 7;
        });
        Prewarm("areapulse", 3, o =>
        {
            var sr = o.AddComponent<SpriteRenderer>();
            sr.sprite = PetCompanion.GetCircleSpriteInternal();
            sr.sortingOrder = 4;
        });
    }
}
