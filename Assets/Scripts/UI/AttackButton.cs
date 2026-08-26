using UnityEngine;
using UnityEngine.EventSystems;

public class AttackButton : MonoBehaviour
{
    public static AttackButton Instance { get; private set; }

    public bool IsPressed { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        // Keyboard input is handled by PlayerInputController.Tick() — no duplicate here
    }

    public void OnPointerDown(BaseEventData data)
    {
        IsPressed = true;
    }

    public void OnPointerUp(BaseEventData data)
    {
        IsPressed = false;
    }
}
