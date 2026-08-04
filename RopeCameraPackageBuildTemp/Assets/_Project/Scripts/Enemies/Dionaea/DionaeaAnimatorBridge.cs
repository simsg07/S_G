using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
#endif

[DisallowMultipleComponent]
public sealed class DionaeaAnimatorBridge : MonoBehaviour
{
    [Header("Animator")]
    [SerializeField] private Animator animator;
    [SerializeField] private bool useAnimator = true;
    [SerializeField] private string attackTriggerName = "Attack";
    [SerializeField] private string attackingBoolName = "IsAttacking";
    [SerializeField] private string retractedBoolName = "IsRetracted";
    [SerializeField] private string recoveringBoolName = "IsRecovering";

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;

    private bool warnedMissingAnimator;
    private bool warnedMissingController;

    public Animator Animator { get => animator; set => animator = value; }

    private void Reset() => AutoFill();
    private void Awake() => AutoFill();
    private void OnValidate() => AutoFill();

    public void SetAttacking(bool value) => SafeSetBool(attackingBoolName, value);
    public void SetRetracted(bool value) => SafeSetBool(retractedBoolName, value);
    public void SetRecovering(bool value) => SafeSetBool(recoveringBoolName, value);
    public void PlayAttack() => SafeSetTrigger(attackTriggerName);
    public void ResetAttackTrigger() => SafeResetTrigger(attackTriggerName);

    [ContextMenu("Test Attack Animation")]
    private void TestAttackAnimation() => PlayAttack();
    [ContextMenu("Test Retracted On")]
    private void TestRetractedOn() => SetRetracted(true);
    [ContextMenu("Test Retracted Off")]
    private void TestRetractedOff() => SetRetracted(false);

    [ContextMenu("Validate Animator Setup")]
    public void ValidateAnimatorSetup()
    {
        AutoFill();
        Transform visual = transform.Find("Visual");
        if (visual == null) Debug.LogWarning("[DionaeaAnimatorBridge] Missing Visual child.", this);
        SpriteRenderer spriteRenderer = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
        if (spriteRenderer == null) Debug.LogWarning("[DionaeaAnimatorBridge] Missing Visual SpriteRenderer.", this);
        else if (spriteRenderer.sprite == null) Debug.LogWarning("[DionaeaAnimatorBridge] Visual SpriteRenderer has no default Sprite.", this);

        if (animator == null)
        {
            Debug.LogWarning("[DionaeaAnimatorBridge] Missing Visual Animator.", this);
            return;
        }
        Debug.Log("[DionaeaAnimatorBridge] Animator found.", this);
        if (animator.applyRootMotion) Debug.LogWarning("[DionaeaAnimatorBridge] Apply Root Motion is ON. It should be OFF.", this);
        if (animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning("[DionaeaAnimatorBridge] Missing Runtime Animator Controller.", this);
            return;
        }
        Debug.Log("[DionaeaAnimatorBridge] Controller found.", this);
        ValidateParameter(attackTriggerName, AnimatorControllerParameterType.Trigger);
        ValidateParameter(attackingBoolName, AnimatorControllerParameterType.Bool);
        ValidateParameter(retractedBoolName, AnimatorControllerParameterType.Bool);
        ValidateParameter(recoveringBoolName, AnimatorControllerParameterType.Bool);

#if UNITY_EDITOR
        AnimatorController controller = animator.runtimeAnimatorController as AnimatorController;
        if (controller == null || controller.layers.Length == 0)
        {
            Debug.LogWarning("[DionaeaAnimatorBridge] Controller state machine could not be inspected.", this);
            return;
        }
        AnimatorState idleState = controller.layers[0].stateMachine.defaultState;
        if (idleState == null || idleState.name != "Idle")
        {
            Debug.LogWarning("[DionaeaAnimatorBridge] Idle state should be default.", this);
            return;
        }
        AnimationClip idleClip = idleState.motion as AnimationClip;
        if (idleClip == null)
        {
            Debug.LogWarning("[DionaeaAnimatorBridge] Default Idle state has no AnimationClip.", this);
            return;
        }
        EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(idleClip);
        bool hasSpriteFrames = false;
        for (int i = 0; i < bindings.Length; i++)
        {
            if (bindings[i].type != typeof(SpriteRenderer) || bindings[i].propertyName != "m_Sprite") continue;
            hasSpriteFrames = AnimationUtility.GetObjectReferenceCurve(idleClip, bindings[i]).Length > 0;
            if (hasSpriteFrames) break;
        }
        if (!hasSpriteFrames) Debug.LogWarning("[DionaeaAnimatorBridge] Idle Clip has no Sprite keyframes.", this);
#endif
    }

    private void ValidateParameter(string parameterName, AnimatorControllerParameterType type)
    {
        if (string.IsNullOrWhiteSpace(parameterName) ||
            !AnimatorParameterUtility3D.HasParameter(animator, Animator.StringToHash(parameterName), type))
        {
            Debug.LogWarning($"[DionaeaAnimatorBridge] Missing parameter: {parameterName} ({type}).", this);
        }
    }

    private void SafeSetBool(string parameterName, bool value)
    {
        if (!CanUseAnimator() || string.IsNullOrWhiteSpace(parameterName)) return;
        int hash = Animator.StringToHash(parameterName);
        if (AnimatorParameterUtility3D.HasParameter(animator, hash, AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(hash, value);
        }
        else if (debugMode)
        {
            Debug.LogWarning($"[DionaeaAnimatorBridge] Bool parameter '{parameterName}' is not present; animation update was skipped.", this);
        }
    }

    private void SafeSetTrigger(string parameterName)
    {
        if (!CanUseAnimator() || string.IsNullOrWhiteSpace(parameterName)) return;
        int hash = Animator.StringToHash(parameterName);
        if (AnimatorParameterUtility3D.HasParameter(animator, hash, AnimatorControllerParameterType.Trigger))
        {
            animator.SetTrigger(hash);
        }
        else if (debugMode)
        {
            Debug.LogWarning($"[DionaeaAnimatorBridge] Trigger parameter '{parameterName}' is not present; animation update was skipped.", this);
        }
    }

    private void SafeResetTrigger(string parameterName)
    {
        if (!CanUseAnimator() || string.IsNullOrWhiteSpace(parameterName)) return;
        int hash = Animator.StringToHash(parameterName);
        if (AnimatorParameterUtility3D.HasParameter(animator, hash, AnimatorControllerParameterType.Trigger))
            animator.ResetTrigger(hash);
    }

    private bool CanUseAnimator()
    {
        if (!useAnimator) return false;
        if (animator == null)
        {
            if (debugMode && !warnedMissingAnimator)
            {
                Debug.LogWarning("[DionaeaAnimatorBridge] Animator is not connected; gameplay will continue without animation.", this);
                warnedMissingAnimator = true;
            }
            return false;
        }
        if (!animator.isActiveAndEnabled) return false;
        if (animator.runtimeAnimatorController == null)
        {
            if (debugMode && !warnedMissingController)
            {
                Debug.LogWarning("[DionaeaAnimatorBridge] Runtime Animator Controller is not connected; gameplay will continue without animation.", this);
                warnedMissingController = true;
            }
            return false;
        }
        return true;
    }

    private void AutoFill()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        if (animator != null) animator.applyRootMotion = false;
    }
}
