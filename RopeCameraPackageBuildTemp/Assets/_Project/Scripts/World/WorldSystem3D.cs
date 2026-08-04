using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum ResearchWorldId
{
    WorldA,
    WorldB
}

public class WorldSystem3D : MonoBehaviour
{
    [SerializeField] private ResearchWorldId currentWorld = ResearchWorldId.WorldA;
    [SerializeField] private bool persistAcrossScenes;
    [SerializeField] private Transform sharedMapRoot; // 월드 A/B가 함께 쓰는 공통 맵 루트입니다.
    [SerializeField] private Transform worldARoot; // 구버전 호환용 월드 A 루트입니다. 새 맵은 sharedMapRoot와 WorldStateObject3D를 사용합니다.
    [SerializeField] private Transform worldBRoot; // 구버전 호환용 월드 B 루트입니다. 새 맵은 sharedMapRoot와 WorldStateObject3D를 사용합니다.
    [SerializeField] private bool alignWorldRootsToPlayOrigin; // 구버전 분리 월드 루트를 플레이 중 같은 위치로 맞출지 정합니다. 새 구조에서는 끕니다.
    [SerializeField] private bool useWorldARootAsPlayOrigin = true; // true면 월드 A 루트 위치를 구버전 플레이 기준점으로 사용합니다.
    [SerializeField] private Vector3 customPlayOrigin = Vector3.zero; // useWorldARootAsPlayOrigin이 false일 때 사용할 구버전 플레이 기준 위치입니다.

    public static event Action<ResearchWorldId, ResearchWorldId> ActiveWorldChanged;

    public static WorldSystem3D Instance { get; private set; }
    public static ResearchWorldId ActiveWorld => Instance != null ? Instance.currentWorld : ResearchWorldId.WorldA;

    public ResearchWorldId CurrentWorld => currentWorld;

    private Vector3 worldAEditPosition;
    private Vector3 worldBEditPosition;
    private bool cachedWorldRootPositions;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveWorldRoots();
        CacheWorldRootPositions();

        if (persistAcrossScenes && Application.isPlaying)
        {
            DontDestroyOnLoad(gameObject);
        }

        AlignWorldRootsForPlay();
        RefreshWorldObjects();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public static WorldSystem3D EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        WorldSystem3D existing = FindFirstObjectByType<WorldSystem3D>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject worldSystemObject = new GameObject("WorldSystem3D");
        return worldSystemObject.AddComponent<WorldSystem3D>();
    }

    public static ResearchWorldId GetOpposite(ResearchWorldId world)
    {
        return world == ResearchWorldId.WorldA ? ResearchWorldId.WorldB : ResearchWorldId.WorldA;
    }

    public void ToggleWorld()
    {
        SetWorld(GetOpposite(currentWorld));
    }

    public void SetWorld(ResearchWorldId nextWorld)
    {
        if (currentWorld == nextWorld)
        {
            return;
        }

        ResearchWorldId previousWorld = currentWorld;
        currentWorld = nextWorld;

        AlignWorldRootsForPlay();
        RefreshWorldObjects();
        ActiveWorldChanged?.Invoke(previousWorld, currentWorld);
        GameProgressSave3D.SetCurrentWorld(currentWorld);
    }

    public void RefreshWorldObjects()
    {
        WorldStateObject3D[] stateObjects = FindObjectsByType<WorldStateObject3D>(FindObjectsSortMode.None);
        for (int i = 0; i < stateObjects.Length; i++)
        {
            if (stateObjects[i] != null)
            {
                stateObjects[i].Apply(currentWorld);
            }
        }

        WorldVariant3D[] variants = FindObjectsByType<WorldVariant3D>(FindObjectsSortMode.None);
        for (int i = 0; i < variants.Length; i++)
        {
            if (variants[i] != null)
            {
                variants[i].Refresh(currentWorld);
            }
        }

        WorldPresenceRegistry.RefreshAllFromScene(currentWorld);
    }

    private void ResolveWorldRoots()
    {
        if (sharedMapRoot == null)
        {
            GameObject foundSharedMap = FindSceneRootByName("Shared");
            sharedMapRoot = foundSharedMap != null ? foundSharedMap.transform : null;
        }

        if (worldARoot == null)
        {
            GameObject foundWorldA = FindSceneRootByName("World_A_Current");
            worldARoot = foundWorldA != null ? foundWorldA.transform : null;
        }

        if (worldBRoot == null)
        {
            GameObject foundWorldB = FindSceneRootByName("World_B_Past");
            worldBRoot = foundWorldB != null ? foundWorldB.transform : null;
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

    private void CacheWorldRootPositions()
    {
        if (cachedWorldRootPositions)
        {
            return;
        }

        worldAEditPosition = worldARoot != null ? worldARoot.position : Vector3.zero;
        worldBEditPosition = worldBRoot != null ? worldBRoot.position : Vector3.zero;
        cachedWorldRootPositions = true;
    }

    private void AlignWorldRootsForPlay()
    {
        if (!Application.isPlaying || !alignWorldRootsToPlayOrigin)
        {
            return;
        }

        ResolveWorldRoots();
        CacheWorldRootPositions();

        Vector3 playOrigin = GetPlayOrigin();
        ApplyWorldRootPosition(ResearchWorldId.WorldA, currentWorld == ResearchWorldId.WorldA ? playOrigin : worldAEditPosition);
        ApplyWorldRootPosition(ResearchWorldId.WorldB, currentWorld == ResearchWorldId.WorldB ? playOrigin : worldBEditPosition);
    }

    private Vector3 GetPlayOrigin()
    {
        if (useWorldARootAsPlayOrigin && worldARoot != null)
        {
            return worldAEditPosition;
        }

        return customPlayOrigin;
    }

    private void ApplyWorldRootPosition(ResearchWorldId world, Vector3 position)
    {
        Transform root = world == ResearchWorldId.WorldA ? worldARoot : worldBRoot;
        if (root != null)
        {
            root.position = position;
        }
    }
}
