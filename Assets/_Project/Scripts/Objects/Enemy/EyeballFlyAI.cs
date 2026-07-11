using System;
using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
public class EyeballFlyAI : MonoBehaviour
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

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 1f; // Proposal speed is 0.03 unit/s; use 1 for visible testing, then lower in Inspector if needed.
    [SerializeField] private float hoverAmplitude = 0.12f;
    [SerializeField] private float hoverFrequency = 2f;
    [SerializeField] private float fixedZPosition = TwoPointFiveDUtility3D.GameplayPlaneZ;

    [Header("Detection")]
    [SerializeField] private Transform lightTarget;
    [SerializeField] private Transform playerTarget;
    [SerializeField] private string lightTag = "Light";
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float lightDetectRange = 4f;
    [SerializeField] private float playerDetectRange = 1.5f;
    [SerializeField] private float attackRange = 0.5f;

    [Header("Attack")]
    [SerializeField] private int attackDamage = 1;
    [SerializeField] private float attackInterval = 1f;

    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private bool debugMode;
    [SerializeField] private EyeballFlyState currentState = EyeballFlyState.IDLE;

    [Header("References")]
    [SerializeField] private EyeballFlyAnimationController animationController;
    [SerializeField] private Animator animator;

    private Rigidbody body;
    private Transform currentTarget;
    private Vector3 anchorPosition;
    private Vector3 lastMoveDirection;
    private float hoverPhase;
    private float nextAttackTime;
    private float nextTargetDebugLogTime;
    private float nextMoveDebugLogTime;
    private float nextAnimatorDebugLogTime;
    private float nextUpdateDebugLogTime;
    private float nextFixedUpdateDebugLogTime;
    private bool warnedPlayerTargetMissing;
    private bool warnedLightTargetMissing;
    private bool dead;

    public EyeballFlyState CurrentState => currentState;
    public Transform CurrentTarget => currentTarget;

    private void Awake()
    {
        CacheReferences();
        LogDebug($"Awake executed. AnimatorFound={animator != null}, AnimationControllerFound={animationController != null}");
        ConfigureRigidbody();
        InitializeAnchor();
        ApplyAnimatorState();
    }

    private void OnEnable()
    {
        CacheReferences();
        LogDebug($"OnEnable executed. AnimatorFound={animator != null}, AnimationControllerFound={animationController != null}");
        ConfigureRigidbody();
        InitializeAnchor();
        ApplyAnimatorState();
    }

    private void Start()
    {
        CacheReferences();
        LogDebug($"Start executed. PlayerTarget={GetNameOrNone(playerTarget)}, LightTarget={GetNameOrNone(lightTarget)}, AnimatorFound={animator != null}");
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        hoverAmplitude = Mathf.Max(0f, hoverAmplitude);
        hoverFrequency = Mathf.Max(0f, hoverFrequency);
        lightDetectRange = Mathf.Max(0f, lightDetectRange);
        playerDetectRange = Mathf.Max(0f, playerDetectRange);
        attackRange = Mathf.Max(0f, attackRange);
        attackDamage = Mathf.Max(0, attackDamage);
        attackInterval = Mathf.Max(0.05f, attackInterval);
        CacheReferences();
        ConfigureRigidbody();
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            ClampToFixedZ();
            return;
        }

        if (dead || currentState == EyeballFlyState.DEAD)
        {
            return;
        }

        AcquireTarget();
        UpdateState();
        LogUpdateDebug();
        UpdateNonMovementStateAction();
    }

    private void FixedUpdate()
    {
        if (!Application.isPlaying || dead || currentState == EyeballFlyState.DEAD)
        {
            return;
        }

        LogDebugFixedUpdate();
        UpdateMovementStateAction();
    }

    public void Die()
    {
        if (dead)
        {
            return;
        }

        dead = true;
        currentTarget = null;
        ChangeState(EyeballFlyState.DEAD);
    }

    public void ResetMonster()
    {
        dead = false;
        currentTarget = null;
        nextAttackTime = 0f;
        lastMoveDirection = Vector3.zero;
        InitializeAnchor();
        SetDeadVisual(false);
        ChangeState(EyeballFlyState.IDLE);
    }

    private void CacheReferences()
    {
        if (animationController == null)
        {
            animationController = GetComponentInChildren<EyeballFlyAnimationController>(true);
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        if (body == null)
        {
            body = GetComponent<Rigidbody>();
        }
    }

    private void ConfigureRigidbody()
    {
        if (body == null)
        {
            return;
        }

        body.useGravity = false;
        body.isKinematic = true;
        body.constraints &= ~RigidbodyConstraints.FreezePositionX;
        body.constraints &= ~RigidbodyConstraints.FreezePositionY;
        body.constraints |= TwoPointFiveDUtility3D.SideViewRigidbodyConstraints;
        body.position = ProjectToFixedZ(body.position);
    }

    private void InitializeAnchor()
    {
        anchorPosition = transform.position;
        anchorPosition.z = fixedZPosition;
        hoverPhase = UnityEngine.Random.value * Mathf.PI * 2f;
        ApplyPosition(anchorPosition);
    }

    private void AcquireTarget()
    {
        if (lightTarget == null)
        {
            lightTarget = FindLightTarget();
        }

        if (playerTarget == null)
        {
            playerTarget = FindPlayerTarget();
        }

        currentTarget = null;
        if (IsLightAvailable(lightTarget) && IsInRange(lightTarget, lightDetectRange))
        {
            currentTarget = lightTarget;
            LogDebugTarget("Light", currentTarget);
            return;
        }

        if (IsInRange(playerTarget, playerDetectRange))
        {
            currentTarget = playerTarget;
            LogDebugTarget("Player", currentTarget);
            return;
        }

        LogDebugTarget("None", null);
    }

    private Transform FindPlayerTarget()
    {
        Transform found = FindTargetByTag(playerTag);
        if (found != null)
        {
            return found;
        }

        found = FindTargetByName(playerTag);
        if (found != null)
        {
            return found;
        }

        found = FindTargetByName("Player");
        if (found != null)
        {
            return found;
        }

        LogDebugWarningOnce(ref warnedPlayerTargetMissing, "Player target was not found by tag or name.");
        return null;
    }

    private Transform FindLightTarget()
    {
        Transform found = FindTargetByTag(lightTag);
        if (found != null)
        {
            return found;
        }

        found = FindTargetByName(lightTag);
        if (found != null)
        {
            return found;
        }

        found = FindTargetByName("Light");
        if (found != null)
        {
            return found;
        }

        Light lightComponent = FindObjectOfType<Light>();
        if (lightComponent != null)
        {
            return lightComponent.transform;
        }

        LogDebugWarningOnce(ref warnedLightTargetMissing, "Light target was not found by tag or name.");
        return null;
    }

    private Transform FindTargetByTag(string tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return null;
        }

        try
        {
            GameObject found = GameObject.FindGameObjectWithTag(tagName);
            return found != null ? found.transform : null;
        }
        catch (UnityException)
        {
            LogDebug($"Tag '{tagName}' does not exist. Auto target search skipped.");
            return null;
        }
    }

    private Transform FindTargetByName(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        GameObject found = GameObject.Find(targetName);
        return found != null ? found.transform : null;
    }

    private bool IsLightAvailable(Transform target)
    {
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            return false;
        }

        Light lightComponent = target.GetComponentInChildren<Light>();
        return lightComponent == null || lightComponent.enabled;
    }

    private bool IsInRange(Transform target, float range)
    {
        if (target == null)
        {
            return false;
        }

        Vector3 delta = target.position - anchorPosition;
        delta.z = 0f;
        return delta.sqrMagnitude <= range * range;
    }

    private void UpdateState()
    {
        if (currentTarget == null)
        {
            ChangeState(EyeballFlyState.IDLE);
            return;
        }

        ChangeState(IsInRange(currentTarget, attackRange) ? EyeballFlyState.ATTACK : EyeballFlyState.MOVE);
    }

    private void UpdateNonMovementStateAction()
    {
        switch (currentState)
        {
            case EyeballFlyState.ATTACK:
                AttackTarget();
                break;
        }
    }

    private void UpdateMovementStateAction()
    {
        switch (currentState)
        {
            case EyeballFlyState.IDLE:
                StayIdle();
                break;
            case EyeballFlyState.MOVE:
                MoveTowardTarget(Time.fixedDeltaTime);
                break;
            case EyeballFlyState.ATTACK:
                ApplyPosition(anchorPosition);
                break;
        }
    }

    private void StayIdle()
    {
        anchorPosition = ProjectToFixedZ(GetCurrentPosition());
        lastMoveDirection = Vector3.zero;
        ApplyPosition(anchorPosition);
    }

    private void MoveTowardTarget(float deltaTime)
    {
        if (currentTarget == null)
        {
            lastMoveDirection = Vector3.zero;
            return;
        }

        Vector3 targetPosition = currentTarget.position;
        targetPosition.z = fixedZPosition;
        Vector3 direction = targetPosition - anchorPosition;
        direction.z = 0f;

        if (direction.sqrMagnitude > 0.0001f)
        {
            lastMoveDirection = direction.normalized;
            anchorPosition += lastMoveDirection * moveSpeed * deltaTime;
        }
        else
        {
            lastMoveDirection = Vector3.zero;
        }

        float bob = Mathf.Sin(Time.time * hoverFrequency + hoverPhase) * hoverAmplitude;
        Vector3 nextPosition = new Vector3(anchorPosition.x, anchorPosition.y + bob, fixedZPosition);
        ApplyPosition(nextPosition);
        LogMovementDebug();
    }

    private void AttackTarget()
    {
        ApplyPosition(anchorPosition);

        if (currentTarget == null || Time.time < nextAttackTime)
        {
            return;
        }

        nextAttackTime = Time.time + attackInterval;
        bool attackTriggered = false;
        attackTriggered = PlayAttackVisual();

        LogAttackDebug(attackTriggered);

        TryApplyPlayerDamage();
    }

    private void TryApplyPlayerDamage()
    {
        if (!IsInRange(playerTarget, attackRange))
        {
            LogDamageSkipped("Player is not in attack range. Damage skipped.");
            return;
        }

        Type damageableType = FindDamageableType();
        if (damageableType == null)
        {
            LogDamageSkipped("IDamageable interface was not found. Damage skipped.");
            return;
        }

        MonoBehaviour[] behaviours = playerTarget.GetComponentsInParent<MonoBehaviour>();
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null || !damageableType.IsAssignableFrom(behaviour.GetType()))
            {
                continue;
            }

            MethodInfo method = behaviour.GetType().GetMethod("TakeDamage", new[] { typeof(int) });
            if (method == null)
            {
                method = behaviour.GetType().GetMethod("Damage", new[] { typeof(int) });
            }

            if (method == null)
            {
                LogDamageSkipped("IDamageable target found, but no supported damage method exists.");
                return;
            }

            method.Invoke(behaviour, new object[] { attackDamage });
            LogDebug($"Damage applied to Player. Damage={attackDamage}, Receiver={behaviour.name}");
            return;
        }

        LogDamageSkipped("Player has no IDamageable component. Damage skipped.");
    }

    private Type FindDamageableType()
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (Assembly assembly in assemblies)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                types = exception.Types;
            }

            foreach (Type type in types)
            {
                if (type != null && type.IsInterface && type.Name == "IDamageable")
                {
                    return type;
                }
            }
        }

        return null;
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
        bool isMoving = currentState == EyeballFlyState.MOVE && currentTarget != null && !isDeadState;
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
        if (animator == null || !HasAnimatorParameter(parameterHash, AnimatorControllerParameterType.Bool))
        {
            return false;
        }

        animator.SetBool(parameterHash, value);
        return true;
    }

    private bool SetAnimatorTrigger(int parameterHash, string parameterName)
    {
        if (animator == null || !HasAnimatorParameter(parameterHash, AnimatorControllerParameterType.Trigger))
        {
            LogDebug($"Animator trigger skipped. Missing Animator or parameter: {parameterName}");
            return false;
        }

        animator.SetTrigger(parameterHash);
        return true;
    }

    private bool HasAnimatorParameter(int parameterHash, AnimatorControllerParameterType parameterType)
    {
        if (animator == null)
        {
            return false;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == parameterHash && parameter.type == parameterType)
            {
                return true;
            }
        }

        return false;
    }

    private void ClampToFixedZ()
    {
        ApplyPosition(ProjectToFixedZ(GetCurrentPosition()));
        anchorPosition.z = fixedZPosition;
    }

    private Vector3 GetCurrentPosition()
    {
        return body != null ? body.position : transform.position;
    }

    private Vector3 ProjectToFixedZ(Vector3 position)
    {
        position.z = fixedZPosition;
        return position;
    }

    private void ApplyPosition(Vector3 position)
    {
        position = ProjectToFixedZ(position);
        if (body != null && Application.isPlaying)
        {
            body.MovePosition(position);
            return;
        }

        transform.position = position;
    }

    private void LogDebug(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[EyeballFlyAI] {message}", this);
        }
    }

    private void LogDebugWarningOnce(ref bool warned, string message)
    {
        if (!debugMode || warned)
        {
            return;
        }

        warned = true;
        Debug.LogWarning($"[EyeballFlyAI] {message}", this);
    }

    private void LogDebugTarget(string targetKind, Transform target)
    {
        if (!debugMode || Time.time < nextTargetDebugLogTime)
        {
            return;
        }

        nextTargetDebugLogTime = Time.time + 0.5f;
        if (target == null)
        {
            Debug.Log($"[EyeballFlyAI] State={currentState}, Target=None", this);
            return;
        }

        float distance = GetPlanarDistance(target);
        Debug.Log($"[EyeballFlyAI] State={currentState}, Target={targetKind}:{target.name}, Distance={distance:0.00}", this);
    }

    private void LogUpdateDebug()
    {
        if (!debugMode || Time.time < nextUpdateDebugLogTime)
        {
            return;
        }

        nextUpdateDebugLogTime = Time.time + 0.5f;

        string targetName = currentTarget != null ? currentTarget.name : "None";
        float targetDistance = currentTarget != null ? GetPlanarDistance(currentTarget) : -1f;
        float playerDistance = playerTarget != null ? GetPlanarDistance(playerTarget) : -1f;
        float lightDistance = lightTarget != null ? GetPlanarDistance(lightTarget) : -1f;
        bool playerInRange = IsInRange(playerTarget, playerDetectRange);
        bool lightInRange = IsLightAvailable(lightTarget) && IsInRange(lightTarget, lightDetectRange);
        bool attackInRange = currentTarget != null && IsInRange(currentTarget, attackRange);

        Debug.Log(
            $"[EyeballFlyAI] Update State={currentState}, Target={targetName}, TargetDistance={targetDistance:0.00}, " +
            $"Player={GetNameOrNone(playerTarget)}({playerDistance:0.00}) InPlayerRange={playerInRange}, " +
            $"Light={GetNameOrNone(lightTarget)}({lightDistance:0.00}) InLightRange={lightInRange}, " +
            $"InAttackRange={attackInRange}, AnimatorFound={animator != null}",
            this);
    }

    private void LogDebugFixedUpdate()
    {
        if (!debugMode || Time.time < nextFixedUpdateDebugLogTime)
        {
            return;
        }

        nextFixedUpdateDebugLogTime = Time.time + 1f;
        Debug.Log($"[EyeballFlyAI] FixedUpdate executed. State={currentState}", this);
    }

    private void LogAnimatorUpdate(bool movingSet, bool attackingSet, bool deadSet)
    {
        if (!debugMode)
        {
            return;
        }

        if (Time.time < nextAnimatorDebugLogTime)
        {
            return;
        }

        nextAnimatorDebugLogTime = Time.time + 0.5f;

        string targetName = currentTarget != null ? currentTarget.name : "None";
        float distance = currentTarget != null ? GetPlanarDistance(currentTarget) : -1f;
        Debug.Log(
            $"[EyeballFlyAI] Animator State={currentState}, Target={targetName}, Distance={distance:0.00}, " +
            $"IsMovingSet={movingSet}, IsAttackingSet={attackingSet}, IsDeadSet={deadSet}",
            this);
    }

    private void LogAttackDebug(bool attackTriggered)
    {
        if (!debugMode || currentTarget == null)
        {
            return;
        }

        float distance = GetPlanarDistance(currentTarget);
        bool inAttackRange = distance <= attackRange;
        bool isMoving = currentState == EyeballFlyState.MOVE;
        bool isAttacking = currentState == EyeballFlyState.ATTACK;
        Debug.Log(
            $"[EyeballFlyAI] AttackDebug State={currentState}, Target={currentTarget.name}, Distance={distance:0.00}, " +
            $"InAttackRange={inAttackRange}, IsMoving={isMoving}, IsAttacking={isAttacking}, AttackTriggerSet={attackTriggered}",
            this);
    }

    private void LogMovementDebug()
    {
        if (!debugMode || Time.time < nextMoveDebugLogTime)
        {
            return;
        }

        nextMoveDebugLogTime = Time.time + 0.5f;

        string constraints = body != null ? body.constraints.ToString() : "No Rigidbody";
        string targetName = currentTarget != null ? currentTarget.name : "None";
        float distance = currentTarget != null ? GetPlanarDistance(currentTarget) : -1f;
        bool lightDetected = IsLightAvailable(lightTarget) && IsInRange(lightTarget, lightDetectRange);
        bool playerDetected = IsInRange(playerTarget, playerDetectRange);

        Debug.Log(
            $"[EyeballFlyAI] MoveDebug State={currentState}, Target={targetName}, Distance={distance:0.00}, " +
            $"LightDetected={lightDetected}, PlayerDetected={playerDetected}, MoveDirection={lastMoveDirection}, " +
            $"MoveSpeed={moveSpeed:0.###}, RigidbodyConstraints={constraints}",
            this);
    }

    private float GetPlanarDistance(Transform target)
    {
        if (target == null)
        {
            return 0f;
        }

        Vector3 delta = target.position - anchorPosition;
        delta.z = 0f;
        return delta.magnitude;
    }

    private string GetNameOrNone(Transform target)
    {
        return target != null ? target.name : "None";
    }

    private void LogDamageSkipped(string message)
    {
        Debug.Log($"[EyeballFlyAI] {message}", this);
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos)
        {
            return;
        }

        Vector3 center = Application.isPlaying ? anchorPosition : transform.position;
        center.z = fixedZPosition;

        Gizmos.color = new Color(1f, 0.95f, 0.15f, 0.35f);
        Gizmos.DrawWireSphere(center, lightDetectRange);

        Gizmos.color = new Color(0.2f, 0.55f, 1f, 0.35f);
        Gizmos.DrawWireSphere(center, playerDetectRange);

        Gizmos.color = new Color(1f, 0.15f, 0.15f, 0.45f);
        Gizmos.DrawWireSphere(center, attackRange);

        if (currentTarget != null)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawLine(center, currentTarget.position);
        }
    }
}
