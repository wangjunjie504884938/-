using UnityEngine;

/// <summary>
/// Simple component to host coroutines on runtime-spawned objects.
/// </summary>
public class VFXRunner : MonoBehaviour
{
    private void OnDestroy()
    {
        StopAllCoroutines();
    }
}
