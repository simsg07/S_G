using UnityEngine;

public class AbilityGate3D : MonoBehaviour
{
    [SerializeField] private CameraAbilityFlags requiredAbilities = CameraAbilityFlags.Focus;
    [SerializeField] private bool hideWhenOpen = true;

    private RendererState[] rendererStates = new RendererState[0];
    private ColliderState[] colliderStates = new ColliderState[0];

    private void Awake()
    {
        CacheStates();
    }

    private void OnEnable()
    {
        CacheStates();
        CameraAbilitySystem3D.AbilitiesChanged += HandleAbilitiesChanged;
        Refresh();
    }

    private void OnDisable()
    {
        CameraAbilitySystem3D.AbilitiesChanged -= HandleAbilitiesChanged;
    }

    private void OnValidate()
    {
        CacheStates();
        Refresh();
    }

    private void HandleAbilitiesChanged(CameraAbilityFlags abilities)
    {
        Refresh();
    }

    private void Refresh()
    {
        bool isOpen = CameraAbilitySystem3D.IsKnown(requiredAbilities);

        for (int i = 0; i < colliderStates.Length; i++)
        {
            ColliderState state = colliderStates[i];
            if (state.Collider != null)
            {
                state.Collider.enabled = !isOpen && state.WasEnabled;
            }
        }

        for (int i = 0; i < rendererStates.Length; i++)
        {
            RendererState state = rendererStates[i];
            if (state.Renderer != null)
            {
                state.Renderer.enabled = state.WasEnabled && (!isOpen || !hideWhenOpen);
            }
        }
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
}
