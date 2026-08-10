using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using Unity.Profiling;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlatformerPlayer3D))]
[RequireComponent(typeof(CameraInterventionLimiter))]
public class CameraAbilitySystem3D : MonoBehaviour
{
    private static readonly ProfilerMarker ShutterMarker = new ProfilerMarker("Camera.Shutter");
    private static readonly ProfilerMarker WorldTransferMarker = new ProfilerMarker("Camera.WorldTransfer");
    private enum CameraModeState
    {
        Ready,
        Active,
        ForcedExitBlocked
    }

    private const CameraAbilityFlags ActiveCameraAbilityMask = CameraAbilityUnlockState3D.KnownAbilityMask;

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
    [SerializeField, InspectorName("Camera Target Layer Mask"), Tooltip("실제 셔터/월드 이동 대상 레이어만 포함합니다. Ground, Wall, TileObstacle과 생성 맵 충돌은 제외합니다.")]
    private LayerMask targetMask = ~0;
    [SerializeField] private float aimHeightOffset = 0.35f; // 방향 조준 시 플레이어 위치에서 위로 올리는 조준 시작점입니다.
    [SerializeField, InspectorName("Target Search Range")] private float shutterRange = 7f; // 셔터가 대상을 찾을 수 있는 최대 거리입니다.
    [SerializeField] private Vector3 shutterBoxSize = new Vector3(1.5f, 1.5f, 1f); // 방향 조준 셔터 판정 박스 크기입니다.
    [SerializeField] private float relayRange = 3f; // 릴레이 기능이 대상을 찾을 수 있는 최대 거리입니다.
    [SerializeField] private Vector3 relayBoxSize = new Vector3(1.6f, 1.6f, 1f); // 방향 조준 릴레이 판정 박스 크기입니다.
    [SerializeField, Tooltip("셔터 시야를 차단할 Ground/Wall/TileObstacle 레이어입니다.")] private LayerMask shutterLineOfSightMask = (1 << 9) | (1 << 10) | (1 << 11);

    [Header("Shutter Debug")]
    [SerializeField] private bool showShutterDebug;
    [SerializeField] private bool logShutterEvents;
    [SerializeField] private bool debugCameraMode;

    [Header("Shutter Runtime Debug (Read Only)")]
    [SerializeField] private bool runtimeCameraModeActive;
    [SerializeField] private bool runtimeLastShutterInputReceived;
    [SerializeField] private string runtimeLastShutterExecutionResult = "Not executed";
    [SerializeField] private string runtimeLastShutterBlockReason = "None";
    [SerializeField] private int runtimeShutterPhysicsHitCount;
    [SerializeField] private int runtimeElectronicNoiseRootCount;
    [SerializeField] private int runtimeMarkableResolvedCount;
    [SerializeField] private int runtimeMarkInvokedCount;
    [SerializeField] private string runtimeLastMarkedObjectName = "None";
    [SerializeField] private float runtimeMarkDuration;

    [Header("Mark")]
    [FormerlySerializedAs("shutterFreezeDuration")]
    [SerializeField, Min(0f), Tooltip("촬영 범위 안 기믹 오브젝트에 부여할 Mark 지속시간입니다.")] private float markDuration = 1.2f;

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
    private readonly CameraAbilityCooldowns3D cameraAbilityCooldowns = new CameraAbilityCooldowns3D();
    private readonly CameraDiagnostics3D cameraDiagnostics = new CameraDiagnostics3D();
    private readonly CameraModeTimeController3D cameraModeTimeController = new CameraModeTimeController3D();
    private readonly CameraModeInputReader3D cameraModeInputReader = new CameraModeInputReader3D();
    private readonly CameraWorldTargetStateController3D cameraWorldTargetStateController = new CameraWorldTargetStateController3D();
    private readonly CameraModeDragController3D cameraModeDragController = new CameraModeDragController3D();
    private readonly List<MonoBehaviour> interfaceSearchBuffer = new List<MonoBehaviour>(16);
    private readonly Transform[] shutterElectronicRoots = new Transform[64];
    private readonly Transform[] markableRoots = new Transform[64];
    private readonly IMarkable3D[] markTargets = new IMarkable3D[64];
    private readonly Component[] markTargetComponents = new Component[64];
    private readonly Transform[] markTargetRoots = new Transform[64];

