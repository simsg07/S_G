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

    [Header("Hit Feedback")]
    [SerializeField] private float hitBlinkDuration = 0.3f;
    [SerializeField] private float hitBlinkInterval = 0.08f;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;

    private Rigidbody body;
    private Renderer[] renderers;
    private Coroutine hitBlinkCoroutine;

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
        StartHitBlink();

        if (!infiniteHealth)
        {
            currentHp = Mathf.Max(0, currentHp - damage);
        }

        if (debugMode)
        {
            Debug.Log($"[PlayerDamageReceiver] Damage={damage}, InfiniteHealth={infiniteHealth}, HP={currentHp}/{maxHp}", this);
        }

        if (!infiniteHealth && currentHp <= 0)
        {
            Respawn();
        }
    }

    public void Respawn()
    {
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
        CacheReferences();

        if (hitBlinkCoroutine != null)
        {
            StopCoroutine(hitBlinkCoroutine);
            SetRenderersEnabled(true);
        }

        hitBlinkCoroutine = StartCoroutine(HitBlinkRoutine());
    }

    private IEnumerator HitBlinkRoutine()
    {
        float elapsed = 0f;
        bool visible = true;

        while (elapsed < hitBlinkDuration)
        {
            visible = !visible;
            SetRenderersEnabled(visible);
            yield return new WaitForSeconds(hitBlinkInterval);
            elapsed += hitBlinkInterval;
        }

        SetRenderersEnabled(true);
        hitBlinkCoroutine = null;
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
