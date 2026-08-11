using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

public enum FocusingSpawnerState
{
    Alive,
    Defeated,
    Disabled
}

public enum FocusingSpawnWorldPolicy
{
    [InspectorName("Use Prefab Setting")]
    UsePrefabSetting = 0,
    [InspectorName("Current World Only (World A)")]
    CurrentWorldOnly = 1,
    [InspectorName("Past World Only (World B)")]
    PastWorldOnly = 2,
    [InspectorName("Both Worlds")]
    BothWorlds = 3
}

[DisallowMultipleComponent]
[AddComponentMenu("_Project/Puzzle/Focusing Spawner 3D")]
public sealed class FocusingSpawner3D : MonoBehaviour
{
    private static readonly HashSet<FocusingSpawner3D> Registered = new HashSet<FocusingSpawner3D>();

    [Header("Spawn")]
    [SerializeField, Tooltip("전체 초기화 생성 순서와 Gizmo 표시를 위한 스포너 분류입니다.")]
    private FocusingSpawnerType spawnerType = FocusingSpawnerType.Monster;
    [FormerlySerializedAs("prefab")]
    [SerializeField, Tooltip("이 스포너가 생성할 프리팹입니다.")]
    private GameObject spawnPrefab;
    [SerializeField, Tooltip("지정하면 이 Transform의 위치와 회전에서 생성합니다. 비어 있으면 스포너 Transform을 사용합니다.")]
    private Transform spawnPoint;
    [FormerlySerializedAs("defaultParent")]
    [SerializeField, Tooltip("생성 인스턴스의 선택적 부모입니다. 비어 있으면 씬 루트에 생성합니다.")]
    private Transform spawnParent;
    [SerializeField, Tooltip("씬 시작 시 자동으로 한 개를 생성합니다.")]
    private bool spawnOnSceneStart = true;
    [FormerlySerializedAs("resetByFocusingRing")]
    [SerializeField, Tooltip("포커싱 링 전체 초기화 대상에 포함합니다.")]
    private bool includeInFullReset = true;
    [SerializeField, Tooltip("이 스포너가 생성한 인스턴스에만 적용할 World 소속입니다. 기본값은 Prefab 설정을 유지합니다.")]
    private FocusingSpawnWorldPolicy spawnWorldPolicy = FocusingSpawnWorldPolicy.UsePrefabSetting;
    [SerializeField, Tooltip("숨겨진 World에서의 실행 정책입니다. Continue Monster Logic은 Monster 스포너에만 적용됩니다.")]
    private HiddenWorldSimulationPolicy hiddenWorldSimulationPolicy = HiddenWorldSimulationPolicy.PauseWhenHidden;

    [Header("Persistent Completion")]
    [SerializeField] private bool permanentlyDisableAfterPuzzleCompletion;
    [SerializeField] private string persistentCompletionKey;

    [Header("Temporary Objects")]
    [SerializeField] private Transform temporaryObjectsRoot;

    [Header("Scene Safety")]
    [SerializeField] private bool protectPlayerFromSpawnOverlap = true;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool showGizmo = true;
    [SerializeField] private Color gizmoColor = new Color(0.2f, 0.85f, 1f, 0.8f);

    [Header("Runtime (Read Only)")]
    [SerializeField] private FocusingSpawnerState state = FocusingSpawnerState.Defeated;
    [SerializeField] private GameObject currentInstance;
    [SerializeField] private bool spawnInProgress;

    [Header("Events")]
    [SerializeField] private UnityEvent spawned = new UnityEvent();
    [SerializeField] private UnityEvent defeated = new UnityEvent();
    [SerializeField] private UnityEvent despawned = new UnityEvent();

    private readonly List<GameObject> registeredTemporaryObjects = new List<GameObject>(8);
    private MonsterHealth subscribedHealth;
    private FocusingSpawnedInstance3D instanceRelay;
    private bool suppressInstanceNotifications;
    private bool missingPrefabWarningLogged;
    private bool fullResetPrepared;

    public static IEnumerable<FocusingSpawner3D> RegisteredSpawners => Registered;
    public GameObject CurrentInstance => currentInstance;
    public FocusingSpawnerState State => state;
    public FocusingSpawnerType SpawnerType => spawnerType;
    public bool IncludeInFullReset => includeInFullReset;
    public bool ResetByFocusingRing => includeInFullReset;
    public bool IsPermanentlyDisabled => state == FocusingSpawnerState.Disabled;
    public bool SpawnInProgress => spawnInProgress;
    public FocusingSpawnWorldPolicy SpawnWorldPolicy => spawnWorldPolicy;
    public HiddenWorldSimulationPolicy EffectiveHiddenWorldSimulationPolicy => GetEffectiveHiddenWorldSimulationPolicy();
    public string AppliedWorldSummary => GetAppliedWorldSummary();

