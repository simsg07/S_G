using UnityEngine;

[DisallowMultipleComponent]
public class ConnectedObjectLink : MonoBehaviour
{
    [Header("Connection")]
    [Tooltip("Behaviour that receives TriggerObject and ResetObject calls.")]
    [SerializeField] private MonoBehaviour connectedBehaviour;
    [Tooltip("When true, ActivateConnectedObject triggers the connected behaviour.")]
    [SerializeField] private bool triggerOnActivate = true;

    [Header("Debug")]
    [Tooltip("Print connection logs and warnings in the Console.")]
    [SerializeField] private bool debugMode = true;

    public void ActivateConnectedObject()
    {
        Log("ActivateConnectedObject called.");

        if (!triggerOnActivate)
        {
            return;
        }

        if (connectedBehaviour == null)
        {
            LogWarning("Connected Behaviour is not assigned.");
            return;
        }

        if (connectedBehaviour is ITriggerableObject triggerable)
        {
            if (!triggerable.CanTrigger)
            {
                Log("Connected object cannot trigger now.");
                return;
            }

            triggerable.TriggerObject();
        }
        else
        {
            connectedBehaviour.SendMessage("TriggerObject", SendMessageOptions.DontRequireReceiver);
        }

        Log($"Activated {connectedBehaviour.name}.");
    }

    public void ResetConnectedObject()
    {
        if (connectedBehaviour == null)
        {
            LogWarning("Connected Behaviour is not assigned.");
            return;
        }

        if (connectedBehaviour is ITriggerableObject triggerable)
        {
            triggerable.ResetObject();
        }
        else
        {
            connectedBehaviour.SendMessage("ResetObject", SendMessageOptions.DontRequireReceiver);
        }

        Log($"Reset {connectedBehaviour.name}.");
    }

    private void Log(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[ConnectedObjectLink] {message}", this);
        }
    }

    private void LogWarning(string message)
    {
        if (debugMode)
        {
            Debug.LogWarning($"[ConnectedObjectLink] {message}", this);
        }
    }
}
