using UnityEngine;

[DisallowMultipleComponent]
public class CameraMarkState3D : MonoBehaviour, IMarkable3D, IMarkState3D
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [SerializeField] private Color markedColor = new Color(0.35f, 0.95f, 1f, 0.95f);
    [SerializeField] private float markerWidth = 0.95f;
    [SerializeField] private float markerHeight = 0.08f;
    [SerializeField] private float markerDepth = 0.08f;
    [SerializeField] private float markerYOffset = 0.28f;
    [SerializeField] private float pulseSpeed = 8f;

    [Header("Mark Physics Target")]
    [SerializeField] private Rigidbody targetBody;
    [SerializeField] private bool pinTransformWhileMarked = true;
    [SerializeField] private bool restoreMarkedTransformOnRelease = true;

    [Header("Runtime Debug (Read Only)")]
    [SerializeField] private bool runtimeMarked;
    [SerializeField] private float runtimeRemainingTime;
    [SerializeField] private Vector3 runtimeStoredLinearVelocity;
    [SerializeField] private Vector3 runtimeStoredAngularVelocity;
    [SerializeField] private bool runtimeStoredKinematic;
    [SerializeField] private bool runtimeStoredUseGravity;
    [SerializeField] private RigidbodyConstraints runtimeStoredConstraints;

    private MeshRenderer markerRenderer;
    private MeshFilter markerFilter;
    private Renderer[] cachedRenderers = System.Array.Empty<Renderer>();
    private MaterialPropertyBlock propertyBlock;
    private float markEndTime;
    private bool markPhysicsCaptured;
    private bool storedKinematic;
    private bool storedUseGravity;
    private RigidbodyConstraints storedConstraints;
    private CollisionDetectionMode storedCollisionDetectionMode;
    private RigidbodyInterpolation storedInterpolation;
    private Vector3 storedLinearVelocity;
    private Vector3 storedAngularVelocity;
    private Vector3 markedPosition;
    private Quaternion markedRotation;

    public bool IsMarked => markPhysicsCaptured;
    public float RemainingMarkTime => IsMarked ? Mathf.Max(0f, markEndTime - Time.time) : 0f;

    private void Awake()
    {
        if (targetBody == null) targetBody = GetComponent<Rigidbody>();
        RefreshCache();
        EnsureMarker();
        ApplyVisual();
        enabled = IsMarked;
    }

    private void Update()
    {
        runtimeMarked = IsMarked;
        runtimeRemainingTime = RemainingMarkTime;
        if (IsMarked && Time.time >= markEndTime)
        {
            ReleaseMark();
            return;
        }
        ApplyVisual();
    }

    private void LateUpdate()
    {
        if (!IsMarked || !pinTransformWhileMarked) return;
        RestoreMarkedTransform();
    }

    private void OnDisable()
    {
        ReleaseMark();
        if (markerRenderer != null) markerRenderer.enabled = false;
    }

    public bool ApplyMark(float duration, CameraAbilitySystem3D source)
    {
        if (duration <= 0f || !gameObject.activeInHierarchy) return false;

        if (!IsMarked)
        {
            CaptureAndStopPhysics();
        }

        // Re촬영은 남은 시간에 더하지 않고 촬영 시점부터 전체 시간을 다시 부여한다.
        markEndTime = Time.time + duration;
        runtimeMarked = true;
        runtimeRemainingTime = duration;
        EnsureMarker();
        ApplyVisual();
        enabled = true;
        return true;
    }

    public void ClearMark()
    {
        ReleaseMark();
    }

    private void CaptureAndStopPhysics()
    {
        if (targetBody == null) targetBody = GetComponent<Rigidbody>();
        markPhysicsCaptured = true;
        Transform markedTransform = targetBody != null ? targetBody.transform : transform;
        markedPosition = markedTransform.position;
        markedRotation = markedTransform.rotation;

        if (targetBody == null) return;

        storedKinematic = targetBody.isKinematic;
        storedUseGravity = targetBody.useGravity;
        storedConstraints = targetBody.constraints;
        storedCollisionDetectionMode = targetBody.collisionDetectionMode;
        storedInterpolation = targetBody.interpolation;
        storedLinearVelocity = targetBody.linearVelocity;
        storedAngularVelocity = targetBody.angularVelocity;
        runtimeStoredKinematic = storedKinematic;
        runtimeStoredUseGravity = storedUseGravity;
        runtimeStoredConstraints = storedConstraints;
        runtimeStoredLinearVelocity = storedLinearVelocity;
        runtimeStoredAngularVelocity = storedAngularVelocity;

        targetBody.linearVelocity = Vector3.zero;
        targetBody.angularVelocity = Vector3.zero;
        targetBody.useGravity = false;
        targetBody.isKinematic = true;
    }

    private void RestoreMarkedTransform()
    {
        if (targetBody != null)
        {
            targetBody.position = markedPosition;
            targetBody.rotation = markedRotation;
        }
        else
        {
            transform.SetPositionAndRotation(markedPosition, markedRotation);
        }
    }

    private void ReleaseMark()
    {
        if (!markPhysicsCaptured)
        {
            markEndTime = 0f;
            runtimeMarked = false;
            runtimeRemainingTime = 0f;
            return;
        }

        if (restoreMarkedTransformOnRelease) RestoreMarkedTransform();
        if (targetBody != null)
        {
            targetBody.isKinematic = storedKinematic;
            targetBody.useGravity = storedUseGravity;
            targetBody.constraints = storedConstraints;
            targetBody.collisionDetectionMode = storedCollisionDetectionMode;
            targetBody.interpolation = storedInterpolation;
            if (!targetBody.isKinematic)
            {
                targetBody.linearVelocity = TwoPointFiveDUtility3D.ProjectVelocityToPlane(storedLinearVelocity);
                targetBody.angularVelocity = storedAngularVelocity;
            }
        }

        markPhysicsCaptured = false;
        markEndTime = 0f;
        runtimeMarked = false;
        runtimeRemainingTime = 0f;
        ApplyVisual();
        enabled = false;
    }

    private void EnsureMarker()
    {
        if (markerRenderer != null)
        {
            return;
        }

        GameObject marker = new GameObject("Camera Mark Indicator", typeof(MeshFilter), typeof(MeshRenderer));
        marker.transform.SetParent(transform, false);
        markerFilter = marker.GetComponent<MeshFilter>();
        markerRenderer = marker.GetComponent<MeshRenderer>();

        markerFilter.sharedMesh = CameraHighlightSharedResources3D.SolidCubeMesh;
        markerRenderer.sharedMaterial = CameraHighlightSharedResources3D.MarkerMaterial;
        markerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        markerRenderer.receiveShadows = false;
    }

    private void ApplyVisual()
    {
        EnsureMarker();

        bool active = IsMarked;
        markerRenderer.enabled = active;
        if (!active)
        {
            enabled = false;
            return;
        }

        Bounds bounds = CalculateBounds();
        if (!SafeMath3D.IsFinite(bounds.center) || !SafeMath3D.IsFinite(bounds.extents))
        {
            markerRenderer.enabled = false;
            return;
        }

        Transform markerTransform = markerRenderer.transform;
        Vector3 markerPosition = new Vector3(bounds.center.x, bounds.max.y + markerYOffset, bounds.center.z);
        if (!SafeMath3D.IsFinite(markerPosition))
        {
            markerRenderer.enabled = false;
            return;
        }
        markerTransform.position = markerPosition;

        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * 0.08f;
        markerTransform.localScale = new Vector3(markerWidth * pulse, markerHeight, markerDepth);

        SetMarkerColor(markedColor);
    }

    private void SetMarkerColor(Color color)
    {
        if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
        propertyBlock.Clear();
        propertyBlock.SetColor(BaseColorId, color);
        propertyBlock.SetColor(ColorId, color);
        markerRenderer.SetPropertyBlock(propertyBlock);
    }

    private Bounds CalculateBounds()
    {
        bool hasBounds = false;
        Bounds bounds = new Bounds(transform.position, Vector3.one);

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer renderer = cachedRenderers[i];
            if (renderer == null || renderer == markerRenderer || !SafeMath3D.IsValidTransform(renderer.transform))
            {
                continue;
            }

            if (!hasBounds)
            {
                Bounds rendererBounds = renderer.bounds;
                if (!SafeMath3D.IsFinite(rendererBounds.center) || !SafeMath3D.IsFinite(rendererBounds.extents))
                {
                    continue;
                }
                bounds = rendererBounds;
                hasBounds = true;
            }
            else
            {
                Bounds rendererBounds = renderer.bounds;
                if (SafeMath3D.IsFinite(rendererBounds.center) && SafeMath3D.IsFinite(rendererBounds.extents))
                {
                    bounds.Encapsulate(rendererBounds);
                }
            }
        }

        return bounds;
    }

    public void RefreshCache()
    {
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
    }

    private void OnTransformChildrenChanged()
    {
        RefreshCache();
    }

}
