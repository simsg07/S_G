using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(MonsterHealth))]
public sealed class HumanBoxAI : MonsterAIBase
{
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int IsAttackingHash = Animator.StringToHash("IsAttacking");
    private static readonly int IsDeadHash = Animator.StringToHash("IsDead");
    private static readonly int IsHowlingHash = Animator.StringToHash("IsHowling");
    private static readonly int IsAttackFalseHash = Animator.StringToHash("IsAttackFalse");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int HowlingHash = Animator.StringToHash("Howling");
    private static readonly int AttackFalseHash = Animator.StringToHash("AttackFalse");
    private static readonly int StateHash = Animator.StringToHash("State");

    [Header("Detection")]
    [SerializeField, Min(0f)] private float detectRange = 3f;
    [SerializeField, Min(0f)] private float chaseRange = 5f;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float testMoveSpeed = 1f;
    [SerializeField] private bool useTestMoveSpeed = true;
    [SerializeField, Min(0f)] private float stopDistance = 0.1f;
    [SerializeField] private bool lockZPosition = true;

    [Header("Howling")]
    [SerializeField] private bool enableHowl = true;
    [SerializeField, Min(0f)] private float howlDuration = 1.5f;
    [SerializeField] private bool howlOnlyOncePerDetection = true;

    [Header("Attack")]
    [FormerlySerializedAs("attackWindup")]
    [SerializeField, Min(0f)] private float attackReadyTime = 0.8f;
    [SerializeField, Min(0f)] private float attackHitWindow = 0.2f;
    [SerializeField, Min(0)] private int attackDamage = 1;
    [SerializeField, Min(0f)] private float attackCooldown = 1f;

    [Header("Stun")]
    [SerializeField, Min(0f)] private float attackFalseStunDuration = 1f;

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private MonsterHealth health;
    [SerializeField] private HumanBoxHowling howling;
    [SerializeField] private HumanBoxDeadPlatform3D deadPlatform;
    [SerializeField] private Transform attackHitboxTransform;
    [SerializeField] private bool facePlayerWhenDetected = true;

    [Header("Debug")]
    [SerializeField] private bool showDetectionDebug = true;
    [SerializeField] private bool logDetectionEvents;
    [SerializeField] private HumanBoxState currentState = HumanBoxState.IDLE;

    private float stateEndTime;
    private float nextAttackTime;
    private bool hasUsedHowling;
    private bool attackHitPlayer;
    private float lockedAttackDirection = 1f;
    private MonsterPatrolController patrolController;

    private float ActiveMoveSpeed => useTestMoveSpeed ? testMoveSpeed : moveSpeed;
    public HumanBoxState CurrentState => currentState;
    public float PlayerDetectionRange => monsterDetection != null ? monsterDetection.PlayerDetectionRange : detectRange;

    public void ConfigureDataDrivenStats(int configuredMaxHp, float configuredHowlDuration,
        float legacyHowlStunDuration, bool configuredEnableHowl)
    {
        enableHowl = configuredEnableHowl;
        howlDuration = configuredEnableHowl ? Mathf.Max(0f, configuredHowlDuration) : 0f;
        if (health != null)
        {
            health.maxHp = Mathf.Max(1, configuredMaxHp);
            health.ResetHealth();
        }
        if (howling != null)
        {
            howling.enableHowling = configuredEnableHowl;
            howling.howlingDuration = howlDuration;
        }
    }

    protected override void Awake()
    {
        ApplyGroundDefaults();
        base.Awake();
        CacheReferences();
        SyncComponentSettings();
        currentState = HumanBoxState.IDLE;
        hasUsedHowling = false;
        deadPlatform?.RestoreAlive();
        ApplyAnimatorState();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        CacheReferences();
        if (health != null) health.Died += HandleDied;
    }

    private void OnDisable()
    {
        if (health != null) health.Died -= HandleDied;
        deadPlatform?.SetAttackHitbox(false);
    }

