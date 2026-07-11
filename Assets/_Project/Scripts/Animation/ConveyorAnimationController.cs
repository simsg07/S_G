using UnityEngine;

[DisallowMultipleComponent]
public class ConveyorAnimationController : MonoBehaviour
{
    private static readonly int IsActiveHash = Animator.StringToHash("IsActive");
    private static readonly int DirectionHash = Animator.StringToHash("Direction");

    [SerializeField] private Animator animator; // Visual-only Animator. Conveyor movement rules stay in ConveyorController.
    [SerializeField] private bool isActive = true; // ConveyorController or UnityEvent can set this value.
    [SerializeField] private float direction = 1f; // -1 for left, 1 for right, or a custom blend value.
    [SerializeField] private bool disableRootMotion = true; // Conveyors should not move gameplay objects through root motion.

    private bool hasIsActive;
    private bool hasDirection;

    public bool IsActive => isActive;
    public float Direction => direction;

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
        direction = Mathf.Approximately(direction, 0f) ? 1f : Mathf.Sign(direction);
        CacheReferences();
        CacheParameters();
        ApplyAnimatorSettings();
        Refresh();
    }

    public void SetActiveState(bool nextActive)
    {
        if (isActive == nextActive)
        {
            Refresh();
            return;
        }

        isActive = nextActive;
        Refresh();
    }

    public void SetDirection(float nextDirection)
    {
        direction = Mathf.Approximately(nextDirection, 0f) ? direction : Mathf.Sign(nextDirection);
        Refresh();
    }

    public void SetConveyorState(bool nextActive, float nextDirection)
    {
        isActive = nextActive;
        direction = Mathf.Approximately(nextDirection, 0f) ? direction : Mathf.Sign(nextDirection);
        Refresh();
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
        hasIsActive = AnimatorParameterUtility3D.HasParameter(animator, IsActiveHash, AnimatorControllerParameterType.Bool);
        hasDirection = AnimatorParameterUtility3D.HasParameter(animator, DirectionHash, AnimatorControllerParameterType.Float);
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
        if (animator == null)
        {
            return;
        }

        if (hasIsActive)
        {
            animator.SetBool(IsActiveHash, isActive);
        }

        if (hasDirection)
        {
            animator.SetFloat(DirectionHash, direction);
        }
    }
}
