using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// 邮件系统 — 暗色ARPG风格
/// 支持多选、一键领取、删除
/// </summary>
public class MailUI : MonoBehaviour
{
    public static MailUI Instance { get; private set; }

    private GameObject panel;
    private Transform listContainer;
    private Text detailTitle;
    private Text detailContent;
    private Button claimBtn;
    private Button deleteBtn;
    private Text statusText;
    private MailData selectedMail;
    private readonly List<MailData> mailList = new();
    private readonly HashSet<int> selectedIds = new();
    private bool isProcessing;

    private static string ApiBase => (CloudSaveManager.Instance?.ServerUrl ?? "http://localhost:5132") + "/api";

    [Serializable]
    public class MailData
    {
        public int id;
        public string title;
        public string content;
        public int attachmentGold;
        public string attachmentItems;
        public bool isRead;
        public bool claimed;
        public string createdAt;
    }

    [Serializable]
    private class MailListResponse { public List<MailData> mails; }
    [Serializable]
    private class ClaimResponse { public bool success; public int attachmentGold; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Show()
    {
        if (panel == null) BuildUI();
        UIHelper.SetupTopBar("邮件", onBack: () => { Hide(); UIHelper.EnsureUIManager(); UIManager.Instance.ShowHub(); });
        panel.SetActive(true);
        var cg = panel.GetComponent<CanvasGroup>();
        if (cg != null) { cg.alpha = 1f; cg.blocksRaycasts = true; }
        isProcessing = false;
        selectedIds.Clear();
        selectedMail = null;
        if (detailTitle != null) detailTitle.text = "请选择一封邮件";
        if (detailContent != null) detailContent.text = "";
        if (claimBtn != null) claimBtn.gameObject.SetActive(false);
        if (deleteBtn != null) deleteBtn.gameObject.SetActive(false);
        StartCoroutine(LoadMails());
    }

    public void Hide()
    {
        if (panel != null)
        {
            var cg = panel.GetComponent<CanvasGroup>();
            if (cg != null) { cg.alpha = 0f; cg.blocksRaycasts = false; }
            panel.SetActive(false);
        }
    }

    private void BuildUI()
    {
        Font font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();

        panel = UiPrefabLoader.TryLoad("MailPanel", canvas);
        if (panel == null)
        panel = new GameObject("MailPanel");
        panel.transform.SetParent(canvas.transform, false);
        RectTransform pr = panel.AddComponent<RectTransform>();
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one;
        pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;
        panel.AddComponent<CanvasGroup>();

        var bg = new GameObject("BG");
        bg.transform.SetParent(panel.transform, false);
        var bgr = bg.AddComponent<RectTransform>();
        bgr.anchorMin = Vector2.zero; bgr.anchorMax = Vector2.one;
        bgr.offsetMin = Vector2.zero; bgr.offsetMax = Vector2.zero;
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.06f, 0.07f, 0.09f, 0.97f);
        bgImg.raycastTarget = true;

        // 左侧邮件列表
        var listArea = UIHelper.MakeGlowCard(panel.transform, "ListArea",
            new Vector2(0.05f, 0.15f), new Vector2(0.5f, 0.85f),
            Vector2.zero, Vector2.zero,
            UIHelper.GlowTop, UIHelper.GlowBottom, UIHelper.Accent);
        UIHelper.MakeSubtitle(listArea.transform, "邮件列表", font, 8, 16);

        var scrollObj = new GameObject("Scroll");
        scrollObj.transform.SetParent(listArea.transform, false);
        var sr = scrollObj.AddComponent<RectTransform>();
        sr.anchorMin = new Vector2(0.05f, 0.02f); sr.anchorMax = new Vector2(0.95f, 0.82f);
        sr.offsetMin = Vector2.zero; sr.offsetMax = Vector2.zero;
        var viewportImg = scrollObj.AddComponent<Image>();
        viewportImg.color = new Color(0.03f, 0.04f, 0.06f, 0.8f);
        var mask = scrollObj.AddComponent<RectMask2D>();
        var scrollRect = scrollObj.AddComponent<ScrollRect>();

