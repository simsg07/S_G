using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider))]
public class StageExitTrigger : MonoBehaviour
{
    [Header("Stage Target")]
    [SerializeField] private string exitId = "Exit_Default";
    [Tooltip("Scene name registered in Build Settings. Example: Stage_02")]
    [SerializeField] private string nextSceneName;
    [Tooltip("PlayerSpawnPoint.spawnPointId in the target scene. Example: From_Stage01")]
    [SerializeField] private string targetSpawnPointId = "Default";

    [Header("Activation")]
    [Tooltip("When false, entering the trigger loads the next scene immediately. When true, press interactionKey while inside.")]
    [SerializeField] private bool requireInteraction;
    [SerializeField] private bool isLocked;
    [Tooltip("Input System key used when requireInteraction is enabled.")]
    [SerializeField] private Key interactionKey = Key.E;
    [Tooltip("If enabled, this trigger can load only once.")]
    [SerializeField] private bool triggerOnce = true;
    [Tooltip("Optional Player layer filter. Leave as 0 to use Player tag / PlayerController / PlatformerPlayer3D checks.")]
    [SerializeField] private LayerMask playerLayerMask;

    [Header("Debug")]
    [Tooltip("Print trigger enter and scene-load logs in the Console.")]
    [SerializeField] private bool debugMode = true;

    private bool playerInside;
    private bool isLoading;
    private bool hasTriggered;

    public string NextSceneName => nextSceneName;
    public string TargetSpawnPointId => targetSpawnPointId;
    public bool RequireInteraction => requireInteraction;
    public LayerMask PlayerLayerMask => playerLayerMask;

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
        if (!requireInteraction || !playerInside || isLoading || (triggerOnce && hasTriggered))
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
        if (isLocked)
        {
            Debug.LogWarning($"[StageExitTrigger] Exit is locked: {exitId}", this);
            return;
        }

        if (isLoading)
        {
            Debug.LogWarning("[StageExitTrigger] Scene load request ignored because this exit is already loading.", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(nextSceneName))
        {
            Debug.LogWarning("[StageExitTrigger] nextSceneName is empty. Scene transition cancelled.", this);
            return;
        }

        if (!SceneLoader.IsSceneRegisteredInBuildSettings(nextSceneName))
        {
            Debug.LogWarning($"[StageExitTrigger] nextSceneName is not registered in Build Settings: {nextSceneName}", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(targetSpawnPointId))
        {
            Debug.LogWarning("[StageExitTrigger] targetSpawnPointId is empty. SceneLoader will use Default.", this);
        }

        bool requestAccepted = SceneLoader.TryLoadStage(nextSceneName, targetSpawnPointId);
        if (!requestAccepted)
        {
            Debug.LogWarning("[StageExitTrigger] Scene transition request was rejected by SceneLoader.", this);
            return;
        }

        isLoading = true;
        hasTriggered = true;
    }

    [ContextMenu("Validate Scene Connection")]
    public void ValidateSceneConnection()
    {
        Collider triggerCollider = GetComponent<Collider>();
        bool sceneNameValid = !string.IsNullOrWhiteSpace(nextSceneName);
        bool sceneInBuildSettings = sceneNameValid && SceneLoader.IsSceneRegisteredInBuildSettings(nextSceneName);
        bool spawnIdValid = !string.IsNullOrWhiteSpace(targetSpawnPointId);
        bool hasSceneLoader = SceneLoader.Instance != null || FindAnyObjectByType<SceneLoader>() != null;
        bool colliderIsTrigger = triggerCollider != null && triggerCollider.isTrigger;
        bool layerMaskValid = playerLayerMask.value != 0;

        Debug.Log(
            "[StageExitTrigger] Validate Scene Connection\n" +
            $"- Object: {name}\n" +
            $"- nextSceneName: {(sceneNameValid ? nextSceneName : "(empty)")}\n" +
            $"- Scene in Build Settings: {sceneInBuildSettings}\n" +
            $"- targetSpawnPointId: {(spawnIdValid ? targetSpawnPointId : "(empty -> Default)")}\n" +
            $"- SceneLoader in current scene: {hasSceneLoader}\n" +
            $"- Collider exists: {triggerCollider != null}\n" +
            $"- Collider isTrigger: {colliderIsTrigger}\n" +
            $"- playerLayerMask set: {layerMaskValid}\n" +
            $"- requireInteraction: {requireInteraction}\n" +
            $"- triggerOnce: {triggerOnce}",
            this);

        if (!sceneNameValid)
        {
            Debug.LogWarning("[StageExitTrigger] nextSceneName is empty. Scene transition will be cancelled.", this);
        }

        if (sceneNameValid && !sceneInBuildSettings)
        {
            Debug.LogWarning($"[StageExitTrigger] Scene is not registered in Build Settings: {nextSceneName}", this);
        }

        if (!spawnIdValid)
        {
            Debug.LogWarning("[StageExitTrigger] targetSpawnPointId is empty. Default will be used.", this);
        }

        if (!hasSceneLoader)
        {
            Debug.LogWarning("[StageExitTrigger] SceneLoader was not found in the current scene. Runtime can auto-create one, but placing SceneLoader prefab is recommended.", this);
        }

        if (!colliderIsTrigger)
        {
            Debug.LogWarning("[StageExitTrigger] Collider must be isTrigger=true.", this);
        }

        if (!layerMaskValid)
        {
            Debug.LogWarning("[StageExitTrigger] playerLayerMask is empty. Tag/Player component fallback will be used.", this);
        }
    }

    private bool IsPlayer(Collider other)
    {
        if (other == null)
        {
            return false;
        }

        if (playerLayerMask.value != 0 &&
            (playerLayerMask.value & (1 << other.gameObject.layer)) != 0)
        {
            return true;
        }

        if (other.CompareTag("Player"))
        {
            return true;
        }

        Component[] components = other.GetComponentsInParent<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component != null &&
                (component.GetType().Name == "PlayerController" ||
                 component.GetType().Name == "PlatformerPlayer3D"))
            {
                return true;
            }
        }

        return false;
    }
}
