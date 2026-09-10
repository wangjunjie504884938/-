using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// GuildUI partial — Boss/Shop/War tab views
/// </summary>
public partial class GuildUI
{
    // ========================================================
    //  Tab 2: Boss挑战
    // ========================================================
    private void BuildBossTab()
    {
        // 每日Boss初始化
        string today = System.DateTime.Now.ToString("yyyyMMdd");
        if (GuildDataManager.Instance.NeedsDailyRefresh(today))
        {
            int initLv = Mathf.Max(1, GuildLevel);
            GuildDataManager.Instance.InitDailyBoss(today, initLv);
        }

        int bossHP = GuildDataManager.Instance.BossHP;
        int bossMaxHP = GuildDataManager.Instance.BossMaxHP;
        int myDamage = GuildDataManager.Instance.BossDamage;
        bool defeated = GuildDataManager.Instance.BossDefeated;
        int bossLv = GuildDataManager.Instance.BossLevel;
        string bossName = BossNames[(System.DateTime.Now.DayOfYear) % BossNames.Length];

        // Boss信息
        var bossInfo = new GameObject("BossInfo"); bossInfo.transform.SetParent(_tabContent.transform, false);
        var bir = bossInfo.AddComponent<RectTransform>();
        bir.anchorMin = new Vector2(0, 0.72f); bir.anchorMax = new Vector2(1, 1f); bir.offsetMin = Vector2.zero; bir.offsetMax = Vector2.zero;
        var biTxt = bossInfo.AddComponent<Text>();
        float hpPct = (float)bossHP / bossMaxHP;
        string hpBar = new string('█', (int)(hpPct * 20)) + new string('░', 20 - (int)(hpPct * 20));
        string status = defeated ? "<color=#FF4444>已击杀</color>" : $"<color=#44FF44>{bossHP}</color>/{bossMaxHP}";
        biTxt.text = $"{bossName} Lv.{bossLv}\n{hpBar}\nHP: {status}";
        biTxt.alignment = TextAnchor.MiddleCenter; biTxt.fontSize = 18; biTxt.color = UIHelper.Accent; biTxt.font = _font; biTxt.supportRichText = true;

        // 我的伤害
        var dmgObj = new GameObject("MyDamage"); dmgObj.transform.SetParent(_tabContent.transform, false);
        var drR = dmgObj.AddComponent<RectTransform>();
        drR.anchorMin = new Vector2(0, 0.62f); drR.anchorMax = new Vector2(1, 0.72f); drR.offsetMin = Vector2.zero; drR.offsetMax = Vector2.zero;
        var dmgTxt = dmgObj.AddComponent<Text>();
        dmgTxt.text = $"你的伤害: {myDamage:N0}";
        dmgTxt.alignment = TextAnchor.MiddleCenter; dmgTxt.fontSize = 16; dmgTxt.color = UIHelper.TextPrimary; dmgTxt.font = _font; dmgTxt.supportRichText = true;

        // 挑战按钮
        var challengeBtn = UIHelper.MakeButton(_tabContent.transform, "ChallengeBtn",
            defeated ? "已击杀 (领取奖励)" : "挑战Boss", _font,
            new Vector2(0.2f, 0.48f), new Vector2(0.8f, 0.60f), Vector2.zero, Vector2.zero,
            defeated ? UIHelper.BtnPrimary : new Color(0.5f, 0.1f, 0.1f, 0.95f), 20, () =>
        {
            if (defeated)
            {
                // 领取奖励
                int reward = 100 + bossLv * 50;
                var player = GameManager.Instance?.Player;
                if (player != null)
                {
                    player.Stats.AddGold(reward);
                    SeasonPass.AddXP(30);
                }
                GuildDataManager.Instance.MarkBossRewardClaimed();
                if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("Boss奖励", $"+{reward}  通行证XP+30");
                RefreshTabLast();
            }
            else
            {
                StartCoroutine(GuildBossBattle(bossName, bossLv, bossHP, bossMaxHP));
            }
        });

        // 伤害排行榜 (Mock + 玩家)
        var rankTitle = new GameObject("RankTitle"); rankTitle.transform.SetParent(_tabContent.transform, false);
        var rtR = rankTitle.AddComponent<RectTransform>();
        rtR.anchorMin = new Vector2(0, 0.38f); rtR.anchorMax = new Vector2(1, 0.46f); rtR.offsetMin = Vector2.zero; rtR.offsetMax = Vector2.zero;
        var rtTxt = rankTitle.AddComponent<Text>();
        rtTxt.text = "伤害排行榜";
        rtTxt.alignment = TextAnchor.MiddleCenter; rtTxt.fontSize = 16; rtTxt.color = UIHelper.TextSecondary; rtTxt.font = _font;

        // 生成Mock伤害
        var members = GetMembers();
        var rankList = new List<(string name, int dmg)>();
        rankList.Add(("你", myDamage));
        for (int i = 0; i < Mathf.Min(members.Count, 5); i++)
        {
            int mockDmg = Random.Range(5000, 30000) + members[i].level * 1000;
            rankList.Add((members[i].name, mockDmg));
        }
        rankList.Sort((a, b) => b.dmg.CompareTo(a.dmg));

        for (int i = 0; i < Mathf.Min(rankList.Count, 5); i++)
        {
            float y = 0.35f - i * 0.07f;
            var rowObj = new GameObject($"Rank_{i}"); rowObj.transform.SetParent(_tabContent.transform, false);
            var rrR = rowObj.AddComponent<RectTransform>();
            rrR.anchorMin = new Vector2(0.1f, y); rrR.anchorMax = new Vector2(0.9f, y + 0.06f); rrR.offsetMin = Vector2.zero; rrR.offsetMax = Vector2.zero;
            var rowTxt = rowObj.AddComponent<Text>();
            string medal = i == 0 ? "" : i == 1 ? "" : i == 2 ? "" : $"  {i+1}.";
            rowTxt.text = $"{medal} {rankList[i].name}  —  {rankList[i].dmg:N0}";
            rowTxt.alignment = TextAnchor.MiddleLeft; rowTxt.fontSize = 14; rowTxt.color = i == 0 ? UIHelper.Accent : UIHelper.TextPrimary; rowTxt.font = _font; rowTxt.supportRichText = true;
        }
    }

