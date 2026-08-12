using UnityEngine;

[DisallowMultipleComponent]
public sealed class HumanBoxDeadPlatform3D : MonoBehaviour
{
    [Header("Dead Platform")]
    [SerializeField] private Collider bodyBlockingCollider;
    [SerializeField] private Collider attackCollider;
    [SerializeField] private Collider topPlatformCollider;
    [SerializeField] private Rigidbody body;
    [SerializeField] private bool freezeRigidbodyOnDeath = true;

    private bool aliveUseGravity;
    private bool aliveIsKinematic;
    private RigidbodyConstraints aliveConstraints;
    private RigidbodyInterpolation aliveInterpolation;
    private CollisionDetectionMode aliveCollisionDetectionMode;
    private bool initialized;
    private bool hasEnteredDeadPlatform;

    public Collider AttackCollider => attackCollider;

    private void Awake()
    {
        EnsureInitialized();
        RestoreAlive();
    }

    public void RestoreAlive()
    {
        EnsureInitialized();
        if (body != null)
        {
            body.isKinematic = aliveIsKinematic;
            body.useGravity = aliveUseGravity;
            body.constraints = aliveConstraints;
            body.interpolation = aliveInterpolation;
            body.collisionDetectionMode = aliveCollisionDetectionMode;
            StopDynamicMotion(body);
        }
        if (bodyBlockingCollider != null) bodyBlockingCollider.enabled = true;
        if (attackCollider != null) attackCollider.enabled = false;
        if (topPlatformCollider != null) topPlatformCollider.enabled = false;
        hasEnteredDeadPlatform = false;
    }

    public void SetAttackHitbox(bool active)
    {
        if (attackCollider != null) attackCollider.enabled = active;
    }

    public void EnterDeadPlatform()
    {
        EnsureInitialized();
        if (hasEnteredDeadPlatform) return;
        hasEnteredDeadPlatform = true;

        if (body != null)
        {
            StopDynamicMotion(body);
            if (freezeRigidbodyOnDeath)
            {
                body.useGravity = false;
                body.isKinematic = true;
            }
        }

        if (attackCollider != null) attackCollider.enabled = false;
        if (bodyBlockingCollider != null) bodyBlockingCollider.enabled = false;
        if (topPlatformCollider != null) topPlatformCollider.enabled = true;
    }

    private void EnsureInitialized()
    {
        if (initialized) return;
        if (body == null) body = GetComponent<Rigidbody>();
        aliveUseGravity = body != null && body.useGravity;
        aliveIsKinematic = body != null && body.isKinematic;
        aliveConstraints = body != null ? body.constraints : RigidbodyConstraints.None;
        aliveInterpolation = body != null ? body.interpolation : RigidbodyInterpolation.None;
        aliveCollisionDetectionMode = body != null
            ? body.collisionDetectionMode
            : CollisionDetectionMode.Discrete;
        initialized = true;
    }

    private static void StopDynamicMotion(Rigidbody targetBody)
    {
        if (targetBody == null || targetBody.isKinematic) return;
        targetBody.linearVelocity = Vector3.zero;
        targetBody.angularVelocity = Vector3.zero;
    }
}
