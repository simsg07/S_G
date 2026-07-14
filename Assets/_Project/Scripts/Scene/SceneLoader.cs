using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    [SerializeField] private bool debugMode;

    private string pendingSpawnPointId = "Default";
    private bool isLoading;

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
        EnsureInstance().LoadStageInternal(sceneName, spawnPointId);
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

    private void LoadStageInternal(string sceneName, string spawnPointId)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("[SceneLoader] Scene name is empty. Stage load skipped.", this);
            return;
        }

        if (isLoading)
        {
            if (debugMode)
            {
                Debug.Log("[SceneLoader] Scene load already in progress.", this);
            }
            return;
        }

        pendingSpawnPointId = string.IsNullOrWhiteSpace(spawnPointId) ? "Default" : spawnPointId;
        StartCoroutine(LoadStageRoutine(sceneName));
    }

    private IEnumerator LoadStageRoutine(string sceneName)
    {
        isLoading = true;

        if (debugMode)
        {
            Debug.Log($"[SceneLoader] Loading scene '{sceneName}' -> spawn '{pendingSpawnPointId}'.", this);
        }

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
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

        yield return null;
        MovePlayerToSpawnPoint();
        isLoading = false;
    }

    private void MovePlayerToSpawnPoint()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("[SceneLoader] Player with tag 'Player' was not found.", this);
            return;
        }

        PlayerSpawnPoint spawnPoint = FindSpawnPoint(pendingSpawnPointId);
        if (spawnPoint == null)
        {
            Debug.LogWarning($"[SceneLoader] Spawn point '{pendingSpawnPointId}' was not found. Player position unchanged.", this);
            ResetPlayerVelocity(player);
            return;
        }

        player.transform.position = spawnPoint.transform.position;
        ResetPlayerVelocity(player);

        if (debugMode)
        {
            Debug.Log($"[SceneLoader] Player moved to spawn '{spawnPoint.SpawnPointId}'.", this);
        }
    }

    private static PlayerSpawnPoint FindSpawnPoint(string spawnPointId)
    {
        PlayerSpawnPoint[] spawnPoints = Object.FindObjectsByType<PlayerSpawnPoint>(FindObjectsSortMode.None);
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i].Matches(spawnPointId))
            {
                return spawnPoints[i];
            }
        }

        return null;
    }

    private static void ResetPlayerVelocity(GameObject player)
    {
        if (player.TryGetComponent(out Rigidbody body))
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        if (player.TryGetComponent(out Rigidbody2D body2D))
        {
            body2D.linearVelocity = Vector2.zero;
            body2D.angularVelocity = 0f;
        }
    }
}