        var contentObj = new GameObject("Content");
        contentObj.transform.SetParent(scrollObj.transform, false);
        var cr = contentObj.AddComponent<RectTransform>();
        cr.anchorMin = new Vector2(0, 1); cr.anchorMax = new Vector2(1, 1);
        cr.pivot = new Vector2(0.5f, 1);
        cr.sizeDelta = new Vector2(0, 0);
        var vg = contentObj.AddComponent<VerticalLayoutGroup>();
        vg.spacing = 4; vg.padding = new RectOffset(5, 5, 5, 5);
        vg.childControlWidth = true; vg.childForceExpandWidth = true;
        var csf = contentObj.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.viewport = scrollObj.GetComponent<RectTransform>();
        scrollRect.content = cr;
        scrollRect.vertical = true; scrollRect.horizontal = false;
        scrollRect.scrollSensitivity = 20f;
        listContainer = contentObj.transform;

        // 右侧邮件详情
        var detailArea = UIHelper.MakeGlowCard(panel.transform, "DetailArea",
            new Vector2(0.52f, 0.15f), new Vector2(0.95f, 0.85f),
            Vector2.zero, Vector2.zero,
            UIHelper.GlowTop, UIHelper.GlowBottom, UIHelper.Accent);
        UIHelper.MakeSubtitle(detailArea.transform, "邮件详情", font, 8, 16);

        var titleObj = new GameObject("DetailTitle");
        titleObj.transform.SetParent(detailArea.transform, false);
        var tr = titleObj.AddComponent<RectTransform>();
        tr.anchorMin = new Vector2(0.05f, 0.75f); tr.anchorMax = new Vector2(0.95f, 0.85f);
        tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
        detailTitle = titleObj.AddComponent<Text>();
        detailTitle.alignment = TextAnchor.MiddleCenter;
        detailTitle.fontSize = 26; detailTitle.color = Color.white; detailTitle.font = font;
        detailTitle.text = "请选择一封邮件";

        var contentObj2 = new GameObject("DetailContent");
        contentObj2.transform.SetParent(detailArea.transform, false);
        var cr2 = contentObj2.AddComponent<RectTransform>();
        cr2.anchorMin = new Vector2(0.05f, 0.25f); cr2.anchorMax = new Vector2(0.95f, 0.72f);
        cr2.offsetMin = Vector2.zero; cr2.offsetMax = Vector2.zero;
        detailContent = contentObj2.AddComponent<Text>();
        detailContent.alignment = TextAnchor.UpperLeft;
        detailContent.fontSize = 22; detailContent.color = Color.white; detailContent.font = font;

        // 单封领取按钮
        var claimBtnObj = UIHelper.MakeGlowCard(detailArea.transform, "ClaimBtn",
            new Vector2(0.05f, 0.05f), new Vector2(0.48f, 0.18f),
            Vector2.zero, Vector2.zero,
            new Color(0.05f, 0.10f, 0.08f, 0.92f),
            new Color(0.02f, 0.05f, 0.04f, 0.92f),
            UIHelper.Accent);
        claimBtn = claimBtnObj.AddComponent<Button>();
        claimBtn.transition = Selectable.Transition.None;
        MakeLabel(claimBtnObj, "领取附件", 22, UIHelper.TextPrimary, font);
        claimBtn.gameObject.SetActive(false);

        // 单封删除按钮
        var delBtnObj = UIHelper.MakeGlowCard(detailArea.transform, "DeleteBtn",
            new Vector2(0.52f, 0.05f), new Vector2(0.95f, 0.18f),
            Vector2.zero, Vector2.zero,
            new Color(0.15f, 0.05f, 0.05f, 0.92f),
            new Color(0.08f, 0.02f, 0.02f, 0.92f),
            new Color(0.8f, 0.3f, 0.3f));
        deleteBtn = delBtnObj.AddComponent<Button>();
        deleteBtn.transition = Selectable.Transition.None;
        MakeLabel(delBtnObj, "删除邮件", 22, Color.white, font);
        deleteBtn.gameObject.SetActive(false);

