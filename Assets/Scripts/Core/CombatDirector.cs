using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CombatDirector : MonoBehaviour
{
    public static CombatDirector Instance { get; private set; }

    public int EnemiesKilled { get; set; }
    public int AliveEnemies => aliveEnemyCount;
    public int CurrentStageWave => currentStageWave;
    public bool WaveActive => waveActive;

    private int aliveEnemyCount;
    private int currentStageWave = 1;
    private bool waveActive;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void StartCombat(int dungeonLevel)
    {
        EnemiesKilled = 0;
        aliveEnemyCount = 0;
        currentStageWave = 1;
        waveActive = false;
        _accumulatedXp = 0;
        _accumulatedGold = 0;
    }

    // ====== 批量死亡处理器 ======
    private static readonly List<EnemyController> _deathQueue = new List<EnemyController>();
    private static bool _deathProcessScheduled = false;

    // 累积奖励（副本结束时才发放）
    private int _accumulatedXp = 0;
    private int _accumulatedGold = 0;

    /// <summary>注册死亡 — 由EnemyCombatController.Die()调用，只收集不处理</summary>
    public void RegisterDeath(EnemyController enemy)
    {
        _deathQueue.Add(enemy);
        if (!_deathProcessScheduled)
        {
            _deathProcessScheduled = true;
            StartCoroutine(ProcessBatchDeaths());
        }
    }

    /// <summary>批量处理所有死亡事件 — 延迟到下一帧执行</summary>
    private IEnumerator ProcessBatchDeaths()
    {
        yield return null; // 等一帧，确保同帧所有死亡都收集完毕

        int count = _deathQueue.Count;
        if (count == 0) { _deathProcessScheduled = false; yield break; }

        var player = GameManager.Instance?.Player;
        int dungeonLevel = GameManager.Instance?.DungeonLevel ?? 1;

        // 1. 批量计算总经验、总金币
        int totalXp = 0;
        int totalGold = 0;
        bool hasBoss = false;

        for (int i = 0; i < count; i++)
        {
            var e = _deathQueue[i];
            totalXp += e.XpReward;
            totalGold += e.Type switch
            {
                EnemyType.Boss => Random.Range(80, 150),
                EnemyType.Elite => Random.Range(20, 40),
                EnemyType.Ranged => Random.Range(10, 20),
                _ => Random.Range(5, 15)
            };
            totalGold = Mathf.RoundToInt(totalGold * (1f + dungeonLevel * 0.1f)); // 缩放
            if (e.Type == EnemyType.Boss) hasBoss = true;
        }

        // 2. 累积奖励，不立即发放
        EnemiesKilled += count;
        aliveEnemyCount = Mathf.Max(0, aliveEnemyCount - count);
        if (hasBoss && GameUI.Instance != null)
            GameUI.Instance.ClearBossTarget(_deathQueue[0]);

        _accumulatedXp += totalXp;
        _accumulatedGold += totalGold;

        // 3. 一次性成就检查
        if (AchievementManager.Instance != null)
        {
            for (int i = 0; i < count; i++)
            {
                var e = _deathQueue[i];
                AchievementManager.Instance.RecordKill(e.Affixes != null && e.Affixes.Count > 0);
                if (e.Type == EnemyType.Boss) AchievementManager.Instance.RecordBossKill();
            }
        }

        // 4. Relic hook（只调第1个）
        if (RelicManager.Instance != null && count > 0)
            RelicManager.Instance.OnEnemyKilled(_deathQueue[0]);

        // 5. 死亡表现：只前3个怪播VFX，音效+Shake各1次
        AudioManager.Instance?.PlayDeath();
        CameraFollow.Shake(hasBoss ? 0.2f : 0.05f, 0.08f);

        int vfxCount = Mathf.Min(count, 3);
        for (int i = 0; i < vfxCount; i++)
        {
            var e = _deathQueue[i];
            Color deathColor = e.Type switch
            {
                EnemyType.Boss => e.BossColor,
                EnemyType.Elite => e.EliteColor,
                _ => e.EnemyColor
            };
            VFXHelper.SpawnDeathExplosion(e.transform.position, deathColor, e.Type == EnemyType.Boss ? 12 : 3);
        }

        // 6. Chain affix + Bomber（只前3个）
        for (int i = 0; i < vfxCount; i++)
        {
            var e = _deathQueue[i];
            var combat = e.Combat;
            if (combat != null) combat.ProcessDeathAffixs();
        }

        // 7. 回收到对象池
        for (int i = 0; i < count; i++)
        {
            var e = _deathQueue[i];
            if (e != null && e.gameObject != null)
                EnemyPool.Return(e.gameObject, 0.1f);
        }

        // 8. 检查波次完成
        if (aliveEnemyCount <= 0 && waveActive)
        {
            waveActive = false;
            OnWaveComplete();
        }

        _deathQueue.Clear();
        _deathProcessScheduled = false;
    }

    /// <summary>发放所有累积的奖励 — 副本结束时调用</summary>
    public void FlushAccumulatedRewards()
    {
        var player = GameManager.Instance?.Player;
        if (player == null) { _accumulatedXp = 0; _accumulatedGold = 0; return; }

        if (_accumulatedXp > 0)
        {
            player.GainXp(_accumulatedXp);
            _accumulatedXp = 0;
        }
        if (_accumulatedGold > 0)
        {
            player.Stats.AddGold(_accumulatedGold);
            _accumulatedGold = 0;
        }
    }

    // 保留旧接口兼容

    public void OnEnemyKilled(EnemyController enemy)
    {
        // 已由RegisterDeath + ProcessBatchDeaths替代
        // 保留空实现防止外部调用崩溃
    }

    public void OnWaveComplete()
    {
        // 每波结束发放累积奖励
        FlushAccumulatedRewards();

        // Endless mode: DungeonLevel is incremented in SpawnEndlessWave
        if (!GameManager.Instance.IsEndlessMode)
            GameManager.Instance.DungeonLevel++;
        currentStageWave++;
                    GameLog.Log($"Wave complete! Now at dungeon level {GameManager.Instance.DungeonLevel}");

        // Auto-save at wave completion
        if (GameManager.Instance.Player != null)
            GameManager.Instance.Player.Stats.Save();

        if (currentStageWave > GameManager.WavesPerStage)
        {
            // Endless mode: continue spawning instead of completing
            if (GameManager.Instance.IsEndlessMode)
            {
                GameManager.Instance.OnEndlessWaveComplete();
                return;
            }
            GameManager.Instance.OnDungeonComplete();
            return;
        }

        if (GameUI.Instance != null)
            GameUI.Instance.ShowWaveAnnouncement(currentStageWave);

        // Room event chance: 40% between waves (not before wave 1)
        if (Random.Range(0f, 1f) < 0.4f && currentStageWave > 1)
        {
            TriggerRoomEvent();
        }
        else
        {
            StartCoroutine(SpawnWaveAfterDelay(2f));
        }
    }

    private void TriggerRoomEvent()
    {
        var eventData = RoomEventData.RollEvent();
        OnRoomEventChosen(eventData);
    }

    private void OnRoomEventChosen(RoomEventData eventData)
    {
        var player = GameManager.Instance.Player;
        if (player == null) { StartCoroutine(SpawnWaveAfterDelay(1f)); return; }

        switch (eventData.Type)
        {
            case RoomEventType.Treasure:
                int gold = Random.Range(30, 80) * GameManager.Instance.DungeonLevel;
                player.Stats.AddGold(gold);
                VFXHelper.SpawnDamageNumber(player.transform.position, gold, false, new Color(1f, 0.85f, 0.2f));
                VFXHelper.SpawnHitParticles(player.transform.position, new Color(1f, 0.85f, 0.2f), 1);
                ShowEventToast("宝箱", $"◆ {gold} 金币");
                if (Random.Range(0f, 1f) < 0.2f)
                {
                    var item = EquipmentItem.GenerateRandom(GameManager.Instance.DungeonLevel);
                    player.Inventory.AddToBackpack(item);
                    VFXHelper.SpawnHitParticles(player.transform.position, new Color(1f, 0.85f, 0.2f), 2);
                    ShowEventToast(item.Name, item.GetStatSummary());
                }
                break;

            case RoomEventType.HealingFountain:
                int healAmt = Mathf.RoundToInt(player.Stats.TotalMaxHp * 0.5f);
                player.Heal(healAmt);
                VFXHelper.SpawnHealEffect(player.transform.position, healAmt);
                ShowEventToast("治愈之泉", $"恢复 {healAmt} 生命");
                break;

            case RoomEventType.Merchant:
                int cost = 30 + GameManager.Instance.DungeonLevel * 10;
                if (player.Stats.Gold >= cost)
                {
                    player.Stats.SpendGold(cost);
                    int bonus = Mathf.RoundToInt(player.Stats.Attack * 0.2f);
                    player.Stats.BonusAttack += bonus;
                    VFXHelper.SpawnHitParticles(player.transform.position, new Color(0.6f, 0.4f, 1f), 2);
                    VFXHelper.SpawnDamageNumber(player.transform.position, cost, false, new Color(1f, 0.5f, 0.5f));
                    ShowEventToast("旅行商人", $"+{bonus} 攻击力 ({cost}金)");
                }
                else
                {
                    ShowEventToast("旅行商人", "金币不足");
                }
                break;
        }

        StartCoroutine(SpawnWaveAfterDelay(1.5f));
    }

    private void ShowEventToast(string title, string desc)
    {
        if (GameUI.Instance != null)
            GameUI.Instance.ShowItemPickupToast(title, desc);
    }

    public IEnumerator SpawnWaveAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (GameManager.Instance == null) yield break;
        if (GameManager.Instance.IsInDungeon) SpawnWave();
    }

    public void SpawnWave()
    {
        if (GameManager.Instance == null) return;
        int stageIndex = GameManager.Instance.CurrentStageIndex;
        int dungeonLevel = GameManager.Instance.DungeonLevel;

        // Increased enemy count per stage — PoE2-style density scaling
        int baseCount;
        if (stageIndex <= 0) baseCount = 4 + dungeonLevel;        // Stage 1: more enemies
        else if (stageIndex <= 1) baseCount = 6 + dungeonLevel;   // Stage 2
        else if (stageIndex <= 3) baseCount = 8 + dungeonLevel * 2; // Stage 3-4: significantly more
        else if (stageIndex <= 5) baseCount = 10 + dungeonLevel * 2; // Stage 5-6: horde
        else baseCount = 12 + dungeonLevel * 3;                    // Stage 7-8: relentless

        aliveEnemyCount = baseCount;
        waveActive = true;

        // Daily Challenge: Horde modifier — +50% enemies
        if (DailyChallenge.ActiveModifier == DailyChallenge.ModifierType.Horde)
        {
            int extra = baseCount / 2;
            for (int i = 0; i < extra; i++) SpawnEnemy(baseCount + i, baseCount + extra);
            aliveEnemyCount += extra;
        }

        for (int i = 0; i < baseCount; i++) SpawnEnemy(i, baseCount);
    }

    private void SpawnEnemy(int index, int total)
    {
        int stageIndex = GameManager.Instance.CurrentStageIndex;
        var mapData = DungeonMapData.GetStageMap(stageIndex);

        Vector3 spawnPos = FindValidSpawnPosition(index, total, mapData);

        GameObject enemyObj = new GameObject("Enemy_" + index);
        enemyObj.transform.position = spawnPos;
        enemyObj.layer = LayerMask.NameToLayer("Enemy");

        var sr = enemyObj.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteCache.WhitePixel;

        EnemyType enemyType = DetermineEnemyType(index, total);

        var rb = enemyObj.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f; rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var col = enemyObj.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.6f, 0.6f);

        var ec = enemyObj.AddComponent<EnemyController>();
        ec.Type = enemyType;

        // Roll affixes for Elite and Boss
        if (enemyType == EnemyType.Elite)
        {
            int dungeonLevel = GameManager.Instance.DungeonLevel;
            ec.Affixes = EliteAffix.RollAffixes(dungeonLevel);
        }
        else if (enemyType == EnemyType.Boss)
        {
            ec.Affixes = EliteAffix.RollBossAffixes(GameManager.Instance.CurrentStageIndex);
        }

        if (enemyType == EnemyType.Boss && GameUI.Instance != null)
        {
            string bossName = GetBossName(stageIndex);
            if (ec.Affixes != null && ec.Affixes.Count > 0)
            {
                string affixPrefix = "";
                foreach (var a in ec.Affixes) affixPrefix += a.Name + "·";
                bossName = affixPrefix + bossName;
            }
            GameUI.Instance.SetBossTarget(ec, bossName);
        }

        EnemySprite.ApplySprite(enemyObj, enemyType, stageIndex);

        // Apply affix aura visuals
        if (ec.Affixes != null && ec.Affixes.Count > 0)
            EnemySprite.ApplyAffixAuras(enemyObj, ec.Affixes);
    }

    private Vector3 FindValidSpawnPosition(int index, int total, DungeonMapData.StageMapConfig mapData)
    {
        float maxHalfW = mapData.mapHalfWidth - 2f;
        float maxHalfH = mapData.mapHalfHeight - 2f;

        Vector3 playerPos = GameManager.Instance.Player != null ? GameManager.Instance.Player.transform.position : Vector3.zero;
        int wallMask = LayerMask.GetMask("Default");

        float angle = (360f / total) * index * Mathf.Deg2Rad;
        float minRadius = 6f;
        float maxRadius = Mathf.Min(maxHalfW, maxHalfH) * 0.7f;
        float spawnRadius = Random.Range(
            Mathf.Max(minRadius, maxRadius * 0.4f),
            Mathf.Max(minRadius + 1f, maxRadius)
        );

        Vector3 candidate = playerPos + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * spawnRadius;
        candidate.x = Mathf.Clamp(candidate.x, -maxHalfW, maxHalfW);
        candidate.y = Mathf.Clamp(candidate.y, -maxHalfH, maxHalfH);

        if (IsSpawnPositionValid(candidate, playerPos, wallMask))
            return candidate;

        for (int attempt = 0; attempt < 30; attempt++)
        {
            float r = Random.Range(minRadius, maxRadius);
            float a = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            candidate = playerPos + new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * r;
            candidate.x = Mathf.Clamp(candidate.x, -maxHalfW, maxHalfW);
            candidate.y = Mathf.Clamp(candidate.y, -maxHalfH, maxHalfH);

            if (IsSpawnPositionValid(candidate, playerPos, wallMask))
                return candidate;
        }

        float fbAngle = (360f / total) * index * Mathf.Deg2Rad;
        return playerPos + new Vector3(Mathf.Cos(fbAngle), Mathf.Sin(fbAngle)) * 3f;
    }

    private bool IsSpawnPositionValid(Vector3 spawnPos, Vector3 playerPos, int wallMask)
    {
        Vector2 dir = (spawnPos - playerPos);
        float dist = dir.magnitude;
        if (dist > 0.1f)
        {
            var hit = Physics2D.Raycast(playerPos, dir.normalized, dist, wallMask);
            if (hit.collider != null)
                return false;
        }

        if (Physics2D.OverlapCircle(spawnPos, 0.8f, wallMask))
            return false;

        return true;
    }

    private EnemyType DetermineEnemyType(int index, int total)
    {
        if (GameManager.Instance == null) return EnemyType.Melee;
        int dungeonLevel = GameManager.Instance.DungeonLevel;
        int stageIndex = GameManager.Instance.CurrentStageIndex;

        // Last enemy on final wave is always Boss
        if (index == total - 1 && currentStageWave == GameManager.WavesPerStage)
            return EnemyType.Boss;

        // Endless mode: every 5th wave spawns a Boss
        if (GameManager.Instance.IsEndlessMode && index == total - 1
            && GameManager.Instance.EndlessWave > 0
            && GameManager.Instance.EndlessWave % 5 == 0)
            return EnemyType.Boss;

        // Mini-boss: 2nd wave final enemy becomes Elite on harder stages
        if (index == total - 1 && currentStageWave == GameManager.WavesPerStage - 1 && stageIndex >= 2)
            return EnemyType.Elite;

        // Stage 2+: introduce ranged enemies and special types
        if (dungeonLevel < 4)
        {
            float roll = Random.Range(0f, 1f);
            float rangedChance = Mathf.Min(0.30f, 0.12f + (dungeonLevel - 2) * 0.05f);
            float specialChance = Mathf.Min(0.15f, 0.05f + (dungeonLevel - 2) * 0.03f);
            if (roll < specialChance) return RollSpecialEnemyType();
            if (roll < specialChance + rangedChance) return EnemyType.Ranged;
            return EnemyType.Melee;
        }

        // Stage 4+: introduce elites — increased chance
        float eliteChance = Mathf.Min(0.25f, 0.10f + (dungeonLevel - 4) * 0.03f + stageIndex * 0.02f);
        if (Random.Range(0f, 1f) < eliteChance) return EnemyType.Elite;

        // Special enemies appear more frequently
        float specialChance2 = Mathf.Min(0.20f, 0.10f + (dungeonLevel - 4) * 0.02f);
        if (Random.Range(0f, 1f) < specialChance2) return RollSpecialEnemyType();

        // Increased ranged proportion
        float rangedChance2 = Mathf.Min(0.40f, 0.25f + (dungeonLevel - 4) * 0.03f);
        if (Random.Range(0f, 1f) < rangedChance2) return EnemyType.Ranged;
        return EnemyType.Melee;
    }

    /// <summary>Roll a random special enemy type.</summary>
    private static EnemyType RollSpecialEnemyType()
    {
        return Random.Range(0, 4) switch
        {
            0 => EnemyType.Bomber,
            1 => EnemyType.Charger,
            2 => EnemyType.Healer,
            _ => EnemyType.Shielder
        };
    }

    private static readonly string[] BossNames =
    {
        "树精长老",
        "矿洞巨魔",
        "冰霜女妖",
        "炎魔将军",
        "亡灵领主",
        "远古幼龙",
        "虚空使者",
        "魔王·深渊"
    };

    private string GetBossName(int stageIndex)
    {
        return BossNames[Mathf.Clamp(stageIndex, 0, BossNames.Length - 1)];
    }

    public void StopCombat()
    {
        waveActive = false;
    }

    /// <summary>Track an enemy spawned outside the normal wave (affix minions, trap spawns).</summary>
    public void AddSpawnedEnemy()
    {
        aliveEnemyCount++;
    }

    private void SpawnRelicPickup(Vector3 position)
    {
        int stageIndex = GameManager.Instance != null ? GameManager.Instance.CurrentStageIndex : 0;
        var choices = RelicData.RollRelicChoice(3, stageIndex);

        Vector3 offset = (Vector3)(Random.insideUnitCircle * 0.5f);
        GameObject relicObj = new GameObject("RelicPickup");
        relicObj.transform.position = position + offset;

        var sr = relicObj.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteCache.WhitePixel;
        sr.color = new Color(1f, 0.85f, 0.1f, 0.9f);
        sr.sortingOrder = 4;
        relicObj.transform.localScale = new Vector3(3f, 3f, 1f);

        // Glow
        GameObject glowObj = new GameObject("Glow");
        glowObj.transform.SetParent(relicObj.transform, false);
        glowObj.transform.localPosition = Vector3.zero;
        var glowSr = glowObj.AddComponent<SpriteRenderer>();
        glowSr.sprite = SpriteCache.WhitePixel;
        glowSr.color = new Color(1f, 0.85f, 0.1f, 0.2f);
        glowSr.sortingOrder = 3;
        glowObj.transform.localScale = new Vector3(2f, 2f, 1f);

        var rb = relicObj.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.isKinematic = true;

        var col = relicObj.AddComponent<CircleCollider2D>();
        col.radius = 0.8f;
        col.isTrigger = true;

        var pickup = relicObj.AddComponent<RelicPickup>();
        pickup.SetRelicChoices(choices, stageIndex);
    }
}
