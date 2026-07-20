using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public class CraneCarryZone3D : MonoBehaviour
{
    [Header("Carry Target")]
    [Tooltip("Layers that the Crane carries. Configure Player and movable Box layers in the prefab or scene.")]
    [SerializeField] private LayerMask carryLayerMask;
    [SerializeField] private bool carryRigidbodies = true;
    [SerializeField] private bool carryTransformsWithoutRigidbody = true;
    [Tooltip("For a dynamic Rigidbody tagged Player, add the platform velocity after Player movement is calculated.")]
    [SerializeField] private bool matchPlayerPlatformVelocity = true;
    [Tooltip("1 = exactly match the platform. Increase only if another movement system still cancels part of the carry velocity.")]
    [Min(0f)] [SerializeField] private float playerPlatformVelocityMultiplier = 1f;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Carry Area")]
    [FormerlySerializedAs("zoneCenterOffset")]
    [SerializeField] private Vector3 carryBoxCenterOffset = new Vector3(0f, 0.75f, 0f);
    [FormerlySerializedAs("zoneSize")]
    [SerializeField] private Vector3 carryBoxSize = new Vector3(2.5f, 1.5f, 1f);
    [SerializeField] private List<Transform> carriedTargets = new List<Transform>();

    [Header("Debug")]
    [SerializeField] private bool showGizmo = true;
    [SerializeField] private bool debugMode;

    private readonly Collider[] overlaps = new Collider[32];
    private readonly HashSet<Rigidbody> movedBodies = new HashSet<Rigidbody>();
    private readonly HashSet<Transform> movedTransforms = new HashSet<Transform>();
    private bool warnedEmptyLayerMask;

    private void OnValidate()
    {
        carryBoxSize.x = Mathf.Max(0.01f, carryBoxSize.x);
        carryBoxSize.y = Mathf.Max(0.01f, carryBoxSize.y);
        carryBoxSize.z = Mathf.Max(0.01f, carryBoxSize.z);
        playerPlatformVelocityMultiplier = Mathf.Max(0f, playerPlatformVelocityMultiplier);
    }

    [ContextMenu("Refresh Carried Targets")]
    public void RefreshCarriedTargets()
    {
        carriedTargets.Clear();
        movedBodies.Clear();
        movedTransforms.Clear();
        bool usePlayerTagFallback = carryLayerMask.value == 0;
        if (usePlayerTagFallback && !warnedEmptyLayerMask)
        {
            Debug.LogWarning("[CraneCarryZone3D] carryLayerMask is empty. Falling back to the Player tag; configure the mask to carry Boxes too.", this);
            warnedEmptyLayerMask = true;
        }
        else if (!usePlayerTagFallback)
        {
            warnedEmptyLayerMask = false;
        }

        CraneObject owner = GetComponentInParent<CraneObject>();
        Vector3 queryCenter = transform.TransformPoint(carryBoxCenterOffset);
        Vector3 querySize = carryBoxSize;
        if (usePlayerTagFallback && owner != null)
        {
            queryCenter = owner.transform.TransformPoint(new Vector3(0f, 0.5f, 0f));
            querySize = new Vector3(Mathf.Max(carryBoxSize.x, 3f), Mathf.Max(carryBoxSize.y, 3f), Mathf.Max(carryBoxSize.z, 1f));
        }

        int count = Physics.OverlapBoxNonAlloc(
            queryCenter,
            querySize * 0.5f,
            overlaps,
            transform.rotation,
            usePlayerTagFallback ? ~0 : carryLayerMask.value,
            triggerInteraction);

        for (int i = 0; i < count; i++)
        {
            Collider target = overlaps[i];
            if (target == null || (owner != null && target.transform.IsChildOf(owner.transform)))
            {
                continue;
            }

            if (IsCraneInfrastructure(target))
            {
                continue;
            }

            if (usePlayerTagFallback && !HasPlayerTag(target.transform))
            {
                continue;
            }

            Rigidbody body = target.attachedRigidbody;
            Transform carried = body != null ? body.transform : target.transform;
            if (!carriedTargets.Contains(carried))
            {
                carriedTargets.Add(carried);
            }
        }

        if (debugMode && carriedTargets.Count == 0)
        {
            Debug.Log($"[CraneCarryZone3D] No targets found. Check carryLayerMask, center {carryBoxCenterOffset}, and size {carryBoxSize}.", this);
        }
    }

    private static bool HasPlayerTag(Transform target)
    {
        Transform current = target;
        while (current != null)
        {
            if (current.CompareTag("Player")) return true;
            current = current.parent;
        }
        return false;
    }

    private static bool IsCraneInfrastructure(Collider target)
    {
        return target.GetComponentInParent<CraneObject>() != null ||
               target.GetComponentInParent<CraneRailPath3D>() != null ||
               target.GetComponentInParent<CraneLeverSwitch>() != null ||
               target.GetComponentInParent<CraneCarryZone3D>() != null;
    }

    public void ApplyCarryDelta(Vector3 worldDelta)
    {
        worldDelta.z = 0f;
        if (worldDelta.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        RefreshCarriedTargets();
        for (int i = 0; i < carriedTargets.Count; i++)
        {
            Transform target = carriedTargets[i];
            if (target == null) continue;
            Rigidbody body = target.GetComponent<Rigidbody>();
            if (body != null && carryRigidbodies)
            {
                if (movedBodies.Add(body))
                {
                    if (matchPlayerPlatformVelocity && !body.isKinematic && HasPlayerTag(body.transform))
                    {
                        float step = Mathf.Max(Time.fixedDeltaTime, 0.0001f);
                        Vector3 platformVelocity = worldDelta / step * playerPlatformVelocityMultiplier;
                        Vector3 velocity = body.linearVelocity;
                        velocity.x += platformVelocity.x;
                        velocity.z += platformVelocity.z;
                        body.linearVelocity = velocity;
                    }
                    else
                    {
                        Vector3 position = body.position + worldDelta;
                        position.z = body.position.z;
                        body.MovePosition(position);
                    }
                }

                continue;
            }

            if (body == null && carryTransformsWithoutRigidbody && movedTransforms.Add(target))
            {
                Vector3 position = target.position + worldDelta;
                position.z = target.position.z;
                target.position = position;
            }
        }

        if (debugMode && (movedBodies.Count > 0 || movedTransforms.Count > 0))
        {
            Debug.Log($"[CraneCarryZone3D] Carried {movedBodies.Count + movedTransforms.Count} target(s) by {worldDelta}.", this);
        }
    }

    public void CarryBy(Vector3 worldDelta) => ApplyCarryDelta(worldDelta);

    [ContextMenu("Validate Carry Zone Setup")]
    public void ValidateCarryZoneSetup()
    {
        Debug.Log($"[CraneCarryZone3D] Center={transform.TransformPoint(carryBoxCenterOffset)}, Size={carryBoxSize}, Mask={carryLayerMask.value}, CarryRigidbodies={carryRigidbodies}, CarryTransforms={carryTransformsWithoutRigidbody}, MatchPlayerVelocity={matchPlayerPlatformVelocity}, PlayerVelocityMultiplier={playerPlatformVelocityMultiplier}", this);
        if (carryLayerMask.value == 0)
        {
            Debug.LogWarning("[CraneCarryZone3D] carryLayerMask is empty. Player and Box cannot be carried until their layers are selected.", this);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmo)
        {
            return;
        }

        Gizmos.color = new Color(0.1f, 0.9f, 1f, 0.7f);
        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
        Gizmos.DrawWireCube(carryBoxCenterOffset, carryBoxSize);
        Gizmos.matrix = previous;
    }
}
