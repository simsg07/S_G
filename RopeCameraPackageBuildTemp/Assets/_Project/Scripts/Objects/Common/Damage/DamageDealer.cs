using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class DamageDealer : MonoBehaviour
{
    [Header("Damage - Designer Settings")]
    [Tooltip("Damage amount sent to IDamageable targets.")]
    [SerializeField] private int damage = 1;
    [Tooltip("Damage category used by receivers that care about hit source.")]
    [SerializeField] private DamageType damageType = DamageType.Generic;
    [Tooltip("Detailed hit source sent to HitReceiver. Leave None to infer from DamageType.")]
    [SerializeField] private HitSourceType hitSourceType = HitSourceType.None;
    [Tooltip("Only objects on these layers can receive damage.")]
    [SerializeField] private LayerMask targetLayerMask = ~0;

    [Header("Collision")]
    [Tooltip("Apply damage from OnCollisionEnter.")]
    [SerializeField] private bool damageOnCollision = true;
    [Tooltip("Apply damage from OnTriggerEnter.")]
    [SerializeField] private bool damageOnTrigger;
    [Tooltip("Prevent the same target root from taking damage more than once from this dealer.")]
    [SerializeField] private bool damageOncePerTarget = true;
    [Tooltip("Ignore collision events while this component is disabled.")]
    [SerializeField] private bool onlyWhenEnabled = true;

    [Header("Debug")]
    [Tooltip("Print damage attempts and warnings in the Console.")]
    [SerializeField] private bool debugMode = true;

    private readonly HashSet<int> damagedTargets = new HashSet<int>();

    private void OnValidate()
    {
        damage = Mathf.Max(0, damage);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!damageOnCollision || collision == null || collision.collider == null)
        {
            return;
        }

        ContactPoint contact = collision.contactCount > 0 ? collision.GetContact(0) : default(ContactPoint);
        TryDamage(collision.collider, contact.point);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!damageOnTrigger || other == null)
        {
            return;
        }

        TryDamage(other, other.ClosestPoint(transform.position));
    }

    public void ClearDamagedTargets()
    {
        damagedTargets.Clear();
    }

    public void ConfigureDamage(int damageAmount, LayerMask targetMask, bool oncePerTarget)
    {
        damage = Mathf.Max(0, damageAmount);
        targetLayerMask = targetMask;
        damageOncePerTarget = oncePerTarget;
    }

    public void ConfigureDamage(int damageAmount, LayerMask targetMask, bool oncePerTarget, HitSourceType sourceType)
    {
        ConfigureDamage(damageAmount, targetMask, oncePerTarget);
        hitSourceType = sourceType;
    }

    public void ConfigureDebug(bool enabled)
    {
        debugMode = enabled;
    }

    private void TryDamage(Collider other, Vector3 hitPoint)
    {
        if (onlyWhenEnabled && !isActiveAndEnabled)
        {
            return;
        }

        if (damage <= 0 || !IsLayerIncluded(other.gameObject.layer, targetLayerMask))
        {
            return;
        }

        IDamageable damageable = FindDamageable(other.transform);
        if (damageable == null)
        {
            LogWarning($"No IDamageable found on {other.name}.");
            return;
        }

        if (!damageable.CanTakeDamage)
        {
            Log($"Target cannot take damage: {other.name}");
            return;
        }

        Transform targetRoot = other.transform.root != null ? other.transform.root : other.transform;
        int targetId = targetRoot.GetInstanceID();
        if (damageOncePerTarget && !damagedTargets.Add(targetId))
        {
            return;
        }

        Vector3 direction = (other.bounds.center - transform.position).normalized;
        DamageInfo damageInfo = new DamageInfo(damage, gameObject, gameObject, hitPoint, direction, damageType, hitSourceType);
        damageable.TakeDamage(damageInfo);
        Log($"Damaged {targetRoot.name}. Damage={damage}, Type={damageType}, Source={damageInfo.hitSourceType}");
    }

    private static IDamageable FindDamageable(Transform target)
    {
        IDamageable damageable = target.GetComponent<IDamageable>();
        if (damageable != null)
        {
            return damageable;
        }

        damageable = target.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            return damageable;
        }

        return target.GetComponentInChildren<IDamageable>(true);
    }

    private static bool IsLayerIncluded(int layer, LayerMask layerMask)
    {
        return (layerMask.value & (1 << layer)) != 0;
    }

    private void Log(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[DamageDealer] {message}", this);
        }
    }

    private void LogWarning(string message)
    {
        if (debugMode)
        {
            Debug.LogWarning($"[DamageDealer] {message}", this);
        }
    }
}