    internal static void CopyRegisteredTo(List<FocusingSpawner3D> destination)
    {
        if (destination == null) return;
        Registered.RemoveWhere(spawner => spawner == null);
        foreach (FocusingSpawner3D spawner in Registered) destination.Add(spawner);
    }

    private void OnEnable() => Registered.Add(this);

    private void OnDisable()
    {
        Registered.Remove(this);
        fullResetPrepared = false;
        UnsubscribeInstanceEvents();
    }

    private void Start()
    {
        RefreshPersistentState();
        if (spawnOnSceneStart && state != FocusingSpawnerState.Disabled && currentInstance == null) SpawnFresh();
    }

    public void RefreshPersistentState()
    {
        if (permanentlyDisableAfterPuzzleCompletion && !string.IsNullOrWhiteSpace(persistentCompletionKey)
            && GameProgressSave3D.IsPuzzlePermanentlyCompleted(PersistentKey))
        {
            DespawnCurrent();
            state = FocusingSpawnerState.Disabled;
        }
    }

    public bool SpawnFresh()
    {
        if (state == FocusingSpawnerState.Disabled || spawnInProgress) return false;
        if (currentInstance != null) return false;
        if (spawnPrefab == null)
        {
            WarnMissingPrefabOnce();
            return false;
        }

        spawnInProgress = true;
        try
        {
            // A defeated instance can remain temporarily registered until the full reset.
            ClearTemporaryObjects();
            Transform point = spawnPoint != null ? spawnPoint : transform;
            GameObject stagingRoot = new GameObject($"{name} (Spawn Staging)");
            stagingRoot.hideFlags = HideFlags.HideAndDontSave;
            stagingRoot.SetActive(false);
            try
            {
                DirectPlacedMonsterSpawnOwner3D.BeginSpawnerInstantiation();
                try
                {
                    currentInstance = Instantiate(spawnPrefab, point.position, point.rotation, stagingRoot.transform);
                    currentInstance.SetActive(false);
                }
                finally
                {
                    DirectPlacedMonsterSpawnOwner3D.EndSpawnerInstantiation();
                }

                ApplySpawnWorldPolicy(currentInstance);
                currentInstance.transform.SetParent(spawnParent, true);
                currentInstance.transform.SetPositionAndRotation(point.position, point.rotation);
            }
            finally
            {
                Destroy(stagingRoot);
            }
            currentInstance.name = spawnPrefab.name;
            BindInstanceEvents(currentInstance);
            state = FocusingSpawnerState.Alive;
            currentInstance.SetActive(true);
            NotifyAfterSpawn(currentInstance);
            ApplyPlayerOverlapProtection(currentInstance);
            spawned.Invoke();
            return true;
        }
        finally
        {
            spawnInProgress = false;
        }
    }

    public void ConfigureRuntimeMonsterSpawner(GameObject prefab, Transform point, Transform parent,
        bool includeInReset)
    {
        spawnerType = FocusingSpawnerType.Monster;
        spawnPrefab = prefab;
        spawnPoint = point;
        spawnParent = parent;
        spawnOnSceneStart = false;
        includeInFullReset = includeInReset;
        missingPrefabWarningLogged = false;
    }

    public bool AdoptExistingInstance(GameObject instance)
    {
        if (instance == null || spawnInProgress || currentInstance != null) return false;
        RefreshPersistentState();
        if (state == FocusingSpawnerState.Disabled) return false;

        currentInstance = instance;
        BindInstanceEvents(instance);
        state = FocusingSpawnerState.Alive;
        return true;
    }

    /// <summary>
    /// Starts the explicit full-reset contract. A live owned instance is included and removed.
    /// Missing-prefab spawners are skipped without destroying their current instance.
    /// </summary>
    public bool PrepareForFullReset()
    {
        fullResetPrepared = false;
        RefreshPersistentState();
        if (!includeInFullReset || state == FocusingSpawnerState.Disabled) return false;
        if (spawnPrefab == null)
        {
            WarnMissingPrefabOnce();
            return false;
        }

        DespawnCurrent();
        fullResetPrepared = true;
        return true;
    }

    /// <summary>
    /// Completes a prepared full reset by creating exactly one fresh prefab at SpawnPoint.
    /// </summary>
    public bool CompleteFullReset()
    {
        if (!fullResetPrepared) return false;
        fullResetPrepared = false;
        RefreshPersistentState();
        return state != FocusingSpawnerState.Disabled && SpawnFresh();
    }

