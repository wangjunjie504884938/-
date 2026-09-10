using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// GuildUI partial — Member management tab + Info tab + appoint/announcement/invite popups
/// </summary>
public partial class GuildUI
{
    // ========================================================
    //  Tab 0: 成员管理
    // ========================================================
    private void BuildMemberTab()
    {
        var members = GetMembers();
        // 按职位排序：会长(0)→副会长(1)→长老(2)→成员(3)
        members.Sort((a, b) => a.rank.CompareTo(b.rank));
        var player = GameManager.Instance?.Player;

        // 公会公告
        var announceArea = UIHelper.MakeGlowCard(_tabContent.transform, "Announce",
            new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(0, -50), new Vector2(0, 0),
            new Color(0.06f, 0.04f, 0.08f, 0.5f), UIHelper.GlowBottom, UIHelper.BorderSubtle);
        var annTxt = new GameObject("Txt"); annTxt.transform.SetParent(announceArea.transform, false);
        var annR = annTxt.AddComponent<RectTransform>(); annR.anchorMin = new Vector2(0.05f, 0); annR.anchorMax = new Vector2(0.75f, 1); annR.offsetMin = new Vector2(8, 4); annR.offsetMax = new Vector2(-8, -4);
        var annLabel = annTxt.AddComponent<Text>();
        annLabel.text = $"{GuildAnnounce}";
        annLabel.alignment = TextAnchor.MiddleLeft; annLabel.fontSize = 14; annLabel.color = UIHelper.TextSecondary; annLabel.font = _font; annLabel.supportRichText = true;

        if (CanEditAnnounce)
        {
            UIHelper.MakeButton(announceArea.transform, "EditBtn", "✎", _font,
                new Vector2(0.75f, 0.1f), new Vector2(0.95f, 0.9f), Vector2.zero, Vector2.zero,
                UIHelper.BtnPrimary, 16, () => StartCoroutine(EditAnnouncement()));
        }

        // 成员列表
        int total = members.Count + 1; // +1 for player
        int start = _memberPage * MembersPerPage;
        int end = Mathf.Min(start + MembersPerPage, total);
        int count = end - start;
        float rowH = 50f, gap = 4f;
        float startY = -10f;

        for (int i = start; i < end; i++)
        {
            int slot = i - start;
            float y = startY - slot * (rowH + gap);

            string name; int cls, level, contribution, rank; bool online;
            if (i == 0)
            {
                name = PlayerProgressData.GetSavedCharacterName();
                if (string.IsNullOrEmpty(name)) name = CloudSaveManager.Instance?.CurrentUsername ?? "冒险者";
                cls = (int)(player?.HeroClass ?? HeroClass.Warrior);
                level = player?.Stats?.Level ?? 1;
                contribution = Contribution;
                rank = 0; // 会长
                online = true;
            }
            else
            {
                var m = members[i - 1];
                name = m.name; cls = m.cls; level = m.level;
                contribution = m.contribution; rank = m.rank; online = m.online;
            }

            var row = UIHelper.MakeGlowCard(_tabContent.transform, $"Member_{i}",
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, y - rowH), new Vector2(0, y),
                new Color(0.06f, 0.04f, 0.08f, 0.92f), UIHelper.GlowBottom, UIHelper.BorderSubtle);

            var txtObj = new GameObject("Lbl"); txtObj.transform.SetParent(row.transform, false);
            var txtR = txtObj.AddComponent<RectTransform>(); txtR.anchorMin = Vector2.zero; txtR.anchorMax = new Vector2(0.7f, 1); txtR.offsetMin = new Vector2(10, 4); txtR.offsetMax = new Vector2(-4, -4);
            var txt = txtObj.AddComponent<Text>();
            int clsIdx = Mathf.Clamp(cls, 0, 2);
            string classColor = ColorUtility.ToHtmlStringRGBA(ClassColors[clsIdx]);
            string rankColor = ColorUtility.ToHtmlStringRGBA(RankColors[rank]);
            string statusIcon = online ? "<color=#55FF55>●</color>" : "<color=#555>○</color>";
            txt.text = $"{statusIcon} <color=#{rankColor}>[{RankNames[rank]}]</color> <color=#{classColor}>{ClassNames[clsIdx]}</color> {name}  Lv.{level}  贡献:{contribution}";
            txt.alignment = TextAnchor.MiddleLeft; txt.fontSize = 14; txt.color = UIHelper.TextPrimary; txt.font = _font; txt.supportRichText = true; txt.raycastTarget = false;

            // 踢人按钮（根据权限）
            if (i > 0)
            {
                int targetRank = rank;
                if (CanKick(targetRank))
                {
                    UIHelper.MakeButton(row.transform, "KickBtn", "踢", _font,
                        new Vector2(0.80f, 0.1f), new Vector2(0.96f, 0.9f), Vector2.zero, Vector2.zero,
                        UIHelper.BtnDanger, 12, () =>
                        {
                            members.RemoveAt(i - 1);
                            SaveMembers(members);
                            RefreshTabLast();
                        });
                }
            }
        }

