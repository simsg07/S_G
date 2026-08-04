using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class EyeballFlyHealth : MonoBehaviour, IAttackable3D
{
    [SerializeField] private int maxHealth = 3;
    [SerializeField] private float destroyDelay = 2f;
    [SerializeField] private float fallSpeed = 1.8f;
    [SerializeField] private EyeballFlyAI ai;

    private int currentHealth;
    private bool dead;

    public int CurrentHealth => currentHealth;
    public bool IsDead => dead;

    private void Awake()
    {
        CacheReferences();
        ResetHealth();
    }

    private void OnEnable()
    {
        CacheReferences();
        ResetHealth();
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        destroyDelay = Mathf.Max(0f, destroyDelay);
        fallSpeed = Mathf.Max(0f, fallSpeed);
        CacheReferences();
    }

    public bool TakeAttack()
    {
        return TakeDamage(1);
    }

    public bool TakeDamage(int amount)
    {
        if (!Application.isPlaying || dead)
        {
            return false;
        }

        currentHealth = Mathf.Max(0, currentHealth - Mathf.Max(1, amount));
        if (currentHealth <= 0)
        {
            Die();
        }

        return true;
    }

    public void ResetHealth()
    {
        dead = false;
        currentHealth = Mathf.Max(1, maxHealth);
        gameObject.SetActive(true);
        if (ai != null)
        {
            ai.ResetMonster();
        }
    }

    private void CacheReferences()
    {
        if (ai == null)
        {
            ai = GetComponent<EyeballFlyAI>();
        }
    }

    private void Die()
    {
        if (dead)
        {
            return;
        }

        dead = true;
        if (ai != null)
        {
            ai.Die();
        }

        StartCoroutine(FallAndDestroy());
    }

    private IEnumerator FallAndDestroy()
    {
        float endTime = Time.time + destroyDelay;
        while (Time.time < endTime)
        {
            transform.position += Vector3.down * fallSpeed * Time.deltaTime;
            yield return null;
        }

        gameObject.SetActive(false);
    }
}
