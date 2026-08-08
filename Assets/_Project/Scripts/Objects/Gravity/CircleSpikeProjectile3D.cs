using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

public enum CircleSpikeMovementState
{
    Attached,
    Falling,
    GroundedRolling,
    Stopped
}

public enum CircleSpikeFallbackRollDirection
{
    Left = -1,
    Right = 1
}

public enum CircleSpikeVisualRotationMode
{
    DistanceBased,
    DegreesPerSecond
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(Collider), typeof(CircleSpikeObject))]
public sealed class CircleSpikeProjectile3D : MonoBehaviour, IShutterFreezable3D, IShutterFreezeState3D, IGravityActivatable3D
{
    [Header("Drop")]
    [SerializeField, Min(0f)] private float gravityScale = 1f;
    [SerializeField, Min(0f)] private float dropImpulse;
    [SerializeField] private LayerMask groundLayerMask = (1 << 8) | (1 << 9) | (1 << 10) | (1 << 11) | (1 << 12);
    [SerializeField, Range(0f, 1f)] private float groundNormalMinimum = 0.65f;
    [SerializeField, Min(0.001f)] private float groundCheckDistance = 0.12f;
    [SerializeField, Min(0f)] private float ownerCollisionIgnoreTime = 0.1f;

    [Header("Movement")]
    [FormerlySerializedAs("initialRollSpeed")]
    [SerializeField, Min(0f)] private float initialMoveSpeed = 5f;
    [FormerlySerializedAs("rollAcceleration")]
    [SerializeField, Min(0f)] private float moveAcceleration;
    [FormerlySerializedAs("maxRollSpeed")]
    [SerializeField, Min(0f)] private float maxMoveSpeed = 5f;
    [SerializeField, Min(0f)] private float rollingLifetime = 8f;
    [SerializeField] private bool stopOnWall = true;
    [SerializeField] private bool disableOnStop = true;
    [SerializeField] private bool disableOnPlayerHit = true;
    [SerializeField] private bool trackPlayerWhileRolling;
    [SerializeField] private bool allowDirectionReversal;
    [SerializeField] private bool reacquirePlayerOnLanding = true;
    [SerializeField] private CircleSpikeFallbackRollDirection fallbackDirection = CircleSpikeFallbackRollDirection.Right;

    [Header("Visual Rotation")]
    [SerializeField] private CircleSpikeVisualRotationMode rotationMode = CircleSpikeVisualRotationMode.DistanceBased;
    [SerializeField, Min(0f)] private float visualRotationSpeed = 180f;
    [FormerlySerializedAs("visualRotationSpeedMultiplier")]
    [SerializeField, Min(0f)] private float visualRotationMultiplier = 0.35f;
    [SerializeField] private Transform circleSpikeVisual;
    [SerializeField, Min(0.001f)] private float visualRadius = 0.65f;
    [SerializeField] private float rotationDirectionMultiplier = -1f;
    [SerializeField, Min(0f)] private float maxVisualRotationSpeed = 360f;
    [SerializeField] private bool rotateOnlyWhileMoving = true;
    [SerializeField, Min(0f)] private float minimumMovementThreshold = 0.001f;
    [SerializeField] private bool resetVisualRotationOnReuse = true;
    [SerializeField] private float rotationOffset;

    [Header("References")]
    [SerializeField] private Rigidbody body;
    [SerializeField] private Collider projectileCollider;
    [SerializeField] private CircleSpikeObject circleSpike;
    [SerializeField] private GravityObject3D gravityObject;
    [SerializeField] private Renderer projectileRenderer;

    [Header("Runtime (Read Only)")]
    [SerializeField] private CircleSpikeMovementState state = CircleSpikeMovementState.Attached;
    [SerializeField] private float rollDirection;
    [FormerlySerializedAs("currentRollSpeed")]
    [SerializeField] private float currentMoveSpeed;

