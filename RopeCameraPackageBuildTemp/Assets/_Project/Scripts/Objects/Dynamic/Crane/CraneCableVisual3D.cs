using UnityEngine;
using UnityEngine.Serialization;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public class CraneCableVisual3D : MonoBehaviour
{
    [Header("Cable Points")]
    [FormerlySerializedAs("endPoint")]
    [SerializeField] private Transform cabinCablePoint;
    [SerializeField] private Transform cabinTransform;
    [SerializeField] private CraneRailPath3D railPath;
    [SerializeField] private LineRenderer lineRenderer;

    [Header("Visual")]
    [Tooltip("Update the cable every frame so the end follows the moving Crane.")]
    [SerializeField] private bool updateEveryFrame = true;
    [SerializeField] private float cableTopYOffset;
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

        Transform bottomTransform = cabinCablePoint != null ? cabinCablePoint : cabinTransform;
        if (bottomTransform == null || railPath == null || !railPath.IsValid)
        {
            if (logMissingReferences)
            {
                Warn($"Cable references are missing. Cabin={(bottomTransform != null ? bottomTransform.name : "None")}, RailPath={(railPath != null ? railPath.name : "None")}");
            }

            return;
        }

        Vector3 bottomPoint = bottomTransform.position;
        float railY = railPath.GetRailPointA().y;
        Vector3 topPoint = new Vector3(bottomPoint.x, railY + cableTopYOffset, bottomPoint.z);
        lineRenderer.SetPosition(0, topPoint);
        lineRenderer.SetPosition(1, bottomPoint);
    }

    public void SetPoints(Transform start, Transform end)
    {
        cabinTransform = end;
        cabinCablePoint = end;
        railPath = start != null ? start.GetComponentInParent<CraneRailPath3D>() : null;
        RefreshCable();
    }

    public void SetVisualReferences(Transform cabin, Transform cablePoint, CraneRailPath3D path)
    {
        cabinTransform = cabin;
        cabinCablePoint = cablePoint;
        railPath = path;
        RefreshCable();
    }

    public void SetPointsFromObjects(Transform leverRoot, Transform craneRoot)
    {
        cabinTransform = craneRoot;
        cabinCablePoint = FindCablePoint(craneRoot);
        RefreshCable();
    }

    [ContextMenu("Validate Cable Setup")]
    public void ValidateCableSetup()
    {
        CacheRenderer();
        Debug.Log(
            "[CraneCableVisual3D] Validate Cable Setup\n" +
            $"- Cabin Transform: {(cabinTransform != null ? cabinTransform.name : "None")}\n" +
            $"- Cabin Cable Point: {(cabinCablePoint != null ? cabinCablePoint.name : "None")}\n" +
            $"- Rail Path: {(railPath != null ? railPath.name : "None")}\n" +
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
        Transform bottomTransform = cabinCablePoint != null ? cabinCablePoint : cabinTransform;
        if (railPath != null && railPath.IsValid && bottomTransform != null)
        {
            Vector3 bottom = bottomTransform.position;
            Vector3 top = new Vector3(bottom.x, railPath.GetRailPointA().y + cableTopYOffset, bottom.z);
            Gizmos.DrawSphere(top, 0.08f);
            Gizmos.DrawSphere(bottom, 0.08f);
            Gizmos.DrawLine(top, bottom);
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
