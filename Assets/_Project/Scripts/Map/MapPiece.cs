using UnityEngine;
using UnityEngine.Serialization;

[ExecuteAlways]
[DisallowMultipleComponent]
public class MapPiece : MonoBehaviour
{
    [Header("Tile Type")]
    [Tooltip("Defines this prefab's designer-facing map role and recommended defaults.")]
    [SerializeField] private MapPieceType pieceType;

    [Header("Collision")]
    [Tooltip("Enables the prefab's existing Collider. Visual-only Tile_Visual prefabs should keep this disabled.")]
    [SerializeField] private bool useCollider;
    [Tooltip("Use Floor_Collision for the surface the player actually stands on.")]
    [SerializeField] private bool isGround;
    [Tooltip("Whether this object blocks player and monster movement when its LayerMask is configured.")]
    [SerializeField] private bool blockMovement;

    [Header("Placement")]
    [Tooltip("Uses the recommended gameplay-plane Z for this piece type. Disable to preserve a custom Z.")]
    [SerializeField] private bool autoSetZ = true;
    [SerializeField] private float zPosition;
    [SerializeField] private bool snapToGrid = true;
    [SerializeField] private float gridSize = 1f;
    [SerializeField] private bool isInteractable;

    [Header("Detection Blocking")]
    [Tooltip("Whether this object is intended to block monster sight. The assigned Layer must also be included in the monster obstacle LayerMask.")]
    [SerializeField] private bool blockLineOfSight;
    [Tooltip("Only validates the recommended Layer; code never changes Layers automatically.")]
    [FormerlySerializedAs("autoSetObstacleLayer")]
    [SerializeField] private bool validateObstacleLayer = true;
    [SerializeField] private string obstacleLayerName = "EnvironmentObstacle";

    [Header("World A / B")]
    [Tooltip("Marks this piece as a camera-switchable World A/B target. Floor_Collision should normally remain disabled.")]
    [SerializeField] private bool canSwitchWorld;
    [SerializeField] private WorldSwitchCategory worldSwitchCategory = WorldSwitchCategory.Default;
    [SerializeField] private WorldState currentWorldState = WorldState.WorldA;
    [SerializeField] private WorldState targetWorldState = WorldState.WorldB;
    [SerializeField] private bool autoConfigureWorldSwitchable = true;

    [Header("Optional Settings / Debug")]
    [SerializeField] private MapLayerSettings layerSettings;
    [SerializeField] private bool debugMode;

    [SerializeField, HideInInspector] private MapPieceType previousPieceType;
    [SerializeField, HideInInspector] private bool initialized;

    private bool layerWarningLogged;

    public MapPieceType PieceType => pieceType;
    public bool UseCollider => useCollider;
    public bool IsInteractable => isInteractable;
    public bool BlockMovement => blockMovement;
    public bool BlockLineOfSight => blockLineOfSight;
    public bool IsGround => isGround;
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
        isGround = GetDefaultIsGround(pieceType);
        obstacleLayerName = GetRecommendedLayerName(pieceType);
    }

    private void ApplySettings()
    {
        if (autoSetZ)
        {
            zPosition = ResolveDefaultZ(pieceType);
        }

        ApplyPositionRules();
        ApplyColliderRules();
        ValidateObstacleLayerRules();
        ApplyWorldSwitchRules();

        if (debugMode)
        {
            Debug.Log(
                $"[MapPiece] Applied {pieceType}, Collider={useCollider}, Z={zPosition:0.###}, Snap={snapToGrid}, Interactable={isInteractable}, IsGround={isGround}, BlockMovement={blockMovement}, BlockLOS={blockLineOfSight}, CanSwitchWorld={canSwitchWorld}",
                this);
        }
    }

    private void ApplyPositionRules()
    {
        // Scene placement is authored in Edit Mode. Never move map geometry when Play starts.
        if (Application.isPlaying)
        {
            return;
        }

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

    private void ValidateObstacleLayerRules()
    {
        if (!validateObstacleLayer || (!blockMovement && !blockLineOfSight) || string.IsNullOrWhiteSpace(obstacleLayerName))
        {
            layerWarningLogged = false;
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

        Transform[] targets = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i].gameObject.layer == obstacleLayer)
            {
                continue;
            }

            if (!layerWarningLogged)
            {
                Debug.LogWarning(
                    $"[MapPiece] '{name}' or a child is not on layer '{obstacleLayerName}'. Assign the layer manually in the Inspector.",
                    this);
                layerWarningLogged = true;
            }

            return;
        }

        layerWarningLogged = false;
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
            if (debugMode)
            {
                Debug.LogWarning(
                    $"[MapPiece] '{name}' canSwitchWorld is enabled, but WorldSwitchable is missing. Add it in the Inspector.",
                    this);
            }

            return;
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
            case MapPieceType.FloorCollision:
            case MapPieceType.Wall:
            case MapPieceType.Tile:
            case MapPieceType.VisualTile:
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
            case MapPieceType.VisualTile:
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
            case MapPieceType.Wall:
            case MapPieceType.Tile:
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
            case MapPieceType.Object:
                return true;
            default:
                return false;
        }
    }

    private static bool GetDefaultIsGround(MapPieceType type)
    {
        return type == MapPieceType.Floor ||
            type == MapPieceType.FloorCollision ||
            type == MapPieceType.Platform;
    }

    private static string GetRecommendedLayerName(MapPieceType type)
    {
        switch (type)
        {
            case MapPieceType.Floor:
            case MapPieceType.FloorCollision:
                return "Ground";
            case MapPieceType.Wall:
                return "Wall";
            case MapPieceType.Tile:
                return "TileObstacle";
            case MapPieceType.Platform:
                return "Platform";
            case MapPieceType.VisualTile:
                return "Default";
            default:
                return "EnvironmentObstacle";
        }
    }

    private static ResearchWorldId ToResearchWorldId(WorldState state)
    {
        return state == WorldState.WorldA ? ResearchWorldId.WorldA : ResearchWorldId.WorldB;
    }
}
