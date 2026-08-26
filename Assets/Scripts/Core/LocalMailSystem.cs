using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 本地邮件系统 — 离线时存储溢出物品到本地"邮箱"
/// 使用唯一ID定位邮件, 避免索引偏移问题
/// </summary>
public static class LocalMailSystem
{
    private const string MailKey = "ARPG_LocalMails";
    // _idCounter removed — IDs are generated dynamically in SendItemMail

    [System.Serializable]
    public class LocalMail
    {
        public int id;
        public string title;
        public string content;
        public int gold;
        public string itemJson;
        public bool claimed;
        public string createdDate;
    }

    [System.Serializable]
    private class MailList { public List<LocalMail> mails = new List<LocalMail>(); }

    /// <summary>添加一封本地邮件 (装备附件)</summary>
    public static void SendItemMail(string title, string content, EquipmentItem item)
    {
        var mails = LoadMails();
        // 生成唯一ID
        int maxId = 0;
        foreach (var m in mails) if (m.id > maxId) maxId = m.id;
        int newId = maxId + 1;

        mails.Add(new LocalMail
        {
            id = newId,
            title = title,
            content = content,
            gold = 0,
            itemJson = JsonUtility.ToJson(new EquipmentItemData(item)),
            claimed = false,
            createdDate = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        });

        // 限制存储数量, 删除最早的未领取邮件
        mails.RemoveAll(m => m.claimed);
        if (mails.Count > 30)
            mails.RemoveRange(0, mails.Count - 30);

        SaveMails(mails);
                    GameLog.Log($"[LocalMail] 已发送本地邮件 #{newId}: {title}");
    }

    /// <summary>获取所有未领取的本地邮件 (最多返回最近20封)</summary>
    public static List<LocalMail> GetMails()
    {
        var all = LoadMails().FindAll(m => !m.claimed);
        if (all.Count > 20)
            all = all.GetRange(all.Count - 20, 20);
        return all;
    }

    /// <summary>获取所有邮件 (含已领取, 供UI显示)</summary>
    public static List<LocalMail> GetAllMails()
    {
        return LoadMails();
    }

    /// <summary>通过ID领取邮件附件, 返回true表示成功</summary>
    public static bool ClaimMailById(int mailId)
    {
        var mails = LoadMails();
        int idx = mails.FindIndex(m => m.id == mailId);
        if (idx < 0) return false;
        if (mails[idx].claimed) return false;

        var mail = mails[idx];

        // Give gold
        if (mail.gold > 0)
        {
            var player = GameManager.Instance?.Player;
            if (player != null) player.Stats.AddGold(mail.gold);
        }

        // Give item — check backpack space first
        if (!string.IsNullOrEmpty(mail.itemJson))
        {
            var itemData = JsonUtility.FromJson<EquipmentItemData>(mail.itemJson);
            var item = itemData.ToItem();
            if (item != null)
            {
                var player = GameManager.Instance?.Player;
                if (player != null)
                {
                    bool added = player.Inventory.AddToBackpack(item);
                    if (!added)
                    {
                        // 背包满了 — 不标记为已领取
                        return false;
                    }
                }
            }
        }

        mails[idx].claimed = true;
        SaveMails(mails);
        CleanClaimed();
        return true;
    }

    /// <summary>删除已领取的邮件 (清理, 保留最近20封)</summary>
    public static void CleanClaimed()
    {
        var mails = LoadMails();
        mails.RemoveAll(m => m.claimed);
        if (mails.Count > 50)
            mails.RemoveRange(0, mails.Count - 50);
        SaveMails(mails);
    }

    /// <summary>通过ID删除本地邮件</summary>
    public static bool DeleteMailById(int mailId)
    {
        var mails = LoadMails();
        int idx = mails.FindIndex(m => m.id == mailId);
        if (idx < 0) return false;
        mails.RemoveAt(idx);
        SaveMails(mails);
        return true;
    }

    /// <summary>通过ID列表批量领取本地邮件</summary>
    public static int ClaimByIds(List<int> mailIds)
    {
        var mails = LoadMails();
        int claimed = 0;
        foreach (var mail in mails)
        {
            if (mail.claimed || !mailIds.Contains(mail.id)) continue;

            if (mail.gold > 0)
            {
                var player = GameManager.Instance?.Player;
                if (player != null) player.Stats.AddGold(mail.gold);
            }

            if (!string.IsNullOrEmpty(mail.itemJson))
            {
                var itemData = JsonUtility.FromJson<EquipmentItemData>(mail.itemJson);
                var item = itemData.ToItem();
                if (item != null)
                {
                    var player = GameManager.Instance?.Player;
                    if (player != null && !player.Inventory.AddToBackpack(item))
                        continue; // 背包满, 跳过这封
                }
            }

            mail.claimed = true;
            claimed++;
        }
        SaveMails(mails);
        CleanClaimed();
        return claimed;
    }

    /// <summary>通过ID列表批量删除本地邮件</summary>
    public static int DeleteByIds(List<int> mailIds)
    {
        var mails = LoadMails();
        int removed = mails.RemoveAll(m => mailIds.Contains(m.id));
        SaveMails(mails);
        return removed;
    }

    /// <summary>获取需要同步到服务器的未领取邮件 (用于跨设备同步)</summary>
    public static List<LocalMail> GetUnsyncedMails()
    {
        return LoadMails().FindAll(m => !m.claimed);
    }

    /// <summary>标记邮件已同步到服务器</summary>
    public static void MarkSynced(int mailId)
    {
        var mails = LoadMails();
        int idx = mails.FindIndex(m => m.id == mailId);
        if (idx >= 0)
        {
            // 同步后删除本地邮件 (服务器已有副本)
            mails.RemoveAt(idx);
            SaveMails(mails);
        }
    }

    /// <summary>一键领取所有本地邮件</summary>
    public static int ClaimAll()
    {
        var mails = LoadMails();
        int claimed = 0;
        foreach (var mail in mails)
        {
            if (mail.claimed) continue;

            // Give gold
            if (mail.gold > 0)
            {
                var player = GameManager.Instance?.Player;
                if (player != null) player.Stats.AddGold(mail.gold);
            }

            // Give item
            if (!string.IsNullOrEmpty(mail.itemJson))
            {
                var itemData = JsonUtility.FromJson<EquipmentItemData>(mail.itemJson);
                var item = itemData.ToItem();
                if (item != null)
                {
                    var player = GameManager.Instance?.Player;
                    if (player != null)
                    {
                        if (!player.Inventory.AddToBackpack(item))
                            break; // 背包满了, 停止领取
                    }
                }
            }

            mail.claimed = true;
            claimed++;
        }
        SaveMails(mails);
        CleanClaimed();
        return claimed;
    }

    /// <summary>获取未领取邮件数量</summary>
    public static int UnclaimedCount => GetMails().Count;

    private static List<LocalMail> LoadMails()
    {
        string json = PlayerPrefs.GetString(MailKey, "");
        if (string.IsNullOrEmpty(json)) return new List<LocalMail>();
        var list = JsonUtility.FromJson<MailList>(json);
        return list?.mails ?? new List<LocalMail>();
    }

    private static void SaveMails(List<LocalMail> mails)
    {
        var list = new MailList { mails = mails };
        PlayerPrefs.SetString(MailKey, JsonUtility.ToJson(list));
        PlayerPrefs.Save();
    }
}
