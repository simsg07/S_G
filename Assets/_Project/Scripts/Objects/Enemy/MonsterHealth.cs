using UnityEngine;

[DisallowMultipleComponent]
// Optional shared health component used by component-based monster prefabs.
public class MonsterHealth : MonoBehaviour, IDamageable
{
    [Header("Health Settings")]
    public bool enableHealth = true;
    public int maxHp = 1;
    public int currentHp = 1;
    public bool destroyOnDeath;
    public float destroyDelay = 2f;
    public bool disableColliderOnDeath = true;

    [Header("Debug")]
    public bool debugMode;

    public bool IsDead => enableHealth && currentHp <= 0;

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
