using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class WorldVariant3D : MonoBehaviour
{
    [SerializeField] private ResearchWorldId activeWorld = ResearchWorldId.WorldA;
    [SerializeField] private bool existsInBothWorlds;
    [SerializeField] private bool controlRenderers = true;
    [SerializeField] private bool controlColliders = true;
    [SerializeField] private bool controlBehaviours = true; // Disables visual helper behaviours together with this world variant.
    [SerializeField] private Behaviour[] extraBehaviours = new Behaviour[0];
    [SerializeField] private GameObject[] extraObjects = new GameObject[0];
    [SerializeField] private bool previewInEditMode; // Lets designers preview world-specific visibility in the editor.

    private RendererState[] rendererStates = new RendererState[0];
    private ColliderState[] colliderStates = new ColliderState[0];
    private BehaviourState[] behaviourStates = new BehaviourState[0];
    private bool statesCached;

    public ResearchWorldId ActiveWorld => activeWorld;
    public bool ExistsInBothWorlds
    {
        get => existsInBothWorlds;
        set => existsInBothWorlds = value;
    }

    private void Awake()
    {
        CacheStates();
    }

    private void OnEnable()
    {
        CacheStates();
        WorldSystem3D.ActiveWorldChanged += HandleWorldChanged;
        Refresh(WorldSystem3D.ActiveWorld);
    }

    private void OnDisable()
    {
        WorldSystem3D.ActiveWorldChanged -= HandleWorldChanged;
    }

    private void OnValidate()
    {
        CacheStates();
        if (!Application.isPlaying && ShouldSkipEditorPreviewForWorldSwitchable())
        {
            return;
        }

        Refresh(WorldSystem3D.ActiveWorld);
    }

    public void SetActiveWorld(ResearchWorldId world)
    {
        activeWorld = world;
        existsInBothWorlds = false;
        Refresh(WorldSystem3D.ActiveWorld);
    }

    public void Refresh(ResearchWorldId currentWorld)
    {
        if (!Application.isPlaying && !previewInEditMode)
        {
            return;
        }

        bool shouldExist = existsInBothWorlds || activeWorld == currentWorld;

        if (controlRenderers)
        {
            for (int i = 0; i < rendererStates.Length; i++)
            {
                RendererState state = rendererStates[i];
                if (state.Renderer != null)
                {
                    state.Renderer.enabled = shouldExist && state.WasEnabled;
                }
            }
        }

        if (controlColliders)
        {
            for (int i = 0; i < colliderStates.Length; i++)
            {
                ColliderState state = colliderStates[i];
                if (state.Collider != null)
                {
                    state.Collider.enabled = shouldExist && state.WasEnabled;
                }
            }
        }

        for (int i = 0; i < behaviourStates.Length; i++)
        {
            BehaviourState state = behaviourStates[i];
            if (state.Behaviour != null)
            {
                state.Behaviour.enabled = shouldExist && state.WasEnabled;
            }
        }

        GameObject[] objects = extraObjects ?? new GameObject[0];
        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null)
            {
                objects[i].SetActive(shouldExist);
            }
        }
    }

    private void HandleWorldChanged(ResearchWorldId previousWorld, ResearchWorldId nextWorld)
    {
        Refresh(nextWorld);
    }

    private bool ShouldSkipEditorPreviewForWorldSwitchable()
    {
        return TryGetComponent(out WorldSwitchable switchable)
            && switchable.ShowInEditor
            && switchable.EditorPreviewMode == WorldSwitchableEditorPreviewMode.AlwaysVisible;
    }

    private void CacheStates()
    {
        RendererState[] previousRendererStates = statesCached ? rendererStates : new RendererState[0];
        ColliderState[] previousColliderStates = statesCached ? colliderStates : new ColliderState[0];
        BehaviourState[] previousBehaviourStates = statesCached ? behaviourStates : new BehaviourState[0];

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        rendererStates = new RendererState[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            bool wasEnabled = TryGetRendererWasEnabled(previousRendererStates, renderers[i], out bool cachedWasEnabled)
                ? cachedWasEnabled
                : renderers[i].enabled;
            rendererStates[i] = new RendererState(renderers[i], wasEnabled);
        }

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        colliderStates = new ColliderState[colliders.Length];
        for (int i = 0; i < colliders.Length; i++)
        {
            bool wasEnabled = TryGetColliderWasEnabled(previousColliderStates, colliders[i], out bool cachedWasEnabled)
                ? cachedWasEnabled
                : colliders[i].enabled;
            colliderStates[i] = new ColliderState(colliders[i], wasEnabled);
        }

        List<Behaviour> behaviours = new List<Behaviour>();
        if (controlBehaviours)
        {
            Behaviour[] childBehaviours = GetComponentsInChildren<Behaviour>(true);
            for (int i = 0; i < childBehaviours.Length; i++)
            {
                Behaviour behaviour = childBehaviours[i];
                if (ShouldSkipBehaviour(behaviour) || behaviours.Contains(behaviour))
                {
                    continue;
                }

                behaviours.Add(behaviour);
            }
        }

        Behaviour[] extraBehaviourArray = extraBehaviours ?? new Behaviour[0];
        for (int i = 0; i < extraBehaviourArray.Length; i++)
        {
            Behaviour behaviour = extraBehaviourArray[i];
            if (!ShouldSkipBehaviour(behaviour) && !behaviours.Contains(behaviour))
            {
                behaviours.Add(behaviour);
            }
        }

        behaviourStates = new BehaviourState[behaviours.Count];
        for (int i = 0; i < behaviours.Count; i++)
        {
            Behaviour behaviour = behaviours[i];
            bool wasEnabled = TryGetBehaviourWasEnabled(previousBehaviourStates, behaviour, out bool cachedWasEnabled)
                ? cachedWasEnabled
                : behaviour != null && behaviour.enabled;
            behaviourStates[i] = new BehaviourState(behaviour, wasEnabled);
        }

        statesCached = true;
    }

    private static bool ShouldSkipBehaviour(Behaviour behaviour)
    {
        return behaviour == null
            || behaviour is WorldSwitchable
            || behaviour is WorldVariant3D
            || behaviour is WorldStateObject3D;
    }

    private static bool TryGetRendererWasEnabled(RendererState[] states, Renderer renderer, out bool wasEnabled)
    {
        for (int i = 0; i < states.Length; i++)
        {
            if (states[i].Renderer == renderer)
            {
                wasEnabled = states[i].WasEnabled;
                return true;
            }
        }

        wasEnabled = false;
        return false;
    }

    private static bool TryGetColliderWasEnabled(ColliderState[] states, Collider targetCollider, out bool wasEnabled)
    {
        for (int i = 0; i < states.Length; i++)
        {
            if (states[i].Collider == targetCollider)
            {
                wasEnabled = states[i].WasEnabled;
                return true;
            }
        }

        wasEnabled = false;
        return false;
    }

    private static bool TryGetBehaviourWasEnabled(BehaviourState[] states, Behaviour behaviour, out bool wasEnabled)
    {
        for (int i = 0; i < states.Length; i++)
        {
            if (states[i].Behaviour == behaviour)
            {
                wasEnabled = states[i].WasEnabled;
                return true;
            }
        }

        wasEnabled = false;
        return false;
    }

    private readonly struct RendererState
    {
        public RendererState(Renderer renderer, bool wasEnabled)
        {
            Renderer = renderer;
            WasEnabled = wasEnabled;
        }

        public Renderer Renderer { get; }
        public bool WasEnabled { get; }
    }

    private readonly struct ColliderState
    {
        public ColliderState(Collider collider, bool wasEnabled)
        {
            Collider = collider;
            WasEnabled = wasEnabled;
        }

        public Collider Collider { get; }
        public bool WasEnabled { get; }
    }

    private readonly struct BehaviourState
    {
        public BehaviourState(Behaviour behaviour, bool wasEnabled)
        {
            Behaviour = behaviour;
            WasEnabled = wasEnabled;
        }

        public Behaviour Behaviour { get; }
        public bool WasEnabled { get; }
    }
}
