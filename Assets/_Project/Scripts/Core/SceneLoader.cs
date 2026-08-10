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
    private string pendingCheckpointId = string.Empty;
    private bool isCheckpointRespawn;
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

    /// <summary>
    /// Loads a non-gameplay utility scene without writing progress or moving the Player to a spawn point.
    /// </summary>
    public static bool TryLoadUtilityScene(string sceneName)
    {
        return EnsureInstance().LoadUtilitySceneInternal(sceneName);
    }

    public static bool TryLoadCheckpointRespawn(string sceneName, string checkpointId)
    {
        SceneLoader loader = EnsureInstance();
        if (loader.isLoading)
        {
            Debug.LogWarning("[SceneLoader] Scene is already loading. Checkpoint respawn ignored.", loader);
            return false;
        }

        if (!IsSceneRegisteredInBuildSettings(sceneName) || string.IsNullOrWhiteSpace(checkpointId))
        {
            Debug.LogWarning($"[SceneLoader] Invalid checkpoint destination: scene='{sceneName}', id='{checkpointId}'.", loader);
            return false;
        }

        loader.pendingCheckpointId = checkpointId;
        loader.isCheckpointRespawn = true;
        bool accepted = loader.LoadStageInternal(sceneName, "Default");
        if (!accepted)
        {
            loader.pendingCheckpointId = string.Empty;
            loader.isCheckpointRespawn = false;
        }
        return accepted;
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

        if (!isCheckpointRespawn)
        {
            pendingCheckpointId = string.Empty;
        }
        GameProgressSave3D.SaveNow();
        pendingSpawnPointId = string.IsNullOrWhiteSpace(spawnPointId) ? "Default" : spawnPointId;
        lastTargetSpawnPointId = pendingSpawnPointId;
        StartCoroutine(LoadSceneRoutine(sceneName, true));
        return true;
    }

    private bool LoadUtilitySceneInternal(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("[SceneLoader] Utility scene name is empty. Load skipped.", this);
            return false;
        }

        if (isLoading)
        {
            Debug.LogWarning("[SceneLoader] Scene is already loading. Ignored duplicate utility scene request.", this);
            return false;
        }

        if (!IsSceneRegisteredInBuildSettings(sceneName))
        {
            Debug.LogWarning($"[SceneLoader] Utility scene is not registered in Build Settings: {sceneName}", this);
            return false;
        }

        Time.timeScale = 1f;
        pendingSpawnPointId = "Default";
        pendingCheckpointId = string.Empty;
        isCheckpointRespawn = false;
        StartCoroutine(LoadSceneRoutine(sceneName, false));
        return true;
    }

    private IEnumerator LoadSceneRoutine(string sceneName, bool movePlayerToDestination)
    {
        isLoading = true;

        if (debugMode)
        {
            string destination = movePlayerToDestination
                ? $"spawn '{pendingSpawnPointId}'"
                : "utility scene (no save/spawn move)";
            Debug.Log($"[SceneLoader] Loading scene '{sceneName}' -> {destination}.", this);
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

        if (!movePlayerToDestination)
        {
            SummerCampStageBootstrap3D.PrepareForUtilitySceneLoad();
        }

        while (!operation.isDone)
        {
            yield return null;
        }

        if (movePlayerToDestination)
        {
            StageExitTrigger.BeginSpawnSafety(0.35f);
            yield return null;
            if (isCheckpointRespawn)
            {
                MovePlayerToCheckpointOrDefault(pendingCheckpointId);
            }
            else
            {
                MovePlayerToSpawnPoint(pendingSpawnPointId);
            }
        }
        isCheckpointRespawn = false;
        pendingCheckpointId = string.Empty;
        isLoading = false;
    }

    public void MovePlayerToCheckpointOrDefault(string checkpointId)
    {
        GameObject player = FindPlayer();
        if (player == null)
        {
            Debug.LogWarning("[SceneLoader] Player was not found for checkpoint respawn.", this);
            return;
        }

        if (GameProgressSave3D.TryGetLastCheckpointPose(
                out string savedScene,
                out string savedCheckpointId,
                out Vector3 savedPosition,
                out Quaternion savedRotation,
                out ResearchWorldId savedWorld)
            && string.Equals(savedScene, SceneManager.GetActiveScene().name, System.StringComparison.Ordinal)
            && string.Equals(savedCheckpointId, checkpointId, System.StringComparison.Ordinal))
        {
            WorldSystem3D.EnsureInstance().SetWorld(savedWorld);
            TeleportPlayer(player, savedPosition, savedRotation);
            SnapCamerasToPlayer(player.transform);
            if (debugMode)
            {
                Debug.Log($"[SceneLoader] Player moved to saved checkpoint pose '{checkpointId}'.", this);
            }
            return;
        }

        Checkpoint3D checkpoint = FindCheckpoint(checkpointId);
        if (checkpoint == null)
        {
            Debug.LogWarning($"[SceneLoader] Checkpoint '{checkpointId}' was not found. Falling back to Default PlayerSpawnPoint.", this);
            MovePlayerToSpawnPoint("Default");
            return;
        }

        TeleportPlayer(player, checkpoint.SpawnPosition.position, checkpoint.SpawnPosition.rotation);
        SnapCamerasToPlayer(player.transform);
        if (debugMode)
        {
            Debug.Log($"[SceneLoader] Player moved to checkpoint '{checkpointId}'.", checkpoint);
        }
    }

    public static Checkpoint3D FindCheckpoint(string checkpointId)
    {
        if (string.IsNullOrWhiteSpace(checkpointId))
        {
            return null;
        }

        Checkpoint3D[] checkpoints = Object.FindObjectsByType<Checkpoint3D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Checkpoint3D checkpoint in checkpoints)
        {
            if (checkpoint != null && string.Equals(checkpoint.CheckpointId, checkpointId, System.StringComparison.Ordinal))
            {
                return checkpoint;
            }
        }

        return null;
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
        TeleportPlayer(player, position, player.transform.rotation);
    }

    private static void TeleportPlayer(GameObject player, Vector3 position, Quaternion rotation)
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
            body.rotation = rotation;
        }
        else
        {
            player.transform.SetPositionAndRotation(position, rotation);
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
