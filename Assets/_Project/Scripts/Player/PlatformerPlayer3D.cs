using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[ExecuteAlways]
public class PlatformerPlayer3D : MonoBehaviour
{
    [Header("Movement - Designer Settings")]
    [SerializeField] private float moveSpeed = 6f; // 플레이어 좌우 이동 속도입니다.
    [Header("Jump")]
    [SerializeField, InspectorName("Jump Force"), Tooltip("점프 시 Rigidbody에 적용할 위쪽 속도입니다.")] private float jumpForce = 13.3f;
    [SerializeField, Tooltip("바닥으로 인정할 3D 물리 레이어입니다.")] private LayerMask groundLayer = 1 << 9;
    [SerializeField, InspectorName("더블 점프 사용"), Tooltip("켜면 지상 점프 후 공중에서 한 번 더 점프할 수 있습니다.")] private bool enableDoubleJump;
    [SerializeField, InspectorName("Separate Double Jump Force")] private bool useSeparateDoubleJumpForce;
    [SerializeField, InspectorName("Double Jump Force")] private float doubleJumpForce = 13.3f;
    [Header("Jump Feel")]
    [SerializeField] private float gravityScale = 3f; // 플레이어에게 적용되는 중력 배율입니다. 값을 올리면 상승과 낙하가 빨라집니다.
    [SerializeField] private float fallGravityMultiplier = 1.35f; // 낙하 중 추가 중력 배율입니다. 값을 올리면 떨어질 때 더 빠르게 내려옵니다.
    [SerializeField] private float maxFallSpeed = 18f; // 최대 낙하 속도 제한입니다. 값을 올리면 더 빠르게 추락할 수 있습니다.
    [SerializeField] private float coyoteTime = 0.08f; // 발판에서 떨어진 직후에도 점프를 허용하는 시간입니다.
    [SerializeField] private float jumpBufferTime = 0.1f; // 착지 직전 점프 입력을 미리 저장해두는 시간입니다.
    [SerializeField] private float jumpCutMultiplier = 0.5f; // 점프키를 빨리 떼면 상승 속도를 줄이는 비율입니다.
    [SerializeField] private float groundCheckDistance = 0.08f; // 바닥 판정을 확인할 추가 거리입니다.
    [SerializeField] private float dropThroughDuration = 0.45f; // S+스페이스로 발판을 내려갈 때 충돌을 무시하는 최소 시간입니다.
    [SerializeField] private float passThroughClearance = 0.05f; // 내려가기 발판 충돌을 복구하기 전에 필요한 여유 거리입니다.

    [Header("Collision - Advanced")]
    [Tooltip("플레이어 충돌 박스와 몸 크기입니다. 값 변경 시 바닥/벽 충돌 느낌이 크게 달라집니다.")]
    [SerializeField] private Vector3 colliderSize = new Vector3(0.8f, 1.2f, 1f); // 플레이어 충돌 박스와 몸 크기입니다.
    [Tooltip("2.5D 규칙상 플레이어가 고정될 Z축 위치입니다. 보통 수정하지 않습니다.")]
    [SerializeField] private float gameplayPlaneZ = TwoPointFiveDUtility3D.GameplayPlaneZ; // 2.5D 규칙상 플레이어가 고정될 Z축 위치입니다.

    private readonly RaycastHit[] groundHits = new RaycastHit[12];
    private readonly List<IgnoredPlatform> ignoredPlatforms = new List<IgnoredPlatform>();

    private Rigidbody body;
    private BoxCollider playerCollider;
    private CameraAbilitySystem3D cameraAbilities;
    private float horizontalInput;
    private float verticalLookInput;
    private float coyoteCounter;
    private float jumpBufferCounter;
    private float facingDirection = 1f;
    private int jumpsUsed;
    private bool jumpReleased;
    private bool isGrounded;
    private bool controlLocked;
    private float externalMoveSpeedMultiplier = 1f;
    private Collider currentGround;
    private float groundingSuppressedUntil;

    public bool IsGrounded => isGrounded;
    public float FacingDirection => facingDirection;
    public float HorizontalInput => horizontalInput;
    public float VerticalLookInput => verticalLookInput;

    public void ResetJumpStateAfterTeleport()
    {
        jumpsUsed = 0;
        jumpBufferCounter = 0f;
        jumpReleased = false;
        coyoteCounter = 0f;
        groundingSuppressedUntil = 0f;
        isGrounded = false;
        currentGround = null;
    }

    public void SetControlLocked(bool locked)
    {
        controlLocked = locked;
        if (!Application.isPlaying || body == null)
        {
            return;
        }

        if (locked)
        {
            ClearBufferedInput();
            StopPlayerVelocity();
        }
    }

    public void SetExternalMoveSpeedMultiplier(float multiplier)
    {
        externalMoveSpeedMultiplier = Mathf.Max(0f, multiplier);
        if (Application.isPlaying && body != null && Mathf.Approximately(externalMoveSpeedMultiplier, 0f))
        {
            ClearBufferedInput();
            StopPlayerVelocity();
        }
    }

