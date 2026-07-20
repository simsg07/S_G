using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public class CraneCableVisual3D : MonoBehaviour
{
    [Header("Cable Points")]
    [Tooltip("Lever side connection point.")]
    [SerializeField] private Transform startPoint;
    [Tooltip("Crane side connection point.")]
    [SerializeField] private Transform endPoint;
    [SerializeField] private LineRenderer lineRenderer;

    [Header("Visual")]
    [Tooltip("Update the cable every frame so the end follows the moving Crane.")]
    [SerializeField] private bool updateEveryFrame = true;
    [Min(0.001f)]
    [SerializeField] private float lineWidth = 0.05f;
    [SerializeField] private int sortingOrder = 10;

    [Header("Debug")]
    [SerializeField] private bool showGizmo = true;
    [SerializeField] private bool debugMode = true;

    private void Reset()
    {
        CacheRenderer();
        ApplyLineSettings();
    }

    private void OnValidate()
    {
        lineWidth = Mathf.Max(0.001f, lineWidth);
        CacheRenderer();
        ApplyLineSettings();
        RefreshCable(false);
    }

    private void Awake()
    {
        CacheRenderer();
        ApplyLineSettings();
        RefreshCable(false);
    }

    private void LateUpdate()
    {
        if (updateEveryFrame)
        {
            RefreshCable();
        }
    }

    [ContextMenu("Refresh Cable")]
    public void RefreshCable()
    {
        RefreshCable(true);
    }

    private void RefreshCable(bool logMissingReferences)
    {
        CacheRenderer();
        if (lineRenderer == null)
        {
            if (logMissingReferences)
            {
                Warn("LineRenderer is missing.");
            }

            return;
        }

        ApplyLineSettings();
        lineRenderer.positionCount = 2;

        if (startPoint == null || endPoint == null)
        {
            if (logMissingReferences)
            {
                Warn($"Cable points are missing. Start={(startPoint != null ? startPoint.name : "None")}, End={(endPoint != null ? endPoint.name : "None")}");
            }

            return;
        }

        lineRenderer.SetPosition(0, startPoint.position);
        lineRenderer.SetPosition(1, endPoint.position);
    }

    public void SetPoints(Transform start, Transform end)
    {
        startPoint = start;
        endPoint = end;
        RefreshCable();
    }

    public void SetPointsFromObjects(Transform leverRoot, Transform craneRoot)
    {
        startPoint = FindCablePoint(leverRoot);
        endPoint = FindCablePoint(craneRoot);
        RefreshCable();
    }

    [ContextMenu("Validate Cable Setup")]
    public void ValidateCableSetup()
    {
        CacheRenderer();
        Debug.Log(
            "[CraneCableVisual3D] Validate Cable Setup\n" +
            $"- Start Point: {(startPoint != null ? startPoint.name : "None")}\n" +
            $"- End Point: {(endPoint != null ? endPoint.name : "None")}\n" +
            $"- LineRenderer: {(lineRenderer != null)}\n" +
            $"- Width: {lineWidth}\n" +
            $"- Sorting Order: {sortingOrder}",
            this);
    }

    private static Transform FindCablePoint(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        Transform point = root.Find("CablePoint");
        if (point != null)
        {
            return point;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == "CablePoint")
            {
                return children[i];
            }
        }

        return null;
    }

    private void CacheRenderer()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }
    }

    private void ApplyLineSettings()
    {
        if (lineRenderer == null)
        {
            return;
        }

        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.positionCount = Mathf.Max(2, lineRenderer.positionCount);
        lineRenderer.sortingOrder = sortingOrder;
        lineRenderer.enabled = true;
    }

    private void OnDrawGizmos()
    {
        if (!showGizmo)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        if (startPoint != null)
        {
            Gizmos.DrawSphere(startPoint.position, 0.08f);
        }

        if (endPoint != null)
        {
            Gizmos.DrawSphere(endPoint.position, 0.08f);
        }

        if (startPoint != null && endPoint != null)
        {
            Gizmos.DrawLine(startPoint.position, endPoint.position);
        }
    }

    private void Warn(string message)
    {
        if (debugMode)
        {
            Debug.LogWarning($"[CraneCableVisual3D] {message}", this);
        }
    }
}