    public void DespawnCurrent()
    {
        suppressInstanceNotifications = true;
        try
        {
            UnsubscribeInstanceEvents();

            if (currentInstance != null)
            {
                NotifyBeforeDespawn(currentInstance);
                currentInstance.SetActive(false);
                Destroy(currentInstance);
                currentInstance = null;
                despawned.Invoke();
            }

            ClearTemporaryObjects();
        }
        finally
        {
            suppressInstanceNotifications = false;
            if (state != FocusingSpawnerState.Disabled) state = FocusingSpawnerState.Defeated;
        }
    }

    public void NotifyInstanceDied(GameObject instance)
    {
        if (suppressInstanceNotifications || instance == null || instance != currentInstance) return;
        UnsubscribeInstanceEvents();
        RegisterTemporaryObject(instance);
        currentInstance = null;
        state = FocusingSpawnerState.Defeated;
        defeated.Invoke();
    }

    public void RegisterTemporaryObject(GameObject temporaryObject)
    {
        if (temporaryObject != null && !registeredTemporaryObjects.Contains(temporaryObject))
            registeredTemporaryObjects.Add(temporaryObject);
    }

    public void MarkPuzzlePermanentlyCompleted()
    {
        if (!permanentlyDisableAfterPuzzleCompletion) return;
        if (!string.IsNullOrWhiteSpace(persistentCompletionKey))
            GameProgressSave3D.RecordPuzzlePermanentlyCompleted(PersistentKey);
        DespawnCurrent();
        state = FocusingSpawnerState.Disabled;
    }

    private void BindInstanceEvents(GameObject instance)
    {
        UnsubscribeInstanceEvents();
        subscribedHealth = instance.GetComponentInChildren<MonsterHealth>(true);
        if (subscribedHealth != null) subscribedHealth.Died += HandleMonsterDied;

        instanceRelay = instance.GetComponent<FocusingSpawnedInstance3D>();
        if (instanceRelay == null) instanceRelay = instance.AddComponent<FocusingSpawnedInstance3D>();
        instanceRelay.Bind(this, instance);
    }

    private void UnsubscribeInstanceEvents()
    {
        if (subscribedHealth != null) subscribedHealth.Died -= HandleMonsterDied;
        subscribedHealth = null;
        if (instanceRelay != null) instanceRelay.Unbind(this);
        instanceRelay = null;
    }

    private void HandleMonsterDied(MonsterHealth health)
    {
        if (health == subscribedHealth) NotifyInstanceDied(currentInstance);
    }

    private void ApplyPlayerOverlapProtection(GameObject instance)
    {
        if (!protectPlayerFromSpawnOverlap || instance == null || string.IsNullOrWhiteSpace(playerTag)) return;
        GameObject player;
        try { player = GameObject.FindGameObjectWithTag(playerTag); }
        catch (UnityException) { return; }
        if (player == null) return;
        FocusingSpawnOverlapGuard3D guard = instance.GetComponent<FocusingSpawnOverlapGuard3D>();
        if (guard == null) guard = instance.AddComponent<FocusingSpawnOverlapGuard3D>();
        guard.ProtectUntilSeparated(player.transform);
    }

    private string PersistentKey => $"{SceneManager.GetActiveScene().name}.{persistentCompletionKey}";

    private void ClearTemporaryObjects()
    {
        for (int i = registeredTemporaryObjects.Count - 1; i >= 0; i--)
        {
            GameObject temporaryObject = registeredTemporaryObjects[i];
            if (temporaryObject == null) continue;
            NotifyBeforeDespawn(temporaryObject);
            temporaryObject.SetActive(false);
            Destroy(temporaryObject);
        }
        registeredTemporaryObjects.Clear();

        if (temporaryObjectsRoot == null) return;
        for (int i = temporaryObjectsRoot.childCount - 1; i >= 0; i--)
        {
            GameObject temporaryObject = temporaryObjectsRoot.GetChild(i).gameObject;
            NotifyBeforeDespawn(temporaryObject);
            temporaryObject.SetActive(false);
            Destroy(temporaryObject);
        }
    }

    private static void NotifyBeforeDespawn(GameObject instance)
    {
        if (instance == null) return;
        MonoBehaviour[] behaviours = instance.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
            if (behaviours[i] is IFocusingResettable3D resettable) resettable.BeforeFocusingDespawn();
    }

    private static void NotifyAfterSpawn(GameObject instance)
    {
        if (instance == null) return;
        MonoBehaviour[] behaviours = instance.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
            if (behaviours[i] is IFocusingResettable3D resettable) resettable.AfterFocusingSpawn();
    }

    private void WarnMissingPrefabOnce()
    {
        if (missingPrefabWarningLogged) return;
        missingPrefabWarningLogged = true;
        Debug.LogWarning($"[FocusingSpawner] Spawn Prefab is not assigned on '{name}'. This spawner was skipped.", this);
    }

