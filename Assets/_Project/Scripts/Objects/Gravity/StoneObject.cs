using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class StoneObject : MonoBehaviour
{
    [Header("State")]
    [SerializeField] private bool isDropped;
    [SerializeField] private bool isBroken;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundLayerMask;

    [Header("Break")]
    [SerializeField] private bool breakOnGroundHit = true;
    [SerializeField] private float destroyDelay = 0.5f;

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
    [SerializeField] private bool debugMode = true;

    private GravityObjectSpawner ownerSpawner;
    private bool wasSpawnedBySpawner;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private Coroutine removeRoutine;

    public bool IsFalling => isDropped && !isBroken;
    public bool IsBroken => isBroken;

    private void Awake()
    {
        CacheReferences();
        CaptureStartTransform();
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

    [ContextMenu("ResetStone")]
    public void ResetStone()
    {
        if (removeRoutine != null)
        {
            StopCoroutine(removeRoutine);
            removeRoutine = null;
        }

        isDropped = false;
        isBroken = false;
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
        if (isBroken)
        {
            return;
        }

        isBroken = true;
        isDropped = false;
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
        if (!isDropped || isBroken || collision == null || collision.collider == null)
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
            BreakStone();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isDropped || isBroken || other == null || !breakOnGroundHit || IsPlayerHit(other))
        {
            return;
        }

        if (IsGround(other))
        {
            BreakStone();
        }
    }

    private bool IsGround(Collider other)
    {
        if (groundLayerMask.value == 0)
        {
            return true;
        }

        return (groundLayerMask.value & (1 << other.gameObject.layer)) != 0;
    }

    private void CacheReferences()
    {
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
            gravityObjectDamageDealer.enabled = enabled;
        }
    }

    private bool IsPlayerHit(Collider target)
    {
        return gravityObjectDamageDealer != null && gravityObjectDamageDealer.IsPlayerCollider(target);
    }

    private void ClearVelocity()
    {
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
