using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class FirstFiveStageConnectionSetup
{
    private const string SceneFolder = "Assets/_Project/Scenes/Stages/ImportedSummerCampMap/";
    private const string ExitPrefabPath = "Assets/_Project/Prefabs/Scene/StageExitTrigger.prefab";
    private const string SpawnPrefabPath = "Assets/_Project/Prefabs/Scene/PlayerSpawnPoint.prefab";
    private const string GeneratedRootName = "[SceneConnections]";

    private static readonly Connection[] Connections =
    {
        new Connection("Start_Room", null, "hallwa_01"),
        new Connection("hallwa_01", "Start_Room", "middle_Room"),
        new Connection("middle_Room", "hallwa_01", "hallwa_02"),
        new Connection("hallwa_02", "middle_Room", "Item_Room_01"),
        new Connection("Item_Room_01", "hallwa_02", null)
    };

    [MenuItem("Tools/Scene Connections/Create Missing Connection Objects")]
    public static void CreateMissingConnectionObjects()
    {
        GameObject exitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ExitPrefabPath);
        GameObject spawnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SpawnPrefabPath);
        if (exitPrefab == null || spawnPrefab == null)
        {
            Debug.LogError("[FirstFiveStageConnectionSetup] Required StageExitTrigger or PlayerSpawnPoint prefab is missing.");
            return;
        }

        string previousScenePath = SceneManager.GetActiveScene().path;
        foreach (Connection connection in Connections)
        {
            ConfigureScene(connection, exitPrefab, spawnPrefab);
        }

        if (!string.IsNullOrEmpty(previousScenePath))
        {
            EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[FirstFiveStageConnectionSetup] Missing connection objects created. Existing scene Transform overrides were preserved.");
    }

    private static void ConfigureScene(Connection connection, GameObject exitPrefab, GameObject spawnPrefab)
    {
        string scenePath = SceneFolder + connection.SceneName + ".unity";
        SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
        if (sceneAsset == null)
        {
            Debug.LogError($"[FirstFiveStageConnectionSetup] Scene is missing: {scenePath}");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        Tilemap tilemap = Object.FindFirstObjectByType<Tilemap>();
        if (tilemap == null)
        {
            Debug.LogError($"[FirstFiveStageConnectionSetup] Tilemap is missing in {connection.SceneName}.");
            return;
        }

        GameObject root = FindSceneObject(scene, GeneratedRootName) ?? new GameObject(GeneratedRootName);
        Transform spawnRoot = EnsureChild(root.transform, "SpawnPoints");
        Transform exitRoot = EnsureChild(root.transform, "Exits");

        PortalRow row = FindPortalRow(tilemap);
        float leftEdge = tilemap.CellToWorld(new Vector3Int(row.MinX, row.Y, 0)).x;
        float rightEdge = tilemap.CellToWorld(new Vector3Int(row.MaxX + 1, row.Y, 0)).x;
        float floorSurfaceY = tilemap.CellToWorld(new Vector3Int(0, row.Y + 1, 0)).y;
        float playerSpawnY = floorSurfaceY + 0.65f;

        CreateOrUpdateSpawn(spawnPrefab, spawnRoot, "LeftEntrance", new Vector3(leftEdge + 3f, playerSpawnY, 0f), true, connection.SceneName == "Start_Room");
        CreateOrUpdateSpawn(spawnPrefab, spawnRoot, "RightEntrance", new Vector3(rightEdge - 3f, playerSpawnY, 0f), false, false);

        if (!string.IsNullOrEmpty(connection.LeftScene))
        {
            string leftEntranceId = connection.SceneName == "hallwa_02" ? "UpperRightEntrance" : "RightEntrance";
            CreateOrUpdateExit(exitPrefab, exitRoot, "LeftExit", new Vector3(leftEdge - 0.5f, floorSurfaceY, 0f), connection.LeftScene, leftEntranceId);
        }

        if (!string.IsNullOrEmpty(connection.RightScene))
        {
            string rightExitId = connection.SceneName == "middle_Room" ? "UpperRightExit" : "RightExit";
            CreateOrUpdateExit(exitPrefab, exitRoot, rightExitId, new Vector3(rightEdge + 0.5f, floorSurfaceY, 0f), connection.RightScene, "LeftEntrance");
        }

        EnsureCamera(connection.SceneName);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static PortalRow FindPortalRow(Tilemap tilemap)
    {
        BoundsInt bounds = tilemap.cellBounds;
        var rows = new Dictionary<int, PortalRow>();
        foreach (Vector3Int position in bounds.allPositionsWithin)
        {
            if (!tilemap.HasTile(position))
            {
                continue;
            }

            if (!rows.TryGetValue(position.y, out PortalRow row))
            {
                row = new PortalRow(position.y, position.x, position.x, 0);
            }

            row.MinX = Mathf.Min(row.MinX, position.x);
            row.MaxX = Mathf.Max(row.MaxX, position.x);
            row.Count++;
            rows[position.y] = row;
        }

        PortalRow best = new PortalRow(0, bounds.xMin, bounds.xMax - 1, 0);
        foreach (PortalRow row in rows.Values)
        {
            if (row.Count > best.Count || (row.Count == best.Count && Mathf.Abs(row.Y) < Mathf.Abs(best.Y)))
            {
                best = row;
            }
        }

        return best;
    }

    private static void CreateOrUpdateSpawn(GameObject prefab, Transform parent, string id, Vector3 initialPosition, bool faceRight, bool isDefault)
    {
        PlayerSpawnPoint spawn = FindSpawn(id);
        if (spawn == null)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            Undo.RegisterCreatedObjectUndo(instance, "Create Missing Entrance");
            instance.name = id;
            instance.transform.position = initialPosition;
            spawn = instance.GetComponent<PlayerSpawnPoint>();
        }

        Undo.RecordObject(spawn, "Update Entrance Connection Data");
        SerializedObject serialized = new SerializedObject(spawn);
        serialized.FindProperty("spawnPointId").stringValue = id;
        serialized.FindProperty("isDefaultSpawn").boolValue = isDefault;
        serialized.FindProperty("faceRightOnSpawn").boolValue = faceRight;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateOrUpdateExit(GameObject prefab, Transform parent, string id, Vector3 initialPosition, string targetSceneName, string targetEntranceId)
    {
        StageExitTrigger trigger = FindExit(id);
        if (trigger == null)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            Undo.RegisterCreatedObjectUndo(instance, "Create Missing Exit");
            instance.name = id;
            instance.transform.position = initialPosition;
            trigger = instance.GetComponent<StageExitTrigger>();
        }

        Undo.RecordObject(trigger, "Update Exit Connection Data");
        SerializedObject serialized = new SerializedObject(trigger);
        serialized.FindProperty("exitId").stringValue = id;
        serialized.FindProperty("targetScene").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<SceneAsset>(SceneFolder + targetSceneName + ".unity");
        serialized.FindProperty("nextSceneName").stringValue = targetSceneName;
        serialized.FindProperty("targetSpawnPointId").stringValue = targetEntranceId;
        serialized.FindProperty("requiredPlayerTag").stringValue = "Player";
        serialized.FindProperty("transitionType").enumValueIndex = 0;
        serialized.FindProperty("useInteractionKey").boolValue = false;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static PlayerSpawnPoint FindSpawn(string id)
    {
        PlayerSpawnPoint[] spawns = Object.FindObjectsByType<PlayerSpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (PlayerSpawnPoint spawn in spawns)
        {
            if (spawn != null && (spawn.name == id || spawn.Matches(id)))
            {
                return spawn;
            }
        }

        return null;
    }

    private static StageExitTrigger FindExit(string id)
    {
        StageExitTrigger[] exits = Object.FindObjectsByType<StageExitTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (StageExitTrigger exit in exits)
        {
            if (exit != null && exit.name == id)
            {
                return exit;
            }
        }

        return null;
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == objectName)
            {
                return root;
            }
        }

        return null;
    }

    private static Transform EnsureChild(Transform parent, string childName)
    {
        Transform existing = parent.Find(childName);
        if (existing != null)
        {
            return existing;
        }

        GameObject child = new GameObject(childName);
        Undo.RegisterCreatedObjectUndo(child, "Create Connection Folder");
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    [MenuItem("Tools/Scene Connections/Auto Place Selected Object")]
    public static void AutoPlaceSelectedObject()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null ||
            (selected.GetComponent<PlayerSpawnPoint>() == null && selected.GetComponent<StageExitTrigger>() == null && selected.GetComponent<SceneSpawnPoint3D>() == null && selected.GetComponent<ScenePortal3D>() == null))
        {
            Debug.LogWarning("[FirstFiveStageConnectionSetup] Select one Entrance, SpawnPoint, Portal, or Exit object first.");
            return;
        }

        Tilemap tilemap = Object.FindFirstObjectByType<Tilemap>();
        if (tilemap == null)
        {
            Debug.LogWarning("[FirstFiveStageConnectionSetup] No Tilemap was found in the active scene.");
            return;
        }

        PortalRow row = FindPortalRow(tilemap);
        float leftEdge = tilemap.CellToWorld(new Vector3Int(row.MinX, row.Y, 0)).x;
        float rightEdge = tilemap.CellToWorld(new Vector3Int(row.MaxX + 1, row.Y, 0)).x;
        float floorSurfaceY = tilemap.CellToWorld(new Vector3Int(0, row.Y + 1, 0)).y;
        bool isLeft = selected.name.IndexOf("Left", System.StringComparison.OrdinalIgnoreCase) >= 0;
        bool isSpawn = selected.GetComponent<PlayerSpawnPoint>() != null || selected.GetComponent<SceneSpawnPoint3D>() != null;
        float x = isLeft
            ? leftEdge + (isSpawn ? 3f : -0.5f)
            : rightEdge + (isSpawn ? -3f : 0.5f);
        float y = floorSurfaceY + (isSpawn ? 0.65f : 0f);

        Undo.RecordObject(selected.transform, "Auto Place Selected Connection Object");
        selected.transform.position = new Vector3(x, y, selected.transform.position.z);
        PrefabUtility.RecordPrefabInstancePropertyModifications(selected.transform);
        EditorSceneManager.MarkSceneDirty(selected.scene);
        Debug.Log($"[FirstFiveStageConnectionSetup] Explicitly auto-placed selected object: {selected.name}", selected);
    }

    [MenuItem("Tools/Scene Connections/Validate Connections")]
    public static void ValidateConnections()
    {
        SceneConnectionValidator.ValidateLoadedScene(SceneManager.GetActiveScene());
        StageExitTrigger[] exits = Object.FindObjectsByType<StageExitTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (StageExitTrigger exit in exits)
        {
            exit.ValidateSceneConnection();
        }

        Debug.Log("[FirstFiveStageConnectionSetup] Validation completed without changing any Transform.");
    }

    private static void EnsureCamera(string sceneName)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(CameraFollow3D), typeof(UniversalAdditionalCameraData));
            cameraObject.tag = "MainCamera";
            camera = cameraObject.GetComponent<Camera>();
        }

        camera.gameObject.SetActive(true);
        camera.enabled = true;
        camera.targetDisplay = 0;
        camera.cullingMask = ~0;
        camera.orthographic = true;
        camera.orthographicSize = 5.2f;
        camera.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Base;
        if (camera.GetComponent<CameraFollow3D>() == null)
        {
            camera.gameObject.AddComponent<CameraFollow3D>();
        }
    }

    private readonly struct Connection
    {
        public Connection(string sceneName, string leftScene, string rightScene)
        {
            SceneName = sceneName;
            LeftScene = leftScene;
            RightScene = rightScene;
        }

        public string SceneName { get; }
        public string LeftScene { get; }
        public string RightScene { get; }
    }

    private struct PortalRow
    {
        public PortalRow(int y, int minX, int maxX, int count)
        {
            Y = y;
            MinX = minX;
            MaxX = maxX;
            Count = count;
        }

        public int Y;
        public int MinX;
        public int MaxX;
        public int Count;
    }
}
