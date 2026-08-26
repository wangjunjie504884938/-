using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 公会邀请顶部横幅 — 进入大本营时检查，有未处理邀请则显示横幅
/// 3秒后自动收起，可点击展开查看详情
/// </summary>
public class GuildInviteBanner : MonoBehaviour
{
    public static GuildInviteBanner Instance { get; private set; }

    private GameObject _banner;
    private GameObject _detailPanel;
    private Text _titleText;
    private Text _descText;
    private Coroutine _autoHideRoutine;
    private bool _detailOpen;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>检查是否有待处理邀请，有则显示横幅</summary>
    public void CheckInvites()
    {
        StartCoroutine(CheckAndShow());
    }

    private IEnumerator CheckAndShow()
    {
        yield return FetchInviteCount((count) =>
        {
            if (count > 0)
                ShowBanner(count);
        });
    }

    private IEnumerator FetchInviteCount(System.Action<int> callback)
    {
        string url = "http://39.107.141.107:5132/api/guild/invite/count";
        string token = PlayerPrefs.GetString("ARPG_AuthToken", "");
        if (string.IsNullOrEmpty(token)) { callback?.Invoke(0); yield break; }

        using (var req = UnityEngine.Networking.UnityWebRequest.Get(url))
        {
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + token);
            yield return req.SendWebRequest();

            if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                try
                {
                    var resp = JsonUtility.FromJson<CountResponse>(req.downloadHandler.text);
                    callback?.Invoke(resp?.count ?? 0);
                }
                catch { callback?.Invoke(0); }
            }
            else
            {
                callback?.Invoke(0);
            }
        }
    }

    [System.Serializable]
    private class CountResponse { public int count; }

    /// <summary>显示横幅</summary>
    private void ShowBanner(int count)
    {
        if (_banner != null) Destroy(_banner);

        Canvas canvas = GameManager.EnsureCanvas();
        Font font = GameManager.GetUIFont();

        _banner = new GameObject("GuildInviteBanner");
        _banner.transform.SetParent(canvas.transform, false);
        var br = _banner.AddComponent<RectTransform>();
        br.anchorMin = new Vector2(0.15f, 1); br.anchorMax = new Vector2(0.85f, 1);
        br.pivot = new Vector2(0.5f, 1);
        br.anchoredPosition = new Vector2(0, 0);
        br.sizeDelta = new Vector2(0, 50);
        var bImg = _banner.AddComponent<Image>();
        bImg.color = new Color(0.15f, 0.08f, 0.25f, 0.95f);

        // 左侧红点
        var dotObj = new GameObject("RedDot");
        dotObj.transform.SetParent(_banner.transform, false);
        var dotR = dotObj.AddComponent<RectTransform>();
        dotR.anchorMin = new Vector2(0.02f, 0.2f); dotR.anchorMax = new Vector2(0.02f, 0.8f);
        dotR.pivot = new Vector2(0, 0.5f);
        dotR.sizeDelta = new Vector2(12, 12);
        var dotImg = dotObj.AddComponent<Image>();
        dotImg.color = new Color(1f, 0.2f, 0.2f, 1f);
        dotImg.raycastTarget = false;

        // 标题
        var titleObj = new GameObject("Title");
        titleObj.transform.SetParent(_banner.transform, false);
        var tR = titleObj.AddComponent<RectTransform>();
        tR.anchorMin = new Vector2(0.06f, 0); tR.anchorMax = new Vector2(0.70f, 1);
        tR.offsetMin = Vector2.zero; tR.offsetMax = Vector2.zero;
        _titleText = titleObj.AddComponent<Text>();
        _titleText.text = $"公会邀请  x{count}";
        _titleText.alignment = TextAnchor.MiddleLeft;
        _titleText.fontSize = 16; _titleText.color = UIHelper.Accent; _titleText.font = font;
        _titleText.raycastTarget = false;

        // 点击查看按钮
        UIHelper.MakeButton(_banner.transform, "ViewBtn", "查看", font,
            new Vector2(0.72f, 0.1f), new Vector2(0.98f, 0.9f), Vector2.zero, Vector2.zero,
            UIHelper.BtnConfirm, 14, () =>
            {
                if (_autoHideRoutine != null) StopCoroutine(_autoHideRoutine);
                StartCoroutine(ShowInviteDetail());
            });

        // 入场动画 + 3秒自动收起
        var cg = _banner.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        _autoHideRoutine = StartCoroutine(AnimateBannerIn(cg));
    }

    private IEnumerator AnimateBannerIn(CanvasGroup cg)
    {
        float t = 0;
        while (t < 0.3f)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Clamp01(t / 0.3f);
            yield return null;
        }
        cg.alpha = 1f;

        // 3秒后自动收起(变为小图标)
        yield return new WaitForSecondsRealtime(3f);

        if (!_detailOpen && _banner != null)
        {
            // 收起为右上角小图标
            var br = _banner.GetComponent<RectTransform>();
            br.anchorMin = new Vector2(1, 1); br.anchorMax = new Vector2(1, 1);
            br.pivot = new Vector2(1, 1);
            br.anchoredPosition = new Vector2(-60, -60);
            br.sizeDelta = new Vector2(40, 40);
            // 隐藏标题，只显示红点
            var title = _banner.transform.Find("Title");
            if (title != null) title.gameObject.SetActive(false);
            var viewBtn = _banner.transform.Find("ViewBtn");
            if (viewBtn != null) viewBtn.gameObject.SetActive(false);
            var dot = _banner.transform.Find("RedDot");
            if (dot != null)
            {
                var dotR = dot.GetComponent<RectTransform>();
                dotR.anchorMin = new Vector2(0.5f, 0.5f); dotR.anchorMax = new Vector2(0.5f, 0.5f);
                dotR.pivot = new Vector2(0.5f, 0.5f);
                dotR.sizeDelta = new Vector2(30, 30);
            }
        }
    }

    /// <summary>展开邀请详情列表</summary>
    private IEnumerator ShowInviteDetail()
    {
        _detailOpen = true;
        if (_banner != null) _banner.SetActive(false);

        string url = "http://39.107.141.107:5132/api/guild/invites";
        string token = PlayerPrefs.GetString("ARPG_AuthToken", "");

        using (var req = UnityEngine.Networking.UnityWebRequest.Get(url))
        {
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + token);
            yield return req.SendWebRequest();

            if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                _detailOpen = false;
                if (_banner != null) _banner.SetActive(true);
                yield break;
            }

            try
            {
                var resp = JsonUtility.FromJson<GuildUI.GuildInvitesResponse>(req.downloadHandler.text);
                if (resp?.invites != null && resp.invites.Length > 0)
                    ShowDetailPanel(resp.invites);
                else
                {
                    _detailOpen = false;
                    if (_banner != null) Destroy(_banner);
                }
            }
            catch
            {
                _detailOpen = false;
                if (_banner != null) _banner.SetActive(true);
            }
        }
    }

    private void ShowDetailPanel(GuildUI.GuildInviteData[] invites)
    {
        Canvas canvas = GameManager.EnsureCanvas();
        Font font = GameManager.GetUIFont();

        _detailPanel = new GameObject("GuildInviteDetail");
        _detailPanel.transform.SetParent(canvas.transform, false);
        var dr = _detailPanel.AddComponent<RectTransform>();
        dr.anchorMin = new Vector2(0.2f, 0.2f); dr.anchorMax = new Vector2(0.8f, 0.8f);
        dr.offsetMin = Vector2.zero; dr.offsetMax = Vector2.zero;
        _detailPanel.AddComponent<Image>().color = new Color(0, 0, 0, 0.8f);

        var box = UIHelper.MakeGlowCard(_detailPanel.transform, "Box",
            new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f), Vector2.zero, Vector2.zero,
            new Color(0.06f, 0.03f, 0.09f, 0.95f), UIHelper.GlowBottom, UIHelper.Accent);

        var titleObj = new GameObject("Title");
        titleObj.transform.SetParent(box.transform, false);
        var tR = titleObj.AddComponent<RectTransform>();
        tR.anchorMin = new Vector2(0, 0.92f); tR.anchorMax = new Vector2(0.85f, 1f);
        tR.offsetMin = Vector2.zero; tR.offsetMax = Vector2.zero;
        var title = titleObj.AddComponent<Text>();
        title.text = "公会邀请"; title.alignment = TextAnchor.MiddleLeft;
        title.fontSize = 22; title.color = UIHelper.Accent; title.font = font;

        UIHelper.MakeButton(box.transform, "CloseBtn", "X", font,
            new Vector2(0.88f, 0.93f), new Vector2(0.98f, 0.99f), Vector2.zero, Vector2.zero,
            UIHelper.BtnDanger, 18, () => CloseDetail());

        var scrollObj = new GameObject("Scroll");
        scrollObj.transform.SetParent(box.transform, false);
        var sR = scrollObj.AddComponent<RectTransform>();
        sR.anchorMin = new Vector2(0.05f, 0.06f); sR.anchorMax = new Vector2(0.95f, 0.90f);
        sR.offsetMin = Vector2.zero; sR.offsetMax = Vector2.zero;
        scrollObj.AddComponent<Image>().color = new Color(0.03f, 0.02f, 0.05f, 0.5f);
        scrollObj.AddComponent<RectMask2D>();

        float rowH = 80f, gap = 6f;
        for (int i = 0; i < invites.Length; i++)
        {
            var inv = invites[i];
            int reqId = inv.id;
            float y = -i * (rowH + gap) - 4f;

            var row = UIHelper.MakeGlowCard(scrollObj.transform, $"Invite_{i}",
                new Vector2(0.02f, 1), new Vector2(0.98f, 1),
                new Vector2(0, y - rowH), new Vector2(0, y),
                new Color(0.06f, 0.04f, 0.08f, 0.9f), UIHelper.GlowBottom, UIHelper.BorderSubtle);

            var infoObj = new GameObject("Info");
            infoObj.transform.SetParent(row.transform, false);
            var iR = infoObj.AddComponent<RectTransform>();
            iR.anchorMin = new Vector2(0.03f, 0.1f); iR.anchorMax = new Vector2(0.60f, 0.9f);
            iR.offsetMin = Vector2.zero; iR.offsetMax = Vector2.zero;
            var infoTxt = infoObj.AddComponent<Text>();
            infoTxt.text = $"{inv.guildName}  Lv.{inv.guildLevel}\n邀请人: {inv.fromName}";
            infoTxt.alignment = TextAnchor.MiddleLeft; infoTxt.fontSize = 15;
            infoTxt.color = UIHelper.TextPrimary; infoTxt.font = font;
            infoTxt.supportRichText = true; infoTxt.raycastTarget = false;

            UIHelper.MakeButton(row.transform, "AcceptBtn", "接受", font,
                new Vector2(0.62f, 0.15f), new Vector2(0.79f, 0.85f), Vector2.zero, Vector2.zero,
                UIHelper.BtnConfirm, 14, () => { RespondInvite(reqId, true); });
            UIHelper.MakeButton(row.transform, "DeclineBtn", "拒绝", font,
                new Vector2(0.81f, 0.15f), new Vector2(0.98f, 0.85f), Vector2.zero, Vector2.zero,
                UIHelper.BtnDanger, 14, () => { RespondInvite(reqId, false); });
        }
    }

    private void RespondInvite(int requestId, bool accept)
    {
        StartCoroutine(RespondCoroutine(requestId, accept));
    }

    private IEnumerator RespondCoroutine(int requestId, bool accept)
    {
        string url = $"http://39.107.141.107:5132/api/guild/respond/{requestId}";
        string body = $"{{\"accept\":{(accept ? "true" : "false")}}}";
        string token = PlayerPrefs.GetString("ARPG_AuthToken", "");

        using (var req = new UnityEngine.Networking.UnityWebRequest(url, "POST"))
        {
            req.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + token);
            yield return req.SendWebRequest();

            if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                if (GameUI.Instance != null)
                    GameUI.Instance.ShowItemPickupToast(accept ? "已加入公会" : "已拒绝邀请", accept ? "欢迎加入！" : "");
                CloseDetail();
                if (accept && GuildUI.Instance != null)
                {
                    // 重新打开公会面板
                    GuildUI.Instance.Hide();
                    GuildUI.Instance.Show();
                }
            }
            else
            {
                if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("操作失败", "");
            }
        }
    }

    private void CloseDetail()
    {
        _detailOpen = false;
        if (_detailPanel != null) { Destroy(_detailPanel); _detailPanel = null; }
        if (_banner != null) Destroy(_banner);
    }

    public void HideBanner()
    {
        if (_banner != null) { Destroy(_banner); _banner = null; }
        if (_detailPanel != null) { Destroy(_detailPanel); _detailPanel = null; }
        _detailOpen = false;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
