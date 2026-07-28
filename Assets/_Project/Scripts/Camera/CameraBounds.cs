using UnityEngine;

[DisallowMultipleComponent]
public class CameraBounds : MonoBehaviour
{
    [SerializeField] private BoxCollider boundsCollider;
    [SerializeField] private Vector3 center;
    [SerializeField] private Vector3 size = new Vector3(30f, 15f, 1f);

    public Bounds WorldBounds
    {
        get
        {
            if (boundsCollider != null)
            {
                return boundsCollider.bounds;
            }

            return new Bounds(transform.TransformPoint(center), Vector3.Scale(size, transform.lossyScale));
        }
    }

    private void Reset()
    {
        boundsCollider = GetComponent<BoxCollider>();
    }

    private void OnValidate()
    {
        size.x = Mathf.Max(0f, size.x);
        size.y = Mathf.Max(0f, size.y);
        size.z = Mathf.Max(0f, size.z);
    }

    private void OnDrawGizmos()
    {
        Bounds bounds = WorldBounds;
        Gizmos.color = new Color(0.15f, 0.85f, 1f, 0.8f);
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
}