    private Transform authoredParent;
    private Vector3 authoredLocalPosition;
    private Quaternion authoredLocalRotation;
    private Vector3 authoredLocalScale;
    private Quaternion authoredVisualLocalRotation;
    private Collider[] ownerColliders;
    private float elapsedLifetime;
    private Vector3 previousVisualPosition;
    private bool visualPositionInitialized;
    private bool collisionPending;
    private bool shutterPauseOverride;
    private float shutterFreezeEndTime;
    private bool physicsFrozenByShutter;
    private Vector3 velocityBeforeShutter;

    public CircleSpikeMovementState State => state;
    public bool IsLaunched => state == CircleSpikeMovementState.Falling || state == CircleSpikeMovementState.GroundedRolling;
    public bool IsShutterFrozen => shutterPauseOverride || Time.time < shutterFreezeEndTime;

    private void Reset() => CacheReferences();

    private void Awake()
    {
        CacheReferences();
        CaptureAuthoredState();
        PrepareForLaunch();
    }

    private void OnDisable()
    {
        RestoreOwnerCollisions();
        physicsFrozenByShutter = false;
    }

    private void OnValidate()
    {
        gravityScale = Mathf.Max(0f, gravityScale);
        dropImpulse = Mathf.Max(0f, dropImpulse);
        groundNormalMinimum = Mathf.Clamp01(groundNormalMinimum);
        groundCheckDistance = Mathf.Max(0.001f, groundCheckDistance);
        ownerCollisionIgnoreTime = Mathf.Max(0f, ownerCollisionIgnoreTime);
        initialMoveSpeed = Mathf.Max(0f, initialMoveSpeed);
        moveAcceleration = Mathf.Max(0f, moveAcceleration);
        maxMoveSpeed = Mathf.Max(initialMoveSpeed, maxMoveSpeed);
        rollingLifetime = Mathf.Max(0f, rollingLifetime);
        visualRotationSpeed = Mathf.Max(0f, visualRotationSpeed);
        visualRotationMultiplier = Mathf.Max(0f, visualRotationMultiplier);
        visualRadius = Mathf.Max(0.001f, visualRadius);
        maxVisualRotationSpeed = Mathf.Max(0f, maxVisualRotationSpeed);
        minimumMovementThreshold = Mathf.Max(0f, minimumMovementThreshold);
        CacheReferences();
    }

    private void FixedUpdate()
    {
        UpdateShutterPhysicsState();
        if (!IsLaunched || IsShutterFrozen || body == null) return;

        float deltaTime = Time.fixedDeltaTime;
        elapsedLifetime += deltaTime;
        if (rollingLifetime > 0f && elapsedLifetime >= rollingLifetime)
        {
            StopProjectile();
            return;
        }

        if (!body.isKinematic && !Mathf.Approximately(gravityScale, 1f))
        {
            body.AddForce(Physics.gravity * (gravityScale - 1f), ForceMode.Acceleration);
        }

        if (state == CircleSpikeMovementState.GroundedRolling && !body.isKinematic)
        {
            if (!HasGroundBelow())
            {
                state = CircleSpikeMovementState.Falling;
                visualPositionInitialized = false;
                return;
            }

            UpdateTrackedDirection();
            currentMoveSpeed = Mathf.MoveTowards(currentMoveSpeed, maxMoveSpeed, moveAcceleration * deltaTime);
            Vector3 velocity = GetVelocity();
            velocity.x = rollDirection * currentMoveSpeed;
            velocity.z = 0f;
            SetVelocity(velocity);
        }
    }

    private void LateUpdate()
    {
        if (circleSpikeVisual == null || body == null) return;
        if (state != CircleSpikeMovementState.GroundedRolling || body.isKinematic)
        {
            visualPositionInitialized = false;
            return;
        }

        Vector3 currentPosition = circleSpikeVisual.position;
        if (!visualPositionInitialized)
        {
            previousVisualPosition = currentPosition;
            visualPositionInitialized = true;
            return;
        }

        float horizontalDistance = Mathf.Abs(currentPosition.x - previousVisualPosition.x);
        previousVisualPosition = currentPosition;
        bool isMoving = horizontalDistance >= minimumMovementThreshold;
        if (rotateOnlyWhileMoving && !isMoving) return;

        float direction = rollDirection * rotationDirectionMultiplier;
        float rotationDelta = rotationMode == CircleSpikeVisualRotationMode.DistanceBased
            ? (horizontalDistance / visualRadius) * Mathf.Rad2Deg * visualRotationMultiplier * direction
            : visualRotationSpeed * direction * Time.deltaTime;
        if (maxVisualRotationSpeed > 0f)
        {
            float maxFrameRotation = maxVisualRotationSpeed * Time.deltaTime;
            rotationDelta = Mathf.Clamp(rotationDelta, -maxFrameRotation, maxFrameRotation);
        }
        circleSpikeVisual.Rotate(0f, 0f, rotationDelta, Space.Self);
    }

