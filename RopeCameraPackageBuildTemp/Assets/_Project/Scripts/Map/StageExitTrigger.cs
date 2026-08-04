using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(Collider))]
public class StageExitTrigger : MonoBehaviour
{
    public enum SceneTransitionType
    {
        Immediate
    }

    [Header("Stage Target")]
    [SerializeField] private bool connectionEnabled = true;
    [SerializeField] private string exitId = "Exit_Default";
#if UNITY_EDITOR
    [Tooltip("Drag a scene asset here. The runtime scene name below is updated automatically.")]
    [SerializeField] private SceneAsset targetScene;
#endif
    [Tooltip("Scene name registered in Build Settings. Example: Stage_02")]
    [SerializeField] private string nextSceneName;
    [Tooltip("PlayerSpawnPoint.spawnPointId in the target scene. Example: From_Stage01")]
    [SerializeField] private string targetSpawnPointId = "Default";

    [Header("Activation")]
    [SerializeField] private string requiredPlayerTag = "Player";
    [SerializeField] private SceneTransitionType transitionType = SceneTransitionType.Immediate;
    [Tooltip("When false, entering the trigger loads the next scene immediately. When true, press interactionKey while inside.")]
    [SerializeField] private bool useInteractionKey;
    [SerializeField] private bool isLocked;
    [SerializeField] private bool useFade;
    [Tooltip("Input System key used when requireInteraction is enabled.")]
    [SerializeField] private Key interactionKey = Key.E;
    [Tooltip("If enabled, this trigger can load only once.")]
    [SerializeField] private bool triggerOnce = true;
    [Tooltip("Optional Player layer filter. Leave as 0 to use Player tag / PlayerController / PlatformerPlayer3D checks.")]
    [SerializeField] private LayerMask playerLayerMask;
    [Header("Interaction Events")]
    [SerializeField] private UnityEvent onInteractionAvailable;
    [SerializeField] private UnityEvent onInteractionUnavailable;

    [Header("Debug")]
    [Tooltip("Print trigger enter and scene-load logs in the Console.")]
    [SerializeField] private bool debugMode = true;

    private bool playerInside;
    private bool isLoading;
    private bool hasTriggered;
    private bool hasExitedSinceSpawn;
    private static float transitionBlockedUntil;

    public string NextSceneName => nextSceneName;
    public string TargetSpawnPointId => targetSpawnPointId;
    public SceneTransitionType TransitionType => transitionType;
    public bool RequireInteraction => useInteractionKey;
    public LayerMask PlayerLayerMask => playerLayerMask;
    public bool ConnectionEnabled => connectionEnabled;
    public bool UseFade => useFade;
#if UNITY_EDITOR
    public SceneAsset TargetSceneAsset => targetScene;
#endif

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

        hasExitedSinceSpawn = Time.unscaledTime >= transitionBlockedUntil;
    }

    public static void BeginSpawnSafety(float seconds = 0.35f)
    {
        transitionBlockedUntil = Mathf.Max(transitionBlockedUntil, Time.unscaledTime + Mathf.Max(0f, seconds));
    }

    private void Update()
    {
        if (!hasExitedSinceSpawn && !playerInside && Time.unscaledTime >= transitionBlockedUntil)
        {
            hasExitedSinceSpawn = true;
        }

        if (!useInteractionKey || !playerInside || !CanActivate() || isLoading || (triggerOnce && hasTriggered))
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
        if (useInteractionKey)
        {
            onInteractionAvailable?.Invoke();
        }

        if (!CanActivate())
        {
            return;
        }

        if (debugMode)
        {
            Debug.Log($"[StageExitTrigger] Player entered. nextSceneName='{nextSceneName}', spawn='{targetSpawnPointId}'.", this);
        }

        if (!useInteractionKey)
        {
            TryLoadNextStage();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other))
        {
            playerInside = false;
            hasExitedSinceSpawn = true;
            if (useInteractionKey)
            {
                onInteractionUnavailable?.Invoke();
            }
        }
    }

    private void TryLoadNextStage()
    {
        if (!connectionEnabled)
        {
            return;
        }
        if (!CanActivate())
        {
            return;
        }
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

    private bool CanActivate()
    {
        return hasExitedSinceSpawn && Time.unscaledTime >= transitionBlockedUntil;
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
            $"- useInteractionKey: {useInteractionKey}\n" +
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

        if (!string.IsNullOrWhiteSpace(requiredPlayerTag) && other.CompareTag(requiredPlayerTag))
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

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (targetScene != null)
        {
            nextSceneName = targetScene.name;
        }

        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void OnDrawGizmos()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        bool valid = connectionEnabled && !string.IsNullOrWhiteSpace(nextSceneName) && !string.IsNullOrWhiteSpace(targetSpawnPointId);
        Gizmos.color = valid ? new Color(0.15f, 0.75f, 1f, 0.28f) : new Color(1f, 0.2f, 0.15f, 0.35f);
        if (box != null)
        {
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = valid ? Color.cyan : Color.red;
            Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.matrix = oldMatrix;
        }

        Vector3 direction = name.IndexOf("Left", System.StringComparison.OrdinalIgnoreCase) >= 0 ? Vector3.left : Vector3.right;
        Gizmos.color = valid ? new Color(1f, 0.55f, 0.05f) : Color.red;
        Gizmos.DrawLine(transform.position, transform.position + direction * 1.2f);
#if UNITY_EDITOR
        if (Selection.activeGameObject == gameObject)
        {
            Handles.color = valid ? Color.cyan : Color.red;
            Handles.Label(transform.position + Vector3.up * 1.5f, $"{name}\n→ {(string.IsNullOrEmpty(nextSceneName) ? "미연결" : nextSceneName)} / {targetSpawnPointId}");
        }
#endif
    }
#endif
}
