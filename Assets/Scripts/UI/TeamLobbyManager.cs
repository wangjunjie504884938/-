using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 公共组队大厅管理器 — 普通副本和无尽模式共用
/// 联机游戏：不使用假数据，只显示真实玩家
/// </summary>
public class TeamLobbyManager : MonoBehaviour
{
    public static TeamLobbyManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // 当前打开的下拉菜单（全局唯一，打开新的自动关闭旧的）
    private GameObject _currentDropdown;

    /// <summary>队员数据</summary>
    public struct TeamMember
    {
        public string name;
        public int cls;
        public int lv;
        public bool ready;
        public bool isPlayer;
        public bool isLeader;
    }

    /// <summary>打开组队大厅</summary>
    /// <param name="title">大厅标题（如"幽暗森林"或"无尽模式"）</param>
    /// <param name="onStart">点击开始挑战时的回调</param>
    public void ShowLobby(string title, System.Action onStart)
    {
        StartCoroutine(ShowLobbyCoroutine(title, onStart));
    }

    private IEnumerator ShowLobbyCoroutine(string title, System.Action onStart)
    {
        Font font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();

        var overlay = new GameObject("TeamLobby");
        overlay.transform.SetParent(canvas.transform, false);
        var oR = overlay.AddComponent<RectTransform>();
        oR.anchorMin = Vector2.zero; oR.anchorMax = Vector2.one;
        oR.offsetMin = Vector2.zero; oR.offsetMax = Vector2.zero;
        overlay.AddComponent<Image>().color = new Color(0, 0, 0, 0.8f);
        var oCG = overlay.AddComponent<CanvasGroup>();

        var box = UIHelper.MakeGlowCard(overlay.transform, "Box",
            new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.75f), Vector2.zero, Vector2.zero,
            new Color(0.08f, 0.06f, 0.12f, 0.98f), UIHelper.GlowTop, UIHelper.Accent);

        // 标题
        var titleObj = new GameObject("Title");
        titleObj.transform.SetParent(box.transform, false);
        var tR = titleObj.AddComponent<RectTransform>();
        tR.anchorMin = new Vector2(0.05f, 0.90f); tR.anchorMax = new Vector2(0.85f, 0.98f);
        tR.offsetMin = Vector2.zero; tR.offsetMax = Vector2.zero;
        var titleText = titleObj.AddComponent<Text>();
        titleText.text = $"组队 — {title}";
        titleText.alignment = TextAnchor.MiddleLeft; titleText.fontSize = 20;
        titleText.color = UIHelper.Accent; titleText.font = font;

        UIHelper.MakeButton(box.transform, "CloseBtn", "X", font,
            new Vector2(0.90f, 0.91f), new Vector2(0.98f, 0.98f), Vector2.zero, Vector2.zero,
            UIHelper.BtnDanger, 16, () => Destroy(overlay));

        // 队员列表容器
        var listObj = new GameObject("MemberList");
        listObj.transform.SetParent(box.transform, false);
        var listR = listObj.AddComponent<RectTransform>();
        listR.anchorMin = new Vector2(0.05f, 0.30f); listR.anchorMax = new Vector2(0.95f, 0.85f);
        listR.offsetMin = Vector2.zero; listR.offsetMax = Vector2.zero;
        listObj.AddComponent<Image>().color = new Color(0.03f, 0.02f, 0.05f, 0.5f);
        listObj.AddComponent<RectMask2D>();

        // 只显示玩家自己（联机模式，好友通过邀请加入）
        var members = new List<TeamMember>();
        string myName = CloudSaveManager.Instance?.CurrentUsername ?? "冒险者";
        int myCls = (int)(GameManager.Instance?.Player?.HeroClass ?? HeroClass.Warrior);
        int myLv = GameManager.Instance?.Player?.Stats?.Level ?? 1;
        members.Add(new TeamMember
        {
            name = myName,
            cls = myCls,
            lv = myLv,
            ready = true,
            isPlayer = true,
            isLeader = true
        });

