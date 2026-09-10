using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Single source of truth for the current game phase.
/// Replaces the previous multiple bool flags (IsInDungeon, IsInHub, etc.).
/// </summary>
public enum GameState
{
    Title,
    CharacterSelect,
    Hub,
    Dungeon,
    Endless,
    GameOver
}

public partial class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public const int WavesPerStage = 4;

    private static Font uiFont;
    private static bool fontLoadingAttempted = false;
    /// <summary>
    /// Returns a clean, Chinese-compatible TrueType font.
    /// Tries: Microsoft YaHei → Arial (builtin) → Resources.Load fallback.
    /// </summary>
    public static Font GetUIFont()
    {
        if (uiFont != null) return uiFont;
        if (fontLoadingAttempted) return Resources.GetBuiltinResource<Font>("Arial.ttf") ?? Resources.GetBuiltinResource<Font>("Arial");

        fontLoadingAttempted = true;
        // Try OS Chinese fonts first (Windows/macOS/Linux)
        string[] osFonts = { "Microsoft YaHei", "微软雅黑", "SimHei", "PingFang SC", "Noto Sans CJK SC", "WenQuanYi Micro Hei" };
        foreach (var name in osFonts)
        {
            var f = Font.CreateDynamicFontFromOSFont(name, 16);
            if (f != null && f.fontSize > 0)
            {
                uiFont = f;
                return uiFont;
            }
        }
        // Try Unity builtin Arial (TrueType, handles CJK on most platforms)
        var arial = Resources.GetBuiltinResource<Font>("Arial.ttf") ?? Resources.GetBuiltinResource<Font>("Arial");
        if (arial != null)
        {
            uiFont = arial;
            return uiFont;
        }
        // Fallback: try loading from Resources
        uiFont = Resources.Load<Font>(ResourcePaths.DefaultFont);
        return uiFont;
    }

    public PlayerController Player { get; private set; }
    private static PlayerController _playerBackup; // 静态备份，防止引用丢失
    public int DungeonLevel { get; set; } = 1;
    private float _dungeonStartTime; // 副本开始时间, 用于服务器结算速度奖励
    public int CurrentStageIndex { get; private set; }
    public int EnemiesKilled => CombatDirector.Instance != null ? CombatDirector.Instance.EnemiesKilled : 0;

    /// <summary>Single source of truth for the current game phase.</summary>
    public GameState CurrentState { get; set; } = GameState.Title;

    // Backward-compatible bool wrappers (read-only, derived from CurrentState)
    public bool IsGameOver => CurrentState == GameState.GameOver;
    public bool IsInDungeon => CurrentState == GameState.Dungeon || CurrentState == GameState.Endless;
    public bool IsInHub => CurrentState == GameState.Hub;
    public bool IsEndlessMode => CurrentState == GameState.Endless;
    public bool IsPaused { get; set; }

    public int AliveEnemies => CombatDirector.Instance != null ? CombatDirector.Instance.AliveEnemies : 0;
    public int CurrentStageWave => CombatDirector.Instance != null ? CombatDirector.Instance.CurrentStageWave : 0;

    private static Canvas _cachedCanvas;
    private HeroClass selectedClass;
    private bool charSelectCameFromHub;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        // 仅添加组件，不加载任何数据 — 等OnSlotLoaded时从服务器同步
        if (FindObjectOfType<RuntimePlayerData>() == null)
            gameObject.AddComponent<RuntimePlayerData>();
        if (FindObjectOfType<DataSyncService>() == null)
            gameObject.AddComponent<DataSyncService>();
        // ActivePetType will be loaded when a slot is selected (in OnSlotLoaded / StartDungeon)
        gameObject.AddComponent<CombatDirector>();
        gameObject.AddComponent<UIManager>();
        gameObject.AddComponent<AchievementManager>();
        var audioMgr = gameObject.AddComponent<AudioManager>();
        audioMgr.LoadSavedVolumes();

        // 云端存档管理器 (单例, 跨场景持久)
        if (FindObjectOfType<CloudSaveManager>() == null)
            gameObject.AddComponent<CloudSaveManager>();

        // 游戏配置管理器 (从服务器拉取属性数据)
        if (FindObjectOfType<GameConfigManager>() == null)
            gameObject.AddComponent<GameConfigManager>();

        // 监听会话过期事件
        if (CloudSaveManager.Instance != null)
            CloudSaveManager.Instance.OnSessionExpired += OnSessionExpired;

        // 强制清理：无论上次是否正常退出，都确保场景干净
        CleanupAllDungeonResiduals();
    }

    /// <summary>启动时强制清理所有副本残留物</summary>
    /// <summary>重置摄像机</summary>
    public void ResetCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            cam = camObj.AddComponent<Camera>();
            camObj.tag = "MainCamera";
        }
        cam.clearFlags = CameraClearFlags.SolidColor;
        // 使用与地板类似的暗色，避免黑色方块
        cam.backgroundColor = new Color(0.12f, 0.10f, 0.15f);
        cam.orthographic = true;
        cam.orthographicSize = 10f;
        if (cam.GetComponent<CameraFollow>() == null)
            cam.gameObject.AddComponent<CameraFollow>();
    }

    /// <summary>确保玩家存在 — 核心安全方法</summary>
    public void EnsurePlayerExists()
    {
        // 从静态备份恢复
        if (Player == null && _playerBackup != null && _playerBackup.gameObject != null)
        {
            Player = _playerBackup;
            GameLog.Log("[GameManager] Player recovered from backup");
        }

        // 仍然为null，从场景查找
        if (Player == null)
        {
            Player = FindObjectOfType<PlayerController>();
            if (Player != null)
                GameLog.Log("[GameManager] Player found in scene");
        }

        // 还是null，重新创建
        if (Player == null)
        {
            HeroClass cls = selectedClass;
            if (cls == HeroClass.Warrior && Stats.GetSavedClass() != HeroClass.Warrior)
                cls = Stats.GetSavedClass();
            GameLog.Log($"[GameManager] Player is NULL, creating new ({cls})");
            CreatePlayer(cls);
            if (Player != null)
                Player.Stats.LoadSaved();
        }

        // 确保 GameObject 激活，重置位置，设置摄像机
        if (Player != null)
        {
            Player.gameObject.SetActive(true);
            Player.transform.position = Vector3.zero;
            // 设置摄像机目标并立即定位
            if (CameraFollow.Instance != null)
            {
                CameraFollow.Instance.Target = Player.transform;
                CameraFollow.Instance.SnapToTarget();
            }
            GameLog.Log($"[GameManager] Player ensured: active={Player.gameObject.activeSelf}");
        }
    }

    private void CleanupAllDungeonResiduals()
    {
        // 1. 销毁残留的 DungeonVisuals
        if (DungeonVisuals.Instance != null)
        {
            var dv = DungeonVisuals.Instance.gameObject;
            DungeonVisuals.Instance = null;
            Destroy(dv);
        }
        // 2. 清理 SceneRegistry 中所有注册对象
        SceneRegistry.ClearAll();
        // 3. 按名称查找并销毁可能残留的地图根物体（兜底）
        var mapRoot = GameObject.Find("DungeonVisuals");
        if (mapRoot != null) { mapRoot.SetActive(false); Destroy(mapRoot); }
        // 3b. 清理残留的Vignette/Fog/HPBar（可能挂在Camera上，不随DV销毁）
        // 只查Camera子物体，不扫描整个场景
        var cam = Camera.main;
        if (cam != null)
        {
            for (int i = cam.transform.childCount - 1; i >= 0; i--)
            {
                var child = cam.transform.GetChild(i);
                if (child.name == "Vignette" || child.name == "Fog_0" || child.name == "Fog_1")
                    { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            }
        }
        // 4. 清理场景对象 — 使用SceneRegistry替代FindObjectsOfType
        SceneRegistry.ClearAll();
        // 5. 清理 VFX 池 + 敌人池
        VFXPool.ClearAll();
        EnemyPool.ClearAll();
        // 6. 清理残留的HPBar根对象 — 使用EnemyHealthBar.CleanupAll()替代FindObjectsOfType
        EnemyHealthBar.CleanupAll();
        // 7. 触发资源卸载
        Resources.UnloadUnusedAssets();
                    GameLog.Log("[GameManager] 启动清理完成");
    }

    /// <summary>Token过期时: 清理登录状态, 回到标题页</summary>
    private void OnSessionExpired()
    {
        if (CloudSaveManager.Instance != null)
            CloudSaveManager.Instance.Logout();
                    GameLog.Log("[GM] 会话过期, 返回标题页");
        ShowTitleScreen();
    }

    private void Start()
    {
        Canvas canvas = EnsureCanvas();

        // 先恢复服务器选择 (在加载配置之前)
        string lastServer = ServerRegionManager.GetLastServerId();
        if (!string.IsNullOrEmpty(lastServer))
            ServerRegionManager.SelectServer(lastServer);

        // 异步拉取游戏配置 (使用正确的服务器地址)
        if (GameConfigManager.Instance != null)
            StartCoroutine(GameConfigManager.Instance.LoadConfigCo());

        // Destroy any stale Player objects left in the scene
        foreach (var go in GameObject.FindGameObjectsWithTag("Player"))
            Destroy(go);

        // 已登录: 检查是否已选过服务器
        if (CloudSaveManager.Instance != null && CloudSaveManager.Instance.IsLoggedIn)
        {
            if (!string.IsNullOrEmpty(lastServer))
            {
                // 已选过服务器, 自动连接
                ShowTitleScreen();
            }
            else
            {
                // 首次登录, 显示服务器选择
                ShowServerSelect();
            }
        }
        else
            ShowAuthPageFirst();
    }

    public void ShowTitleScreen()
    {
        CurrentState = GameState.Title;
        charSelectCameFromHub = false;
        AudioManager.Instance?.PlayBGM(AudioManager.BGMType.Title);

        // Save before cleanup
        if (Player != null) Player.Stats.Save();
        OfflineRewards.RecordLogout();

        // Clean up old player if any
        if (Player != null) Destroy(Player.gameObject);
        Player = null;

        // Clean up stale UI from other pages
        DestroyUIComponent<CharacterSelectUI>();
        DestroyUIComponent<CharacterSlotUI>();
        DestroyUIComponent<HubUI>();

        // Hide gameplay HUD on full-screen pages
        if (GameUI.Instance != null) GameUI.Instance.SetHUDVisible(false);

        // Show title screen
        if (TitleScreen.Instance == null)
            gameObject.AddComponent<TitleScreen>();
        TitleScreen.Instance.Show();
    }

    /// <summary>
    /// Show the cloud auth page as the first screen (before title).
    /// Must login before entering the game.
    /// </summary>
    public void ShowAuthPageFirst()
    {
        Canvas canvas = EnsureCanvas();
        CloudAuthUI authUI = GetComponent<CloudAuthUI>();
        if (authUI == null)
            authUI = gameObject.AddComponent<CloudAuthUI>();
        authUI.ShowAuthPage(canvas.transform, GetUIFont());
    }

    /// <summary>显示服务器选择页面</summary>
    public void ShowServerSelect()
    {
        Canvas canvas = EnsureCanvas();
        ServerSelectUI serverUI = GetComponent<ServerSelectUI>();
        if (serverUI == null)
            serverUI = gameObject.AddComponent<ServerSelectUI>();
        serverUI.Show(GetUIFont());
    }

    /// <summary>
    /// Called from TitleScreen when "开始游戏" is pressed.
    /// Shows character slot selection (empty slots for new characters).
    /// </summary>
    public void OnTitleStartNewGame()
    {
        charSelectCameFromHub = false;
        if (TitleScreen.Instance != null) TitleScreen.Instance.Hide();
        ShowCharacterSlots();
    }


    /// <summary>
    /// Show the character slot selection page.
    /// </summary>
    public void ShowCharacterSlots()
    {
        // Clean up old player if any
        if (Player != null) Destroy(Player.gameObject);
        Player = null;
        if (GameUI.Instance != null) GameUI.Instance.SetHUDVisible(false);

        DestroyUIComponent<CharacterSelectUI>();
        EnsureCharacterSlotUI().Show();
    }

    private CharacterSlotUI EnsureCharacterSlotUI()
    {
        var ui = GetComponent<CharacterSlotUI>();
        if (ui == null) ui = gameObject.AddComponent<CharacterSlotUI>();
        return ui;
    }

    /// <summary>
    /// Called from CharacterSlotUI when a slot is selected and save data loaded.
    /// Creates player from loaded data and enters Hub.
    /// </summary>
    public void OnSlotLoaded()
    {
        if (Stats.HasSaveData())
        {
            HeroClass savedClass = Stats.GetSavedClass();
            selectedClass = savedClass;
            CreatePlayer(savedClass);
            // LoadSaved 必须在 CreatePlayer 之后立即调用
            // CreatePlayer 中 InitializeClass 会创建空的 Inventory
            // LoadSaved 会从 PlayerPrefs 恢复装备到这个空 Inventory
            Player.Stats.LoadSaved();

            // 验证装备是否正确加载
                        GameLog.Log($"[OnSlotLoaded] Backpack: {Player.Inventory.Backpack.Count}, Equipped: {Player.Inventory.Equipped.Count}");
            // Clean up legacy free pet + load pet data for this slot
            PetDataManager.CleanupLegacyFreePet();
            ActivePetType = PetDataManager.GetActivePet();

            // 使用DataSyncService从服务器同步
            if (DataSyncService.Instance != null)
            {
                StartCoroutine(DataSyncService.Instance.SyncFromServerOrCache((success, msg) =>
                {
                    if (!success && !string.IsNullOrEmpty(msg))
                        GameLog.LogWarning($"[Boot] {msg}");
                    BuildUI();
                    CreateDungeonVisuals();
                    ShowHub();
                }));
            }
            else
            {
                // fallback: 离线模式
                RuntimePlayerData.Instance?.RefreshFromCache(LocalSettingsManager.GetCachedStage());
                BuildUI();
                CreateDungeonVisuals();
                ShowHub();
            }
        }
        else
        {
            ShowCharacterSelect();
        }
    }

    /// <summary>
    /// Ensures a Canvas exists in the scene. Called before any UI component needs it.
    /// Also ensures EventSystem exists (required for UI button clicks to work).
    /// </summary>
    public static Canvas EnsureCanvas()
    {
        // Fast path: return cached canvas if still valid
        if (_cachedCanvas != null) return _cachedCanvas;

        // Ensure a Camera exists with CameraFollow for 2D gameplay
        Camera cam = FindObjectOfType<Camera>();
        if (cam == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.1f, 1f);
            cam.orthographic = true;
            cam.orthographicSize = 10.0f;
            camObj.AddComponent<AudioListener>();
            camObj.tag = "MainCamera";
            camObj.AddComponent<CameraFollow>();
        }
        else
        {
            if (cam.GetComponent<CameraFollow>() == null)
                cam.gameObject.AddComponent<CameraFollow>();
            if (cam.GetComponent<AudioListener>() == null)
                cam.gameObject.AddComponent<AudioListener>();
            if (!cam.orthographic)
            {
                cam.orthographic = true;
                cam.orthographicSize = 10.0f;
            }
        }

        // Ensure EventSystem exists (required for all UI interaction)
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas != null) { _cachedCanvas = canvas; return canvas; }

        GameObject canvasObj = new GameObject("Canvas");
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObj.AddComponent<GraphicRaycaster>();
        _cachedCanvas = canvas;
        return canvas;
    }

    // === GAME FLOW ===

    public void ResetAndShowCharacterSelect()
    {
        // Don't destroy player or clear save yet — only do that if user
        // actually confirms a new class. This way "return" goes back to Hub.
        charSelectCameFromHub = true;

        // Clean up old UI
        DestroyUIComponent<HubUI>();

        var oldCharSelect = GetComponent<CharacterSelectUI>();
        if (oldCharSelect != null) Destroy(oldCharSelect);

        ShowCharacterSelect();
    }

    public void ShowCharacterSelect()
    {
        CurrentState = GameState.CharacterSelect;

        // Only destroy player if we are NOT coming from Hub
        // (Hub → 重新选择 keeps the player alive so returning works)
        if (!charSelectCameFromHub)
        {
            if (Player != null) Destroy(Player.gameObject);
            Player = null;
        }

        // Clean up old UI components
        DestroyUIComponent<CharacterSelectUI>();
        DestroyUIComponent<HubUI>();
        DestroyUIComponent<TitleScreen>();

        // Hide gameplay HUD on full-screen pages
        if (GameUI.Instance != null) GameUI.Instance.SetHUDVisible(false);

        // Show character select
        var charSelect = gameObject.AddComponent<CharacterSelectUI>();
        charSelect.Show();
    }

    public void OnClassSelected(HeroClass heroClass)
    {
        selectedClass = heroClass;
        Destroy(GetComponent<CharacterSelectUI>());

        // If re-selecting from Hub, clear old save and destroy old player
        if (charSelectCameFromHub)
        {
            Stats.ClearSave();
            if (Player != null) Destroy(Player.gameObject);
            Player = null;
        }

        charSelectCameFromHub = false;

        // Create player with selected class
        CreatePlayer(heroClass);
        // 新角色初始化: 赠送1个技能点 + 保存
        Player.Stats.SkillPoints = 1;
        Player.Stats.Save();
        BuildUI();
        CreateDungeonVisuals();

        // Go to hub
        ShowHub();
    }

    public void ShowHub()
    {
        CurrentState = GameState.Hub;
        Time.timeScale = 1f;
        GameLog.Log($"[ShowHub] START Player={(Player != null ? "exists" : "NULL")} active={(Player != null && Player.gameObject.activeSelf)}");

        // 隐藏GameOver和Pause面板
        if (GameUI.Instance != null)
        {
            if (GameUI.Instance.GameOverPanel != null) GameUI.Instance.GameOverPanel.SetActive(false);
            if (GameUI.Instance.PausePanel != null) GameUI.Instance.PausePanel.SetActive(false);
        }

        AudioManager.Instance?.PlayBGM(AudioManager.BGMType.Hub);

        // Save progress (HighestStage由服务器管理，不在此保存)
        if (Player != null) Player.Stats.Save();

        // 同步本地邮件到服务器 (跨设备支持)
        if (CloudSaveManager.Instance != null && CloudSaveManager.Instance.IsLoggedIn)
            CloudSaveManager.Instance.StartCoroutine(CloudSaveManager.Instance.SyncLocalMailsToServerCo());

        // Disable auto-battle when returning to hub
        if (Player != null && Player.Combat != null)
            Player.Combat.IsAutoBattle = false;

        // Destroy pet when returning to hub
        DestroyPets();
        DestroyTeammates();

        // Disable camera and hide player in hub
        if (CameraFollow.Instance != null)
        {
            CameraFollow.Instance.enabled = false;
            CameraFollow.Instance.ClampToMap = false;
        }
        if (Player != null)
        {
            Player.transform.position = new Vector3(9999, 9999, 0);
        }

        // Stop combat and destroy all enemies
        if (CombatDirector.Instance != null)
            CombatDirector.Instance.StopCombat();
        SceneRegistry.ClearAll();
        VFXPool.ClearAll();
        EnemyPool.ClearAll();

        // 延迟销毁DungeonVisuals，确保渲染线程完成当前帧的GPU资源释放
        if (DungeonVisuals.Instance != null)
        {
            var dvObj = DungeonVisuals.Instance.gameObject;
            DungeonVisuals.Instance = null;
            dvObj.SetActive(false); // 立即隐藏，防止延迟销毁期间渲染黑块
            Destroy(dvObj);
        }

        // Clean up stale UI from other pages
        DestroyUIComponent<TitleScreen>();
        DestroyUIComponent<CharacterSelectUI>();
        DestroyUIComponent<CharacterSlotUI>();

        // Show hub panel with stage select, shop, skill tree buttons
        if (HubUI.Instance == null)
            gameObject.AddComponent<HubUI>();
        HubUI.Instance.Show();
        GameLog.Log($"[ShowHub] DONE Player.active={(Player != null && Player.gameObject.activeSelf)} state={CurrentState}");

        // First-time hub tutorial — disabled

        // Ensure meta-game UI components exist for static method calls (ReportProgress, SubmitScore)
        if (DailyTaskUI.Instance == null) gameObject.AddComponent<DailyTaskUI>();
        if (LeaderboardUI.Instance == null) gameObject.AddComponent<LeaderboardUI>();

        // Hide gameplay HUD in hub (ShowHubMode handles the rest)
        if (GameUI.Instance != null) GameUI.Instance.ShowHubMode(true);
    }

    public void StartDungeon(int stageIndex)
    {
        CurrentStageIndex = stageIndex;
        DungeonLevel = (stageIndex + 1);
        CurrentState = GameState.Dungeon;
        Time.timeScale = 1f;
        IsPaused = false;
        GameLog.Log($"[StartDungeon] stage={stageIndex} Player={(Player != null ? Player.gameObject.name : "NULL")} active={(Player != null && Player.gameObject.activeSelf)}");
        AudioManager.Instance?.PlayBGM(AudioManager.BGMType.Battle);

        // Reset achievement run state for new dungeon
        if (AchievementManager.Instance != null)
            AchievementManager.Instance.ResetRunState();

        // Force-destroy all hub/sub-page UI components (OnDestroy cleans up their panels)
        DestroyUIComponent<HubUI>();
        DestroyUIComponent<StageSelectUI>();
        DestroyUIComponent<ShopUI>();
        DestroyUIComponent<SkillTreeUI>();
        DestroyUIComponent<EquipUI>();
        DestroyUIComponent<TitleScreen>();
        DestroyUIComponent<CharacterSelectUI>();

        // 副本中隐藏TopNavBar（导航栏只在UI面板显示）
        if (TopNavBar.Instance != null) TopNavBar.Instance.HideBar();

        // Re-enable gameplay HUD for dungeon
        if (GameUI.Instance != null)
        {
            GameUI.Instance.ShowHubMode(false);
            GameUI.Instance.SetHUDVisible(true);
        }

        // Clean up any leftover from previous runs
        SceneRegistry.ClearAll();
        VFXPool.ClearAll();
        EnemyPool.ClearAll();

        // 清理残留HPBar根对象
        EnemyHealthBar.CleanupAll();

        // 延迟销毁旧地图，确保渲染线程完成当前帧的GPU资源释放
        if (DungeonVisuals.Instance != null)
        {
            var oldDv = DungeonVisuals.Instance.gameObject;
            DungeonVisuals.Instance = null;
            oldDv.SetActive(false);
            Destroy(oldDv);
        }
        CreateDungeonVisuals();

        // ★ 核心安全：确保玩家存在并可见 — 先移动玩家再设置相机
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
            // 确保技能数据已加载
            if (Player.Skills != null && Player.SkillCtrl != null)
                Player.SkillCtrl.SetSkills(Player.Skills.ToArray());
            // 确保Combat引用有效
            if (Player.Combat != null && Player.SkillCtrl != null)
                Player.SkillCtrl.SetCombat(Player.Combat);
            GameLog.Log($"[StartDungeon] Player ready: IsDead={Player.IsDead} State={Player.StateMachine?.CurrentState} Skills={Player.Skills?.Count}");
        }

        // 重置摄像机 — 先设置位置再启用跟随
        ResetCamera();
        if (Camera.main != null)
            Camera.main.transform.position = new Vector3(0, 0, -10);
        var mapData = DungeonMapData.GetStageMap(stageIndex);
        if (CameraFollow.Instance != null)
        {
            CameraFollow.Instance.ForcedOrthoSize = mapData.cameraOrthoSize;
            CameraFollow.Instance.MapHalfWidth = mapData.mapHalfWidth;
            CameraFollow.Instance.MapHalfHeight = mapData.mapHalfHeight;
            CameraFollow.Instance.ClampToMap = true;
            CameraFollow.Instance.enabled = true;
            CameraFollow.Instance.SnapToTarget();
        }

        // 清理上次副本的VFX对象池, 避免积累
        VFXPool.ClearAll();

        // 预热VFX对象池, 避免首帧卡顿
        VFXPool.PrewarmAll();

        // Delegate combat initialization to CombatDirector
        if (CombatDirector.Instance != null)
        {
            CombatDirector.Instance.StartCombat(DungeonLevel);
            StartCoroutine(CombatDirector.Instance.SpawnWaveAfterDelay(1f));
        }
        _dungeonStartTime = Time.time; // 记录副本开始时间用于服务器结算

        // Recreate pets (in case they were destroyed by previous dungeon clear/Hub)
        if (Player != null && PetDataManager.HasActivePet())
            CreatePet(Player.transform);

        // 组队模式：创建AI队友（与普通副本一致）
        DestroyTeammates();
        if (IsTeamMode && Player != null)
            PetTeammates.CreateTeammates(Player.transform, stageIndex);

        // First-time dungeon tutorial — disabled

        // Trigger stage start dialogue
        if (DialogueSystem.Instance == null) gameObject.AddComponent<DialogueSystem>();
        DialogueSystem.Instance.ShowStageStartDialogue(stageIndex);

        GameLog.Log($"[StartDungeon] DONE State={CurrentState} Player.active={(Player != null && Player.gameObject.activeSelf)} DV={(DungeonVisuals.Instance != null ? "exists" : "NULL")}");
    }

    private DungeonRewardFlow _rewardFlow;
    private DungeonRewardFlow RewardFlow => _rewardFlow ??= gameObject.GetComponent<DungeonRewardFlow>() ?? gameObject.AddComponent<DungeonRewardFlow>();

    public void OnDungeonComplete()
    {
        CurrentState = GameState.Hub;

        if (Player != null && Player.Combat != null)
            Player.Combat.IsAutoBattle = false;

        PetTeammates.DestroyPets();
        PetTeammates.DestroyTeammates();

        int hpRemaining = Player != null ? Player.Stats.Hp : 0;
        int hpMax = Player != null ? Player.Stats.TotalMaxHp : 1;
        int enemiesKilled = CombatDirector.Instance != null ? CombatDirector.Instance.EnemiesKilled : 0;
        float timeUsed = Time.time - _dungeonStartTime;

        // 立即应用经验+金币+掉落（不等服务器，防止协程未完成就退出导致丢失）
        if (Player != null)
        {
            int enemiesKilledForReward = CombatDirector.Instance != null ? CombatDirector.Instance.EnemiesKilled : 0;
            int instantXp = 50 + CurrentStageIndex * 30 + enemiesKilledForReward * 5;
            int instantGold = 0;
            if (StageSelectUI.Stages != null && CurrentStageIndex >= 0 && CurrentStageIndex < StageSelectUI.Stages.Length)
                instantGold = StageSelectUI.Stages[CurrentStageIndex].goldReward;
            if (hpRemaining >= hpMax && hpMax > 0)
                instantGold += Mathf.RoundToInt(instantGold * 0.5f);

            Player.GainXp(instantXp);
            Player.Stats.AddGold(instantGold);
            Player.RunGoldEarned += instantGold;
            Player.RunXpEarned += instantXp;

            int dungeonLevel = CurrentStageIndex + 1;
            int dropCount = 3;
            for (int i = 0; i < dropCount; i++)
            {
                float roll = Random.Range(0f, 1f);
                int rarity = roll < 0.02f ? 3 : roll < 0.10f ? 2 : roll < 0.30f ? 1 : 0;
                var slotType = (EquipSlotType)Random.Range(0, 3);
                var item = EquipmentItem.Generate(slotType, (ItemRarity)rarity, dungeonLevel);
                Player.RunLootItems.Add(item);
                if (!Player.Inventory.AddToBackpack(item))
                    LocalMailSystem.SendItemMail($"副本掉落: {item.Name}", $"背包已满，装备已发送至邮箱。\n{item.GetStatSummary()}", item);
            }
            if ((CurrentStageIndex + 1) % 5 == 0)
            {
                var bonusItem = EquipmentItem.GenerateRandom(dungeonLevel);
                Player.Inventory.AddToBackpack(bonusItem);
            }

            Player.Stats.Save();
            GameLog.Log($"[Dungeon] 立即保存: xp={instantXp}, gold={instantGold}, level={Player.Stats.Level}, backpack={Player.Inventory.Backpack.Count}");
        }

        if (CombatDirector.Instance != null)
            CombatDirector.Instance.StopCombat();
        if (Player != null)
            Player.transform.position = new Vector3(9999, 9999, 0);

        if (Player != null)
            LeaderboardUI.SubmitScore(CurrentStageIndex + 1, (int)Player.HeroClass, Player.Stats.Level);

        StartCoroutine(RewardFlow.CompleteDungeonFlow(timeUsed, hpRemaining, hpMax, enemiesKilled, CurrentStageIndex));
    }

    // === PLAYER CREATION ===

    private void CreatePlayer(HeroClass heroClass)
    {
        // Always clean up any existing Player objects first
        if (Player != null)
        {
            // Clear relic manager reference before destroying
            var oldRelic = Player.GetComponent<RelicManager>();
            if (oldRelic != null) oldRelic.ClearRelics();
            Destroy(Player.gameObject);
        }
        Player = null;
        foreach (var go in GameObject.FindGameObjectsWithTag("Player"))
            Destroy(go);

        GameObject playerObj = new GameObject("Player");
        playerObj.tag = "Player";
        playerObj.layer = LayerMask.NameToLayer("Player");
        playerObj.transform.position = Vector3.zero;

        var sr = playerObj.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteCache.WhitePixel;

        var rb = playerObj.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var col = playerObj.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.6f, 0.6f);

        var pc = playerObj.AddComponent<PlayerController>();
        pc.InitializeClass(heroClass);

        // Attach RelicManager to player
        var relicMgr = playerObj.AddComponent<RelicManager>();
        relicMgr.SetPlayer(pc);

        Player = pc;
        _playerBackup = pc; // 静态备份，防止引用丢失
        // 注意：此处不调用Save() — OnSlotLoaded会先LoadSaved()恢复数据，
        // OnClassSelected会自行调用Save()保存新角色。此处Save()会用空背包覆盖已有存档。
        if (PetDataManager.HasActivePet())
            PetTeammates.CreatePet(pc.transform);
    }

    private PetTeammateManager _petTeammateMgr;
    public PetTeammateManager PetTeammates
    {
        get
        {
            if (_petTeammateMgr == null)
                _petTeammateMgr = gameObject.GetComponent<PetTeammateManager>() ?? gameObject.AddComponent<PetTeammateManager>();
            return _petTeammateMgr;
        }
    }
    public bool IsTeamMode { get => PetTeammates.IsTeamMode; set => PetTeammates.IsTeamMode = value; }
    public PetCompanion.PetType ActivePetType { get => PetTeammates.ActivePetType; set => PetTeammates.ActivePetType = value; }

    public void CreatePet(Transform playerTransform) => PetTeammates.CreatePet(playerTransform);
    public void DestroyPets() => PetTeammates.DestroyPets();
    public void DestroyTeammates() => PetTeammates.DestroyTeammates();

    // === UI BUILDING — moved to GameManager.Hud.cs (partial class) ===

    // === WAVE SYSTEM ===
    // Combat logic (OnEnemyKilled, OnWaveComplete, SpawnWave, etc.) has been moved to CombatDirector.
    // GameManager.OnEnemyKilled and OnWaveComplete are pass-throughs for backward compatibility.

    public void OnEnemyKilled(EnemyController enemy)
    {
        if (CombatDirector.Instance != null)
            CombatDirector.Instance.OnEnemyKilled(enemy);

        // Report daily task progress: enemy killed
        DailyTaskUI.ReportProgress(0, 1);
    }

    public void OnPlayerDeath()
    {
        CurrentState = GameState.GameOver;
        Time.timeScale = 0f;
        GameLog.Log($"[OnPlayerDeath] State={CurrentState} Player={(Player != null ? "exists" : "NULL")} active={(Player != null && Player.gameObject.activeSelf)}");
        AudioManager.Instance?.PlayBGM(AudioManager.BGMType.GameOver);
        if (CombatDirector.Instance != null)
            CombatDirector.Instance.StopCombat();

        // Disable auto-battle on death
        if (Player != null && Player.Combat != null)
            Player.Combat.IsAutoBattle = false;

        // Destroy pets on death
        DestroyPets();
        DestroyTeammates();

        // End daily challenge on death (no reward)
        DailyChallenge.EndChallenge();

        // Submit endless mode score on death
        if (IsEndlessMode && Player != null && EndlessWave > 0)
        {
            if (LeaderboardUI.Instance == null) gameObject.AddComponent<LeaderboardUI>();
            LeaderboardUI.SubmitScore(EndlessWave, (int)Player.HeroClass, Player.Stats.Level);
        }

        // 清理战场残留物品 + VFX特效
        SceneRegistry.ClearProjectilesAndLoot();
        SceneRegistry.ClearAll();
        VFXPool.ClearAll();

        // 延迟销毁地图，确保渲染线程完成当前帧的GPU资源释放
        if (DungeonVisuals.Instance != null)
        {
            var dvObj = DungeonVisuals.Instance.gameObject;
            DungeonVisuals.Instance = null;
            dvObj.SetActive(false);
            Destroy(dvObj);
        }
        DungeonVisuals.Instance = null;

        // Mark death for achievement tracking
        if (AchievementManager.Instance != null)
            AchievementManager.Instance.DiedThisRun = true;

        // Auto-save on death (preserves gold/xp earned)
        if (Player != null)
            Player.Stats.Save();

                    GameLog.Log("Game Over! Player died.");
    }

    public void RestartGame()
    {
        DungeonLevel = 1;
        CurrentState = GameState.Title;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void RetryStage()
    {
        Time.timeScale = 1f;
        GameLog.Log($"[RetryStage] START State={CurrentState} Player={(Player != null ? "exists" : "NULL")}");

        // 必须在最前面改为Dungeon，否则GameUI.Update()每帧都会重新显示GameOverPanel
        CurrentState = GameState.Dungeon;

        // 关闭面板
        if (GameUI.Instance != null)
        {
            if (GameUI.Instance.GameOverPanel != null) GameUI.Instance.GameOverPanel.SetActive(false);
            if (GameUI.Instance.PausePanel != null) GameUI.Instance.PausePanel.SetActive(false);
            GameUI.Instance.SetHUDVisible(true);
        }

        // 清理全部场景对象
        CleanupAllDungeonResiduals();
        DestroyTeammates();
        DestroyPets();

        // ★ 核心安全：确保玩家存在并可见 — 先移动玩家再设置相机
        EnsurePlayerExists();
        if (Player != null)
        {
            Player.IsDead = false;
            Player.Stats.Hp = Player.Stats.TotalMaxHp;
            Player.Stats.Runtime.Potions = PlayerRuntimeStats.MaxPotions;
            Player.StateMachine.Reset();
            Player.CharSprite?.ResetDeath();
            Player.CharSprite?.ResetCharacterSprite();
            Player.transform.position = Vector3.zero;
            Player.ResetRunLoot();
            Player.RollRunMutationPool();
        }

        // 无尽模式
        if (EndlessWave > 0)
        {
            StartEndlessMode();
            return;
        }

        // 重建地图
        CreateDungeonVisuals();

        // 重置摄像机 — 先设置位置再启用跟随
        ResetCamera();
        if (Camera.main != null)
            Camera.main.transform.position = new Vector3(0, 0, -10);
        var mapData = DungeonMapData.GetStageMap(CurrentStageIndex);
        if (CameraFollow.Instance != null)
        {
            CameraFollow.Instance.ForcedOrthoSize = mapData.cameraOrthoSize;
            CameraFollow.Instance.MapHalfWidth = mapData.mapHalfWidth;
            CameraFollow.Instance.MapHalfHeight = mapData.mapHalfHeight;
            CameraFollow.Instance.ClampToMap = true;
            CameraFollow.Instance.enabled = true;
            CameraFollow.Instance.SnapToTarget();
        }

        // 战斗
        if (CombatDirector.Instance != null)
        {
            CombatDirector.Instance.StartCombat(DungeonLevel);
            StartCoroutine(CombatDirector.Instance.SpawnWaveAfterDelay(0.5f));
        }

        // VFX预热
        VFXPool.PrewarmAll();

        // 宠物
        if (Player != null && PetDataManager.HasActivePet())
            CreatePet(Player.transform);

        GameLog.Log($"[RetryStage] DONE State={CurrentState} Player.active={(Player != null && Player.gameObject.activeSelf)} DV={(DungeonVisuals.Instance != null ? "exists" : "NULL")}");
    }

        // === ENDLESS MODE — moved to GameManager.Endless.cs (partial class) ===

public void TogglePause()
    {
        IsPaused = !IsPaused;
        Time.timeScale = IsPaused ? 0f : 1f;
    }

    public void CreateDungeonVisuals()
    {
        SceneController.CreateDungeonVisuals(CurrentStageIndex);
    }

    /// <summary>Drops boss equipment — delegates to SceneController.</summary>
    private void DropBossEquipment(int dungeonLevel, Vector3 dropPos)
    {
        SceneController.DropBossEquipment(dungeonLevel, dropPos);
    }

    private void OnApplicationQuit()
    {
        SaveAll();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            SaveAll();
        }
    }

    /// <summary>统一保存入口：仅写入本地PlayerPrefs + 发送Ping</summary>
    private void SaveAll()
    {
        // 1. 立即同步写入本地PlayerPrefs（不启动协程，确保退出前完成）
        if (Player != null)
        {
            Player.Stats.Progress.Save();
            var inv = Player.Inventory;
            if (inv != null)
                PlayerPrefs.SetString($"ARPG_S{PlayerProgressData.ActiveSlot}_Equipment", inv.ToJson());
            PlayerPrefs.Save();
        }
        // 2. 发送Ping（不等待，发完即走）
        if (CloudSaveManager.HasInstance && CloudSaveManager.Instance.IsLoggedIn)
            CloudSaveManager.Instance.SendPing();
        // 3. 记录离线时间
        OfflineRewards.RecordLogout();
        UIHelper.ClearCache();
    }
}
