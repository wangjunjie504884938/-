using UnityEngine;
using System.Collections;

/// <summary>
/// VFXHelper partial — Skill-specific visual effects (IceNova/HealAura/ShieldBreak/Whirlwind/WarCry/Lightning/FireExplosion/MuzzleFlash/HolyLightBeam/BuffAura/EnergyOrb)
/// </summary>
public static partial class VFXHelper
{
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
}
