using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 开放世界管理器 — 进入/退出世界、区域刷怪(按距离激活+重生计时)、传送点发现与快速旅行
/// 复用Dungeon状态(玩家控制/HUD/死亡流程兼容) + InWorld标志区分
/// </summary>
public class WorldManager : MonoBehaviour
{
    public static WorldManager Instance { get; private set; }
    public static bool Active { get; private set; }

    public static float WorldHalfWidth => WorldData.WorldHalf;
    public static float WorldHalfHeight => WorldData.WorldHalf;

    private const float WaypointDiscoverRadius = 5f;
    private const float ZoneActivateRadius = 100f;   // 玩家距区中心<100时维持刷怪
    private const int GlobalEnemyCap = 25;           // 全局同屏敌人上限(GameSettings.MaxConcurrentEnemies高档)
    private const float RespawnCooldown = 15f;

    private static readonly Dictionary<int, List<EnemyController>> _zoneEnemies = new();
    private static readonly Dictionary<int, float> _zoneRespawnAt = new();
    private static readonly Dictionary<int, int> _zoneKills = new();
    private static int _eventZone = -1;      // 魔潮事件区 (-1=无)
    private static float _eventEndAt;
    private static float _nextEventAt = 60f; // 首次魔潮: 进入世界60秒后
    private const float EventDuration = 90f;
    private GameObject _hudBtn;
    private Coroutine _spawnerRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    public static WorldManager Get()
    {
        if (Instance == null && GameManager.Instance != null)
            Instance = GameManager.Instance.gameObject.AddComponent<WorldManager>();
        return Instance;
    }

    /// <summary>从大本营进入开放世界 (spawnZoneIndex=出生区)</summary>
    public static void EnterWorld(int spawnZoneIndex = 0)
    {
        if (Get() == null) return;
        Instance.DoEnterWorld(spawnZoneIndex);
    }

    private void DoEnterWorld(int spawnZoneIndex)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        if (Active) { TeleportTo(spawnZoneIndex); return; }

        Active = true;
        gm.CurrentState = GameState.Dungeon; // 复用副本状态：玩家控制/HUD/死亡流程全部兼容
        gm.InWorld = true;
        Time.timeScale = 1f;
        gm.IsPaused = false;
        AudioManager.Instance?.PlayBGM(AudioManager.BGMType.Battle);

        // 关闭大本营及子页面UI (镜像StartDungeon)
        DestroyPanel<HubUI>(); DestroyPanel<StageSelectUI>(); DestroyPanel<ShopUI>();
        DestroyPanel<SkillTreeUI>(); DestroyPanel<EquipUI>(); DestroyPanel<TitleScreen>();
        DestroyPanel<CharacterSlotUI>();
        if (TopNavBar.Instance != null) TopNavBar.Instance.HideBar();
        if (GameUI.Instance != null)
        {
            GameUI.Instance.ShowHubMode(false);
            GameUI.Instance.SetHUDVisible(true);
        }

        // 清理残留
        SceneRegistry.ClearAll();
        VFXPool.ClearAll();
        EnemyPool.ClearAll();
        EnemyHealthBar.CleanupAll();
        if (DungeonVisuals.Instance != null)
        {
            var oldDv = DungeonVisuals.Instance.gameObject;
            DungeonVisuals.Instance = null;
            oldDv.SetActive(false);
            Destroy(oldDv);
        }

        // 构建世界
        WorldVisuals.Build();

        // 玩家就位 (出生在指定区传送点)
        gm.EnsurePlayerExists();
        var spawnZone = WorldData.Get(spawnZoneIndex);
        if (gm.Player != null)
        {
            gm.Player.CharSprite?.ResetCharacterSprite();
            gm.Player.IsDead = false;
            gm.Player.StateMachine?.Reset();
            gm.Player.transform.position = spawnZone.WaypointPos;
            gm.Player.Stats.Hp = gm.Player.Stats.TotalMaxHp;
            gm.Player.Stats.Runtime.Potions = PlayerRuntimeStats.MaxPotions;
            if (gm.Player.Skills != null && gm.Player.SkillCtrl != null)
                gm.Player.SkillCtrl.SetSkills(gm.Player.Skills.ToArray());
            if (gm.Player.Combat != null && gm.Player.SkillCtrl != null)
                gm.Player.SkillCtrl.SetCombat(gm.Player.Combat);
        }

