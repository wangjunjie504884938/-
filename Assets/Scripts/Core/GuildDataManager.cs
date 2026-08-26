using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 公会数据管理器 — 缓存公会Boss/商店数据到内存，减少PlayerPrefs读写
/// 后续可扩展为服务器同步
/// </summary>
public class GuildDataManager : MonoBehaviour
{
    public static GuildDataManager Instance { get; private set; }

    // Boss数据缓存
    public string BossDate { get; private set; } = "";
    public int BossHP { get; private set; }
    public int BossMaxHP { get; private set; }
    public int BossDamage { get; private set; }
    public bool BossDefeated { get; private set; }
    public int BossLevel { get; private set; }

    // 商店购买记录缓存
    private HashSet<int> _purchasedItems = new HashSet<int>();

    // 数据键
    private const string KeyBossDate = "ARPG_GuildBossDate";
    private const string KeyBossHP = "ARPG_GuildBossHP";
    private const string KeyBossMaxHP = "ARPG_GuildBossMaxHP";
    private const string KeyBossDamage = "ARPG_GuildBossDamage";
    private const string KeyBossDefeated = "ARPG_GuildBossDefeated";
    private const string KeyBossLevel = "ARPG_GuildBossLevel";
    private const string KeyShopPurchased = "ARPG_GuildShopPurchased";

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        LoadFromPrefs();
    }

    /// <summary>从PlayerPrefs加载到内存</summary>
    public void LoadFromPrefs()
    {
        BossDate = PlayerPrefs.GetString(KeyBossDate, "");
        BossHP = PlayerPrefs.GetInt(KeyBossHP, 0);
        BossMaxHP = PlayerPrefs.GetInt(KeyBossMaxHP, 0);
        BossDamage = PlayerPrefs.GetInt(KeyBossDamage, 0);
        BossDefeated = PlayerPrefs.GetInt(KeyBossDefeated, 0) == 1;
        BossLevel = PlayerPrefs.GetInt(KeyBossLevel, 1);

        // 加载购买记录
        _purchasedItems.Clear();
        string json = PlayerPrefs.GetString(KeyShopPurchased, "");
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                var arr = JsonUtility.FromJson<IntArray>(json);
                if (arr?.items != null)
                    foreach (var id in arr.items)
                        _purchasedItems.Add(id);
            }
            catch { }
        }
    }

    /// <summary>保存到PlayerPrefs</summary>
    public void SaveToPrefs()
    {
        PlayerPrefs.SetString(KeyBossDate, BossDate);
        PlayerPrefs.SetInt(KeyBossHP, BossHP);
        PlayerPrefs.SetInt(KeyBossMaxHP, BossMaxHP);
        PlayerPrefs.SetInt(KeyBossDamage, BossDamage);
        PlayerPrefs.SetInt(KeyBossDefeated, BossDefeated ? 1 : 0);
        PlayerPrefs.SetInt(KeyBossLevel, BossLevel);
        PlayerPrefs.SetString(KeyShopPurchased, JsonUtility.ToJson(new IntArray { items = new List<int>(_purchasedItems).ToArray() }));
        PlayerPrefs.Save();
    }

    /// <summary>初始化每日Boss</summary>
    public void InitDailyBoss(string date, int level)
    {
        BossDate = date;
        int hp = 50000 + level * 10000;
        BossHP = hp;
        BossMaxHP = hp;
        BossDamage = 0;
        BossDefeated = false;
        BossLevel = level;
        SaveToPrefs();
    }

    /// <summary>对Boss造成伤害</summary>
    public void DamageBoss(int damage)
    {
        BossDamage += damage;
        BossHP = Mathf.Max(0, BossHP - damage);
        if (BossHP <= 0 && !BossDefeated)
            BossDefeated = true;
        SaveToPrefs();
    }

    /// <summary>标记Boss奖励已领取</summary>
    public void MarkBossRewardClaimed()
    {
        PlayerPrefs.SetInt(KeyBossDefeated, 2);
        BossDefeated = true;
        PlayerPrefs.Save();
    }

    /// <summary>检查商店物品是否已购买</summary>
    public bool IsShopItemPurchased(int itemId) => _purchasedItems.Contains(itemId);

    /// <summary>标记商店物品已购买</summary>
    public void MarkShopItemPurchased(int itemId)
    {
        _purchasedItems.Add(itemId);
        SaveToPrefs();
    }

    /// <summary>检查是否需要刷新每日Boss</summary>
    public bool NeedsDailyRefresh(string today) => BossDate != today;

    [System.Serializable]
    private class IntArray { public int[] items; }
}
