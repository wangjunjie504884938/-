using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// DNF风格Boss掉落动画 — 装备从天而降, 显示名称和稀有度, 持续3秒后消失
/// </summary>
public class BossDropAnimation : MonoBehaviour
{
    private int _myStackIdx;

    /// <summary>显示Boss掉落装备动画 (支持多件错开排列)</summary>
    public static void ShowDrop(Vector3 worldPos, EquipmentItem item)
    {
        int idx = _activeCount++;
        var go = new GameObject("BossDropAnim");
        var anim = go.AddComponent<BossDropAnimation>();
        SceneRegistry.Register(anim);
        anim._myStackIdx = idx;
        anim.StartCoroutine(anim.AnimateDrop(worldPos, item, idx));
    }

    private static int _activeCount = 0;

    private void OnDestroy()
    {
        _activeCount = Mathf.Max(0, _activeCount - 1);
        SceneRegistry.Unregister(this);
    }

    private IEnumerator AnimateDrop(Vector3 worldPos, EquipmentItem item, int stackIdx)
    {
        Font font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();
        Color rarityColor = ItemData.GetRarityColor(item.Rarity);

        // 主面板 — 按stackIdx垂直错开, 避免重叠
        var panel = new GameObject("DropPanel");
        panel.transform.SetParent(canvas.transform, false);
        var pr = panel.AddComponent<RectTransform>();
        pr.anchorMin = new Vector2(0.5f, 0.5f);
        pr.anchorMax = new Vector2(0.5f, 0.5f);
        pr.pivot = new Vector2(0.5f, 0.5f);
        // 错开: 每件向下偏移220px, 最多3列后换行
        float colOffset = (stackIdx % 3 - 1) * 420f;
        float rowOffset = -(stackIdx / 3) * 230f;
        pr.anchoredPosition = new Vector2(colOffset, rowOffset);
        pr.sizeDelta = new Vector2(380, 200);
        var cg = panel.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        // 背景
        var bg = panel.AddComponent<Image>();
        bg.color = new Color(0.02f, 0.02f, 0.05f, 0.95f);
        bg.raycastTarget = false;

        // 稀有度光效边框
        var glow = new GameObject("Glow");
        glow.transform.SetParent(panel.transform, false);
        var gr = glow.AddComponent<RectTransform>();
        gr.anchorMin = Vector2.zero; gr.anchorMax = Vector2.one;
        gr.offsetMin = new Vector2(-3, -3); gr.offsetMax = new Vector3(3, 3);
        var glowImg = glow.AddComponent<Image>();
        glowImg.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.6f);
        glowImg.raycastTarget = false;

        // 装备图标 (宝石形状)
        var iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(panel.transform, false);
        var ir = iconObj.AddComponent<RectTransform>();
        ir.anchorMin = new Vector2(0.5f, 0.7f); ir.anchorMax = new Vector2(0.5f, 0.7f);
        ir.pivot = new Vector2(0.5f, 0.5f);
        ir.anchoredPosition = Vector2.zero;
        ir.sizeDelta = new Vector2(60, 60);
        var iconSr = iconObj.AddComponent<Image>();
        iconSr.sprite = SpriteCache.WhitePixel;
        iconSr.color = rarityColor;
        iconSr.raycastTarget = false;

        // 稀有度标签
        var rarityObj = new GameObject("RarityLabel");
        rarityObj.transform.SetParent(panel.transform, false);
        var rr = rarityObj.AddComponent<RectTransform>();
        rr.anchorMin = new Vector2(0.5f, 0.55f); rr.anchorMax = new Vector2(0.5f, 0.55f);
        rr.pivot = new Vector2(0.5f, 0.5f);
        rr.sizeDelta = new Vector2(380, 25);
        var rarityTxt = rarityObj.AddComponent<Text>();
        rarityTxt.text = item.Rarity switch
        {
            ItemRarity.Legendary => "★ 传  说 ★",
            ItemRarity.Epic => "◆ 史  诗 ◆",
            ItemRarity.Rare => "◇ 稀  有 ◇",
            _ => "普  通",
        };
        rarityTxt.alignment = TextAnchor.MiddleCenter;
        rarityTxt.fontSize = 18;
        rarityTxt.color = rarityColor;
        rarityTxt.font = font;
        rarityTxt.raycastTarget = false;

