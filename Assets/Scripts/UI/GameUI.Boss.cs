using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GameUI partial — Boss health bar panel
/// </summary>
public partial class GameUI
{
    public void BindBossHealthPanel(Canvas canvas)
    {
        if (BossHealthPanel != null || canvas == null) return;

        Transform panel = FindChildRecursive(canvas.transform, "BossHealthPanel");
        if (panel == null)
        {
            GameObject prefab = Resources.Load<GameObject>(ResourcePaths.BossHealthPanel);
            if (prefab != null)
                panel = Instantiate(prefab, canvas.transform, false).transform;
        }

        if (panel == null) return;

        BossHealthPanel = panel.gameObject;
        BossHealthBar = FindChildRecursive(panel, "BossHpBar")?.GetComponent<Slider>() ??
                        panel.GetComponentInChildren<Slider>(true);
        BossNameText = FindChildRecursive(panel, "BossName")?.GetComponent<Text>();
        BossHpText = FindChildRecursive(panel, "BossHpText")?.GetComponent<Text>();
        BossHealthPanel.SetActive(false);
    }

    public void SetBossTarget(EnemyController boss, string bossName)
    {
        bossTarget = boss;
        currentBossName = bossName;
        if (BossNameText != null) BossNameText.text = bossName;
        if (BossHealthPanel != null) BossHealthPanel.SetActive(boss != null);
        if (boss != null) AudioManager.Instance?.PlayBGM(AudioManager.BGMType.Boss);
        UpdateBossHealthBar();
    }

    public void ClearBossTarget(EnemyController boss)
    {
        if (bossTarget != boss) return;
        string defeatedName = currentBossName;
        bossTarget = null;
        if (BossHealthPanel != null) BossHealthPanel.SetActive(false);
        AudioManager.Instance?.PlayBGM(AudioManager.BGMType.Battle);
        ShowItemPickupToast($"{defeatedName} 被击败!", "");
    }

    private void UpdateBossHealthBar()
    {
        if (BossHealthPanel == null) return;

        if (bossTarget == null || bossTarget.IsDead || !GameManager.Instance.IsInDungeon)
        {
            BossHealthPanel.SetActive(false);
            return;
        }

        BossHealthPanel.SetActive(true);

        int maxHp = Mathf.Max(1, bossTarget.MaxHp);
        int hp = Mathf.Clamp(bossTarget.CurrentHp, 0, maxHp);
        if (BossHealthBar != null)
        {
            BossHealthBar.maxValue = maxHp;
            BossHealthBar.value = hp;
        }
        if (BossNameText != null)
            BossNameText.text = string.IsNullOrEmpty(currentBossName) ? "Boss" : currentBossName;
        if (BossHpText != null)
        {
            var sb = UIStringBuilderPool.Get();
            sb.Append(hp).Append('/').Append(maxHp);
            BossHpText.text = sb.ToString();
        }
    }

    private Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null) return null;
        foreach (Transform child in root)
        {
            if (child.name == childName) return child;
            Transform found = FindChildRecursive(child, childName);
            if (found != null) return found;
        }
        return null;
    }
}
