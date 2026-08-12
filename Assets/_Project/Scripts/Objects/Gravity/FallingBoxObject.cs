using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum FallingBoxState
{
    GROUNDED,
    FALLING,
    MAGNETIZED
}

[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class FallingBoxObject : MonoBehaviour, IGravityActivatable3D
{
    private const float SupportingContactNormalMinimum = 0.5f;

    [Header("State")]
    [SerializeField] private FallingBoxState currentState = FallingBoxState.GROUNDED;
    [SerializeField, HideInInspector] private bool isDropped;
    [SerializeField, HideInInspector] private bool isLanded;
    [InspectorName("Can Become Platform")]
    [SerializeField] private bool remainAsPlatformOnGround = true;

    [Header("Ground Check")]
    [Tooltip("Only an upward-facing collision contact on one of these layers can ground the Box.")]
    [SerializeField] private LayerMask groundLayerMask;

    [Header("References")]
    [SerializeField] private GravityObject3D gravityObject;
    [SerializeField] private DamageDealer damageDealer;
    [SerializeField] private PausablePhysicsObject pausablePhysicsObject;
    [SerializeField] private MagneticCarryable3D magneticCarryable;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Collider mainCollider;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;

    private readonly HashSet<Collider> supportingColliders = new HashSet<Collider>();
    private GravityObjectSpawner ownerSpawner;
    private bool wasSpawnedBySpawner;
    private bool wasMagnetReserved;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private Coroutine pendingLandingRoutine;
    private IMarkState3D markState;

    public FallingBoxState CurrentState => currentState;
    public bool IsFalling => currentState == FallingBoxState.FALLING;
    public bool HasLanded => currentState == FallingBoxState.GROUNDED;

    public void RefreshAfterMarkReleased()
    {
        CacheReferences();
        bool isMagnetReserved = magneticCarryable != null && magneticCarryable.IsReserved;
        wasMagnetReserved = isMagnetReserved;
        SetDamageEnabled(currentState == FallingBoxState.FALLING && !isMagnetReserved);
        if (mainCollider != null)
        {
            mainCollider.enabled = true;
            mainCollider.isTrigger = false;
        }
    }

    private void Awake()
    {
        CacheReferences();
        CaptureStartTransform();
        currentState = isDropped && !isLanded
            ? FallingBoxState.FALLING
            : FallingBoxState.GROUNDED;
        SyncLegacyState();
        wasMagnetReserved = magneticCarryable != null && magneticCarryable.IsReserved;

        if (wasMagnetReserved)
        {
            EnterMagnetized();
        }
        else
        {
            SetDamageEnabled(IsFalling);
        }
    }

    private void FixedUpdate()
    {
        if (markState != null && markState.IsMarked) return;
        bool isMagnetReserved = magneticCarryable != null && magneticCarryable.IsReserved;
        if (isMagnetReserved == wasMagnetReserved)
        {
            return;
        }

        wasMagnetReserved = isMagnetReserved;
        if (isMagnetReserved)
        {
            EnterMagnetized();
        }
        else if (currentState == FallingBoxState.MAGNETIZED)
        {
            ExitMagnetized();
        }
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            if (markState != null && markState.IsMarked)
            {
                SetDamageEnabled(false);
                return;
            }
            bool isMagnetReserved = magneticCarryable != null && magneticCarryable.IsReserved;
            SetDamageEnabled(IsFalling && !isMagnetReserved);
        }
    }

    private void OnDisable()
    {
        CancelPendingLanding();
        supportingColliders.Clear();
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

        if (damageDealer != null)
        {
            damageDealer.ConfigureDamage(data.damage, ~0, data.damageOncePerTarget, HitSourceType.Environment);
            damageDealer.ConfigureDebug(data.debugMode);
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
        if (currentState != FallingBoxState.GROUNDED ||
            (magneticCarryable != null && magneticCarryable.IsReserved))
        {
            return;
        }

        supportingColliders.Clear();
        EnterFalling(false);
        Log("Dropped.");
    }

    public bool TryActivateGravity(GameObject source)
    {
        if (currentState != FallingBoxState.GROUNDED ||
            (magneticCarryable != null && magneticCarryable.IsReserved))
        {
            return false;
        }

        ConnectedObjectLink support = GetComponentInParent<ConnectedObjectLink>();
        if (support != null)
        {
            return support.ReleaseConnectedObject();
        }

        TriggerDrop();
        return IsFalling;
    }

    [ContextMenu("ResetBox")]
    public void ResetBox()
    {
        CancelPendingLanding();
        supportingColliders.Clear();
        currentState = FallingBoxState.GROUNDED;
        SyncLegacyState();
        transform.SetPositionAndRotation(startPosition, startRotation);
        ClearVelocity();

        if (gravityObject != null)
        {
            gravityObject.ResetGravityObject();
        }

        if (mainCollider != null)
        {
            mainCollider.enabled = true;
            mainCollider.isTrigger = false;
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
        LogComponent("PausablePhysicsObject", pausablePhysicsObject);
        LogComponent("MagneticCarryable3D", magneticCarryable);
        LogComponent("Rigidbody", rb);
        LogComponent("Collider", mainCollider);
        Log($"State={currentState}, DamageEnabled={damageDealer != null && damageDealer.enabled}, GroundMask={groundLayerMask.value}");
    }

    public void LandAsPlatform()
    {
        if (currentState != FallingBoxState.FALLING)
        {
            return;
        }

        CancelPendingLanding();
        currentState = FallingBoxState.GROUNDED;
        SyncLegacyState();
        SetDamageEnabled(false);
        ClearVelocity();

        if (rb != null)
        {
            // A grounded Box remains a physical platform so Dionaea can push it and
            // the Crane magnet can attract it without changing its authored body settings.
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        if (mainCollider != null)
        {
            mainCollider.enabled = true;
            mainCollider.isTrigger = false;
        }

        Log("Landed as physical platform.");
    }

    private void EnterFalling(bool allowHorizontalMotion)
    {
        CancelPendingLanding();
        currentState = FallingBoxState.FALLING;
        SyncLegacyState();

        if (gravityObject != null)
        {
            gravityObject.TriggerDrop();
            gravityObject.SetHorizontalMotionAllowed(allowHorizontalMotion);
        }

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        SetDamageEnabled(true);
    }

    private void EnterMagnetized()
    {
        CancelPendingLanding();
        currentState = FallingBoxState.MAGNETIZED;
        SyncLegacyState();
        SetDamageEnabled(false);

        if (gravityObject != null)
        {
            gravityObject.SetHorizontalMotionAllowed(true);
        }

        Log("Magnetized.");
    }

    private void ExitMagnetized()
    {
        if (remainAsPlatformOnGround && supportingColliders.Count > 0)
        {
            currentState = FallingBoxState.FALLING;
            SyncLegacyState();
            LandAsPlatform();
            return;
        }

        // Falling from the Crane's release point must not snap back to the original X.
        EnterFalling(true);
        Log("Released from magnet and falling.");
    }

    private void OnCollisionEnter(Collision collision)
    {
        UpdateSupportingCollision(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        UpdateSupportingCollision(collision);
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision == null || collision.collider == null)
        {
            return;
        }

        supportingColliders.Remove(collision.collider);
        TryEnterFallingAfterSupportLoss();
    }

    private void UpdateSupportingCollision(Collision collision)
    {
        if (collision == null || collision.collider == null)
        {
            return;
        }

        Collider other = collision.collider;
        if (!IsSupportingGroundContact(collision))
        {
            supportingColliders.Remove(other);
            TryEnterFallingAfterSupportLoss();
            return;
        }

        supportingColliders.Add(other);
        if (currentState == FallingBoxState.FALLING && remainAsPlatformOnGround)
        {
            QueueLanding();
        }
    }

    private void TryEnterFallingAfterSupportLoss()
    {
        if (supportingColliders.Count != 0 ||
            currentState != FallingBoxState.GROUNDED ||
            (magneticCarryable != null && magneticCarryable.IsReserved) ||
            rb == null || rb.isKinematic)
        {
            return;
        }

        EnterFalling(true);
        Log("Ground support lost.");
    }

    private bool IsSupportingGroundContact(Collision collision)
    {
        if (!IsGroundLayer(collision.collider))
        {
            return false;
        }

        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y >= SupportingContactNormalMinimum)
            {
                return true;
            }
        }

        return false;
    }

    private void QueueLanding()
    {
        if (pendingLandingRoutine == null)
        {
            pendingLandingRoutine = StartCoroutine(ConfirmLandingAfterPhysicsStep());
        }
    }

    private IEnumerator ConfirmLandingAfterPhysicsStep()
    {
        yield return new WaitForFixedUpdate();
        pendingLandingRoutine = null;

        if (markState != null && markState.IsMarked) yield break;

        if (currentState == FallingBoxState.FALLING &&
            supportingColliders.Count > 0 &&
            (magneticCarryable == null || !magneticCarryable.IsReserved))
        {
            LandAsPlatform();
        }
    }

    private void CancelPendingLanding()
    {
        if (pendingLandingRoutine == null)
        {
            return;
        }

        StopCoroutine(pendingLandingRoutine);
        pendingLandingRoutine = null;
    }

    private bool IsGroundLayer(Collider other)
    {
        return other != null &&
               groundLayerMask.value != 0 &&
               (groundLayerMask.value & (1 << other.gameObject.layer)) != 0;
    }

    private void SyncLegacyState()
    {
        isDropped = currentState == FallingBoxState.FALLING;
        isLanded = currentState == FallingBoxState.GROUNDED;
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

        if (pausablePhysicsObject == null)
        {
            pausablePhysicsObject = GetComponent<PausablePhysicsObject>();
        }

        if (magneticCarryable == null)
        {
            magneticCarryable = GetComponent<MagneticCarryable3D>();
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
        bool shouldEnableSharedDealer = enabled && currentState == FallingBoxState.FALLING;
        if (damageDealer != null)
        {
            damageDealer.enabled = shouldEnableSharedDealer;
            if (!shouldEnableSharedDealer)
            {
                damageDealer.ClearDamagedTargets();
            }
        }
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
