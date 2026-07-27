using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class CosmosLift3D : MonoBehaviour
{
    public enum LiftState
    {
        Retracted,
        Growing,
        Raised,
        Holding,
        Retracting
    }

    [Header("Lift Parts")]
    [SerializeField] private Transform budPlatform;
    [SerializeField] private Rigidbody budRigidbody;
    [SerializeField] private Collider platformCollider;
    [SerializeField] private Transform stemVisual;
    [SerializeField] private Transform lightReceiver;

    [Header("Movement")]
    [Min(0f)] [SerializeField] private float maximumHeight = 5f;
    [Min(0.01f)] [SerializeField] private float riseDuration = 1.5f;
    [Min(0.01f)] [SerializeField] private float retractDuration = 1.25f;
    [Min(0f)] [SerializeField] private float darknessHoldDuration = 1f;
    [SerializeField] private AnimationCurve riseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve retractCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Range(0f, 1f)] [SerializeField] private float platformEnableProgress = 0.08f;

    [Header("Light Detection")]
    [Tooltip("Optional explicit lights. Active scene lights are also detected, including the runtime camera light.")]
    [SerializeField] private Light[] additionalLights = new Light[0];
    [Min(0.02f)] [SerializeField] private float lightScanInterval = 0.15f;
    [Min(0f)] [SerializeField] private float receiverRadius = 0.3f;
    [SerializeField] private bool requireLineOfSight;
    [SerializeField] private LayerMask lightBlockingMask;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [Tooltip("Optional animator state sampled from normalized time 0 (closed) to 1 (open).")]
    [SerializeField] private string growthStateName;
    [SerializeField] private string growthFloatParameter = "Growth";

    [Header("Events")]
    [SerializeField] private UnityEvent onRiseStarted;
    [SerializeField] private UnityEvent onFullyRaised;
    [SerializeField] private UnityEvent onRetractStarted;
    [SerializeField] private UnityEvent onFullyRetracted;

    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private LiftState currentState = LiftState.Retracted;
    [Range(0f, 1f)] [SerializeField] private float growthProgress;
    [SerializeField] private bool isReceivingLight;

    private readonly List<Light> sceneLights = new List<Light>();
    private Vector3 retractedLocalPosition;
    private Vector3 stemRetractedScale;
    private Vector3 stemRetractedLocalPosition;
    private float darknessTimer;
    private float nextLightScanTime;
    private int growthParameterHash;
    private bool hasGrowthParameter;
    private bool externalLightSignal;

    public LiftState CurrentState => currentState;
    public float GrowthProgress => growthProgress;
    public bool IsReceivingLight => isReceivingLight;
    public float MaximumHeight => maximumHeight;

    private void Reset()
    {
        AutoFillReferences();
    }

    private void Awake()
    {
        AutoFillReferences();
        SanitizeValues();
        CaptureRetractedPose();
        ConfigurePhysics();
        CacheAnimatorParameter();
        RefreshSceneLights();
        ApplyPose(0f, true);
    }

    private void OnValidate()
    {
        SanitizeValues();
        AutoFillReferences();
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextLightScanTime)
        {
            RefreshSceneLights();
            nextLightScanTime = Time.unscaledTime + lightScanInterval;
        }

        isReceivingLight = externalLightSignal || DetectLight();
        externalLightSignal = false;
        UpdateState(Time.deltaTime);
        UpdateAnimator();
    }

    private void FixedUpdate()
    {
        ApplyPose(growthProgress, false);
    }

    public void SetMaximumHeight(float height)
    {
        maximumHeight = Mathf.Max(0f, height);
        ApplyPose(growthProgress, true);
    }

    public void SetLightReceived(bool received)
    {
        externalLightSignal = received;
        isReceivingLight = received;
        if (received)
        {
            BeginGrowing();
        }
    }

    [ContextMenu("Validate Cosmos Lift Setup")]
    public void ValidateSetup()
    {
        bool valid = budPlatform != null && budRigidbody != null && platformCollider != null;
        Debug.Log($"[CosmosLift3D] Valid={valid}, Bud={budPlatform != null}, Rigidbody3D={budRigidbody != null}, Collider3D={platformCollider != null}, Height={maximumHeight:0.##}", this);
    }

    private void UpdateState(float deltaTime)
    {
        if (isReceivingLight)
        {
            darknessTimer = 0f;
            if (currentState == LiftState.Retracted || currentState == LiftState.Holding || currentState == LiftState.Retracting)
            {
                BeginGrowing();
            }

            if (currentState == LiftState.Growing)
            {
                growthProgress = Mathf.MoveTowards(growthProgress, 1f, deltaTime / riseDuration);
                if (growthProgress >= 1f)
                {
                    currentState = LiftState.Raised;
                    onFullyRaised?.Invoke();
                }
            }

            return;
        }

        if (currentState == LiftState.Growing || currentState == LiftState.Raised)
        {
            currentState = LiftState.Holding;
            darknessTimer = 0f;
        }

        if (currentState == LiftState.Holding)
        {
            darknessTimer += deltaTime;
            if (darknessTimer >= darknessHoldDuration)
            {
                currentState = LiftState.Retracting;
                onRetractStarted?.Invoke();
            }
        }

        if (currentState != LiftState.Retracting)
        {
            return;
        }

        growthProgress = Mathf.MoveTowards(growthProgress, 0f, deltaTime / retractDuration);
        if (growthProgress <= 0f)
        {
            currentState = LiftState.Retracted;
            onFullyRetracted?.Invoke();
        }
    }

    private void BeginGrowing()
    {
        if (currentState == LiftState.Growing || currentState == LiftState.Raised)
        {
            return;
        }

        currentState = LiftState.Growing;
        darknessTimer = 0f;
        onRiseStarted?.Invoke();
    }

    private void ApplyPose(float progress, bool immediate)
    {
        if (budPlatform == null)
        {
            return;
        }

        float curvedProgress = currentState == LiftState.Retracting
            ? retractCurve.Evaluate(progress)
            : riseCurve.Evaluate(progress);
        Vector3 targetLocalPosition = retractedLocalPosition + Vector3.up * (maximumHeight * curvedProgress);

        if (budRigidbody != null && !immediate && Application.isPlaying)
        {
            Vector3 worldTarget = budPlatform.parent != null
                ? budPlatform.parent.TransformPoint(targetLocalPosition)
                : targetLocalPosition;
            budRigidbody.MovePosition(worldTarget);
        }
        else
        {
            budPlatform.localPosition = targetLocalPosition;
        }

        if (stemVisual != null)
        {
            Vector3 scale = stemRetractedScale;
            scale.y = stemRetractedScale.y + maximumHeight * curvedProgress;
            stemVisual.localScale = scale;
            Vector3 stemPosition = stemRetractedLocalPosition;
            stemPosition.y += maximumHeight * curvedProgress * 0.5f;
            stemVisual.localPosition = stemPosition;
        }

        if (platformCollider != null)
        {
            platformCollider.enabled = progress >= platformEnableProgress;
        }
    }

    private bool DetectLight()
    {
        Vector3 point = lightReceiver != null ? lightReceiver.position : transform.position;
        for (int i = 0; i < sceneLights.Count; i++)
        {
            if (LightReaches(sceneLights[i], point))
            {
                return true;
            }
        }

        Light[] explicitLights = additionalLights ?? new Light[0];
        for (int i = 0; i < explicitLights.Length; i++)
        {
            if (LightReaches(explicitLights[i], point))
            {
                return true;
            }
        }

        return false;
    }

    private bool LightReaches(Light light, Vector3 receiverPoint)
    {
        if (light == null || !light.isActiveAndEnabled || light.intensity <= 0f)
        {
            return false;
        }

        if (light.type != LightType.Directional)
        {
            Vector3 toReceiver = receiverPoint - light.transform.position;
            float allowedRange = light.range + receiverRadius;
            if (toReceiver.sqrMagnitude > allowedRange * allowedRange)
            {
                return false;
            }

            if (light.type == LightType.Spot && toReceiver.sqrMagnitude > 0.0001f)
            {
                float minimumDot = Mathf.Cos(light.spotAngle * 0.5f * Mathf.Deg2Rad);
                if (Vector3.Dot(light.transform.forward, toReceiver.normalized) < minimumDot)
                {
                    return false;
                }
            }
        }

        if (!requireLineOfSight || light.type == LightType.Directional)
        {
            return true;
        }

        Vector3 origin = light.transform.position;
        Vector3 direction = receiverPoint - origin;
        float distance = direction.magnitude;
        return distance <= 0.001f || !Physics.Raycast(origin, direction / distance, distance, lightBlockingMask, QueryTriggerInteraction.Ignore);
    }

    private void RefreshSceneLights()
    {
        sceneLights.Clear();
        Light[] found = FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null && CameraTagUtility3D.HasAnyTag(found[i], CameraTagUtility3D.LightTag))
            {
                sceneLights.Add(found[i]);
            }
        }
    }

    private void ConfigurePhysics()
    {
        if (budRigidbody == null)
        {
            return;
        }

        budRigidbody.isKinematic = true;
        budRigidbody.useGravity = false;
        budRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        budRigidbody.constraints |= RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
    }

    private void CaptureRetractedPose()
    {
        if (budPlatform != null)
        {
            retractedLocalPosition = budPlatform.localPosition;
        }

        if (stemVisual != null)
        {
            stemRetractedScale = stemVisual.localScale;
            stemRetractedLocalPosition = stemVisual.localPosition;
        }
    }

    private void UpdateAnimator()
    {
        if (animator == null)
        {
            return;
        }

        if (hasGrowthParameter)
        {
            animator.SetFloat(growthParameterHash, growthProgress);
        }

        if (!string.IsNullOrWhiteSpace(growthStateName))
        {
            animator.speed = 0f;
            animator.Play(growthStateName, 0, growthProgress);
            animator.Update(0f);
        }
    }

    private void CacheAnimatorParameter()
    {
        hasGrowthParameter = false;
        if (animator == null || string.IsNullOrWhiteSpace(growthFloatParameter))
        {
            return;
        }

        growthParameterHash = Animator.StringToHash(growthFloatParameter);
        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].nameHash == growthParameterHash && parameters[i].type == AnimatorControllerParameterType.Float)
            {
                hasGrowthParameter = true;
                break;
            }
        }
    }

    private void AutoFillReferences()
    {
        if (budPlatform == null && transform.childCount > 0)
        {
            budPlatform = transform.GetChild(0);
        }
        if (budRigidbody == null && budPlatform != null) budRigidbody = budPlatform.GetComponent<Rigidbody>();
        if (platformCollider == null && budPlatform != null) platformCollider = budPlatform.GetComponent<Collider>();
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        if (lightReceiver == null) lightReceiver = transform;
    }

    private void SanitizeValues()
    {
        maximumHeight = Mathf.Max(0f, maximumHeight);
        riseDuration = Mathf.Max(0.01f, riseDuration);
        retractDuration = Mathf.Max(0.01f, retractDuration);
        darknessHoldDuration = Mathf.Max(0f, darknessHoldDuration);
        lightScanInterval = Mathf.Max(0.02f, lightScanInterval);
        receiverRadius = Mathf.Max(0f, receiverRadius);
        platformEnableProgress = Mathf.Clamp01(platformEnableProgress);
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos)
        {
            return;
        }

        Transform receiver = lightReceiver != null ? lightReceiver : transform;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(receiver.position, receiverRadius);

        if (budPlatform != null)
        {
            Vector3 start = Application.isPlaying
                ? (budPlatform.parent != null ? budPlatform.parent.TransformPoint(retractedLocalPosition) : retractedLocalPosition)
                : budPlatform.position;
            Gizmos.color = new Color(0.8f, 0.35f, 1f, 1f);
            Gizmos.DrawLine(start, start + transform.up * maximumHeight);
            Gizmos.DrawWireSphere(start + transform.up * maximumHeight, 0.2f);
        }
    }
}