    protected override void OnValidate()
    {
        ApplyGroundDefaults();
        base.OnValidate();
        if (monsterDetection == null) monsterDetection = GetComponent<MonsterDetection>();
        detectRange = Mathf.Max(0f, detectRange);
        chaseRange = Mathf.Max(detectRange, chaseRange);
        testMoveSpeed = Mathf.Max(0f, testMoveSpeed);
        stopDistance = Mathf.Max(0f, stopDistance);
        howlDuration = Mathf.Max(0f, howlDuration);
        attackReadyTime = Mathf.Max(0f, attackReadyTime);
        attackHitWindow = Mathf.Max(0.01f, attackHitWindow);
        attackDamage = Mathf.Max(0, attackDamage);
        attackCooldown = Mathf.Max(0f, attackCooldown);
        attackFalseStunDuration = Mathf.Max(0f, attackFalseStunDuration);
    }

    protected override void Update()
    {
        if (currentState == HumanBoxState.DEAD_PLATFORM) return;
        SyncComponentSettings();
        base.Update();
        if (lockZPosition) ClampToFixedZ();

        switch (currentState)
        {
            case HumanBoxState.IDLE: UpdateIdle(); break;
            case HumanBoxState.HOWLING: UpdateHowling(); break;
            case HumanBoxState.WALK: UpdateWalk(); break;
            case HumanBoxState.ATTACK_READY: UpdateAttackReady(); break;
            case HumanBoxState.ATTACK: UpdateAttack(); break;
            case HumanBoxState.STUN: UpdateStun(); break;
        }
    }

    protected override void FixedUpdate()
    {
        if (currentState == HumanBoxState.DEAD_PLATFORM) return;
        if (IsWorldPhysicsSuspended) return;
        UpdateGroundCheck();
        if (currentState == HumanBoxState.WALK)
        {
            MoveTowardPlayer(Time.fixedDeltaTime);
            SetMoving(IsMoving);
        }
        else
        {
            StopHorizontalMovement();
            SetMoving(false);
        }
        MaintainGroundExternalPushControl();
    }

    private void UpdateIdle()
    {
        SetMoving(false);
        if (!CanDetectPlayer(PlayerDetectionRange)) return;
        patrolController?.PauseForCombat();
        bool repeatHowling = howling != null && !howling.howlingOncePerLife;
        if (enableHowl && (!hasUsedHowling || repeatHowling)) ChangeState(HumanBoxState.HOWLING);
        else ChangeState(HumanBoxState.WALK);
    }

    private void UpdateHowling()
    {
        if (Time.time < stateEndTime) return;
        ChangeState(CanDetectPlayer(chaseRange) ? HumanBoxState.WALK : HumanBoxState.IDLE);
    }

    private void UpdateWalk()
    {
        FacePlayer();
        if (!CanDetectPlayer(chaseRange))
        {
            patrolController?.ResumeAfterCombat(GetCurrentPosition());
            ChangeState(HumanBoxState.IDLE);
            return;
        }
        if (Time.time >= nextAttackTime && IsInRange(playerTarget, attackRange))
            ChangeState(HumanBoxState.ATTACK_READY);
    }

    private void UpdateAttackReady()
    {
        if (!CanDetectPlayer(chaseRange))
        {
            ChangeState(HumanBoxState.IDLE);
            return;
        }
        if (Time.time >= stateEndTime) ChangeState(HumanBoxState.ATTACK);
    }

    private void UpdateAttack()
    {
        if (Time.time < stateEndTime) return;
        deadPlatform?.SetAttackHitbox(false);
        nextAttackTime = Time.time + attackCooldown;
        ChangeState(attackHitPlayer ? HumanBoxState.WALK : HumanBoxState.STUN);
    }

    private void UpdateStun()
    {
        if (Time.time < stateEndTime) return;
        ChangeState(CanDetectPlayer(chaseRange) ? HumanBoxState.WALK : HumanBoxState.IDLE);
    }

    public void TryRegisterAttackHit(Collider other)
    {
        if (!MonsterWorldSimulationGate3D.AllowsPlayerInteraction(this)) return;
        if (currentState != HumanBoxState.ATTACK || attackHitPlayer || other == null) return;
        Transform player = ResolvePlayerTransform(other.transform);
        if (player == null) return;
        IDamageable damageable = player.GetComponent<IDamageable>()
            ?? player.GetComponentInParent<IDamageable>()
            ?? player.GetComponentInChildren<IDamageable>();
        if (damageable == null || !damageable.CanTakeDamage) return;
        damageable.TakeDamage(attackDamage);
        attackHitPlayer = true;
    }

