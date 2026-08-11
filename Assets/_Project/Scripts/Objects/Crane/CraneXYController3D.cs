using UnityEngine;
using UnityEngine.Events;

public enum CraneXYOperationState { Idle, ActivationDelay, MovingHorizontal, MovingVertical, Arrived, Blocked }
public enum CraneHorizontalSide { Left, Right }
public enum CraneVerticalSide { Top, Bottom }
public enum CraneXYActiveAxis { None, Horizontal, Vertical }

[DisallowMultipleComponent]
public sealed class CraneXYController3D : MonoBehaviour
{
    [Header("Structure")]
    [SerializeField] private Transform fixedRoot;
    [SerializeField] private Transform horizontalMovingRoot;
    [SerializeField] private Transform verticalMovingRoot;
    [SerializeField] private Transform horizontalLeftPoint;
    [SerializeField] private Transform horizontalRightPoint;
    [SerializeField] private Transform ropeTopAnchor;
    [SerializeField] private Transform ropeBottomAnchor;
    [SerializeField] private Transform ropeVisualRoot;
    [SerializeField] private CraneXYLeverSwitch3D horizontalLever;
    [SerializeField] private CraneXYLeverSwitch3D verticalLever;

    [Header("Horizontal Movement")]
    [Min(0f)] [SerializeField] private float horizontalMoveSpeed = 2f;
    [Min(0f)] [SerializeField] private float horizontalAcceleration = 4f;
    [Min(0f)] [SerializeField] private float horizontalDeceleration = 4f;
    [Min(0.001f)] [SerializeField] private float horizontalArrivalTolerance = 0.02f;
    [SerializeField] private CraneHorizontalSide startHorizontalSide = CraneHorizontalSide.Left;
    [Min(0f)] [SerializeField] private float horizontalActivationDelay = 3f;

    [Header("Vertical Movement / Rope")]
    [Min(1)] [SerializeField] private int ropeSegmentCount = 8;
    [Min(0.001f)] [SerializeField] private float ropeSegmentLength = 0.5f;
    [Min(0f)] [SerializeField] private float verticalMoveSpeed = 2f;
    [Min(0f)] [SerializeField] private float verticalAcceleration = 4f;
    [Min(0f)] [SerializeField] private float verticalDeceleration = 4f;
    [Min(0.001f)] [SerializeField] private float verticalArrivalTolerance = 0.02f;
    [SerializeField] private CraneVerticalSide startVerticalSide = CraneVerticalSide.Top;
    [Min(0f)] [SerializeField] private float verticalActivationDelay = 3f;
    [Min(0f)] [SerializeField] private float ropeBottomOverlap = 0.03f;
    [SerializeField] private SpriteRenderer[] ropeSegmentRenderers;

    [Header("Command Policy")]
    [SerializeField] private bool allowSimultaneousAxisMovement;
    [SerializeField] private bool queueBlockedCommand;

    [Header("Obstacle Detection")]
    [SerializeField] private LayerMask horizontalObstacleMask;
    [SerializeField] private LayerMask verticalObstacleMask;
    [SerializeField] private bool includeCarriedObjectBounds = true;
    [SerializeField] private bool stopOnObstruction = true;
    [Min(0f)] [SerializeField] private float obstructionCheckPadding = 0.05f;
    [SerializeField] private BoxCollider carryAreaCollider;
    [SerializeField] private CraneCarryZone3D magnetHeadCarryZone;
    [SerializeField] private UnityEvent onObstructed = new UnityEvent();

