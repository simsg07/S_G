using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class CraneLeverSwitch : MonoBehaviour, IInteractable3D
{
    [Header("Crane Target")]
    [Tooltip("Crane controlled by this Lever. A scene Lever must reference the CraneObject in the same scene.")]
    [SerializeField] private CraneObject targetCrane;
    [Tooltip("If Target Crane is empty, use the only CraneObject found in the scene. If multiple cranes exist, assign Target Crane manually.")]
    [SerializeField] private bool autoFindSingleCraneIfMissing = true;
    [Tooltip("Allow the Lever to reverse the target while the Crane is travelling.")]
    [SerializeField] private bool canUseWhileCraneMoving;

    [Header("Player Interaction")]
    [Tooltip("When enabled, fallback keyboard input works only while a Player is inside this trigger.")]
    [SerializeField] private bool requirePlayerInRange = true;
    [Tooltip("Use the local keyboard fallback when no existing interaction sender calls this Lever.")]
    [SerializeField] private bool useFallbackInput = true;
    [SerializeField] private LayerMask playerLayerMask;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private Key fallbackInteractKey = Key.F;
    [SerializeField] private bool playerInRange;

    [Header("Optional Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string activateTriggerName = "Activate";

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;

    private int lastInteractionFrame = -1;

    private void Awake()
    {
        ResolveTargetCrane(false);
    }

    private void Reset()
    {
        Collider trigger = GetComponent<Collider>();
        if (trigger != null)
        {
            trigger.isTrigger = true;
        }
    }

    private void Update()
    {
        if (!useFallbackInput)
        {
            return;
        }

        if (requirePlayerInRange && !playerInRange)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && fallbackInteractKey != Key.None && keyboard[fallbackInteractKey].wasPressedThisFrame)
        {
            Interact();
        }
    }

    public bool TryInteract(GameObject actor)
    {
        if (actor != null && !IsPlayer(actor.transform))
        {
            return false;
        }

        return ActivateLever();
    }

    public void Interact()
    {
        ActivateLever();
    }

    public bool ActivateLever()
    {
        Log("Interact called.");
        if (lastInteractionFrame == Time.frameCount)
        {
            return false;
        }

        lastInteractionFrame = Time.frameCount;
        ResolveTargetCrane(true);
        if (targetCrane == null)
        {
            Debug.LogWarning("[CraneLeverSwitch] targetCrane is missing.", this);
            return false;
        }

        if (targetCrane.IsMoving && !canUseWhileCraneMoving)
        {
            Log("Input ignored while Crane is moving.");
            return false;
        }

        if (!targetCrane.TryToggleMoveTarget(canUseWhileCraneMoving))
        {
            Debug.LogWarning("[CraneLeverSwitch] Crane rejected the move request. Validate its Rail Path setup.", this);
            return false;
        }

        Log("ToggleMoveTarget sent to Crane.");

        if (animator != null && HasTriggerParameter(activateTriggerName))
        {
            animator.SetTrigger(activateTriggerName);
        }

        Log("Lever activated.");
        return true;
    }

    public void SetTargetCrane(CraneObject crane)
    {
        targetCrane = crane;
    }

    [ContextMenu("Find Single Crane In Scene")]
    public void FindSingleCraneInScene()
    {
        ResolveTargetCrane(true);
    }

    [ContextMenu("Test Interact")]
    public void TestInteract()
    {
        Interact();
    }

    [ContextMenu("Validate Lever Setup")]
    public void ValidateLeverSetup()
    {
        Collider trigger = GetComponent<Collider>();
        Debug.Log(
            "[CraneLeverSwitch] Validate Lever Setup\n" +
            $"- Lever: {name}\n" +
            $"- Target Crane: {(targetCrane != null ? targetCrane.name : "None")}\n" +
            $"- Auto Find Single Crane: {autoFindSingleCraneIfMissing}\n" +
            $"- Trigger Collider: {(trigger != null && trigger.isTrigger)}\n" +
            $"- Use Fallback Input: {useFallbackInput}\n" +
            $"- Player Layer Mask: {playerLayerMask.value}\n" +
            $"- Player Tag Fallback: {playerTag}\n" +
            $"- Animator: {(animator != null ? animator.name : "None")}",
            this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other != null && IsPlayer(other.transform))
        {
            playerInRange = true;
            Log("Player entered range.");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other != null && IsPlayer(other.transform))
        {
            playerInRange = false;
            Log("Player exited range.");
        }
    }

    private bool IsPlayer(Transform target)
    {
        Transform current = target;
        while (current != null)
        {
            if (playerLayerMask.value != 0 && (playerLayerMask.value & (1 << current.gameObject.layer)) != 0)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(playerTag) && current.CompareTag(playerTag))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private void ResolveTargetCrane(bool logResult)
    {
        if (targetCrane != null || !autoFindSingleCraneIfMissing)
        {
            return;
        }

        CraneObject[] cranes = FindObjectsByType<CraneObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (cranes.Length == 1)
        {
            targetCrane = cranes[0];
            Log($"Auto-linked to Crane '{targetCrane.name}'.");
            return;
        }

        if (logResult)
        {
            Debug.LogWarning($"[CraneLeverSwitch] Could not auto-link Crane. Found {cranes.Length} CraneObject(s). Assign Target Crane manually.", this);
        }
    }

    private bool HasTriggerParameter(string parameterName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(parameterName))
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == AnimatorControllerParameterType.Trigger && parameters[i].name == parameterName)
            {
                return true;
            }
        }

        return false;
    }

    private void Log(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[CraneLeverSwitch] {message}", this);
        }
    }
}
