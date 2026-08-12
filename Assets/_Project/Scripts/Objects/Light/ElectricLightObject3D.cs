using UnityEngine;
using UnityEngine.Serialization;

public enum ElectricLightState
{
    ACTIVE = 0,
    DESTROYED = 1
}

[DisallowMultipleComponent]
public sealed class ElectricLightObject3D : MonoBehaviour, IDamageable, IGameplayLightSource3D
{
    [Header("Gameplay Light (Player Light Compatible)")]
    [SerializeField] private Light gameplayLight;
    [SerializeField, Min(0f)] private float gameplayIntensity = 7.5f;
    [SerializeField] private Color gameplayColor = new Color(0.78f, 0.95f, 1f, 1f);

    [Header("Directional Cone")]
    [SerializeField, Tooltip("Legacy prefab compatibility. Off uses the original radial Point Light range.")]
    private bool useDirectionalCone = true;
    [SerializeField] private Transform coneOrigin;
    [SerializeField] private Transform directionTransform;
    [FormerlySerializedAs("gameplayRange")]
    [SerializeField, Min(0f)] private float lightRange = 4f;
    [SerializeField, Range(1f, 179f)] private float lightAngle = 90f;
    [SerializeField] private LayerMask occlusionLayerMask = (1 << 9) | (1 << 10) | (1 << 11);
    [SerializeField] private Transform coneVisual;
    [SerializeField] private Color coneColor = new Color(1f, 0.86f, 0.42f, 0.28f);
    [SerializeField] private string coneSortingLayer = "Default";
    [SerializeField] private int coneSortingOrder;
    [SerializeField, Range(3, 64)] private int coneSegments = 24;

    [Header("Durability")]
    [SerializeField, Min(1), Tooltip("Temporary balance value. Adjust this in the prefab Inspector when the design value is fixed.")]
    private int maxHP = 3;
    [SerializeField] private Collider[] damageColliders = System.Array.Empty<Collider>();

    [Header("Optional Visuals")]
    [SerializeField] private GameObject activeVisualRoot;

    [Header("Runtime Debug (Read Only)")]
    [SerializeField] private ElectricLightState currentState = ElectricLightState.ACTIVE;
    [SerializeField] private int currentHP;
    [SerializeField] private string lastDamageResult = "None";
    [SerializeField] private bool gameplayLightActive;

    [Header("Editor")]
    [SerializeField] private bool showRangeGizmo = true;

    private WorldPresence worldPresence;
    private bool initialized;
    private Mesh coneMesh;
    private MeshRenderer coneRenderer;
    private Material coneMaterial;
    private readonly RaycastHit[] occlusionHits = new RaycastHit[16];
    private Vector3 cachedOriginPosition;
    private Quaternion cachedOriginRotation;
    private Quaternion cachedDirectionRotation;
    private float cachedRange = -1f;
    private float cachedAngle = -1f;

    public ElectricLightState CurrentState => currentState;
    public Transform LightSourceTransform => coneOrigin != null ? coneOrigin : transform;
    public int CurrentHP => currentHP;
    public int MaxHP => maxHP;
    public bool IsProvidingLight => currentState == ElectricLightState.ACTIVE &&
                                    isActiveAndEnabled &&
                                    IsPresentInCurrentWorld() &&
                                    gameplayLight != null &&
                                    gameplayLight.isActiveAndEnabled &&
                                    gameplayLight.intensity > 0f;
    public float LightRange => lightRange;
    public float LightAngle => lightAngle;
    public Transform ConeOrigin => LightSourceTransform;
    public Vector3 ConeDirection => GetConeDirection();
    public bool CanTakeDamage => currentState == ElectricLightState.ACTIVE &&
                                 currentHP > 0 &&
                                 isActiveAndEnabled &&
                                 IsPresentInCurrentWorld();

    private void Awake()
    {
        CacheReferences();
        InitializeConeVisualResources();
        RefreshConeVisual(true);
        currentHP = maxHP;
        currentState = ElectricLightState.ACTIVE;
        initialized = true;
        ApplyState();
    }

    private void OnEnable()
    {
        CacheReferences();
        if (initialized)
        {
            ApplyState();
        }
        RefreshConeVisual(true);
    }

    private void OnDisable()
    {
        SetLightOutput(false);
        SetConeVisible(false);
    }

    private void LateUpdate() => RefreshConeVisual(false);

    private void OnDestroy()
    {
        if (coneMaterial != null) Destroy(coneMaterial);
        if (coneMesh != null) Destroy(coneMesh);
    }

    private void OnValidate()
    {
        maxHP = Mathf.Max(1, maxHP);
        gameplayIntensity = Mathf.Max(0f, gameplayIntensity);
        lightRange = Mathf.Max(0f, lightRange);
        lightAngle = Mathf.Clamp(lightAngle, 1f, 179f);
        coneSegments = Mathf.Clamp(coneSegments, 3, 64);
        currentHP = Application.isPlaying
            ? Mathf.Clamp(currentHP, 0, maxHP)
            : maxHP;
        CacheReferences();
        ApplyLightSettings();
        TryBindConeVisualComponents();
        RefreshConeVisual(true);
    }

