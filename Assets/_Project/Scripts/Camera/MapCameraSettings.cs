using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class MapCameraSettings : MonoBehaviour
{
    [Header("Map Override")]
    [SerializeField] private bool useMapOverride = true;
    [SerializeField, Min(0.01f)] private float viewSize = 5.2f;
    [SerializeField, Range(1f, 179f)] private float fieldOfView = 60f;
    [SerializeField] private Vector3 followOffset = new Vector3(0f, 1f, -10f);
    [SerializeField] private float verticalOffset;
    [SerializeField, Min(0f)] private float edgePadding;
    [SerializeField, Min(0f)] private float ceilingOffset;
    [SerializeField, Min(0f)] private float floorOffset;
    [SerializeField, Min(0f)] private float followSmoothTime = 0.125f;
    [SerializeField, Min(0f)] private float boundarySmoothTime = 0.18f;

    [Header("Map Boundary")]
    [SerializeField] private CameraBounds cameraBounds;

    public bool UseMapOverride => useMapOverride;
    public float ViewSize => Mathf.Max(0.01f, viewSize);
    public float FieldOfView => Mathf.Clamp(fieldOfView, 1f, 179f);
    public Vector3 FollowOffset => followOffset;
    public float VerticalOffset => verticalOffset;
    public float EdgePadding => Mathf.Max(0f, edgePadding);
    public float CeilingOffset => Mathf.Max(0f, ceilingOffset);
    public float FloorOffset => Mathf.Max(0f, floorOffset);
    public float FollowSmoothTime => Mathf.Max(0f, followSmoothTime);
    public float BoundarySmoothTime => Mathf.Max(0f, boundarySmoothTime);

    public bool TryGetMapBounds(out Bounds bounds)
    {
        if (cameraBounds != null)
        {
            bounds = cameraBounds.WorldBounds;
            return bounds.size.x > 0f && bounds.size.y > 0f;
        }

        bounds = default;
        return false;
    }

    public static MapCameraSettings FindForScene(Scene scene)
    {
        MapCameraSettings selected = null;
        int count = 0;
        MapCameraSettings[] settings = FindObjectsByType<MapCameraSettings>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (MapCameraSettings candidate in settings)
        {
            if (candidate.gameObject.scene != scene)
            {
                continue;
            }

            count++;
            if (selected == null || candidate.isActiveAndEnabled)
            {
                selected = candidate;
            }
        }

        if (count > 1)
        {
            Debug.LogWarning($"[MapCameraSettings] Scene '{scene.name}' has {count} settings components. Only '{selected.name}' will be used.", selected);
        }

        return selected;
    }

    private void OnValidate()
    {
        viewSize = Mathf.Max(0.01f, viewSize);
        fieldOfView = Mathf.Clamp(fieldOfView, 1f, 179f);
        if (cameraBounds == null)
        {
            cameraBounds = GetComponent<CameraBounds>();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!TryGetMapBounds(out Bounds bounds))
        {
            return;
        }

        Camera camera = Camera.main;
        if (camera == null || !camera.orthographic)
        {
            return;
        }

        float halfHeight = ViewSize;
        CameraFollow3D follow = camera.GetComponent<CameraFollow3D>();
        float halfWidth = halfHeight * (follow != null ? follow.FixedAspect : camera.aspect);
        float minX = bounds.min.x + halfWidth + EdgePadding;
        float maxX = bounds.max.x - halfWidth - EdgePadding;
        float minY = bounds.min.y + halfHeight + EdgePadding + FloorOffset;
        float maxY = bounds.max.y - halfHeight - EdgePadding - CeilingOffset;
        Vector3 center = bounds.center;
        Vector3 size = Vector3.zero;
        center.x = minX <= maxX ? (minX + maxX) * 0.5f : bounds.center.x;
        center.y = minY <= maxY ? (minY + maxY) * 0.5f : bounds.center.y;
        size.x = Mathf.Max(0f, maxX - minX);
        size.y = Mathf.Max(0f, maxY - minY);
        size.z = 0.1f;
        Gizmos.color = new Color(1f, 0.75f, 0.1f, 0.9f);
        Gizmos.DrawWireCube(center, size);
    }
}