    private PlatformerPlayer3D movement;
    private Camera targetCamera;
    private CameraFramePresenter3D cameraFramePresenter;
    private CameraLightAbilityController3D cameraLightController;
    private CameraAbilityUnlockState3D cameraAbilityUnlockState;
    private CameraModeState cameraModeState = CameraModeState.Ready;
    private Vector3 lastShutterTargetPosition;
    private bool lastShutterTargetBlocked;
    private float pendingPrimaryFireTime;
    private bool primaryFirePending;
    private bool requireRightMouseRelease;
    private Predicate<Collider> relayCandidatePredicate;

    private const float DoubleClickInterval = 0.3f;

    public static event Action<CameraAbilityFlags> AbilitiesChanged;
    public static event Action<bool> CameraSlowMotionChanged;

    public static CameraAbilityFlags KnownAbilities { get; private set; } = CameraAbilityFlags.None;
    public CameraAbilityFlags UnlockedAbilities => cameraAbilityUnlockState != null
        ? cameraAbilityUnlockState.UnlockedAbilities
        : unlockedAbilities;
    public bool IsCameraModeActive => cameraModeState == CameraModeState.Active;
    public bool CanEnterCameraMode => cameraModeState == CameraModeState.Ready && !requireRightMouseRelease;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public int DebugEnterRequestCount => cameraDiagnostics.EnterRequestCount;
    public int DebugActualEnterCount => cameraDiagnostics.ActualEnterCount;
    public int DebugApplySlowMotionCount => cameraDiagnostics.ApplySlowMotionCount;
    public int DebugExitRequestCount => cameraDiagnostics.ExitRequestCount;
    public int DebugActualExitCount => cameraDiagnostics.ActualExitCount;
    public int DebugRestoreSlowMotionCount => cameraDiagnostics.RestoreSlowMotionCount;
    public int DebugDuplicateTransitionBlockCount => cameraDiagnostics.DuplicateTransitionBlockCount;
#endif
    private void Awake()
    {
        movement = GetComponent<PlatformerPlayer3D>();
        relayCandidatePredicate = CanResolveRelayTarget;
        targetCamera = Camera.main;
        EnsureCameraInterventionLimiter();
        cameraFramePresenter = new CameraFramePresenter3D(
            transform,
            showCameraFrame,
            frameBorderThickness,
            frameColor,
            frameAccentColor,
            frameRecordColor,
            frameCooldownColor,
            shutterFrameReferenceSize.y);
        cameraFramePresenter.Initialize();
        cameraLightController = new CameraLightAbilityController3D(
            transform,
            flashLightIntensity,
            flashLightRange,
            flashLightColor);
        cameraLightController.Initialize(GetTargetCamera(), cameraLightFollower);
        cameraLightFollower = cameraLightController.Follower;
        SetupCameraHelpers();
        EnsureCameraAbilityUnlockState();
        SyncUnlockedAbilitiesMirror();
        PublishAbilityState();
    }

