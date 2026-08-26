using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// 通知中心 — 管理游戏内通知（公会邀请/好友申请/系统消息）
/// 客户端轮询服务器获取待处理通知，在TopNavBar显示红点
/// </summary>
public class NotificationCenter : MonoBehaviour
{
    public static NotificationCenter Instance { get; private set; }

    private GameObject _badge;
    private Text _badgeText;
    private int _pendingCount;

    private float _pollTimer = 0f;
    private const float PollInterval = 30f; // 30秒轮询一次

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        _pollTimer += Time.deltaTime;
        if (_pollTimer >= PollInterval)
        {
            _pollTimer = 0f;
            StartCoroutine(PollNotifications());
        }
    }

    /// <summary>在TopNavBar上创建红点徽标</summary>
    public void SetupBadge(Transform navBar)
    {
        if (_badge != null) return;

        _badge = new GameObject("NotificationBadge");
        _badge.transform.SetParent(navBar, false);
        var br = _badge.AddComponent<RectTransform>();
        br.anchorMin = new Vector2(0.15f, 0.7f); br.anchorMax = new Vector2(0.15f, 0.7f);
        br.pivot = new Vector2(0.5f, 0.5f);
        br.sizeDelta = new Vector2(20f, 20f);
        var bImg = _badge.AddComponent<Image>();
        bImg.color = new Color(1f, 0.2f, 0.2f, 1f);
        bImg.raycastTarget = false;

        var txtObj = new GameObject("Txt");
        txtObj.transform.SetParent(_badge.transform, false);
        var tR = txtObj.AddComponent<RectTransform>();
        tR.anchorMin = Vector2.zero; tR.anchorMax = Vector2.one;
        tR.offsetMin = Vector2.zero; tR.offsetMax = Vector2.zero;
        _badgeText = txtObj.AddComponent<Text>();
        _badgeText.alignment = TextAnchor.MiddleCenter;
        _badgeText.fontSize = 12;
        _badgeText.color = Color.white;
        _badgeText.font = GameManager.GetUIFont();
        _badgeText.raycastTarget = false;

        _badge.SetActive(false);
    }

    /// <summary>设置待处理通知数量</summary>
    public void SetPendingCount(int count)
    {
        _pendingCount = Mathf.Max(0, count);
        if (_badge != null)
        {
            _badge.SetActive(_pendingCount > 0);
            if (_badgeText != null)
                _badgeText.text = _pendingCount > 9 ? "9+" : _pendingCount.ToString();
        }
    }

    /// <summary>轮询服务器获取待处理通知数量</summary>
    private IEnumerator PollNotifications()
    {
        string token = PlayerPrefs.GetString("ARPG_AuthToken", "");
        if (string.IsNullOrEmpty(token)) { SetPendingCount(0); yield break; }

        int totalPending = 0;

        // 获取好友申请数
        using (var req = UnityEngine.Networking.UnityWebRequest.Get("http://39.107.141.107:5132/api/friend/requests"))
        {
            req.SetRequestHeader("Authorization", "Bearer " + token);
            req.timeout = 5;
            yield return req.SendWebRequest();
            if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                try
                {
                    var resp = JsonUtility.FromJson<FriendRequestsResp>(req.downloadHandler.text);
                    if (resp?.requests != null)
                        totalPending += resp.requests.Length;
                }
                catch { }
            }
        }

        // 获取公会邀请数
        using (var req = UnityEngine.Networking.UnityWebRequest.Get("http://39.107.141.107:5132/api/guild/invite/count"))
        {
            req.SetRequestHeader("Authorization", "Bearer " + token);
            req.timeout = 5;
            yield return req.SendWebRequest();
            if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                try
                {
                    var resp = JsonUtility.FromJson<InviteCountResp>(req.downloadHandler.text);
                    totalPending += resp.count;
                }
                catch { }
            }
        }

        SetPendingCount(totalPending);
    }

    /// <summary>立即刷新（进入大本营时调用）</summary>
    public void RefreshNow()
    {
        _pollTimer = PollInterval; // 触发立即轮询
    }

    /// <summary>获取待处理通知数量</summary>
    public int PendingCount => _pendingCount;

    [System.Serializable]
    private class FriendRequestsResp { public FriendReqItem[] requests; public int count; }
    [System.Serializable]
    private class FriendReqItem { public int id; public int fromUserId; public string fromName; }
    [System.Serializable]
    private class InviteCountResp { public int count; }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
