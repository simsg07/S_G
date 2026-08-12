using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

public interface IHowlingInteractable3D
{
    void OnHowlingActivated(GameObject source);
}

public enum HowlingRangeMode
{
    MatchPlayerDetectionRange,
    Custom
}

[DisallowMultipleComponent]
public sealed class HumanBoxHowling : MonoBehaviour
{
    private const int MaxCandidates = 64;

    [Header("Howling")]
    public bool enableHowling = true;
    [FormerlySerializedAs("howlDuration")]
    [Min(0f)] public float howlingDuration = 1.5f;
    [FormerlySerializedAs("howlOnlyOncePerDetection")]
    public bool howlingOncePerLife = true;
    [Header("Howling Range")]
    [SerializeField] private HowlingRangeMode howlingRangeMode = HowlingRangeMode.MatchPlayerDetectionRange;
    [FormerlySerializedAs("howlingRadius")]
    [SerializeField, Min(0f), Tooltip("Used only when Range Mode is Custom.")]
    private float customHowlingRadius = 3f;
    [SerializeField] private MonsterDetection detection;
    public LayerMask howlingInteractableMask;
    [FormerlySerializedAs("howlOrigin")]
    [SerializeField] private Transform howlingOrigin;

    [Header("Player Stun")]
    [SerializeField] private bool howlingStunsPlayer = true;
    [SerializeField, Min(0f)] private float howlingPlayerStunDuration = 1.5f;

    [Header("Debug")]
    public bool debugMode;
    [FormerlySerializedAs("showGizmo")]
    [SerializeField] private bool showHowlingRange = true;
    [SerializeField] private bool showDetectionRange = true;
    [SerializeField] private bool showGizmosWhenNotSelected;
    [SerializeField] private Color howlingRangeColor = Color.cyan;
    [SerializeField] private Color detectionRangeColor = Color.yellow;

    private readonly Collider[] candidates = new Collider[MaxCandidates];
    private readonly HashSet<IHowlingInteractable3D> invoked = new HashSet<IHowlingInteractable3D>();
    private readonly HashSet<IGravityActivatable3D> gravityInvoked = new HashSet<IGravityActivatable3D>();
    private bool playerStunAppliedThisHowl;
    private bool playerStunAttemptedThisHowl;
    private bool missingStunReceiverWarningLogged;

    public HowlingRangeMode RangeMode => howlingRangeMode;
    public float CustomHowlingRadius => customHowlingRadius;
    public float EffectiveHowlingRadius => howlingRangeMode == HowlingRangeMode.MatchPlayerDetectionRange && detection != null
        ? detection.PlayerDetectionRange
        : customHowlingRadius;
    public Vector3 HowlingOriginPosition => howlingOrigin != null ? howlingOrigin.position : transform.position;

    private void Awake()
    {
        CacheReferences();
    }

    private void Reset()
    {
        CacheReferences();
    }

    public void BeginHowling(GameObject source, Transform playerTarget)
    {
        playerStunAppliedThisHowl = false;
        playerStunAttemptedThisHowl = false;
        TryApplyPlayerStun(playerTarget);
        ActivateInteractables(source);
    }

    public bool TryApplyPlayerStun(Transform playerTarget)
    {
        if (!MonsterWorldSimulationGate3D.AllowsPlayerInteraction(this)) return false;
        if (playerStunAppliedThisHowl || playerStunAttemptedThisHowl) return playerStunAppliedThisHowl;
        playerStunAttemptedThisHowl = true;

        if (!enableHowling || !howlingStunsPlayer || howlingPlayerStunDuration <= 0f || playerTarget == null)
            return false;
        if (!playerTarget.gameObject.activeInHierarchy || !IsInsideHowlingRange(playerTarget.position))
            return false;

        PlayerDamageReceiver damageReceiver = playerTarget.GetComponent<PlayerDamageReceiver>()
            ?? playerTarget.GetComponentInParent<PlayerDamageReceiver>()
            ?? playerTarget.GetComponentInChildren<PlayerDamageReceiver>();
        if (damageReceiver != null && damageReceiver.IsDead) return false;

        IStunnable stunReceiver = playerTarget.GetComponent<IStunnable>()
            ?? playerTarget.GetComponentInParent<IStunnable>()
            ?? playerTarget.GetComponentInChildren<IStunnable>();
        if (stunReceiver == null)
        {
            WarnMissingStunReceiverOnce();
            return false;
        }

        stunReceiver.Stun(howlingPlayerStunDuration);
        playerStunAppliedThisHowl = true;
        return true;
    }

