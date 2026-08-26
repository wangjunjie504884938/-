using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class SkillTreeUI : MonoBehaviour
{
    public static SkillTreeUI Instance { get; private set; }

    private GameObject panel;
    private Text skillPointsText;
    private List<GameObject> skillButtons = new List<GameObject>();
    private Transform scrollContent;
    private Coroutine showRoutine;
    private Coroutine hideRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Show()
    {
        // Cancel any pending hide
        if (hideRoutine != null) { StopCoroutine(hideRoutine); hideRoutine = null; }
        if (panel == null) BuildUI();
        UIHelper.SetupTopBar("技能升级", () => { Hide(); UIHelper.EnsureUIManager(); UIManager.Instance.ShowHub(); });
        RefreshDisplay();
        panel.SetActive(true);
        var cg = panel.GetComponent<CanvasGroup>();
        if (cg != null) { cg.alpha = 1f; cg.blocksRaycasts = true; }
        if (showRoutine != null) StopCoroutine(showRoutine);
        showRoutine = StartCoroutine(UIHelper.FadeIn(panel, 0.2f));
    }

    public void Hide()
    {
        if (panel == null) return;
        if (showRoutine != null) StopCoroutine(showRoutine);
        if (hideRoutine != null) StopCoroutine(hideRoutine);
        hideRoutine = StartCoroutine(HideCoroutine());
    }

    private IEnumerator HideCoroutine()
    {
        var cg = panel.GetComponent<CanvasGroup>();
        if (cg != null) cg.blocksRaycasts = false;
        float duration = 0.15f;
        float t = 0f;
        while (t < duration)
        {
            if (panel == null) yield break;
            t += Time.unscaledDeltaTime;
            if (cg != null) cg.alpha = 1f - Mathf.Clamp01(t / duration);
            yield return null;
        }
        if (panel != null)
        {
            panel.SetActive(false);
            if (cg != null) cg.alpha = 1f;
        }
    }

    private void BuildUI()
    {
        Font font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();

        panel = new GameObject("SkillTreePanel");
        panel.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero; panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero; panelRect.offsetMax = Vector2.zero;
        Image bg = panel.AddComponent<Image>();
        bg.color = UIHelper.BgDark;
        bg.raycastTarget = true;
        panel.AddComponent<CanvasGroup>();

        // Top nav bar

        // Skill points (left) + Gold (right)
        GameObject spObj = new GameObject("SkillPoints");
        spObj.transform.SetParent(panel.transform, false);
        RectTransform spRect = spObj.AddComponent<RectTransform>();
        spRect.anchorMin = new Vector2(0.3f, 1); spRect.anchorMax = new Vector2(0.3f, 1);
        spRect.pivot = new Vector2(0.5f, 1); spRect.anchoredPosition = new Vector2(0, -98);
        spRect.sizeDelta = new Vector2(220, 28);
        skillPointsText = spObj.AddComponent<Text>();
        skillPointsText.alignment = TextAnchor.MiddleCenter; skillPointsText.fontSize = 20;
        skillPointsText.color = new Color(0.3f, 1f, 0.5f); skillPointsText.font = font;
        skillPointsText.raycastTarget = false;

        // Hint text
        GameObject hintObj = new GameObject("Hint");
        hintObj.transform.SetParent(panel.transform, false);
        RectTransform hintRect = hintObj.AddComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0.5f, 1); hintRect.anchorMax = new Vector2(0.5f, 1);
        hintRect.pivot = new Vector2(0.5f, 1); hintRect.anchoredPosition = new Vector2(0, -128);
        hintRect.sizeDelta = new Vector2(500, 22);
        Text hintText = hintObj.AddComponent<Text>();
        hintText.text = "技能点免费升级 · 金币也可升级 · Lv5解锁进化";
        hintText.alignment = TextAnchor.MiddleCenter; hintText.fontSize = 15;
        hintText.color = UIHelper.TextDim; hintText.font = font;
        hintText.raycastTarget = false;

        // Scrollable area for skill cards
        GameObject scrollObj = new GameObject("SkillScroll");
        scrollObj.transform.SetParent(panel.transform, false);
        var scrollRt = scrollObj.AddComponent<RectTransform>();
        scrollRt.anchorMin = new Vector2(0.05f, 0.02f); scrollRt.anchorMax = new Vector2(0.95f, 0.88f);
        scrollRt.offsetMin = Vector2.zero; scrollRt.offsetMax = new Vector2(0, -140);
        var scrollImg = scrollObj.AddComponent<Image>();
        scrollImg.color = new Color(0.03f, 0.04f, 0.06f, 0.5f);
        scrollImg.raycastTarget = true;
        scrollObj.AddComponent<RectMask2D>();
        var scrollRect = scrollObj.AddComponent<ScrollRect>();

        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(scrollObj.transform, false);
        var contentRt = contentObj.AddComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0, 1); contentRt.anchorMax = new Vector2(1, 1);
        contentRt.pivot = new Vector2(0.5f, 1);
        contentRt.sizeDelta = new Vector2(0, 0);
        var vg = contentObj.AddComponent<VerticalLayoutGroup>();
        vg.spacing = 8; vg.padding = new RectOffset(8, 8, 8, 8);
        vg.childControlWidth = true; vg.childForceExpandWidth = true;
        vg.childControlHeight = true; vg.childForceExpandHeight = false;
        var csf = contentObj.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.viewport = scrollRt;
        scrollRect.content = contentRt;
        scrollRect.vertical = true; scrollRect.horizontal = false;
        scrollRect.scrollSensitivity = 25f;
        scrollContent = contentObj.transform;

        panel.SetActive(false);
    }

    private void RefreshDisplay()
    {
        foreach (var btn in skillButtons) if (btn != null) Destroy(btn);
        skillButtons.Clear();

        var player = GameManager.Instance?.Player;
        if (player == null || player.Skills == null) return;

        if (skillPointsText != null)
            skillPointsText.text = $"技能点: {player.Stats.SkillPoints}";
        TopNavBar.Instance?.RefreshCurrency();

        Font font = GameManager.GetUIFont();
        ClassData classData = ClassData.GetClassData(player.HeroClass);
        Color[] skillColors = { classData.SkillColor1, classData.SkillColor2, classData.SkillColor3 };
        string[] keyLabels = { "[Q]", "[E]", "[R]" };

        for (int i = 0; i < player.Skills.Count; i++)
        {
            int idx = i;
            var skill = player.Skills[i];
            Color sc = i < skillColors.Length ? skillColors[i] : Color.white;
            string keyLabel = i < keyLabels.Length ? keyLabels[i] : "";

            // === Card (parented to scroll content, sized via LayoutElement) ===
            GameObject cardObj = UIHelper.MakeGlowCard(scrollContent, "Skill_" + i,
                new Vector2(0, 1), new Vector2(1, 1),
                Vector2.zero, Vector2.zero,
                new Color(sc.r * 0.15f, sc.g * 0.15f, sc.b * 0.15f, 0.92f),
                new Color(sc.r * 0.05f, sc.g * 0.05f, sc.b * 0.05f, 0.92f),
                sc);
            var le = cardObj.AddComponent<LayoutElement>();
            le.preferredHeight = 160;
            le.flexibleWidth = 1;

            // BUGFIX: MakeGlowCard's bg Image defaults to raycastTarget=true, which
            // intercepts clicks before child buttons can receive them.
            var cardBg = cardObj.GetComponent<Image>();
            if (cardBg != null) cardBg.raycastTarget = false;

            cardObj.AddComponent<CardHoverEffect>();

            // === Row 1: Name + key + level bar ===
            GameObject nameObj = new GameObject("SkillName");
            nameObj.transform.SetParent(cardObj.transform, false);
            RectTransform nRect = nameObj.AddComponent<RectTransform>();
            nRect.anchorMin = new Vector2(0, 1); nRect.anchorMax = new Vector2(1, 1);
            nRect.pivot = new Vector2(0, 1);
            nRect.anchoredPosition = new Vector2(15, -8); nRect.sizeDelta = new Vector2(570, 28);
            Text nText = nameObj.AddComponent<Text>();
            nText.text = $"{keyLabel}  {skill.Name}";
            nText.alignment = TextAnchor.MiddleLeft; nText.fontSize = 22;
            nText.color = sc; nText.font = font;
            nText.raycastTarget = false;

            // Level pips
            GameObject pipsObj = new GameObject("Pips");
            pipsObj.transform.SetParent(cardObj.transform, false);
            RectTransform pipsRect = pipsObj.AddComponent<RectTransform>();
            pipsRect.anchorMin = new Vector2(1, 1); pipsRect.anchorMax = new Vector2(1, 1);
            pipsRect.pivot = new Vector2(1, 1); pipsRect.anchoredPosition = new Vector2(-15, -10);
            pipsRect.sizeDelta = new Vector2(130, 24);
            Text pipsText = pipsObj.AddComponent<Text>();
            string pips = "";
            for (int l = 0; l < skill.MaxLevel; l++)
                pips += l < skill.CurrentLevel ? "●" : "○";
            pipsText.text = $"{pips}  Lv.{skill.CurrentLevel}/{skill.MaxLevel}";
            pipsText.alignment = TextAnchor.MiddleRight; pipsText.fontSize = 18;
            pipsText.color = sc; pipsText.font = font;
            pipsText.raycastTarget = false;

            // === Row 2: Description ===
            GameObject descObj = new GameObject("Desc");
            descObj.transform.SetParent(cardObj.transform, false);
            RectTransform dRect = descObj.AddComponent<RectTransform>();
            dRect.anchorMin = new Vector2(0, 0.55f); dRect.anchorMax = new Vector2(1, 0.85f);
            dRect.offsetMin = new Vector2(15, 0); dRect.offsetMax = new Vector2(-15, 0);
            Text dText = descObj.AddComponent<Text>();
            dText.text = skill.Description;
            dText.alignment = TextAnchor.MiddleLeft; dText.fontSize = 15;
            dText.color = UIHelper.TextSecondary; dText.font = font;
            dText.raycastTarget = false;

            // === Row 3: Stat preview (current → next) ===
            GameObject statObj = new GameObject("StatPreview");
            statObj.transform.SetParent(cardObj.transform, false);
            RectTransform sRect = statObj.AddComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0, 0.30f); sRect.anchorMax = new Vector2(0.58f, 0.55f);
            sRect.offsetMin = new Vector2(15, 0); sRect.offsetMax = new Vector2(0, 0);
            Text sText = statObj.AddComponent<Text>();
            sText.supportRichText = true;
            sText.alignment = TextAnchor.MiddleLeft; sText.fontSize = 14;
            sText.color = UIHelper.TextSecondary; sText.font = font;
            sText.raycastTarget = false;

            if (skill.CanUpgrade)
            {
                float curDmg = skill.CurrentDamageMultiplier;
                float nextDmg = skill.NextDamageMultiplier;
                float curCd = skill.CurrentCooldown;
                float nextCd = skill.NextCooldown;
                float curRng = skill.CurrentRange;
                float nextRng = skill.NextRange;

                string dmgDelta = nextDmg > curDmg ? $"<color=#44FF44>→ {nextDmg:F1}</color>" : "";
                string cdDelta = nextCd < curCd ? $"<color=#44FF44>→ {nextCd:F1}s</color>" : (nextCd != curCd ? $"<color=#FF8844>→ {nextCd:F1}s</color>" : "");
                string rngDelta = nextRng > curRng ? $"<color=#44FF44>→ {nextRng:F1}</color>" : "";

                string line1 = curDmg >= 1f ? $"伤害: x{curDmg:F1} {dmgDelta}" : $"效果: x{curDmg:F1} {dmgDelta}";
                string line2 = curCd > 0 ? $"冷却: {curCd:F1}s {cdDelta}" : "";
                string line3 = curRng > 0 ? $"范围: {curRng:F1} {rngDelta}" : "";

                sText.text = $"{line1}";
                if (!string.IsNullOrEmpty(line2)) sText.text += $"\n{line2}";
                if (!string.IsNullOrEmpty(line3)) sText.text += $"\n{line3}";
            }
            else
            {
                // Maxed — show evolution info
                sText.text = $"<color=#FF88FF>★ 进化: {skill.EvolutionName}</color>\n{skill.EvolutionDescription}";
                sText.fontSize = 15;
            }

            // === Row 4: Buttons ===
            if (skill.CanUpgrade)
            {
                bool canUseSP = player.Stats.SkillPoints >= skill.UpgradeCost;
                int goldCost = skill.GoldUpgradeCost;
                bool canUseGold = player.Stats.Gold >= goldCost;

                // SP upgrade button
                CreateUpgradeButton(cardObj.transform, "SP_Btn", font,
                    new Vector2(0.60f, 0.08f), new Vector2(0.78f, 0.28f),
                    new Vector2(0, 0), new Vector2(-3, 0),
                    canUseSP, new Color(0.15f, 0.55f, 0.15f, 0.9f), new Color(0.2f, 0.2f, 0.2f, 0.6f),
                    $"{skill.UpgradeCost}点",
                    () => UpgradeSkill(idx, useGold: false));

                // Gold upgrade button
                CreateUpgradeButton(cardObj.transform, "Gold_Btn", font,
                    new Vector2(0.80f, 0.08f), new Vector2(1f, 0.28f),
                    new Vector2(3, 0), new Vector2(-10, 0),
                    canUseGold, new Color(0.6f, 0.45f, 0.1f, 0.9f), new Color(0.2f, 0.2f, 0.2f, 0.6f),
                    $"{goldCost}",
                    () => UpgradeSkill(idx, useGold: true));
            }
            else
            {
                // Maxed — evolution badge
                GameObject evoObj = new GameObject("EvoBadge");
                evoObj.transform.SetParent(cardObj.transform, false);
                RectTransform evoR = evoObj.AddComponent<RectTransform>();
                evoR.anchorMin = new Vector2(0.60f, 0.08f); evoR.anchorMax = new Vector2(1f, 0.28f);
                evoR.offsetMin = new Vector2(0, 0); evoR.offsetMax = new Vector2(-10, 0);
                Image evoImg = evoObj.AddComponent<Image>();
                evoImg.color = new Color(0.3f, 0.1f, 0.4f, 0.8f);
                evoImg.raycastTarget = false;

                GameObject evoLbl = new GameObject("L");
                evoLbl.transform.SetParent(evoObj.transform, false);
                RectTransform evoLblR = evoLbl.AddComponent<RectTransform>();
                evoLblR.anchorMin = Vector2.zero; evoLblR.anchorMax = Vector2.one;
                evoLblR.offsetMin = Vector2.zero; evoLblR.offsetMax = Vector2.zero;
                Text evoText = evoLbl.AddComponent<Text>();
                evoText.text = "★ 已进化";
                evoText.alignment = TextAnchor.MiddleCenter; evoText.fontSize = 16;
                evoText.color = new Color(1f, 0.6f, 1f); evoText.font = font;
                evoText.raycastTarget = false;
            }

            skillButtons.Add(cardObj);
        }

        BuildMutationSection(font, player);
    }

    /// <summary>Helper: create a properly-configured upgrade button with border + label.</summary>
    private void CreateUpgradeButton(Transform parent, string name, Font font,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
        bool interactable, Color activeColor, Color disabledColor,
        string label, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        RectTransform btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin = anchorMin; btnRect.anchorMax = anchorMax;
        btnRect.offsetMin = offsetMin; btnRect.offsetMax = offsetMax;

        Image btnImg = btnObj.AddComponent<Image>();
        btnImg.color = interactable ? activeColor : disabledColor;
        btnImg.raycastTarget = true; // Must be true for button clicks

        Button btn = btnObj.AddComponent<Button>();
        btn.interactable = interactable;
        btn.targetGraphic = btnImg;

        // Override color tint to keep our custom colors
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        colors.fadeDuration = 0.08f;
        btn.colors = colors;

        btn.onClick.AddListener(onClick);

        GameObject lbl = new GameObject("L");
        lbl.transform.SetParent(btnObj.transform, false);
        RectTransform lblR = lbl.AddComponent<RectTransform>();
        lblR.anchorMin = Vector2.zero; lblR.anchorMax = Vector2.one;
        lblR.offsetMin = Vector2.zero; lblR.offsetMax = Vector2.zero;
        Text lblText = lbl.AddComponent<Text>();
        lblText.text = label;
        lblText.alignment = TextAnchor.MiddleCenter; lblText.fontSize = 15;
        lblText.color = Color.white; lblText.font = font;
        lblText.raycastTarget = false;
    }

    private void BuildMutationSection(Font font, PlayerController player)
    {
        if (player.RunMutationPool == null || player.RunMutationPool.Count == 0) return;

        GameObject sectionTitle = new GameObject("MutationTitle");
        sectionTitle.transform.SetParent(scrollContent, false);
        RectTransform stRect = sectionTitle.AddComponent<RectTransform>();
        stRect.anchorMin = new Vector2(0, 1); stRect.anchorMax = new Vector2(1, 1);
        stRect.pivot = new Vector2(0.5f, 1); stRect.anchoredPosition = new Vector2(0, 0);
        stRect.sizeDelta = new Vector2(0, 28);
        var stLe = sectionTitle.AddComponent<LayoutElement>();
        stLe.preferredHeight = 28;
        Text stText = sectionTitle.AddComponent<Text>();
        stText.text = "◆ 技能变异 ◆";
        stText.alignment = TextAnchor.MiddleCenter; stText.fontSize = 22;
        stText.color = UIHelper.RarityEpic; stText.font = font;
        stText.raycastTarget = false;
        skillButtons.Add(sectionTitle);

        for (int i = 0; i < player.RunMutationPool.Count; i++)
        {
            var mutation = player.RunMutationPool[i];

            SkillData targetSkill = null;
            foreach (var s in player.Skills)
            {
                if (s.Slot == mutation.TargetSlot && s.RequiredClass == mutation.TargetClass)
                { targetSkill = s; break; }
            }
            if (targetSkill == null) continue;

            int currentStacks = targetSkill.GetMutationStacks(mutation.Id);
            bool canStack = currentStacks < mutation.MaxStacks;
            bool canAfford = player.Stats.SkillPoints >= 1;

            Color rarityColor = mutation.Rarity switch
            {
                MutationRarity.Legendary => UIHelper.RarityLegendary,
                MutationRarity.Powerful => UIHelper.RarityEpic,
                _ => UIHelper.RarityRare
            };

            GameObject cardObj = UIHelper.MakeGlowCard(scrollContent, "Mutation_" + i,
                new Vector2(0, 1), new Vector2(1, 1),
                Vector2.zero, Vector2.zero,
                new Color(rarityColor.r * 0.12f, rarityColor.g * 0.12f, rarityColor.b * 0.12f, 0.92f),
                new Color(rarityColor.r * 0.04f, rarityColor.g * 0.04f, rarityColor.b * 0.04f, 0.92f),
                rarityColor);

            var mLe = cardObj.AddComponent<LayoutElement>();
            mLe.preferredHeight = 88;

            // BUGFIX: disable raycast on card bg so mutation button is clickable
            var cardBg = cardObj.GetComponent<Image>();
            if (cardBg != null) cardBg.raycastTarget = false;

            cardObj.AddComponent<CardHoverEffect>();

            GameObject nameObj = new GameObject("MutationName");
            nameObj.transform.SetParent(cardObj.transform, false);
            RectTransform nRect = nameObj.AddComponent<RectTransform>();
            nRect.anchorMin = new Vector2(0, 1); nRect.anchorMax = new Vector2(1, 1);
            nRect.pivot = new Vector2(0, 1);
            nRect.anchoredPosition = new Vector2(15, -6); nRect.sizeDelta = new Vector2(570, 24);
            Text nText = nameObj.AddComponent<Text>();
            nText.text = $"{mutation.DisplayName} ({currentStacks}/{mutation.MaxStacks})";
            nText.alignment = TextAnchor.MiddleLeft; nText.fontSize = 17;
            nText.color = rarityColor; nText.font = font;
            nText.raycastTarget = false;

            GameObject descObj = new GameObject("MutationDesc");
            descObj.transform.SetParent(cardObj.transform, false);
            RectTransform dRect = descObj.AddComponent<RectTransform>();
            dRect.anchorMin = new Vector2(0, 0.5f); dRect.anchorMax = new Vector2(0.65f, 1);
            dRect.offsetMin = new Vector2(15, 5); dRect.offsetMax = new Vector2(-5, -30);
            Text dText = descObj.AddComponent<Text>();
            dText.text = $"{mutation.Description}\n叠加: {mutation.StackEffect}";
            dText.alignment = TextAnchor.UpperLeft; dText.fontSize = 13;
            dText.color = UIHelper.TextSecondary; dText.font = font;
            dText.raycastTarget = false;

            if (canStack)
            {
                GameObject btnObj = new GameObject("ChooseBtn");
                btnObj.transform.SetParent(cardObj.transform, false);
                RectTransform bRect = btnObj.AddComponent<RectTransform>();
                bRect.anchorMin = new Vector2(0.65f, 0.1f); bRect.anchorMax = new Vector2(1, 0.9f);
                bRect.offsetMin = new Vector2(5, 5); bRect.offsetMax = new Vector2(-10, -5);
                Image btnImg = btnObj.AddComponent<Image>();
                btnImg.color = canAfford
                    ? new Color(rarityColor.r * 0.3f, rarityColor.g * 0.3f, rarityColor.b * 0.3f, 0.9f)
                    : new Color(0.3f, 0.3f, 0.3f, 0.7f);
                btnImg.raycastTarget = true;
                Button btn = btnObj.AddComponent<Button>();
                btn.interactable = canAfford;
                btn.targetGraphic = btnImg;
                btn.onClick.AddListener(() => ChooseMutation(mutation));
                GameObject btnLabel = new GameObject("Label");
                btnLabel.transform.SetParent(btnObj.transform, false);
                RectTransform blRect = btnLabel.AddComponent<RectTransform>();
                blRect.anchorMin = Vector2.zero; blRect.anchorMax = Vector2.one;
                blRect.offsetMin = Vector2.zero; blRect.offsetMax = Vector2.zero;
                Text blText = btnLabel.AddComponent<Text>();
                blText.text = currentStacks > 0 ? "叠加\n(1点)" : "解锁\n(1点)";
                blText.alignment = TextAnchor.MiddleCenter;
                blText.fontSize = 13; blText.color = Color.white; blText.font = font;
                blText.raycastTarget = false;
            }
            else
            {
                GameObject maxObj = new GameObject("MaxLabel");
                maxObj.transform.SetParent(cardObj.transform, false);
                RectTransform mRect = maxObj.AddComponent<RectTransform>();
                mRect.anchorMin = new Vector2(0.65f, 0.1f); mRect.anchorMax = new Vector2(1, 0.9f);
                mRect.offsetMin = new Vector2(5, 5); mRect.offsetMax = new Vector2(-10, -5);
                Text mText = maxObj.AddComponent<Text>();
                mText.text = "已满层"; mText.alignment = TextAnchor.MiddleCenter;
                mText.fontSize = 15; mText.color = UIHelper.TextDim; mText.font = font;
                mText.raycastTarget = false;
            }

            skillButtons.Add(cardObj);
        }
    }

    private void ChooseMutation(SkillMutation mutation)
    {
        var player = GameManager.Instance?.Player;
        if (player == null) return;
        if (!player.Stats.SpendSkillPoint()) return;

        foreach (var skill in player.Skills)
        {
            if (skill.Slot == mutation.TargetSlot && skill.RequiredClass == mutation.TargetClass)
            {
                if (skill.TryAddMutation(mutation))
                {
                    AudioManager.Instance?.PlayLevelUp();
                    VFXHelper.SpawnLevelUpEffect(player.transform.position);
                }
                break;
            }
        }
        player.Stats.Save();
        RefreshDisplay();
    }

    private void UpgradeSkill(int index, bool useGold)
    {
        var player = GameManager.Instance?.Player;
        if (player == null || index >= player.Skills.Count) return;
        var skill = player.Skills[index];
        if (!skill.CanUpgrade) return;

        if (useGold)
        {
            int goldCost = skill.GoldUpgradeCost;
            if (!player.Stats.SpendGold(goldCost)) return;
        }
        else
        {
            if (!player.Stats.SpendSkillPoint()) return;
        }

        skill.CurrentLevel++;
        player.Stats.Save();
        AudioManager.Instance?.PlayLevelUp();
        RefreshDisplay();
    }

    public bool IsVisible() => panel != null && panel.activeSelf;

    private void OnDestroy()
    {
        if (panel != null) Destroy(panel);
        if (Instance == this) Instance = null;
    }
}