        // 分页
        int totalPages = Mathf.Max(1, Mathf.CeilToInt((float)total / MembersPerPage));
        var pageRow = new GameObject("PageRow"); pageRow.transform.SetParent(_tabContent.transform, false);
        var pgr = pageRow.AddComponent<RectTransform>();
        pgr.anchorMin = new Vector2(0.5f, 0); pgr.anchorMax = new Vector2(0.5f, 0);
        pgr.pivot = new Vector2(0.5f, 0);
        pgr.offsetMin = new Vector2(-150, 5); pgr.offsetMax = new Vector2(150, 55);

        UIHelper.MakeButton(pageRow.transform, "PrevBtn", "◀ 上一页", _font,
            new Vector2(0, 0), new Vector2(0.4f, 1), Vector2.zero, Vector2.zero,
            new Color(0.08f, 0.08f, 0.12f, 0.9f), 13, () => { if (_memberPage > 0) { _memberPage--; RefreshTabLast(); } });

        _pageText = new GameObject("PageText").AddComponent<Text>();
        _pageText.transform.SetParent(pageRow.transform, false);
        var ptr = _pageText.GetComponent<RectTransform>();
        ptr.anchorMin = new Vector2(0.4f, 0); ptr.anchorMax = new Vector2(0.6f, 1); ptr.offsetMin = Vector2.zero; ptr.offsetMax = Vector2.zero;
        _pageText.alignment = TextAnchor.MiddleCenter; _pageText.fontSize = 14; _pageText.color = UIHelper.TextSecondary; _pageText.font = _font;
        _pageText.text = $"{_memberPage + 1} / {totalPages}";

        UIHelper.MakeButton(pageRow.transform, "NextBtn", "下一页 ▶", _font,
            new Vector2(0.6f, 0), new Vector2(1f, 1), Vector2.zero, Vector2.zero,
            new Color(0.08f, 0.08f, 0.12f, 0.9f), 13, () => { if (_memberPage < totalPages - 1) { _memberPage++; RefreshTabLast(); } });

        // 底部按钮行：邀请/任命/转让/退出 — 固定位置
        float btnY = 75, btnH = 70, btnW = 180;
        float[] btnXs = { -381, -191, -1, 189 }; // InviteBtn, AppointBtn, TransferBtn, LeaveBtn offsetMin.x
        int btnIdx = 0;

        // 邀请成员 (会长/副会长/长老可见)
        if (PlayerRank <= 2)
        {
            float x = btnXs[btnIdx];
            var inviteBtn = new GameObject("InviteBtn"); inviteBtn.transform.SetParent(_tabContent.transform, false);
            var ir = inviteBtn.AddComponent<RectTransform>();
            ir.anchorMin = new Vector2(0.5f, 0); ir.anchorMax = new Vector2(0.5f, 0);
            ir.pivot = new Vector2(0.5f, 0);
            ir.offsetMin = new Vector2(x, btnY); ir.offsetMax = new Vector2(x + btnW, btnY + btnH);
            inviteBtn.AddComponent<Image>().color = UIHelper.BtnConfirm;
            var iBtn = inviteBtn.AddComponent<Button>();
            var iLbl = new GameObject("Lbl"); iLbl.transform.SetParent(inviteBtn.transform, false);
            var ilR = iLbl.AddComponent<RectTransform>(); ilR.anchorMin = Vector2.zero; ilR.anchorMax = Vector2.one; ilR.offsetMin = Vector2.zero; ilR.offsetMax = Vector2.zero;
            var iTxt = iLbl.AddComponent<Text>(); iTxt.text = "邀请"; iTxt.alignment = TextAnchor.MiddleCenter; iTxt.fontSize = 14; iTxt.color = UIHelper.TextPrimary; iTxt.font = _font; iTxt.raycastTarget = false;
            iBtn.onClick.AddListener(() => StartCoroutine(ShowGuildInvitePopup(members)));
            btnIdx++;
        }

        // 任命职位（会长+副会长可见）
        if (PlayerRank <= 1)
        {
            float x = btnXs[btnIdx];
            var appointBtn = new GameObject("AppointBtn"); appointBtn.transform.SetParent(_tabContent.transform, false);
            var apR = appointBtn.AddComponent<RectTransform>();
            apR.anchorMin = new Vector2(0.5f, 0); apR.anchorMax = new Vector2(0.5f, 0);
            apR.pivot = new Vector2(0.5f, 0);
            apR.offsetMin = new Vector2(x, btnY); apR.offsetMax = new Vector2(x + btnW, btnY + btnH);
            appointBtn.AddComponent<Image>().color = new Color(0.15f, 0.25f, 0.5f, 0.9f);
            var apBtn = appointBtn.AddComponent<Button>();
            var apLbl = new GameObject("Lbl"); apLbl.transform.SetParent(appointBtn.transform, false);
            var aplR = apLbl.AddComponent<RectTransform>(); aplR.anchorMin = Vector2.zero; aplR.anchorMax = Vector2.one; aplR.offsetMin = Vector2.zero; aplR.offsetMax = Vector2.zero;
            var apTxt = apLbl.AddComponent<Text>(); apTxt.text = "任命"; apTxt.alignment = TextAnchor.MiddleCenter; apTxt.fontSize = 14; apTxt.color = UIHelper.TextPrimary; apTxt.font = _font; apTxt.raycastTarget = false;
            apBtn.onClick.AddListener(() => StartCoroutine(ShowAppointPopup(members)));
            btnIdx++;
        }

