using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlatformerPlayer3D))]
public class PlayerAnimationController : MonoBehaviour
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int YVelocityHash = Animator.StringToHash("YVelocity");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int IsDeadHash = Animator.StringToHash("IsDead");

    [Header("Animator Bridge - References")]
    [Tooltip("Visual 자식에 있는 Animator입니다. 이동/충돌은 Root의 Player 스크립트가 담당합니다.")]
    [SerializeField] private Animator animator;
    [Tooltip("읽기용 참조입니다. 비워두면 같은 오브젝트의 PlatformerPlayer3D를 자동으로 찾습니다.")]
    [SerializeField] private PlatformerPlayer3D movement;
    [Tooltip("읽기용 참조입니다. 속도 값을 Animator 파라미터로 전달할 때 사용합니다.")]
    [SerializeField] private Rigidbody body;

    [Header("Animator Bridge - Designer Settings")]
    [Tooltip("Speed Animator 파라미터가 부드럽게 변하는 시간입니다.")]
    [SerializeField] private float speedDampTime;
    [Tooltip("YVelocity Animator 파라미터가 부드럽게 변하는 시간입니다.")]
    [SerializeField] private float yVelocityDampTime;
    [Tooltip("Animator Root Motion이 Player 위치를 직접 움직이지 못하게 합니다.")]
    [SerializeField] private bool disableRootMotion = true;

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
