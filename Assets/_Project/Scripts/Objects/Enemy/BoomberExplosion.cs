using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BoomberExplosion : MonoBehaviour
{
    [Header("Explosion")]
    public bool enableExplosion = true;
    public float fuseDuration = 1f;
    public float explosionRadius = 1.25f;
    public int explosionDamage = 2;
    public bool destroyOnExplosion = true;
    public float destroyDelay = 0.25f;

    [Header("Optional Breakable Objects")]
    [Tooltip("Only objects that explicitly implement IExplosionBreakable are affected.")]
    public bool affectBreakableObjects = true;
    [Tooltip("Set this to the layer used by optional BreakableObject instances. Zero disables the overlap query.")]
    public LayerMask breakableLayerMask;

    [Header("Debug")]
    public bool debugMode;
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
        }
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
