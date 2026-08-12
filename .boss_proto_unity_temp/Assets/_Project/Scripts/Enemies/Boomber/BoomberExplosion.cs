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
    [Tooltip("Final part of the existing fuse reserved for the visual leap pose.")]
    public float attackLeapDuration = 0.35f;
    [Tooltip("Time allowed for the explosion sprite sequence before death/removal.")]
    public float explosionVisualDuration = 0.625f;
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
    private Coroutine finishRoutine;
    private Transform pendingPlayerTarget;
    private bool pausedByShutter;
    private float remainingExplosionDelay;
    private Collider contactCandidate;

    public bool HasExploded => exploded;
    public bool IsExploding => isExploding;
    public bool IsPausedByShutter => pausedByShutter;
    public float RemainingExplosionDelay => remainingExplosionDelay;
    public event Action OnAttackLeapStarted;
    public event Action OnExplosionStarted;
    public event Action OnExploded;

    public void ConfigureDamage(int damage)
    {
        explosionDamage = Mathf.Max(0, damage);
    }

    public void RegisterContactCandidate(Collider candidate)
    {
        if (candidate != null && WorldDamageFilter3D.CanAffect(this, candidate)) contactCandidate = candidate;
    }

    public void ResetExplosion()
    {
        if (countdownRoutine != null)
        {
            StopCoroutine(countdownRoutine);
            countdownRoutine = null;
        }

        if (finishRoutine != null)
        {
            StopCoroutine(finishRoutine);
            finishRoutine = null;
        }

        exploded = false;
        isExploding = false;
        pendingPlayerTarget = null;
        pausedByShutter = false;
        remainingExplosionDelay = 0f;
        contactCandidate = null;
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
        OnExplosionStarted?.Invoke();
        Log("Explosion triggered");
        DamageBreakableObjects();

        Component playerComponent = playerTarget != null ? playerTarget : null;
        if (!MonsterWorldSimulationGate3D.AllowsPlayerInteraction(this)
            || (playerComponent != null && !WorldDamageFilter3D.CanAffect(this, playerComponent)))
        {
            Log("Explosion Player damage skipped: monster is outside the active World");
        }
        else if (playerTarget == null)
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

        Log("Explosion damage applied once; visual sequence started");
        finishRoutine = StartCoroutine(FinishExplosionAfterVisual());

        return true;
    }

    private IEnumerator ExplosionCountdown()
    {
        float leapDuration = Mathf.Min(Mathf.Max(0f, attackLeapDuration), Mathf.Max(0f, fuseDuration));
        float prepareDuration = Mathf.Max(0f, fuseDuration - leapDuration);
        remainingExplosionDelay = prepareDuration + leapDuration;
        if (prepareDuration > 0f)
        {
            yield return CountdownSegment(prepareDuration);
        }

        if (exploded) yield break;
        OnAttackLeapStarted?.Invoke();
        if (leapDuration > 0f) yield return CountdownSegment(leapDuration);

        if (!exploded) Explode(pendingPlayerTarget);
    }

    private IEnumerator CountdownSegment(float duration)
    {
        float remaining = Mathf.Max(0f, duration);
        while (remaining > 0f && !exploded)
        {
            if (!pausedByShutter)
            {
                float delta = Time.deltaTime;
                if (SafeMath3D.IsFinite(delta) && delta > 0f)
                {
                    remaining = Mathf.Max(0f, remaining - delta);
                    remainingExplosionDelay = Mathf.Max(0f, remainingExplosionDelay - delta);
                }
            }
            yield return null;
        }
    }

    public bool CanBePausedByShutter()
    {
        return isActiveAndEnabled && isExploding && !exploded;
    }

    public void PauseByShutter()
    {
        if (!CanBePausedByShutter()) return;
        pausedByShutter = true;
        Log($"Explosion countdown paused. Remaining={remainingExplosionDelay:0.###}s");
    }

    public void ResumeByShutter()
    {
        if (!pausedByShutter) return;
        pausedByShutter = false;
        Log($"Explosion countdown resumed. Remaining={remainingExplosionDelay:0.###}s");
    }

    private IEnumerator FinishExplosionAfterVisual()
    {
        if (explosionVisualDuration > 0f) yield return new WaitForSeconds(explosionVisualDuration);
        finishRoutine = null;
        isExploding = false;
        Log("Explosion visual finished");
        OnExploded?.Invoke();
        if (destroyOnExplosion) Destroy(gameObject, destroyDelay);
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
                    if (!WorldDamageFilter3D.CanAffect(this, behaviours[j])) continue;
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
            if (!WorldDamageFilter3D.CanAffect(this, hitReceiver)) continue;

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


        DamageContactCandidate(hitReceivers);
    }

    private void DamageContactCandidate(HashSet<HitReceiver> hitReceivers)
    {
        Collider candidate = contactCandidate;
        contactCandidate = null;
        if (candidate == null || !candidate.enabled || !WorldDamageFilter3D.CanAffect(this, candidate)) return;

        Vector3 closestPoint = candidate.ClosestPoint(transform.position);
        Vector3 delta = closestPoint - transform.position;
        delta.z = 0f;
        if (delta.sqrMagnitude > explosionRadius * explosionRadius) return;

        HitReceiver receiver = FindHitReceiver(candidate.transform);
        if (receiver == null || !receiver.CanAcceptHitSource(HitSourceType.BoomberExplosion)
            || !hitReceivers.Add(receiver)) return;

        Vector3 hitDirection = (receiver.transform.position - transform.position).normalized;
        DamageInfo damageInfo = new DamageInfo(
            Mathf.Max(1, explosionDamage),
            gameObject,
            gameObject,
            closestPoint,
            hitDirection,
            DamageType.Explosion,
            HitSourceType.BoomberExplosion);
        receiver.RegisterHit(damageInfo);
        Log($"Contact candidate explosion hit: {receiver.name}");
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
        attackLeapDuration = Mathf.Clamp(attackLeapDuration, 0f, fuseDuration);
        explosionVisualDuration = Mathf.Max(0f, explosionVisualDuration);
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
