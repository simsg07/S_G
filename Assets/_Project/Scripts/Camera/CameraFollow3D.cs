using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

[RequireComponent(typeof(Camera))]
public class CameraFollow3D : MonoBehaviour
{
    private const string DefaultBoundaryLayerName = "Ground";

    [Header("Default Camera Settings")]
    [SerializeField] private Transform target;
    [FormerlySerializedAs("offset")]
    [SerializeField] private Vector3 followOffset = new Vector3(0f, 1f, -10f);
    [FormerlySerializedAs("orthographicSize")]
    [SerializeField, Min(0.01f)] private float viewSize = 5.2f;
    [SerializeField, Range(1f, 179f)] private float fieldOfView = 60f;
    [SerializeField, Min(0f)] private float followSmoothTime = 0.125f;
    [SerializeField, Min(0f)] private float boundarySmoothTime = 0.18f;
    [SerializeField, Min(0f)] private float edgePadding;

    [Header("Fixed Game View")]
    [SerializeField] private Vector2 referenceAspect = new Vector2(16f, 9f);
    [SerializeField] private Color letterboxColor = Color.black;

    [Header("Ground Layer Boundary")]
    [SerializeField] private bool constrainToLayerBounds = true;
    [SerializeField] private LayerMask boundaryLayerMask;
    [SerializeField, Min(0f)] private float layerBoundsPadding;
    [SerializeField] private bool includeTriggerBoundaries;

    [Header("Debug")]
    [SerializeField] private bool drawLayerBoundaryGizmos = true;

    private Camera targetCamera;
    private MapCameraSettings activeSettings;
    private Vector3 velocity;
    private Vector3 runtimeOffset;
    private float runtimeFollowSmoothTime;
    private float runtimeBoundarySmoothTime;
    private float runtimeEdgePadding;
    private Bounds cachedLayerBounds;
    private bool hasCachedLayerBounds;
    private bool boundaryCacheBuilt;
    private bool targetLookupPending;
    private bool hadValidTarget;
    private int lastScreenWidth;
    private int lastScreenHeight;

