using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public enum TimelineWorldState
{
    WorldA_Current,
    WorldB_Past
}

[DisallowMultipleComponent]
public class WorldManager : MonoBehaviour
{
    [SerializeField] private TimelineWorldState currentWorld = TimelineWorldState.WorldA_Current; // Active timeline state applied to the scene.
    [SerializeField] private GameObject sharedRoot; // Common root that must stay active: Player, Camera, UI, GameManager.
    [SerializeField] private GameObject worldACurrentRoot; // Objects visible and collidable only in the current-time world.
    [SerializeField] private GameObject worldBPastRoot; // Objects visible and collidable only in the past-time world.
    [SerializeField] private bool applyOnAwake = true; // Applies currentWorld as soon as the scene starts.
    [SerializeField] private bool previewInEditMode; // Lets designers preview A/B root activation in the editor.
    [SerializeField] private bool syncLegacyWorldSystem = true; // Keeps older WorldSystem3D effects and world data in sync.
    [SerializeField] private TimelineWorldEvent beforeWorldChanged = new TimelineWorldEvent(); // Hook for transition effects or sound start.
    [SerializeField] private TimelineWorldEvent afterWorldChanged = new TimelineWorldEvent(); // Hook for transition effects or sound finish.

    public static event Action<TimelineWorldState, TimelineWorldState> WorldChanged;

    public static WorldManager Instance { get; private set; }
    public TimelineWorldState CurrentWorld => currentWorld;
    public bool IsCurrentWorld => currentWorld == TimelineWorldState.WorldA_Current;
    public bool IsPastWorld => currentWorld == TimelineWorldState.WorldB_Past;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveRoots();

        if (applyOnAwake)
        {
            SetWorld(currentWorld, true);
        }
    }

    private void OnValidate()
    {
        ResolveRoots();
        if (previewInEditMode && !Application.isPlaying)
        {
            ApplyRootActiveState();
        }
    }

    public static bool TrySwitchWorld()
    {
        WorldManager manager = Instance != null ? Instance : FindFirstObjectByType<WorldManager>();
        if (manager == null)
        {
            return false;
        }

        manager.SwitchWorld();
        return true;
    }

    public void SwitchWorld()
    {
        TimelineWorldState nextWorld = currentWorld == TimelineWorldState.WorldA_Current
            ? TimelineWorldState.WorldB_Past
            : TimelineWorldState.WorldA_Current;
        SetWorld(nextWorld);
    }

    public void SetWorld(TimelineWorldState nextWorld)
    {
        SetWorld(nextWorld, false);
    }

    public void SetWorld(TimelineWorldState nextWorld, bool force)
    {
        ResolveRoots();

        if (!force && currentWorld == nextWorld)
        {
            ApplyRootActiveState();
            return;
        }

        TimelineWorldState previousWorld = currentWorld;
        beforeWorldChanged.Invoke(nextWorld);
        currentWorld = nextWorld;

        SyncLegacyWorldSystem();
        ApplyRootActiveState();

        afterWorldChanged.Invoke(currentWorld);
        WorldChanged?.Invoke(previousWorld, currentWorld);
    }

    private void ResolveRoots()
    {
        if (sharedRoot == null)
        {
            sharedRoot = FindSceneRootByName("Shared");
        }

        if (worldACurrentRoot == null)
        {
            worldACurrentRoot = FindSceneRootByName("World_A_Current");
        }

        if (worldBPastRoot == null)
        {
            worldBPastRoot = FindSceneRootByName("World_B_Past");
        }
    }

    private static GameObject FindSceneRootByName(string rootName)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            return null;
        }

        GameObject[] rootObjects = activeScene.GetRootGameObjects();
        for (int i = 0; i < rootObjects.Length; i++)
        {
            if (rootObjects[i].name == rootName)
            {
                return rootObjects[i];
            }
        }

        return null;
    }

    private void ApplyRootActiveState()
    {
        if (sharedRoot != null && !sharedRoot.activeSelf)
        {
            sharedRoot.SetActive(true);
        }

        if (worldACurrentRoot != null)
        {
            worldACurrentRoot.SetActive(currentWorld == TimelineWorldState.WorldA_Current);
        }

        if (worldBPastRoot != null)
        {
            worldBPastRoot.SetActive(currentWorld == TimelineWorldState.WorldB_Past);
        }
    }

    private void SyncLegacyWorldSystem()
    {
        if (!syncLegacyWorldSystem)
        {
            return;
        }

        WorldSystem3D legacySystem = WorldSystem3D.Instance != null
            ? WorldSystem3D.Instance
            : FindFirstObjectByType<WorldSystem3D>();

        if (legacySystem == null)
        {
            return;
        }

        ResearchWorldId legacyWorld = currentWorld == TimelineWorldState.WorldA_Current
            ? ResearchWorldId.WorldA
            : ResearchWorldId.WorldB;
        legacySystem.SetWorld(legacyWorld);
    }
}

[Serializable]
public class TimelineWorldEvent : UnityEvent<TimelineWorldState>
{
}
