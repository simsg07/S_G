using System;
using UnityEngine;

public enum ResearchWorldId
{
    WorldA,
    WorldB
}

public class WorldSystem3D : MonoBehaviour
{
    [SerializeField] private ResearchWorldId currentWorld = ResearchWorldId.WorldA;
    [SerializeField] private bool persistAcrossScenes;

    public static event Action<ResearchWorldId, ResearchWorldId> ActiveWorldChanged;

    public static WorldSystem3D Instance { get; private set; }
    public static ResearchWorldId ActiveWorld => Instance != null ? Instance.currentWorld : ResearchWorldId.WorldA;

    public ResearchWorldId CurrentWorld => currentWorld;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (persistAcrossScenes && Application.isPlaying)
        {
            DontDestroyOnLoad(gameObject);
        }

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

        GameObject worldSystemObject = new GameObject("World System");
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

        RefreshWorldObjects();
        ActiveWorldChanged?.Invoke(previousWorld, currentWorld);
        GameProgressSave3D.SetCurrentWorld(currentWorld);
    }

    public void RefreshWorldObjects()
    {
        WorldVariant3D[] variants = FindObjectsByType<WorldVariant3D>(FindObjectsSortMode.None);
        for (int i = 0; i < variants.Length; i++)
        {
            if (variants[i] != null)
            {
                variants[i].Refresh(currentWorld);
            }
        }
    }
}