    private void Start()
    {
        if (loadProgressOnStart)
        {
            EnsureCameraAbilityUnlockState();
            cameraAbilityUnlockState.Merge(GameProgressSave3D.GetUnlockedAbilities());
            SyncUnlockedAbilitiesMirror();
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
        ForceExitCameraMode("Component disabled", false, false);
        cameraModeState = CameraModeState.Ready;
        requireRightMouseRelease = false;
        cameraFramePresenter?.RestoreCursor();
        cameraLightController?.TurnOff();
        ClearCameraWorldTargetStates();
    }

    private void OnDestroy()
    {
        ForceExitCameraMode("Object destroyed", false, false);
        cameraFramePresenter?.RestoreCursor();
        cameraLightController?.TurnOff();
        cameraLightController?.Dispose();
        cameraLightController = null;
        cameraFramePresenter?.Dispose();
        cameraFramePresenter = null;
    }

    private void Update()
    {
        runtimeCameraModeActive = cameraModeState == CameraModeState.Active;
        cameraAbilityCooldowns.Tick(Time.unscaledDeltaTime);

        if (Application.isPlaying && GameplayInputLock3D.IsLocked)
        {
            if (cameraModeState == CameraModeState.Active || cameraModeTimeController.IsActive)
                ForceExitCameraMode("Gameplay input locked", true);
            UpdateCameraFrame();
            return;
        }

        UpdateFlashLight();

        if (!Application.isPlaying)
        {
            UpdateCameraFrame();
            return;
        }

        DetectPauseTimeOverride();
        CameraInputSnapshot3D input = cameraModeInputReader.Read(
            usePrimaryFireForCameraAbility,
            useSecondaryFireForCameraMode,
            worldSwitchKey,
            lightToggleKey);
        UpdateCameraModeInput(input);
        TickPendingPrimaryCameraClick();
        UpdateCameraWorldTargetStates();

        UpdateCameraFrame();
        if (input.WorldSwitchPressedThisFrame)
        {
            TryUseGlobalWorldSwitch();
            return;
        }

        if (input.LightPressedThisFrame)
        {
            ToggleCameraLight();
        }

        if (cameraModeState != CameraModeState.Active)
        {
            return;
        }

        if (input.PrimaryPressedThisFrame)
        {
            runtimeLastShutterInputReceived = true;
            HandlePrimaryCameraClick();
        }
    }

    private void UpdateCameraModeInput(in CameraInputSnapshot3D input)
    {
        if (!input.HasSecondaryInput)
        {
            ExitCameraMode("Secondary camera input unavailable");
            return;
        }

        bool rightPressed = input.SecondaryHeld;
        bool rightDown = input.SecondaryPressedThisFrame;
        bool rightUp = input.SecondaryReleasedThisFrame;

        if (cameraModeState == CameraModeState.ForcedExitBlocked)
        {
            if (rightUp || !rightPressed)
            {
                requireRightMouseRelease = false;
                cameraModeState = CameraModeState.Ready;
                LogCameraMode("Right Mouse Up - forced-exit block cleared");
            }
            return;
        }

        if (rightDown)
        {
            LogCameraMode("Right Mouse Down");
            EnterCameraMode("Right Mouse Down", input.PointerScreenPosition);
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

        UpdateCameraDrag(input.HasMouse, input.PointerDelta);
    }

    private void EnterCameraMode(string reason)
    {
        Vector2 fallback = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        EnterCameraMode(reason, cameraModeInputReader.ReadPointerScreenPosition(fallback));
    }

    private void EnterCameraMode(string reason, Vector2 pointerScreenPosition)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        cameraDiagnostics.RecordEnterRequest();
#endif
        if (!CanEnterCameraMode || !Application.isPlaying || Time.timeScale <= 0f)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            cameraDiagnostics.RecordDuplicateTransitionBlock();
#endif
            return;
        }

        cameraModeState = CameraModeState.Active;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        cameraDiagnostics.RecordActualEnter();
#endif
        InitializeCameraDrag(pointerScreenPosition);
        ApplyCameraModeSlowMotion();
        LogCameraTransition("Camera Enter", reason);
    }

    private void ExitCameraMode(string reason, bool startCooldown = true)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        cameraDiagnostics.RecordExitRequest();
#endif
        if (cameraModeState != CameraModeState.Active)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            cameraDiagnostics.RecordDuplicateTransitionBlock();
#endif
            return;
        }

        cameraModeState = CameraModeState.Ready;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        cameraDiagnostics.RecordActualExit();