    public void PrepareForLaunch()
    {
        state = CircleSpikeMovementState.Attached;
        gravityObject?.SetHorizontalMotionAllowed(false);
        rollDirection = 0f;
        currentMoveSpeed = initialMoveSpeed;
        elapsedLifetime = 0f;
        collisionPending = false;
        shutterPauseOverride = false;
        shutterFreezeEndTime = 0f;
        physicsFrozenByShutter = false;
        EnterAttachedState();
        if (projectileCollider != null) projectileCollider.enabled = true;
        if (projectileRenderer != null) projectileRenderer.enabled = true;
        circleSpike?.SetDamageEnabled(false);
    }

    public bool ReleaseAndDrop()
    {
        if (state != CircleSpikeMovementState.Attached) return false;

        DetachFromRopeSet();
        IgnoreOwnerCollisions();
        elapsedLifetime = 0f;
        visualPositionInitialized = false;
        circleSpike?.TriggerObject();
        EnterFallingState();
        if (projectileCollider != null) projectileCollider.enabled = true;
        circleSpike?.SetDamageEnabled(true);
        return true;
    }

    public bool TryActivateGravity(GameObject source)
    {
        return ReleaseAndDrop();
    }

    public void StopProjectile()
    {
        if (state == CircleSpikeMovementState.Stopped) return;
        EnterStoppedState();
        circleSpike?.SetDamageEnabled(false);
        RestoreOwnerCollisions();
        if (disableOnStop) gameObject.SetActive(false);
    }

