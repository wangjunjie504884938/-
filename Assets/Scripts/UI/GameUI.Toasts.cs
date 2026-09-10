using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// GameUI partial — Toast notifications (achievement + item pickup)
/// </summary>
public partial class GameUI
{
    public void ShowAchievementToast(string name, string description)
    {
        Font font = GameManager.GetUIFont();
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        GameObject toast = new GameObject("AchievementToast");
        toast.transform.SetParent(canvas.transform, false);
        RectTransform tRect = toast.AddComponent<RectTransform>();
        tRect.anchorMin = new Vector2(0.5f, 1); tRect.anchorMax = new Vector2(0.5f, 1);
        tRect.pivot = new Vector2(0.5f, 1); tRect.anchoredPosition = new Vector2(0, -80);
        tRect.sizeDelta = new Vector2(350, 70);
        Image tBg = toast.AddComponent<Image>();
        tBg.color = new Color(0.1f, 0.1f, 0.15f, 0.9f);

        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(toast.transform, false);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 0.5f); titleRect.anchorMax = new Vector2(1, 0.5f);
        titleRect.offsetMin = new Vector2(15, 5); titleRect.offsetMax = new Vector2(-15, 18);
        Text titleText = titleObj.AddComponent<Text>();
        titleText.text = $"{name}"; titleText.alignment = TextAnchor.MiddleCenter;
        titleText.fontSize = 20; titleText.color = new Color(1f, 0.85f, 0.2f); titleText.font = font;

        GameObject descObj = new GameObject("Desc");
        descObj.transform.SetParent(toast.transform, false);
        RectTransform dRect = descObj.AddComponent<RectTransform>();
        dRect.anchorMin = new Vector2(0, 0.5f); dRect.anchorMax = new Vector2(1, 0.5f);
        dRect.offsetMin = new Vector2(15, -18); dRect.offsetMax = new Vector2(-15, -3);
        Text descText = descObj.AddComponent<Text>();
        descText.text = description; descText.alignment = TextAnchor.MiddleCenter;
        descText.fontSize = 14; descText.color = Color.white; descText.font = font;

        // Auto-destroy after 3s
        Destroy(toast, 3f);
    }

    private static int _itemToastCount;

    public void ShowItemPickupToast(string title, string desc)
    {
        Font font = GameManager.GetUIFont();
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        int offsetIdx = _itemToastCount;
        _itemToastCount++;

        GameObject toast = new GameObject("ItemPickupToast");
        toast.transform.SetParent(canvas.transform, false);
        RectTransform tRect = toast.AddComponent<RectTransform>();
        tRect.anchorMin = new Vector2(0.5f, 0); tRect.anchorMax = new Vector2(0.5f, 0);
        tRect.pivot = new Vector2(0.5f, 0);
        tRect.anchoredPosition = new Vector2(0, 30 + offsetIdx * 55);
        tRect.sizeDelta = new Vector3(300, 50);

        Image tBg = toast.AddComponent<Image>();
        tBg.color = new Color(0.08f, 0.08f, 0.12f, 0.92f);

        // Left accent bar
        GameObject bar = new GameObject("Bar");
        bar.transform.SetParent(toast.transform, false);
        RectTransform bRect = bar.AddComponent<RectTransform>();
        bRect.anchorMin = new Vector2(0, 0); bRect.anchorMax = new Vector2(0, 1);
        bRect.pivot = new Vector2(0, 0.5f);
        bRect.sizeDelta = new Vector2(4, 0);
        Image bImg = bar.AddComponent<Image>();
        bImg.color = new Color(1f, 0.85f, 0.2f);

        // Title
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(toast.transform, false);
        RectTransform tr = titleObj.AddComponent<RectTransform>();
        tr.anchorMin = new Vector2(0, 0.4f); tr.anchorMax = new Vector2(1, 1);
        tr.offsetMin = new Vector2(12, 0); tr.offsetMax = new Vector2(-8, -2);
        Text titleText = titleObj.AddComponent<Text>();
        titleText.text = $"+ {title}"; titleText.alignment = TextAnchor.MiddleLeft;
        titleText.fontSize = 16; titleText.color = new Color(1f, 0.95f, 0.7f);
        titleText.font = font; titleText.supportRichText = true;

        // Description
        if (!string.IsNullOrEmpty(desc))
        {
            GameObject descObj = new GameObject("Desc");
            descObj.transform.SetParent(toast.transform, false);
            RectTransform dr = descObj.AddComponent<RectTransform>();
            dr.anchorMin = new Vector2(0, 0); dr.anchorMax = new Vector2(1, 0.45f);
            dr.offsetMin = new Vector2(12, 2); dr.offsetMax = new Vector2(-8, 0);
            Text descText = descObj.AddComponent<Text>();
            descText.text = desc; descText.alignment = TextAnchor.MiddleLeft;
            descText.fontSize = 12; descText.color = UIHelper.TextSecondary;
            descText.font = font;
        }

        // Fade out and clean up
        StartCoroutine(FadeItemToast(toast, 2.5f));
    }

    private IEnumerator FadeItemToast(GameObject toast, float delay)
    {
        yield return new WaitForSeconds(delay);
        _itemToastCount = Mathf.Max(0, _itemToastCount - 1);
        if (toast != null) Destroy(toast);
    }

    /// <summary>清理所有残留的toast提示</summary>
    private void ClearAllToasts()
    {
        _itemToastCount = 0;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        for (int i = canvas.transform.childCount - 1; i >= 0; i--)
        {
            var child = canvas.transform.GetChild(i);
            if (child.name == "ItemPickupToast")
                Destroy(child.gameObject);
        }
    }
}
