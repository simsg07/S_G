using UnityEngine;

[DisallowMultipleComponent]
public sealed class TilemapGeneratedColliderMarker : MonoBehaviour
{
    public enum MarkerKind { Root, Collider }

    [SerializeField] private MarkerKind kind;
    [SerializeField] private TilemapCollisionAuthoring source;
    [SerializeField] private TilemapCollisionAuthoring[] sources;
    [SerializeField] private string bakeSignature;
    [SerializeField] private Vector3Int minimumCell;
    [SerializeField] private Vector3Int sizeInCells;
    [SerializeField] private string surfaceType;
    [SerializeField] private Vector2 edgeStart;
    [SerializeField] private Vector2 edgeEnd;
    [SerializeField] private Vector3 surfaceNormal;
    [SerializeField] private string collisionGroup;
    [SerializeField] private string seedRegionId;

    public MarkerKind Kind => kind;
    public TilemapCollisionAuthoring Source => source;
    public TilemapCollisionAuthoring[] Sources => sources;
    public string BakeSignature => bakeSignature;
    public Vector3Int MinimumCell => minimumCell;
    public Vector3Int SizeInCells => sizeInCells;
    public string SurfaceType => surfaceType;
    public Vector2 EdgeStart => edgeStart;
    public Vector2 EdgeEnd => edgeEnd;
    public Vector3 SurfaceNormal => surfaceNormal;
    public string CollisionGroup => collisionGroup;
    public string SeedRegionId => seedRegionId;

    public void Configure(MarkerKind markerKind, TilemapCollisionAuthoring tilemapSource, string signature, Vector3Int minCell, Vector3Int cellSize)
    {
        kind = markerKind;
        source = tilemapSource;
        sources = tilemapSource != null ? new[] { tilemapSource } : System.Array.Empty<TilemapCollisionAuthoring>();
        bakeSignature = signature;
        minimumCell = minCell;
        sizeInCells = cellSize;
    }

    public void ConfigureSources(TilemapCollisionAuthoring[] tilemapSources)
    {
        sources = tilemapSources ?? System.Array.Empty<TilemapCollisionAuthoring>();
        source = sources.Length > 0 ? sources[0] : null;
    }

    public void ConfigureSurface(string type, Vector2 start, Vector2 end, Vector3 normal, string group, string regionId)
    {
        surfaceType = type;
        edgeStart = start;
        edgeEnd = end;
        surfaceNormal = normal;
        collisionGroup = group;
        seedRegionId = regionId;
    }

    public bool ContainsSource(TilemapCollisionAuthoring candidate)
    {
        if (source == candidate) return true;
        if (sources == null) return false;
        for (int i = 0; i < sources.Length; i++) if (sources[i] == candidate) return true;
        return false;
    }
}
