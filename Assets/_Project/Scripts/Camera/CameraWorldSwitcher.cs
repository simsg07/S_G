using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class CameraWorldSwitcher : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera; // Camera whose visible bounds decide which objects can switch.
    [SerializeField] private CameraWorldSwitchSettings settings; // Optional default values shared across scenes.

    [Header("Inspector Override")]
    [SerializeField] private bool useInspectorOverride = false; // If true, this component uses the values below instead of the settings asset.
    [FormerlySerializedAs("allowedTags")]
    [SerializeField] private string[] switchableTags =
    {
        "SwitchableWorldObject",
        "PuzzleObject",
        "Door",
        "Platform"
    }; // Only objects with one of these tags can be switched by camera range.
    [FormerlySerializedAs("targetLayers")]
    [SerializeField] private LayerMask switchableLayerMask = ~0; // Layer filter for camera range world switching.
    [FormerlySerializedAs("boundsMargin")]
    [SerializeField] private float cameraBoundsMargin = 0.2f; // Extra padding around the camera view used for switch target checks.
    [SerializeField] private float queryDepth = 20f; // Z depth of the 2.5D camera range query.
    [SerializeField] private float queryPlaneZ = 0f; // Gameplay plane Z used to convert camera viewport bounds.
    [FormerlySerializedAs("debugDraw")]
    [SerializeField] private bool debugMode = true; // Draws the switch search bounds in the Scene view.

    [Header("Events")]
    [SerializeField] private UnityEvent beforeSwitch = new UnityEvent(); // Hook for future switch effects or sound.
    [SerializeField] private UnityEvent afterSwitch = new UnityEvent(); // Hook for future switch effects or sound.

    private readonly List<WorldSwitchable> candidates = new List<WorldSwitchable>(64);
    private Bounds lastQueryBounds;
    private bool hasLastQueryBounds;

    public int LastSwitchCount { get; private set; }

    private void Reset()
    {
        targetCamera = GetComponent<Camera>();
    }

    private void OnValidate()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }
    }

    public void SetTargetCamera(Camera camera)
    {
        if (camera != null)
        {
            targetCamera = camera;
        }
    }

    public bool TrySwitchVisibleObjects()
    {
        LastSwitchCount = SwitchVisibleObjects();
        return LastSwitchCount > 0;
    }

    public int SwitchVisibleObjects()
    {
        Camera camera = ResolveCamera();
        if (camera == null || !TryBuildCameraBounds(camera, out Bounds cameraBounds))
        {
            LastSwitchCount = 0;
            return 0;
        }

        hasLastQueryBounds = true;
        lastQueryBounds = cameraBounds;
        CollectCandidates(cameraBounds);

        if (candidates.Count == 0)
        {
            LastSwitchCount = 0;
            return 0;
        }

        beforeSwitch.Invoke();
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i] != null)
            {
                candidates[i].ToggleWorld();
            }
        }

        afterSwitch.Invoke();
        LastSwitchCount = candidates.Count;
        return LastSwitchCount;
    }

    private void CollectCandidates(Bounds cameraBounds)
    {
        candidates.Clear();
        WorldSwitchable[] switchables = FindObjectsByType<WorldSwitchable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        string[] tags = ResolveAllowedTags();
        LayerMask layers = ResolveTargetLayers();

        for (int i = 0; i < switchables.Length; i++)
        {
            WorldSwitchable switchable = switchables[i];
            if (!IsValidCandidate(switchable, tags, layers))
            {
                continue;
            }

            if (!switchable.TryGetWorldBounds(out Bounds targetBounds))
            {
                continue;
            }

            if (cameraBounds.Intersects(targetBounds))
            {
                candidates.Add(switchable);
            }
        }
    }

    private bool IsValidCandidate(WorldSwitchable switchable, string[] tags, LayerMask layers)
    {
        if (switchable == null || !switchable.CanSwitchByCamera)
        {
            return false;
        }

        GameObject target = switchable.gameObject;
        if (target == null || IsProtectedObject(target))
        {
            return false;
        }

        if ((layers.value & (1 << target.layer)) == 0)
        {
            return false;
        }

        return CameraTagUtility3D.HasAnyTag(target, tags);
    }

    private bool IsProtectedObject(GameObject target)
    {
        return target.GetComponentInParent<PlatformerPlayer3D>(true) != null
            || target.GetComponentInParent<Camera>(true) != null
            || target.GetComponentInParent<Canvas>(true) != null
            || HasProtectedName(target)
            || target.GetComponentInParent<WorldManager>(true) != null
            || target.GetComponentInParent<WorldSystem3D>(true) != null
            || target.GetComponentInParent<CameraAbilitySystem3D>(true) != null;
    }

    private static bool HasProtectedName(GameObject target)
    {
        Transform current = target != null ? target.transform : null;
        while (current != null)
        {
            string objectName = current.name;
            if (objectName == "GameManager" || objectName == "WorldManager")
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private bool TryBuildCameraBounds(Camera camera, out Bounds bounds)
    {
        float planeZ = ResolveQueryPlaneZ();
        if (!TryViewportPointOnPlane(camera, new Vector2(0f, 0f), planeZ, out Vector3 bottomLeft)
            || !TryViewportPointOnPlane(camera, new Vector2(1f, 1f), planeZ, out Vector3 topRight))
        {
            bounds = default;
            return false;
        }

        float margin = ResolveBoundsMargin();
        float minX = Mathf.Min(bottomLeft.x, topRight.x) - margin;
        float maxX = Mathf.Max(bottomLeft.x, topRight.x) + margin;
        float minY = Mathf.Min(bottomLeft.y, topRight.y) - margin;
        float maxY = Mathf.Max(bottomLeft.y, topRight.y) + margin;
        float depth = ResolveQueryDepth();

        Vector3 center = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, planeZ);
        Vector3 size = new Vector3(maxX - minX, maxY - minY, depth);
        bounds = new Bounds(center, size);
        return true;
    }

    private bool TryViewportPointOnPlane(Camera camera, Vector2 viewportPoint, float planeZ, out Vector3 worldPoint)
    {
        Ray ray = camera.ViewportPointToRay(new Vector3(viewportPoint.x, viewportPoint.y, 0f));
        Plane plane = new Plane(Vector3.forward, new Vector3(0f, 0f, planeZ));
        if (plane.Raycast(ray, out float enter))
        {
            worldPoint = ray.GetPoint(enter);
            return true;
        }

        worldPoint = Vector3.zero;
        return false;
    }

    private Camera ResolveCamera()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        return targetCamera;
    }

    private string[] ResolveAllowedTags()
    {
        if (!useInspectorOverride && settings != null)
        {
            return settings.SwitchableTags;
        }

        return switchableTags;
    }

    private LayerMask ResolveTargetLayers()
    {
        if (!useInspectorOverride && settings != null)
        {
            return settings.SwitchTargetLayers;
        }

        return switchableLayerMask;
    }

    private float ResolveBoundsMargin()
    {
        if (!useInspectorOverride && settings != null)
        {
            return settings.SwitchBoundsMargin;
        }

        return Mathf.Max(0f, cameraBoundsMargin);
    }

    private float ResolveQueryDepth()
    {
        if (!useInspectorOverride && settings != null)
        {
            return settings.SwitchDepth;
        }

        return Mathf.Max(0.1f, queryDepth);
    }

    private float ResolveQueryPlaneZ()
    {
        if (!useInspectorOverride && settings != null)
        {
            return settings.QueryPlaneZ;
        }

        return queryPlaneZ;
    }

    private bool ResolveDebugDraw()
    {
        if (!useInspectorOverride && settings != null)
        {
            return settings.DebugDraw;
        }

        return debugMode;
    }

    private void OnDrawGizmosSelected()
    {
        if (!ResolveDebugDraw())
        {
            return;
        }

        Camera camera = ResolveCamera();
        if (camera != null && TryBuildCameraBounds(camera, out Bounds bounds))
        {
            lastQueryBounds = bounds;
            hasLastQueryBounds = true;
        }

        if (!hasLastQueryBounds)
        {
            return;
        }

        Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.22f);
        Gizmos.DrawCube(lastQueryBounds.center, lastQueryBounds.size);
        Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.95f);
        Gizmos.DrawWireCube(lastQueryBounds.center, lastQueryBounds.size);
    }
}
