using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlatformerPlayer3D))]
[RequireComponent(typeof(CameraInterventionLimiter))]
public class CameraAbilitySystem3D : MonoBehaviour
{
    private enum CameraModeState
    {
        Inactive,
        Active,
        ForcedExitBlocked
    }

    private const CameraAbilityFlags ActiveCameraAbilityMask = CameraAbilityFlags.Shutter | CameraAbilityFlags.Focus;

    [Header("Unlocked")]
    [SerializeField] private CameraAbilityFlags unlockedAbilities = ActiveCameraAbilityMask; // 처음부터 사용할 수 있는 카메라 기능 목록입니다.
    [SerializeField] private bool loadProgressOnStart = true; // 저장된 진행도에서 해금된 카메라 기능을 불러올지 정합니다.

    [Header("Input")]
    [SerializeField] private bool usePrimaryFireForCameraAbility = true; // 카메라 모드에서 마우스 좌클릭으로 선택된 카메라 기능을 실행할지 정합니다.
    [SerializeField] private bool useSecondaryFireForCameraMode = true; // 마우스 우클릭으로 카메라 모드에 들어갈지 정합니다.
    [FormerlySerializedAs("directWorldSwitchKey")]
    [SerializeField] private Key worldSwitchKey = Key.Q; // Global world switch key. This does not consume camera interventions.
    [SerializeField] private Key lightToggleKey = Key.R; // Simple camera light on/off key.
    // Legacy serialized fields. Keep these for save/prefab compatibility, but camera-slot input is no longer used.
    [SerializeField, HideInInspector] private Key previousCameraSlotKey = Key.None;
    [SerializeField, HideInInspector] private Key nextCameraSlotKey = Key.E;
    [SerializeField, HideInInspector] private CameraAbilityId selectedCameraAbility = CameraAbilityId.Shutter;

    [Header("Camera Mode")]
    [SerializeField] private Key cameraModeKey = Key.None; // 보조 키보드 카메라 모드 키입니다. None이면 마우스 우클릭만 사용합니다.
    [SerializeField, HideInInspector] private bool holdCameraMode = true; // Legacy serialized value. Right mouse camera input is always Hold.
    [SerializeField] private bool startInCameraMode = false; // 테스트용으로 시작하자마자 카메라 모드를 켤지 정합니다.
    [SerializeField, HideInInspector] private float cameraModeSlowDuration = 2f; // Legacy serialized value; camera slow motion now lasts for the whole camera mode.
    [SerializeField, Range(0.01f, 1f)] private float cameraModeTimeScale = 0.25f;

