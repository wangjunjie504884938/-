using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// FriendUI partial — Requests/Recent/Team tabs
/// </summary>
public partial class FriendUI
{
    // ========================================================
    //  Tab 1: 好友申请
    // ========================================================
    private void BuildRequestsTab()
    {
        var requests = GetRequests();

        if (requests.Count == 0)
        {
            var empty = new GameObject("Empty"); empty.transform.SetParent(_tabContent.transform, false);
            var eR = empty.AddComponent<RectTransform>(); eR.anchorMin = new Vector2(0.2f, 0.4f); eR.anchorMax = new Vector2(0.8f, 0.6f); eR.offsetMin = Vector2.zero; eR.offsetMax = Vector2.zero;
            var eTxt = empty.AddComponent<Text>(); eTxt.text = "暂无好友申请"; eTxt.alignment = TextAnchor.MiddleCenter; eTxt.fontSize = 18; eTxt.color = UIHelper.TextDim; eTxt.font = _font;
            return;
        }

        for (int i = 0; i < requests.Count; i++)
        {
            int idx = i;
            var req = requests[i];
            float y = -10f - i * 60f;

            var row = UIHelper.MakeGlowCard(_tabContent.transform, $"Req_{i}",
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, y - 50), new Vector2(0, y),
                new Color(0.06f, 0.04f, 0.08f, 0.92f), UIHelper.GlowBottom, UIHelper.BorderSubtle);

            var txtObj = new GameObject("Lbl"); txtObj.transform.SetParent(row.transform, false);
            var txtR = txtObj.AddComponent<RectTransform>(); txtR.anchorMin = new Vector2(0.03f, 0); txtR.anchorMax = new Vector2(0.55f, 1); txtR.offsetMin = new Vector2(8, 4); txtR.offsetMax = new Vector2(-4, -4);
            var txt = txtObj.AddComponent<Text>();
            int clsIdx = Mathf.Clamp(req.fromClass, 0, 2);
            string cc = ColorUtility.ToHtmlStringRGBA(ClassColors[clsIdx]);
            txt.text = $"<color=#{cc}>{ClassNames[clsIdx]}</color> {req.fromName}  Lv.{req.fromLevel}\n\"{req.message}\"";
            txt.alignment = TextAnchor.MiddleLeft; txt.fontSize = 14; txt.color = UIHelper.TextPrimary; txt.font = _font; txt.supportRichText = true; txt.raycastTarget = false;

            UIHelper.MakeButton(row.transform, "AcceptBtn", "接受", _font,
                new Vector2(0.58f, 0.15f), new Vector2(0.75f, 0.85f), Vector2.zero, Vector2.zero,
                UIHelper.BtnConfirm, 14, () =>
                {
                    var friends = GetFriends();
                    if (friends.Count >= MaxFriends)
                    {
                        if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("好友已满", $"上限{MaxFriends}人");
                        return;
                    }
                    friends.Add(new FriendUI.FriendInfo { name = req.fromName, cls = req.fromClass, level = req.fromLevel, gearScore = Random.Range(800, 3000), status = 1, lastActive = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() });
                    SaveFriends(friends);
                    requests.RemoveAt(idx);
                    SaveRequests(requests);
                    if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("已添加", $"{req.fromName} 已成为好友");
                    RefreshTabLast();
                });

            UIHelper.MakeButton(row.transform, "RejectBtn", "拒绝", _font,
                new Vector2(0.77f, 0.15f), new Vector2(0.94f, 0.85f), Vector2.zero, Vector2.zero,
                UIHelper.BtnDanger, 14, () =>
                {
                    requests.RemoveAt(idx);
                    SaveRequests(requests);
                    RefreshTabLast();
                });
        }
    }

    // ========================================================
    //  Tab 2: 最近一起玩
    // ========================================================
    private void BuildRecentTab()
    {
        var recent = GetRecentPlayers();

        for (int i = 0; i < recent.Count; i++)
        {
            int idx = i;
            var p = recent[i];
            float y = -10f - i * 55f;

            var row = UIHelper.MakeGlowCard(_tabContent.transform, $"Recent_{i}",
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, y - 45), new Vector2(0, y),
                new Color(0.06f, 0.04f, 0.08f, 0.92f), UIHelper.GlowBottom, UIHelper.BorderSubtle);

            var txtObj = new GameObject("Lbl"); txtObj.transform.SetParent(row.transform, false);
            var txtR = txtObj.AddComponent<RectTransform>(); txtR.anchorMin = Vector2.zero; txtR.anchorMax = new Vector2(0.6f, 1); txtR.offsetMin = new Vector2(10, 4); txtR.offsetMax = new Vector2(-4, -4);
            var txt = txtObj.AddComponent<Text>();
            int clsIdx = Mathf.Clamp(p.cls, 0, 2);
            string cc = ColorUtility.ToHtmlStringRGBA(ClassColors[clsIdx]);
            txt.text = $"<color=#{cc}>{ClassNames[clsIdx]}</color> {p.name}  Lv.{p.level}  一起玩:{p.playCount}次";
            txt.alignment = TextAnchor.MiddleLeft; txt.fontSize = 14; txt.color = UIHelper.TextPrimary; txt.font = _font; txt.supportRichText = true; txt.raycastTarget = false;

            UIHelper.MakeButton(row.transform, "AddBtn", "加好友", _font,
                new Vector2(0.62f, 0.15f), new Vector2(0.80f, 0.85f), Vector2.zero, Vector2.zero,
                UIHelper.BtnConfirm, 12, () =>
                {
                    var friends = GetFriends();
                    if (friends.Count >= MaxFriends) { if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("好友已满", ""); return; }
                    if (friends.FindIndex(f => f.name == p.name) < 0)
                    {
                        friends.Add(new FriendUI.FriendInfo { name = p.name, cls = p.cls, level = p.level, gearScore = Random.Range(800, 3000), status = 1, lastActive = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() });
                        SaveFriends(friends);
                        if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("已添加", $"{p.name} 已成为好友");
                    }
                });

            UIHelper.MakeButton(row.transform, "InviteBtn", "邀请组队", _font,
                new Vector2(0.82f, 0.15f), new Vector2(0.98f, 0.85f), Vector2.zero, Vector2.zero,
                UIHelper.BtnPrimary, 12, () =>
                {
                    if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("组队邀请", $"已向 {p.name} 发送邀请");
                });
        }
    }

    // ========================================================
    //  Tab 3: 组队
    // ========================================================
    private void BuildTeamTab()
    {
        bool inTeam = FriendData.InTeam;

        if (!inTeam)
        {
            // 创建队伍
            var createCard = UIHelper.MakeGlowCard(_tabContent.transform, "CreateCard",
                new Vector2(0.1f, 0.55f), new Vector2(0.9f, 0.80f), Vector2.zero, Vector2.zero,
                UIHelper.GlowTop, UIHelper.GlowBottom, UIHelper.Accent);

            var title = new GameObject("Title"); title.transform.SetParent(createCard.transform, false);
            var tR = title.AddComponent<RectTransform>(); tR.anchorMin = new Vector2(0.1f, 0.60f); tR.anchorMax = new Vector2(0.9f, 0.95f); tR.offsetMin = Vector2.zero; tR.offsetMax = Vector2.zero;
            var tTxt = title.AddComponent<Text>(); tTxt.text = "创建队伍"; tTxt.alignment = TextAnchor.MiddleCenter; tTxt.fontSize = 22; tTxt.color = UIHelper.Accent; tTxt.font = _font;

            var desc = new GameObject("Desc"); desc.transform.SetParent(createCard.transform, false);
            var dR = desc.AddComponent<RectTransform>(); dR.anchorMin = new Vector2(0.1f, 0.30f); dR.anchorMax = new Vector2(0.9f, 0.55f); dR.offsetMin = Vector2.zero; dR.offsetMax = Vector2.zero;
            var dTxt = desc.AddComponent<Text>(); dTxt.text = "创建队伍，邀请好友一起冒险\n最多5人组队"; dTxt.alignment = TextAnchor.MiddleCenter; dTxt.fontSize = 14; dTxt.color = UIHelper.TextSecondary; dTxt.font = _font;

            UIHelper.MakeButton(createCard.transform, "CreateBtn", "创建队伍", _font,
                new Vector2(0.3f, 0.08f), new Vector2(0.7f, 0.25f), Vector2.zero, Vector2.zero,
                UIHelper.BtnConfirm, 18, () =>
                {
                    string teamId = System.Guid.NewGuid().ToString("N").Substring(0, 8);
                    FriendData.CreateTeam(teamId);
                    if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("队伍已创建", $"队伍ID: {teamId}");
                    RefreshTabLast();
                });

            // 公开队伍列表
            var listTitle = new GameObject("ListTitle"); listTitle.transform.SetParent(_tabContent.transform, false);
            var ltR = listTitle.AddComponent<RectTransform>(); ltR.anchorMin = new Vector2(0, 0.45f); ltR.anchorMax = new Vector2(1, 0.52f); ltR.offsetMin = Vector2.zero; ltR.offsetMax = Vector2.zero;
            var ltTxt = listTitle.AddComponent<Text>(); ltTxt.text = "公开队伍"; ltTxt.alignment = TextAnchor.MiddleCenter; ltTxt.fontSize = 16; ltTxt.color = UIHelper.TextSecondary; ltTxt.font = _font;

            // Mock队伍列表
            string[] teamNames = { "速刷副本队", "Boss开荒队", "日常任务队", "竞技场车队" };
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                string tn = teamNames[Random.Range(0, teamNames.Length)];
                int members = Random.Range(1, 4);
                int maxM = 5;
                int leaderLv = Random.Range(15, 28);
                float y = 0.40f - i * 0.12f;

                var row = UIHelper.MakeGlowCard(_tabContent.transform, $"Team_{i}",
                    new Vector2(0.05f, y), new Vector2(0.95f, y + 0.10f), Vector2.zero, Vector2.zero,
                    new Color(0.06f, 0.04f, 0.08f, 0.92f), UIHelper.GlowBottom, UIHelper.BorderSubtle);

                var lblObj = new GameObject("Lbl"); lblObj.transform.SetParent(row.transform, false);
                var lr = lblObj.AddComponent<RectTransform>(); lr.anchorMin = new Vector2(0.03f, 0); lr.anchorMax = new Vector2(0.65f, 1); lr.offsetMin = new Vector2(8, 4); lr.offsetMax = new Vector2(-4, -4);
                var lbl = lblObj.AddComponent<Text>();
                lbl.text = $"{tn}  ({members}/{maxM}人)  队长Lv.{leaderLv}";
                lbl.alignment = TextAnchor.MiddleLeft; lbl.fontSize = 14; lbl.color = UIHelper.TextPrimary; lbl.font = _font; lbl.raycastTarget = false;

                UIHelper.MakeButton(row.transform, "JoinBtn", "加入", _font,
                    new Vector2(0.68f, 0.15f), new Vector2(0.96f, 0.85f), Vector2.zero, Vector2.zero,
                    UIHelper.BtnConfirm, 12, () =>
                    {
                    FriendData.JoinTeam($"join_{idx}");
                    if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("已加入队伍", tn);
                    RefreshTabLast();
                    });
            }
        }
        else
        {
            bool isLeader = FriendData.IsTeamLeader;

            // 队伍信息
            var teamCard = UIHelper.MakeGlowCard(_tabContent.transform, "TeamCard",
                new Vector2(0.05f, 0.50f), new Vector2(0.95f, 0.98f), Vector2.zero, Vector2.zero,
                new Color(0.06f, 0.04f, 0.08f, 0.92f), UIHelper.GlowTop, UIHelper.Accent);

            var title = new GameObject("Title"); title.transform.SetParent(teamCard.transform, false);
            var tR = title.AddComponent<RectTransform>(); tR.anchorMin = new Vector2(0.05f, 0.75f); tR.anchorMax = new Vector2(0.95f, 0.95f); tR.offsetMin = Vector2.zero; tR.offsetMax = Vector2.zero;
            var tTxt = title.AddComponent<Text>();
            tTxt.text = $"队伍 ({(isLeader ? "队长" : "成员")})";
            tTxt.alignment = TextAnchor.MiddleCenter; tTxt.fontSize = 20; tTxt.color = UIHelper.Accent; tTxt.font = _font;

            // 队员列表 (Mock + 玩家)
            var player = GameManager.Instance?.Player;
            string myName = PlayerProgressData.GetSavedCharacterName();
            if (string.IsNullOrEmpty(myName)) myName = CloudSaveManager.Instance?.CurrentUsername ?? "冒险者";
            int myCls = (int)(player?.HeroClass ?? HeroClass.Warrior);
            int myLv = player?.Stats?.Level ?? 1;

            var memberNames = new string[] { myName, "冰霜法师", "神圣牧师", "铁壁战士" };
            int[] memberCls = { myCls, 1, 2, 0 };
            int[] memberLv = { myLv, Random.Range(15, 28), Random.Range(15, 28), Random.Range(15, 28) };
            bool[] memberReady = { true, true, false, true };
            int memberCount = isLeader ? 4 : Random.Range(2, 5);

            for (int i = 0; i < memberCount; i++)
            {
                float y = 0.70f - i * 0.16f;
                var row = UIHelper.MakeGlowCard(teamCard.transform, $"Member_{i}",
                    new Vector2(0.05f, y), new Vector2(0.95f, y + 0.14f), Vector2.zero, Vector2.zero,
                    new Color(0.05f, 0.03f, 0.07f, 0.9f), UIHelper.GlowBottom, UIHelper.BorderSubtle);

                var lblObj = new GameObject("Lbl"); lblObj.transform.SetParent(row.transform, false);
                var lr = lblObj.AddComponent<RectTransform>(); lr.anchorMin = new Vector2(0.05f, 0); lr.anchorMax = new Vector2(0.7f, 1); lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;
                var lbl = lblObj.AddComponent<Text>();
                int clsIdx = Mathf.Clamp(memberCls[i], 0, 2);
                string cc = ColorUtility.ToHtmlStringRGBA(ClassColors[clsIdx]);
                string leaderIcon = (i == 0) ? "" : "";
                string readyText = memberReady[i] ? "<color=#55FF55>✓ 准备就绪</color>" : "<color=#FF6644>✗ 未准备</color>";
                lbl.text = $"{leaderIcon}<color=#{cc}>{ClassNames[clsIdx]}</color> {memberNames[i]}  Lv.{memberLv[i]}  {readyText}";
                lbl.alignment = TextAnchor.MiddleLeft; lbl.fontSize = 14; lbl.color = UIHelper.TextPrimary; lbl.font = _font; lbl.supportRichText = true; lbl.raycastTarget = false;

                if (isLeader && i > 0)
                {
                    UIHelper.MakeButton(row.transform, "KickBtn", "踢出", _font,
                        new Vector2(0.75f, 0.15f), new Vector2(0.95f, 0.85f), Vector2.zero, Vector2.zero,
                        UIHelper.BtnDanger, 11, () =>
                        {
                            if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("已踢出", memberNames[i]);
                        });
                }
            }

            // 底部操作
            if (isLeader)
            {
                UIHelper.MakeButton(_tabContent.transform, "InviteBtn", "邀请好友", _font,
                    new Vector2(0.1f, 0.05f), new Vector2(0.35f, 0.15f), Vector2.zero, Vector2.zero,
                    UIHelper.BtnConfirm, 14, () =>
                    {
                        var friends = GetFriends();
                        var online = friends.FindAll(f => f.status == 1);
                        if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("邀请发送", $"已邀请{online.Count}位在线好友");
                    });

                UIHelper.MakeButton(_tabContent.transform, "TransferBtn", "转让队长", _font,
                    new Vector2(0.38f, 0.05f), new Vector2(0.63f, 0.15f), Vector2.zero, Vector2.zero,
                    new Color(0.2f, 0.3f, 0.6f, 0.9f), 14, () =>
                    {
                        FriendData.LeaveTeam();
                        if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("转让成功", "队长已转让");
                        RefreshTabLast();
                    });

                UIHelper.MakeButton(_tabContent.transform, "DisbandBtn", "解散队伍", _font,
                    new Vector2(0.66f, 0.05f), new Vector2(0.95f, 0.15f), Vector2.zero, Vector2.zero,
                    UIHelper.BtnDanger, 14, () =>
                    {
                        FriendData.DisbandTeam();
                        if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("队伍已解散", "");
                        RefreshTabLast();
                    });
            }
            else
            {
                UIHelper.MakeButton(_tabContent.transform, "LeaveBtn", "退出队伍", _font,
                    new Vector2(0.3f, 0.05f), new Vector2(0.7f, 0.15f), Vector2.zero, Vector2.zero,
                    UIHelper.BtnDanger, 16, () =>
                    {
                        FriendData.LeaveTeam();
                        if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("已退出队伍", "");
                        RefreshTabLast();
                    });
            }
        }
    }
}
