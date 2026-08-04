using System;
using UnityEngine;

public class AbilityGate3D : MonoBehaviour
{
    [SerializeField] private CameraAbilityFlags requiredAbilities = CameraAbilityFlags.Focus;
    [SerializeField] private bool hideWhenOpen = true;

    private RendererState[] rendererStates = new RendererState[0];
    private ColliderState[] colliderStates = new ColliderState[0];
    private bool isOpen;
    private bool statesCached;

    public event Action<bool> OpenStateChanged;
    public bool IsOpen => isOpen;

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
        bool nextOpen = CameraAbilitySystem3D.IsKnown(requiredAbilities);
        bool changed = isOpen != nextOpen;
        isOpen = nextOpen;

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

        if (changed)
        {
            OpenStateChanged?.Invoke(isOpen);
        }
    }

    private void CacheStates()
    {
        RendererState[] previousRendererStates = statesCached ? rendererStates : new RendererState[0];
        ColliderState[] previousColliderStates = statesCached ? colliderStates : new ColliderState[0];

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

        statesCached = true;
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