    private IEnumerator GuildBossBattle(string bossName, int bossLv, int bossHP, int bossMaxHP)
    {
        var player = GameManager.Instance?.Player;
        if (player == null) yield break;

        // 使用ArenaBattleUI进行战斗
        int myPower = player.Stats.GearScore + GuildLevel * 300;
        int myClass = (int)player.HeroClass;
        var bossGhost = new AsyncPvpUI.GhostData
        {
            playerName = bossName,
            classType = Random.Range(0, 3),
            level = bossLv + 10,
            gearScore = bossLv * 2000,
            wins = 999,
            losses = 0,
            defenseRating = 100
        };
        bool won = false;
        yield return ArenaBattleUI.StartBattle(GuildName, myPower, myClass, bossGhost, result => won = result);

        // 计算伤害
        int damage = won ? Random.Range(15000, 35000) + player.Stats.Level * 500 : Random.Range(5000, 15000) + player.Stats.Level * 200;
        bool wasDefeated = GuildDataManager.Instance.BossDefeated;
        GuildDataManager.Instance.DamageBoss(damage);
        bool justDefeated = !wasDefeated && GuildDataManager.Instance.BossDefeated;

        if (justDefeated)
        {
            int contribution = 50 + bossLv * 10;
            PlayerPrefs.SetInt(GuildData.KeyContribution, Contribution + contribution);
            PlayerPrefs.Save();
            if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("击杀Boss!", $"伤害:{damage:N0}  +{contribution}贡献点\n请领取奖励!");
        }
        else
        {
            int contribution = damage / 1000;
            PlayerPrefs.SetInt(GuildData.KeyContribution, Contribution + contribution);
            PlayerPrefs.Save();
            if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("Boss挑战", $"造成伤害:{damage:N0}  +{contribution}贡献点");
        }
        RefreshTabLast();
    }

    // ========================================================
    //  Tab 3: 公会商店
    // ========================================================
    private void BuildShopTab()
    {
        // 贡献点显示
        var infoObj = new GameObject("Info"); infoObj.transform.SetParent(_tabContent.transform, false);
        var ir = infoObj.AddComponent<RectTransform>();
        ir.anchorMin = new Vector2(0, 0.88f); ir.anchorMax = new Vector2(1, 1f); ir.offsetMin = Vector2.zero; ir.offsetMax = Vector2.zero;
        var info = infoObj.AddComponent<Text>();
        info.text = $" 贡献点: {Contribution}";
        info.alignment = TextAnchor.MiddleCenter; info.fontSize = 18; info.color = UIHelper.Accent; info.font = _font; info.supportRichText = true;

        // 商店物品列表
        for (int i = 0; i < ShopItems.Length; i++)
        {
            int idx = i;
            float y = 0.85f - (i + 1) * 0.12f;
            var card = UIHelper.MakeGlowCard(_tabContent.transform, $"Shop_{i}",
                new Vector2(0.05f, y), new Vector2(0.95f, y + 0.10f), Vector2.zero, Vector2.zero,
                new Color(0.06f, 0.04f, 0.08f, 0.92f), UIHelper.GlowBottom, UIHelper.BorderSubtle);

            var item = ShopItems[i];
            string rarityColor = item.rarity switch
            {
                3 => "<color=#FF8800>",
                2 => "<color=#AA44FF>",
                1 => "<color=#4488FF>",
                _ => "<color=#CCCCCC>",
            };

            var lblObj = new GameObject("Lbl"); lblObj.transform.SetParent(card.transform, false);
            var lr = lblObj.AddComponent<RectTransform>(); lr.anchorMin = new Vector2(0.05f, 0); lr.anchorMax = new Vector2(0.55f, 1); lr.offsetMin = new Vector2(8, 4); lr.offsetMax = new Vector2(-4, -4);
            var lbl = lblObj.AddComponent<Text>();
            lbl.text = $"{rarityColor}{item.name}</color>  {item.cost}";
            lbl.alignment = TextAnchor.MiddleLeft; lbl.fontSize = 15; lbl.color = UIHelper.TextPrimary; lbl.font = _font; lbl.supportRichText = true; lbl.raycastTarget = false;

            bool purchased = GuildDataManager.Instance.IsShopItemPurchased(idx);
            bool canAfford = Contribution >= item.cost && !purchased;
            var btn = UIHelper.MakeButton(card.transform, "BuyBtn", purchased ? "已兑换" : "兑换", _font,
                new Vector2(0.6f, 0.15f), new Vector2(0.95f, 0.85f), Vector2.zero, Vector2.zero,
                canAfford ? UIHelper.BtnConfirm : new Color(0.3f, 0.3f, 0.3f, 0.8f), 14, () => BuyShopItem(idx));
            var btnComp = btn.GetComponent<Button>();
            btnComp.interactable = canAfford;
        }

        // 每日限购提示
        var hintObj = new GameObject("Hint"); hintObj.transform.SetParent(_tabContent.transform, false);
        var hr = hintObj.AddComponent<RectTransform>();
        hr.anchorMin = new Vector2(0, 0.01f); hr.anchorMax = new Vector2(1, 0.05f); hr.offsetMin = Vector2.zero; hr.offsetMax = Vector2.zero;
        var hint = hintObj.AddComponent<Text>();
        hint.text = "每日重置购买限制 | 通过Boss挑战获取贡献点";
        hint.alignment = TextAnchor.MiddleCenter; hint.fontSize = 12; hint.color = UIHelper.TextDim; hint.font = _font;
    }