    [Header("Mouse Camera Frame")]
    [SerializeField] private bool useMouseFrameTargeting = true; // 마우스 위치의 카메라 프레임 안에 들어온 대상을 조준할지 정합니다.
    [SerializeField] private bool showCameraFrame = true; // 카메라 모드 중 조준 프레임 UI를 보여줄지 정합니다.
    [SerializeField] private bool hideSystemCursor = true; // 카메라 프레임을 표시하는 동안 기본 마우스 커서를 숨길지 정합니다.
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f); // 카메라 프레임 크기 계산 기준 해상도입니다.
    [SerializeField] private Vector2 shutterFrameReferenceSize = new Vector2(720f, 405f); // 기준 해상도에서 셔터 프레임이 차지하는 픽셀 크기입니다.
    [SerializeField] private float frameBorderThickness = 3f; // 카메라 프레임 선 두께입니다.
    [SerializeField] private Color frameColor = new Color(0.86f, 0.96f, 1f, 0.92f); // 카메라 프레임 기본 색상입니다.
    [SerializeField] private Color frameAccentColor = new Color(0.42f, 0.78f, 1f, 0.48f); // 카메라 프레임 내부 보조선 색상입니다.
    [SerializeField] private Color frameRecordColor = new Color(1f, 0.12f, 0.12f, 0.95f); // 촬영점과 기록 표시용 강조 색상입니다.
    [SerializeField] private Color frameCooldownColor = new Color(1f, 0.7f, 0.25f, 0.8f); // 카메라 기능 쿨타임 중 표시 색상입니다.
    [SerializeField, Min(0f)] private float cameraDragSensitivity = 1f;

    [Header("Targeting")]
    [SerializeField, InspectorName("Shutter Target Layer Mask")] private LayerMask targetMask = ~0; // 카메라 기능이 감지할 대상 레이어 범위입니다.
    [SerializeField] private float aimHeightOffset = 0.35f; // 방향 조준 시 플레이어 위치에서 위로 올리는 조준 시작점입니다.
    [SerializeField, InspectorName("Target Search Range")] private float shutterRange = 7f; // 셔터가 대상을 찾을 수 있는 최대 거리입니다.
    [SerializeField] private Vector3 shutterBoxSize = new Vector3(1.5f, 1.5f, 1f); // 방향 조준 셔터 판정 박스 크기입니다.
    [SerializeField] private float relayRange = 3f; // 릴레이 기능이 대상을 찾을 수 있는 최대 거리입니다.
    [SerializeField] private Vector3 relayBoxSize = new Vector3(1.6f, 1.6f, 1f); // 방향 조준 릴레이 판정 박스 크기입니다.
    [SerializeField] private bool allowUntaggedShutterTargets = true; // 전용 태그가 없는 Rigidbody도 셔터 대상으로 허용할지 정합니다.
    [SerializeField, Tooltip("셔터 시야를 차단할 Ground/Wall 레이어입니다.")] private LayerMask shutterLineOfSightMask = (1 << 9) | (1 << 10);

    [Header("Shutter Debug")]
    [SerializeField] private bool showShutterDebug;
    [SerializeField] private bool logShutterEvents;
    [SerializeField] private bool debugCameraMode;

    [Header("셔터 시간 정지")]
    [SerializeField, Tooltip("촬영한 오브젝트의 물리, 애니메이션과 AI가 멈추는 시간입니다.")] private float shutterFreezeDuration = 1.2f;
    [SerializeField, InspectorName("Global Cooldown"), Tooltip("셔터를 다시 사용할 수 있기까지의 전체 쿨타임입니다.")] private float shutterCooldown = 1f;
    [SerializeField, Tooltip("이미 정지 중인 대상을 다시 촬영했을 때 남은 정지 시간을 갱신합니다.")] private bool refreshFreezeWhileFrozen;

    [Header("마크")]
    [SerializeField, Tooltip("촬영 대상에 마크가 표시되고 후속 능력 대상으로 사용할 수 있는 시간입니다.")] private float shutterMarkDuration = 5f;
    [SerializeField, Tooltip("마크가 만료되거나 소비된 뒤 같은 대상에 새 마크를 부여할 때까지의 시간입니다.")] private float shutterRemarkCooldown = 7f;
    [SerializeField, Tooltip("이미 마크된 대상을 재촬영할 때 마크 유지시간을 갱신합니다.")] private bool refreshMarkOnShutter;
    [SerializeField, Tooltip("마크가 남아 있는 대상에도 시간 정지를 다시 적용할 수 있습니다.")] private bool allowFreezeWhileMarked = true;
    [SerializeField, Tooltip("재마크 대기 중인 대상에도 시간 정지를 다시 적용할 수 있습니다.")] private bool allowFreezeDuringRemarkCooldown = true;

    [Header("기타 능력 시간")]
    [SerializeField] private float focusCooldown = 0.2f; // 초점 기능 재사용 대기 시간입니다.
    [SerializeField] private float relayCooldown = 0.35f; // 릴레이 기능 재사용 대기 시간입니다.

    [Header("Camera Light")]
    [SerializeField] private float flashLightIntensity = 7.5f; // 라이트가 켜졌을 때 빛의 세기입니다.
    [SerializeField] private float flashLightRange = 6.5f; // 라이트가 비추는 거리입니다.
    [SerializeField] private Color flashLightColor = new Color(0.78f, 0.95f, 1f, 1f); // 라이트 색상입니다.

    [Header("Camera Helpers")]
    [SerializeField] private bool useCameraRangeWorldSwitching = true; // If true, Focus switches only WorldSwitchable objects inside the camera view.
    [SerializeField] private CameraLightFollower cameraLightFollower; // Clamps and moves the camera light inside the camera view.
    [SerializeField] private CameraWorldSwitcher cameraWorldSwitcher; // Switches only tagged WorldSwitchable objects inside the camera view.
    [SerializeField] private CameraInterventionLimiter interventionLimiter; // Limits successful camera freeze and camera-range world switch uses.
    [SerializeField, Min(0.02f)] private float cameraTargetRefreshInterval = 0.05f;

    private readonly Collider[] targetHits = new Collider[64];
    private readonly RaycastHit[] targetCastHits = new RaycastHit[40];
    private readonly Dictionary<Component, ShutterMarkRecord> shutterMarks = new Dictionary<Component, ShutterMarkRecord>();
    private readonly List<Component> expiredMarkTargets = new List<Component>();
    private readonly List<Graphic> frameTintGraphics = new List<Graphic>();

    private PlatformerPlayer3D movement;
    private Camera targetCamera;
    private Canvas frameCanvas;
    private RectTransform frameRoot;
    private RectTransform reticleRoot;
    private Texture2D ringTexture;
    private Texture2D diskTexture;
    private Light flashLight;
    private bool cursorHiddenByFrame;
    private CameraModeState cameraModeState = CameraModeState.Inactive;
    private bool cameraModeSlowActive;
    private bool cameraLightOn;
    private bool cameraTargetScanActive;
    private bool ownsGeneratedFlashLight;
    private float shutterCooldownTimer;
    private float focusCooldownTimer;
    private float relayCooldownTimer;
    private float storedTimeScale = 1f;
    private float storedFixedDeltaTime = 0.02f;
    private float appliedCameraTimeScale = 0.25f;
    private float appliedCameraFixedDeltaTime = 0.005f;
    private Vector3 lastShutterTargetPosition;
    private bool lastShutterTargetBlocked;
    private float pendingPrimaryFireTime;
    private bool primaryFirePending;
    private Vector2 cameraDragScreenPosition;
    private Vector2 cameraDragDelta;
    private float nextCameraTargetRefreshTime;

    private const float DoubleClickInterval = 0.3f;

    public static event Action<CameraAbilityFlags> AbilitiesChanged;
    public static event Action<bool> CameraSlowMotionChanged;

    public static CameraAbilityFlags KnownAbilities { get; private set; } = CameraAbilityFlags.None;
    public CameraAbilityFlags UnlockedAbilities => unlockedAbilities;
    public bool IsCameraModeActive => cameraModeState == CameraModeState.Active;
    private void Awake()
    {
        movement = GetComponent<PlatformerPlayer3D>();
        targetCamera = Camera.main;
        EnsureCameraInterventionLimiter();
        SetupCameraFrame();
        SetupFlashLight();
        SetupCameraHelpers();
        ClampUnlockedAbilities();
        PublishAbilityState();
    }

    private void Start()
    {
        if (loadProgressOnStart)
        {
            unlockedAbilities |= GameProgressSave3D.GetUnlockedAbilities();
            ClampUnlockedAbilities();
            PublishAbilityState();
        }

        if (startInCameraMode)
        {
            EnterCameraMode("Start In Camera Mode");
        }
    }

    private void OnEnable()
    {
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        PlayerDamageReceiver.PlayerDied -= HandlePlayerDied;
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        PlayerDamageReceiver.PlayerDied += HandlePlayerDied;
        PublishAbilityState();
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        PlayerDamageReceiver.PlayerDied -= HandlePlayerDied;
        ForceExitCameraMode("Component disabled", false);
        RestoreSystemCursor();
        TurnOffCameraLight();
        ClearCameraWorldTargetStates();
        ClearAllShutterMarks();
    }

    private void OnDestroy()
    {
        ForceExitCameraMode("Object destroyed", false);
        RestoreSystemCursor();
        TurnOffCameraLight();
        if (ownsGeneratedFlashLight && flashLight != null)
        {
            DestroyGenerated(flashLight.gameObject);
            flashLight = null;
            ownsGeneratedFlashLight = false;
        }
        DestroyGenerated(ringTexture);
        DestroyGenerated(diskTexture);
    }

    private void Update()
    {
        TickCooldowns();
        TickShutterMarks();
        UpdateFlashLight();

        if (!Application.isPlaying)
        {
            UpdateCameraFrame();
            return;
        }

        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        DetectPauseTimeOverride();
        UpdateCameraModeInput(mouse);
        TickPendingPrimaryCameraClick();
        UpdateCameraWorldTargetStates();

        UpdateCameraFrame();
        if (WasPressed(keyboard, worldSwitchKey))
        {
            TryUseGlobalWorldSwitch();
            return;
        }

        if (WasPressed(keyboard, lightToggleKey))
        {
            ToggleCameraLight();
        }

        if (cameraModeState != CameraModeState.Active)
        {
            return;
        }

        if (usePrimaryFireForCameraAbility && mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            HandlePrimaryCameraClick();
        }
    }

    private void UpdateCameraModeInput(Mouse mouse)
    {
        if (!useSecondaryFireForCameraMode || mouse == null)
        {
            ExitCameraMode("Secondary camera input unavailable");
            return;
        }

        bool rightPressed = mouse.rightButton.isPressed;
        bool rightDown = mouse.rightButton.wasPressedThisFrame;
        bool rightUp = mouse.rightButton.wasReleasedThisFrame;

        if (cameraModeState == CameraModeState.ForcedExitBlocked)
        {
            if (rightUp || !rightPressed)
            {
                cameraModeState = CameraModeState.Inactive;
                LogCameraMode("Right Mouse Up - forced-exit block cleared");
            }
            return;
        }

        if (rightDown)
        {
            LogCameraMode("Right Mouse Down");
            EnterCameraMode("Right Mouse Down");
        }

        if (cameraModeState != CameraModeState.Active)
        {
            return;
        }

        if (rightUp || !rightPressed)
        {
            LogCameraMode("Right Mouse Up");
            ExitCameraMode("Right Mouse Up");
            return;
        }

        UpdateCameraDrag(mouse);
    }

    private void EnterCameraMode(string reason)
    {
        if (cameraModeState != CameraModeState.Inactive || !Application.isPlaying || Time.timeScale <= 0f)
        {
            return;
        }

        cameraModeState = CameraModeState.Active;
        InitializeCameraDrag();
        ApplyCameraModeSlowMotion();
        LogCameraTransition("Camera Enter", reason);
    }

    private void ExitCameraMode(string reason)
    {
        if (cameraModeState != CameraModeState.Active)
        {
            return;
        }

        cameraModeState = CameraModeState.Inactive;
        ResetCameraDrag();
        primaryFirePending = false;
        RestoreCameraModeSlowMotion();
        LogCameraTransition("Camera Exit", reason);
    }

    private void ApplyCameraModeSlowMotion()
    {
        if (!Application.isPlaying || cameraModeSlowActive || cameraModeTimeScale <= 0f || Time.timeScale <= 0f)
        {
            return;
        }

        storedTimeScale = Time.timeScale;
        storedFixedDeltaTime = Time.fixedDeltaTime;

        appliedCameraTimeScale = Mathf.Clamp(cameraModeTimeScale, 0.01f, 1f);
        float normalizedFixedDelta = storedTimeScale > 0.001f
            ? storedFixedDeltaTime / storedTimeScale
            : storedFixedDeltaTime;
        appliedCameraFixedDeltaTime = normalizedFixedDelta * appliedCameraTimeScale;

        Time.timeScale = appliedCameraTimeScale;
        Time.fixedDeltaTime = appliedCameraFixedDeltaTime;
        cameraModeSlowActive = true;
        CameraSlowMotionChanged?.Invoke(true);
    }

    private void RestoreCameraModeSlowMotion()
    {
        if (!cameraModeSlowActive)
        {
            return;
        }

        bool stillOwnsTimeScale = Mathf.Approximately(Time.timeScale, appliedCameraTimeScale);
        bool stillOwnsFixedDelta = Mathf.Approximately(Time.fixedDeltaTime, appliedCameraFixedDeltaTime);
        cameraModeSlowActive = false;
        if (stillOwnsTimeScale)
        {
            Time.timeScale = storedTimeScale;
        }

        if (stillOwnsFixedDelta)
        {
            Time.fixedDeltaTime = storedFixedDeltaTime;
        }

        CameraSlowMotionChanged?.Invoke(false);
    }

    private void DetectPauseTimeOverride()
    {
        if (!cameraModeSlowActive || Time.timeScale > 0f)
        {
            return;
        }

        if (debugCameraMode)
        {
            Debug.Log($"[CameraMode] External pause detected: timeScale={Time.timeScale}, fixedDeltaTime={Time.fixedDeltaTime}", this);
        }
        ForceExitCameraMode("External Time.timeScale = 0", true);
    }

    public void ForceExitCameraMode()
    {
        ForceExitCameraMode("External request", true);
    }

    private void ForceExitCameraMode(string reason, bool blockUntilRightMouseRelease)
    {
        bool wasActive = cameraModeState == CameraModeState.Active || cameraModeSlowActive;
        if (cameraModeState == CameraModeState.Active)
        {
            ExitCameraMode(reason);
        }
        else
        {
            primaryFirePending = false;
            ResetCameraDrag();
            RestoreCameraModeSlowMotion();
        }

        if (blockUntilRightMouseRelease)
        {
            cameraModeState = CameraModeState.ForcedExitBlocked;
        }

        if (wasActive)
        {
            LogCameraTransition("Force Exit", reason);
        }
    }

    public static void ForceExitAllCameraModes()
    {
        CameraAbilitySystem3D[] systems = FindObjectsByType<CameraAbilitySystem3D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < systems.Length; i++)
        {
            if (systems[i] != null)
            {
                systems[i].ForceExitCameraMode("Global external request", true);
            }
        }
    }

    private void HandlePlayerDied(PlayerDamageReceiver receiver)
    {
        if (receiver != null && receiver.transform.root == transform.root)
        {
            ForceExitCameraMode("Player died", true);
        }
    }

    private void HandleActiveSceneChanged(Scene previousScene, Scene nextScene)
    {
        ForceExitCameraMode("Active scene changed", true);
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            ForceExitCameraMode("Application paused", true);
        }
    }

    private void OnApplicationQuit()
    {
        ForceExitCameraMode("Application quit", false);
    }

    private void InitializeCameraDrag()
    {
        Mouse mouse = Mouse.current;
        cameraDragScreenPosition = mouse != null
            ? mouse.position.ReadValue()
            : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        cameraDragDelta = Vector2.zero;
    }

    private void UpdateCameraDrag(Mouse mouse)
    {
        if (mouse == null)
        {
            return;
        }

        float unscaledFrameFactor = Mathf.Clamp(Time.unscaledDeltaTime * 60f, 0f, 3f);
        cameraDragDelta = mouse.delta.ReadValue() * Mathf.Max(0f, cameraDragSensitivity) * unscaledFrameFactor;
        cameraDragScreenPosition += cameraDragDelta;
        cameraDragScreenPosition.x = Mathf.Clamp(cameraDragScreenPosition.x, 0f, Screen.width);
        cameraDragScreenPosition.y = Mathf.Clamp(cameraDragScreenPosition.y, 0f, Screen.height);
    }

    private void ResetCameraDrag()
    {
        cameraDragDelta = Vector2.zero;
    }

    private void LogCameraMode(string message)
    {
        if (debugCameraMode)
        {
            Debug.Log($"[CameraMode] {message}", this);
        }
    }

    private void LogCameraTransition(string transition, string reason)
    {
        if (debugCameraMode)
        {
            Debug.Log($"[CameraMode] {transition} ({reason})", this);
        }
    }

    private void HandlePrimaryCameraClick()
    {
        float clickTime = Time.unscaledTime;
        if (primaryFirePending && clickTime - pendingPrimaryFireTime <= DoubleClickInterval)
        {
            primaryFirePending = false;
            TryUseFocus();
            return;
        }

        pendingPrimaryFireTime = clickTime;
        primaryFirePending = true;
    }

    private void TickPendingPrimaryCameraClick()
    {
        if (!primaryFirePending || Time.unscaledTime - pendingPrimaryFireTime <= DoubleClickInterval)
        {
            return;
        }

        primaryFirePending = false;
        TryUseShutter(); // Capture and Shutter are the same single-click action.
    }

    public bool IsUnlocked(CameraAbilityId ability)
    {
        CameraAbilityFlags flag = ToFlag(ability);
        return flag != CameraAbilityFlags.None
            && (ActiveCameraAbilityMask & flag) != 0
            && (unlockedAbilities & flag) != 0;
    }

    public bool UnlockAbility(CameraAbilityId ability)
    {
        CameraAbilityFlags flag = ToFlag(ability);
        if (flag == CameraAbilityFlags.None || (ActiveCameraAbilityMask & flag) == 0)
        {
            return false;
        }

        if ((unlockedAbilities & flag) != 0)
        {
            return false;
        }

        unlockedAbilities |= flag;
        PublishAbilityState();
        GameProgressSave3D.RecordAbilityUnlocked(ability);
        return true;
    }

    public static bool IsKnown(CameraAbilityFlags requiredAbilities)
    {
        return requiredAbilities == CameraAbilityFlags.None || (KnownAbilities & requiredAbilities) == requiredAbilities;
    }

    public static CameraAbilityFlags ToFlag(CameraAbilityId ability)
    {
        switch (ability)
        {
            case CameraAbilityId.Shutter:
                return CameraAbilityFlags.Shutter;
            case CameraAbilityId.Focus:
                return CameraAbilityFlags.Focus;
            case CameraAbilityId.Flash:
                return CameraAbilityFlags.Flash;
            case CameraAbilityId.Relay:
                return CameraAbilityFlags.Relay;
            default:
                return CameraAbilityFlags.None;
        }
    }

    private void TryUseShutter()
    {
        if (!IsUnlocked(CameraAbilityId.Shutter) || shutterCooldownTimer > 0f)
        {
            LogShutter(!IsUnlocked(CameraAbilityId.Shutter) ? "사용 불가: 미해금" : "사용 불가: 쿨다운");
            return;
        }

        if (!TryFindShutterTarget(out IShutterFreezable3D target, out Component targetComponent))
        {
            LogShutter("유효한 촬영 대상이 없습니다.");
            return;
        }

        if (target is ShutterTarget3D shutterTarget)
        {
            if (!shutterTarget.CanReceiveShutter())
            {
                LogShutter("대상별 쿨다운 또는 대상 상태 때문에 사용할 수 없습니다.");
                return;
            }

            shutterTarget.ApplyShutter();
            StartShutterCooldown();
            LogShutter($"공통 셔터 대상 처리 성공: {shutterTarget.name}");
            return;
        }

        if (!CanUseCameraIntervention())
        {
            return;
        }

        bool isMarked = IsMarked(targetComponent);
        bool isInRemarkCooldown = IsInRemarkCooldown(targetComponent);
        bool isFrozen = target is IShutterFreezeState3D freezeState && freezeState.IsShutterFrozen;

        if ((isMarked && !allowFreezeWhileMarked) || (isInRemarkCooldown && !allowFreezeDuringRemarkCooldown))
        {
            LogShutter("마크 설정에 의해 시간 정지 재사용이 제한되었습니다.");
            return;
        }

        if (isFrozen && !refreshFreezeWhileFrozen)
        {
            LogShutter("대상이 아직 정지 중이므로 정지 시간을 갱신하지 않았습니다.");
            return;
        }

        if (!ConsumeCameraIntervention("Freeze object"))
        {
            LogShutter("사용 불가: 카메라 개입 횟수 부족");
            return;
        }

        if (!target.ApplyShutterFreeze(shutterFreezeDuration, this))
        {
            RestoreCameraInterventions(1);
            LogShutter("대상이 시간 정지를 거부해 개입 횟수를 복구했습니다.");
            return;
        }

        StartShutterCooldown();
        if ((!isMarked && !isInRemarkCooldown) || (isMarked && refreshMarkOnShutter))
        {
            MarkTarget(targetComponent, target);
        }

        LogShutter($"촬영 성공: {targetComponent.name}, Mark={(isMarked ? "유지" : isInRemarkCooldown ? "재마크 대기" : "생성")}");
    }

    private void StartShutterCooldown()
    {
        shutterCooldownTimer = Mathf.Max(0.01f, shutterCooldown);
    }

    private void TryUseGlobalWorldSwitch()
    {
        if (!WorldManager.TrySwitchWorld())
        {
            WorldSystem3D.EnsureInstance().ToggleWorld();
        }
    }

    private void TryUseFocus()
    {
        if (!IsUnlocked(CameraAbilityId.Focus) || focusCooldownTimer > 0f)
        {
            return;
        }

        if (!useCameraRangeWorldSwitching || !CanUseCameraIntervention())
        {
            return;
        }

        if (TryUseCameraWorldSwitcher())
        {
            if (ConsumeCameraIntervention("Camera-range world switch"))
            {
                focusCooldownTimer = focusCooldown;
            }
        }
    }

    private void ToggleCameraLight()
    {
        if (flashLight == null)
        {
            SetupFlashLight();
        }

        CameraLightFollower follower = ResolveCameraLightFollower();
        if (follower == null)
        {
            return;
        }

        cameraLightOn = follower.ToggleLight();
        if (flashLight != null)
        {
            flashLight.intensity = cameraLightOn ? flashLightIntensity : 0f;
            flashLight.range = flashLightRange;
            flashLight.color = flashLightColor;
        }
    }

    private void TryUseRelay()
    {
        if (!IsUnlocked(CameraAbilityId.Relay) || relayCooldownTimer > 0f)
        {
            return;
        }

        if (!TryFindRelayTarget(out IRelayTransferable3D target))
        {
            relayCooldownTimer = relayCooldown;
            return;
        }

        ResearchWorldId targetWorld = WorldSystem3D.GetOpposite(WorldSystem3D.ActiveWorld);
        if (target.TryRelayToWorld(targetWorld, this))
        {
            relayCooldownTimer = relayCooldown;
        }
    }

    private bool TryFindShutterTarget(out IShutterFreezable3D target, out Component targetComponent)
    {
        target = null;
        targetComponent = null;

        Collider hit = useMouseFrameTargeting
            ? FindBestScreenFramedCollider(shutterRange, ResolveShutterTarget)
            : FindBestDirectionalCollider(shutterRange, shutterBoxSize, ResolveShutterTarget, true);

        if (hit == null)
        {
            return false;
        }

        if (!CameraObjectTag3D.AllowsCameraInteraction(hit) || !CameraObjectTag3D.AllowsCameraFreeze(hit))
        {
            return false;
        }

        target = ResolveShutterTarget(hit);
        targetComponent = ResolveTargetComponent(target, hit);
        if (target == null || targetComponent == null)
        {
            return false;
        }

        if (!allowUntaggedShutterTargets
            && !CameraTagUtility3D.HasAnyTag(targetComponent, CameraTagUtility3D.TargetTag, CameraTagUtility3D.RelayTargetTag, CameraTagUtility3D.CameraFreezableTag))
        {
            return false;
        }

        return true;
    }

    private bool TryFindRelayTarget(out IRelayTransferable3D target)
    {
        target = null;
        Collider hit = useMouseFrameTargeting
            ? FindBestScreenFramedCollider(relayRange, ResolveRelayTarget)
            : FindBestDirectionalCollider(relayRange, relayBoxSize, ResolveRelayTarget, false);

        if (hit == null)
        {
            return false;
        }

        target = ResolveRelayTarget(hit);
        return target != null;
    }

    private Collider FindBestDirectionalCollider<T>(float range, Vector3 boxSize, Func<Collider, T> resolver, bool requireLineOfSight) where T : class
    {
        Vector3 direction = GetAimDirection();
        Vector3 origin = transform.position + Vector3.up * aimHeightOffset;
        Vector3 halfExtents = new Vector3(
            Mathf.Max(0.05f, boxSize.x * 0.5f),
            Mathf.Max(0.05f, boxSize.y * 0.5f),
            Mathf.Max(0.05f, boxSize.z * 0.5f)
        );

        int hitCount = Physics.BoxCastNonAlloc(
            origin,
            halfExtents,
            direction,
            targetCastHits,
            Quaternion.identity,
            Mathf.Max(0.1f, range),
            targetMask,
            QueryTriggerInteraction.Collide
        );

        Collider bestHit = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = targetCastHits[i].collider;
            if (hit == null || hit.transform.IsChildOf(transform) || resolver(hit) == null)
            {
                continue;
            }

            Camera camera = GetTargetCamera();
            Vector3 sightOrigin = camera != null ? camera.transform.position : origin;
            if (requireLineOfSight && IsShutterLineOfSightBlocked(sightOrigin, hit))
            {
                continue;
            }

            float distance = Vector3.SqrMagnitude(hit.bounds.center - origin);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestHit = hit;
            }
        }

        return bestHit;
    }

    private Collider FindBestScreenFramedCollider<T>(float range, Func<Collider, T> resolver) where T : class
    {
        Camera camera = GetTargetCamera();
        if (camera == null)
        {
            return null;
        }

        Rect frameRect = GetMouseFrameRect();
        Vector2 frameCenter = frameRect.center;
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, Mathf.Max(0.1f, range), targetHits, targetMask, QueryTriggerInteraction.Collide);

        Collider bestHit = null;
        float bestScore = float.MaxValue;
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = targetHits[i];
            if (hit == null || hit.transform.IsChildOf(transform) || resolver(hit) == null)
            {
                continue;
            }

            if (!BoundsIntersectsFrame(hit.bounds, frameRect, camera))
            {
                continue;
            }

            if (IsShutterLineOfSightBlocked(camera.transform.position, hit))
            {
                continue;
            }

            Vector3 screenPoint = camera.WorldToScreenPoint(hit.bounds.center);
            float screenScore = Vector2.SqrMagnitude(new Vector2(screenPoint.x, screenPoint.y) - frameCenter);
            float distanceScore = Vector3.SqrMagnitude(hit.bounds.center - transform.position) * 0.05f;
            float score = screenScore + distanceScore;
            if (score < bestScore)
            {
                bestScore = score;
                bestHit = hit;
            }
        }

        return bestHit;
    }

    private bool IsShutterLineOfSightBlocked(Vector3 origin, Collider targetCollider)
    {
        Vector3 targetPoint = targetCollider.bounds.center;
        lastShutterTargetPosition = targetPoint;
        Vector3 direction = targetPoint - origin;
        float distance = direction.magnitude;
        if (distance <= 0.001f || shutterLineOfSightMask.value == 0)
        {
            lastShutterTargetBlocked = false;
            return false;
        }

        if (!Physics.Raycast(origin, direction / distance, out RaycastHit hit, distance, shutterLineOfSightMask, QueryTriggerInteraction.Ignore))
        {
            lastShutterTargetBlocked = false;
            return false;
        }

        lastShutterTargetBlocked = hit.collider != targetCollider && !hit.collider.transform.IsChildOf(targetCollider.transform);
        return lastShutterTargetBlocked;
    }

    private void LogShutter(string message)
    {
        if (logShutterEvents)
        {
            Debug.Log($"[Shutter] {message}", this);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showShutterDebug)
        {
            return;
        }

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, shutterRange);
        if (lastShutterTargetPosition != Vector3.zero)
        {
            Gizmos.color = lastShutterTargetBlocked ? Color.red : Color.green;
            Camera camera = GetTargetCamera();
            Gizmos.DrawLine(camera != null ? camera.transform.position : transform.position, lastShutterTargetPosition);
            Gizmos.DrawWireSphere(lastShutterTargetPosition, 0.15f);
        }
    }

    private IShutterFreezable3D ResolveShutterTarget(Collider hit)
    {
        if (!CameraObjectTag3D.AllowsCameraInteraction(hit) || !CameraObjectTag3D.AllowsCameraFreeze(hit))
        {
            return null;
        }

        IShutterFreezable3D freezable = ResolveInterface<IShutterFreezable3D>(hit);
        if (freezable != null)
        {
            if (freezable is Component freezableComponent)
            {
                CameraObjectTag3D existingTag = freezableComponent.GetComponent<CameraObjectTag3D>();
                if (existingTag == null)
                {
                    existingTag = freezableComponent.gameObject.AddComponent<CameraObjectTag3D>();
                }

                existingTag.MarkAsAutoCameraTarget();
            }

            return freezable;
        }

        Rigidbody targetBody = hit.GetComponentInParent<Rigidbody>();
        if (targetBody == null || targetBody.transform.IsChildOf(transform))
        {
            return null;
        }

        ShutterFreezable3D generatedFreezable = targetBody.GetComponent<ShutterFreezable3D>();
        if (generatedFreezable == null)
        {
            generatedFreezable = targetBody.gameObject.AddComponent<ShutterFreezable3D>();
        }

        CameraObjectTag3D objectTag = targetBody.GetComponent<CameraObjectTag3D>();
        if (objectTag == null)
        {
            objectTag = targetBody.gameObject.AddComponent<CameraObjectTag3D>();
        }

        objectTag.MarkAsAutoCameraTarget();
        return generatedFreezable;
    }

    private IRelayTransferable3D ResolveRelayTarget(Collider hit)
    {
        IRelayTransferable3D relayTarget = ResolveInterface<IRelayTransferable3D>(hit);
        if (relayTarget != null)
        {
            return relayTarget;
        }

        if (!HasTagInParents(hit, CameraTagUtility3D.RelayTargetTag))
        {
            return null;
        }

        WorldVariant3D variant = hit.GetComponentInParent<WorldVariant3D>();
        if (variant == null)
        {
            return null;
        }

        RelayTransferable3D generatedRelay = variant.GetComponent<RelayTransferable3D>();
        if (generatedRelay == null)
        {
            generatedRelay = variant.gameObject.AddComponent<RelayTransferable3D>();
        }

        return generatedRelay;
    }

    private bool TryRelayMarkedTarget(Component targetComponent)
    {
        IRelayTransferable3D relayTarget = ResolveRelayTargetFromComponent(targetComponent);
        if (relayTarget == null)
        {
            return false;
        }

        ResearchWorldId targetWorld = WorldSystem3D.GetOpposite(WorldSystem3D.ActiveWorld);
        return relayTarget.TryRelayToWorld(targetWorld, this);
    }

    private IRelayTransferable3D ResolveRelayTargetFromComponent(Component component)
    {
        if (component == null)
        {
            return null;
        }

        MonoBehaviour[] behaviours = component.GetComponentsInParent<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IRelayTransferable3D relayTarget)
            {
                return relayTarget;
            }
        }

        if (!HasTagInParents(component, CameraTagUtility3D.RelayTargetTag))
        {
            return null;
        }

        WorldVariant3D variant = component.GetComponentInParent<WorldVariant3D>();
        if (variant == null)
        {
            return null;
        }

        RelayTransferable3D generatedRelay = variant.GetComponent<RelayTransferable3D>();
        if (generatedRelay == null)
        {
            generatedRelay = variant.gameObject.AddComponent<RelayTransferable3D>();
        }

        return generatedRelay;
    }

    private T ResolveInterface<T>(Collider hit) where T : class
    {
        if (hit == null)
        {
            return null;
        }

        MonoBehaviour[] behaviours = hit.GetComponentsInParent<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is T target)
            {
                return target;
            }
        }

        return null;
    }

    private Component ResolveTargetComponent<T>(T target, Collider hit) where T : class
    {
        if (target is Component component)
        {
            return component;
        }

        return hit != null ? hit.GetComponentInParent<MonoBehaviour>() : null;
    }

    private void MarkTarget(Component targetComponent, IShutterFreezable3D target)
    {
        if (targetComponent == null || target == null)
        {
            return;
        }

        float markEnd = Time.time + Mathf.Max(0.1f, shutterMarkDuration);
        float cooldownEnd = markEnd + Mathf.Max(0f, shutterRemarkCooldown);
        shutterMarks[targetComponent] = new ShutterMarkRecord(target, markEnd, cooldownEnd);

        CameraMarkState3D markState = targetComponent.GetComponent<CameraMarkState3D>();
        if (markState == null)
        {
            markState = targetComponent.gameObject.AddComponent<CameraMarkState3D>();
        }

        markState.SetMarkWindow(markEnd, cooldownEnd);
    }

    private void StartRemarkCooldown(Component targetComponent)
    {
        if (targetComponent == null)
        {
            return;
        }

        float cooldownEnd = Time.time + Mathf.Max(0f, shutterRemarkCooldown);
        if (shutterMarks.TryGetValue(targetComponent, out ShutterMarkRecord record))
        {
            shutterMarks[targetComponent] = new ShutterMarkRecord(record.Target, 0f, cooldownEnd);
        }

        CameraMarkState3D markState = targetComponent.GetComponent<CameraMarkState3D>();
        if (markState != null)
        {
            markState.SetMarkWindow(0f, cooldownEnd);
        }
    }

    private bool IsMarked(Component targetComponent)
    {
        return targetComponent != null
            && shutterMarks.TryGetValue(targetComponent, out ShutterMarkRecord record)
            && Time.time < record.MarkEndTime;
    }

    private bool IsInRemarkCooldown(Component targetComponent)
    {
        return targetComponent != null
            && shutterMarks.TryGetValue(targetComponent, out ShutterMarkRecord record)
            && Time.time >= record.MarkEndTime
            && Time.time < record.CooldownEndTime;
    }

    private void TickShutterMarks()
    {
        expiredMarkTargets.Clear();
        foreach (KeyValuePair<Component, ShutterMarkRecord> pair in shutterMarks)
        {
            if (pair.Key == null || !pair.Key.gameObject.activeInHierarchy || Time.time >= pair.Value.CooldownEndTime)
            {
                expiredMarkTargets.Add(pair.Key);
            }
        }

        for (int i = 0; i < expiredMarkTargets.Count; i++)
        {
            Component target = expiredMarkTargets[i];
            if (target != null)
            {
                CameraMarkState3D markState = target.GetComponent<CameraMarkState3D>();
                if (markState != null)
                {
                    markState.ClearMark();
                }
            }

            shutterMarks.Remove(target);
        }
    }

    private void ClearAllShutterMarks()
    {
        foreach (KeyValuePair<Component, ShutterMarkRecord> pair in shutterMarks)
        {
            if (pair.Key == null)
            {
                continue;
            }

            CameraMarkState3D markState = pair.Key.GetComponent<CameraMarkState3D>();
            if (markState != null)
            {
                markState.ClearMark();
            }
        }

        shutterMarks.Clear();
        expiredMarkTargets.Clear();
    }

    private void SetupCameraFrame()
    {
        if (!showCameraFrame || frameCanvas != null)
        {
            return;
        }

        Canvas[] existingCanvases = GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < existingCanvases.Length; i++)
        {
            Canvas candidate = existingCanvases[i];
            if (candidate != null && candidate.name == "Camera Ability Frame")
            {
                BindExistingCameraFrame(candidate);
                return;
            }
        }

        GameObject canvasObject = new GameObject("Camera Ability Frame", typeof(Canvas));
        canvasObject.transform.SetParent(transform, false);

        frameCanvas = canvasObject.GetComponent<Canvas>();
        frameCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        frameCanvas.sortingOrder = 490;

        GameObject frameObject = new GameObject("Shutter Frame", typeof(RectTransform));
        frameObject.transform.SetParent(canvasObject.transform, false);
        frameRoot = frameObject.GetComponent<RectTransform>();
        frameRoot.anchorMin = Vector2.zero;
        frameRoot.anchorMax = Vector2.zero;
        frameRoot.pivot = new Vector2(0.5f, 0.5f);

        frameTintGraphics.Clear();
        CreateCameraCursorVisual(frameRoot);
    }

    private void BindExistingCameraFrame(Canvas existingCanvas)
    {
        frameCanvas = existingCanvas;
        frameCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        frameCanvas.sortingOrder = 490;

        frameTintGraphics.Clear();
        RectTransform[] rects = frameCanvas.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rects.Length; i++)
        {
            RectTransform rect = rects[i];
            if (rect == null)
            {
                continue;
            }

            if (rect.name == "Shutter Frame") frameRoot = rect;
            else if (rect.name == "Cursor Reticle") reticleRoot = rect;

            Graphic graphic = rect.GetComponent<Graphic>();
            if (graphic != null && rect.name != "Capture Dot" && rect.name != "Capture Dot Halo")
            {
                frameTintGraphics.Add(graphic);
            }

            RawImage rawImage = graphic as RawImage;
            if (rawImage == null)
            {
                continue;
            }

            rawImage.texture = rect.name == "Capture Dot" ? GetDiskTexture() : GetRingTexture();
        }

        if (frameRoot == null)
        {
            Debug.LogWarning("[CameraAbilitySystem3D] Existing camera canvas has no Shutter Frame; rebuilding runtime frame.", this);
            GameObject frameObject = new GameObject("Shutter Frame", typeof(RectTransform));
            frameObject.transform.SetParent(frameCanvas.transform, false);
            frameRoot = frameObject.GetComponent<RectTransform>();
            frameRoot.anchorMin = Vector2.zero;
            frameRoot.anchorMax = Vector2.zero;
            frameRoot.pivot = new Vector2(0.5f, 0.5f);
            frameTintGraphics.Clear();
            CreateCameraCursorVisual(frameRoot);
        }

        frameCanvas.enabled = false;
    }

    private void CreateCameraCursorVisual(RectTransform parent)
    {
        float thick = Mathf.Max(1f, frameBorderThickness);
        CreateFrameLine(parent, "Top Rail", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, thick * 1.8f), frameColor);
        CreateFrameLine(parent, "Bottom Rail", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, thick * 1.8f), frameColor);
        CreateFrameLine(parent, "Left Rail", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(thick * 1.8f, 0f), frameColor);
        CreateFrameLine(parent, "Right Rail", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(thick * 1.8f, 0f), frameColor);

        CreateFrameLine(parent, "Top Inner Rail", new Vector2(0.09f, 1f), new Vector2(0.91f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(0f, thick), frameAccentColor);
        CreateFrameLine(parent, "Bottom Inner Rail", new Vector2(0.09f, 0f), new Vector2(0.91f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(0f, thick), frameAccentColor);
        CreateFrameLine(parent, "Top Scanline A", new Vector2(0.22f, 1f), new Vector2(0.78f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(0f, 1f), frameAccentColor);
        CreateFrameLine(parent, "Bottom Scanline A", new Vector2(0.22f, 0f), new Vector2(0.78f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(0f, 1f), frameAccentColor);

        CreateFrameLine(parent, "Center Left Trace", new Vector2(0.04f, 0.5f), new Vector2(0.36f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 1f), frameAccentColor);
        CreateFrameLine(parent, "Center Right Trace", new Vector2(0.64f, 0.5f), new Vector2(0.96f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 1f), frameAccentColor);
        CreateFrameLine(parent, "Left Interior Tick", new Vector2(0.06f, 0.32f), new Vector2(0.28f, 0.32f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 1f), frameAccentColor);
        CreateFrameLine(parent, "Right Interior Tick", new Vector2(0.72f, 0.32f), new Vector2(0.94f, 0.32f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(0f, 1f), frameAccentColor);

        CreateFrameLine(parent, "Top Left Slash", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(44f, -24f), new Vector2(118f, thick * 1.5f), frameColor, -38f);
        CreateFrameLine(parent, "Top Right Slash", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-44f, -24f), new Vector2(118f, thick * 1.5f), frameColor, 38f);
        CreateFrameLine(parent, "Bottom Left Slash", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), new Vector2(44f, 24f), new Vector2(118f, thick * 1.5f), frameColor, 38f);
        CreateFrameLine(parent, "Bottom Right Slash", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-44f, 24f), new Vector2(118f, thick * 1.5f), frameColor, -38f);

        CreateFrameText(parent, "RSEQ Label", "RSEQ", new Vector2(0.5f, 0f), new Vector2(0f, 21f), new Vector2(126f, 34f), 22, frameColor, TextAnchor.MiddleCenter);
        CreateFrameText(parent, "SUB Label", "SUB", new Vector2(0.78f, 0.12f), new Vector2(0f, 0f), new Vector2(86f, 30f), 18, frameColor, TextAnchor.MiddleCenter);

        GameObject reticleObject = new GameObject("Cursor Reticle", typeof(RectTransform));
        reticleObject.transform.SetParent(parent, false);
        reticleRoot = reticleObject.GetComponent<RectTransform>();
        reticleRoot.anchorMin = new Vector2(0.5f, 0.5f);
        reticleRoot.anchorMax = new Vector2(0.5f, 0.5f);
        reticleRoot.pivot = new Vector2(0.5f, 0.5f);
        reticleRoot.anchoredPosition = Vector2.zero;
        reticleRoot.sizeDelta = new Vector2(170f, 170f);

        CreateFrameTexture(reticleRoot, "Outer Reticle Ring", GetRingTexture(), Vector2.zero, new Vector2(170f, 170f), frameColor, true);
        CreateFrameTexture(reticleRoot, "Inner Reticle Ring", GetRingTexture(), Vector2.zero, new Vector2(108f, 108f), frameAccentColor, true);
        CreateFrameTexture(reticleRoot, "Capture Dot Halo", GetRingTexture(), Vector2.zero, new Vector2(64f, 64f), frameRecordColor, false);
        CreateFrameTexture(reticleRoot, "Capture Dot", GetDiskTexture(), Vector2.zero, new Vector2(34f, 34f), frameRecordColor, false);
        CreateFrameLine(reticleRoot, "Reticle Top Gap", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -11f), new Vector2(76f, thick * 1.8f), frameColor);
        CreateFrameLine(reticleRoot, "Reticle Bottom Gap", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 11f), new Vector2(76f, thick * 1.8f), frameColor);
        CreateFrameLine(reticleRoot, "Reticle Left Tick", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(12f, 0f), new Vector2(36f, thick * 1.5f), frameColor, 45f);
        CreateFrameLine(reticleRoot, "Reticle Right Tick", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-12f, 0f), new Vector2(36f, thick * 1.5f), frameColor, -45f);
    }

    private Image CreateFrameLine(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size, Color color, float rotation = 0f, bool tintWithFrame = true)
    {
        GameObject lineObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        lineObject.transform.SetParent(parent, false);

        RectTransform rectTransform = lineObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.pivot = pivot;
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;
        rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);

        Image image = lineObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        if (tintWithFrame)
        {
            frameTintGraphics.Add(image);
        }

        return image;
    }

    private RawImage CreateFrameTexture(RectTransform parent, string name, Texture texture, Vector2 anchoredPosition, Vector2 size, Color color, bool tintWithFrame)
    {
        GameObject textureObject = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        textureObject.transform.SetParent(parent, false);

        RectTransform rectTransform = textureObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;

        RawImage rawImage = textureObject.GetComponent<RawImage>();
        rawImage.texture = texture;
        rawImage.color = color;
        rawImage.raycastTarget = false;
        if (tintWithFrame)
        {
            frameTintGraphics.Add(rawImage);
        }

        return rawImage;
    }

    private Text CreateFrameText(RectTransform parent, string name, string textValue, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, int fontSize, Color color, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);

        RectTransform rectTransform = textObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = anchor;
        rectTransform.anchorMax = anchor;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;

        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = textValue;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        frameTintGraphics.Add(text);
        return text;
    }

    private void UpdateCameraFrame()
    {
        if (frameCanvas == null || frameRoot == null)
        {
            return;
        }

        bool visible = showCameraFrame && useMouseFrameTargeting && (!Application.isPlaying || cameraModeState == CameraModeState.Active);
        frameCanvas.enabled = visible;
        SetSystemCursorHidden(visible && hideSystemCursor && Application.isPlaying);
        if (!visible)
        {
            return;
        }

        Rect frameRect = GetMouseFrameRect();
        frameRoot.position = frameRect.center;
        frameRoot.sizeDelta = frameRect.size;

        Color color = shutterCooldownTimer > 0f ? frameCooldownColor : frameColor;
        for (int i = 0; i < frameTintGraphics.Count; i++)
        {
            if (frameTintGraphics[i] != null)
            {
                frameTintGraphics[i].color = color;
            }
        }

        if (reticleRoot != null)
        {
            float reticleScale = Mathf.Clamp(frameRect.height / shutterFrameReferenceSize.y, 0.55f, 1.45f);
            reticleRoot.localScale = Vector3.one * reticleScale;
        }
    }

    private Rect GetMouseFrameRect()
    {
        Vector2 frameSize = GetFramePixelSize();
        Vector2 center = GetMouseScreenPosition();
        center.x = Mathf.Clamp(center.x, frameSize.x * 0.5f, Screen.width - frameSize.x * 0.5f);
        center.y = Mathf.Clamp(center.y, frameSize.y * 0.5f, Screen.height - frameSize.y * 0.5f);
        return new Rect(center - frameSize * 0.5f, frameSize);
    }

    private Vector2 GetFramePixelSize()
    {
        float widthScale = referenceResolution.x > 0f ? Screen.width / referenceResolution.x : 1f;
        float heightScale = referenceResolution.y > 0f ? Screen.height / referenceResolution.y : 1f;
        return new Vector2(
            Mathf.Max(32f, shutterFrameReferenceSize.x * widthScale),
            Mathf.Max(18f, shutterFrameReferenceSize.y * heightScale)
        );
    }

    private Texture2D GetRingTexture()
    {
        if (ringTexture == null)
        {
            ringTexture = CreateCircleTexture("Generated Camera Ring Texture", 128, false, 0.075f);
        }

        return ringTexture;
    }

    private Texture2D GetDiskTexture()
    {
        if (diskTexture == null)
        {
            diskTexture = CreateCircleTexture("Generated Camera Dot Texture", 64, true, 0.1f);
        }

        return diskTexture;
    }

    private Texture2D CreateCircleTexture(string textureName, int size, bool filled, float ringThickness)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = textureName,
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color32[] pixels = new Color32[size * size];
        float radius = (size - 1) * 0.5f;
        float innerRadius = radius * Mathf.Clamp01(1f - ringThickness);
        Vector2 center = new Vector2(radius, radius);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                bool inside = filled ? distance <= radius : distance <= radius && distance >= innerRadius;
                byte alpha = inside ? (byte)255 : (byte)0;
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false);
        return texture;
    }

    private Vector2 GetMouseScreenPosition()
    {
        if (Application.isPlaying && cameraModeState == CameraModeState.Active)
        {
            return cameraDragScreenPosition;
        }

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            return mouse.position.ReadValue();
        }

        return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
    }

    private void SetSystemCursorHidden(bool hidden)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (cursorHiddenByFrame == hidden)
        {
            return;
        }

        Cursor.visible = !hidden;
        cursorHiddenByFrame = hidden;
    }

    private void RestoreSystemCursor()
    {
        if (!cursorHiddenByFrame)
        {
            return;
        }

        Cursor.visible = true;
        cursorHiddenByFrame = false;
    }

    private bool BoundsIntersectsFrame(Bounds bounds, Rect frameRect, Camera camera)
    {
        if (camera == null || !SafeMath3D.IsFinite(bounds.center) || !SafeMath3D.IsFinite(bounds.extents))
        {
            return false;
        }
        if (PointIsInsideFrame(bounds.center, frameRect, camera))
        {
            return true;
        }

        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        return PointIsInsideFrame(new Vector3(min.x, min.y, min.z), frameRect, camera)
            || PointIsInsideFrame(new Vector3(min.x, min.y, max.z), frameRect, camera)
            || PointIsInsideFrame(new Vector3(min.x, max.y, min.z), frameRect, camera)
            || PointIsInsideFrame(new Vector3(min.x, max.y, max.z), frameRect, camera)
            || PointIsInsideFrame(new Vector3(max.x, min.y, min.z), frameRect, camera)
            || PointIsInsideFrame(new Vector3(max.x, min.y, max.z), frameRect, camera)
            || PointIsInsideFrame(new Vector3(max.x, max.y, min.z), frameRect, camera)
            || PointIsInsideFrame(new Vector3(max.x, max.y, max.z), frameRect, camera);
    }

    private bool PointIsInsideFrame(Vector3 worldPoint, Rect frameRect, Camera camera)
    {
        if (camera == null || !SafeMath3D.IsFinite(worldPoint))
        {
            return false;
        }
        Vector3 screenPoint = camera.WorldToScreenPoint(worldPoint);
        return SafeMath3D.IsFinite(screenPoint) && screenPoint.z > 0f && frameRect.Contains(new Vector2(screenPoint.x, screenPoint.y));
    }

    public void ResetCameraInterventions()
    {
        EnsureCameraInterventionLimiter();
        if (interventionLimiter != null)
        {
            interventionLimiter.ResetCameraInterventions();
        }
    }

    public void RestoreCameraInterventions(int amount)
    {
        EnsureCameraInterventionLimiter();
        if (interventionLimiter != null)
        {
            interventionLimiter.RestoreCameraInterventions(amount);
        }
    }

    private bool CanUseCameraIntervention()
    {
        EnsureCameraInterventionLimiter();
        return interventionLimiter == null || interventionLimiter.CanUseIntervention;
    }

    private bool ConsumeCameraIntervention(string reason)
    {
        EnsureCameraInterventionLimiter();
        return interventionLimiter == null || interventionLimiter.TryConsumeIntervention(reason);
    }

    private void EnsureCameraInterventionLimiter()
    {
        if (interventionLimiter == null)
        {
            interventionLimiter = GetComponent<CameraInterventionLimiter>();
        }

        if (interventionLimiter == null && Application.isPlaying)
        {
            interventionLimiter = gameObject.AddComponent<CameraInterventionLimiter>();
        }
    }

    private void SetupCameraHelpers()
    {
        ResolveCameraLightFollower();
        ResolveCameraWorldSwitcher();
    }

    private bool TryUseCameraWorldSwitcher()
    {
        CameraWorldSwitcher switcher = ResolveCameraWorldSwitcher();
        if (switcher == null)
        {
            return false;
        }

        return switcher.TrySwitchVisibleObjects();
    }

    private void UpdateCameraWorldTargetStates()
    {
        CameraWorldSwitcher switcher = ResolveCameraWorldSwitcher();
        if (switcher == null)
        {
            return;
        }

        if (cameraModeState == CameraModeState.Active)
        {
            cameraTargetScanActive = true;
            if (Time.unscaledTime >= nextCameraTargetRefreshTime)
            {
                nextCameraTargetRefreshTime = Time.unscaledTime + Mathf.Max(0.02f, cameraTargetRefreshInterval);
                switcher.RefreshVisibleTargets();
            }
        }
        else if (cameraTargetScanActive)
        {
            cameraTargetScanActive = false;
            nextCameraTargetRefreshTime = 0f;
            switcher.ClearTargetStates();
        }
    }

    private void ClearCameraWorldTargetStates()
    {
        cameraTargetScanActive = false;
        nextCameraTargetRefreshTime = 0f;
        if (cameraWorldSwitcher != null)
        {
            cameraWorldSwitcher.ClearTargetStates();
        }
    }

    private CameraLightFollower ResolveCameraLightFollower()
    {
        if (cameraLightFollower != null)
        {
            return cameraLightFollower;
        }

        Camera camera = GetTargetCamera();
        if (camera == null)
        {
            return null;
        }

        cameraLightFollower = camera.GetComponent<CameraLightFollower>();
        if (cameraLightFollower == null && Application.isPlaying)
        {
            cameraLightFollower = camera.gameObject.AddComponent<CameraLightFollower>();
        }

        if (cameraLightFollower != null)
        {
            cameraLightFollower.Bind(camera, flashLight);
            cameraLightFollower.SetPlayerTransform(transform);
        }

        return cameraLightFollower;
    }

    private CameraWorldSwitcher ResolveCameraWorldSwitcher()
    {
        if (cameraWorldSwitcher != null)
        {
            return cameraWorldSwitcher;
        }

        Camera camera = GetTargetCamera();
        if (camera == null)
        {
            return null;
        }

        cameraWorldSwitcher = camera.GetComponent<CameraWorldSwitcher>();
        if (cameraWorldSwitcher == null && Application.isPlaying)
        {
            cameraWorldSwitcher = camera.gameObject.AddComponent<CameraWorldSwitcher>();
        }

        if (cameraWorldSwitcher != null)
        {
            cameraWorldSwitcher.SetTargetCamera(camera);
        }

        return cameraWorldSwitcher;
    }

    private Vector3 ResolveCameraLightPosition(Vector3 origin, bool instant)
    {
        Vector3 fallback = origin + new Vector3(0f, 0f, -0.55f);
        CameraLightFollower follower = ResolveCameraLightFollower();
        return follower != null ? follower.MoveBoundLight(fallback, instant) : fallback;
    }

    private void SetupFlashLight()
    {
        if (cameraLightFollower != null && cameraLightFollower.LightObject != null)
        {
            flashLight = cameraLightFollower.LightObject;
            flashLight.type = LightType.Point;
            flashLight.color = flashLightColor;
            flashLight.range = flashLightRange;
            flashLight.intensity = 0f;
            cameraLightFollower.Bind(GetTargetCamera(), flashLight);
            cameraLightFollower.SetPlayerTransform(transform);
            cameraLightFollower.SetLightActive(false);
            ownsGeneratedFlashLight = false;
            return;
        }

        Camera camera = GetTargetCamera();
        Transform existingLightTransform = camera != null ? camera.transform.Find("Camera Toggle Light") : null;
        GameObject lightObject = existingLightTransform != null
            ? existingLightTransform.gameObject
            : new GameObject("Camera Toggle Light", typeof(Light));
        ownsGeneratedFlashLight = existingLightTransform == null;
        if (camera != null && lightObject.transform.parent != camera.transform)
        {
            lightObject.transform.SetParent(camera.transform, true);
        }
        CameraTagUtility3D.TrySetTag(lightObject, CameraTagUtility3D.LightTag);

        flashLight = lightObject.GetComponent<Light>();
        if (flashLight == null)
        {
            flashLight = lightObject.AddComponent<Light>();
        }
        flashLight.type = LightType.Point;
        flashLight.color = flashLightColor;
        flashLight.range = flashLightRange;
        flashLight.intensity = 0f;
        flashLight.enabled = false;
        if (cameraLightFollower != null)
        {
            cameraLightFollower.Bind(GetTargetCamera(), flashLight);
            cameraLightFollower.SetPlayerTransform(transform);
        }
    }

    private void SetCameraLight(bool active, Vector3 origin)
    {
        if (flashLight == null)
        {
            SetupFlashLight();
        }

        if (!active)
        {
            flashLight.intensity = 0f;
            CameraLightFollower follower = ResolveCameraLightFollower();
            if (follower != null)
            {
                follower.SetLightActive(false);
            }
            else
            {
                flashLight.enabled = false;
            }
            return;
        }

        flashLight.range = flashLightRange;
        flashLight.color = flashLightColor;
        flashLight.intensity = flashLightIntensity;
        CameraLightFollower activeFollower = ResolveCameraLightFollower();
        if (activeFollower != null)
        {
            activeFollower.SetLightActive(true, false);
        }
        else
        {
            flashLight.transform.position = ResolveCameraLightPosition(origin, false);
            flashLight.enabled = true;
        }
    }

    private void TurnOffCameraLight()
    {
        cameraLightOn = false;
        if (flashLight == null)
        {
            return;
        }

        flashLight.intensity = 0f;
        CameraLightFollower follower = ResolveCameraLightFollower();
        if (follower != null)
        {
            follower.SetLightActive(false);
        }
        else
        {
            flashLight.enabled = false;
        }
    }

    private void UpdateFlashLight()
    {
        if (flashLight == null)
        {
            return;
        }

        if (cameraLightOn)
        {
            Vector3 origin = useMouseFrameTargeting ? GetMouseWorldPoint(transform.position.z) : transform.position;
            SetCameraLight(true, origin);
            return;
        }

        if (flashLight.enabled)
        {
            SetCameraLight(false, transform.position);
        }
    }

    private Vector3 GetMouseWorldPoint(float targetZ)
    {
        Camera camera = GetTargetCamera();
        if (camera == null)
        {
            return transform.position;
        }

        Ray ray = camera.ScreenPointToRay(GetMouseScreenPosition());
        Plane plane = new Plane(Vector3.forward, new Vector3(0f, 0f, targetZ));
        return plane.Raycast(ray, out float enter) ? ray.GetPoint(enter) : transform.position;
    }

    private Camera GetTargetCamera()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        return targetCamera;
    }

    private Vector3 GetAimDirection()
    {
        if (movement == null)
        {
            movement = GetComponent<PlatformerPlayer3D>();
        }

        if (movement != null && Mathf.Abs(movement.VerticalLookInput) > 0.01f)
        {
            return movement.VerticalLookInput > 0f ? Vector3.up : Vector3.down;
        }

        float facing = movement != null ? movement.FacingDirection : 1f;
        return facing < 0f ? Vector3.left : Vector3.right;
    }

    private bool HasTagInParents(Component component, string tagName)
    {
        Transform current = component != null ? component.transform : null;
        while (current != null)
        {
            if (CameraTagUtility3D.HasAnyTag(current.gameObject, tagName))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private void TickCooldowns()
    {
        float cooldownDelta = Time.unscaledDeltaTime;
        shutterCooldownTimer = Mathf.Max(0f, shutterCooldownTimer - cooldownDelta);
        focusCooldownTimer = Mathf.Max(0f, focusCooldownTimer - cooldownDelta);
        relayCooldownTimer = Mathf.Max(0f, relayCooldownTimer - cooldownDelta);
    }

    private void PublishAbilityState()
    {
        ClampUnlockedAbilities();
        KnownAbilities = unlockedAbilities;
        AbilitiesChanged?.Invoke(KnownAbilities);
    }

    private void ClampUnlockedAbilities()
    {
        unlockedAbilities &= ActiveCameraAbilityMask;
    }

    private static bool WasPressed(Keyboard keyboard, Key key)
    {
        return keyboard != null && key != Key.None && keyboard[key].wasPressedThisFrame;
    }

    private static bool IsHeld(Keyboard keyboard, Key key)
    {
        return keyboard != null && key != Key.None && keyboard[key].isPressed;
    }

    private static void DestroyGenerated(UnityEngine.Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private readonly struct ShutterMarkRecord
    {
        public ShutterMarkRecord(IShutterFreezable3D target, float markEndTime, float cooldownEndTime)
        {
            Target = target;
            MarkEndTime = markEndTime;
            CooldownEndTime = cooldownEndTime;
        }

        public IShutterFreezable3D Target { get; }
        public float MarkEndTime { get; }
        public float CooldownEndTime { get; }
    }
}
