using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class GravityObjectSpawner : MonoBehaviour
{
    [Header("Spawn")]
    [SerializeField] private GameObject objectPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private bool autoRespawn;
    [SerializeField] private float respawnDelay = 1f;

    [Header("State")]
    [SerializeField] private GameObject currentInstance;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;

    private Coroutine respawnRoutine;

    private void Start()
    {
        if (spawnOnStart)
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
        if (currentInstance != null)
        {
            Log("Spawn skipped. Current instance already exists.");
            return currentInstance;
        }

        if (objectPrefab == null)
        {
            Debug.LogWarning("[GravityObjectSpawner] Object prefab is not assigned.", this);
            return null;
        }

        Transform targetSpawnPoint = spawnPoint != null ? spawnPoint : transform;
        currentInstance = Instantiate(objectPrefab, targetSpawnPoint.position, targetSpawnPoint.rotation);
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

        if (autoRespawn && isActiveAndEnabled)
        {
            if (respawnRoutine != null)
            {
                StopCoroutine(respawnRoutine);
            }

            respawnRoutine = StartCoroutine(RespawnAfterDelay());
        }
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
        LogComponent("ObjectPrefab", objectPrefab);
        LogComponent("SpawnPoint", spawnPoint);
        LogComponent("CurrentInstance", currentInstance);
        Log($"spawnOnStart={spawnOnStart}, autoRespawn={autoRespawn}, respawnDelay={respawnDelay}");
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
    }

    private IEnumerator RespawnAfterDelay()
    {
        if (respawnDelay > 0f)
        {
            yield return new WaitForSeconds(respawnDelay);
        }

        respawnRoutine = null;
        SpawnObject();
    }

    private void LogComponent(string label, Object component)
    {
        if (!debugMode)
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
        if (debugMode)
        {
            Debug.Log($"[GravityObjectSpawner] {message}", this);
        }
    }
}