    private void BuyShopItem(int idx)
    {
        var item = ShopItems[idx];
        if (Contribution < item.cost)
        {
            if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("贡献点不足", $"需要{item.cost}贡献点");
            return;
        }

        PlayerPrefs.SetInt(GuildData.KeyContribution, Contribution - item.cost);

        var player = GameManager.Instance?.Player;
        if (player == null) return;

        switch (item.type)
        {
            case "equipment":
                var eqItem = EquipmentItem.GenerateRandom(item.value + 1); // rarity+1 as dungeonLevel
                if (!player.Inventory.AddToBackpack(eqItem))
                    LocalMailSystem.SendItemMail($"公会商店: {eqItem.Name}", $"背包已满，装备已发送至邮箱。\n{eqItem.GetStatSummary()}", eqItem);
                if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("兑换成功", $"{eqItem.Name}");
                break;
            case "gold":
                player.Stats.AddGold(item.value);
                if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("兑换成功", $"+{item.value}金币");
                break;
            case "skillpoint":
                player.Stats.SkillPoints += item.value;
                if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("兑换成功", $"+{item.value}技能点");
                break;
            case "fragment":
                int frag = PlayerPrefs.GetInt($"ARPG_S{PlayerProgressData.ActiveSlot}_DismantleFragments", 0) + item.value;
                PlayerPrefs.SetInt($"ARPG_S{PlayerProgressData.ActiveSlot}_DismantleFragments", frag);
                if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("兑换成功", $"+{item.value}碎片");
                break;
        }
        GuildDataManager.Instance.MarkShopItemPurchased(idx);
        PlayerPrefs.Save();
        RefreshTabLast();
    }

    // ========================================================
    //  公会战 (保留原功能)
    // ========================================================
    private IEnumerator GuildWarBattle(Font font)
    {
        string[] enemyNames = { "黑暗军团", "龙裔先锋", "深渊联盟", "圣光守卫", "风暴骑士团" };
        string enemy = enemyNames[Random.Range(0, enemyNames.Length)];
        int enemyPower = Random.Range(3000, 9000);
        var player = GameManager.Instance?.Player;
        if (player == null) yield break;
        int myPower = player.Stats.GearScore + GuildLevel * 500;
        int myClass = (int)player.HeroClass;
        var enemyGhost = new AsyncPvpUI.GhostData { playerName = enemy, classType = Random.Range(0, 3), level = Random.Range(15, 28), gearScore = enemyPower, wins = Random.Range(20, 80), losses = Random.Range(5, 30), defenseRating = Random.Range(50, 100) };
        bool won = false;
        yield return ArenaBattleUI.StartBattle(GuildName, myPower, myClass, enemyGhost, result => won = result);
        if (won)
        {
            int reward = 200 + GuildLevel * 50; player.Stats.AddGold(reward); SeasonPass.AddXP(50);
            PlayerPrefs.SetInt(GuildData.KeyGuildExp, GuildExp + 100);
            if (GuildExp + 100 >= GuildExpToNext) { PlayerPrefs.SetInt(GuildData.KeyGuildLevel, GuildLevel + 1); PlayerPrefs.SetInt(GuildData.KeyGuildExp, 0); }
            PlayerPrefs.Save();
            if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("公会战胜利!", $"+{reward} 通行证XP+50");
        }
        else
        {
            int consolation = 50; player.Stats.AddGold(consolation); PlayerPrefs.Save();
            if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("公会战失败", $"+{consolation} (参与奖)");
        }
        Show();
    }

    // ========================================================
}