        // 转让会长（仅会长）
        if (PlayerRank == 0 && members.Count > 0)
        {
            float x = btnXs[btnIdx];
            var transferBtn = new GameObject("TransferBtn"); transferBtn.transform.SetParent(_tabContent.transform, false);
            var tfR = transferBtn.AddComponent<RectTransform>();
            tfR.anchorMin = new Vector2(0.5f, 0); tfR.anchorMax = new Vector2(0.5f, 0);
            tfR.pivot = new Vector2(0.5f, 0);
            tfR.offsetMin = new Vector2(x, btnY); tfR.offsetMax = new Vector2(x + btnW, btnY + btnH);
            transferBtn.AddComponent<Image>().color = new Color(0.2f, 0.3f, 0.6f, 0.9f);
            var tfBtn = transferBtn.AddComponent<Button>();
            var tfLbl = new GameObject("Lbl"); tfLbl.transform.SetParent(transferBtn.transform, false);
            var tflR = tfLbl.AddComponent<RectTransform>(); tflR.anchorMin = Vector2.zero; tflR.anchorMax = Vector2.one; tflR.offsetMin = Vector2.zero; tflR.offsetMax = Vector2.zero;
            var tfTxt = tfLbl.AddComponent<Text>(); tfTxt.text = "转让"; tfTxt.alignment = TextAnchor.MiddleCenter; tfTxt.fontSize = 14; tfTxt.color = UIHelper.TextPrimary; tfTxt.font = _font; tfTxt.raycastTarget = false;
            tfBtn.onClick.AddListener(() =>
            {
                members[0] = new GuildData.GuildMember { name = members[0].name, cls = members[0].cls, level = members[0].level, contribution = members[0].contribution, rank = 0, online = members[0].online };
                PlayerPrefs.SetInt(GuildData.KeyPlayerRank, 3);
                SaveMembers(members);
                if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("转让成功", $"会长已转让给 {members[0].name}");
                RefreshTabLast();
            });
            btnIdx++;
        }

