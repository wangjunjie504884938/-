using UnityEngine;
using UnityEngine.EventSystems;

public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public static VirtualJoystick Instance { get; private set; }

    [SerializeField] private float handleRange = 1f;
    [SerializeField] private float deadZone = 0.1f;

    private RectTransform backgroundRect;
    private RectTransform handleRect;
    private Vector2 inputVector;

    public Vector2 InputVector
    {
        get
        {
            return inputVector.magnitude > deadZone ? inputVector : Vector2.zero;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        backgroundRect = transform as RectTransform;
        handleRect = transform.GetChild(0) as RectTransform;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        UpdateHandlePosition(eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        UpdateHandlePosition(eventData.position);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        ResetJoystick();
    }

    private void UpdateHandlePosition(Vector2 screenPos)
    {
        Camera cam = null;
        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            backgroundRect, screenPos, cam, out Vector2 localPoint);

        inputVector = localPoint / (backgroundRect.sizeDelta.x * 0.5f);
        inputVector = Vector2.ClampMagnitude(inputVector, handleRange);

        handleRect.anchoredPosition = inputVector * backgroundRect.sizeDelta.x * 0.5f;
    }

    private void ResetJoystick()
    {
        inputVector = Vector2.zero;
        handleRect.anchoredPosition = Vector2.zero;
    }
}
