using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 剧情对话系统 — 打字机效果 + NPC头像 + 分段对话
/// 在关卡开始/结束时自动触发剧情对话
/// </summary>
public class DialogueSystem : MonoBehaviour
{
    public static DialogueSystem Instance { get; private set; }

    private GameObject panel;
    private Image portraitImg;
    private Text nameText;
    private Text dialogueText;
    private Text continueHint;
    private Coroutine typeRoutine;
    private bool isTyping;
    private string[] currentLines;
    private int currentLineIndex;
    private System.Action onComplete;

    // 剧情数据 — 每关对话
    private static readonly string[][] StageDialogueStart = new string[][]
    {
        new[] { "冒险者, 你终于来了。", "这片大陆正被黑暗侵蚀...", "去消灭前方的敌人吧!" },
        new[] { "森林深处传来异响...", "小心, 敌人更加凶猛了。" },
        new[] { "遗迹中隐藏着古老的秘密。", "坚持住, 光明就在前方!" },
        new[] { "寒风刺骨, 但你的意志更坚强。", "继续前进!" },
        new[] { "岩浆翻涌, 危机四伏...", "不要退缩, 战士!" },
        new[] { "天空之城, 古神的领地。", "你已经走了很远。" },
        new[] { "黑暗的源头就在前方...", "这是最后的考验!" },
        new[] { "最终决战即将开始!", "为了卫冕之王的荣耀!" },
    };

    private static readonly string[][] StageDialogueEnd = new string[][]
    {
        new[] { "做得好!但这只是开始...", "更大的挑战在等着你。" },
        new[] { "森林的异变已被平息。", "继续前行吧, 冒险者。" },
        new[] { "古老的秘密正在揭开...", "你越来越强了!" },
        new[] { "寒冰已被你的热血融化。", "前方还有更艰险的道路。" },
        new[] { "岩浆中的敌人已被击退!", "你的勇气令人敬佩。" },
        new[] { "天空之城恢复了宁静。", "最后的决战即将来临..." },
        new[] { "黑暗退散, 黎明将至!", "你已经准备好了。" },
        new[] { "你成功了!卫冕之王!", "这片大陆因你而重获新生!" },
    };

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void BuildUI()
    {
        Font font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();

        panel = new GameObject("DialoguePanel");
        panel.transform.SetParent(canvas.transform, false);
        var pr = panel.AddComponent<RectTransform>();
        pr.anchorMin = new Vector2(0.1f, 0); pr.anchorMax = new Vector2(0.9f, 0.3f);
        pr.offsetMin = new Vector2(0, 20); pr.offsetMax = Vector2.zero;
        var panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0.06f, 0.06f, 0.1f, 0.97f);

        // 边框
        var border = new GameObject("Border");
        border.transform.SetParent(panel.transform, false);
        var br = border.AddComponent<RectTransform>();
        br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one;
        br.offsetMin = new Vector2(-2, -2); br.offsetMax = new Vector2(2, 2);
        var bImg = border.AddComponent<Image>();
        bImg.color = UIHelper.AccentDim;
        bImg.raycastTarget = false;
        border.transform.SetAsFirstSibling();

        // NPC头像 (左侧圆形)
        var portraitObj = new GameObject("Portrait");
        portraitObj.transform.SetParent(panel.transform, false);
        var ptr = portraitObj.AddComponent<RectTransform>();
        ptr.anchorMin = new Vector2(0, 0); ptr.anchorMax = new Vector2(0, 1);
        ptr.pivot = new Vector2(0, 0.5f);
        ptr.anchoredPosition = new Vector2(10, 0);
        ptr.sizeDelta = new Vector2(80, 0);
        portraitImg = portraitObj.AddComponent<Image>();
        portraitImg.color = new Color(0.15f, 0.15f, 0.25f, 0.9f);

        // NPC名字
        var nameObj = new GameObject("NameText");
        nameObj.transform.SetParent(panel.transform, false);
        var nr = nameObj.AddComponent<RectTransform>();
        nr.anchorMin = new Vector2(0.1f, 0.7f); nr.anchorMax = new Vector2(0.9f, 1f);
        nr.offsetMin = Vector2.zero; nr.offsetMax = Vector2.zero;
        nameText = nameObj.AddComponent<Text>();
        nameText.text = "???"; nameText.alignment = TextAnchor.MiddleLeft;
        nameText.fontSize = 20; nameText.color = UIHelper.Accent; nameText.font = font;

