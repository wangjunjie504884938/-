using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// GameManager partial — HUD building methods extracted from GameManager.cs
/// Contains: BuildUI, WireSkillButtons, BuildHUDElements, BuildPausePanel, BuildGameOverPanel,
/// DestroyUIComponent, BuildMobileJoystick, BuildAttackButton
/// </summary>
public partial class GameManager
{
    private void BuildUI()
    {
        Canvas canvas = EnsureCanvas();

        GameUI gameUI = canvas.GetComponent<GameUI>();
        if (gameUI == null) gameUI = canvas.gameObject.AddComponent<GameUI>();

        if (gameUI.HpBar != null)
        {
            WireSkillButtons(canvas.transform, gameUI);
            return;
        }

        BuildHUDElements(canvas.transform, gameUI);
    }

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

        GameObject portraitObj = new GameObject("PortraitIcon");
        portraitObj.transform.SetParent(canvasTransform, false);
        RectTransform portraitRect = portraitObj.AddComponent<RectTransform>();
        portraitRect.anchorMin = new Vector2(0, 1); portraitRect.anchorMax = new Vector2(0, 1);
        portraitRect.pivot = new Vector2(0.5f, 0.5f); portraitRect.anchoredPosition = new Vector2(30, -70);
        portraitRect.sizeDelta = new Vector2(36, 36);
        Image portraitImg = portraitObj.AddComponent<Image>();
        portraitImg.sprite = HudSpriteFactory.CreatePortraitSprite(32);
        portraitImg.color = Color.white;

        GameObject hpBarObj = new GameObject("HpBar");
        hpBarObj.transform.SetParent(canvasTransform, false);
        RectTransform hpBarRect = hpBarObj.AddComponent<RectTransform>();
        hpBarRect.anchorMin = new Vector2(0, 1); hpBarRect.anchorMax = new Vector2(0, 1);
        hpBarRect.pivot = new Vector2(0, 0.5f); hpBarRect.anchoredPosition = new Vector2(54, -70);
        hpBarRect.sizeDelta = new Vector2(300, 24);
        Image hpBg = hpBarObj.AddComponent<Image>(); hpBg.color = new Color(0.08f, 0.08f, 0.12f, 0.9f);

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

        gameUI.LevelText = HudSpriteFactory.CreateText(canvasTransform, "LevelText", new Vector2(12, -102), new Vector2(200, 30), 24, Color.white);
        gameUI.XpText = HudSpriteFactory.CreateText(canvasTransform, "XpText", new Vector2(12, -134), new Vector2(250, 28), 18, Color.cyan);
        gameUI.DungeonLevelText = HudSpriteFactory.CreateText(canvasTransform, "DungeonLevelText", new Vector2(12, -162), new Vector2(250, 28), 18, Color.yellow);
        gameUI.GoldText = HudSpriteFactory.CreateText(canvasTransform, "GoldText", new Vector2(12, -190), new Vector2(250, 28), 22, new Color(1f, 0.85f, 0.2f));
        gameUI.EnemyCountText = HudSpriteFactory.CreateText(canvasTransform, "EnemyCountText", new Vector2(12, -218), new Vector2(300, 28), 18, new Color(1f, 0.6f, 0.6f));

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

        BuildMobileJoystick(canvasTransform);
        BuildAttackButton(canvasTransform, font);

        var joyComp = FindObjectOfType<VirtualJoystick>();
        if (joyComp != null) gameUI.JoystickObj = joyComp.gameObject;
        var atkComp = FindObjectOfType<AttackButton>();
        if (atkComp != null) gameUI.AttackButtonObj = atkComp.gameObject;

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

        if (Player != null && Player.InputCtrl != null)
        {
            Player.InputCtrl.Skill1BtnObj = s1Btn.gameObject;
            Player.InputCtrl.Skill2BtnObj = s2Btn.gameObject;
            Player.InputCtrl.Skill3BtnObj = s3Btn.gameObject;
            Player.InputCtrl.DashBtnObj = dashBtn.gameObject;
            Player.InputCtrl.AttackBtnObj = atkComp.gameObject;
            Player.InputCtrl.WireButtonListeners();
        }

        BuildPausePanel(canvasTransform, gameUI, font);

        UIHelper.MakeNavButtons(canvasTransform, font, onBack: () =>
        {
            if (!IsPaused) TogglePause();
        });
        var navLayer = canvasTransform.Find("_NavLayer");
        if (navLayer != null) gameUI.NavLayer = navLayer.gameObject;

        var gearBtn = navLayer?.Find("GearBtn");
        if (gearBtn != null) Destroy(gearBtn.gameObject);

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

        UIHelper.MakeButton(gameOverPanel.transform, "RestartButton", "返回大厅", font,
            new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-100, 75), new Vector2(100, 125),
            new Color(0.2f, 0.6f, 1f, 0.9f), 24, () => { if (GameUI.Instance?.GameOverPanel != null) GameUI.Instance.GameOverPanel.SetActive(false); ShowHub(); });

        UIHelper.MakeButton(gameOverPanel.transform, "RetryButton", "重新挑战", font,
            new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-100, 145), new Vector2(100, 195),
            new Color(0.8f, 0.3f, 0.1f, 0.9f), 24, () => RetryStage());
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

    private void BuildMobileJoystick(Transform canvasTransform)
    {
        GameObject joystickBg = new GameObject("JoystickBg");
        joystickBg.transform.SetParent(canvasTransform, false);
        RectTransform joystickBgRect = joystickBg.AddComponent<RectTransform>();
        joystickBgRect.anchorMin = new Vector2(0, 0); joystickBgRect.anchorMax = new Vector2(0, 0);
        joystickBgRect.pivot = new Vector2(0.5f, 0.5f); joystickBgRect.anchoredPosition = new Vector2(170, 170);
        joystickBgRect.sizeDelta = new Vector2(220, 220);

        Image joystickBgImg = joystickBg.AddComponent<Image>();
        joystickBgImg.sprite = HudSpriteFactory.CreateJoystickRingSprite(64);
        joystickBgImg.color = Color.white;

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
        GameObject atkBtnObj = new GameObject("AttackButton");
        atkBtnObj.transform.SetParent(canvasTransform, false);
        RectTransform atkBtnRect = atkBtnObj.AddComponent<RectTransform>();
        atkBtnRect.anchorMin = new Vector2(1, 0); atkBtnRect.anchorMax = new Vector2(1, 0);
        atkBtnRect.pivot = new Vector2(0.5f, 0.5f); atkBtnRect.anchoredPosition = new Vector2(-140, 100);
        atkBtnRect.sizeDelta = new Vector2(140, 140);

        Image atkRingImg = atkBtnObj.AddComponent<Image>();
        atkRingImg.sprite = HudSpriteFactory.CreateOrnateRingSprite(64);
        atkRingImg.color = Color.white;

        GameObject atkInner = new GameObject("InnerFill");
        atkInner.transform.SetParent(atkBtnObj.transform, false);
        RectTransform atkInnerRect = atkInner.AddComponent<RectTransform>();
        atkInnerRect.anchorMin = new Vector2(0.5f, 0.5f); atkInnerRect.anchorMax = new Vector2(0.5f, 0.5f);
        atkInnerRect.pivot = new Vector2(0.5f, 0.5f); atkInnerRect.sizeDelta = new Vector2(120, 120);
        Image atkInnerImg = atkInner.AddComponent<Image>();
        atkInnerImg.sprite = HudSpriteFactory.CreateDarkFillSprite(64, new Color(0.5f, 0.08f, 0.08f));
        atkInnerImg.color = Color.white;

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
}
