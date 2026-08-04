using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class GravityDropSensor : MonoBehaviour
{
    [Header("Detection From Prefab")]
    [FormerlySerializedAs("boxCenterOffset")]
    [SerializeField] private Vector3 detectionCenterOffset = new Vector3(0f, -2f, 0f);
    [FormerlySerializedAs("boxSize")]
    [SerializeField] private Vector3 detectionBoxSize = new Vector3(3f, 4f, 1f);
    [SerializeField] private bool usePlayerTagFallback = true;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool detectOnlyOnce = true;

    [Header("State")]
    [SerializeField] private bool hasDetected;

    [Header("Layer")]
    [SerializeField] private LayerMask playerLayerMask;

    [Header("Target")]
    [SerializeField] private StoneObject stoneObject;
    [SerializeField] private FallingBoxObject fallingBoxObject;

    [Header("Debug")]
    [SerializeField] private bool showGizmo = true;
    [SerializeField] private bool debugMode = true;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnValidate()
    {
        detectionBoxSize.x = Mathf.Max(0.01f, detectionBoxSize.x);
        detectionBoxSize.y = Mathf.Max(0.01f, detectionBoxSize.y);
        detectionBoxSize.z = Mathf.Max(0.01f, detectionBoxSize.z);
        CacheReferences();
    }

    private void Update()
    {
        if (detectOnlyOnce && hasDetected)
        {
            return;
        }

        if (CheckPlayerInDetectionBox())
        {
            TriggerDropTarget();
        }
    }

    public void ApplyDetectionData(ObjectData data)
    {
        if (data == null)
        {
            Debug.LogWarning("[GravityDropSensor] ObjectData is not assigned. Detection settings were not applied.", this);
            return;
        }

        detectionCenterOffset = data.gravityDetectionCenterOffset;
        detectionBoxSize = new Vector3(
            Mathf.Max(0.01f, data.gravityDetectionBoxSize.x),
            Mathf.Max(0.01f, data.gravityDetectionBoxSize.y),
            Mathf.Max(0.01f, data.gravityDetectionBoxSize.z));
        detectOnlyOnce = data.gravityDetectOnlyOnce;
        usePlayerTagFallback = data.usePlayerTagFallback;
        playerTag = string.IsNullOrWhiteSpace(data.playerTag) ? "Player" : data.playerTag;
        enabled = data.useGravityDropSensor;

        Log("Detection data applied from ObjectData.");
    }

    [ContextMenu("Test Check Player In Detection Box")]
    public void TestCheckPlayerInDetectionBox()
    {
        Log($"Player in detection box: {CheckPlayerInDetectionBox()}");
    }

    public bool CheckPlayerInDetectionBox()
    {
        Vector3 center = transform.position + detectionCenterOffset;
        Vector3 halfExtents = detectionBoxSize * 0.5f;
        int layerMask = playerLayerMask.value != 0 ? playerLayerMask.value : ~0;
        Collider[] overlaps = Physics.OverlapBox(
            center,
            halfExtents,
            Quaternion.identity,
            layerMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < overlaps.Length; i++)
        {
            Collider candidate = overlaps[i];
            if (candidate == null || candidate.transform.IsChildOf(transform))
            {
                continue;
            }

            if (playerLayerMask.value != 0)
            {
                Log("Player detected in range.");
                return true;
            }

            if (usePlayerTagFallback && IsPlayerTagged(candidate.transform))
            {
                Log("Player detected in range.");
                return true;
            }
        }

        return false;
    }

    [ContextMenu("Test Trigger Drop")]
    public void TriggerDropTarget()
    {
        if (detectOnlyOnce && hasDetected)
        {
            return;
        }

        if (stoneObject != null)
        {
            hasDetected = true;
            stoneObject.TriggerDrop();
            Log("TriggerDrop called immediately.");
            return;
        }

        if (fallingBoxObject != null)
        {
            hasDetected = true;
            fallingBoxObject.TriggerDrop();
            Log("TriggerDrop called immediately.");
            return;
        }

        Debug.LogWarning("[GravityDropSensor] No drop target assigned.", this);
    }

    [ContextMenu("Reset Sensor")]
    public void ResetSensor()
    {
        hasDetected = false;
        Log("Sensor reset.");
    }

    [ContextMenu("Validate Drop Sensor")]
    public void ValidateSensorSetup()
    {
        CacheReferences();
        Log($"Detection box center={transform.position + detectionCenterOffset}, size={detectionBoxSize}, playerLayerMask={playerLayerMask.value}");
        LogComponent("StoneObject", stoneObject);
        LogComponent("FallingBoxObject", fallingBoxObject);

        if (playerLayerMask.value == 0 && !usePlayerTagFallback)
        {
            Debug.LogWarning("[GravityDropSensor] PlayerLayerMask is empty and tag fallback is disabled.", this);
        }
    }

    private void CacheReferences()
    {
        if (stoneObject == null)
        {
            stoneObject = GetComponent<StoneObject>();
        }

        if (fallingBoxObject == null)
        {
            fallingBoxObject = GetComponent<FallingBoxObject>();
        }
    }

    private bool IsPlayerTagged(Transform target)
    {
        Transform current = target;
        while (current != null)
        {
            if (current.tag == playerTag)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmo)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.35f);
        Gizmos.DrawWireCube(transform.position + detectionCenterOffset, detectionBoxSize);
    }

    private void LogComponent(string label, Object component)
    {
        if (!debugMode)
        {
            return;
        }

        if (component != null)
        {
            Debug.Log($"[GravityDropSensor] {label} found: {component.GetType().Name}", this);
            return;
        }

        Debug.LogWarning($"[GravityDropSensor] {label} not assigned.", this);
    }

    private void Log(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[GravityDropSensor] {message}", this);
        }
    }
}