    private void ApplySpawnWorldPolicy(GameObject instance)
    {
        if (instance == null) return;

        WorldPresence[] presences = instance.GetComponentsInChildren<WorldPresence>(true);
        if (spawnWorldPolicy != FocusingSpawnWorldPolicy.UsePrefabSetting)
        {
            WorldPresenceMode mode = ToPresenceMode(spawnWorldPolicy);
            if (presences.Length == 0)
            {
                WorldPresence adapter = instance.AddComponent<WorldPresence>();
                adapter.ConfigureRuntimeAdapter(mode);
                presences = new[] { adapter };
            }
            else
            {
                for (int i = 0; i < presences.Length; i++) presences[i].SetPresenceMode(mode);
            }
        }

        HiddenWorldSimulationPolicy simulationPolicy = GetEffectiveHiddenWorldSimulationPolicy();
        for (int i = 0; i < presences.Length; i++)
            presences[i].ConfigureHiddenWorldSimulation(simulationPolicy, instance);

        if (WorldManager.Instance != null)
        {
            for (int i = 0; i < presences.Length; i++) presences[i].ApplyWorldState(WorldManager.Instance.CurrentWorld);
        }
        else
        {
            ResearchWorldId activeWorld = WorldSystem3D.ActiveWorld;
            for (int i = 0; i < presences.Length; i++) presences[i].ApplyWorldState(activeWorld);
        }
    }

    private static WorldPresenceMode ToPresenceMode(FocusingSpawnWorldPolicy policy)
    {
        switch (policy)
        {
            case FocusingSpawnWorldPolicy.CurrentWorldOnly: return WorldPresenceMode.WorldAOnly;
            case FocusingSpawnWorldPolicy.PastWorldOnly: return WorldPresenceMode.WorldBOnly;
            default: return WorldPresenceMode.Both;
        }
    }

    private string GetAppliedWorldSummary()
    {
        switch (spawnWorldPolicy)
        {
            case FocusingSpawnWorldPolicy.CurrentWorldOnly: return "Current World only (World A)";
            case FocusingSpawnWorldPolicy.PastWorldOnly: return "Past World only (World B)";
            case FocusingSpawnWorldPolicy.BothWorlds: return "Both Worlds";
            default: return "Uses each spawned Prefab's WorldPresence setting";
        }
    }

    private HiddenWorldSimulationPolicy GetEffectiveHiddenWorldSimulationPolicy()
    {
        return spawnerType == FocusingSpawnerType.Monster
            ? hiddenWorldSimulationPolicy
            : HiddenWorldSimulationPolicy.PauseWhenHidden;
    }

    private void OnValidate()
    {
        if (spawnerType != FocusingSpawnerType.Monster)
            hiddenWorldSimulationPolicy = HiddenWorldSimulationPolicy.PauseWhenHidden;
        if (spawnPrefab != null) missingPrefabWarningLogged = false;
        if (permanentlyDisableAfterPuzzleCompletion && string.IsNullOrWhiteSpace(persistentCompletionKey))
            Debug.LogWarning("[FocusingSpawner] Permanent completion requires a unique key.", this);
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmo) return;
        Transform point = spawnPoint != null ? spawnPoint : transform;
        Gizmos.color = GetSpawnWorldGizmoColor();
        Gizmos.DrawWireSphere(point.position, 0.35f);
        Gizmos.DrawRay(point.position, point.right * 0.8f);
        if (point != transform) Gizmos.DrawLine(transform.position, point.position);
    }

    private Color GetSpawnerTypeColor()
    {
        switch (spawnerType)
        {
            case FocusingSpawnerType.PuzzleObject: return new Color(0.25f, 0.85f, 1f, 0.9f);
            case FocusingSpawnerType.GravityObject: return new Color(0.35f, 1f, 0.45f, 0.9f);
            case FocusingSpawnerType.DestructibleObject: return new Color(1f, 0.65f, 0.15f, 0.9f);
            case FocusingSpawnerType.Monster: return new Color(1f, 0.25f, 0.3f, 0.9f);
            default: return gizmoColor;
        }
    }

    private Color GetSpawnWorldGizmoColor()
    {
        switch (spawnWorldPolicy)
        {
            case FocusingSpawnWorldPolicy.CurrentWorldOnly: return new Color(0.1f, 0.95f, 0.9f, 0.9f);
            case FocusingSpawnWorldPolicy.PastWorldOnly: return new Color(0.85f, 0.3f, 1f, 0.9f);
            case FocusingSpawnWorldPolicy.BothWorlds: return Color.white;
            default: return GetSpawnerTypeColor();
        }
    }
}
