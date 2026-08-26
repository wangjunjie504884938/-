using UnityEngine;
using UnityEngine.EventSystems;
using System;

/// <summary>
/// Touch-friendly skill button. Reports press state via IsPressed.
/// Fires OnPressedCallback on pointer down for immediate response.
/// </summary>
public class SkillButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public bool IsPressed { get; private set; }
    public Action OnPressedCallback;

    public void OnPointerDown(PointerEventData eventData)
    {
        IsPressed = true;
        OnPressedCallback?.Invoke();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        IsPressed = false;
    }
}
