using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

[DisallowMultipleComponent]
public class PlayerDamageReceiver : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField] private bool infiniteHealth = true;
    [SerializeField] private int maxHp = 999;
    [SerializeField] private int currentHp = 999;

    [Header("Respawn")]
    [SerializeField] private Transform respawnPoint;
    [SerializeField] private Key manualRespawnKey = Key.T;
    [SerializeField] private bool respawnImmediatelyOnDeath;
    [SerializeField] private float respawnDelay = 0.3f;

    [Header("Hit Feedback")]
    [SerializeField] private float hitBlinkDuration = 0.3f;
    [SerializeField] private float hitBlinkInterval = 0.08f;
    [SerializeField] private float deathBlinkDuration = 0.8f;
    [SerializeField] private float deathBlinkInterval = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;

    private Rigidbody body;
    private Renderer[] renderers;
    private Coroutine hitBlinkCoroutine;
    private Coroutine deathRoutine;
    private bool isDead;

    public int CurrentHp => currentHp;

    private void Awake()
    {
        CacheReferences();
        currentHp = Mathf.Clamp(currentHp, 0, maxHp);
    }

    private void OnValidate()
    {
        maxHp = Mathf.Max(1, maxHp);
        currentHp = Mathf.Clamp(currentHp, 0, maxHp);
        hitBlinkDuration = Mathf.Max(0f, hitBlinkDuration);
        hitBlinkInterval = Mathf.Max(0.01f, hitBlinkInterval);
        deathBlinkDuration = Mathf.Max(0f, deathBlinkDuration);
        deathBlinkInterval = Mathf.Max(0.01f, deathBlinkInterval);
        respawnDelay = Mathf.Max(0f, respawnDelay);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard[manualRespawnKey].wasPressedThisFrame)
        {
            Respawn();
        }
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0)
        {
            return;
        }

        Debug.Log($"[PlayerDamageReceiver] Player hit. Damage={damage}", this);

        if (infiniteHealth)
        {
            StartHitBlink();
            if (debugMode)
            {
                Debug.Log($"[PlayerDamageReceiver] Damage={damage}, InfiniteHealth=True, HP={currentHp}/{maxHp}", this);
            }

            return;
        }

        currentHp = Mathf.Max(0, currentHp - damage);

        if (debugMode)
        {
            Debug.Log($"[PlayerDamageReceiver] Damage={damage}, InfiniteHealth={infiniteHealth}, HP={currentHp}/{maxHp}", this);
        }

        if (!infiniteHealth && currentHp <= 0)
        {
            HandleDeath();
            return;
        }

        StartHitBlink();
    }

    public void Respawn()
    {
        StopBlinkCoroutines(true);
        SetRenderersEnabled(true);

        if (respawnPoint != null)
        {
            transform.position = respawnPoint.position;
        }
        else
        {
            Debug.LogWarning("[PlayerDamageReceiver] Respawn Point is not assigned.", this);
        }

        ResetVelocity();
        currentHp = maxHp;
        isDead = false;

        Debug.Log("[PlayerDamageReceiver] Player Respawned, HP restored", this);

        if (debugMode)
        {
            Debug.Log($"[PlayerDamageReceiver] Respawned. HP={currentHp}/{maxHp}", this);
        }
    }

    private void ResetVelocity()
    {
        CacheReferences();

        if (body == null)
        {
            return;
        }

#if UNITY_6000_0_OR_NEWER
        body.linearVelocity = Vector3.zero;
#else
        body.velocity = Vector3.zero;
#endif
        body.angularVelocity = Vector3.zero;
    }

    private void CacheReferences()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody>();
        }

        if (renderers == null || renderers.Length == 0)
        {
            renderers = GetComponentsInChildren<Renderer>(true);
        }
    }

    private void StartHitBlink()
    {
        if (isDead)
        {
            return;
        }

        CacheReferences();

        if (hitBlinkCoroutine != null)
        {
            StopCoroutine(hitBlinkCoroutine);
            SetRenderersEnabled(true);
        }

        hitBlinkCoroutine = StartCoroutine(HitBlinkRoutine());
    }

    private void HandleDeath()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        Debug.Log("[PlayerDamageReceiver] Player Dead", this);

        StopBlinkCoroutines(true);

        if (respawnImmediatelyOnDeath)
        {
            Respawn();
            return;
        }

        deathRoutine = StartCoroutine(DeathAndRespawnRoutine());
    }

    private IEnumerator DeathAndRespawnRoutine()
    {
        yield return BlinkRoutine(deathBlinkDuration, deathBlinkInterval);

        if (respawnDelay > 0f)
        {
            yield return new WaitForSeconds(respawnDelay);
        }

        deathRoutine = null;
        Respawn();
    }

    private IEnumerator HitBlinkRoutine()
    {
        yield return BlinkRoutine(hitBlinkDuration, hitBlinkInterval);
        hitBlinkCoroutine = null;
    }

    private IEnumerator BlinkRoutine(float duration, float interval)
    {
        float elapsed = 0f;
        bool visible = true;

        while (elapsed < duration)
        {
            visible = !visible;
            SetRenderersEnabled(visible);
            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }

        SetRenderersEnabled(true);
    }

    private void StopBlinkCoroutines(bool includeDeathRoutine)
    {
        if (hitBlinkCoroutine != null)
        {
            StopCoroutine(hitBlinkCoroutine);
            hitBlinkCoroutine = null;
        }

        if (includeDeathRoutine && deathRoutine != null)
        {
            StopCoroutine(deathRoutine);
            deathRoutine = null;
        }

        SetRenderersEnabled(true);
    }

    private void SetRenderersEnabled(bool value)
    {
        if (renderers == null)
        {
            return;
        }

        foreach (Renderer targetRenderer in renderers)
        {
            if (targetRenderer != null)
            {
                targetRenderer.enabled = value;
            }
        }
    }
}
