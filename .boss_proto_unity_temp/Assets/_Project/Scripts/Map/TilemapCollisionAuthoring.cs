using System;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum TilemapCollisionRole
{
    None,
    Solid,
    OneWayPlatform,
    Hazard,
    Decoration,
    Unassigned
}

public enum TilemapCollisionBakeMode
{
    LegacySolidVolume,
    ReachableSurfaceBoundary
}

[Flags]
public enum TilemapExposedFaces
{
    None = 0,
    Top = 1 << 0,
    Bottom = 1 << 1,
    Left = 1 << 2,
    Right = 1 << 3
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Tilemap))]
public sealed class TilemapCollisionAuthoring : MonoBehaviour
{
    [SerializeField] private TilemapCollisionRole role = TilemapCollisionRole.Unassigned;
    [SerializeField] private float collisionCenterZ;
    [SerializeField, Min(0.001f)] private float collisionDepth = 1f;
    [SerializeField, Range(0, 31)] private int generatedLayer = 9;
    [SerializeField] private bool isTrigger;
    [SerializeField] private bool mergeEnabled = true;
    [SerializeField] private bool allowCrossTilemapMerge;
    [SerializeField] private string collisionGroupId = "MapSolid";
    [SerializeField] private TilemapCollisionBakeMode bakeMode = TilemapCollisionBakeMode.LegacySolidVolume;
    [SerializeField, Min(0.001f)] private float surfaceThickness = 0.1f;
    [SerializeField] private float floorSurfaceOffset;
    [SerializeField] private float ceilingSurfaceOffset;
    [SerializeField] private float leftWallSurfaceOffset;
    [SerializeField] private float rightWallSurfaceOffset;
    [SerializeField, Range(0, 31)] private int floorLayer = 9;
    [SerializeField, Range(0, 31)] private int wallLayer = 10;
    [SerializeField, Range(0, 31)] private int ceilingLayer = 9;
    [SerializeField, Min(0)] private int reachableBoundsPadding = 2;
    [SerializeField] private bool usePlayerClearance;
    [SerializeField, Min(1)] private int playerWidthCells = 1;
    [SerializeField, Min(1)] private int playerHeightCells = 2;
    [SerializeField] private bool useDominantSolidFillForReachable;
    [SerializeField] private Color gizmoColor = new Color(0.15f, 0.9f, 0.35f, 0.3f);
    [SerializeField, HideInInspector] private int automaticRoleVersion;
    [SerializeField, HideInInspector] private int automaticBakeModeVersion;

    public TilemapCollisionRole Role => role;
    public float CollisionCenterZ => collisionCenterZ;
    public float CollisionDepth => collisionDepth;
    public int GeneratedLayer => generatedLayer;
    public bool IsTrigger => isTrigger;
    public bool MergeEnabled => mergeEnabled;
    public bool AllowCrossTilemapMerge => allowCrossTilemapMerge;
    public string CollisionGroupId => collisionGroupId;
    public TilemapCollisionBakeMode BakeMode => bakeMode;
    public float SurfaceThickness => surfaceThickness;
    public float FloorSurfaceOffset => floorSurfaceOffset;
    public float CeilingSurfaceOffset => ceilingSurfaceOffset;
    public float LeftWallSurfaceOffset => leftWallSurfaceOffset;
    public float RightWallSurfaceOffset => rightWallSurfaceOffset;
    public int FloorLayer => floorLayer;
    public int WallLayer => wallLayer;
    public int CeilingLayer => ceilingLayer;
    public int ReachableBoundsPadding => reachableBoundsPadding;
    public bool UsePlayerClearance => usePlayerClearance;
    public int PlayerWidthCells => playerWidthCells;
    public int PlayerHeightCells => playerHeightCells;
    public bool UseDominantSolidFillForReachable => useDominantSolidFillForReachable;
    public Color GizmoColor => gizmoColor;
    public int AutomaticRoleVersion => automaticRoleVersion;
    public int AutomaticBakeModeVersion => automaticBakeModeVersion;

    public void SetInitialRole(TilemapCollisionRole value)
    {
        role = value;
        automaticRoleVersion = 1;
    }

    public void SetInitialGeneratedLayer(int value)
    {
        generatedLayer = Mathf.Clamp(value, 0, 31);
    }

    public void UseReachableDefaultsForNewTilemap()
    {
        bakeMode = TilemapCollisionBakeMode.ReachableSurfaceBoundary;
        automaticBakeModeVersion = 4;
        // Clearance is an opt-in traversal diagnostic. Enabling it by default removes
        // the empty cells directly below ceilings from the contour source.
        usePlayerClearance = false;
        useDominantSolidFillForReachable = true;
    }

    private void OnValidate()
    {
        collisionDepth = Mathf.Max(0.001f, collisionDepth);
        generatedLayer = Mathf.Clamp(generatedLayer, 0, 31);
        collisionGroupId = collisionGroupId == null ? string.Empty : collisionGroupId.Trim();
        surfaceThickness = Mathf.Max(0.001f, surfaceThickness);
        reachableBoundsPadding = Mathf.Max(0, reachableBoundsPadding);
        playerWidthCells = Mathf.Max(1, playerWidthCells);
        playerHeightCells = Mathf.Max(1, playerHeightCells);
    }
}
