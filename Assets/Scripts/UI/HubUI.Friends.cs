using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// HubUI partial — Friend list panel + rendering + offline rewards popup
/// </summary>
public partial class HubUI
{
    // ========================================================
    //  好友列表面板 (右侧底部)
    // ========================================================
    private void BuildFriendListPanel(Transform parent, Font font)
    {
        var friendArea = new GameObject("FriendListArea", typeof(RectTransform));
        friendArea.transform.SetParent(parent, false);
        var frR = friendArea.GetComponent<RectTransform>();
        frR.anchorMin = new Vector2(0.02f, 0.01f); frR.anchorMax = new Vector2(0.98f, 1f);
        frR.offsetMin = new Vector2(-21, 0); frR.offsetMax = new Vector2(-21, -450);

        // 背景
        var frBg = friendArea.AddComponent<Image>();
        frBg.color = new Color(0.05f, 0.03f, 0.08f, 0.8f);
        frBg.raycastTarget = false;
        // 边框
        var frBorder = new GameObject("Border", typeof(RectTransform));
        frBorder.transform.SetParent(friendArea.transform, false);
        var fbR = frBorder.GetComponent<RectTransform>();
        fbR.anchorMin = Vector2.zero; fbR.anchorMax = Vector2.one;
        fbR.offsetMin = new Vector2(-1, -1); fbR.offsetMax = new Vector2(1, 1);
        var fbImg = frBorder.AddComponent<Image>();
        fbImg.color = UIHelper.BorderSubtle; fbImg.raycastTarget = false;
        frBorder.transform.SetAsFirstSibling();

        // 标题行: 好友列表 + 添加好友按钮
        var titleRow = new GameObject("FriendTitleRow");
        titleRow.transform.SetParent(friendArea.transform, false);
        var trR = titleRow.AddComponent<RectTransform>();
        trR.anchorMin = new Vector2(0, 1); trR.anchorMax = new Vector2(1, 1);
        trR.pivot = new Vector2(0.5f, 1);
        trR.anchoredPosition = new Vector2(0, -4);
        trR.sizeDelta = new Vector2(0, 26);

        var titleTxt = new GameObject("Title");
        titleTxt.transform.SetParent(titleRow.transform, false);
        var ttR = titleTxt.AddComponent<RectTransform>();
        ttR.anchorMin = new Vector2(0.04f, 0); ttR.anchorMax = new Vector2(0.5f, 1);
        ttR.offsetMin = Vector2.zero; ttR.offsetMax = Vector2.zero;
        var tTxt = titleTxt.AddComponent<Text>();
        tTxt.text = "好友列表"; tTxt.alignment = TextAnchor.MiddleLeft;
        tTxt.fontSize = 15; tTxt.color = UIHelper.Accent; tTxt.font = font;
        tTxt.raycastTarget = false;

        _friendCountText = new GameObject("Count").AddComponent<Text>();
        _friendCountText.transform.SetParent(titleRow.transform, false);
        var fcR = _friendCountText.GetComponent<RectTransform>();
        fcR.anchorMin = new Vector2(0.5f, 0); fcR.anchorMax = new Vector2(0.75f, 1);
        fcR.offsetMin = Vector2.zero; fcR.offsetMax = Vector2.zero;
        _friendCountText.text = ""; _friendCountText.alignment = TextAnchor.MiddleLeft;
        _friendCountText.fontSize = 13; _friendCountText.color = UIHelper.TextDim;
        _friendCountText.font = font; _friendCountText.raycastTarget = false;

        // + 添加好友 按钮
        UIHelper.MakeButton(titleRow.transform, "AddFriendBtn", "+ 添加好友", font,
            new Vector2(0.75f, 0.05f), new Vector2(0.98f, 0.95f), Vector2.zero, Vector2.zero,
            UIHelper.BtnConfirm, 13, () =>
            {
                EnsureUIManager();
                if (FriendUI.Instance == null) GameManager.Instance.gameObject.AddComponent<FriendUI>();
                FriendUI.Instance.ShowAddFriendDirect();
            });

        // 好友列表容器 (滚动)
        _friendListContainer = new GameObject("FriendItems");
        _friendListContainer.transform.SetParent(friendArea.transform, false);
        var fiR = _friendListContainer.AddComponent<RectTransform>();
        fiR.anchorMin = new Vector2(0.02f, 0.02f); fiR.anchorMax = new Vector2(0.98f, 0.88f);
        fiR.offsetMin = Vector2.zero; fiR.offsetMax = Vector2.zero;
        _friendListContainer.AddComponent<RectMask2D>();
    }

