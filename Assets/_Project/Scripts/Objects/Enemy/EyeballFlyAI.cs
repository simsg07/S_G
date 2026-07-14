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

    [Header("Eyeball Fly State")]
    [SerializeField] private EyeballFlyState currentState = EyeballFlyState.IDLE;

    [Header("References")]
    [SerializeField] private EyeballFlyAnimationController animationController;
    [SerializeField] private Animator animator;

    private float hoverPhase;
    private float nextAttackTime;
    private float nextAnimatorDebugLogTime;
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
        UpdateState();

        if (currentState == EyeballFlyState.ATTACK)
        {
            AttackTarget();
        }
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

    protected override Vector3 GetMovementOffset()
    {
        float bob = Mathf.Sin(Time.time * hoverFrequency + hoverPhase) * hoverAmplitude;
        return new Vector3(0f, bob, 0f);
    }

    protected override void UpdateBaseMovement(float deltaTime)
    {
        if (currentState == EyeballFlyState.ATTACK)
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
    }

    private void InitializeHover()
    {
        hoverPhase = Random.value * Mathf.PI * 2f;
    }

    private void UpdateState()
    {
        if (IsTargetInAttackRange)
        {
            ChangeState(EyeballFlyState.ATTACK);
            return;
        }

        if (HasTarget || IsReturningHome)
        {
            ChangeState(EyeballFlyState.MOVE);
            return;
        }

        ChangeState(EyeballFlyState.IDLE);
    }

    private void AttackTarget()
    {
        if (Time.time < nextAttackTime)
        {
            return;
        }

        nextAttackTime = Time.time + attackInterval;
        bool attackTriggered = PlayAttackVisual();
        LogDebug($"Attack Target: {CurrentTargetType}");
        LogDebug($"Attack Trigger Fired={attackTriggered}, TargetType={CurrentTargetType}, InAttackRange={IsTargetInAttackRange}");
        TryApplyPlayerDamage();
    }

    private void TryApplyPlayerDamage()
    {
        if (CurrentTargetType == MonsterTargetType.Light)
        {
            LogDebug("Light attack visual played. Damage skipped.");
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
        bool isAttacking = currentState == EyeballFlyState.ATTACK;

        bool movingSet = SetMovingVisual(isMoving);
        bool attackingSet = SetAttackingVisual(isAttacking);
        bool deadSet = isDeadState && SetDeadVisual(true);

        LogAnimatorUpdate(movingSet, attackingSet, deadSet);
    }

    private bool SetMovingVisual(bool value)
    {
        bool wrapperSet = animationController != null && animationController.SetMovingVisual(value);
        bool directSet = !wrapperSet && SetAnimatorBool(IsMovingHash, "IsMoving", value);
        return wrapperSet || directSet;
    }

    private bool SetAttackingVisual(bool value)
    {
        bool wrapperSet = animationController != null && animationController.SetAttackingVisual(value);
        bool directSet = !wrapperSet && SetAnimatorBool(IsAttackingHash, "IsAttacking", value);
        return wrapperSet || directSet;
    }

    private bool SetDeadVisual(bool value)
    {
        bool wrapperSet = animationController != null && animationController.SetDeadVisual(value);
        bool directSet = !wrapperSet && SetAnimatorBool(IsDeadHash, "IsDead", value);
        return wrapperSet || directSet;
    }

    private bool PlayAttackVisual()
    {
        bool wrapperSet = animationController != null && animationController.PlayAttack();
        bool directSet = !wrapperSet && SetAnimatorTrigger(AttackHash, "Attack");
        return wrapperSet || directSet;
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
}
