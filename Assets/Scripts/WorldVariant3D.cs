using UnityEngine;

[ExecuteAlways]
public class WorldVariant3D : MonoBehaviour
{
    [SerializeField] private ResearchWorldId activeWorld = ResearchWorldId.WorldA;
    [SerializeField] private bool existsInBothWorlds;
    [SerializeField] private bool controlRenderers = true;
    [SerializeField] private bool controlColliders = true;
    [SerializeField] private Behaviour[] extraBehaviours = new Behaviour[0];
    [SerializeField] private GameObject[] extraObjects = new GameObject[0];

    private RendererState[] rendererStates = new RendererState[0];
    private ColliderState[] colliderStates = new ColliderState[0];
    private BehaviourState[] behaviourStates = new BehaviourState[0];

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

    private void CacheStates()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        rendererStates = new RendererState[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            rendererStates[i] = new RendererState(renderers[i], renderers[i].enabled);
        }

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        colliderStates = new ColliderState[colliders.Length];
        for (int i = 0; i < colliders.Length; i++)
        {
            colliderStates[i] = new ColliderState(colliders[i], colliders[i].enabled);
        }

        Behaviour[] behaviours = extraBehaviours ?? new Behaviour[0];
        behaviourStates = new BehaviourState[behaviours.Length];
        for (int i = 0; i < behaviours.Length; i++)
        {
            Behaviour behaviour = behaviours[i];
            behaviourStates[i] = new BehaviourState(behaviour, behaviour != null && behaviour.enabled);
        }
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
