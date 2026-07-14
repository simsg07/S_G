using UnityEngine;

[DisallowMultipleComponent]
public abstract class MonsterAIBase : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] protected bool detectPlayer = true;
    [SerializeField] protected bool detectLight = true;
    [SerializeField] protected bool canDetectLight = true;
    [SerializeField] protected Transform playerTarget;
    [SerializeField] protected Transform lightTarget;
    [SerializeField] protected string playerTag = "Player";
    [SerializeField] protected string lightTag = "Light";
    [SerializeField] protected float playerDetectRange = 1.5f;
    [SerializeField] protected float lightDetectRange = 4f;
    [SerializeField] protected float targetKeepRange = 6f;
    [SerializeField] protected float attackRange = 0.5f;
    [SerializeField] protected bool requireLineOfSight = true;
    [SerializeField] protected LayerMask obstacleLayerMask;
    [SerializeField] protected Vector3 lineOfSightStartOffset;
    [SerializeField] protected Vector3 targetCheckOffset;
    [SerializeField] protected float lostSightDelay = 0.2f;
    [SerializeField] protected bool debugLineOfSight = true;
    [SerializeField] protected float detectionLogInterval = 0.5f;

    [Header("Movement")]
    [SerializeField] protected MonsterMovementType movementType = MonsterMovementType.Ground;
    [SerializeField] protected float moveSpeed = 1f;
    [SerializeField] protected float returnHomeSpeed = 1f;
    [SerializeField] protected bool returnHomeWhenTargetLost = true;
    [SerializeField] protected bool faceTargetOnlyWhenDetected = true;
    [SerializeField] protected bool setMoveAnimatorOnlyWhenMoving = true;
    [SerializeField] protected float fixedZPosition = TwoPointFiveDUtility3D.GameplayPlaneZ;
    [SerializeField] protected bool blockMovementByObstacles = true;
    [SerializeField] protected LayerMask movementObstacleLayerMask;
    [SerializeField] protected float collisionCheckRadius = 0.25f;
    [SerializeField] protected float collisionCheckDistance = 0.1f;
    [SerializeField] protected bool useColliderBoundsForMovementCheck = true;
    [SerializeField] protected float movementCastVerticalOffset = 0.2f;
    [SerializeField] protected bool ignoreGroundWhenCheckingHorizontalMovement = true;
    [SerializeField] protected bool useGravityForGround = true;
    [SerializeField] protected LayerMask groundLayerMask;
    [SerializeField] protected float groundCheckDistance = 0.15f;
    [SerializeField] protected float groundCheckRadius = 0.2f;
    [SerializeField] protected bool isGrounded;
    [SerializeField] protected bool groundOnlyMoveX = true;
    [SerializeField] protected bool autoSnapToGroundOnStart;
    [SerializeField] protected float groundSnapMaxDistance = 1.5f;
    [SerializeField] protected float groundSnapOffset = 0.05f;
    [SerializeField] protected bool preventExternalPush = true;
    [SerializeField] protected float groundLinearDamping = 5f;
    [SerializeField] protected bool stopHorizontalVelocityWhenIdle = true;

    [Header("Visual Facing")]
    [SerializeField] protected Transform visualRoot;
    [SerializeField] protected bool flipVisualByScale = true;
    [SerializeField] protected bool visualFacesRightByDefault = true;
    [SerializeField] protected Transform facingVisualRoot;
    [SerializeField] protected bool invertFacing;
    [SerializeField] protected bool useVisualScaleFacing = true;

    [Header("Debug")]
    [SerializeField] protected bool showGizmos = true;
    [SerializeField] protected bool debugMode;

    protected Rigidbody body;
    protected Transform currentTarget;
    protected MonsterTargetType currentTargetType = MonsterTargetType.None;
    protected Vector3 homePosition;
    protected Vector3 moveAnchorPosition;
    protected Vector3 lastMoveDirection;
    protected bool isReturningHome;

    protected MonsterCore monsterCore;
    protected MonsterDetection monsterDetection;
    protected MonsterMovement monsterMovement;
    protected MonsterFacing monsterFacing;
    protected MonsterAttack monsterAttack;
    protected MonsterAnimatorBridge monsterAnimatorBridge;
    protected MonsterDebugInfo monsterDebugInfo;

    private Collider movementCollider;
    private float facingVisualBaseScaleX;
    private bool homeInitialized;
    private bool loggedInitialDebugInfo;
    private float nextDebugLogTime;
    private string targetSelectionReason = "None";
    private bool warnedPlayerTargetMissing;
    private bool warnedLightTargetMissing;
    private bool warnedObstacleMaskMissing;
    private bool warnedMovementMaskMissing;
    private bool warnedGroundMaskMissing;
    private bool warnedMovementColliderMissing;
    private Vector3 lastMovementCastStart;
    private Vector3 lastMovementCastEnd;
    private Vector3 lastGroundCheckStart;
    private Vector3 lastGroundCheckEnd;
    private Vector3 lastBlockedPoint;
    private Collider lastBlockedCollider;
    private Vector3 lastPlayerSightStart;
    private Vector3 lastPlayerSightEnd;
    private Vector3 lastLightSightStart;
    private Vector3 lastLightSightEnd;
    private Vector3 lastSightBlockedPoint;
    private Collider lastSightBlockedCollider;
    private Collider lastPlayerSightBlockedCollider;
    private Collider lastLightSightBlockedCollider;
    private bool lastPlayerLineOfSight = true;
    private bool lastLightLineOfSight = true;
    private float nextLineOfSightDebugLogTime;

    public Transform CurrentTarget => currentTarget;
    public MonsterTargetType CurrentTargetType => currentTargetType;
    public bool HasTarget => currentTarget != null && (currentTargetType == MonsterTargetType.Player || currentTargetType == MonsterTargetType.Light);
    public bool IsReturningHome => isReturningHome;
    public bool IsMoving => lastMoveDirection.sqrMagnitude > 0.0001f;
    public bool IsTargetInAttackRange => currentTarget != null && IsInRange(currentTarget, attackRange);
    public bool IsPlayerInAttackRange => IsInRange(playerTarget, attackRange);
    protected string LastBlockedColliderName => lastBlockedCollider != null ? lastBlockedCollider.name : "None";
    protected string LastSightBlockedColliderName => lastSightBlockedCollider != null ? lastSightBlockedCollider.name : "None";

    protected virtual void Awake()
    {
        CacheBaseReferences();
        InitializeHomeFromCurrentPosition();
        ConfigureBaseRigidbody();
    }

    protected virtual void OnEnable()
    {
        CacheBaseReferences();
        if (Application.isPlaying && !homeInitialized)
        {
            InitializeHomeFromCurrentPosition();
        }

        ConfigureBaseRigidbody();
    }

    protected virtual void Start()
    {
        CacheBaseReferences();
        SnapToGroundIfNeeded();
    }

    protected virtual void OnValidate()
    {
        playerDetectRange = Mathf.Max(0f, playerDetectRange);
        lightDetectRange = Mathf.Max(0f, lightDetectRange);
        targetKeepRange = Mathf.Max(0f, targetKeepRange);
        attackRange = Mathf.Max(0f, attackRange);
        moveSpeed = Mathf.Max(0f, moveSpeed);
        returnHomeSpeed = Mathf.Max(0f, returnHomeSpeed);
        lostSightDelay = Mathf.Max(0f, lostSightDelay);
        collisionCheckRadius = Mathf.Max(0.01f, collisionCheckRadius);
        collisionCheckDistance = Mathf.Max(0f, collisionCheckDistance);
        movementCastVerticalOffset = Mathf.Max(0f, movementCastVerticalOffset);
        groundCheckDistance = Mathf.Max(0.01f, groundCheckDistance);
        groundCheckRadius = Mathf.Max(0.01f, groundCheckRadius);
        groundSnapMaxDistance = Mathf.Max(0.01f, groundSnapMaxDistance);
        groundSnapOffset = Mathf.Max(0f, groundSnapOffset);
        groundLinearDamping = Mathf.Max(0f, groundLinearDamping);
        detectionLogInterval = Mathf.Max(0.05f, detectionLogInterval);
        ConfigureObstacleMasks();
        CacheBaseReferences();
    }

    protected virtual void Update()
    {
        if (!Application.isPlaying)
        {
            ClampToFixedZ();
            return;
        }

        UpdateTargetSelection();
        LogBaseDebug();
    }

    protected virtual void FixedUpdate()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        UpdateGroundCheck();
        UpdateBaseMovement(Time.fixedDeltaTime);
        MaintainGroundExternalPushControl();
    }

    public virtual void ResetMonster()
    {
        currentTarget = null;
        currentTargetType = MonsterTargetType.None;
        isReturningHome = false;
        lastMoveDirection = Vector3.zero;
        moveAnchorPosition = homePosition;
        ApplyPosition(homePosition);
    }

    protected virtual void CacheBaseReferences()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody>();
        }

        CacheFunctionalComponents();
        ApplyFunctionalComponentSettings();

        if (movementCollider == null)
        {
            movementCollider = GetPrimaryMovementCollider();
        }

        if (visualRoot == null && facingVisualRoot != null)
        {
            visualRoot = facingVisualRoot;
        }

        if (visualRoot == null)
        {
            Animator childAnimator = GetComponentInChildren<Animator>(true);
            if (childAnimator != null)
            {
                visualRoot = childAnimator.transform;
            }
            else
            {
                Renderer childRenderer = GetComponentInChildren<Renderer>(true);
                visualRoot = childRenderer != null ? childRenderer.transform : transform;
            }
        }

        if (facingVisualRoot == null)
        {
            facingVisualRoot = visualRoot;
        }

        if (visualRoot != null && Mathf.Abs(facingVisualBaseScaleX) <= 0.0001f)
        {
            facingVisualBaseScaleX = Mathf.Abs(visualRoot.localScale.x);
        }

        ConfigureObstacleMasks();
    }

    protected void CacheFunctionalComponents()
    {
        if (monsterCore == null)
        {
            monsterCore = GetComponent<MonsterCore>();
        }

        if (monsterDetection == null)
        {
            monsterDetection = GetComponent<MonsterDetection>();
        }

        if (monsterMovement == null)
        {
            monsterMovement = GetComponent<MonsterMovement>();
        }

        if (monsterFacing == null)
        {
            monsterFacing = GetComponent<MonsterFacing>();
        }

        if (monsterAttack == null)
        {
            monsterAttack = GetComponent<MonsterAttack>();
        }

        if (monsterAnimatorBridge == null)
        {
            monsterAnimatorBridge = GetComponent<MonsterAnimatorBridge>();
        }

        if (monsterDebugInfo == null)
        {
            monsterDebugInfo = GetComponent<MonsterDebugInfo>();
        }
    }

    protected virtual void ApplyFunctionalComponentSettings()
    {
        if (monsterCore != null)
        {
            monsterCore.AutoFill();
            if (monsterCore.monsterRigidbody != null)
            {
                body = monsterCore.monsterRigidbody;
            }

            if (monsterCore.mainCollider != null)
            {
                movementCollider = monsterCore.mainCollider;
            }

            if (monsterCore.playerTarget != null)
            {
                playerTarget = monsterCore.playerTarget;
            }

            if (monsterCore.lightTarget != null)
            {
                lightTarget = monsterCore.lightTarget;
            }

            if (monsterCore.visualRoot != null)
            {
                visualRoot = monsterCore.visualRoot;
                facingVisualRoot = monsterCore.visualRoot;
            }

            debugMode = debugMode || monsterCore.debugMode;
        }

        if (monsterDetection != null)
        {
            detectPlayer = monsterDetection.enableDetection && monsterDetection.canDetectPlayer;
            detectLight = monsterDetection.enableDetection && monsterDetection.canDetectLight;
            canDetectLight = monsterDetection.enableDetection && monsterDetection.canDetectLight;
            playerDetectRange = monsterDetection.playerDetectRange;
            lightDetectRange = monsterDetection.lightDetectRange;
            targetKeepRange = monsterDetection.chaseRange;
            requireLineOfSight = monsterDetection.requireLineOfSight;
            obstacleLayerMask = monsterDetection.obstacleLayerMask;
            lineOfSightStartOffset = monsterDetection.lineOfSightStartOffset;
            targetCheckOffset = monsterDetection.targetCheckOffset;
            debugLineOfSight = monsterDetection.debugLineOfSight;
            detectionLogInterval = monsterDetection.logInterval;
            showGizmos = showGizmos || monsterDetection.showGizmos;
            debugMode = debugMode || monsterDetection.debugMode;
        }

        if (monsterMovement != null)
        {
            movementType = monsterMovement.movementType;
            moveSpeed = monsterMovement.ActiveMoveSpeed;
            returnHomeSpeed = monsterMovement.returnSpeed;
            returnHomeWhenTargetLost = monsterMovement.returnToHomeWhenLost;
            blockMovementByObstacles = monsterMovement.enableMovement && monsterMovement.blockMovementByObstacles;
            movementObstacleLayerMask = monsterMovement.movementObstacleLayerMask;
            groundLayerMask = monsterMovement.groundLayerMask;
            groundOnlyMoveX = monsterMovement.groundOnlyMoveX;
            useGravityForGround = monsterMovement.useGravityForGround;
            preventExternalPush = monsterMovement.preventExternalPush;
            groundLinearDamping = monsterMovement.groundLinearDamping;
            stopHorizontalVelocityWhenIdle = monsterMovement.stopHorizontalVelocityWhenIdle;
            showGizmos = showGizmos || monsterMovement.showGizmos;
            debugMode = debugMode || monsterMovement.debugMode;
        }

        if (monsterFacing != null)
        {
            faceTargetOnlyWhenDetected = monsterFacing.faceOnlyWhenDetected;
            visualFacesRightByDefault = monsterFacing.visualFacesRightByDefault;
            invertFacing = monsterFacing.invertFacing;
            if (monsterFacing.visualRoot != null)
            {
                visualRoot = monsterFacing.visualRoot;
                facingVisualRoot = monsterFacing.visualRoot;
            }

            debugMode = debugMode || monsterFacing.debugMode;
        }

        if (monsterAttack != null)
        {
            attackRange = monsterAttack.attackRange;
            showGizmos = showGizmos || monsterAttack.showGizmos;
            debugMode = debugMode || monsterAttack.debugMode;
        }

        if (monsterDebugInfo != null)
        {
            showGizmos = showGizmos || monsterDebugInfo.showGizmos;
            debugMode = debugMode || monsterDebugInfo.debugMode;
        }
    }

    protected virtual void ConfigureBaseRigidbody()
    {
        if (body == null)
        {
            LogInitialDebugInfo();
            return;
        }

        body.useGravity = movementType == MonsterMovementType.Ground && useGravityForGround;
        body.isKinematic = movementType == MonsterMovementType.Flying;
        if (movementType == MonsterMovementType.Ground)
        {
            body.linearDamping = groundLinearDamping;
        }

        body.constraints &= ~RigidbodyConstraints.FreezePositionX;
        body.constraints &= ~RigidbodyConstraints.FreezePositionY;
        body.constraints |= TwoPointFiveDUtility3D.SideViewRigidbodyConstraints;
        body.position = ProjectToFixedZ(body.position);
        LogInitialDebugInfo();
    }

    protected virtual void InitializeHomeFromCurrentPosition()
    {
        fixedZPosition = transform.position.z;
        homePosition = transform.position;
        moveAnchorPosition = homePosition;
        homeInitialized = true;
    }

    protected virtual void UpdateTargetSelection()
    {
        CacheTargetsIfNeeded();

        Transform nextTarget = null;
        MonsterTargetType nextTargetType = MonsterTargetType.None;

        bool playerDetected = IsPlayerDetected();
        bool playerKept = currentTargetType == MonsterTargetType.Player
            && IsCurrentTargetValid()
            && IsTargetDetected(currentTarget, targetKeepRange);
        bool lightDetected = IsLightDetected();
        bool lightKept = currentTargetType == MonsterTargetType.Light
            && IsCurrentTargetValid()
            && IsTargetDetected(currentTarget, targetKeepRange);

        if (playerDetected)
        {
            nextTarget = playerTarget;
            nextTargetType = MonsterTargetType.Player;
            targetSelectionReason = "Target selected: Player because player has priority";
        }
        else if (playerKept)
        {
            nextTarget = currentTarget;
            nextTargetType = MonsterTargetType.Player;
            targetSelectionReason = "Target selected: Player because player is inside chase range";
        }
        else if (lightDetected)
        {
            nextTarget = lightTarget;
            nextTargetType = MonsterTargetType.Light;
            targetSelectionReason = "Target selected: Light because player not detected";
        }
        else if (lightKept)
        {
            nextTarget = currentTarget;
            nextTargetType = MonsterTargetType.Light;
            targetSelectionReason = "Target selected: Light because player not detected";
        }

        if (nextTarget != null)
        {
            currentTarget = nextTarget;
            currentTargetType = nextTargetType;
            isReturningHome = false;
            return;
        }

        currentTarget = null;
        currentTargetType = returnHomeWhenTargetLost ? MonsterTargetType.Home : MonsterTargetType.None;
        isReturningHome = returnHomeWhenTargetLost && !IsHomeReached();
        targetSelectionReason = returnHomeWhenTargetLost ? "Target selected: Home because no target detected" : "Target selected: None";
    }

    protected virtual void UpdateBaseMovement(float deltaTime)
    {
        if (monsterMovement != null && !monsterMovement.enableMovement)
        {
            lastMoveDirection = Vector3.zero;
            LogDebug("Movement disabled by MonsterMovement.");
            return;
        }

        if (currentTarget != null)
        {
            MoveTowardPosition(ProjectToFixedZ(currentTarget.position), moveSpeed, deltaTime);
            FaceTargetIfNeeded(currentTarget.position);
            return;
        }

        if (returnHomeWhenTargetLost && !IsHomeReached())
        {
            isReturningHome = true;
            MoveTowardPosition(homePosition, returnHomeSpeed, deltaTime);
            FaceTargetIfNeeded(homePosition);
            return;
        }

        isReturningHome = false;
        lastMoveDirection = Vector3.zero;
        ApplyPosition(moveAnchorPosition + GetMovementOffset());
    }

    protected virtual Vector3 GetMovementOffset()
    {
        return Vector3.zero;
    }

    protected void MoveTowardPosition(Vector3 targetPosition, float speed, float deltaTime)
    {
        if (movementType == MonsterMovementType.Ground)
        {
            MoveGroundTowardPosition(targetPosition, speed, deltaTime);
            return;
        }

        MoveFlyingTowardPosition(targetPosition, speed, deltaTime);
    }

    private void MoveFlyingTowardPosition(Vector3 targetPosition, float speed, float deltaTime)
    {
        Vector3 direction = targetPosition - moveAnchorPosition;
        direction.z = 0f;

        if (direction.sqrMagnitude > 0.0001f)
        {
            lastMoveDirection = direction.normalized;
            Vector3 delta = lastMoveDirection * Mathf.Min(speed * deltaTime, direction.magnitude);
            if (IsMovementBlocked(delta, out Collider blockedCollider, out Vector3 blockedPoint))
            {
                lastMoveDirection = Vector3.zero;
                lastBlockedCollider = blockedCollider;
                lastBlockedPoint = blockedPoint;
                LogDebug($"Movement blocked by {blockedCollider.name}");
                ApplyPosition(moveAnchorPosition + GetMovementOffset());
                return;
            }

            lastBlockedCollider = null;
            moveAnchorPosition += delta;
        }
        else
        {
            lastMoveDirection = Vector3.zero;
        }

        ApplyPosition(moveAnchorPosition + GetMovementOffset());
    }

    private void MoveGroundTowardPosition(Vector3 targetPosition, float speed, float deltaTime)
    {
        Vector3 currentPosition = GetCurrentPosition();
        moveAnchorPosition = ProjectToFixedZ(currentPosition);

        float xDelta = targetPosition.x - moveAnchorPosition.x;
        if (Mathf.Abs(xDelta) <= 0.001f)
        {
            lastMoveDirection = Vector3.zero;
            ApplyPosition(moveAnchorPosition);
            return;
        }

        if (!isGrounded)
        {
            LogDebug("Ground move X only while not grounded. Y velocity preserved.");
        }

        Vector3 direction = groundOnlyMoveX
            ? new Vector3(Mathf.Sign(xDelta), 0f, 0f)
            : targetPosition - moveAnchorPosition;
        direction.z = 0f;
        if (groundOnlyMoveX)
        {
            direction.y = 0f;
        }

        lastMoveDirection = direction.normalized;
        Vector3 delta = lastMoveDirection * Mathf.Min(speed * deltaTime, Mathf.Abs(xDelta));
        if (IsMovementBlocked(delta, out Collider blockedCollider, out Vector3 blockedPoint))
        {
            lastMoveDirection = Vector3.zero;
            lastBlockedCollider = blockedCollider;
            lastBlockedPoint = blockedPoint;
            LogDebug($"Movement blocked by {blockedCollider.name}");
            ApplyPosition(moveAnchorPosition);
            return;
        }

        lastBlockedCollider = null;
        moveAnchorPosition += delta;
        moveAnchorPosition.y = currentPosition.y;
        ApplyPosition(moveAnchorPosition);
    }

    protected void ApplyPosition(Vector3 position)
    {
        position = ProjectToFixedZ(position);
        if (movementType == MonsterMovementType.Ground)
        {
            position.y = GetCurrentPosition().y;
        }

        if (body != null && Application.isPlaying)
        {
            body.MovePosition(position);
            return;
        }

        transform.position = position;
    }

    protected Vector3 GetCurrentPosition()
    {
        return body != null ? body.position : transform.position;
    }

    protected Vector3 ProjectToFixedZ(Vector3 position)
    {
        position.z = fixedZPosition;
        return position;
    }

    protected void ClampToFixedZ()
    {
        ApplyPosition(ProjectToFixedZ(GetCurrentPosition()));
        moveAnchorPosition.z = fixedZPosition;
    }

    protected void MaintainGroundExternalPushControl()
    {
        if (body == null || movementType != MonsterMovementType.Ground || !preventExternalPush)
        {
            return;
        }

        Vector3 velocity = body.linearVelocity;
        bool shouldStopHorizontal = stopHorizontalVelocityWhenIdle || !IsMoving;
        if (shouldStopHorizontal && Mathf.Abs(velocity.x) > 0.001f)
        {
            velocity.x = 0f;
            body.linearVelocity = velocity;
        }
    }

    protected void UpdateGroundCheck()
    {
        if (movementType != MonsterMovementType.Ground)
        {
            isGrounded = false;
            return;
        }

        if (groundLayerMask.value == 0)
        {
            isGrounded = false;
            WarnMissingGroundMask();
            return;
        }

        Vector3 origin = transform.position;
        if (movementCollider == null)
        {
            movementCollider = GetPrimaryMovementCollider();
        }

        if (movementCollider != null)
        {
            Bounds bounds = movementCollider.bounds;
            origin = new Vector3(bounds.center.x, bounds.min.y + groundCheckRadius + 0.02f, fixedZPosition);
        }

        lastGroundCheckStart = origin;
        lastGroundCheckEnd = origin + Vector3.down * groundCheckDistance;

        if (groundLayerMask.value == 0)
        {
            isGrounded = false;
            return;
        }

        Vector3 checkPoint = origin + Vector3.down * groundCheckDistance;
        isGrounded = Physics.CheckSphere(
            checkPoint,
            groundCheckRadius,
            groundLayerMask,
            QueryTriggerInteraction.Ignore);

        if (isGrounded)
        {
            return;
        }

        isGrounded = Physics.SphereCast(
            origin,
            groundCheckRadius,
            Vector3.down,
            out _,
            groundCheckDistance,
            groundLayerMask,
            QueryTriggerInteraction.Ignore);
    }

    protected void SnapToGroundIfNeeded()
    {
        if (!Application.isPlaying ||
            movementType != MonsterMovementType.Ground ||
            !autoSnapToGroundOnStart ||
            groundLayerMask.value == 0)
        {
            return;
        }

        if (movementCollider == null)
        {
            movementCollider = GetPrimaryMovementCollider();
        }

        Vector3 origin = transform.position + Vector3.up * Mathf.Max(0.1f, groundSnapMaxDistance * 0.5f);
        origin.z = fixedZPosition;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, groundSnapMaxDistance, groundLayerMask, QueryTriggerInteraction.Ignore))
        {
            Debug.LogWarning($"[{GetType().Name}] Ground snap failed. No ground found.", this);
            return;
        }

        float targetY = hit.point.y + groundSnapOffset;
        if (movementCollider != null)
        {
            targetY += transform.position.y - movementCollider.bounds.min.y;
        }

        Vector3 snapped = GetCurrentPosition();
        snapped.y = targetY;
        snapped.z = fixedZPosition;
        moveAnchorPosition = snapped;

        if (body != null)
        {
            body.position = snapped;
        }
        else
        {
            transform.position = snapped;
        }

        if (debugMode)
        {
            LogDebug($"Snapped to ground at Y={targetY:0.###}");
        }
    }

    protected bool IsInRange(Transform target, float range)
    {
        if (target == null)
        {
            return false;
        }

        Vector3 delta = target.position - moveAnchorPosition;
        delta.z = 0f;
        return delta.sqrMagnitude <= range * range;
    }

    protected bool IsTargetDetected(Transform target, float range)
    {
        if (!IsInRange(target, range))
        {
            return false;
        }

        if (!requireLineOfSight)
        {
            RecordLineOfSight(target, true, transform.position + lineOfSightStartOffset, target.position + targetCheckOffset, null, target.position);
            return true;
        }

        return HasLineOfSightTo(target);
    }

    protected bool IsPlayerDetected()
    {
        if (monsterDetection != null && !monsterDetection.enableDetection)
        {
            LogDebug("Detection disabled by MonsterDetection.");
            return false;
        }

        return detectPlayer && IsTargetDetected(playerTarget, playerDetectRange);
    }

    protected bool IsLightDetected()
    {
        if (monsterDetection != null && !monsterDetection.enableDetection)
        {
            LogDebug("Detection disabled by MonsterDetection.");
            return false;
        }

        return detectLight && canDetectLight && IsLightAvailable(lightTarget) && IsTargetDetected(lightTarget, lightDetectRange);
    }

    protected float GetPlanarDistance(Transform target)
    {
        if (target == null)
        {
            return 0f;
        }

        Vector3 delta = target.position - moveAnchorPosition;
        delta.z = 0f;
        return delta.magnitude;
    }

    protected bool IsLightAvailable(Transform target)
    {
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            return false;
        }

        Light lightComponent = target.GetComponentInChildren<Light>();
        return lightComponent == null || lightComponent.enabled;
    }

    protected bool IsCurrentTargetValid()
    {
        if (currentTarget == null)
        {
            return false;
        }

        if (currentTargetType == MonsterTargetType.Light)
        {
            return detectLight && canDetectLight && IsLightAvailable(currentTarget);
        }

        if (currentTargetType == MonsterTargetType.Player)
        {
            return detectPlayer && currentTarget.gameObject.activeInHierarchy;
        }

        return false;
    }

    protected bool HasLineOfSightTo(Transform target)
    {
        return HasLineOfSightTo(target, obstacleLayerMask);
    }

    protected bool IsBlockedByObstacle(Transform target)
    {
        return target != null && !HasLineOfSightTo(target);
    }

    protected bool IsPlayerVisible()
    {
        return detectPlayer && playerTarget != null && IsTargetVisible(playerTarget);
    }

    protected bool IsLightVisible()
    {
        return detectLight && canDetectLight && IsLightAvailable(lightTarget) && IsTargetVisible(lightTarget);
    }

    private bool IsTargetVisible(Transform target)
    {
        if (!requireLineOfSight)
        {
            RecordLineOfSight(target, true, transform.position + lineOfSightStartOffset, target.position + targetCheckOffset, null, target.position);
            return true;
        }

        return HasLineOfSightTo(target);
    }

    protected bool HasLineOfSightTo(Transform target, LayerMask obstacleMask)
    {
        if (target == null)
        {
            return false;
        }

        if (requireLineOfSight && obstacleMask.value == 0)
        {
            RecordLineOfSight(
                target,
                true,
                transform.position + lineOfSightStartOffset,
                target.position + targetCheckOffset,
                null,
                target.position);
            WarnMissingObstacleMask();
            return true;
        }

        Vector3 origin = transform.position + lineOfSightStartOffset;
        Vector3 destination = target.position + targetCheckOffset;
        origin.z = fixedZPosition;
        destination.z = fixedZPosition;
        Vector3 direction = destination - origin;
        float distance = direction.magnitude;

        if (distance <= 0.001f)
        {
            RecordLineOfSight(target, true, origin, destination, null, destination);
            return true;
        }

        RaycastHit[] hits = Physics.RaycastAll(origin, direction.normalized, distance, obstacleMask, QueryTriggerInteraction.Ignore);
        Collider nearestBlockingCollider = null;
        Vector3 nearestBlockingPoint = destination;
        float nearestBlockingDistance = float.PositiveInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null || BelongsToHierarchy(hitCollider.transform, transform) || BelongsToHierarchy(hitCollider.transform, target))
            {
                continue;
            }

            MapPiece mapPiece = hitCollider.GetComponentInParent<MapPiece>();
            if (mapPiece != null && !mapPiece.BlockLineOfSight)
            {
                continue;
            }

            if (hits[i].distance < nearestBlockingDistance)
            {
                nearestBlockingDistance = hits[i].distance;
                nearestBlockingCollider = hitCollider;
                nearestBlockingPoint = hits[i].point;
            }
        }

        bool hasLineOfSight = nearestBlockingCollider == null;
        RecordLineOfSight(target, hasLineOfSight, origin, destination, nearestBlockingCollider, nearestBlockingPoint);

        if (!hasLineOfSight && debugMode && debugLineOfSight && Time.time >= nextLineOfSightDebugLogTime)
        {
            nextLineOfSightDebugLogTime = Time.time + detectionLogInterval;
            LogDebug($"Line of sight blocked by {nearestBlockingCollider.name}. Target={target.name}");
        }

        if (!hasLineOfSight)
        {
            return false;
        }

        return true;
    }

    private static bool BelongsToHierarchy(Transform candidate, Transform hierarchyRoot)
    {
        return candidate != null && hierarchyRoot != null &&
            (candidate == hierarchyRoot || candidate.IsChildOf(hierarchyRoot) || hierarchyRoot.IsChildOf(candidate));
    }

    private void RecordLineOfSight(
        Transform target,
        bool hasLineOfSight,
        Vector3 origin,
        Vector3 destination,
        Collider blockingCollider,
        Vector3 blockingPoint)
    {
        origin.z = fixedZPosition;
        destination.z = fixedZPosition;

        if (target == playerTarget)
        {
            lastPlayerSightStart = origin;
            lastPlayerSightEnd = destination;
            lastPlayerLineOfSight = hasLineOfSight;
            lastPlayerSightBlockedCollider = blockingCollider;
        }
        else if (target == lightTarget)
        {
            lastLightSightStart = origin;
            lastLightSightEnd = destination;
            lastLightLineOfSight = hasLineOfSight;
            lastLightSightBlockedCollider = blockingCollider;
        }

        if (!hasLineOfSight)
        {
            lastSightBlockedCollider = blockingCollider;
            lastSightBlockedPoint = blockingPoint;
        }
        else if (blockingCollider == null && lastSightBlockedCollider != null && target == currentTarget)
        {
            lastSightBlockedCollider = null;
        }
    }

    protected bool IsMovementBlocked(Vector3 delta, out Collider blockedCollider, out Vector3 blockedPoint)
    {
        blockedCollider = null;
        blockedPoint = moveAnchorPosition;
        if (!blockMovementByObstacles || delta.sqrMagnitude <= 0.000001f)
        {
            return false;
        }

        if (movementObstacleLayerMask.value == 0)
        {
            WarnMissingMovementMask();
            return false;
        }

        Vector3 direction = delta.normalized;
        float distance = delta.magnitude + collisionCheckDistance;
        if (distance <= 0.001f)
        {
            return false;
        }

        if (movementCollider == null)
        {
            movementCollider = GetPrimaryMovementCollider();
        }

        bool blocked = movementCollider != null && useColliderBoundsForMovementCheck
            ? CastColliderBounds(direction, distance, out blockedCollider, out blockedPoint)
            : CastFallbackSphere(direction, distance, out blockedCollider, out blockedPoint);

        if (!blocked && movementCollider == null)
        {
            WarnMissingMovementCollider();
        }

        return blocked;
    }

    private bool CastColliderBounds(Vector3 direction, float distance, out Collider blockedCollider, out Vector3 blockedPoint)
    {
        Bounds bounds = movementCollider.bounds;
        Vector3 center = bounds.center;
        center.z = fixedZPosition;
        if (movementType == MonsterMovementType.Ground)
        {
            center.y += movementCastVerticalOffset;
        }

        Vector3 halfExtents = bounds.extents * 0.9f;
        halfExtents.x = Mathf.Max(0.01f, halfExtents.x);
        halfExtents.y = Mathf.Max(0.01f, halfExtents.y);
        halfExtents.z = Mathf.Max(0.01f, halfExtents.z);
        lastMovementCastStart = center;
        lastMovementCastEnd = center + direction * distance;

        RaycastHit[] hits = Physics.BoxCastAll(center, halfExtents, direction, Quaternion.identity, distance, movementObstacleLayerMask, QueryTriggerInteraction.Ignore);
        return TryResolveBlockingHit(hits, direction, out blockedCollider, out blockedPoint);
    }

    private bool CastFallbackSphere(Vector3 direction, float distance, out Collider blockedCollider, out Vector3 blockedPoint)
    {
        Vector3 center = moveAnchorPosition;
        center.z = fixedZPosition;
        lastMovementCastStart = center;
        lastMovementCastEnd = center + direction * distance;

        RaycastHit[] hits = Physics.SphereCastAll(center, collisionCheckRadius, direction, distance, movementObstacleLayerMask, QueryTriggerInteraction.Ignore);
        return TryResolveBlockingHit(hits, direction, out blockedCollider, out blockedPoint);
    }

    private bool TryResolveBlockingHit(RaycastHit[] hits, Vector3 movementDirection, out Collider blockedCollider, out Vector3 blockedPoint)
    {
        blockedCollider = null;
        blockedPoint = moveAnchorPosition;
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            Collider hitCollider = hit.collider;
            if (hitCollider == null || hitCollider.transform.IsChildOf(transform))
            {
                continue;
            }

            if (movementType == MonsterMovementType.Ground &&
                ignoreGroundWhenCheckingHorizontalMovement &&
                IsGroundContactHit(hit))
            {
                if (debugMode)
                {
                    LogDebug($"Movement not blocked by ground contact: {hitCollider.name}");
                }

                continue;
            }

            if (hit.normal.sqrMagnitude > 0.0001f && Vector3.Dot(hit.normal, -movementDirection) < 0.2f)
            {
                continue;
            }

            blockedCollider = hitCollider;
            blockedPoint = hit.point;
            return true;
        }

        return false;
    }

    private bool IsGroundContactHit(RaycastHit hit)
    {
        if (hit.collider == null)
        {
            return false;
        }

        if (hit.normal.y > 0.35f)
        {
            return true;
        }

        if (movementCollider == null)
        {
            return false;
        }

        Bounds bounds = movementCollider.bounds;
        return hit.point.y <= bounds.min.y + movementCastVerticalOffset;
    }

    private Collider GetPrimaryMovementCollider()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider targetCollider = colliders[i];
            if (targetCollider != null && !targetCollider.isTrigger)
            {
                return targetCollider;
            }
        }

        return null;
    }

    private void ConfigureObstacleMasks()
    {
        int sightMask = LayerMask.GetMask("Ground", "Wall", "TileObstacle", "Platform", "EnvironmentObstacle");
        int movementMask = LayerMask.GetMask("Wall", "TileObstacle", "EnvironmentObstacle");
        int defaultGroundMask = LayerMask.GetMask("Ground", "Floor", "Platform");

        if (sightMask != 0)
        {
            obstacleLayerMask |= sightMask;
        }

        if (movementObstacleLayerMask.value == 0 && movementMask != 0)
        {
            movementObstacleLayerMask = movementMask;
        }

        if (groundLayerMask.value == 0 && defaultGroundMask != 0)
        {
            groundLayerMask = defaultGroundMask;
        }
    }

    protected bool TryDamageTarget(Transform target, int damage)
    {
        if (damage <= 0)
        {
            return false;
        }

        IDamageable damageable = GetDamageableFrom(target);
        if (damageable == null)
        {
            LogDebug("No IDamageable found on target.");
            return false;
        }

        damageable.TakeDamage(damage);
        LogDebug($"Damage Applied. Damage={damage}, Receiver={damageable}");
        return true;
    }

    protected IDamageable GetDamageableFrom(Transform target)
    {
        if (target == null)
        {
            return null;
        }

        IDamageable damageable = target.GetComponent<IDamageable>();
        if (damageable != null)
        {
            return damageable;
        }

        damageable = target.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            return damageable;
        }

        return target.GetComponentInChildren<IDamageable>();
    }

    protected bool SetAnimatorBoolIfExists(Animator targetAnimator, int parameterHash, bool value)
    {
        if (!HasAnimatorParameter(targetAnimator, parameterHash, AnimatorControllerParameterType.Bool))
        {
            return false;
        }

        targetAnimator.SetBool(parameterHash, value);
        return true;
    }

    protected bool SetAnimatorIntIfExists(Animator targetAnimator, int parameterHash, int value)
    {
        if (!HasAnimatorParameter(targetAnimator, parameterHash, AnimatorControllerParameterType.Int))
        {
            return false;
        }

        targetAnimator.SetInteger(parameterHash, value);
        return true;
    }

    protected bool TriggerAnimatorIfExists(Animator targetAnimator, int parameterHash)
    {
        if (!HasAnimatorParameter(targetAnimator, parameterHash, AnimatorControllerParameterType.Trigger))
        {
            return false;
        }

        targetAnimator.SetTrigger(parameterHash);
        return true;
    }

    protected virtual void LogDebug(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[{GetType().Name}] {message}", this);
        }
    }

    private void CacheTargetsIfNeeded()
    {
        if (detectPlayer && playerTarget == null)
        {
            playerTarget = FindTarget(playerTag, "Player", ref warnedPlayerTargetMissing);
        }

        if (detectLight && canDetectLight && lightTarget == null)
        {
            lightTarget = FindTarget(lightTag, "Light", ref warnedLightTargetMissing);
            if (lightTarget == null)
            {
                Light lightComponent = FindFirstObjectByType<Light>();
                lightTarget = lightComponent != null ? lightComponent.transform : null;
            }
        }
    }

    private Transform FindTarget(string tagName, string fallbackName, ref bool warned)
    {
        if (!string.IsNullOrWhiteSpace(tagName))
        {
            try
            {
                GameObject tagged = GameObject.FindGameObjectWithTag(tagName);
                if (tagged != null)
                {
                    return tagged.transform;
                }
            }
            catch (UnityException)
            {
                LogDebug($"Tag '{tagName}' does not exist. Falling back to name search.");
            }
        }

        GameObject named = GameObject.Find(fallbackName);
        if (named != null)
        {
            return named.transform;
        }

        if (debugMode && !warned)
        {
            warned = true;
            Debug.LogWarning($"[{GetType().Name}] Target '{fallbackName}' was not found.", this);
        }

        return null;
    }

    private bool IsHomeReached()
    {
        Vector3 delta = homePosition - moveAnchorPosition;
        delta.z = 0f;
        return delta.sqrMagnitude <= 0.0001f;
    }

    protected virtual void FaceTargetIfNeeded(Vector3 targetPosition)
    {
        if (monsterFacing != null && !monsterFacing.enableFacing)
        {
            LogDebug("Facing disabled by MonsterFacing.");
            return;
        }

        if (faceTargetOnlyWhenDetected && currentTarget == null && !isReturningHome)
        {
            return;
        }

        float xDelta = targetPosition.x - transform.position.x;
        if (Mathf.Abs(xDelta) <= 0.001f)
        {
            return;
        }

        bool desiredFaceRight = xDelta > 0f;
        bool usePositiveScale = desiredFaceRight == visualFacesRightByDefault;
        if (invertFacing)
        {
            usePositiveScale = !usePositiveScale;
        }

        Transform targetVisualRoot = visualRoot != null ? visualRoot : facingVisualRoot;
        if (flipVisualByScale && useVisualScaleFacing && targetVisualRoot != null && targetVisualRoot != transform)
        {
            Vector3 scale = targetVisualRoot.localScale;
            float baseScaleX = facingVisualBaseScaleX > 0.0001f ? facingVisualBaseScaleX : Mathf.Abs(scale.x);
            scale.x = baseScaleX * (usePositiveScale ? 1f : -1f);
            targetVisualRoot.localScale = scale;

            if (debugMode)
            {
                LogDebug($"Facing targetDirection.x={xDelta:0.###}, desiredFaceRight={desiredFaceRight}, visualFacesRightByDefault={visualFacesRightByDefault}, invertFacing={invertFacing}, finalScaleX={scale.x:0.###}");
            }

            return;
        }

        SpriteRenderer[] spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        bool flipX = !usePositiveScale;
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
            {
                spriteRenderers[i].flipX = flipX;
            }
        }

        if (debugMode)
        {
            LogDebug($"Facing targetDirection.x={xDelta:0.###}, desiredFaceRight={desiredFaceRight}, visualFacesRightByDefault={visualFacesRightByDefault}, invertFacing={invertFacing}, spriteFlipX={flipX}");
        }
    }

    private void LogBaseDebug()
    {
        if (!debugMode || Time.time < nextDebugLogTime)
        {
            return;
        }

        nextDebugLogTime = Time.time + detectionLogInterval;
        string targetName = currentTarget != null ? currentTarget.name : "None";
        float distance = currentTarget != null ? GetPlanarDistance(currentTarget) : -1f;
        float playerDistance = playerTarget != null ? GetPlanarDistance(playerTarget) : -1f;
        float lightDistance = lightTarget != null ? GetPlanarDistance(lightTarget) : -1f;
        bool playerInRange = IsInRange(playerTarget, playerDetectRange);
        bool lightInRange = IsInRange(lightTarget, lightDetectRange);
        bool playerLineOfSight = playerTarget != null && (!requireLineOfSight || HasLineOfSightTo(playerTarget));
        bool lightLineOfSight = lightTarget != null && (!requireLineOfSight || HasLineOfSightTo(lightTarget));
        bool playerDetected = detectPlayer && playerInRange && playerLineOfSight;
        bool lightDetected = detectLight && canDetectLight && IsLightAvailable(lightTarget) && lightInRange && lightLineOfSight;
        string playerFailure = GetDetectionFailureReason(playerTarget, playerInRange, playerLineOfSight, false);
        string lightFailure = GetDetectionFailureReason(lightTarget, lightInRange, lightLineOfSight, true);
        Debug.Log(
            $"[{GetType().Name}] TargetType={currentTargetType}, Target={targetName}, Distance={distance:0.00}, " +
            $"PlayerDistance={playerDistance:0.00}, PlayerInRange={playerInRange}, PlayerLOS={playerLineOfSight}, PlayerDetected={playerDetected}, PlayerReason='{playerFailure}', " +
            $"LightDistance={lightDistance:0.00}, LightInRange={lightInRange}, LightLOS={lightLineOfSight}, LightDetected={lightDetected}, LightReason='{lightFailure}', " +
            $"AttackInRange={IsTargetInAttackRange}, Moving={IsMoving}, MovementType={movementType}, IsGrounded={isGrounded}, " +
            $"ObstacleMask={obstacleLayerMask.value}, SightBlockedBy={(lastSightBlockedCollider != null ? lastSightBlockedCollider.name : "None")}, " +
            $"MoveDirection={lastMoveDirection}, BlockedBy={(lastBlockedCollider != null ? lastBlockedCollider.name : "None")}, ReturningHome={isReturningHome}, Reason='{targetSelectionReason}'",
            this);
    }

    private string GetDetectionFailureReason(Transform target, bool inRange, bool lineOfSight, bool isLight)
    {
        if (target == null)
        {
            return "No target";
        }

        if (isLight && !IsLightAvailable(target))
        {
            return "Light inactive";
        }

        if (!inRange)
        {
            return "Out of range";
        }

        if (!lineOfSight)
        {
            Collider blockingCollider = isLight ? lastLightSightBlockedCollider : lastPlayerSightBlockedCollider;
            return blockingCollider != null
                ? $"Line of sight blocked by {blockingCollider.name}"
                : "Line of sight blocked";
        }

        return "Detected";
    }

    private void LogInitialDebugInfo()
    {
        if (!debugMode || loggedInitialDebugInfo)
        {
            return;
        }

        loggedInitialDebugInfo = true;
        Debug.Log(
            $"[{GetType().Name}] MovementType={movementType}, Rigidbody={body != null}, IsKinematic={(body != null && body.isKinematic)}, UseGravity={(body != null && body.useGravity)}, " +
            $"Constraints={(body != null ? body.constraints.ToString() : "None")}, Collider={movementCollider != null}, " +
            $"ObstacleMask={obstacleLayerMask.value}, MovementObstacleMask={movementObstacleLayerMask.value}, GroundMask={groundLayerMask.value}, FixedZ={fixedZPosition:0.###}",
            this);

        if (body != null && (body.constraints & RigidbodyConstraints.FreezePositionX) != 0)
        {
            Debug.LogWarning($"[{GetType().Name}] Rigidbody Freeze Position X is enabled.", this);
        }

        if (body != null && (body.constraints & RigidbodyConstraints.FreezePositionY) != 0)
        {
            Debug.LogWarning($"[{GetType().Name}] Rigidbody Freeze Position Y is enabled.", this);
        }

        WarnMissingObstacleMask();
        WarnMissingMovementMask();
        WarnMissingMovementCollider();
    }

    private void WarnMissingObstacleMask()
    {
        if (warnedObstacleMaskMissing || !requireLineOfSight || obstacleLayerMask.value != 0)
        {
            return;
        }

        warnedObstacleMaskMissing = true;
        Debug.LogWarning($"[{GetType().Name}] obstacleLayerMask is empty. Line-of-sight checks will not block through walls.", this);
    }

    private void WarnMissingMovementMask()
    {
        if (warnedMovementMaskMissing || !blockMovementByObstacles || movementObstacleLayerMask.value != 0)
        {
            return;
        }

        warnedMovementMaskMissing = true;
        Debug.LogWarning($"[{GetType().Name}] movementObstacleLayerMask is empty. Movement will not be blocked by map obstacles.", this);
    }

    private void WarnMissingGroundMask()
    {
        if (warnedGroundMaskMissing || movementType != MonsterMovementType.Ground || groundLayerMask.value != 0)
        {
            return;
        }

        warnedGroundMaskMissing = true;
        Debug.LogWarning($"[{GetType().Name}] groundLayerMask is empty. Ground detection is disabled until Ground or Platform is assigned.", this);
    }

    private void WarnMissingMovementCollider()
    {
        if (warnedMovementColliderMissing || !blockMovementByObstacles || movementCollider != null)
        {
            return;
        }

        warnedMovementColliderMissing = true;
        Debug.LogWarning($"[{GetType().Name}] No non-trigger Collider found. Movement collision check will use SphereCast fallback.", this);
    }

    protected virtual void OnDrawGizmos()
    {
        if (!showGizmos)
        {
            return;
        }

        Vector3 center = Application.isPlaying ? moveAnchorPosition : transform.position;
        center.z = fixedZPosition;

        Gizmos.color = new Color(1f, 0.95f, 0.15f, 0.35f);
        Gizmos.DrawWireSphere(center, lightDetectRange);

        Gizmos.color = new Color(0.2f, 0.55f, 1f, 0.35f);
        Gizmos.DrawWireSphere(center, playerDetectRange);

        Gizmos.color = new Color(0.45f, 1f, 0.45f, 0.25f);
        Gizmos.DrawWireSphere(center, targetKeepRange);

        Gizmos.color = new Color(1f, 0.15f, 0.15f, 0.45f);
        Gizmos.DrawWireSphere(center, attackRange);

        if (currentTarget != null)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawLine(center, currentTarget.position);
        }

        if (playerTarget != null)
        {
            Gizmos.color = lastPlayerLineOfSight ? Color.green : Color.red;
            Vector3 playerRayStart = Application.isPlaying ? lastPlayerSightStart : transform.position + lineOfSightStartOffset;
            Vector3 playerRayEnd = Application.isPlaying ? lastPlayerSightEnd : playerTarget.position + targetCheckOffset;
            Gizmos.DrawLine(playerRayStart, playerRayEnd);
        }

        if (lightTarget != null)
        {
            Gizmos.color = lastLightLineOfSight ? Color.yellow : Color.red;
            Vector3 lightRayStart = Application.isPlaying ? lastLightSightStart : transform.position + lineOfSightStartOffset;
            Vector3 lightRayEnd = Application.isPlaying ? lastLightSightEnd : lightTarget.position + targetCheckOffset;
            Gizmos.DrawLine(lightRayStart, lightRayEnd);
        }

        if (lastSightBlockedCollider != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(lastSightBlockedPoint, 0.08f);
        }

        if (Application.isPlaying)
        {
            Gizmos.color = lastBlockedCollider != null ? Color.red : Color.cyan;
            Gizmos.DrawLine(lastMovementCastStart, lastMovementCastEnd);
            Gizmos.DrawWireSphere(lastMovementCastEnd, collisionCheckRadius);

            if (lastBlockedCollider != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(lastBlockedPoint, collisionCheckRadius);
            }

            if (movementType == MonsterMovementType.Ground)
            {
                Gizmos.color = isGrounded ? Color.green : Color.red;
                Gizmos.DrawWireSphere(lastGroundCheckStart, groundCheckRadius);
                Gizmos.DrawLine(lastGroundCheckStart, lastGroundCheckEnd);
            }
        }
    }

    private bool HasAnimatorParameter(Animator targetAnimator, int parameterHash, AnimatorControllerParameterType parameterType)
    {
        if (targetAnimator == null)
        {
            return false;
        }

        foreach (AnimatorControllerParameter parameter in targetAnimator.parameters)
        {
            if (parameter.nameHash == parameterHash && parameter.type == parameterType)
            {
                return true;
            }
        }

        return false;
    }
}