#endif
        ResetCameraDrag();
        primaryFirePending = false;
        RestoreCameraModeSlowMotion();
        ClearCameraWorldTargetStates();
        LogCameraTransition("Camera Exit", reason);
    }

    private void ApplyCameraModeSlowMotion()
    {
        if (!cameraModeTimeController.Apply(cameraModeTimeScale))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            cameraDiagnostics.RecordDuplicateTransitionBlock();
#endif
            return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        cameraDiagnostics.RecordApplySlowMotion();
#endif
        CameraSlowMotionChanged?.Invoke(true);
    }

    private void RestoreCameraModeSlowMotion()
    {
        bool wasSlowActive = cameraModeTimeController.IsActive;
        if (!cameraModeTimeController.Restore())
        {
            return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        cameraDiagnostics.RecordRestoreSlowMotion();
#endif
        if (wasSlowActive) CameraSlowMotionChanged?.Invoke(false);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public void ResetCameraTransitionDiagnostics()
    {
        cameraDiagnostics.Reset();
    }
#endif

    private void DetectPauseTimeOverride()
    {
        if (!cameraModeTimeController.HasExternalPauseOverride())
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

    public void SuspendInputForModalUI()
    {
        ForceExitCameraMode("Modal UI opened", true, false);
    }

    private void ForceExitCameraMode(string reason, bool blockUntilRightMouseRelease, bool startCooldown = true)
    {
        bool wasActive = cameraModeState == CameraModeState.Active || cameraModeTimeController.IsActive;
        if (cameraModeState == CameraModeState.Active)
        {
            ExitCameraMode(reason, startCooldown);
        }
        else
        {
            primaryFirePending = false;
            ResetCameraDrag();
            RestoreCameraModeSlowMotion();
        }

        if (blockUntilRightMouseRelease)
        {
            requireRightMouseRelease = true;
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
        ForceExitCameraMode("Application quit", false, false);
    }

    private void InitializeCameraDrag(Vector2 pointerScreenPosition)
    {
        cameraModeDragController.Initialize(pointerScreenPosition);
    }

    private void UpdateCameraDrag(bool hasMouse, Vector2 pointerDelta)
    {
        cameraModeDragController.Tick(
            hasMouse,
            pointerDelta,
            cameraDragSensitivity,
            Time.unscaledDeltaTime,
            new Vector2(Screen.width, Screen.height));
    }

    private void ResetCameraDrag()
    {
        cameraModeDragController.Reset();
    }

    private void LogCameraMode(string message)
    {
        cameraDiagnostics.LogCameraMode(debugCameraMode, message, this);
    }

    private void LogCameraTransition(string transition, string reason)
    {
        cameraDiagnostics.LogCameraTransition(debugCameraMode, transition, reason, this);
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
        EnsureCameraAbilityUnlockState();
        return cameraAbilityUnlockState.IsUnlocked(ability);
    }

    public bool UnlockAbility(CameraAbilityId ability)
    {
        EnsureCameraAbilityUnlockState();
        if (!cameraAbilityUnlockState.TryUnlock(ability))
        {
            return false;
        }

        SyncUnlockedAbilitiesMirror();
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
        return CameraAbilityUnlockState3D.ToFlag(ability);
    }

    private void TryUseShutter()
    {
        using (ShutterMarker.Auto()) TryUseShutterCore();
    }

    private void TryUseShutterCore()
    {
        ResetShutterRuntimeDebug();
        if (!IsUnlocked(CameraAbilityId.Shutter))
        {
            runtimeLastShutterExecutionResult = "Blocked";
            runtimeLastShutterBlockReason = "Shutter locked";
            LogShutter("사용 불가: 미해금");
            return;
        }

        if (markDuration <= 0f)
        {
            runtimeLastShutterExecutionResult = "Blocked";
            runtimeLastShutterBlockReason = "Mark Duration must be greater than zero";
            return;
        }

        int targetCount = CollectMarkTargets();
        if (targetCount == 0)
        {
            runtimeLastShutterExecutionResult = "No target";
            runtimeLastShutterBlockReason = runtimeShutterPhysicsHitCount == 0
                ? "No collider in Mark range"
                : runtimeElectronicNoiseRootCount == 0
                    ? "No ElectronicNoise root in camera frame"
                    : runtimeMarkableResolvedCount == 0
                        ? "IMarkable3D not resolved"
                        : "Rejected by frame, line of sight, or camera policy";
            LogShutter("유효한 Mark 대상이 없습니다.");
            return;
        }

        for (int i = 0; i < targetCount; i++)
        {
            IMarkable3D target = markTargets[i];
            Component targetComponent = markTargetComponents[i];
            if (target == null || targetComponent == null || !target.ApplyMark(markDuration, this)) continue;

            runtimeMarkInvokedCount++;
            runtimeLastMarkedObjectName = markTargetRoots[i] != null ? markTargetRoots[i].name : targetComponent.name;
        }

        runtimeLastShutterExecutionResult = runtimeMarkInvokedCount > 0
            ? $"Mark applied: {runtimeMarkInvokedCount}"
            : "Targets rejected Mark";
        runtimeLastShutterBlockReason = runtimeMarkInvokedCount > 0 ? "None" : "ApplyMark rejected";
        LogShutter($"Mark 적용 완료: {runtimeMarkInvokedCount}/{targetCount}");
    }

    private void ResetShutterRuntimeDebug()
    {
        runtimeLastShutterExecutionResult = "Executing";
        runtimeLastShutterBlockReason = "None";
        runtimeShutterPhysicsHitCount = 0;
        runtimeElectronicNoiseRootCount = 0;
        runtimeMarkableResolvedCount = 0;
        runtimeMarkInvokedCount = 0;
        runtimeLastMarkedObjectName = "None";
        runtimeMarkDuration = markDuration;
        for (int i = 0; i < shutterElectronicRoots.Length; i++)
        {
            shutterElectronicRoots[i] = null;
            markableRoots[i] = null;
            markTargets[i] = null;
            markTargetComponents[i] = null;
            markTargetRoots[i] = null;
        }
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
        using (WorldTransferMarker.Auto()) TryUseFocusCore();
    }

    private void TryUseFocusCore()
    {
        if (!IsUnlocked(CameraAbilityId.Focus) || !cameraAbilityCooldowns.CanUseFocus)
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
                cameraAbilityCooldowns.StartFocus(focusCooldown);
            }
        }
    }

    private void ToggleCameraLight()
    {
        cameraLightController?.Toggle();
    }

    private void TryUseRelay()
    {
        if (!IsUnlocked(CameraAbilityId.Relay) || !cameraAbilityCooldowns.CanUseRelay)
        {
            return;
        }

        if (!TryFindRelayTarget(out IRelayTransferable3D target))
        {
            cameraAbilityCooldowns.StartRelay(relayCooldown);
            return;
        }

        ResearchWorldId targetWorld = WorldSystem3D.GetOpposite(WorldSystem3D.ActiveWorld);
        if (target.TryRelayToWorld(targetWorld, this))
        {
            cameraAbilityCooldowns.StartRelay(relayCooldown);
        }
    }

    private int CollectMarkTargets()
    {
        Camera camera = GetTargetCamera();
        if (useMouseFrameTargeting)
        {
            if (camera == null) return 0;
            Rect frameRect = GetMouseFrameRect();
            int hitCount = Physics.OverlapSphereNonAlloc(
                transform.position,
                Mathf.Max(0.1f, shutterRange),
                targetHits,
                targetMask,
                QueryTriggerInteraction.Collide);
            runtimeShutterPhysicsHitCount = hitCount;

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = targetHits[i];
                if (!IsValidMarkHit(hit) || !BoundsIntersectsFrame(hit.bounds, frameRect, camera)) continue;
                if (IsShutterLineOfSightBlocked(camera.transform.position, hit)) continue;
                RegisterMarkTarget(hit);
            }
        }
        else
        {
            Vector3 direction = GetAimDirection();
            Vector3 origin = transform.position + Vector3.up * aimHeightOffset;
            Vector3 halfExtents = new Vector3(
                Mathf.Max(0.05f, shutterBoxSize.x * 0.5f),
                Mathf.Max(0.05f, shutterBoxSize.y * 0.5f),
                Mathf.Max(0.05f, shutterBoxSize.z * 0.5f));
            int hitCount = Physics.BoxCastNonAlloc(
                origin,
                halfExtents,
                direction,
                targetCastHits,
                Quaternion.identity,
                Mathf.Max(0.1f, shutterRange),
                targetMask,
                QueryTriggerInteraction.Collide);
            runtimeShutterPhysicsHitCount = hitCount;
            Vector3 sightOrigin = camera != null ? camera.transform.position : origin;

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = targetCastHits[i].collider;
                if (!IsValidMarkHit(hit) || IsShutterLineOfSightBlocked(sightOrigin, hit)) continue;
                RegisterMarkTarget(hit);
            }
        }

        return runtimeMarkableResolvedCount;
    }

    private bool IsValidMarkHit(Collider hit)
    {
        return hit != null
            && !hit.transform.IsChildOf(transform)
            && !IsGeneratedMapCollider(hit)
            && CanResolveShutterTarget(hit);
    }

    private void RegisterMarkTarget(Collider hit)
    {
        Transform taggedRoot = FindTaggedParent(hit, CameraTagUtility3D.ElectronicNoiseTag);
        if (taggedRoot == null) return;

        for (int i = 0; i < runtimeMarkableResolvedCount; i++)
        {
            if (markTargetRoots[i] == taggedRoot) return;
        }

        if (runtimeMarkableResolvedCount >= markTargets.Length) return;
        IMarkable3D target = ResolveInterface<IMarkable3D>(hit);
        Component targetComponent = ResolveTargetComponent(target, hit);
        if (target == null || targetComponent == null) return;

        int index = runtimeMarkableResolvedCount;
        markTargets[index] = target;
        markTargetComponents[index] = targetComponent;
        markTargetRoots[index] = taggedRoot;
        RegisterMarkableRoot(taggedRoot);
    }

    private bool TryFindRelayTarget(out IRelayTransferable3D target)
    {
        target = null;
        Collider hit = useMouseFrameTargeting
            ? FindBestScreenFramedCollider(relayRange, relayCandidatePredicate)
            : FindBestDirectionalCollider(relayRange, relayBoxSize, relayCandidatePredicate, false);

        if (hit == null)
        {
            return false;
        }

        target = EnsureRelayTarget(hit);
        return target != null;
    }

    private Collider FindBestDirectionalCollider(float range, Vector3 boxSize, Predicate<Collider> isCandidate, bool requireLineOfSight)
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
        runtimeShutterPhysicsHitCount = hitCount;

        Collider bestHit = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = targetCastHits[i].collider;
            if (hit == null || hit.transform.IsChildOf(transform) || IsGeneratedMapCollider(hit) || !isCandidate(hit))
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

    private Collider FindBestScreenFramedCollider(float range, Predicate<Collider> isCandidate)
    {
        Camera camera = GetTargetCamera();
        if (camera == null)
        {
            return null;
        }

        Rect frameRect = GetMouseFrameRect();
        Vector2 frameCenter = frameRect.center;
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, Mathf.Max(0.1f, range), targetHits, targetMask, QueryTriggerInteraction.Collide);
        runtimeShutterPhysicsHitCount = hitCount;

        Collider bestHit = null;
        float bestScore = float.MaxValue;
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = targetHits[i];
            if (hit == null || hit.transform.IsChildOf(transform) || IsGeneratedMapCollider(hit) || !isCandidate(hit))
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

        Transform targetRoot = FindTaggedParent(targetCollider, CameraTagUtility3D.ElectronicNoiseTag);
        Transform hitRoot = FindTaggedParent(hit.collider, CameraTagUtility3D.ElectronicNoiseTag);
        bool sameMarkRoot = targetRoot != null && targetRoot == hitRoot;
        lastShutterTargetBlocked = !sameMarkRoot
            && hit.collider != targetCollider
            && !hit.collider.transform.IsChildOf(targetCollider.transform);
        return lastShutterTargetBlocked;
    }

    private static bool IsGeneratedMapCollider(Collider hit)
    {
        return hit != null && hit.GetComponentInParent<TilemapGeneratedColliderMarker>(true) != null;
    }

    private void LogShutter(string message)
    {
        cameraDiagnostics.LogShutter(logShutterEvents, message, this);
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

    private bool CanResolveShutterTarget(Collider hit)
    {
        if (hit == null || !HasTagInParents(hit, CameraTagUtility3D.ElectronicNoiseTag))
        {
            return false;
        }
        Transform taggedRoot = FindTaggedParent(hit, CameraTagUtility3D.ElectronicNoiseTag);
        RegisterElectronicNoiseRoot(taggedRoot);
        if (!CameraObjectTag3D.AllowsCameraInteraction(hit) || !CameraObjectTag3D.AllowsCameraFreeze(hit))
        {
            return false;
        }

        IMarkable3D target = ResolveInterface<IMarkable3D>(hit);
        if (target == null)
        {
            return false;
        }

        return ResolveTargetComponent(target, hit) != null;
    }

    private void RegisterElectronicNoiseRoot(Transform root)
    {
        if (root == null) return;
        for (int i = 0; i < runtimeElectronicNoiseRootCount; i++)
        {
            if (shutterElectronicRoots[i] == root) return;
        }

        if (runtimeElectronicNoiseRootCount >= shutterElectronicRoots.Length) return;
        shutterElectronicRoots[runtimeElectronicNoiseRootCount] = root;
        runtimeElectronicNoiseRootCount++;
    }

    private void RegisterMarkableRoot(Transform root)
    {
        if (root == null) return;
        for (int i = 0; i < runtimeMarkableResolvedCount; i++)
        {
            if (markableRoots[i] == root) return;
        }

        if (runtimeMarkableResolvedCount >= markableRoots.Length) return;
        markableRoots[runtimeMarkableResolvedCount] = root;
        runtimeMarkableResolvedCount++;
    }

    private bool CanResolveRelayTarget(Collider hit)
    {
        return ResolveInterface<IRelayTransferable3D>(hit) != null
            || (HasTagInParents(hit, CameraTagUtility3D.RelayTargetTag)
                && hit.GetComponentInParent<WorldVariant3D>() != null);
    }

    private IRelayTransferable3D EnsureRelayTarget(Collider hit)
    {
        IRelayTransferable3D relayTarget = ResolveInterface<IRelayTransferable3D>(hit);
        if (relayTarget != null) return relayTarget;
        if (!CanResolveRelayTarget(hit)) return null;
        WorldVariant3D variant = hit.GetComponentInParent<WorldVariant3D>();
        RelayTransferable3D generatedRelay = variant.GetComponent<RelayTransferable3D>();
        if (generatedRelay == null)
        {
            generatedRelay = variant.gameObject.AddComponent<RelayTransferable3D>();
        }

        return generatedRelay;
    }

    private bool TryRelayMarkedTarget(Component targetComponent)
    {
        IRelayTransferable3D relayTarget = EnsureRelayTargetFromComponent(targetComponent);
        if (relayTarget == null)
        {
            return false;
        }

        ResearchWorldId targetWorld = WorldSystem3D.GetOpposite(WorldSystem3D.ActiveWorld);
        return relayTarget.TryRelayToWorld(targetWorld, this);
    }

    private IRelayTransferable3D EnsureRelayTargetFromComponent(Component component)
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

        interfaceSearchBuffer.Clear();
        hit.GetComponentsInParent(true, interfaceSearchBuffer);
        for (int i = 0; i < interfaceSearchBuffer.Count; i++)
        {
            if (interfaceSearchBuffer[i] is T target)
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

    private void UpdateCameraFrame()
    {
        bool visible = showCameraFrame && useMouseFrameTargeting && (!Application.isPlaying || cameraModeState == CameraModeState.Active);
        Rect frameRect = visible ? GetMouseFrameRect() : default;
        cameraFramePresenter?.Present(visible, frameRect, cameraAbilityCooldowns.ShutterRemaining > 0f, hideSystemCursor);
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

    private Vector2 GetMouseScreenPosition()
    {
        if (Application.isPlaying && cameraModeState == CameraModeState.Active)
        {
            return cameraModeDragController.ScreenPosition;
        }

        Vector2 fallback = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        return cameraModeInputReader.ReadPointerScreenPosition(fallback);
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
        bool cameraModeActive = cameraModeState == CameraModeState.Active;
        if (cameraModeActive)
        {
            ResolveCameraWorldSwitcher();
        }

        cameraWorldTargetStateController.Tick(
            cameraModeActive,
            cameraWorldSwitcher,
            Time.unscaledTime,
            cameraTargetRefreshInterval);
    }

    private void ClearCameraWorldTargetStates()
    {
        cameraWorldTargetStateController.Clear(cameraWorldSwitcher);
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

    private void UpdateFlashLight()
    {
        if (cameraLightController == null)
        {
            return;
        }

        if (cameraLightController.IsOn)
        {
            Vector3 origin = useMouseFrameTargeting ? GetMouseWorldPoint(transform.position.z) : transform.position;
            cameraLightController.Tick(origin);
            return;
        }

        cameraLightController.Tick(transform.position);
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
        return FindTaggedParent(component, tagName) != null;
    }

    private static Transform FindTaggedParent(Component component, string tagName)
    {
        Transform current = component != null ? component.transform : null;
        while (current != null)
        {
            if (CameraTagUtility3D.HasAnyTag(current.gameObject, tagName)) return current;
            current = current.parent;
        }
        return null;
    }

    private void PublishAbilityState()
    {
        EnsureCameraAbilityUnlockState();
        KnownAbilities = cameraAbilityUnlockState.UnlockedAbilities;
        AbilitiesChanged?.Invoke(KnownAbilities);
    }

    private void EnsureCameraAbilityUnlockState()
    {
        if (cameraAbilityUnlockState == null)
        {
            cameraAbilityUnlockState = new CameraAbilityUnlockState3D(unlockedAbilities);
            unlockedAbilities = cameraAbilityUnlockState.UnlockedAbilities;
        }
    }

    private void SyncUnlockedAbilitiesMirror()
    {
        unlockedAbilities = cameraAbilityUnlockState.UnlockedAbilities;
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

}
