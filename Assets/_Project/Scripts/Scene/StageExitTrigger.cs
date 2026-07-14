using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider))]
public class StageExitTrigger : MonoBehaviour
{
    [SerializeField] private string nextSceneName;
    [SerializeField] private string targetSpawnPointId = "Default";
    [SerializeField] private bool requireInteraction;
    [SerializeField] private Key interactionKey = Key.E;
    [SerializeField] private bool debugMode = true;

    private bool playerInside;
    private bool isLoading;

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
            triggerCollider.isTrigger = true;
        }
    }

    private void Update()
    {
        if (!requireInteraction || !playerInside || isLoading)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard[interactionKey].wasPressedThisFrame)
        {
            TryLoadNextStage();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other))
        {
            return;
        }

        playerInside = true;

        if (debugMode)
        {
            Debug.Log($"[StageExitTrigger] Player entered. nextSceneName='{nextSceneName}', spawn='{targetSpawnPointId}'.", this);
        }

        if (!requireInteraction)
        {
            TryLoadNextStage();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other))
        {
            playerInside = false;
        }
    }

    private void TryLoadNextStage()
    {
        if (isLoading)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(nextSceneName))
        {
            Debug.LogWarning("[StageExitTrigger] nextSceneName is empty. Scene load skipped.", this);
            return;
        }

        isLoading = true;
        SceneLoader.LoadStage(nextSceneName, targetSpawnPointId);
    }

    private static bool IsPlayer(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            return true;
        }

        Component[] components = other.GetComponentsInParent<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component != null && component.GetType().Name == "PlayerController")
            {
                return true;
            }
        }

        return false;
    }
}
