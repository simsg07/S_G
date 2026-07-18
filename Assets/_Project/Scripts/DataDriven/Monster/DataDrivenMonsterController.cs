using UnityEngine;

[DisallowMultipleComponent]
public class DataDrivenMonsterController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private MonsterData monsterData;
    [SerializeField] private bool applyOnAwake = true;
    [SerializeField] private bool applyOnStart;

    [Header("Target Components")]
    [SerializeField] private MonsterDetection detection;
    [SerializeField] private MonsterMovement movement;
    [SerializeField] private MonsterAttack attack;
    [SerializeField] private MonsterHealth health;
    [SerializeField] private MonsterAnimatorBridge animatorBridge;
    [SerializeField] private MonoBehaviour brain;

    [Header("Debug")]
    [SerializeField] private bool debugMode;

    private void Reset()
    {
        AutoFill();
    }

    private void Awake()
    {
        AutoFill();
        if (applyOnAwake)
        {
            ApplyData();
        }
    }

    private void Start()
    {
        if (applyOnStart)
        {
            ApplyData();
        }
    }

    private void OnValidate()
    {
        AutoFill();
    }

    [ContextMenu("Apply Monster Data")]
    public void ApplyData()
    {
        AutoFill();

        if (monsterData == null)
        {
            Debug.LogWarning("[DataDrivenMonsterController] MonsterData is not assigned.", this);
            return;
        }

        ApplyDetection();
        ApplyMovement();
        ApplyAttack();
        ApplyHealth();
        ApplyAnimatorBridge();
        ApplyMonsterSpecificData();

        Log($"Applied MonsterData: {monsterData.displayName} ({monsterData.monsterKind})");
    }

    [ContextMenu("Validate Monster Data Setup")]
    public void ValidateMonsterDataSetup()
    {
        AutoFill();

        Log($"MonsterData: {(monsterData != null ? monsterData.name : "None")}");
        LogComponent("Detection", detection);
        LogComponent("Movement", movement);
        LogComponent("Attack", attack);
        LogComponent("Health", health);
        LogComponent("AnimatorBridge", animatorBridge);
        LogComponent("Brain", brain);

        if (monsterData == null)
        {
            Debug.LogWarning("[DataDrivenMonsterController] Warning: MonsterData not assigned.", this);
        }

        Debug.Log("[DataDrivenMonsterController] Validate complete. Missing optional components are warnings only.", this);
    }

    private void AutoFill()
    {
        if (detection == null)
        {
            detection = GetComponent<MonsterDetection>();
        }

        if (movement == null)
        {
            movement = GetComponent<MonsterMovement>();
        }

        if (attack == null)
        {
            attack = GetComponent<MonsterAttack>();
        }

        if (health == null)
        {
            health = GetComponent<MonsterHealth>();
        }

        if (animatorBridge == null)
        {
            animatorBridge = GetComponent<MonsterAnimatorBridge>();
        }

        if (brain == null)
        {
            brain = GetComponent<EyeballFlyAI>();
        }

        if (brain == null)
        {
            brain = GetComponent<EyeballFlyBrain>();
        }

        if (brain == null)
        {
            brain = GetComponent<HumanBoxAI>();
        }

        if (brain == null)
        {
            brain = GetComponent<HumanBoxBrain>();
        }

        if (brain == null)
        {
            brain = GetComponent<BoomberBrain>();
        }
    }

    private void ApplyDetection()
    {
        if (detection == null)
        {
            WarnMissing(nameof(MonsterDetection));
            return;
        }

        detection.enableDetection = monsterData.canDetectPlayer || monsterData.canDetectLight;
        detection.canDetectPlayer = monsterData.canDetectPlayer;
        detection.canDetectLight = monsterData.canDetectLight;
        detection.prioritizePlayer = monsterData.prioritizePlayer;
        detection.playerDetectRange = monsterData.playerDetectRange;
        detection.lightDetectRange = monsterData.lightDetectRange;
        detection.chaseRange = monsterData.chaseRange;
        detection.requireLineOfSight = monsterData.requireLineOfSight;
        detection.obstacleLayerMask = monsterData.obstacleLayerMask;
        detection.debugMode = monsterData.debugMode;
    }

    private void ApplyMovement()
    {
        if (movement == null)
        {
            WarnMissing(nameof(MonsterMovement));
            return;
        }

        movement.enableMovement = monsterData.moveType != MonsterMoveType.None;
        movement.movementType = ToRuntimeMovementType(monsterData.moveType);
        movement.moveSpeed = monsterData.moveSpeed;
        movement.testMoveSpeed = monsterData.moveSpeed;
        movement.returnSpeed = monsterData.returnSpeed;
        movement.stopDistance = monsterData.stopDistance;
        movement.lockZPosition = monsterData.lockZPosition;
        movement.returnToHomeWhenLost = monsterData.moveType == MonsterMoveType.FlyingChase ||
            monsterData.moveType == MonsterMoveType.ReturnHome;
        movement.groundOnlyMoveX = monsterData.moveType == MonsterMoveType.GroundChase ||
            monsterData.moveType == MonsterMoveType.StraightRush;
        movement.useGravityForGround = monsterData.useGravity;
        movement.debugMode = monsterData.debugMode;
    }

    private void ApplyAttack()
    {
        if (attack == null)
        {
            WarnMissing(nameof(MonsterAttack));
            return;
        }

        attack.enableAttack = monsterData.attackType != MonsterAttackType.None;
        attack.attackRange = monsterData.attackRange;
        attack.attackDamage = monsterData.attackDamage;
        attack.attackInterval = Mathf.Max(0.05f, monsterData.attackCooldown);
        attack.attackCooldown = monsterData.attackCooldown;
        attack.allowLightAttackVisual = monsterData.canAttackLight;
        attack.debugMode = monsterData.debugMode;
    }

    private void ApplyHealth()
    {
        if (health == null)
        {
            WarnMissing(nameof(MonsterHealth));
            return;
        }

        health.maxHp = monsterData.maxHp;
        health.currentHp = Mathf.Clamp(health.currentHp, 0, monsterData.maxHp);
        if (health.currentHp <= 0)
        {
            health.currentHp = monsterData.maxHp;
        }

        health.debugMode = monsterData.debugMode;
    }

    private void ApplyAnimatorBridge()
    {
        if (animatorBridge == null)
        {
            WarnMissing(nameof(MonsterAnimatorBridge));
            return;
        }

        animatorBridge.debugMode = monsterData.debugMode;
    }

    private void ApplyMonsterSpecificData()
    {
        if (monsterData.monsterKind != MonsterKind.EyeballFly)
        {
            return;
        }

        EyeballFlyAI eyeballFlyAI = GetComponent<EyeballFlyAI>();
        if (eyeballFlyAI == null)
        {
            WarnMissing(nameof(EyeballFlyAI));
            return;
        }

        eyeballFlyAI.ConfigureDataDrivenAttack(
            monsterData.attackDuration,
            monsterData.attackTargetLayerMask,
            monsterData.canAttackHitReceivers,
            monsterData.canAttackPlayer,
            monsterData.canAttackLight,
            monsterData.attackType == MonsterAttackType.ObjectHit || monsterData.canAttackHitReceivers,
            monsterData.debugMode);
    }

    private static MonsterMovementType ToRuntimeMovementType(MonsterMoveType moveType)
    {
        switch (moveType)
        {
            case MonsterMoveType.FlyingChase:
                return MonsterMovementType.Flying;
            case MonsterMoveType.GroundChase:
            case MonsterMoveType.StraightRush:
                return MonsterMovementType.Ground;
            default:
                return MonsterMovementType.Ground;
        }
    }

    private void WarnMissing(string componentName)
    {
        if (debugMode || (monsterData != null && monsterData.debugMode))
        {
            Debug.LogWarning($"[DataDrivenMonsterController] {componentName} is missing. Data value skipped.", this);
        }
    }

    private void Log(string message)
    {
        if (debugMode || (monsterData != null && monsterData.debugMode))
        {
            Debug.Log($"[DataDrivenMonsterController] {message}", this);
        }
    }

    private void LogComponent(string label, Object component)
    {
        if (component != null)
        {
            Debug.Log($"[DataDrivenMonsterController] {label} found: {component.GetType().Name}", this);
            return;
        }

        Debug.LogWarning($"[DataDrivenMonsterController] Warning: {label} not assigned. Settings skipped if needed.", this);
    }
}
