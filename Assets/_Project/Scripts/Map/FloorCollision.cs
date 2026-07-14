using UnityEngine;
using UnityEngine.Serialization;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class FloorCollision : MonoBehaviour
{
    [Header("Collision Size")]
    [SerializeField] private float width = 4f;
    [FormerlySerializedAs("height")]
    [Tooltip("Thin walkable collision plate height. Default 0.08 keeps the floor readable in Scene view without feeling thick.")]
    [SerializeField] private float colliderHeight = 0.08f;
    [FormerlySerializedAs("depth")]
    [Tooltip("3D physics depth on the Z axis. Keep this narrow for 2.5D floor collision.")]
    [SerializeField] private float colliderDepth = 0.3f;

    [Header("Top Surface Placement")]
    [FormerlySerializedAs("useCollisionYOffset")]
    [Tooltip("Treat this object's origin as the visible floor tile's top surface.")]
    [SerializeField] private bool alignToTopSurface = true;
    [FormerlySerializedAs("visualReferenceOffset")]
    [Tooltip("Height of the visible floor tile used as designer reference. The Floor_Collision root should be placed at the tile top surface.")]
    [SerializeField] private float visualTileHeight = 1f;
    [Tooltip("Recommended range is 0.01 to 0.03 units. The default is 0.02.")]
    [SerializeField] private float collisionYOffset = 0.02f;

    [Header("Legacy Placement")]
    [Tooltip("GridSnapper is the primary placement component. This fallback keeps older prefabs compatible.")]
    [SerializeField] private bool snapToGrid = true;
    [SerializeField] private float gridSize = 1f;

    [Header("Debug")]
    [Tooltip("Shows a Scene-view Gizmo only. Floor_Collision has no Renderer and remains invisible in Game/build views.")]
    [SerializeField] private bool showDebugVisual = true;
    [SerializeField] private bool debugMode;

    public Vector3 Size => new Vector3(width, colliderHeight, colliderDepth);
    public float AppliedCollisionYOffset => alignToTopSurface ? collisionYOffset : 0f;
    public float VisualTileHeight => visualTileHeight;
    public Vector3 ColliderCenter => new Vector3(0f, AppliedCollisionYOffset + colliderHeight * 0.5f, 0f);

    private void Reset()
    {
        ApplySettings();
    }

    private void OnValidate()
    {
        width = Mathf.Max(0.01f, width);
        colliderHeight = Mathf.Max(0.01f, colliderHeight);
        colliderDepth = Mathf.Max(0.01f, colliderDepth);
        visualTileHeight = Mathf.Max(0.01f, visualTileHeight);
        gridSize = Mathf.Max(0.01f, gridSize);
        ApplySettings();
    }

    [ContextMenu("Apply Floor Collision")]
    public void ApplySettings()
    {
        BoxCollider floorCollider = GetComponent<BoxCollider>();
        if (floorCollider != null)
        {
            floorCollider.isTrigger = false;
            floorCollider.center = ColliderCenter;
            floorCollider.size = Size;
        }

        if (!Application.isPlaying && snapToGrid)
        {
            Vector3 position = transform.position;
            position.x = Mathf.Round(position.x / gridSize) * gridSize;
            position.y = Mathf.Round(position.y / gridSize) * gridSize;
            transform.position = position;
        }

        if (debugMode && gameObject.layer != LayerMask.NameToLayer("Ground"))
        {
            Debug.LogWarning($"[FloorCollision] '{name}' should normally use the Ground layer. Assign it manually in the Inspector.", this);
        }
    }

    private void OnDrawGizmos()
    {
        if (!showDebugVisual)
        {
            return;
        }

        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.1f, 0.9f, 0.45f, 0.2f);
        Gizmos.DrawCube(ColliderCenter, Size);
        Gizmos.color = new Color(0.1f, 1f, 0.55f, 0.9f);
        Gizmos.DrawWireCube(ColliderCenter, Size);
        Gizmos.matrix = oldMatrix;
    }
}
