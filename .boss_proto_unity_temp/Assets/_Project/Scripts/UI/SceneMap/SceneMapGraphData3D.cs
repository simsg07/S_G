using System;
using System.Collections.Generic;
using UnityEngine;

public enum SceneMapRoomStyle
{
    Standard,
    Start,
    Transition,
    Item,
    Boss,
    Special
}

public enum SceneMapConnectionType
{
    StageExit,
    Portal
}

public enum SceneMapPortalDirection
{
    Unknown,
    Left,
    Right,
    Up,
    Down
}

[Flags]
public enum SceneMapConnectionIssue
{
    None = 0,
    MissingDestinationScene = 1 << 0,
    MissingSpawnPoint = 1 << 1,
    DuplicateSceneLink = 1 << 2,
    EmptyPortalId = 1 << 3
}

[Serializable]
public sealed class SceneMapRoomData3D
{
    [SerializeField] private string stableSceneKey;
    [SerializeField] private string sceneGuid;
    [SerializeField] private string scenePath;
    [SerializeField] private string sceneName;
    [SerializeField] private string displayName;
    [SerializeField] private Vector2 mapPosition;
    [SerializeField] private Vector2 mapSize = new Vector2(160f, 68f);
    [SerializeField] private Color roomColor = new Color(0.18f, 0.25f, 0.34f, 1f);
    [SerializeField] private bool active = true;
    [SerializeField] private bool useManualLayout;
    [SerializeField] private SceneMapRoomStyle style;
    [SerializeField] private bool hasSceneWorldBounds;
    [SerializeField] private Bounds sceneWorldBounds;

    public string StableSceneKey => stableSceneKey;
    public string SceneGuid => sceneGuid;
    public string ScenePath => scenePath;
    public string SceneName => sceneName;
    public string DisplayName => displayName;
    public Vector2 MapPosition => mapPosition;
    public Vector2 MapSize => mapSize;
    public Color RoomColor => roomColor;
    public bool Active => active;
    public bool UseManualLayout => useManualLayout;
    public SceneMapRoomStyle Style => style;
    public bool HasSceneWorldBounds => hasSceneWorldBounds;
    public Bounds SceneWorldBounds => sceneWorldBounds;

    public void InitializeAutomatic(string key, string guid, string path, string runtimeName,
        string defaultDisplayName, Vector2 defaultSize, Color defaultColor, bool isActive)
    {
        stableSceneKey = key;
        sceneGuid = guid;
        scenePath = path;
        sceneName = runtimeName;
        displayName = defaultDisplayName;
        mapSize = defaultSize;
        roomColor = defaultColor;
        active = isActive;
    }

    public void PreserveDesignerSettings(SceneMapRoomData3D previous)
    {
        if (previous == null) return;
        stableSceneKey = previous.stableSceneKey;
        displayName = previous.displayName;
        mapPosition = previous.mapPosition;
        mapSize = previous.mapSize;
        roomColor = previous.roomColor;
        active = previous.active;
        useManualLayout = previous.useManualLayout;
        style = previous.style;
        hasSceneWorldBounds = previous.hasSceneWorldBounds;
        sceneWorldBounds = previous.sceneWorldBounds;
    }

    public void ApplyAutomaticLayout(Vector2 position)
    {
        if (!useManualLayout) mapPosition = position;
    }

    public void PreserveCurrentLayout()
    {
        useManualLayout = true;
    }

    public void SetActive(bool value)
    {
        active = value;
    }
}

[Serializable]
public sealed class SceneMapConnectionData3D
{
    [SerializeField] private string fromRoomKey;
    [SerializeField] private string toRoomKey;
    [SerializeField] private string targetSceneName;
    [SerializeField] private string fromPortalId;
    [SerializeField] private string portalHierarchyPath;
    [SerializeField] private string targetSpawnId;
    [SerializeField] private SceneMapPortalDirection direction;
    [SerializeField] private SceneMapConnectionType connectionType;
    [SerializeField] private bool active;
    [SerializeField] private bool bidirectional;
    [SerializeField] private SceneMapConnectionIssue issues;

