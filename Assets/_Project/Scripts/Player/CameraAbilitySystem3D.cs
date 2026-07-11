using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlatformerPlayer3D))]
[RequireComponent(typeof(CameraInterventionLimiter))]
public class CameraAbilitySystem3D : MonoBehaviour
{
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
    [SerializeField] private Key previousCameraSlotKey = Key.None; // Optional key for moving to the previous camera function slot.
    [SerializeField] private Key nextCameraSlotKey = Key.E; // 카메라 기능 슬롯을 오른쪽으로 이동하는 키입니다.
    [SerializeField] private CameraAbilityId selectedCameraAbility = CameraAbilityId.Shutter; // 현재 선택된 카메라 기능 슬롯입니다.

    [Header("Camera Mode")]
    [SerializeField] private Key cameraModeKey = Key.None; // 보조 키보드 카메라 모드 키입니다. None이면 마우스 우클릭만 사용합니다.
    [SerializeField] private bool holdCameraMode = true; // 켜면 키를 누르는 동안만 카메라 모드가 유지되고, 끄면 키를 누를 때마다 토글됩니다.
    [SerializeField] private bool startInCameraMode = false; // 테스트용으로 시작하자마자 카메라 모드를 켤지 정합니다.
    [SerializeField] private float cameraModeSlowDuration = 2f; // 카메라 모드에 들어갈 때 전체 시간이 느려지는 실제 시간입니다.
    [SerializeField] private float cameraModeTimeScale = 0.25f; // 카메라 모드 진입 직후 적용되는 전체 오브젝트 배속입니다.

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

    [Header("Targeting")]
    [SerializeField] private LayerMask targetMask = ~0; // 카메라 기능이 감지할 대상 레이어 범위입니다.
    [SerializeField] private float aimHeightOffset = 0.35f; // 방향 조준 시 플레이어 위치에서 위로 올리는 조준 시작점입니다.
    [SerializeField] private float shutterRange = 7f; // 셔터가 대상을 찾을 수 있는 최대 거리입니다.
    [SerializeField] private Vector3 shutterBoxSize = new Vector3(1.5f, 1.5f, 1f); // 방향 조준 셔터 판정 박스 크기입니다.
    [SerializeField] private float relayRange = 3f; // 릴레이 기능이 대상을 찾을 수 있는 최대 거리입니다.
    [SerializeField] private Vector3 relayBoxSize = new Vector3(1.6f, 1.6f, 1f); // 방향 조준 릴레이 판정 박스 크기입니다.
    [SerializeField] private bool allowUntaggedShutterTargets = true; // 전용 태그가 없는 Rigidbody도 셔터 대상으로 허용할지 정합니다.

    [Header("Timing")]
    [SerializeField] private float shutterFreezeDuration = 1.2f; // 사진을 찍은 오브젝트가 물리 정지 상태로 유지되는 시간입니다.
    [SerializeField] private float shutterMarkDuration = 5f; // 셔터로 찍은 대상을 표시해두는 시간입니다.
    [SerializeField] private float shutterRemarkCooldown = 7f; // 같은 대상을 다시 마킹하기 전 기다리는 시간입니다.
    [SerializeField] private float shutterCooldown = 1f; // 셔터 기능 재사용 대기 시간입니다.
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
    private Text modeLabel;
    private Texture2D ringTexture;
    private Texture2D diskTexture;
    private Light flashLight;
    private bool cursorHiddenByFrame;
    private bool cameraModeActive;
    private bool cameraModeSlowActive;
    private bool cameraLightOn;
    private float shutterCooldownTimer;
    private float focusCooldownTimer;
    private float relayCooldownTimer;
    private float cameraModeSlowEndTime;
    private float storedTimeScale = 1f;
    private float storedFixedDeltaTime = 0.02f;

    public static event Action<CameraAbilityFlags> AbilitiesChanged;