    public float FixedAspect => Mathf.Max(0.01f, referenceAspect.x) / Mathf.Max(0.01f, referenceAspect.y);
    public Rect GameViewportRect => targetCamera != null ? targetCamera.rect : new Rect(0f, 0f, 1f, 1f);

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
        ApplyFixedAspectViewport(true);
        EnsureDefaultBoundaryLayer();
        RefreshMapSettings();
    }

    private void OnEnable()
    {
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        ApplyFixedAspectViewport(true);
        RefreshMapSettings();
        RequestTargetRefresh();
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void OnValidate()
    {
        viewSize = Mathf.Max(0.01f, viewSize);
        fieldOfView = Mathf.Clamp(fieldOfView, 1f, 179f);
        referenceAspect.x = Mathf.Max(0.01f, referenceAspect.x);
        referenceAspect.y = Mathf.Max(0.01f, referenceAspect.y);
        EnsureDefaultBoundaryLayer();
        if (!Application.isPlaying)
        {
            ApplyProjection(viewSize, fieldOfView);
        }
    }

    private void HandleActiveSceneChanged(Scene previous, Scene next)
    {
        RefreshMapSettings();
        RequestTargetRefresh();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshMapSettings();
        RequestTargetRefresh();
    }

    public void RefreshMapSettings()
    {
        hasCachedLayerBounds = false;
        boundaryCacheBuilt = false;
        activeSettings = MapCameraSettings.FindForScene(SceneManager.GetActiveScene());
        runtimeOffset = followOffset;
        runtimeFollowSmoothTime = followSmoothTime;
        runtimeBoundarySmoothTime = boundarySmoothTime;
        runtimeEdgePadding = edgePadding;

        float selectedViewSize = viewSize;
        float selectedFieldOfView = fieldOfView;
        if (activeSettings != null && activeSettings.UseMapOverride)
        {
            runtimeOffset = activeSettings.FollowOffset + Vector3.up * activeSettings.VerticalOffset;
            runtimeFollowSmoothTime = activeSettings.FollowSmoothTime;
            runtimeBoundarySmoothTime = activeSettings.BoundarySmoothTime;
            runtimeEdgePadding = activeSettings.EdgePadding;
            selectedViewSize = activeSettings.ViewSize;
            selectedFieldOfView = activeSettings.FieldOfView;
        }

        ApplyProjection(selectedViewSize, selectedFieldOfView);
    }

    private void LateUpdate()
    {
        ApplyFixedAspectViewport(false);
        if (target != null)
        {
            hadValidTarget = true;
        }
        else if (hadValidTarget)
        {
            hadValidTarget = false;
            targetLookupPending = true;
        }

        if (target == null && targetLookupPending)
        {
            targetLookupPending = false;
            TryAcquirePlayerTarget();
        }

        if (target == null)
        {
            return;
        }

        Vector3 desired = target.position + runtimeOffset;
        desired.z = runtimeOffset.z;
        bool boundaryAdjusted = TryConstrainToMap(ref desired);
        boundaryAdjusted |= TryConstrainToLayerBounds(ref desired);

        if (!Application.isPlaying)
        {
            transform.position = desired;
            return;
        }

        float smoothTime = boundaryAdjusted ? runtimeBoundarySmoothTime : runtimeFollowSmoothTime;
        transform.position = smoothTime <= 0f
            ? desired
            : Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
    }

    private bool TryConstrainToMap(ref Vector3 desired)
    {
        if (activeSettings == null || !activeSettings.TryGetMapBounds(out Bounds bounds))
        {
            return false;
        }

        GetViewHalfExtents(desired, out float halfWidth, out float halfHeight);
        float leftPadding = runtimeEdgePadding;
        float rightPadding = runtimeEdgePadding;
        float bottomPadding = runtimeEdgePadding + activeSettings.FloorOffset;
        float topPadding = runtimeEdgePadding + activeSettings.CeilingOffset;

        float minX = bounds.min.x + halfWidth + leftPadding;
        float maxX = bounds.max.x - halfWidth - rightPadding;
        float minY = bounds.min.y + halfHeight + bottomPadding;
        float maxY = bounds.max.y - halfHeight - topPadding;
        float constrainedX = minX <= maxX ? Mathf.Clamp(desired.x, minX, maxX) : bounds.center.x;
        float constrainedY = minY <= maxY ? Mathf.Clamp(desired.y, minY, maxY) : bounds.center.y;
        bool changed = !Mathf.Approximately(desired.x, constrainedX) || !Mathf.Approximately(desired.y, constrainedY);
        desired.x = constrainedX;
        desired.y = constrainedY;
        return changed;
    }

    private bool TryConstrainToLayerBounds(ref Vector3 desired)
    {
        if (!constrainToLayerBounds || !TryGetLayerBounds(out Bounds bounds))
        {
            return false;
        }

        GetViewHalfExtents(desired, out float halfWidth, out float halfHeight);
        float padding = layerBoundsPadding + runtimeEdgePadding;
        float minX = bounds.min.x + halfWidth + padding;
        float maxX = bounds.max.x - halfWidth - padding;
        float minY = bounds.min.y + halfHeight + padding;
        float maxY = bounds.max.y - halfHeight - padding;
        bool changed = false;

        if (minX <= maxX)
        {
            float value = Mathf.Clamp(desired.x, minX, maxX);
            changed |= !Mathf.Approximately(desired.x, value);
            desired.x = value;
        }

        if (minY <= maxY)
        {
            float value = Mathf.Clamp(desired.y, minY, maxY);
            changed |= !Mathf.Approximately(desired.y, value);
            desired.y = value;
        }

        return changed;
    }

    private bool TryGetLayerBounds(out Bounds bounds)
    {
        TryRefreshLayerBoundaryCache();
        bounds = cachedLayerBounds;
        return hasCachedLayerBounds;
    }

    private bool TryRefreshLayerBoundaryCache()
    {
        if (boundaryLayerMask.value == 0)
        {
            hasCachedLayerBounds = false;
            boundaryCacheBuilt = true;
            return false;
        }

        if (boundaryCacheBuilt)
        {
            return hasCachedLayerBounds;
        }

        Collider[] colliders = FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        boundaryCacheBuilt = true;
        hasCachedLayerBounds = false;
        foreach (Collider candidate in colliders)
        {
            if (candidate == null || !candidate.enabled || candidate.gameObject.scene != gameObject.scene ||
                (!includeTriggerBoundaries && candidate.isTrigger) ||
                (boundaryLayerMask.value & (1 << candidate.gameObject.layer)) == 0)
            {
                continue;
            }

            if (!hasCachedLayerBounds)
            {
                cachedLayerBounds = candidate.bounds;
                hasCachedLayerBounds = true;
            }
            else
            {
                cachedLayerBounds.Encapsulate(candidate.bounds);
            }
        }

        return hasCachedLayerBounds;
    }

    private void EnsureDefaultBoundaryLayer()
    {
        if (boundaryLayerMask.value == 0)
        {
            boundaryLayerMask = LayerMask.GetMask(DefaultBoundaryLayerName);
        }
    }

    private void RequestTargetRefresh()
    {
        targetLookupPending = true;
    }

    private void GetViewHalfExtents(Vector3 cameraPosition, out float halfWidth, out float halfHeight)
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }

        if (targetCamera.orthographic)
        {
            halfHeight = targetCamera.orthographicSize;
        }
        else
        {
            float targetPlaneZ = target != null ? target.position.z : 0f;
            float distance = Mathf.Abs(targetPlaneZ - cameraPosition.z);
            halfHeight = Mathf.Tan(targetCamera.fieldOfView * Mathf.Deg2Rad * 0.5f) * distance;
        }

        halfWidth = halfHeight * FixedAspect;
    }

    private void TryAcquirePlayerTarget()
    {
        PlatformerPlayer3D player = FindFirstObjectByType<PlatformerPlayer3D>();
        if (player != null)
        {
            target = player.transform;
            hadValidTarget = true;
            velocity = Vector3.zero;
        }
    }

    public void SnapToTarget(Transform newTarget)
    {
        target = newTarget;
        targetLookupPending = target == null;
        hadValidTarget = target != null;
        RefreshMapSettings();
        velocity = Vector3.zero;
        if (target == null)
        {
            return;
        }

        Vector3 desired = target.position + runtimeOffset;
        desired.z = runtimeOffset.z;
        TryConstrainToMap(ref desired);
        TryConstrainToLayerBounds(ref desired);
        transform.position = desired;
    }

    private void ApplyProjection(float selectedViewSize, float selectedFieldOfView)
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }

        if (targetCamera == null)
        {
            return;
        }

        if (targetCamera.orthographic)
        {
            targetCamera.orthographicSize = Mathf.Max(0.01f, selectedViewSize);
        }
        else
        {
            targetCamera.fieldOfView = Mathf.Clamp(selectedFieldOfView, 1f, 179f);
        }
    }

    private void ApplyFixedAspectViewport(bool force)
    {
        if (targetCamera == null) targetCamera = GetComponent<Camera>();
        int width = Mathf.Max(1, Screen.width);
        int height = Mathf.Max(1, Screen.height);
        if (!force && width == lastScreenWidth && height == lastScreenHeight) return;

        lastScreenWidth = width;
        lastScreenHeight = height;
        float windowAspect = width / (float)height;
        float targetAspect = FixedAspect;
        Rect viewport = new Rect(0f, 0f, 1f, 1f);
        if (windowAspect > targetAspect)
        {
            viewport.width = targetAspect / windowAspect;
            viewport.x = (1f - viewport.width) * 0.5f;
        }
        else if (windowAspect < targetAspect)
        {
            viewport.height = windowAspect / targetAspect;
            viewport.y = (1f - viewport.height) * 0.5f;
        }
        targetCamera.rect = viewport;
        targetCamera.backgroundColor = letterboxColor;
    }

    private void OnGUI()
    {
        if (targetCamera == null || Event.current.type != EventType.Repaint) return;
        Rect pixelRect = targetCamera.pixelRect;
        Color previousColor = GUI.color;
        GUI.color = letterboxColor;
        if (pixelRect.xMin > 0f)
        {
            GUI.DrawTexture(new Rect(0f, 0f, pixelRect.xMin, Screen.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(pixelRect.xMax, 0f, Screen.width - pixelRect.xMax, Screen.height), Texture2D.whiteTexture);
        }
        if (pixelRect.yMin > 0f)
        {
            float topHeight = Screen.height - pixelRect.yMax;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, topHeight), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0f, Screen.height - pixelRect.yMin, Screen.width, pixelRect.yMin), Texture2D.whiteTexture);
        }
        GUI.color = previousColor;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawLayerBoundaryGizmos || !TryGetLayerBounds(out Bounds bounds))
        {
            return;
        }

        Gizmos.color = new Color(0.35f, 1f, 0.25f, 0.8f);
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
}
