using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class StoneTrigger : MonoBehaviour
{
    public StoneTrap targetStone;
    public bool triggerOnPlayerEnter = true;
    public bool triggerOnce = true;
    public LayerMask playerLayerMask;
    public bool debugMode = true;

    private bool triggered;
    private bool warnedMissingStone;

    private void Reset()
    {
        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void Awake()
    {
        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null && !triggerCollider.isTrigger)
        {
            Debug.LogWarning("[StoneTrigger] Collider should have Is Trigger enabled.", this);
        }

        WarnIfStoneMissing();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!triggerOnPlayerEnter || (triggerOnce && triggered) || other == null || !IsPlayer(other))
        {
            return;
        }

        if (targetStone == null)
        {
            WarnIfStoneMissing();
            return;
        }

        triggered = true;
        targetStone.TriggerDrop();
        Log($"Triggered by {other.name}");
    }

    public void ResetTrigger()
    {
        triggered = false;
    }

    private bool IsPlayer(Collider other)
    {
        bool layerMatches = playerLayerMask.value != 0 &&
            (playerLayerMask.value & (1 << other.gameObject.layer)) != 0;
        if (layerMatches || other.GetComponentInParent<PlayerDamageReceiver>() != null)
        {
            return true;
        }

        Transform current = other.transform;
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

    private void WarnIfStoneMissing()
    {
        if (targetStone != null || warnedMissingStone)
        {
            return;
        }

        warnedMissingStone = true;
        Debug.LogWarning("[StoneTrigger] Target Stone is not assigned.", this);
    }

    private void Log(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[StoneTrigger] {message}", this);
        }
    }
}
