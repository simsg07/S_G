using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneConnectionsPrefabBuilder
{
    private const string PrefabFolder = "Assets/_Project/Scenes/SceneConnections/Prefabs";
    private const string PrefabPath = PrefabFolder + "/SceneConnections.prefab";
    private const string EntrancePrefabPath = "Assets/_Project/Prefabs/Scene/PlayerSpawnPoint.prefab";
    private const string ExitPrefabPath = "Assets/_Project/Prefabs/Scene/StageExitTrigger.prefab";
    private const string BuildKey = "SummerCamp.SceneConnectionsPrefab.v1";
    private const string UpgradeKey = "SummerCamp.SceneConnectionsExistingScenes.v1";
    private static readonly string[] ExistingScenes = { "Start_Room", "hallwa_01", "middle_Room", "hallwa_02", "Item_Room_01" };

    [InitializeOnLoadMethod]
    private static void ScheduleBuild()
    {
        if (!EditorPrefs.GetBool(BuildKey, false)) EditorApplication.delayCall += BuildPrefab;
        if (!EditorPrefs.GetBool(UpgradeKey, false)) EditorApplication.delayCall += UpgradeExistingScenes;
    }

    private static void UpgradeExistingScenes()
    {
        string previous = SceneManager.GetActiveScene().path;
        foreach (string sceneName in ExistingScenes)
        {
            string path = "Assets/_Project/Scenes/Stages/ImportedSummerCampMap/" + sceneName + ".unity";
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            GameObject root = null;
            foreach (GameObject candidate in scene.GetRootGameObjects()) if (candidate.name == "[SceneConnections]") root = candidate;
            if (root == null) continue;
            if (root.GetComponent<SceneConnectionsAuthoring>() == null) root.AddComponent<SceneConnectionsAuthoring>();
            if (sceneName == "middle_Room")
            {
                foreach (StageExitTrigger exit in Object.FindObjectsByType<StageExitTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (exit.name == "RightExit") exit.name = "UpperRightExit";
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        EditorPrefs.SetBool(UpgradeKey, true);
        AssetDatabase.SaveAssets();
        Debug.Log("[씬 연결] 기존 5개 씬에 기획자용 SceneConnections Inspector를 연결했습니다. Transform은 변경하지 않았습니다.");
    }

    [MenuItem("Tools/Scene Connections/Create Or Update Common Prefab")]
    public static void BuildPrefab()
    {
        GameObject entrancePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EntrancePrefabPath);
        GameObject exitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ExitPrefabPath);
        if (entrancePrefab == null || exitPrefab == null) { Debug.LogError("[씬 연결] 기존 Entrance 또는 Exit 프리팹을 찾지 못했습니다."); return; }
        EnsureFolder("Assets/_Project/Scenes", "SceneConnections");
        EnsureFolder("Assets/_Project/Scenes/SceneConnections", "Prefabs");

        GameObject root = new GameObject("[SceneConnections]");
        root.AddComponent<SceneConnectionsAuthoring>();
        Transform spawnRoot = new GameObject("SpawnPoints").transform; spawnRoot.SetParent(root.transform, false);
        Transform exitRoot = new GameObject("Exits").transform; exitRoot.SetParent(root.transform, false);
        CreateEntrance(entrancePrefab, spawnRoot, "LeftEntrance", true);
        CreateEntrance(entrancePrefab, spawnRoot, "RightEntrance", false);
        CreateExit(exitPrefab, exitRoot, "LeftExit");
        CreateExit(exitPrefab, exitRoot, "RightExit");
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        EditorPrefs.SetBool(BuildKey, true);
        AssetDatabase.SaveAssets();
        Debug.Log($"[씬 연결] 공통 프리팹을 생성했습니다: {PrefabPath}");
    }

    public static void CreateMissingForSelectedRoot()
    {
        SceneConnectionsAuthoring authoring = Selection.activeGameObject?.GetComponent<SceneConnectionsAuthoring>();
        if (authoring == null) { Debug.LogWarning("[씬 연결] [SceneConnections] 루트를 선택하세요."); return; }
        GameObject entrancePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EntrancePrefabPath);
        GameObject exitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ExitPrefabPath);
        Transform spawnRoot = EnsureChild(authoring.transform, "SpawnPoints");
        Transform exitRoot = EnsureChild(authoring.transform, "Exits");
        if (spawnRoot.Find("LeftEntrance") == null) CreateEntrance(entrancePrefab, spawnRoot, "LeftEntrance", true);
        if (spawnRoot.Find("RightEntrance") == null) CreateEntrance(entrancePrefab, spawnRoot, "RightEntrance", false);
        if (exitRoot.Find("LeftExit") == null) CreateExit(exitPrefab, exitRoot, "LeftExit");
        if (exitRoot.Find("RightExit") == null) CreateExit(exitPrefab, exitRoot, "RightExit");
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(authoring.gameObject.scene);
        Debug.Log("[씬 연결] 누락된 오브젝트만 생성했습니다. 기존 Transform은 변경하지 않았습니다.", authoring);
    }

    private static void CreateEntrance(GameObject prefab, Transform parent, string id, bool faceRight)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.name = id;
        SerializedObject data = new SerializedObject(instance.GetComponent<PlayerSpawnPoint>());
        data.FindProperty("spawnPointId").stringValue = id;
        data.FindProperty("isDefaultSpawn").boolValue = false;
        data.FindProperty("faceRightOnSpawn").boolValue = faceRight;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateExit(GameObject prefab, Transform parent, string id)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.name = id;
        SerializedObject data = new SerializedObject(instance.GetComponent<StageExitTrigger>());
        data.FindProperty("exitId").stringValue = id;
        data.FindProperty("connectionEnabled").boolValue = false;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Transform EnsureChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null) return child;
        GameObject created = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(created, "누락된 씬 연결 구조 생성");
        created.transform.SetParent(parent, false);
        return created.transform;
    }

    private static void EnsureFolder(string parent, string name)
    {
        string path = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
    }
}
