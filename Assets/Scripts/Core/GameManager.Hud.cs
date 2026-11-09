using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// GameManager partial — HUD building methods extracted from GameManager.cs
/// Contains: BuildUI, BuildHUDElements, BuildPausePanel, BuildGameOverPanel, DestroyUIComponent
/// 旧版HUD元素(JoystickBg/HpBar/LevelText/XpBar/XpText/AttackButton/DashBtn/Skill1-3Btn/GoldText/DungeonLevelText/MapNameText/HudNavBtns)已删除 — 由InGameHud全量替代
/// </summary>
public partial class GameManager
{
    /// <summary>HUD幂等自愈 — 供世界等外部流程调用 (Canvas被毁后重建)</summary>
    public void EnsureHud()
    {
        if (GameUI.Instance == null || GameUI.Instance.ShieldBar == null)
            BuildUI();
    }

    private void BuildUI()
    {
        Canvas canvas = EnsureCanvas();

        GameUI gameUI = canvas.GetComponent<GameUI>();
        if (gameUI == null) gameUI = canvas.gameObject.AddComponent<GameUI>();

        if (gameUI.ShieldBar != null)
            return;

        BuildHUDElements(canvas.transform, gameUI);
    }

    private void BuildHUDElements(Transform canvasTransform, GameUI gameUI)
    {
        Font font = GameManager.GetUIFont();

        // 旧版元素(JoystickBg/HpBar/HpText/LevelText/XpBar/XpText/AttackButton/DashBtn/Skill1-3Btn)已删除 — InGameHud全量替代
        // ShieldBar保留(护盾条, 默认隐藏, 未被InGameHud覆盖, 兼作BuildUI幂等锚)
        GameObject shieldBarObj = new GameObject("ShieldBar");
        shieldBarObj.transform.SetParent(canvasTransform, false);
        RectTransform shieldRect = shieldBarObj.AddComponent<RectTransform>();
        shieldRect.anchorMin = new Vector2(0, 1); shieldRect.anchorMax = new Vector2(0, 1);
        shieldRect.pivot = new Vector2(0, 0.5f); shieldRect.anchoredPosition = new Vector2(54, -86);
        shieldRect.sizeDelta = new Vector2(300, 8);
        Image shieldBg = shieldBarObj.AddComponent<Image>(); shieldBg.color = new Color(0.06f, 0.06f, 0.1f, 0.9f);

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

        // 剩余敌人 (右上角, 挂机按钮左侧)
        var enemyText = gameUI.EnemyCountText = HudSpriteFactory.CreateText(canvasTransform, "EnemyCountText", new Vector2(-170, -20), new Vector2(70, 25), 17, new Color(1f, 0.6f, 0.6f));
        var enemyRect = enemyText.GetComponent<RectTransform>();
        enemyRect.anchorMin = new Vector2(1, 1); enemyRect.anchorMax = new Vector2(1, 1);
        enemyRect.pivot = new Vector2(1, 1); enemyRect.anchoredPosition = new Vector2(-170, -20);
        enemyText.alignment = TextAnchor.MiddleRight;

        gameUI.BindBossHealthPanel(canvasTransform.GetComponentInParent<Canvas>());

        // 旧版摇杆/攻击按钮/技能键已删除 — InGameHud全量替代

        BuildPausePanel(canvasTransform, gameUI, font);

        BuildGameOverPanel(canvasTransform, gameUI, font);

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

        GameObject ppTitle = new GameObject("Title");
        ppTitle.transform.SetParent(pausePanel.transform, false);
        RectTransform ppTitleR = ppTitle.AddComponent<RectTransform>();
        ppTitleR.anchorMin = new Vector2(0.5f, 1); ppTitleR.anchorMax = new Vector2(0.5f, 1);
        ppTitleR.pivot = new Vector2(0.5f, 1); ppTitleR.anchoredPosition = new Vector2(0, -120);
        ppTitleR.sizeDelta = new Vector2(300, 50);
        Text ppTitleT = ppTitle.AddComponent<Text>();
        ppTitleT.text = "暂停"; ppTitleT.alignment = TextAnchor.MiddleCenter;
        ppTitleT.fontSize = 36; ppTitleT.color = Color.white; ppTitleT.font = font;

        UIHelper.MakeButton(pausePanel.transform, "ResumeBtn", "继续游戏", font,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-125, 22.5f), new Vector2(125, 77.5f),
            new Color(0.2f, 0.6f, 1f, 0.9f), 24, () => gameUI.OnResumeFromPause());

        UIHelper.MakeButton(pausePanel.transform, "SettingsBtn", "设置", font,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-125, -42.5f), new Vector2(125, 12.5f),
            new Color(0.3f, 0.3f, 0.4f, 0.9f), 24, () => { var s = FindObjectOfType<SettingsUI>(); if (s == null) s = gameObject.AddComponent<SettingsUI>(); s.Show(); });

        UIHelper.MakeButton(pausePanel.transform, "ReturnBtn", "返回大厅", font,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-125, -107.5f), new Vector2(125, -52.5f),
            new Color(0.7f, 0.2f, 0.2f, 0.9f), 24, () => gameUI.OnReturnToHubFromPause());

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

        UIHelper.MakeButton(gameOverPanel.transform, "BackButton", "返回", font,
            new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-100, 75), new Vector2(100, 125),
            new Color(0.2f, 0.6f, 1f, 0.9f), 24, () => { if (GameUI.Instance?.GameOverPanel != null) GameUI.Instance.GameOverPanel.SetActive(false); ReturnToHubExitingWorld(); });

        UIHelper.MakeButton(gameOverPanel.transform, "RetryButton", "重新挑战", font,
            new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-100, 145), new Vector2(100, 195),
            new Color(0.8f, 0.3f, 0.1f, 0.9f), 24, () =>
            {
                if (GameUI.Instance?.GameOverPanel != null) GameUI.Instance.GameOverPanel.SetActive(false);
                if (DiedInWorld)
                {
                    DiedInWorld = false;
                    WorldManager.EnterWorld(0); // 世界内死亡: 重进世界安全区
                }
                else RetryStage();
            });

        UIHelper.MakeButton(gameOverPanel.transform, "ReviveButton", "原地复活 50位面", font,
            new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-100, 210), new Vector2(100, 260),
            new Color(0.55f, 0.35f, 0.9f, 0.9f), 20, ReviveInPlace);
    }

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
}
