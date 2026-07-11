using UnityEngine;

[DisallowMultipleComponent]
public class EyeballFlyAnimationController : MonoBehaviour
{
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int IsAttackingHash = Animator.StringToHash("IsAttacking");
    private static readonly int IsDeadHash = Animator.StringToHash("IsDead");
    private static readonly int AttackHash = Animator.StringToHash("Attack");

    [SerializeField] private Animator animator;
    [SerializeField] private bool disableRootMotion = true;

    private bool hasIsMoving;
    private bool hasIsAttacking;
    private bool hasIsDead;
    private bool hasAttack;
    private bool warnedMissingAnimator;

    public bool HasAnimator => animator != null;

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
    }

    private void OnValidate()
    {
        CacheReferences();
        CacheParameters();
        ApplyAnimatorSettings();
    }

    public bool SetMovingVisual(bool value)
    {
        return SetBool(IsMovingHash, hasIsMoving, value);
    }

    public bool SetAttackingVisual(bool value)
    {
        return SetBool(IsAttackingHash, hasIsAttacking, value);
    }

    public bool SetDeadVisual(bool value)
    {
        return SetBool(IsDeadHash, hasIsDead, value);
    }

    public bool PlayAttack()
    {
        if (animator != null && hasAttack)
        {
            animator.SetTrigger(AttackHash);
            return true;
        }

        WarnIfMissingAnimator();
        return false;
    }

    private void CacheReferences()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        if (animator != null)
        {
            warnedMissingAnimator = false;
        }
    }

    private void CacheParameters()
    {
        hasIsMoving = AnimatorParameterUtility3D.HasParameter(animator, IsMovingHash, AnimatorControllerParameterType.Bool);
        hasIsAttacking = AnimatorParameterUtility3D.HasParameter(animator, IsAttackingHash, AnimatorControllerParameterType.Bool);
        hasIsDead = AnimatorParameterUtility3D.HasParameter(animator, IsDeadHash, AnimatorControllerParameterType.Bool);
        hasAttack = AnimatorParameterUtility3D.HasParameter(animator, AttackHash, AnimatorControllerParameterType.Trigger);
    }

    private void ApplyAnimatorSettings()
    {
        if (animator != null && disableRootMotion)
        {
            animator.applyRootMotion = false;
        }
    }

    private bool SetBool(int parameterHash, bool hasParameter, bool value)
    {
        if (animator != null && hasParameter)
        {
            animator.SetBool(parameterHash, value);
            return true;
        }

        WarnIfMissingAnimator();
        return false;
    }

    private void WarnIfMissingAnimator()
    {
        if (animator != null || warnedMissingAnimator)
        {
            return;
        }

        warnedMissingAnimator = true;
        Debug.LogWarning("[EyeballFlyAnimationController] Animator was not found on this object or its children.", this);
    }
}
