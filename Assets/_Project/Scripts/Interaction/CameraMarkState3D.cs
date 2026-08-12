using UnityEngine;

[DisallowMultipleComponent]
public class CameraMarkState3D : MonoBehaviour, IMarkable3D, IMarkState3D, IShutterFreezable3D
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
    [SerializeField] private DamageDealer[] damageDealers = System.Array.Empty<DamageDealer>();
    [SerializeField] private GravityObjectDamageDealer[] legacyDamageDealers = System.Array.Empty<GravityObjectDamageDealer>();
    [SerializeField] private bool pinTransformWhileMarked = true;
    [SerializeField] private bool restoreMarkedTransformOnRelease = true;

    [Header("Runtime Debug (Read Only)")]
    [SerializeField] private bool runtimeMarked;
    [SerializeField] private Vector3 runtimeStoredLinearVelocity;
    [SerializeField] private Vector3 runtimeStoredAngularVelocity;
    [SerializeField] private bool runtimeStoredKinematic;
    [SerializeField] private bool runtimeStoredUseGravity;
    [SerializeField] private RigidbodyConstraints runtimeStoredConstraints;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [SerializeField] private bool runtimeHasMarkSnapshot;
    [SerializeField] private int runtimeSnapshotCreatedCount;
    [SerializeField] private int runtimeSnapshotDiscardedCount;
    [SerializeField] private int runtimeRegistryRegisteredCount;
    [SerializeField] private int runtimeRegistryReleasedCount;
    [SerializeField] private int runtimeSnapshotInstanceId;
    [SerializeField] private bool runtimeRegistryRegistered;
    [SerializeField] private bool runtimeRigidbodyIsKinematic;
    [SerializeField] private bool runtimeRigidbodyUseGravity;
    [SerializeField] private FallingBoxState runtimeFallingBoxState;
    [SerializeField] private bool runtimeDamageDealerEnabled;
#endif

    private MeshRenderer markerRenderer;
    private MeshFilter markerFilter;
    private Renderer[] cachedRenderers = System.Array.Empty<Renderer>();
    private MaterialPropertyBlock propertyBlock;
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
    private FallingBoxObject fallingBox;
    private bool[] storedDamageDealerEnabled = System.Array.Empty<bool>();
    private bool[] storedLegacyDamageDealerEnabled = System.Array.Empty<bool>();

    public bool IsMarked => markPhysicsCaptured;
    public bool IsShutterFrozen => markPhysicsCaptured;

    private void Awake()
    {
        ShutterTargetRegistry3D.Register(this, this);
        if (targetBody == null) targetBody = GetComponent<Rigidbody>();
        fallingBox = GetComponent<FallingBoxObject>();
        CacheDamageDealerReferences();
        RefreshCache();
        EnsureMarker();
        ApplyVisual();
        enabled = IsMarked;
    }

    private void OnDestroy() => ShutterTargetRegistry3D.Unregister(this);

    private void Update()
    {
        runtimeMarked = IsMarked;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        runtimeRegistryRegistered = ShutterTargetRegistry3D.IsFreezeRegistered(this);
        runtimeRigidbodyIsKinematic = targetBody != null && targetBody.isKinematic;
        runtimeRigidbodyUseGravity = targetBody != null && targetBody.useGravity;
        runtimeFallingBoxState = fallingBox != null ? fallingBox.CurrentState : FallingBoxState.GROUNDED;
        runtimeDamageDealerEnabled = damageDealers != null && damageDealers.Length > 0
            && damageDealers[0] != null && damageDealers[0].enabled;
#endif
        ApplyVisual();
    }

    private void LateUpdate()
    {
        if (!IsMarked || !pinTransformWhileMarked) return;
        RestoreMarkedTransform();
    }

    private void OnDisable()
    {
        if (markerRenderer != null) markerRenderer.enabled = false;
        if (!IsMarked) return;
        WorldPresence presence = GetComponentInParent<WorldPresence>(true);
        if (presence == null || !presence.IsHiddenByCurrentWorld()) ReleaseShutterFreeze();
    }

    private void OnEnable()
    {
        if (!IsMarked) return;
        ReapplyShutterFreeze();
        ApplyVisual();
    }

    public bool ApplyMark(float duration, CameraAbilitySystem3D source)
    {
        if (!gameObject.activeInHierarchy) return false;

        if (!IsMarked)
        {
            CaptureAndStopPhysics();
        }

        runtimeMarked = true;
        EnsureMarker();
        ApplyVisual();
        enabled = true;
        return true;
    }

    public void ClearMark()
    {
        ReleaseShutterFreeze();
    }

    public void ReapplyShutterFreeze()
    {
        if (!markPhysicsCaptured || targetBody == null) return;
        if (!targetBody.isKinematic)
        {
            targetBody.linearVelocity = Vector3.zero;
            targetBody.angularVelocity = Vector3.zero;
        }
        targetBody.useGravity = false;
        targetBody.isKinematic = true;
        SetMarkedDamageEnabled(false);
    }

    public void ReleaseShutterFreeze() => ReleaseMark();

    private void CaptureAndStopPhysics()
    {
        if (targetBody == null) targetBody = GetComponent<Rigidbody>();
        markPhysicsCaptured = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        runtimeHasMarkSnapshot = true;
        runtimeSnapshotCreatedCount++;
        runtimeRegistryRegisteredCount++;
        runtimeSnapshotInstanceId = GetInstanceID();
#endif
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
        CaptureDamageDealerStates();

        targetBody.linearVelocity = Vector3.zero;
        targetBody.angularVelocity = Vector3.zero;
        targetBody.useGravity = false;
        targetBody.isKinematic = true;
        SetMarkedDamageEnabled(false);
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
            ShutterTargetRegistry3D.RemoveFreezeEntry(this);
            runtimeMarked = false;
            return;
        }

        // Mark state is cleared first so OnEnable/physics callbacks cannot
        // observe the object as frozen while the snapshot is being restored.
        markPhysicsCaptured = false;
        runtimeMarked = false;
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
        RestoreDamageDealerStates();
        if (fallingBox == null) fallingBox = GetComponent<FallingBoxObject>();
        fallingBox?.RefreshAfterMarkReleased();
        ClearSnapshot();
        ShutterTargetRegistry3D.RemoveFreezeEntry(this);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        runtimeRegistryReleasedCount++;
        runtimeRegistryRegistered = false;