        // 对话文字
        var dialogueObj = new GameObject("DialogueText");
        dialogueObj.transform.SetParent(panel.transform, false);
        var dr = dialogueObj.AddComponent<RectTransform>();
        dr.anchorMin = new Vector2(0.1f, 0.15f); dr.anchorMax = new Vector2(0.85f, 0.65f);
        dr.offsetMin = Vector2.zero; dr.offsetMax = Vector2.zero;
        dialogueText = dialogueObj.AddComponent<Text>();
        dialogueText.alignment = TextAnchor.MiddleLeft;
        dialogueText.fontSize = 18; dialogueText.color = UIHelper.TextPrimary; dialogueText.font = font;

        // 继续提示
        var hintObj = new GameObject("ContinueHint");
        hintObj.transform.SetParent(panel.transform, false);
        var hr = hintObj.AddComponent<RectTransform>();
        hr.anchorMin = new Vector2(0.8f, 0.05f); hr.anchorMax = new Vector2(0.98f, 0.2f);
        hr.offsetMin = Vector2.zero; hr.offsetMax = Vector2.zero;
        continueHint = hintObj.AddComponent<Text>();
        continueHint.text = "▶"; continueHint.alignment = TextAnchor.MiddleRight;
        continueHint.fontSize = 18; continueHint.color = UIHelper.AccentDim; continueHint.font = font;

        // 点击继续
        var btn = panel.AddComponent<Button>();
        btn.targetGraphic = panelImg;
        btn.onClick.AddListener(OnContinue);

        panel.SetActive(false);
    }

    /// <summary>显示一段对话</summary>
    public void ShowDialogue(string[] lines, string speakerName = "???", System.Action onComplete = null)
    {
        if (panel == null) BuildUI();
        panel.SetActive(true);
        currentLines = lines;
        currentLineIndex = 0;
        nameText.text = speakerName;
        this.onComplete = onComplete;
        Time.timeScale = 0f; // 暂停游戏，对话期间怪物不攻击
        ShowLine(0);
    }

    /// <summary>触发关卡开始对话</summary>
    public void ShowStageStartDialogue(int stageIndex)
    {
        if (stageIndex < 0 || stageIndex >= StageDialogueStart.Length) return;
        // 跳过已读对话
        if (DialogueData.IsStartDialogueRead(stageIndex)) return;
        DialogueData.MarkStartDialogueRead(stageIndex);

        string[] lines = StageDialogueStart[stageIndex];
        string speaker = stageIndex == 0 ? "神秘指引者" : stageIndex == 7 ? "卫冕之王" : "神秘指引者";
        ShowDialogue(lines, speaker);
    }

    /// <summary>触发关卡结束对话</summary>
    public void ShowStageEndDialogue(int stageIndex)
    {
        if (stageIndex < 0 || stageIndex >= StageDialogueEnd.Length) return;
        if (DialogueData.IsEndDialogueRead(stageIndex)) return;
        DialogueData.MarkEndDialogueRead(stageIndex);

        string[] lines = StageDialogueEnd[stageIndex];
        string speaker = stageIndex == 7 ? "卫冕之王" : "神秘指引者";
        ShowDialogue(lines, speaker);
    }

    private void ShowLine(int index)
    {
        if (index >= currentLines.Length)
        {
            EndDialogue();
            return;
        }
        currentLineIndex = index;
        if (typeRoutine != null) StopCoroutine(typeRoutine);
        typeRoutine = StartCoroutine(TypeText(currentLines[index]));
    }

    private IEnumerator TypeText(string text)
    {
        isTyping = true;
        dialogueText.text = "";
        continueHint.gameObject.SetActive(false);

        foreach (char c in text)
        {
            dialogueText.text += c;
            yield return new WaitForSecondsRealtime(0.03f);
        }

        isTyping = false;
        continueHint.gameObject.SetActive(true);
    }

    private void OnContinue()
    {
        if (!panel.activeSelf) return;

        if (isTyping)
        {
            // 快进打字效果
            if (typeRoutine != null) StopCoroutine(typeRoutine);
            dialogueText.text = currentLines[currentLineIndex];
            isTyping = false;
            continueHint.gameObject.SetActive(true);
        }
        else
        {
            ShowLine(currentLineIndex + 1);
        }
    }

    private void EndDialogue()
    {
        panel.SetActive(false);
        Time.timeScale = 1f; // 恢复游戏
        onComplete?.Invoke();
        onComplete = null;
    }

    private void OnDestroy()
    {
        if (panel != null) Destroy(panel);
        if (Instance == this) Instance = null;
    }
}
