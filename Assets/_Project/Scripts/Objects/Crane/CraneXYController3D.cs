using UnityEngine;
using UnityEngine.Events;

public enum CraneXYOperationState { Idle, ActivationDelay, MovingHorizontal, MovingVertical, Arrived, Blocked }
public enum CraneHorizontalSide { Left, Right }
public enum CraneVerticalSide { Top, Bottom }
public enum CraneXYActiveAxis { None, Horizontal, Vertical }

[DisallowMultipleComponent]
public sealed class CraneXYController3D : MonoBehaviour, IShutterFreezable3D, IShutterFreezeState3D
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
    [SerializeField] private CraneCarryZone3D carryArea;
    [SerializeField] private Transform carryAnchor;

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
    [SerializeField] private UnityEvent onObstructed = new UnityEvent();

    [Header("Pause / Debug")]
    [SerializeField] private bool canPauseByShutter = true;
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private bool debugMode;

    [Header("Runtime State")]
    [SerializeField] private CraneXYOperationState state = CraneXYOperationState.Idle;
    [SerializeField] private CraneHorizontalSide horizontalSide;
    [SerializeField] private CraneVerticalSide verticalSide;
    [SerializeField] private CraneXYActiveAxis activeAxis;
    [SerializeField] private CraneXYActiveAxis pendingCommand;
    [SerializeField] private float activationRemainingTime;
    [SerializeField] private bool isCarryingObject;

    private readonly RaycastHit[] obstacleHits = new RaycastHit[16];
    private float currentAxisSpeed;
    private float topLocalY;
    private float shutterReleaseTime;
    private bool initialized;
    private bool obstructionWarned;

    public CraneXYOperationState State => state;
    public bool IsMoving => state == CraneXYOperationState.MovingHorizontal || state == CraneXYOperationState.MovingVertical;
    public bool IsBusy => state == CraneXYOperationState.ActivationDelay || IsMoving;
    public bool IsShutterFrozen => shutterReleaseTime > Time.time;
    public float MaxDropDistance => ropeSegmentCount * ropeSegmentLength;
    public float RopeEndError { get; private set; }

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

    private void Awake() => InitializeOnce();

    private void OnDisable()
    {
        state = CraneXYOperationState.Idle;
        activeAxis = CraneXYActiveAxis.None;
        pendingCommand = CraneXYActiveAxis.None;
        activationRemainingTime = 0f;
        currentAxisSpeed = 0f;
    }

    private void Update()
    {
        if (IsShutterFrozen) return;
        if (state != CraneXYOperationState.ActivationDelay) return;
        activationRemainingTime = Mathf.Max(0f, activationRemainingTime - Time.deltaTime);
        if (activationRemainingTime > 0f) return;
        state = activeAxis == CraneXYActiveAxis.Horizontal
            ? CraneXYOperationState.MovingHorizontal
            : CraneXYOperationState.MovingVertical;
    }

    private void FixedUpdate()
    {
        if (IsShutterFrozen || !IsMoving) return;
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
        pendingCommand = CraneXYActiveAxis.None;
        currentAxisSpeed = 0f;
        activationRemainingTime = axis == CraneXYAxis.Horizontal ? horizontalActivationDelay : verticalActivationDelay;
        state = CraneXYOperationState.ActivationDelay;
        return true;
    }

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
        carryArea?.CarryBy(delta);
        horizontalMovingRoot.position += delta;
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
        carryArea?.CarryBy(worldDelta);
        Vector3 local = verticalMovingRoot.localPosition;
        local.y = nextY;
        verticalMovingRoot.localPosition = local;
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
            if (hit != null && !hit.transform.IsChildOf(transform)) return true;
        }
        return false;
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
        carryArea?.ReleaseAtDestinationIfConfigured();
        state = CraneXYOperationState.Arrived;
        activeAxis = CraneXYActiveAxis.None;
        obstructionWarned = false;
        state = CraneXYOperationState.Idle;
    }

    private void Block()
    {
        currentAxisSpeed = 0f;
        state = CraneXYOperationState.Blocked;
        activeAxis = CraneXYActiveAxis.None;
        pendingCommand = CraneXYActiveAxis.None;
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
        state = CraneXYOperationState.Idle;
        activeAxis = CraneXYActiveAxis.None;
        currentAxisSpeed = 0f;
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

    public bool ApplyShutterFreeze(float duration, CameraAbilitySystem3D source)
    {
        if (!canPauseByShutter || duration <= 0f) return false;
        shutterReleaseTime = Mathf.Max(shutterReleaseTime, Time.time + duration);
        return true;
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