        // 相机: 边界=整个世界
        gm.ResetCamera();
        if (Camera.main != null)
            Camera.main.transform.position = new Vector3(spawnZone.WaypointPos.x, spawnZone.WaypointPos.y, -10f);
        if (CameraFollow.Instance != null)
        {
            CameraFollow.Instance.ForcedOrthoSize = 10f;
            CameraFollow.Instance.MapHalfWidth = WorldData.WorldHalf;
            CameraFollow.Instance.MapHalfHeight = WorldData.WorldHalf;
            CameraFollow.Instance.ClampToMap = true;
            CameraFollow.Instance.enabled = true;
            CameraFollow.Instance.SnapToTarget();
        }

        VFXPool.ClearAll();
        VFXPool.PrewarmAll();

        if (CombatDirector.Instance != null)
        {
            CombatDirector.WorldMode = true;
            CombatDirector.Instance.StartCombat(1);
        }

        _zoneEnemies.Clear();
        _zoneRespawnAt.Clear();
        CreateHudButton();
        _spawnerRoutine = StartCoroutine(SpawnerLoop());

        GameUI.Instance?.ShowItemPickupToast("进入世界", $"当前位置：{spawnZone.Name} — 靠近光柱激活传送点");
        GameLog.Log($"[World] EnterWorld zone={spawnZone.Index}({spawnZone.Name}) pos={spawnZone.WaypointPos}");
    }

    /// <summary>退出世界回大本营</summary>
    public static void ExitWorld()
    {
        if (!Active) return;
        CleanupWorld();
        GameManager.Instance?.ShowHub();
    }

    /// <summary>清理世界实体与UI (死亡/退出共用)</summary>
    public static void CleanupWorld()
    {
        Active = false;
        if (CombatDirector.Instance != null) CombatDirector.WorldMode = false;
        if (GameManager.Instance != null) GameManager.Instance.InWorld = false;
        if (Instance != null)
        {
            if (Instance._spawnerRoutine != null)
            {
                Instance.StopCoroutine(Instance._spawnerRoutine);
                Instance._spawnerRoutine = null;
            }
            if (Instance._hudBtn != null) Destroy(Instance._hudBtn);
            _zoneEnemies.Clear();
            _zoneRespawnAt.Clear();
            HidePrompt();
        }
        WaypointUI.Hide();
        WorldQuestUI.ShutdownWorld();
        WorldVisuals.DestroyInstance();
    }

    /// <summary>快速旅行到指定区传送点 (需已发现)</summary>
    public static void TeleportTo(int zoneIndex)
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Player == null) return;
        var zone = WorldData.Get(zoneIndex);
        if (!WorldData.IsDiscovered(zone.Index))
        {
            GameUI.Instance?.ShowItemPickupToast("传送点未激活", $"先到达 {zone.Name} 的光柱附近");
            return;
        }
        gm.Player.transform.position = zone.WaypointPos;
        if (CameraFollow.Instance != null) CameraFollow.Instance.SnapToTarget();
        GameUI.Instance?.ShowItemPickupToast("传送完成", zone.Name);
        GameLog.Log($"[World] Teleport → zone={zone.Index}({zone.Name})");
    }

    // ===== 区域刷怪 + 传送点发现 主循环 =====
    private IEnumerator SpawnerLoop()
    {
        var wait = new WaitForSeconds(1f);
        while (Active)
        {
            yield return wait;
            var gm = GameManager.Instance;
            if (gm == null || gm.Player == null) continue;
            Vector3 playerPos = gm.Player.transform.position;

            TryDiscoverWaypoints(playerPos);
            TickEvent();
            TickZoneSpawns(playerPos);
        }
    }

    private void TryDiscoverWaypoints(Vector3 playerPos)
    {
        foreach (var zone in WorldData.Zones)
        {
            if (WorldData.IsDiscovered(zone.Index)) continue;
            if (Vector2.Distance(playerPos, zone.WaypointPos) <= WaypointDiscoverRadius)
            {
                WorldData.MarkDiscovered(zone.Index);
                GameUI.Instance?.ShowItemPickupToast("发现传送点", $"{zone.Name} — 可在地图面板快速旅行");
                GameLog.Log($"[World] Waypoint discovered: {zone.Index}({zone.Name})");
            }
        }
    }

    // ===== 世界事件: 魔潮 (定期随机区刷强化敌人, 90秒) =====
    private void TickEvent()
    {
        if (_eventZone >= 0)
        {
            if (Time.time >= _eventEndAt)
            {
                var ended = WorldData.Get(_eventZone);
                _eventZone = -1;
                GameUI.Instance?.ShowItemPickupToast("魔潮退去", $"{ended.Name} 恢复平静");
                _nextEventAt = Time.time + Random.Range(150f, 240f);
            }
            return;
        }
        if (Time.time < _nextEventAt) return;

        var playerPos = GameManager.Instance?.Player?.transform.position ?? Vector3.zero;
        var candidates = new List<WorldData.Zone>();
        foreach (var z in WorldData.Zones)
        {
            if (WorldData.IsSafeZone(z.Index)) continue;
            var cur = WorldData.GetAt(playerPos.x, playerPos.y);
            if (cur != null && cur.Index == z.Index) continue; // 不在玩家所在区开魔潮
            candidates.Add(z);
        }
        if (candidates.Count == 0) return;

        _eventZone = candidates[Random.Range(0, candidates.Count)].Index;
        _eventEndAt = Time.time + EventDuration;
        var zone = WorldData.Get(_eventZone);
        GameUI.Instance?.ShowItemPickupToast("⚠ 魔潮来袭", $"{zone.Name} 出现强化敌人 — 持续90秒");
        GameLog.Log($"[World] 魔潮 eventZone={_eventZone}({zone.Name}) dur={EventDuration}s");
    }

    /// <summary>当前魔潮事件区 (-1=无)</summary>
    public static int EventZoneIndex => _eventZone;

    /// <summary>区域本会话击杀数 (声望进度)</summary>
    public static int GetZoneKills(int zoneIndex) => _zoneKills.TryGetValue(zoneIndex, out int k) ? k : 0;

    private void CheckZoneCompletion(WorldData.Zone zone)
    {
        if (WorldData.IsZoneDone(zone.Index) || !WorldData.IsDiscovered(zone.Index)) return;
        _zoneKills.TryGetValue(zone.Index, out int kills);
        if (kills < 10) return;
        WorldData.MarkZoneDone(zone.Index);
        GameManager.Instance?.Player?.Stats?.AddGold(100);
        GameUI.Instance?.ShowItemPickupToast("区域声望完成", $"{zone.Name} — 奖励金币×100");
        GameLog.Log($"[World] ZoneDone: {zone.Index}({zone.Name})");
    }

    private void TickZoneSpawns(Vector3 playerPos)
    {
        int globalAlive = 0;
        foreach (var kv in _zoneEnemies)
            globalAlive += CountAlive(kv.Value);

        foreach (var zone in WorldData.Zones)
        {
            if (WorldData.IsSafeZone(zone.Index)) continue; // 安全区("家"附近)无怪物

            if (!_zoneEnemies.TryGetValue(zone.Index, out var list))
            {
                list = new List<EnemyController>();
                _zoneEnemies[zone.Index] = list;
            }
            int removed = list.RemoveAll(e => e == null || e.IsDead || !e.gameObject.activeInHierarchy);
            if (removed > 0)
            {
                _zoneKills.TryGetValue(zone.Index, out int kills);
                _zoneKills[zone.Index] = kills + removed;
                CheckZoneCompletion(zone);
                WorldQuests.OnZoneKills(zone.Index, removed); // 讨伐任务进度
            }

            float dist = Vector2.Distance(playerPos, zone.Center);
            if (dist > ZoneActivateRadius) continue; // 远区不刷怪

            int target = (4 + zone.Tier) * (zone.Index == _eventZone ? 2 : 1); // 魔潮区怪物翻倍
            int alive = CountAlive(list);
            if (alive >= target) continue;
            if (globalAlive >= GlobalEnemyCap) continue;
            if (_zoneRespawnAt.TryGetValue(zone.Index, out float next) && Time.time < next) continue;

            int spawnNow = Mathf.Min(target - alive, 3);
            spawnNow = Mathf.Min(spawnNow, GlobalEnemyCap - globalAlive);
            for (int i = 0; i < spawnNow; i++)
            {
                var enemy = CreateWorldEnemy(zone, list);
                if (enemy != null) globalAlive++;
            }
            _zoneRespawnAt[zone.Index] = Time.time + RespawnCooldown;
        }
    }

    private static int CountAlive(List<EnemyController> list)
    {
        int n = 0;
        foreach (var e in list)
            if (e != null && !e.IsDead && e.gameObject.activeInHierarchy) n++;
        return n;
    }

    private EnemyController CreateWorldEnemy(WorldData.Zone zone, List<EnemyController> into)
    {
        var gm = GameManager.Instance;
        if (gm == null) { Debug.LogWarning($"[WorldSpawn] Z{zone.Index} gm null"); return null; }

        Vector3 pos = FindSpawnPos(zone);
        if (pos.sqrMagnitude < 0.01f) { Debug.LogWarning($"[WorldSpawn] Z{zone.Index} pos=zero"); return null; }

        // 区域难度 = 主题关卡 + 世界等级加成 + 魔潮加成
        gm.SetWorldStage(zone.Theme);
        int eventBonus = (zone.Index == _eventZone) ? 2 : 0;
        gm.DungeonLevel = zone.Tier + 1 + WorldData.WorldTierLevelBonus + eventBonus;

        var enemyObj = new GameObject($"WorldEnemy_Z{zone.Index}");
        enemyObj.transform.position = pos;
        enemyObj.layer = LayerMask.NameToLayer("Enemy");

        var sr = enemyObj.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteCache.WhitePixel;

        // 物理组件必须在EnemyController之前添加（其Awake会引用rb）
        var rb = enemyObj.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f; rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        var col = enemyObj.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.6f, 0.6f);

        var ec = enemyObj.AddComponent<EnemyController>();
        ec.Type = RollEnemyType();
        ec.WorldZoneIndex = zone.Index; // 区域绑定 + 被动仇恨(被打才反击)

        if (ec.Type == EnemyType.Elite)
            ec.Affixes = EliteAffix.RollAffixes(zone.Tier + 1);

        EnemySprite.ApplySprite(enemyObj, ec.Type, zone.Theme);
        if (ec.Affixes != null && ec.Affixes.Count > 0)
            EnemySprite.ApplyAffixAuras(enemyObj, ec.Affixes);

        ec.Initialize(zone.Tier + 1);
        ec.Brain.SetWorldZone(zone.Index);
        into.Add(ec);
        return ec;
    }

    private static EnemyType RollEnemyType()
    {
        float r = Random.value;
        if (r < 0.55f) return EnemyType.Melee;
        if (r < 0.70f) return EnemyType.Ranged;
        if (r < 0.82f) return EnemyType.Elite;
        if (r < 0.88f) return EnemyType.Charger;
        if (r < 0.93f) return EnemyType.Bomber;
        if (r < 0.97f) return EnemyType.Healer;
        return EnemyType.Shielder;
    }

    private static Vector3 FindSpawnPos(WorldData.Zone zone)
    {
        var gm = GameManager.Instance;
        Vector3 playerPos = gm?.Player != null ? gm.Player.transform.position : zone.Center;
        float half = WorldData.ZoneSize / 2f - 3f;

        for (int attempt = 0; attempt < 20; attempt++)
        {
            float x = Random.Range(zone.Center.x - half, zone.Center.x + half);
            float y = Random.Range(zone.Center.y - half, zone.Center.y + half);
            var p = new Vector3(x, y, 0f);
            if (Vector2.Distance(p, playerPos) < 10f) continue;                       // 不在玩家脸上刷
            if (Vector2.Distance(p, zone.WaypointPos) < 6f) continue;                 // 不挡传送点
            return p;
        }
        return Vector3.zero;
    }

    // ===== HUD按钮: 世界地图 + 回家 =====
    private void CreateHudButton()
    {
        var canvas = GameManager.EnsureCanvas();
        _hudBtn = new GameObject("WorldHudBtns");
        _hudBtn.transform.SetParent(canvas.transform, false);
        var root = _hudBtn.AddComponent<RectTransform>();
        root.anchorMin = new Vector2(1, 1); root.anchorMax = new Vector2(1, 1);
        root.pivot = new Vector2(1, 1);
        root.anchoredPosition = new Vector2(-14, -14);
        root.sizeDelta = new Vector2(240, 40);
        CreateHudSub(root.transform, "MapBtn", "🗺 世界地图", -5, () => WaypointUI.Toggle());
        CreateHudSub(root.transform, "HomeBtn", "🏠 回家", -125, () => ExitWorld());
    }

    private static void CreateHudSub(Transform parent, string name, string label, float xOffset, System.Action onClick)
    {
        var btnObj = new GameObject(name); btnObj.transform.SetParent(parent, false);
        var r = btnObj.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(1, 1); r.anchorMax = new Vector2(1, 1);
        r.pivot = new Vector2(1, 1);
        r.anchoredPosition = new Vector2(xOffset, 0);
        r.sizeDelta = new Vector2(110, 40);
        var img = btnObj.AddComponent<Image>();
        img.color = new Color(0.1f, 0.1f, 0.18f, 0.9f);
        var btn = btnObj.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        UIHelper.SetupButtonFeedback(btn, img.color);
        btn.onClick.AddListener(() => onClick());
        var lbl = new GameObject("Lbl"); lbl.transform.SetParent(btnObj.transform, false);
        var lblR = lbl.AddComponent<RectTransform>(); lblR.anchorMin = Vector2.zero; lblR.anchorMax = Vector2.one; lblR.offsetMin = Vector2.zero; lblR.offsetMax = Vector2.zero;
        var txt = lbl.AddComponent<Text>(); txt.text = label; txt.alignment = TextAnchor.MiddleCenter; txt.fontSize = 15; txt.color = UIHelper.TextPrimary; txt.font = GameManager.GetUIFont(); txt.raycastTarget = false;
    }

    private static void DestroyPanel<T>() where T : MonoBehaviour
    {
        var inst = typeof(T).GetProperty("Instance")?.GetValue(null) as MonoBehaviour;
        if (inst == null) return;
        // UI组件可能挂在GameManager.gameObject上(AddComponent模式)——只销毁组件，否则会连带杀死GameManager
        if (GameManager.Instance != null && inst.gameObject == GameManager.Instance.gameObject)
            Destroy(inst);
        else
            Destroy(inst.gameObject);
    }

    // ===== "家"NPC交互 — 靠近弹出原大本营功能面板 =====
    private static readonly Dictionary<string, System.Action> NpcActions = new()
    {
        { "副本入口",   () => { UIHelper.EnsureUIManager(); UIManager.Instance.ShowStageSelect(); } },
        { "铁匠·装备",   () => { UIHelper.EnsureUIManager(); UIManager.Instance.ShowEquip(); } },
        { "熔炉·强化",   () => { UIHelper.EnsureUIManager(); UIManager.Instance.ShowForge(); } },
        { "杂货商人",   () => { UIHelper.EnsureUIManager(); UIManager.Instance.ShowShop(); } },
        { "碎片商人",   () => { UIHelper.EnsureUIManager(); UIManager.Instance.ShowFragmentShop(); } },
        { "技能导师",   () => { UIHelper.EnsureUIManager(); UIManager.Instance.ShowSkillTree(); } },
        { "公会告示板", () => { UIHelper.EnsureUIManager(); UIManager.Instance.ShowGuild(); } },
        { "邮箱",       () => { UIHelper.EnsureUIManager(); UIManager.Instance.ShowMail(); } },
    };

    private GameObject _promptBtn;
    private Text _promptText;
    private WorldData.HomeNpcDef _nearNpc;
    private int _nearQuestZone = -1;

    private void Update()
    {
        if (!Active) return;
        var p = GameManager.Instance?.Player;
        if (p == null) return;
        TickHomeInteraction(p.transform.position);
        WorldQuestUI.UpdateTracker();
    }

    private void TickHomeInteraction(Vector3 playerPos)
    {
        var homePos = WorldData.Get(0).WaypointPos;
        _nearNpc = null;
        _nearQuestZone = -1;
        float best = 3.2f;
        foreach (var npc in WorldData.HomeNpcs)
        {
            float d = Vector2.Distance(playerPos, homePos + npc.Offset);
            if (d < best) { best = d; _nearNpc = npc; }
        }
        // 野外任务发布人 (每区一名)
        foreach (var zone in WorldData.Zones)
        {
            if (!WorldQuests.HasNpc(zone.Index)) continue;
            float d = Vector2.Distance(playerPos, WorldQuests.NpcPos(zone.Index));
            if (d < best) { best = d; _nearNpc = null; _nearQuestZone = zone.Index; }
        }

        if (_nearNpc != null)
        {
            if (_promptBtn == null) CreatePrompt();
            if (!_promptBtn.activeSelf) _promptBtn.SetActive(true);
            if (_promptText != null) _promptText.text = $"与 {_nearNpc.Name} 交互";
        }
        else if (_nearQuestZone >= 0)
        {
            if (_promptBtn == null) CreatePrompt();
            if (!_promptBtn.activeSelf) _promptBtn.SetActive(true);
            if (_promptText != null) _promptText.text = "与 任务发布人 交互";
        }
        else if (_promptBtn != null && _promptBtn.activeSelf)
        {
            _promptBtn.SetActive(false);
        }
    }

    private void CreatePrompt()
    {
        var canvas = GameManager.EnsureCanvas();
        _promptBtn = new GameObject("HomePrompt");
        _promptBtn.transform.SetParent(canvas.transform, false);
        var r = _promptBtn.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(0.5f, 0); r.anchorMax = new Vector2(0.5f, 0);
        r.pivot = new Vector2(0.5f, 0);
        r.anchoredPosition = new Vector2(0, 170);
        r.sizeDelta = new Vector2(280, 48);
        var img = _promptBtn.AddComponent<Image>();
        img.color = new Color(0.08f, 0.08f, 0.16f, 0.95f);
        var btn = _promptBtn.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        UIHelper.SetupButtonFeedback(btn, img.color);
        btn.onClick.AddListener(() =>
        {
            if (_nearNpc != null && NpcActions.TryGetValue(_nearNpc.Name, out var open))
            {
                HidePrompt();
                open();
            }
            else if (_nearQuestZone >= 0)
            {
                HidePrompt();
                WorldQuestUI.Show(_nearQuestZone);
            }
        });
        var lbl = new GameObject("Lbl"); lbl.transform.SetParent(_promptBtn.transform, false);
        var lblR = lbl.AddComponent<RectTransform>(); lblR.anchorMin = Vector2.zero; lblR.anchorMax = Vector2.one; lblR.offsetMin = Vector2.zero; lblR.offsetMax = Vector2.zero;
        _promptText = lbl.AddComponent<Text>(); _promptText.alignment = TextAnchor.MiddleCenter; _promptText.fontSize = 16; _promptText.color = UIHelper.TextPrimary; _promptText.font = GameManager.GetUIFont(); _promptText.raycastTarget = false;
        _promptBtn.SetActive(false);
    }

    private static void HidePrompt()
    {
        if (Instance != null && Instance._promptBtn != null)
            Instance._promptBtn.SetActive(false);
    }
}
