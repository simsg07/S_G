using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Gives a directly placed movable monster an independent, fixed FocusingSpawner owner.
/// Instances created by an existing FocusingSpawner skip this bootstrap because they already
/// receive a FocusingSpawnedInstance3D relay before their first Start callback.
/// </summary>
[DisallowMultipleComponent]
public sealed class DirectPlacedMonsterSpawnOwner3D : MonoBehaviour
{
    private const string CatalogResourcePath = "DirectPlacedMonsterSpawnCatalog3D";
    private static DirectPlacedMonsterSpawnCatalog3D catalog;
    private static int spawnerInstantiationDepth;

    [SerializeField]
    private DirectPlacedMonsterKind3D monsterKind;
    [SerializeField, Tooltip("Focusing Ring 전체 초기화 대상에 포함합니다.")]
    private bool includeInFullReset = true;

    private FocusingSpawner3D ownerSpawner;
    private bool setupAttempted;

    public FocusingSpawner3D OwnerSpawner => ownerSpawner;

    internal static bool SpawnerInstantiationInProgress => spawnerInstantiationDepth > 0;

    internal static void BeginSpawnerInstantiation()
    {
        spawnerInstantiationDepth++;
    }

    internal static void EndSpawnerInstantiation()
    {
        if (spawnerInstantiationDepth > 0) spawnerInstantiationDepth--;
    }

    private void Start()
    {
        EnsureSpawnerOwnership();
    }

    public bool EnsureSpawnerOwnership()
    {
        if (setupAttempted) return ownerSpawner != null;
        setupAttempted = true;

        // Spawned replacements are already owned. Creating another owner here would duplicate them.
        if (SpawnerInstantiationInProgress || GetComponent<FocusingSpawnedInstance3D>() != null)
        {
            enabled = false;
            return true;
        }

        if (catalog == null) catalog = Resources.Load<DirectPlacedMonsterSpawnCatalog3D>(CatalogResourcePath);
        GameObject spawnPrefab = catalog != null ? catalog.GetPrefab(monsterKind) : null;
        if (spawnPrefab == null)
        {
            Debug.LogError($"[DirectMonsterSpawnOwner] No spawn prefab is configured for {monsterKind}.", this);
            return false;
        }

        Transform stableParent = transform.parent;
        GameObject ownerObject = new GameObject($"{name} Spawn Owner");
        Scene sourceScene = gameObject.scene;
        if (sourceScene.IsValid()) SceneManager.MoveGameObjectToScene(ownerObject, sourceScene);
        ownerObject.transform.SetPositionAndRotation(transform.position, transform.rotation);
        if (stableParent != null) ownerObject.transform.SetParent(stableParent, true);

        GameObject pointObject = new GameObject("SpawnPoint");
        pointObject.transform.SetParent(ownerObject.transform, false);

        FocusingSpawner3D spawner = ownerObject.AddComponent<FocusingSpawner3D>();
        spawner.ConfigureRuntimeMonsterSpawner(spawnPrefab, pointObject.transform, stableParent,
            includeInFullReset);
        if (!spawner.AdoptExistingInstance(gameObject))
        {
            ownerObject.SetActive(false);
            Destroy(ownerObject);
            return false;
        }

        ownerSpawner = spawner;
        enabled = false;
        return true;
    }
}
