using UnityEngine;

[DisallowMultipleComponent]
public class ButtonAnimationController : MonoBehaviour
{
    private static readonly int IsPressedHash = Animator.StringToHash("IsPressed");

    [SerializeField] private Animator animator; // Visual-only Animator. Button trigger rules stay in ButtonController.
    [SerializeField] private bool isPressed; // ButtonController or UnityEvent can set this value.
    [SerializeField] private bool disableRootMotion = true; // Buttons should not move gameplay objects through root motion.

    private bool hasIsPressed;

    public bool IsPressed => isPressed;

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
        Refresh();
    }

    private void OnValidate()
    {
        CacheReferences();
        CacheParameters();
        ApplyAnimatorSettings();
        Refresh();
    }

    public void SetPressed(bool nextPressed)
    {
        if (isPressed == nextPressed)
        {
            Refresh();
            return;
        }

        isPressed = nextPressed;
        Refresh();
    }

    public void Press()
    {
        SetPressed(true);
    }

    public void Release()
    {
        SetPressed(false);
    }

    private void CacheReferences()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }
    }

    private void CacheParameters()
    {
        hasIsPressed = AnimatorParameterUtility3D.HasParameter(animator, IsPressedHash, AnimatorControllerParameterType.Bool);
    }

    private void ApplyAnimatorSettings()
    {
        if (animator != null && disableRootMotion)
        {
            animator.applyRootMotion = false;
        }
    }

    private void Refresh()
    {
        if (animator != null && hasIsPressed)
        {
            animator.SetBool(IsPressedHash, isPressed);
        }
    }
}
