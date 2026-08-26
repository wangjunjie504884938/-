using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform Target;
    public float SmoothSpeed = 0.125f;
    public Vector3 Offset = new Vector3(0, 0, -10);

    private Vector3 shakeOffset;
    private float shakeIntensity;
    private float shakeDuration;
    private float shakeTimer;

    public static CameraFollow Instance { get; private set; }

    // Reference aspect for ortho size tuning (16:9 landscape)
    private const float ReferenceAspect = 16f / 9f;
    // Target: player should occupy ~9% of screen height at baseOrthoSize on a 16:9 screen.
    // Player sprite = 64px/32ppu * scale(2.0) = 4 world units.
    // For player = 9% of screen: orthoSize = 4 / (2 * 0.09) ≈ 7
    private const float BaseOrthoSize = 10.0f;

    // Combined Awake: singleton + camera cache (merged to avoid duplicate method)

    public static void Shake(float intensity = 0.15f, float duration = 0.15f)
    {
        if (Instance == null) return;
        if (!GameSettings.ScreenShake) return;
        Instance.shakeIntensity = intensity;
        Instance.shakeDuration = duration;
        Instance.shakeTimer = 0f;
    }

    /// <summary>
    /// Brief slow-motion effect. Scales Time.timeScale then restores after real-time delay.
    /// </summary>
    private static int _slowMoStack;

    public static void SlowMotion(float timeScale, float duration)
    {
        if (Instance == null) return;
        Instance.StartCoroutine(Instance.SlowMotionCoroutine(timeScale, duration));
    }

    private System.Collections.IEnumerator SlowMotionCoroutine(float timeScale, float duration)
    {
        _slowMoStack++;
        Time.timeScale = timeScale;
        yield return new WaitForSecondsRealtime(duration);
        _slowMoStack--;
        if (_slowMoStack <= 0)
        {
            _slowMoStack = 0;
            Time.timeScale = 1f;
        }
    }

    private void OnDestroy()
    {
        // Ensure timeScale is restored if destroyed during slow-mo
        Time.timeScale = 1f;
        _slowMoStack = 0;
    }

    /// <summary>
    /// Base orthographic size set by DungeonMapData. Will be adjusted for screen aspect.
    /// </summary>
    public float ForcedOrthoSize { get; set; } = BaseOrthoSize;

    // Map bounds for camera clamping (set when entering dungeon)
    public float MapHalfWidth { get; set; } = 12f;
    public float MapHalfHeight { get; set; } = 15f;
    public bool ClampToMap { get; set; } = false;

    /// <summary>
    /// Calculates orthographic size that fits the map without showing void on any side.
    /// For narrow (portrait) maps on landscape screens, zooms in to fit map width.
    /// For wide maps, uses the designer-specified ForcedOrthoSize.
    /// </summary>
    public float GetAdaptedOrthoSize()
    {
        float aspect = (float)Screen.width / Screen.height;

        // Ortho size needed so the camera's horizontal view exactly covers the map width
        float fitWidthOrtho = MapHalfWidth / aspect;

        // Use the smaller of: designer intent (ForcedOrthoSize) or width-fit
        // This ensures narrow maps don't show void on the sides
        float result = Mathf.Min(ForcedOrthoSize, fitWidthOrtho);

        // Don't zoom in beyond a reasonable minimum (keep character visible)
        result = Mathf.Max(result, 4f);

        return result;
    }

    private Camera _cachedCam;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _cachedCam = GetComponent<Camera>();
    }

    /// <summary>立即将摄像机移动到目标位置（无平滑过渡）</summary>
    public void SnapToTarget()
    {
        if (Target == null)
        {
            if (GameManager.Instance != null && GameManager.Instance.Player != null)
                Target = GameManager.Instance.Player.transform;
        }
        if (Target == null || _cachedCam == null) return;

        Vector3 desiredPosition = Target.position + Offset;
        if (ClampToMap)
        {
            float camHalfH = _cachedCam.orthographicSize;
            float camHalfW = camHalfH * ((float)Screen.width / Screen.height);
            desiredPosition.x = Mathf.Clamp(desiredPosition.x, -MapHalfWidth + camHalfW, MapHalfWidth - camHalfW);
            desiredPosition.y = Mathf.Clamp(desiredPosition.y, -MapHalfHeight + camHalfH, MapHalfHeight - camHalfH);
        }
        _cachedCam.transform.position = desiredPosition;
    }

    private void LateUpdate()
    {
        if (!enabled) return;

        var cam = _cachedCam;
        if (cam != null)
        {
            float targetSize = GetAdaptedOrthoSize();
            if (Mathf.Abs(cam.orthographicSize - targetSize) > 0.05f)
                cam.orthographicSize = targetSize;
        }

        if (Target == null)
        {
            if (GameManager.Instance != null && GameManager.Instance.Player != null)
                Target = GameManager.Instance.Player.transform;
            return;
        }

        // Apply tilt offset so character stays centered despite camera angle
        Vector3 desiredPosition = Target.position + Offset;

        // Pre-clamp desired position to map bounds before Lerp to avoid jitter
        if (ClampToMap && cam != null)
        {
            float halfVisibleW = cam.orthographicSize * cam.aspect;
            float halfVisibleH = cam.orthographicSize;
            float minX = -MapHalfWidth + halfVisibleW;
            float maxX = MapHalfWidth - halfVisibleW;
            float minY = -MapHalfHeight + halfVisibleH;
            float maxY = MapHalfHeight - halfVisibleH;
            if (minX < maxX) desiredPosition.x = Mathf.Clamp(desiredPosition.x, minX, maxX);
            else desiredPosition.x = 0f;
            if (minY < maxY) desiredPosition.y = Mathf.Clamp(desiredPosition.y, minY, maxY);
            else desiredPosition.y = 0f;
        }

        // Screen shake
        if (shakeTimer < shakeDuration)
        {
            shakeTimer += Time.unscaledDeltaTime;
            float progress = shakeTimer / shakeDuration;
            float currentIntensity = shakeIntensity * (1f - progress);
            shakeOffset = new Vector3(
                Random.Range(-1f, 1f) * currentIntensity,
                Random.Range(-1f, 1f) * currentIntensity,
                0f
            );
        }
        else
        {
            shakeOffset = Vector3.zero;
        }

        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, SmoothSpeed);

        // Clamp final position (including shake) to map bounds
        Vector3 finalPosition = smoothedPosition + shakeOffset;
        if (ClampToMap && cam != null)
        {
            float halfVisibleW = cam.orthographicSize * cam.aspect;
            float halfVisibleH = cam.orthographicSize;
            float minX = -MapHalfWidth + halfVisibleW;
            float maxX = MapHalfWidth - halfVisibleW;
            float minY = -MapHalfHeight + halfVisibleH;
            float maxY = MapHalfHeight - halfVisibleH;
            if (minX < maxX) finalPosition.x = Mathf.Clamp(finalPosition.x, minX, maxX);
            else finalPosition.x = 0f;
            if (minY < maxY) finalPosition.y = Mathf.Clamp(finalPosition.y, minY, maxY);
            else finalPosition.y = 0f;
        }

        transform.position = finalPosition;
    }
}
