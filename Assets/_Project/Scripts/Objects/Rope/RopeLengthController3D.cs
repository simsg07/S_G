using UnityEngine;

[DisallowMultipleComponent]
public sealed class RopeLengthController3D : MonoBehaviour
{
    private const float MinimumLength = 0.001f;
    private const float MinimumThickness = 0.01f;

    [Header("Length Points")]
    [SerializeField] private Transform ceilingAnchor;
    [SerializeField] private Transform ropeEndPoint;
    [Header("Visible Debug Box")]
    [SerializeField] private Transform ropeDebugVisual;
    [Header("3D Hit Collider")]
    [SerializeField] private BoxCollider ropeHitCollider;
    [SerializeField, Min(MinimumThickness)] private float ropeThickness = 0.15f;
    [SerializeField] private Vector3 colliderExtraSize;
    [Header("Update Rules")]
    [SerializeField] private bool updateOnValidate;
    [SerializeField] private bool updateOnStart = true;
    [SerializeField] private bool preserveManualOffsets = true;
    [Header("Debug")]
    [SerializeField] private bool debugMode = true;
    [SerializeField] private bool showGizmo = true;

    public float CurrentLength { get; private set; }
    public bool PreserveManualOffsets => preserveManualOffsets;

    private void Start()
    {
        if (updateOnStart)
        {
            ApplyRopeLength();
        }
    }

    private void OnValidate()
    {
        ropeThickness = IsFinite(ropeThickness) ? Mathf.Max(MinimumThickness, ropeThickness) : 0.15f;
        colliderExtraSize = SanitizeSize(colliderExtraSize);
        if (updateOnValidate && !Application.isPlaying)
        {
            ApplyRopeLength();
        }
    }

    public void ConfigureReferences(Transform ceiling, Transform endPoint, BoxCollider hitCollider, Transform debugVisual)
    {
        ceilingAnchor = ceiling;
        ropeEndPoint = endPoint;
        ropeHitCollider = hitCollider;
        ropeDebugVisual = debugVisual;
    }

    [ContextMenu("Apply Rope Length")]
    public void ApplyRopeLength()
    {
        if (!TryGetGeometry(out Vector3 start, out Vector3 end, out Vector3 direction, out float length)) return;
        CurrentLength = length;
        UpdateCollider(start, end, direction, length);
        UpdateDebugVisual();
        Log($"Applied rope length: {length:0.###}.");
    }

    [ContextMenu("Update Rope Debug Visual")]
    public void UpdateRopeDebugVisual()
    {
        UpdateDebugVisual();
    }

    [ContextMenu("Update Rope Collider")]
    public void UpdateRopeCollider()
    {
        if (TryGetGeometry(out Vector3 start, out Vector3 end, out Vector3 direction, out float length))
        {
            CurrentLength = length;
            UpdateCollider(start, end, direction, length);
        }
    }

    [ContextMenu("Validate Rope Length Setup")]
    public bool ValidateRopeLengthSetup()
    {
        if (ceilingAnchor == null || ropeEndPoint == null || ropeHitCollider == null || ropeDebugVisual == null)
        {
            LogWarning("Ceiling Anchor, Rope End Point, 3D Rope Hit Collider, and Rope Debug Visual are required.");
            return false;
        }
        if (GetComponent<HitReceiver>() == null || !TryGetGeometry(out _, out _, out _, out _))
        {
            LogWarning("HitReceiver must remain on the Rope object and both length points must be valid.");
            return false;
        }
        Log("Rope length setup validation passed.");
        return true;
    }

    private void UpdateCollider(Vector3 start, Vector3 end, Vector3 direction, float length)
    {
        if (ropeHitCollider == null) return;
        Transform target = ropeHitCollider.transform;
        Transform parent = target.parent;
        target.localPosition = parent != null ? parent.InverseTransformPoint((start + end) * 0.5f) : (start + end) * 0.5f;
        target.localRotation = DirectionRotation(parent, direction);
        target.localScale = Vector3.one;
        ropeHitCollider.center = Vector3.zero;
        Vector3 extra = SanitizeSize(colliderExtraSize);
        ropeHitCollider.size = new Vector3(Mathf.Max(MinimumThickness, ropeThickness + extra.x),
            Mathf.Max(MinimumLength, length + extra.y), Mathf.Max(MinimumThickness, ropeThickness + extra.z));
    }

    private void UpdateDebugVisual()
    {
        if (ropeDebugVisual == null || ropeHitCollider == null) return;
        Transform colliderTransform = ropeHitCollider.transform;
        Transform visualParent = ropeDebugVisual.parent;
        ropeDebugVisual.position = colliderTransform.position;
        ropeDebugVisual.rotation = colliderTransform.rotation;
        Vector3 colliderWorldSize = Vector3.Scale(ropeHitCollider.size, Abs(colliderTransform.lossyScale));
        Vector3 parentScale = visualParent != null ? Abs(visualParent.lossyScale) : Vector3.one;
        ropeDebugVisual.localScale = SanitizeScale(new Vector3(
            colliderWorldSize.x / Mathf.Max(MinimumThickness, parentScale.x),
            colliderWorldSize.y / Mathf.Max(MinimumThickness, parentScale.y),
            colliderWorldSize.z / Mathf.Max(MinimumThickness, parentScale.z)));
    }

    private bool TryGetGeometry(out Vector3 start, out Vector3 end, out Vector3 direction, out float length)
    {
        start = end = Vector3.zero;
        direction = Vector3.down;
        length = 0f;
        if (ceilingAnchor == null || ropeEndPoint == null) return false;
        start = ceilingAnchor.position;
        end = ropeEndPoint.position;
        Vector3 delta = end - start;
        length = delta.magnitude;
        if (!IsFinite(start) || !IsFinite(end) || !IsFinite(length) || length < MinimumLength)
        {
            LogWarning("Rope points must be finite and separated by a non-zero distance.");
            return false;
        }
        direction = delta / length;
        return IsFinite(direction);
    }

    private static Quaternion DirectionRotation(Transform parent, Vector3 direction)
    {
        Vector3 local = parent != null ? parent.InverseTransformDirection(direction).normalized : direction.normalized;
        return Quaternion.FromToRotation(Vector3.up, local);
    }
    private static Vector3 SanitizeScale(Vector3 v) => new Vector3(SafePositive(v.x, 1f, MinimumThickness), SafePositive(v.y, 1f, MinimumLength), SafePositive(v.z, 1f, MinimumThickness));
    private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    private static Vector3 SanitizeSize(Vector3 v) => new Vector3(SafePositive(v.x, 0f, 0f), SafePositive(v.y, 0f, 0f), SafePositive(v.z, 0f, 0f));
    private static float SafePositive(float value, float fallback, float minimum) => IsFinite(value) ? Mathf.Max(minimum, Mathf.Abs(value)) : fallback;
    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

    private void OnDrawGizmosSelected()
    {
        if (!showGizmo || ceilingAnchor == null || ropeEndPoint == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(ceilingAnchor.position, ropeEndPoint.position);
        Gizmos.DrawWireSphere(ceilingAnchor.position, 0.1f);
        Gizmos.DrawWireSphere(ropeEndPoint.position, 0.1f);
    }
    private void Log(string message) { if (debugMode) Debug.Log($"[RopeLengthController3D] {message}", this); }
    private void LogWarning(string message) { if (debugMode) Debug.LogWarning($"[RopeLengthController3D] {message}", this); }
}
