using UnityEngine;

[DisallowMultipleComponent]
public class HumanBoxAI : MonsterAIBase, IDamageable
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

    [Header("Stats")]
    [SerializeField] private int maxHp = 4;
    [SerializeField] private int currentHp = 4;

    [Header("Detection")]
    [SerializeField] private float detectRange = 3f;
    [SerializeField] private float chaseRange = 5f;

    [Header("Movement")]
    [SerializeField] private float testMoveSpeed = 1f;
    [SerializeField] private bool useTestMoveSpeed = true;
    [SerializeField] private float stopDistance = 0.1f;
    [SerializeField] private bool lockZPosition = true;

    [Header("Howling")]
    [SerializeField] private float howlStunDuration = 1.5f;
    [SerializeField] private float howlDuration = 1f;
    [SerializeField] private bool howlOnlyOncePerDetection = true;

    [Header("Attack")]
    [SerializeField] private float attackWindup = 0.5f;
    [SerializeField] private int attackDamage = 1;
    [SerializeField] private float attackCooldown = 1f;
    [SerializeField] private float attackFalseStunDuration = 1f;

    [Header("Death")]
    [SerializeField] private bool disableColliderOnDeath = true;
    [SerializeField] private bool destroyOnDeath;
    [SerializeField] private float destroyDelay = 2f;

    [Header("Visual / Animator")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private bool facePlayerWhenDetected = true;
    [SerializeField] private bool flipVisualByScale = true;
    [SerializeField] private bool facingRight = true;

    [Header("Debug")]
    [SerializeField] private float logInterval = 0.5f;

    [SerializeField] private HumanBoxState currentState = HumanBoxState.Idle;
    private Collider[] colliders;
    private float stateEndTime;
    private float nextAttackTime;
    private float lastSeenTime = -999f;
    private float nextLogTime;
    private float nextDetectFailureLogTime;
    private bool warnedPlayerTargetMissing;
    private bool hasHowledThisDetection;
    private bool attackDamageApplied;
    private bool lastLineOfSight;
    private Vector3 lastRayOrigin;
    private Vector3 lastRayEnd;

    private float ActiveMoveSpeed => useTestMoveSpeed ? testMoveSpeed : moveSpeed;

    protected override void Awake()
    {
        ApplyHumanBoxMovementDefaults();
        base.Awake();
        SyncBaseSettings();
        CacheReferences();
        EnsurePlayerTarget();
        currentHp = Mathf.Clamp(currentHp, 0, maxHp);
        ApplyAnimatorState();
    }

    protected override void OnValidate()
    {
        ApplyHumanBoxMovementDefaults();
        base.OnValidate();
        SyncBaseSettings();
        maxHp = Mathf.Max(1, maxHp);
        currentHp = Mathf.Clamp(currentHp, 0, maxHp);
        detectRange = Mathf.Max(0f, detectRange);
        chaseRange = Mathf.Max(0f, chaseRange);
        attackRange = Mathf.Max(0f, attackRange);
        moveSpeed = Mathf.Max(0f, moveSpeed);
        testMoveSpeed = Mathf.Max(0f, testMoveSpeed);
        stopDistance = Mathf.Max(0f, stopDistance);
        lostSightDelay = Mathf.Max(0f, lostSightDelay);
        howlStunDuration = Mathf.Max(0f, howlStunDuration);
        howlDuration = Mathf.Max(0f, howlDuration);
        attackWindup = Mathf.Max(0f, attackWindup);
        attackDamage = Mathf.Max(0, attackDamage);
        attackCooldown = Mathf.Max(0f, attackCooldown);
        attackFalseStunDuration = Mathf.Max(0f, attackFalseStunDuration);
        destroyDelay = Mathf.Max(0f, destroyDelay);
        logInterval = Mathf.Max(0.05f, logInterval);
    }

    protected override void Update()
    {
        if (currentState == HumanBoxState.Dead)
        {
            return;
        }

        SyncBaseSettings();
        EnsurePlayerTarget();
        base.Update();
        if (lockZPosition)
        {
            ClampToFixedZ();
        }

        UpdateLineOfSightMemory();

        switch (currentState)
        {
            case HumanBoxState.Idle:
                UpdateIdle();
                break;
            case HumanBoxState.Howling:
                UpdateHowling();
                break;
            case HumanBoxState.Walk:
                UpdateWalk();
                break;
            case HumanBoxState.Attack:
                UpdateAttack();
                break;
            case HumanBoxState.AttackFalse:
                UpdateAttackFalse();
                break;
        }

        LogDebugState();
    }

    protected override void FixedUpdate()
    {
        UpdateGroundCheck();

        if (currentState != HumanBoxState.Walk)
        {
            return;
        }

        MoveTowardPlayer(Time.fixedDeltaTime);
        SetMoving(!setMoveAnimatorOnlyWhenMoving || IsMoving);
    }

    public void TakeDamage(int damage)
    {
        if (currentState == HumanBoxState.Dead || damage <= 0)
        {
            return;
        }

        currentHp = Mathf.Max(0, currentHp - damage);
        LogDebug($"TakeDamage={damage}, HP={currentHp}/{maxHp}");

        if (currentHp <= 0)
        {
            ChangeState(HumanBoxState.Dead);
        }
    }

    private void CacheReferences()
    {
        if (body != null)
        {
            body.constraints &= ~RigidbodyConstraints.FreezePositionX;
            body.constraints &= ~RigidbodyConstraints.FreezePositionY;
            body.constraints |= TwoPointFiveDUtility3D.SideViewRigidbodyConstraints;
        }

        if (colliders == null || colliders.Length == 0)
        {
            colliders = GetComponentsInChildren<Collider>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        if (visualRoot == null)
        {
            if (animator != null)
            {
                visualRoot = animator.transform;
            }
            else
            {
                Renderer renderer = GetComponentInChildren<Renderer>(true);
                visualRoot = renderer != null ? renderer.transform : transform;
            }
        }

        if (facingVisualRoot == null)
        {
            facingVisualRoot = visualRoot;
        }
    }

    private void SyncBaseSettings()
    {
        ApplyHumanBoxMovementDefaults();
        detectPlayer = true;
        detectLight = false;
        canDetectLight = false;
        playerDetectRange = detectRange;
        targetKeepRange = chaseRange;
        returnHomeWhenTargetLost = false;
        setMoveAnimatorOnlyWhenMoving = true;
    }

    private void ApplyHumanBoxMovementDefaults()
    {
        movementType = MonsterMovementType.Ground;
        useGravityForGround = true;
        groundOnlyMoveX = true;
        returnHomeWhenTargetLost = false;
        detectPlayer = true;
        detectLight = false;
        canDetectLight = false;
    }

    private void UpdateIdle()
    {
        SetMoving(false);
        SetAttacking(false);
        SetHowling(false);
        SetAttackFalse(false);

        if (!CanDetectPlayerForHowling())
        {
            hasHowledThisDetection = false;
            return;
        }

        if (!howlOnlyOncePerDetection || !hasHowledThisDetection)
        {
            ChangeState(HumanBoxState.Howling);
            return;
        }

        ChangeState(HumanBoxState.Walk);
    }

    private void UpdateHowling()
    {
        FacePlayer();

        if (Time.time >= stateEndTime)
        {
            ChangeState(IsPlayerInsideChaseRange() && CanSeePlayerNow() ? HumanBoxState.Walk : HumanBoxState.Idle);
        }
    }

    private void UpdateWalk()
    {
        SetMoving(!setMoveAnimatorOnlyWhenMoving || IsMoving);
        FacePlayer();

        if (!IsPlayerInsideChaseRange() || !CanSeePlayerNow())
        {
            ChangeState(HumanBoxState.Idle);
            return;
        }

        if (IsPlayerInsideAttackRange() && IsPlayerVisible() && Time.time >= nextAttackTime)
        {
            ChangeState(HumanBoxState.Attack);
        }
    }

    private void UpdateAttack()
    {
        FacePlayer();

        if (!attackDamageApplied && Time.time >= stateEndTime)
        {
            attackDamageApplied = true;

            if (IsPlayerInsideAttackRange() && IsPlayerVisible())
            {
                ApplyDamageToPlayer();
                nextAttackTime = Time.time + attackCooldown;
                LogDebug("Attack success.");
            }
            else
            {
                LogDebug("Attack failed. Enter AttackFalse.");
                ChangeState(HumanBoxState.AttackFalse);
                return;
            }
        }

        if (attackDamageApplied && Time.time >= nextAttackTime)
        {
            if (IsPlayerInsideAttackRange() && IsPlayerVisible())
            {
                ChangeState(HumanBoxState.Attack);
            }
            else if (IsPlayerInsideChaseRange() && CanSeePlayerNow())
            {
                ChangeState(HumanBoxState.Walk);
            }
            else
            {
                ChangeState(HumanBoxState.Idle);
            }
        }
    }

    private void UpdateAttackFalse()
    {
        if (Time.time < stateEndTime)
        {
            return;
        }

        ChangeState(IsPlayerInsideChaseRange() && CanSeePlayerNow() ? HumanBoxState.Walk : HumanBoxState.Idle);
    }

    private void ChangeState(HumanBoxState nextState)
    {
        if (currentState == nextState)
        {
            return;
        }

        HumanBoxState previousState = currentState;
        currentState = nextState;
        LogDebug($"State changed: {previousState} -> {currentState}");

        switch (currentState)
        {
            case HumanBoxState.Idle:
                SetMoving(false);
                SetAttacking(false);
                SetHowling(false);
                SetAttackFalse(false);
                break;
            case HumanBoxState.Howling:
                hasHowledThisDetection = true;
                stateEndTime = Time.time + howlDuration;
                SetMoving(false);
                SetAttacking(false);
                SetHowling(true);
                Trigger(HowlingHash, "Howling");
                LogDebug("Howling started");
                TryStunPlayer();
                break;
            case HumanBoxState.Walk:
                SetMoving(true);
                SetAttacking(false);
                SetHowling(false);
                SetAttackFalse(false);
                break;
            case HumanBoxState.Attack:
                stateEndTime = Time.time + attackWindup;
                attackDamageApplied = false;
                SetMoving(false);
                SetAttacking(true);
                Trigger(AttackHash, "Attack");
                LogDebug("Attack windup started.");
                break;
            case HumanBoxState.AttackFalse:
                stateEndTime = Time.time + attackFalseStunDuration;
                SetMoving(false);
                SetAttacking(false);
                SetAttackFalse(true);
                Trigger(AttackFalseHash, "AttackFalse");
                LogDebug("AttackFalse entered.");
                break;
            case HumanBoxState.Dead:
                SetMoving(false);
                SetAttacking(false);
                SetHowling(false);
                SetAttackFalse(false);
                SetBool(IsDeadHash, "IsDead", true);
                DisableCollidersIfNeeded();
                if (destroyOnDeath)
                {
                    Destroy(gameObject, destroyDelay);
                }
                LogDebug("Dead entered.");
                break;
        }

        SetInt(StateHash, "State", (int)currentState);
    }

    private void MoveTowardPlayer(float deltaTime)
    {
        if (playerTarget == null)
        {
            return;
        }

        Vector3 targetPosition = playerTarget.position;
        Vector3 currentPosition = GetCurrentPosition();
        Vector3 delta = targetPosition - currentPosition;
        delta.z = 0f;

        if (delta.magnitude <= stopDistance)
        {
            return;
        }

        MoveTowardPosition(ProjectToFixedZ(targetPosition), ActiveMoveSpeed, deltaTime);
    }

    private void FacePlayer()
    {
        if (!facePlayerWhenDetected || playerTarget == null || visualRoot == null)
        {
            return;
        }

        if (!IsPlayerInsideDetectRange() || !CanSeePlayerNow())
        {
            LogDetectFailure("FaceTarget skipped because player is not detected");
            return;
        }

        float xDelta = playerTarget.position.x - transform.position.x;
        if (Mathf.Abs(xDelta) <= 0.001f)
        {
            return;
        }

        bool shouldFaceRight = invertFacing ? xDelta < 0f : xDelta > 0f;
        if (shouldFaceRight == facingRight)
        {
            return;
        }

        facingRight = shouldFaceRight;

        if (flipVisualByScale)
        {
            Vector3 scale = visualRoot.localScale;
            scale.x = Mathf.Abs(scale.x) * (facingRight ? 1f : -1f);
            visualRoot.localScale = scale;
            LogDebug($"Facing targetDirection.x={xDelta:0.###}, shouldFaceRight={shouldFaceRight}, visualScaleX={scale.x:0.###}, invertFacing={invertFacing}");
        }
    }

    private void UpdateLineOfSightMemory()
    {
        lastLineOfSight = CanSeePlayerNow();
        if (lastLineOfSight)
        {
            lastSeenTime = Time.time;
        }
    }

    private bool CanSeePlayerRecently()
    {
        return CanSeePlayerNow() || Time.time - lastSeenTime <= lostSightDelay;
    }

    private bool CanSeePlayerNow()
    {
        if (playerTarget == null)
        {
            return false;
        }

        if (!requireLineOfSight)
        {
            lastRayOrigin = transform.position + lineOfSightStartOffset;
            lastRayEnd = playerTarget.position + targetCheckOffset;
            return true;
        }

        Vector3 origin = transform.position + lineOfSightStartOffset;
        Vector3 target = playerTarget.position + targetCheckOffset;
        origin.z = fixedZPosition;
        target.z = fixedZPosition;
        Vector3 direction = target - origin;
        float distance = direction.magnitude;

        lastRayOrigin = origin;
        lastRayEnd = target;

        if (distance <= 0.001f)
        {
            return true;
        }

        return HasLineOfSightTo(playerTarget);
    }

    private bool IsPlayerInsideDetectRange()
    {
        return IsPlayerInsideRange(detectRange);
    }

    private bool IsPlayerInsideChaseRange()
    {
        return IsPlayerInsideRange(chaseRange);
    }

    private bool IsPlayerInsideAttackRange()
    {
        return IsPlayerInsideRange(attackRange);
    }

    private bool IsPlayerInsideRange(float range)
    {
        return IsInRange(playerTarget, range);
    }

    private float GetPlayerDistance()
    {
        if (playerTarget == null)
        {
            return -1f;
        }

        return GetPlanarDistance(playerTarget);
    }

    private bool CanDetectPlayerForHowling()
    {
        if (currentState == HumanBoxState.Dead)
        {
            LogDetectFailure("Dead state");
            return false;
        }

        if (playerTarget == null)
        {
            LogDetectFailure("No player target");
            return false;
        }

        if (!IsPlayerInsideDetectRange())
        {
            LogDetectFailure("Out of detect range");
            return false;
        }

        if (requireLineOfSight && !IsPlayerVisible())
        {
            LogDetectFailure("Line of sight blocked");
            return false;
        }

        LogDebug("Player detected");
        return true;
    }

    private void LogDetectFailure(string reason)
    {
        if (!debugMode || Time.time < nextDetectFailureLogTime)
        {
            return;
        }

        nextDetectFailureLogTime = Time.time + logInterval;
        LogDebug($"Detect failed: {reason}");
    }

    private void EnsurePlayerTarget()
    {
        if (playerTarget != null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(playerTag))
        {
            try
            {
                GameObject taggedPlayer = GameObject.FindGameObjectWithTag(playerTag);
                if (taggedPlayer != null)
                {
                    playerTarget = taggedPlayer.transform;
                    warnedPlayerTargetMissing = false;
                    LogDebug($"Player target found by tag: {playerTarget.name}");
                    return;
                }
            }
            catch (UnityException)
            {
                LogDebug($"Player tag '{playerTag}' does not exist. Falling back to name/component search.");
            }
        }

        GameObject namedPlayer = GameObject.Find("Player");
        if (namedPlayer != null)
        {
            playerTarget = namedPlayer.transform;
            warnedPlayerTargetMissing = false;
            LogDebug($"Player target found by name: {playerTarget.name}");
            return;
        }

        PlatformerPlayer3D platformerPlayer = FindFirstObjectByType<PlatformerPlayer3D>();
        if (platformerPlayer != null)
        {
            playerTarget = platformerPlayer.transform;
            warnedPlayerTargetMissing = false;
            LogDebug($"Player target found by PlatformerPlayer3D: {playerTarget.name}");
            return;
        }

        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && candidate.name.Contains("Player"))
            {
                playerTarget = candidate;
                warnedPlayerTargetMissing = false;
                LogDebug($"Player target found by partial name: {playerTarget.name}");
                return;
            }
        }

        if (debugMode && !warnedPlayerTargetMissing)
        {
            warnedPlayerTargetMissing = true;
            Debug.LogWarning("[HumanBoxAI] Player target was not found.", this);
        }
    }

    private void TryStunPlayer()
    {
        IStunnable stunnable = FindPlayerComponent<IStunnable>();
        if (stunnable == null)
        {
            Debug.LogWarning("[HumanBoxAI] No IStunnable found on Player", this);
            return;
        }

        LogDebug("IStunnable found");
        stunnable.Stun(howlStunDuration);
        LogDebug($"Player stun requested: {howlStunDuration:0.##}");
        LogDebug($"Player stunned for {howlStunDuration:0.##} seconds");
    }

    private void ApplyDamageToPlayer()
    {
        TryDamageTarget(playerTarget, attackDamage);
    }

    private T FindPlayerComponent<T>() where T : class
    {
        if (playerTarget == null)
        {
            return null;
        }

        T component = playerTarget.GetComponent<T>();
        if (component != null)
        {
            return component;
        }

        component = playerTarget.GetComponentInParent<T>();
        if (component != null)
        {
            return component;
        }

        return playerTarget.GetComponentInChildren<T>();
    }

    private void DisableCollidersIfNeeded()
    {
        if (!disableColliderOnDeath || colliders == null)
        {
            return;
        }

        foreach (Collider targetCollider in colliders)
        {
            if (targetCollider != null)
            {
                targetCollider.enabled = false;
            }
        }
    }

    private void SetMoving(bool value)
    {
        SetBool(IsMovingHash, "IsMoving", value);
    }

    private void SetAttacking(bool value)
    {
        SetBool(IsAttackingHash, "IsAttacking", value);
    }

    private void SetHowling(bool value)
    {
        SetBool(IsHowlingHash, "IsHowling", value);
    }

    private void SetAttackFalse(bool value)
    {
        SetBool(IsAttackFalseHash, "IsAttackFalse", value);
    }

    private void SetBool(int hash, string parameterName, bool value)
    {
        SetAnimatorBoolIfExists(animator, hash, value);
    }

    private void SetInt(int hash, string parameterName, int value)
    {
        SetAnimatorIntIfExists(animator, hash, value);
    }

    private void Trigger(int hash, string parameterName)
    {
        TriggerAnimatorIfExists(animator, hash);
    }

    private void ApplyAnimatorState()
    {
        SetMoving(false);
        SetAttacking(false);
        SetHowling(false);
        SetAttackFalse(false);
        SetBool(IsDeadHash, "IsDead", false);
        SetInt(StateHash, "State", (int)currentState);
    }

    private void LogDebugState()
    {
        if (!debugMode || Time.time < nextLogTime)
        {
            return;
        }

        nextLogTime = Time.time + logInterval;
        string playerName = playerTarget != null ? playerTarget.name : "None";
        float distance = GetPlayerDistance();
        bool inDetectRange = IsPlayerInsideDetectRange();
        bool lineOfSight = !requireLineOfSight || lastLineOfSight;
        Debug.Log(
            $"[HumanBoxAI] Player={playerName}, distance={distance:0.00}, detectRange={detectRange:0.##}, " +
            $"chaseRange={chaseRange:0.##}, attackRange={attackRange:0.##}, inDetectRange={inDetectRange}, " +
            $"requireLineOfSight={requireLineOfSight}, lineOfSight={lineOfSight}, state={currentState}, HP={currentHp}/{maxHp}",
            this);
    }

    private new void LogDebug(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[HumanBoxAI] {message}", this);
        }
    }

    protected override void OnDrawGizmos()
    {
        if (!showGizmos)
        {
            return;
        }

        Vector3 center = transform.position;

        Gizmos.color = new Color(0.2f, 0.55f, 1f, 0.35f);
        Gizmos.DrawWireSphere(center, detectRange);

        Gizmos.color = new Color(0.45f, 1f, 0.45f, 0.25f);
        Gizmos.DrawWireSphere(center, chaseRange);

        Gizmos.color = new Color(1f, 0.15f, 0.15f, 0.45f);
        Gizmos.DrawWireSphere(center, attackRange);

        if (playerTarget != null)
        {
            Gizmos.color = lastLineOfSight ? Color.white : Color.red;
            Gizmos.DrawLine(Application.isPlaying ? lastRayOrigin : center, Application.isPlaying ? lastRayEnd : playerTarget.position);
        }
    }
}