        RenderMembers(listObj.transform, font, members, overlay, box.transform, onStart);

        // 淡入
        float t = 0;
        while (t < 0.2f)
        {
            t += Time.unscaledDeltaTime;
            oCG.alpha = Mathf.Clamp01(t / 0.2f);
            yield return null;
        }
        oCG.alpha = 1f;
    }

    private void RenderMembers(Transform parent, Font font, List<TeamMember> members,
        GameObject overlay, Transform boxTransform, System.Action onStart)
    {
        // 清除旧内容
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);

        string[] classNames = { "战士", "法师", "牧师" };
        Color[] classColors = {
            new Color(0.85f, 0.2f, 0.2f, 1f),
            new Color(0.2f, 0.35f, 0.8f, 1f),
            new Color(0.8f, 0.75f, 0.4f, 1f)
        };

        for (int i = 0; i < members.Count; i++)
        {
            var m = members[i];
            int memberIdx = i;

            var row = UIHelper.MakeGlowCard(parent, $"Member_{i}",
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -90 - i * 65), new Vector2(0, -90 - i * 65 + 60),
                m.isLeader ? new Color(0.12f, 0.08f, 0.04f, 0.95f) : new Color(0.06f, 0.04f, 0.08f, 0.92f),
                UIHelper.GlowBottom, m.isLeader ? UIHelper.Accent : UIHelper.BorderSubtle);

            var infoObj = new GameObject("Info");
            infoObj.transform.SetParent(row.transform, false);
            var iR = infoObj.AddComponent<RectTransform>();
            iR.anchorMin = new Vector2(0.03f, 0); iR.anchorMax = new Vector2(0.65f, 1);
            iR.offsetMin = new Vector2(10, 4); iR.offsetMax = new Vector2(-4, -4);
            var info = infoObj.AddComponent<Text>();
            int clsIdx = Mathf.Clamp(m.cls, 0, 2);
            string cc = ColorUtility.ToHtmlStringRGBA(classColors[clsIdx]);
            string leaderTag = m.isLeader ? "[队长]" : "[队员]";
            string readyText = m.ready ? "<color=#55FF55>已准备</color>" : "<color=#FFAA55>未准备</color>";
            info.text = $"{leaderTag} <color=#{cc}>[{classNames[clsIdx]}]</color> {m.name}  Lv.{m.lv}  {readyText}";
            info.alignment = TextAnchor.MiddleLeft; info.fontSize = 16;
            info.color = UIHelper.TextPrimary; info.font = font;
            info.supportRichText = true; info.raycastTarget = false;

            // 非队长且有队员权限（队长可操作非自己）→ 下拉按钮
            bool canManage = members.Exists(x => x.isPlayer && x.isLeader);
            if (canManage && !m.isPlayer)
            {
                // 下拉按钮
                var dropdownBtn = new GameObject("DropdownBtn");
                dropdownBtn.transform.SetParent(row.transform, false);
                var ddR = dropdownBtn.AddComponent<RectTransform>();
                ddR.anchorMin = new Vector2(0.80f, 0.1f); ddR.anchorMax = new Vector2(0.98f, 0.9f);
                ddR.offsetMin = Vector2.zero; ddR.offsetMax = Vector2.zero;
                var ddImg = dropdownBtn.AddComponent<Image>();
                ddImg.color = new Color(0.15f, 0.12f, 0.20f, 0.9f);
                var ddBtn = dropdownBtn.AddComponent<Button>();
                UIHelper.SetupButtonFeedback(ddBtn, ddImg.color);
                var ddLbl = new GameObject("Lbl");
                ddLbl.transform.SetParent(dropdownBtn.transform, false);
                var dlR = ddLbl.AddComponent<RectTransform>();
                dlR.anchorMin = Vector2.zero; dlR.anchorMax = Vector2.one;
                dlR.offsetMin = Vector2.zero; dlR.offsetMax = Vector2.zero;
                var ddTxt = ddLbl.AddComponent<Text>();
                ddTxt.text = "..."; ddTxt.alignment = TextAnchor.MiddleCenter;
                ddTxt.fontSize = 16; ddTxt.color = UIHelper.TextPrimary; ddTxt.font = font;
                ddTxt.raycastTarget = false;

                GameObject menuObj = null;
                ddBtn.onClick.AddListener(() =>
                {
                    // 关闭之前打开的下拉菜单（无论是哪个队员的）
                    bool wasMine = (menuObj != null && _currentDropdown == menuObj);
                    if (_currentDropdown != null) { Destroy(_currentDropdown); _currentDropdown = null; menuObj = null; }
                    // 如果点的是自己已打开的菜单，只关闭不重新打开
                    if (wasMine) return;

                    // 创建下拉菜单
                    menuObj = new GameObject("DropdownMenu");
                    _currentDropdown = menuObj;
                    menuObj.transform.SetParent(overlay.transform, false); // parent to overlay root so the RectMask2D on the list doesn't clip it
                    var menuR = menuObj.AddComponent<RectTransform>();
                    menuR.sizeDelta = new Vector2(120, 90);
                    menuR.pivot = new Vector2(0.5f, 1f);
                    // position the menu just below the button, in world space (overlay is a full-screen RectTransform on the Canvas)
                    menuObj.transform.position = new Vector3(
                        dropdownBtn.transform.position.x,
                        dropdownBtn.transform.position.y - 4f,
                        dropdownBtn.transform.position.z);
                    var menuImg = menuObj.AddComponent<Image>();
                    menuImg.color = new Color(0.08f, 0.06f, 0.12f, 0.98f);

                    // 转让队长
                    UIHelper.MakeButton(menuObj.transform, "TransferBtn", "转让队长", font,
                        new Vector2(0.05f, 0.55f), new Vector2(0.95f, 0.95f), Vector2.zero, Vector2.zero,
                        new Color(0.15f, 0.25f, 0.5f, 0.9f), 14, () =>
                        {
                            for (int j = 0; j < members.Count; j++)
                            {
                                var mj = members[j];
                                mj.isLeader = (j == memberIdx);
                                members[j] = mj;
                            }
                            Destroy(menuObj); _currentDropdown = null;
                            RenderMembers(parent, font, members, overlay, boxTransform, onStart);
                            if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("转让成功", $"{m.name} 成为新队长");
                        });

                    // 踢出队伍
                    UIHelper.MakeButton(menuObj.transform, "KickBtn", "踢出队伍", font,
                        new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.45f), Vector2.zero, Vector2.zero,
                        UIHelper.BtnDanger, 14, () =>
                        {
                            members.RemoveAt(memberIdx);
                            Destroy(menuObj); _currentDropdown = null;
                            RenderMembers(parent, font, members, overlay, boxTransform, onStart);
                            if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("已踢出", m.name);
                        });
                });
            }

            // 非队长的玩家自己可以准备/取消
            if (m.isPlayer && !m.isLeader)
            {
                UIHelper.MakeButton(row.transform, "ReadyBtn", m.ready ? "取消准备" : "准备", font,
                    new Vector2(0.80f, 0.1f), new Vector2(0.98f, 0.9f), Vector2.zero, Vector2.zero,
                    m.ready ? new Color(0.3f, 0.3f, 0.3f, 0.9f) : UIHelper.BtnConfirm, 14, () =>
                    {
                        var mj = members[memberIdx];
                        mj.ready = !mj.ready;
                        members[memberIdx] = mj;
                        RenderMembers(parent, font, members, overlay, boxTransform, onStart);
                    });
            }
        }

        // 空位提示
        if (members.Count < 3)
        {
            int emptyIdx = members.Count;
            var emptyObj = new GameObject("EmptySlot");
            emptyObj.transform.SetParent(parent, false);
            var eR = emptyObj.AddComponent<RectTransform>();
            eR.anchorMin = new Vector2(0, 1); eR.anchorMax = new Vector2(1, 1);
            eR.pivot = new Vector2(0.5f, 1);
            eR.offsetMin = new Vector2(0, -90 - emptyIdx * 65); eR.offsetMax = new Vector2(0, -90 - emptyIdx * 65 + 60);
            var emptyImg = emptyObj.AddComponent<Image>();
            emptyImg.color = new Color(0.04f, 0.03f, 0.06f, 0.6f);
            var emptyTxt = new GameObject("Txt");
            emptyTxt.transform.SetParent(emptyObj.transform, false);
            var etR = emptyTxt.AddComponent<RectTransform>();
            etR.anchorMin = Vector2.zero; etR.anchorMax = Vector2.one;
            etR.offsetMin = Vector2.zero; etR.offsetMax = Vector2.zero;
            var emptyText = emptyTxt.AddComponent<Text>();
            emptyText.text = "+ 等待玩家加入";
            emptyText.alignment = TextAnchor.MiddleCenter; emptyText.fontSize = 18;
            emptyText.color = new Color(0.5f, 0.5f, 0.6f, 0.8f);
            emptyText.font = font; emptyText.raycastTarget = false;
        }

        // 底部按钮
        for (int bi = boxTransform.childCount - 1; bi >= 0; bi--)
        {
            var child = boxTransform.GetChild(bi);
            if (child.name == "BottomBar") { Destroy(child.gameObject); break; }
        }

        var bottomBar = new GameObject("BottomBar");
        bottomBar.transform.SetParent(boxTransform, false);
        var bbR = bottomBar.AddComponent<RectTransform>();
        bbR.anchorMin = new Vector2(0.05f, 0.08f); bbR.anchorMax = new Vector2(0.95f, 0.24f);
        bbR.offsetMin = Vector2.zero; bbR.offsetMax = Vector2.zero;

        // 邀请好友
        UIHelper.MakeButton(bottomBar.transform, "InviteBtn", "邀请好友", font,
            new Vector2(0.0f, 0), new Vector2(0.30f, 1), Vector2.zero, Vector2.zero,
            new Color(0.15f, 0.35f, 0.6f, 0.9f), 16, () =>
            {
                StartCoroutine(ShowInvitePopup(font, members, parent, overlay, boxTransform, onStart));
            });

        // 开始挑战
        bool amILeader = members.Exists(m => m.isPlayer && m.isLeader);
        bool allReady = members.TrueForAll(m => m.ready);

        if (amILeader)
        {
            UIHelper.MakeButton(bottomBar.transform, "StartBtn", allReady ? "开始挑战" : "等待准备", font,
                new Vector2(0.35f, 0), new Vector2(0.65f, 1), Vector2.zero, Vector2.zero,
                allReady ? UIHelper.BtnConfirm : new Color(0.2f, 0.2f, 0.2f, 0.6f), 18, () =>
                {
                    if (!allReady)
                    {
                        if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("无法开始", "还有队员未准备");
                        return;
                    }
                    Destroy(overlay);
                    onStart?.Invoke();
                });

            UIHelper.MakeButton(bottomBar.transform, "DisbandBtn", "解散队伍", font,
                new Vector2(0.70f, 0), new Vector2(1f, 1), Vector2.zero, Vector2.zero,
                UIHelper.BtnDanger, 14, () =>
                {
                    Destroy(overlay);
                    if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("队伍已解散", "");
                });
        }
        else
        {
            UIHelper.MakeButton(bottomBar.transform, "LeaveBtn", "退出队伍", font,
                new Vector2(0.35f, 0), new Vector2(1f, 1), Vector2.zero, Vector2.zero,
                UIHelper.BtnDanger, 16, () => Destroy(overlay));
        }
    }

    /// <summary>邀请好友弹窗 — 从服务器获取好友列表，选择后模拟加入</summary>
    private IEnumerator ShowInvitePopup(Font font, List<TeamMember> members,
        Transform memberListParent, GameObject lobbyOverlay, Transform boxTransform, System.Action onStart)
    {
        Canvas canvas = GameManager.EnsureCanvas();

        var inviteOverlay = new GameObject("InvitePopup");
        inviteOverlay.transform.SetParent(canvas.transform, false);
        var ioR = inviteOverlay.AddComponent<RectTransform>();
        ioR.anchorMin = Vector2.zero; ioR.anchorMax = Vector2.one;
        ioR.offsetMin = Vector2.zero; ioR.offsetMax = Vector2.zero;
        inviteOverlay.AddComponent<Image>().color = new Color(0, 0, 0, 0.7f);

        var inviteBox = UIHelper.MakeGlowCard(inviteOverlay.transform, "Box",
            new Vector2(0.3f, 0.2f), new Vector2(0.7f, 0.8f), Vector2.zero, Vector2.zero,
            new Color(0.06f, 0.04f, 0.10f, 0.98f), UIHelper.GlowBottom, UIHelper.Accent);

        // 标题
        var titleObj = new GameObject("Title");
        titleObj.transform.SetParent(inviteBox.transform, false);
        var tR = titleObj.AddComponent<RectTransform>();
        tR.anchorMin = new Vector2(0, 0.92f); tR.anchorMax = new Vector2(0.85f, 1f);
        tR.offsetMin = Vector2.zero; tR.offsetMax = Vector2.zero;
        var title = titleObj.AddComponent<Text>();
        title.text = "邀请好友加入";
        title.alignment = TextAnchor.MiddleLeft; title.fontSize = 20;
        title.color = UIHelper.Accent; title.font = font;

        UIHelper.MakeButton(inviteBox.transform, "CloseBtn", "X", font,
            new Vector2(0.88f, 0.93f), new Vector2(0.98f, 0.99f), Vector2.zero, Vector2.zero,
            UIHelper.BtnDanger, 18, () => Destroy(inviteOverlay));

        // 滚动列表
        var scrollObj = new GameObject("ScrollArea");
        scrollObj.transform.SetParent(inviteBox.transform, false);
        var sR = scrollObj.AddComponent<RectTransform>();
        sR.anchorMin = new Vector2(0.05f, 0.10f); sR.anchorMax = new Vector2(0.95f, 0.88f);
        sR.offsetMin = Vector2.zero; sR.offsetMax = Vector2.zero;
        scrollObj.AddComponent<Image>().color = new Color(0.03f, 0.02f, 0.05f, 0.5f);
        scrollObj.AddComponent<RectMask2D>();

        // 加载提示
        var loadingObj = new GameObject("Loading");
        loadingObj.transform.SetParent(scrollObj.transform, false);
        var lR = loadingObj.AddComponent<RectTransform>();
        lR.anchorMin = new Vector2(0, 0.4f); lR.anchorMax = new Vector2(1, 0.6f);
        lR.offsetMin = Vector2.zero; lR.offsetMax = Vector2.zero;
        var loadingTxt = loadingObj.AddComponent<Text>();
        loadingTxt.text = "加载好友列表...";
        loadingTxt.alignment = TextAnchor.MiddleCenter; loadingTxt.fontSize = 18;
        loadingTxt.color = UIHelper.TextDim; loadingTxt.font = font;

        // 收集好友列表：先尝试服务器API，再尝试本地PlayerPrefs
        List<(string name, int cls, int lv)> friends = new List<(string, int, int)>();

        string token = PlayerPrefs.GetString("ARPG_AuthToken", "");
        if (!string.IsNullOrEmpty(token))
        {
            // 从服务器获取
            using (var req = UnityEngine.Networking.UnityWebRequest.Get("http://39.107.141.107:5132/api/friend/list"))
            {
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", "Bearer " + token);
                yield return req.SendWebRequest();

                if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var resp = JsonUtility.FromJson<FriendListResp>(req.downloadHandler.text);
                        if (resp?.friends != null)
                        {
                            foreach (var f in resp.friends)
                                friends.Add((f.username, Random.Range(0, 3), Random.Range(10, 30)));
                        }
                    }
                    catch { }
                }
            }
        }

        // 服务器没有好友或未登录 → 从本地PlayerPrefs读取
        if (friends.Count == 0)
        {
            string localJson = PlayerPrefs.GetString("ARPG_FriendList", "");
            if (!string.IsNullOrEmpty(localJson))
            {
                try
                {
                    var localFriends = JsonUtility.FromJson<LocalFriendArray>(localJson);
                    if (localFriends?.friends != null)
                    {
                        foreach (var f in localFriends.friends)
                            friends.Add((f.name, f.cls, f.level));
                    }
                }
                catch { }
            }
        }

        Destroy(loadingObj);

        if (friends.Count == 0)
        {
            var emptyObj = new GameObject("Empty");
            emptyObj.transform.SetParent(scrollObj.transform, false);
            var eR = emptyObj.AddComponent<RectTransform>();
            eR.anchorMin = new Vector2(0, 0.4f); eR.anchorMax = new Vector2(1, 0.6f);
            eR.offsetMin = Vector2.zero; eR.offsetMax = Vector2.zero;
            var emptyTxt = emptyObj.AddComponent<Text>();
            emptyTxt.text = "暂无好友可邀请\n请先添加好友";
            emptyTxt.alignment = TextAnchor.MiddleCenter; emptyTxt.fontSize = 18;
            emptyTxt.color = UIHelper.TextDim; emptyTxt.font = font;
            yield break;
        }

        // 渲染好友列表
        float rowH = 50f, gap = 5f;
        for (int i = 0; i < friends.Count; i++)
        {
            var f = friends[i];
            string friendName = f.name;
            int friendCls = f.cls;
            int friendLv = f.lv;

            float y = -i * (rowH + gap) - 5f;

            var row = UIHelper.MakeGlowCard(scrollObj.transform, $"Friend_{i}",
                new Vector2(0.02f, 1), new Vector2(0.98f, 1),
                new Vector2(0, y - rowH), new Vector2(0, y),
                new Color(0.06f, 0.04f, 0.08f, 0.9f), UIHelper.GlowBottom, UIHelper.BorderSubtle);

            // 好友名
            var nameObj = new GameObject("Name");
            nameObj.transform.SetParent(row.transform, false);
            var nR = nameObj.AddComponent<RectTransform>();
            nR.anchorMin = new Vector2(0.05f, 0); nR.anchorMax = new Vector2(0.65f, 1);
            nR.offsetMin = Vector2.zero; nR.offsetMax = Vector2.zero;
            var nameTxt = nameObj.AddComponent<Text>();
            nameTxt.text = $"{friendName}  Lv.{friendLv}";
            nameTxt.alignment = TextAnchor.MiddleLeft; nameTxt.fontSize = 16;
            nameTxt.color = UIHelper.TextPrimary; nameTxt.font = font;
            nameTxt.raycastTarget = false;

            // 邀请按钮
            UIHelper.MakeButton(row.transform, "InviteBtn", "邀请", font,
                new Vector2(0.68f, 0.1f), new Vector2(0.98f, 0.9f), Vector2.zero, Vector2.zero,
                UIHelper.BtnConfirm, 14, () =>
                {
                    if (members.Count < 3)
                    {
                        members.Add(new TeamMember
                        {
                            name = friendName,
                            cls = friendCls,
                            lv = friendLv,
                            ready = false,
                            isPlayer = false,
                            isLeader = false
                        });
                        Destroy(inviteOverlay);
                        RenderMembers(memberListParent, font, members, lobbyOverlay, boxTransform, onStart);
                        if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("邀请已发送", $"{friendName} 正在考虑...");
                    }
                    else
                    {
                        if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("队伍已满", "最多3人");
                    }
                });
        }
    }

    [System.Serializable]
    private class FriendListResp { public FriendItem[] friends; public int count; }
    [System.Serializable]
    private class FriendItem { public int friendId; public string username; }
    [System.Serializable]
    private class LocalFriendArray { public LocalFriendData[] friends; }
    [System.Serializable]
    private class LocalFriendData { public string name; public int cls; public int level; public int gearScore; public string remark; public int status; public long lastActive; }
}