        // 底部操作栏
        var bottomBar = UIHelper.MakeGlowCard(panel.transform, "BottomBar",
            new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.12f),
            Vector2.zero, Vector2.zero,
            UIHelper.GlowTop, UIHelper.GlowBottom, UIHelper.Accent);

        var selectAllObj = new GameObject("SelectAllBtn");
        selectAllObj.transform.SetParent(bottomBar.transform, false);
        var saRect = selectAllObj.AddComponent<RectTransform>();
        saRect.anchorMin = new Vector2(0.01f, 0.1f); saRect.anchorMax = new Vector2(0.18f, 0.9f);
        saRect.offsetMin = Vector2.zero; saRect.offsetMax = Vector2.zero;
        var saImg = selectAllObj.AddComponent<Image>();
        saImg.color = new Color(0.10f, 0.12f, 0.16f, 0.9f);
        var saBtn = selectAllObj.AddComponent<Button>();
        saBtn.onClick.AddListener(ToggleSelectAll);
        MakeLabel(selectAllObj, "全选", 18, Color.white, font);

        var claimAllObj = new GameObject("ClaimAllBtn");
        claimAllObj.transform.SetParent(bottomBar.transform, false);
        var caRect = claimAllObj.AddComponent<RectTransform>();
        caRect.anchorMin = new Vector2(0.20f, 0.1f); caRect.anchorMax = new Vector2(0.42f, 0.9f);
        caRect.offsetMin = Vector2.zero; caRect.offsetMax = Vector2.zero;
        var caImg = claimAllObj.AddComponent<Image>();
        caImg.color = new Color(0.05f, 0.15f, 0.08f, 0.95f);
        var caBtn = claimAllObj.AddComponent<Button>();
        caBtn.onClick.AddListener(ClaimSelected);
        MakeLabel(claimAllObj, "一键领取", 18, new Color(0.5f, 1f, 0.5f), font);

        var delAllObj = new GameObject("DeleteAllBtn");
        delAllObj.transform.SetParent(bottomBar.transform, false);
        var daRect = delAllObj.AddComponent<RectTransform>();
        daRect.anchorMin = new Vector2(0.44f, 0.1f); daRect.anchorMax = new Vector2(0.66f, 0.9f);
        daRect.offsetMin = Vector2.zero; daRect.offsetMax = Vector2.zero;
        var daImg = delAllObj.AddComponent<Image>();
        daImg.color = new Color(0.20f, 0.06f, 0.06f, 0.95f);
        var daBtn = delAllObj.AddComponent<Button>();
        daBtn.onClick.AddListener(DeleteSelected);
        MakeLabel(delAllObj, "批量删除", 18, new Color(1f, 0.5f, 0.5f), font);

        var statusObj = new GameObject("StatusText");
        statusObj.transform.SetParent(bottomBar.transform, false);
        var stRect = statusObj.AddComponent<RectTransform>();
        stRect.anchorMin = new Vector2(0.68f, 0.1f); stRect.anchorMax = new Vector2(0.99f, 0.9f);
        stRect.offsetMin = Vector2.zero; stRect.offsetMax = Vector2.zero;
        statusText = statusObj.AddComponent<Text>();
        statusText.alignment = TextAnchor.MiddleCenter;
        statusText.fontSize = 16; statusText.color = UIHelper.TextDim; statusText.font = font;
        statusText.text = "已选: 0";

