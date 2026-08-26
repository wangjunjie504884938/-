using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 聊天弹窗 — 点击好友行触发，显示与该好友的聊天记录
/// 聊天记录存储在PlayerPrefs，换设备通过云存档同步
/// </summary>
public class ChatPopupUI : MonoBehaviour
{
    public static ChatPopupUI Instance { get; private set; }

    private GameObject _popup;
    private Text _chatLog;
    private InputField _inputField;
    private string _friendName;
    private ScrollRect _scrollRect;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>打开与指定好友的聊天弹窗</summary>
    public void Show(string friendName)
    {
        _friendName = friendName;
        if (_popup != null) Destroy(_popup);
        BuildPopup();
        _popup.SetActive(true);
        RefreshChatLog();
    }

    public void Hide()
    {
        if (_popup != null) _popup.SetActive(false);
    }

    private void BuildPopup()
    {
        Font font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();

        // === 外层遮罩 ===
        _popup = new GameObject("ChatPopup");
        _popup.transform.SetParent(canvas.transform, false);
        var pR = _popup.AddComponent<RectTransform>();
        pR.anchorMin = new Vector2(0.5f, 0.5f); pR.anchorMax = new Vector2(0.5f, 0.5f);
        pR.sizeDelta = new Vector2(1152, 756);
        _popup.AddComponent<Image>().color = new Color(0, 0, 0, 0.8f);

        // === Box容器 ===
        var box = UIHelper.MakeGlowCard(_popup.transform, "Box",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
            new Color(0.06f, 0.03f, 0.09f, 0.97f), UIHelper.GlowBottom, UIHelper.Accent);
        var boxR = box.GetComponent<RectTransform>();
        boxR.sizeDelta = new Vector2(1152, 756);

        // === 标题栏 ===
        var titleRow = new GameObject("TitleRow");
        titleRow.transform.SetParent(box.transform, false);
        var trR = titleRow.AddComponent<RectTransform>();
        trR.anchorMin = new Vector2(0, 0.92f); trR.anchorMax = new Vector2(1, 1f);
        trR.offsetMin = Vector2.zero; trR.offsetMax = Vector2.zero;
        var trImg = titleRow.AddComponent<Image>();
        trImg.color = new Color(0.10f, 0.06f, 0.15f, 0.9f);

        var titleObj = new GameObject("Title");
        titleObj.transform.SetParent(titleRow.transform, false);
        var tR = titleObj.AddComponent<RectTransform>();
        tR.anchorMin = new Vector2(0, 0); tR.anchorMax = new Vector2(0.85f, 1);
        tR.offsetMin = new Vector2(34, 0); tR.offsetMax = Vector2.zero;
        var title = titleObj.AddComponent<Text>();
        title.text = $"与 {_friendName} 聊天";
        title.alignment = TextAnchor.MiddleLeft; title.fontSize = 22;
        title.color = UIHelper.Accent; title.font = font;
        title.raycastTarget = false;

        UIHelper.MakeButton(titleRow.transform, "CloseBtn", "X", font,
            new Vector2(0.88f, 0.1f), new Vector2(0.98f, 0.9f), Vector2.zero, Vector2.zero,
            UIHelper.BtnDanger, 18, () => Hide());

        // === 聊天记录区 (ScrollRect + 固定高度Content) ===
        var scrollObj = new GameObject("ScrollArea");
        scrollObj.transform.SetParent(box.transform, false);
        var sR = scrollObj.AddComponent<RectTransform>();
        sR.anchorMin = new Vector2(0.03f, 0.15f); sR.anchorMax = new Vector2(0.97f, 0.90f);
        sR.offsetMin = Vector2.zero; sR.offsetMax = Vector2.zero;
        scrollObj.AddComponent<Image>().color = new Color(0.03f, 0.02f, 0.05f, 0.6f);
        _scrollRect = scrollObj.AddComponent<ScrollRect>();
        _scrollRect.scrollSensitivity = 30f;

        // Viewport (RectMask2D裁剪)
        var vpObj = new GameObject("Viewport");
        vpObj.transform.SetParent(scrollObj.transform, false);
        var vpR = vpObj.AddComponent<RectTransform>();
        vpR.anchorMin = Vector2.zero; vpR.anchorMax = Vector2.one;
        vpR.offsetMin = Vector2.zero; vpR.offsetMax = Vector2.zero;
        vpObj.AddComponent<RectMask2D>();

        // Content (固定高度，Text垂直溢出显示全部内容)
        var contentObj = new GameObject("Content");
        contentObj.transform.SetParent(vpObj.transform, false);
        var cR = contentObj.AddComponent<RectTransform>();
        cR.anchorMin = new Vector2(0, 1); cR.anchorMax = new Vector2(1, 1);
        cR.pivot = new Vector2(0.5f, 1);
        cR.offsetMin = Vector2.zero; cR.offsetMax = Vector2.zero;
        cR.sizeDelta = new Vector2(0, 600);

        _scrollRect.content = cR;
        _scrollRect.viewport = vpR;
        _scrollRect.horizontal = false;
        _scrollRect.vertical = true;

        _chatLog = contentObj.AddComponent<Text>();
        _chatLog.alignment = TextAnchor.UpperLeft; _chatLog.fontSize = 18;
        _chatLog.color = UIHelper.TextPrimary; _chatLog.font = font;
        _chatLog.supportRichText = true; _chatLog.raycastTarget = false;
        _chatLog.horizontalOverflow = HorizontalWrapMode.Wrap;
        _chatLog.verticalOverflow = VerticalWrapMode.Overflow;
        _chatLog.lineSpacing = 1.2f;

        // === 底部输入栏 ===
        var inputBar = new GameObject("InputBar");
        inputBar.transform.SetParent(box.transform, false);
        var ibR = inputBar.AddComponent<RectTransform>();
        ibR.anchorMin = new Vector2(0.03f, 0.03f); ibR.anchorMax = new Vector2(0.97f, 0.13f);
        ibR.offsetMin = Vector2.zero; ibR.offsetMax = Vector2.zero;
        inputBar.AddComponent<Image>().color = new Color(0.05f, 0.03f, 0.08f, 0.9f);

        var inputObj = new GameObject("InputField");
        inputObj.transform.SetParent(inputBar.transform, false);
        var iR = inputObj.AddComponent<RectTransform>();
        iR.anchorMin = new Vector2(0.01f, 0.1f); iR.anchorMax = new Vector2(0.80f, 0.9f);
        iR.offsetMin = Vector2.zero; iR.offsetMax = Vector2.zero;
        inputObj.AddComponent<Image>().color = new Color(0.08f, 0.06f, 0.12f, 0.95f);
        _inputField = inputObj.AddComponent<InputField>();
        _inputField.lineType = InputField.LineType.SingleLine;

        var inputText = new GameObject("Text");
        inputText.transform.SetParent(inputObj.transform, false);
        var itR = inputText.AddComponent<RectTransform>();
        itR.anchorMin = Vector2.zero; itR.anchorMax = Vector2.one;
        itR.offsetMin = new Vector2(12, 2); itR.offsetMax = new Vector2(-8, -2);
        var iTxt = inputText.AddComponent<Text>();
        iTxt.alignment = TextAnchor.MiddleLeft; iTxt.fontSize = 18;
        iTxt.color = UIHelper.TextPrimary; iTxt.font = font;
        iTxt.raycastTarget = false;

        var placeholder = new GameObject("Placeholder");
        placeholder.transform.SetParent(inputObj.transform, false);
        var phR = placeholder.AddComponent<RectTransform>();
        phR.anchorMin = Vector2.zero; phR.anchorMax = Vector2.one;
        phR.offsetMin = new Vector2(12, 2); phR.offsetMax = new Vector2(-8, -2);
        var phTxt = placeholder.AddComponent<Text>();
        phTxt.text = "输入消息后按发送..."; phTxt.alignment = TextAnchor.MiddleLeft;
        phTxt.fontSize = 18; phTxt.color = new Color(0.4f, 0.4f, 0.4f, 1f);
        phTxt.fontStyle = FontStyle.Italic; phTxt.font = font;
        _inputField.textComponent = iTxt; _inputField.placeholder = phTxt;

        // 发送按钮
        UIHelper.MakeButton(inputBar.transform, "SendBtn", "发送", font,
            new Vector2(0.82f, 0.1f), new Vector2(0.99f, 0.9f), Vector2.zero, Vector2.zero,
            UIHelper.BtnConfirm, 18, () => SendMessage());

        // 回车发送
        _inputField.onEndEdit.AddListener((text) => { if (Input.GetKeyDown(KeyCode.Return)) SendMessage(); });
    }

    private void SendMessage()
    {
        if (_inputField == null || string.IsNullOrEmpty(_inputField.text)) return;
        string msg = _inputField.text.Trim();
        if (string.IsNullOrEmpty(msg)) return;
        _inputField.text = "";

        // 通过ChatDataManager发送
        if (ChatDataManager.Instance == null) gameObject.AddComponent<ChatDataManager>();
        ChatDataManager.Instance.SendMessage(_friendName, msg);
        RefreshChatLog();
    }

    private void RefreshChatLog()
    {
        if (_chatLog == null) return;

        // 通过ChatDataManager获取聊天记录
        var log = ChatDataManager.Instance?.GetHistory(_friendName);
        if (log == null || log.Count == 0)
        {
            _chatLog.text = $"<color=#888>开始与 {_friendName} 的对话...</color>";
        }
        else
        {
            _chatLog.text = string.Join("\n", log);
        }

        // 滚动到底部
        if (_scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            _scrollRect.verticalNormalizedPosition = 0f;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
