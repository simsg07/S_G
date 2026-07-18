using UnityEngine;

[DisallowMultipleComponent]
public class MonsterAnimatorBridge : MonoBehaviour
{
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int IsAttackingHash = Animator.StringToHash("IsAttacking");
    private static readonly int IsDeadHash = Animator.StringToHash("IsDead");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int StateHash = Animator.StringToHash("State");
    private static readonly int IsHowlingHash = Animator.StringToHash("IsHowling");
    private static readonly int HowlingHash = Animator.StringToHash("Howling");
    private static readonly int IsAttackFalseHash = Animator.StringToHash("IsAttackFalse");
    private static readonly int AttackFalseHash = Animator.StringToHash("AttackFalse");

    [Header("Animator Settings")]
    public bool enableAnimatorBridge = true;
    public Animator animator;
    public bool useIsMoving = true;
    public bool useIsAttacking = true;
    public bool useIsDead = true;
    public bool useAttackTrigger = true;
    public bool useCustomStateInt;

    [Header("Human Box Optional Parameters")]
    public bool useIsHowling = true;
    public bool useHowlingTrigger = true;
    public bool useIsAttackFalse = true;
    public bool useAttackFalseTrigger = true;

    [Header("Debug")]
    public bool debugMode;

    private void Reset()
    {
        AutoFill();
    }

    private void Awake()
    {
        AutoFill();
    }

    private void OnValidate()
    {
        AutoFill();
    }

    public void AutoFill()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }
    }

    public void SetMoving(bool value)
    {
        if (enableAnimatorBridge && useIsMoving)
        {
            SetBool(IsMovingHash, value);
        }
    }

    public void SetAttacking(bool value)
    {
        if (enableAnimatorBridge && useIsAttacking)
        {
            SetBool(IsAttackingHash, value);
        }
    }

    public void SetDead(bool value)
    {
        if (enableAnimatorBridge && useIsDead)
        {
            SetBool(IsDeadHash, value);
        }
    }

    public void TriggerAttack()
    {
        if (enableAnimatorBridge && useAttackTrigger)
        {
            SetTrigger(AttackHash);
        }
    }

    public void SetState(int value)
    {
        if (enableAnimatorBridge && useCustomStateInt)
        {
            SetInt(StateHash, value);
        }
    }

    public void SetHowling(bool value)
    {
        if (enableAnimatorBridge && useIsHowling)
        {
            SetBool(IsHowlingHash, value);
        }
    }

    public void TriggerHowling()
    {
        if (enableAnimatorBridge && useHowlingTrigger)
        {
            SetTrigger(HowlingHash);
        }
    }

    public void SetAttackFalse(bool value)
    {
        if (enableAnimatorBridge && useIsAttackFalse)
        {
            SetBool(IsAttackFalseHash, value);
        }
    }

    public void TriggerAttackFalse()
    {
        if (enableAnimatorBridge && useAttackFalseTrigger)
        {
            SetTrigger(AttackFalseHash);
        }
    }

    private void SetBool(int hash, bool value)
    {
        if (animator == null)
        {
            Log("Animator missing. Bool skipped.");
            return;
        }

        if (!AnimatorParameterUtility3D.HasParameter(animator, hash, AnimatorControllerParameterType.Bool))
        {
            Log("Animator bool parameter missing. Bool skipped.");
            return;
        }

        animator.SetBool(hash, value);
    }

    private void SetInt(int hash, int value)
    {
        if (animator == null)
        {
            Log("Animator missing. Int skipped.");
            return;
        }

        if (!AnimatorParameterUtility3D.HasParameter(animator, hash, AnimatorControllerParameterType.Int))
        {
            Log("Animator int parameter missing. Int skipped.");
            return;
        }

        animator.SetInteger(hash, value);
    }

    private void SetTrigger(int hash)
    {
        if (animator == null)
        {
            Log("Animator missing. Trigger skipped.");
            return;
        }

        if (!AnimatorParameterUtility3D.HasParameter(animator, hash, AnimatorControllerParameterType.Trigger))
        {
            Log("Animator trigger parameter missing. Trigger skipped.");
            return;
        }

        animator.SetTrigger(hash);
    }

    private void Log(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[MonsterAnimatorBridge] {message}", this);
        }
    }
}
