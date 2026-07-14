using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class MapPiece : MonoBehaviour
{
    [SerializeField] private MapPieceType pieceType;
    [SerializeField] private bool useCollider;
    [SerializeField] private bool autoSetZ = true;
    [SerializeField] private float zPosition;
    [SerializeField] private bool snapToGrid = true;
    [SerializeField] private float gridSize = 1f;
    [SerializeField] private bool isInteractable;
    [SerializeField] private bool blockMovement;
    [SerializeField] private bool blockLineOfSight;
    [SerializeField] private bool autoSetObstacleLayer = true;
    [SerializeField] private string obstacleLayerName = "EnvironmentObstacle";

    [Header("World Switch")]
    [SerializeField] private bool canSwitchWorld;
    [SerializeField] private WorldSwitchCategory worldSwitchCategory = WorldSwitchCategory.Default;
    [SerializeField] private WorldState currentWorldState = WorldState.Current;
    [SerializeField] private WorldState targetWorldState = WorldState.Past;
    [SerializeField] private bool autoConfigureWorldSwitchable = true;

    [Header("Optional Settings")]
    [SerializeField] private MapLayerSettings layerSettings;
    [SerializeField] private bool debugMode;

    [SerializeField, HideInInspector] private MapPieceType previousPieceType;
    [SerializeField, HideInInspector] private bool initialized;

    public MapPieceType PieceType => pieceType;
    public bool UseCollider => useCollider;
    public bool IsInteractable => isInteractable;
    public bool BlockMovement => blockMovement;
    public bool BlockLineOfSight => blockLineOfSight;
    public bool CanSwitchWorld => canSwitchWorld;
    public WorldSwitchCategory WorldSwitchCategory => worldSwitchCategory;
    public WorldState CurrentWorldState => currentWorldState;
    public WorldState TargetWorldState => targetWorldState;

    private void Reset()
    {
        initialized = true;
        previousPieceType = pieceType;
        ApplyRecommendedDefaults();
        ApplySettings();
    }

    private void Awake()
    {
        ApplySettings();
    }

    private void OnEnable()
    {
        ApplySettings();
    }

    private void OnValidate()
    {
        gridSize = Mathf.Max(0.01f, gridSize);

        if (!initialized)
        {
            initialized = true;
            previousPieceType = pieceType;
            ApplyRecommendedDefaults();
        }
        else if (previousPieceType != pieceType)
        {
            previousPieceType = pieceType;
            ApplyRecommendedDefaults();
        }

        ApplySettings();
    }

    [ContextMenu("Apply Recommended Defaults")]
    private void ApplyRecommendedDefaults()
    {
        zPosition = ResolveDefaultZ(pieceType);
        useCollider = GetDefaultColliderUse(pieceType);
        isInteractable = pieceType == MapPieceType.Object;
        blockMovement = GetDefaultBlocksMovement(pieceType);
        blockLineOfSight = GetDefaultBlocksLineOfSight(pieceType);
    }

    private void ApplySettings()
    {
        if (autoSetZ)
        {
            zPosition = ResolveDefaultZ(pieceType);
        }

        ApplyPositionRules();
        ApplyColliderRules();
        ApplyObstacleLayerRules();
        ApplyWorldSwitchRules();

        if (debugMode)
        {
            Debug.Log(
                $"[MapPiece] Applied {pieceType}, Collider={useCollider}, Z={zPosition:0.###}, Snap={snapToGrid}, Interactable={isInteractable}, BlockMovement={blockMovement}, BlockLOS={blockLineOfSight}, CanSwitchWorld={canSwitchWorld}",
                this);
        }
    }

    private void ApplyPositionRules()
    {
        Vector3 position = transform.position;

        if (snapToGrid)
        {
            position.x = Mathf.Round(position.x / gridSize) * gridSize;
            position.y = Mathf.Round(position.y / gridSize) * gridSize;
        }

        if (autoSetZ)
        {
            position.z = zPosition;
        }

        transform.position = position;
    }

    private void ApplyColliderRules()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);

        if (useCollider && colliders.Length == 0)
        {
            BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
            boxCollider.isTrigger = false;
            colliders = new Collider[] { boxCollider };
        }

        foreach (Collider targetCollider in colliders)
        {
            if (targetCollider == null)
            {
                continue;
            }

            targetCollider.enabled = useCollider;
            targetCollider.isTrigger = false;
        }
    }

    private void ApplyObstacleLayerRules()
    {
        if (!autoSetObstacleLayer || (!blockMovement && !blockLineOfSight) || string.IsNullOrWhiteSpace(obstacleLayerName))
        {
            return;
        }

        int obstacleLayer = LayerMask.NameToLayer(obstacleLayerName);
        if (obstacleLayer < 0)
        {
            if (debugMode)
            {
                Debug.LogWarning($"[MapPiece] Layer '{obstacleLayerName}' does not exist. Create it and assign this object to it.", this);
            }

            return;
        }

        SetLayerRecursive(transform, obstacleLayer);
    }

    private static void SetLayerRecursive(Transform target, int layer)
    {
        target.gameObject.layer = layer;
        for (int i = 0; i < target.childCount; i++)
        {
            SetLayerRecursive(target.GetChild(i), layer);
        }
    }

    private void ApplyWorldSwitchRules()
    {
        if (!autoConfigureWorldSwitchable)
        {
            return;
        }

        WorldSwitchable switchable = GetComponent<WorldSwitchable>();
        if (!canSwitchWorld)
        {
            if (switchable != null)
            {
                switchable.SetCanSwitchByCamera(false);
            }

            return;
        }

        if (switchable == null)
        {
            switchable = gameObject.AddComponent<WorldSwitchable>();
        }

        switchable.SetCanSwitchByCamera(true);
        switchable.ConfigureSimpleSwitchPair(ToResearchWorldId(currentWorldState), ToResearchWorldId(targetWorldState));
    }

    private float ResolveDefaultZ(MapPieceType type)
    {
        return layerSettings != null ? layerSettings.GetZ(type) : GetFallbackDefaultZ(type);
    }

    private static float GetFallbackDefaultZ(MapPieceType type)
    {
        switch (type)
        {
            case MapPieceType.BackgroundWall:
                return 2f;
            case MapPieceType.Decoration:
                return 1f;
            case MapPieceType.Object:
                return -0.2f;
            case MapPieceType.Floor:
            case MapPieceType.Wall:
            case MapPieceType.Tile:
            case MapPieceType.Platform:
            case MapPieceType.Structure:
            default:
                return TwoPointFiveDUtility3D.GameplayPlaneZ;
        }
    }

    private static bool GetDefaultColliderUse(MapPieceType type)
    {
        switch (type)
        {
            case MapPieceType.Floor:
            case MapPieceType.Wall:
            case MapPieceType.Tile:
            case MapPieceType.Platform:
            case MapPieceType.Structure:
                return true;
            case MapPieceType.BackgroundWall:
            case MapPieceType.Decoration:
                return false;
            case MapPieceType.Object:
            default:
                return false;
        }
    }

    private static bool GetDefaultBlocksMovement(MapPieceType type)
    {
        switch (type)
        {
            case MapPieceType.Floor:
            case MapPieceType.Wall:
            case MapPieceType.Tile:
            case MapPieceType.Platform:
            case MapPieceType.Structure:
                return true;
            default:
                return false;
        }
    }

    private static bool GetDefaultBlocksLineOfSight(MapPieceType type)
    {
        switch (type)
        {
            case MapPieceType.Floor:
            case MapPieceType.Wall:
            case MapPieceType.Tile:
            case MapPieceType.Platform:
            case MapPieceType.Structure:
                return true;
            default:
                return false;
        }
    }

    private static ResearchWorldId ToResearchWorldId(WorldState state)
    {
        return state == WorldState.Current ? ResearchWorldId.WorldA : ResearchWorldId.WorldB;
    }
}
