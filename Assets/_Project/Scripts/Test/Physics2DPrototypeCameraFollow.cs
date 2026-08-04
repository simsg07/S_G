using UnityEngine;

[DisallowMultipleComponent]
public sealed class Physics2DPrototypeCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField, Min(0.01f)] private float smoothTime = 0.15f;
    [SerializeField] private Vector3 offset = new Vector3(0f, 1f, -10f);

    private Vector3 velocity;

    public void SetTarget(Transform value) => target = value;

    private void LateUpdate()
    {
        if (target == null) return;
        Vector3 desired = target.position + offset;
        transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
    }
}
