using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Runtime visual effects utility. All effects are generated without prefabs.
/// Provides class-specific and generic VFX for attacks, skills, and events.
/// </summary>
public static class VFXHelper
{
    /// <summary>
    /// Public accessor for the shared white sprite (used by VFXPool).
    /// </summary>
    public static Sprite GetSharedWhiteSprite() => SpriteCache.WhitePixel;

    // ===================================================================
    // EXISTING EFFECTS (kept for backward compatibility)
    // ===================================================================

    private static bool TryGetRunner(GameObject obj, out VFXRunner runner)
    {
        runner = obj.GetComponent<VFXRunner>();
        if (runner == null) runner = obj.AddComponent<VFXRunner>();
        return runner != null;
    }

    /// <summary>在独立VFXManager上启动协程，不受敌人回收影响</summary>
    private static void RunCoroutine(System.Collections.IEnumerator routine)
    {
        VFXManager.Instance.StartCoroutine(routine);
    }

    public static void SpawnLevelUpEffect(Vector3 position)
    {
        SpawnAreaPulse(position, 2f, new Color(1f, 0.9f, 0.2f, 0.7f));
        for (int i = 0; i < 8; i++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            float speed = Random.Range(2f, 5f);
            float lifetime = Random.Range(0.4f, 0.8f);
            GameObject p = VFXPool.GetParticle();
            if (p == null) continue;
            p.transform.position = position;
            p.transform.localScale = new Vector3(0.15f, 0.15f, 1f);
            var sr = p.GetComponent<SpriteRenderer>();
            sr.color = new Color(1f, 0.85f, 0.1f);
            sr.sortingOrder = 8;
            var rb = p.GetComponent<Rigidbody2D>();
            rb.gravityScale = 0.3f;
            rb.velocity = dir * speed;
            
            
            
            RunCoroutine(ParticleAnim(p, sr, lifetime));
        }
    }

    public static void SpawnHealEffect(Vector3 position, int amount)
    {
        for (int i = 0; i < 5; i++)
        {
            Vector2 dir = new Vector2(Random.Range(-1f, 1f), Random.Range(1.5f, 3f));
            float lifetime = Random.Range(0.3f, 0.6f);
            GameObject p = VFXPool.GetParticle();
            p.transform.position = position + new Vector3(Random.Range(-0.3f, 0.3f), 0f, 0f);
            p.transform.localScale = new Vector3(0.1f, 0.1f, 1f);
            var sr = p.GetComponent<SpriteRenderer>();
            sr.color = new Color(0.2f, 1f, 0.3f);
            sr.sortingOrder = 8;
            var rb = p.GetComponent<Rigidbody2D>();
            rb.gravityScale = -0.5f;
            rb.velocity = dir;
            
            
            
            RunCoroutine(ParticleAnim(p, sr, lifetime));
        }
        SpawnDamageNumber(position, amount, false, new Color(0.2f, 1f, 0.3f));
    }

    public static void SpawnSlashEffect(Vector3 position, Vector2 direction, float range = 1.5f, Color? color = null)
    {
        Color c = color ?? new Color(0.9f, 0.9f, 0.9f, 0.6f);
        // VFX范围限制：不超过2f，避免太大
        SpawnAreaPulse(position, Mathf.Min(range * 0.3f, 1.5f), c);
    }

    private static float _lastShakeTime;
    private const float MinShakeInterval = 0.08f;

    public static void SpawnDamageNumber(Vector3 position, int damage, bool isCrit = false, Color? customColor = null)
    {
        return; // 伤害数字已关闭
    }

    public static void SpawnAreaPulse(Vector3 position, float radius, Color? color = null)
    {
        Color c = color ?? new Color(0.5f, 0.8f, 1f, 0.6f);

        GameObject obj = VFXPool.GetAreaPulse();
        if (obj == null) return;
        obj.transform.position = position;

        var sr = obj.GetComponent<SpriteRenderer>();
        sr.color = c;

        obj.transform.localScale = new Vector3(0.1f, 0.1f, 1f);

        
        
            RunCoroutine(AreaPulseAnim(obj, sr, radius));
    }

    public static void SpawnHitParticles(Vector3 position, Color color, int count = 5)
    {
        count = Mathf.Min(count, 3); // 限制粒子数量减少卡顿
        for (int i = 0; i < count; i++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            float speed = Random.Range(1.5f, 4f);
            float lifetime = Random.Range(0.15f, 0.35f);

            GameObject p = VFXPool.GetParticle();
            if (p == null) continue;
            p.transform.position = position;
            p.transform.localScale = new Vector3(0.12f, 0.12f, 1f);

            var sr = p.GetComponent<SpriteRenderer>();
            sr.color = color;
            sr.sortingOrder = 6;

            var rb = p.GetComponent<Rigidbody2D>();
            rb.gravityScale = 0.5f;
            rb.velocity = dir * speed;

            
            RunCoroutine(ParticleAnim(p, sr, lifetime));
        }
    }

    public static void SpawnDeathExplosion(Vector3 position, Color color, int count = 8)
    {
        // 闪光
        GameObject flash = VFXPool.GetDeathFlash();
        if (flash == null) return;
        flash.transform.position = position;
        var flashSr = flash.GetComponent<SpriteRenderer>();
        flashSr.color = new Color(0.8f, 0.8f, 0.8f, 0.4f);
        flash.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
        var flashRunner = flash.GetComponent<VFXRunner>();
        if (flashRunner == null) flashRunner = flash.AddComponent<VFXRunner>();
        flashRunner.StartCoroutine(DeathFlashAnim(flash, flashSr));

        // 少量粒子，不向上飞，就地消失
        for (int i = 0; i < count; i++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            float speed = Random.Range(1f, 3f);
            float lifetime = Random.Range(0.2f, 0.4f);
            float size = Random.Range(0.08f, 0.16f);

            GameObject p = VFXPool.GetParticle();
            p.transform.position = position + (Vector3)(Random.insideUnitCircle * 0.2f);
            p.transform.localScale = new Vector3(size, size, 1f);

            var sr = p.GetComponent<SpriteRenderer>();
            sr.color = new Color(0.7f, 0.7f, 0.7f, 0.6f);
            sr.sortingOrder = 5;

            var rb = p.GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.velocity = dir * speed;

            RunCoroutine(ParticleAnim(p, sr, lifetime));
        }
    }

    // ===================================================================
    // NEW: CLASS-SPECIFIC ATTACK EFFECTS
    // ===================================================================

    /// <summary>
    /// Warrior melee slash with multiple arc trails and sparks.
    /// </summary>
    public static void SpawnWarriorSlash(Vector3 position, Vector2 direction, float range)
    {
        // 简洁冲击波，不再生成大量火花粒子
        SpawnAreaPulse(position, Mathf.Min(range * 0.3f, 1f), new Color(0.9f, 0.8f, 0.6f, 0.5f));
    }

    /// <summary>战士斩击火花 — 白→黄→橙→红渐变，方向散射+重力下落+光晕</summary>
    private static void SpawnWarriorSparks(Vector3 position, Vector2 slashDir, int count)
    {
        // 光晕底层
        GameObject glow = VFXPool.GetParticle();
        glow.transform.position = position;
        glow.transform.localScale = new Vector3(0.4f, 0.4f, 1f);
        var glowSr = glow.GetComponent<SpriteRenderer>();
        glowSr.sprite = SpriteCache.WhitePixel;
        glowSr.color = new Color(1f, 0.6f, 0.2f, 0.2f);
        glowSr.sortingOrder = 3;
        if (!TryGetRunner(glow, out var glowRunner)) return;
        glowRunner.StartCoroutine(SparkGlowAnim(glow, glowSr, 0.3f));

        // 火花粒子
        for (int i = 0; i < count; i++)
        {
            float spreadAngle = Random.Range(-60f, 60f);
            float rad = Mathf.Atan2(slashDir.y, slashDir.x) + spreadAngle * Mathf.Deg2Rad;
            Vector2 sparkDir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            float speed = Random.Range(2f, 5f);
            float lifetime = Random.Range(0.25f, 0.45f);
            float startSize = Random.Range(0.06f, 0.14f);

            GameObject p = VFXPool.GetParticle();
            if (p == null) continue;
            p.transform.position = position + (Vector3)(Random.insideUnitCircle * 0.1f);
            p.transform.localScale = new Vector3(startSize, startSize, 1f);

            var sr = p.GetComponent<SpriteRenderer>();
            sr.sprite = SpriteCache.WhitePixel;
            sr.color = Color.white;
            sr.sortingOrder = 8;

            var rb = p.GetComponent<Rigidbody2D>();
            rb.gravityScale = 2f;
            rb.velocity = sparkDir * speed;

            if (!TryGetRunner(p, out var runner)) continue;
            runner.StartCoroutine(SparkParticleAnim(p, sr, lifetime, startSize));
        }
    }