        // 退出/解散
        {
            float x = btnXs[btnIdx];
            var leaveBtn = new GameObject("LeaveBtn"); leaveBtn.transform.SetParent(_tabContent.transform, false);
            var lvR = leaveBtn.AddComponent<RectTransform>();
            lvR.anchorMin = new Vector2(0.5f, 0); lvR.anchorMax = new Vector2(0.5f, 0);
            lvR.pivot = new Vector2(0.5f, 0);
            lvR.offsetMin = new Vector2(x, btnY); lvR.offsetMax = new Vector2(x + btnW, btnY + btnH);
            leaveBtn.AddComponent<Image>().color = UIHelper.BtnDanger;
            var lBtn = leaveBtn.AddComponent<Button>();
            var lLbl = new GameObject("Lbl"); lLbl.transform.SetParent(leaveBtn.transform, false);
            var llR = lLbl.AddComponent<RectTransform>(); llR.anchorMin = Vector2.zero; llR.anchorMax = Vector2.one; llR.offsetMin = Vector2.zero; llR.offsetMax = Vector2.zero;
            var lTxt = lLbl.AddComponent<Text>();
            lTxt.text = PlayerRank == 0 ? "解散公会" : "退出公会";
            lTxt.alignment = TextAnchor.MiddleCenter; lTxt.fontSize = 14; lTxt.color = UIHelper.TextPrimary; lTxt.font = _font; lTxt.raycastTarget = false;
            lBtn.onClick.AddListener(() => {
                PlayerPrefs.DeleteKey(GuildData.KeyGuildName); PlayerPrefs.DeleteKey(GuildData.KeyGuildLevel); PlayerPrefs.DeleteKey(GuildData.KeyGuildExp);
                PlayerPrefs.DeleteKey(GuildData.KeyGuildAnnounce); PlayerPrefs.DeleteKey(GuildData.KeyContribution);
                PlayerPrefs.DeleteKey(GuildData.KeyGuildMembers); PlayerPrefs.DeleteKey(GuildData.KeyPlayerRank);
                PlayerPrefs.Save();
                if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast(PlayerRank == 0 ? "公会已解散" : "已退出公会", "");
                Show();
            });
        }
    }

    /// <summary>任命弹窗 — 显示成员列表，可设置职位</summary>
    private IEnumerator ShowAppointPopup(List<GuildData.GuildMember> members)
    {
        Canvas canvas = GameManager.EnsureCanvas();

        // 全屏遮罩
        var overlay = new GameObject("AppointPopup");
        overlay.transform.SetParent(canvas.transform, false);
        var oR = overlay.AddComponent<RectTransform>();
        oR.anchorMin = Vector2.zero; oR.anchorMax = Vector2.one; oR.offsetMin = Vector2.zero; oR.offsetMax = Vector2.zero;
        overlay.AddComponent<Image>().color = new Color(0, 0, 0, 0.7f);
        var oCG = overlay.AddComponent<CanvasGroup>();

        // 弹窗
        var box = UIHelper.MakeGlowCard(overlay.transform, "Box",
            new Vector2(0.15f, 0.1f), new Vector2(0.85f, 0.9f), Vector2.zero, Vector2.zero,
            new Color(0.06f, 0.05f, 0.10f, 0.98f), UIHelper.GlowTop, UIHelper.Accent);

        // 标题
        var titleObj = new GameObject("Title"); titleObj.transform.SetParent(box.transform, false);
        var tR = titleObj.AddComponent<RectTransform>(); tR.anchorMin = new Vector2(0, 0.92f); tR.anchorMax = new Vector2(0.85f, 1f); tR.offsetMin = Vector2.zero; tR.offsetMax = Vector2.zero;
        var title = titleObj.AddComponent<Text>(); title.text = "任命职位"; title.alignment = TextAnchor.MiddleLeft; title.fontSize = 22; title.color = UIHelper.Accent; title.font = _font;

        // 关闭按钮
        UIHelper.MakeButton(box.transform, "CloseBtn", "✕", _font,
            new Vector2(0.88f, 0.93f), new Vector2(0.98f, 0.99f), Vector2.zero, Vector2.zero,
            UIHelper.BtnDanger, 18, () => Destroy(overlay));

        // 滚动列表
        var scrollObj = new GameObject("ScrollArea");
        scrollObj.transform.SetParent(box.transform, false);
        var saR = scrollObj.AddComponent<RectTransform>();
        saR.anchorMin = new Vector2(0.05f, 0.06f); saR.anchorMax = new Vector2(0.95f, 0.90f); saR.offsetMin = Vector2.zero; saR.offsetMax = Vector2.zero;
        scrollObj.AddComponent<Image>().color = new Color(0.03f, 0.02f, 0.05f, 0.5f);
        var scroll = scrollObj.AddComponent<ScrollRect>();
        scroll.scrollSensitivity = 20f;

        var vpObj = new GameObject("Viewport");
        vpObj.transform.SetParent(scrollObj.transform, false);
        var vpR = vpObj.AddComponent<RectTransform>();
        vpR.anchorMin = Vector2.zero; vpR.anchorMax = Vector2.one; vpR.offsetMin = Vector2.zero; vpR.offsetMax = Vector2.zero;
        vpObj.AddComponent<RectMask2D>();
        vpObj.AddComponent<Image>().color = Color.clear;

        var contentObj = new GameObject("Content");
        contentObj.transform.SetParent(vpObj.transform, false);
        var cR = contentObj.AddComponent<RectTransform>();
        cR.anchorMin = new Vector2(0, 1); cR.anchorMax = Vector2.one; cR.pivot = new Vector2(0.5f, 1);
        cR.offsetMin = Vector2.zero; cR.offsetMax = Vector2.zero;

        scroll.content = cR;
        scroll.viewport = vpR;

        // 生成成员行
        int memberCount = members.Count;
        float rowH = 80f, gap = 8f;
        float totalH = memberCount * rowH + (memberCount - 1) * gap + 16f;
        cR.sizeDelta = new Vector2(0, totalH);

        for (int i = 0; i < memberCount; i++)
        {
            int idx = i;
            var m = members[i];
            float y = -8f - i * (rowH + gap);

            var row = UIHelper.MakeGlowCard(contentObj.transform, $"Member_{i}",
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(8, y - rowH), new Vector2(-8, y),
                new Color(0.06f, 0.04f, 0.08f, 0.92f), UIHelper.GlowBottom, UIHelper.BorderSubtle);

            // 成员信息
            var infoObj = new GameObject("Info"); infoObj.transform.SetParent(row.transform, false);
            var iR = infoObj.AddComponent<RectTransform>(); iR.anchorMin = new Vector2(0.03f, 0); iR.anchorMax = new Vector2(0.45f, 1); iR.offsetMin = new Vector2(8, 4); iR.offsetMax = new Vector2(-4, -4);
            var info = infoObj.AddComponent<Text>();
            int clsIdx = Mathf.Clamp(m.cls, 0, 2);
            string classColor = ColorUtility.ToHtmlStringRGBA(ClassColors[clsIdx]);
            string rankColor = ColorUtility.ToHtmlStringRGBA(RankColors[m.rank]);
            info.text = $"<color=#{rankColor}>[{RankNames[m.rank]}]</color> <color=#{classColor}>{ClassNames[clsIdx]}</color> {m.name}  Lv.{m.level}";
            info.alignment = TextAnchor.MiddleLeft; info.fontSize = 15; info.color = UIHelper.TextPrimary; info.font = _font; info.supportRichText = true; info.raycastTarget = false;

            // 当前职位标签
            var curRankObj = new GameObject("CurRank"); curRankObj.transform.SetParent(row.transform, false);
            var crR = curRankObj.AddComponent<RectTransform>(); crR.anchorMin = new Vector2(0.45f, 0.15f); crR.anchorMax = new Vector2(0.62f, 0.85f); crR.offsetMin = Vector2.zero; crR.offsetMax = Vector2.zero;
            var curTxt = curRankObj.AddComponent<Text>();
            curTxt.text = $"当前: {RankNames[m.rank]}";
            curTxt.alignment = TextAnchor.MiddleCenter; curTxt.fontSize = 13; curTxt.color = UIHelper.TextSecondary; curTxt.font = _font; curTxt.raycastTarget = false;

            // 职位选择按钮（根据权限决定可选范围）
            // 会长: 可设置副会长/长老/成员
            // 副会长: 可设置长老/成员
            int minRank = PlayerRank == 0 ? 1 : 2;
            int maxRank = 3;
            for (int r = minRank; r <= maxRank; r++)
            {
                int targetR = r;
                bool canSet = PlayerRank == 0 ? true : (targetR >= 2); // 副会长只能设长老/成员
                if (!canSet) continue;

                float btnW2 = 0.11f;
                float btnX2 = 0.64f + (r - minRank) * (btnW2 + 0.01f);
                var btnColor = targetR == m.rank ? UIHelper.Accent : new Color(0.1f, 0.1f, 0.15f, 0.9f);

                UIHelper.MakeButton(row.transform, $"Rank_{r}", RankNames[targetR], _font,
                    new Vector2(btnX2, 0.15f), new Vector2(btnX2 + btnW2, 0.85f), Vector2.zero, Vector2.zero,
                    btnColor, 12, () =>
                    {
                        if (targetR == m.rank) return;
                        members[idx] = new GuildData.GuildMember
                        {
                            name = m.name, cls = m.cls, level = m.level,
                            contribution = m.contribution, rank = targetR, online = m.online,
                        };
                        SaveMembers(members);
                        if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("任命成功", $"{m.name} → {RankNames[targetR]}");
                        Destroy(overlay);
                        RefreshTabLast();
                    });
            }
        }

        // 淡入
        float t = 0;
        while (t < 0.2f)
        {
            t += Time.unscaledDeltaTime;
            oCG.alpha = Mathf.Clamp01(t / 0.2f);
            yield return null;
        }
        oCG.alpha = 1f;
        yield return null;
    }

    private IEnumerator EditAnnouncement()
    {
        // 简易编辑：弹出输入框
        // 由于Tuanjie引擎InputField可能不稳定，使用循环弹窗
        string[] presets = {
            "欢迎来到公会！一起努力变强吧！",
            "每日捐献+Boss挑战，冲冲冲！",
            "有困难找会长，大家一起帮忙！",
            "本周目标：公会Boss击杀！全员参与！",
            "新人先看公告，有问题问副会长。",
        };
        // 简单选择方式：点击切换
        int idx = 0;
        // 弹一个简单覆盖层
        Canvas canvas = GameManager.EnsureCanvas();
        var overlay = new GameObject("AnnounceEdit");
        overlay.transform.SetParent(canvas.transform, false);
        var or = overlay.AddComponent<RectTransform>();
        or.anchorMin = Vector2.zero; or.anchorMax = Vector2.one; or.offsetMin = Vector2.zero; or.offsetMax = Vector2.zero;
        or.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.7f);
        var cg = overlay.AddComponent<CanvasGroup>();

        var box = UIHelper.MakeGlowCard(overlay.transform, "Box",
            new Vector2(0.2f, 0.3f), new Vector2(0.8f, 0.7f), Vector2.zero, Vector2.zero,
            UIHelper.GlowTop, UIHelper.GlowBottom, UIHelper.Accent);

        var titleObj = new GameObject("Title"); titleObj.transform.SetParent(box.transform, false);
        var ttr = titleObj.AddComponent<RectTransform>(); ttr.anchorMin = new Vector2(0, 0.75f); ttr.anchorMax = new Vector2(1, 1f); ttr.offsetMin = Vector2.zero; ttr.offsetMax = Vector2.zero;
        var title = titleObj.AddComponent<Text>(); title.text = "编辑公会公告"; title.alignment = TextAnchor.MiddleCenter; title.fontSize = 20; title.color = UIHelper.Accent; title.font = _font;

        var displayObj = new GameObject("Display"); displayObj.transform.SetParent(box.transform, false);
        var drR = displayObj.AddComponent<RectTransform>(); drR.anchorMin = new Vector2(0.05f, 0.4f); drR.anchorMax = new Vector2(0.95f, 0.7f); drR.offsetMin = Vector2.zero; drR.offsetMax = Vector2.zero;
        var display = displayObj.AddComponent<Text>(); display.alignment = TextAnchor.MiddleCenter; display.fontSize = 16; display.color = UIHelper.TextPrimary; display.font = _font; display.text = presets[0];

        var prevBtn = UIHelper.MakeButton(box.transform, "Prev", "◀", _font,
            new Vector2(0.05f, 0.1f), new Vector2(0.25f, 0.3f), Vector2.zero, Vector2.zero,
            UIHelper.BtnPrimary, 16, () => { idx = (idx + presets.Length - 1) % presets.Length; display.text = presets[idx]; });

        var okBtn = UIHelper.MakeButton(box.transform, "OK", "确认", _font,
            new Vector2(0.3f, 0.1f), new Vector2(0.7f, 0.3f), Vector2.zero, Vector2.zero,
            UIHelper.BtnConfirm, 16, () => {
                PlayerPrefs.SetString(GuildData.KeyGuildAnnounce, presets[idx]); PlayerPrefs.Save();
                Destroy(overlay); Show();
            });

        var nextBtn = UIHelper.MakeButton(box.transform, "Next", "▶", _font,
            new Vector2(0.75f, 0.1f), new Vector2(0.95f, 0.3f), Vector2.zero, Vector2.zero,
            UIHelper.BtnPrimary, 16, () => { idx = (idx + 1) % presets.Length; display.text = presets[idx]; });

        yield return null;
    }

    // ========================================================
    //  Tab 1: 信息
    // ========================================================
    private void BuildInfoTab()
    {
        // 公会信息卡片
        var infoCard = UIHelper.MakeGlowCard(_tabContent.transform, "InfoCard",
            new Vector2(0.05f, 0.70f), new Vector2(0.95f, 0.98f), Vector2.zero, Vector2.zero,
            new Color(0.06f, 0.04f, 0.08f, 0.92f), UIHelper.GlowTop, UIHelper.Accent);

        var titleObj = new GameObject("Title"); titleObj.transform.SetParent(infoCard.transform, false);
        var tR = titleObj.AddComponent<RectTransform>(); tR.anchorMin = new Vector2(0.05f, 0.70f); tR.anchorMax = new Vector2(0.95f, 0.95f); tR.offsetMin = Vector2.zero; tR.offsetMax = Vector2.zero;
        var title = titleObj.AddComponent<Text>();
        title.text = $"{GuildName}  Lv.{GuildLevel}";
        title.alignment = TextAnchor.MiddleCenter; title.fontSize = 20; title.color = UIHelper.Accent; title.font = _font;

        var statsObj = new GameObject("Stats"); statsObj.transform.SetParent(infoCard.transform, false);
        var sR = statsObj.AddComponent<RectTransform>(); sR.anchorMin = new Vector2(0.05f, 0.25f); sR.anchorMax = new Vector2(0.95f, 0.65f); sR.offsetMin = Vector2.zero; sR.offsetMax = Vector2.zero;
        var stats = statsObj.AddComponent<Text>();
        var members = GetMembers();
        int onlineCount = 1; // player
        foreach (var m in members) if (m.online) onlineCount++;
        stats.text = $"公会经验: {GuildExp}/{GuildExpToNext}\n成员: {members.Count + 1}/{GuildMaxMembers}  在线: {onlineCount}\n 贡献点: {Contribution}  职位: {RankNames[PlayerRank]}\n每日签到加成: +{DailyBonus*100:F0}%";
        stats.alignment = TextAnchor.MiddleCenter; stats.fontSize = 15; stats.color = UIHelper.TextPrimary; stats.font = _font; stats.supportRichText = true;

        // 公会公告
        var announceCard = UIHelper.MakeGlowCard(_tabContent.transform, "AnnounceCard",
            new Vector2(0.05f, 0.40f), new Vector2(0.95f, 0.68f), Vector2.zero, Vector2.zero,
            new Color(0.06f, 0.04f, 0.08f, 0.92f), UIHelper.GlowBottom, UIHelper.BorderSubtle);

        var annTitle = new GameObject("AnnTitle"); annTitle.transform.SetParent(announceCard.transform, false);
        var aR = annTitle.AddComponent<RectTransform>(); aR.anchorMin = new Vector2(0.05f, 0.65f); aR.anchorMax = new Vector2(0.75f, 0.95f); aR.offsetMin = Vector2.zero; aR.offsetMax = Vector2.zero;
        var annT = annTitle.AddComponent<Text>();
        annT.text = "公会公告"; annT.alignment = TextAnchor.MiddleLeft; annT.fontSize = 16; annT.color = UIHelper.Accent; annT.font = _font;

        if (CanEditAnnounce)
        {
            UIHelper.MakeButton(announceCard.transform, "EditBtn", "✎ 编辑", _font,
                new Vector2(0.72f, 0.65f), new Vector2(0.95f, 0.95f), Vector2.zero, Vector2.zero,
                UIHelper.BtnPrimary, 12, () => StartCoroutine(EditAnnouncement()));
        }

        var annContent = new GameObject("AnnContent"); annContent.transform.SetParent(announceCard.transform, false);
        var acR = annContent.AddComponent<RectTransform>(); acR.anchorMin = new Vector2(0.05f, 0.05f); acR.anchorMax = new Vector2(0.95f, 0.60f); acR.offsetMin = Vector2.zero; acR.offsetMax = Vector2.zero;
        var annTxt = annContent.AddComponent<Text>();
        annTxt.text = GuildAnnounce;
        annTxt.alignment = TextAnchor.MiddleLeft; annTxt.fontSize = 14; annTxt.color = UIHelper.TextSecondary; annTxt.font = _font; annTxt.supportRichText = true;

        // 邀请成员卡片 (会长/副会长/长老可见)
        if (PlayerRank <= 2)
        {
        var inviteCard = UIHelper.MakeGlowCard(_tabContent.transform, "InviteCard",
            new Vector2(0.05f, 0.10f), new Vector2(0.95f, 0.38f), Vector2.zero, Vector2.zero,
            new Color(0.06f, 0.04f, 0.08f, 0.92f), UIHelper.GlowBottom, UIHelper.BorderSubtle);

        var inviteTitle = new GameObject("Title"); inviteTitle.transform.SetParent(inviteCard.transform, false);
        var itR = inviteTitle.AddComponent<RectTransform>(); itR.anchorMin = new Vector2(0.05f, 0.55f); itR.anchorMax = new Vector2(0.95f, 0.90f); itR.offsetMin = Vector2.zero; itR.offsetMax = Vector2.zero;
        var itTxt = inviteTitle.AddComponent<Text>();
        itTxt.text = "邀请新成员"; itTxt.alignment = TextAnchor.MiddleLeft; itTxt.fontSize = 16; itTxt.color = UIHelper.Accent; itTxt.font = _font;

        var inviteDesc = new GameObject("Desc"); inviteDesc.transform.SetParent(inviteCard.transform, false);
        var idR = inviteDesc.AddComponent<RectTransform>(); idR.anchorMin = new Vector2(0.05f, 0.30f); idR.anchorMax = new Vector2(0.55f, 0.50f); idR.offsetMin = Vector2.zero; idR.offsetMax = Vector2.zero;
        var idTxt = inviteDesc.AddComponent<Text>();
        idTxt.text = $"当前: {members.Count + 1}/{GuildMaxMembers}人";
        idTxt.alignment = TextAnchor.MiddleLeft; idTxt.fontSize = 13; idTxt.color = UIHelper.TextSecondary; idTxt.font = _font;

        var inviteBtn = UIHelper.MakeButton(inviteCard.transform, "Btn", "邀请成员", _font,
            new Vector2(0.55f, 0.10f), new Vector2(0.95f, 0.50f), Vector2.zero, Vector2.zero,
            UIHelper.BtnConfirm, 14, () =>
            {
                var m = GetMembers();
                StartCoroutine(ShowGuildInvitePopup(m));
                });
                } // end if PlayerRank <= 2
            }

            // ========================================================
            //  公会邀请弹窗 — 选择好友/最近玩家邀请加入公会
            // ========================================================
            private IEnumerator ShowGuildInvitePopup(List<GuildData.GuildMember> members)
    {
        Canvas canvas = GameManager.EnsureCanvas();
        var overlay = new GameObject("GuildInvitePopup");
        overlay.transform.SetParent(canvas.transform, false);
        var oR = overlay.AddComponent<RectTransform>();
        oR.anchorMin = Vector2.zero; oR.anchorMax = Vector2.one;
        oR.offsetMin = Vector2.zero; oR.offsetMax = Vector2.zero;
        overlay.AddComponent<Image>().color = new Color(0, 0, 0, 0.7f);
        overlay.AddComponent<CanvasGroup>();

        var box = UIHelper.MakeGlowCard(overlay.transform, "Box",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-360, -360), new Vector2(360, 360),
            new Color(0.06f, 0.03f, 0.09f, 0.95f),
            UIHelper.GlowBottom, UIHelper.Accent);

        var titleObj = new GameObject("Title");
        titleObj.transform.SetParent(box.transform, false);
        var tR = titleObj.AddComponent<RectTransform>();
        tR.anchorMin = new Vector2(0, 1); tR.anchorMax = new Vector2(1, 1);
        tR.pivot = new Vector2(0.5f, 1); tR.anchoredPosition = new Vector2(0, -12);
        tR.sizeDelta = new Vector2(0, 32);
        var titleTxt = titleObj.AddComponent<Text>();
        titleTxt.text = "邀请好友加入公会";
        titleTxt.alignment = TextAnchor.MiddleCenter;
        titleTxt.fontSize = 22; titleTxt.color = UIHelper.Accent; titleTxt.font = _font;

        UIHelper.MakeButton(box.transform, "CloseBtn", "X", _font,
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(-44, -40), new Vector2(-8, -8),
            new Color(0.4f, 0.1f, 0.1f, 0.9f), 18, () => Destroy(overlay));

        var scrollObj = new GameObject("ScrollArea");
        scrollObj.transform.SetParent(box.transform, false);
        var sR = scrollObj.AddComponent<RectTransform>();
        sR.anchorMin = new Vector2(0.05f, 0.12f); sR.anchorMax = new Vector2(0.95f, 0.88f);
        sR.offsetMin = Vector2.zero; sR.offsetMax = Vector2.zero;
        scrollObj.AddComponent<Image>().color = new Color(0.03f, 0.02f, 0.05f, 0.5f);

        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollObj.transform, false);
        var vpR = viewport.AddComponent<RectTransform>();
        vpR.anchorMin = Vector2.zero; vpR.anchorMax = Vector2.one;
        vpR.offsetMin = Vector2.zero; vpR.offsetMax = Vector2.zero;
        viewport.AddComponent<RectMask2D>();

        var content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        var cR = content.AddComponent<RectTransform>();
        cR.anchorMin = new Vector2(0, 1); cR.anchorMax = new Vector2(1, 1);
        cR.pivot = new Vector2(0.5f, 1);
        cR.offsetMin = Vector2.zero; cR.offsetMax = Vector2.zero;

        // 加载提示
        var loadingObj = new GameObject("Loading");
        loadingObj.transform.SetParent(viewport.transform, false);
        var lR = loadingObj.AddComponent<RectTransform>();
        lR.anchorMin = new Vector2(0, 0.4f); lR.anchorMax = new Vector2(1, 0.6f);
        lR.offsetMin = Vector2.zero; lR.offsetMax = Vector2.zero;
        var lTxt = loadingObj.AddComponent<Text>();
        lTxt.text = "加载好友列表..."; lTxt.alignment = TextAnchor.MiddleCenter;
        lTxt.fontSize = 18; lTxt.color = UIHelper.TextDim; lTxt.font = _font;

        // 从服务器获取好友列表
        yield return GetGuildAPI("/api/friend/list", (ok, resp) =>
        {
            Destroy(loadingObj);
            if (!ok || string.IsNullOrEmpty(resp))
            {
                lTxt.text = "加载失败，请先登录";
                return;
            }
            try
            {
                var friendResp = JsonUtility.FromJson<ServerFriendListResponse>(resp);
                if (friendResp?.friends == null || friendResp.friends.Length == 0)
                {
                    var empty = new GameObject("Empty");
                    empty.transform.SetParent(viewport.transform, false);
                    var eR = empty.AddComponent<RectTransform>();
                    eR.anchorMin = new Vector2(0, 0.4f); eR.anchorMax = new Vector2(1, 0.6f);
                    eR.offsetMin = Vector2.zero; eR.offsetMax = Vector2.zero;
                    var eTxt = empty.AddComponent<Text>();
                    eTxt.text = "暂无好友可邀请\n请先添加好友";
                    eTxt.alignment = TextAnchor.MiddleCenter; eTxt.fontSize = 18;
                    eTxt.color = UIHelper.TextDim; eTxt.font = _font;
                    return;
                }

                var friends = friendResp.friends;
                float rowH = 56f, gap = 4f;
                cR.sizeDelta = new Vector2(0, friends.Length * (rowH + gap));

                for (int i = 0; i < friends.Length; i++)
                {
                    var f = friends[i];
                    int friendUserId = f.friendId;
                    float y = -i * (rowH + gap);

                    var row = UIHelper.MakeGlowCard(content.transform, $"Row_{i}",
                        new Vector2(0.02f, 1), new Vector2(0.98f, 1),
                        new Vector2(0, y - rowH), new Vector2(0, y),
                        new Color(0.06f, 0.04f, 0.08f, 0.9f),
                        UIHelper.GlowBottom, UIHelper.BorderSubtle);

                    var nameObj = new GameObject("Name");
                    nameObj.transform.SetParent(row.transform, false);
                    var nR = nameObj.AddComponent<RectTransform>();
                    nR.anchorMin = new Vector2(0.03f, 0); nR.anchorMax = new Vector2(0.65f, 1);
                    nR.offsetMin = Vector2.zero; nR.offsetMax = Vector2.zero;
                    var nTxt = nameObj.AddComponent<Text>();
                    nTxt.text = $"好友  ID:{friendUserId}";
                    nTxt.alignment = TextAnchor.MiddleLeft; nTxt.fontSize = 15;
                    nTxt.color = UIHelper.TextPrimary; nTxt.font = _font;
                    nTxt.raycastTarget = false;

                    var inviteBtnText = "邀请";
                    var btn = UIHelper.MakeButton(row.transform, "InviteBtn", inviteBtnText, _font,
                        new Vector2(0.68f, 0.1f), new Vector2(0.98f, 0.9f),
                        Vector2.zero, Vector2.zero,
                        UIHelper.BtnConfirm, 14, () =>
                        {
                            StartCoroutine(SendGuildInvite(friendUserId, row, overlay));
                        });
                }
            }
            catch { }
        });
        yield return null;
    }

    [System.Serializable]
    private class ServerFriendListResponse { public ServerFriendData[] friends; public int count; public int max; }
    [System.Serializable]
    private class ServerFriendData { public int friendId; public string remark; }

    /// <summary>发送公会邀请到服务器</summary>
    private IEnumerator SendGuildInvite(int targetUserId, GameObject rowObj, GameObject overlay)
    {
        yield return PostGuildAPI($"/api/guild/invite/{targetUserId}", "{}", (ok, resp) =>
        {
            if (ok)
            {
                if (GameUI.Instance != null)
                    GameUI.Instance.ShowItemPickupToast("邀请已发送", "等待对方确认");
                // 禁用按钮防止重复
                var btn = rowObj?.transform.Find("InviteBtn")?.GetComponent<Button>();
                if (btn != null) { btn.interactable = false; }
            }
            else
            {
                if (GameUI.Instance != null)
                {
                    string err = "邀请失败";
                    try { var e = JsonUtility.FromJson<SimpleResponse>(resp); if (!string.IsNullOrEmpty(e.error)) err = e.error; } catch { }
                    GameUI.Instance.ShowItemPickupToast(err, "");
                }
            }
        });
    }

    [System.Serializable]
    private class SimpleResponse { public bool success; public string error; }

    [System.Serializable]
    private class ServerGuildListResponse { public ServerGuildData[] guilds; public int total; public int page; public int pageSize; }
    [System.Serializable]
    private class ServerGuildData { public int id; public string name; public int level; public int memberCount; public int maxMembers; public string leaderName; public string announce; }

    /// <summary>发送加入公会申请</summary>
    private IEnumerator SendJoinRequest(int guildId, GameObject rowObj, GameObject overlay)
    {
        yield return PostGuildAPI($"/api/guild/join/{guildId}", "{}", (ok, resp) =>
        {
            if (ok)
            {
                if (GameUI.Instance != null)
                    GameUI.Instance.ShowItemPickupToast("申请已发送", "等待会长确认");
                var btn = rowObj?.transform.Find("JoinBtn")?.GetComponent<Button>();
                if (btn != null) { btn.interactable = false; }
            }
            else
            {
                if (GameUI.Instance != null)
                {
                    string err = "申请失败";
                    try { var e = JsonUtility.FromJson<SimpleResponse>(resp); if (!string.IsNullOrEmpty(e.error)) err = e.error; } catch { }
                    GameUI.Instance.ShowItemPickupToast(err, "");
                }
            }
        });
    }
}
