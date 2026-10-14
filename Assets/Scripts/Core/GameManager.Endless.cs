using UnityEngine;
using System.Collections;

/// <summary>
/// GameManager partial — Endless mode logic
/// </summary>
public partial class GameManager
{
// IsEndlessMode is now a wrapper around CurrentState — see above.
    public int EndlessWave { get; private set; }

    public void StartEndlessMode()
    {
        // 同步先置状态, 重活分帧由协程完成
        CurrentState = GameState.Endless;
        EndlessWave = 0;
        DungeonLevel = 5; // Fixed starting difficulty
        StartCoroutine(StartEndlessModeCo());
    }

    private System.Collections.IEnumerator StartEndlessModeCo()
    {
        LoadingScreen.Show("正在进入无尽模式...");

        // Clean up hub UI
        DestroyUIComponent<HubUI>();

        // 副本中隐藏TopNavBar
        if (TopNavBar.Instance != null) TopNavBar.Instance.HideBar();

        // Re-enable gameplay HUD
        // 自愈：Canvas/GameUI/HUD可能已被意外销毁，确保存在
        if (GameUI.Instance == null || GameUI.Instance.HpBar == null) BuildUI();
        if (GameUI.Instance != null)
        {
            GameUI.Instance.ShowHubMode(false);
            GameUI.Instance.SetHUDVisible(true);
        }
        LoadingScreen.SetProgress(0.15f);
        yield return null;

        // Clear leftovers
        SceneRegistry.ClearAll();
        VFXPool.ClearAll();
        EnemyPool.ClearAll();
        EnemyHealthBar.CleanupAll();

        // 无尽模式随机地图 — Stage 1-7 每次不同
        int randomStage = Random.Range(1, 8);
        CurrentStageIndex = randomStage;

        // Rebuild dungeon visuals
        // 延迟销毁旧地图，确保渲染线程完成当前帧的GPU资源释放
        if (DungeonVisuals.Instance != null)
        {
            var oldDv = DungeonVisuals.Instance.gameObject;
            DungeonVisuals.Instance = null;
            oldDv.SetActive(false);
            Destroy(oldDv);
        }
        LoadingScreen.SetProgress(0.3f);
        yield return null;

        CreateDungeonVisuals();
        LoadingScreen.SetProgress(0.6f);
        yield return null;

        // ★ 核心安全：确保玩家存在并可见
        EnsurePlayerExists();
        if (Player != null)
        {
            Player.CharSprite?.ResetCharacterSprite();
            Player.IsDead = false;
            Player.StateMachine?.Reset();
            Player.transform.position = Vector3.zero;
            Player.Stats.Hp = Player.Stats.TotalMaxHp;
            Player.Stats.Runtime.Potions = PlayerRuntimeStats.MaxPotions;
            Player.ResetRunLoot();
            Player.RollRunMutationPool();
        }

        // 重置摄像机
        ResetCamera();
        var mapData = DungeonMapData.GetStageMap(randomStage);
        if (CameraFollow.Instance != null)
        {
            CameraFollow.Instance.ForcedOrthoSize = mapData.cameraOrthoSize;
            CameraFollow.Instance.MapHalfWidth = mapData.mapHalfWidth;
            CameraFollow.Instance.MapHalfHeight = mapData.mapHalfHeight;
            CameraFollow.Instance.ClampToMap = true;
            CameraFollow.Instance.enabled = true;
        }
        LoadingScreen.SetProgress(0.8f);
        yield return null;

        // Start combat system
        if (CombatDirector.Instance != null)
            CombatDirector.Instance.StartCombat(DungeonLevel);

        // Create pet for endless mode
        if (Player != null && PetDataManager.HasActivePet())
            CreatePet(Player.transform);

        // 组队模式：创建AI队友（与普通副本一致）
        DestroyTeammates();
        if (IsTeamMode && Player != null)
            PetTeammates.CreateTeammates(Player.transform, CurrentStageIndex);

        LoadingScreen.SetProgress(1f);
        yield return null;
        LoadingScreen.Hide();

        StartCoroutine(SpawnEndlessWaveAfterDelay(2f));
    }

    private System.Collections.IEnumerator SpawnEndlessWaveAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        SpawnEndlessWave();
    }

    private void SpawnEndlessWave()
    {
        EndlessWave++;
        DungeonLevel += 1;

        if (GameUI.Instance != null)
            GameUI.Instance.ShowWaveAnnouncement(EndlessWave);

        // Use CombatDirector's spawn system directly
        if (CombatDirector.Instance != null)
                        CombatDirector.Instance.SpawnWave();

        #if UNITY_EDITOR
                                GameLog.Log($"[Endless] Wave {EndlessWave}, DungeonLevel {DungeonLevel}");
        #endif
                }

    public void OnEndlessWaveComplete()
    {
        // 清理战场残留（但不重建地图，保持流畅）
        SceneRegistry.ClearAll();
        VFXPool.ClearAll();

        // Boss wave (every 5th wave) — drop equipment
        bool isBossWave = EndlessWave > 0 && EndlessWave % 5 == 0;

        if (isBossWave && Player != null)
        {
            DropBossEquipment(DungeonLevel, Player.transform.position + Vector3.up * 2f);
        }

        // 每波奖励金币+经验
        if (Player != null)
        {
            int bonus = 20 + EndlessWave * 10;
            Player.Stats.AddGold(bonus);
            int xpBonus = 30 + EndlessWave * 15;
            Player.GainXp(xpBonus);
            Player.Stats.Save();
        }

        // 发放累积奖励
        if (CombatDirector.Instance != null)
            CombatDirector.Instance.FlushAccumulatedRewards();

        // Check if player is dead
        if (Player == null || Player.IsDead)
        {
            if (Player != null && EndlessWave > 0)
            {
                if (LeaderboardUI.Instance == null) gameObject.AddComponent<LeaderboardUI>();
                LeaderboardUI.SubmitScore(EndlessWave, (int)Player.HeroClass, Player.Stats.Level);
            }
            CurrentState = GameState.Hub;
            ShowHub();
            return;
        }

        // 重置玩家位置和血量
        if (Player != null)
        {
            Player.transform.position = Vector3.zero;
            Player.Stats.Hp = Player.Stats.TotalMaxHp;
        }

        // 重建AI队友（如果有）
        DestroyTeammates();
        if (IsTeamMode && Player != null)
            PetTeammates.CreateTeammates(Player.transform, CurrentStageIndex);

        // 重置CombatDirector状态，确保下一波能正确生成
        if (CombatDirector.Instance != null)
        {
            CombatDirector.Instance.StartCombat(DungeonLevel);
            // currentStageWave重置为0，这样OnWaveComplete不会立即触发OnEndlessWaveComplete
        }

        // 继续下一波
        StartCoroutine(SpawnEndlessWaveAfterDelay(2f));
    }
}
