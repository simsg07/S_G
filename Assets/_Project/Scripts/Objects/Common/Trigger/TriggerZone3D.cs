using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class TriggerZone3D : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Behaviour that receives TriggerObject when the player enters this zone.")]
    [SerializeField] private MonoBehaviour targetBehaviour;

    [Header("Trigger Rules")]
    [Tooltip("Trigger the connected object when the player enters.")]
    [SerializeField] private bool triggerOnPlayerEnter = true;
    [Tooltip("Allow this trigger to fire only once.")]
    [SerializeField] private bool triggerOnce = true;
    [Tooltip("Player layers allowed to activate this trigger.")]
    [SerializeField] private LayerMask playerLayerMask = ~0;
    [Tooltip("Also require the Player tag.")]
    [SerializeField] private bool usePlayerTag = true;
    [Tooltip("Tag used when usePlayerTag is enabled.")]
    [SerializeField] private string playerTag = "Player";

    [Header("Debug")]
    [Tooltip("Print trigger logs and warnings in the Console.")]
    [SerializeField] private bool debugMode = true;

    private bool hasTriggered;

    private void Reset()
    {
        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void OnValidate()
    {
        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!triggerOnPlayerEnter || other == null || (triggerOnce && hasTriggered))
        {
            return;
        }

        if (!IsPlayerCandidate(other))
        {
            return;
        }

        TriggerTarget();
    }

    public void TriggerTarget()
    {
        if (targetBehaviour == null)
        {
            LogWarning("Target Behaviour is not assigned.");
            return;
        }

        if (targetBehaviour is ITriggerableObject triggerable)
        {
            if (!triggerable.CanTrigger)
            {
                Log("Target cannot trigger now.");
                return;
            }

            triggerable.TriggerObject();
        }
        else
        {
            targetBehaviour.SendMessage("TriggerObject", SendMessageOptions.DontRequireReceiver);
        }

        hasTriggered = true;
        Log($"Triggered {targetBehaviour.name}.");
    }

    public void ResetZone()
    {
        hasTriggered = false;
    }

    public void ConfigureTrigger(bool triggerOnceValue, bool debugModeValue)
    {
        triggerOnce = triggerOnceValue;
        debugMode = debugModeValue;
    }

    private bool IsPlayerCandidate(Collider other)
    {
        if (!IsLayerIncluded(other.gameObject.layer, playerLayerMask))
        {
            return false;
        }

        if (!usePlayerTag)
        {
            return true;
        }

        Transform current = other.transform;
        while (current != null)
        {
            if (current.CompareTag(playerTag))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static bool IsLayerIncluded(int layer, LayerMask layerMask)
    {
        return (layerMask.value & (1 << layer)) != 0;
    }

    private void Log(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[TriggerZone3D] {message}", this);
        }
    }

    private void LogWarning(string message)
    {
        if (debugMode)
        {
            Debug.LogWarning($"[TriggerZone3D] {message}", this);
        }
    }
}
