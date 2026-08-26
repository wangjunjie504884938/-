using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI调度器 — 同一时间只有一个全屏面板可见
/// 规则：每个UI的Show/Hide只操作panel.SetActive，不做其他逻辑
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }
    public string ActivePanel { get; private set; } = "";

    // 所有已知面板名（Canvas 上的 GameObject 名）
    private static readonly string[] AllPanelNames = {
        "HubPanel", "ShopPanel", "SkillTreePanel", "RuneEquipPanel",
        "EquipPanel", "StageSelectPanel", "SettingsPanel",
        "CharacterSelectPanel", "CharacterSlotPanel", "TitleScreenPanel",
        "DailyTaskPanel", "GachaPanel", "LeaderboardPanel", "MailPanel",
        "FragmentShopPanel", "BossCodexPanel", "DailyChallengePanel", "AchievementPanel", "PassiveTreePanel", "ForgePanel", "SeasonPassPanel", "AsyncPvpPanel", "GuildPanel"
    };

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    /// <summary>隐藏所有面板（除指定面板外），同时清理 Canvas 上残留的面板对象</summary>
    public void HideAllExcept(string keepPanel)
    {
        ActivePanel = keepPanel;

        // 先通过 Instance 隐藏（触发各 UI 的 Hide 逻辑）
        if (keepPanel != "Hub")            HubUI.Instance?.Hide();
        if (keepPanel != "Shop")           ShopUI.Instance?.Hide();
        if (keepPanel != "SkillTree")      SkillTreeUI.Instance?.Hide();
        if (keepPanel != "RuneEquip")      RuneEquipUI.Instance?.Hide();
        if (keepPanel != "Equip")           EquipUI.Instance?.Hide();
        if (keepPanel != "StageSelect")    StageSelectUI.Instance?.Hide();
        if (keepPanel != "Settings")       SettingsUI.Instance?.Hide();
        if (keepPanel != "CharacterSelect") CharacterSelectUI.Instance?.Hide();
        if (keepPanel != "TitleScreen")     TitleScreen.Instance?.Hide();
        if (keepPanel != "DailyTask")       DailyTaskUI.Instance?.Hide();
        if (keepPanel != "Gacha")           GachaUI.Instance?.Hide();
        if (keepPanel != "Leaderboard")     LeaderboardUI.Instance?.Hide();
        if (keepPanel != "Mail")            MailUI.Instance?.Hide();
        if (keepPanel != "FragmentShop")    FragmentShopUI.Instance?.Hide();
        if (keepPanel != "BossCodex")       BossCodexUI.Instance?.Hide();
        if (keepPanel != "DailyChallenge")  DailyChallengeUI.Instance?.Hide();
        if (keepPanel != "Achievement")     AchievementUI.Instance?.Hide();
        if (keepPanel != "PassiveTree")     PassiveTreeUI.Instance?.Hide();
        if (keepPanel != "Forge")           ForgeUI.Instance?.Hide();
        if (keepPanel != "SeasonPass")      SeasonPassUI.Instance?.Hide();
        if (keepPanel != "AsyncPvp")        AsyncPvpUI.Instance?.Hide();
        if (keepPanel != "Guild")           GuildUI.Instance?.Hide();

        // 安全网：直接在 Canvas 上按名称隐藏，防止 Instance 已 null 但面板残留
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;
        string keepObj = keepPanel + "Panel"; // 命名规则：Hub → HubPanel
        if (keepPanel == "TitleScreen") keepObj = "TitleScreenPanel";

        foreach (var panelName in AllPanelNames)
        {
            if (panelName == keepObj) continue;
            Transform t = canvas.transform.Find(panelName);
            if (t != null && t.gameObject.activeSelf)
                t.gameObject.SetActive(false);
        }
    }

    // ====== Show 方法：先隐藏所有，再显示目标 ======

    public void ShowHub()
    {
        HideAllExcept("Hub");
        if (HubUI.Instance == null) GameManager.Instance.gameObject.AddComponent<HubUI>();
        HubUI.Instance.Show();
    }

    public void ShowShop()
    {
        HideAllExcept("Shop");
        if (ShopUI.Instance == null) GameManager.Instance.gameObject.AddComponent<ShopUI>();
        ShopUI.Instance.Show();
    }

    public void ShowSkillTree()
    {
        HideAllExcept("SkillTree");
        if (SkillTreeUI.Instance == null) GameManager.Instance.gameObject.AddComponent<SkillTreeUI>();
        SkillTreeUI.Instance.Show();
    }

    public void ShowRuneEquip(int skillIndex = 0)
    {
        HideAllExcept("RuneEquip");
        if (RuneEquipUI.Instance == null) GameManager.Instance.gameObject.AddComponent<RuneEquipUI>();
        RuneEquipUI.Instance.Show(skillIndex);
    }

    public void ShowEquip()
    {
        HideAllExcept("Equip");
        if (EquipUI.Instance == null) GameManager.Instance.gameObject.AddComponent<EquipUI>();
        EquipUI.Instance.Show();
    }

    public void ShowStageSelect()
    {
        HideAllExcept("StageSelect");
        if (StageSelectUI.Instance == null) GameManager.Instance.gameObject.AddComponent<StageSelectUI>();
        StageSelectUI.Instance.Show();
    }

    public void ShowSettings()
    {
        // Settings is an overlay — don't hide the panel underneath
        if (SettingsUI.Instance == null) GameManager.Instance.gameObject.AddComponent<SettingsUI>();
        SettingsUI.Instance.Show();
    }

    public void ShowDailyTask()
    {
        HideAllExcept("DailyTask");
        if (DailyTaskUI.Instance == null) GameManager.Instance.gameObject.AddComponent<DailyTaskUI>();
        DailyTaskUI.Instance.Show();
    }

    public void ShowGacha()
    {
        HideAllExcept("Gacha");
        if (GachaUI.Instance == null) GameManager.Instance.gameObject.AddComponent<GachaUI>();
        GachaUI.Instance.Show();
    }

    public void ShowLeaderboard()
    {
        HideAllExcept("Leaderboard");
        if (LeaderboardUI.Instance == null) GameManager.Instance.gameObject.AddComponent<LeaderboardUI>();
        LeaderboardUI.Instance.Show();
    }

    public void ShowMail()
    {
        HideAllExcept("Mail");
        if (MailUI.Instance == null) GameManager.Instance.gameObject.AddComponent<MailUI>();
        MailUI.Instance.Show();
    }

    public void ShowFragmentShop()
    {
        HideAllExcept("FragmentShop");
        if (FragmentShopUI.Instance == null) GameManager.Instance.gameObject.AddComponent<FragmentShopUI>();
        FragmentShopUI.Instance.Show();
    }

    public void ShowBossCodex()
    {
        HideAllExcept("BossCodex");
        if (BossCodexUI.Instance == null) GameManager.Instance.gameObject.AddComponent<BossCodexUI>();
        BossCodexUI.Instance.Show();
    }

    public void ShowDailyChallenge()
    {
        HideAllExcept("DailyChallenge");
        if (DailyChallengeUI.Instance == null) GameManager.Instance.gameObject.AddComponent<DailyChallengeUI>();
        DailyChallengeUI.Instance.Show();
    }

    public void ShowAchievement()
    {
        HideAllExcept("Achievement");
        if (AchievementUI.Instance == null) GameManager.Instance.gameObject.AddComponent<AchievementUI>();
        AchievementUI.Instance.Show();
    }

    public void ShowPassiveTree()
    {
        HideAllExcept("PassiveTree");
        if (PassiveTreeUI.Instance == null) GameManager.Instance.gameObject.AddComponent<PassiveTreeUI>();
        PassiveTreeUI.Instance.Show();
    }

    public void ShowForge()
    {
        HideAllExcept("Forge");
        if (ForgeUI.Instance == null) GameManager.Instance.gameObject.AddComponent<ForgeUI>();
        ForgeUI.Instance.Show();
    }

    public void ShowSeasonPass()
    {
        HideAllExcept("SeasonPass");
        if (SeasonPassUI.Instance == null) GameManager.Instance.gameObject.AddComponent<SeasonPassUI>();
        SeasonPassUI.Instance.Show();
    }

    public void ShowAsyncPvp()
    {
        HideAllExcept("AsyncPvp");
        if (AsyncPvpUI.Instance == null) GameManager.Instance.gameObject.AddComponent<AsyncPvpUI>();
        AsyncPvpUI.Instance.Show();
    }

    public void ShowGuild()
    {
        HideAllExcept("Guild");
        if (GuildUI.Instance == null) GameManager.Instance.gameObject.AddComponent<GuildUI>();
        GuildUI.Instance.Show();
    }

    public void ShowFriend()
    {
        HideAllExcept("Friend");
        if (FriendUI.Instance == null) GameManager.Instance.gameObject.AddComponent<FriendUI>();
        FriendUI.Instance.Show();
    }
}