    /// <summary>从服务器获取好友列表并刷新Hub右侧面板</summary>
    private System.Collections.IEnumerator RefreshFriendListHub()
    {
        if (_friendListContainer == null) yield break;

        string token = UnityEngine.PlayerPrefs.GetString("ARPG_AuthToken", "");

        // 有token时从服务器获取
        if (!string.IsNullOrEmpty(token))
        {
            using (var req = UnityEngine.Networking.UnityWebRequest.Get("http://39.107.141.107:5132/api/friend/list"))
            {
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", "Bearer " + token);
                yield return req.SendWebRequest();

                if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var resp = JsonUtility.FromJson<HubFriendListResp>(req.downloadHandler.text);
                        if (resp?.friends != null && resp.friends.Length > 0)
                        {
                            RenderFriendList(resp.friends.Length,
                                (i) => $"{resp.friends[i].username}  Lv.{resp.friends[i].level}",
                                (i) => resp.friends[i].friendId,
                                (i) => resp.friends[i].online);
                            yield break;
                        }
                    }
                    catch { }
                }
            }
        }

        // 无token或服务器失败 — 从本地缓存读取好友列表
        string localJson = UnityEngine.PlayerPrefs.GetString("ARPG_FriendList", "");
        if (!string.IsNullOrEmpty(localJson))
        {
            try
            {
                var localArr = JsonUtility.FromJson<FriendData.FriendArray>(localJson);
                if (localArr?.friends != null && localArr.friends.Length > 0)
                {
                    RenderFriendList(localArr.friends.Length,
                        (i) => $"{localArr.friends[i].name}  Lv.{localArr.friends[i].level}",
                        (i) => 0,
                        (i) => false); // 本地缓存默认离线
                    yield break;
                }
            }
            catch { }
        }

        // 无任何好友数据 — 显示空状态
        RenderFriendList(0, (i) => "", (i) => 0, (i) => false);
    }

    /// <summary>渲染好友列表到Hub面板</summary>
    private void RenderFriendList(int count, System.Func<int, string> getName, System.Func<int, int> getId, System.Func<int, bool> getOnline = null)
    {
        // 清除旧内容
        for (int i = _friendListContainer.transform.childCount - 1; i >= 0; i--)
            Destroy(_friendListContainer.transform.GetChild(i).gameObject);

        Font font = GameManager.GetUIFont();
        float rowH = 36f, gap = 3f;

        for (int i = 0; i < count && i < 6; i++)
        {
            string fName = getName(i);
            int fId = getId(i);
            bool online = getOnline?.Invoke(i) ?? false;
            float y = -i * (rowH + gap);

            var row = UIHelper.MakeGlowCard(_friendListContainer.transform, $"Friend_{i}",
                new Vector2(0.01f, 1), new Vector2(0.99f, 1),
                new Vector2(0, y - rowH), new Vector2(0, y),
                new Color(0.06f, 0.04f, 0.08f, 0.9f), UIHelper.GlowBottom, UIHelper.BorderSubtle);

            // 整行可点击 → 打开聊天
            var rowBtn = row.AddComponent<Button>();
            rowBtn.transition = Selectable.Transition.None;
            var rowImg = row.GetComponent<Image>();
            UIHelper.SetupButtonFeedback(rowBtn, rowImg.color);
            string chatName = fName;
            rowBtn.onClick.AddListener(() =>
            {
                if (ChatPopupUI.Instance == null) gameObject.AddComponent<ChatPopupUI>();
                ChatPopupUI.Instance.Show(chatName);
            });

            // 在线状态圆点
            var dotObj = new GameObject("StatusDot");
            dotObj.transform.SetParent(row.transform, false);
            var dR = dotObj.AddComponent<RectTransform>();
            dR.anchorMin = new Vector2(0.03f, 0.3f); dR.anchorMax = new Vector2(0.03f, 0.7f);
            dR.pivot = new Vector2(0, 0.5f);
            dR.sizeDelta = new Vector2(10, 10);
            var dotImg = dotObj.AddComponent<Image>();
            dotImg.color = online ? new Color(0.3f, 0.8f, 0.3f, 1f) : new Color(0.5f, 0.3f, 0.3f, 1f);
            dotImg.raycastTarget = false;

            // 名字 + 状态
            var nameObj = new GameObject("Name");
            nameObj.transform.SetParent(row.transform, false);
            var nR = nameObj.AddComponent<RectTransform>();
            nR.anchorMin = new Vector2(0.08f, 0); nR.anchorMax = new Vector2(0.65f, 1);
            nR.offsetMin = Vector2.zero; nR.offsetMax = Vector2.zero;
            var nTxt = nameObj.AddComponent<Text>();
            nTxt.text = $"{fName}  <size=12><color=#{(online ? "55FF55" : "888888")}>{(online ? "在线" : "离线")}</color></size>";
            nTxt.alignment = TextAnchor.MiddleLeft; nTxt.fontSize = 14;
            nTxt.color = UIHelper.TextPrimary; nTxt.font = font;
            nTxt.supportRichText = true; nTxt.raycastTarget = false;
        }

        if (_friendCountText != null)
            _friendCountText.text = count > 0 ? $"({count}/50)" : "";
    }

    [System.Serializable]
    private class HubFriendListResp { public HubFriendItem[] friends; public int count; public int max; }
    [System.Serializable]
    private class HubFriendItem { public int friendId; public string username; public int level; public int classType; public bool online; public string remark; }

    private void UpdateInfo()
    {
        var player = GameManager.Instance?.Player;
        if (player == null) return;
        var data = ClassData.GetClassData(player.HeroClass);

        TopNavBar.Instance?.RefreshCurrency();
        if (classText != null)
            classText.text = $"{data.ClassName}  Lv.{player.Stats.Level}  战力: {player.Stats.GearScore}";
        if (hpText != null)
            hpText.text = $"HP: {player.Stats.Hp}/{player.Stats.TotalMaxHp}";
        if (atkText != null)
            atkText.text = $"ATK: {player.Stats.BuffedAttack}  移速: {player.Stats.TotalMoveSpeed:F1}  暴击: {player.Stats.TotalCritChance*100:F0}%";
        if (defText != null)
            defText.text = $"DEF: {player.Stats.BuffedDefense}  吸血: {player.Stats.LifeSteal*100:F0}%  攻速: {player.Stats.TotalAttackSpeed:F1}x";

        var inv = player.Inventory;
        if (inv != null)
        {
            if (weaponText != null)
            {
                var weapon = inv.Equipped.Find(s => s.SlotType == EquipSlotType.Weapon);
                if (weapon != null && weapon.Item != null)
                {
                    string upgrade = weapon.Item.UpgradeLevel > 0 ? $" <color=#8C55C0>+{weapon.Item.UpgradeLevel}</color>" : "";
                    weaponText.text = $"武器: {weapon.Item.Name}{upgrade}";
                }
                else
                    weaponText.text = "武器: <color=#555>空</color>";
            }
            if (armorText != null)
            {
                var armor = inv.Equipped.Find(s => s.SlotType == EquipSlotType.Armor);
                if (armor != null && armor.Item != null)
                {
                    string upgrade = armor.Item.UpgradeLevel > 0 ? $" <color=#8C55C0>+{armor.Item.UpgradeLevel}</color>" : "";
                    armorText.text = $"护甲: {armor.Item.Name}{upgrade}";
                }
                else
                    armorText.text = "护甲: <color=#555>空</color>";
            }
            if (accessoryText != null)
            {
                var acc = inv.Equipped.Find(s => s.SlotType == EquipSlotType.Accessory);
                if (acc != null && acc.Item != null)
                {
                    string upgrade = acc.Item.UpgradeLevel > 0 ? $" <color=#8C55C0>+{acc.Item.UpgradeLevel}</color>" : "";
                    accessoryText.text = $"饰品: {acc.Item.Name}{upgrade}";
                }
                else
                    accessoryText.text = "饰品: <color=#555>空</color>";
            }
            if (backpackText != null)
                backpackText.text = $"背包: {inv.Backpack.Count}/{EquipmentInventory.MaxBackpackSize}";
        }
    }

    private void ShowOfflineRewardPopup(OfflineRewards.OfflineRewardInfo reward)
    {
        Font font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();

        var popup = new GameObject("OfflineRewardPopup");
        popup.transform.SetParent(canvas.transform, false);
        var pr = popup.AddComponent<RectTransform>();
        pr.anchorMin = new Vector2(0.3f, 0.3f); pr.anchorMax = new Vector2(0.7f, 0.7f);
        pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;

        var bg = popup.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.02f, 0.06f, 0.98f);

        var pc = popup.AddComponent<Canvas>();
        pc.overrideSorting = true; pc.sortingOrder = 20;
        popup.AddComponent<GraphicRaycaster>();

        // Title
        var titleObj = new GameObject("Title");
        titleObj.transform.SetParent(popup.transform, false);
        var tr = titleObj.AddComponent<RectTransform>();
        tr.anchorMin = new Vector2(0, 0.75f); tr.anchorMax = new Vector2(1, 1);
        tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
        var titleTxt = titleObj.AddComponent<Text>();
        titleTxt.text = "离线收益"; titleTxt.alignment = TextAnchor.MiddleCenter;
        titleTxt.fontSize = 28; titleTxt.color = UIHelper.Accent; titleTxt.font = font;

        // Info
        var infoObj = new GameObject("Info");
        infoObj.transform.SetParent(popup.transform, false);
        var ir = infoObj.AddComponent<RectTransform>();
        ir.anchorMin = new Vector2(0.1f, 0.35f); ir.anchorMax = new Vector2(0.9f, 0.72f);
        ir.offsetMin = Vector2.zero; ir.offsetMax = Vector2.zero;
        var infoTxt = infoObj.AddComponent<Text>();
        infoTxt.alignment = TextAnchor.MiddleCenter;
        infoTxt.fontSize = 20; infoTxt.color = UIHelper.TextPrimary; infoTxt.font = font;
        infoTxt.supportRichText = true;
        infoTxt.text = $"离线时长: <color=#88CCFF>{reward.hours:F1}小时</color>\n" +
                       $"每小时收益: <color=#FFD700>{reward.goldPerHour}金币</color>\n" +
                       $"<size=24><color=#FFD700>+{reward.goldReward} 金币</color></size>";

        // Claim button
        var btnObj = new GameObject("ClaimBtn");
        btnObj.transform.SetParent(popup.transform, false);
        var br = btnObj.AddComponent<RectTransform>();
        br.anchorMin = new Vector2(0.25f, 0.12f); br.anchorMax = new Vector2(0.75f, 0.30f);
        br.offsetMin = Vector2.zero; br.offsetMax = Vector2.zero;
        var btnImg = btnObj.AddComponent<Image>();
        btnImg.color = UIHelper.BtnConfirm;
        var btn = btnObj.AddComponent<Button>();
        var lbl = new GameObject("Lbl");
        lbl.transform.SetParent(btnObj.transform, false);
        var lr = lbl.AddComponent<RectTransform>();
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
        lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;
        var lblTxt = lbl.AddComponent<Text>();
        lblTxt.text = "领取"; lblTxt.alignment = TextAnchor.MiddleCenter;
        lblTxt.fontSize = 24; lblTxt.color = UIHelper.TextPrimary; lblTxt.font = font;
        lblTxt.raycastTarget = false;

        btn.onClick.AddListener(() =>
        {
            var player = GameManager.Instance?.Player;
            if (player != null) player.Stats.AddGold(reward.goldReward);
            OfflineRewards.ClaimReward();
            Destroy(popup);
        });
    }
}