    public bool IsIlluminating(Vector3 targetPosition)
    {
        if (!IsProvidingLight || !SafeMath3D.IsFinite(targetPosition)) return false;
        Transform originTransform = LightSourceTransform;
        Vector3 origin = originTransform.position;
        Vector3 delta = targetPosition - origin;
        delta.z = 0f;
        float sqrDistance = delta.sqrMagnitude;
        if (sqrDistance > lightRange * lightRange) return false;
        if (!useDirectionalCone) return !IsOccluded(origin, targetPosition);
        if (sqrDistance > 0.000001f)
        {
            Vector3 forward = GetConeDirection();
            if (Vector3.Dot(forward, delta / Mathf.Sqrt(sqrDistance)) <
                Mathf.Cos(lightAngle * 0.5f * Mathf.Deg2Rad)) return false;
        }
        return !IsOccluded(origin, targetPosition);
    }

    public void TakeDamage(int damage)
    {
        if (damage > 0)
        {
            lastDamageResult = "Ignored: source information is required";
        }
    }

    public void TakeDamage(DamageInfo damageInfo)
    {
        if (!CanTakeDamage)
        {
            lastDamageResult = "Ignored: inactive or destroyed";
            return;
        }

        HitSourceType sourceType = damageInfo.hitSourceType == HitSourceType.None
            ? DamageInfo.ToHitSourceType(damageInfo.damageType)
            : damageInfo.hitSourceType;
        if (sourceType != HitSourceType.EyeballFlyAttack)
        {
            lastDamageResult = "Ignored: " + sourceType;
            return;
        }

        int damage = Mathf.Max(0, damageInfo.damageAmount);
        if (damage == 0)
        {
            lastDamageResult = "Ignored: zero damage";
            return;
        }

        currentHP = Mathf.Max(0, currentHP - damage);
        lastDamageResult = "EyeballFlyAttack -" + damage;
        if (currentHP == 0)
        {
            EnterDestroyedState();
        }
    }

    public void EnterDestroyedState()
    {
        if (currentState == ElectricLightState.DESTROYED)
        {
            return;
        }

        currentHP = 0;
        currentState = ElectricLightState.DESTROYED;
        ApplyState();
    }

    private void ApplyState()
    {
        bool active = currentState == ElectricLightState.ACTIVE && IsPresentInCurrentWorld();
        SetLightOutput(active);
        SetConeVisible(active && useDirectionalCone);
        SetDamageCollidersEnabled(active);

        if (activeVisualRoot != null)
        {
            activeVisualRoot.SetActive(active);
        }
    }

    private void SetLightOutput(bool active)
    {
        if (gameplayLight == null)
        {
            gameplayLightActive = false;
            return;
        }

        ApplyLightSettings();
        gameplayLight.enabled = active;
        gameplayLightActive = active && gameplayLight.intensity > 0f;
    }

    private void SetDamageCollidersEnabled(bool active)
    {
        if (damageColliders == null)
        {
            return;
        }

        for (int i = 0; i < damageColliders.Length; i++)
        {
            Collider target = damageColliders[i];
            if (target != null)
            {
                target.enabled = active;
            }
        }
    }

    private void ApplyLightSettings()
    {
        if (gameplayLight == null)
        {
            return;
        }

        gameplayLight.type = LightType.Point;
        gameplayLight.range = lightRange;
        gameplayLight.intensity = gameplayIntensity;
        gameplayLight.color = gameplayColor;
    }

    private bool IsPresentInCurrentWorld()
    {
        return worldPresence == null || worldPresence.IsPresentInCurrentWorld;
    }

    private void CacheReferences()
    {
        if (gameplayLight == null)
        {
            gameplayLight = GetComponentInChildren<Light>(true);
        }

        if (worldPresence == null)
        {
            worldPresence = GetComponent<WorldPresence>();
        }

        if (coneOrigin == null) coneOrigin = transform;
        if (directionTransform == null) directionTransform = coneOrigin;

        if (damageColliders == null || damageColliders.Length == 0)
        {
            damageColliders = GetComponentsInChildren<Collider>(true);
        }
    }

    private Vector3 GetConeDirection()
    {
        Transform basis = directionTransform != null ? directionTransform : LightSourceTransform;
        Vector3 direction = -basis.up;
        direction.z = 0f;
        return direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector3.right;
    }

    private bool IsOccluded(Vector3 origin, Vector3 target)
    {
        Vector3 delta = target - origin;
        float distance = delta.magnitude;
        if (distance <= 0.001f || occlusionLayerMask.value == 0) return false;
        int count = Physics.RaycastNonAlloc(origin, delta / distance, occlusionHits, distance,
            ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider hit = occlusionHits[i].collider;
            if (hit == null || hit.transform.IsChildOf(transform)) continue;
            bool layerBlocks = (occlusionLayerMask.value & (1 << hit.gameObject.layer)) != 0;
            MapPiece mapPiece = hit.GetComponentInParent<MapPiece>();
            if (layerBlocks || (mapPiece != null && mapPiece.BlockLineOfSight)) return true;
        }
        return false;
    }