#endif
        ApplyVisual();
        enabled = false;
        ReapplyHiddenWorldPolicyOnly();
    }

    private void ReapplyHiddenWorldPolicyOnly()
    {
        WorldPresence presence = GetComponentInParent<WorldPresence>(true);
        if (presence != null && presence.IsHiddenByCurrentWorld()) presence.ReapplyCurrentWorldPolicy();
    }

    private void ClearSnapshot()
    {
        storedKinematic = false;
        storedUseGravity = false;
        storedConstraints = RigidbodyConstraints.None;
        storedCollisionDetectionMode = CollisionDetectionMode.Discrete;
        storedInterpolation = RigidbodyInterpolation.None;
        storedLinearVelocity = Vector3.zero;
        storedAngularVelocity = Vector3.zero;
        storedDamageDealerEnabled = System.Array.Empty<bool>();
        storedLegacyDamageDealerEnabled = System.Array.Empty<bool>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        runtimeHasMarkSnapshot = false;
        runtimeSnapshotDiscardedCount++;
        runtimeSnapshotInstanceId = 0;
#endif
    }

    private void CaptureDamageDealerStates()
    {
        CacheDamageDealerReferences();
        storedDamageDealerEnabled = new bool[damageDealers.Length];
        for (int i = 0; i < damageDealers.Length; i++)
            storedDamageDealerEnabled[i] = damageDealers[i] != null && damageDealers[i].enabled;
        storedLegacyDamageDealerEnabled = new bool[legacyDamageDealers.Length];
        for (int i = 0; i < legacyDamageDealers.Length; i++)
            storedLegacyDamageDealerEnabled[i] = legacyDamageDealers[i] != null && legacyDamageDealers[i].enabled;
    }

    private void CacheDamageDealerReferences()
    {
        if (damageDealers == null || damageDealers.Length == 0)
            damageDealers = GetComponentsInChildren<DamageDealer>(true);
        if (legacyDamageDealers == null || legacyDamageDealers.Length == 0)
            legacyDamageDealers = GetComponentsInChildren<GravityObjectDamageDealer>(true);
    }

    private void SetMarkedDamageEnabled(bool enabled)
    {
        for (int i = 0; i < damageDealers.Length; i++)
        {
            DamageDealer dealer = damageDealers[i];
            if (dealer == null) continue;
            dealer.enabled = enabled;
            if (!enabled) dealer.ClearDamagedTargets();
        }
        for (int i = 0; i < legacyDamageDealers.Length; i++)
            if (legacyDamageDealers[i] != null) legacyDamageDealers[i].enabled = enabled;
    }

    private void RestoreDamageDealerStates()
    {
        for (int i = 0; i < damageDealers.Length; i++)
            if (damageDealers[i] != null) damageDealers[i].enabled = i < storedDamageDealerEnabled.Length && storedDamageDealerEnabled[i];
        for (int i = 0; i < legacyDamageDealers.Length; i++)
            if (legacyDamageDealers[i] != null) legacyDamageDealers[i].enabled = i < storedLegacyDamageDealerEnabled.Length && storedLegacyDamageDealerEnabled[i];
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
