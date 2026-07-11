using UnityEngine;

[DisallowMultipleComponent]
public class TerminalAnimationController : MonoBehaviour
{
    private static readonly int IsOnHash = Animator.StringToHash("IsOn");
    private static readonly int IsActiveHash = Animator.StringToHash("IsActive");

    [SerializeField] private Animator animator; // Visual-only Animator. Interaction and UI rules stay in TerminalController.
    [SerializeField] private ResearchDevice3D terminalSource; // Optional existing terminal/device logic source.
    [SerializeField] private WorldStateObject3D worldStateSource; // Optional world-state source for inactive terminals.
    [SerializeField] private bool isOn; // Fallback value when no source is assigned.
    [SerializeField] private bool driveIsOn = true; // Sends IsOn to Animator when that parameter exists.
    [SerializeField] private bool driveIsActive = true; // Sends IsActive to Animator when that parameter exists.
    [SerializeField] private bool disableRootMotion = true; // Terminals should not move gameplay objects through root motion.

    private bool hasIsOn;
    private bool hasIsActive;

    public bool IsOn => isOn;

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

    public void SetOn(bool nextOn)
    {
        if (isOn == nextOn)
        {
            Refresh();
            return;
        }

        isOn = nextOn;
        Refresh();
    }

    public void RefreshFromSource()
    {
        if (terminalSource != null)
        {
            SetOn(terminalSource.IsActivated);
            return;
        }

        if (worldStateSource != null)
        {
            WorldObjectState3D state = worldStateSource.GetState(WorldSystem3D.ActiveWorld);
            SetOn(state.enabledInWorld && state.operationEnabled);
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

        if (terminalSource == null)
        {
            terminalSource = GetComponent<ResearchDevice3D>();
        }

        if (worldStateSource == null)
        {
            worldStateSource = GetComponent<WorldStateObject3D>();
        }
    }

    private void CacheParameters()
    {
        hasIsOn = AnimatorParameterUtility3D.HasParameter(animator, IsOnHash, AnimatorControllerParameterType.Bool);
        hasIsActive = AnimatorParameterUtility3D.HasParameter(animator, IsActiveHash, AnimatorControllerParameterType.Bool);
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
        if (terminalSource != null)
        {
            terminalSource.ActivationStateChanged += HandleActivationStateChanged;
        }

        WorldSystem3D.ActiveWorldChanged += HandleWorldChanged;
    }

    private void Unsubscribe()
    {
        if (terminalSource != null)
        {
            terminalSource.ActivationStateChanged -= HandleActivationStateChanged;
        }

        WorldSystem3D.ActiveWorldChanged -= HandleWorldChanged;
    }

    private void HandleActivationStateChanged(bool nextOn)
    {
        SetOn(nextOn);
    }

    private void HandleWorldChanged(ResearchWorldId previousWorld, ResearchWorldId nextWorld)
    {
        RefreshFromSource();
    }

    private void Refresh()
    {
        if (animator == null)
        {
            return;
        }

        if (driveIsOn && hasIsOn)
        {
            animator.SetBool(IsOnHash, isOn);
        }

        if (driveIsActive && hasIsActive)
        {
            animator.SetBool(IsActiveHash, isOn);
        }
    }
}