        // 装备名称
        var nameObj = new GameObject("ItemName");
        nameObj.transform.SetParent(panel.transform, false);
        var nr = nameObj.AddComponent<RectTransform>();
        nr.anchorMin = new Vector2(0.5f, 0.4f); nr.anchorMax = new Vector2(0.5f, 0.4f);
        nr.pivot = new Vector2(0.5f, 0.5f);
        nr.sizeDelta = new Vector2(380, 30);
        var nameTxt = nameObj.AddComponent<Text>();
        nameTxt.text = item.Name;
        nameTxt.alignment = TextAnchor.MiddleCenter;
        nameTxt.fontSize = 24;
        nameTxt.color = Color.white;
        nameTxt.font = font;
        nameTxt.raycastTarget = false;

        // 属性摘要
        var statObj = new GameObject("Stats");
        statObj.transform.SetParent(panel.transform, false);
        var sr2 = statObj.AddComponent<RectTransform>();
        sr2.anchorMin = new Vector2(0.05f, 0.05f); sr2.anchorMax = new Vector2(0.95f, 0.35f);
        sr2.offsetMin = Vector2.zero; sr2.offsetMax = Vector2.zero;
        var statTxt = statObj.AddComponent<Text>();
        statTxt.text = item.GetStatSummary();
        statTxt.alignment = TextAnchor.MiddleCenter;
        statTxt.fontSize = 16;
        statTxt.color = new Color(0.8f, 0.8f, 0.85f);
        statTxt.font = font;
        statTxt.raycastTarget = false;

        // 词缀
        if (item.Affixes != null && item.Affixes.Count > 0)
        {
            var affixObj = new GameObject("Affixes");
            affixObj.transform.SetParent(panel.transform, false);
            var ar2 = affixObj.AddComponent<RectTransform>();
            ar2.anchorMin = new Vector2(0.05f, 0.0f); ar2.anchorMax = new Vector2(0.95f, 0.08f);
            ar2.offsetMin = Vector2.zero; ar2.offsetMax = Vector2.zero;
            var affixTxt = affixObj.AddComponent<Text>();
            affixTxt.text = item.GetAffixDisplayString();
            affixTxt.alignment = TextAnchor.MiddleCenter;
            affixTxt.fontSize = 14;
            affixTxt.color = new Color(0.6f, 0.9f, 0.6f);
            affixTxt.font = font;
            affixTxt.raycastTarget = false;
        }

        // 动画: 从上方落下 + 淡入
        Vector2 startPos = new Vector2(0, 300f);
        Vector2 endPos = Vector2.zero;
        float elapsed = 0f;
        float dropDuration = 0.5f;

        // 淡入 + 下落
        while (elapsed < dropDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / dropDuration);
            // 弹性缓动
            float easeT = 1f - Mathf.Pow(1f - t, 3f);
            cg.alpha = t;
            pr.anchoredPosition = Vector2.Lerp(startPos, endPos, easeT);
            yield return null;
        }
        cg.alpha = 1f;
        pr.anchoredPosition = endPos;

        // 闪烁脉冲效果
        float holdTime = 2.5f;
        elapsed = 0f;
        while (elapsed < holdTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float pulse = 1f + Mathf.Sin(elapsed * 6f) * 0.05f;
            pr.localScale = new Vector3(pulse, pulse, 1f);
            // 边框闪烁
            float glowAlpha = 0.4f + Mathf.Sin(elapsed * 8f) * 0.2f;
            glowImg.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, glowAlpha);
            yield return null;
        }

        // 淡出
        float fadeDuration = 0.4f;
        elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = 1f - Mathf.Clamp01(elapsed / fadeDuration);
            cg.alpha = t;
            pr.localScale = Vector3.one * (1f + (1f - t) * 0.1f);
            yield return null;
        }

        Destroy(panel);
        Destroy(gameObject);
    }
}
