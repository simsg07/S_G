using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class HitReceiver : MonoBehaviour, IDamageable
{
    private static readonly int HitCountHash = Animator.StringToHash("HitCount");
    private static readonly int IsDamagedHash = Animator.StringToHash("IsDamaged");
    private static readonly int BreakHash = Animator.StringToHash("Break");

    [Header("Hit Rules")]
    [Tooltip("Number of hits required before onMaxHit is invoked.")]
    [SerializeField] private int maxHitCount = 1;
    [Tooltip("Runtime hit count. Read-only during Play Mode; use ResetHitCount for reset logic.")]
    [SerializeField] private int currentHitCount;
    [Tooltip("When false, this receiver ignores incoming hits.")]
    [SerializeField] private bool canBeTargeted = true;
    [SerializeField] private bool acceptGenericHit = true;
    [SerializeField] private bool acceptEyeballFlyAttack;
    [SerializeField] private bool acceptBoomberContact;
    [SerializeField] private bool acceptBoomberExplosion;
    [SerializeField] private bool acceptMonsterAttack;
    [Tooltip("Destroy this GameObject after max hits.")]
    [SerializeField] private bool destroyAfterMaxHit;

    [Header("Events")]
    [Tooltip("Invoked every time a valid hit is registered.")]
    public UnityEvent onHit;
    [Tooltip("Invoked only when the first valid hit is registered.")]
    public UnityEvent onFirstHit;
    [Tooltip("Invoked when currentHitCount reaches maxHitCount.")]
    public UnityEvent onMaxHit;

    [Header("Visual")]
    [Tooltip("Optional animator that receives HitCount, IsDamaged, and Break parameters when present.")]
    [SerializeField] private Animator animator;

    [Header("Debug")]
    [Tooltip("Print hit logs in the Console.")]
    [SerializeField] private bool debugMode = true;

    public int CurrentHitCount => currentHitCount;
    public int MaxHitCount => maxHitCount;
    public bool CanTakeDamage => canBeTargeted && currentHitCount < maxHitCount;
    public bool CanAcceptHitSource(HitSourceType sourceType)
    {
        return canBeTargeted && currentHitCount < maxHitCount && IsSourceAllowed(sourceType);
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnValidate()
    {
        maxHitCount = Mathf.Max(1, maxHitCount);
        currentHitCount = Mathf.Clamp(currentHitCount, 0, maxHitCount);
        CacheReferences();
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0)
        {
            return;
        }

        RegisterHit(new DamageInfo(damage, null, gameObject, transform.position, Vector3.zero, DamageType.Generic));
    }

    public void TakeDamage(DamageInfo damageInfo)
    {
        if (damageInfo.damageAmount <= 0)
        {
            return;
        }

        RegisterHit(damageInfo);
    }

    public void RegisterHit(DamageInfo damageInfo)
    {
        HitSourceType sourceType = damageInfo.hitSourceType == HitSourceType.None
            ? DamageInfo.ToHitSourceType(damageInfo.damageType)
            : damageInfo.hitSourceType;

        if (!canBeTargeted || currentHitCount >= maxHitCount)
        {
            string attackerName = damageInfo.attacker != null ? damageInfo.attacker.name : "Unknown";
            Log($"{name} ignored hit from {attackerName}. CanBeTargeted={canBeTargeted}, Count={currentHitCount}/{maxHitCount}, Source={sourceType}");
            return;
        }

        if (!IsSourceAllowed(sourceType))
        {
            Log($"Hit ignored. Source {sourceType} is not allowed for {name}.");
            return;
        }

        currentHitCount++;
        ApplyAnimatorState(false);
        string hitAttackerName = damageInfo.attacker != null ? damageInfo.attacker.name : "Unknown";
        Log($"{name} received hit from {hitAttackerName}. Source={sourceType}. Count: {currentHitCount} / {maxHitCount}");
        onHit?.Invoke();

        if (currentHitCount == 1)
        {
            Log($"{name} invoking onFirstHit.");
            onFirstHit?.Invoke();
        }

        if (currentHitCount >= maxHitCount)
        {
            ApplyAnimatorState(true);
            Log($"{name} reached max hit count. Invoking onMaxHit.");
            onMaxHit?.Invoke();

            if (destroyAfterMaxHit)
            {
                Destroy(gameObject);
            }
        }

        Log($"Hit registered. Count={currentHitCount}/{maxHitCount}, Source={sourceType}");
    }

    public void ResetHitCount()
    {
        currentHitCount = 0;
        ApplyAnimatorState(false);
        Log("Hit count reset.");
    }

    public void SetCanBeTargeted(bool value)
    {
        canBeTargeted = value;
    }

    public void ConfigureHitRules(int maxHits, bool targetable)
    {
        maxHitCount = Mathf.Max(1, maxHits);
        currentHitCount = Mathf.Clamp(currentHitCount, 0, maxHitCount);
        canBeTargeted = targetable;
        ApplyAnimatorState(false);
    }

    public void ConfigureHitRules(
        int maxHits,
        bool targetable,
        bool acceptGeneric,
        bool acceptEyeballFly,
        bool acceptBoomberContactValue,
        bool acceptBoomberExplosionValue,
        bool acceptMonsterAttackValue)
    {
        ConfigureHitRules(maxHits, targetable);
        acceptGenericHit = acceptGeneric;
        acceptEyeballFlyAttack = acceptEyeballFly;
        acceptBoomberContact = acceptBoomberContactValue;
        acceptBoomberExplosion = acceptBoomberExplosionValue;
        acceptMonsterAttack = acceptMonsterAttackValue;
    }

    private bool IsSourceAllowed(HitSourceType sourceType)
    {
        switch (sourceType)
        {
            case HitSourceType.None:
            case HitSourceType.Generic:
            case HitSourceType.Player:
            case HitSourceType.Environment:
                return acceptGenericHit;
            case HitSourceType.EyeballFlyAttack:
                return acceptEyeballFlyAttack;
            case HitSourceType.BoomberContact:
                return acceptBoomberContact;
            case HitSourceType.BoomberExplosion:
                return acceptBoomberExplosion;
            case HitSourceType.HumanBoxAttack:
            case HitSourceType.MonsterAttack:
                return acceptMonsterAttack;
            default:
                return acceptGenericHit;
        }
    }

    private void CacheReferences()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }
    }

    private void ApplyAnimatorState(bool reachedMaxHit)
    {
        if (animator == null)
        {
            return;
        }

        if (HasParameter(HitCountHash, AnimatorControllerParameterType.Int))
        {
            animator.SetInteger(HitCountHash, currentHitCount);
        }

        if (HasParameter(IsDamagedHash, AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(IsDamagedHash, currentHitCount > 0);
        }

        if (reachedMaxHit && HasParameter(BreakHash, AnimatorControllerParameterType.Trigger))
        {
            animator.SetTrigger(BreakHash);
        }
    }

    private bool HasParameter(int hash, AnimatorControllerParameterType type)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].nameHash == hash && parameters[i].type == type)
            {
                return true;
            }
        }

        return false;
    }

    private void Log(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[HitReceiver] {message}", this);
        }
    }
}
