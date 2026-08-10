using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneMapEditorBuilder3D
{
    private const string SettingsPath = "Assets/_Project/Resources/SceneMap/SceneMapSettings3D.asset";
    private const string GraphPath = "Assets/_Project/Resources/SceneMap/SceneMapGraphData3D.asset";

    private sealed class ScannedScene
    {
        public SceneMapRoomData3D room;
        public readonly List<RawConnection> connections = new List<RawConnection>();
        public readonly HashSet<string> spawnIds = new HashSet<string>(StringComparer.Ordinal);
    }

    private sealed class RawConnection
    {
        public string fromScene;
        public string targetScene;
        public string portalId;
        public string hierarchyPath;
        public string targetSpawnId;
        public Vector3 worldPosition;
        public SceneMapPortalDirection direction;
        public SceneMapConnectionType type;
        public bool active;
    }

    private sealed class LayoutLink
    {
        public string toRoomKey;
        public SceneMapPortalDirection direction;
    }

    [MenuItem("Tools/Scene Map/Bake Scene Map")]
    public static void BakeSceneMap() => Bake(false);

    [MenuItem("Tools/Scene Map/Update New Rooms Only")]
    public static void UpdateNewRoomsOnly() => Bake(true);

    [MenuItem("Tools/Scene Map/Validate Scene Connections")]
    public static void ValidateSceneConnections()
    {
        SceneMapSettings3D settings = LoadSettings();
        SceneMapGraphData3D graph = LoadGraph();
        if (settings == null || graph == null) return;
        List<ScannedScene> scanned = ScanBuildScenes(settings);
        List<SceneMapConnectionData3D> bakedConnections = BuildConnections(scanned);
        int errors = 0;
        int warnings = 0;
        HashSet<string> roomKeys = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> sceneGuids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < graph.Rooms.Count; i++)
        {
            SceneMapRoomData3D room = graph.Rooms[i];
            if (room == null) { errors++; Debug.LogError("[SceneMap] Null room record.", graph); continue; }
            if (string.IsNullOrWhiteSpace(room.StableSceneKey) || !roomKeys.Add(room.StableSceneKey))
            { errors++; Debug.LogError($"[SceneMap] Missing or duplicate Stable Scene Key: {room.StableSceneKey}", graph); }
            if (string.IsNullOrWhiteSpace(room.SceneGuid) || !sceneGuids.Add(room.SceneGuid))
            { errors++; Debug.LogError($"[SceneMap] Missing or duplicate Scene GUID: {room.SceneGuid}", graph); }
            string currentPath = AssetDatabase.GUIDToAssetPath(room.SceneGuid);
            if (!string.Equals(currentPath, room.ScenePath, StringComparison.Ordinal))
            { errors++; Debug.LogError($"[SceneMap] Scene path changed or is missing: {room.SceneName} / baked='{room.ScenePath}' / current='{currentPath}'", graph); }
            if (!string.Equals(Path.GetFileNameWithoutExtension(currentPath), room.SceneName, StringComparison.Ordinal))
            { errors++; Debug.LogError($"[SceneMap] Scene was renamed without a new bake: {room.SceneName} -> {Path.GetFileNameWithoutExtension(currentPath)}", graph); }
            if (room.Active && settings.IsSceneExplicitlyExcluded(room.SceneGuid))
            { errors++; Debug.LogError($"[SceneMap] Explicitly excluded Scene is active: {room.SceneName}", graph); }
        }

        for (int i = 0; i < scanned.Count; i++)
        {
            SceneMapRoomData3D scannedRoom = scanned[i].room;
            if (scannedRoom.Active && !sceneGuids.Contains(scannedRoom.SceneGuid))
            { errors++; Debug.LogError($"[SceneMap] Build Scene is missing from baked rooms: {scannedRoom.ScenePath}", graph); }
        }

        Dictionary<string, string> storedConnectionSignatures = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < graph.Connections.Count; i++)
        {
            SceneMapConnectionData3D stored = graph.Connections[i];
            if (stored != null) storedConnectionSignatures[ConnectionIdentity(stored)] = ConnectionBakeSignature(stored);
        }
        HashSet<string> scannedConnectionIdentities = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < bakedConnections.Count; i++)
        {
            SceneMapConnectionData3D scannedConnection = bakedConnections[i];
            string identity = ConnectionIdentity(scannedConnection);
            scannedConnectionIdentities.Add(identity);
            if (!storedConnectionSignatures.TryGetValue(identity, out string storedSignature))
            { errors++; Debug.LogError($"[SceneMap] Portal is missing from baked data: {scannedConnection.PortalHierarchyPath}", graph); }
            else if (!string.Equals(storedSignature, ConnectionBakeSignature(scannedConnection), StringComparison.Ordinal))
            { errors++; Debug.LogError($"[SceneMap] Portal target, Spawn ID or active state changed after bake: {scannedConnection.PortalHierarchyPath}", graph); }
        }
        foreach (KeyValuePair<string, string> stored in storedConnectionSignatures)
        {
            if (!scannedConnectionIdentities.Contains(stored.Key))
            { warnings++; Debug.LogWarning($"[SceneMap] Baked Portal no longer exists in Build Scenes: {stored.Key}", graph); }
        }

        for (int i = 0; i < bakedConnections.Count; i++)
        {
            SceneMapConnectionData3D connection = bakedConnections[i];
            if (!connection.Active) continue;
            if ((connection.Issues & SceneMapConnectionIssue.MissingDestinationScene) != 0)
            { errors++; Debug.LogError($"[SceneMap] Missing destination Scene: {connection.PortalHierarchyPath} -> {connection.TargetSceneName}", graph); }
            if ((connection.Issues & SceneMapConnectionIssue.MissingSpawnPoint) != 0)
            { errors++; Debug.LogError($"[SceneMap] Missing destination SpawnPoint: {connection.PortalHierarchyPath} -> {connection.TargetSceneName}/{connection.TargetSpawnId}", graph); }
            if ((connection.Issues & SceneMapConnectionIssue.EmptyPortalId) != 0)
            { warnings++; Debug.LogWarning($"[SceneMap] Empty Portal ID: {connection.PortalHierarchyPath}", graph); }
            if ((connection.Issues & SceneMapConnectionIssue.DuplicateSceneLink) != 0)
            { warnings++; Debug.LogWarning($"[SceneMap] Duplicate active Scene link: {connection.PortalHierarchyPath} -> {connection.TargetSceneName}", graph); }
        }
        Debug.Log($"[SceneMap] Validation finished: {errors} error(s), {warnings} warning(s), {scanned.Count} Build Scene(s), {bakedConnections.Count} Portal record(s).", graph);
    }

    [MenuItem("Tools/Scene Map/Preserve Current Layout")]
    public static void PreserveCurrentLayout()
    {
        SceneMapGraphData3D graph = LoadGraph();
        if (graph == null) return;
        graph.PreserveAllCurrentLayout();
        EditorUtility.SetDirty(graph);
        AssetDatabase.SaveAssets();
        Debug.Log("[SceneMap] Current Room positions are marked as manual layout overrides.", graph);
    }

    private static void Bake(bool newRoomsOnly)
    {
        SceneMapSettings3D settings = LoadSettings();
        SceneMapGraphData3D graph = LoadGraph();
        if (settings == null || graph == null) return;
        List<ScannedScene> scanned = ScanBuildScenes(settings);
        Dictionary<string, SceneMapRoomData3D> previousByGuid = new Dictionary<string, SceneMapRoomData3D>(StringComparer.Ordinal);
        for (int i = 0; i < graph.Rooms.Count; i++)
        {
            SceneMapRoomData3D previous = graph.Rooms[i];
            if (previous != null && !string.IsNullOrWhiteSpace(previous.SceneGuid)) previousByGuid[previous.SceneGuid] = previous;
        }

        List<SceneMapRoomData3D> rooms = new List<SceneMapRoomData3D>();
        if (newRoomsOnly)
            for (int i = 0; i < graph.Rooms.Count; i++) if (graph.Rooms[i] != null) rooms.Add(graph.Rooms[i]);
        for (int i = 0; i < scanned.Count; i++)
        {
            SceneMapRoomData3D room = scanned[i].room;
            if (previousByGuid.TryGetValue(room.SceneGuid, out SceneMapRoomData3D previous))
            {
                if (newRoomsOnly) continue;
                room.PreserveDesignerSettings(previous);
            }
            if (settings.IsSceneExplicitlyExcluded(room.SceneGuid)) room.SetActive(false);
            rooms.Add(room);
        }

        List<SceneMapConnectionData3D> connections;
        if (newRoomsOnly)
        {
            connections = new List<SceneMapConnectionData3D>();
            for (int i = 0; i < graph.Connections.Count; i++) if (graph.Connections[i] != null) connections.Add(graph.Connections[i]);
            HashSet<string> existing = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < connections.Count; i++) existing.Add(ConnectionIdentity(connections[i]));
            List<SceneMapConnectionData3D> scannedConnections = BuildConnections(scanned);
            for (int i = 0; i < scannedConnections.Count; i++)
                if (existing.Add(ConnectionIdentity(scannedConnections[i]))) connections.Add(scannedConnections[i]);
            PlaceOnlyNewRooms(rooms, previousByGuid.Count, settings);
        }
        else
        {
            connections = BuildConnections(scanned);
            BuildDraftLayout(rooms, connections, settings);
        }
        graph.ReplaceBakedData(rooms, connections, DateTime.UtcNow.ToString("O"));
        EditorUtility.SetDirty(graph);
        AssetDatabase.SaveAssets();
        Debug.Log($"[SceneMap] {(newRoomsOnly ? "New-room update" : "Bake")} complete: {rooms.Count} Room(s), {connections.Count} Portal record(s). No Scene was saved.", graph);
        ValidateSceneConnections();
    }

    private static List<ScannedScene> ScanBuildScenes(SceneMapSettings3D settings)
    {
        List<ScannedScene> result = new List<ScannedScene>();
        Scene activeBefore = SceneManager.GetActiveScene();
        EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
        for (int i = 0; i < buildScenes.Length; i++)
        {
            EditorBuildSettingsScene build = buildScenes[i];
            if (!build.enabled || string.IsNullOrWhiteSpace(build.path)) continue;
            string guid = AssetDatabase.AssetPathToGUID(build.path);
            string sceneName = Path.GetFileNameWithoutExtension(build.path);
            ScannedScene scanned = new ScannedScene();
            scanned.room = new SceneMapRoomData3D();
            scanned.room.InitializeAutomatic("room-" + guid, guid, build.path, sceneName,
                ObjectNames.NicifyVariableName(sceneName), settings.DefaultRoomSize,
                settings.DefaultRoomColor, !settings.IsSceneExplicitlyExcluded(guid));

            Scene scene = SceneManager.GetSceneByPath(build.path);
            bool openedByTool = !scene.IsValid() || !scene.isLoaded;
            try
            {
                if (openedByTool) scene = EditorSceneManager.OpenScene(build.path, OpenSceneMode.Additive);
                GameObject[] roots = scene.GetRootGameObjects();
                for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                {
                    PlayerSpawnPoint[] spawnPoints = roots[rootIndex].GetComponentsInChildren<PlayerSpawnPoint>(true);
                    for (int spawnIndex = 0; spawnIndex < spawnPoints.Length; spawnIndex++)
                        if (!string.IsNullOrWhiteSpace(spawnPoints[spawnIndex].ResolvedSpawnPointId))
                            scanned.spawnIds.Add(spawnPoints[spawnIndex].ResolvedSpawnPointId);

                    StageExitTrigger[] exits = roots[rootIndex].GetComponentsInChildren<StageExitTrigger>(true);
                    for (int exitIndex = 0; exitIndex < exits.Length; exitIndex++)
                    {
                        StageExitTrigger exit = exits[exitIndex];
                        scanned.connections.Add(new RawConnection
                        {
                            fromScene = sceneName,
                            targetScene = exit.NextSceneName,
                            portalId = exit.ExitId,
                            hierarchyPath = GetHierarchyPath(exit.transform),
                            targetSpawnId = exit.TargetSpawnPointId,
                            worldPosition = exit.transform.position,
                            type = SceneMapConnectionType.StageExit,
                            active = exit.isActiveAndEnabled && exit.ConnectionEnabled
                        });
                    }

                    ScenePortal3D[] portals = roots[rootIndex].GetComponentsInChildren<ScenePortal3D>(true);
                    for (int portalIndex = 0; portalIndex < portals.Length; portalIndex++)
                    {
                        ScenePortal3D portal = portals[portalIndex];
                        scanned.connections.Add(new RawConnection
                        {
                            fromScene = sceneName,
                            targetScene = portal.TargetSceneName,
                            portalId = portal.PortalId,
                            hierarchyPath = GetHierarchyPath(portal.transform),
                            targetSpawnId = portal.TargetSpawnId,
                            worldPosition = portal.transform.position,
                            type = SceneMapConnectionType.Portal,
                            active = portal.isActiveAndEnabled
                        });
                    }
                }
                AssignDirections(scanned.connections);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[SceneMap] Failed to scan '{build.path}': {exception.Message}");
            }
            finally
            {
                if (openedByTool && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
            result.Add(scanned);
        }
        if (activeBefore.IsValid() && activeBefore.isLoaded) SceneManager.SetActiveScene(activeBefore);
        return result;
    }

    private static List<SceneMapConnectionData3D> BuildConnections(List<ScannedScene> scanned)
    {
        Dictionary<string, ScannedScene> byScene = new Dictionary<string, ScannedScene>(StringComparer.Ordinal);
        List<RawConnection> raw = new List<RawConnection>();
        for (int i = 0; i < scanned.Count; i++)
        {
            byScene[scanned[i].room.SceneName] = scanned[i];
            raw.AddRange(scanned[i].connections);
        }
        Dictionary<string, int> activePairCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        HashSet<string> activeDirections = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < raw.Count; i++)
        {
            RawConnection item = raw[i];
            if (!item.active || string.IsNullOrWhiteSpace(item.targetScene)) continue;
            string directionKey = item.fromScene + "|" + item.targetScene;
            activeDirections.Add(directionKey);
            activePairCounts.TryGetValue(directionKey, out int count);
            activePairCounts[directionKey] = count + 1;
        }

        List<SceneMapConnectionData3D> result = new List<SceneMapConnectionData3D>(raw.Count);
        for (int i = 0; i < raw.Count; i++)
        {
            RawConnection item = raw[i];
            SceneMapConnectionIssue issues = SceneMapConnectionIssue.None;
            if (string.IsNullOrWhiteSpace(item.portalId)) issues |= SceneMapConnectionIssue.EmptyPortalId;
            string toRoomKey = string.Empty;
            if (string.IsNullOrWhiteSpace(item.targetScene) || !byScene.TryGetValue(item.targetScene, out ScannedScene destination))
                issues |= SceneMapConnectionIssue.MissingDestinationScene;
            else
            {
                toRoomKey = destination.room.StableSceneKey;
                if (!string.IsNullOrWhiteSpace(item.targetSpawnId) && !destination.spawnIds.Contains(item.targetSpawnId))
                    issues |= SceneMapConnectionIssue.MissingSpawnPoint;
            }
            string directionalPair = item.fromScene + "|" + item.targetScene;
            if (item.active && activePairCounts.TryGetValue(directionalPair, out int count) && count > 1)
                issues |= SceneMapConnectionIssue.DuplicateSceneLink;
            bool reverse = item.active && activeDirections.Contains(item.targetScene + "|" + item.fromScene);
            SceneMapConnectionData3D connection = new SceneMapConnectionData3D();
            connection.Initialize(byScene[item.fromScene].room.StableSceneKey, toRoomKey, item.targetScene,
                item.portalId, item.hierarchyPath, item.targetSpawnId, item.direction,
                item.type, item.active, reverse, issues);
            result.Add(connection);
        }
        return result;
    }

    private static void AssignDirections(List<RawConnection> connections)
    {
        if (connections.Count < 2) return;
        Vector3 center = Vector3.zero;
        for (int i = 0; i < connections.Count; i++) center += connections[i].worldPosition;
        center /= connections.Count;
        for (int i = 0; i < connections.Count; i++)
        {
            Vector3 delta = connections[i].worldPosition - center;
            if (Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y)) < 0.05f) continue;
            connections[i].direction = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
                ? delta.x < 0f ? SceneMapPortalDirection.Left : SceneMapPortalDirection.Right
                : delta.y < 0f ? SceneMapPortalDirection.Down : SceneMapPortalDirection.Up;
        }
    }

    private static void BuildDraftLayout(List<SceneMapRoomData3D> rooms,
        List<SceneMapConnectionData3D> connections, SceneMapSettings3D settings)
    {
        Dictionary<string, SceneMapRoomData3D> byKey = new Dictionary<string, SceneMapRoomData3D>(StringComparer.Ordinal);
        Dictionary<string, List<LayoutLink>> neighbours = new Dictionary<string, List<LayoutLink>>(StringComparer.Ordinal);
        for (int i = 0; i < rooms.Count; i++)
        {
            if (rooms[i] == null || !rooms[i].Active) continue;
            byKey[rooms[i].StableSceneKey] = rooms[i];
            neighbours[rooms[i].StableSceneKey] = new List<LayoutLink>();
        }
        for (int i = 0; i < connections.Count; i++)
        {
            SceneMapConnectionData3D connection = connections[i];
            if (connection == null || !connection.CanDraw
                || !neighbours.ContainsKey(connection.FromRoomKey) || !neighbours.ContainsKey(connection.ToRoomKey)) continue;
            neighbours[connection.FromRoomKey].Add(new LayoutLink
                { toRoomKey = connection.ToRoomKey, direction = connection.Direction });
            neighbours[connection.ToRoomKey].Add(new LayoutLink
                { toRoomKey = connection.FromRoomKey, direction = Opposite(connection.Direction) });
        }
        HashSet<string> placed = new HashSet<string>(StringComparer.Ordinal);
        HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();
        Queue<string> queue = new Queue<string>();
        if (byKey.ContainsKey(settings.RootRoomKey))
        {
            byKey[settings.RootRoomKey].ApplyAutomaticLayout(Vector2.zero);
            placed.Add(settings.RootRoomKey);
            occupied.Add(Vector2Int.zero);
            queue.Enqueue(settings.RootRoomKey);
        }
        while (queue.Count > 0)
        {
            string currentKey = queue.Dequeue();
            Vector2 current = byKey[currentKey].MapPosition;
            List<LayoutLink> nextConnections = neighbours[currentKey];
            for (int i = 0; i < nextConnections.Count; i++)
            {
                LayoutLink connection = nextConnections[i];
                if (!byKey.ContainsKey(connection.toRoomKey) || !placed.Add(connection.toRoomKey)) continue;
                Vector2 step = DirectionStep(connection.direction, settings);
                Vector2 candidate = current + step;
                Vector2Int cell = new Vector2Int(Mathf.RoundToInt(candidate.x / settings.HorizontalSpacing),
                    Mathf.RoundToInt(candidate.y / settings.VerticalSpacing));
                while (!occupied.Add(cell)) { cell.y--; candidate.y -= settings.VerticalSpacing; }
                byKey[connection.toRoomKey].ApplyAutomaticLayout(candidate);
                queue.Enqueue(connection.toRoomKey);
            }
        }
        int disconnectedIndex = 0;
        for (int i = 0; i < rooms.Count; i++)
        {
            SceneMapRoomData3D room = rooms[i];
            if (room == null || !room.Active || placed.Contains(room.StableSceneKey)) continue;
            room.ApplyAutomaticLayout(new Vector2(disconnectedIndex * settings.HorizontalSpacing, -3f * settings.VerticalSpacing));
            disconnectedIndex++;
        }
    }

    private static Vector2 DirectionStep(SceneMapPortalDirection direction, SceneMapSettings3D settings)
    {
        switch (direction)
        {
            case SceneMapPortalDirection.Left: return new Vector2(-settings.HorizontalSpacing, 0f);
            case SceneMapPortalDirection.Up: return new Vector2(0f, settings.VerticalSpacing);
            case SceneMapPortalDirection.Down: return new Vector2(0f, -settings.VerticalSpacing);
            default: return new Vector2(settings.HorizontalSpacing, 0f);
        }
    }

    private static SceneMapPortalDirection Opposite(SceneMapPortalDirection direction)
    {
        switch (direction)
        {
            case SceneMapPortalDirection.Left: return SceneMapPortalDirection.Right;
            case SceneMapPortalDirection.Right: return SceneMapPortalDirection.Left;
            case SceneMapPortalDirection.Up: return SceneMapPortalDirection.Down;
            case SceneMapPortalDirection.Down: return SceneMapPortalDirection.Up;
            default: return SceneMapPortalDirection.Unknown;
        }
    }

    private static void PlaceOnlyNewRooms(List<SceneMapRoomData3D> rooms, int previousRoomCount, SceneMapSettings3D settings)
    {
        int newIndex = 0;
        for (int i = previousRoomCount; i < rooms.Count; i++)
        {
            SceneMapRoomData3D room = rooms[i];
            if (room == null || !room.Active) continue;
            room.ApplyAutomaticLayout(new Vector2(newIndex * settings.HorizontalSpacing, -4f * settings.VerticalSpacing));
            newIndex++;
        }
    }

    private static string GetHierarchyPath(Transform target)
    {
        if (target == null) return string.Empty;
        string path = target.name;
        Transform parent = target.parent;
        while (parent != null) { path = parent.name + "/" + path; parent = parent.parent; }
        return path;
    }

    private static string ConnectionIdentity(SceneMapConnectionData3D connection)
    {
        return connection.FromRoomKey + "|" + connection.PortalHierarchyPath + "|" + connection.ConnectionType;
    }

    private static string ConnectionBakeSignature(SceneMapConnectionData3D connection)
    {
        return connection.TargetSceneName + "|" + connection.TargetSpawnId + "|" + connection.Active;
    }

    private static SceneMapSettings3D LoadSettings()
    {
        SceneMapSettings3D settings = AssetDatabase.LoadAssetAtPath<SceneMapSettings3D>(SettingsPath);
        if (settings == null) Debug.LogError("[SceneMap] Missing Settings asset: " + SettingsPath);
        return settings;
    }

    private static SceneMapGraphData3D LoadGraph()
    {
        SceneMapGraphData3D graph = AssetDatabase.LoadAssetAtPath<SceneMapGraphData3D>(GraphPath);
        if (graph == null) Debug.LogError("[SceneMap] Missing Graph asset: " + GraphPath);
        return graph;
    }
}

[CustomEditor(typeof(SceneMapGraphData3D))]
public sealed class SceneMapGraphData3DEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Scene Map Bake", EditorStyles.boldLabel);
        if (GUILayout.Button("Bake Scene Map")) SceneMapEditorBuilder3D.BakeSceneMap();
        if (GUILayout.Button("Validate Scene Connections")) SceneMapEditorBuilder3D.ValidateSceneConnections();
        if (GUILayout.Button("Update New Rooms Only")) SceneMapEditorBuilder3D.UpdateNewRoomsOnly();
        if (GUILayout.Button("Preserve Current Layout")) SceneMapEditorBuilder3D.PreserveCurrentLayout();
    }
}