    public static CameraAbilityFlags KnownAbilities { get; private set; } = CameraAbilityFlags.None;
    public CameraAbilityFlags UnlockedAbilities => unlockedAbilities;
    public bool IsCameraModeActive => cameraModeActive;
    private void Awake()
    {
        movement = GetComponent<PlatformerPlayer3D>();
        targetCamera = Camera.main;
        NormalizeSelectedCameraAbility();
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

        SetCameraModeActive(startInCameraMode);
    }

    private void OnEnable()
    {
        PublishAbilityState();
    }

    private void OnDisable()
    {
        RestoreSystemCursor();
        RestoreCameraModeSlow();
        TurnOffCameraLight();
    }

    private void OnDestroy()
    {
        RestoreSystemCursor();
        RestoreCameraModeSlow();
        TurnOffCameraLight();
        DestroyGenerated(ringTexture);
        DestroyGenerated(diskTexture);
    }

    private void Update()
    {
        TickCameraModeSlow();
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
        UpdateCameraModeInput(keyboard, mouse);

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

        if (!cameraModeActive)
        {
            return;
        }

        if (WasPressed(keyboard, previousCameraSlotKey))
        {
            CycleSelectedCameraAbility(-1);
        }

        if (WasPressed(keyboard, nextCameraSlotKey))
        {
            CycleSelectedCameraAbility(1);
        }

        if (usePrimaryFireForCameraAbility && mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            TryUseSelectedCameraAbility();
        }
    }

    private void UpdateCameraModeInput(Keyboard keyboard, Mouse mouse)
    {
        if (holdCameraMode)
        {
            bool keyHeld = keyboard != null && IsHeld(keyboard, cameraModeKey);
            bool mouseHeld = useSecondaryFireForCameraMode && mouse != null && mouse.rightButton.isPressed;
            SetCameraModeActive(startInCameraMode || keyHeld || mouseHeld);
            return;
        }

        bool keyPressed = keyboard != null && WasPressed(keyboard, cameraModeKey);
        bool mousePressed = useSecondaryFireForCameraMode && mouse != null && mouse.rightButton.wasPressedThisFrame;
        if (keyPressed || mousePressed)
        {
            SetCameraModeActive(!cameraModeActive);
        }
    }

    private void SetCameraModeActive(bool active)
    {
        if (cameraModeActive == active)
        {
            return;
        }

        cameraModeActive = active;
        if (cameraModeActive)
        {
            StartCameraModeSlow();
        }
    }

    private void StartCameraModeSlow()
    {
        if (!Application.isPlaying || cameraModeSlowDuration <= 0f || cameraModeTimeScale <= 0f)
        {
            return;
        }

        if (!cameraModeSlowActive)
        {
            storedTimeScale = Time.timeScale;
            storedFixedDeltaTime = Time.fixedDeltaTime;
        }

        float slowScale = Mathf.Clamp(cameraModeTimeScale, 0.01f, 1f);
        float normalizedFixedDelta = storedTimeScale > 0.001f
            ? storedFixedDeltaTime / storedTimeScale
            : storedFixedDeltaTime;

        Time.timeScale = slowScale;
        Time.fixedDeltaTime = normalizedFixedDelta * slowScale;
        cameraModeSlowEndTime = Time.unscaledTime + Mathf.Max(0.01f, cameraModeSlowDuration);
        cameraModeSlowActive = true;
    }

    private void TickCameraModeSlow()
    {
        if (!cameraModeSlowActive || Time.unscaledTime < cameraModeSlowEndTime)
        {
            return;
        }

        RestoreCameraModeSlow();
    }

    private void RestoreCameraModeSlow()
    {
        if (!cameraModeSlowActive)
        {
            return;
        }

        Time.timeScale = storedTimeScale;
        Time.fixedDeltaTime = storedFixedDeltaTime;
        cameraModeSlowActive = false;
        cameraModeSlowEndTime = 0f;
    }

