using UnityEngine;

public static class TwoPointFiveDUtility3D
{
    public const float GameplayPlaneZ = 0f;

    public static RigidbodyConstraints SideViewRigidbodyConstraints =>
        RigidbodyConstraints.FreezePositionZ |
        RigidbodyConstraints.FreezeRotationX |
        RigidbodyConstraints.FreezeRotationY |
        RigidbodyConstraints.FreezeRotationZ;

    public static void ConfigureRigidbodyForSideView(Rigidbody body, float planeZ = GameplayPlaneZ)
    {
        if (body == null)
        {
            return;
        }

        body.constraints |= SideViewRigidbodyConstraints;
        ClampRigidbodyToPlane(body, planeZ);
    }

    public static void ClampRigidbodyToPlane(Rigidbody body, float planeZ = GameplayPlaneZ)
    {
        if (body == null)
        {
            return;
        }

        ClampTransformToPlane(body.transform, planeZ);

        if (!Application.isPlaying)
        {
            return;
        }

        body.position = ProjectPositionToPlane(body.position, planeZ);
        body.linearVelocity = ProjectVelocityToPlane(body.linearVelocity);
        body.angularVelocity = Vector3.zero;
    }

    public static void ClampTransformToPlane(Transform target, float planeZ = GameplayPlaneZ)
    {
        if (target == null)
        {
            return;
        }

        Vector3 position = target.position;
        if (Mathf.Approximately(position.z, planeZ))
        {
            return;
        }

        position.z = planeZ;
        target.position = position;
    }

    public static Vector3 ProjectPositionToPlane(Vector3 position, float planeZ = GameplayPlaneZ)
    {
        position.z = planeZ;
        return position;
    }

    public static Vector3 ProjectVelocityToPlane(Vector3 velocity)
    {
        velocity.z = 0f;
        return velocity;
    }

    public static void ConfigureSideViewCamera(Camera camera, float orthographicSize)
    {
        if (camera == null)
        {
            return;
        }

        camera.orthographic = true;
        if (orthographicSize > 0f)
        {
            camera.orthographicSize = orthographicSize;
        }

        camera.transform.rotation = Quaternion.identity;
    }
}
