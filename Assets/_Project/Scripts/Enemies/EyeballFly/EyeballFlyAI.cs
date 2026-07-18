using System.Collections.Generic;
using UnityEngine;

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
        ATTACK = 2,
        DEAD = 3
    }

    [Header("Eyeball Fly Movement")]
    [SerializeField] private float hoverAmplitude = 0.12f;
    [SerializeField] private float hoverFrequency = 2f;

    [Header("Attack")]
    [SerializeField] private int attackDamage = 1;
    [SerializeField] private float attackInterval = 1f;
    [SerializeField] private float attackDuration = 0.5f;
    [SerializeField] private bool attackPlayer = true;
    [SerializeField] private bool attackLight = true;
    [SerializeField] private bool attackObjects = true;
    [SerializeField] private bool canAttackHitReceivers = true;
    [SerializeField] private float objectAttackRange = 0.5f;
    [SerializeField] private LayerMask objectAttackLayerMask = ~0;
    [SerializeField] private bool debugAttackHit = true;

    [Header("Eyeball Fly State")]
    [SerializeField] private EyeballFlyState currentState = EyeballFlyState.IDLE;

    [Header("References")]
    [SerializeField] private EyeballFlyAnimationController animationController;
    [SerializeField] private Animator animator;

    private float hoverPhase;
    private float nextAttackTime;
    private float attackEndTime;
    private float nextAnimatorDebugLogTime;
    private bool attackInProgress;
    private bool dead;

    public EyeballFlyState CurrentState => currentState;

    protected override void Awake()
    {
        ApplyEyeballMovementDefaults();
        base.Awake();
        CacheEyeballReferences();
        InitializeHover();
        ApplyAnimatorState();
    }

    protected override void OnEnable()
    {
        ApplyEyeballMovementDefaults();
        base.OnEnable();
        CacheEyeballReferences();
        InitializeHover();
        ApplyAnimatorState();
    }

    protected override void Start()
    {
        base.Start();
        CacheEyeballReferences();
        LogDebug($"Start executed. AnimatorFound={animator != null}, AnimationControllerFound={animationController != null}");
    }

    protected override void OnValidate()
    {
        ApplyEyeballMovementDefaults();
        base.OnValidate();
        hoverAmplitude = Mathf.Max(0f, hoverAmplitude);
        hoverFrequency = Mathf.Max(0f, hoverFrequency);
        attackDamage = Mathf.Max(0, attackDamage);
        attackInterval = Mathf.Max(0.05f, attackInterval);
        attackDuration = Mathf.Max(0.01f, attackDuration);
        objectAttackRange = Mathf.Max(0f, objectAttackRange);
        CacheEyeballReferences();
    }

    private void ApplyEyeballMovementDefaults()
    {
        movementType = MonsterMovementType.Flying;
        useGravityForGround = false;
        detectPlayer = true;
        detectLight = true;
        canDetectLight = true;
        returnHomeWhenTargetLost = true;
    }

    protected override void Update()
    {
        if (dead || currentState == EyeballFlyState.DEAD)
        {
            return;
        }

        base.Update();
        UpdateAttackProgress();
        UpdateState();

        if (currentState == EyeballFlyState.ATTACK)
        {
            AttackTarget();
        }
    }

    protected override void UpdateTargetSelection()
    {
        Transform previousTarget = currentTarget;
        MonsterTargetType previousTargetType = currentTargetType;

        // Preserve automatic target lookup, then enforce EyeballFly's strict LOS result.
        base.UpdateTargetSelection();

        bool detectionEnabled = monsterDetection == null || monsterDetection.enableDetection;
        bool playerDetected = detectionEnabled && detectPlayer &&
            IsStrictTargetDetected(playerTarget, playerDetectRange, false, out _);
        bool playerKept = detectionEnabled && detectPlayer &&
            previousTargetType == MonsterTargetType.Player && previousTarget != null &&
            IsStrictTargetDetected(previousTarget, targetKeepRange, false, out _);
        bool lightDetected = detectionEnabled && detectLight && canDetectLight && IsLightAvailable(lightTarget) &&
            IsStrictTargetDetected(lightTarget, lightDetectRange, true, out _);
        bool lightKept = detectionEnabled && detectLight && canDetectLight &&
            previousTargetType == MonsterTargetType.Light && previousTarget != null && IsLightAvailable(previousTarget) &&
            IsStrictTargetDetected(previousTarget, targetKeepRange, true, out _);

        if (playerDetected)
        {
            currentTarget = playerTarget;
            currentTargetType = MonsterTargetType.Player;
            isReturningHome = false;
            return;
        }

        if (playerKept)
        {
            currentTarget = previousTarget;
            currentTargetType = MonsterTargetType.Player;
            isReturningHome = false;
            return;
        }

        if (lightDetected)
        {
            currentTarget = lightTarget;
            currentTargetType = MonsterTargetType.Light;
            isReturningHome = false;
            return;
        }

        if (lightKept)
        {
            currentTarget = previousTarget;
            currentTargetType = MonsterTargetType.Light;
            isReturningHome = false;
            return;
        }

        currentTarget = null;
        currentTargetType = returnHomeWhenTargetLost ? MonsterTargetType.Home : MonsterTargetType.None;
        Vector3 homeDelta = homePosition - moveAnchorPosition;
        homeDelta.z = 0f;
        isReturningHome = returnHomeWhenTargetLost && homeDelta.sqrMagnitude > 0.0001f;
    }

    protected override void FixedUpdate()
    {
        if (dead || currentState == EyeballFlyState.DEAD)
        {
            return;
        }

        base.FixedUpdate();
    }

    public void Die()
    {
        if (dead)
        {
            return;
        }

        dead = true;
        currentTarget = null;
        currentTargetType = MonsterTargetType.None;
        ChangeState(EyeballFlyState.DEAD);
    }

    public override void ResetMonster()
    {
        dead = false;
        nextAttackTime = 0f;
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
        attackDuration = Mathf.Max(0.01f, duration);
        objectAttackLayerMask = targetLayerMask;
        canAttackHitReceivers = hitReceivers;
        attackPlayer = playerEnabled;
        attackLight = lightEnabled;
        attackObjects = objectsEnabled;
        debugAttackHit = attackDebug;
    }

    protected override Vector3 GetMovementOffset()
    {
        float bob = Mathf.Sin(Time.time * hoverFrequency + hoverPhase) * hoverAmplitude;
        return new Vector3(0f, bob, 0f);
    }

    protected override void UpdateBaseMovement(float deltaTime)
    {
        if (currentState != EyeballFlyState.MOVE)
        {
            lastMoveDirection = Vector3.zero;
            ApplyPosition(moveAnchorPosition + GetMovementOffset());
            return;
        }

        base.UpdateBaseMovement(deltaTime);
    }

    private void CacheEyeballReferences()
    {
        if (animationController == null)
        {
            animationController = GetComponentInChildren<EyeballFlyAnimationController>(true);
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        if (monsterAttack != null)
        {
            attackDamage = monsterAttack.attackDamage;
            attackInterval = monsterAttack.attackInterval;
            attackRange = monsterAttack.attackRange;
            objectAttackRange = objectAttackRange > 0f ? objectAttackRange : monsterAttack.attackRange;
        }
    }

    private void InitializeHover()
    {
        hoverPhase = Random.value * Mathf.PI * 2f;
    }

    private void UpdateState()
    {
        bool hasVisibleTarget = HasVisibleSelectedTarget();
        bool hasAttackableObject = HasAttackableObjectInRange();

        if ((hasVisibleTarget && IsTargetInAttackRange) || hasAttackableObject || attackInProgress)
        {
            ChangeState(EyeballFlyState.ATTACK);
            return;
        }

        if ((HasTarget && hasVisibleTarget) || IsReturningHome)
        {
            ChangeState(EyeballFlyState.MOVE);
            return;
        }

        ChangeState(EyeballFlyState.IDLE);
    }

    private void AttackTarget()
    {
        bool hasVisibleTarget = HasVisibleSelectedTarget();
        bool hasAttackableObject = HasAttackableObjectInRange();

        if (!hasVisibleTarget && !hasAttackableObject)
        {
            if (!attackInProgress)
            {
                ResetAttackAnimationState();
            }

            LogDebug("Attack cancelled because no visible target or attackable object is in range.");
            return;
        }

        if (monsterAttack != null && !monsterAttack.enableAttack)
        {
            ResetAttackAnimationState();
            LogDebug("Attack disabled by MonsterAttack.");
            return;
        }

        if (attackInProgress)
        {
            return;
        }

        if (Time.time < nextAttackTime)
        {
            return;
        }

        StartAttack();
    }

    private void StartAttack()
    {
        if (attackInProgress)
        {
            return;
        }

        attackInProgress = true;
        attackEndTime = Time.time + attackDuration;
        nextAttackTime = Time.time + attackInterval;
        SetAttackingVisual(true);
        bool attackTriggered = PlayAttackVisual();
        LogAttackHit($"Attack started. TriggerFired={attackTriggered}, TargetType={CurrentTargetType}, InAttackRange={IsTargetInAttackRange}");
        TryApplyPlayerDamage();
        TryApplyObjectHitReceivers();
    }

    private void UpdateAttackProgress()
    {
        if (!attackInProgress || Time.time < attackEndTime)
        {
            return;
        }

        EndAttack();
    }

    private void EndAttack()
    {
        if (!attackInProgress)
        {
            ResetAttackAnimationState();
            return;
        }

        attackInProgress = false;
        ResetAttackAnimationState();
        LogAttackHit("Attack ended.");
        ApplyAnimatorState();
    }

    private void ResetAttackAnimationState()
    {
        SetAttackingVisual(false);
    }

    private bool HasVisibleSelectedTarget()
    {
        if (CurrentTargetType == MonsterTargetType.Player)
        {
            if (!attackPlayer)
            {
                return false;
            }

            return CurrentTarget == playerTarget &&
                IsStrictTargetDetected(playerTarget, Mathf.Max(playerDetectRange, targetKeepRange), false, out _);
        }

        if (CurrentTargetType == MonsterTargetType.Light)
        {
            if (!attackLight)
            {
                return false;
            }

            return CurrentTarget == lightTarget && IsLightAvailable(lightTarget) &&
                IsStrictTargetDetected(lightTarget, Mathf.Max(lightDetectRange, targetKeepRange), true, out _);
        }

        return false;
    }

    private bool IsStrictTargetDetected(
        Transform target,
        float range,
        bool requireEnabledLight,
        out Collider blockingCollider)
    {
        if (monsterDetection != null)
        {
            return monsterDetection.IsTargetDetected(
                transform,
                target,
                range,
                requireEnabledLight,
                out blockingCollider);
        }

        blockingCollider = null;
        return IsTargetDetected(target, range);
    }

    private void TryApplyPlayerDamage()
    {
        if (CurrentTargetType == MonsterTargetType.Light)
        {
            LogAttackHit("Light attack visual played. Damage skipped.");
            return;
        }

        if (!attackPlayer)
        {
            LogAttackHit("Player attack disabled. Damage skipped.");
            return;
        }

        if (CurrentTargetType != MonsterTargetType.Player || !IsTargetInAttackRange || playerTarget == null)
        {
            LogDebug("Player is not in attack range. Damage skipped.");
            return;
        }

        if (!TryDamageTarget(playerTarget, attackDamage))
        {
            LogDebug("No IDamageable found on Player.");
            return;
        }
    }

    private void ChangeState(EyeballFlyState nextState)
    {
        if (currentState == nextState)
        {
            ApplyAnimatorState();
            return;
        }

        currentState = nextState;
        LogDebug($"State changed to {currentState}");
        ApplyAnimatorState();
    }

    private void ApplyAnimatorState()
    {
        if (animationController == null && animator == null)
        {
            LogDebug("Animator update skipped. EyeballFlyAnimationController and Animator references are missing.");
            return;
        }

        bool isDeadState = currentState == EyeballFlyState.DEAD;
        bool isMoving = currentState == EyeballFlyState.MOVE
            && (!setMoveAnimatorOnlyWhenMoving || IsMoving || IsReturningHome)
            && !isDeadState;
        bool isAttacking = attackInProgress && !isDeadState;

        bool movingSet = SetMovingVisual(isMoving);
        bool attackingSet = SetAttackingVisual(isAttacking);
        bool deadSet = isDeadState && SetDeadVisual(true);

        LogAnimatorUpdate(movingSet, attackingSet, deadSet);
    }

    private bool SetMovingVisual(bool value)
    {
        bool wrapperSet = animationController != null && animationController.SetMovingVisual(value);
        if (!wrapperSet && monsterAnimatorBridge != null && monsterAnimatorBridge.enableAnimatorBridge)
        {
            monsterAnimatorBridge.SetMoving(value);
            return true;
        }

        bool directSet = !wrapperSet && SetAnimatorBool(IsMovingHash, "IsMoving", value);
        return wrapperSet || directSet;
    }

    private bool SetAttackingVisual(bool value)
    {
        bool wrapperSet = animationController != null && animationController.SetAttackingVisual(value);
        if (!wrapperSet && monsterAnimatorBridge != null && monsterAnimatorBridge.enableAnimatorBridge)
        {
            monsterAnimatorBridge.SetAttacking(value);
            return true;
        }

        bool directSet = !wrapperSet && SetAnimatorBool(IsAttackingHash, "IsAttacking", value);
        return wrapperSet || directSet;
    }

    private bool SetDeadVisual(bool value)
    {
        bool wrapperSet = animationController != null && animationController.SetDeadVisual(value);
        if (!wrapperSet && monsterAnimatorBridge != null && monsterAnimatorBridge.enableAnimatorBridge)
        {
            monsterAnimatorBridge.SetDead(value);
            return true;
        }

        bool directSet = !wrapperSet && SetAnimatorBool(IsDeadHash, "IsDead", value);
        return wrapperSet || directSet;
    }

    private bool PlayAttackVisual()
    {
        bool wrapperSet = animationController != null && animationController.PlayAttack();
        if (!wrapperSet && monsterAnimatorBridge != null && monsterAnimatorBridge.enableAnimatorBridge)
        {
            monsterAnimatorBridge.TriggerAttack();
            return true;
        }

        bool directSet = !wrapperSet && SetAnimatorTrigger(AttackHash, "Attack");
        return wrapperSet || directSet;
    }

    private bool HasAttackableObjectInRange()
    {
        return attackObjects && canAttackHitReceivers && FindAttackableHitReceivers(false) > 0;
    }

    private int TryApplyObjectHitReceivers()
    {
        if (!attackObjects || !canAttackHitReceivers)
        {
            return 0;
        }

        return FindAttackableHitReceivers(true);
    }

    private int FindAttackableHitReceivers(bool applyHit)
    {
        if (objectAttackRange <= 0f)
        {
            return 0;
        }

        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            objectAttackRange,
            objectAttackLayerMask,
            QueryTriggerInteraction.Collide);

        if (hits == null || hits.Length == 0)
        {
            if (applyHit)
            {
                LogAttackHit("No HitReceiver found in object attack range.");
            }

            return 0;
        }

        int hitCount = 0;
        HashSet<int> registeredReceivers = applyHit ? new HashSet<int>() : null;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i];
            if (hitCollider == null || hitCollider.transform.IsChildOf(transform))
            {
                continue;
            }

            HitReceiver hitReceiver = FindHitReceiver(hitCollider.transform);
            if (hitReceiver == null || !hitReceiver.CanAcceptHitSource(HitSourceType.EyeballFlyAttack))
            {
                continue;
            }

            if (!applyHit)
            {
                return 1;
            }

            int receiverId = hitReceiver.GetInstanceID();
            if (!registeredReceivers.Add(receiverId))
            {
                continue;
            }

            Vector3 hitPoint = hitCollider.ClosestPoint(transform.position);
            Vector3 hitDirection = (hitReceiver.transform.position - transform.position).normalized;
            DamageInfo damageInfo = new DamageInfo(
                Mathf.Max(1, attackDamage),
                gameObject,
                gameObject,
                hitPoint,
                hitDirection,
                DamageType.MonsterAttack,
                HitSourceType.EyeballFlyAttack);

            LogAttackHit($"HitReceiver found: {hitReceiver.name}");
            hitReceiver.RegisterHit(damageInfo);
            LogAttackHit($"RegisterHit sent to {hitReceiver.name}. Source: {damageInfo.hitSourceType}");
            hitCount++;
        }

        if (applyHit && hitCount == 0)
        {
            LogAttackHit("No targetable HitReceiver found in object attack range.");
        }

        return hitCount;
    }

    private HitReceiver FindHitReceiver(Transform target)
    {
        if (target == null)
        {
            return null;
        }

        HitReceiver hitReceiver = target.GetComponent<HitReceiver>();
        if (hitReceiver != null)
        {
            return hitReceiver;
        }

        hitReceiver = target.GetComponentInParent<HitReceiver>();
        if (hitReceiver != null)
        {
            return hitReceiver;
        }

        return target.GetComponentInChildren<HitReceiver>(true);
    }

    private bool SetAnimatorBool(int parameterHash, string parameterName, bool value)
    {
        return SetAnimatorBoolIfExists(animator, parameterHash, value);
    }

    private bool SetAnimatorTrigger(int parameterHash, string parameterName)
    {
        if (!TriggerAnimatorIfExists(animator, parameterHash))
        {
            LogDebug($"Animator trigger skipped. Missing Animator or parameter: {parameterName}");
            return false;
        }

        return true;
    }

    private void LogAnimatorUpdate(bool movingSet, bool attackingSet, bool deadSet)
    {
        if (!debugMode || Time.time < nextAnimatorDebugLogTime)
        {
            return;
        }

        nextAnimatorDebugLogTime = Time.time + 0.5f;
        string targetName = CurrentTarget != null ? CurrentTarget.name : "None";
        float distance = CurrentTarget != null ? GetPlanarDistance(CurrentTarget) : -1f;
        Debug.Log(
            $"[EyeballFlyAI] Animator State={currentState}, Target={targetName}, Distance={distance:0.00}, " +
            $"IsMovingSet={movingSet}, IsAttackingSet={attackingSet}, IsDeadSet={deadSet}",
            this);
    }

    private void LogAttackHit(string message)
    {
        if (debugMode || debugAttackHit)
        {
            Debug.Log($"[EyeballFlyAttack] {message}", this);
        }
    }

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        if (!showGizmos || !attackObjects || objectAttackRange <= 0f)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.45f, 0.05f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, objectAttackRange);
    }
}