    private static IEnumerator SparkParticleAnim(GameObject obj, SpriteRenderer sr, float lifetime, float startSize)
    {
        float t = 0f;
        while (t < lifetime)
        {
            t += Time.deltaTime;
            float progress = t / lifetime;

            // 大小：先增大再缩小（抛物线）
            float sizeCurve = Mathf.Sin(progress * Mathf.PI); // 0→1→0
            float currentSize = startSize * (0.5f + sizeCurve * 0.8f);
            obj.transform.localScale = new Vector3(currentSize, currentSize, 1f);

            // 颜色渐变：白→黄→橙→红
            Color c;
            if (progress < 0.25f)
                c = Color.Lerp(Color.white, new Color(1f, 0.95f, 0.3f), progress / 0.25f);
            else if (progress < 0.55f)
                c = Color.Lerp(new Color(1f, 0.95f, 0.3f), new Color(1f, 0.6f, 0.15f), (progress - 0.25f) / 0.3f);
            else
                c = Color.Lerp(new Color(1f, 0.6f, 0.15f), new Color(0.9f, 0.15f, 0.05f), (progress - 0.55f) / 0.45f);
            c.a = (1f - progress * 0.7f);
            sr.color = c;

            // 减速
            var rb = obj.GetComponent<Rigidbody2D>();
            if (rb != null) rb.velocity *= 0.92f;

            yield return null;
        }
        VFXPool.Return("particle", obj);
    }

    private static IEnumerator SparkGlowAnim(GameObject obj, SpriteRenderer sr, float lifetime)
    {
        float t = 0f;
        float startScale = obj.transform.localScale.x;
        while (t < lifetime)
        {
            t += Time.deltaTime;
            float progress = t / lifetime;
            float s = startScale * (1f + progress * 1.5f);
            obj.transform.localScale = new Vector3(s, s, 1f);
            Color c = sr.color;
            c.a = 0.2f * (1f - progress);
            sr.color = c;
            yield return null;
        }
        VFXPool.Return("particle", obj);
    }

    /// <summary>
    /// Mage ice nova - expanding ring with crystal shards.
    /// </summary>
    public static void SpawnIceNova(Vector3 position, float radius)
    {
        Color iceColor = new Color(0.5f, 0.8f, 1f, 0.8f);
        Color crystalColor = new Color(0.7f, 0.95f, 1f);

        // Expanding ring
        SpawnAreaPulse(position, radius, iceColor);

        // Crystal shards flying outward
        int shardCount = 10;
        for (int i = 0; i < shardCount; i++)
        {
            float angle = (360f / shardCount) * i + Random.Range(-10f, 10f);
            float rad = angle * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            GameObject shard = VFXPool.GetParticle();
            shard.transform.position = position;
            shard.transform.rotation = Quaternion.Euler(0, 0, angle);
            shard.transform.localScale = new Vector3(0.35f, 0.1f, 1f);

            var sr = shard.GetComponent<SpriteRenderer>();
            sr.sprite = SpriteCache.WhitePixel;
            sr.color = crystalColor;
            sr.sortingOrder = 6;

            var rb = shard.GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.velocity = dir * Random.Range(3f, 6f);

            
            
            
            var runner = shard.GetComponent<VFXRunner>(); if (runner == null) runner = shard.AddComponent<VFXRunner>(); runner.StartCoroutine(ParticleAnim(shard, sr, Random.Range(0.3f, 0.6f)));
        }

        // Frost mist at center
        for (int i = 0; i < 6; i++)
        {
            Vector2 mistPos = (Vector2)position + Random.insideUnitCircle * radius * 0.5f;
            GameObject mist = VFXPool.GetParticle();
            mist.transform.position = mistPos;
            mist.transform.localScale = new Vector3(0.4f, 0.4f, 1f);

            var sr = mist.GetComponent<SpriteRenderer>();
            sr.sprite = SpriteCache.WhitePixel;
            sr.color = new Color(0.7f, 0.9f, 1f, 0.3f);
            sr.sortingOrder = 3;

            
            
            var runner = mist.GetComponent<VFXRunner>(); if (runner == null) runner = mist.AddComponent<VFXRunner>(); runner.StartCoroutine(SlashTrailAnim(mist, sr, 0.8f));
        }
    }

