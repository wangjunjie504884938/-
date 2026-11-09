using UnityEngine;
using UnityEngine.EventSystems;
using System;

/// <summary>
/// 轻量横向滑动手势 — 挂在页面根节点上(背景图需可接收Raycast)
/// 水平位移超过阈值且大于竖直分量时触发:
/// OnSwipeLeft 手指左滑(下一个) / OnSwipeRight 手指右滑(上一个)
/// </summary>
public class SwipeHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public Action OnSwipeLeft;
    public Action OnSwipeRight;

    private const float Threshold = 80f;
    private Vector2 _startPos;
    private bool _dragging;

    public void OnBeginDrag(PointerEventData eventData)
    {
        _startPos = eventData.position;
        _dragging = true;
    }

    public void OnDrag(PointerEventData eventData) { }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_dragging) return;
        _dragging = false;

        Vector2 delta = eventData.position - _startPos;
        if (Mathf.Abs(delta.x) <= Threshold || Mathf.Abs(delta.x) <= Mathf.Abs(delta.y)) return;
        if (delta.x < 0) OnSwipeLeft?.Invoke();
        else OnSwipeRight?.Invoke();
    }
}
