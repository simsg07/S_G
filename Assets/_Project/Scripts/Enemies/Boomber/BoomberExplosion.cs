using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BoomberExplosion : MonoBehaviour
{
    [Header("Explosion - Designer Settings")]
    [Tooltip("Turn off to disable Boomber's explosion behavior for testing.")]
    public bool enableExplosion = true;
    [Tooltip("Seconds between attack start and explosion.")]
    public float fuseDuration = 1f;
    [Tooltip("Explosion damage radius in world units.")]
    public float explosionRadius = 1.25f;
    [Tooltip("Damage applied to the Player and supported breakable objects.")]
    public int explosionDamage = 2;
    [Tooltip("Destroy Boomber after the explosion finishes.")]
    public bool destroyOnExplosion = true;
    [Tooltip("Delay before destroyOnExplosion removes the GameObject.")]
    public float destroyDelay = 0.25f;

    [Header("Optional Breakable Objects")]
    [Tooltip("Only objects that explicitly implement IExplosionBreakable are affected.")]
    public bool affectBreakableObjects = true;
    [Tooltip("Set this to the layer used by optional BreakableObject instances. Zero disables the overlap query.")]
    public LayerMask breakableLayerMask;
    [Tooltip("Also send DamageInfo to HitReceiver components inside the explosion radius.")]
    public bool affectHitReceivers = true;

    [Header("Debug")]
    [Tooltip("Print explosion state changes in the Console.")]
    public bool debugMode;
    [Tooltip("Draw the explosion radius when this prefab is selected in Scene view.")]
    public bool showGizmos = true;

    private bool exploded;
    private bool isExploding;
    private Coroutine countdownRoutine;
    private Transform pendingPlayerTarget;

    public bool HasExploded => exploded;
    public bool IsExploding => isExploding;
    public event Action OnExploded;

    public void ConfigureDamage(int damage)
    {
        explosionDamage = Mathf.Max(0, damage);
    }

    public void ResetExplosion()
    {
        if (countdownRoutine != null)
        {
            StopCoroutine(countdownRoutine);
            countdownRoutine = null;
        }

        exploded = false;
        isExploding = false;
        pendingPlayerTarget = null;
    }

    public bool StartExplosion(Transform playerTarget)
    {
        if (!enableExplosion || isExploding || exploded)
        {
            return false;
        }

        isExploding = true;
        pendingPlayerTarget = playerTarget;
        countdownRoutine = StartCoroutine(ExplosionCountdown());
        Log($"Countdown started. Delay={fuseDuration:0.##}s");
        return true;
    }

    public bool Explode(Transform playerTarget)
    {
        if (!enableExplosion || exploded)
        {
            return false;
        }

        isExploding = false;
        exploded = true;
        countdownRoutine = null;
        Log("Explosion triggered");
        DamageBreakableObjects();

        if (playerTarget == null)
        {
            Log("Explosion damage skipped: Player target missing");
        }
        else
        {
            Vector3 delta = playerTarget.position - transform.position;
            delta.z = 0f;
            if (delta.sqrMagnitude > explosionRadius * explosionRadius)
            {
                Log("Explosion damage skipped: Player outside explosion radius");
            }
            else
            {
                IDamageable damageable = FindDamageable(playerTarget);
                if (damageable == null)
                {
                    Log("Explosion damage skipped: No IDamageable found on Player");
                }
                else
                {
                    damageable.TakeDamage(explosionDamage);
                    Log($"Explosion damage applied: {explosionDamage}");
                }
            }
        }

        Log("Exploded");
        OnExploded?.Invoke();
        if (destroyOnExplosion)
        {
            Destroy(gameObject, destroyDelay);
        }

        return true;
    }

    private IEnumerator ExplosionCountdown()
    {
        if (fuseDuration > 0f)
        {
            yield return new WaitForSeconds(fuseDuration);
        }

        Explode(pendingPlayerTarget);
    }

    private void DamageBreakableObjects()
    {
        if (!affectBreakableObjects || breakableLayerMask.value == 0 || explosionRadius <= 0f)
        {
            return;
        }

        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            explosionRadius,
            breakableLayerMask,
            QueryTriggerInteraction.Ignore);
        HashSet<IExplosionBreakable> damaged = new HashSet<IExplosionBreakable>();
        HashSet<HitReceiver> hitReceivers = new HashSet<HitReceiver>();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null)
            {
                continue;
            }

            MonoBehaviour[] behaviours = hit.GetComponentsInParent<MonoBehaviour>(true);
            for (int j = 0; j < behaviours.Length; j++)
            {
                if (behaviours[j] is IExplosionBreakable breakable && damaged.Add(breakable))
                {
                    breakable.ReceiveExplosion(explosionDamage, transform.position);
                    Log($"Breakable hit: {behaviours[j].name}");
                }
            }

            if (!affectHitReceivers)
            {
                continue;
            }

            HitReceiver hitReceiver = FindHitReceiver(hit.transform);
            if (hitReceiver == null || !hitReceiver.CanAcceptHitSource(HitSourceType.BoomberExplosion) || !hitReceivers.Add(hitReceiver))
            {
                continue;
            }

            Vector3 hitPoint = hit.ClosestPoint(transform.position);
            Vector3 hitDirection = (hitReceiver.transform.position - transform.position).normalized;
            DamageInfo damageInfo = new DamageInfo(
                Mathf.Max(1, explosionDamage),
                gameObject,
                gameObject,
                hitPoint,
                hitDirection,
                DamageType.Explosion,
                HitSourceType.BoomberExplosion);

            Log($"HitReceiver found: {hitReceiver.name}");
            hitReceiver.RegisterHit(damageInfo);
            Log($"RegisterHit sent. Source: {damageInfo.hitSourceType}");
        }
    }

    private static HitReceiver FindHitReceiver(Transform target)
    {
        if (target == null)
        {
            return null;
        }

        HitReceiver hitReceiver = target.GetComponent<HitReceiver>();
        if (hitReceiver != null)
        {
            return hitReceiver;
        }

        hitReceiver = target.GetComponentInParent<HitReceiver>();
        if (hitReceiver != null)
        {
            return hitReceiver;
        }

        return target.GetComponentInChildren<HitReceiver>(true);
    }

    private static IDamageable FindDamageable(Transform target)
    {
        IDamageable damageable = target.GetComponent<IDamageable>();
        if (damageable != null)
        {
            return damageable;
        }

        damageable = target.GetComponentInParent<IDamageable>();
        return damageable ?? target.GetComponentInChildren<IDamageable>(true);
    }

    private void OnValidate()
    {
        fuseDuration = Mathf.Max(0f, fuseDuration);
        explosionRadius = Mathf.Max(0f, explosionRadius);
        explosionDamage = Mathf.Max(0, explosionDamage);
        destroyDelay = Mathf.Max(0f, destroyDelay);
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.25f, 0.1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }

    private void Log(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[BoomberExplosion] {message}", this);
        }
    }
}
