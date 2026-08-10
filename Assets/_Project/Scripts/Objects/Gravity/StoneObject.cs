using System.Collections;
using UnityEngine;

public enum StoneObjectState
{
    IDLE,
    FALLING,
    BROKEN
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class StoneObject : MonoBehaviour, IGravityActivatable3D
{
    [Header("State")]
    [SerializeField] private StoneObjectState currentState = StoneObjectState.IDLE;
    [SerializeField, HideInInspector] private bool isDropped;
    [SerializeField, HideInInspector] private bool isBroken;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundLayerMask;

    [Header("Break")]
    [SerializeField] private bool breakOnGroundHit = true;
    [SerializeField] private float destroyDelay = 0.5f;

    [Header("Switch Interaction")]
    [SerializeField] private bool canActivateSwitch = true;
    [SerializeField] private bool breakAfterSwitchContact = true;

    [Header("References")]
    [SerializeField] private GravityObject3D gravityObject;
    [SerializeField] private DamageDealer damageDealer;
    [SerializeField] private GravityObjectDamageDealer gravityObjectDamageDealer;
    [SerializeField] private PausablePhysicsObject pausablePhysicsObject;
    [SerializeField] private BreakableObject3D breakableObject;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Collider mainCollider;
    [SerializeField] private Renderer[] renderers;

    [Header("Debug")]
    [SerializeField] private bool debugMode;
    [SerializeField] private bool logSwitchActivation;

    private GravityObjectSpawner ownerSpawner;
    private bool wasSpawnedBySpawner;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private Coroutine removeRoutine;
    private Coroutine pendingGroundBreakRoutine;
    private bool hasTriggeredSwitch;
    private bool isBreaking;
    private IMarkState3D markState;

    public StoneObjectState CurrentState => currentState;
    public bool IsFalling => currentState == StoneObjectState.FALLING;
    public bool IsBroken => currentState == StoneObjectState.BROKEN;

    private void Awake()
    {
        CacheReferences();
        CaptureStartTransform();
        currentState = isBroken ? StoneObjectState.BROKEN
            : isDropped ? StoneObjectState.FALLING
            : StoneObjectState.IDLE;
        SetDamageEnabled(false);
    }

    private void OnValidate()
    {
        destroyDelay = Mathf.Max(0f, destroyDelay);
        CacheReferences();
    }

    public void ConfigureDataDrivenObject(bool breakOnGroundHitValue, float destroyDelayValue, bool debugModeValue)
    {
        breakOnGroundHit = breakOnGroundHitValue;
        destroyDelay = Mathf.Max(0f, destroyDelayValue);
        debugMode = debugModeValue;
    }

    public void ApplyStoneData(ObjectData data)
    {
        if (data == null)
        {
            return;
        }

        ConfigureDataDrivenObject(
            data.breakOnGroundHit || data.breakMode == ObjectBreakMode.OnGroundHit,
            data.destroyDelay,
            data.debugMode);

        if (gravityObjectDamageDealer != null)
        {
            gravityObjectDamageDealer.ApplyGravityObjectData(data);
        }
    }

    public void SetOwnerSpawner(GravityObjectSpawner spawner)
    {
        ownerSpawner = spawner;
        wasSpawnedBySpawner = spawner != null;
    }

    [ContextMenu("TestDrop")]
    public void TestDrop()
    {
        TriggerDrop();
    }

    public void TriggerDrop()
    {
        if (isDropped || isBroken)
        {
            return;
        }

        isDropped = true;
        currentState = StoneObjectState.FALLING;
        SetDamageEnabled(true);
        if (gravityObject != null)
        {
            gravityObject.TriggerDrop();
        }

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        Log("Dropped.");
    }

    public bool TryActivateGravity(GameObject source)
    {
        if (currentState != StoneObjectState.IDLE || isDropped || isBroken) return false;
        ConnectedObjectLink support = GetComponentInParent<ConnectedObjectLink>();
        if (support != null) return support.ReleaseConnectedObject();
        TriggerDrop();
        return currentState == StoneObjectState.FALLING;
    }

    [ContextMenu("ResetStone")]
    public void ResetStone()
    {
        if (removeRoutine != null)
        {
            StopCoroutine(removeRoutine);
            removeRoutine = null;
        }

        if (pendingGroundBreakRoutine != null)
        {
            StopCoroutine(pendingGroundBreakRoutine);
            pendingGroundBreakRoutine = null;
        }

        isDropped = false;
        isBroken = false;
        currentState = StoneObjectState.IDLE;
        hasTriggeredSwitch = false;
        isBreaking = false;
        transform.SetPositionAndRotation(startPosition, startRotation);
        ClearVelocity();

        if (gravityObject != null)
        {
            gravityObject.ResetGravityObject();
        }

        if (breakableObject != null)
        {
            breakableObject.ResetBreakable();
        }

        if (mainCollider != null)
        {
            mainCollider.enabled = true;
        }

        SetRenderersEnabled(true);
        SetDamageEnabled(false);
        Log("Reset.");
    }

    [ContextMenu("ValidateStoneSetup")]
    public void ValidateStoneSetup()
    {
        CacheReferences();
        LogComponent("GravityObject3D", gravityObject);
        LogComponent("DamageDealer", damageDealer);
        LogComponent("GravityObjectDamageDealer", gravityObjectDamageDealer);
        LogComponent("PausablePhysicsObject", pausablePhysicsObject);
        LogComponent("BreakableObject3D", breakableObject);
        LogComponent("Rigidbody", rb);
        LogComponent("Collider", mainCollider);
        Log($"Renderers: {(renderers != null ? renderers.Length : 0)}");
    }

    public void BreakStone()
    {
        if (isBroken || isBreaking)
        {
            return;
        }

        isBreaking = true;
        isBroken = true;
        isDropped = false;
        currentState = StoneObjectState.BROKEN;
        if (pendingGroundBreakRoutine != null)
        {
            StopCoroutine(pendingGroundBreakRoutine);
            pendingGroundBreakRoutine = null;
        }
        SetDamageEnabled(false);
        ClearVelocity();

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        if (breakableObject != null)
        {
            breakableObject.BreakObject();
        }
        else if (mainCollider != null)
        {
            mainCollider.enabled = false;
        }

        if (removeRoutine != null)
        {
            StopCoroutine(removeRoutine);
        }

        if (Application.isPlaying)
        {
            removeRoutine = StartCoroutine(RemoveAfterDelay());
        }
        else
        {
            NotifySpawnerObjectFinished();
        }

        Log("Broken.");
    }

    private void NotifySpawnerObjectFinished()
    {
        if (wasSpawnedBySpawner && ownerSpawner != null)
        {
            ownerSpawner.NotifySpawnedObjectFinished(gameObject);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (markState != null && markState.IsMarked) return;
        if (!isDropped || isBroken || collision == null || collision.collider == null)
        {
            return;
        }

        if (TryHandleSwitchContact(collision.collider))
        {
            return;
        }

        if (!breakOnGroundHit)
        {
            return;
        }

        if (IsPlayerHit(collision.collider))
        {
            return;
        }

        if (IsGround(collision.collider))
        {
            QueueGroundBreak();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (markState != null && markState.IsMarked) return;
        if (!isDropped || isBroken || other == null)
        {
            return;
        }

        if (TryHandleSwitchContact(other)) return;
        if (!breakOnGroundHit || IsPlayerHit(other)) return;

        if (IsGround(other))
        {
            QueueGroundBreak();
        }
    }

    private bool TryHandleSwitchContact(Collider contact)
    {
        if (!canActivateSwitch || !IsFalling || hasTriggeredSwitch || contact == null) return false;

        ISwitchActivation3D switchReceiver = contact.GetComponent<ISwitchActivation3D>()
            ?? contact.GetComponentInParent<ISwitchActivation3D>();
        if (switchReceiver == null) return false;

        hasTriggeredSwitch = true;
        bool accepted = switchReceiver.TryActivate(SwitchActivationSource.Stone, gameObject);
        if (logSwitchActivation)
            Debug.Log($"[StoneObject] Switch contact. Accepted={accepted}, Target={contact.name}", this);

        if (breakAfterSwitchContact) BreakStone();
        return true;
    }

    private void QueueGroundBreak()
    {
        if (pendingGroundBreakRoutine == null && !isBreaking)
            pendingGroundBreakRoutine = StartCoroutine(ConfirmGroundBreakAfterPhysicsStep());
    }

    private IEnumerator ConfirmGroundBreakAfterPhysicsStep()
    {
        yield return new WaitForFixedUpdate();
        pendingGroundBreakRoutine = null;
        if (markState != null && markState.IsMarked) yield break;
        if (IsFalling && !hasTriggeredSwitch) BreakStone();
    }

    private bool IsGround(Collider other)
    {
        if (groundLayerMask.value == 0)
        {
            return false;
        }

        return (groundLayerMask.value & (1 << other.gameObject.layer)) != 0;
    }

    private void CacheReferences()
    {
        if (markState == null)
        {
            markState = GetComponent<IMarkState3D>();
        }
        if (gravityObject == null)
        {
            gravityObject = GetComponent<GravityObject3D>();
        }

        if (damageDealer == null)
        {
            damageDealer = GetComponent<DamageDealer>();
        }

        if (gravityObjectDamageDealer == null)
        {
            gravityObjectDamageDealer = GetComponent<GravityObjectDamageDealer>();
        }

        if (pausablePhysicsObject == null)
        {
            pausablePhysicsObject = GetComponent<PausablePhysicsObject>();
        }

        if (breakableObject == null)
        {
            breakableObject = GetComponent<BreakableObject3D>();
        }

        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (mainCollider == null)
        {
            mainCollider = GetComponent<Collider>();
        }

        if (renderers == null || renderers.Length == 0)
        {
            renderers = GetComponentsInChildren<Renderer>(true);
        }
    }

    private void CaptureStartTransform()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    private void SetDamageEnabled(bool enabled)
    {
        if (damageDealer != null)
        {
            damageDealer.enabled = enabled;
            if (!enabled)
            {
                damageDealer.ClearDamagedTargets();
            }
        }

        if (gravityObjectDamageDealer != null)
        {
            // DamageDealer is the shared Player/Monster damage path. Keep the legacy
            // player-only dealer only as a fallback to avoid double damage.
            gravityObjectDamageDealer.enabled = enabled && damageDealer == null;
        }
    }

    private bool IsPlayerHit(Collider target)
    {
        return gravityObjectDamageDealer != null && gravityObjectDamageDealer.IsPlayerCollider(target);
    }

    private void ClearVelocity()
    {
        if (rb == null || rb.isKinematic)
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

    private IEnumerator RemoveAfterDelay()
    {
        if (destroyDelay > 0f)
        {
            yield return new WaitForSeconds(destroyDelay);
        }

        NotifySpawnerObjectFinished();
        gameObject.SetActive(false);
    }

    private void SetRenderersEnabled(bool enabled)
    {
        if (renderers == null)
        {
            return;
        }

        foreach (Renderer targetRenderer in renderers)
        {
            if (targetRenderer != null)
            {
                targetRenderer.enabled = enabled;
            }
        }
    }

    private void LogComponent(string label, Object component)
    {
        if (!debugMode)
        {
            return;
        }

        if (component != null)
        {
            Debug.Log($"[StoneObject] {label} found: {component.GetType().Name}", this);
            return;
        }

        Debug.LogWarning($"[StoneObject] {label} not assigned.", this);
    }

    private void Log(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[StoneObject] {message}", this);
        }
    }
}
