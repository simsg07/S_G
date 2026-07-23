using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class FullSummerCampMapConnectionSetup
{
    private const string SceneFolder = "Assets/_Project/Scenes/Stages/ImportedSummerCampMap/";
    private const string ExitPrefabPath = "Assets/_Project/Prefabs/Scene/StageExitTrigger.prefab";
    private const string SpawnPrefabPath = "Assets/_Project/Prefabs/Scene/PlayerSpawnPoint.prefab";

    private static readonly SceneSpec[] Specs =
    {
        S("Start_Room", new[] { "RightEntrance" }, E("RightExit", "hallwa_01", "LeftEntrance")),
        S("hallwa_01", new[] { "LeftEntrance", "RightEntrance" }, E("LeftExit", "Start_Room", "RightEntrance"), E("RightExit", "middle_Room", "LeftEntrance")),
        S("middle_Room", new[] { "LeftEntrance", "UpperRightEntrance", "CenterRightEntrance", "LowerRightEntrance" }, E("LeftExit", "hallwa_01", "RightEntrance"), E("UpperRightExit", "hallwa_02", "LeftEntrance"), E("CenterRightExit", "hallwa_03", "LeftEntrance"), E("LowerRightExit", "hallwa_05", "LeftEntrance")),
        S("hallwa_02", new[] { "LeftEntrance", "RightEntrance" }, E("LeftExit", "middle_Room", "UpperRightEntrance"), E("RightExit", "Item_Room_01", "LeftEntrance")),
        S("Item_Room_01", new[] { "LeftEntrance" }, E("LeftExit", "hallwa_02", "RightEntrance")),
        S("hallwa_03", new[] { "LeftEntrance", "RightEntrance" }, E("LeftExit", "middle_Room", "CenterRightEntrance"), E("RightExit", "Item_Room_02", "LeftEntrance")),
        S("Item_Room_02", new[] { "LeftEntrance", "RightEntrance" }, E("LeftExit", "hallwa_03", "RightEntrance"), E("RightExit", "hallwa_04", "LeftEntrance")),
        S("hallwa_04", new[] { "LeftEntrance" }, E("LeftExit", "Item_Room_02", "RightEntrance")),
        S("hallwa_05", new[] { "LeftEntrance", "RightEntrance" }, E("LeftExit", "middle_Room", "LowerRightEntrance"), E("RightExit", "Boss_Hint_Room", "LeftEntrance")),
        S("Boss_Hint_Room", new[] { "LeftEntrance", "RightEntrance" }, E("LeftExit", "hallwa_05", "RightEntrance"), E("RightExit", "hallwa_06", "LeftEntrance")),
        S("hallwa_06", new[] { "LeftEntrance" }, E("LeftExit", "Boss_Hint_Room", "RightEntrance"))
    };

    [MenuItem("Tools/Scene Connections/Apply Confirmed Full Map Connections")]
    public static void ApplyFromMenu() => Apply();

    private static void Apply()
    {
        GameObject exitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ExitPrefabPath);
        GameObject spawnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SpawnPrefabPath);
        if (exitPrefab == null || spawnPrefab == null) { Debug.LogError("[전체 씬 연결] Entrance 또는 Exit 프리팹을 찾지 못했습니다."); return; }

        string previous = SceneManager.GetActiveScene().path;
        foreach (SceneSpec spec in Specs) ApplyScene(spec, spawnPrefab, exitPrefab);
        if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        AssetDatabase.SaveAssets();
        Debug.Log("[전체 씬 연결] 확정된 11개 씬의 양방향 연결을 저장했습니다. 기존 Entrance/Exit Transform과 Collision은 변경하지 않았습니다.");
    }

    private static void ApplyScene(SceneSpec spec, GameObject spawnPrefab, GameObject exitPrefab)
    {
        string path = SceneFolder + spec.Name + ".unity";
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        TilemapRenderer renderer = UnityEngine.Object.FindFirstObjectByType<TilemapRenderer>();
        Bounds bounds = renderer != null ? renderer.bounds : new Bounds(Vector3.zero, new Vector3(20f, 10f, 1f));

        GameObject root = FindRoot(scene, "[SceneConnections]");
        if (root == null)
        {
            root = new GameObject("[SceneConnections]");
            Undo.RegisterCreatedObjectUndo(root, "SceneConnections 생성");
        }
        if (root.GetComponent<SceneConnectionsAuthoring>() == null) root.AddComponent<SceneConnectionsAuthoring>();
        Transform spawnRoot = EnsureChild(root.transform, "SpawnPoints");
        Transform exitRoot = EnsureChild(root.transform, "Exits");

        foreach (string entranceId in spec.Entrances)
        {
            PlayerSpawnPoint point = FindByName<PlayerSpawnPoint>(entranceId);
            bool created = false;
            if (point == null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(spawnPrefab, spawnRoot);
                Undo.RegisterCreatedObjectUndo(instance, "누락된 Entrance 생성");
                instance.name = entranceId;
                instance.transform.position = TemporaryPosition(entranceId, bounds, true);
                point = instance.GetComponent<PlayerSpawnPoint>();
                created = true;
            }
            SerializedObject data = new SerializedObject(point);
            data.FindProperty("spawnPointId").stringValue = entranceId;
            if (created) data.FindProperty("isDefaultSpawn").boolValue = false;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        foreach (ExitSpec connection in spec.Exits)
        {
            StageExitTrigger exit = FindByName<StageExitTrigger>(connection.Id);
            if (exit == null && connection.Id == "UpperRightExit")
            {
                exit = FindByName<StageExitTrigger>("RightExit");
                if (exit != null) exit.name = "UpperRightExit";
            }
            if (exit == null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(exitPrefab, exitRoot);
                Undo.RegisterCreatedObjectUndo(instance, "누락된 Exit 생성");
                instance.name = connection.Id;
                instance.transform.position = TemporaryPosition(connection.Id, bounds, false);
                exit = instance.GetComponent<StageExitTrigger>();
            }

            Undo.RecordObject(exit, "씬 연결 데이터 설정");
            SerializedObject data = new SerializedObject(exit);
            data.FindProperty("exitId").stringValue = connection.Id;
            data.FindProperty("connectionEnabled").boolValue = true;
            data.FindProperty("targetScene").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SceneAsset>(SceneFolder + connection.Scene + ".unity");
            data.FindProperty("nextSceneName").stringValue = connection.Scene;
            data.FindProperty("targetSpawnPointId").stringValue = connection.Entrance;
            data.FindProperty("requiredPlayerTag").stringValue = "Player";
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        // 이전 5개 씬 시범 연결에서 남은 지름길은 오브젝트와 수동 Transform을
        // 보존한 채 연결만 끈다. 확정된 전체 맵에는 이 두 출구가 없다.
        if (spec.Name == "middle_Room" || spec.Name == "Item_Room_01")
            DisableLegacyExit("RightExit");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void DisableLegacyExit(string exitId)
    {
        StageExitTrigger exit = FindByName<StageExitTrigger>(exitId);
        if (exit == null) return;
        Undo.RecordObject(exit, "미확정 출구 연결 해제");
        SerializedObject data = new SerializedObject(exit);
        data.FindProperty("connectionEnabled").boolValue = false;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Vector3 TemporaryPosition(string id, Bounds bounds, bool spawn)
    {
        bool left = id.IndexOf("Left", StringComparison.OrdinalIgnoreCase) >= 0;
        float x = left ? bounds.min.x + (spawn ? 2f : 0.5f) : bounds.max.x - (spawn ? 2f : 0.5f);
        float y = bounds.center.y;
        if (id.StartsWith("Upper", StringComparison.Ordinal)) y += bounds.extents.y * 0.5f;
        else if (id.StartsWith("Lower", StringComparison.Ordinal)) y -= bounds.extents.y * 0.5f;
        return new Vector3(x, y, 0f);
    }

    private static T FindByName<T>(string name) where T : Component
    {
        foreach (T item in UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (item.name == name || (item is PlayerSpawnPoint point && point.Matches(name))) return item;
        return null;
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects()) if (root.name == name) return root;
        return null;
    }

    private static Transform EnsureChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing;
        GameObject child = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(child, "씬 연결 폴더 생성");
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    private static SceneSpec S(string name, string[] entrances, params ExitSpec[] exits) => new SceneSpec(name, entrances, exits);
    private static ExitSpec E(string id, string scene, string entrance) => new ExitSpec(id, scene, entrance);
    private readonly struct SceneSpec { public SceneSpec(string name, string[] entrances, ExitSpec[] exits) { Name=name; Entrances=entrances; Exits=exits; } public string Name { get; } public string[] Entrances { get; } public ExitSpec[] Exits { get; } }
    private readonly struct ExitSpec { public ExitSpec(string id, string scene, string entrance) { Id=id; Scene=scene; Entrance=entrance; } public string Id { get; } public string Scene { get; } public string Entrance { get; } }
}
