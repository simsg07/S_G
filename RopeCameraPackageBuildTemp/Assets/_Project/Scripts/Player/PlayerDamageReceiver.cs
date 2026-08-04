using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class PlayerDamageReceiver : MonoBehaviour, IDamageable
{
    public static event Action<PlayerDamageReceiver> PlayerDied;
    [Header("Health - Designer Settings")]
    [Tooltip("When true, damage only plays hit feedback and does not reduce HP.")]
    [SerializeField] private bool infiniteHealth = true;
    [Tooltip("Maximum Player HP when infiniteHealth is off.")]
    [SerializeField] private int maxHp = 999;
    [Tooltip("Runtime HP. Read-only during Play Mode; tune maxHp instead.")]
    [SerializeField] private int currentHp = 999;

    [Header("Respawn - Designer Settings")]
    [Tooltip("Optional point where the Player returns after manual respawn or death.")]
    [SerializeField] private Transform respawnPoint;
    [Tooltip("PlayerSpawnPoint ID used when respawnPoint is not assigned.")]
    [SerializeField] private string respawnPointId = "Default";
    [Tooltip("Keyboard key used for manual respawn during testing.")]
    [SerializeField] private Key manualRespawnKey = Key.T;
    [Tooltip("Respawn immediately when HP reaches zero instead of playing the death blink.")]
    [SerializeField] private bool respawnImmediatelyOnDeath;
    [Tooltip("Delay after death feedback before respawn.")]
    [SerializeField] private float respawnDelay = 0.3f;

    [Header("Respawn References")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Collider playerCollider;
    [Tooltip("Optional behaviours to disable between death and respawn.")]
    [SerializeField] private Behaviour[] behavioursToDisableWhileDead;

    [Header("Hit Feedback - Designer Settings")]
    [Tooltip("How long the Player blinks after taking non-lethal damage.")]
    [SerializeField] private float hitBlinkDuration = 0.3f;
    [Tooltip("Blink interval during hit feedback.")]
    [SerializeField] private float hitBlinkInterval = 0.08f;
    [Tooltip("How long the Player blinks after death before respawn.")]
    [SerializeField] private float deathBlinkDuration = 0.8f;
    [Tooltip("Blink interval during death feedback.")]
    [SerializeField] private float deathBlinkInterval = 0.1f;

    [Header("Debug")]
    [Tooltip("Print damage and respawn logs in the Console.")]
    [SerializeField] private bool debugMode = true;

    private Renderer[] renderers;
    private Coroutine hitBlinkCoroutine;
    private Coroutine deathRoutine;
    [SerializeField] private bool isDead;

    public int CurrentHp => currentHp;
    public bool CanTakeDamage => !isDead && (infiniteHealth || currentHp > 0);

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

    public void TakeDamage(DamageInfo damageInfo)
    {
        TakeDamage(damageInfo.damageAmount);
    }

    public void Respawn()
    {
        CameraAbilitySystem3D.ForceExitAllCameraModes();
        StopBlinkCoroutines(true);
        SetRenderersEnabled(true);

        bool checkpointLoadStarted = false;
        Transform targetRespawnPoint = ResolveCheckpointRespawnPoint(ref checkpointLoadStarted);
        if (targetRespawnPoint == null && !checkpointLoadStarted)
        {
            targetRespawnPoint = ResolveRespawnPoint();
        }
        if (targetRespawnPoint != null)
        {
            transform.position = targetRespawnPoint.position;
        }
        else if (!checkpointLoadStarted)
        {
            Debug.LogWarning("[PlayerDamageReceiver] Respawn Point is not assigned.", this);
        }

        ResetVelocity();
        if (TryGetComponent(out PlatformerPlayer3D movement))
        {
            movement.ResetJumpStateAfterTeleport();
        }
        SetDeathControlsEnabled(true);
        currentHp = maxHp;
        isDead = false;

        Debug.Log("[PlayerDamageReceiver] Player respawned.", this);

        if (debugMode)
        {
            Debug.Log($"[PlayerDamageReceiver] Respawned. HP={currentHp}/{maxHp}", this);
        }
    }

    private Transform ResolveCheckpointRespawnPoint(ref bool checkpointLoadStarted)
    {
        checkpointLoadStarted = false;
        if (!GameProgressSave3D.TryGetLastCheckpoint(out string checkpointScene, out string checkpointId))
        {
            return null;
        }

        string activeScene = SceneManager.GetActiveScene().name;
        if (!string.Equals(activeScene, checkpointScene, StringComparison.Ordinal))
        {
            checkpointLoadStarted = SceneLoader.TryLoadCheckpointRespawn(checkpointScene, checkpointId);
            if (!checkpointLoadStarted)
            {
                Debug.LogWarning($"[PlayerDamageReceiver] Could not load saved checkpoint '{checkpointScene}/{checkpointId}'. Using current scene Default spawn.", this);
            }
            return null;
        }

        Checkpoint3D checkpoint = SceneLoader.FindCheckpoint(checkpointId);
        if (checkpoint == null)
        {
            Debug.LogWarning($"[PlayerDamageReceiver] Saved checkpoint '{checkpointId}' is missing in scene '{activeScene}'. Using Default PlayerSpawnPoint.", this);
            return null;
        }

        return checkpoint.SpawnPosition;
    }

    [ContextMenu("Test Kill And Respawn")]
    public void TestKillAndRespawn()
    {
        KillAndRespawn();
    }

    [ContextMenu("Test Respawn")]
    public void TestRespawn()
    {
        Respawn();
    }

    [ContextMenu("Validate Respawn Setup")]
    public void ValidateRespawnSetup()
    {
        Transform targetRespawnPoint = ResolveRespawnPoint();
        if (targetRespawnPoint != null)
        {
            Debug.Log($"[PlayerDamageReceiver] Respawn point: {targetRespawnPoint.name} / {targetRespawnPoint.position}", this);
            return;
        }

        Debug.LogWarning("[PlayerDamageReceiver] Respawn point is not assigned and no Default PlayerSpawnPoint was found.", this);
    }

    public void KillAndRespawn()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        PlayerDied?.Invoke(this);
        Debug.Log("[PlayerDamageReceiver] KillAndRespawn called.", this);
        StopBlinkCoroutines(true);
        SetRenderersEnabled(true);
        ResetVelocity();
        SetDeathControlsEnabled(false);

        if (respawnDelay <= 0f || !Application.isPlaying)
        {
            Respawn();
            return;
        }

        deathRoutine = StartCoroutine(KillAndRespawnRoutine());
    }

    public void SetRespawnPoint(Transform newPoint)
    {
        respawnPoint = newPoint;
    }

    private IEnumerator KillAndRespawnRoutine()
    {
        yield return new WaitForSeconds(respawnDelay);
        deathRoutine = null;
        Respawn();
    }

    private Transform ResolveRespawnPoint()
    {
        if (respawnPoint != null)
        {
            LogRespawnPointFound(respawnPoint);
            return respawnPoint;
        }

        PlayerSpawnPoint[] spawnPoints = FindObjectsByType<PlayerSpawnPoint>(FindObjectsSortMode.None);
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i] != null &&
                spawnPoints[i].CanUseAsRespawnPoint &&
                spawnPoints[i].Matches(respawnPointId))
            {
                LogRespawnPointFound(spawnPoints[i].transform);
                return spawnPoints[i].transform;
            }
        }

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i] != null &&
                spawnPoints[i].CanUseAsRespawnPoint &&
                spawnPoints[i].IsDefaultSpawn)
            {
                LogRespawnPointFound(spawnPoints[i].transform);
                return spawnPoints[i].transform;
            }
        }

        return null;
    }

    private void ResetVelocity()
    {
        CacheReferences();

        if (rb == null)
        {
            return;
        }

#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = Vector3.zero;
#else
        rb.velocity = Vector3.zero;
#endif
        rb.angularVelocity = Vector3.zero;
    }

    private void CacheReferences()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (playerCollider == null)
        {
            playerCollider = GetComponent<Collider>();
        }

        if (renderers == null || renderers.Length == 0)
        {
            renderers = GetComponentsInChildren<Renderer>(true);
        }
    }

    private void SetDeathControlsEnabled(bool enabled)
    {
        if (behavioursToDisableWhileDead == null)
        {
            return;
        }

        for (int i = 0; i < behavioursToDisableWhileDead.Length; i++)
        {
            if (behavioursToDisableWhileDead[i] != null)
            {
                behavioursToDisableWhileDead[i].enabled = enabled;
            }
        }
    }

    private void LogRespawnPointFound(Transform target)
    {
        if (debugMode && target != null)
        {
            Debug.Log($"[PlayerDamageReceiver] RespawnPoint found: {target.name}", target);
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
        PlayerDied?.Invoke(this);
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
