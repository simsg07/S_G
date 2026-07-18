using UnityEngine;

public enum MonsterKind
{
    None,
    EyeballFly,
    HumanBox,
    Boomber
}

public enum MonsterMoveType
{
    None,
    FlyingChase,
    GroundChase,
    StraightRush,
    ReturnHome
}

public enum MonsterAttackType
{
    None,
    Melee,
    ObjectHit,
    Explosion,
    HowlThenMelee
}

[CreateAssetMenu(menuName = "_Project/Data/Monster Data", fileName = "MonsterData")]
public class MonsterData : ScriptableObject
{
    [Header("Identity")]
    public string monsterId;
    public string displayName;
    public MonsterKind monsterKind;

    [Header("Stats")]
    public int maxHp = 1;
    public int contactDamage = 1;

    [Header("Detection")]
    public bool canDetectPlayer = true;
    public bool canDetectLight;
    public bool prioritizePlayer = true;
    public float playerDetectRange = 1.5f;
    public float lightDetectRange = 4f;
    public float chaseRange = 6f;
    public bool requireLineOfSight = true;
    public LayerMask obstacleLayerMask;

    [Header("Movement")]
    public MonsterMoveType moveType = MonsterMoveType.GroundChase;
    public float moveSpeed = 1f;
    public float returnSpeed = 1f;
    public float stopDistance = 0.1f;
    public bool lockZPosition = true;
    public bool useGravity = true;

    [Header("Attack")]
    public MonsterAttackType attackType = MonsterAttackType.Melee;
    public int attackDamage = 1;
    public float attackRange = 0.5f;
    public float attackCooldown = 1f;
    public float attackDuration = 0.5f;
    public LayerMask attackTargetLayerMask = ~0;
    public bool canAttackHitReceivers;
    public bool canAttackPlayer = true;
    public bool canAttackLight = true;

    [Header("Special")]
    public bool useHowl;
    public float howlDuration = 1f;
    public float howlStunDuration = 1.5f;
    public bool useSelfDestruct;
    public float explosionRadius = 1.5f;
    public bool lockRunDirectionOnDetect;
    public float speedIncreasePerSecond = 1f;
    public float maxRunSpeed = 7f;

    [Header("Animation")]
    public string idleBoolName = "IsIdle";
    public string moveBoolName = "IsMoving";
    public string attackBoolName = "IsAttacking";
    public string deadBoolName = "IsDead";
    public string attackTriggerName = "Attack";

    [Header("Debug")]
    public bool debugMode;

    private void OnValidate()
    {
        maxHp = Mathf.Max(1, maxHp);
        contactDamage = Mathf.Max(0, contactDamage);
        playerDetectRange = Mathf.Max(0f, playerDetectRange);
        lightDetectRange = Mathf.Max(0f, lightDetectRange);
        chaseRange = Mathf.Max(0f, chaseRange);
        moveSpeed = Mathf.Max(0f, moveSpeed);
        returnSpeed = Mathf.Max(0f, returnSpeed);
        stopDistance = Mathf.Max(0f, stopDistance);
        attackDamage = Mathf.Max(0, attackDamage);
        attackRange = Mathf.Max(0f, attackRange);
        attackCooldown = Mathf.Max(0f, attackCooldown);
        attackDuration = Mathf.Max(0.01f, attackDuration);
        howlDuration = Mathf.Max(0f, howlDuration);
        howlStunDuration = Mathf.Max(0f, howlStunDuration);
        explosionRadius = Mathf.Max(0f, explosionRadius);
        speedIncreasePerSecond = Mathf.Max(0f, speedIncreasePerSecond);
        maxRunSpeed = Mathf.Max(0f, maxRunSpeed);
    }
}
