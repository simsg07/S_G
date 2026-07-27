using UnityEngine;

[DisallowMultipleComponent]
public sealed class DionaeaLightReceiver : MonoBehaviour
{
    [Header("Light")]
    [SerializeField] private LayerMask lightLayerMask;
    [SerializeField] private string lightTag = "Light";
    [SerializeField] private bool useTagFallback = true;
    [SerializeField, Tooltip("Detect active Point/Spot lights even when they have no Collider, including the camera toggle light.")]
    private bool detectUncollideredLights = true;
    [SerializeField] private float lightSearchRefreshInterval = 0.25f;
    [SerializeField] private Vector3 receiverBoxSize = new Vector3(1.2f, 1.5f, 0.8f);
    [SerializeField] private bool isReceivingLight;
    [SerializeField] private float currentLightTime;
    [SerializeField] private float requiredLightTime = 1f;
    [SerializeField] private DionaeaAI dionaeaAI;
    [SerializeField] private bool debugMode;
    [SerializeField] private bool showGizmo = true;

    private readonly Collider[] hits = new Collider[16];
    private bool externalLightSignal;
    private Light[] cachedSceneLights = System.Array.Empty<Light>();
    private float nextLightSearchTime;

    public bool IsReceivingLight => isReceivingLight;
    public float CurrentLightTime => currentLightTime;

    private void Awake()
    {
        AutoFill();
        RefreshSceneLights();
        if (dionaeaAI == null)
        {
            Debug.LogWarning("[DionaeaLightReceiver] DionaeaAI is not assigned. Light state will be tracked without forwarding it.", this);
        }
    }
    private void OnValidate() { requiredLightTime = Mathf.Max(0f, requiredLightTime); lightSearchRefreshInterval = Mathf.Max(0.05f, lightSearchRefreshInterval); receiverBoxSize = MonsterRuntime3D.ClampSize(receiverBoxSize, 0.01f); AutoFill(); }

    private void Update()
    {
        bool received = externalLightSignal || DetectLightOverlap();
        isReceivingLight = received;
        if (received) AddLightExposure(Time.deltaTime); else ResetLightExposure();
        if (dionaeaAI != null) dionaeaAI.SetLit(received);
        externalLightSignal = false;
    }

    public void SetLightReceived(bool received)
    {
        externalLightSignal = received;
        isReceivingLight = received;
        if (dionaeaAI != null) dionaeaAI.SetLit(received);
    }

    public void AddLightExposure(float deltaTime)
    {
        currentLightTime += Mathf.Max(0f, deltaTime);
        if (currentLightTime >= requiredLightTime && dionaeaAI != null) dionaeaAI.StartRetractFromLight();
    }

    public void ResetLightExposure() => currentLightTime = 0f;

    [ContextMenu("Validate Light Receiver Setup")]
    public void ValidateLightReceiverSetup() => Debug.Log($"[DionaeaLightReceiver] AI={dionaeaAI != null}, Mask={lightLayerMask.value}, Tag={lightTag}", this);

    public void Configure(DionaeaAI ai, float requiredTime) { dionaeaAI = ai; requiredLightTime = Mathf.Max(0f, requiredTime); }

    private bool DetectLightOverlap()
    {
        int mask = lightLayerMask.value == 0 ? ~0 : lightLayerMask.value;
        int count = Physics.OverlapBoxNonAlloc(transform.position, receiverBoxSize * 0.5f, hits,
            Quaternion.identity, mask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            Collider hit = hits[i];
            if (hit == null || hit.transform.IsChildOf(transform)) continue;
            Light light = hit.GetComponentInParent<Light>();
            if (light != null && LightReaches(light)) return true;
            if (useTagFallback && HasTagSafely(hit.gameObject, lightTag)) return true;
        }

        MonsterCore core = GetComponentInParent<MonsterCore>();
        Light assigned = core != null && core.lightTarget != null ? core.lightTarget.GetComponentInChildren<Light>(true) : null;
        if (assigned != null && LightReaches(assigned)) return true;

        if (!detectUncollideredLights) return false;
        if (Time.unscaledTime >= nextLightSearchTime) RefreshSceneLights();
        for (int i = 0; i < cachedSceneLights.Length; i++)
        {
            Light sceneLight = cachedSceneLights[i];
            // Directional scene lighting is ambient illumination, not a gameplay light signal.
            if (sceneLight != null && sceneLight.type != LightType.Directional && LightReaches(sceneLight)) return true;
        }
        return false;
    }

    private void RefreshSceneLights()
    {
        cachedSceneLights = FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        nextLightSearchTime = Time.unscaledTime + lightSearchRefreshInterval;
    }

    private bool LightReaches(Light light)
    {
        if (!light.isActiveAndEnabled || light.intensity <= 0f) return false;
        if (light.type == LightType.Directional) return true;
        Vector3 delta = transform.position - light.transform.position;
        if (delta.sqrMagnitude > light.range * light.range) return false;
        if (light.type != LightType.Spot) return true;
        return Vector3.Dot(light.transform.forward, delta.normalized) >= Mathf.Cos(light.spotAngle * 0.5f * Mathf.Deg2Rad);
    }

    private static bool HasTagSafely(GameObject candidate, string tagName)
    {
        if (candidate == null || string.IsNullOrWhiteSpace(tagName)) return false;
        return candidate.tag == tagName;
    }

    private void AutoFill() { if (dionaeaAI == null) dionaeaAI = GetComponentInParent<DionaeaAI>(); }
    private void OnDrawGizmosSelected() { if (showGizmo) { Gizmos.color = Color.yellow; Gizmos.DrawWireCube(transform.position, receiverBoxSize); } }
}
