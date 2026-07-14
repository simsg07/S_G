using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MonsterCore))]
[RequireComponent(typeof(MonsterDetection))]
[RequireComponent(typeof(MonsterMovement))]
public class BoomberBrain : MonsterAIBase
{
    [Header("Boomber State")]
    [SerializeField] private BoomberState currentState = BoomberState.Idle;

    [Header("Boomber References")]
    [SerializeField] private BoomberExplosion explosion;
    [Tooltip("Optional shared health component. Kept as MonoBehaviour so Boomber can run even when the health module is absent.")]
    [SerializeField] private MonoBehaviour health;

    [Header("Locked Run")]
    [SerializeField] private float lockedRunDirection;

    [Header("Run Acceleration")]
    [SerializeField] private float baseRunSpeed = 3f;
    [SerializeField] private float speedIncreasePerSecond = 1f;
    [SerializeField] private float maxRunSpeed = 7f;
    [SerializeField] private bool useTestSpeedMultiplier;
    [SerializeField] private float testSpeedMultiplier = 20f;
    [SerializeField] private float currentRunSpeed = 3f;
    [SerializeField] private float runElapsedTime;
    [SerializeField] private float actualMoveSpeed;

    [Header("Run Stop Check")]
    [SerializeField] private LayerMask runObstacleLayerMask;
    [SerializeField] private LayerMask objectStopLayerMask;
    [SerializeField] private LayerMask playerLayerMask;
    [SerializeField] private Vector3 obstacleCheckOffset;
    [SerializeField] private float obstacleCheckDistance = 0.2f;
    [SerializeField] private float obstacleCheckWidth = 0.35f;
    [SerializeField] private float obstacleCheckHeight = 0.8f;
    [SerializeField] private float obstacleCheckDepth = 0.4f;

    private bool isAttackStarted;
    private int lastLoggedSpeedStep = -1;

    public BoomberState CurrentState => currentState;
    public float LockedRunDirection => lockedRunDirection;
    public float CurrentRunSpeed => currentRunSpeed;
    public float RunElapsedTime => runElapsedTime;
    public float ActualMoveSpeed => actualMoveSpeed;

    protected override void Awake()
    {
        ApplyBoomberDefaults();
        base.Awake();
        CacheBoomberReferences();
        SubscribeExplosion();
        ChangeState(BoomberState.Idle, true);
    }

    protected override void OnEnable()
    {
        ApplyBoomberDefaults();
        base.OnEnable();
        CacheBoomberReferences();
        SubscribeExplosion();

        currentTarget = null;
        currentTargetType = MonsterTargetType.None;
        lockedRunDirection = 0f;
        currentRunSpeed = baseRunSpeed;
        runElapsedTime = 0f;
        actualMoveSpeed = 0f;
        isAttackStarted = false;
        lastLoggedSpeedStep = -1;

        if (explosion != null)
        {
            explosion.ResetExplosion();
        }

        ChangeState(BoomberState.Idle, true);
    }

    protected override void OnValidate()
    {
        ApplyBoomberDefaults();
        base.OnValidate();
        CacheBoomberReferences();
        baseRunSpeed = Mathf.Max(0f, baseRunSpeed);
        speedIncreasePerSecond = Mathf.Max(0f, speedIncreasePerSecond);
        maxRunSpeed = Mathf.Max(baseRunSpeed, maxRunSpeed);
        testSpeedMultiplier = Mathf.Max(0f, testSpeedMultiplier);
        currentRunSpeed = Mathf.Clamp(currentRunSpeed, baseRunSpeed, maxRunSpeed);
        runElapsedTime = Mathf.Max(0f, runElapsedTime);
        actualMoveSpeed = Mathf.Max(0f, actualMoveSpeed);
        obstacleCheckDistance = Mathf.Max(0.01f, obstacleCheckDistance);
        obstacleCheckWidth = Mathf.Max(0.01f, obstacleCheckWidth);
        obstacleCheckHeight = Mathf.Max(0.01f, obstacleCheckHeight);
        obstacleCheckDepth = Mathf.Max(0.01f, obstacleCheckDepth);

        if (runObstacleLayerMask.value == 0)
        {
            runObstacleLayerMask = LayerMask.GetMask("EnvironmentObstacle", "Wall", "TileObstacle", "Platform");
        }
    }