        panel.SetActive(false);
    }

    private IEnumerator LoadMails()
    {
        mailList.Clear();
        selectedIds.Clear();

        var localMails = LocalMailSystem.GetMails();
        foreach (var lm in localMails)
        {
            mailList.Add(new MailData
            {
                id = -(lm.id + 1),
                title = lm.title,
                content = lm.content,
                attachmentGold = lm.gold,
                attachmentItems = lm.itemJson,
                isRead = true,
                claimed = lm.claimed,
                createdAt = lm.createdDate
            });
        }

        RefreshList();

        string token = CloudSaveManager.Instance?.Token ?? "";
        if (!string.IsNullOrEmpty(token))
        {
            using (var req = UnityWebRequest.Get($"{ApiBase}/mail/list"))
            {
                req.SetRequestHeader("Authorization", $"Bearer {token}");
                req.timeout = 5;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    var resp = JsonUtility.FromJson<MailListResponse>(req.downloadHandler.text);
                    if (resp?.mails != null) mailList.AddRange(resp.mails);
                }
            }
            RefreshList();
        }
    }

    private void RefreshList()
    {
        for (int i = listContainer.childCount - 1; i >= 0; i--)
            Destroy(listContainer.GetChild(i).gameObject);

        Font font = GameManager.GetUIFont();

        if (mailList.Count == 0)
        {
            var emptyObj = new GameObject("Empty");
            emptyObj.transform.SetParent(listContainer, false);
            var emptyLE = emptyObj.AddComponent<LayoutElement>();
            emptyLE.preferredHeight = 60f;
            var emptyTxt = emptyObj.AddComponent<Text>();
            emptyTxt.text = "暂无邮件"; emptyTxt.alignment = TextAnchor.MiddleCenter;
            emptyTxt.fontSize = 20; emptyTxt.color = UIHelper.TextDim; emptyTxt.font = font;
            UpdateStatusText();
            return;
        }

        foreach (var mail in mailList)
        {
            var rowObj = new GameObject($"Mail_{mail.id}");
            rowObj.transform.SetParent(listContainer, false);
            var rowRT = rowObj.AddComponent<RectTransform>();
            var rowLE = rowObj.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 55f;
            rowLE.flexibleWidth = 1f;

            bool isSelected = selectedIds.Contains(mail.id);

            var rowImg = rowObj.AddComponent<Image>();
            rowImg.color = isSelected ? new Color(0.12f, 0.25f, 0.15f, 0.95f) :
                           mail.claimed ? new Color(0.12f, 0.12f, 0.14f, 0.6f) :
                           new Color(0.15f, 0.17f, 0.22f, 0.9f);

            // 左侧：复选框（独立按钮，不被行按钮覆盖）
            var checkObj = new GameObject("CheckBtn");
            checkObj.transform.SetParent(rowObj.transform, false);
            var chkRect = checkObj.AddComponent<RectTransform>();
            chkRect.anchorMin = new Vector2(0, 0.1f);
            chkRect.anchorMax = new Vector2(0.12f, 0.9f);
            chkRect.offsetMin = Vector2.zero;
            chkRect.offsetMax = Vector2.zero;
            var chkImg = checkObj.AddComponent<Image>();
            chkImg.color = mail.claimed ? new Color(0.08f, 0.08f, 0.08f, 0.5f) :
                          isSelected ? new Color(0.25f, 0.65f, 0.25f, 0.95f) : new Color(0.18f, 0.20f, 0.25f, 0.8f);
            var chkBtn = checkObj.AddComponent<Button>();
            var data = mail;
            chkBtn.onClick.AddListener(() => ToggleSelect(data));
            chkBtn.interactable = true;
            MakeLabel(checkObj, isSelected ? "✓" : "", 24, mail.claimed ? new Color(0.4f, 0.4f, 0.4f) : Color.white, font);

            // 中间：邮件标题（点击查看详情）
            var txtBtnObj = new GameObject("TextBtn");
            txtBtnObj.transform.SetParent(rowObj.transform, false);
            var txtBtnRect = txtBtnObj.AddComponent<RectTransform>();
            txtBtnRect.anchorMin = new Vector2(0.12f, 0);
            txtBtnRect.anchorMax = new Vector2(1, 1);
            txtBtnRect.offsetMin = Vector2.zero;
            txtBtnRect.offsetMax = Vector2.zero;
            var txtBtnImg = txtBtnObj.AddComponent<Image>();
            txtBtnImg.color = Color.clear;
            var txtBtn = txtBtnObj.AddComponent<Button>();
            txtBtn.transition = Selectable.Transition.None;
            txtBtn.targetGraphic = txtBtnImg;
            txtBtn.onClick.AddListener(() => SelectMail(data));

            var txtObj = new GameObject("Label");
            txtObj.transform.SetParent(txtBtnObj.transform, false);
            var txtRect = txtObj.AddComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = new Vector2(10, 0);
            txtRect.offsetMax = new Vector2(-10, 0);
            var txt = txtObj.AddComponent<Text>();
            string prefix = mail.claimed ? "✓ 已领取 " : "○ ";
            txt.text = $"{prefix}{mail.title}";
            txt.alignment = TextAnchor.MiddleLeft;
            txt.fontSize = 22;
            txt.color = mail.claimed ? new Color(0.4f, 0.4f, 0.45f, 0.7f) : Color.white;
            txt.font = font;
            txt.raycastTarget = false;
        }

        UpdateStatusText();
    }

    private void ToggleSelect(MailData mail)
    {
        if (selectedIds.Contains(mail.id))
            selectedIds.Remove(mail.id);
        else
            selectedIds.Add(mail.id);
        RefreshList();
    }

    private void ToggleSelectAll()
    {
        if (selectedIds.Count >= mailList.Count)
            selectedIds.Clear();
        else
        {
            selectedIds.Clear();
            foreach (var m in mailList) selectedIds.Add(m.id);
        }
        RefreshList();
    }

    private void UpdateStatusText()
    {
        if (statusText != null)
            statusText.text = $"已选: {selectedIds.Count}/{mailList.Count}";
    }

    private void SelectMail(MailData mail)
    {
        selectedMail = mail;
        if (detailTitle != null) detailTitle.text = mail.title;
        if (detailContent != null)
        {
            string content = mail.content;
            if (mail.attachmentGold > 0)
                content += $"\n\n附件: ◆{mail.attachmentGold} 金币";
            if (!string.IsNullOrEmpty(mail.attachmentItems) && mail.attachmentItems != "[]")
                content += "\n附件物品已附带";
            if (mail.claimed)
                content += "\n\n<color=#888888>[已领取]</color>";
            detailContent.text = content;
            detailContent.color = mail.claimed ? new Color(0.6f, 0.6f, 0.65f) : Color.white;
        }

        if (detailTitle != null)
            detailTitle.color = mail.claimed ? new Color(0.5f, 0.5f, 0.55f) : Color.white;

        if (!mail.isRead && mail.id > 0)
            StartCoroutine(MarkAsRead(mail.id));

        if (claimBtn != null)
        {
            bool hasAttachment = mail.attachmentGold > 0 ||
                (!string.IsNullOrEmpty(mail.attachmentItems) && mail.attachmentItems != "[]");

            if (mail.claimed)
            {
                // 已领取：按钮灰色显示,不可点击
                claimBtn.gameObject.SetActive(true);
                claimBtn.interactable = false;
                claimBtn.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 0.6f);
            }
            else if (hasAttachment)
            {
                claimBtn.gameObject.SetActive(true);
                claimBtn.interactable = true;
                claimBtn.GetComponent<Image>().color = new Color(0.05f, 0.20f, 0.10f, 0.92f);
                claimBtn.onClick.RemoveAllListeners();
                if (mail.id < 0)
                {
                    int localMailId = -(mail.id + 1);
                    claimBtn.onClick.AddListener(() => ClaimLocalMail(localMailId));
                }
                else
                    claimBtn.onClick.AddListener(() => StartCoroutine(ClaimMail(mail.id)));
            }
            else
            {
                claimBtn.gameObject.SetActive(false);
            }
        }

        if (deleteBtn != null)
        {
            deleteBtn.gameObject.SetActive(true);
            deleteBtn.onClick.RemoveAllListeners();
            if (mail.id < 0)
            {
                int localMailId = -(mail.id + 1);
                deleteBtn.onClick.AddListener(() => DeleteLocalMail(localMailId));
            }
            else
                deleteBtn.onClick.AddListener(() => StartCoroutine(DeleteServerMail(mail.id)));
        }
    }

    private void ClaimSelected()
    {
        if (isProcessing) return;
        if (selectedIds.Count == 0)
        {
            ShowToast("请先选择邮件");
            return;
        }
        isProcessing = true;

        var localIds = new List<int>();
        var serverIds = new List<int>();
        foreach (var id in selectedIds)
        {
            if (id < 0)
                localIds.Add(-(id + 1));
            else
                serverIds.Add(id);
        }

        int totalClaimed = 0;

        if (localIds.Count > 0)
        {
            totalClaimed += LocalMailSystem.ClaimByIds(localIds);
            var player = GameManager.Instance?.Player;
            if (player != null) player.Stats.Save();
        }

        if (serverIds.Count > 0)
            StartCoroutine(ClaimServerMails(serverIds, totalClaimed));
        else
            StartCoroutine(ReloadAfterClaim());
    }

    private IEnumerator ClaimServerMails(List<int> mailIds, int alreadyClaimed)
    {
        string token = CloudSaveManager.Instance?.Token ?? "";
        int claimed = alreadyClaimed;

        foreach (var mailId in mailIds)
        {
            using (var req = UnityWebRequest.PostWwwForm($"{ApiBase}/mail/claim?mailId={mailId}", ""))
            {
                req.SetRequestHeader("Authorization", $"Bearer {token}");
                req.timeout = 10;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    var resp = JsonUtility.FromJson<ClaimResponse>(req.downloadHandler.text);
                    if (resp?.success == true && resp.attachmentGold > 0)
                    {
                        var player = GameManager.Instance?.Player;
                        if (player != null) player.Stats.AddGold(resp.attachmentGold);
                        claimed++;
                    }
                }
            }
        }

                    GameLog.Log($"[Mail] 一键领取完成: {claimed}封");
        ShowToast(claimed > 0 ? $"领取成功! {claimed}封邮件" : "无可领取的邮件(背包已满?)");
        StartCoroutine(ReloadAfterClaim());
    }

    private void DeleteSelected()
    {
        if (isProcessing) return;
        if (selectedIds.Count == 0)
        {
            ShowToast("请先选择邮件");
            return;
        }
        isProcessing = true;

        var localIds = new List<int>();
        var serverIds = new List<int>();
        foreach (var id in selectedIds)
        {
            if (id < 0)
                localIds.Add(-(id + 1));
            else
                serverIds.Add(id);
        }

        int deleted = 0;

        if (localIds.Count > 0)
            deleted += LocalMailSystem.DeleteByIds(localIds);

        if (serverIds.Count > 0)
            StartCoroutine(DeleteServerMails(serverIds, deleted));
        else
        {
            GameLog.Log($"[Mail] 本地批量删除完成: {deleted}封");
            ShowToast(deleted > 0 ? $"已删除 {deleted}封邮件" : "未找到匹配邮件");
            StartCoroutine(ReloadAfterClaim());
        }
    }

    private IEnumerator DeleteServerMails(List<int> mailIds, int alreadyDeleted)
    {
        string token = CloudSaveManager.Instance?.Token ?? "";
        int deleted = alreadyDeleted;

        foreach (var mailId in mailIds)
        {
            using (var req = UnityWebRequest.PostWwwForm($"{ApiBase}/mail/delete?mailId={mailId}", ""))
            {
                req.SetRequestHeader("Authorization", $"Bearer {token}");
                req.timeout = 5;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success) deleted++;
            }
        }

                    GameLog.Log($"[Mail] 批量删除完成: {deleted}封");
        ShowToast($"已删除 {deleted}封邮件");
        StartCoroutine(ReloadAfterClaim());
    }

    private void DeleteLocalMail(int mailId)
    {
        if (LocalMailSystem.DeleteMailById(mailId))
        {
                        GameLog.Log($"[Mail] 本地邮件 #{mailId} 已删除");
            if (claimBtn != null) claimBtn.gameObject.SetActive(false);
            if (deleteBtn != null) deleteBtn.gameObject.SetActive(false);
            if (detailTitle != null) detailTitle.text = "已删除";
            if (detailContent != null) detailContent.text = "";
            StartCoroutine(ReloadAfterClaim());
        }
    }

    private IEnumerator DeleteServerMail(int mailId)
    {
        string token = CloudSaveManager.Instance?.Token ?? "";
        using (var req = UnityWebRequest.PostWwwForm($"{ApiBase}/mail/delete?mailId={mailId}", ""))
        {
            req.SetRequestHeader("Authorization", $"Bearer {token}");
            req.timeout = 5;
            yield return req.SendWebRequest();
        }
        if (claimBtn != null) claimBtn.gameObject.SetActive(false);
        if (deleteBtn != null) deleteBtn.gameObject.SetActive(false);
        if (detailTitle != null) detailTitle.text = "已删除";
        if (detailContent != null) detailContent.text = "";
        StartCoroutine(ReloadAfterClaim());
    }

    private IEnumerator MarkAsRead(int mailId)
    {
        string token = CloudSaveManager.Instance?.Token ?? "";
        using (var req = UnityWebRequest.PostWwwForm($"{ApiBase}/mail/read?mailId={mailId}", ""))
        {
            req.SetRequestHeader("Authorization", $"Bearer {token}");
            req.timeout = 5;
            yield return req.SendWebRequest();
        }
    }

    private IEnumerator ClaimMail(int mailId)
    {
        string token = CloudSaveManager.Instance?.Token ?? "";
        using (var req = UnityWebRequest.PostWwwForm($"{ApiBase}/mail/claim?mailId={mailId}", ""))
        {
            req.SetRequestHeader("Authorization", $"Bearer {token}");
            req.timeout = 10;
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var resp = JsonUtility.FromJson<ClaimResponse>(req.downloadHandler.text);
                if (resp?.success == true && resp.attachmentGold > 0)
                {
                    var player = GameManager.Instance?.Player;
                    if (player != null) player.Stats.AddGold(resp.attachmentGold);
                }
            }
        }
        StartCoroutine(ReloadAfterClaim());
    }

    private void ClaimLocalMail(int mailId)
    {
        if (LocalMailSystem.ClaimMailById(mailId))
        {
            var player = GameManager.Instance?.Player;
            if (player != null) player.Stats.Save();
            if (detailContent != null) detailContent.text += "\n\n<color=#66FF66>领取成功!</color>";
            if (claimBtn != null) claimBtn.gameObject.SetActive(false);
            ShowToast("领取成功!");
            StartCoroutine(ReloadAfterClaim());
        }
        else
        {
            ShowToast("背包已满! 请先清理背包");
            if (detailContent != null)
                detailContent.text += "\n\n<color=#FF6666>背包已满! 请先清理背包再领取。</color>";
        }
    }

    private void ShowToast(string message)
    {
        if (GameUI.Instance != null)
            GameUI.Instance.ShowItemPickupToast("邮件", message);
    }

    private IEnumerator ReloadAfterClaim()
    {
        yield return LoadMails();
        isProcessing = false;
    }

    private void MakeLabel(GameObject parent, string text, int fontSize, Color color, Font font)
    {
        var label = new GameObject("L");
        label.transform.SetParent(parent.transform, false);
        var rect = label.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        var t = label.AddComponent<Text>();
        t.text = text; t.alignment = TextAnchor.MiddleCenter;
        t.fontSize = fontSize; t.color = color; t.font = font;
        t.raycastTarget = false;
    }

    private void OnDestroy()
    {
        if (panel != null) Destroy(panel);
        if (Instance == this) Instance = null;
    }
}
