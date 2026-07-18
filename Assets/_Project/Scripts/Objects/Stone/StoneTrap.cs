using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public enum StoneTrapState
{
    Idle,
    Falling,
    PausedPlatform,
    Broken
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class StoneTrap : MonoBehaviour
{
    private static readonly int IsFallingHash = Animator.StringToHash("IsFalling");
    private static readonly int IsPausedHash = Animator.StringToHash("IsPaused");
    private static readonly int IsBrokenHash = Animator.StringToHash("IsBroken");
    private static readonly int BreakHash = Animator.StringToHash("Break");
    private static readonly int StateHash = Animator.StringToHash("State");

    [Header("Identity")]
    [Tooltip("기획용 오브젝트 ID입니다. 게임 로직을 직접 바꾸지는 않습니다.")]
    public string objectId = "G_OBJ_001";

    [Header("State - 상태")]
    [Tooltip("현재 Stone 상태입니다. Play Mode 중 읽기용으로 확인합니다.")]
    [SerializeField] private StoneTrapState currentState = StoneTrapState.Idle;
    [Tooltip("천장에 붙어 있다가 TriggerDrop 호출 전까지 대기합니다.")]
    public bool startAttachedToCeiling = true;
    [Tooltip("시작 시 Gravity를 꺼서 트리거 전에는 떨어지지 않게 합니다.")]
    public bool gravityDisabledOnStart = true;

    [Header("Drop - 낙하")]
    [Tooltip("Rigidbody Gravity를 사용해 떨어집니다. 끄면 manualDropSpeed를 사용합니다.")]
    public bool useGravityDrop = true;
    [Tooltip("Gravity를 사용하지 않을 때의 수동 낙하 속도입니다. 0이면 수동 이동하지 않습니다.")]
    public float manualDropSpeed;
    [Tooltip("낙하 중 X 좌표를 고정해 배치한 세로 라인 그대로 떨어지게 합니다.")]
    public bool lockXWhileFalling = true;
    [FormerlySerializedAs("lockZWhileFalling")]
    [Tooltip("3D 사이드뷰 기준으로 Z 좌표를 고정합니다.")]
    public bool lockZPosition = true;

    [Header("Damage - 데미지")]
    [Tooltip("낙하 중 충돌한 대상에게 주는 데미지입니다.")]
    public int damage = 1;
    [Tooltip("Player에게 데미지를 줍니다.")]
    public bool damagePlayer = true;
    [Tooltip("MonsterHealth 또는 IDamageable 몬스터에게 데미지를 줍니다.")]
    public bool damageMonster = true;
    [Tooltip("Falling 상태에서만 데미지를 줍니다. PausedPlatform/Broken 상태에서는 데미지를 주지 않습니다.")]
    public bool damageOnlyWhileFalling = true;
    [FormerlySerializedAs("damageOnce")]
    [Tooltip("한 번 떨어지는 동안 같은 대상에게 한 번만 데미지를 줍니다.")]
    public bool damageOncePerTarget = true;
    [Tooltip("현재 기획에서는 기본 false입니다. Breakable/Cracked 계열까지 데미지 대상으로 볼 때만 켭니다.")]
    public bool canDamageBreakables;
    [Tooltip("대상에게 데미지를 준 직후 Stone도 부서지게 할 때 사용합니다.")]
    public bool breakAfterDamagingTarget;
    [Tooltip("비워두면 Player/Monster/IDamageable 판단을 사용합니다. 지정하면 해당 레이어만 데미지 후보가 됩니다.")]
    public LayerMask damageLayerMask;

    [Header("Platform Mode - 일시정지 플랫폼")]
    [Tooltip("Camera/Shutter로 멈췄을 때 플레이어가 밟을 수 있는 임시 플랫폼이 됩니다.")]
    public bool canBecomePlatformWhenPaused = true;
    [Tooltip("PausedPlatform 상태에서 Collider를 유지해 플레이어가 설 수 있게 합니다.")]
    public bool playerCanStandOnPausedStone = true;
    [Tooltip("PausedPlatform 상태에서는 데미지를 끕니다.")]
    public bool disableDamageWhenPaused = true;

    [Header("Camera / Shutter Control")]
    [Tooltip("Camera 시스템에서 Stone 낙하를 멈추거나 재개할 수 있습니다.")]
    public bool canBeControlledByCamera = true;
    [Tooltip("Shutter 시스템에서 Stone 낙하를 멈추거나 재개할 수 있습니다.")]
    public bool canBeControlledByShutter = true;
    [FormerlySerializedAs("allowResumeAfterShutter")]
    [Tooltip("Camera/Shutter 일시정지가 모두 풀렸을 때 낙하를 재개할 수 있습니다.")]
    public bool allowResumeAfterPause = true;
    [Tooltip("Camera에 의해 일시정지된 상태입니다. Play Mode 중 읽기용입니다.")]
    [SerializeField] private bool isPausedByCamera;
    [Tooltip("Shutter에 의해 일시정지된 상태입니다. Play Mode 중 읽기용입니다.")]
    [SerializeField] private bool isPausedByShutter;

    [Header("Break - 파괴")]
    [Tooltip("Stone이 땅으로 인식할 레이어입니다. Ground 레이어를 Inspector에서 직접 지정합니다.")]
    public LayerMask groundLayerMask;
    [FormerlySerializedAs("destroyOnGroundHit")]
    [Tooltip("Falling 상태에서 Ground/FloorCollision에 닿으면 부서집니다.")]
    public bool breakOnGroundHit = true;
    [Tooltip("부서진 뒤 Collider를 끕니다.")]
    public bool disableColliderOnBreak = true;
    [Tooltip("부서진 뒤 GameObject를 제거합니다. 끄면 비활성화합니다.")]
    public bool destroyGameObject = true;
    [Tooltip("Broken 상태 진입 후 제거/비활성화까지 걸리는 시간입니다.")]
    public float destroyTime = 0.5f;

    [Header("References - 참조")]
    [Tooltip("낙하에 사용하는 Rigidbody입니다. 자동으로 채워지지만 임의 변경은 주의하세요.")]
    public Rigidbody rb;
    [Tooltip("Stone의 메인 3D Collider입니다. Trigger가 아니어야 합니다.")]
    public Collider mainCollider;
    [Tooltip("Visual 자식에 있는 Animator입니다. 없어도 동작합니다.")]
    public Animator animator;
    [Tooltip("Visual 루트입니다. Visual에는 Collider를 넣지 않습니다.")]
    public Transform visualRoot;

    [Header("Debug")]
    [Tooltip("Stone 상태 변화 로그를 Console에 출력합니다.")]
    public bool debugMode = true;

    private readonly HashSet<int> damagedTargets = new HashSet<int>();
    private RigidbodyConstraints initialConstraints;
    private Coroutine destroyRoutine;

    public StoneTrapState CurrentState => currentState;
    public bool IsPausedByCamera => isPausedByCamera;
    public bool IsPausedByShutter => isPausedByShutter;
    public bool IsPausedByAnySource => isPausedByCamera || isPausedByShutter;

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

            if (lockZPosition)
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

        if (currentState == StoneTrapState.PausedPlatform)
        {
            Log("TriggerDrop ignored while PausedPlatform. Use Camera/Shutter resume APIs.");
            return;
        }

        isPausedByCamera = false;
        isPausedByShutter = false;
        damagedTargets.Clear();
        BeginFalling();
        Log("TriggerDrop");
    }

    public void PauseByCamera()
    {
        if (!canBeControlledByCamera || currentState == StoneTrapState.Broken)
        {
            return;
        }

        isPausedByCamera = true;
        EnterPausedPlatform("camera");
    }

    public void ResumeByCamera()
    {
        if (!canBeControlledByCamera)
        {
            return;
        }

        isPausedByCamera = false;
        TryResumeFromPause("camera");
    }

    public void SetCameraPaused(bool paused)
    {
        if (paused)
        {
            PauseByCamera();
        }
        else
        {
            ResumeByCamera();
        }
    }

    public void PauseByShutter()
    {
        if (!canBeControlledByShutter || currentState == StoneTrapState.Broken)
        {
            return;
        }

        isPausedByShutter = true;
        EnterPausedPlatform("shutter");
    }

    public void ResumeByShutter()
    {
        if (!canBeControlledByShutter)
        {
            return;
        }

        isPausedByShutter = false;
        TryResumeFromPause("shutter");
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
        isPausedByCamera = false;
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
        isPausedByCamera = false;
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
        StopMotion();

        if (rb == null)
        {
            Debug.LogWarning("[StoneTrap] Rigidbody is missing. Falling cannot start.", this);
            return;
        }

        if (mainCollider != null)
        {
            mainCollider.enabled = true;
            mainCollider.isTrigger = false;
        }

        ApplyMovementConstraints();
        rb.isKinematic = !useGravityDrop;
        rb.useGravity = useGravityDrop;
        ApplyAnimatorState();
        Log("Falling started");
    }

    private void EnterPausedPlatform(string source)
    {
        if (!canBecomePlatformWhenPaused || currentState == StoneTrapState.Broken)
        {
            return;
        }

        if (currentState != StoneTrapState.Falling && currentState != StoneTrapState.PausedPlatform)
        {
            return;
        }

        currentState = StoneTrapState.PausedPlatform;
        StopMotion();

        if (rb != null)
        {
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        if (mainCollider != null)
        {
            mainCollider.enabled = playerCanStandOnPausedStone;
            mainCollider.isTrigger = false;
        }

        ApplyAnimatorState();
        Log($"PausedPlatform by {source}");
    }

    private void TryResumeFromPause(string source)
    {
        if (!allowResumeAfterPause || currentState == StoneTrapState.Broken)
        {
            return;
        }

        if (currentState != StoneTrapState.PausedPlatform || IsPausedByAnySource)
        {
            Log($"Resume by {source} waiting. Camera={isPausedByCamera}, Shutter={isPausedByShutter}");
            return;
        }

        BeginFalling();
        Log($"Resumed by {source}");
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

        if (lockZPosition)
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
            Log("Hit ground");
            if (breakOnGroundHit)
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
        if ((damageOnlyWhileFalling && currentState != StoneTrapState.Falling) ||
            (disableDamageWhenPaused && currentState == StoneTrapState.PausedPlatform) ||
            currentState == StoneTrapState.Broken)
        {
            return;
        }

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
        if (damageOncePerTarget && !damagedTargets.Add(targetId))
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

        if (visualRoot == null)
        {
            Transform foundVisual = transform.Find("Visual");
            if (foundVisual != null)
            {
                visualRoot = foundVisual;
            }
            else if (animator != null)
            {
                visualRoot = animator.transform;
            }
        }
    }

    private void ApplyAnimatorState()
    {
        if (animator == null)
        {
            return;
        }

        SetBoolIfPresent(IsFallingHash, currentState == StoneTrapState.Falling);
        SetBoolIfPresent(IsPausedHash, currentState == StoneTrapState.PausedPlatform);
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