    public void ResetProjectile()
    {
        gameObject.SetActive(true);
        RestoreOwnerCollisions();
        circleSpike?.ResetObject();
        if (authoredParent != null)
        {
            transform.SetParent(authoredParent, false);
            transform.localPosition = authoredLocalPosition;
            transform.localRotation = authoredLocalRotation;
            transform.localScale = authoredLocalScale;
        }
        if (resetVisualRotationOnReuse && circleSpikeVisual != null)
            circleSpikeVisual.localRotation = authoredVisualLocalRotation;
        PrepareForLaunch();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null || collision.collider == null || !IsLaunched) return;
        if (IsPlayer(collision.collider.transform))
        {
            HandlePlayerHit();
            return;
        }
        if (state == CircleSpikeMovementState.Falling && TryEnterGroundedRolling(collision)) return;
        if (state == CircleSpikeMovementState.GroundedRolling && stopOnWall && HasWallContact(collision))
        {
            StopProjectile();
            return;
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (state == CircleSpikeMovementState.Falling) TryEnterGroundedRolling(collision);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other != null && IsLaunched && IsPlayer(other.transform)) HandlePlayerHit();
    }

    private bool TryEnterGroundedRolling(Collision collision)
    {
        if (collision == null || collision.collider == null || !IsGroundLayer(collision.collider.gameObject.layer)) return false;
        bool validNormal = false;
        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y >= groundNormalMinimum)
            {
                validNormal = true;
                break;
            }
        }
        if (!validNormal) return false;

        EnterGroundedRollingState();
        return true;
    }

    private void EnterAttachedState()
    {
        state = CircleSpikeMovementState.Attached;
        if (body == null) return;
        ClearVelocityIfDynamic();
        body.useGravity = false;
        body.isKinematic = true;
        body.constraints = RigidbodyConstraints.FreezePositionZ |
                           RigidbodyConstraints.FreezeRotationX |
                           RigidbodyConstraints.FreezeRotationY;
    }

    private void EnterFallingState()
    {
        state = CircleSpikeMovementState.Falling;
        gravityObject?.SetHorizontalMotionAllowed(false);
        if (body == null) return;
        body.isKinematic = false;
        body.useGravity = true;
        body.constraints = RigidbodyConstraints.FreezePositionZ |
                           RigidbodyConstraints.FreezeRotationX |
                           RigidbodyConstraints.FreezeRotationY;
        SetVelocity(dropImpulse > 0f ? Vector3.down * dropImpulse : Vector3.zero);
        body.angularVelocity = Vector3.zero;
        body.WakeUp();
    }

    private void EnterGroundedRollingState()
    {
        state = CircleSpikeMovementState.GroundedRolling;
        gravityObject?.SetHorizontalMotionAllowed(true);
        currentMoveSpeed = initialMoveSpeed;
        previousVisualPosition = circleSpikeVisual != null ? circleSpikeVisual.position : transform.position;
        visualPositionInitialized = true;
        if (rollDirection == 0f || reacquirePlayerOnLanding) AcquireRollDirection();
    }

    private void EnterStoppedState()
    {
        state = CircleSpikeMovementState.Stopped;
        if (body == null) return;
        ClearVelocityIfDynamic();
        if (!body.isKinematic) body.Sleep();
        body.useGravity = false;
        body.isKinematic = true;
    }

    private bool HasGroundBelow()
    {
        if (projectileCollider == null) return false;
        Bounds bounds = projectileCollider.bounds;
        float radius = Mathf.Max(0.02f, Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.8f);
        float distance = bounds.extents.y + groundCheckDistance;
        return Physics.SphereCast(bounds.center, radius, Vector3.down, out RaycastHit hit, distance,
            groundLayerMask, QueryTriggerInteraction.Ignore) && hit.normal.y >= groundNormalMinimum;
    }

    private void AcquireRollDirection()
    {
        Transform player = ResolvePlayerTarget();
        float deltaX = player != null ? player.position.x - transform.position.x : 0f;
        rollDirection = Mathf.Abs(deltaX) > 0.01f ? Mathf.Sign(deltaX) : (float)fallbackDirection;
    }

    private void UpdateTrackedDirection()
    {
        if (!trackPlayerWhileRolling || !allowDirectionReversal) return;
        Transform player = ResolvePlayerTarget();
        if (player == null) return;
        float deltaX = player.position.x - transform.position.x;
        if (Mathf.Abs(deltaX) > 0.01f) rollDirection = Mathf.Sign(deltaX);
    }

    private bool HasWallContact(Collision collision)
    {
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector3 normal = collision.GetContact(i).normal;
            if (Mathf.Abs(normal.x) >= groundNormalMinimum && normal.y < groundNormalMinimum) return true;
        }
        return false;
    }

    private void HandlePlayerHit()
    {
        if (collisionPending) return;
        collisionPending = true;
        if (disableOnPlayerHit) StartCoroutine(DisableAfterDamageCallback());
    }

    private IEnumerator DisableAfterDamageCallback()
    {
        yield return null;
        StopProjectile();
    }

    private void DetachFromRopeSet()
    {
        Transform setRoot = transform.parent;
        transform.SetParent(setRoot != null ? setRoot.parent : null, true);
    }

    private void IgnoreOwnerCollisions()
    {
        ownerColliders = authoredParent != null ? authoredParent.GetComponentsInChildren<Collider>(true) : null;
        if (projectileCollider == null || ownerColliders == null) return;
        for (int i = 0; i < ownerColliders.Length; i++)
        {
            if (ownerColliders[i] != null && ownerColliders[i] != projectileCollider)
                Physics.IgnoreCollision(projectileCollider, ownerColliders[i], true);
        }
        if (ownerCollisionIgnoreTime > 0f) StartCoroutine(RestoreOwnerCollisionsAfterDelay());
    }

    private IEnumerator RestoreOwnerCollisionsAfterDelay()
    {
        yield return new WaitForSeconds(ownerCollisionIgnoreTime);
        RestoreOwnerCollisions();
    }

    private void RestoreOwnerCollisions()
    {
        if (projectileCollider != null && ownerColliders != null)
        {
            for (int i = 0; i < ownerColliders.Length; i++)
            {
                if (ownerColliders[i] != null && ownerColliders[i] != projectileCollider)
                    Physics.IgnoreCollision(projectileCollider, ownerColliders[i], false);
            }
        }
        ownerColliders = null;
    }

    public bool ApplyShutterFreeze(float duration, CameraAbilitySystem3D source)
    {
        if (!IsLaunched) return false;
        shutterFreezeEndTime = Mathf.Max(shutterFreezeEndTime, Time.time + Mathf.Max(0f, duration));
        UpdateShutterPhysicsState();
        return true;
    }

    public void SetShutterPaused(bool paused)
    {
        shutterPauseOverride = paused;
        UpdateShutterPhysicsState();
    }

    private void UpdateShutterPhysicsState()
    {
        if (body == null || !IsLaunched) return;
        if (IsShutterFrozen && !physicsFrozenByShutter)
        {
            velocityBeforeShutter = GetVelocity();
            body.isKinematic = true;
            physicsFrozenByShutter = true;
        }
        else if (!IsShutterFrozen && physicsFrozenByShutter)
        {
            body.isKinematic = false;
            body.useGravity = true;
            SetVelocity(velocityBeforeShutter);
            physicsFrozenByShutter = false;
        }
    }

    private bool IsGroundLayer(int layer) => (groundLayerMask.value & (1 << layer)) != 0;

    private Transform ResolvePlayerTarget()
    {
        PlatformerPlayer3D player = FindFirstObjectByType<PlatformerPlayer3D>();
        if (player != null && player.isActiveAndEnabled) return player.transform;
        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        return taggedPlayer != null && taggedPlayer.activeInHierarchy ? taggedPlayer.transform : null;
    }

    private static bool IsPlayer(Transform candidate)
    {
        for (Transform current = candidate; current != null; current = current.parent)
        {
            if (current.CompareTag("Player")) return true;
        }
        return false;
    }

    private void CacheReferences()
    {
        if (body == null) body = GetComponent<Rigidbody>();
        if (projectileCollider == null) projectileCollider = GetComponent<Collider>();
        if (circleSpike == null) circleSpike = GetComponent<CircleSpikeObject>();
        if (gravityObject == null) gravityObject = GetComponent<GravityObject3D>();
        if (projectileRenderer == null) projectileRenderer = GetComponentInChildren<Renderer>(true);
        if (circleSpikeVisual == null && projectileRenderer != null) circleSpikeVisual = projectileRenderer.transform;
    }

    private void CaptureAuthoredState()
    {
        authoredParent = transform.parent;
        authoredLocalPosition = transform.localPosition;
        authoredLocalRotation = transform.localRotation;
        authoredLocalScale = transform.localScale;
        if (circleSpikeVisual != null) authoredVisualLocalRotation = circleSpikeVisual.localRotation;
        if (!Mathf.Approximately(rotationOffset, 0f) && circleSpikeVisual != null)
            circleSpikeVisual.Rotate(0f, 0f, rotationOffset, Space.Self);
    }

    private void ClearVelocityIfDynamic()
    {
        if (body == null || body.isKinematic) return;
        SetVelocity(Vector3.zero);
        body.angularVelocity = Vector3.zero;
    }

    private Vector3 GetVelocity()
    {
#if UNITY_6000_0_OR_NEWER
        return body != null ? body.linearVelocity : Vector3.zero;
#else
        return body != null ? body.velocity : Vector3.zero;
#endif
    }

    private void SetVelocity(Vector3 velocity)
    {
        if (body == null) return;
#if UNITY_6000_0_OR_NEWER
        body.linearVelocity = velocity;
#else
        body.velocity = velocity;
#endif
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position + Vector3.down * groundCheckDistance, visualRadius * 0.8f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.right * (float)fallbackDirection * initialMoveSpeed);
    }
}