    private void ChangeState(HumanBoxState next)
    {
        if (currentState == next) return;
        currentState = next;
        deadPlatform?.SetAttackHitbox(false);
        SetMoving(false);
        SetAttacking(false);
        SetHowling(false);
        SetAttackFalse(false);

        switch (next)
        {
            case HumanBoxState.HOWLING:
                hasUsedHowling = true;
                stateEndTime = Time.time + howlDuration;
                SetHowling(true);
                Trigger(HowlingHash, "Howling");
                howling?.BeginHowling(gameObject, playerTarget);
                if (logDetectionEvents) Debug.Log("[Human_Box] Howling puzzle signal emitted.", this);
                break;
            case HumanBoxState.WALK:
                SetMoving(true);
                break;
            case HumanBoxState.ATTACK_READY:
                lockedAttackDirection = playerTarget != null && playerTarget.position.x < transform.position.x ? -1f : 1f;
                stateEndTime = Time.time + attackReadyTime;
                FaceLockedDirection();
                break;
            case HumanBoxState.ATTACK:
                attackHitPlayer = false;
                stateEndTime = Time.time + attackHitWindow;
                PositionAttackHitbox();
                SetAttacking(true);
                Trigger(AttackHash, "Attack");
                deadPlatform?.SetAttackHitbox(true);
                break;
            case HumanBoxState.STUN:
                stateEndTime = Time.time + attackFalseStunDuration;
                SetAttackFalse(true);
                Trigger(AttackFalseHash, "AttackFalse");
                break;
            case HumanBoxState.DEAD_PLATFORM:
                SetBool(IsDeadHash, true);
                deadPlatform?.EnterDeadPlatform();
                break;
        }

        int animatorState = next == HumanBoxState.ATTACK_READY ? (int)HumanBoxState.IDLE : (int)next;
        SetInt(StateHash, animatorState);
    }

    private void HandleDied(MonsterHealth _)
    {
        playerTarget = null;
        currentTarget = null;
        currentTargetType = MonsterTargetType.None;
        ChangeState(HumanBoxState.DEAD_PLATFORM);
    }

    private bool CanDetectPlayer(float range)
    {
        if (!MonsterWorldSimulationGate3D.AllowsPlayerInteraction(this)) return false;
        if (playerTarget == null || currentState == HumanBoxState.DEAD_PLATFORM || !IsPlayerAlive()) return false;
        if (monsterDetection != null && (!monsterDetection.enableDetection || !monsterDetection.canDetectPlayer)) return false;
        return IsInRange(playerTarget, range) && (!requireLineOfSight || IsPlayerVisible());
    }

    private bool IsPlayerAlive()
    {
        if (playerTarget == null || !playerTarget.gameObject.activeInHierarchy) return false;
        IDamageable damageable = playerTarget.GetComponent<IDamageable>()
            ?? playerTarget.GetComponentInParent<IDamageable>()
            ?? playerTarget.GetComponentInChildren<IDamageable>();
        return damageable == null || damageable.CanTakeDamage;
    }

    private Transform ResolvePlayerTransform(Transform candidate)
    {
        if (playerTarget == null || candidate == null) return null;
        if (candidate == playerTarget || candidate.IsChildOf(playerTarget) || playerTarget.IsChildOf(candidate)) return playerTarget;
        return null;
    }

    private void MoveTowardPlayer(float deltaTime)
    {
        if (playerTarget == null) return;
        Vector3 delta = playerTarget.position - GetCurrentPosition();
        delta.z = 0f;
        if (Mathf.Abs(delta.x) <= stopDistance) return;
        MoveTowardPosition(ProjectToFixedZ(playerTarget.position), ActiveMoveSpeed, deltaTime);
    }

    private void StopHorizontalMovement()
    {
        if (body == null || IsWorldPhysicsSuspended || body.isKinematic) return;
        Vector3 velocity = body.linearVelocity;
        velocity.x = 0f;
        body.linearVelocity = velocity;
    }

    private void FacePlayer()
    {
        if (facePlayerWhenDetected && playerTarget != null) FaceTargetIfNeeded(playerTarget.position);
    }

    private void FaceLockedDirection()
    {
        FaceTargetIfNeeded(transform.position + Vector3.right * lockedAttackDirection);
    }

    private void PositionAttackHitbox()
    {
        if (attackHitboxTransform == null) return;
        Vector3 local = attackHitboxTransform.localPosition;
        local.x = Mathf.Abs(local.x) * lockedAttackDirection;
        attackHitboxTransform.localPosition = local;
    }