    [Header("Pause / Debug")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private bool debugMode;

    [Header("Runtime State")]
    [SerializeField] private CraneXYOperationState state = CraneXYOperationState.Idle;
    [SerializeField] private CraneHorizontalSide horizontalSide;
    [SerializeField] private CraneVerticalSide verticalSide;
    [SerializeField] private CraneXYActiveAxis activeAxis;
    [SerializeField] private CraneXYActiveAxis pendingCommand;
    [SerializeField] private float activationRemainingTime;
    [SerializeField] private string lastStopReason = "Idle";
    [SerializeField] private string lastBlockingColliderName;
    [SerializeField] private string lastBlockingParentName;
    [SerializeField] private int lastBlockingLayer = -1;

    private readonly RaycastHit[] obstacleHits = new RaycastHit[16];
    private float currentAxisSpeed;
    private float topLocalY;
    private bool initialized;
    private bool obstructionWarned;
    private CraneXYLeverSwitch3D activeLever;
    private Collider[] magnetTransportColliders;
    private int playerLayer = -1;

    public CraneXYOperationState State => state;
    public bool IsMoving => state == CraneXYOperationState.MovingHorizontal || state == CraneXYOperationState.MovingVertical;
    public bool IsBusy => state == CraneXYOperationState.ActivationDelay || IsMoving;
    public float MaxDropDistance => ropeSegmentCount * ropeSegmentLength;
    public float RopeEndError { get; private set; }
    public string LastStopReason => lastStopReason;
    public string LastBlockingColliderName => lastBlockingColliderName;
    public string LastBlockingParentName => lastBlockingParentName;
    public int LastBlockingLayer => lastBlockingLayer;
    public Vector3 CurrentRailDestination
    {
        get
        {
            if (!initialized) return transform.position;
            if (activeAxis == CraneXYActiveAxis.Horizontal && horizontalMovingRoot != null && horizontalLeftPoint != null && horizontalRightPoint != null)
            {
                Vector3 destination = horizontalMovingRoot.position;
                destination.x = horizontalSide == CraneHorizontalSide.Left ? horizontalRightPoint.position.x : horizontalLeftPoint.position.x;
                return destination;
            }
            if (activeAxis == CraneXYActiveAxis.Vertical && verticalMovingRoot != null && verticalMovingRoot.parent != null)
            {
                Vector3 localDestination = verticalMovingRoot.localPosition;
                localDestination.y = verticalSide == CraneVerticalSide.Top ? topLocalY - MaxDropDistance : topLocalY;
                return verticalMovingRoot.parent.TransformPoint(localDestination);
            }
            return horizontalMovingRoot != null ? horizontalMovingRoot.position : transform.position;
        }
    }
    public float RemainingRailDistance => activeAxis == CraneXYActiveAxis.Horizontal && horizontalMovingRoot != null
        ? Mathf.Abs(CurrentRailDestination.x - horizontalMovingRoot.position.x)
        : activeAxis == CraneXYActiveAxis.Vertical && verticalMovingRoot != null
            ? Mathf.Abs(CurrentRailDestination.y - verticalMovingRoot.position.y)
            : 0f;

    private void OnValidate()
    {
        horizontalMoveSpeed = Mathf.Max(0f, horizontalMoveSpeed);
        horizontalAcceleration = Mathf.Max(0f, horizontalAcceleration);
        horizontalDeceleration = Mathf.Max(0f, horizontalDeceleration);
        horizontalArrivalTolerance = Mathf.Max(0.001f, horizontalArrivalTolerance);
        horizontalActivationDelay = Mathf.Max(0f, horizontalActivationDelay);
        ropeSegmentCount = Mathf.Max(1, ropeSegmentCount);
        ropeSegmentLength = Mathf.Max(0.001f, ropeSegmentLength);
        verticalMoveSpeed = Mathf.Max(0f, verticalMoveSpeed);
        verticalAcceleration = Mathf.Max(0f, verticalAcceleration);
        verticalDeceleration = Mathf.Max(0f, verticalDeceleration);
        verticalArrivalTolerance = Mathf.Max(0.001f, verticalArrivalTolerance);
        verticalActivationDelay = Mathf.Max(0f, verticalActivationDelay);
        ropeBottomOverlap = Mathf.Max(0f, ropeBottomOverlap);
        obstructionCheckPadding = Mathf.Max(0f, obstructionCheckPadding);
    }

    private void Awake()
    {
        playerLayer = LayerMask.NameToLayer("Player");
        InitializeOnce();
    }

    private void OnDisable()
    {
        magnetTransportColliders = null;
        FinishLeverVisual(true);
        state = CraneXYOperationState.Idle;
        activeAxis = CraneXYActiveAxis.None;
        pendingCommand = CraneXYActiveAxis.None;
        activationRemainingTime = 0f;
        currentAxisSpeed = 0f;
    }

    private void Update()
    {
        if (state != CraneXYOperationState.ActivationDelay) return;
        activationRemainingTime = Mathf.Max(0f, activationRemainingTime - Time.deltaTime);
        if (activationRemainingTime > 0f) return;
        state = activeAxis == CraneXYActiveAxis.Horizontal
            ? CraneXYOperationState.MovingHorizontal
            : CraneXYOperationState.MovingVertical;
    }

    private void FixedUpdate()
    {
        if (!IsMoving) return;
        if (state == CraneXYOperationState.MovingHorizontal) MoveHorizontal();
        else MoveVertical();
    }

    private void LateUpdate() => RefreshRopeVisual();

    public void InitializeOnce()
    {
        if (initialized || horizontalMovingRoot == null || verticalMovingRoot == null) return;
        topLocalY = verticalMovingRoot.localPosition.y;
        horizontalSide = startHorizontalSide;
        verticalSide = startVerticalSide;
        Vector3 horizontalPosition = horizontalMovingRoot.position;
        if (startHorizontalSide == CraneHorizontalSide.Left && horizontalLeftPoint != null) horizontalPosition.x = horizontalLeftPoint.position.x;
        else if (horizontalRightPoint != null) horizontalPosition.x = horizontalRightPoint.position.x;
        horizontalMovingRoot.position = horizontalPosition;
        Vector3 verticalPosition = verticalMovingRoot.localPosition;
        verticalPosition.y = topLocalY - (startVerticalSide == CraneVerticalSide.Bottom ? MaxDropDistance : 0f);
        verticalMovingRoot.localPosition = verticalPosition;
        ropeSegmentRenderers = ropeVisualRoot != null ? ropeVisualRoot.GetComponentsInChildren<SpriteRenderer>(true) : ropeSegmentRenderers;
        state = CraneXYOperationState.Idle;
        initialized = true;
        RefreshRopeVisual();
    }

    public bool RequestAxisMove(CraneXYAxis axis, CraneXYLeverSwitch3D sourceLever, Transform actor)
    {
        InitializeOnce();
        if (!initialized || !LeverWins(axis, sourceLever, actor)) return false;
        CraneXYActiveAxis requested = axis == CraneXYAxis.Horizontal ? CraneXYActiveAxis.Horizontal : CraneXYActiveAxis.Vertical;
        if (IsBusy && !allowSimultaneousAxisMovement)
        {
            if (queueBlockedCommand) pendingCommand = requested;
            return false;
        }
        if (IsBusy) return false;
        activeAxis = requested;
        activeLever = sourceLever;
        pendingCommand = CraneXYActiveAxis.None;
        currentAxisSpeed = 0f;
        activationRemainingTime = axis == CraneXYAxis.Horizontal ? horizontalActivationDelay : verticalActivationDelay;
        lastStopReason = "Moving to lever-selected rail endpoint";
        state = CraneXYOperationState.ActivationDelay;
        return true;
    }

    public bool WillNextMoveInNegativeDirection(CraneXYAxis axis)
    {
        InitializeOnce();
        return axis == CraneXYAxis.Horizontal
            ? horizontalSide == CraneHorizontalSide.Right
            : verticalSide == CraneVerticalSide.Bottom;
    }

    // This only prevents the active magnet target from being treated as a rail
    // obstruction. It never changes the lever command or rail destination.
    public void SetMagnetTransportTargetColliders(Collider[] colliders) => magnetTransportColliders = colliders;

    private bool LeverWins(CraneXYAxis axis, CraneXYLeverSwitch3D source, Transform actor)
    {
        if (source == null || actor == null) return true;
        CraneXYLeverSwitch3D other = axis == CraneXYAxis.Horizontal ? verticalLever : horizontalLever;
        if (other == null || !other.PlayerInRange) return true;
        float sourceDistance = (source.transform.position - actor.position).sqrMagnitude;
        float otherDistance = (other.transform.position - actor.position).sqrMagnitude;
        if (Mathf.Abs(sourceDistance - otherDistance) > 0.0001f) return sourceDistance < otherDistance;
        if (source.LastEnterSequence != other.LastEnterSequence) return source.LastEnterSequence > other.LastEnterSequence;
        Debug.LogWarning("[CraneXYController3D] Lever priority is ambiguous; command ignored.", this);
        return false;
    }

    private void MoveHorizontal()
    {
        if (horizontalMovingRoot == null || horizontalLeftPoint == null || horizontalRightPoint == null) { CancelCommand(); return; }
        float destination = horizontalSide == CraneHorizontalSide.Left ? horizontalRightPoint.position.x : horizontalLeftPoint.position.x;
        float remaining = Mathf.Abs(destination - horizontalMovingRoot.position.x);
        if (remaining <= horizontalArrivalTolerance) { ArriveHorizontal(destination); return; }
        currentAxisSpeed = CalculateSpeed(currentAxisSpeed, horizontalMoveSpeed, horizontalAcceleration, horizontalDeceleration, remaining);
        float nextX = Mathf.MoveTowards(horizontalMovingRoot.position.x, destination, currentAxisSpeed * Time.fixedDeltaTime);
        Vector3 delta = new Vector3(nextX - horizontalMovingRoot.position.x, 0f, 0f);
        if (HasObstruction(delta, horizontalObstacleMask)) { Block(); return; }
        horizontalMovingRoot.position += delta;
        magnetHeadCarryZone?.ApplyCarryDelta(delta);
    }

    private void MoveVertical()
    {
        if (verticalMovingRoot == null) { CancelCommand(); return; }
        float destination = verticalSide == CraneVerticalSide.Top ? topLocalY - MaxDropDistance : topLocalY;
        float remaining = Mathf.Abs(destination - verticalMovingRoot.localPosition.y);
        if (remaining <= verticalArrivalTolerance) { ArriveVertical(destination); return; }
        currentAxisSpeed = CalculateSpeed(currentAxisSpeed, verticalMoveSpeed, verticalAcceleration, verticalDeceleration, remaining);
        float nextY = Mathf.MoveTowards(verticalMovingRoot.localPosition.y, destination, currentAxisSpeed * Time.fixedDeltaTime);
        Vector3 worldDelta = verticalMovingRoot.parent.TransformVector(new Vector3(0f, nextY - verticalMovingRoot.localPosition.y, 0f));
        if (HasObstruction(worldDelta, verticalObstacleMask)) { Block(); return; }
        Vector3 local = verticalMovingRoot.localPosition;
        local.y = nextY;
        verticalMovingRoot.localPosition = local;
        magnetHeadCarryZone?.ApplyCarryDelta(worldDelta);
    }

    private static float CalculateSpeed(float current, float maximum, float acceleration, float deceleration, float remaining)
    {
        if (maximum <= 0f) return 0f;
        float brakingDistance = deceleration > 0f ? current * current / (2f * deceleration) : 0f;
        float target = remaining <= brakingDistance ? 0f : maximum;
        float rate = target < current ? deceleration : acceleration;
        if (rate <= 0f) return target > current ? maximum : current;
        return Mathf.MoveTowards(current, target, rate * Time.fixedDeltaTime);
    }

    private bool HasObstruction(Vector3 delta, LayerMask mask)
    {
        if (!stopOnObstruction || mask.value == 0 || delta.sqrMagnitude <= Mathf.Epsilon || carryAreaCollider == null) return false;
        Bounds bounds = carryAreaCollider.bounds;
        Vector3 half = includeCarriedObjectBounds ? bounds.extents : bounds.extents * 0.75f;
        int count = Physics.BoxCastNonAlloc(bounds.center, half, delta.normalized, obstacleHits,
            carryAreaCollider.transform.rotation, delta.magnitude + obstructionCheckPadding, mask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider hit = obstacleHits[i].collider;
            if (hit == null || hit.transform.IsChildOf(transform) || IsMagnetTransportCollider(hit) ||
                IsMagneticCarryableTransportObject(hit)) continue;
            lastBlockingColliderName = hit.name;
            lastBlockingParentName = hit.transform.parent != null ? hit.transform.parent.name : "(none)";
            lastBlockingLayer = hit.gameObject.layer;
            lastStopReason = $"Blocked by {lastBlockingColliderName} (Layer {lastBlockingLayer})";
            return true;
        }
        return false;
    }

    private bool IsMagnetTransportCollider(Collider collider)
    {
        if (magnetTransportColliders == null) return false;
        for (int i = 0; i < magnetTransportColliders.Length; i++)
        {
            if (magnetTransportColliders[i] == collider) return true;
        }
        return false;
    }

    // This works before the magnet controller has selected or reserved a target.
    // Only an explicitly movable magnetic object is exempt; other Ground objects
    // continue through the existing obstruction path.
    private bool IsMagneticCarryableTransportObject(Collider collider)
    {
        MagneticCarryable3D carryable = collider.GetComponentInParent<MagneticCarryable3D>();
        if (carryable == null || !carryable.CanBeMovedByMagnet) return false;

        for (Transform current = carryable.transform; current != null; current = current.parent)
        {
            if (current.CompareTag("Player") || (playerLayer >= 0 && current.gameObject.layer == playerLayer) ||
                current.GetComponent<PlatformerPlayer3D>() != null) return false;
        }
        return true;
    }

    private void ArriveHorizontal(float x)
    {
        Vector3 position = horizontalMovingRoot.position; position.x = x; horizontalMovingRoot.position = position;
        horizontalSide = horizontalSide == CraneHorizontalSide.Left ? CraneHorizontalSide.Right : CraneHorizontalSide.Left;
        Arrive();
    }

    private void ArriveVertical(float localY)
    {
        Vector3 position = verticalMovingRoot.localPosition; position.y = localY; verticalMovingRoot.localPosition = position;
        verticalSide = verticalSide == CraneVerticalSide.Top ? CraneVerticalSide.Bottom : CraneVerticalSide.Top;
        Arrive();
    }

    private void Arrive()
    {
        currentAxisSpeed = 0f;
        state = CraneXYOperationState.Arrived;
        activeAxis = CraneXYActiveAxis.None;
        obstructionWarned = false;
        lastStopReason = "Reached rail endpoint";
        FinishLeverVisual(false);
        state = CraneXYOperationState.Idle;
    }

    private void Block()
    {
        currentAxisSpeed = 0f;
        state = CraneXYOperationState.Blocked;
        activeAxis = CraneXYActiveAxis.None;
        pendingCommand = CraneXYActiveAxis.None;
        FinishLeverVisual(true);
        onObstructed?.Invoke();
        if (!obstructionWarned)
        {
            obstructionWarned = true;
            Debug.LogWarning("[CraneXYController3D] Movement stopped by an obstruction.", this);
        }
        state = CraneXYOperationState.Idle;
    }

    private void CancelCommand()
    {
        FinishLeverVisual(true);
        state = CraneXYOperationState.Idle;
        activeAxis = CraneXYActiveAxis.None;
        currentAxisSpeed = 0f;
        lastStopReason = "Command cancelled";
    }

    private void FinishLeverVisual(bool cancelled)
    {
        if (activeLever != null) activeLever.NotifyCommandFinished(cancelled);
        activeLever = null;
    }

    private void RefreshRopeVisual()
    {
        if (ropeTopAnchor == null || ropeBottomAnchor == null || ropeSegmentRenderers == null) return;
        Vector3 start = ropeTopAnchor.position;
        Vector3 delta = ropeBottomAnchor.position - start;
        float distance = delta.magnitude;
        Vector3 direction = distance > 0.000001f ? delta / distance : Vector3.down;
        float visualLength = distance + ropeBottomOverlap;
        Vector3 visualEnd = start;
        for (int i = 0; i < ropeSegmentRenderers.Length; i++)
        {
            SpriteRenderer renderer = ropeSegmentRenderers[i];
            if (renderer == null) continue;
            float shown = Mathf.Clamp(visualLength - i * ropeSegmentLength, 0f, ropeSegmentLength);
            renderer.enabled = i < ropeSegmentCount && shown > 0.0001f;
            if (!renderer.enabled) continue;
            Transform segment = renderer.transform;
            Vector3 scale = segment.localScale; scale.y = shown / ropeSegmentLength; segment.localScale = scale;
            segment.rotation = Quaternion.FromToRotation(Vector3.down, direction);
            segment.position = start + direction * (i * ropeSegmentLength + shown * 0.5f);
            visualEnd = start + direction * (i * ropeSegmentLength + shown);
        }
        RopeEndError = Mathf.Max(0f, distance - Vector3.Dot(visualEnd - start, direction));
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;
        if (horizontalLeftPoint != null && horizontalRightPoint != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(horizontalLeftPoint.position, horizontalRightPoint.position);
            Gizmos.DrawWireCube(horizontalLeftPoint.position, Vector3.one * 0.2f);
            Gizmos.DrawWireCube(horizontalRightPoint.position, Vector3.one * 0.2f);
        }
        if (ropeTopAnchor != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(ropeTopAnchor.position, ropeTopAnchor.position + Vector3.down * MaxDropDistance);
            Gizmos.DrawWireSphere(ropeTopAnchor.position, 0.12f);
            Gizmos.DrawWireSphere(ropeTopAnchor.position + Vector3.down * MaxDropDistance, 0.12f);
        }
        if (carryAreaCollider != null)
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.35f);
            Gizmos.DrawCube(carryAreaCollider.bounds.center, carryAreaCollider.bounds.size);
        }
    }
}
