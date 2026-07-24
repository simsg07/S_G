using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class GravityObjectSpawner : MonoBehaviour
{
    [Header("Spawn")]
    [FormerlySerializedAs("objectPrefab")]
    [SerializeField, Tooltip("생성할 프리팹입니다.")] private GameObject spawnPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private bool spawnOnStart = true;
    [FormerlySerializedAs("autoRespawn")]
    [SerializeField] private bool respawnAfterDespawn;
    [SerializeField] private float respawnDelay = 1f;
    [SerializeField] private bool enableSpawn = true;
    [SerializeField] private bool useSpawnPointRotation = true;
    [SerializeField, Min(1)] private int maxAliveCount = 1;
    [SerializeField] private bool allowRepeatedRespawn = true;
    [SerializeField] private Transform spawnedParent;

    [Header("State")]
    [SerializeField] private GameObject currentInstance;

    [Header("Debug")]
    [FormerlySerializedAs("debugMode")]
    [SerializeField] private bool logSpawnEvents = true;
    [SerializeField] private bool showSpawnGizmo = true;

    private Coroutine respawnRoutine;
    private SpawnedObjectLifecycle currentLifecycle;
    private bool sceneUnloading;
    private bool hasRespawned;
    private bool missingPrefabWarningLogged;

    private void OnEnable()
    {
        sceneUnloading = false;
        SceneManager.sceneUnloaded += HandleSceneUnloaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        CancelRespawn();
        if (currentLifecycle != null) currentLifecycle.MarkSceneUnloading();
    }

    private void Start()
    {
        if (enableSpawn && spawnOnStart)
        {
            SpawnObject();
        }
    }

    private void OnValidate()
    {
        respawnDelay = Mathf.Max(0f, respawnDelay);
    }

    [ContextMenu("Spawn Object")]
    public GameObject SpawnObject()
    {
        if (!enableSpawn || sceneUnloading || currentInstance != null)
        {
            Log("Spawn skipped. Current instance already exists.");
            return currentInstance;
        }

        if (spawnPrefab == null)
        {
            if (!missingPrefabWarningLogged)
            {
                Debug.LogWarning("[GravityObjectSpawner] Spawn prefab is not assigned.", this);
                missingPrefabWarningLogged = true;
            }
            return null;
        }

        Transform targetSpawnPoint = spawnPoint != null ? spawnPoint : transform;
        Quaternion rotation = useSpawnPointRotation ? targetSpawnPoint.rotation : spawnPrefab.transform.rotation;
        currentInstance = Instantiate(spawnPrefab, targetSpawnPoint.position, rotation, spawnedParent);
        RegisterSpawnedObject(currentInstance);
        Log($"Spawned: {currentInstance.name}");
        return currentInstance;
    }

    [ContextMenu("Despawn Current")]
    public void DespawnCurrent()
    {
        if (currentInstance == null)
        {
            return;
        }

        GameObject target = currentInstance;
        currentInstance = null;
        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }

        Log("Current instance despawned.");
    }

    [ContextMenu("Respawn Object")]
    public void RespawnObject()
    {
        DespawnCurrent();
        SpawnObject();
    }

    public void NotifySpawnedObjectFinished(GameObject finishedObject)
    {
        if (finishedObject == null || currentInstance != finishedObject)
        {
            return;
        }

        currentInstance = null;
        Log($"Spawned object finished: {finishedObject.name}");

        ScheduleRespawn();
    }

    private void HandleLifecycleDespawned(SpawnedObjectLifecycle lifecycle)
    {
        if (lifecycle == null || lifecycle != currentLifecycle)
        {
            return;
        }

        lifecycle.Despawned -= HandleLifecycleDespawned;
        currentLifecycle = null;
        currentInstance = null;
        ScheduleRespawn();
    }

    [ContextMenu("Reset Spawner")]
    public void ResetSpawner()
    {
        if (respawnRoutine != null)
        {
            StopCoroutine(respawnRoutine);
            respawnRoutine = null;
        }

        DespawnCurrent();

        if (spawnOnStart)
        {
            SpawnObject();
        }

        Log("Spawner reset.");
    }

    [ContextMenu("Validate Spawner Setup")]
    public void ValidateSpawnerSetup()
    {
        LogComponent("SpawnPrefab", spawnPrefab);
        LogComponent("SpawnPoint", spawnPoint);
        LogComponent("CurrentInstance", currentInstance);
        Log($"spawnOnStart={spawnOnStart}, respawnAfterDespawn={respawnAfterDespawn}, respawnDelay={respawnDelay}");
    }

    private void RegisterSpawnedObject(GameObject spawnedObject)
    {
        if (spawnedObject == null)
        {
            return;
        }

        StoneObject stoneObject = spawnedObject.GetComponent<StoneObject>();
        if (stoneObject != null)
        {
            stoneObject.SetOwnerSpawner(this);
        }

        FallingBoxObject fallingBoxObject = spawnedObject.GetComponent<FallingBoxObject>();
        if (fallingBoxObject != null)
        {
            fallingBoxObject.SetOwnerSpawner(this);
        }

        currentLifecycle = spawnedObject.GetComponent<SpawnedObjectLifecycle>();
        if (currentLifecycle == null) currentLifecycle = spawnedObject.AddComponent<SpawnedObjectLifecycle>();
        currentLifecycle.Despawned -= HandleLifecycleDespawned;
        currentLifecycle.Despawned += HandleLifecycleDespawned;
    }

    private IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(Mathf.Max(0.02f, respawnDelay));

        respawnRoutine = null;
        if (!sceneUnloading && isActiveAndEnabled && currentInstance == null)
        {
            hasRespawned = true;
            SpawnObject();
        }
    }

    private void ScheduleRespawn()
    {
        if (!respawnAfterDespawn || sceneUnloading || !isActiveAndEnabled || respawnRoutine != null || (!allowRepeatedRespawn && hasRespawned)) return;
        respawnRoutine = StartCoroutine(RespawnAfterDelay());
    }

    private void CancelRespawn()
    {
        if (respawnRoutine == null) return;
        StopCoroutine(respawnRoutine);
        respawnRoutine = null;
    }

    private void HandleSceneUnloaded(Scene scene)
    {
        if (scene != gameObject.scene) return;
        sceneUnloading = true;
        CancelRespawn();
        if (currentLifecycle != null) currentLifecycle.MarkSceneUnloading();
    }

    private void OnDrawGizmosSelected()
    {
        if (!showSpawnGizmo) return;
        Transform point = spawnPoint != null ? spawnPoint : transform;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(point.position, 0.2f);
        Gizmos.DrawLine(point.position, point.position + point.right * 0.8f);
    }

    private void LogComponent(string label, Object component)
    {
        if (!logSpawnEvents)
        {
            return;
        }

        if (component != null)
        {
            Debug.Log($"[GravityObjectSpawner] {label} assigned: {component.name}", this);
            return;
        }

        Debug.LogWarning($"[GravityObjectSpawner] {label} not assigned.", this);
    }

    private void Log(string message)
    {
        if (logSpawnEvents)
        {
            Debug.Log($"[GravityObjectSpawner] {message}", this);
        }
    }
}
