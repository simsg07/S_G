using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MonsterCore))]
[RequireComponent(typeof(MonsterDetection))]
[RequireComponent(typeof(MonsterAttack))]
public class DionaeaAI : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private Transform detectionOrigin;
    [SerializeField] private Transform forwardRoot;
    [SerializeField] private Vector3 forwardDirection = Vector3.up;
    [SerializeField] private Vector3 detectionBoxOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private Vector3 detectionBoxSize = new Vector3(1.8f, 3f, 1f);
    [SerializeField] private LayerMask playerLayerMask;
    [SerializeField] private LayerMask obstacleLayerMask;
    [SerializeField] private bool requireLineOfSight = true;
    [SerializeField] private bool detectOnlyInFront = true;

    [Header("Attack")]
    [SerializeField] private float attackRange = 1.2f;
    [SerializeField] private float attackCooldown = 1f;
    [SerializeField] private float attackWindup = 0.25f;
    [SerializeField] private int attackDamage = 2;
    [SerializeField] private LayerMask attackTargetLayerMask;

    [Header("Light Reaction")]
    [SerializeField] private bool isLit;
    [SerializeField] private float lightExposureTime;
    [SerializeField] private float requiredLightExposureTime = 1f;

    [Header("Retract Timing")]
    [SerializeField] private float retractAnimationDuration = 1f;
    [SerializeField] private float recoverAnimationDuration = 1f;
    [SerializeField] private float postRecoverAttackLockTime = 0.5f;
    [SerializeField] private float recoverFromLightDelay = 1f;
    [SerializeField] private bool waitRetractAnimationBeforeFullRetracted = true;
    [SerializeField] private bool retractWhenLit = true;

    [Header("State")]
    [SerializeField] private bool canDie;
    [SerializeField] private bool isIndestructible = true;
    [SerializeField] private DionaeaState currentState = DionaeaState.Idle;
    [SerializeField] private bool canAttack = true;
    [SerializeField] private bool isAttacking;
    [SerializeField] private bool isRetracted;

    [Header("References")]
    [SerializeField] private DionaeaAttack dionaeaAttack;
    [SerializeField] private DionaeaLightReceiver lightReceiver;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private DionaeaAnimatorBridge dionaeaAnimatorBridge;

    [Header("Debug")]
    [SerializeField] private bool showGizmo = true;
    [SerializeField] private bool debugMode;

    private readonly Collider[] detectionHits = new Collider[24];
    private Transform detectedPlayer;
    private Rigidbody body;
    private MonsterCore monsterCore;
    private MonsterDetection monsterDetection;
    private MonsterAttack monsterAttack;
    private MonsterAnimatorBridge animatorBridge;
    private float darknessTime;
    private float retractAnimationTime;
    private float recoverAnimationTime;
    private float nextAttackTime;
    private float nextAllowedAttackTime;
    private float attackResolveTime = -1f;

    public DionaeaState CurrentState => currentState;
    public bool CanDie => canDie;
    public bool IsIndestructible => isIndestructible;
    public bool IsRetracted => isRetracted;
    public bool IsRetracting => currentState == DionaeaState.Retracting;
    public bool IsRecovering => currentState == DionaeaState.Recovering;
    public bool IsLit => isLit;
    public bool CanAttack => canAttack && currentState == DionaeaState.Idle && !isLit && Time.time >= nextAllowedAttackTime;

    private void Reset() => AutoFill();

    private void Awake()
    {
        AutoFill();
        EnforceInvulnerability();
        ApplySharedSettings();
        LockBody();
        ResetAnimatorState();
        nextAllowedAttackTime = Time.time + postRecoverAttackLockTime;
        SetState(DionaeaState.Idle);
    }

    private void OnEnable()
    {
        AutoFill();
        LockBody();
    }

    private void OnValidate()
    {
        EnforceInvulnerability();
        detectionBoxSize = MonsterRuntime3D.ClampSize(detectionBoxSize, 0.01f);
        attackRange = Mathf.Max(0f, attackRange);
        attackCooldown = Mathf.Max(0f, attackCooldown);
        attackWindup = Mathf.Max(0f, attackWindup);
        attackDamage = Mathf.Max(0, attackDamage);
        requiredLightExposureTime = Mathf.Max(0f, requiredLightExposureTime);
        recoverFromLightDelay = Mathf.Max(0f, recoverFromLightDelay);
        retractAnimationDuration = Mathf.Max(0.01f, retractAnimationDuration);
        recoverAnimationDuration = Mathf.Max(0.01f, recoverAnimationDuration);
        postRecoverAttackLockTime = Mathf.Max(0f, postRecoverAttackLockTime);
        AutoFill();
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        UpdateState();
    }

    private void FixedUpdate() => LockBody();

    public void UpdateState()
    {
        if (currentState == DionaeaState.Disabled) return;

        UpdateLightState(Time.deltaTime);
        if (currentState == DionaeaState.Retracting)
        {
            retractAnimationTime += Time.deltaTime;
            if (!waitRetractAnimationBeforeFullRetracted || retractAnimationTime >= retractAnimationDuration)
                CompleteRetract();
            return;
        }
        if (currentState == DionaeaState.Recovering)
        {
            recoverAnimationTime += Time.deltaTime;
            if (recoverAnimationTime >= recoverAnimationDuration) CompleteRecovery();
            return;
        }
        if (isRetracted) return;

        if (isAttacking)
        {
            if (Time.time >= attackResolveTime)
            {
                isAttacking = false;
                bool stillValid = !isLit && detectedPlayer != null &&
                    IsInsideAttackRange(detectedPlayer) && HasLineOfSightToPlayer(detectedPlayer);
                if (stillValid && dionaeaAttack != null) dionaeaAttack.PerformAttack();
                nextAttackTime = Time.time + attackCooldown;
                SetState(DionaeaState.Idle);
            }
            return;
        }

        Transform player = CheckPlayerDetection();
        if (player != null) TryAttack(player);
    }

    public Transform CheckPlayerDetection()
    {
        detectedPlayer = null;
        Vector3 center = GetDetectionCenter();
        int mask = GetEffectivePlayerMask();
        if (mask == 0) return null;

        int count = Physics.OverlapBoxNonAlloc(center, detectionBoxSize * 0.5f, detectionHits,
            Quaternion.identity, mask, QueryTriggerInteraction.Collide);
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            Collider hit = detectionHits[i];
            if (hit == null || hit.transform.IsChildOf(transform)) continue;
            Transform player = ResolvePlayerTransform(hit.transform);
            if (player == null || (detectOnlyInFront && !IsPlayerInFront(player)) || !HasLineOfSightToPlayer(player)) continue;
            float distance = (player.position - transform.position).sqrMagnitude;
            if (distance < nearest) { nearest = distance; detectedPlayer = player; }
        }
        return detectedPlayer;
    }

    public bool HasLineOfSightToPlayer(Transform player)
    {
        if (player == null) return false;
        if (!requireLineOfSight) return true;
        if (monsterDetection != null)
        {
            monsterDetection.obstacleLayerMask = obstacleLayerMask;
            return monsterDetection.HasLineOfSight(detectionOrigin != null ? detectionOrigin : transform, player, out _);
        }

        Vector3 origin = detectionOrigin != null ? detectionOrigin.position : transform.position;
        Vector3 target = GetTargetPoint(player);
        Vector3 direction = target - origin;
        float distance = direction.magnitude;
        if (distance <= 0.001f) return true;
        return !Physics.Raycast(origin, direction / distance, distance, obstacleLayerMask, QueryTriggerInteraction.Ignore);
    }

    public bool IsPlayerInFront(Transform player)
    {
        if (player == null) return false;
        Vector3 delta = player.position - (detectionOrigin != null ? detectionOrigin.position : transform.position);
        delta.z = 0f;
        if (delta.sqrMagnitude <= 0.0001f) return true;
        return Vector3.Dot(GetForwardDirection(), delta.normalized) > 0f;
    }

    public void TryAttack(Transform player)
    {
        if (!CanAttack || player == null || Time.time < nextAttackTime || !IsInsideAttackRange(player)) return;
        detectedPlayer = player;
        isAttacking = true;
        attackResolveTime = Time.time + attackWindup;
        SetState(DionaeaState.Attacking);
        if (animatorBridge != null) animatorBridge.TriggerAttack();
        if (dionaeaAnimatorBridge != null) dionaeaAnimatorBridge.PlayAttack();
    }

    public void EnterRetracted()
    {
        StartRetractFromLight();
    }

    public void StartRetractFromLight()
    {
        if (!retractWhenLit || currentState == DionaeaState.Disabled ||
            currentState == DionaeaState.Retracting || currentState == DionaeaState.Retracted) return;
        isAttacking = false;
        isRetracted = false;
        retractAnimationTime = 0f;
        attackResolveTime = -1f;
        if (dionaeaAnimatorBridge != null) dionaeaAnimatorBridge.ResetAttackTrigger();
        SetState(DionaeaState.Retracting);
    }

    private void CompleteRetract()
    {
        SetState(DionaeaState.Retracted);
    }

    public void StartRecoveringFromLightLost()
    {
        if (currentState != DionaeaState.Retracting && currentState != DionaeaState.Retracted) return;
        isAttacking = false;
        attackResolveTime = -1f;
        recoverAnimationTime = 0f;
        if (dionaeaAnimatorBridge != null) dionaeaAnimatorBridge.ResetAttackTrigger();
        SetState(DionaeaState.Recovering);
    }

    private void CompleteRecovery()
    {
        lightExposureTime = 0f;
        darknessTime = 0f;
        recoverAnimationTime = 0f;
        nextAllowedAttackTime = Time.time + postRecoverAttackLockTime;
        SetState(DionaeaState.Idle);
    }

    public void ExitRetracted()
    {
        if (currentState == DionaeaState.Disabled) return;
        isRetracted = false;
        retractAnimationTime = 0f;
        lightExposureTime = 0f;
        darknessTime = 0f;
        StartRecoveringFromLightLost();
    }

    public void SetLit(bool lit)
    {
        isLit = lit;
        if (lit && isAttacking)
        {
            isAttacking = false;
            attackResolveTime = -1f;
            SetState(DionaeaState.Idle);
        }
    }

    public void ConfigureDataDrivenStats(float requiredExposure, float recoveryDelay)
    {
        requiredLightExposureTime = Mathf.Max(0f, requiredExposure);
        recoverFromLightDelay = Mathf.Max(0f, recoveryDelay);
        if (lightReceiver != null) lightReceiver.Configure(this, requiredLightExposureTime);
    }

    [ContextMenu("Test Detect Player")]
    private void TestDetectPlayer() => Debug.Log($"[DionaeaAI] Detected={(CheckPlayerDetection() != null)}", this);
    [ContextMenu("Debug Detection Conditions")]
    public void DebugDetectionConditions()
    {
        AutoFill();
        Vector3 center = GetDetectionCenter();
        int mask = GetEffectivePlayerMask();
        int count = Physics.OverlapBoxNonAlloc(center, detectionBoxSize * 0.5f, detectionHits,
            Quaternion.identity, mask, QueryTriggerInteraction.Collide);
        Debug.Log($"[DionaeaAI] DetectionBox Center={center}, Size={detectionBoxSize}, Mask={MaskToNames(mask)}, Hits={count}, Forward={GetForwardDirection()}, Lit={isLit}, Retracted={isRetracted}, CanAttack={CanAttack}", this);
        for (int i = 0; i < count; i++)
        {
            Collider hit = detectionHits[i];
            if (hit == null) continue;
            Transform player = ResolvePlayerTransform(hit.transform);
            bool resolved = player != null;
            bool inFront = resolved && IsPlayerInFront(player);
            bool hasLineOfSight = resolved && HasLineOfSightToPlayer(player);
            Debug.Log($"[DionaeaAI] Hit={hit.name}, Layer={LayerMask.LayerToName(hit.gameObject.layer)}, Tag={hit.tag}, PlayerResolved={resolved}, InFront={inFront}, LOS={hasLineOfSight}", hit);
        }
    }
    [ContextMenu("Test Attack")]
    private void TestAttack() { Transform player = CheckPlayerDetection(); if (player != null) TryAttack(player); }
    [ContextMenu("Test Enter Retracted")]
    private void TestEnterRetracted() => EnterRetracted();
    [ContextMenu("Test Exit Retracted")]
    private void TestExitRetracted() => ExitRetracted();

    [ContextMenu("Validate Dionaea Setup")]
    public void ValidateDionaeaSetup()
    {
        AutoFill();
        Debug.Log($"[DionaeaAI] Core={monsterCore != null}, Detection={monsterDetection != null}, Attack={dionaeaAttack != null}, Light={lightReceiver != null}, Invulnerable=True, PlayerMask={playerLayerMask.value}, ObstacleMask={obstacleLayerMask.value}", this);
    }

    private void UpdateLightState(float deltaTime)
    {
        if (isLit)
        {
            lightExposureTime += deltaTime;
            darknessTime = 0f;
            if (retractWhenLit && lightExposureTime >= requiredLightExposureTime) StartRetractFromLight();
            return;
        }
        lightExposureTime = 0f;
        if (currentState == DionaeaState.Retracting || currentState == DionaeaState.Retracted)
            StartRecoveringFromLightLost();
    }

    private void SetState(DionaeaState state)
    {
        currentState = state;
        isAttacking = state == DionaeaState.Attacking;
        isRetracted = state == DionaeaState.Retracted;
        canAttack = state != DionaeaState.Disabled;
        if (animatorBridge != null) animatorBridge.SetAttacking(isAttacking);
        if (dionaeaAnimatorBridge != null)
        {
            dionaeaAnimatorBridge.SetAttacking(isAttacking);
            dionaeaAnimatorBridge.SetRetracted(state == DionaeaState.Retracting || state == DionaeaState.Retracted);
            dionaeaAnimatorBridge.SetRecovering(state == DionaeaState.Recovering);
        }
    }

    private bool IsInsideAttackRange(Transform player)
    {
        if (dionaeaAttack != null) return dionaeaAttack.IsTargetInsideAttackBox(player);
        Vector3 delta = player.position - transform.position;
        delta.z = 0f;
        return delta.sqrMagnitude <= attackRange * attackRange;
    }

    private Vector3 GetDetectionCenter()
    {
        Transform origin = detectionOrigin != null ? detectionOrigin : transform;
        Transform root = forwardRoot != null ? forwardRoot : transform;
        return origin.position + root.TransformDirection(detectionBoxOffset);
    }

    private int GetEffectivePlayerMask()
    {
        return playerLayerMask.value != 0 ? playerLayerMask.value : LayerMask.GetMask("Default", "Player");
    }

    private static string MaskToNames(int mask)
    {
        string names = string.Empty;
        for (int layer = 0; layer < 32; layer++)
        {
            if ((mask & (1 << layer)) == 0) continue;
            string layerName = LayerMask.LayerToName(layer);
            if (string.IsNullOrEmpty(layerName)) layerName = layer.ToString();
            names = string.IsNullOrEmpty(names) ? layerName : names + ", " + layerName;
        }
        return names;
    }

    private Vector3 GetForwardDirection()
    {
        Transform root = forwardRoot != null ? forwardRoot : transform;
        Vector3 localDirection = forwardDirection.sqrMagnitude > 0.0001f ? forwardDirection.normalized : Vector3.up;
        Vector3 direction = root.TransformDirection(localDirection);
        direction.z = 0f;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up;
    }

    private static Vector3 GetTargetPoint(Transform player)
    {
        Collider collider = player.GetComponentInChildren<Collider>();
        return collider != null ? collider.bounds.center : player.position;
    }

    private static Transform ResolvePlayerTransform(Transform candidate)
    {
        PlayerDamageReceiver receiver = candidate.GetComponentInParent<PlayerDamageReceiver>();
        if (receiver != null) return receiver.transform;
        Transform current = candidate;
        while (current != null)
        {
            if (current.tag == "Player") return current;
            current = current.parent;
        }
        return null;
    }

    private void ApplySharedSettings()
    {
        if (monsterDetection != null)
        {
            obstacleLayerMask = monsterDetection.obstacleLayerMask;
            if (monsterDetection.playerDetectRange > 0f)
            {
                detectionBoxSize.y = monsterDetection.playerDetectRange;
                detectionBoxOffset.y = detectionBoxSize.y * 0.5f;
            }
        }
        if (monsterAttack != null)
        {
            attackRange = monsterAttack.attackRange;
            attackCooldown = Mathf.Max(monsterAttack.attackInterval, monsterAttack.attackCooldown);
            attackWindup = monsterAttack.attackWindup;
            attackDamage = monsterAttack.attackDamage;
        }
        if (dionaeaAttack != null) dionaeaAttack.Configure(attackDamage, attackTargetLayerMask.value != 0 ? attackTargetLayerMask : playerLayerMask);
        if (lightReceiver != null) lightReceiver.Configure(this, requiredLightExposureTime);
    }

    private void AutoFill()
    {
        if (monsterCore == null) monsterCore = GetComponent<MonsterCore>();
        if (monsterDetection == null) monsterDetection = GetComponent<MonsterDetection>();
        if (monsterAttack == null) monsterAttack = GetComponent<MonsterAttack>();
        if (animatorBridge == null) animatorBridge = GetComponent<MonsterAnimatorBridge>();
        if (dionaeaAnimatorBridge == null) dionaeaAnimatorBridge = GetComponent<DionaeaAnimatorBridge>();
        if (dionaeaAttack == null) dionaeaAttack = GetComponent<DionaeaAttack>();
        if (lightReceiver == null) lightReceiver = GetComponentInChildren<DionaeaLightReceiver>(true);
        if (visualRoot == null && monsterCore != null) visualRoot = monsterCore.visualRoot;
        if (body == null) body = GetComponent<Rigidbody>();
    }

    private void LockBody()
    {
        if (body == null) return;
        body.useGravity = false;
        body.isKinematic = true;
        body.constraints = RigidbodyConstraints.FreezeAll;
    }

    private void EnforceInvulnerability()
    {
        canDie = false;
        isIndestructible = true;
    }

    private void ResetAnimatorState()
    {
        if (dionaeaAnimatorBridge == null) return;
        dionaeaAnimatorBridge.ResetAttackTrigger();
        dionaeaAnimatorBridge.SetAttacking(false);
        dionaeaAnimatorBridge.SetRetracted(false);
        dionaeaAnimatorBridge.SetRecovering(false);
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmo) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(GetDetectionCenter(), detectionBoxSize);
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(detectionOrigin != null ? detectionOrigin.position : transform.position, GetForwardDirection() * detectionBoxSize.x);
    }
}
