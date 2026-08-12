using UnityEngine;
using UnityEngine.Serialization;

[ExecuteAlways]
[DisallowMultipleComponent]
public class CraneMovingVisualAligner3D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CraneRailPath3D railPath;
    [SerializeField] private Transform craneCabinRoot;
    [SerializeField] private Transform trolleyVisual;
    [SerializeField] private Transform upperConnectorVisual;
    [SerializeField] private Transform cabinVisual;
    [SerializeField] private Transform trolleyCablePoint;
    [SerializeField] private Transform cabinCablePoint;
    [SerializeField] private LineRenderer cableLineRenderer;

    [Header("Visual Alignment")]
    [SerializeField] private float cabinYOffset = -3f;
    [SerializeField] private float trolleyYOffset;
    [SerializeField] private Vector3 cabinVisualOffset;
    [SerializeField] private Vector3 trolleyVisualOffset;
    [FormerlySerializedAs("alignOnStart")]
    [SerializeField] private bool alignVisualsOnStart;
    [SerializeField] private bool alignVisualsOnValidate;
    [FormerlySerializedAs("updateEveryFrame")]
    [SerializeField] private bool updateCableEveryFrame = true;
    [SerializeField] private bool preserveManualVisualOffsets = true;
    [SerializeField] private bool debugMode = true;

    private void Reset()
    {
        if (craneCabinRoot == null) craneCabinRoot = transform;
    }

    private void Start()
    {
        if (alignVisualsOnStart) AlignVisualsOnce();
    }

    private void LateUpdate()
    {
        if (updateCableEveryFrame) RefreshCableOnly();
    }

    private void OnValidate()
    {
        if (alignVisualsOnValidate) AlignVisualsOnce();
    }

    [ContextMenu("Align Visuals Once")]
    public void AlignVisualsOnce()
    {
        if (!HasRequiredAlignmentReferences(false)) return;

        Vector3 cabinPosition = craneCabinRoot.position;
        float railY = railPath.GetRailPointA().y;
        Vector3 trolleyPosition = new Vector3(cabinPosition.x, railY + trolleyYOffset, cabinPosition.z) + trolleyVisualOffset;

        if (!preserveManualVisualOffsets)
        {
            trolleyVisual.position = trolleyPosition;
            if (upperConnectorVisual != null)
            {
                upperConnectorVisual.position = trolleyPosition + new Vector3(0f, -0.45f, 0f);
            }

            cabinVisual.localPosition = cabinVisualOffset;
            if (trolleyCablePoint != null) trolleyCablePoint.position = trolleyPosition;
        }
        RefreshCableOnly();
    }

    [ContextMenu("Refresh Cable Only")]
    public void RefreshCableOnly()
    {
        if (cableLineRenderer == null || trolleyCablePoint == null || cabinCablePoint == null) return;
        cableLineRenderer.useWorldSpace = true;
        cableLineRenderer.positionCount = 2;
        cableLineRenderer.enabled = true;
        cableLineRenderer.SetPosition(0, trolleyCablePoint.position);
        cableLineRenderer.SetPosition(1, cabinCablePoint.position);
    }

    public void AlignVisuals() => AlignVisualsOnce();
    public void RefreshCableLine() => RefreshCableOnly();

    [ContextMenu("Validate Visual Setup")]
    public void ValidateVisualSetup()
    {
        bool valid = HasRequiredAlignmentReferences(true);
        Debug.Log(
            "[CraneMovingVisualAligner3D] Validate Visual Setup\n" +
            $"- Valid: {valid}\n" +
            $"- RailPath: {(railPath != null ? railPath.name : "None")}\n" +
            $"- Cabin Root: {(craneCabinRoot != null ? craneCabinRoot.name : "None")}\n" +
            $"- Trolley: {(trolleyVisual != null ? trolleyVisual.name : "None")}\n" +
            $"- Upper Connector: {(upperConnectorVisual != null ? upperConnectorVisual.name : "None")}\n" +
            $"- Cabin Visual: {(cabinVisual != null ? cabinVisual.name : "None")}\n" +
            $"- Cabin Y Offset: {cabinYOffset}", this);
    }

    private bool HasRequiredAlignmentReferences(bool logWarning)
    {
        bool valid = railPath != null && railPath.IsValid && craneCabinRoot != null && trolleyVisual != null && cabinVisual != null;
        if (!valid && logWarning && debugMode)
        {
            Debug.LogWarning("[CraneMovingVisualAligner3D] Visual references are incomplete. Crane movement is unaffected.", this);
        }
        return valid;
    }
}
