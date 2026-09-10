using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// CharacterSlotUI partial — Delete confirmation dialog
/// </summary>
public partial class CharacterSlotUI
{
    // ========== 删除确认弹窗 ==========

    private void ShowDeleteConfirm(int slot, Font font)
    {
        if (_confirmDialog != null) Destroy(_confirmDialog);

        _confirmDialog = new GameObject("DeleteConfirmDialog");
        _confirmDialog.transform.SetParent(panel.transform, false);
        RectTransform dialogRect = _confirmDialog.AddComponent<RectTransform>();
        dialogRect.anchorMin = Vector2.zero; dialogRect.anchorMax = Vector2.one;
        dialogRect.offsetMin = Vector2.zero; dialogRect.offsetMax = Vector2.zero;

        Image backdrop = _confirmDialog.AddComponent<Image>();
        backdrop.color = new Color(0, 0, 0, 0.6f);

        GameObject boxObj = new GameObject("DialogBox");
        boxObj.transform.SetParent(_confirmDialog.transform, false);
        RectTransform boxRect = boxObj.AddComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0.5f, 0.5f); boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        boxRect.pivot = new Vector2(0.5f, 0.5f);
        boxRect.sizeDelta = new Vector2(400, 220);

        Image boxBg = boxObj.AddComponent<Image>();
        boxBg.color = new Color(0.08f, 0.06f, 0.10f, 0.98f);

        GameObject borderObj = new GameObject("Border");
        borderObj.transform.SetParent(boxObj.transform, false);
        RectTransform bRect = borderObj.AddComponent<RectTransform>();
        bRect.anchorMin = Vector2.zero; bRect.anchorMax = Vector2.one;
        bRect.offsetMin = new Vector2(-2, -2); bRect.offsetMax = new Vector2(2, 2);
        Image bImg = borderObj.AddComponent<Image>();
        bImg.color = new Color(0.6f, 0.15f, 0.1f, 0.8f);
        bImg.raycastTarget = false;

        GameObject innerObj = new GameObject("Inner");
        innerObj.transform.SetParent(borderObj.transform, false);
        RectTransform iRect = innerObj.AddComponent<RectTransform>();
        iRect.anchorMin = Vector2.zero; iRect.anchorMax = Vector2.one;
        iRect.offsetMin = new Vector2(2, 2); iRect.offsetMax = new Vector2(-2, -2);
        Image iImg = innerObj.AddComponent<Image>();
        iImg.color = new Color(0.08f, 0.06f, 0.10f, 1f);
        iImg.raycastTarget = false;

        CreateTextInBox(boxObj.transform, "Title", "确认删除", font, 24,
            new Color(1f, 0.5f, 0.4f), new Vector2(0, -20), new Vector2(360, 36));
        CreateTextInBox(boxObj.transform, "Warning", "该角色将被永久删除\n此操作无法撤销!", font, 18,
            new Color(0.85f, 0.8f, 0.75f), new Vector2(0, -65), new Vector2(360, 50));

        CreateActionButton(boxObj.transform, "确认删除", font,
            new Vector2(-80, -145), new Vector2(140, 40),
            new Color(0.4f, 0.08f, 0.08f, 0.95f),
            new Color(0.7f, 0.2f, 0.15f, 0.8f),
            () => { CloseConfirmDialog(); OnDeleteSlotConfirmed(slot); });

        CreateActionButton(boxObj.transform, "取消", font,
            new Vector2(80, -145), new Vector2(140, 40),
            new Color(0.15f, 0.15f, 0.20f, 0.95f),
            new Color(0.35f, 0.35f, 0.40f, 0.6f),
            () => CloseConfirmDialog());

        boxObj.transform.localScale = Vector3.zero;
        StartCoroutine(AnimateDialogEntrance(boxObj));
    }

    private void CloseConfirmDialog()
    {
        if (_confirmDialog != null) Destroy(_confirmDialog);
        _confirmDialog = null;
    }

    private void OnDeleteSlotConfirmed(int slot)
    {
        // Always clear local PlayerPrefs data for this slot
        PlayerProgressData.SetActiveSlot(slot);
        PlayerProgressData.ClearSave();

        if (CloudSaveManager.Instance != null && CloudSaveManager.Instance.IsLoggedIn)
        {
            if (_statusText != null)
            {
                _statusText.text = "正在删除角色...";
                _statusText.color = UIHelper.Accent;
            }
            CloudSaveManager.Instance.DeleteSave(slot,
                () =>
                {
                    if (_statusText != null)
                    {
                        _statusText.text = "角色已删除";
                        _statusText.color = new Color(0.3f, 1f, 0.5f);
                    }
                    _selectedSlot = -1;
                    RefreshSlots();
                },
                (err) =>
                {
                    // Server delete failed, but local data already cleared
                    if (_statusText != null)
                    {
                        _statusText.text = $"本地已删除 (服务器同步失败)";
                        _statusText.color = new Color(1f, 0.7f, 0.3f);
                    }
                    _selectedSlot = -1;
                    RefreshSlots();
                });
        }
        else
        {
            if (_statusText != null)
            {
                _statusText.text = "角色已删除";
                _statusText.color = new Color(0.3f, 1f, 0.5f);
            }
            _selectedSlot = -1;
            RefreshSlots();
        }
    }

    private IEnumerator AnimateDialogEntrance(GameObject dialog)
    {
        float duration = 0.2f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duration);
            float s = 1f - Mathf.Pow(1f - p, 3f);
            dialog.transform.localScale = Vector3.one * s;
            yield return null;
        }
        dialog.transform.localScale = Vector3.one;
    }

}
