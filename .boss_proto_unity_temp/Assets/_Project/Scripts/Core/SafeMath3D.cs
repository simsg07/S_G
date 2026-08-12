using UnityEngine;

public static class SafeMath3D
{
    public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    public static bool IsFinite(Vector3 value) =>
        IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

    public static Vector3 SafeVector3(Vector3 value, Vector3 fallback) =>
        IsFinite(value) ? value : fallback;

    public static bool IsValidTransform(Transform target) =>
        target != null && IsFinite(target.position) && IsFinite(target.localScale);
}
