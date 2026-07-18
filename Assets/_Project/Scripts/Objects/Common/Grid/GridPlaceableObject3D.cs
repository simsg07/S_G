using UnityEngine;

[DisallowMultipleComponent]
public class GridPlaceableObject3D : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] private float cellSize = 1f;
    [SerializeField] private int widthCells = 1;
    [SerializeField] private int heightCells = 1;
    [SerializeField] private float depth = 1f;
    [SerializeField] private bool snapPositionOnValidate = true;
    [SerializeField] private bool applyColliderSizeOnValidate = true;
    [SerializeField] private bool applyVisualScaleOnValidate;

    [Header("Pivot")]
    [SerializeField] private bool useBottomLeftPivot;
    [SerializeField] private Vector3 colliderCenterOffset = Vector3.zero;

    [Header("References")]
    [SerializeField] private BoxCollider boxCollider;
    [SerializeField] private Transform visualRoot;

    [Header("Debug")]
    [SerializeField] private bool showGizmo = true;
    [SerializeField] private bool debugMode = true;

    private void Reset()
    {
        CacheReferences();
    }

    private void OnValidate()
    {
        cellSize = Mathf.Max(0.01f, cellSize);
        widthCells = Mathf.Max(1, widthCells);
        heightCells = Mathf.Max(1, heightCells);
        depth = Mathf.Max(0.01f, depth);
        CacheReferences();

        if (snapPositionOnValidate)
        {
            SnapToGrid();
        }

        if (applyColliderSizeOnValidate)
        {
            ApplyColliderSize();
        }

        if (applyVisualScaleOnValidate)
        {
            ApplyVisualScaleOptional();
        }
    }

    [ContextMenu("Snap To Grid")]
    public void SnapToGrid()
    {
        Vector3 position = transform.position;
        position.x = Mathf.Round(position.x / cellSize) * cellSize;
        position.y = Mathf.Round(position.y / cellSize) * cellSize;
        transform.position = position;
        Log("Snapped to grid.");
    }

    [ContextMenu("Apply Collider Size")]
    public void ApplyColliderSize()
    {
        CacheReferences();
        if (boxCollider == null)
        {
            WarnMissing(nameof(BoxCollider));
            return;
        }

        Vector3 size = new Vector3(widthCells * cellSize, heightCells * cellSize, depth);
        boxCollider.size = size;
        boxCollider.center = useBottomLeftPivot
            ? new Vector3(size.x * 0.5f, size.y * 0.5f, 0f) + colliderCenterOffset
            : colliderCenterOffset;

        Log($"Collider size applied: {size}");
    }

    public void ApplyVisualScaleOptional()
    {
        if (visualRoot == null)
        {
            return;
        }

        visualRoot.localScale = new Vector3(widthCells * cellSize, heightCells * cellSize, depth);
        if (useBottomLeftPivot)
        {
            visualRoot.localPosition = new Vector3(widthCells * cellSize * 0.5f, heightCells * cellSize * 0.5f, 0f);
        }
    }

    [ContextMenu("Apply Grid Setup")]
    public void ApplyGridSetup()
    {
        SnapToGrid();
        ApplyColliderSize();
        ApplyVisualScaleOptional();
    }

    [ContextMenu("Validate Grid Setup")]
    public void ValidateGridSetup()
    {
        CacheReferences();
        Log($"Grid: cell={cellSize}, width={widthCells}, height={heightCells}, depth={depth}");
        LogComponent("BoxCollider", boxCollider);
        LogComponent("VisualRoot", visualRoot);
    }

    private void CacheReferences()
    {
        if (boxCollider == null)
        {
            boxCollider = GetComponent<BoxCollider>();
        }

        if (visualRoot == null)
        {
            Transform foundVisual = transform.Find("Visual");
            if (foundVisual != null)
            {
                visualRoot = foundVisual;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmo)
        {
            return;
        }

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
        Vector3 size = new Vector3(widthCells * Mathf.Max(0.01f, cellSize), heightCells * Mathf.Max(0.01f, cellSize), Mathf.Max(0.01f, depth));
        Vector3 center = transform.position + (useBottomLeftPivot ? new Vector3(size.x * 0.5f, size.y * 0.5f, 0f) : Vector3.zero) + colliderCenterOffset;
        Gizmos.DrawWireCube(center, size);
    }

    private void LogComponent(string label, Object component)
    {
        if (!debugMode)
        {
            return;
        }

        if (component != null)
        {
            Debug.Log($"[GridPlaceableObject3D] {label} found: {component.GetType().Name}", this);
            return;
        }

        Debug.LogWarning($"[GridPlaceableObject3D] {label} not assigned.", this);
    }

    private void WarnMissing(string componentName)
    {
        if (debugMode)
        {
            Debug.LogWarning($"[GridPlaceableObject3D] {componentName} is missing.", this);
        }
    }

    private void Log(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[GridPlaceableObject3D] {message}", this);
        }
    }
}
