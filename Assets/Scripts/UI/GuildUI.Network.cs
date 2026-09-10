using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// GuildUI partial — HTTP network helpers for guild server API.
/// Contains: GetGuildAPI, PostGuildAPI, CheckGuildInvites, ShowGuildInvitesPopup, RespondGuildRequest
/// </summary>
public partial class GuildUI
{
    private const string ServerUrl = "http://39.107.141.107:5132";

    private IEnumerator GetGuildAPI(string path, System.Action<bool, string> callback)
    {
        string url = $"{ServerUrl}{path}";
        string token = PlayerPrefs.GetString("ARPG_AuthToken", "");
        using (var req = UnityEngine.Networking.UnityWebRequest.Get(url))
        {
            req.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrEmpty(token)) req.SetRequestHeader("Authorization", "Bearer " + token);
            yield return req.SendWebRequest();
            callback(req.result == UnityEngine.Networking.UnityWebRequest.Result.Success, req.downloadHandler?.text ?? "");
        }
    }

    private IEnumerator PostGuildAPI(string path, string jsonBody, System.Action<bool, string> callback)
    {
        string url = $"{ServerUrl}{path}";
        string token = PlayerPrefs.GetString("ARPG_AuthToken", "");
        using (var req = new UnityEngine.Networking.UnityWebRequest(url, "POST"))
        {
            req.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonBody ?? "{}"));
            req.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrEmpty(token)) req.SetRequestHeader("Authorization", "Bearer " + token);
            yield return req.SendWebRequest();
            callback(req.result == UnityEngine.Networking.UnityWebRequest.Result.Success, req.downloadHandler?.text ?? "");
        }
    }

    /// <summary>检查待处理的公会邀请</summary>
    private IEnumerator CheckGuildInvites()
    {
        yield return GetGuildAPI("/api/guild/invites", (ok, resp) =>
        {
            if (!ok || string.IsNullOrEmpty(resp)) return;
            try
            {
                var obj = JsonUtility.FromJson<GuildInvitesResponse>(resp);
                if (obj?.invites != null && obj.invites.Length > 0)
                    StartCoroutine(ShowGuildInvitesPopup(obj.invites));
            }
            catch { }
        });
    }

    [System.Serializable]
    public class GuildInvitesResponse { public GuildInviteData[] invites; public int count; }
    [System.Serializable]
    public class GuildInviteData { public int id; public int guildId; public string guildName; public int guildLevel; public string fromName; public string message; }

    /// <summary>显示公会邀请弹窗</summary>
    private IEnumerator ShowGuildInvitesPopup(GuildInviteData[] invites)
    {
        Canvas canvas = GameManager.EnsureCanvas();
        var overlay = new GameObject("GuildInviteNotice");
        overlay.transform.SetParent(canvas.transform, false);
        var oR = overlay.AddComponent<RectTransform>();
        oR.anchorMin = Vector2.zero; oR.anchorMax = Vector2.one;
        oR.offsetMin = Vector2.zero; oR.offsetMax = Vector2.zero;
        overlay.AddComponent<Image>().color = new Color(0, 0, 0, 0.7f);

        var box = UIHelper.MakeGlowCard(overlay.transform, "Box",
            new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.8f), Vector2.zero, Vector2.zero,
            new Color(0.06f, 0.03f, 0.09f, 0.95f), UIHelper.GlowBottom, UIHelper.Accent);

        var titleObj = new GameObject("Title"); titleObj.transform.SetParent(box.transform, false);
        var tR = titleObj.AddComponent<RectTransform>();
        tR.anchorMin = new Vector2(0, 0.92f); tR.anchorMax = new Vector2(0.85f, 1f);
        tR.offsetMin = Vector2.zero; tR.offsetMax = Vector2.zero;
        var title = titleObj.AddComponent<Text>();
        title.text = "公会邀请"; title.alignment = TextAnchor.MiddleLeft;
        title.fontSize = 22; title.color = UIHelper.Accent; title.font = _font;

        UIHelper.MakeButton(box.transform, "CloseBtn", "X", _font,
            new Vector2(0.88f, 0.93f), new Vector2(0.98f, 0.99f), Vector2.zero, Vector2.zero,
            UIHelper.BtnDanger, 18, () => Destroy(overlay));

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
            string gName = inv.guildName;
            int gLv = inv.guildLevel;
            string fromName = inv.fromName;
            float y = -i * (rowH + gap) - 4f;

            var row = UIHelper.MakeGlowCard(scrollObj.transform, $"Invite_{i}",
                new Vector2(0.02f, 1), new Vector2(0.98f, 1),
                new Vector2(0, y - rowH), new Vector2(0, y),
                new Color(0.06f, 0.04f, 0.08f, 0.9f), UIHelper.GlowBottom, UIHelper.BorderSubtle);

            var infoObj = new GameObject("Info"); infoObj.transform.SetParent(row.transform, false);
            var iR = infoObj.AddComponent<RectTransform>();
            iR.anchorMin = new Vector2(0.03f, 0.1f); iR.anchorMax = new Vector2(0.60f, 0.9f);
            iR.offsetMin = Vector2.zero; iR.offsetMax = Vector2.zero;
            var infoTxt = infoObj.AddComponent<Text>();
            infoTxt.text = $"{gName}  Lv.{gLv}\n邀请人: {fromName}";
            infoTxt.alignment = TextAnchor.MiddleLeft; infoTxt.fontSize = 15;
            infoTxt.color = UIHelper.TextPrimary; infoTxt.font = _font;
            infoTxt.supportRichText = true; infoTxt.raycastTarget = false;

            UIHelper.MakeButton(row.transform, "AcceptBtn", "接受", _font,
                new Vector2(0.62f, 0.15f), new Vector2(0.79f, 0.85f), Vector2.zero, Vector2.zero,
                UIHelper.BtnConfirm, 14, () => { StartCoroutine(RespondGuildRequest(reqId, true, overlay)); });
            UIHelper.MakeButton(row.transform, "DeclineBtn", "拒绝", _font,
                new Vector2(0.81f, 0.15f), new Vector2(0.98f, 0.85f), Vector2.zero, Vector2.zero,
                UIHelper.BtnDanger, 14, () => { StartCoroutine(RespondGuildRequest(reqId, false, overlay)); });
        }
        yield return null;
    }

    /// <summary>响应公会邀请/申请</summary>
    private IEnumerator RespondGuildRequest(int requestId, bool accept, GameObject overlay)
    {
        string body = $"{{\"accept\":{(accept ? "true" : "false")}}}";
        yield return PostGuildAPI($"/api/guild/respond/{requestId}", body, (ok, resp) =>
        {
            if (ok)
            {
                if (GameUI.Instance != null)
                    GameUI.Instance.ShowItemPickupToast(accept ? "已加入公会" : "已拒绝", accept ? "欢迎加入！" : "");
                if (accept)
                {
                    if (overlay != null) Destroy(overlay);
                    Hide();
                    Show();
                }
                else
                {
                    if (overlay != null) Destroy(overlay);
                }
            }
            else
            {
                if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("操作失败", "");
            }
        });
    }
}
