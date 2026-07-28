using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

[RequireComponent(typeof(Camera))]
public class CameraFollow3D : MonoBehaviour
{
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

    private Camera targetCamera;
    private MapCameraSettings activeSettings;
    private Vector3 velocity;
    private Vector3 runtimeOffset;
    private float runtimeFollowSmoothTime;
    private float runtimeBoundarySmoothTime;
    private float runtimeEdgePadding;

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
        RefreshMapSettings();
    }

    private void OnEnable()
    {
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        RefreshMapSettings();
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
    }

    private void OnValidate()
    {
        viewSize = Mathf.Max(0.01f, viewSize);
        fieldOfView = Mathf.Clamp(fieldOfView, 1f, 179f);
        if (!Application.isPlaying)
        {
            ApplyProjection(viewSize, fieldOfView);
        }
    }

    private void HandleActiveSceneChanged(Scene previous, Scene next)
    {
        RefreshMapSettings();
    }

    public void RefreshMapSettings()
    {
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
        if (target == null)
        {
            FindPlayerTarget();
        }

        if (target == null)
        {
            return;
        }

        Vector3 desired = target.position + runtimeOffset;
        desired.z = runtimeOffset.z;
        bool boundaryAdjusted = TryConstrainToMap(ref desired);

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

        halfWidth = halfHeight * targetCamera.aspect;
    }

    private void FindPlayerTarget()
    {
        PlatformerPlayer3D player = FindFirstObjectByType<PlatformerPlayer3D>();
        if (player != null)
        {
            target = player.transform;
        }
    }

    public void SnapToTarget(Transform newTarget)
    {
        target = newTarget;
        RefreshMapSettings();
        velocity = Vector3.zero;
        if (target == null)
        {
            return;
        }

        Vector3 desired = target.position + runtimeOffset;
        desired.z = runtimeOffset.z;
        TryConstrainToMap(ref desired);
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
}
