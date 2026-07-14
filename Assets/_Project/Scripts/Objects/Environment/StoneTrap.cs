using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum StoneTrapState
{
    Idle,
    Falling,
    Paused,
    Broken
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class StoneTrap : MonoBehaviour
{
    private static readonly int IsFallingHash = Animator.StringToHash("IsFalling");
    private static readonly int IsBrokenHash = Animator.StringToHash("IsBroken");
    private static readonly int BreakHash = Animator.StringToHash("Break");
    private static readonly int StateHash = Animator.StringToHash("State");

    [Header("Identity")]
    public string objectId = "G_OBJ_001";

    [Header("State")]
    [SerializeField] private StoneTrapState currentState = StoneTrapState.Idle;
    public bool startAttachedToCeiling = true;
    public bool gravityDisabledOnStart = true;

    [Header("Damage")]
    public int damage = 1;
    public bool damagePlayer = true;
    public bool damageMonster = true;
    public bool damageOnce = true;
    public bool canDamageBreakables;
    public bool breakAfterDamagingTarget;
    public LayerMask damageLayerMask;

    [Header("Ground")]
    public LayerMask groundLayerMask;
    public bool destroyOnGroundHit = true;
    public float destroyTime = 0.5f;
    public bool disableColliderOnBreak = true;
    public bool destroyGameObject = true;

    [Header("Movement")]
    public bool useGravityDrop = true;
    public float manualDropSpeed;
    public bool lockXWhileFalling = true;
    public bool lockZWhileFalling = true;

    [Header("Shutter Control")]
    public bool canBeControlledByShutter = true;
    [SerializeField] private bool isPausedByShutter;
    public bool allowResumeAfterShutter = true;

    [Header("References")]
    public Rigidbody rb;
    public Collider mainCollider;
    public Animator animator;

    [Header("Debug")]
    public bool debugMode = true;

    private readonly HashSet<int> damagedTargets = new HashSet<int>();
    private RigidbodyConstraints initialConstraints;
    private Coroutine destroyRoutine;

    public StoneTrapState CurrentState => currentState;
    public bool IsPausedByShutter => isPausedByShutter;

    private void Awake()
    {
        CacheReferences();
        initialConstraints = rb != null ? rb.constraints : RigidbodyConstraints.None;
        InitializeState();
    }

    private void OnValidate()
    {
        damage = Mathf.Max(0, damage);
        destroyTime = Mathf.Max(0f, destroyTime);
        manualDropSpeed = Mathf.Max(0f, manualDropSpeed);
        CacheReferences();
    }

    private void FixedUpdate()
    {
        if (currentState != StoneTrapState.Falling || rb == null)
        {
            return;
        }

        if (useGravityDrop)
        {
            Vector3 velocity = rb.linearVelocity;
            if (lockXWhileFalling)
            {
                velocity.x = 0f;
            }

            if (lockZWhileFalling)
            {
                velocity.z = 0f;
            }

            rb.linearVelocity = velocity;
            return;
        }

        if (manualDropSpeed > 0f)
        {
            rb.MovePosition(rb.position + Vector3.down * (manualDropSpeed * Time.fixedDeltaTime));
        }
    }

    public void TriggerDrop()
    {
        if (currentState == StoneTrapState.Broken || currentState == StoneTrapState.Falling)
        {
            return;
        }

        Log("TriggerDrop");
        BeginFalling();
    }

    public void PauseByShutter()
    {
        if (!canBeControlledByShutter || currentState != StoneTrapState.Falling)
        {
            return;
        }

        currentState = StoneTrapState.Paused;
        isPausedByShutter = true;
        StopMotion();
        if (rb != null)
        {
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        ApplyAnimatorState();
        Log("Paused by shutter");
    }

    public void ResumeByShutter()
    {
        if (!canBeControlledByShutter || !allowResumeAfterShutter ||
            currentState != StoneTrapState.Paused || currentState == StoneTrapState.Broken)
        {
            return;
        }

        BeginFalling();
        Log("Resumed by shutter");
    }

    public void SetShutterPaused(bool paused)
    {
        if (paused)
        {
            PauseByShutter();
        }
        else
        {
            ResumeByShutter();
        }
    }

    public void BreakStone()
    {
        if (currentState == StoneTrapState.Broken)
        {
            return;
        }

        currentState = StoneTrapState.Broken;
        isPausedByShutter = false;
        StopMotion();

        if (rb != null)
        {
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        if (disableColliderOnBreak && mainCollider != null)
        {
            mainCollider.enabled = false;
        }

        ApplyAnimatorState();
        Log("Broken");

        if (destroyRoutine != null)
        {
            StopCoroutine(destroyRoutine);
        }

        destroyRoutine = StartCoroutine(RemoveAfterDelay());
    }

    public void StopMotion()
    {
        if (rb == null)
        {
            return;
        }

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    private void InitializeState()
    {
        currentState = StoneTrapState.Idle;
        isPausedByShutter = false;
        damagedTargets.Clear();
        StopMotion();

        if (rb != null)
        {
            rb.useGravity = false;
            rb.isKinematic = true;
            ApplyMovementConstraints();
        }

        if (mainCollider != null)
        {
            mainCollider.enabled = true;
            mainCollider.isTrigger = false;
        }

        ApplyAnimatorState();

        if (!startAttachedToCeiling && !gravityDisabledOnStart)
        {
            BeginFalling();
        }
    }

    private void BeginFalling()
    {
        currentState = StoneTrapState.Falling;
        isPausedByShutter = false;
        StopMotion();

        if (rb == null)
        {
            Debug.LogWarning("[StoneTrap] Rigidbody is missing. Falling cannot start.", this);
            return;
        }

        ApplyMovementConstraints();
        rb.isKinematic = !useGravityDrop;
        rb.useGravity = useGravityDrop;
        ApplyAnimatorState();
        Log("Falling started");
    }

    private void ApplyMovementConstraints()
    {
        if (rb == null)
        {
            return;
        }

        RigidbodyConstraints constraints = initialConstraints |
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationY |
            RigidbodyConstraints.FreezeRotationZ;

        if (lockXWhileFalling)
        {
            constraints |= RigidbodyConstraints.FreezePositionX;
        }

        if (lockZWhileFalling)
        {
            constraints |= RigidbodyConstraints.FreezePositionZ;
        }

        rb.constraints = constraints;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (currentState != StoneTrapState.Falling || collision == null || collision.collider == null)
        {
            return;
        }

        Collider other = collision.collider;
        if (BelongsToStone(other.transform))
        {
            return;
        }

        if (IsGround(other))
        {
            Log("Hit ground. Stone breaks");
            if (destroyOnGroundHit)
            {
                BreakStone();
            }

            return;
        }

        bool isBreakable = IsBreakable(other.transform);
        if (isBreakable && !canDamageBreakables)
        {
            Log("Hit BreakableObject but ignored");
            return;
        }

        TryDamage(other, isBreakable);
    }

    private void TryDamage(Collider other, bool isBreakable)
    {
        PlayerDamageReceiver player = other.GetComponentInParent<PlayerDamageReceiver>();
        MonsterHealth monster = other.GetComponentInParent<MonsterHealth>();
        bool isPlayer = player != null || HasPlayerTag(other.transform);
        bool isMonster = monster != null;
        bool explicitlyIncludedByLayer = damageLayerMask.value != 0 &&
            IsInLayerMask(other.gameObject.layer, damageLayerMask);
        bool layerAllowed = damageLayerMask.value == 0 || explicitlyIncludedByLayer;

        if (!layerAllowed ||
            (isPlayer && !damagePlayer) ||
            (isMonster && !damageMonster) ||
            (!isPlayer && !isMonster && !explicitlyIncludedByLayer &&
                !(isBreakable && canDamageBreakables)))
        {
            return;
        }

        IDamageable damageable = FindDamageable(other.transform);
        if (damageable == null)
        {
            return;
        }

        Transform damageRoot = other.transform.root;
        int targetId = damageRoot.GetInstanceID();
        if (damageOnce && !damagedTargets.Add(targetId))
        {
            return;
        }

        damageable.TakeDamage(damage);
        Log($"Hit {(isPlayer ? "Player" : isMonster ? "Monster" : "Damageable")}. Damage={damage}");

        if (breakAfterDamagingTarget)
        {
            BreakStone();
        }
    }

    private bool IsGround(Collider other)
    {
        if (IsInLayerMask(other.gameObject.layer, groundLayerMask))
        {
            return true;
        }

        if (other.GetComponentInParent<FloorCollision>() != null)
        {
            return true;
        }

        MapPiece mapPiece = other.GetComponentInParent<MapPiece>();
        return mapPiece != null && mapPiece.IsGround;
    }

    private static bool IsBreakable(Transform target)
    {
        MonoBehaviour[] behaviours = target.GetComponentsInParent<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null)
            {
                continue;
            }

            string typeName = behaviour.GetType().Name;
            if (behaviour is IExplosionBreakable ||
                typeName.Contains("Breakable") ||
                typeName.Contains("CrackedFloor") ||
                typeName.Contains("CrackedWall"))
            {
                return true;
            }
        }

        return false;
    }

    private static IDamageable FindDamageable(Transform target)
    {
        IDamageable damageable = target.GetComponent<IDamageable>();
        if (damageable != null)
        {
            return damageable;
        }

        damageable = target.GetComponentInParent<IDamageable>();
        return damageable ?? target.GetComponentInChildren<IDamageable>(true);
    }

    private static bool HasPlayerTag(Transform target)
    {
        Transform current = target;
        while (current != null)
        {
            if (current.CompareTag("Player"))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private bool BelongsToStone(Transform target)
    {
        return target == transform || target.IsChildOf(transform);
    }

    private static bool IsInLayerMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    private void CacheReferences()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (mainCollider == null)
        {
            mainCollider = GetComponent<Collider>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }
    }

    private void ApplyAnimatorState()
    {
        if (animator == null)
        {
            return;
        }

        SetBoolIfPresent(IsFallingHash, currentState == StoneTrapState.Falling);
        SetBoolIfPresent(IsBrokenHash, currentState == StoneTrapState.Broken);
        SetIntIfPresent(StateHash, (int)currentState);
        if (currentState == StoneTrapState.Broken && HasParameter(BreakHash, AnimatorControllerParameterType.Trigger))
        {
            animator.SetTrigger(BreakHash);
        }
    }

    private void SetBoolIfPresent(int hash, bool value)
    {
        if (HasParameter(hash, AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(hash, value);
        }
    }

    private void SetIntIfPresent(int hash, int value)
    {
        if (HasParameter(hash, AnimatorControllerParameterType.Int))
        {
            animator.SetInteger(hash, value);
        }
    }

    private bool HasParameter(int hash, AnimatorControllerParameterType type)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].nameHash == hash && parameters[i].type == type)
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerator RemoveAfterDelay()
    {
        if (destroyTime > 0f)
        {
            yield return new WaitForSeconds(destroyTime);
        }

        if (destroyGameObject)
        {
            Destroy(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void Log(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[StoneTrap] {message}", this);
        }
    }
}