    public int ActivateInteractables(GameObject source)
    {
        if (!enableHowling) return 0;

        invoked.Clear();
        gravityInvoked.Clear();
        Vector3 center = HowlingOriginPosition;
        float effectiveRadius = EffectiveHowlingRadius;
        int count = Physics.OverlapSphereNonAlloc(center, effectiveRadius, candidates,
            howlingInteractableMask, QueryTriggerInteraction.Collide);
        int gravityActivations = 0;

        for (int i = 0; i < count; i++)
        {
            Collider candidate = candidates[i];
            candidates[i] = null;
            if (candidate == null || !candidate.gameObject.activeInHierarchy || candidate.transform.IsChildOf(transform)) continue;

            IHowlingInteractable3D target = candidate.GetComponent<IHowlingInteractable3D>()
                ?? candidate.GetComponentInParent<IHowlingInteractable3D>()
                ?? candidate.GetComponentInChildren<IHowlingInteractable3D>();
            if (target != null && invoked.Add(target))
            {
                Component component = target as Component;
                if (IsUsable(component)) target.OnHowlingActivated(source);
            }

            IGravityActivatable3D gravityTarget = candidate.GetComponent<IGravityActivatable3D>()
                ?? candidate.GetComponentInParent<IGravityActivatable3D>()
                ?? candidate.GetComponentInChildren<IGravityActivatable3D>();
            if (gravityTarget == null || !gravityInvoked.Add(gravityTarget)) continue;
            Component gravityComponent = gravityTarget as Component;
            if (IsUsable(gravityComponent) && gravityTarget.TryActivateGravity(source)) gravityActivations++;
        }

        if (debugMode)
            Debug.Log($"[HumanBoxHowling] Radius: {effectiveRadius:0.##}, Colliders: {count}, Unique interactables: {invoked.Count + gravityInvoked.Count}, Gravity activations: {gravityActivations}, GOJ activations: {invoked.Count}, Player stunned: {playerStunAppliedThisHowl}", this);
        return invoked.Count + gravityInvoked.Count;
    }

    private static bool IsUsable(Component component)
    {
        if (component == null || !component.gameObject.activeInHierarchy) return false;
        WorldPresence presence = component.GetComponentInParent<WorldPresence>();
        return presence == null || presence.IsPresentInCurrentWorld;
    }

    private void OnValidate()
    {
        howlingDuration = Mathf.Max(0f, howlingDuration);
        customHowlingRadius = Mathf.Max(0f, customHowlingRadius);
        howlingPlayerStunDuration = Mathf.Max(0f, howlingPlayerStunDuration);
        CacheReferences();
    }

    private bool IsInsideHowlingRange(Vector3 targetPosition)
    {
        float effectiveRadius = EffectiveHowlingRadius;
        return (targetPosition - HowlingOriginPosition).sqrMagnitude <= effectiveRadius * effectiveRadius;
    }

    private void CacheReferences()
    {
        if (detection == null) detection = GetComponent<MonsterDetection>();
    }

    private void WarnMissingStunReceiverOnce()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (missingStunReceiverWarningLogged) return;
        missingStunReceiverWarningLogged = true;
        Debug.LogWarning("[HumanBox] PlayerStunReceiver was not found. Howling stun was skipped.", this);
#endif
    }

    private void OnDrawGizmos()
    {
        if (showGizmosWhenNotSelected) DrawHowlingGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmosWhenNotSelected) DrawHowlingGizmos();
    }

    private void DrawHowlingGizmos()
    {
        CacheReferences();
        Vector3 howlingCenter = HowlingOriginPosition;
        if (showDetectionRange && detection != null)
        {
            Gizmos.color = detectionRangeColor;
            Gizmos.DrawWireSphere(transform.position, detection.PlayerDetectionRange);
#if UNITY_EDITOR
            Handles.color = detectionRangeColor;
            Handles.Label(transform.position + Vector3.up * detection.PlayerDetectionRange,
                $"Detection: {detection.PlayerDetectionRange:0.0}");
#endif
        }
        if (showHowlingRange)
        {
            Gizmos.color = howlingRangeColor;
            Gizmos.DrawWireSphere(howlingCenter, EffectiveHowlingRadius);
#if UNITY_EDITOR
            Handles.color = howlingRangeColor;
            string mode = howlingRangeMode == HowlingRangeMode.MatchPlayerDetectionRange ? "Matched" : "Custom";
            Handles.Label(howlingCenter + Vector3.up * EffectiveHowlingRadius + Vector3.right * 0.15f,
                $"Howling: {EffectiveHowlingRadius:0.0} ({mode})");
#endif
        }
    }
}
