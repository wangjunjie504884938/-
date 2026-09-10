using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public partial class GameUI : MonoBehaviour
{
    public static GameUI Instance { get; private set; }

    [Header("HUD")]
    public Slider HpBar;
    public Text LevelText;
    public Text XpText;
    public Text DungeonLevelText;
    public Text GoldText;
    public GameObject ShieldBar;
    private Text potionText;

    [Header("Stat Details")]
    public Text StatDetailText;
    public Text MapNameText;

    [Header("Game Over")]
    public GameObject GameOverPanel;
    public Text GameOverStatsText;

    [Header("Wave Announcement")]
    public Text WaveAnnounceText;

    [Header("Combo Counter")]
    public Text ComboText;

    [Header("Skill Cooldowns")]
    public Image DashCooldownOverlay;
    public Image Skill1CooldownOverlay;
    public Image Skill2CooldownOverlay;
    public Image Skill3CooldownOverlay;
    public Text DashCooldownText;
    public Text Skill1CooldownText;
    public Text Skill2CooldownText;
    public Text Skill3CooldownText;

    [Header("Enemy Count")]
    public Text EnemyCountText;

    [Header("Boss")]
    public GameObject BossHealthPanel;
    public Slider BossHealthBar;
    public Text BossNameText;
    public Text BossHpText;

    [Header("Dungeon Complete")]
    public GameObject DungeonCompletePanel;

    [Header("Pause")]
    public GameObject PausePanel;

    [Header("Nav Buttons")]
    public GameObject NavLayer;

    private GameObject _autoBattleBtn;
    private Text _autoBattleLabel;

    [Header("Relic Select")]
    public GameObject RelicSelectPanel;

    [Header("Room Event")]
    // Room events now apply directly with toast notifications — no popup panel needed

    [Header("Mobile Controls")]
    public GameObject JoystickObj;
    public GameObject AttackButtonObj;

    private EnemyController bossTarget;
    private string currentBossName;

    // Cached frame-level values to skip redundant .text assignments
    private int _lastHp, _lastMaxHp, _lastLevel, _lastXp, _lastXpToNext, _lastGold, _lastAlive, _lastWave;
    private int _lastDungeonLevel;
    private float _lastStatUpdate;

    // Detailed stats toggle panel
    private GameObject _statsPanel;
    private Text _statsText;
    private float _statsPanelTimer;
    private bool _statsPanelVisible;

    // FPS counter
    private Text _fpsText;
    private float _fpsTimer;
    private int _fpsFrames;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        CleanupStaleButtons();
        CreatePotionText();
        CreateAutoBattleButton();
        CreateFPSText();
    }

    private void CreateFPSText()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        Font font = GameManager.GetUIFont();

        var obj = new GameObject("FPSText");
        obj.transform.SetParent(canvas.transform, false);
        var rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(0, 0);
        rt.pivot = new Vector2(0, 0);
        rt.anchoredPosition = new Vector2(10, 10);
        rt.sizeDelta = new Vector2(100, 24);
        _fpsText = obj.AddComponent<Text>();
        _fpsText.alignment = TextAnchor.LowerLeft;
        _fpsText.fontSize = 14;
        _fpsText.color = new Color(0.6f, 0.8f, 0.4f, 0.7f);
        _fpsText.font = font;
        _fpsText.raycastTarget = false;
        _fpsText.gameObject.SetActive(false);
    }

    private void CreatePotionText()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        Font font = GameManager.GetUIFont();

        var obj = new GameObject("PotionText");
        obj.transform.SetParent(canvas.transform, false);
        var rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(10, -80);
        rt.sizeDelta = new Vector2(200, 28);
        potionText = obj.AddComponent<Text>();
        potionText.text = "0/5 [H]";
        potionText.alignment = TextAnchor.MiddleLeft;
        potionText.fontSize = 18;
        potionText.color = new Color(0.3f, 1f, 0.4f);
        potionText.font = font;
        potionText.raycastTarget = false;
    }

    private void CreateAutoBattleButton()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        Font font = GameManager.GetUIFont();

        _autoBattleBtn = new GameObject("AutoBattleBtn");
        _autoBattleBtn.transform.SetParent(canvas.transform, false);
        var rt = _autoBattleBtn.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(1, 1);
        rt.anchoredPosition = new Vector2(-12, -160);
        rt.sizeDelta = new Vector2(100, 40);

        var img = _autoBattleBtn.AddComponent<Image>();
        img.color = new Color(0.2f, 0.5f, 0.2f, 0.85f);

        var btn = _autoBattleBtn.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = new Color(0.2f, 0.5f, 0.2f, 0.85f);
        colors.highlightedColor = new Color(0.3f, 0.6f, 0.3f, 0.9f);
        colors.pressedColor = new Color(0.15f, 0.4f, 0.15f, 1f);
        btn.colors = colors;

        var labelObj = new GameObject("Label");
        labelObj.transform.SetParent(_autoBattleBtn.transform, false);
        var labelRt = labelObj.AddComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;
        _autoBattleLabel = labelObj.AddComponent<Text>();
        _autoBattleLabel.text = "挂机";
        _autoBattleLabel.alignment = TextAnchor.MiddleCenter;
        _autoBattleLabel.fontSize = 18;
        _autoBattleLabel.color = Color.white;
        _autoBattleLabel.font = font;
        _autoBattleLabel.raycastTarget = false;

        btn.onClick.AddListener(() =>
        {
            var player = GameManager.Instance?.Player;
            if (player == null || player.Combat == null) return;
            player.Combat.IsAutoBattle = !player.Combat.IsAutoBattle;
            if (_autoBattleLabel != null)
            {
                _autoBattleLabel.text = player.Combat.IsAutoBattle ? "停止" : "挂机";
                var btnImg = _autoBattleBtn.GetComponent<Image>();
                if (btnImg != null)
                    btnImg.color = player.Combat.IsAutoBattle
                        ? new Color(0.7f, 0.2f, 0.2f, 0.85f)
                        : new Color(0.2f, 0.5f, 0.2f, 0.85f);
            }
        });

        _autoBattleBtn.SetActive(false);
    }

    private void CreateStatsPanel()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        Font font = GameManager.GetUIFont();

        _statsPanel = new GameObject("StatsPanel");
        _statsPanel.transform.SetParent(canvas.transform, false);
        var rt = _statsPanel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0.5f);
        rt.anchorMax = new Vector2(0, 0.5f);
        rt.pivot = new Vector2(0, 0.5f);
        rt.anchoredPosition = new Vector2(10, 0);
        rt.sizeDelta = new Vector2(280, 320);

        var bg = _statsPanel.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.02f, 0.06f, 0.88f);
        bg.raycastTarget = false;

        var panelCanvas = _statsPanel.AddComponent<Canvas>();
        panelCanvas.overrideSorting = true;
        panelCanvas.sortingOrder = 5;

        var titleObj = new GameObject("Title");
        titleObj.transform.SetParent(_statsPanel.transform, false);
        var tr = titleObj.AddComponent<RectTransform>();
        tr.anchorMin = new Vector2(0, 1); tr.anchorMax = new Vector2(1, 1);
        tr.pivot = new Vector2(0.5f, 1);
        tr.anchoredPosition = new Vector2(0, -6);
        tr.sizeDelta = new Vector2(0, 28);
        var titleTxt = titleObj.AddComponent<Text>();
        titleTxt.text = "角色属性 [Tab]";
        titleTxt.alignment = TextAnchor.MiddleCenter;
        titleTxt.fontSize = 16; titleTxt.color = UIHelper.Accent; titleTxt.font = font;
        titleTxt.raycastTarget = false;

        var textObj = new GameObject("StatsText");
        textObj.transform.SetParent(_statsPanel.transform, false);
        var txtr = textObj.AddComponent<RectTransform>();
        txtr.anchorMin = new Vector2(0, 0); txtr.anchorMax = new Vector2(1, 1);
        txtr.offsetMin = new Vector2(8, 8); txtr.offsetMax = new Vector2(-8, -34);
        _statsText = textObj.AddComponent<Text>();
        _statsText.alignment = TextAnchor.UpperLeft;
        _statsText.fontSize = 14; _statsText.color = UIHelper.TextPrimary; _statsText.font = font;
        _statsText.supportRichText = true;
        _statsText.raycastTarget = false;

        _statsPanel.SetActive(false);
    }

    private void UpdateStatsPanel(PlayerController player)
    {
        if (_statsText == null || player == null) return;
        var s = player.Stats;
        var sb = UIStringBuilderPool.Get();

        sb.Append("<color=#FFD700>攻击力:</color> ").Append(s.BuffedAttack).Append('\n');
        sb.Append("<color=#7788FF>防御力:</color> ").Append(s.BuffedDefense).Append('\n');
        sb.Append("<color=#55FF55>生命值:</color> ").Append(s.Hp).Append('/').Append(s.TotalMaxHp).Append('\n');
        sb.Append("<color=#AAFFAA>移速:</color> ").Append(s.TotalMoveSpeed.ToString("F1")).Append('\n');
        sb.Append("<color=#FFAA44>暴击率:</color> ").Append((s.TotalCritChance * 100).ToString("F1")).Append("%\n");
        sb.Append("<color=#FF6688>吸血:</color> ").Append((s.TotalLifeSteal * 100).ToString("F0")).Append("%\n");
        sb.Append("<color=#66FFCC>攻速:</color> ").Append(s.TotalAttackSpeed.ToString("F2")).Append("x\n");
        sb.Append("<color=#CCAAFF>范围:</color> ").Append(s.TotalAttackRange.ToString("F1")).Append('\n');
        sb.Append("<color=#88FF88>回复:</color> ").Append(s.HpRegen.ToString("F1")).Append("/s\n");
        if (s.ShieldHp > 0)
            sb.Append("<color=#88CCFF>护盾:</color> ").Append(s.ShieldHp).Append('\n');
        sb.Append("<color=#FFCC44>金币:</color> ").Append(UIStringBuilderPool.FormatNumber(s.Gold)).Append('\n');
        sb.Append("<color=#AAAAFF>技能点:</color> ").Append(s.SkillPoints).Append('\n');
        sb.Append("<color=#55FF55>药水:</color> ").Append(s.Runtime?.Potions ?? 0).Append('/').Append(PlayerRuntimeStats.MaxPotions);

        _statsText.text = sb.ToString();
    }

    /// <summary>
    /// Remove orphaned buttons from previous compiled versions (ReturnButton, SettingsButton, SettingsPanel).
    /// </summary>
    private void CleanupStaleButtons()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        string[] staleNames = { "ReturnButton", "SettingsButton", "SettingsPanel" };
        foreach (Transform child in canvas.transform)
        {
            foreach (var stale in staleNames)
            {
                if (child.name == stale)
                {
                    Destroy(child.gameObject);
                    break;
                }
            }
        }
    }

    private void Update()
    {
        PlayerController player = GameManager.Instance?.Player;
        if (player == null) return;

        // --- HP Bar (always check — visual slider) ---
        if (HpBar != null)
        {
            int maxHp = player.Stats.TotalMaxHp;
            int hp = player.Stats.Hp;
            if (hp != _lastHp || maxHp != _lastMaxHp)
            {
                HpBar.maxValue = maxHp;
                HpBar.value = hp;
                _lastHp = hp; _lastMaxHp = maxHp;
            }
        }

        // --- HUD texts — only update when values change ---
        int level = player.Stats.Level;
        if (LevelText != null && level != _lastLevel)
        {
            LevelText.text = UIStringBuilderPool.FormatLevel(level);
            _lastLevel = level;
        }

        int xp = player.Stats.Xp, xpNext = player.Stats.XpToNextLevel;
        if (XpText != null && (xp != _lastXp || xpNext != _lastXpToNext))
        {
            XpText.text = UIStringBuilderPool.FormatXp(xp, xpNext);
            _lastXp = xp; _lastXpToNext = xpNext;
        }

        int dl = GameManager.Instance.DungeonLevel;
        int wave = CombatDirector.Instance?.CurrentStageWave ?? 0;
        if (DungeonLevelText != null && (dl != _lastDungeonLevel || wave != _lastWave))
        {
            DungeonLevelText.text = UIStringBuilderPool.FormatDungeonInfo(dl, wave, GameManager.WavesPerStage);
            _lastDungeonLevel = dl; _lastWave = wave;
        }

        // 显示当前地图名称
        if (MapNameText != null && GameManager.Instance != null)
        {
            int stageIdx = GameManager.Instance.CurrentStageIndex;
            string mapName = stageIdx switch
            {
                0 => "石墓深渊", 1 => "幽暗森林", 2 => "冰霜洞窟", 3 => "烈焰熔岩",
                4 => "暗影领域", 5 => "龙巢", 6 => "虚空裂隙", 7 => "魔王殿",
                _ => $"第{stageIdx + 1}关"
            };
            string mode = GameManager.Instance.IsEndlessMode ? " [无尽]" : "";
            MapNameText.text = $"{mapName}  {dl}F  波{wave}/{GameManager.WavesPerStage}{mode}";
        }

        int gold = player.Stats.Gold;
        if (GoldText != null && gold != _lastGold)
        {
            GoldText.text = UIStringBuilderPool.FormatGold(gold);
            _lastGold = gold;
        }

        // Potion count display
        int potions = player.Stats.Runtime?.Potions ?? 0;
        if (potionText != null)
            potionText.text = $"{potions}/{PlayerRuntimeStats.MaxPotions} [H]";

        int alive = CombatDirector.Instance?.AliveEnemies ?? 0;
        if (EnemyCountText != null && alive != _lastAlive)
        {
            EnemyCountText.text = UIStringBuilderPool.FormatEnemyRemaining(alive);
            _lastAlive = alive;
        }

        UpdateBossHealthBar();

        // Shield bar
        if (ShieldBar != null)
        {
            bool hasShield = player.Stats.ShieldHp > 0 && Time.time < player.Stats.ShieldEndTime;
            ShieldBar.SetActive(hasShield);
        }

        // Stat details — update every 0.25s, ensure visible
        if (StatDetailText != null && Time.time - _lastStatUpdate > 0.25f)
        {
            _lastStatUpdate = Time.time;
            var s = player.Stats;
            StatDetailText.text = UIStringBuilderPool.FormatStats(
                s.BuffedAttack, s.BuffedDefense, s.TotalMoveSpeed,
                s.HpRegen, s.TotalCritChance, s.TotalLifeSteal,
                s.TotalAttackRange, s.TotalAttackSpeed, s.ShieldHp);
            // Ensure stat text is bright enough to read
            if (StatDetailText.color.a < 0.8f)
                StatDetailText.color = new Color(0.85f, 0.82f, 0.88f, 1f);
        }

        // Combo counter
        if (ComboText != null)
        {
            if (player.ComboCount >= 3)
            {
                ComboText.gameObject.SetActive(true);
                var csb = UIStringBuilderPool.Get();
                csb.Append(player.ComboCount).Append(" COMBO!");
                ComboText.text = csb.ToString();
                float scale = 1f + Mathf.Sin(Time.unscaledTime * 6f) * 0.1f;
                ComboText.transform.localScale = new Vector3(scale, scale, 1f);
            }
            else
            {
                ComboText.gameObject.SetActive(false);
            }
        }

        // Skill cooldowns
        UpdateSkillCooldowns(player);

        // Sync auto-battle button label
        if (_autoBattleLabel != null && player.Combat != null)
        {
            bool isAuto = player.Combat.IsAutoBattle;
            if (_autoBattleLabel.text != (isAuto ? "停止" : "挂机"))
            {
                _autoBattleLabel.text = isAuto ? "停止" : "挂机";
                var btnImg = _autoBattleBtn?.GetComponent<Image>();
                if (btnImg != null)
                    btnImg.color = isAuto
                        ? new Color(0.7f, 0.2f, 0.2f, 0.85f)
                        : new Color(0.2f, 0.5f, 0.2f, 0.85f);
            }
        }

        if (GameOverPanel != null)
        {
            // 只在GameOver状态显示，且不在Hub/Dungeon状态重新显示
            bool shouldShow = GameManager.Instance.IsGameOver && !GameManager.Instance.IsInDungeon && !GameManager.Instance.IsInHub;
            GameOverPanel.SetActive(shouldShow);
            if (shouldShow && GameOverStatsText != null)
            {
                GameOverStatsText.text = $"等级: {player.Stats.Level}\n地下城: {GameManager.Instance.DungeonLevel}F\n击杀: {CombatDirector.Instance?.EnemiesKilled ?? 0}";
            }
        }

        // FPS counter
        if (_fpsText != null)
        {
            _fpsText.gameObject.SetActive(GameSettings.FPSDisplay);
            if (GameSettings.FPSDisplay)
            {
                _fpsFrames++;
                _fpsTimer += Time.unscaledDeltaTime;
                if (_fpsTimer >= 0.5f)
                {
                    int fps = Mathf.RoundToInt(_fpsFrames / _fpsTimer);
                    _fpsText.text = $"{fps} FPS";
                    _fpsText.color = fps >= 50 ? new Color(0.4f, 0.9f, 0.3f, 0.8f) : fps >= 30 ? new Color(0.9f, 0.7f, 0.2f, 0.8f) : new Color(0.9f, 0.3f, 0.2f, 0.8f);
                    _fpsFrames = 0;
                    _fpsTimer = 0f;
                }
            }
        }

        // Tab key toggles detailed stats panel
        if (Input.GetKeyDown(KeyCode.Tab) && GameManager.Instance.IsInDungeon && !GameManager.Instance.IsGameOver)
        {
            _statsPanelVisible = !_statsPanelVisible;
            if (_statsPanel == null) CreateStatsPanel();
            if (_statsPanel != null) _statsPanel.SetActive(_statsPanelVisible);
            if (_statsPanelVisible) UpdateStatsPanel(player);
        }

        // Update stats panel content every 0.5s when visible
        if (_statsPanelVisible && _statsPanel != null && Time.time - _statsPanelTimer > 0.5f)
        {
            _statsPanelTimer = Time.time;
            UpdateStatsPanel(player);
        }

        // Pause menu
        if (Input.GetKeyDown(KeyCode.Escape) && GameManager.Instance.IsInDungeon && !GameManager.Instance.IsGameOver)
        {
            if (PausePanel != null && PausePanel.activeSelf)
                OnResumeFromPause();
            else
                GameManager.Instance.TogglePause();
        }

        if (PausePanel != null)
        {
            bool shouldShow = GameManager.Instance.IsPaused && GameManager.Instance.IsInDungeon;
            if (PausePanel.activeSelf != shouldShow)
                PausePanel.SetActive(shouldShow);
        }
    }

    private void UpdateSkillCooldowns(PlayerController player)
    {
        // Dash cooldown
        UpdateSingleCooldown(DashCooldownOverlay, DashCooldownText,
            player.DashCooldownRemaining, player.DashCooldownMax);

        // Skills use CD system — show cooldown timer
        UpdateSingleCooldown(Skill1CooldownOverlay, Skill1CooldownText,
            player.Skill1CooldownRemaining, player.Skill1CooldownMax);
        UpdateSingleCooldown(Skill2CooldownOverlay, Skill2CooldownText,
            player.Skill2CooldownRemaining, player.Skill2CooldownMax);
        UpdateSingleCooldown(Skill3CooldownOverlay, Skill3CooldownText,
            player.Skill3CooldownRemaining, player.Skill3CooldownMax);
    }

    private static void UpdateSingleCooldown(Image overlay, Text text, float remaining, float maxCd)
    {
        if (overlay != null)
            overlay.fillAmount = maxCd > 0 ? remaining / maxCd : 0f;
        if (text != null)
        {
            if (remaining > 0)
            {
                text.gameObject.SetActive(true);
                text.text = remaining.ToString("F1");
            }
            else
            {
                text.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Show or hide all gameplay HUD elements (HP bar, joystick, skill buttons, etc.)
    /// Call with false when showing full-screen menus (Title, Hub, CharacterSelect, etc.)
    /// </summary>
    public void SetHUDVisible(bool visible)
    {
        SetHUDVisibleInternal(visible);
    }

    public void ShowHubMode(bool inHub)
    {
        SetHUDVisibleInternal(!inHub);
        if (inHub) _itemToastCount = 0;
    }

    private void SetHUDVisibleInternal(bool visible)
    {
        if (HpBar != null) HpBar.gameObject.SetActive(visible);
        if (LevelText != null) LevelText.gameObject.SetActive(visible);
        if (XpText != null) XpText.gameObject.SetActive(visible);
        if (DungeonLevelText != null) DungeonLevelText.gameObject.SetActive(visible);
        if (EnemyCountText != null) EnemyCountText.gameObject.SetActive(visible);
        if (GoldText != null) GoldText.gameObject.SetActive(visible);
        if (StatDetailText != null) StatDetailText.gameObject.SetActive(visible);
        if (_statsPanel != null && !visible) { _statsPanel.SetActive(false); _statsPanelVisible = false; }
        if (ShieldBar != null) ShieldBar.SetActive(false);
        if (!visible && BossHealthPanel != null) BossHealthPanel.SetActive(false);

        // Skill buttons
        if (Skill1CooldownOverlay != null) Skill1CooldownOverlay.transform.parent.gameObject.SetActive(visible);
        if (Skill2CooldownOverlay != null) Skill2CooldownOverlay.transform.parent.gameObject.SetActive(visible);
        if (Skill3CooldownOverlay != null) Skill3CooldownOverlay.transform.parent.gameObject.SetActive(visible);
        if (DashCooldownOverlay != null) DashCooldownOverlay.transform.parent.gameObject.SetActive(visible);

        // Attack button
        if (AttackButtonObj != null) AttackButtonObj.SetActive(visible);
        else { var atkBtn = FindObjectOfType<AttackButton>(); if (atkBtn != null) { AttackButtonObj = atkBtn.gameObject; AttackButtonObj.SetActive(visible); } }

        // Joystick
        if (JoystickObj != null) JoystickObj.SetActive(visible);
        else { var joystick = FindObjectOfType<VirtualJoystick>(); if (joystick != null) { JoystickObj = joystick.gameObject; JoystickObj.SetActive(visible); } }

        // Nav buttons
        if (NavLayer != null) NavLayer.SetActive(visible);

        // Potion display
        if (potionText != null) potionText.gameObject.SetActive(visible);

        // Auto-battle button
        if (_autoBattleBtn != null) _autoBattleBtn.SetActive(visible);
    }

    // ==== Boss health panel — moved to GameUI.Boss.cs (partial class) ====

    public void ShowDungeonComplete(int goldReward)
    {
        // 每次重建面板, 确保显示最新loot
        if (DungeonCompletePanel != null)
            Destroy(DungeonCompletePanel);

        Font font = GameManager.GetUIFont();
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        DungeonCompletePanel = new GameObject("DungeonCompletePanel");
        DungeonCompletePanel.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = DungeonCompletePanel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero; panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero; panelRect.offsetMax = Vector2.zero;
        Image bg = DungeonCompletePanel.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.85f);

        var player = GameManager.Instance?.Player;

        // Title
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(DungeonCompletePanel.transform, false);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1); titleRect.anchorMax = new Vector2(0.5f, 1);
        titleRect.pivot = new Vector2(0.5f, 1); titleRect.anchoredPosition = new Vector2(0, -50);
        titleRect.sizeDelta = new Vector2(400, 50);
        Text titleText = titleObj.AddComponent<Text>();
        titleText.text = "副本完成!"; titleText.alignment = TextAnchor.MiddleCenter;
        titleText.fontSize = 38; titleText.color = new Color(1f, 0.9f, 0.2f); titleText.font = font;

        // Build loot summary content
        string lootSummary = BuildRunLootSummary(player, goldReward);

        // Scrollable loot area
        GameObject scrollObj = new GameObject("LootArea");
        scrollObj.transform.SetParent(DungeonCompletePanel.transform, false);
        RectTransform scrollRect = scrollObj.AddComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0.1f, 0.18f); scrollRect.anchorMax = new Vector2(0.9f, 0.88f);
        scrollRect.offsetMin = Vector2.zero; scrollRect.offsetMax = Vector2.zero;
        Image scrollBg = scrollObj.AddComponent<Image>();
        scrollBg.color = new Color(0.05f, 0.05f, 0.1f, 0.9f);

        GameObject lootTextObj = new GameObject("LootText");
        lootTextObj.transform.SetParent(scrollObj.transform, false);
        RectTransform lootRect = lootTextObj.AddComponent<RectTransform>();
        lootRect.anchorMin = Vector2.zero; lootRect.anchorMax = Vector2.one;
        lootRect.offsetMin = new Vector2(15, 10); lootRect.offsetMax = new Vector2(-15, -10);
        Text lootText = lootTextObj.AddComponent<Text>();
        lootText.text = lootSummary; lootText.alignment = TextAnchor.UpperCenter;
        lootText.fontSize = 20; lootText.color = Color.white; lootText.font = font;
        lootText.supportRichText = true;

        // Return to hub button
        GameObject returnObj = new GameObject("ReturnBtn");
        returnObj.transform.SetParent(DungeonCompletePanel.transform, false);
        RectTransform returnRect = returnObj.AddComponent<RectTransform>();
        returnRect.anchorMin = new Vector2(0.5f, 0); returnRect.anchorMax = new Vector2(0.5f, 0);
        returnRect.pivot = new Vector2(0.5f, 0.5f); returnRect.anchoredPosition = new Vector2(0, 60);
        returnRect.sizeDelta = new Vector2(250, 55);
        Image returnImg = returnObj.AddComponent<Image>();
        returnImg.color = new Color(0.2f, 0.6f, 1f, 0.9f);
        Button returnBtn = returnObj.AddComponent<Button>();
        returnBtn.onClick.AddListener(() =>
        {
            // 清理战场残留toast + 运行时掉落记录
            ClearAllToasts();
            DungeonCompletePanel.SetActive(false);
            if (GameManager.Instance.Player != null)
                GameManager.Instance.Player.ResetRunLoot();
            GameManager.Instance.ShowHub();
        });
        GameObject rlObj = new GameObject("Label");
        rlObj.transform.SetParent(returnObj.transform, false);
        RectTransform rlRect = rlObj.AddComponent<RectTransform>();
        rlRect.anchorMin = Vector2.zero; rlRect.anchorMax = Vector2.one;
        rlRect.offsetMin = Vector2.zero; rlRect.offsetMax = Vector2.zero;
        Text rlText = rlObj.AddComponent<Text>();
        rlText.text = "返回大厅"; rlText.alignment = TextAnchor.MiddleCenter;
        rlText.fontSize = 24; rlText.color = Color.white; rlText.font = font;
    }

    private string BuildRunLootSummary(PlayerController player, int goldReward)
    {
        var sb = new System.Text.StringBuilder();

        // Gold
        int totalGold = goldReward + (player != null ? player.RunGoldEarned : 0);
        sb.AppendLine($"<color=#FFD933>◆ 获得金币: {totalGold}</color>");

        // XP
        int totalXp = player != null ? player.RunXpEarned : 0;
        if (totalXp > 0)
            sb.AppendLine($"<color=#88FF88>◆ 获得经验: {totalXp}</color>");
        sb.AppendLine();

        // Items
        if (player != null && player.RunLootItems.Count > 0)
        {
            sb.AppendLine("<color=#88CCFF>━━ 装备 ━━</color>");
            foreach (var item in player.RunLootItems)
            {
                string rarityColor = GetRarityHex(item.Rarity);
                sb.AppendLine($"  <color={rarityColor}>{item.Name}</color>");
                sb.AppendLine($"  <size=16><color=#aaaaaa>{item.GetStatSummary()}</color></size>");
            }
            sb.AppendLine();
        }

        // Relics
        if (player != null && player.RunLootRelics.Count > 0)
        {
            sb.AppendLine("<color=#FFCC44>━━ 遗物 ━━</color>");
            foreach (var relic in player.RunLootRelics)
            {
                sb.AppendLine($"  <color=#FFD933>{relic.Name}</color>");
                sb.AppendLine($"  <size=16><color=#aaaaaa>{relic.Description}</color></size>");
            }
            sb.AppendLine();
        }

        // Stats summary
        if (player != null)
        {
            sb.AppendLine("<color=#AADDAA>━━ 战绩 ━━</color>");
            int kills = CombatDirector.Instance != null ? CombatDirector.Instance.EnemiesKilled : 0;
            sb.AppendLine($"  击杀: {kills}  |  等级: {player.Stats.Level}");
        }

        return sb.ToString();
    }

    private static string GetRarityHex(ItemRarity rarity)
    {
        return rarity switch
        {
            ItemRarity.Legendary => "#FF8800",
            ItemRarity.Epic => "#AA44FF",
            ItemRarity.Rare => "#4488FF",
            _ => "#CCCCCC"
        };
    }

    public void ShowWaveAnnouncement(int wave)
    {
        if (WaveAnnounceText != null)
        {
            StartCoroutine(WaveAnnounceCoroutine(wave));
        }
    }

    private IEnumerator WaveAnnounceCoroutine(int wave)
    {
        WaveAnnounceText.gameObject.SetActive(true);
        WaveAnnounceText.text = $"第 {wave} 波";
        if (WaveAnnounceText.fontSize > 28) WaveAnnounceText.fontSize = 28;

        float duration = 1.2f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float progress = t / duration;

            if (progress < 0.15f)
            {
                float s = Mathf.Lerp(0.5f, 1.0f, progress / 0.15f);
                WaveAnnounceText.transform.localScale = new Vector3(s, s, 1f);
                Color c = WaveAnnounceText.color;
                c.a = progress / 0.15f;
                WaveAnnounceText.color = c;
            }
            else if (progress > 0.7f)
            {
                Color c = WaveAnnounceText.color;
                c.a = 1f - (progress - 0.7f) / 0.3f;
                WaveAnnounceText.color = c;
            }

            yield return null;
        }

        WaveAnnounceText.gameObject.SetActive(false);
    }

    // ==== Toasts — moved to GameUI.Toasts.cs (partial class) ====

    public void OnRestartButton()
    {
        GameManager.Instance.RestartGame();
    }

    public void OnResumeFromPause()
    {
        GameManager.Instance.IsPaused = false;
        Time.timeScale = 1f;
        if (PausePanel != null) PausePanel.SetActive(false);
    }

    public void OnReturnToHubFromPause()
    {
        GameManager.Instance.IsPaused = false;
        Time.timeScale = 1f;
        if (PausePanel != null) PausePanel.SetActive(false);
        GameManager.Instance.ShowHub();
    }

    public void ShowVictoryScreen(int goldReward)
    {
        Font font = GameManager.GetUIFont();
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        GameObject victoryPanel = new GameObject("VictoryPanel");
        victoryPanel.transform.SetParent(canvas.transform, false);
        RectTransform vpRect = victoryPanel.AddComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero; vpRect.anchorMax = Vector2.one;
        vpRect.offsetMin = Vector2.zero; vpRect.offsetMax = Vector2.zero;
        Image vpBg = victoryPanel.AddComponent<Image>();
        vpBg.color = new Color(0.02f, 0.01f, 0.05f, 0.92f);

        // Victory title
        GameObject titleObj = new GameObject("VictoryTitle");
        titleObj.transform.SetParent(victoryPanel.transform, false);
        RectTransform tRect = titleObj.AddComponent<RectTransform>();
        tRect.anchorMin = new Vector2(0.5f, 1); tRect.anchorMax = new Vector2(0.5f, 1);
        tRect.pivot = new Vector2(0.5f, 1); tRect.anchoredPosition = new Vector2(0, -50);
        tRect.sizeDelta = new Vector2(500, 70);
        Text titleText = titleObj.AddComponent<Text>();
        titleText.text = "通 关 !"; titleText.alignment = TextAnchor.MiddleCenter;
        titleText.fontSize = 48; titleText.color = new Color(1f, 0.85f, 0.1f); titleText.font = font;

        // Subtitle
        GameObject subObj = new GameObject("Subtitle");
        subObj.transform.SetParent(victoryPanel.transform, false);
        RectTransform sRect = subObj.AddComponent<RectTransform>();
        sRect.anchorMin = new Vector2(0.5f, 1); sRect.anchorMax = new Vector2(0.5f, 1);
        sRect.pivot = new Vector2(0.5f, 1); sRect.anchoredPosition = new Vector2(0, -110);
        sRect.sizeDelta = new Vector2(400, 30);
        Text subText = subObj.AddComponent<Text>();
        subText.text = "你击败了魔王，拯救了世界"; subText.alignment = TextAnchor.MiddleCenter;
        subText.fontSize = 20; subText.color = new Color(0.8f, 0.75f, 0.9f); subText.font = font;

        // Loot summary area
        var player = GameManager.Instance?.Player;
        string lootSummary = BuildRunLootSummary(player, goldReward);

        GameObject lootArea = new GameObject("LootArea");
        lootArea.transform.SetParent(victoryPanel.transform, false);
        RectTransform lRect = lootArea.AddComponent<RectTransform>();
        lRect.anchorMin = new Vector2(0.1f, 0.25f); lRect.anchorMax = new Vector2(0.9f, 0.82f);
        lRect.offsetMin = Vector2.zero; lRect.offsetMax = Vector2.zero;
        Image lBg = lootArea.AddComponent<Image>();
        lBg.color = new Color(0.05f, 0.05f, 0.1f, 0.9f);

        GameObject lootTextObj = new GameObject("LootText");
        lootTextObj.transform.SetParent(lootArea.transform, false);
        RectTransform ltRect = lootTextObj.AddComponent<RectTransform>();
        ltRect.anchorMin = Vector2.zero; ltRect.anchorMax = Vector2.one;
        ltRect.offsetMin = new Vector2(15, 10); ltRect.offsetMax = new Vector2(-15, -10);
        Text lootText = lootTextObj.AddComponent<Text>();
        lootText.text = lootSummary; lootText.alignment = TextAnchor.UpperCenter;
        lootText.fontSize = 20; lootText.color = Color.white; lootText.font = font;
        lootText.supportRichText = true;

        // Credits
        GameObject credObj = new GameObject("Credits");
        credObj.transform.SetParent(victoryPanel.transform, false);
        RectTransform cRect = credObj.AddComponent<RectTransform>();
        cRect.anchorMin = new Vector2(0.5f, 0); cRect.anchorMax = new Vector2(0.5f, 0);
        cRect.pivot = new Vector2(0.5f, 0.5f); cRect.anchoredPosition = new Vector2(0, 230);
        cRect.sizeDelta = new Vector2(300, 25);
        Text credText = credObj.AddComponent<Text>();
        credText.text = "卫冕战争 — 感谢游玩"; credText.alignment = TextAnchor.MiddleCenter;
        credText.fontSize = 16; credText.color = new Color(0.6f, 0.6f, 0.65f); credText.font = font;

        // Endless mode button
        GameObject endlessObj = new GameObject("EndlessBtn");
        endlessObj.transform.SetParent(victoryPanel.transform, false);
        RectTransform eRect = endlessObj.AddComponent<RectTransform>();
        eRect.anchorMin = new Vector2(0.5f, 0); eRect.anchorMax = new Vector2(0.5f, 0);
        eRect.pivot = new Vector2(0.5f, 0.5f); eRect.anchoredPosition = new Vector2(0, 190);
        eRect.sizeDelta = new Vector2(250, 50);
        Image eImg = endlessObj.AddComponent<Image>();
        eImg.color = new Color(0.8f, 0.2f, 0.2f, 0.9f);
        Button eBtn = endlessObj.AddComponent<Button>();
        eBtn.onClick.AddListener(() => { Destroy(victoryPanel); GameManager.Instance.StartEndlessMode(); });
        GameObject elObj = new GameObject("Label");
        elObj.transform.SetParent(endlessObj.transform, false);
        RectTransform elRect = elObj.AddComponent<RectTransform>();
        elRect.anchorMin = Vector2.zero; elRect.anchorMax = Vector2.one;
        elRect.offsetMin = Vector2.zero; elRect.offsetMax = Vector2.zero;
        Text elText = elObj.AddComponent<Text>();
        elText.text = "无尽模式"; elText.alignment = TextAnchor.MiddleCenter;
        elText.fontSize = 22; elText.color = Color.white; elText.font = font;

        // Return button
        GameObject retObj = new GameObject("ReturnBtn");
        retObj.transform.SetParent(victoryPanel.transform, false);
        RectTransform rRect = retObj.AddComponent<RectTransform>();
        rRect.anchorMin = new Vector2(0.5f, 0); rRect.anchorMax = new Vector2(0.5f, 0);
        rRect.pivot = new Vector2(0.5f, 0.5f); rRect.anchoredPosition = new Vector2(0, 130);
        rRect.sizeDelta = new Vector2(250, 50);
        Image retImg = retObj.AddComponent<Image>();
        retImg.color = new Color(1f, 0.75f, 0.1f, 0.9f);
        Button retBtn = retObj.AddComponent<Button>();
        retBtn.onClick.AddListener(() => { Destroy(victoryPanel); GameManager.Instance.ShowHub(); });
        GameObject rlObj = new GameObject("Label");
        rlObj.transform.SetParent(retObj.transform, false);
        RectTransform rlRect = rlObj.AddComponent<RectTransform>();
        rlRect.anchorMin = Vector2.zero; rlRect.anchorMax = Vector2.one;
        rlRect.offsetMin = Vector2.zero; rlRect.offsetMax = Vector2.zero;
        Text rlText = rlObj.AddComponent<Text>();
        rlText.text = "返回大厅"; rlText.alignment = TextAnchor.MiddleCenter;
        rlText.fontSize = 22; rlText.color = new Color(0.15f, 0.1f, 0f); rlText.font = font;
    }
}
