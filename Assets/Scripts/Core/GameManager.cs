using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Networking;
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

public class GameManager : MonoBehaviour
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

    public void OnDungeonComplete()
    {
        CurrentState = GameState.Hub;

        // Disable auto-battle
        if (Player != null && Player.Combat != null)
            Player.Combat.IsAutoBattle = false;

        // Destroy pet when dungeon completes
        DestroyPets();
        DestroyTeammates();

        // 记录战斗数据 (服务器权威结算用)
        int hpRemaining = Player != null ? Player.Stats.Hp : 0;
        int hpMax = Player != null ? Player.Stats.TotalMaxHp : 1;
        int enemiesKilled = CombatDirector.Instance != null ? CombatDirector.Instance.EnemiesKilled : 0;
        float timeUsed = Time.time - _dungeonStartTime;

        // 通关记录由服务器权威管理 — 禁止本地写入 HighestStageCleared

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

            // 立即生成本地掉落装备（不等服务器，防止退出丢失）
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
            // 每5关额外1件
            if ((CurrentStageIndex + 1) % 5 == 0)
            {
                var bonusItem = EquipmentItem.GenerateRandom(dungeonLevel);
                Player.Inventory.AddToBackpack(bonusItem);
            }

            Player.Stats.Save();
                        GameLog.Log($"[Dungeon] 立即保存: xp={instantXp}, gold={instantGold}, level={Player.Stats.Level}, backpack={Player.Inventory.Backpack.Count}");
        }

        // 停止战斗（但不清理战场，等结算完成后再清理）
        if (CombatDirector.Instance != null)
            CombatDirector.Instance.StopCombat();
        // 隐藏玩家 — 移到远处，保持引用和组件完整
        if (Player != null)
            Player.transform.position = new Vector3(9999, 9999, 0);

        // 通关排行榜提交（普通副本用 HighestStage 作为 Wave）
        if (Player != null)
            LeaderboardUI.SubmitScore(CurrentStageIndex + 1, (int)Player.HeroClass, Player.Stats.Level);

        // 服务器权威结算: 发送战斗结果, 等待服务器计算奖励, 完成后再清理
        StartCoroutine(CompleteDungeonFlow(timeUsed, hpRemaining, hpMax, enemiesKilled));
    }

    /// <summary>完整副本完成流程: 服务器同步 → 清理战场 → 显示结算</summary>
    /// <remarks>经验/金币/掉落已在OnDungeonComplete中同步保存，此协程只处理服务器同步和UI</remarks>
    private IEnumerator CompleteDungeonFlow(float timeUsed, int hpRemaining, int hpMax, int enemiesKilled)
    {
        // 1. 服务器同步（不等服务器也能正常工作，奖励已在OnDungeonComplete中保存）
        yield return StartCoroutine(SubmitDungeonResult(CurrentStageIndex, timeUsed, hpRemaining, hpMax, enemiesKilled));

        // 2. 等待一帧
        yield return null;

        // 3. 清理所有战场残留
        SceneRegistry.ClearAll();
        VFXPool.ClearAll();
        EnemyPool.ClearAll();

        // 4. 销毁地图
        if (DungeonVisuals.Instance != null)
        {
            var dvObj = DungeonVisuals.Instance.gameObject;
            DungeonVisuals.Instance = null;
            dvObj.SetActive(false);
            Destroy(dvObj);
        }
    }

    [System.Serializable]
    private class DungeonResultBody
    {
        public int dungeonId;
        public float timeUsed;
        public int hpRemaining;
        public int hpMax;
        public int enemiesKilled;
    }

    [System.Serializable]
    private class DungeonResultDrop
    {
        public int rarity;
        public int slotType;
        public int dungeonLevel;
    }

    [System.Serializable]
    private class DungeonResultResponse
    {
        public bool success;
        public int goldReward;
        public int baseGold;
        public int speedBonus;
        public int fullHpBonus;
        public int xpReward;
        public int newGold;
        public int newHighestStage;
        public List<DungeonResultDrop> drops;
    }

    private IEnumerator SubmitDungeonResult(int dungeonId, float timeUsed, int hpRemaining, int hpMax, int enemiesKilled)
    {
        string token = CloudSaveManager.Instance?.Token ?? "";
        int slot = CloudSaveManager.Instance?.ActiveSlot ?? 0;
        string apiBase = (CloudSaveManager.Instance?.ServerUrl ?? "http://localhost:5132") + "/api";

        // 服务器可用: 服务器权威结算
        if (!string.IsNullOrEmpty(token))
        {
            var body = new DungeonResultBody
            {
                dungeonId = dungeonId,
                timeUsed = timeUsed,
                hpRemaining = hpRemaining,
                hpMax = hpMax,
                enemiesKilled = enemiesKilled
            };
            string json = JsonUtility.ToJson(body);

            using (var req = new UnityWebRequest($"{apiBase}/save/dungeon/complete?slot={slot}", "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {token}");
                req.timeout = 10;
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    var resp = JsonUtility.FromJson<DungeonResultResponse>(req.downloadHandler.text);
                    if (resp != null && resp.success)
                    {
                        // 服务器权威: 更新RuntimePlayerData通关记录
                        if (RuntimePlayerData.Instance != null && resp.newHighestStage >= 0)
                        {
                            RuntimePlayerData.Instance.RefreshFromServer(resp.newHighestStage, null);
                            LocalSettingsManager.CacheStageProgress(resp.newHighestStage, null);
                        }
                        // 服务器返回的奖励 — 客户端只负责展示
                        ApplyDungeonRewards(resp.goldReward, resp.xpReward, resp.newGold, resp.newHighestStage, resp.drops, dungeonId);
                        yield break;
                    }
                }
                GameLog.LogWarning("[Dungeon] 服务器结算失败, 使用本地结算");
            }
        }

        // 本地fallback结算 (服务器不可用时)
        int goldReward = 0;
        if (StageSelectUI.Stages != null && dungeonId >= 0 && dungeonId < StageSelectUI.Stages.Length)
            goldReward = StageSelectUI.Stages[dungeonId].goldReward;

        int fullHpBonus = (hpRemaining >= hpMax) ? Mathf.RoundToInt(goldReward * 0.5f) : 0;
        int totalGold = goldReward + fullHpBonus;
        int xpReward = 50 + dungeonId * 30 + enemiesKilled * 5;

        // 本地生成掉落
        var localDrops = new List<DungeonResultDrop>();
        int dropCount = 3;
        for (int i = 0; i < dropCount; i++)
        {
            float roll = Random.Range(0f, 1f);
            int rarity = roll < 0.02f ? 3 : roll < 0.10f ? 2 : roll < 0.30f ? 1 : 0;
            localDrops.Add(new DungeonResultDrop { rarity = rarity, slotType = Random.Range(0, 3), dungeonLevel = dungeonId + 1 });
        }

        // 本地fallback: 更新RuntimePlayerData
        if (RuntimePlayerData.Instance != null && dungeonId > RuntimePlayerData.Instance.HighestStage)
        {
            RuntimePlayerData.Instance.RefreshFromServer(dungeonId, null);
            LocalSettingsManager.CacheStageProgress(dungeonId, null);
        }

        ApplyDungeonRewards(totalGold, xpReward, -1, -1, localDrops, dungeonId);
    }

    private void ApplyDungeonRewards(int goldReward, int xpReward, int serverGold, int newHighestStage, List<DungeonResultDrop> drops, int dungeonId)
    {
        if (Player != null)
        {
            // 金币：不再用服务器值覆盖，而是累加奖励（防止服务器旧值覆盖本地新值）
            // OnDungeonComplete中已经调用了AddGold(instantGold)，这里只补充服务器差额
            if (serverGold > 0)
            {
                // 服务器返回的是"通关后总金币"，如果大于当前就补差，否则不动
                int currentGold = Player.Stats.Gold;
                if (serverGold > currentGold)
                    Player.Stats.AddGold(serverGold - currentGold);
                // 如果 serverGold < currentGold 说明本地已经有更多金币（OnDungeonComplete已加过），不覆盖
            }
            else
            {
                // 本地fallback：OnDungeonComplete已经加过instantGold，这里只加服务器返回的奖励
                // 但如果是从SubmitDungeonResult的fallback调用的，需要加
                // 检查是否已经加过：RunGoldEarned已包含OnDungeonComplete的instantGold
            }

            Player.RunGoldEarned += goldReward;

            // 经验奖励
            Player.GainXp(xpReward);

            // 通关记录由服务器权威管理 — 禁止本地修改 HighestStageCleared
            // 服务器返回的 newHighestStage 通过 RuntimePlayerData 更新

            // 成就
            if (AchievementManager.Instance != null)
            {
                bool fullHp = Player.Stats.Hp == Player.Stats.TotalMaxHp;
                AchievementManager.Instance.RecordStageClear(dungeonId, fullHp);
            }

            // 掉落装备 — 由服务器返回的drops列表生成 (服务器权威)
            if (drops != null)
            {
                foreach (var drop in drops)
                {
                    var item = EquipmentItem.Generate((EquipSlotType)drop.slotType, (ItemRarity)drop.rarity, drop.dungeonLevel);
                    Player.RunLootItems.Add(item);
                    if (!Player.Inventory.AddToBackpack(item))
                    {
                        LocalMailSystem.SendItemMail($"副本掉落: {item.Name}", $"背包已满，装备已发送至邮箱。\n{item.GetStatSummary()}", item);
                    }
                }
                // BossDropAnimation 只播放视觉效果，不再生成装备（装备已在上面生成）
            }

            // 保存非通关进度数据（金币/装备/技能等），HighestStage由服务器管理
            Player.Stats.Save();

            // 上传最新存档到服务器（更新SaveJson中的Level/Gold/Xp等）
            if (CloudSaveManager.HasInstance && CloudSaveManager.Instance.IsLoggedIn)
            {
                CloudSaveManager.Instance.StartCoroutine(CloudSaveManager.Instance.UploadSaveCo());
            }
        }

        // 每日任务/赛季/挑战
        DailyTaskUI.ReportProgress(1, 1);
        SeasonPass.AddXP(xpReward);
        if (DailyChallenge.ActiveModifier.HasValue && !DailyChallenge.IsCompleted())
        {
            DailyChallenge.MarkCompleted();
            DailyChallenge.EndChallenge();
            if (Player != null) Player.Stats.AddGold(DailyChallenge.RewardGold);
        }
        else if (DailyChallenge.ActiveModifier.HasValue)
        {
            DailyChallenge.EndChallenge();
        }

        // 对话
        if (DialogueSystem.Instance != null)
            DialogueSystem.Instance.ShowStageEndDialogue(dungeonId);

        // 结算界面
        if (dungeonId >= 7)
            GameUI.Instance?.ShowVictoryScreen(goldReward);
        else
            GameUI.Instance?.ShowDungeonComplete(goldReward);
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

    // === UI BUILDING ===

    private void BuildUI()
    {
        Canvas canvas = EnsureCanvas();

        GameUI gameUI = canvas.GetComponent<GameUI>();
        if (gameUI == null) gameUI = canvas.gameObject.AddComponent<GameUI>();

        // Only build HUD elements once — prevents duplicate _NavLayer / buttons on re-entry
        if (gameUI.HpBar != null)
        {
            WireSkillButtons(canvas.transform, gameUI);
            return;
        }

        BuildHUDElements(canvas.transform, gameUI);
    }

    /// <summary>
    /// Re-wires skill button references to the current PlayerController and updates labels.
    /// Called when HUD already exists (e.g. re-entering Hub after returning to Title).
    /// </summary>
    private void WireSkillButtons(Transform canvasTransform, GameUI gameUI)
    {
        if (Player == null || Player.InputCtrl == null) return;

        ClassData cd = ClassData.GetClassData(selectedClass);

        var s1 = canvasTransform.Find("Skill1Btn");
        var s2 = canvasTransform.Find("Skill2Btn");
        var s3 = canvasTransform.Find("Skill3Btn");
        var dash = canvasTransform.Find("DashBtn");
        var atkComp = FindObjectOfType<AttackButton>();

        if (s1 != null) { Player.InputCtrl.Skill1BtnObj = s1.gameObject; var lbl = s1.Find("Label")?.GetComponent<Text>(); if (lbl != null) lbl.text = cd.Skill1Name; }
        if (s2 != null) { Player.InputCtrl.Skill2BtnObj = s2.gameObject; var lbl = s2.Find("Label")?.GetComponent<Text>(); if (lbl != null) lbl.text = cd.Skill2Name; }
        if (s3 != null) { Player.InputCtrl.Skill3BtnObj = s3.gameObject; var lbl = s3.Find("Label")?.GetComponent<Text>(); if (lbl != null) lbl.text = cd.Skill3Name; }
        if (dash != null) Player.InputCtrl.DashBtnObj = dash.gameObject;
        if (atkComp != null) Player.InputCtrl.AttackBtnObj = atkComp.gameObject;

        Player.InputCtrl.WireButtonListeners();
    }

    private void BuildHUDElements(Transform canvasTransform, GameUI gameUI)
    {
        Font font = GameManager.GetUIFont();

        // --- Character Portrait (golden ring, Dark Souls style) ---
        GameObject portraitObj = new GameObject("PortraitIcon");
        portraitObj.transform.SetParent(canvasTransform, false);
        RectTransform portraitRect = portraitObj.AddComponent<RectTransform>();
        portraitRect.anchorMin = new Vector2(0, 1); portraitRect.anchorMax = new Vector2(0, 1);
        portraitRect.pivot = new Vector2(0.5f, 0.5f); portraitRect.anchoredPosition = new Vector2(30, -70);
        portraitRect.sizeDelta = new Vector2(36, 36);
        Image portraitImg = portraitObj.AddComponent<Image>();
        portraitImg.sprite = HudSpriteFactory.CreatePortraitSprite(32);
        portraitImg.color = Color.white;

        // --- HP Bar (dark gold border, red-orange fill) ---
        GameObject hpBarObj = new GameObject("HpBar");
        hpBarObj.transform.SetParent(canvasTransform, false);
        RectTransform hpBarRect = hpBarObj.AddComponent<RectTransform>();
        hpBarRect.anchorMin = new Vector2(0, 1); hpBarRect.anchorMax = new Vector2(0, 1);
        hpBarRect.pivot = new Vector2(0, 0.5f); hpBarRect.anchoredPosition = new Vector2(54, -70);
        hpBarRect.sizeDelta = new Vector2(300, 24);
        Image hpBg = hpBarObj.AddComponent<Image>(); hpBg.color = new Color(0.08f, 0.08f, 0.12f, 0.9f);

        // HP border (dark gold, slightly larger than bar)
        GameObject hpBorderObj = new GameObject("Border");
        hpBorderObj.transform.SetParent(hpBarObj.transform, false);
        RectTransform hpBorderRect = hpBorderObj.AddComponent<RectTransform>();
        hpBorderRect.anchorMin = Vector2.zero; hpBorderRect.anchorMax = Vector2.one;
        hpBorderRect.offsetMin = new Vector2(-1, -1); hpBorderRect.offsetMax = new Vector2(1, 1);
        Image hpBorderImg = hpBorderObj.AddComponent<Image>();
        hpBorderImg.color = new Color(0.35f, 0.27f, 0.05f, 0.8f);
        hpBorderImg.raycastTarget = false;
        hpBorderObj.transform.SetAsFirstSibling();

        GameObject hpFillObj = new GameObject("Fill");
        hpFillObj.transform.SetParent(hpBarObj.transform, false);
        RectTransform hpFillRect = hpFillObj.AddComponent<RectTransform>();
        hpFillRect.anchorMin = Vector2.zero; hpFillRect.anchorMax = Vector2.one;
        hpFillRect.offsetMin = Vector2.zero; hpFillRect.offsetMax = Vector2.zero;
        Image hpFillImg = hpFillObj.AddComponent<Image>(); hpFillImg.color = new Color(0.75f, 0.15f, 0.1f, 1f);

        Slider hpSlider = hpBarObj.AddComponent<Slider>();
        hpSlider.targetGraphic = hpFillImg; hpSlider.fillRect = hpFillRect;
        hpSlider.handleRect = null; hpSlider.direction = Slider.Direction.LeftToRight;
        hpSlider.minValue = 0; hpSlider.maxValue = 100; hpSlider.value = 100; hpSlider.interactable = false;
        gameUI.HpBar = hpSlider;

        // --- Shield Bar (thin stamina-like bar below HP) ---
        GameObject shieldBarObj = new GameObject("ShieldBar");
        shieldBarObj.transform.SetParent(canvasTransform, false);
        RectTransform shieldRect = shieldBarObj.AddComponent<RectTransform>();
        shieldRect.anchorMin = new Vector2(0, 1); shieldRect.anchorMax = new Vector2(0, 1);
        shieldRect.pivot = new Vector2(0, 0.5f); shieldRect.anchoredPosition = new Vector2(54, -86);
        shieldRect.sizeDelta = new Vector2(300, 8);
        Image shieldBg = shieldBarObj.AddComponent<Image>(); shieldBg.color = new Color(0.06f, 0.06f, 0.1f, 0.9f);

        // Shield border
        GameObject shieldBorder = new GameObject("Border");
        shieldBorder.transform.SetParent(shieldBarObj.transform, false);
        RectTransform sbRect = shieldBorder.AddComponent<RectTransform>();
        sbRect.anchorMin = Vector2.zero; sbRect.anchorMax = Vector2.one;
        sbRect.offsetMin = new Vector2(-1, -1); sbRect.offsetMax = new Vector2(1, 1);
        Image sbImg = shieldBorder.AddComponent<Image>();
        sbImg.color = new Color(0.3f, 0.23f, 0.04f, 0.7f);
        sbImg.raycastTarget = false;
        shieldBorder.transform.SetAsFirstSibling();

        GameObject shieldFillObj = new GameObject("ShieldFill");
        shieldFillObj.transform.SetParent(shieldBarObj.transform, false);
        RectTransform sfRect = shieldFillObj.AddComponent<RectTransform>();
        sfRect.anchorMin = Vector2.zero; sfRect.anchorMax = Vector2.one;
        sfRect.offsetMin = Vector2.zero; sfRect.offsetMax = Vector2.zero;
        Image sfImg = shieldFillObj.AddComponent<Image>(); sfImg.color = new Color(0.4f, 0.7f, 1f, 0.9f);
        gameUI.ShieldBar = shieldBarObj;
        shieldBarObj.SetActive(false);

        // --- Info texts (compact, below bars) ---
        gameUI.LevelText = HudSpriteFactory.CreateText(canvasTransform, "LevelText", new Vector2(12, -102), new Vector2(200, 30), 24, Color.white);
        gameUI.XpText = HudSpriteFactory.CreateText(canvasTransform, "XpText", new Vector2(12, -134), new Vector2(250, 28), 18, Color.cyan);
        gameUI.DungeonLevelText = HudSpriteFactory.CreateText(canvasTransform, "DungeonLevelText", new Vector2(12, -162), new Vector2(250, 28), 18, Color.yellow);
        gameUI.GoldText = HudSpriteFactory.CreateText(canvasTransform, "GoldText", new Vector2(12, -190), new Vector2(250, 28), 22, new Color(1f, 0.85f, 0.2f));

        // --- Enemy count text ---
        gameUI.EnemyCountText = HudSpriteFactory.CreateText(canvasTransform, "EnemyCountText", new Vector2(12, -218), new Vector2(300, 28), 18, new Color(1f, 0.6f, 0.6f));

        // --- Stat detail text (larger for mobile) ---
        GameObject statObj = new GameObject("StatDetailText");
        statObj.transform.SetParent(canvasTransform, false);
        RectTransform statRect = statObj.AddComponent<RectTransform>();
        statRect.anchorMin = new Vector2(1, 1); statRect.anchorMax = new Vector2(1, 1);
        statRect.pivot = new Vector2(1, 1); statRect.anchoredPosition = new Vector2(-12.5f, -62);
        statRect.sizeDelta = new Vector2(380, 110);
        Text statText = statObj.AddComponent<Text>();
        statText.alignment = TextAnchor.UpperRight; statText.fontSize = 22;
        statText.color = new Color(0.8f, 0.8f, 0.8f); statText.font = font;
        gameUI.StatDetailText = statText;

        // --- Map name text (副本导航上方) ---
        GameObject mapNameObj = new GameObject("MapNameText");
        mapNameObj.transform.SetParent(canvasTransform, false);
        RectTransform mapNameRect = mapNameObj.AddComponent<RectTransform>();
        mapNameRect.anchorMin = new Vector2(1, 1); mapNameRect.anchorMax = new Vector2(1, 1);
        mapNameRect.pivot = new Vector2(1, 1); mapNameRect.anchoredPosition = new Vector2(-12.5f, -20);
        mapNameRect.sizeDelta = new Vector2(380, 36);
        Text mapNameText = mapNameObj.AddComponent<Text>();
        mapNameText.alignment = TextAnchor.UpperRight; mapNameText.fontSize = 20;
        mapNameText.color = new Color(0.9f, 0.85f, 0.4f, 1f); mapNameText.font = font;
        mapNameText.raycastTarget = false;
        gameUI.MapNameText = mapNameText;

        gameUI.BindBossHealthPanel(canvasTransform.GetComponentInParent<Canvas>());

        // --- Virtual Joystick (clearly visible for mobile) ---
        BuildMobileJoystick(canvasTransform);

        // --- Attack Button (large, circular, prominent) ---
        BuildAttackButton(canvasTransform, font);

        // Cache joystick and attack button refs in GameUI for reliable show/hide
        var joyComp = FindObjectOfType<VirtualJoystick>();
        if (joyComp != null) gameUI.JoystickObj = joyComp.gameObject;
        var atkComp = FindObjectOfType<AttackButton>();
        if (atkComp != null) gameUI.AttackButtonObj = atkComp.gameObject;

        // --- Skill Buttons (semicircle around attack button) ---
        // Attack button center at (-140, 100); skills arranged in arc above it
        ClassData cd = ClassData.GetClassData(selectedClass);
        SkillButton s1Btn, s2Btn, s3Btn, dashBtn;
        HudSpriteFactory.CreateSkillButton(canvasTransform, gameUI, "DashBtn", "闪避", new Vector2(-300, 72.5f),
            new Color(0.25f, 0.25f, 0.3f), new Color(0.6f, 0.6f, 0.75f, 0.9f), out gameUI.DashCooldownOverlay, out gameUI.DashCooldownText, out dashBtn);
        HudSpriteFactory.CreateSkillButton(canvasTransform, gameUI, "Skill1Btn", cd.Skill1Name, new Vector2(-300, 183),
            new Color(0.15f, 0.3f, 0.6f), new Color(0.4f, 0.65f, 1f, 0.9f), out gameUI.Skill1CooldownOverlay, out gameUI.Skill1CooldownText, out s1Btn);
        HudSpriteFactory.CreateSkillButton(canvasTransform, gameUI, "Skill2Btn", cd.Skill2Name, new Vector2(-182, 237.5f),
            new Color(0.5f, 0.2f, 0.05f), new Color(1f, 0.6f, 0.2f, 0.9f), out gameUI.Skill2CooldownOverlay, out gameUI.Skill2CooldownText, out s2Btn);
        HudSpriteFactory.CreateSkillButton(canvasTransform, gameUI, "Skill3Btn", cd.Skill3Name, new Vector2(-62.5f, 237.5f),
            new Color(0.08f, 0.4f, 0.15f), new Color(0.3f, 1f, 0.5f, 0.9f), out gameUI.Skill3CooldownOverlay, out gameUI.Skill3CooldownText, out s3Btn);

        // Wire touch buttons to input controller
        if (Player != null && Player.InputCtrl != null)
        {
            Player.InputCtrl.Skill1BtnObj = s1Btn.gameObject;
            Player.InputCtrl.Skill2BtnObj = s2Btn.gameObject;
            Player.InputCtrl.Skill3BtnObj = s3Btn.gameObject;
            Player.InputCtrl.DashBtnObj = dashBtn.gameObject;
            Player.InputCtrl.AttackBtnObj = atkComp.gameObject;
            Player.InputCtrl.WireButtonListeners();
        }

        // --- Pause Panel ---
        BuildPausePanel(canvasTransform, gameUI, font);

        // --- Nav Buttons (left-top, same style as hub) ---
        UIHelper.MakeNavButtons(canvasTransform, font, onBack: () =>
        {
            if (!IsPaused) TogglePause();
        });
        var navLayer = canvasTransform.Find("_NavLayer");
        if (navLayer != null) gameUI.NavLayer = navLayer.gameObject;

        // Remove gear button — pause panel already has settings
        var gearBtn = navLayer?.Find("GearBtn");
        if (gearBtn != null) Destroy(gearBtn.gameObject);

        // --- Game Over Panel ---
        BuildGameOverPanel(canvasTransform, gameUI, font);

        // --- Wave Announcement ---
        GameObject waveAnnObj = new GameObject("WaveAnnounce");
        waveAnnObj.transform.SetParent(canvasTransform, false);
        RectTransform waveAnnRect = waveAnnObj.AddComponent<RectTransform>();
        waveAnnRect.anchorMin = new Vector2(0.5f, 0.5f); waveAnnRect.anchorMax = new Vector2(0.5f, 0.5f);
        waveAnnRect.pivot = new Vector2(0.5f, 0.5f); waveAnnRect.anchoredPosition = new Vector2(0, 80);
        waveAnnRect.sizeDelta = new Vector2(400, 80);
        Text waveAnnText = waveAnnObj.AddComponent<Text>();
        waveAnnText.alignment = TextAnchor.MiddleCenter; waveAnnText.fontSize = 48;
        waveAnnText.color = new Color(1f, 0.85f, 0.2f); waveAnnText.font = font;
        waveAnnText.gameObject.SetActive(false);
        gameUI.WaveAnnounceText = waveAnnText;

        // --- Combo Counter ---
        GameObject comboObj = new GameObject("ComboCounter");
        comboObj.transform.SetParent(canvasTransform, false);
        RectTransform comboRect = comboObj.AddComponent<RectTransform>();
        comboRect.anchorMin = new Vector2(0.5f, 1); comboRect.anchorMax = new Vector2(0.5f, 1);
        comboRect.pivot = new Vector2(0.5f, 1); comboRect.anchoredPosition = new Vector2(0, -200);
        comboRect.sizeDelta = new Vector2(250, 50);
        Text comboText = comboObj.AddComponent<Text>();
        comboText.alignment = TextAnchor.MiddleCenter; comboText.fontSize = 30;
        comboText.color = new Color(1f, 0.9f, 0.2f); comboText.font = font;
        comboText.gameObject.SetActive(false);
        gameUI.ComboText = comboText;
    }

    private void BuildPausePanel(Transform canvasTransform, GameUI gameUI, Font font)
    {
        GameObject pausePanel = new GameObject("PausePanel");
        pausePanel.transform.SetParent(canvasTransform, false);
        RectTransform ppRect = pausePanel.AddComponent<RectTransform>();
        ppRect.anchorMin = Vector2.zero; ppRect.anchorMax = Vector2.one;
        ppRect.offsetMin = Vector2.zero; ppRect.offsetMax = Vector2.zero;
        Image ppBg = pausePanel.AddComponent<Image>();
        ppBg.color = new Color(0, 0, 0, 0.75f);
        pausePanel.SetActive(false);

        // Title
        GameObject ppTitle = new GameObject("Title");
        ppTitle.transform.SetParent(pausePanel.transform, false);
        RectTransform ppTitleR = ppTitle.AddComponent<RectTransform>();
        ppTitleR.anchorMin = new Vector2(0.5f, 1); ppTitleR.anchorMax = new Vector2(0.5f, 1);
        ppTitleR.pivot = new Vector2(0.5f, 1); ppTitleR.anchoredPosition = new Vector2(0, -120);
        ppTitleR.sizeDelta = new Vector2(300, 50);
        Text ppTitleT = ppTitle.AddComponent<Text>();
        ppTitleT.text = "暂停"; ppTitleT.alignment = TextAnchor.MiddleCenter;
        ppTitleT.fontSize = 36; ppTitleT.color = Color.white; ppTitleT.font = font;

        // Resume button
        GameObject resumeBtn = new GameObject("ResumeBtn");
        resumeBtn.transform.SetParent(pausePanel.transform, false);
        RectTransform rr = resumeBtn.AddComponent<RectTransform>();
        rr.anchorMin = new Vector2(0.5f, 0.5f); rr.anchorMax = new Vector2(0.5f, 0.5f);
        rr.pivot = new Vector2(0.5f, 0.5f); rr.anchoredPosition = new Vector2(0, 50);
        rr.sizeDelta = new Vector2(250, 55);
        Image rrImg = resumeBtn.AddComponent<Image>();
        rrImg.color = new Color(0.2f, 0.6f, 1f, 0.9f);
        Button rrBtn = resumeBtn.AddComponent<Button>();
        rrBtn.onClick.AddListener(() => { gameUI.OnResumeFromPause(); });
        GameObject rrLbl = new GameObject("Label");
        rrLbl.transform.SetParent(resumeBtn.transform, false);
        RectTransform rrLblR = rrLbl.AddComponent<RectTransform>();
        rrLblR.anchorMin = Vector2.zero; rrLblR.anchorMax = Vector2.one;
        rrLblR.offsetMin = Vector2.zero; rrLblR.offsetMax = Vector2.zero;
        Text rrLblT = rrLbl.AddComponent<Text>();
        rrLblT.text = "继续游戏"; rrLblT.alignment = TextAnchor.MiddleCenter;
        rrLblT.fontSize = 24; rrLblT.color = Color.white; rrLblT.font = font;

        // Settings button
        GameObject setBtn = new GameObject("SettingsBtn");
        setBtn.transform.SetParent(pausePanel.transform, false);
        RectTransform sr = setBtn.AddComponent<RectTransform>();
        sr.anchorMin = new Vector2(0.5f, 0.5f); sr.anchorMax = new Vector2(0.5f, 0.5f);
        sr.pivot = new Vector2(0.5f, 0.5f); sr.anchoredPosition = new Vector2(0, -15);
        sr.sizeDelta = new Vector2(250, 55);
        Image srImg = setBtn.AddComponent<Image>();
        srImg.color = new Color(0.3f, 0.3f, 0.4f, 0.9f);
        Button srBtn = setBtn.AddComponent<Button>();
        srBtn.onClick.AddListener(() => { var settings = FindObjectOfType<SettingsUI>(); if (settings == null) settings = gameObject.AddComponent<SettingsUI>(); settings.Show(); });
        GameObject srLbl = new GameObject("Label");
        srLbl.transform.SetParent(setBtn.transform, false);
        RectTransform srLblR = srLbl.AddComponent<RectTransform>();
        srLblR.anchorMin = Vector2.zero; srLblR.anchorMax = Vector2.one;
        srLblR.offsetMin = Vector2.zero; srLblR.offsetMax = Vector2.zero;
        Text srLblT = srLbl.AddComponent<Text>();
        srLblT.text = "设置"; srLblT.alignment = TextAnchor.MiddleCenter;
        srLblT.fontSize = 24; srLblT.color = Color.white; srLblT.font = font;

        // Return to hub button
        GameObject retBtn = new GameObject("ReturnBtn");
        retBtn.transform.SetParent(pausePanel.transform, false);
        RectTransform rtr = retBtn.AddComponent<RectTransform>();
        rtr.anchorMin = new Vector2(0.5f, 0.5f); rtr.anchorMax = new Vector2(0.5f, 0.5f);
        rtr.pivot = new Vector2(0.5f, 0.5f); rtr.anchoredPosition = new Vector2(0, -80);
        rtr.sizeDelta = new Vector2(250, 55);
        Image rtrImg = retBtn.AddComponent<Image>();
        rtrImg.color = new Color(0.7f, 0.2f, 0.2f, 0.9f);
        Button rtrBtn = retBtn.AddComponent<Button>();
        rtrBtn.onClick.AddListener(() => { gameUI.OnReturnToHubFromPause(); });
        GameObject rtrLbl = new GameObject("Label");
        rtrLbl.transform.SetParent(retBtn.transform, false);
        RectTransform rtrLblR = rtrLbl.AddComponent<RectTransform>();
        rtrLblR.anchorMin = Vector2.zero; rtrLblR.anchorMax = Vector2.one;
        rtrLblR.offsetMin = Vector2.zero; rtrLblR.offsetMax = Vector2.zero;
        Text rtrLblT = rtrLbl.AddComponent<Text>();
        rtrLblT.text = "返回大厅"; rtrLblT.alignment = TextAnchor.MiddleCenter;
        rtrLblT.fontSize = 24; rtrLblT.color = Color.white; rtrLblT.font = font;

        gameUI.PausePanel = pausePanel;
    }

    private void BuildGameOverPanel(Transform canvasTransform, GameUI gameUI, Font font)
    {
        GameObject gameOverPanel = new GameObject("GameOverPanel");
        gameOverPanel.transform.SetParent(canvasTransform, false);
        RectTransform goPanelRect = gameOverPanel.AddComponent<RectTransform>();
        goPanelRect.anchorMin = Vector2.zero; goPanelRect.anchorMax = Vector2.one;
        goPanelRect.offsetMin = Vector2.zero; goPanelRect.offsetMax = Vector2.zero;
        Image goPanelImg = gameOverPanel.AddComponent<Image>();
        goPanelImg.color = new Color(0, 0, 0, 0.7f);
        gameOverPanel.SetActive(false);

        GameObject goTitle = new GameObject("Title");
        goTitle.transform.SetParent(gameOverPanel.transform, false);
        RectTransform goTitleRect = goTitle.AddComponent<RectTransform>();
        goTitleRect.anchorMin = new Vector2(0.5f, 1); goTitleRect.anchorMax = new Vector2(0.5f, 1);
        goTitleRect.pivot = new Vector2(0.5f, 1); goTitleRect.anchoredPosition = new Vector2(0, -100);
        goTitleRect.sizeDelta = new Vector2(400, 60);
        Text goTitleText = goTitle.AddComponent<Text>();
        goTitleText.text = "游戏结束"; goTitleText.alignment = TextAnchor.MiddleCenter;
        goTitleText.fontSize = 40; goTitleText.color = Color.red; goTitleText.font = font;

        GameObject goStats = new GameObject("StatsText");
        goStats.transform.SetParent(gameOverPanel.transform, false);
        RectTransform goStatsRect = goStats.AddComponent<RectTransform>();
        goStatsRect.anchorMin = new Vector2(0.5f, 0.5f); goStatsRect.anchorMax = new Vector2(0.5f, 0.5f);
        goStatsRect.pivot = new Vector2(0.5f, 0.5f); goStatsRect.anchoredPosition = Vector2.zero;
        goStatsRect.sizeDelta = new Vector2(300, 150);
        Text goStatsTextComp = goStats.AddComponent<Text>();
        goStatsTextComp.alignment = TextAnchor.MiddleCenter; goStatsTextComp.fontSize = 22;
        goStatsTextComp.color = Color.white; goStatsTextComp.font = font;

        gameUI.GameOverPanel = gameOverPanel;
        gameUI.GameOverStatsText = goStatsTextComp;

        GameObject restartBtn = new GameObject("RestartButton");
        restartBtn.transform.SetParent(gameOverPanel.transform, false);
        RectTransform restartRect = restartBtn.AddComponent<RectTransform>();
        restartRect.anchorMin = new Vector2(0.5f, 0); restartRect.anchorMax = new Vector2(0.5f, 0);
        restartRect.pivot = new Vector2(0.5f, 0.5f); restartRect.anchoredPosition = new Vector2(0, 100);
        restartRect.sizeDelta = new Vector2(200, 50);
        Image restartImg = restartBtn.AddComponent<Image>();
        restartImg.color = new Color(0.2f, 0.6f, 1f, 0.9f);

        GameObject restartLabel = new GameObject("Label");
        restartLabel.transform.SetParent(restartBtn.transform, false);
        RectTransform rlRect = restartLabel.AddComponent<RectTransform>();
        rlRect.anchorMin = Vector2.zero; rlRect.anchorMax = Vector2.one;
        rlRect.offsetMin = Vector2.zero; rlRect.offsetMax = Vector2.zero;
        Text rlText = restartLabel.AddComponent<Text>();
        rlText.text = "返回大厅"; rlText.alignment = TextAnchor.MiddleCenter;
        rlText.fontSize = 24; rlText.color = Color.white; rlText.font = font;

        Button restartButton = restartBtn.AddComponent<Button>();
        restartButton.targetGraphic = restartImg;
        restartButton.onClick.AddListener(() => { 
            if (GameUI.Instance?.GameOverPanel != null) GameUI.Instance.GameOverPanel.SetActive(false);
            ShowHub(); 
        });

        // Retry button — restart current dungeon stage
        GameObject retryBtn = new GameObject("RetryButton");
        retryBtn.transform.SetParent(gameOverPanel.transform, false);
        RectTransform retryRect = retryBtn.AddComponent<RectTransform>();
        retryRect.anchorMin = new Vector2(0.5f, 0); retryRect.anchorMax = new Vector2(0.5f, 0);
        retryRect.pivot = new Vector2(0.5f, 0.5f); retryRect.anchoredPosition = new Vector2(0, 170);
        retryRect.sizeDelta = new Vector2(200, 50);
        Image retryImg = retryBtn.AddComponent<Image>();
        retryImg.color = new Color(0.8f, 0.3f, 0.1f, 0.9f);

        GameObject retryLabel = new GameObject("Label");
        retryLabel.transform.SetParent(retryBtn.transform, false);
        RectTransform ryLblRect = retryLabel.AddComponent<RectTransform>();
        ryLblRect.anchorMin = Vector2.zero; ryLblRect.anchorMax = Vector2.one;
        ryLblRect.offsetMin = Vector2.zero; ryLblRect.offsetMax = Vector2.zero;
        Text ryLblText = retryLabel.AddComponent<Text>();
        ryLblText.text = "重新挑战"; ryLblText.alignment = TextAnchor.MiddleCenter;
        ryLblText.fontSize = 24; ryLblText.color = Color.white; ryLblText.font = font;

        Button retryButton = retryBtn.AddComponent<Button>();
        retryButton.targetGraphic = retryImg;
        retryButton.onClick.AddListener(() => { RetryStage(); });
    }

    /// <summary>
    /// Destroys a UI component on this gameObject and lets its OnDestroy clean up the panel.
    /// Also finds and destroys any orphaned panel by component name convention.
    /// </summary>
    private void DestroyUIComponent<T>() where T : Component
    {
        var comp = GetComponent<T>();
        if (comp != null) Destroy(comp);

        string panelName = typeof(T).Name;
        if (panelName.EndsWith("UI")) panelName = panelName.Substring(0, panelName.Length - 2);
        panelName += "Panel";

        Canvas canvas = _cachedCanvas;
        if (canvas == null) return;
        foreach (Transform child in canvas.transform)
        {
            if (child.name == panelName)
            {
                Destroy(child.gameObject);
                break;
            }
        }
    }

    private void BuildMobileJoystick(Transform canvasTransform)
    {
        // Outer ring (translucent, barely visible — Dark Souls style)
        GameObject joystickBg = new GameObject("JoystickBg");
        joystickBg.transform.SetParent(canvasTransform, false);
        RectTransform joystickBgRect = joystickBg.AddComponent<RectTransform>();
        joystickBgRect.anchorMin = new Vector2(0, 0); joystickBgRect.anchorMax = new Vector2(0, 0);
        joystickBgRect.pivot = new Vector2(0.5f, 0.5f); joystickBgRect.anchoredPosition = new Vector2(170, 170);
        joystickBgRect.sizeDelta = new Vector2(220, 220);

        Image joystickBgImg = joystickBg.AddComponent<Image>();
        joystickBgImg.sprite = HudSpriteFactory.CreateJoystickRingSprite(64);
        joystickBgImg.color = Color.white;

        // Handle (subtle, ghosted)
        GameObject joystickHandle = new GameObject("Handle");
        joystickHandle.transform.SetParent(joystickBg.transform, false);
        RectTransform handleRect = joystickHandle.AddComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0.5f, 0.5f); handleRect.anchorMax = new Vector2(0.5f, 0.5f);
        handleRect.pivot = new Vector2(0.5f, 0.5f); handleRect.sizeDelta = new Vector2(80, 80);
        Image handleImg = joystickHandle.AddComponent<Image>();
        handleImg.sprite = HudSpriteFactory.CreateJoystickHandleSprite(64);
        handleImg.color = Color.white;

        joystickBg.AddComponent<VirtualJoystick>();
    }

    private void BuildAttackButton(Transform canvasTransform, Font font)
    {
        // Large circular attack button — golden ornate border, dark center (largest in cluster)
        GameObject atkBtnObj = new GameObject("AttackButton");
        atkBtnObj.transform.SetParent(canvasTransform, false);
        RectTransform atkBtnRect = atkBtnObj.AddComponent<RectTransform>();
        atkBtnRect.anchorMin = new Vector2(1, 0); atkBtnRect.anchorMax = new Vector2(1, 0);
        atkBtnRect.pivot = new Vector2(0.5f, 0.5f); atkBtnRect.anchoredPosition = new Vector2(-140, 100);
        atkBtnRect.sizeDelta = new Vector2(140, 140);

        // Golden ornate ring
        Image atkRingImg = atkBtnObj.AddComponent<Image>();
        atkRingImg.sprite = HudSpriteFactory.CreateOrnateRingSprite(64);
        atkRingImg.color = Color.white;

        // Dark center fill with red glow
        GameObject atkInner = new GameObject("InnerFill");
        atkInner.transform.SetParent(atkBtnObj.transform, false);
        RectTransform atkInnerRect = atkInner.AddComponent<RectTransform>();
        atkInnerRect.anchorMin = new Vector2(0.5f, 0.5f); atkInnerRect.anchorMax = new Vector2(0.5f, 0.5f);
        atkInnerRect.pivot = new Vector2(0.5f, 0.5f); atkInnerRect.sizeDelta = new Vector2(120, 120);
        Image atkInnerImg = atkInner.AddComponent<Image>();
        atkInnerImg.sprite = HudSpriteFactory.CreateDarkFillSprite(64, new Color(0.5f, 0.08f, 0.08f));
        atkInnerImg.color = Color.white;

        // Sword icon
        GameObject atkLabel = new GameObject("Label");
        atkLabel.transform.SetParent(atkBtnObj.transform, false);
        RectTransform atkLabelRect = atkLabel.AddComponent<RectTransform>();
        atkLabelRect.anchorMin = Vector2.zero; atkLabelRect.anchorMax = Vector2.one;
        atkLabelRect.offsetMin = Vector2.zero; atkLabelRect.offsetMax = Vector2.zero;
        Text atkText = atkLabel.AddComponent<Text>();
        atkText.text = ""; atkText.alignment = TextAnchor.MiddleCenter;
        atkText.fontSize = 52; atkText.color = new Color(1f, 0.92f, 0.5f); atkText.font = font;

        AttackButton atkBtn = atkBtnObj.AddComponent<AttackButton>();
        var trigger = atkBtnObj.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        var pointerDown = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown };
        pointerDown.callback.AddListener((data) => atkBtn.OnPointerDown((UnityEngine.EventSystems.BaseEventData)data));
        trigger.triggers.Add(pointerDown);
        var pointerUp = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp };
        pointerUp.callback.AddListener((data) => atkBtn.OnPointerUp((UnityEngine.EventSystems.BaseEventData)data));
        trigger.triggers.Add(pointerUp);
    }

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

    // IsEndlessMode is now a wrapper around CurrentState — see above.
    public int EndlessWave { get; private set; }

    public void StartEndlessMode()
    {
        CurrentState = GameState.Endless;
        EndlessWave = 0;
        DungeonLevel = 5; // Fixed starting difficulty

        // Clean up hub UI
        DestroyUIComponent<HubUI>();

        // 副本中隐藏TopNavBar
        if (TopNavBar.Instance != null) TopNavBar.Instance.HideBar();

        // Re-enable gameplay HUD
        if (GameUI.Instance != null)
        {
            GameUI.Instance.ShowHubMode(false);
            GameUI.Instance.SetHUDVisible(true);
        }

        // Clear leftovers
        SceneRegistry.ClearAll();
        VFXPool.ClearAll();
        EnemyPool.ClearAll();

        // 清理残留HPBar根对象
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
        CreateDungeonVisuals();

        // Camera clamp
        var mapData = DungeonMapData.GetStageMap(randomStage);
        if (CameraFollow.Instance != null)
        {
            CameraFollow.Instance.ForcedOrthoSize = mapData.cameraOrthoSize;
            CameraFollow.Instance.MapHalfWidth = mapData.mapHalfWidth;
            CameraFollow.Instance.MapHalfHeight = mapData.mapHalfHeight;
            CameraFollow.Instance.ClampToMap = true;
            CameraFollow.Instance.enabled = true;
        }

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
        if (CameraFollow.Instance != null)
        {
            CameraFollow.Instance.ForcedOrthoSize = mapData.cameraOrthoSize;
            CameraFollow.Instance.MapHalfWidth = mapData.mapHalfWidth;
            CameraFollow.Instance.MapHalfHeight = mapData.mapHalfHeight;
            CameraFollow.Instance.ClampToMap = true;
            CameraFollow.Instance.enabled = true;
        }

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
