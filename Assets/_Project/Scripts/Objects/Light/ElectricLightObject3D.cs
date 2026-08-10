using UnityEngine;

public enum ElectricLightState
{
    ACTIVE = 0,
    DESTROYED = 1
}

[DisallowMultipleComponent]
public sealed class ElectricLightObject3D : MonoBehaviour, IDamageable
{
    [Header("Gameplay Light (Player Light Compatible)")]
    [SerializeField] private Light gameplayLight;
    [SerializeField, Min(0f)] private float gameplayRange = 6.5f;
    [SerializeField, Min(0f)] private float gameplayIntensity = 7.5f;
    [SerializeField] private Color gameplayColor = new Color(0.78f, 0.95f, 1f, 1f);

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

    public ElectricLightState CurrentState => currentState;
    public int CurrentHP => currentHP;
    public int MaxHP => maxHP;
    public bool IsProvidingLight => currentState == ElectricLightState.ACTIVE &&
                                    isActiveAndEnabled &&
                                    IsPresentInCurrentWorld() &&
                                    gameplayLight != null &&
                                    gameplayLight.isActiveAndEnabled &&
                                    gameplayLight.intensity > 0f;
    public bool CanTakeDamage => currentState == ElectricLightState.ACTIVE &&
                                 currentHP > 0 &&
                                 isActiveAndEnabled &&
                                 IsPresentInCurrentWorld();

    private void Awake()
    {
        CacheReferences();
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
    }

    private void OnDisable()
    {
        SetLightOutput(false);
    }

    private void OnValidate()
    {
        maxHP = Mathf.Max(1, maxHP);
        gameplayRange = Mathf.Max(0f, gameplayRange);
        gameplayIntensity = Mathf.Max(0f, gameplayIntensity);
        currentHP = Application.isPlaying
            ? Mathf.Clamp(currentHP, 0, maxHP)
            : maxHP;
        CacheReferences();
        ApplyLightSettings();
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
        gameplayLight.range = gameplayRange;
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

        if (damageColliders == null || damageColliders.Length == 0)
        {
            damageColliders = GetComponentsInChildren<Collider>(true);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showRangeGizmo)
        {
            return;
        }

        Light source = gameplayLight != null ? gameplayLight : GetComponentInChildren<Light>(true);
        Vector3 center = source != null ? source.transform.position : transform.position;
        float range = source != null ? source.range : gameplayRange;
        Gizmos.color = new Color(1f, 0.9f, 0.25f, 0.8f);
        Gizmos.DrawWireSphere(center, range);
    }
}
