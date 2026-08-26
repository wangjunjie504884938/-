using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 符文装备面板 — 从HubUI打开
/// 上半区：技能切换 + 2个符文槽位
/// 下半区：8种可装备符文列表
/// </summary>
public class RuneEquipUI : MonoBehaviour
{
    public static RuneEquipUI Instance { get; private set; }

    private GameObject panel;
    private int selectedSkillIndex;
    private List<RuneData> availableRunes;
    private Font font;
    private Coroutine showRoutine;

    // 暗青蓝配色

    public bool IsVisible() => panel != null && panel.activeSelf;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        font = GameManager.GetUIFont();
        availableRunes = RuneData.GetAllRunes();
    }

    private void OnDestroy()
    {
        if (panel != null) Destroy(panel);
        if (Instance == this) Instance = null;
    }

    public void Show(int skillIndex)
    {
        selectedSkillIndex = skillIndex;
        if (panel == null) BuildPanel();
        UIHelper.SetupTopBar("符文装备", () => { Hide(); UIHelper.EnsureUIManager(); UIManager.Instance.ShowHub(); });
        RefreshPanel();
        panel.SetActive(true);
        if (showRoutine != null) StopCoroutine(showRoutine);
        showRoutine = StartCoroutine(UIHelper.FadeIn(panel, 0.2f));
    }

    public void Hide()
    {
        if (panel != null)
        {
            if (showRoutine != null) StopCoroutine(showRoutine);
            showRoutine = StartCoroutine(UIHelper.FadeOut(panel, 0.15f));
        }
    }

    private GameObject MakeBtn(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax, Color bgColor, string label, int fontSize, System.Action onClick)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform r = obj.AddComponent<RectTransform>();
        r.anchorMin = anchorMin; r.anchorMax = anchorMax;
        r.offsetMin = offsetMin; r.offsetMax = offsetMax;
        Image img = obj.AddComponent<Image>();
        img.color = bgColor;
        if (onClick != null)
        {
            Button btn = obj.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick());
        }
        GameObject lbl = new GameObject("Lbl");
        lbl.transform.SetParent(obj.transform, false);
        RectTransform lr = lbl.AddComponent<RectTransform>();
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
        lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;
        Text t = lbl.AddComponent<Text>();
        t.text = label; t.alignment = TextAnchor.MiddleCenter;
        t.fontSize = fontSize; t.color = Color.white; t.font = font;
        return obj;
    }

    private void BuildPanel()
    {
        Canvas canvas = GameManager.EnsureCanvas();

        panel = new GameObject("RuneEquipPanel");
        panel.transform.SetParent(canvas.transform, false);
        RectTransform pr = panel.AddComponent<RectTransform>();
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one;
        pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;
        Image pBg = panel.AddComponent<Image>();
        pBg.color = UIHelper.BgDark;
        // === 导航顶栏 (TopNavBar 单例) ===

        panel.SetActive(false);

        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(panel.transform, false);
        RectTransform tr = titleObj.AddComponent<RectTransform>();
        tr.anchorMin = new Vector2(0, 1); tr.anchorMax = new Vector2(1, 1);
        tr.pivot = new Vector2(0.5f, 1); tr.anchoredPosition = new Vector2(0, -65);
        tr.sizeDelta = new Vector2(400, 40);
        Text titleTxt = titleObj.AddComponent<Text>();
        titleTxt.alignment = TextAnchor.MiddleCenter; titleTxt.fontSize = 28;
        titleTxt.color = UIHelper.Accent; titleTxt.font = font;

        // === Skill selector row (3 buttons) ===
        GameObject skillRow = new GameObject("SkillRow");
        skillRow.transform.SetParent(panel.transform, false);
        RectTransform srR = skillRow.AddComponent<RectTransform>();
        srR.anchorMin = new Vector2(0.1f, 1); srR.anchorMax = new Vector2(0.9f, 1);
        srR.pivot = new Vector2(0.5f, 1); srR.anchoredPosition = new Vector2(0, -120);
        srR.sizeDelta = new Vector2(0, 50);

        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            GameObject sb = new GameObject($"SkillBtn_{i}");
            sb.transform.SetParent(skillRow.transform, false);
            RectTransform sbr = sb.AddComponent<RectTransform>();
            float third = 1f / 3f;
            sbr.anchorMin = new Vector2(third * i + 0.02f, 0);
            sbr.anchorMax = new Vector2(third * (i + 1) - 0.02f, 1);
            sbr.offsetMin = Vector2.zero; sbr.offsetMax = Vector2.zero;
            Image sbg = sb.AddComponent<Image>();
            sbg.color = UIHelper.CardBg;
            Button sbb = sb.AddComponent<Button>();
            sbb.onClick.AddListener(() => { selectedSkillIndex = idx; RefreshPanel(); });
            GameObject sl = new GameObject("Lbl");
            sl.transform.SetParent(sb.transform, false);
            RectTransform slr = sl.AddComponent<RectTransform>();
            slr.anchorMin = Vector2.zero; slr.anchorMax = Vector2.one;
            slr.offsetMin = Vector2.zero; slr.offsetMax = Vector2.zero;
            Text st = sl.AddComponent<Text>();
            st.alignment = TextAnchor.MiddleCenter; st.fontSize = 18;
            st.color = Color.white; st.font = font;
        }

        // === Rune slots (2) ===
        GameObject slotRow = new GameObject("SlotRow");
        slotRow.transform.SetParent(panel.transform, false);
        RectTransform slR = slotRow.AddComponent<RectTransform>();
        slR.anchorMin = new Vector2(0.1f, 1); slR.anchorMax = new Vector2(0.9f, 1);
        slR.pivot = new Vector2(0.5f, 1); slR.anchoredPosition = new Vector2(0, -190);
        slR.sizeDelta = new Vector2(0, 90);

        for (int i = 0; i < SkillData.MaxRuneSlots; i++)
        {
            int slotIdx = i;
            GameObject slot = new GameObject($"RuneSlot_{i}");
            slot.transform.SetParent(slotRow.transform, false);
            RectTransform slotR = slot.AddComponent<RectTransform>();
            float half = 0.5f;
            slotR.anchorMin = new Vector2(i == 0 ? 0.02f : half + 0.02f, 0);
            slotR.anchorMax = new Vector2(i == 0 ? half - 0.02f : 0.98f, 1);
            slotR.offsetMin = Vector2.zero; slotR.offsetMax = Vector2.zero;
            Image slotBg = slot.AddComponent<Image>();
            slotBg.color = UIHelper.GlowTop;
            Button slotBtn = slot.AddComponent<Button>();
            slotBtn.onClick.AddListener(() => UnequipRune(slotIdx));

            GameObject slotLbl = new GameObject("SlotLbl");
            slotLbl.transform.SetParent(slot.transform, false);
            RectTransform llr = slotLbl.AddComponent<RectTransform>();
            llr.anchorMin = Vector2.zero; llr.anchorMax = Vector2.one;
            llr.offsetMin = Vector2.zero; llr.offsetMax = Vector2.zero;
            Text lt = slotLbl.AddComponent<Text>();
            lt.alignment = TextAnchor.MiddleCenter; lt.fontSize = 18;
            lt.color = new Color(0.5f, 0.5f, 0.6f); lt.font = font;
            lt.text = "空槽位";
        }

        // === Available runes list ===
        // Background
        GameObject listBg = new GameObject("RuneListBg");
        listBg.transform.SetParent(panel.transform, false);
        RectTransform lbR = listBg.AddComponent<RectTransform>();
        lbR.anchorMin = new Vector2(0.05f, 0); lbR.anchorMax = new Vector2(0.95f, 1);
        lbR.offsetMin = new Vector2(0, 15); lbR.offsetMax = new Vector2(0, -300);
        Image lbImg = listBg.AddComponent<Image>();
        lbImg.color = UIHelper.GlowBottom;

        // "可装备符文" label
        GameObject listTitle = new GameObject("ListTitle");
        listTitle.transform.SetParent(panel.transform, false);
        RectTransform ltR = listTitle.AddComponent<RectTransform>();
        ltR.anchorMin = new Vector2(0, 1); ltR.anchorMax = new Vector2(1, 1);
        ltR.pivot = new Vector2(0.5f, 1); ltR.anchoredPosition = new Vector2(0, -285);
        ltR.sizeDelta = new Vector2(400, 30);
        Text ltTxt = listTitle.AddComponent<Text>();
        ltTxt.text = "点击装备符文"; ltTxt.alignment = TextAnchor.MiddleCenter;
        ltTxt.fontSize = 18; ltTxt.color = UIHelper.Accent; ltTxt.font = font;

        // Rune entries
        float entryH = 60f;
        float startY = 0f;
        foreach (var rune in availableRunes)
        {
            var r = rune;
            GameObject entry = new GameObject($"Rune_{r.Type}");
            entry.transform.SetParent(listBg.transform, false);
            RectTransform eR = entry.AddComponent<RectTransform>();
            eR.anchorMin = new Vector2(0, 1); eR.anchorMax = new Vector2(1, 1);
            eR.pivot = new Vector2(0, 1);
            eR.anchoredPosition = new Vector2(0, -startY);
            eR.sizeDelta = new Vector2(0, entryH - 5f);
            Image eBg = entry.AddComponent<Image>();
            eBg.color = new Color(r.RuneColor.r * 0.2f, r.RuneColor.g * 0.2f, r.RuneColor.b * 0.2f, 0.7f);
            Button eBtn = entry.AddComponent<Button>();
            eBtn.onClick.AddListener(() => EquipRune(r.Type));

            // Rune icon + name
            GameObject nm = new GameObject("Name");
            nm.transform.SetParent(entry.transform, false);
            RectTransform nR = nm.AddComponent<RectTransform>();
            nR.anchorMin = new Vector2(0, 0); nR.anchorMax = new Vector2(0.35f, 1);
            nR.offsetMin = new Vector2(10, 2); nR.offsetMax = new Vector2(-5, -2);
            Text nT = nm.AddComponent<Text>();
            nT.text = $"◆ {r.Name}"; nT.alignment = TextAnchor.MiddleLeft;
            nT.fontSize = 18; nT.color = r.RuneColor; nT.font = font;

            // Rune description
            GameObject dc = new GameObject("Desc");
            dc.transform.SetParent(entry.transform, false);
            RectTransform dR = dc.AddComponent<RectTransform>();
            dR.anchorMin = new Vector2(0.35f, 0); dR.anchorMax = new Vector2(1, 1);
            dR.offsetMin = new Vector2(5, 2); dR.offsetMax = new Vector2(-10, -2);
            Text dT = dc.AddComponent<Text>();
            dT.text = r.Description; dT.alignment = TextAnchor.MiddleLeft;
            dT.fontSize = 14; dT.color = UIHelper.TextSecondary; dT.font = font;

            startY += entryH;
        }
    }

    private void RefreshPanel()
    {
        if (panel == null) return;
        var player = GameManager.Instance?.Player;
        if (player == null || selectedSkillIndex >= player.Skills.Count) return;

        SkillData skill = player.Skills[selectedSkillIndex];

        // Title
        Transform titleT = panel.transform.Find("Title");
        if (titleT != null) titleT.GetComponent<Text>().text = $"符文装备 — {skill.Name}";

        // Skill buttons
        Transform skillRow = panel.transform.Find("SkillRow");
        if (skillRow != null)
        {
            for (int i = 0; i < 3; i++)
            {
                Transform btn = skillRow.Find($"SkillBtn_{i}");
                if (btn == null) continue;
                Text txt = btn.GetComponentInChildren<Text>();
                Image bg = btn.GetComponent<Image>();
                if (i < player.Skills.Count)
                {
                    txt.text = player.Skills[i].Name;
                    bg.color = i == selectedSkillIndex
                        ? new Color(0.08f, 0.15f, 0.25f, 0.95f)
                        : UIHelper.GlowTop;
                }
            }
        }

        // Rune slots
        Transform slotRow = panel.transform.Find("SlotRow");
        if (slotRow != null)
        {
            for (int i = 0; i < SkillData.MaxRuneSlots; i++)
            {
                Transform slot = slotRow.Find($"RuneSlot_{i}");
                if (slot == null) continue;
                Image slotBg = slot.GetComponent<Image>();
                Text slotLbl = slot.Find("SlotLbl")?.GetComponent<Text>();

                if (i < skill.EquippedRunes.Count)
                {
                    var r = skill.EquippedRunes[i];
                    slotBg.color = new Color(r.RuneColor.r * 0.4f, r.RuneColor.g * 0.4f, r.RuneColor.b * 0.4f, 0.9f);
                    if (slotLbl != null)
                    {
                        slotLbl.text = $"◆ {r.Name}\n(点击卸下)";
                        slotLbl.color = Color.white;
                    }
                }
                else
                {
                    slotBg.color = UIHelper.GlowTop;
                    if (slotLbl != null)
                    {
                        slotLbl.text = "空槽位";
                        slotLbl.color = new Color(0.3f, 0.3f, 0.35f);
                    }
                }
            }
        }

        // Rune list entries
        Transform listBg = panel.transform.Find("RuneListBg");
        if (listBg != null)
        {
            for (int i = 0; i < availableRunes.Count; i++)
            {
                Transform entry = listBg.Find($"Rune_{availableRunes[i].Type}");
                if (entry == null) continue;
                bool equipped = skill.HasRune(availableRunes[i].Type);
                bool slotsFull = skill.EquippedRunes.Count >= SkillData.MaxRuneSlots;
                Image eBg = entry.GetComponent<Image>();
                var rc = availableRunes[i].RuneColor;

                if (equipped)
                    eBg.color = new Color(rc.r * 0.5f, rc.g * 0.5f, rc.b * 0.5f, 0.9f);
                else if (slotsFull)
                    eBg.color = new Color(0.08f, 0.08f, 0.1f, 0.3f);
                else
                    eBg.color = new Color(rc.r * 0.2f, rc.g * 0.2f, rc.b * 0.2f, 0.7f);
            }
        }
    }

    private void EquipRune(RuneType type)
    {
        var player = GameManager.Instance?.Player;
        if (player == null || selectedSkillIndex >= player.Skills.Count) return;

        SkillData skill = player.Skills[selectedSkillIndex];
        if (skill.HasRune(type))
            skill.UnequipRune(type);
        else
            skill.EquipRune(RuneData.Create(type));
        RefreshPanel();
    }

    private void UnequipRune(int slotIndex)
    {
        var player = GameManager.Instance?.Player;
        if (player == null || selectedSkillIndex >= player.Skills.Count) return;

        SkillData skill = player.Skills[selectedSkillIndex];
        if (slotIndex < skill.EquippedRunes.Count)
        {
            skill.UnequipRune(skill.EquippedRunes[slotIndex].Type);
            RefreshPanel();
        }
    }
}
