using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    [Header("Scene Loading")]
    [Tooltip("Tag used to find the existing Player after a scene loads. The loader does not create a new Player.")]
    [SerializeField] private string persistentPlayerTag = "Player";
    [Tooltip("Print scene transition and spawn logs in the Console.")]
    [SerializeField] private bool debugMode = true;
    [Tooltip("Runtime value. Read-only: last spawnPointId requested by StageExitTrigger.")]
    [SerializeField] private string lastTargetSpawnPointId = "Default";

    private string pendingSpawnPointId = "Default";
    private bool isLoading;
    public bool IsLoadingScene => isLoading;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public static void LoadStage(string sceneName, string spawnPointId)
    {
        TryLoadStage(sceneName, spawnPointId);
    }

    public static bool TryLoadStage(string sceneName, string spawnPointId)
    {
        return EnsureInstance().LoadStageInternal(sceneName, spawnPointId);
    }

    private static SceneLoader EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        GameObject loaderObject = new GameObject("SceneLoader");
        return loaderObject.AddComponent<SceneLoader>();
    }

    private bool LoadStageInternal(string sceneName, string spawnPointId)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("[SceneLoader] Scene name is empty. Stage load skipped.", this);
            return false;
        }

        if (isLoading)
        {
            Debug.LogWarning("[SceneLoader] Scene is already loading. Ignored duplicate request.", this);
            return false;
        }

        if (!IsSceneRegisteredInBuildSettings(sceneName))
        {
            Debug.LogWarning($"[SceneLoader] Scene is not registered in Build Settings: {sceneName}", this);
            return false;
        }

        if (string.IsNullOrWhiteSpace(spawnPointId))
        {
            Debug.LogWarning("[SceneLoader] targetSpawnPointId is empty. Using Default.", this);
        }

        pendingSpawnPointId = string.IsNullOrWhiteSpace(spawnPointId) ? "Default" : spawnPointId;
        lastTargetSpawnPointId = pendingSpawnPointId;
        StartCoroutine(LoadStageRoutine(sceneName));
        return true;
    }

    private IEnumerator LoadStageRoutine(string sceneName)
    {
        isLoading = true;

        if (debugMode)
        {
            Debug.Log($"[SceneLoader] Loading scene '{sceneName}' -> spawn '{pendingSpawnPointId}'.", this);
        }

        AsyncOperation operation;
        try
        {
            operation = SceneManager.LoadSceneAsync(sceneName);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"[SceneLoader] Failed to load scene '{sceneName}'. Check Build Settings and scene name. {exception.Message}", this);
            isLoading = false;
            yield break;
        }

        if (operation == null)
        {
            Debug.LogWarning($"[SceneLoader] Failed to start loading scene '{sceneName}'.", this);
            isLoading = false;
            yield break;
        }

        while (!operation.isDone)
        {
            yield return null;
        }

        StageExitTrigger.BeginSpawnSafety(0.35f);
        yield return null;
        MovePlayerToSpawnPoint(pendingSpawnPointId);
        isLoading = false;
    }

    public void MovePlayerToSpawnPoint(string spawnPointId)
    {
        GameObject player = FindPlayer();
        if (player == null)
        {
            Debug.LogWarning($"[SceneLoader] Player with tag '{persistentPlayerTag}' was not found.", this);
            return;
        }

        PlayerSpawnPoint spawnPoint = FindSpawnPoint(spawnPointId);
        if (spawnPoint == null)
        {
            Debug.LogError($"[SceneLoader] SpawnPoint not found: '{spawnPointId}'. Player position was not changed.", this);
            ResetPlayerVelocity(player);
            return;
        }

        TeleportPlayer(player, spawnPoint.transform.position);
        SnapCamerasToPlayer(player.transform);

        if (debugMode)
        {
            Debug.Log($"[SceneLoader] Player moved to spawn '{spawnPoint.SpawnPointId}'.", this);
        }
    }

    public GameObject FindPlayer()
    {
        if (string.IsNullOrWhiteSpace(persistentPlayerTag))
        {
            Debug.LogWarning("[SceneLoader] persistentPlayerTag is empty.", this);
            return null;
        }

        return GameObject.FindGameObjectWithTag(persistentPlayerTag);
    }

    public PlayerSpawnPoint FindSpawnPoint(string spawnPointId)
    {
        string targetId = string.IsNullOrWhiteSpace(spawnPointId) ? "Default" : spawnPointId;
        PlayerSpawnPoint[] spawnPoints = Object.FindObjectsByType<PlayerSpawnPoint>(FindObjectsSortMode.None);
        PlayerSpawnPoint firstMatch = null;
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i].Matches(targetId))
            {
                if (firstMatch == null)
                {
                    firstMatch = spawnPoints[i];
                }
                else
                {
                    Debug.LogWarning($"[PlayerSpawnPoint] Duplicate spawnPointId found: {targetId}", spawnPoints[i]);
                }
            }
        }

        return firstMatch;
    }

    public static bool IsSceneRegisteredInBuildSettings(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return false;
        }

        int sceneCount = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < sceneCount; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            string registeredName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            if (string.Equals(registeredName, sceneName, System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void ResetPlayerVelocity(GameObject player)
    {
        if (player.TryGetComponent(out Rigidbody body))
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

    }

    private static void TeleportPlayer(GameObject player, Vector3 position)
    {
        if (player.TryGetComponent(out PlatformerPlayer3D movement))
        {
            movement.ResetJumpStateAfterTeleport();
        }

        CharacterController controller = player.GetComponent<CharacterController>();
        bool controllerWasEnabled = controller != null && controller.enabled;
        if (controllerWasEnabled)
        {
            controller.enabled = false;
        }

        if (player.TryGetComponent(out Rigidbody body))
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = position;
        }
        else
        {
            player.transform.position = position;
        }

        Physics.SyncTransforms();
        if (controllerWasEnabled)
        {
            controller.enabled = true;
        }
    }

    private static void SnapCamerasToPlayer(Transform player)
    {
        CameraFollow3D[] cameras = Object.FindObjectsByType<CameraFollow3D>(FindObjectsSortMode.None);
        foreach (CameraFollow3D cameraFollow in cameras)
        {
            cameraFollow.SnapToTarget(player);
        }
    }
}
