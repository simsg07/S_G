using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ShutterTarget3D : MonoBehaviour, IMarkable3D, IMarkState3D
{
    [Header("Shutter State")]
    [SerializeField, Tooltip("이 오브젝트가 셔터 입력을 받을 수 있습니다.")] private bool canBeShuttered = true;
    [SerializeField] private bool isMarked;
    [SerializeField] private bool isPausedByShutter;
    [SerializeField] private bool hasWorldShifted;

    [Header("Mark Duration")]
    [SerializeField, Min(0f), Tooltip("0보다 크면 Mark가 자동 해제되는 시간입니다.")] private float markDuration;
    [SerializeField, Tooltip("Mark를 두 번째 셔터 입력까지 유지합니다.")] private bool markDurationInfinite = true;

    [Header("Behavior")]
    [SerializeField] private bool pauseOnFirstUse = true;
    [SerializeField] private bool switchWorldOnSecondUse = true;
    [SerializeField] private bool resumeAfterWorldSwitch = true;

    [Header("References")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Animator animator;
    [SerializeField] private Renderer[] renderers = new Renderer[0];
    [SerializeField] private Behaviour[] behavioursToPause = new Behaviour[0];
    [SerializeField] private MonoBehaviour[] scriptsToPause = new MonoBehaviour[0];
    [SerializeField] private WorldPresence worldPresence;
    [SerializeField] private WorldSwitchable worldSwitchable;
    [SerializeField] private Transform markVisual;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;
    [SerializeField] private bool showMarkGizmo = true;

    private readonly List<BehaviourState> pausedBehaviours = new List<BehaviourState>();
    private Vector3 storedVelocity;
    private Vector3 storedAngularVelocity;
    private bool storedUseGravity;
    private bool storedIsKinematic;
    private float storedAnimatorSpeed = 1f;
    private float markExpireTime;
    private BoomberBrain boomber;

    public bool IsMarked => isMarked;
    public float VisualMarkEndTime => markDurationInfinite ? float.PositiveInfinity : Time.time + Mathf.Max(0f, markExpireTime - Time.unscaledTime);

    private void Awake()
    {
        CacheReferences();
        UpdateMarkVisual();
    }

    private void Update()
    {
        if (isMarked && !markDurationInfinite && markDuration > 0f && Time.unscaledTime >= markExpireTime)
        {
            ClearMark();
            ResumeByShutter();
        }
    }

    private void OnDisable()
    {
        ResumeByShutter();
        ClearMark();
    }

    private void OnValidate()
    {
        markDuration = Mathf.Max(0f, markDuration);
        CacheReferences();
    }

    public bool CanReceiveShutter()
    {
        if (!canBeShuttered || !isActiveAndEnabled || !SafeMath3D.IsValidTransform(transform)) return false;
        if (boomber != null && !boomber.CanBePausedByShutter()) return false;
        return true;
    }

    [ContextMenu("Test Apply Shutter")]
    public void ApplyShutter()
    {
        if (!CanReceiveShutter())
        {
            Log("Camera Mark ignored: unavailable or invalid.");
            return;
        }

        ApplyFirstShutterUse();
    }

    [ContextMenu("Test First Use Mark Pause")]
    public void ApplyFirstShutterUse()
    {
        isMarked = true;
        hasWorldShifted = false;
        markExpireTime = markDurationInfinite || markDuration <= 0f ? float.MaxValue : Time.unscaledTime + markDuration;
        UpdateMarkVisual();
        if (pauseOnFirstUse) PauseByShutter();
        Log("First use: Mark + Pause.");
    }

    [ContextMenu("Test Second Use World Switch")]
    public void ApplySecondShutterUse()
    {
        if (switchWorldOnSecondUse) SwitchObjectWorld();
        ClearMark();
        if (resumeAfterWorldSwitch) ResumeByShutter();
        Log("Second use: object world switch.");
    }

    public void PauseByShutter()
    {
        if (isPausedByShutter) return;
        if (boomber != null && !boomber.CanBePausedByShutter()) return;

        isPausedByShutter = true;
        if (rb != null)
        {
            storedVelocity = SafeMath3D.SafeVector3(rb.linearVelocity, Vector3.zero);
            storedAngularVelocity = SafeMath3D.SafeVector3(rb.angularVelocity, Vector3.zero);
            storedUseGravity = rb.useGravity;
            storedIsKinematic = rb.isKinematic;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        if (animator != null)
        {
            storedAnimatorSpeed = SafeMath3D.IsFinite(animator.speed) ? animator.speed : 1f;
            animator.speed = 0f;
        }

        boomber?.PauseByShutter();
        PauseListedBehaviours();
        Log("Paused by shutter.");
    }

    [ContextMenu("Test Resume")]
    public void ResumeByShutter()
    {
        if (!isPausedByShutter) return;
        isPausedByShutter = false;

        if (rb != null)
        {
            rb.isKinematic = storedIsKinematic;
            rb.useGravity = storedUseGravity;
            if (!rb.isKinematic)
            {
                rb.linearVelocity = SafeMath3D.SafeVector3(storedVelocity, Vector3.zero);
                rb.angularVelocity = SafeMath3D.SafeVector3(storedAngularVelocity, Vector3.zero);
            }
        }

        if (animator != null) animator.speed = SafeMath3D.IsFinite(storedAnimatorSpeed) ? storedAnimatorSpeed : 1f;
        RestoreListedBehaviours();
        boomber?.ResumeByShutter();
        Log("Resumed after shutter.");
    }

    public void SwitchObjectWorld()
    {
        if (worldSwitchable != null)
        {
            worldSwitchable.ToggleWorld();
            hasWorldShifted = true;
            return;
        }

        Debug.LogWarning("[ShutterTarget3D] WorldSwitchable이 없어 개별 월드 전환을 수행하지 않았습니다.", this);
    }

    [ContextMenu("Clear Mark")]
    public void ClearMark()
    {
        isMarked = false;
        markExpireTime = 0f;
        UpdateMarkVisual();
    }

    [ContextMenu("Validate Shutter Target")]
    public void ValidateShutterTargetSetup()
    {
        CacheReferences();
        Log($"RB={rb != null}, Animator={animator != null}, WorldPresence={worldPresence != null}, WorldSwitchable={worldSwitchable != null}, Boomber={boomber != null}");
    }

    public bool ApplyMark(float duration, CameraAbilitySystem3D source)
    {
        if (!canBeShuttered || duration <= 0f || !isActiveAndEnabled) return false;
        if (!isMarked) PauseByShutter();
        isMarked = true;
        markDurationInfinite = false;
        markDuration = duration;
        markExpireTime = Time.unscaledTime + duration;
        UpdateMarkVisual();
        return true;
    }

    private void CacheReferences()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        if (worldPresence == null) worldPresence = GetComponent<WorldPresence>();
        if (worldSwitchable == null) worldSwitchable = GetComponent<WorldSwitchable>();
        if (boomber == null) boomber = GetComponent<BoomberBrain>();
        if (renderers == null || renderers.Length == 0) renderers = GetComponentsInChildren<Renderer>(true);
    }

    private void PauseListedBehaviours()
    {
        pausedBehaviours.Clear();
        PauseArray(behavioursToPause);
        PauseArray(scriptsToPause);
    }

    private void PauseArray(Behaviour[] targets)
    {
        if (targets == null) return;
        foreach (Behaviour target in targets)
        {
            if (target == null || target == this || target == boomber || target is BoomberExplosion || !target.enabled) continue;
            pausedBehaviours.Add(new BehaviourState(target, true));
            target.enabled = false;
        }
    }

    private void RestoreListedBehaviours()
    {
        foreach (BehaviourState state in pausedBehaviours)
        {
            if (state.Target != null) state.Target.enabled = state.WasEnabled;
        }
        pausedBehaviours.Clear();
    }

    private void UpdateMarkVisual()
    {
        if (markVisual != null) markVisual.gameObject.SetActive(isMarked);
    }

    private void OnDrawGizmosSelected()
    {
        if (!showMarkGizmo || !isMarked || !SafeMath3D.IsValidTransform(transform)) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.35f);
    }

    private void Log(string message)
    {
        if (debugMode) Debug.Log($"[ShutterTarget3D] {message}", this);
    }

    private readonly struct BehaviourState
    {
        public BehaviourState(Behaviour target, bool wasEnabled) { Target = target; WasEnabled = wasEnabled; }
        public Behaviour Target { get; }
        public bool WasEnabled { get; }
    }
}
