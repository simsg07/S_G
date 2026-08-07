using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public interface IHowlingInteractable3D
{
    void OnHowlingActivated(GameObject source);
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
    [Min(0f)] public float howlingRadius = 3f;
    public LayerMask howlingInteractableMask = ~0;
    public Transform howlOrigin;

    [Header("Player Stun")]
    [SerializeField] private bool howlingStunsPlayer = true;
    [SerializeField, Min(0f)] private float howlingPlayerStunDuration = 1.5f;

    [Header("Debug")]
    public bool debugMode;
    public bool showGizmo = true;

    private readonly Collider[] candidates = new Collider[MaxCandidates];
    private readonly HashSet<IHowlingInteractable3D> invoked = new HashSet<IHowlingInteractable3D>();
    private bool playerStunAppliedThisHowl;
    private bool playerStunAttemptedThisHowl;
    private bool missingStunReceiverWarningLogged;

    public void BeginHowling(GameObject source, Transform playerTarget)
    {
        playerStunAppliedThisHowl = false;
        playerStunAttemptedThisHowl = false;
        TryApplyPlayerStun(playerTarget);
        ActivateInteractables(source);
    }

    public bool TryApplyPlayerStun(Transform playerTarget)
    {
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
        Vector3 center = howlOrigin != null ? howlOrigin.position : transform.position;
        int count = Physics.OverlapSphereNonAlloc(center, howlingRadius, candidates,
            howlingInteractableMask, QueryTriggerInteraction.Collide);

        for (int i = 0; i < count; i++)
        {
            Collider candidate = candidates[i];
            candidates[i] = null;
            if (candidate == null || !candidate.gameObject.activeInHierarchy || candidate.transform.IsChildOf(transform)) continue;

            IHowlingInteractable3D target = candidate.GetComponent<IHowlingInteractable3D>()
                ?? candidate.GetComponentInParent<IHowlingInteractable3D>()
                ?? candidate.GetComponentInChildren<IHowlingInteractable3D>();
            if (target == null || !invoked.Add(target)) continue;

            Component component = target as Component;
            if (component == null || !component.gameObject.activeInHierarchy) continue;
            target.OnHowlingActivated(source);
        }

        if (debugMode) Debug.Log($"[HumanBoxHowling] Activated {invoked.Count} interactable(s).", this);
        return invoked.Count;
    }

    private void OnValidate()
    {
        howlingDuration = Mathf.Max(0f, howlingDuration);
        howlingRadius = Mathf.Max(0f, howlingRadius);
        howlingPlayerStunDuration = Mathf.Max(0f, howlingPlayerStunDuration);
    }

    private bool IsInsideHowlingRange(Vector3 targetPosition)
    {
        Vector3 center = howlOrigin != null ? howlOrigin.position : transform.position;
        return (targetPosition - center).sqrMagnitude <= howlingRadius * howlingRadius;
    }

    private void WarnMissingStunReceiverOnce()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (missingStunReceiverWarningLogged) return;
        missingStunReceiverWarningLogged = true;
        Debug.LogWarning("[HumanBox] PlayerStunReceiver was not found. Howling stun was skipped.", this);
#endif
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmo) return;
        Gizmos.color = new Color(1f, 0.35f, 0.1f, 0.7f);
        Gizmos.DrawWireSphere(howlOrigin != null ? howlOrigin.position : transform.position, howlingRadius);
    }
}