    private void Awake()
    {
        ApplyDatabaseTuning();
        ConfigureComponents();
        EnsureCameraAbilitySystem();
        EnsurePlayerInteractionSystem();
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
        if (controlLocked)
        {
            ClearBufferedInput();
            return;
        }

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

        UpdatePassThroughPlatformCollisions();
        UpdateGroundedState();

        if (controlLocked)
        {
            ClearBufferedInput();
            StopPlayerVelocity();
            LockToGameplayPlane();
            return;
        }

        TryConsumeBufferedJump();

        ApplyHorizontalMovement();

        ApplyJumpCut();
        ApplyCustomGravity();

        LimitFallSpeed();
        LockToGameplayPlane();
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
        TwoPointFiveDUtility3D.ConfigureRigidbodyForSideView(body, gameplayPlaneZ);
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        playerCollider.size = Vector3.one;
        playerCollider.center = Vector3.zero;
        transform.localScale = colliderSize;
        DisableGeneratedBoxVisual();
    }

    private void EnsureCameraAbilitySystem()
    {
        cameraAbilities = GetComponent<CameraAbilitySystem3D>();
        if (!Application.isPlaying || cameraAbilities != null)
        {
            return;
        }

        cameraAbilities = gameObject.AddComponent<CameraAbilitySystem3D>();
    }

    private void EnsurePlayerInteractionSystem()
    {
        if (!Application.isPlaying || GetComponent<PlayerInteraction3D>() != null)
        {
            return;
        }

        gameObject.AddComponent<PlayerInteraction3D>();
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
        dropThroughDuration = tuning.dropThroughDuration;
        passThroughClearance = tuning.passThroughClearance;
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
        Gamepad gamepad = Gamepad.current;

        horizontalInput = 0f;
        verticalLookInput = 0f;
        if (keyboard != null && (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed))
        {
            horizontalInput -= 1f;
        }

        if (keyboard != null && (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed))
        {
            horizontalInput += 1f;
        }

        if (gamepad != null)
        {
            Vector2 stick = gamepad.leftStick.ReadValue();
            if (Mathf.Abs(horizontalInput) < 0.01f) horizontalInput = stick.x;
            verticalLookInput = stick.y;
        }

        if (Mathf.Abs(horizontalInput) > 0.01f)
        {
            facingDirection = Mathf.Sign(horizontalInput);
        }

        if (keyboard != null && (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed))
        {
            verticalLookInput += 1f;
        }

        bool isHoldingDown = keyboard != null && (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed);
        if (isHoldingDown)
        {
            verticalLookInput -= 1f;
        }

        bool jumpPressed = (keyboard != null && keyboard.spaceKey.wasPressedThisFrame) ||
            (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);
        if (jumpPressed)
        {
            if (isHoldingDown && TryDropThroughCurrentPlatform())
            {
                jumpBufferCounter = 0f;
                return;
            }

            jumpBufferCounter = jumpBufferTime;
        }

        bool jumpReleasedThisFrame = (keyboard != null && keyboard.spaceKey.wasReleasedThisFrame) ||
            (gamepad != null && gamepad.buttonSouth.wasReleasedThisFrame);
        if (jumpReleasedThisFrame)
        {
            jumpReleased = true;
        }

    }

    private void ClearBufferedInput()
    {
        horizontalInput = 0f;
        verticalLookInput = 0f;
        jumpBufferCounter = 0f;
        jumpReleased = false;
    }

    private void StopPlayerVelocity()
    {
        if (body == null)
        {
            return;
        }

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
    }

    private void UpdateGroundedState()
    {
        if (Time.time < groundingSuppressedUntil)
        {
            currentGround = null;
            isGrounded = false;
            coyoteCounter = 0f;
            return;
        }

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
            groundLayer.value == 0 ? 1 << LayerMask.NameToLayer("Ground") : groundLayer.value,
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
            jumpsUsed = 0;
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
        bool canAirJump = enableDoubleJump && !isGrounded && !canGroundJump && jumpsUsed == 1;

        if (!canGroundJump && !canAirJump)
        {
            return;
        }

        Vector3 velocity = body.linearVelocity;
        velocity.y = canAirJump && useSeparateDoubleJumpForce ? doubleJumpForce : jumpForce;
        body.linearVelocity = velocity;

        jumpBufferCounter = 0f;
        coyoteCounter = 0f;
        isGrounded = false;

        groundingSuppressedUntil = Time.time + 0.1f;
        jumpsUsed = canAirJump ? 2 : 1;
    }

    private void ApplyHorizontalMovement()
    {
        Vector3 velocity = body.linearVelocity;
        velocity.x = horizontalInput * moveSpeed * externalMoveSpeedMultiplier;
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

    private void LockToGameplayPlane()
    {
        if (body != null)
        {
            TwoPointFiveDUtility3D.ClampRigidbodyToPlane(body, gameplayPlaneZ);
            return;
        }

        TwoPointFiveDUtility3D.ClampTransformToPlane(transform, gameplayPlaneZ);
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
