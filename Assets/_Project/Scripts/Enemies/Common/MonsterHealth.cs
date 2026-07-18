using UnityEngine;

[DisallowMultipleComponent]
// Optional shared health component used by component-based monster prefabs.
public class MonsterHealth : MonoBehaviour, IDamageable
{
    [Header("Health - Designer Settings")]
    [Tooltip("Turn off only for test monsters that should ignore damage.")]
    public bool enableHealth = true;
    [Tooltip("Maximum HP. Designers can tune this per monster prefab.")]
    public int maxHp = 1;
    [Tooltip("Runtime value. Read-only during Play Mode; use maxHp for prefab tuning.")]
    public int currentHp = 1;
    [Tooltip("Destroy this monster GameObject after death.")]
    public bool destroyOnDeath;
    [Tooltip("Delay before destroyOnDeath removes the monster.")]
    public float destroyDelay = 2f;
    [Tooltip("Disable all child colliders after death so the body no longer blocks gameplay.")]
    public bool disableColliderOnDeath = true;

    [Header("Debug")]
    [Tooltip("Print health changes in the Console for debugging.")]
    public bool debugMode;

    public bool IsDead => enableHealth && currentHp <= 0;
    public bool CanTakeDamage => enableHealth && !IsDead;

    public void TakeDamage(int damage)
    {
        if (!enableHealth)
        {
            Log("Health disabled. Damage ignored.");
            return;
        }

        if (damage <= 0 || IsDead)
        {
            return;
        }

        currentHp = Mathf.Max(0, currentHp - damage);
        Log($"TakeDamage={damage}, HP={currentHp}/{maxHp}");

        if (currentHp <= 0)
        {
            OnDead();
        }
    }

    public void TakeDamage(DamageInfo damageInfo)
    {
        TakeDamage(damageInfo.damageAmount);
    }

    public void ResetHealth()
    {
        currentHp = maxHp;
    }

    private void OnDead()
    {
        Log("Dead");

        if (disableColliderOnDeath)
        {
            Collider[] colliders = GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }
        }

        if (destroyOnDeath)
        {
            Destroy(gameObject, destroyDelay);
        }
    }

    private void OnValidate()
    {
        maxHp = Mathf.Max(1, maxHp);
        currentHp = Mathf.Clamp(currentHp, 0, maxHp);
        destroyDelay = Mathf.Max(0f, destroyDelay);
    }

    private void Log(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[MonsterHealth] {message}", this);
        }
    }
}
