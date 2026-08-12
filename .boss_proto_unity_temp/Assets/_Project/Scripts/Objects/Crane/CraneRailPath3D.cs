using UnityEngine;

[DisallowMultipleComponent]
public class CraneRailPath3D : MonoBehaviour
{
    [Header("Rail End Points")]
    [Tooltip("Crane travel point A.")]
    [SerializeField] private Transform pointA;
    [Tooltip("Crane travel point B.")]
    [SerializeField] private Transform pointB;

    [Header("Functional Debug Visual")]
    [Tooltip("Opaque cube placed between Point_A and Point_B for image-free testing.")]
    [SerializeField] private Transform debugRailVisual;
    [Min(0.01f)] [SerializeField] private float debugRailThickness = 0.15f;

    [Header("Rail Limits")]
    [Tooltip("Keep the Crane on the straight A/B segment.")]
    [SerializeField] private bool clampToRail = true;
    [Tooltip("Recommended arrival distance for CraneObject instances using this rail.")]
    [Min(0.001f)]
    [SerializeField] private float arriveDistance = 0.03f;

    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private Color railColor = new Color(1f, 0.75f, 0.15f, 1f);
    [SerializeField] private bool debugMode = true;

    public bool IsValid => pointA != null && pointB != null;
    public Transform PointATransform => pointA;
    public Transform PointBTransform => pointB;
    public Vector3 PointA => pointA != null ? pointA.position : transform.position;
    public Vector3 PointB => pointB != null ? pointB.position : transform.position;
    public float ArriveDistance => arriveDistance;

    public Vector3 GetPointA() => PointA;
    public Vector3 GetPointB() => PointB;
    public Vector3 GetRailPointA() => PointA;
    public Vector3 GetRailPointB() => PointB;

    public Vector3 GetOtherPoint(Vector3 currentPosition)
    {
        return Vector3.SqrMagnitude(currentPosition - PointA) <=
               Vector3.SqrMagnitude(currentPosition - PointB) ? PointB : PointA;
    }

    public Vector3 GetOtherRailPoint(Vector3 currentRailPosition) => GetOtherPoint(currentRailPosition);

    public Vector3 GetClosestRailPoint(Vector3 position)
    {
        return Vector3.SqrMagnitude(position - PointA) <= Vector3.SqrMagnitude(position - PointB)
            ? PointA
            : PointB;
    }

    public bool IsNearPointA(Vector3 position) =>
        IsValid && Vector3.Distance(position, PointA) <= arriveDistance;

    public bool IsNearPointB(Vector3 position) =>
        IsValid && Vector3.Distance(position, PointB) <= arriveDistance;

    public Vector3 ClampToRail(Vector3 position) => ClampPosition(position);
    public Vector3 ClampToRailSegment(Vector3 position) => ClampPosition(position);

    [ContextMenu("Update Debug Rail Visual")]
    public void UpdateDebugRailVisual()
    {
        if (!IsValid || debugRailVisual == null)
        {
            Debug.LogWarning("[CraneRailPath3D] Debug rail visual or Point_A / Point_B is missing.", this);
            return;
        }

        Vector3 direction = PointB - PointA;
        float length = direction.magnitude;
        debugRailVisual.position = (PointA + PointB) * 0.5f;
        debugRailVisual.rotation = length > Mathf.Epsilon
            ? Quaternion.FromToRotation(Vector3.right, direction.normalized)
            : Quaternion.identity;
        Transform visualParent = debugRailVisual.parent;
        Vector3 parentScale = visualParent != null ? visualParent.lossyScale : Vector3.one;
        debugRailVisual.localScale = new Vector3(
            length / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
            debugRailThickness / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
            debugRailThickness / Mathf.Max(0.0001f, Mathf.Abs(parentScale.z)));
    }

    public Vector3 GetPoint(bool usePointB)
    {
        return usePointB ? PointB : PointA;
    }

    public Vector3 ClampPosition(Vector3 position)
    {
        if (!clampToRail || !IsValid)
        {
            return position;
        }

        Vector3 start = PointA;
        Vector3 segment = PointB - start;
        float lengthSquared = segment.sqrMagnitude;
        if (lengthSquared <= Mathf.Epsilon)
        {
            return start;
        }

        float t = Mathf.Clamp01(Vector3.Dot(position - start, segment) / lengthSquared);
        return start + segment * t;
    }

    [ContextMenu("Validate Rail Setup")]
    public void ValidateRailSetup()
    {
        if (!IsValid)
        {
            Debug.LogWarning("[CraneRailPath3D] Point_A and Point_B must both be assigned.", this);
            return;
        }

        if (debugMode)
        {
            Debug.Log($"[CraneRailPath3D] A={PointA}, B={PointB}, Length={Vector3.Distance(PointA, PointB):0.###}, ArriveDistance={arriveDistance:0.###}", this);
        }
    }

    [ContextMenu("Validate Rail Path")]
    private void ValidateRailPathLegacy()
    {
        ValidateRailSetup();
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos || !IsValid)
        {
            return;
        }

        Gizmos.color = railColor;
        Gizmos.DrawLine(PointA, PointB);
        Gizmos.DrawWireSphere(PointA, 0.2f);
        Gizmos.DrawWireSphere(PointB, 0.2f);
    }
}
