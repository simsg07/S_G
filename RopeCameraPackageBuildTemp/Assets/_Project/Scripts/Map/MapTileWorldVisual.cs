using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class MapTileWorldVisual : MonoBehaviour
{
    [Header("World Visuals")]
    [SerializeField] private GameObject worldAVisual;
    [SerializeField] private GameObject worldBVisual;

    [Header("World Settings")]
    [SerializeField] private bool followGlobalWorld = true;
    [SerializeField] private WorldState initialWorld = WorldState.WorldA;
    [SerializeField] private WorldState editorPreviewWorld = WorldState.WorldA;
    [SerializeField] private bool showBothInEditMode = true;

    [Header("Debug")]
    [SerializeField] private bool debugMode;

    public GameObject WorldAVisual => worldAVisual;
    public GameObject WorldBVisual => worldBVisual;
    public WorldState CurrentWorld { get; private set; } = WorldState.WorldA;

    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            WorldSystem3D.ActiveWorldChanged += HandleWorldChanged;
            Refresh();
        }
    }

    private void OnDisable()
    {
        WorldSystem3D.ActiveWorldChanged -= HandleWorldChanged;
    }

    public void Refresh()
    {
        if (!Application.isPlaying)
        {
            ApplyEditorPreview();
            return;
        }

        ResearchWorldId world = followGlobalWorld ? WorldSystem3D.ActiveWorld : ToResearchWorld(initialWorld);
        ApplyWorld(world);
    }

    public void ApplyWorld(ResearchWorldId world)
    {
        CurrentWorld = world == ResearchWorldId.WorldA ? WorldState.WorldA : WorldState.WorldB;
        SetVisuals(CurrentWorld == WorldState.WorldA, CurrentWorld == WorldState.WorldB);
        Log($"Applied {CurrentWorld}");
    }

    public void ApplyWorld(WorldState world)
    {
        ApplyWorld(ToResearchWorld(world));
    }

    public void ShowAllInEditor()
    {
        if (Application.isPlaying)
        {
            return;
        }

        SetVisuals(true, true);
    }

    private void ApplyEditorPreview()
    {
        if (showBothInEditMode)
        {
            ShowAllInEditor();
            return;
        }

        ApplyWorld(editorPreviewWorld);
    }

    private void HandleWorldChanged(ResearchWorldId previousWorld, ResearchWorldId nextWorld)
    {
        if (followGlobalWorld)
        {
            ApplyWorld(nextWorld);
        }
    }

    private void SetVisuals(bool showWorldA, bool showWorldB)
    {
        if (worldAVisual != null && worldAVisual != gameObject)
        {
            if (worldAVisual.activeSelf != showWorldA)
            {
                worldAVisual.SetActive(showWorldA);
            }
        }

        if (worldBVisual != null && worldBVisual != gameObject)
        {
            if (worldBVisual.activeSelf != showWorldB)
            {
                worldBVisual.SetActive(showWorldB);
            }
        }
    }

    private static ResearchWorldId ToResearchWorld(WorldState world)
    {
        return world == WorldState.WorldA ? ResearchWorldId.WorldA : ResearchWorldId.WorldB;
    }

    private void Log(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[MapTileWorldVisual] {message}", this);
        }
    }
}
