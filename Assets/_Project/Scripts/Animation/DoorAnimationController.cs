using UnityEngine;

[DisallowMultipleComponent]
public class DoorAnimationController : MonoBehaviour
{
    private static readonly int IsOpenHash = Animator.StringToHash("IsOpen");

    [SerializeField] private Animator animator; // Visual-only Animator. Door collision and open rules stay in code.
    [SerializeField] private AbilityGate3D gateSource; // Optional existing door/gate logic source.
    [SerializeField] private WorldStateObject3D worldStateSource; // Optional world-state source for doors whose open state differs by world.
    [SerializeField] private bool isOpen; // Fallback value when no source is assigned.
    [SerializeField] private bool disableRootMotion = true; // Doors should not move gameplay objects through root motion.

    private bool hasIsOpen;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        CacheReferences();
        CacheParameters();
        ApplyAnimatorSettings();
    }

    private void OnEnable()
    {
        CacheReferences();
        CacheParameters();
        ApplyAnimatorSettings();
        Subscribe();
        RefreshFromSource();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnValidate()
    {
        CacheReferences();
        CacheParameters();
        ApplyAnimatorSettings();
        Refresh();
    }

    public void SetOpen(bool nextOpen)
    {
        if (isOpen == nextOpen)
        {
            Refresh();
            return;
        }

        isOpen = nextOpen;
        Refresh();
    }

    public void RefreshFromSource()
    {
        if (gateSource != null)
        {
            SetOpen(gateSource.IsOpen);
            return;
        }

        if (worldStateSource != null)
        {
            SetOpen(worldStateSource.GetState(WorldSystem3D.ActiveWorld).doorOpen);
            return;
        }

        Refresh();
    }

    private void CacheReferences()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        if (gateSource == null)
        {
            gateSource = GetComponent<AbilityGate3D>();
        }

        if (worldStateSource == null)
        {
            worldStateSource = GetComponent<WorldStateObject3D>();
        }
    }

    private void CacheParameters()
    {
        hasIsOpen = AnimatorParameterUtility3D.HasParameter(animator, IsOpenHash, AnimatorControllerParameterType.Bool);
    }

    private void ApplyAnimatorSettings()
    {
        if (animator != null && disableRootMotion)
        {
            animator.applyRootMotion = false;
        }
    }

    private void Subscribe()
    {
        if (gateSource != null)
        {
            gateSource.OpenStateChanged += HandleGateOpenStateChanged;
        }

        WorldSystem3D.ActiveWorldChanged += HandleWorldChanged;
    }

    private void Unsubscribe()
    {
        if (gateSource != null)
        {
            gateSource.OpenStateChanged -= HandleGateOpenStateChanged;
        }

        WorldSystem3D.ActiveWorldChanged -= HandleWorldChanged;
    }

    private void HandleGateOpenStateChanged(bool nextOpen)
    {
        SetOpen(nextOpen);
    }

    private void HandleWorldChanged(ResearchWorldId previousWorld, ResearchWorldId nextWorld)
    {
        RefreshFromSource();
    }

    private void Refresh()
    {
        if (animator != null && hasIsOpen)
        {
            animator.SetBool(IsOpenHash, isOpen);
        }
    }
}
