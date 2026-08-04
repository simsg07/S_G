using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [SerializeField] private string pendingSpawnId = "Spawn_Default";
    [SerializeField] private bool isLoading;
    [Tooltip("Player tag used after scene load.")]
    [SerializeField] private string playerTag = "Player";
    [Tooltip("Optional Player layer filter. Zero uses the Player tag.")]
    [SerializeField] private LayerMask playerLayerMask;
    [SerializeField] private bool debugMode = true;

    private string pendingSceneName;
    public string PendingSpawnId => pendingSpawnId;
    public bool IsLoading => isLoading;

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

    public static bool RequestSceneMove(string sceneName, string spawnId)
    {
        SceneTransitionManager manager = EnsureInstance();
        if (manager.isLoading)
        {
            Debug.LogWarning("[SceneTransitionManager] A scene is already loading. Duplicate request ignored.", manager);
            return false;
        }
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("[SceneTransitionManager] Scene name is missing.", manager);
            return false;
        }
        if (!SceneLoader.IsSceneRegisteredInBuildSettings(sceneName))
        {
            Debug.LogWarning($"[SceneTransitionManager] Scene not in Build Settings: {sceneName}", manager);
            return false;
        }
        manager.pendingSceneName = sceneName;
        manager.pendingSpawnId = string.IsNullOrWhiteSpace(spawnId) ? "Spawn_Default" : spawnId;
        manager.StartCoroutine(manager.LoadSceneRoutine());
        return true;
    }

    private static SceneTransitionManager EnsureInstance()
    {
        if (Instance != null) return Instance;
        SceneTransitionManager existing = FindAnyObjectByType<SceneTransitionManager>();
        if (existing != null) return existing;
        return new GameObject("SceneTransitionManager").AddComponent<SceneTransitionManager>();
    }

    public IEnumerator LoadSceneRoutine()
    {
        isLoading = true;
        if (debugMode)
        {
            Debug.Log($"[SceneTransitionManager] Loading scene: {pendingSceneName}", this);
            Debug.Log($"[SceneTransitionManager] Pending spawn: {pendingSpawnId}", this);
        }
        AsyncOperation operation = SceneManager.LoadSceneAsync(pendingSceneName);
        if (operation == null)
        {
            Debug.LogWarning($"[SceneTransitionManager] Could not start loading: {pendingSceneName}", this);
            isLoading = false;
            yield break;
        }
        while (!operation.isDone) yield return null;
        yield return null;
        MovePlayerToSpawnPoint();
        isLoading = false;
    }

    public void MovePlayerToSpawnPoint()
    {
        GameObject player = FindPlayer();
        if (player == null)
        {
            Debug.LogWarning("[SceneTransitionManager] Player not found. Check Player tag/layer.", this);
            return;
        }

        SceneSpawnPoint3D point = FindSpawnPointById(pendingSpawnId);
        if (point == null)
        {
            Debug.LogWarning("[SceneTransitionManager] Spawn not found. Using default spawn.", this);
            point = FindDefaultSpawnPoint();
        }

        Vector3 destination;
        if (point != null)
        {
            destination = point.GetSpawnPosition();
            if (debugMode) Debug.Log("[SceneTransitionManager] Spawn found.", point);
        }
        else
        {
            PlayerSpawnPoint legacy = FindLegacySpawnPoint(pendingSpawnId);
            if (legacy == null)
            {
                Debug.LogWarning("[SceneTransitionManager] No new or legacy default SpawnPoint found.", this);
                return;
            }
            destination = legacy.transform.position;
            if (debugMode) Debug.Log($"[SceneTransitionManager] Using legacy PlayerSpawnPoint: {legacy.SpawnPointId}", legacy);
        }

        ResetVelocity(player);
        player.transform.position = destination;
        ResetVelocity(player);
        if (debugMode) Debug.Log("[SceneTransitionManager] Player moved to spawn.", this);
    }

    public SceneSpawnPoint3D FindSpawnPointById(string spawnId)
    {
        SceneSpawnPoint3D[] points = FindObjectsByType<SceneSpawnPoint3D>(FindObjectsSortMode.None);
        foreach (SceneSpawnPoint3D point in points) if (point != null && point.Matches(spawnId)) return point;
        return null;
    }

    public SceneSpawnPoint3D FindDefaultSpawnPoint()
    {
        SceneSpawnPoint3D[] points = FindObjectsByType<SceneSpawnPoint3D>(FindObjectsSortMode.None);
        foreach (SceneSpawnPoint3D point in points) if (point != null && point.IsDefaultSpawn) return point;
        return null;
    }

    [ContextMenu("Validate Transition Manager Setup")]
    public void ValidateTransitionManagerSetup()
    {
        Debug.Log($"[SceneTransitionManager] Setup: playerTag='{playerTag}', layerMask={playerLayerMask.value}, loading={isLoading}", this);
        if (string.IsNullOrWhiteSpace(playerTag) && playerLayerMask.value == 0)
            Debug.LogWarning("[SceneTransitionManager] Set playerTag or playerLayerMask.", this);
    }

    private GameObject FindPlayer()
    {
        if (!string.IsNullOrWhiteSpace(playerTag))
        {
            GameObject tagged = GameObject.FindGameObjectWithTag(playerTag);
            if (tagged != null) return tagged;
        }
        if (playerLayerMask.value == 0) return null;
        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        foreach (Transform candidate in transforms)
            if ((playerLayerMask.value & (1 << candidate.gameObject.layer)) != 0) return candidate.gameObject;
        return null;
    }

    private static PlayerSpawnPoint FindLegacySpawnPoint(string requestedId)
    {
        PlayerSpawnPoint[] points = FindObjectsByType<PlayerSpawnPoint>(FindObjectsSortMode.None);
        foreach (PlayerSpawnPoint point in points) if (point != null && point.Matches(requestedId)) return point;
        foreach (PlayerSpawnPoint point in points) if (point != null && point.IsDefaultSpawn) return point;
        return null;
    }

    private static void ResetVelocity(GameObject player)
    {
        Rigidbody body = player.GetComponentInParent<Rigidbody>();
        if (body == null) body = player.GetComponentInChildren<Rigidbody>();
        if (body == null) return;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
    }
}