    protected override void Update()
    {
        if (currentState == BoomberState.Dead || IsHealthDead())
        {
            ChangeState(BoomberState.Dead);
            return;
        }

        if (currentState == BoomberState.Idle)
        {
            base.Update();
        }

        switch (currentState)
        {
            case BoomberState.Idle:
                UpdateIdle();
                break;
            case BoomberState.Run:
                UpdateRun();
                break;
        }
    }

    protected override void FixedUpdate()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (currentState != BoomberState.Run)
        {
            UpdateGroundCheck();
            StopHorizontalMovement();
            return;
        }

        UpdateGroundCheck();
        UpdateBaseMovement(Time.fixedDeltaTime);
    }

    protected override void UpdateBaseMovement(float deltaTime)
    {
        if (currentState != BoomberState.Run)
        {
            StopHorizontalMovement();
            return;
        }

        SetHorizontalMovementLocked(false);

        Vector3 direction = new Vector3(Mathf.Sign(lockedRunDirection), 0f, 0f);
        if (direction.x == 0f)
        {
            StopHorizontalMovement();
            StartAttack("Run direction is zero");
            return;
        }

        AdvanceRunSpeed(deltaTime);
        if (CheckStopObstacleAhead(out Collider stopCollider))
        {
            StartAttack($"BoxCast hit: {stopCollider.name}");
            return;
        }

        Vector3 delta = direction * (actualMoveSpeed * deltaTime);
        if (IsMovementBlocked(delta, out Collider blockedCollider, out Vector3 blockedPoint))
        {
            StopHorizontalMovement();
            LogDebug($"Run stopped by {(blockedCollider != null ? blockedCollider.name : "obstacle")} at {blockedPoint}");
            StartAttack($"Movement cast hit: {(blockedCollider != null ? blockedCollider.name : "obstacle")}");
            return;
        }

        lastMoveDirection = direction;
        if (body != null)
        {
            Vector3 velocity = body.linearVelocity;
            velocity.x = direction.x * actualMoveSpeed;
            velocity.z = 0f;
            body.linearVelocity = velocity;
            moveAnchorPosition = ProjectToFixedZ(body.position);
            return;
        }

        moveAnchorPosition = ProjectToFixedZ(transform.position + delta);
        transform.position = moveAnchorPosition;
    }

    public override void ResetMonster()
    {
        base.ResetMonster();
        if (health != null)
        {
            health.SendMessage("ResetHealth", SendMessageOptions.DontRequireReceiver);
        }

        if (explosion != null)
        {
            explosion.ResetExplosion();
        }

        lockedRunDirection = 0f;
        currentRunSpeed = baseRunSpeed;
        runElapsedTime = 0f;
        actualMoveSpeed = 0f;
        isAttackStarted = false;
        lastLoggedSpeedStep = -1;

        ChangeState(BoomberState.Idle, true);
    }

    private void ApplyBoomberDefaults()
    {
        movementType = MonsterMovementType.Ground;
        detectPlayer = true;
        detectLight = false;
        canDetectLight = false;
        useGravityForGround = true;
        groundOnlyMoveX = true;
        returnHomeWhenTargetLost = false;
        setMoveAnimatorOnlyWhenMoving = true;
    }

    private void CacheBoomberReferences()
    {
        if (explosion == null)
        {
            explosion = GetComponent<BoomberExplosion>();
        }

        if (health == null)
        {
            MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] != null && behaviours[i].GetType().Name == "MonsterHealth")
                {
                    health = behaviours[i];
                    break;
                }
            }
        }

        if (explosion != null && monsterAttack != null)
        {
            explosion.ConfigureDamage(monsterAttack.attackDamage);
        }
    }

    private void UpdateIdle()
    {
        if (!HasVisiblePlayerTarget())
        {
            return;
        }

        float dx = playerTarget.position.x - transform.position.x;
        lockedRunDirection = dx >= 0f ? 1f : -1f;
        LogDebug($"Run direction locked: {(lockedRunDirection > 0f ? "Right" : "Left")}");
        ChangeState(BoomberState.Run);
    }

    private void UpdateRun()
    {
        FaceLockedRunDirection();
    }

    private void AdvanceRunSpeed(float deltaTime)
    {
        runElapsedTime += deltaTime;
        int speedStep = Mathf.FloorToInt(runElapsedTime);
        float increasedSpeed = baseRunSpeed + speedStep * speedIncreasePerSecond;
        float previousSpeed = currentRunSpeed;
        currentRunSpeed = Mathf.Min(increasedSpeed, maxRunSpeed);
        actualMoveSpeed = currentRunSpeed * (useTestSpeedMultiplier ? testSpeedMultiplier : 1f);

        if (speedStep != lastLoggedSpeedStep && !Mathf.Approximately(previousSpeed, currentRunSpeed))
        {
            lastLoggedSpeedStep = speedStep;
            LogDebug($"Speed increased. currentRunSpeed={currentRunSpeed:0.##} actualMoveSpeed={actualMoveSpeed:0.##}");
        }
    }

    private bool CheckStopObstacleAhead(out Collider stopCollider)
    {
        stopCollider = null;
        if (currentState != BoomberState.Run || lockedRunDirection == 0f)
        {
            return false;
        }

        int combinedStopMask = movementObstacleLayerMask.value |
            runObstacleLayerMask.value |
            objectStopLayerMask.value |
            playerLayerMask.value;
        if (combinedStopMask == 0)
        {
            LogDebug("Stop layer mask is empty.");
            return false;
        }

        Vector3 direction = lockedRunDirection > 0f ? Vector3.right : Vector3.left;
        Vector3 origin = transform.position + obstacleCheckOffset;
        origin.z = fixedZPosition;
        Vector3 halfExtents = new Vector3(
            obstacleCheckWidth * 0.5f,
            obstacleCheckHeight * 0.5f,
            obstacleCheckDepth * 0.5f);

        RaycastHit[] hits = Physics.BoxCastAll(
            origin,
            halfExtents,
            direction,
            Quaternion.identity,
            obstacleCheckDistance,
            combinedStopMask,
            QueryTriggerInteraction.Ignore);

        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider candidate = hits[i].collider;
            if (candidate == null || BelongsToBoomber(candidate.transform))
            {
                continue;
            }

            if (hits[i].distance < nearestDistance)
            {
                nearestDistance = hits[i].distance;
                stopCollider = candidate;
            }
        }

        return stopCollider != null;
    }

    private void StopHorizontalMovement()
    {
        lastMoveDirection = Vector3.zero;
        actualMoveSpeed = 0f;

        Vector3 currentPosition = ProjectToFixedZ(GetCurrentPosition());
        moveAnchorPosition.x = currentPosition.x;
        moveAnchorPosition.y = currentPosition.y;
        moveAnchorPosition.z = currentPosition.z;

        if (body == null)
        {
            return;
        }

        Vector3 velocity = body.linearVelocity;
        velocity.x = 0f;
        body.linearVelocity = velocity;
        SetHorizontalMovementLocked(true);
    }

    private void SetHorizontalMovementLocked(bool locked)
    {
        if (body == null)
        {
            return;
        }

        if (locked)
        {
            body.constraints |= RigidbodyConstraints.FreezePositionX;
        }
        else
        {
            body.constraints &= ~RigidbodyConstraints.FreezePositionX;
        }
    }

    private void StartAttack(string reason)
    {
        if (isAttackStarted || currentState == BoomberState.Attack || currentState == BoomberState.Dead)
        {
            return;
        }

        isAttackStarted = true;
        StopHorizontalMovement();
        ChangeState(BoomberState.Attack);
        LogDebug($"Attack started. Reason={reason}");

        if (explosion == null)
        {
            Debug.LogWarning("[BoomberBrain] BoomberExplosion is missing.", this);
            return;
        }

        if (!explosion.StartExplosion(playerTarget))
        {
            LogDebug("Explosion countdown was not started because it is already running or disabled.");
        }
    }

    private void HandleExplosionFinished()
    {
        if (currentState == BoomberState.Dead)
        {
            return;
        }

        ChangeState(BoomberState.Dead);
        DisableColliders();
        LogDebug("Dead. Reason=Explosion finished");

        if (health is IDamageable damageable && !IsHealthDead())
        {
            damageable.TakeDamage(1000000);
        }
    }

    private void DisableColliders()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }
    }

    private void SubscribeExplosion()
    {
        if (explosion == null)
        {
            return;
        }

        explosion.OnExploded -= HandleExplosionFinished;
        explosion.OnExploded += HandleExplosionFinished;
    }

    private void OnDestroy()
    {
        if (explosion != null)
        {
            explosion.OnExploded -= HandleExplosionFinished;
        }
    }

    private void FaceLockedRunDirection()
    {
        if (lockedRunDirection == 0f)
        {
            return;
        }

        FaceTargetIfNeeded(transform.position + Vector3.right * lockedRunDirection);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryBeginAttackFromCollision(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        TryBeginAttackFromCollision(collision);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryBeginAttackFromContact(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryBeginAttackFromContact(other);
    }

    private void TryBeginAttackFromContact(Collider other)
    {
        TryBeginAttackFromContact(other, false);
    }

    private void TryBeginAttackFromCollision(Collision collision)
    {
        if (collision == null)
        {
            return;
        }

        bool horizontalContact = false;
        for (int i = 0; i < collision.contactCount; i++)
        {
            if (Mathf.Abs(collision.GetContact(i).normal.x) >= 0.35f)
            {
                horizontalContact = true;
                break;
            }
        }

        TryBeginAttackFromContact(collision.collider, horizontalContact);
    }

    private void TryBeginAttackFromContact(Collider other, bool horizontalContact)
    {
        if (currentState != BoomberState.Run || other == null || BelongsToBoomber(other.transform))
        {
            return;
        }

        bool hitPlayer = playerTarget != null &&
            (other.transform == playerTarget ||
             other.transform.IsChildOf(playerTarget) ||
             playerTarget.IsChildOf(other.transform));
        int stopMask = movementObstacleLayerMask.value |
            runObstacleLayerMask.value |
            objectStopLayerMask.value |
            playerLayerMask.value;
        bool hitBlockingLayer = (stopMask & (1 << other.gameObject.layer)) != 0;

        if (!hitPlayer && !hitBlockingLayer && !horizontalContact)
        {
            return;
        }

        StartAttack(hitPlayer
            ? $"Collision with Player: {other.name}"
            : $"Collision with obstacle: {other.name}");
    }

    private bool BelongsToBoomber(Transform candidate)
    {
        return candidate == transform || candidate.IsChildOf(transform);
    }

    private bool IsHealthDead()
    {
        if (health == null)
        {
            return false;
        }

        System.Reflection.PropertyInfo property = health.GetType().GetProperty("IsDead");
        return property != null && property.PropertyType == typeof(bool) && (bool)property.GetValue(health);
    }

    private bool HasVisiblePlayerTarget()
    {
        return CurrentTargetType == MonsterTargetType.Player &&
            CurrentTarget == playerTarget &&
            IsPlayerVisible();
    }

    private void ChangeState(BoomberState nextState, bool force = false)
    {
        if (!force && currentState == nextState)
        {
            return;
        }

        BoomberState previous = currentState;
        currentState = nextState;

        if (monsterAnimatorBridge != null)
        {
            monsterAnimatorBridge.SetState((int)currentState);
            monsterAnimatorBridge.SetMoving(currentState == BoomberState.Run);
            monsterAnimatorBridge.SetAttacking(currentState == BoomberState.Attack);
            monsterAnimatorBridge.SetDead(currentState == BoomberState.Dead);

            if (currentState == BoomberState.Attack)
            {
                monsterAnimatorBridge.TriggerAttack();
            }
        }

        if (currentState == BoomberState.Run)
        {
            SetHorizontalMovementLocked(false);
            currentRunSpeed = baseRunSpeed;
            runElapsedTime = 0f;
            actualMoveSpeed = currentRunSpeed * (useTestSpeedMultiplier ? testSpeedMultiplier : 1f);
            lastLoggedSpeedStep = 0;
        }
        else
        {
            StopHorizontalMovement();
        }

        if (currentState == BoomberState.Idle)
        {
            lockedRunDirection = 0f;
            isAttackStarted = false;
        }

        LogDebug($"State changed: {previous} -> {currentState}");
    }

    private void OnDisable()
    {
        StopHorizontalMovement();
    }
}
