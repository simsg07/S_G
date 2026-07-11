using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlatformerPlayer3D))]
public class PlayerAnimationController : MonoBehaviour
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int YVelocityHash = Animator.StringToHash("YVelocity");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int IsDeadHash = Animator.StringToHash("IsDead");

    [SerializeField] private Animator animator; // Visual-only Animator. It must not drive player movement or collision.
    [SerializeField] private PlatformerPlayer3D movement; // Source of grounded and movement-facing state from code.
    [SerializeField] private Rigidbody body; // Source of current velocity from code physics.
    [SerializeField] private float speedDampTime; // Optional smoothing time for the Speed animator parameter.
    [SerializeField] private float yVelocityDampTime; // Optional smoothing time for the YVelocity animator parameter.
    [SerializeField] private bool disableRootMotion = true; // Keeps Animator from moving the player transform.

    private bool hasSpeed;
    private bool hasYVelocity;
    private bool hasIsGrounded;
    private bool hasIsDead;

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

    private void LateUpdate()
    {
        Refresh();
    }

    private void CacheReferences()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        if (movement == null)
        {
            movement = GetComponent<PlatformerPlayer3D>();
        }

        if (body == null)
        {
            body = GetComponent<Rigidbody>();
        }
    }

    private void CacheParameters()
    {
        hasSpeed = AnimatorParameterUtility3D.HasParameter(animator, SpeedHash, AnimatorControllerParameterType.Float);
        hasYVelocity = AnimatorParameterUtility3D.HasParameter(animator, YVelocityHash, AnimatorControllerParameterType.Float);
        hasIsGrounded = AnimatorParameterUtility3D.HasParameter(animator, IsGroundedHash, AnimatorControllerParameterType.Bool);
        hasIsDead = AnimatorParameterUtility3D.HasParameter(animator, IsDeadHash, AnimatorControllerParameterType.Bool);
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
        if (animator == null || movement == null)
        {
            return;
        }

        Vector3 velocity = body != null ? body.linearVelocity : Vector3.zero;
        float speed = Mathf.Abs(velocity.x);
        float yVelocity = velocity.y;
        bool isDead = false;

        if (hasSpeed)
        {
            animator.SetFloat(SpeedHash, speed, Mathf.Max(0f, speedDampTime), Time.deltaTime);
        }

        if (hasYVelocity)
        {
            animator.SetFloat(YVelocityHash, yVelocity, Mathf.Max(0f, yVelocityDampTime), Time.deltaTime);
        }

        if (hasIsGrounded)
        {
            animator.SetBool(IsGroundedHash, movement.IsGrounded);
        }

        if (hasIsDead)
        {
            animator.SetBool(IsDeadHash, isDead);
        }
    }
}