    private void CacheReferences()
    {
        if (health == null) health = GetComponent<MonsterHealth>();
        if (howling == null) howling = GetComponent<HumanBoxHowling>();
        if (deadPlatform == null) deadPlatform = GetComponent<HumanBoxDeadPlatform3D>();
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        if (patrolController == null) patrolController = GetComponent<MonsterPatrolController>();
        if (attackHitboxTransform == null && deadPlatform != null && deadPlatform.AttackCollider != null)
            attackHitboxTransform = deadPlatform.AttackCollider.transform;
    }

    private void SyncComponentSettings()
    {
        if (monsterDetection != null)
        {
            detectRange = monsterDetection.playerDetectRange;
            chaseRange = monsterDetection.chaseRange;
        }
        if (monsterMovement != null)
        {
            testMoveSpeed = monsterMovement.testMoveSpeed;
            useTestMoveSpeed = monsterMovement.useTestMoveSpeed;
            moveSpeed = monsterMovement.moveSpeed;
            stopDistance = monsterMovement.stopDistance;
            lockZPosition = monsterMovement.lockZPosition;
        }
        if (monsterAttack != null)
        {
            attackRange = monsterAttack.attackRange;
            attackDamage = monsterAttack.attackDamage;
            attackReadyTime = monsterAttack.attackWindup;
            attackCooldown = monsterAttack.attackCooldown;
        }
        if (howling != null)
        {
            enableHowl = howling.enableHowling;
            howlDuration = howling.howlingDuration;
            howlOnlyOncePerDetection = howling.howlingOncePerLife;
        }
    }

    private void ApplyGroundDefaults()
    {
        movementType = MonsterMovementType.Ground;
        useGravityForGround = true;
        groundOnlyMoveX = true;
        returnHomeWhenTargetLost = false;
        detectPlayer = true;
        detectLight = false;
        canDetectLight = false;
    }

    private void ApplyAnimatorState()
    {
        SetMoving(false);
        SetAttacking(false);
        SetHowling(false);
        SetAttackFalse(false);
        SetBool(IsDeadHash, false);
        SetInt(StateHash, (int)HumanBoxState.IDLE);
    }

    private void SetMoving(bool value)
    {
        if (monsterAnimatorBridge != null && monsterAnimatorBridge.enableAnimatorBridge) monsterAnimatorBridge.SetMoving(value);
        else SetAnimatorBoolIfExists(animator, IsMovingHash, value);
    }
    private void SetAttacking(bool value)
    {
        if (monsterAnimatorBridge != null && monsterAnimatorBridge.enableAnimatorBridge) monsterAnimatorBridge.SetAttacking(value);
        else SetAnimatorBoolIfExists(animator, IsAttackingHash, value);
    }
    private void SetHowling(bool value)
    {
        if (monsterAnimatorBridge != null && monsterAnimatorBridge.enableAnimatorBridge) monsterAnimatorBridge.SetHowling(value);
        else SetAnimatorBoolIfExists(animator, IsHowlingHash, value);
    }
    private void SetAttackFalse(bool value)
    {
        if (monsterAnimatorBridge != null && monsterAnimatorBridge.enableAnimatorBridge) monsterAnimatorBridge.SetAttackFalse(value);
        else SetAnimatorBoolIfExists(animator, IsAttackFalseHash, value);
    }
    private void SetBool(int hash, bool value) => SetAnimatorBoolIfExists(animator, hash, value);
    private void SetInt(int hash, int value)
    {
        if (monsterAnimatorBridge != null && monsterAnimatorBridge.enableAnimatorBridge) monsterAnimatorBridge.SetState(value);
        else SetAnimatorIntIfExists(animator, hash, value);
    }
    private void Trigger(int hash, string name)
    {
        if (monsterAnimatorBridge != null && monsterAnimatorBridge.enableAnimatorBridge)
        {
            if (name == "Attack") monsterAnimatorBridge.TriggerAttack();
            else if (name == "Howling") monsterAnimatorBridge.TriggerHowling();
            else monsterAnimatorBridge.TriggerAttackFalse();
        }
        else TriggerAnimatorIfExists(animator, hash);
    }

    protected override void OnDrawGizmos()
    {
        if (!showGizmos || !showDetectionDebug) return;
        Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
