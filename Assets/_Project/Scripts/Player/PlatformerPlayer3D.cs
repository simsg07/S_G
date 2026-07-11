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
    [SerializeField] private int maxAirJumps = 1; // 바닥 점프 이후 허용되는 추가 공중 점프 횟수입니다.
    [SerializeField] private Vector3 colliderSize = new Vector3(0.8f, 1.2f, 1f); // 플레이어 충돌 박스와 몸 크기입니다.
    [SerializeField] private float gameplayPlaneZ = TwoPointFiveDUtility3D.GameplayPlaneZ; // 2.5D 규칙상 플레이어가 고정될 Z축 위치입니다.
    [SerializeField] private bool useRobotLegJump = true; // 스페이스를 누르는 동안 로봇 다리가 늘어나는 점프 방식을 사용할지 정합니다.
    [SerializeField] private float maxLegExtension = 10f; // Space를 누르고 있을 때 플레이어가 상승할 수 있는 최대 다리 길이입니다.
    [SerializeField] private float legExtendSpeed = 4.8f; // 스페이스를 누르고 있을 때 다리가 늘어나는 속도입니다.
    [SerializeField] private float legRetractSpeed = 8.5f; // 스페이스를 떼었을 때 다리가 줄어드는 속도입니다.
    [SerializeField] private float legReleaseJumpHeight = 2f; // Space를 떼는 순간 현재 높이에서 추가로 뛰어오르는 목표 높이입니다.
    [SerializeField] private float ceilingCheckDistance = 0.08f; // 다리 상승 중 천장에 닿기 전에 멈추기 위한 여유 거리입니다.
    [SerializeField] private float legObstacleClearance = 0.04f; // 로봇 다리가 발판이나 박스에 닿기 전에 남길 여유 거리입니다.
    [SerializeField] private bool showRobotLegVisual = true; // 늘어난 다리를 간단한 회색 박스로 표시할지 정합니다.
    [SerializeField] private Color robotLegColor = new Color(0.55f, 0.6f, 0.62f, 1f); // 로봇 다리 표시 색상입니다.

    private readonly RaycastHit[] groundHits = new RaycastHit[12];
    private readonly RaycastHit[] ceilingHits = new RaycastHit[8];
    private readonly Collider[] legObstacleHits = new Collider[24];
    private readonly List<IgnoredPlatform> ignoredPlatforms = new List<IgnoredPlatform>();

    private Rigidbody body;
    private BoxCollider playerCollider;
    private CameraAbilitySystem3D cameraAbilities;
    private float horizontalInput;
    private float verticalLookInput;
    private float coyoteCounter;
    private float jumpBufferCounter;
    private float facingDirection = 1f;
    private int airJumpsUsed;
    private bool jumpReleased;
    private bool robotLegInputHeld;
    private bool robotLegExtending;
    private bool robotLegAutoExtending;
    private bool robotLegRetracting;
    private bool robotLegReleaseJumpUsed;
    private bool isGrounded;
    private Collider currentGround;
    private float currentLegExtension;
    private Transform leftLegVisual;
    private Transform rightLegVisual;
    private Material robotLegMaterial;

    public bool IsGrounded => isGrounded;
    public float FacingDirection => facingDirection;
    public float HorizontalInput => horizontalInput;
    public float VerticalLookInput => verticalLookInput;

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

        bool isAnchoredByRobotLegs = UpdateRobotLegJump();

        if (!useRobotLegJump)
        {
            TryConsumeBufferedJump();
        }

        ApplyHorizontalMovement();

        if (!useRobotLegJump)
        {
            ApplyJumpCut();
        }

        if (isAnchoredByRobotLegs)
        {
            KeepRobotLegAnchorVelocity();
        }

        if (!IsRobotLegAnchored())
        {
            ApplyCustomGravity();
        }

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
        ApplyRobotLegShape(currentLegExtension, currentLegExtension, RobotLegAnchorMode.Top);

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
        maxAirJumps = tuning.maxAirJumps;
        colliderSize = tuning.colliderSize;
        useRobotLegJump = tuning.useRobotLegJump;
        maxLegExtension = tuning.maxLegExtension;
        legExtendSpeed = tuning.legExtendSpeed;
        legRetractSpeed = tuning.legRetractSpeed;
        legReleaseJumpHeight = tuning.legReleaseJumpHeight;
        ceilingCheckDistance = tuning.ceilingCheckDistance;
        legObstacleClearance = tuning.legObstacleClearance;
        showRobotLegVisual = tuning.showRobotLegVisual;
        robotLegColor = tuning.robotLegColor;
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
        verticalLookInput = 0f;
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

        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
        {
            verticalLookInput += 1f;
        }

        bool isHoldingDown = keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed;
        if (isHoldingDown)
        {
            verticalLookInput -= 1f;
        }

        if (useRobotLegJump && robotLegExtending && robotLegInputHeld && robotLegAutoExtending && isHoldingDown)
        {
            robotLegAutoExtending = false;
        }

        if (keyboard.spaceKey.wasPressedThisFrame)
        {
            if (isHoldingDown && TryDropThroughCurrentPlatform())
            {
                jumpBufferCounter = 0f;
                return;
            }

            if (useRobotLegJump)
            {
                TryStartRobotLegJump(!isHoldingDown);
            }
            else
            {
                jumpBufferCounter = jumpBufferTime;
            }
        }

        if (keyboard.spaceKey.wasReleasedThisFrame)
        {
            if (useRobotLegJump)
            {
                BeginRobotLegRetract(true);
            }
            else
            {
                jumpReleased = true;
            }
        }

        if (useRobotLegJump && robotLegExtending && !keyboard.spaceKey.isPressed)
        {
            BeginRobotLegRetract(true);
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

    private void TryStartRobotLegJump(bool autoExtend)
    {
        if (!isGrounded || robotLegRetracting)
        {
            return;
        }

        robotLegInputHeld = true;
        robotLegExtending = true;
        robotLegAutoExtending = autoExtend;
        robotLegRetracting = false;
        robotLegReleaseJumpUsed = false;
        jumpBufferCounter = 0f;
        jumpReleased = false;

        Vector3 velocity = body.linearVelocity;
        velocity.y = 0f;
        velocity.z = 0f;
        body.linearVelocity = velocity;
    }

    private bool UpdateRobotLegJump()
    {
        if (!useRobotLegJump)
        {
            HideRobotLegVisual();
            return false;
        }

        if (robotLegExtending && robotLegInputHeld)
        {
            if (currentLegExtension > 0.001f && !HasRobotLegSupport())
            {
                BeginRobotLegRetract(false);
                return false;
            }

            float previousExtension = currentLegExtension;
            float maximumExtension = Mathf.Max(0f, maxLegExtension);
            if (robotLegAutoExtending && currentLegExtension >= maximumExtension - 0.001f)
            {
                robotLegAutoExtending = false;
            }

            float legControl = robotLegAutoExtending ? 1f : Mathf.Clamp(verticalLookInput, -1f, 1f);
            float legAdjustSpeed = legControl > 0f ? legExtendSpeed : legRetractSpeed;
            float desiredExtension = Mathf.Clamp(
                currentLegExtension + legControl * Mathf.Max(0.01f, legAdjustSpeed) * Time.fixedDeltaTime,
                0f,
                maximumExtension
            );

            currentLegExtension = legControl > 0f
                ? ClampRobotLegExtensionByObstacles(previousExtension, ClampRobotLegExtensionByCeiling(previousExtension, desiredExtension))
                : desiredExtension;

            ApplyRobotLegShape(previousExtension, currentLegExtension, RobotLegAnchorMode.Bottom);
            if (legControl > 0f && currentLegExtension < desiredExtension - 0.001f)
            {
                robotLegAutoExtending = false;
            }

            if (robotLegAutoExtending && currentLegExtension >= maximumExtension - 0.001f)
            {
                robotLegAutoExtending = false;
            }

            if (currentLegExtension <= 0.001f)
            {
                currentLegExtension = 0f;
                ApplyRobotLegShape(previousExtension, currentLegExtension, RobotLegAnchorMode.Top);
                return false;
            }

            return currentLegExtension > 0.001f && HasRobotLegSupport();
        }

        if (robotLegRetracting || currentLegExtension > 0f)
        {
            float previousExtension = currentLegExtension;
            currentLegExtension = Mathf.MoveTowards(
                currentLegExtension,
                0f,
                Mathf.Max(0.01f, legRetractSpeed) * Time.fixedDeltaTime
            );

            ApplyRobotLegShape(previousExtension, currentLegExtension, RobotLegAnchorMode.Top);

            if (currentLegExtension <= 0.001f)
            {
                currentLegExtension = 0f;
                robotLegExtending = false;
                robotLegRetracting = false;
                robotLegReleaseJumpUsed = false;
                ApplyRobotLegShape(previousExtension, currentLegExtension, RobotLegAnchorMode.Top);
            }
        }

        return false;
    }

    private void BeginRobotLegRetract(bool launchOnRelease)
    {
        robotLegInputHeld = false;

        if (!useRobotLegJump || currentLegExtension <= 0f)
        {
            robotLegExtending = false;
            robotLegAutoExtending = false;
            return;
        }

        robotLegExtending = false;
        robotLegAutoExtending = false;
        robotLegRetracting = true;
        isGrounded = false;
        currentGround = null;
        coyoteCounter = 0f;
        bool canLaunchFromLegSupport = launchOnRelease && HasRobotLegSupport();
        if (!canLaunchFromLegSupport)
        {
            robotLegReleaseJumpUsed = true;
        }

        if (canLaunchFromLegSupport && !robotLegReleaseJumpUsed)
        {
            ApplyRobotLegReleaseJump();
            robotLegReleaseJumpUsed = true;
        }
    }

    private void CancelRobotLegJumpState(bool resetShape)
    {
        float previousExtension = currentLegExtension;
        robotLegInputHeld = false;
        robotLegExtending = false;
        robotLegAutoExtending = false;
        robotLegRetracting = false;
        robotLegReleaseJumpUsed = false;
        jumpReleased = false;

        if (resetShape)
        {
            ApplyRobotLegShape(previousExtension, 0f, RobotLegAnchorMode.Top);
        }
        else
        {
            currentLegExtension = 0f;
            HideRobotLegVisual();
        }
    }

    private float ClampRobotLegExtensionByCeiling(float previousExtension, float desiredExtension)
    {
        float delta = desiredExtension - previousExtension;
        if (delta <= 0f || playerCollider == null)
        {
            return desiredExtension;
        }

        Bounds bounds = playerCollider.bounds;
        Vector3 halfExtents = new Vector3(
            bounds.extents.x * 0.92f,
            bounds.extents.y * 0.92f,
            bounds.extents.z * 0.92f
        );

        int hitCount = Physics.BoxCastNonAlloc(
            bounds.center,
            halfExtents,
            Vector3.up,
            ceilingHits,
            Quaternion.identity,
            delta + ceilingCheckDistance,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        float allowedDelta = delta;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = ceilingHits[i];
            if (hit.collider == null || hit.collider == playerCollider || hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            allowedDelta = Mathf.Min(allowedDelta, Mathf.Max(0f, hit.distance - ceilingCheckDistance));
        }

        return previousExtension + allowedDelta;
    }

    private float ClampRobotLegExtensionByObstacles(float previousExtension, float desiredExtension)
    {
        if (desiredExtension <= previousExtension + 0.001f || playerCollider == null)
        {
            return desiredExtension;
        }

        if (!RobotLegExtensionBlocked(previousExtension, desiredExtension))
        {
            return desiredExtension;
        }

        float low = Mathf.Max(0f, previousExtension);
        float high = desiredExtension;
        for (int i = 0; i < 8; i++)
        {
            float middle = (low + high) * 0.5f;
            if (RobotLegExtensionBlocked(previousExtension, middle))
            {
                high = middle;
            }
            else
            {
                low = middle;
            }
        }

        return low;
    }

    private bool RobotLegExtensionBlocked(float previousExtension, float testExtension)
    {
        float bottomClearance = Mathf.Max(0.01f, groundCheckDistance + legObstacleClearance);
        float checkHeight = testExtension - bottomClearance;
        if (checkHeight <= 0.01f)
        {
            return false;
        }

        float legWidth = Mathf.Max(0.06f, colliderSize.x * 0.18f);
        float legDepth = Mathf.Max(0.08f, colliderSize.z * 0.65f);
        float legOffset = Mathf.Max(0.08f, colliderSize.x * 0.22f);
        Vector3 bodyPosition = body != null ? body.position : transform.position;
        Vector3 simulatedBodyPosition = bodyPosition + Vector3.up * (testExtension - Mathf.Max(0f, previousExtension));
        Vector3 legCenter = simulatedBodyPosition + Vector3.down * (colliderSize.y * 0.5f + checkHeight * 0.5f);
        Vector3 halfExtents = new Vector3(
            legWidth * 0.5f + legObstacleClearance,
            checkHeight * 0.5f,
            legDepth * 0.5f + legObstacleClearance
        );

        return RobotLegColumnBlocked(legCenter + Vector3.left * legOffset, halfExtents)
            || RobotLegColumnBlocked(legCenter + Vector3.right * legOffset, halfExtents);
    }

    private bool RobotLegColumnBlocked(Vector3 center, Vector3 halfExtents)
    {
        int hitCount = Physics.OverlapBoxNonAlloc(
            center,
            halfExtents,
            legObstacleHits,
            Quaternion.identity,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = legObstacleHits[i];
            if (hit == null ||
                hit == playerCollider ||
                hit.transform.IsChildOf(transform) ||
                IsTemporarilyIgnored(hit))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private void ApplyRobotLegReleaseJump()
    {
        if (body == null)
        {
            return;
        }

        float effectiveGravity = Mathf.Abs(Physics.gravity.y * gravityScale);
        if (effectiveGravity < 0.01f)
        {
            effectiveGravity = 9.81f * Mathf.Max(0.01f, gravityScale);
        }

        Vector3 velocity = body.linearVelocity;
        velocity.y = Mathf.Sqrt(Mathf.Max(0f, legReleaseJumpHeight) * 2f * effectiveGravity);
        velocity.z = 0f;
        body.linearVelocity = velocity;
    }

    private void ApplyRobotLegShape(float previousExtension, float nextExtension, RobotLegAnchorMode anchorMode)
    {
        if (playerCollider == null)
        {
            return;
        }

        float clampedExtension = Mathf.Clamp(nextExtension, 0f, Mathf.Max(0f, maxLegExtension));
        currentLegExtension = clampedExtension;

        if (anchorMode == RobotLegAnchorMode.Bottom && body != null && Application.isPlaying)
        {
            float delta = clampedExtension - Mathf.Max(0f, previousExtension);
            if (Mathf.Abs(delta) > 0.0001f)
            {
                body.position += Vector3.up * delta;
            }
        }

        playerCollider.size = Vector3.one;
        playerCollider.center = Vector3.zero;
        UpdateRobotLegVisual(clampedExtension);
    }

    private void KeepRobotLegAnchorVelocity()
    {
        Vector3 velocity = body.linearVelocity;
        velocity.y = 0f;
        velocity.z = 0f;
        body.linearVelocity = velocity;
    }

    private bool IsRobotLegAnchored()
    {
        return useRobotLegJump
            && robotLegExtending
            && robotLegInputHeld
            && currentLegExtension > 0f
            && HasRobotLegSupport();
    }

    private bool HasRobotLegSupport()
    {
        if (currentLegExtension <= 0.001f)
        {
            return isGrounded;
        }

        Vector3 footCenter = transform.position + Vector3.down * (colliderSize.y * 0.5f + currentLegExtension);
        Vector3 halfExtents = new Vector3(
            Mathf.Max(0.04f, colliderSize.x * 0.35f),
            0.025f,
            Mathf.Max(0.04f, colliderSize.z * 0.35f)
        );

        int hitCount = Physics.BoxCastNonAlloc(
            footCenter + Vector3.up * 0.08f,
            halfExtents,
            Vector3.down,
            groundHits,
            Quaternion.identity,
            groundCheckDistance + 0.12f,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = groundHits[i];
            if (hit.collider == null ||
                hit.collider == playerCollider ||
                hit.collider.transform.IsChildOf(transform) ||
                ShouldIgnorePlatformForGround(hit.collider) ||
                hit.normal.y < 0.5f)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private void UpdateRobotLegVisual(float extension)
    {
        if (!showRobotLegVisual || extension <= 0.01f)
        {
            HideRobotLegVisual();
            return;
        }

        EnsureRobotLegVisuals();

        if (leftLegVisual == null || rightLegVisual == null)
        {
            return;
        }

        float legWidth = Mathf.Max(0.06f, colliderSize.x * 0.18f);
        float legDepth = Mathf.Max(0.08f, colliderSize.z * 0.65f);
        float legOffset = Mathf.Max(0.08f, colliderSize.x * 0.22f);
        Vector3 legSize = new Vector3(legWidth, extension, legDepth);
        Vector3 center = transform.position + Vector3.down * (colliderSize.y * 0.5f + extension * 0.5f);

        MonsterRuntime3D.ApplyWorldBoxVisual(transform, leftLegVisual, center + Vector3.left * legOffset, legSize, legDepth);
        MonsterRuntime3D.ApplyWorldBoxVisual(transform, rightLegVisual, center + Vector3.right * legOffset, legSize, legDepth);
        MonsterRuntime3D.SetVisualVisible(leftLegVisual, true);
        MonsterRuntime3D.SetVisualVisible(rightLegVisual, true);
    }

    private void EnsureRobotLegVisuals()
    {
        if (leftLegVisual == null)
        {
            leftLegVisual = MonsterRuntime3D.FindOrCreateBoxVisual(transform, "Robot Left Leg Extension", robotLegColor, ref robotLegMaterial);
        }

        if (rightLegVisual == null)
        {
            rightLegVisual = MonsterRuntime3D.FindOrCreateBoxVisual(transform, "Robot Right Leg Extension", robotLegColor, ref robotLegMaterial);
        }
    }

    private void HideRobotLegVisual()
    {
        MonsterRuntime3D.SetVisualVisible(leftLegVisual, false);
        MonsterRuntime3D.SetVisualVisible(rightLegVisual, false);
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
        CancelRobotLegJumpState(true);
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

    private enum RobotLegAnchorMode
    {
        Bottom,
        Top
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