    /// <summary>
    /// Priest heal aura - rising golden/white light particles.
    /// </summary>
    public static void SpawnHealAura(Vector3 position, float radius)
    {
        Color healColor = new Color(0.2f, 1f, 0.4f, 0.5f);
        Color holyColor = new Color(1f, 1f, 0.6f, 0.4f);

        // Ground ring
        SpawnAreaPulse(position, radius, healColor);

        // Rising light columns
        for (int i = 0; i < 8; i++)
        {
            float angle = (360f / 8) * i;
            float rad = angle * Mathf.Deg2Rad;
            Vector2 offset = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius * 0.5f;

            GameObject col = VFXPool.GetParticle();
            col.transform.position = position + (Vector3)offset;
            col.transform.localScale = new Vector3(0.15f, 1.5f, 1f);

            var sr = col.GetComponent<SpriteRenderer>();
            sr.sprite = SpriteCache.WhitePixel;
            sr.color = holyColor;
            sr.sortingOrder = 5;

            
            
            
            var runner = col.GetComponent<VFXRunner>(); if (runner == null) runner = col.AddComponent<VFXRunner>(); runner.StartCoroutine(SlashTrailAnim(col, sr, 0.5f));
        }

        // Sparkles
        for (int i = 0; i < 8; i++)
        {
            Vector2 sparkPos = (Vector2)position + Random.insideUnitCircle * radius;
            float lifetime = Random.Range(0.3f, 0.7f);

            GameObject spark = VFXPool.GetParticle();
            spark.transform.position = sparkPos;
            spark.transform.localScale = new Vector3(0.1f, 0.1f, 1f);

            var sr = spark.GetComponent<SpriteRenderer>();
            sr.sprite = SpriteCache.WhitePixel;
            sr.color = Color.Lerp(healColor, holyColor, Random.Range(0f, 1f));
            sr.sortingOrder = 8;

            var rb = spark.GetComponent<Rigidbody2D>();
            rb.gravityScale = -1f;
            rb.velocity = new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(1f, 3f));

            
            
            
            var runner = spark.GetComponent<VFXRunner>(); if (runner == null) runner = spark.AddComponent<VFXRunner>(); runner.StartCoroutine(ParticleAnim(spark, sr, lifetime));
        }
    }

    /// <summary>
    /// Priest divine shield — persistent full-circle shield that follows the player.
    /// </summary>
    public static DivineShieldVFX SpawnDivineShieldEffect(Vector3 position, float radius, bool isEvolved = false, Transform followTarget = null, float duration = 5f)
    {
        Color shieldColor = new Color(0.8f, 0.9f, 1f, 0.4f);
        Color goldColor = new Color(1f, 0.9f, 0.3f, 0.7f);

        // Initial burst — expanding ring
        SpawnAreaPulse(position, radius * 0.5f, goldColor);
        SpawnAreaPulse(position, radius, shieldColor);

        // Ascending sparkles on cast
        for (int i = 0; i < 6; i++)
        {
            Vector2 sparkPos = (Vector2)position + Random.insideUnitCircle * radius * 0.6f;
            GameObject spark = VFXPool.GetParticle();
            spark.transform.position = sparkPos;
            spark.transform.localScale = new Vector3(0.1f, 0.1f, 1f);

            var sr = spark.GetComponent<SpriteRenderer>();
            sr.sprite = SpriteCache.WhitePixel;
            sr.color = Color.Lerp(shieldColor, goldColor, Random.Range(0f, 1f));
            sr.sortingOrder = 8;

            var rb = spark.GetComponent<Rigidbody2D>();
            rb.gravityScale = -0.5f;
            rb.velocity = new Vector2(0, Random.Range(1f, 3f));

            
            
            
            var runner = spark.GetComponent<VFXRunner>(); if (runner == null) runner = spark.AddComponent<VFXRunner>(); runner.StartCoroutine(MistAnim(spark, sr, Random.Range(0.4f, 0.8f)));
        }

        // Persistent shield GameObject
        GameObject shieldObj = new GameObject("DivineShield");
        shieldObj.transform.position = position;

        var shield = shieldObj.AddComponent<DivineShieldVFX>();
        shield.Initialize(radius, duration, isEvolved, followTarget);
        return shield;
    }

    /// <summary>
    /// Shield break/shatter effect — ring fragments fly outward.
    /// </summary>
    public static void SpawnShieldBreakEffect(Vector3 position, float radius, bool isEvolved = false)
    {
        Color shardColor = isEvolved
            ? new Color(1f, 0.9f, 0.3f, 0.9f)
            : new Color(0.8f, 0.9f, 1f, 0.9f);

        // Shatter flash
        SpawnAreaPulse(position, radius, shardColor);

        // Fragments flying outward
        int shardCount = isEvolved ? 16 : 10;
        for (int i = 0; i < shardCount; i++)
        {
            float angle = (360f / shardCount) * i + Random.Range(-10f, 10f);
            float rad = angle * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            GameObject shard = VFXPool.GetParticle();
            shard.transform.position = position + (Vector3)(dir * radius * 0.8f);
            shard.transform.rotation = Quaternion.Euler(0, 0, angle);
            shard.transform.localScale = new Vector3(0.3f, 0.08f, 1f);

            var sr = shard.GetComponent<SpriteRenderer>();
            sr.sprite = SpriteCache.WhitePixel;
            sr.color = shardColor;
            sr.sortingOrder = 7;

            var rb = shard.GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.velocity = dir * Random.Range(4f, 8f);

            
            
            
            var runner = shard.GetComponent<VFXRunner>(); if (runner == null) runner = shard.AddComponent<VFXRunner>(); runner.StartCoroutine(ParticleAnim(shard, sr, Random.Range(0.4f, 0.7f)));
        }

        // Sparkle dust
        for (int i = 0; i < 8; i++)
        {
            Vector2 dustPos = (Vector2)position + Random.insideUnitCircle * radius;
            GameObject dust = VFXPool.GetParticle();
            dust.transform.position = dustPos;
            dust.transform.localScale = new Vector3(0.06f, 0.06f, 1f);

            var sr = dust.GetComponent<SpriteRenderer>();
            sr.sprite = SpriteCache.WhitePixel;
            sr.color = new Color(shardColor.r, shardColor.g, shardColor.b, 0.6f);
            sr.sortingOrder = 8;

            var rb = dust.GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.velocity = Random.insideUnitCircle * Random.Range(1f, 3f);

            
            
            
            var runner = dust.GetComponent<VFXRunner>(); if (runner == null) runner = dust.AddComponent<VFXRunner>(); runner.StartCoroutine(ParticleAnim(dust, sr, Random.Range(0.3f, 0.6f)));
        }
    }

    /// <summary>
    /// Warrior whirlwind - spinning blade circle.
    /// </summary>
    public static void SpawnWhirlwindEffect(Vector3 position, float radius, float duration)
    {
        Color bladeColor = new Color(0.8f, 0.8f, 0.8f, 0.6f);

        // Spawning blade arcs at intervals is handled by skill code,
        // but we add the central dust/devastation ring
        for (int i = 0; i < 12; i++)
        {
            float angle = (360f / 12) * i;
            float rad = angle * Mathf.Deg2Rad;
            Vector2 edgePos = (Vector2)position + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius * 0.7f;

            GameObject dust = VFXPool.GetParticle();
            dust.transform.position = edgePos;
            dust.transform.localScale = new Vector3(0.15f, 0.15f, 1f);

            var sr = dust.GetComponent<SpriteRenderer>();
            sr.sprite = SpriteCache.WhitePixel;
            sr.color = new Color(0.6f, 0.5f, 0.4f, 0.5f);
            sr.sortingOrder = 3;

            var rb = dust.GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            // Spiral outward
            Vector2 tangent = new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad));
            rb.velocity = tangent * 3f;

            
            
            
            var runner = dust.GetComponent<VFXRunner>(); if (runner == null) runner = dust.AddComponent<VFXRunner>(); runner.StartCoroutine(MistAnim(dust, sr, duration * 0.8f));
        }

        // Central impact ring
        SpawnAreaPulse(position, radius, bladeColor);
    }

    /// <summary>
    /// Warrior war cry - expanding aura rings with rising energy.
    /// </summary>
    public static void SpawnWarCryEffect(Vector3 position, float radius)
    {
        Color auraColor = new Color(0.8f, 0.8f, 0.8f, 0.4f);
        Color energyColor = new Color(1f, 0.9f, 0.6f, 0.5f);

        // Multiple expanding rings
        for (int i = 0; i < 3; i++)
        {
            float delay = i * 0.1f;
            float r = radius * (0.6f + i * 0.2f);
            SpawnAreaPulse(position, r, auraColor);
        }

        // Rising energy pillars
        for (int i = 0; i < 6; i++)
        {
            float angle = (360f / 6) * i;
            float rad = angle * Mathf.Deg2Rad;
            Vector2 basePos = (Vector2)position + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius * 0.4f;

            GameObject pillar = VFXPool.GetParticle();
            pillar.transform.position = basePos;
            pillar.transform.localScale = new Vector3(0.15f, 0.5f, 1f);

            var sr = pillar.GetComponent<SpriteRenderer>();
            sr.sprite = SpriteCache.WhitePixel;
            sr.color = energyColor;
            sr.sortingOrder = 6;

            var rb = pillar.GetComponent<Rigidbody2D>();
            rb.gravityScale = -2f;
            rb.velocity = new Vector2(0, Random.Range(3f, 6f));

            
            
            var runner = pillar.GetComponent<VFXRunner>(); if (runner == null) runner = pillar.AddComponent<VFXRunner>(); runner.StartCoroutine(SlashTrailAnim(pillar, sr, 0.5f));
        }
    }

    /// <summary>
    /// Lightning strike from above - useful for mage arcane blast variant or boss attacks.
    /// </summary>
    public static void SpawnLightningStrike(Vector3 position, float width = 0.3f)
    {
        Color boltColor = new Color(0.6f, 0.4f, 1f, 0.9f);
        Color flashColor = new Color(0.8f, 0.7f, 1f, 0.5f);

        // Main bolt (vertical line from above)
        GameObject bolt = VFXPool.GetParticle();
        bolt.transform.position = position + Vector3.up * 5f;
        bolt.transform.localScale = new Vector3(width, 10f, 1f);

        var sr = bolt.GetComponent<SpriteRenderer>();
        sr.sprite = SpriteCache.WhitePixel;
        sr.color = boltColor;
        sr.sortingOrder = 8;

        
            
            var runner = bolt.GetComponent<VFXRunner>(); if (runner == null) runner = bolt.AddComponent<VFXRunner>(); runner.StartCoroutine(SlashTrailAnim(bolt, sr, 0.15f));

        // Branch segments
        for (int i = 0; i < 4; i++)
        {
            float yOffset = 4f - i * 1.5f;
            float xOffset = Random.Range(-0.5f, 0.5f);
            Vector3 branchPos = position + new Vector3(xOffset, yOffset, 0);
            float branchAngle = Random.Range(-30f, 30f);

            GameObject branch = VFXPool.GetParticle();
            branch.transform.position = branchPos;
            branch.transform.rotation = Quaternion.Euler(0, 0, branchAngle);
            branch.transform.localScale = new Vector3(width * 0.6f, 1.2f, 1f);

            var bsr = branch.GetComponent<SpriteRenderer>();
            bsr.sprite = SpriteCache.WhitePixel;
            bsr.color = boltColor;
            bsr.sortingOrder = 8;

            var brunner = branch.GetComponent<VFXRunner>();
            if (brunner == null) brunner = branch.AddComponent<VFXRunner>();
            brunner.StartCoroutine(ParticleAnim(branch, bsr, 0.1f));
        }

        // Impact flash
        SpawnAreaPulse(position, 1.5f, flashColor);
        CameraFollow.Shake(0.2f, 0.1f);
    }

    /// <summary>
    /// Fire explosion - for mage fireball impact.
    /// </summary>
    public static void SpawnFireExplosion(Vector3 position, float radius)
    {
        Color fireCore = new Color(1f, 0.9f, 0.3f, 0.9f);
        Color fireEdge = new Color(1f, 0.3f, 0f, 0.7f);

        // Core flash
        SpawnAreaPulse(position, radius * 0.5f, fireCore);
        // Outer ring
        SpawnAreaPulse(position, radius, fireEdge);

        // Flame particles flying outward
        for (int i = 0; i < 12; i++)
        {
            float angle = (360f / 12) * i + Random.Range(-15f, 15f);
            float rad = angle * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            GameObject flame = VFXPool.GetParticle();
            flame.transform.position = position;
            flame.transform.localScale = new Vector3(Random.Range(0.1f, 0.25f), Random.Range(0.1f, 0.25f), 1f);

            var sr = flame.GetComponent<SpriteRenderer>();
            sr.sprite = SpriteCache.WhitePixel;
            sr.color = Color.Lerp(fireCore, fireEdge, Random.Range(0f, 1f));
            sr.sortingOrder = 7;

            var rb = flame.GetComponent<Rigidbody2D>();
            rb.gravityScale = -0.3f;
            rb.velocity = dir * Random.Range(3f, 7f);

            
            
            var runner = flame.GetComponent<VFXRunner>(); if (runner == null) runner = flame.AddComponent<VFXRunner>(); runner.StartCoroutine(ParticleAnim(flame, sr, Random.Range(0.3f, 0.6f)));
        }

        // Embers
        for (int i = 0; i < 6; i++)
        {
            Vector2 emberPos = (Vector2)position + Random.insideUnitCircle * radius * 0.3f;
            float lifetime = Random.Range(0.5f, 1f);

            GameObject ember = VFXPool.GetParticle();
            ember.transform.position = emberPos;
            ember.transform.localScale = new Vector3(0.06f, 0.06f, 1f);

            var sr = ember.GetComponent<SpriteRenderer>();
            sr.sprite = SpriteCache.WhitePixel;
            sr.color = fireCore;
            sr.sortingOrder = 8;

            var rb = ember.GetComponent<Rigidbody2D>();
            rb.gravityScale = 0.2f;
            rb.velocity = new Vector2(Random.Range(-2f, 2f), Random.Range(2f, 5f));

            
            
            var runner = ember.GetComponent<VFXRunner>(); if (runner == null) runner = ember.AddComponent<VFXRunner>(); runner.StartCoroutine(ParticleAnim(ember, sr, lifetime));
        }
    }

    // ===================================================================
    // SKILL CAST VFX
    // ===================================================================

    /// <summary>
    /// Energy charge effect — particles spiraling inward, growing in intensity.
    /// Use as a coroutine started by the caller.
    /// </summary>
    public static IEnumerator SkillChargeEffect(Vector3 position, Color color, float duration)
    {
        float elapsed = 0f;
        float interval = 0.02f;
        float timer = 0f;

        while (elapsed < duration)
        {
            yield return null;
            elapsed += Time.deltaTime;
            timer += Time.deltaTime;
            if (timer >= interval)
            {
                timer = 0f;
                float progress = elapsed / duration;
                float startRadius = 1.5f * (1f - progress * 0.5f);
                int count = Mathf.RoundToInt(2 + progress * 4);

                for (int i = 0; i < count; i++)
                {
                    float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                    Vector2 startPos = (Vector2)position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * startRadius;

                    GameObject p = VFXPool.GetParticle();
                    p.transform.position = startPos;
                    p.transform.localScale = new Vector3(0.08f, 0.08f, 1f);

                    var sr = p.GetComponent<SpriteRenderer>();
                    sr.color = new Color(color.r, color.g, color.b, 0.6f + progress * 0.4f);
                    sr.sortingOrder = 9;

                    var rb = p.GetComponent<Rigidbody2D>();
                    rb.gravityScale = 0f;
                    Vector2 inward = ((Vector2)position - startPos).normalized * (3f + progress * 5f);
                    rb.velocity = inward;

                    
            
                    
                    if (!TryGetRunner(p, out var runner)) yield break; runner.StartCoroutine(ParticleAnim(p, sr, 0.2f + progress * 0.1f));
                }
            }
        }

        // Release burst at end
        SpawnImpactFlash(position, color, 1f);
    }

    /// <summary>
    /// Muzzle flash — directional burst at projectile spawn position.
    /// </summary>
    public static void SpawnProjectileMuzzleFlash(Vector3 position, Vector2 direction, Color color)
    {
        // Core flash
        SpawnImpactFlash(position, color, 0.4f);

        // Directional sparks
        for (int i = 0; i < 5; i++)
        {
            float spread = Random.Range(-25f, 25f) * Mathf.Deg2Rad;
            float baseAngle = Mathf.Atan2(direction.y, direction.x);
            float angle = baseAngle + spread;
            Vector2 sparkDir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            GameObject p = VFXPool.GetParticle();
            if (p == null) continue;
            p.transform.position = position;
            p.transform.localScale = new Vector3(0.1f, 0.1f, 1f);

            var sr = p.GetComponent<SpriteRenderer>();
            sr.color = new Color(color.r, color.g, color.b, 0.8f);
            sr.sortingOrder = 9;

            var rb = p.GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.velocity = sparkDir * Random.Range(3f, 7f);

            
            
            
            if (!TryGetRunner(p, out var runner)) return; runner.StartCoroutine(ParticleAnim(p, sr, Random.Range(0.1f, 0.25f)));
        }
    }

    /// <summary>
    /// Holy light beam — dramatic heal effect with descending light from above.
    /// </summary>
    public static void SpawnHolyLightBeam(Vector3 position, float radius)
    {
        Color beamColor = new Color(1f, 1f, 0.7f, 0.4f);
        Color groundColor = new Color(0.3f, 1f, 0.5f, 0.5f);
        Color sparkleColor = new Color(1f, 1f, 0.8f, 0.9f);

        // Ground ring
        SpawnAreaPulse(position, radius, groundColor);
        SpawnAreaPulse(position, radius * 0.5f, new Color(1f, 1f, 0.6f, 0.6f));

        // Vertical light beam
        GameObject beam = VFXPool.GetBeam();
        beam.transform.position = position + new Vector3(0, 3f, 0);
        beam.transform.localScale = new Vector3(radius * 0.3f, 6f, 1f);

        var beamSr = beam.GetComponent<SpriteRenderer>();
        beamSr.color = beamColor;
        beamSr.sortingOrder = 4;

        var beamRunner = beam.GetComponent<VFXRunner>();
        if (beamRunner == null) beamRunner = beam.AddComponent<VFXRunner>();
        beamRunner.StartCoroutine(BeamAnim(beam, beamSr, 0.4f));

        // Descending sparkles
        for (int i = 0; i < 12; i++)
        {
            Vector2 sparkPos = (Vector2)position + Random.insideUnitCircle * radius * 0.7f;
            float startHeight = Random.Range(3f, 6f);

            GameObject spark = VFXPool.GetParticle();
            spark.transform.position = new Vector3(sparkPos.x, position.y + startHeight, 0);
            spark.transform.localScale = new Vector3(0.1f, 0.1f, 1f);

            var sr = spark.GetComponent<SpriteRenderer>();
            sr.color = sparkleColor;
            sr.sortingOrder = 8;

            var rb = spark.GetComponent<Rigidbody2D>();
            rb.gravityScale = 0.5f;
            rb.velocity = new Vector2(Random.Range(-0.5f, 0.5f), -Random.Range(2f, 5f));

            
            
            
            var runner = spark.GetComponent<VFXRunner>(); if (runner == null) runner = spark.AddComponent<VFXRunner>(); runner.StartCoroutine(MistAnim(spark, sr, Random.Range(0.4f, 0.8f)));
        }

        // Rising healing particles (ground up)
        for (int i = 0; i < 6; i++)
        {
            Vector2 healPos = (Vector2)position + Random.insideUnitCircle * radius * 0.4f;
            GameObject heal = VFXPool.GetParticle();
            heal.transform.position = healPos;
            heal.transform.localScale = new Vector3(0.1f, 0.1f, 1f);

            var sr = heal.GetComponent<SpriteRenderer>();
            sr.color = new Color(0.3f, 1f, 0.4f, 0.8f);
            sr.sortingOrder = 7;

            var rb = heal.GetComponent<Rigidbody2D>();
            rb.gravityScale = -0.8f;
            rb.velocity = new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(1.5f, 4f));

            
            
            
            var runner = heal.GetComponent<VFXRunner>(); if (runner == null) runner = heal.AddComponent<VFXRunner>(); runner.StartCoroutine(ParticleAnim(heal, sr, Random.Range(0.5f, 0.9f)));
        }
    }

    /// <summary>
    /// Persistent buff aura — follows the player for the buff duration.
    /// </summary>
    public static void SpawnBuffAura(Transform followTarget, Color color, float duration)
    {
        GameObject auraObj = new GameObject("BuffAura");
        auraObj.transform.position = followTarget.position;

        // Pulsing ring at feet
        var ringSr = auraObj.AddComponent<SpriteRenderer>();
        ringSr.sprite = SpriteCache.WhitePixel;
        ringSr.color = new Color(color.r, color.g, color.b, 0.3f);
        ringSr.sortingOrder = 3;
        auraObj.transform.localScale = new Vector3(2f, 0.4f, 1f);

        // Rising energy particles (periodic)
        var runner = auraObj.AddComponent<VFXRunner>();
        runner.StartCoroutine(BuffAuraLoop(auraObj, ringSr, followTarget, color, duration));
    }

    private static IEnumerator BuffAuraLoop(GameObject aura, SpriteRenderer ringSr, Transform target, Color color, float duration)
    {
        float elapsed = 0f;
        float particleTimer = 0f;

        while (elapsed < duration && target != null)
        {
            elapsed += Time.deltaTime;
            aura.transform.position = target.position;

            // Pulse ring
            float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * 6f);
            float ringScale = 1.8f + pulse * 0.4f;
            aura.transform.localScale = new Vector3(ringScale, 0.4f, 1f);
            Color rc = ringSr.color;
            float fadeOut = elapsed > duration - 0.5f ? Mathf.Max(0f, 1f - (elapsed - duration + 0.5f) / 0.5f) : 1f;
            rc.a = (0.2f + pulse * 0.2f) * fadeOut;
            ringSr.color = rc;

            // Spawn rising particles periodically
            particleTimer += Time.deltaTime;
            if (particleTimer >= 0.08f)
            {
                particleTimer = 0f;
                Vector2 pos = (Vector2)target.position + Random.insideUnitCircle * 0.8f;
                GameObject p = VFXPool.GetParticle();
                p.transform.position = pos;
                p.transform.localScale = new Vector3(0.06f, 0.06f, 1f);

                var sr = p.GetComponent<SpriteRenderer>();
                sr.color = new Color(color.r, color.g, color.b, 0.6f * fadeOut);
                sr.sortingOrder = 6;

                var rb = p.GetComponent<Rigidbody2D>();
                rb.gravityScale = -1f;
                rb.velocity = new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(2f, 4f));

                
            
                
                if (!TryGetRunner(p, out var runner)) yield break; runner.StartCoroutine(ParticleAnim(p, sr, Random.Range(0.3f, 0.5f)));
            }

            yield return null;
        }

        Object.Destroy(aura);
    }

    // ===================================================================
    // CINEMATIC SKILL VFX
    // ===================================================================

    /// <summary>
    /// Energy orb — pulsing orb that grows then bursts outward. Coroutine.
    /// </summary>
    public static IEnumerator EnergyOrbEffect(Vector3 position, Color color, float targetRadius, float duration)
    {
        // Create orb
        GameObject orb = VFXPool.GetAreaPulse();
        orb.transform.position = position;
        orb.transform.localScale = new Vector3(0.1f, 0.1f, 1f);
        var orbSr = orb.GetComponent<SpriteRenderer>();
        orbSr.sortingOrder = 9;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            yield return null;
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 20f);
            float scale = Mathf.Lerp(0.1f, targetRadius, t * (1f - t * 0.3f)) * (0.9f + pulse * 0.1f);
            orb.transform.localScale = new Vector3(scale * 2f, scale * 2f, 1f);
            orbSr.color = new Color(color.r, color.g, color.b, 0.4f + pulse * 0.3f);

            // Orbiting particles
            if (elapsed % 0.04f < Time.deltaTime)
            {
                float angle = elapsed * 360f * Mathf.Deg2Rad;
                Vector2 orbitPos = (Vector2)position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * scale;
                GameObject p = VFXPool.GetParticle();
                p.transform.position = orbitPos;
                p.transform.localScale = new Vector3(0.1f, 0.1f, 1f);
                var psr = p.GetComponent<SpriteRenderer>();
                psr.color = new Color(color.r, color.g, color.b, 0.8f);
                psr.sortingOrder = 8;
                var prb = p.GetComponent<Rigidbody2D>();
                prb.gravityScale = 0f;
                prb.velocity = (position - (Vector3)orbitPos).normalized * 3f;
                
            
                
                if (!TryGetRunner(p, out var runner)) yield break; runner.StartCoroutine(ParticleAnim(p, psr, 0.15f));
            }
        }

        // Burst
        SpawnImpactFlash(position, color, targetRadius);
        for (int i = 0; i < 12; i++)
        {
            float angle = (360f / 12) * i * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            GameObject p = VFXPool.GetParticle();
            if (p == null) continue;
            p.transform.position = position;
            p.transform.localScale = new Vector3(0.15f, 0.15f, 1f);
            var psr = p.GetComponent<SpriteRenderer>();
            psr.color = Color.Lerp(color, Color.white, Random.Range(0f, 0.5f));
            psr.sortingOrder = 8;
            var prb = p.GetComponent<Rigidbody2D>();
            prb.gravityScale = 0f;
            prb.velocity = dir * Random.Range(4f, 8f);
            
            
            
            if (!TryGetRunner(p, out var runner)) yield break; runner.StartCoroutine(ParticleAnim(p, psr, Random.Range(0.3f, 0.6f)));
        }
        VFXPool.Return("areapulse", orb);
    }

    /// <summary>
    /// Shockwave — fast expanding ring with distortion-like afterimages.
    /// </summary>
    public static void SpawnShockwave(Vector3 position, Color color, float radius, float speed = 8f)
    {
        // Multiple expanding rings for depth
        for (int ring = 0; ring < 3; ring++)
        {
            GameObject obj = VFXPool.GetAreaPulse();
            obj.transform.position = position;
            obj.transform.localScale = new Vector3(0.1f, 0.1f, 1f);
            var sr = obj.GetComponent<SpriteRenderer>();
            sr.color = new Color(color.r, color.g, color.b, 0.6f - ring * 0.15f);
            sr.sortingOrder = 6;

            
            
            var runner = obj.GetComponent<VFXRunner>(); if (runner == null) runner = obj.AddComponent<VFXRunner>(); runner.StartCoroutine(ShockwaveAnim(obj, sr, radius * (1f + ring * 0.2f), speed, ring * 0.05f));
        }

        // Ground crack particles
        for (int i = 0; i < 10; i++)
        {
            float angle = (360f / 10) * i * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            GameObject p = VFXPool.GetParticle();
            if (p == null) continue;
            p.transform.position = position;
            p.transform.localScale = new Vector3(0.2f, 0.05f, 1f);
            p.transform.rotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg);
            var psr = p.GetComponent<SpriteRenderer>();
            psr.color = new Color(color.r * 0.6f, color.g * 0.6f, color.b * 0.6f, 0.5f);
            psr.sortingOrder = 4;
            var prb = p.GetComponent<Rigidbody2D>();
            prb.gravityScale = 0f;
            prb.velocity = dir * speed * 0.5f;
            
            
            
            if (!TryGetRunner(p, out var runner)) return; runner.StartCoroutine(ParticleAnim(p, psr, 0.3f));
        }
    }

    private static IEnumerator ShockwaveAnim(GameObject obj, SpriteRenderer sr, float targetRadius, float speed, float delay)
    {
        yield return new WaitForSeconds(delay);
        float duration = targetRadius / speed;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = t / duration;
            float scale = Mathf.Lerp(0.1f, targetRadius * 2f, p * p); // ease-in for fast expansion
            obj.transform.localScale = new Vector3(scale, scale, 1f);
            Color c = sr.color;
            c.a = (0.6f - delay * 5f) * (1f - p);
            sr.color = c;
            yield return null;
        }
        VFXPool.Return("areapulse", obj);
    }

    // ===================================================================
    // PROJECTILE IMPACT & GROUND SLAM & SCREEN FLASH
    // ===================================================================

    /// <summary>
    /// Projectile hit effect — directional shrapnel burst + flash, bigger than ImpactFlash.
    /// </summary>
    public static void SpawnProjectileHitEffect(Vector3 position, Vector2 direction, Color color, bool isExplosion = false)
    {
        float flashRadius = isExplosion ? 1.2f : 0.6f;
        SpawnImpactFlash(position, color, flashRadius);

        int shardCount = isExplosion ? 10 : 6;
        for (int i = 0; i < shardCount; i++)
        {
            float spread = Random.Range(-60f, 60f) * Mathf.Deg2Rad;
            float baseAngle = Mathf.Atan2(direction.y, direction.x);
            float angle = baseAngle + spread;
            Vector2 shardDir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            GameObject p = VFXPool.GetParticle();
            if (p == null) continue;
            p.transform.position = position;
            p.transform.localScale = new Vector3(Random.Range(0.08f, 0.18f), Random.Range(0.08f, 0.18f), 1f);

            var sr = p.GetComponent<SpriteRenderer>();
            sr.color = Color.Lerp(color, Color.white, Random.Range(0f, 0.5f));
            sr.sortingOrder = 8;

            var rb = p.GetComponent<Rigidbody2D>();
            rb.gravityScale = 0.3f;
            rb.velocity = shardDir * Random.Range(2f, 6f);

            
            
            
            if (!TryGetRunner(p, out var runner)) return; runner.StartCoroutine(ParticleAnim(p, sr, Random.Range(0.2f, 0.4f)));
        }
    }

    /// <summary>
    /// Ground slam — expanding crack ring + dust wave + debris.
    /// </summary>
    public static void SpawnGroundSlam(Vector3 position, float radius, Color color)
    {
        // Crack ring (two expanding pulses)
        SpawnAreaPulse(position, radius * 0.5f, new Color(color.r, color.g, color.b, 0.6f));
        SpawnAreaPulse(position, radius, new Color(0.5f, 0.4f, 0.3f, 0.4f));

        // Ground dust wave — ring of dust particles pushing outward
        int dustCount = 14;
        for (int i = 0; i < dustCount; i++)
        {
            float angle = (360f / dustCount) * i + Random.Range(-10f, 10f);
            float rad = angle * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            GameObject dust = VFXPool.GetParticle();
            dust.transform.position = position + new Vector3(0, -0.1f, 0);
            dust.transform.localScale = new Vector3(Random.Range(0.15f, 0.3f), Random.Range(0.1f, 0.2f), 1f);

            var sr = dust.GetComponent<SpriteRenderer>();
            sr.color = new Color(0.55f, 0.45f, 0.35f, 0.6f);
            sr.sortingOrder = 3;

            var rb = dust.GetComponent<Rigidbody2D>();
            rb.gravityScale = 0.2f;
            rb.velocity = dir * Random.Range(2f, 5f);

            
            
            
            var runner = dust.GetComponent<VFXRunner>(); if (runner == null) runner = dust.AddComponent<VFXRunner>(); runner.StartCoroutine(ParticleAnim(dust, sr, Random.Range(0.3f, 0.6f)));
        }

        // Debris flying up
        for (int i = 0; i < 8; i++)
        {
            float angle = Random.Range(-60f, 60f) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Sin(angle), Mathf.Abs(Mathf.Cos(angle)));

            GameObject debris = VFXPool.GetParticle();
            debris.transform.position = position;
            debris.transform.localScale = new Vector3(Random.Range(0.06f, 0.12f), Random.Range(0.06f, 0.12f), 1f);

            var sr = debris.GetComponent<SpriteRenderer>();
            sr.color = Color.Lerp(new Color(color.r, color.g, color.b, 0.8f), new Color(0.5f, 0.4f, 0.3f, 0.8f), Random.Range(0f, 0.6f));
            sr.sortingOrder = 6;

            var rb = debris.GetComponent<Rigidbody2D>();
            rb.gravityScale = 1f;
            rb.velocity = dir * Random.Range(3f, 7f);

            
            
            var runner = debris.GetComponent<VFXRunner>(); if (runner == null) runner = debris.AddComponent<VFXRunner>(); runner.StartCoroutine(ParticleAnim(debris, sr, Random.Range(0.4f, 0.7f)));
        }
    }

    /// <summary>
    /// Screen flash — brief full-screen color overlay for major skill casts.
    /// </summary>
    private static GameObject _screenFlashObj;
    private static SpriteRenderer _screenFlashSr;

    public static void SpawnScreenFlash(Color color, float duration = 0.15f)
    {
        if (_screenFlashObj == null)
        {
            _screenFlashObj = new GameObject("ScreenFlash");
            _screenFlashObj.transform.SetParent(null);
            _screenFlashSr = _screenFlashObj.AddComponent<SpriteRenderer>();
            _screenFlashSr.sprite = SpriteCache.WhitePixel;
            _screenFlashSr.sortingOrder = 100;
            _screenFlashSr.color = Color.clear;
        }

        _screenFlashObj.SetActive(true);
        _screenFlashSr.color = new Color(color.r, color.g, color.b, 0.3f);

        
        
            var runner = _screenFlashObj.GetComponent<VFXRunner>(); if (runner == null) runner = _screenFlashObj.AddComponent<VFXRunner>(); runner.StartCoroutine(ScreenFlashAnim(_screenFlashSr, color, duration));
    }

    private static IEnumerator ScreenFlashAnim(SpriteRenderer sr, Color color, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float progress = t / duration;
            float alpha = 0.3f * (1f - progress * progress);
            sr.color = new Color(color.r, color.g, color.b, alpha);
            yield return null;
        }
        sr.color = Color.clear;
    }

    // ===================================================================
    // IMPACT & FEEL EFFECTS
    // ===================================================================

    /// <summary>
    /// Small expanding flash at a hit point — adds punch to melee/projectile impacts.
    /// </summary>
    public static void SpawnImpactFlash(Vector3 position, Color color, float radius = 0.5f)
    {
        GameObject obj = VFXPool.GetAreaPulse();
        obj.transform.position = position;
        obj.transform.localScale = new Vector3(0.1f, 0.1f, 1f);

        var sr = obj.GetComponent<SpriteRenderer>();
        sr.color = new Color(color.r, color.g, color.b, 0.8f);
        sr.sortingOrder = 9;

            RunCoroutine(AreaPulseAnim(obj, sr, radius));
    }

    private static IEnumerator ImpactFlashAnim(GameObject obj, SpriteRenderer sr, float radius)
    {
        float duration = 0.12f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float progress = t / duration;
            float scale = Mathf.Lerp(0.1f, radius * 2f, progress);
            obj.transform.localScale = new Vector3(scale, scale, 1f);
            Color c = sr.color;
            c.a = 0.8f * (1f - progress);
            sr.color = c;
            yield return null;
        }
        VFXPool.Return("areapulse", obj);
    }

    /// <summary>
    /// Landing dust — small ground particles when character lands/attacks.
    /// </summary>
    public static void SpawnLandingDust(Vector3 position, Vector2 direction)
    {
        for (int i = 0; i < 4; i++)
        {
            Vector2 dustDir = new Vector2(-direction.x, -direction.y).normalized;
            float angle = Random.Range(-30f, 30f) * Mathf.Deg2Rad;
            dustDir = new Vector2(
                dustDir.x * Mathf.Cos(angle) - dustDir.y * Mathf.Sin(angle),
                dustDir.x * Mathf.Sin(angle) + dustDir.y * Mathf.Cos(angle));

            GameObject p = VFXPool.GetParticle();
            p.transform.position = position + new Vector3(0, -0.2f, 0);
            p.transform.localScale = new Vector3(0.1f, 0.1f, 1f);

            var sr = p.GetComponent<SpriteRenderer>();
            sr.color = new Color(0.6f, 0.55f, 0.45f, 0.5f);
            sr.sortingOrder = 3;

            var rb = p.GetComponent<Rigidbody2D>();
            rb.gravityScale = 0.3f;
            rb.velocity = dustDir * Random.Range(1f, 3f);

            
            
            
            if (!TryGetRunner(p, out var runner)) return; runner.StartCoroutine(ParticleAnim(p, sr, Random.Range(0.2f, 0.4f)));
        }
    }

    /// <summary>
    /// Player death explosion — dramatic burst of particles in class color.
    /// </summary>
    public static void SpawnPlayerDeathEffect(Vector3 position, Color classColor)
    {
        // Expanding shockwave
        SpawnAreaPulse(position, 1.5f, new Color(classColor.r, classColor.g, classColor.b, 0.6f));
        SpawnAreaPulse(position, 2.5f, new Color(0.8f, 0.8f, 0.8f, 0.2f));

        // Death flash
        GameObject flash = VFXPool.GetDeathFlash();
        if (flash == null) return;
        flash.transform.position = position;
        var flashSr = flash.GetComponent<SpriteRenderer>();
        flashSr.color = new Color(1f, 0.8f, 0.3f, 0.8f);
        flash.transform.localScale = new Vector3(2f, 2f, 1f);
        var flashRunner = flash.GetComponent<VFXRunner>();
        if (flashRunner == null) flashRunner = flash.AddComponent<VFXRunner>();
        flashRunner.StartCoroutine(DeathFlashAnim(flash, flashSr));

        // Burst particles
        for (int i = 0; i < 16; i++)
        {
            float angle = (360f / 16) * i + Random.Range(-15f, 15f);
            float rad = angle * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            GameObject p = VFXPool.GetParticle();
            if (p == null) continue;
            p.transform.position = position;
            p.transform.localScale = new Vector3(Random.Range(0.1f, 0.25f), Random.Range(0.1f, 0.25f), 1f);

            var sr = p.GetComponent<SpriteRenderer>();
            sr.color = Color.Lerp(classColor, Color.white, Random.Range(0f, 0.5f));
            sr.sortingOrder = 7;

            var rb = p.GetComponent<Rigidbody2D>();
            rb.gravityScale = 0.5f;
            rb.velocity = dir * Random.Range(3f, 8f);

            
            
            
            if (!TryGetRunner(p, out var runner)) return; runner.StartCoroutine(ParticleAnim(p, sr, Random.Range(0.4f, 0.8f)));
        }

        // Rising soul particles
        for (int i = 0; i < 8; i++)
        {
            GameObject soul = VFXPool.GetParticle();
            soul.transform.position = position + new Vector3(Random.Range(-0.3f, 0.3f), 0, 0);
            soul.transform.localScale = new Vector3(0.12f, 0.12f, 1f);

            var sr = soul.GetComponent<SpriteRenderer>();
            sr.color = new Color(classColor.r, classColor.g, classColor.b, 0.6f);
            sr.sortingOrder = 8;

            var rb = soul.GetComponent<Rigidbody2D>();
            rb.gravityScale = -0.5f;
            rb.velocity = new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(2f, 5f));

            
            
            var runner = soul.GetComponent<VFXRunner>(); if (runner == null) runner = soul.AddComponent<VFXRunner>(); runner.StartCoroutine(ParticleAnim(soul, sr, Random.Range(0.6f, 1f)));
        }
    }

    // ===================================================================
    // ANIMATION COROUTINES
    // ===================================================================

    private static IEnumerator SlashAnim(GameObject obj, SpriteRenderer sr)
    {
        float duration = 0.2f;
        float t = 0f;
        Vector3 startScale = obj.transform.localScale;
        while (t < duration)
        {
            t += Time.deltaTime;
            float progress = t / duration;
            obj.transform.localScale = new Vector3(
                startScale.x * (1f + progress * 0.5f),
                startScale.y * (1f - progress * 0.5f),
                1f
            );
            Color c = sr.color;
            c.a = 0.8f * (1f - progress);
            sr.color = c;
            yield return null;
        }
        VFXPool.Return("slash", obj);
    }

    private static IEnumerator SlashTrailAnim(GameObject obj, SpriteRenderer sr, float delay)
    {
        yield return new WaitForSeconds(delay);

        float duration = 0.15f;
        float t = 0f;
        Vector3 startScale = obj.transform.localScale;
        while (t < duration)
        {
            t += Time.deltaTime;
            float progress = t / duration;
            obj.transform.localScale = new Vector3(
                startScale.x * (1f + progress * 0.3f),
                startScale.y * (1f - progress * 0.7f),
                1f
            );
            Color c = sr.color;
            c.a = 0.6f * (1f - progress);
            sr.color = c;
            yield return null;
        }
        VFXPool.Return("slash", obj);
    }

    private static IEnumerator BeamAnim(GameObject obj, SpriteRenderer sr, float duration)
    {
        float t = 0f;
        Vector3 startScale = obj.transform.localScale;
        while (t < duration)
        {
            t += Time.deltaTime;
            float progress = t / duration;
            obj.transform.localScale = new Vector3(
                startScale.x,
                startScale.y * (1f - progress * 0.8f),
                1f
            );
            Color c = sr.color;
            c.a = 0.8f * (1f - progress);
            sr.color = c;
            yield return null;
        }
        VFXPool.Return("beam", obj);
    }

    private static IEnumerator MistAnim(GameObject obj, SpriteRenderer sr, float duration)
    {
        float t = 0f;
        Vector3 startScale = obj.transform.localScale;
        while (t < duration)
        {
            t += Time.deltaTime;
            float progress = t / duration;
            obj.transform.localScale = startScale * (1f + progress * 2f);
            Color c = sr.color;
            c.a = 0.3f * (1f - progress);
            sr.color = c;
            yield return null;
        }
        VFXPool.Return("mist", obj);
    }

    private static IEnumerator DamageNumberAnim(GameObject obj, SpriteRenderer sr, bool isCrit)
    {
        float duration = isCrit ? 1.2f : 0.8f;
        float t = 0f;
        Vector3 pos = obj.transform.position;
        float speed = 2f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            pos.y += speed * Time.unscaledDeltaTime;
            obj.transform.position = pos;

            // Bounce scale for crit
            if (isCrit)
            {
                float scaleT = t / 0.15f; // first 0.15s = bounce
                if (scaleT < 1f)
                {
                    // Overshoot: 0 → 1.3
                    float s = Mathf.Lerp(0.1f, 1.3f, scaleT * scaleT);
                    obj.transform.localScale = new Vector3(s, s, 1f);
                }
                else if (t < 0.25f)
                {
                    // Settle: 1.3 → 0.8
                    float settleT = (t - 0.15f) / 0.1f;
                    float s = Mathf.Lerp(1.3f, 0.8f, settleT);
                    obj.transform.localScale = new Vector3(s, s, 1f);
                }
                else
                {
                    obj.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
                }
            }
            else
            {
                if (t < 0.1f)
                {
                    float s = Mathf.Lerp(0.1f, 0.5f, t / 0.1f);
                    obj.transform.localScale = new Vector3(s, s, 1f);
                }
            }

            float progress = t / duration;
            Color c = sr.color;
            c.a = 1f - progress;
            sr.color = c;
            yield return null;
        }
        // Clean up non-cached sprites before returning to pool
        if (sr != null && sr.sprite != null && sr.sprite != SpriteCache.WhitePixel)
        {
            // Only destroy non-cached sprites (cached ones are managed by _numberSpriteCache)
            bool isCached = false;
            foreach (var kv in _numberSpriteCache)
            {
                if (kv.Value == sr.sprite) { isCached = true; break; }
            }
            if (!isCached)
            {
                Object.Destroy(sr.sprite.texture);
                Object.Destroy(sr.sprite);
            }
            sr.sprite = SpriteCache.WhitePixel;
        }
        VFXPool.Return("dmgnum", obj);
    }

    private static IEnumerator AreaPulseAnim(GameObject obj, SpriteRenderer sr, float targetRadius)
    {
        float duration = 0.3f;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float progress = t / duration;
            float scale = Mathf.Lerp(0.1f, targetRadius * 2f, progress);
            obj.transform.localScale = new Vector3(scale, scale, 1f);

            Color c = sr.color;
            c.a = 0.3f * (1f - progress);
            sr.color = c;
            yield return null;
        }
        VFXPool.Return("areapulse", obj);
    }

    private static IEnumerator DeathFlashAnim(GameObject obj, SpriteRenderer sr)
    {
        float duration = 0.2f;
        float t = 0f;
        Vector3 startScale = obj.transform.localScale;
        while (t < duration)
        {
            t += Time.deltaTime;
            float progress = t / duration;
            obj.transform.localScale = startScale * (1f + progress * 1.5f);
            Color c = sr.color;
            c.a = 0.6f * (1f - progress);
            sr.color = c;
            yield return null;
        }
        VFXPool.Return("deathflash", obj);
    }

    private static IEnumerator ParticleAnim(GameObject obj, SpriteRenderer sr, float lifetime)
    {
        float t = 0f;
        while (t < lifetime)
        {
            t += Time.deltaTime;
            Color c = sr.color;
            c.a = 1f - (t / lifetime);
            sr.color = c;
            yield return null;
        }
        VFXPool.Return("particle", obj);
    }

    // ===================================================================
    // NUMBER SPRITE GENERATION (cached)
    // ===================================================================

    private static readonly Dictionary<int, Sprite> _numberSpriteCache = new Dictionary<int, Sprite>();
    private const int MaxNumberCacheSize = 128;
    private static readonly List<int> _evictKeys = new List<int>();

    private static Sprite CreateNumberSprite(int number, bool isCrit, Color? customColor = null)
    {
        int key = (number << 2) | (isCrit ? 2 : 0) | (customColor.HasValue ? 1 : 0);

        if (!customColor.HasValue && _numberSpriteCache.TryGetValue(key, out var cached))
            return cached;

        Sprite sprite = BuildNumberSprite(number, isCrit, customColor);

        if (!customColor.HasValue)
        {
            if (_numberSpriteCache.Count >= MaxNumberCacheSize)
            {
                // Partial eviction: remove oldest 32 entries instead of full flush
                // to avoid destroying sprites still in use by active animations
                _evictKeys.Clear();
                int evictCount = 0;
                foreach (var kv in _numberSpriteCache)
                {
                    if (evictCount >= 32) break;
                    _evictKeys.Add(kv.Key);
                    evictCount++;
                }
                foreach (var k in _evictKeys)
                {
                    if (_numberSpriteCache.TryGetValue(k, out var s) && s != null)
                    {
                        Object.Destroy(s.texture);
                        Object.Destroy(s);
                    }
                    _numberSpriteCache.Remove(k);
                }
            }
            _numberSpriteCache[key] = sprite;
        }

        return sprite;
    }

    private static Sprite BuildNumberSprite(int number, bool isCrit, Color? customColor)
    {
        string text = isCrit ? number + "!" : number.ToString();
        int width = text.Length * 8;
        int height = 12;

        Texture2D tex = new Texture2D(width, height);
        Color[] pixels = new Color[width * height];

        Color digitColor = customColor ?? (isCrit ? new Color(1f, 1f, 0.2f) : new Color(1f, 0.3f, 0.3f));

        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            int[] pattern = GetDigitPattern(ch);
            int offsetX = i * 8;

            for (int y = 0; y < 10; y++)
            {
                for (int x = 0; x < 6; x++)
                {
                    int px = offsetX + x;
                    int py = y + 1;
                    if (px < width && py < height)
                    {
                        bool on = (pattern[y] & (1 << (5 - x))) != 0;
                        pixels[py * width + px] = on ? digitColor : Color.clear;
                    }
                }
            }
        }

        tex.SetPixels(pixels);
        tex.filterMode = FilterMode.Point;
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 10f);
    }

    private static int[] GetDigitPattern(char ch)
    {
        switch (ch)
        {
            case '0': return new int[] { 0b011110, 0b110011, 0b110011, 0b110011, 0b110011, 0b110011, 0b110011, 0b110011, 0b011110, 0b000000 };
            case '1': return new int[] { 0b001100, 0b011100, 0b101100, 0b001100, 0b001100, 0b001100, 0b001100, 0b001100, 0b111111, 0b000000 };
            case '2': return new int[] { 0b011110, 0b110011, 0b000011, 0b000110, 0b001100, 0b011000, 0b110000, 0b110000, 0b111111, 0b000000 };
            case '3': return new int[] { 0b011110, 0b110011, 0b000011, 0b000011, 0b001110, 0b000011, 0b000011, 0b110011, 0b011110, 0b000000 };
            case '4': return new int[] { 0b000110, 0b001110, 0b011110, 0b110110, 0b111111, 0b000110, 0b000110, 0b000110, 0b000110, 0b000000 };
            case '5': return new int[] { 0b111111, 0b110000, 0b110000, 0b111110, 0b000011, 0b000011, 0b000011, 0b110011, 0b011110, 0b000000 };
            case '6': return new int[] { 0b011110, 0b110011, 0b110000, 0b111110, 0b110011, 0b110011, 0b110011, 0b110011, 0b011110, 0b000000 };
            case '7': return new int[] { 0b111111, 0b000011, 0b000110, 0b000110, 0b001100, 0b001100, 0b011000, 0b011000, 0b011000, 0b000000 };
            case '8': return new int[] { 0b011110, 0b110011, 0b110011, 0b011110, 0b110011, 0b110011, 0b110011, 0b110011, 0b011110, 0b000000 };
            case '9': return new int[] { 0b011110, 0b110011, 0b110011, 0b110011, 0b011111, 0b000011, 0b000011, 0b110011, 0b011110, 0b000000 };
            case '!': return new int[] { 0b011000, 0b011000, 0b011000, 0b011000, 0b011000, 0b011000, 0b000000, 0b011000, 0b011000, 0b000000 };
            default:  return new int[] { 0b111111, 0b111111, 0b111111, 0b111111, 0b111111, 0b111111, 0b111111, 0b111111, 0b111111, 0b000000 };
        }
    }
}
