using UnityEngine;

[DisallowMultipleComponent]
public class MonsterDetection : MonoBehaviour
{
    [Header("Detection Settings")]
    [Tooltip("Turn off to disable all target detection.")]
    public bool enableDetection = true;

    [Tooltip("Allows this monster to detect the Player.")]
    public bool canDetectPlayer = true;

    [Tooltip("Allows this monster to detect Light targets.")]
    public bool canDetectLight;

    [Tooltip("If both Player and Light are detected, Player is selected first.")]
    public bool prioritizePlayer = true;

    [Tooltip("Distance where Player is first detected.")]
    public float playerDetectRange = 1.5f;

    [Tooltip("Distance where Light is first detected.")]
    public float lightDetectRange = 4f;

    [Tooltip("Distance where the current target remains tracked after initial detection.")]
    public float chaseRange = 6f;

    [Tooltip("When true, colliders in obstacleLayerMask block detection.")]
    public bool requireLineOfSight = true;

    [Tooltip("Layers that block detection. Include Ground, Wall, TileObstacle, Platform and EnvironmentObstacle so solid map pieces block Player and Light sight.")]
    public LayerMask obstacleLayerMask;

    [Tooltip("If true, Light detection uses lightObstacleLayerMask instead of obstacleLayerMask.")]
    public bool useSeparateLightObstacleMask;

    [Tooltip("Layers that block Light detection only. Use this for Tilemap_LightSightBlock generated colliders.")]
    public LayerMask lightObstacleLayerMask;

    [Tooltip("Offset from the monster position where the sight ray starts.")]
    public Vector3 lineOfSightStartOffset;

    [Tooltip("Offset from the target position where the sight ray ends.")]
    public Vector3 targetCheckOffset;

    [Header("Debug")]
    public bool debugMode;
    public bool showGizmos = true;
    public bool debugLineOfSight = true;
    public float logInterval = 0.5f;

    private float nextLineOfSightLogTime;

    public bool IsTargetDetected(
        Transform observer,
        Transform target,
        float detectRange,
        bool requireEnabledLight,
        out Collider blockingCollider)
    {
        blockingCollider = null;
        if (!enableDetection || observer == null || target == null || !target.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (requireEnabledLight)
        {
            Light lightComponent = target.GetComponentInChildren<Light>(true);
            if (lightComponent != null && !lightComponent.enabled)
            {
                return false;
            }
        }

        Vector3 planarDelta = target.position - observer.position;
        planarDelta.z = 0f;
        if (planarDelta.sqrMagnitude > detectRange * detectRange)
        {
            return false;
        }

        return !requireLineOfSight || HasLineOfSight(observer, target, requireEnabledLight, out blockingCollider);
    }

    public bool HasLineOfSight(Transform observer, Transform target, out Collider blockingCollider)
    {
        return HasLineOfSight(observer, target, false, out blockingCollider);
    }

    public bool HasLineOfSightToLight(Transform observer, Transform target, out Collider blockingCollider)
    {
        return HasLineOfSight(observer, target, true, out blockingCollider);
    }

    private bool HasLineOfSight(Transform observer, Transform target, bool isLightTarget, out Collider blockingCollider)
    {
        blockingCollider = null;
        if (observer == null || target == null)
        {
            return false;
        }

        if (!requireLineOfSight)
        {
            return true;
        }

        LayerMask activeObstacleMask = GetObstacleMask(isLightTarget);
        if (activeObstacleMask.value == 0)
        {
            if (debugMode && Time.time >= nextLineOfSightLogTime)
            {
                nextLineOfSightLogTime = Time.time + logInterval;
                Debug.LogWarning("[MonsterDetection] LOS check failed because the active obstacle LayerMask is empty.", this);
            }

            return false;
        }

        Vector3 start = observer.position + lineOfSightStartOffset;
        Vector3 end = target.position + targetCheckOffset;
        start.z = observer.position.z;
        end.z = observer.position.z;
        Vector3 direction = end - start;
        float distance = direction.magnitude;
        if (distance <= 0.001f)
        {
            return true;
        }

        RaycastHit[] hits = Physics.RaycastAll(
            start,
            direction.normalized,
            distance,
            ~0,
            QueryTriggerInteraction.Ignore);

        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider candidate = hits[i].collider;
            if (candidate == null ||
                BelongsToHierarchy(candidate.transform, observer) ||
                BelongsToHierarchy(candidate.transform, target))
            {
                continue;
            }

            bool layerBlocks = (activeObstacleMask.value & (1 << candidate.gameObject.layer)) != 0;
            MapPiece mapPiece = candidate.GetComponentInParent<MapPiece>();
            bool mapPieceBlocks = mapPiece != null && mapPiece.BlockLineOfSight;
            if (!layerBlocks && !mapPieceBlocks)
            {
                continue;
            }

            if (hits[i].distance < nearestDistance)
            {
                nearestDistance = hits[i].distance;
                blockingCollider = candidate;
            }
        }

        if (blockingCollider != null && debugMode && debugLineOfSight && Time.time >= nextLineOfSightLogTime)
        {
            nextLineOfSightLogTime = Time.time + logInterval;
            Debug.Log($"[MonsterDetection] LOS blocked by {blockingCollider.name}. Target={target.name}", this);
        }

        return blockingCollider == null;
    }

    private static bool BelongsToHierarchy(Transform candidate, Transform hierarchyRoot)
    {
        return candidate != null && hierarchyRoot != null &&
            (candidate == hierarchyRoot || candidate.IsChildOf(hierarchyRoot) || hierarchyRoot.IsChildOf(candidate));
    }

    private LayerMask GetObstacleMask(bool isLightTarget)
    {
        if (isLightTarget && useSeparateLightObstacleMask)
        {
            return lightObstacleLayerMask;
        }

        return obstacleLayerMask;
    }

    private void Reset()
    {
        obstacleLayerMask = LayerMask.GetMask("Ground", "Wall", "TileObstacle", "Platform", "EnvironmentObstacle");
    }

    private void OnValidate()
    {
        playerDetectRange = Mathf.Max(0f, playerDetectRange);
        lightDetectRange = Mathf.Max(0f, lightDetectRange);
        chaseRange = Mathf.Max(0f, chaseRange);
        logInterval = Mathf.Max(0.05f, logInterval);

        if (requireLineOfSight)
        {
            obstacleLayerMask |= LayerMask.GetMask(
                "Ground",
                "Wall",
                "TileObstacle",
                "Platform",
                "EnvironmentObstacle");
        }
    }
}
