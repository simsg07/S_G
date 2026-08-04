using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
public sealed class Physics2DPrototypePlayer : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 6f;
    [SerializeField, Min(0f)] private float acceleration = 55f;
    [SerializeField, Min(0f)] private float deceleration = 70f;

    [Header("Jump")]
    [SerializeField, Min(0f)] private float jumpForce = 13.3f;
    [SerializeField, Min(0f)] private float gravityScale = 3f;
    [SerializeField, Min(1f)] private float fallGravityMultiplier = 1.35f;
    [SerializeField, Min(0.01f)] private float maxFallSpeed = 18f;
    [SerializeField, Min(0f)] private float groundCheckDistance = 0.08f;
    [SerializeField] private LayerMask groundLayer = 1 << 9;
    [SerializeField, Min(0f)] private float coyoteTime = 0.08f;
    [SerializeField, Min(0f)] private float jumpBufferTime = 0.1f;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer visual;

    private readonly RaycastHit2D[] groundHits = new RaycastHit2D[8];
    private Rigidbody2D body;
    private CapsuleCollider2D bodyCollider;
    private float moveInput;
    private float coyoteCounter;
    private float jumpBufferCounter;
    private bool isGrounded;

    public bool IsGrounded => isGrounded;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<CapsuleCollider2D>();
        ApplyBodySettings();
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        acceleration = Mathf.Max(0f, acceleration);
        deceleration = Mathf.Max(0f, deceleration);
        jumpForce = Mathf.Max(0f, jumpForce);
        gravityScale = Mathf.Max(0f, gravityScale);
        fallGravityMultiplier = Mathf.Max(1f, fallGravityMultiplier);
        groundCheckDistance = Mathf.Max(0f, groundCheckDistance);
        coyoteTime = Mathf.Max(0f, coyoteTime);
        jumpBufferTime = Mathf.Max(0f, jumpBufferTime);

        if (Application.isPlaying)
        {
            ApplyBodySettings();
        }
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Gamepad gamepad = Gamepad.current;

        moveInput = 0f;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) moveInput -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) moveInput += 1f;
        }

        if (gamepad != null && Mathf.Abs(moveInput) < 0.01f)
        {
            moveInput = gamepad.leftStick.ReadValue().x;
        }

        bool jumpPressed = (keyboard != null && keyboard.spaceKey.wasPressedThisFrame) ||
                           (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);
        if (jumpPressed)
        {
            jumpBufferCounter = jumpBufferTime;
        }
        else
        {
            jumpBufferCounter = Mathf.Max(0f, jumpBufferCounter - Time.deltaTime);
        }

        if (visual != null && Mathf.Abs(moveInput) > 0.01f)
        {
            visual.flipX = moveInput < 0f;
        }
    }

    private void FixedUpdate()
    {
        UpdateGroundedState();
        ApplyHorizontalMovement();
        TryJump();
        ApplyFallGravity();
    }

    private void ApplyBodySettings()
    {
        if (body == null) body = GetComponent<Rigidbody2D>();
        if (body == null) return;

        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = gravityScale;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    private void UpdateGroundedState()
    {
        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = groundLayer,
            useTriggers = false
        };

        int hitCount = bodyCollider.Cast(Vector2.down, filter, groundHits, groundCheckDistance);
        isGrounded = false;
        for (int i = 0; i < hitCount; i++)
        {
            if (groundHits[i].collider != null && groundHits[i].normal.y >= 0.65f)
            {
                isGrounded = true;
                break;
            }
        }

        coyoteCounter = isGrounded ? coyoteTime : Mathf.Max(0f, coyoteCounter - Time.fixedDeltaTime);
    }

    private void ApplyHorizontalMovement()
    {
        float targetSpeed = moveInput * moveSpeed;
        float rate = Mathf.Abs(targetSpeed) > 0.01f ? acceleration : deceleration;
        Vector2 velocity = body.linearVelocity;
        velocity.x = Mathf.MoveTowards(velocity.x, targetSpeed, rate * Time.fixedDeltaTime);
        body.linearVelocity = velocity;
    }

    private void TryJump()
    {
        if (jumpBufferCounter <= 0f || coyoteCounter <= 0f)
        {
            return;
        }

        Vector2 velocity = body.linearVelocity;
        velocity.y = jumpForce;
        body.linearVelocity = velocity;
        jumpBufferCounter = 0f;
        coyoteCounter = 0f;
        isGrounded = false;
    }

    private void ApplyFallGravity()
    {
        if (body.linearVelocity.y >= 0f)
        {
            return;
        }

        Vector2 velocity = body.linearVelocity;
        velocity.y += Physics2D.gravity.y * gravityScale * (fallGravityMultiplier - 1f) * Time.fixedDeltaTime;
        velocity.y = Mathf.Max(velocity.y, -maxFallSpeed);
        body.linearVelocity = velocity;
    }
}
