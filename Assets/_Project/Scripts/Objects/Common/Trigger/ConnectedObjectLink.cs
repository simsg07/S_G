using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class ConnectedObjectLink : MonoBehaviour
{
    [Header("Connection")]
    [Tooltip("Connected object root. Used to resolve trigger and 3D physics components automatically.")]
    [SerializeField] private GameObject connectedObject;
    [Tooltip("Rope end point used as the attachment and detachment reference.")]
    [SerializeField] private Transform connectedObjectAttachPoint;
    [Tooltip("Behaviour that receives TriggerObject and ResetObject calls.")]
    [SerializeField] private MonoBehaviour connectedBehaviour;
    [Tooltip("When true, ActivateConnectedObject triggers the connected behaviour.")]
    [FormerlySerializedAs("triggerOnActivate")]
    [SerializeField] private bool activateOnCut = true;
    [Tooltip("Release a connected 3D Rigidbody when no ITriggerableObject handles the release.")]
    [SerializeField] private bool releasePhysicsOnCut = true;
    [Tooltip("Detach the connected object when it is parented below the attach point.")]
    [SerializeField] private bool detachFromAttachPointOnCut = true;
    [Tooltip("Keep the designer-authored Connected Object local scale unchanged during detach/release.")]
    [SerializeField] private bool preserveConnectedObjectScale = true;

    [Header("Debug")]
    [Tooltip("Print connection logs and warnings in the Console.")]
    [SerializeField] private bool debugMode = true;

    public void ActivateConnectedObject()
    {
        Log("ActivateConnectedObject called.");

        if (!activateOnCut)
        {
            return;
        }

        GameObject target = ResolveConnectedObject();
        PrepareDetach(target);
        if (releasePhysicsOnCut && target != null && target.GetComponentInChildren<FallingBoxObject>(true) != null)
        {
            ReleaseConnectedObject();
            return;
        }

        ITriggerableObject triggerable = ResolveTriggerable();
        if (triggerable != null)
        {
            if (!triggerable.CanTrigger)
            {
                Log("Connected object cannot trigger now.");
                return;
            }

            triggerable.TriggerObject();
            Log("Connected triggerable object activated.");
            return;
        }

        if (releasePhysicsOnCut && ReleaseConnectedObject())
        {
            return;
        }

        if (connectedBehaviour != null)
        {
            connectedBehaviour.SendMessage("TriggerObject", SendMessageOptions.DontRequireReceiver);
            Log($"Activated {connectedBehaviour.name}.");
            return;
        }

        LogWarning("Connected Object or Connected Behaviour is not assigned.");
    }

    public bool ReleaseConnectedObject()
    {
        GameObject target = ResolveConnectedObject();
        if (target == null)
        {
            LogWarning("Connected Object is not assigned; physics release was skipped.");
            return false;
        }

        PrepareDetach(target);

        FallingBoxObject fallingBox = target.GetComponentInChildren<FallingBoxObject>(true);
        if (fallingBox != null)
        {
            fallingBox.TriggerDrop();
            return true;
        }

        GravityObject3D gravityObject = target.GetComponentInChildren<GravityObject3D>(true);
        if (gravityObject != null)
        {
            gravityObject.TriggerDrop();
            return true;
        }

        Rigidbody body = target.GetComponentInChildren<Rigidbody>(true);
        if (body != null)
        {
            body.isKinematic = false;
            body.useGravity = true;
            Log($"Released 3D Rigidbody on {target.name}.");
            return true;
        }

        LogWarning($"{target.name} has no triggerable object or 3D Rigidbody to release.");
        return false;
    }

    public void ResetConnectedObject()
    {
        ITriggerableObject triggerable = ResolveTriggerable();
        if (triggerable != null)
        {
            triggerable.ResetObject();
            Log("Connected triggerable object reset.");
            return;
        }

        if (connectedBehaviour != null)
        {
            GameObject target = ResolveConnectedObject();
            FallingBoxObject fallingBox = target != null ? target.GetComponentInChildren<FallingBoxObject>(true) : null;
            if (fallingBox != null)
            {
                fallingBox.ResetBox();
                Log($"Reset {fallingBox.name}.");
                return;
            }

            GravityObject3D gravityObject = target != null ? target.GetComponentInChildren<GravityObject3D>(true) : null;
            if (gravityObject != null)
            {
                gravityObject.ResetGravityObject();
                Log($"Reset {gravityObject.name}.");
                return;
            }

            connectedBehaviour.SendMessage("ResetObject", SendMessageOptions.DontRequireReceiver);
            Log($"Reset {connectedBehaviour.name}.");
            return;
        }

        LogWarning("Connected Object or Connected Behaviour is not assigned.");
    }

    [ContextMenu("Validate Link Setup")]
    public bool ValidateLinkSetup()
    {
        GameObject target = ResolveConnectedObject();
        if (target == null)
        {
            LogWarning("Link validation: Connected Object and Connected Behaviour are both missing.");
            return false;
        }

        if (connectedObjectAttachPoint == null)
        {
            LogWarning("Link validation: Connected Object Attach Point is missing.");
            return false;
        }

        bool hasTriggerable = ResolveTriggerable() != null;
        bool hasRigidbody = target.GetComponentInChildren<Rigidbody>(true) != null;
        if (!hasTriggerable && (!releasePhysicsOnCut || !hasRigidbody))
        {
            LogWarning($"Link validation: {target.name} has no ITriggerableObject or releasable 3D Rigidbody.");
            return false;
        }

        Log($"Link validation passed: {target.name}.");
        return true;
    }

    private GameObject ResolveConnectedObject()
    {
        return connectedObject != null ? connectedObject : connectedBehaviour != null ? connectedBehaviour.gameObject : null;
    }

    private void PrepareDetach(GameObject target)
    {
        if (!detachFromAttachPointOnCut || target == null || connectedObjectAttachPoint == null ||
            !target.transform.IsChildOf(connectedObjectAttachPoint))
        {
            return;
        }

        Vector3 authoredLocalScale = target.transform.localScale;
        Transform newParent = connectedObjectAttachPoint.parent;
        target.transform.SetParent(newParent, true);
        if (preserveConnectedObjectScale)
        {
            target.transform.localScale = authoredLocalScale;
        }

        Log($"Detached {target.name} from {connectedObjectAttachPoint.name} without changing its authored scale.");
    }

    private ITriggerableObject ResolveTriggerable()
    {
        if (connectedBehaviour is ITriggerableObject direct)
        {
            return direct;
        }

        GameObject target = ResolveConnectedObject();
        if (target == null)
        {
            return null;
        }

        MonoBehaviour[] behaviours = target.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is ITriggerableObject triggerable)
            {
                return triggerable;
            }
        }

        return null;
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
