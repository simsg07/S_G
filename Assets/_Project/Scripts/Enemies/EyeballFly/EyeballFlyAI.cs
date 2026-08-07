using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class EyeballFlyAI : MonsterAIBase
{
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int IsAttackingHash = Animator.StringToHash("IsAttacking");
    private static readonly int IsDeadHash = Animator.StringToHash("IsDead");
    private static readonly int AttackHash = Animator.StringToHash("Attack");

    public enum EyeballFlyState
    {
        IDLE = 0,
        MOVE = 1,
        ATTACK_READY = 2,
        DASH_ATTACK = 3,
        RECOVERY = 4,
        RETURN_HOME = 5,
        DEAD = 6
    }

    [Header("Detection")]
    [SerializeField, Min(0.05f)] private float lightCandidateRefreshInterval = 0.25f;

    [Header("Movement")]
    [FormerlySerializedAs("hoverAmplitude")]
    [SerializeField, Min(0f)] private float hoverAmount = 0.12f;
    [FormerlySerializedAs("hoverFrequency")]
    [SerializeField, Min(0f)] private float hoverSpeed = 2f;

    [Header("Attack")]
    [FormerlySerializedAs("attackDamage")]
    [SerializeField, Min(1)] private int damage = 1;
    [FormerlySerializedAs("attackDuration")]
    [SerializeField, Min(0f)] private float attackReadyTime = 0.5f;
    [SerializeField, Min(0f)] private float dashTriggerRange = 2f;
    [SerializeField, Min(0f)] private float dashSpeed = 6f;
    [SerializeField, Min(0f)] private float dashDistance = 2f;
    [FormerlySerializedAs("objectAttackLayerMask")]
    [SerializeField] private LayerMask damageableLayers = ~0;
    [SerializeField] private LayerMask worldBlockingLayers;
    [SerializeField] private bool attackPlayer = true;
    [FormerlySerializedAs("attackObjects")]
    [SerializeField] private bool attackPuzzleObjects = true;
    [SerializeField] private bool damageOtherMonsters;

    [Header("Recovery")]
    [FormerlySerializedAs("attackInterval")]
    [SerializeField, Min(0f)] private float recoveryTime = 0.6f;

    [Header("Return Home")]
    [SerializeField, Min(0f)] private float homeArrivalDistance = 0.15f;

    [Header("Debug")]
    [FormerlySerializedAs("debugAttackHit")]
    [SerializeField] private bool debugAttackHit;
    [SerializeField] private EyeballFlyState currentState = EyeballFlyState.IDLE;
    [SerializeField] private Transform currentDebugTarget;
    [SerializeField] private int detectedLightCandidateCount;
    [SerializeField] private string selectedLightName = "None";
    [SerializeField] private string lastLightRejectionReason = "None";
    [SerializeField] private int attackTriggerCount;
    [SerializeField] private int currentDashDamagedTargetCount;

    [Header("References")]
    [SerializeField] private EyeballFlyAnimationController animationController;
    [SerializeField] private Animator animator;

    private readonly RaycastHit[] dashHits = new RaycastHit[24];
    private readonly HashSet<int> damagedTargetIds = new HashSet<int>();
    private readonly List<Transform> cachedLightCandidates = new List<Transform>(8);
    private Collider bodyCollider;
    private float hoverPhase;
    private float stateEndsAt;
    private Vector3 lockedTargetPosition;
    private Vector3 lockedDashDirection;
    private Vector3 dashStartPosition;
    private bool dead;
    private bool initialUseGravity;
    private bool initialIsKinematic;
    private RigidbodyConstraints initialConstraints;
    private float nextLightCandidateRefreshTime;
    private bool dashDamageEnabled;

    public EyeballFlyState CurrentState => currentState;
    public Vector3 LockedDashDirection => lockedDashDirection;

    protected override void Awake()
    {
        ApplyEyeballDefaults();
        base.Awake();
        CacheEyeballReferences();
        CacheInitialPhysics();
        InitializeHover();
        ApplyAnimatorState();
    }

    protected override void OnEnable()
    {
        ApplyEyeballDefaults();
        base.OnEnable();
        CacheEyeballReferences();
        RestoreFlightPhysics();
        InitializeHover();
        ApplyAnimatorState();
    }

    protected override void OnValidate()
    {
        ApplyEyeballDefaults();
        base.OnValidate();
        hoverAmount = Mathf.Max(0f, hoverAmount);
        hoverSpeed = Mathf.Max(0f, hoverSpeed);
        damage = Mathf.Max(1, damage);
        attackReadyTime = Mathf.Max(0f, attackReadyTime);
        dashTriggerRange = Mathf.Max(0f, dashTriggerRange);
        dashSpeed = Mathf.Max(0f, dashSpeed);
        dashDistance = Mathf.Max(0f, dashDistance);
        recoveryTime = Mathf.Max(0f, recoveryTime);
        homeArrivalDistance = Mathf.Max(0f, homeArrivalDistance);
        lightCandidateRefreshInterval = Mathf.Max(0.05f, lightCandidateRefreshInterval);
        CacheEyeballReferences();
    }

    private void ApplyEyeballDefaults()
    {
        movementType = MonsterMovementType.Flying;
        useGravityForGround = false;
        detectPlayer = true;
        detectLight = true;
        canDetectLight = true;
        lightTag = CameraTagUtility3D.LightTag;
    }

    protected override void Update()
    {
        if (dead || currentState == EyeballFlyState.DEAD) return;

        if (currentState != EyeballFlyState.ATTACK_READY && currentState != EyeballFlyState.DASH_ATTACK)
        {
            base.Update();
        }

        currentDebugTarget = currentTarget;
        switch (currentState)
        {
            case EyeballFlyState.IDLE:
            case EyeballFlyState.MOVE:
                UpdateTrackingState();
                break;
            case EyeballFlyState.ATTACK_READY:
                if (Time.time >= stateEndsAt) BeginDash();
                break;
            case EyeballFlyState.RECOVERY:
                if (Time.time >= stateEndsAt)
                {
                    if (HasVisibleSelectedTarget()) ChangeState(EyeballFlyState.MOVE);
                    else EnterReturnHomeOrIdle();
                }
                break;
            case EyeballFlyState.RETURN_HOME:
                UpdateReturnHomeState();
                break;
        }
    }

    protected override void UpdateTargetSelection()
    {
        Transform previousTarget = currentTarget;
        MonsterTargetType previousType = currentTargetType;

        // Reuse the project's cached Player/Light lookup, then enforce Light > Player.
        base.UpdateTargetSelection();

        bool detectionEnabled = monsterDetection == null || monsterDetection.enableDetection;
        Transform nearestLight = detectionEnabled && detectLight && canDetectLight
            ? FindNearestVisibleLight()
            : null;
        bool lightDetected = nearestLight != null;
        bool lightKept = detectionEnabled && previousType == MonsterTargetType.Light && IsLightAvailable(previousTarget) &&
            IsStrictTargetDetected(previousTarget, targetKeepRange, true);
        bool playerDetected = detectionEnabled && detectPlayer &&
            IsStrictTargetDetected(playerTarget, playerDetectRange, false);
        bool playerKept = detectionEnabled && previousType == MonsterTargetType.Player && previousTarget != null &&
            IsStrictTargetDetected(previousTarget, targetKeepRange, false);

        if (lightDetected || lightKept)
        {
            currentTarget = lightDetected ? nearestLight : previousTarget;
            currentTargetType = MonsterTargetType.Light;
        }
        else if (playerDetected || playerKept)
        {
            currentTarget = playerDetected ? playerTarget : previousTarget;
            currentTargetType = MonsterTargetType.Player;
        }
        else
        {
            currentTarget = null;
            currentTargetType = MonsterTargetType.None;
        }

        isReturningHome = false;
    }

    protected override void FixedUpdate()
    {
        if (dead || currentState == EyeballFlyState.DEAD) return;

        if (currentState == EyeballFlyState.DASH_ATTACK)
        {
            UpdateDash(Time.fixedDeltaTime);
            return;
        }

        base.FixedUpdate();
    }

    protected override void UpdateBaseMovement(float deltaTime)
    {
        if (currentState == EyeballFlyState.MOVE)
        {
            base.UpdateBaseMovement(deltaTime);
            return;
        }

        if (currentState == EyeballFlyState.RETURN_HOME)
        {
            float speed = returnHomeSpeed > 0f ? returnHomeSpeed : moveSpeed;
            MoveTowardPosition(homePosition, speed, deltaTime);
            FaceTargetIfNeeded(homePosition, true);
            return;
        }

        lastMoveDirection = Vector3.zero;
        ApplyPosition(moveAnchorPosition + GetMovementOffset());
    }

    protected override Vector3 GetMovementOffset()
    {
        if (currentState != EyeballFlyState.IDLE) return Vector3.zero;
        float bob = Mathf.Sin(Time.time * hoverSpeed + hoverPhase) * hoverAmount;
        return new Vector3(0f, bob, 0f);
    }

    public void Die()
    {
        if (dead) return;

        dead = true;
        currentTarget = null;
        currentTargetType = MonsterTargetType.None;
        dashDamageEnabled = false;
        damagedTargetIds.Clear();
        ResetAttackVisual();
        ChangeState(EyeballFlyState.DEAD);

        if (body != null)
        {
            body.isKinematic = false;
            body.useGravity = true;
            body.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
            body.linearVelocity = Vector3.zero;
        }
    }

    public override void ResetMonster()
    {
        dead = false;
        dashDamageEnabled = false;
        damagedTargetIds.Clear();
        attackTriggerCount = 0;
        currentDashDamagedTargetCount = 0;
        ResetAttackVisual();
        RestoreFlightPhysics();
        base.ResetMonster();
        SetDeadVisual(false);
        ChangeState(EyeballFlyState.IDLE);
    }

    public void ConfigureDataDrivenAttack(
        float duration,
        LayerMask targetLayerMask,
        bool hitReceivers,
        bool playerEnabled,
        bool lightEnabled,
        bool objectsEnabled,
        bool attackDebug)
    {
        attackReadyTime = Mathf.Max(0f, duration);
        damageableLayers = targetLayerMask;
        attackPlayer = playerEnabled;
        attackPuzzleObjects = objectsEnabled && hitReceivers;
        detectLight = lightEnabled;
        debugAttackHit = attackDebug;
    }

    private void UpdateTrackingState()
    {
        if (!HasVisibleSelectedTarget())
        {
            EnterReturnHomeOrIdle();
            return;
        }

        if (GetPlanarDistance(currentTarget) <= dashTriggerRange)
        {
            BeginAttackReady();
            return;
        }

        ChangeState(EyeballFlyState.MOVE);
    }

    private void BeginAttackReady()
    {
        if (currentTarget == null) return;

        moveAnchorPosition = ProjectToFixedZ(GetCurrentPosition());
        lockedTargetPosition = ProjectToFixedZ(currentTarget.position);
        lockedDashDirection = lockedTargetPosition - moveAnchorPosition;
        lockedDashDirection.z = 0f;
        if (lockedDashDirection.sqrMagnitude <= 0.0001f)
        {
            lockedDashDirection = visualFacesRightByDefault ? Vector3.right : Vector3.left;
        }
        else
        {
            lockedDashDirection.Normalize();
        }

        stateEndsAt = Time.time + attackReadyTime;
        ChangeState(EyeballFlyState.ATTACK_READY);
    }

    private void BeginDash()
    {
        moveAnchorPosition = ProjectToFixedZ(GetCurrentPosition());
        dashStartPosition = moveAnchorPosition;
        damagedTargetIds.Clear();
        currentDashDamagedTargetCount = 0;
        dashDamageEnabled = true;
        ChangeState(EyeballFlyState.DASH_ATTACK);
        PlayAttackVisual();
    }

    private void UpdateDash(float deltaTime)
    {
        float travelled = Vector3.Distance(dashStartPosition, moveAnchorPosition);
        float remaining = Mathf.Max(0f, dashDistance - travelled);
        if (remaining <= 0.001f)
        {
            BeginRecovery();
            return;
        }

        float stepDistance = Mathf.Min(dashSpeed * deltaTime, remaining);
        if (stepDistance <= 0f)
        {
            BeginRecovery();
            return;
        }

        if (TryGetNearestDashHit(stepDistance, out RaycastHit nearestHit))
        {
            float safeDistance = Mathf.Max(0f, nearestHit.distance - 0.01f);
            moveAnchorPosition += lockedDashDirection * safeDistance;
            ApplyPosition(moveAnchorPosition);
            TryDamageDashTarget(nearestHit.collider, nearestHit.point);
            BeginRecovery();
            return;
        }

        moveAnchorPosition += lockedDashDirection * stepDistance;
        lastMoveDirection = lockedDashDirection;
        ApplyPosition(moveAnchorPosition);

        if (Vector3.Distance(dashStartPosition, moveAnchorPosition) >= dashDistance - 0.001f)
        {
            BeginRecovery();
        }
    }

    private bool TryGetNearestDashHit(float distance, out RaycastHit nearestHit)
    {
        nearestHit = default(RaycastHit);
        int combinedMask = worldBlockingLayers.value | damageableLayers.value;
        float radius = bodyCollider != null
            ? Mathf.Max(0.05f, Mathf.Min(bodyCollider.bounds.extents.x, bodyCollider.bounds.extents.y))
            : 0.1f;
        int count = Physics.SphereCastNonAlloc(
            GetCurrentPosition(),
            radius,
            lockedDashDirection,
            dashHits,
            distance + 0.02f,
            combinedMask,
            QueryTriggerInteraction.Collide);

        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            Collider candidate = dashHits[i].collider;
            if (candidate == null || candidate.transform.IsChildOf(transform)) continue;
            if (dashHits[i].distance >= nearestDistance) continue;

            nearestDistance = dashHits[i].distance;
            nearestHit = dashHits[i];
        }

        return nearestHit.collider != null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (currentState != EyeballFlyState.DASH_ATTACK || !dashDamageEnabled ||
            other == null || other.transform.IsChildOf(transform)) return;
        TryDamageDashTarget(other, other.ClosestPoint(transform.position));
        BeginRecovery();
    }

    private bool TryDamageDashTarget(Collider hitCollider, Vector3 hitPoint)
    {
        if (currentState != EyeballFlyState.DASH_ATTACK || !dashDamageEnabled ||
            hitCollider == null || !IsLayerIncluded(hitCollider.gameObject.layer, damageableLayers)) return false;

        Transform targetRoot = hitCollider.transform.root != null ? hitCollider.transform.root : hitCollider.transform;
        if (targetRoot == transform.root) return false;
        if (!damageOtherMonsters && targetRoot.GetComponentInChildren<MonsterCore>(true) != null) return false;

        IDamageable damageable = FindDamageable(hitCollider.transform);
        if (damageable == null || !damageable.CanTakeDamage) return false;

        if (!attackPlayer && targetRoot.CompareTag("Player")) return false;
        if (!attackPuzzleObjects && !targetRoot.CompareTag("Player")) return false;

        int targetId = targetRoot.GetInstanceID();
        if (!damagedTargetIds.Add(targetId)) return false;

        DamageInfo damageInfo = new DamageInfo(
            damage,
            gameObject,
            gameObject,
            hitPoint,
            lockedDashDirection,
            DamageType.MonsterAttack,
            HitSourceType.EyeballFlyAttack);
        damageable.TakeDamage(damageInfo);
        currentDashDamagedTargetCount++;
        LogAttack($"Dash damaged {targetRoot.name}. Damage={damage}");
        return true;
    }

    private void BeginRecovery()
    {
        if (currentState != EyeballFlyState.DASH_ATTACK) return;
        dashDamageEnabled = false;
        moveAnchorPosition = ProjectToFixedZ(GetCurrentPosition());
        lastMoveDirection = Vector3.zero;
        stateEndsAt = Time.time + recoveryTime;
        ResetAttackVisual();
        ChangeState(EyeballFlyState.RECOVERY);
    }

    private void EnterReturnHomeOrIdle()
    {
        currentTarget = null;
        currentTargetType = MonsterTargetType.None;
        if (returnHomeWhenTargetLost && !HasReachedHome())
        {
            isReturningHome = true;
            ChangeState(EyeballFlyState.RETURN_HOME);
            return;
        }

        isReturningHome = false;
        if (HasReachedHome())
        {
            moveAnchorPosition = homePosition;
            ClearBodyVelocity();
        }
        ChangeState(EyeballFlyState.IDLE);
    }

    private void UpdateReturnHomeState()
    {
        if (HasVisibleSelectedTarget())
        {
            isReturningHome = false;
            ChangeState(EyeballFlyState.MOVE);
            return;
        }

        if (!HasReachedHome()) return;

        moveAnchorPosition = homePosition;
        isReturningHome = false;
        ClearBodyVelocity();
        ApplyPosition(homePosition + GetMovementOffset());
        ChangeState(EyeballFlyState.IDLE);
    }

    private bool HasReachedHome()
    {
        Vector3 delta = homePosition - moveAnchorPosition;
        delta.z = 0f;
        return delta.sqrMagnitude <= homeArrivalDistance * homeArrivalDistance;
    }

    private void ClearBodyVelocity()
    {
        lastMoveDirection = Vector3.zero;
        if (body == null || body.isKinematic) return;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
    }

    private bool HasVisibleSelectedTarget()
    {
        if (currentTargetType == MonsterTargetType.Light)
        {
            return IsLightAvailable(currentTarget) &&
                IsStrictTargetDetected(currentTarget, Mathf.Max(lightDetectRange, targetKeepRange), true);
        }

        return currentTargetType == MonsterTargetType.Player && currentTarget != null &&
            IsStrictTargetDetected(currentTarget, Mathf.Max(playerDetectRange, targetKeepRange), false);
    }

    private bool IsStrictTargetDetected(Transform target, float range, bool requireEnabledLight)
    {
        if (monsterDetection != null)
        {
            return monsterDetection.IsTargetDetected(transform, target, range, requireEnabledLight, out _);
        }

        return IsTargetDetected(target, range);
    }

    private Transform FindNearestVisibleLight()
    {
        RefreshLightCandidatesIfNeeded();

        Transform best = null;
        float bestDistance = float.PositiveInfinity;
        detectedLightCandidateCount = 0;
        lastLightRejectionReason = cachedLightCandidates.Count == 0 ? "No tagged light candidates" : "None";
        for (int i = cachedLightCandidates.Count - 1; i >= 0; i--)
        {
            Transform candidate = cachedLightCandidates[i];
            if (candidate == null)
            {
                cachedLightCandidates.RemoveAt(i);
                lastLightRejectionReason = "Destroyed or missing";
                continue;
            }

            if (!candidate.gameObject.activeInHierarchy || !IsLightAvailable(candidate))
            {
                lastLightRejectionReason = $"{candidate.name}: inactive or light disabled";
                continue;
            }

            Vector3 candidateDelta = candidate.position - moveAnchorPosition;
            candidateDelta.z = 0f;
            if (candidateDelta.sqrMagnitude > lightDetectRange * lightDetectRange)
            {
                lastLightRejectionReason = $"{candidate.name}: out of range";
                continue;
            }

            if (!IsStrictTargetDetected(candidate, lightDetectRange, true))
            {
                lastLightRejectionReason = $"{candidate.name}: line of sight blocked";
                continue;
            }

            detectedLightCandidateCount++;
            Vector3 delta = candidate.position - moveAnchorPosition;
            delta.z = 0f;
            float distance = delta.sqrMagnitude;
            if (distance + 0.0001f >= bestDistance) continue;
            best = candidate;
            bestDistance = distance;
        }

        selectedLightName = best != null ? best.name : "None";
        return best;
    }

    private void RefreshLightCandidatesIfNeeded()
    {
        if (Time.time < nextLightCandidateRefreshTime) return;
        nextLightCandidateRefreshTime = Time.time + lightCandidateRefreshInterval;
        cachedLightCandidates.Clear();

        GameObject[] taggedLights;
        try
        {
            taggedLights = GameObject.FindGameObjectsWithTag(lightTag);
        }
        catch (UnityException)
        {
            return;
        }

        for (int i = 0; i < taggedLights.Length; i++)
        {
            GameObject candidate = taggedLights[i];
            if (candidate != null) cachedLightCandidates.Add(candidate.transform);
        }
    }

    private void CacheEyeballReferences()
    {
        if (animationController == null) animationController = GetComponentInChildren<EyeballFlyAnimationController>(true);
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        if (bodyCollider == null) bodyCollider = GetComponent<Collider>();

        if (monsterAttack != null)
        {
            damage = Mathf.Max(1, monsterAttack.attackDamage);
            attackRange = monsterAttack.attackRange;
        }

        if (worldBlockingLayers.value == 0)
        {
            worldBlockingLayers = obstacleLayerMask | movementObstacleLayerMask;
        }
    }

    private void CacheInitialPhysics()
    {
        if (body == null) return;
        initialUseGravity = body.useGravity;
        initialIsKinematic = body.isKinematic;
        initialConstraints = body.constraints;
    }

    private void RestoreFlightPhysics()
    {
        if (body == null) return;
        body.useGravity = initialUseGravity;
        body.isKinematic = initialIsKinematic;
        body.constraints = initialConstraints;
        if (!body.isKinematic)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }

    private void InitializeHover()
    {
        hoverPhase = Random.value * Mathf.PI * 2f;
    }

    private void ChangeState(EyeballFlyState nextState)
    {
        if (currentState == nextState) return;
        currentState = nextState;
        ApplyAnimatorState();
    }

    private void ApplyAnimatorState()
    {
        bool isDead = currentState == EyeballFlyState.DEAD;
        bool isMoving = currentState == EyeballFlyState.MOVE ||
            currentState == EyeballFlyState.DASH_ATTACK ||
            currentState == EyeballFlyState.RETURN_HOME;
        bool isAttacking = currentState == EyeballFlyState.DASH_ATTACK;

        SetMovingVisual(isMoving && !isDead);
        // Attack Trigger is the only entry path; this bool only keeps/exits the active Attack state.
        SetAttackingVisual(isAttacking && !isDead);
        SetDeadVisual(isDead);
    }

    private bool SetMovingVisual(bool value)
    {
        bool handled = animationController != null && animationController.SetMovingVisual(value);
        if (!handled && monsterAnimatorBridge != null && monsterAnimatorBridge.enableAnimatorBridge)
        {
            monsterAnimatorBridge.SetMoving(value);
            return true;
        }
        return handled || SetAnimatorBoolIfExists(animator, IsMovingHash, value);
    }

    private bool SetAttackingVisual(bool value)
    {
        bool handled = animationController != null && animationController.SetAttackingVisual(value);
        if (!handled && monsterAnimatorBridge != null && monsterAnimatorBridge.enableAnimatorBridge)
        {
            monsterAnimatorBridge.SetAttacking(value);
            return true;
        }
        return handled || SetAnimatorBoolIfExists(animator, IsAttackingHash, value);
    }

    private bool SetDeadVisual(bool value)
    {
        bool handled = animationController != null && animationController.SetDeadVisual(value);
        if (!handled && monsterAnimatorBridge != null && monsterAnimatorBridge.enableAnimatorBridge)
        {
            monsterAnimatorBridge.SetDead(value);
            return true;
        }
        return handled || SetAnimatorBoolIfExists(animator, IsDeadHash, value);
    }

    private void PlayAttackVisual()
    {
        attackTriggerCount++;
        if (animationController != null && animationController.PlayAttack()) return;
        if (monsterAnimatorBridge != null && monsterAnimatorBridge.enableAnimatorBridge)
        {
            monsterAnimatorBridge.TriggerAttack();
            return;
        }
        TriggerAnimatorIfExists(animator, AttackHash);
    }

    private void ResetAttackVisual()
    {
        if (animator != null) animator.ResetTrigger(AttackHash);
        SetAttackingVisual(false);
    }

    private static IDamageable FindDamageable(Transform target)
    {
        IDamageable damageable = target.GetComponent<IDamageable>();
        if (damageable != null) return damageable;
        damageable = target.GetComponentInParent<IDamageable>();
        return damageable ?? target.GetComponentInChildren<IDamageable>(true);
    }

    private static bool IsLayerIncluded(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    private void LogAttack(string message)
    {
        if (debugAttackHit) Debug.Log($"[EyeballFlyAI] {message}", this);
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, dashTriggerRange);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, playerDetectRange);
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, lightDetectRange);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(homePosition, homeArrivalDistance);
        Gizmos.DrawLine(transform.position, homePosition);
        if (currentState == EyeballFlyState.ATTACK_READY || currentState == EyeballFlyState.DASH_ATTACK)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, transform.position + lockedDashDirection * dashDistance);
            Gizmos.DrawWireSphere(lockedTargetPosition, 0.08f);
        }
    }
}
