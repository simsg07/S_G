using UnityEngine;
using UnityEngine.Serialization;

public enum CraneStartPoint
{
    PointA,
    PointB
}

[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class CraneObject : MonoBehaviour, IShutterFreezable3D
{
    [Header("Rail")]
    [SerializeField] private CraneRailPath3D railPath;
    [FormerlySerializedAs("startPoint")]
    [SerializeField] private CraneStartPoint startPoint = CraneStartPoint.PointA;
    [Tooltip("When enabled, the Crane starts from Point_A. When disabled, it starts from Point_B.")]
    [SerializeField] private bool startAtPointA = true;
    [Tooltip("Snap to the selected start point when Play begins.")]
    [FormerlySerializedAs("snapToStartPoint")]
    [FormerlySerializedAs("snapToStartPointOnStart")]
    [SerializeField] private bool snapToStartOnPlay = true;

    [Header("Cabin Offset From Rail")]
    [Tooltip("Vertical world-space distance from the upper rail to the hanging cabin. Usually negative.")]
    [SerializeField] private float cabinYOffset = -3f;
    [SerializeField] private bool lockZ = true;
    [SerializeField] private float fixedZ;

    [Header("Movement")]
    [Min(0f)] [SerializeField] private float moveSpeed = 2f;
    [Min(0.001f)] [SerializeField] private float arrivalDistance = 0.03f;
    [Tooltip("Ignore another move request while travelling.")]
    [SerializeField] private bool blockCommandsWhileMoving = true;

    [Header("Obstacle Detection")]
    [Tooltip("Stop movement when the obstacle box cast or collision finds a matching Layer.")]
    [SerializeField] private bool stopOnObstacle = true;
    [SerializeField] private LayerMask obstacleLayerMask;
    [SerializeField] private Vector3 obstacleCheckCenterOffset;
    [SerializeField] private Vector3 obstacleCheckSize = new Vector3(2f, 1f, 1f);
    [Min(0f)] [SerializeField] private float obstacleCheckPadding = 0.05f;

    [Header("Pause / Shutter")]
    [SerializeField] private bool canPauseByShutter = true;
    [SerializeField] private bool resumeTargetAfterPause = true;

    [Header("References")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Collider mainCollider;
    [SerializeField] private CraneCarryZone3D carryZone;

    [Header("Runtime State")]
    [SerializeField] private bool isMoving;
    [SerializeField] private bool isStopped = true;
    [SerializeField] private bool isPaused;
    [SerializeField] private bool isBlocked;
    [SerializeField] private bool targetIsPointB;
    [SerializeField] private Vector3 currentTarget;

    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private bool debugMode = true;

    private readonly RaycastHit[] obstacleHits = new RaycastHit[16];
    private float shutterReleaseTime;

    public bool IsMoving => isMoving;
    public bool IsStopped => isStopped || isPaused || isBlocked;
    public Vector3 CurrentTarget => currentTarget;
    public bool IsPaused => isPaused;
    public bool IsBlocked => isBlocked;

    private void Reset()
    {
        CacheReferences();
        ConfigureBody();
    }

    private void Awake()
    {
        CacheReferences();
        ConfigureBody();
        SyncStartPointFromBool();

        if (snapToStartOnPlay && railPath != null && railPath.IsValid)
        {
            targetIsPointB = startPoint == CraneStartPoint.PointB;
            SetPosition(GetCabinPoint(targetIsPointB));
        }

        isStopped = !isMoving;
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        arrivalDistance = Mathf.Max(0.001f, arrivalDistance);
        obstacleCheckSize.x = Mathf.Max(0.01f, obstacleCheckSize.x);
        obstacleCheckSize.y = Mathf.Max(0.01f, obstacleCheckSize.y);
        obstacleCheckSize.z = Mathf.Max(0.01f, obstacleCheckSize.z);
        obstacleCheckPadding = Mathf.Max(0f, obstacleCheckPadding);
        SyncStartPointFromBool();
        CacheReferences();
    }

    private void Update()
    {
        if (isPaused && shutterReleaseTime > 0f && Time.time >= shutterReleaseTime)
        {
            ResumeByShutter();
        }
    }

    private void FixedUpdate()
    {
        if (!isMoving || isPaused || railPath == null || !railPath.IsValid)
        {
            return;
        }

        Vector3 current = rb != null ? rb.position : transform.position;
        Vector3 destination = GetCabinPoint(targetIsPointB);
        currentTarget = destination;
        if (lockZ) destination.z = fixedZ;
        Vector3 remaining = destination - current;
        if (lockZ) remaining.z = 0f;

        if (remaining.magnitude <= arrivalDistance)
        {
            Arrive(destination);
            return;
        }

        Vector3 next = Vector3.MoveTowards(current, destination, moveSpeed * Time.fixedDeltaTime);
        next = ClampToCabinSegment(next);
        if (lockZ) next.z = fixedZ;
        Vector3 delta = next - current;

        if (HasObstacle(current, delta))
        {
            isBlocked = true;
            isMoving = false;
            isStopped = true;
            Log("Movement stopped by obstacle.");
            return;
        }

        isBlocked = false;
        carryZone?.CarryBy(delta);
        SetPosition(next);
    }

    public bool RequestMoveToOppositePoint(bool allowWhileMoving = false)
    {
        Log("ToggleMoveTarget called.");
        if (railPath == null || !railPath.IsValid || (isMoving && blockCommandsWhileMoving && !allowWhileMoving))
        {
            if (railPath == null || !railPath.IsValid)
            {
                Debug.LogWarning("[CraneObject] railPath is missing.", this);
            }

            return false;
        }

        if (isMoving && allowWhileMoving)
        {
            targetIsPointB = !targetIsPointB;
        }
        else
        {
            Vector3 position = rb != null ? rb.position : transform.position;
            float distanceToA = Mathf.Abs(position.x - railPath.GetRailPointA().x);
            float distanceToB = Mathf.Abs(position.x - railPath.GetRailPointB().x);
            targetIsPointB = distanceToA <= distanceToB;
        }

        isBlocked = false;
        isMoving = true;
        isStopped = false;
        currentTarget = GetCabinPoint(targetIsPointB);
        Log(targetIsPointB ? "Moving to Cabin Target B." : "Moving to Cabin Target A.");
        return true;
    }

    [ContextMenu("Test Toggle Move Target")]
    public void ToggleMoveTarget()
    {
        TryToggleMoveTarget(false);
    }

    public bool TryToggleMoveTarget(bool allowWhileMoving = false)
    {
        return RequestMoveToOppositePoint(allowWhileMoving);
    }

    [ContextMenu("Test Move To A")]
    public void MoveToA()
    {
        RequestMove(false);
    }

    public bool MoveToPointA()
    {
        return RequestMove(false);
    }

    [ContextMenu("Test Move To B")]
    public void MoveToB()
    {
        RequestMove(true);
    }

    public bool MoveToPointB()
    {
        return RequestMove(true);
    }

    public bool RequestMove(bool moveToPointB)
    {
        if (railPath == null || !railPath.IsValid || (isMoving && blockCommandsWhileMoving))
        {
            if (railPath == null || !railPath.IsValid)
            {
                Debug.LogWarning("[CraneObject] railPath is missing.", this);
            }

            return false;
        }

        targetIsPointB = moveToPointB;
        isBlocked = false;
        isMoving = true;
        isStopped = false;
        currentTarget = GetCabinPoint(targetIsPointB);
        Log(targetIsPointB ? "Moving to Cabin Target B." : "Moving to Cabin Target A.");
        return true;
    }

    [ContextMenu("Test Stop")]
    public void StopMovement()
    {
        isMoving = false;
        isStopped = true;
    }

    public void StopMove()
    {
        StopMovement();
    }

    [ContextMenu("Reset To Start")]
    public void ResetToStartPosition()
    {
        CacheReferences();
        SyncStartPointFromBool();
        if (railPath == null || !railPath.IsValid)
        {
            Debug.LogWarning("[CraneObject] railPath is missing.", this);
            return;
        }

        targetIsPointB = startPoint == CraneStartPoint.PointB;
        isMoving = false;
        isStopped = true;
        isBlocked = false;
        SetPosition(GetCabinPoint(targetIsPointB));
        currentTarget = GetCabinPoint(targetIsPointB);
    }

    [ContextMenu("Reset To Point A")]
    public void ResetToPointA()
    {
        startAtPointA = true;
        ResetToStartPosition();
    }

    [ContextMenu("Reset To Point B")]
    public void ResetToPointB()
    {
        startAtPointA = false;
        ResetToStartPosition();
    }

    public void PauseByShutter()
    {
        if (!canPauseByShutter)
        {
            return;
        }

        isPaused = true;
        shutterReleaseTime = 0f;
        Log("Paused by shutter/external command.");
    }

    public void ResumeByShutter()
    {
        if (!canPauseByShutter)
        {
            return;
        }

        isPaused = false;
        shutterReleaseTime = 0f;
        if (!resumeTargetAfterPause)
        {
            isMoving = false;
        }

        Log("Shutter/external pause released.");
    }

    public void SetShutterPaused(bool paused)
    {
        if (paused)
        {
            PauseByShutter();
        }
        else
        {
            ResumeByShutter();
        }
    }

    public bool ApplyShutterFreeze(float duration, CameraAbilitySystem3D source)
    {
        if (!canPauseByShutter || duration <= 0f)
        {
            return false;
        }

        isPaused = true;
        shutterReleaseTime = Mathf.Max(shutterReleaseTime, Time.time + duration);
        return true;
    }

    [ContextMenu("Validate Crane Setup")]
    public void ValidateCraneSetup()
    {
        CacheReferences();
        if (railPath == null || !railPath.IsValid)
        {
            Debug.LogWarning("[CraneObject] railPath is missing.", this);
            return;
        }

        Debug.Log(
            $"[CraneObject] Rail A: {railPath.GetRailPointA()}\n" +
            $"[CraneObject] Rail B: {railPath.GetRailPointB()}\n" +
            $"[CraneObject] Cabin Target A: {GetCabinPoint(false)}\n" +
            $"[CraneObject] Cabin Target B: {GetCabinPoint(true)}\n" +
            $"[CraneObject] Rigidbody={(rb != null)}, Collider={(mainCollider != null)}, CarryZone={(carryZone != null)}, CabinYOffset={cabinYOffset}", this);
    }

    private bool HasObstacle(Vector3 current, Vector3 delta)
    {
        if (!stopOnObstacle || obstacleLayerMask.value == 0 || delta.sqrMagnitude <= Mathf.Epsilon)
        {
            return false;
        }

        Vector3 direction = delta.normalized;
        Vector3 center = current + transform.TransformVector(obstacleCheckCenterOffset);
        int count = Physics.BoxCastNonAlloc(
            center,
            obstacleCheckSize * 0.5f,
            direction,
            obstacleHits,
            transform.rotation,
            delta.magnitude + obstacleCheckPadding,
            obstacleLayerMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider hit = obstacleHits[i].collider;
            if (hit != null && !hit.transform.IsChildOf(transform))
            {
                return true;
            }
        }

        return false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!stopOnObstacle || !isMoving || collision == null || collision.collider == null || obstacleLayerMask.value == 0)
        {
            return;
        }

        if ((obstacleLayerMask.value & (1 << collision.collider.gameObject.layer)) == 0)
        {
            return;
        }

        isBlocked = true;
        isMoving = false;
        isStopped = true;
        Log($"Movement stopped after collision with {collision.collider.name}.");
    }

    private void Arrive(Vector3 destination)
    {
        SetPosition(destination);
        isMoving = false;
        isStopped = true;
        isBlocked = false;
        currentTarget = destination;
        Log("Arrived at target.");
    }

    private void SetPosition(Vector3 position)
    {
        position = ClampToCabinSegment(position);
        if (lockZ) position.z = fixedZ;
        if (rb != null && Application.isPlaying)
        {
            rb.MovePosition(position);
        }
        else
        {
            transform.position = position;
        }
    }

    private Vector3 GetCabinPoint(bool pointB)
    {
        if (railPath == null) return transform.position;
        Vector3 point = pointB ? railPath.GetRailPointB() : railPath.GetRailPointA();
        point.y += cabinYOffset;
        if (lockZ) point.z = fixedZ;
        return point;
    }

    private Vector3 ClampToCabinSegment(Vector3 position)
    {
        if (railPath == null || !railPath.IsValid) return position;
        Vector3 start = GetCabinPoint(false);
        Vector3 segment = GetCabinPoint(true) - start;
        float lengthSquared = segment.sqrMagnitude;
        if (lengthSquared <= Mathf.Epsilon) return start;
        float t = Mathf.Clamp01(Vector3.Dot(position - start, segment) / lengthSquared);
        return start + segment * t;
    }

    private void CacheReferences()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (mainCollider == null) mainCollider = GetComponent<Collider>();
        if (carryZone == null) carryZone = GetComponentInChildren<CraneCarryZone3D>(true);
        if (railPath == null) railPath = GetComponentInParent<CraneRailPath3D>();
    }

    private void SyncStartPointFromBool()
    {
        startPoint = startAtPointA ? CraneStartPoint.PointA : CraneStartPoint.PointB;
    }

    private void ConfigureBody()
    {
        if (rb == null)
        {
            return;
        }

        rb.useGravity = false;
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos)
        {
            return;
        }

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.TransformPoint(obstacleCheckCenterOffset), obstacleCheckSize);
    }

    private void Log(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[CraneObject] {message}", this);
        }
    }
}
