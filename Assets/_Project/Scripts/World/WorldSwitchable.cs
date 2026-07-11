using System;
using UnityEngine;
using UnityEngine.Serialization;

public enum WorldSwitchableEditorPreviewMode
{
    AlwaysVisible,
    PreviewWorldA,
    PreviewWorldB
}

[ExecuteAlways]
[DisallowMultipleComponent]
public class WorldSwitchable : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [Header("Switch")]
    [FormerlySerializedAs("canSwitch")]
    [SerializeField] private bool canSwitchByCamera = true; // If false, CameraWorldSwitcher ignores this object.
    [SerializeField] private bool initializeFromGlobalWorld = true; // If true, play mode starts from WorldSystem3D.ActiveWorld.
    [SerializeField] private ResearchWorldId initialWorld = ResearchWorldId.WorldA; // Initial world used when initializeFromGlobalWorld is false.

    [Header("Editor Display")]
    [SerializeField] private bool showInEditor = true; // Keeps this object visible while editing the map.
    [SerializeField] private bool showGizmo = true; // Draws a small bounds outline for this switchable object.
    [SerializeField] private WorldSwitchableEditorPreviewMode editorPreviewMode = WorldSwitchableEditorPreviewMode.AlwaysVisible; // Default keeps every switch target visible in Edit Mode.

    [Header("State Sources")]
    [SerializeField] private bool preferWorldStateObject = true; // Uses WorldStateObject3D state data first when available.
    [SerializeField] private bool preferWorldVariant = true; // Uses WorldVariant3D membership data when no WorldStateObject3D is available.
    [SerializeField] private WorldStateObject3D worldStateObject; // Optional existing world-state component.
    [SerializeField] private WorldVariant3D worldVariant; // Optional existing world-variant component.

    [Header("Local State")]
    [SerializeField] private bool includeChildRenderers = true; // Auto-controls child renderers when local state is used.
    [SerializeField] private bool includeChildColliders = true; // Auto-controls child colliders when local state is used.
    [SerializeField] private bool includeChildBehaviours; // Auto-controls child behaviours when local state is used.
    [SerializeField] private Renderer[] extraRenderers = new Renderer[0]; // Additional renderers controlled by local state.
    [SerializeField] private Collider[] extraColliders = new Collider[0]; // Additional colliders controlled by local state.
    [SerializeField] private Behaviour[] extraBehaviours = new Behaviour[0]; // Additional interaction scripts controlled by local state.
    [SerializeField] private GameObject[] extraObjects = new GameObject[0]; // Additional objects toggled by local state.
    [SerializeField] private WorldObjectState3D worldA = WorldObjectState3D.CreateDefault(Color.white); // Local state used in World A when no existing state component is preferred.
    [SerializeField] private WorldObjectState3D worldB = WorldObjectState3D.CreateDefault(Color.white); // Local state used in World B when no existing state component is preferred.

    private Renderer[] cachedRenderers = new Renderer[0];
    private Collider[] cachedColliders = new Collider[0];
    private Behaviour[] cachedBehaviours = new Behaviour[0];
    private Vector3 baseLocalPosition;
    private Vector3 baseLocalScale;
    private bool baseTransformCached;
    private bool targetsCached;

    public bool CanSwitchByCamera => canSwitchByCamera;
    public bool ShowInEditor
    {
        get => showInEditor;
        set => showInEditor = value;
    }
    public bool ShowGizmo
    {
        get => showGizmo;
        set => showGizmo = value;
    }
    public WorldSwitchableEditorPreviewMode EditorPreviewMode
    {
        get => editorPreviewMode;
        set => editorPreviewMode = value;
    }
    public ResearchWorldId CurrentWorld { get; private set; } = ResearchWorldId.WorldA;

    private void Awake()
    {
        CacheBaseTransform(false);
        CacheTargets();
    }

    private void OnEnable()
    {
        CacheBaseTransform(false);
        CacheTargets();

        if (Application.isPlaying)
        {
            CurrentWorld = initializeFromGlobalWorld ? WorldSystem3D.ActiveWorld : initialWorld;
            ApplyWorld(CurrentWorld, true);
            return;
        }

        ApplyEditorDisplay();
    }

    private void OnValidate()
    {
        CacheBaseTransform(true);
        CacheTargets();

        if (!Application.isPlaying)
        {
            ApplyEditorDisplay();
        }
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            ApplyEditorDisplay();
        }
    }

    public void ToggleWorld()
    {
        ResearchWorldId nextWorld = CurrentWorld == ResearchWorldId.WorldA
            ? ResearchWorldId.WorldB
            : ResearchWorldId.WorldA;
        ApplyWorld(nextWorld, true);
    }

    public void ApplyWorld(ResearchWorldId world, bool force)
    {
        if (!force && CurrentWorld == world)
        {
            return;
        }

        CurrentWorld = world;

        if (!Application.isPlaying)
        {
            ApplyEditorDisplay();
            return;
        }

        if (preferWorldStateObject && ResolveWorldStateObject() != null)
        {
            worldStateObject.Apply(world);
            return;
        }

        if (preferWorldVariant && ResolveWorldVariant() != null)
        {
            worldVariant.Refresh(world);
            return;
        }

        ApplyLocalState(world);
    }

    public bool TryGetWorldBounds(out Bounds bounds)
    {
        CacheTargets();

        bool hasBounds = false;
        bounds = new Bounds(transform.position, Vector3.one * 0.25f);

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer target = cachedRenderers[i];
            if (target == null)
            {
                continue;
            }

            AddBounds(ref bounds, ref hasBounds, target.bounds);
        }

        for (int i = 0; i < cachedColliders.Length; i++)
        {
            Collider target = cachedColliders[i];
            if (target == null)
            {
                continue;
            }

            AddBounds(ref bounds, ref hasBounds, target.bounds);
        }

        if (!hasBounds)
        {
            bounds = new Bounds(transform.position, Vector3.one * 0.5f);
        }

        return true;
    }

    private void ApplyEditorDisplay()
    {
        if (!showInEditor)
        {
            return;
        }

        switch (editorPreviewMode)
        {
            case WorldSwitchableEditorPreviewMode.PreviewWorldA:
                ApplyLocalState(ResearchWorldId.WorldA);
                break;
            case WorldSwitchableEditorPreviewMode.PreviewWorldB:
                ApplyLocalState(ResearchWorldId.WorldB);
                break;
            default:
                ApplyAlwaysVisibleInEditor();
                break;
        }
    }

    private void ApplyAlwaysVisibleInEditor()
    {
        CacheTargets();
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer target = cachedRenderers[i];
            if (target != null)
            {
                target.enabled = true;
            }
        }

        for (int i = 0; i < cachedColliders.Length; i++)
        {
            Collider target = cachedColliders[i];
            if (target != null)
            {
                target.enabled = true;
            }
        }
    }

    private void ApplyLocalState(ResearchWorldId world)
    {
        CacheBaseTransform(false);
        CacheTargets();
        WorldObjectState3D state = world == ResearchWorldId.WorldA ? worldA : worldB;
        bool enabledInWorld = state.enabledInWorld;

        if (Application.isPlaying)
        {
            ApplyTransformState(state);
        }

        ApplyRendererState(state, enabledInWorld);
        ApplyColliderState(state, enabledInWorld);

        if (Application.isPlaying)
        {
            ApplyBehaviourState(state, enabledInWorld);
            ApplyExtraObjectState(enabledInWorld);
        }
    }

    private void ApplyTransformState(WorldObjectState3D state)
    {
        Vector3 nextPosition = baseLocalPosition;
        if (state.useLocalPositionOffset)
        {
            nextPosition += state.localPositionOffset;
        }

        if (state.doorOpen)
        {
            nextPosition += state.doorOpenLocalOffset;
        }

        transform.localPosition = nextPosition;
        TwoPointFiveDUtility3D.ClampTransformToPlane(transform);

        transform.localScale = state.useLocalScaleOverride ? state.localScaleOverride : baseLocalScale;
    }

    private void ApplyRendererState(WorldObjectState3D state, bool enabledInWorld)
    {
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer target = cachedRenderers[i];
            if (target == null)
            {
                continue;
            }

            target.enabled = enabledInWorld && state.rendererEnabled;
            ApplyRendererColor(target, enabledInWorld && state.useColorOverride, state.colorOverride);
        }
    }

    private void ApplyColliderState(WorldObjectState3D state, bool enabledInWorld)
    {
        for (int i = 0; i < cachedColliders.Length; i++)
        {
            Collider target = cachedColliders[i];
            if (target != null)
            {
                target.enabled = enabledInWorld && state.collisionEnabled;
            }
        }
    }

    private void ApplyBehaviourState(WorldObjectState3D state, bool enabledInWorld)
    {
        for (int i = 0; i < cachedBehaviours.Length; i++)
        {
            Behaviour target = cachedBehaviours[i];
            if (target != null)
            {
                target.enabled = enabledInWorld && state.operationEnabled;
            }
        }
    }

    private void ApplyExtraObjectState(bool enabledInWorld)
    {
        GameObject[] objects = extraObjects ?? new GameObject[0];
        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null)
            {
                objects[i].SetActive(enabledInWorld);
            }
        }
    }

    private void CacheBaseTransform(bool force)
    {
        if (baseTransformCached && !force)
        {
            return;
        }

        baseLocalPosition = transform.localPosition;
        baseLocalScale = transform.localScale;
        baseTransformCached = true;
    }

    private void CacheTargets()
    {
        if (targetsCached && Application.isPlaying)
        {
            return;
        }

        if (worldStateObject == null)
        {
            worldStateObject = GetComponent<WorldStateObject3D>();
        }

        if (worldVariant == null)
        {
            worldVariant = GetComponent<WorldVariant3D>();
        }

        cachedRenderers = includeChildRenderers
            ? MergeTargets(GetComponentsInChildren<Renderer>(true), extraRenderers)
            : MergeTargets(GetComponents<Renderer>(), extraRenderers);

        cachedColliders = includeChildColliders
            ? MergeTargets(GetComponentsInChildren<Collider>(true), extraColliders)
            : MergeTargets(GetComponents<Collider>(), extraColliders);

        Behaviour[] autoBehaviours = includeChildBehaviours
            ? GetComponentsInChildren<Behaviour>(true)
            : GetComponents<Behaviour>();
        cachedBehaviours = MergeBehaviours(autoBehaviours, extraBehaviours);
        targetsCached = true;
    }

    private WorldStateObject3D ResolveWorldStateObject()
    {
        if (worldStateObject == null)
        {
            worldStateObject = GetComponent<WorldStateObject3D>();
        }

        return worldStateObject;
    }

    private WorldVariant3D ResolveWorldVariant()
    {
        if (worldVariant == null)
        {
            worldVariant = GetComponent<WorldVariant3D>();
        }

        return worldVariant;
    }

    private static void ApplyRendererColor(Renderer target, bool useColor, Color color)
    {
        if (target == null)
        {
            return;
        }

        if (!useColor)
        {
            target.SetPropertyBlock(null);
            return;
        }

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        target.GetPropertyBlock(block);
        block.SetColor(BaseColorId, color);
        block.SetColor(ColorId, color);
        target.SetPropertyBlock(block);
    }

    private static T[] MergeTargets<T>(T[] automaticTargets, T[] extraTargets) where T : Component
    {
        int automaticCount = automaticTargets != null ? automaticTargets.Length : 0;
        int extraCount = extraTargets != null ? extraTargets.Length : 0;
        T[] merged = new T[automaticCount + extraCount];
        int count = 0;

        for (int i = 0; i < automaticCount; i++)
        {
            AddUnique(merged, ref count, automaticTargets[i]);
        }

        for (int i = 0; i < extraCount; i++)
        {
            AddUnique(merged, ref count, extraTargets[i]);
        }

        Array.Resize(ref merged, count);
        return merged;
    }

    private Behaviour[] MergeBehaviours(Behaviour[] automaticTargets, Behaviour[] extraTargets)
    {
        int automaticCount = automaticTargets != null ? automaticTargets.Length : 0;
        int extraCount = extraTargets != null ? extraTargets.Length : 0;
        Behaviour[] merged = new Behaviour[automaticCount + extraCount];
        int count = 0;

        for (int i = 0; i < automaticCount; i++)
        {
            Behaviour target = automaticTargets[i];
            if (ShouldControlBehaviour(target))
            {
                AddUnique(merged, ref count, target);
            }
        }

        for (int i = 0; i < extraCount; i++)
        {
            Behaviour target = extraTargets[i];
            if (ShouldControlBehaviour(target))
            {
                AddUnique(merged, ref count, target);
            }
        }

        Array.Resize(ref merged, count);
        return merged;
    }

    private bool ShouldControlBehaviour(Behaviour target)
    {
        return target != null
            && target != this
            && !(target is WorldSwitchable)
            && !(target is WorldSystem3D)
            && !(target is WorldManager)
            && !(target is WorldVariant3D)
            && !(target is WorldStateObject3D);
    }

    private static void AddUnique<T>(T[] targets, ref int count, T target) where T : Component
    {
        if (target == null)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            if (targets[i] == target)
            {
                return;
            }
        }

        targets[count] = target;
        count++;
    }

    private static void AddBounds(ref Bounds bounds, ref bool hasBounds, Bounds nextBounds)
    {
        if (nextBounds.size == Vector3.zero)
        {
            return;
        }

        if (!hasBounds)
        {
            bounds = nextBounds;
            hasBounds = true;
            return;
        }

        bounds.Encapsulate(nextBounds);
    }

    private void OnDrawGizmos()
    {
        if (!showGizmo || !TryGetWorldBounds(out Bounds bounds))
        {
            return;
        }

        Gizmos.color = CurrentWorld == ResearchWorldId.WorldA
            ? new Color(0.35f, 0.9f, 1f, 0.75f)
            : new Color(1f, 0.55f, 0.95f, 0.75f);
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
}
