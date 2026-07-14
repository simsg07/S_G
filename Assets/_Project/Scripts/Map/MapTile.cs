using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class MapTile : MonoBehaviour
{
    [Header("Placement")]
    [Tooltip("Designer-facing note describing how this prefab should be placed.")]
    [TextArea(2, 4)]
    [SerializeField] private string placementNote = "Place on the 1-unit grid. Keep the root scale at (1, 1, 1).";
    [Tooltip("Width and height in map units. Use these values instead of scaling the root transform.")]
    [SerializeField] private Vector2 tileSize = Vector2.one;

    [Header("Collision Shape")]
    [Tooltip("Depth of the optional 3D BoxCollider.")]
    [SerializeField] private float colliderDepth = 0.3f;
    [Tooltip("Local center of the optional BoxCollider.")]
    [SerializeField] private Vector3 colliderCenter;

    [Tooltip("Disable for Tile_Visual. The actual walkable floor collision belongs to Floor_Collision.")]
    [SerializeField] private bool configureCollider = true;
    [Header("Validation / Debug")]
    [SerializeField] private bool warnForMultipleColliders = true;
    [SerializeField] private bool showColliderGizmo = true;

    [Header("World A / B")]
    [Tooltip("Optional Visual_A / Visual_B slot controller. This does not change the world system itself.")]
    [SerializeField] private MapTileWorldVisual worldVisual;

    private bool multipleColliderWarningLogged;

    public Vector2 TileSize => tileSize;
    public string PlacementNote => placementNote;
    public MapTileWorldVisual WorldVisual => worldVisual;

    private void Reset()
    {
        AutoFillWorldVisual();
        ApplyColliderShape();
    }

    private void OnValidate()
    {
        tileSize.x = Mathf.Max(0.01f, tileSize.x);
        tileSize.y = Mathf.Max(0.01f, tileSize.y);
        colliderDepth = Mathf.Max(0.01f, colliderDepth);
        AutoFillWorldVisual();
        ApplyColliderShape();
        ValidateColliderCount();
    }

    private void AutoFillWorldVisual()
    {
        if (worldVisual == null)
        {
            worldVisual = GetComponent<MapTileWorldVisual>();
        }
    }

    [ContextMenu("Apply Tile Collider")]
    public void ApplyColliderShape()
    {
        if (!configureCollider)
        {
            return;
        }

        BoxCollider tileCollider = GetComponent<BoxCollider>();
        if (tileCollider == null)
        {
            return;
        }

        tileCollider.isTrigger = false;
        tileCollider.center = colliderCenter;
        tileCollider.size = new Vector3(tileSize.x, tileSize.y, colliderDepth);
    }

    private void ValidateColliderCount()
    {
        if (!warnForMultipleColliders)
        {
            multipleColliderWarningLogged = false;
            return;
        }

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        if (colliders.Length <= 1)
        {
            multipleColliderWarningLogged = false;
            return;
        }

        if (!multipleColliderWarningLogged)
        {
            Debug.LogWarning(
                $"[MapTile] '{name}' has {colliders.Length} colliders. Solid map tiles should normally use one BoxCollider.",
                this);
            multipleColliderWarningLogged = true;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showColliderGizmo)
        {
            return;
        }

        Gizmos.color = new Color(0.2f, 0.9f, 0.45f, 0.8f);
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(colliderCenter, new Vector3(tileSize.x, tileSize.y, colliderDepth));
        Gizmos.matrix = oldMatrix;
    }
}
