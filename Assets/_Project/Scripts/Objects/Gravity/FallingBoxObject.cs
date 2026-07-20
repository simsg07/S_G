using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class FallingBoxObject : MonoBehaviour
{
    [Header("State")]
    [SerializeField] private bool isDropped;
    [SerializeField] private bool isLanded;
    [InspectorName("Can Become Platform")]
    [SerializeField] private bool remainAsPlatformOnGround = true;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundLayerMask;

    [Header("References")]
    [SerializeField] private GravityObject3D gravityObject;
    [SerializeField] private DamageDealer damageDealer;
    [SerializeField] private GravityObjectDamageDealer gravityObjectDamageDealer;
    [SerializeField] private PausablePhysicsObject pausablePhysicsObject;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Collider mainCollider;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;

    private GravityObjectSpawner ownerSpawner;
    private bool wasSpawnedBySpawner;
    private Vector3 startPosition;
    private Quaternion startRotation;

    public bool IsFalling => isDropped && !isLanded;
    public bool HasLanded => isLanded;

    private void Awake()
    {
        CacheReferences();
        CaptureStartTransform();
        SetDamageEnabled(false);
    }

    private void OnValidate()
    {
        CacheReferences();
    }

    public void ConfigureDataDrivenObject(bool remainAsPlatformOnGroundValue, bool debugModeValue)
    {
        remainAsPlatformOnGround = remainAsPlatformOnGroundValue;
        debugMode = debugModeValue;
    }

    public void ApplyBoxData(ObjectData data)
    {
        if (data == null)
        {
            return;
        }

        ConfigureDataDrivenObject(
            data.remainAsPlatformOnGround || data.becomePlatformWhenPaused,
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
        if (isDropped || isLanded)
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

    [ContextMenu("ResetBox")]
    public void ResetBox()
    {
        isDropped = false;
        isLanded = false;
        transform.SetPositionAndRotation(startPosition, startRotation);
        ClearVelocity();

        if (gravityObject != null)
        {
            gravityObject.ResetGravityObject();
        }

        if (mainCollider != null)
        {
            mainCollider.enabled = true;
        }

        SetDamageEnabled(false);
        Log("Reset.");
    }

    [ContextMenu("ValidateBoxSetup")]
    public void ValidateBoxSetup()
    {
        CacheReferences();
        LogComponent("GravityObject3D", gravityObject);
        LogComponent("DamageDealer", damageDealer);
        LogComponent("GravityObjectDamageDealer", gravityObjectDamageDealer);
        LogComponent("PausablePhysicsObject", pausablePhysicsObject);
        LogComponent("Rigidbody", rb);
        LogComponent("Collider", mainCollider);
    }

    public void LandAsPlatform()
    {
        if (isLanded)
        {
            return;
        }

        isDropped = false;
        isLanded = true;
        SetDamageEnabled(false);
        ClearVelocity();

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        if (mainCollider != null)
        {
            mainCollider.enabled = true;
            mainCollider.isTrigger = false;
        }

        Log("Landed as platform.");
        NotifySpawnerObjectFinished();
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
        if (!isDropped || isLanded || collision == null || collision.collider == null)
        {
            return;
        }

        if (IsPlayerHit(collision.collider))
        {
            return;
        }

        if (IsGround(collision.collider) && remainAsPlatformOnGround)
        {
            LandAsPlatform();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isDropped || isLanded || other == null || IsPlayerHit(other))
        {
            return;
        }

        if (IsGround(other) && remainAsPlatformOnGround)
        {
            LandAsPlatform();
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

        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (mainCollider == null)
        {
            mainCollider = GetComponent<Collider>();
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

    private void LogComponent(string label, Object component)
    {
        if (!debugMode)
        {
            return;
        }

        if (component != null)
        {
            Debug.Log($"[FallingBoxObject] {label} found: {component.GetType().Name}", this);
            return;
        }

        Debug.LogWarning($"[FallingBoxObject] {label} not assigned.", this);
    }

    private void Log(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[FallingBoxObject] {message}", this);
        }
    }
}
