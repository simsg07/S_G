using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public enum CameraLightRangeCenterMode
{
    Player,
    Camera,
    CustomTransform
}

[DisallowMultipleComponent]
public class CameraLightFollower : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera; // Camera used to convert mouse position into world space.
    [FormerlySerializedAs("targetLight")]
    [SerializeField] private Light lightObject; // Light controlled by the R-key toggle.
    [SerializeField] private Transform playerTransform; // Player center used when lightRangeCenterMode is Player.
    [SerializeField] private CameraWorldSwitchSettings settings; // Optional default values shared across scenes.

    [Header("Inspector Override")]
    [SerializeField] private bool useInspectorOverride = true; // If false, this component uses the settings asset values.

    [Header("Light Range")]
    [SerializeField] private float lightRangeRadius = 4f; // Maximum circular distance from the selected range center.
    [SerializeField] private float lightMoveSpeed = 24f; // Follow speed while the light is on.
    [FormerlySerializedAs("lightWorldZ")]
    [SerializeField] private float lightZPosition = -0.55f; // Fixed 2.5D Z position for the light.
    [SerializeField] private CameraLightRangeCenterMode lightRangeCenterMode = CameraLightRangeCenterMode.Player; // Center used for the circular light range.
    [SerializeField] private Transform customLightRangeCenter; // Custom range center when lightRangeCenterMode is CustomTransform.
    [SerializeField] private bool showLightRangeGizmo = true; // Shows the circular light range in the Scene view.

    private bool lightActive;

    public bool IsLightActive => lightActive;
    public Light LightObject => lightObject;
    public float LightRangeRadius => ResolveLightRangeRadius();

    private void Reset()
    {
        targetCamera = GetComponent<Camera>();
    }

    private void Awake()
    {
        ResolveReferences();
        SetLightActive(false, true);
    }

    private void OnValidate()
    {
        ResolveReferences();
        lightRangeRadius = Mathf.Max(0f, lightRangeRadius);
        lightMoveSpeed = Mathf.Max(0f, lightMoveSpeed);
    }

    private void Update()
    {
        if (!Application.isPlaying || !lightActive)
        {
            return;
        }

        MoveBoundLight(GetRangeCenter(), false);
    }

    public void Bind(Camera camera, Light light)
    {
        if (camera != null)
        {
            targetCamera = camera;
        }

        if (light != null)
        {
            lightObject = light;
        }

        ResolveReferences();
    }

    public void SetPlayerTransform(Transform player)
    {
        if (player != null)
        {
            playerTransform = player;
        }
    }

    public bool ToggleLight()
    {
        SetLightActive(!lightActive, true);
        return lightActive;
    }

    public void SetLightActive(bool active)
    {
        SetLightActive(active, true);
    }

    public void SetLightActive(bool active, bool instant)
    {
        ResolveReferences();
        lightActive = active;

        if (lightObject == null)
        {
            return;
        }

        lightObject.enabled = active;
        if (!active)
        {
            return;
        }

        MoveBoundLight(GetRangeCenter(), instant);
    }

    public Vector3 GetClampedMouseWorldPoint(float targetZ, Vector3 fallback)
    {
        Camera camera = ResolveCamera();
        if (camera == null)
        {
            return ClampToLightRange(new Vector3(fallback.x, fallback.y, targetZ));
        }

        Vector2 mouseScreenPosition = GetMouseScreenPosition();
        Ray ray = camera.ScreenPointToRay(new Vector3(mouseScreenPosition.x, mouseScreenPosition.y, 0f));
        Plane plane = new Plane(Vector3.forward, new Vector3(0f, 0f, targetZ));
        Vector3 worldPoint = plane.Raycast(ray, out float enter) ? ray.GetPoint(enter) : fallback;
        worldPoint.z = targetZ;
        return ClampToLightRange(worldPoint);
    }

    public Vector3 GetLightTargetPosition(Vector3 fallback)
    {
        return GetClampedMouseWorldPoint(ResolveLightZPosition(), fallback);
    }

    public Vector3 MoveBoundLight(Vector3 fallback, bool instant)
    {
        Vector3 targetPosition = GetLightTargetPosition(fallback);
        if (lightObject == null)
        {
            return targetPosition;
        }

        float moveSpeed = ResolveLightMoveSpeed();
        if (instant || moveSpeed <= 0f)
        {
            lightObject.transform.position = targetPosition;
            return targetPosition;
        }

        float maxDistance = moveSpeed * Time.unscaledDeltaTime;
        lightObject.transform.position = Vector3.MoveTowards(lightObject.transform.position, targetPosition, maxDistance);
        return lightObject.transform.position;
    }

    private Vector3 ClampToLightRange(Vector3 worldPoint)
    {
        Vector3 center = GetRangeCenter();
        Vector2 offset = new Vector2(worldPoint.x - center.x, worldPoint.y - center.y);
        float radius = ResolveLightRangeRadius();
        if (offset.magnitude > radius && radius > 0f)
        {
            offset = offset.normalized * radius;
        }

        return new Vector3(center.x + offset.x, center.y + offset.y, ResolveLightZPosition());
    }

    private Vector3 GetRangeCenter()
    {
        ResolveReferences();

        Transform centerTransform = null;
        switch (lightRangeCenterMode)
        {
            case CameraLightRangeCenterMode.Camera:
                centerTransform = ResolveCamera() != null ? ResolveCamera().transform : null;
                break;
            case CameraLightRangeCenterMode.CustomTransform:
                centerTransform = customLightRangeCenter;
                break;
            default:
                centerTransform = playerTransform;
                break;
        }

        Vector3 center = centerTransform != null ? centerTransform.position : transform.position;
        center.z = ResolveLightZPosition();
        return center;
    }

    private float ResolveLightRangeRadius()
    {
        if (!useInspectorOverride && settings != null)
        {
            return settings.LightRangeRadius;
        }

        return Mathf.Max(0f, lightRangeRadius);
    }

    private float ResolveLightMoveSpeed()
    {
        if (!useInspectorOverride && settings != null)
        {
            return settings.LightMoveSpeed;
        }

        return Mathf.Max(0f, lightMoveSpeed);
    }

    private float ResolveLightZPosition()
    {
        if (!useInspectorOverride && settings != null)
        {
            return settings.LightZPosition;
        }

        return lightZPosition;
    }

    private bool ResolveShowLightRangeGizmo()
    {
        if (!useInspectorOverride && settings != null)
        {
            return settings.ShowLightRangeGizmo;
        }

        return showLightRangeGizmo;
    }

    private void ResolveReferences()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (playerTransform == null)
        {
            PlatformerPlayer3D player = FindFirstObjectByType<PlatformerPlayer3D>();
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }
    }

    private Camera ResolveCamera()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        return targetCamera;
    }

    private Vector2 GetMouseScreenPosition()
    {
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            return mouse.position.ReadValue();
        }

        return Input.mousePosition;
    }

    private void OnDrawGizmosSelected()
    {
        if (!ResolveShowLightRangeGizmo())
        {
            return;
        }

        Vector3 center = GetRangeCenter();
        Gizmos.color = new Color(1f, 0.95f, 0.35f, 0.22f);
        Gizmos.DrawSphere(center, ResolveLightRangeRadius());
        Gizmos.color = new Color(1f, 0.95f, 0.35f, 0.95f);
        Gizmos.DrawWireSphere(center, ResolveLightRangeRadius());
    }
}