    public string FromRoomKey => fromRoomKey;
    public string ToRoomKey => toRoomKey;
    public string TargetSceneName => targetSceneName;
    public string FromPortalId => fromPortalId;
    public string PortalHierarchyPath => portalHierarchyPath;
    public string TargetSpawnId => targetSpawnId;
    public SceneMapPortalDirection Direction => direction;
    public SceneMapConnectionType ConnectionType => connectionType;
    public bool Active => active;
    public bool Bidirectional => bidirectional;
    public SceneMapConnectionIssue Issues => issues;
    public bool CanDraw => active && !string.IsNullOrWhiteSpace(toRoomKey)
        && (issues & SceneMapConnectionIssue.MissingDestinationScene) == 0;

    public void Initialize(string fromKey, string toKey, string targetScene, string portalId,
        string hierarchyPath, string spawnId, SceneMapPortalDirection portalDirection,
        SceneMapConnectionType type, bool isActive, bool hasReverse, SceneMapConnectionIssue connectionIssues)
    {
        fromRoomKey = fromKey;
        toRoomKey = toKey;
        targetSceneName = targetScene;
        fromPortalId = portalId;
        portalHierarchyPath = hierarchyPath;
        targetSpawnId = spawnId;
        direction = portalDirection;
        connectionType = type;
        active = isActive;
        bidirectional = hasReverse;
        issues = connectionIssues;
    }
}

[CreateAssetMenu(fileName = "SceneMapGraphData3D", menuName = "_Project/Scene Map/Graph Data 3D")]
public sealed class SceneMapGraphData3D : ScriptableObject
{
    [SerializeField] private int dataVersion = 1;
    [SerializeField] private string bakedAtUtc;
    [SerializeField] private List<SceneMapRoomData3D> rooms = new List<SceneMapRoomData3D>();
    [SerializeField] private List<SceneMapConnectionData3D> connections = new List<SceneMapConnectionData3D>();

    private readonly Dictionary<string, SceneMapRoomData3D> roomsByKey = new Dictionary<string, SceneMapRoomData3D>();
    private readonly Dictionary<string, SceneMapRoomData3D> roomsByScene = new Dictionary<string, SceneMapRoomData3D>();
    private bool cacheReady;

    public int DataVersion => dataVersion;
    public string BakedAtUtc => bakedAtUtc;
    public IReadOnlyList<SceneMapRoomData3D> Rooms => rooms;
    public IReadOnlyList<SceneMapConnectionData3D> Connections => connections;

    public bool TryGetRoomByKey(string key, out SceneMapRoomData3D room)
    {
        EnsureCache();
        return roomsByKey.TryGetValue(key, out room);
    }

    public bool TryGetRoomByScene(string sceneName, out SceneMapRoomData3D room)
    {
        EnsureCache();
        return roomsByScene.TryGetValue(sceneName, out room);
    }

    public void ReplaceBakedData(List<SceneMapRoomData3D> bakedRooms,
        List<SceneMapConnectionData3D> bakedConnections, string bakeTimestampUtc)
    {
        rooms = bakedRooms ?? new List<SceneMapRoomData3D>();
        connections = bakedConnections ?? new List<SceneMapConnectionData3D>();
        bakedAtUtc = bakeTimestampUtc;
        dataVersion = Mathf.Max(1, dataVersion + 1);
        cacheReady = false;
    }

    public void PreserveAllCurrentLayout()
    {
        for (int i = 0; i < rooms.Count; i++) rooms[i]?.PreserveCurrentLayout();
    }

    public void InvalidateCache()
    {
        cacheReady = false;
    }

    private void OnEnable() => cacheReady = false;
    private void OnValidate() => cacheReady = false;

    private void EnsureCache()
    {
        if (cacheReady) return;
        roomsByKey.Clear();
        roomsByScene.Clear();
        for (int i = 0; i < rooms.Count; i++)
        {
            SceneMapRoomData3D room = rooms[i];
            if (room == null) continue;
            if (!string.IsNullOrWhiteSpace(room.StableSceneKey) && !roomsByKey.ContainsKey(room.StableSceneKey))
                roomsByKey.Add(room.StableSceneKey, room);
            if (!string.IsNullOrWhiteSpace(room.SceneName) && !roomsByScene.ContainsKey(room.SceneName))
                roomsByScene.Add(room.SceneName, room);
        }
        cacheReady = true;
    }
}