    private void CycleSelectedCameraAbility(int direction)
    {
        NormalizeSelectedCameraAbility();
        CameraAbilityId[] slots = { CameraAbilityId.Shutter, CameraAbilityId.Focus };
        int currentIndex = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == selectedCameraAbility)
            {
                currentIndex = i;
                break;
            }
        }

        int nextIndex = (currentIndex + direction) % slots.Length;
        if (nextIndex < 0)
        {
            nextIndex += slots.Length;
        }

        selectedCameraAbility = slots[nextIndex];
    }

    private void NormalizeSelectedCameraAbility()
    {
        if (selectedCameraAbility != CameraAbilityId.Shutter && selectedCameraAbility != CameraAbilityId.Focus)
        {
            selectedCameraAbility = CameraAbilityId.Shutter;
        }
    }

    private void TryUseSelectedCameraAbility()
    {
        switch (selectedCameraAbility)
        {
            case CameraAbilityId.Shutter:
                TryUseShutter();
                break;
            case CameraAbilityId.Focus:
                TryUseFocus();
                break;
        }
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
            return;
        }

        if (!CanUseCameraIntervention())
        {
            return;
        }

        if (!TryFindShutterTarget(out IShutterFreezable3D target, out Component targetComponent))
        {
            return;
        }

        if (IsMarked(targetComponent) || IsInRemarkCooldown(targetComponent))
        {
            return;
        }

        if (target.ApplyShutterFreeze(shutterFreezeDuration, this))
        {
            if (ConsumeCameraIntervention("Freeze object"))
            {
                StartShutterCooldown();
                MarkTarget(targetComponent, target);
            }
        }
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
            : FindBestDirectionalCollider(shutterRange, shutterBoxSize, ResolveShutterTarget);

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
            : FindBestDirectionalCollider(relayRange, relayBoxSize, ResolveRelayTarget);

        if (hit == null)
        {
            return false;
        }

        target = ResolveRelayTarget(hit);
        return target != null;
    }

    private Collider FindBestDirectionalCollider<T>(float range, Vector3 boxSize, Func<Collider, T> resolver) where T : class
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
            if (pair.Key == null || Time.time >= pair.Value.CooldownEndTime)
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

    private void SetupCameraFrame()
    {
        if (!showCameraFrame || frameCanvas != null)
        {
            return;
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

        modeLabel = CreateFrameText(parent, "Mode Label", GetCameraModeDisplayName(), new Vector2(0.91f, 0.76f), new Vector2(0f, 0f), new Vector2(132f, 42f), 24, frameColor, TextAnchor.MiddleCenter);
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

        bool visible = showCameraFrame && useMouseFrameTargeting && (!Application.isPlaying || cameraModeActive);
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

        if (modeLabel != null)
        {
            modeLabel.text = GetCameraModeDisplayName();
        }

        if (reticleRoot != null)
        {
            float reticleScale = Mathf.Clamp(frameRect.height / shutterFrameReferenceSize.y, 0.55f, 1.45f);
            reticleRoot.localScale = Vector3.one * reticleScale;
        }
    }

    private string GetCameraModeDisplayName()
    {
        switch (selectedCameraAbility)
        {
            case CameraAbilityId.Focus:
                return "FOCUS";
            default:
                return "SHUTTER";
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
        Vector3 screenPoint = camera.WorldToScreenPoint(worldPoint);
        return screenPoint.z > 0f && frameRect.Contains(new Vector2(screenPoint.x, screenPoint.y));
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

    private CameraLightFollower ResolveCameraLightFollower()
    {
        if (cameraLightFollower != null)
        {
            cameraLightFollower.Bind(GetTargetCamera(), flashLight);
            cameraLightFollower.SetPlayerTransform(transform);
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
            cameraWorldSwitcher.SetTargetCamera(GetTargetCamera());
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
            return;
        }

        GameObject lightObject = new GameObject("Camera Toggle Light", typeof(Light));
        CameraTagUtility3D.TrySetTag(lightObject, CameraTagUtility3D.LightTag);

        flashLight = lightObject.GetComponent<Light>();
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