    private void TryBindConeVisualComponents()
    {
        if (coneVisual == null)
        {
            Transform existing = LightSourceTransform.Find("Electric Light Cone");
            if (existing != null) coneVisual = existing;
        }
        if (coneVisual == null) return;
        MeshFilter filter = coneVisual.GetComponent<MeshFilter>();
        coneRenderer = coneVisual.GetComponent<MeshRenderer>();
        if (filter == null || coneRenderer == null) return;
    }

    private void InitializeConeVisualResources()
    {
        TryBindConeVisualComponents();
        if (coneVisual == null || coneRenderer == null) return;
        MeshFilter filter = coneVisual.GetComponent<MeshFilter>();
        if (filter == null) return;
        if (coneMesh == null)
        {
            coneMesh = new Mesh { name = "Electric Light Cone Mesh" };
            filter.sharedMesh = coneMesh;
        }
        if (coneMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null) coneMaterial = new Material(shader) { name = "Electric Light Cone Material" };
            coneRenderer.sharedMaterial = coneMaterial;
        }
    }

    private void RefreshConeVisual(bool force)
    {
        CacheReferences();
        if (!useDirectionalCone)
        {
            SetConeVisible(false);
            return;
        }
        if (Application.isPlaying && (coneMesh == null || coneRenderer == null))
        {
            InitializeConeVisualResources();
        }
        if (coneMesh == null || coneRenderer == null) return;
        Transform origin = LightSourceTransform;
        Quaternion directionRotation = directionTransform != null ? directionTransform.rotation : origin.rotation;
        if (!force && cachedOriginPosition == origin.position && cachedOriginRotation == origin.rotation &&
            cachedDirectionRotation == directionRotation && Mathf.Approximately(cachedRange, lightRange) &&
            Mathf.Approximately(cachedAngle, lightAngle)) return;

        int vertexCount = coneSegments + 2;
        Vector3[] vertices = new Vector3[vertexCount];
        int[] triangles = new int[coneSegments * 3];
        Vector3 localDirection = origin.InverseTransformDirection(GetConeDirection());
        float baseAngle = Mathf.Atan2(localDirection.y, localDirection.x) * Mathf.Rad2Deg;
        vertices[0] = Vector3.zero;
        for (int i = 0; i <= coneSegments; i++)
        {
            float angle = baseAngle - lightAngle * 0.5f + lightAngle * i / coneSegments;
            float radians = angle * Mathf.Deg2Rad;
            vertices[i + 1] = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f) * lightRange;
            if (i == coneSegments) continue;
            int triangle = i * 3;
            triangles[triangle] = 0;
            triangles[triangle + 1] = i + 1;
            triangles[triangle + 2] = i + 2;
        }
        coneMesh.Clear();
        coneMesh.vertices = vertices;
        coneMesh.triangles = triangles;
        coneMesh.RecalculateBounds();
        if (coneMaterial != null) coneMaterial.color = coneColor;
        coneRenderer.sortingLayerName = coneSortingLayer;
        coneRenderer.sortingOrder = coneSortingOrder;
        SetConeVisible(useDirectionalCone && currentState == ElectricLightState.ACTIVE && IsPresentInCurrentWorld());
        cachedOriginPosition = origin.position;
        cachedOriginRotation = origin.rotation;
        cachedDirectionRotation = directionRotation;
        cachedRange = lightRange;
        cachedAngle = lightAngle;
    }

#if UNITY_EDITOR
    public void RefreshEditorConeVisual()
    {
        if (Application.isPlaying) return;
        CacheReferences();
        InitializeConeVisualResources();
        ApplyLightSettings();
        RefreshConeVisual(true);
    }
#endif

    private void SetConeVisible(bool visible)
    {
        if (coneRenderer != null) coneRenderer.enabled = visible;
    }

    private void OnDrawGizmosSelected()
    {
        if (!showRangeGizmo)
        {
            return;
        }

        Vector3 center = LightSourceTransform.position;
        Vector3 direction = GetConeDirection();
        Gizmos.color = new Color(1f, 0.9f, 0.25f, 0.8f);
        if (!useDirectionalCone)
        {
            Gizmos.DrawWireSphere(center, lightRange);
            return;
        }
        Gizmos.DrawLine(center, center + Quaternion.AngleAxis(-lightAngle * 0.5f, Vector3.forward) * direction * lightRange);
        Gizmos.DrawLine(center, center + Quaternion.AngleAxis(lightAngle * 0.5f, Vector3.forward) * direction * lightRange);
        Gizmos.DrawLine(center, center + direction * lightRange);
        Vector3 previous = center + Quaternion.AngleAxis(-lightAngle * 0.5f, Vector3.forward) * direction * lightRange;
        for (int i = 1; i <= coneSegments; i++)
        {
            float angle = -lightAngle * 0.5f + lightAngle * i / coneSegments;
            Vector3 next = center + Quaternion.AngleAxis(angle, Vector3.forward) * direction * lightRange;
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
}
