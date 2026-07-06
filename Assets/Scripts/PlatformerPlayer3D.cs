using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[ExecuteAlways]
public class PlatformerPlayer3D : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 6f; // 플레이어 좌우 이동 속도입니다.
    [SerializeField] private float jumpHeight = 3f; // 점프 높이입니다. 값을 올리면 더 높게 뜁니다.
    [SerializeField] private float gravityScale = 3f; // 플레이어에게 적용되는 중력 배율입니다. 값을 올리면 상승과 낙하가 빨라집니다.
    [SerializeField] private float fallGravityMultiplier = 1.35f; // 낙하 중 추가 중력 배율입니다. 값을 올리면 떨어질 때 더 빠르게 내려옵니다.
    [SerializeField] private float maxFallSpeed = 18f; // 최대 낙하 속도 제한입니다. 값을 올리면 더 빠르게 추락할 수 있습니다.
    [SerializeField] private float coyoteTime = 0.08f; // 발판에서 떨어진 직후에도 점프를 허용하는 시간입니다.
    [SerializeField] private float jumpBufferTime = 0.1f; // 착지 직전 점프 입력을 미리 저장해두는 시간입니다.
    [SerializeField] private float jumpCutMultiplier = 0.5f; // 점프키를 빨리 떼면 상승 속도를 줄이는 비율입니다.
    [SerializeField] private float groundCheckDistance = 0.08f; // 바닥 판정을 확인할 추가 거리입니다.
    [SerializeField] private float dropThroughDuration = 0.45f; // S+스페이스로 발판을 내려갈 때 충돌을 무시하는 최소 시간입니다.
    [SerializeField] private float passThroughClearance = 0.05f; // 내려가기 발판 충돌을 복구하기 전에 필요한 여유 거리입니다.
    [SerializeField] private float knockbackHorizontalSpeed = 8f; // 피격 넉백의 가로 밀림 속도입니다.
    [SerializeField] private float knockbackVerticalSpeed = 5f; // 피격 넉백의 위로 튀는 속도입니다.
    [SerializeField] private float knockbackDuration = 0.28f; // 넉백 상태가 유지되는 시간입니다.
    [SerializeField] private float dashSpeed = 14f; // 우클릭 대쉬 이동 속도입니다.
    [SerializeField] private float dashDuration = 0.14f; // 대쉬가 지속되는 시간입니다.
    [SerializeField] private float dashCooldown = 0.4f; // 다음 대쉬를 다시 쓸 수 있을 때까지의 대기 시간입니다.
    [SerializeField] private int maxAirJumps = 1; // 바닥 점프 이후 허용되는 추가 공중 점프 횟수입니다.
    [SerializeField] private int maxAirDashes = 1; // 착지 전 허용되는 공중 대쉬 횟수입니다.
    [SerializeField] private Vector3 colliderSize = new Vector3(0.8f, 1.2f, 1f); // 플레이어 충돌 박스와 몸 크기입니다.

    private readonly RaycastHit[] groundHits = new RaycastHit[12];
    private readonly List<IgnoredPlatform> ignoredPlatforms = new List<IgnoredPlatform>();

    private Rigidbody body;
    private BoxCollider playerCollider;
    private float horizontalInput;
    private float coyoteCounter;
    private float jumpBufferCounter;
    private float knockbackCounter;
    private float dashCounter;
    private float dashCooldownCounter;
    private float dashDirection = 1f;
    private float facingDirection = 1f;
    private int airJumpsUsed;
    private int airDashesUsed;
    private bool jumpReleased;
    private bool isGrounded;
    private Collider currentGround;

    public bool IsKnockedBack => knockbackCounter > 0f;
    public bool IsDashing => dashCounter > 0f;
    public bool IsGrounded => isGrounded;
    public float FacingDirection => facingDirection;
    public float HorizontalInput => horizontalInput;

    private void Awake()
    {
        ApplyDatabaseTuning();
        ConfigureComponents();
    }

    private void OnEnable()
    {
        ApplyDatabaseTuning();
        ConfigureComponents();
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            DisableGeneratedBoxVisual();
            return;
        }

        RestoreIgnoredPlatforms();
        ReadMovementInput();

        if (jumpBufferCounter > 0f)
        {
            jumpBufferCounter -= Time.deltaTime;
        }
    }

    private void FixedUpdate()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        UpdateKnockbackTimer();
        UpdateDashTimers();
        UpdatePassThroughPlatformCollisions();
        UpdateGroundedState();

        if (IsKnockedBack)
        {
            KeepKnockbackOnMovementPlane();
        }
        else if (IsDashing)
        {
            ApplyDashMovement();
        }
        else
        {
            TryConsumeBufferedJump();
            ApplyHorizontalMovement();
            ApplyJumpCut();
        }

        ApplyCustomGravity();
        LimitFallSpeed();
    }

    public void ApplyKnockback(Vector3 sourcePosition)
    {
        if (!Application.isPlaying || IsKnockedBack)
        {
            return;
        }

        if (body == null)
        {
            ConfigureComponents();
        }

        float direction = Mathf.Sign(transform.position.x - sourcePosition.x);
        if (Mathf.Abs(direction) < 0.01f)
        {
            direction = Mathf.Abs(horizontalInput) > 0.01f ? -Mathf.Sign(horizontalInput) : -1f;
        }

        Vector3 velocity = body.linearVelocity;
        velocity.x = direction * knockbackHorizontalSpeed;
        velocity.y = Mathf.Max(velocity.y, knockbackVerticalSpeed);
        velocity.z = 0f;
        body.linearVelocity = velocity;

        knockbackCounter = knockbackDuration;
        dashCounter = 0f;
        jumpBufferCounter = 0f;
        jumpReleased = false;
        coyoteCounter = 0f;
        isGrounded = false;
    }

    private void ConfigureComponents()
    {
        ApplyDatabaseTuning();

        body = GetComponent<Rigidbody>();
        if (body == null)
        {
            body = gameObject.AddComponent<Rigidbody>();
        }

        playerCollider = GetComponent<BoxCollider>();
        if (playerCollider == null)
        {
            playerCollider = gameObject.AddComponent<BoxCollider>();
        }

        body.useGravity = false;
        body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        playerCollider.size = Vector3.one;
        playerCollider.center = Vector3.zero;
        transform.localScale = colliderSize;

        DisableGeneratedBoxVisual();
    }

    private void ApplyDatabaseTuning()
    {
        UnitBalanceDatabase3D database = UnitBalanceDatabase3D.Load();
        if (database == null || database.PlayerMovement == null)
        {
            return;
        }

        PlayerMovementBalance3D tuning = database.PlayerMovement;
        moveSpeed = tuning.moveSpeed;
        jumpHeight = tuning.jumpHeight;
        gravityScale = tuning.gravityScale;
        fallGravityMultiplier = tuning.fallGravityMultiplier;
        maxFallSpeed = tuning.maxFallSpeed;
        coyoteTime = tuning.coyoteTime;
        jumpBufferTime = tuning.jumpBufferTime;
        jumpCutMultiplier = tuning.jumpCutMultiplier;
        groundCheckDistance = tuning.groundCheckDistance;
        dropThroughDuration = tuning.dropThroughDuration;
        passThroughClearance = tuning.passThroughClearance;
        knockbackHorizontalSpeed = tuning.knockbackHorizontalSpeed;
        knockbackVerticalSpeed = tuning.knockbackVerticalSpeed;
        knockbackDuration = tuning.knockbackDuration;
        dashSpeed = tuning.dashSpeed;
        dashDuration = tuning.dashDuration;
        dashCooldown = tuning.dashCooldown;
        maxAirJumps = tuning.maxAirJumps;
        maxAirDashes = tuning.maxAirDashes;
        colliderSize = tuning.colliderSize;
    }

    private void DisableGeneratedBoxVisual()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();

        if (meshFilter != null && meshFilter.sharedMesh != null && meshFilter.sharedMesh.name == "Generated Box Mesh")
        {
            meshFilter.sharedMesh = null;
        }

        if (meshRenderer != null && (meshRenderer.sharedMaterial == null || meshRenderer.sharedMaterial.name.StartsWith("Generated Player Material")))
        {
            meshRenderer.enabled = false;
        }
    }

    private void ReadMovementInput()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            horizontalInput = 0f;
            return;
        }

        horizontalInput = 0f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
        {
            horizontalInput -= 1f;
        }

        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
        {
            horizontalInput += 1f;
        }

        if (Mathf.Abs(horizontalInput) > 0.01f)
        {
            facingDirection = Mathf.Sign(horizontalInput);
        }

        bool isHoldingDown = keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed;

        if (keyboard.spaceKey.wasPressedThisFrame)
        {
            if (isHoldingDown && TryDropThroughCurrentPlatform())
            {
                jumpBufferCounter = 0f;
                return;
            }

            jumpBufferCounter = jumpBufferTime;
        }

        if (keyboard.spaceKey.wasReleasedThisFrame)
        {
            jumpReleased = true;
        }

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.rightButton.wasPressedThisFrame)
        {
            TryStartDash();
        }
    }

    private void UpdateGroundedState()
    {
        Bounds bounds = playerCollider.bounds;
        Vector3 halfExtents = new Vector3(bounds.extents.x * 0.9f, 0.03f, bounds.extents.z * 0.9f);

        currentGround = null;
        isGrounded = false;

        int hitCount = Physics.BoxCastNonAlloc(
            bounds.center,
            halfExtents,
            Vector3.down,
            groundHits,
            Quaternion.identity,
            bounds.extents.y + groundCheckDistance,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = groundHits[i];

            if (hit.collider == null ||
                hit.collider == playerCollider ||
                ShouldIgnorePlatformForGround(hit.collider) ||
                hit.normal.y < 0.5f)
            {
                continue;
            }

            currentGround = hit.collider;
            isGrounded = true;
            break;
        }

        if (isGrounded)
        {
            coyoteCounter = coyoteTime;
            airJumpsUsed = 0;
            airDashesUsed = 0;
            return;
        }

        coyoteCounter -= Time.fixedDeltaTime;
    }

    private void TryConsumeBufferedJump()
    {
        if (jumpBufferCounter <= 0f)
        {
            return;
        }

        bool canGroundJump = coyoteCounter > 0f;
        bool canAirJump = !isGrounded && !canGroundJump && airJumpsUsed < maxAirJumps;

        if (!canGroundJump && !canAirJump)
        {
            return;
        }

        Vector3 velocity = body.linearVelocity;
        float effectiveGravity = Mathf.Abs(Physics.gravity.y * gravityScale);

        if (effectiveGravity < 0.01f)
        {
            effectiveGravity = 9.81f * gravityScale;
        }

        velocity.y = Mathf.Sqrt(jumpHeight * 2f * effectiveGravity);
        body.linearVelocity = velocity;

        jumpBufferCounter = 0f;
        coyoteCounter = 0f;
        isGrounded = false;

        if (canAirJump)
        {
            airJumpsUsed++;
        }
    }

    private void ApplyHorizontalMovement()
    {
        Vector3 velocity = body.linearVelocity;
        velocity.x = horizontalInput * moveSpeed;
        velocity.z = 0f;
        body.linearVelocity = velocity;
    }

    private void ApplyJumpCut()
    {
        if (!jumpReleased)
        {
            return;
        }

        jumpReleased = false;

        if (body.linearVelocity.y <= 0f)
        {
            return;
        }

        Vector3 velocity = body.linearVelocity;
        velocity.y *= jumpCutMultiplier;
        body.linearVelocity = velocity;
    }

    private void ApplyCustomGravity()
    {
        if (IsDashing)
        {
            return;
        }

        Vector3 velocity = body.linearVelocity;

        if (isGrounded && velocity.y <= 0f)
        {
            velocity.y = -1f;
            body.linearVelocity = velocity;
            return;
        }

        float multiplier = velocity.y < -0.01f ? fallGravityMultiplier : 1f;
        velocity.y += Physics.gravity.y * gravityScale * multiplier * Time.fixedDeltaTime;
        body.linearVelocity = velocity;
    }

    private void LimitFallSpeed()
    {
        if (body.linearVelocity.y >= -maxFallSpeed)
        {
            return;
        }

        Vector3 velocity = body.linearVelocity;
        velocity.y = -maxFallSpeed;
        body.linearVelocity = velocity;
    }

    private bool TryDropThroughCurrentPlatform()
    {
        UpdateGroundedState();

        if (!isGrounded || currentGround == null || !CanDropThrough(currentGround))
        {
            return false;
        }

        Physics.IgnoreCollision(playerCollider, currentGround, true);
        ignoredPlatforms.Add(new IgnoredPlatform(currentGround, Time.time + dropThroughDuration));

        Vector3 velocity = body.linearVelocity;
        velocity.y = Mathf.Min(velocity.y, -4f);
        body.linearVelocity = velocity;

        currentGround = null;
        isGrounded = false;
        coyoteCounter = 0f;
        return true;
    }

    private void UpdatePassThroughPlatformCollisions()
    {
        foreach (PlatformSurface3D platform in PlatformSurface3D.ActiveSurfaces)
        {
            if (platform == null || !platform.CanDropThrough || platform.PlatformCollider == null)
            {
                continue;
            }

            Collider platformCollider = platform.PlatformCollider;
            Physics.IgnoreCollision(playerCollider, platformCollider, ShouldIgnorePassThroughPlatform(platformCollider));
        }
    }

    private void UpdateKnockbackTimer()
    {
        if (knockbackCounter <= 0f)
        {
            return;
        }

        knockbackCounter = Mathf.Max(0f, knockbackCounter - Time.fixedDeltaTime);
    }

    private void UpdateDashTimers()
    {
        if (dashCooldownCounter > 0f)
        {
            dashCooldownCounter = Mathf.Max(0f, dashCooldownCounter - Time.fixedDeltaTime);
        }

        if (dashCounter > 0f)
        {
            dashCounter = Mathf.Max(0f, dashCounter - Time.fixedDeltaTime);
        }
    }

    private void TryStartDash()
    {
        if (IsKnockedBack || IsDashing || dashCooldownCounter > 0f)
        {
            return;
        }

        bool isAirDash = !isGrounded;
        if (isAirDash && airDashesUsed >= maxAirDashes)
        {
            return;
        }

        dashDirection = Mathf.Abs(horizontalInput) > 0.01f ? Mathf.Sign(horizontalInput) : facingDirection;
        facingDirection = dashDirection;
        dashCounter = dashDuration;
        dashCooldownCounter = dashCooldown;

        Vector3 velocity = body.linearVelocity;
        velocity.x = dashDirection * dashSpeed;
        velocity.y = 0f;
        velocity.z = 0f;
        body.linearVelocity = velocity;

        if (isAirDash)
        {
            airDashesUsed++;
        }
    }

    private void ApplyDashMovement()
    {
        Vector3 velocity = body.linearVelocity;
        velocity.x = dashDirection * dashSpeed;
        velocity.y = 0f;
        velocity.z = 0f;
        body.linearVelocity = velocity;
    }

    private void KeepKnockbackOnMovementPlane()
    {
        Vector3 velocity = body.linearVelocity;
        velocity.z = 0f;
        body.linearVelocity = velocity;
    }

    private static bool CanDropThrough(Collider platformCollider)
    {
        PlatformSurface3D platform = platformCollider.GetComponent<PlatformSurface3D>();
        return platform != null && platform.CanDropThrough;
    }

    private bool IsTemporarilyIgnored(Collider platformCollider)
    {
        for (int i = 0; i < ignoredPlatforms.Count; i++)
        {
            if (ignoredPlatforms[i].Collider == platformCollider)
            {
                return true;
            }
        }

        return false;
    }

    private bool ShouldIgnorePlatformForGround(Collider platformCollider)
    {
        PlatformSurface3D platform = platformCollider.GetComponent<PlatformSurface3D>();
        return platform != null && platform.CanDropThrough && ShouldIgnorePassThroughPlatform(platformCollider);
    }

    private bool ShouldIgnorePassThroughPlatform(Collider platformCollider)
    {
        if (playerCollider == null || platformCollider == null)
        {
            return false;
        }

        if (IsTemporarilyIgnored(platformCollider))
        {
            return true;
        }

        Bounds playerBounds = playerCollider.bounds;
        Bounds platformBounds = platformCollider.bounds;
        bool isMovingUp = body != null && body.linearVelocity.y > 0.01f;
        bool isBelowPlatformTop = playerBounds.min.y < platformBounds.max.y - 0.02f;

        return isMovingUp || isBelowPlatformTop;
    }

    private bool IsPlayerClearOfIgnoredPlatform(Collider platformCollider)
    {
        Bounds playerBounds = playerCollider.bounds;
        Bounds platformBounds = platformCollider.bounds;
        bool fullyBelow = playerBounds.max.y < platformBounds.min.y - passThroughClearance;
        bool safelyAbove = playerBounds.min.y > platformBounds.max.y + passThroughClearance;

        return fullyBelow || safelyAbove;
    }

    private void RestoreIgnoredPlatforms()
    {
        for (int i = ignoredPlatforms.Count - 1; i >= 0; i--)
        {
            IgnoredPlatform ignoredPlatform = ignoredPlatforms[i];

            if (ignoredPlatform.Collider == null)
            {
                ignoredPlatforms.RemoveAt(i);
                continue;
            }

            if (Time.time < ignoredPlatform.RestoreTime || !IsPlayerClearOfIgnoredPlatform(ignoredPlatform.Collider))
            {
                continue;
            }

            ignoredPlatforms.RemoveAt(i);
        }
    }

    private struct IgnoredPlatform
    {
        public IgnoredPlatform(Collider collider, float restoreTime)
        {
            Collider = collider;
            RestoreTime = restoreTime;
        }

        public Collider Collider { get; }
        public float RestoreTime { get; }
    }
}
