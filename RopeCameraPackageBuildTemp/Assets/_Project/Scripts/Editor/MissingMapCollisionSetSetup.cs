using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class MissingMapCollisionSetSetup
{
    private const string SceneFolder = "Assets/_Project/Scenes/Stages/ImportedSummerCampMap/";
    private const string PrefabFolder = "Assets/_Project/Scenes/SceneConnections/Prefabs/Collision";

    private static readonly string[] SceneNames =
    {
        "hallwa_03", "Item_Room_02", "hallwa_04", "hallwa_05",
        "Boss_Hint_Room", "hallwa_06", "Boss_Room"
    };

    [MenuItem("Tools/Scene Connections/Generate Missing Map Collisions")]
    public static void ApplyFromMenu() => Apply();

    private static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[Collision Set] Play Mode에서는 씬을 수정하지 않습니다. Edit Mode에서 메뉴를 다시 실행하세요.");
            return;
        }

        CreatePrefabAssetsIfMissing();
        string previousScenePath = SceneManager.GetActiveScene().path;
        foreach (string sceneName in SceneNames) AddMissingSet(sceneName);
        if (!string.IsNullOrEmpty(previousScenePath) && File.Exists(previousScenePath))
            EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);

        AssetDatabase.SaveAssets();
        Debug.Log("[Collision Set] 7개 대상 씬에 누락된 수동 편집용 Ground/Wall 템플릿을 저장했습니다. 기존 오브젝트는 변경하지 않았습니다.");
    }

    private static void AddMissingSet(string sceneName)
    {
        string path = SceneFolder + sceneName + ".unity";
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
        {
            Debug.LogError("[Collision Set] 씬 파일을 찾을 수 없습니다: " + path);
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        GameObject root = FindRoot(scene, "Collision");
        bool changed = false;
        if (root == null)
        {
            root = new GameObject("Collision");
            Undo.RegisterCreatedObjectUndo(root, "Collision 세트 생성");
            changed = true;
        }

        Transform groundGroup = EnsureGroup(root.transform, "Ground", ref changed);
        Transform wallGroup = EnsureGroup(root.transform, "Wall", ref changed);
        Bounds bounds = GetVisualBounds();

        if (groundGroup.GetComponentsInChildren<BoxCollider>(true).Length == 0)
        {
            CreateTemplate(
                groundGroup, "Ground_01", LayerMask.NameToLayer("Ground"),
                new Vector3(bounds.center.x, bounds.min.y - 2f, 0f), new Vector3(4f, 0.25f, 1f));
            changed = true;
        }

        if (wallGroup.GetComponentsInChildren<BoxCollider>(true).Length == 0)
        {
            CreateTemplate(
                wallGroup, "Wall_01", LayerMask.NameToLayer("Wall"),
                new Vector3(bounds.min.x - 2f, bounds.center.y, 0f), new Vector3(0.25f, 4f, 1f));
            changed = true;
        }

        if (!changed)
        {
            Debug.Log($"[Collision Set] {sceneName}: 기존 Collision 세트를 보존하고 건너뛰었습니다.", root);
            return;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Collision Set] {sceneName}: 비활성 Ground_01/Wall_01 템플릿을 저장했습니다.", root);
    }

    private static Transform EnsureGroup(Transform parent, string name, ref bool changed)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing;
        var group = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(group, name + " 그룹 생성");
        group.transform.SetParent(parent, false);
        changed = true;
        return group.transform;
    }

    private static void CreateTemplate(Transform parent, string name, int layer, Vector3 position, Vector3 size)
    {
        var template = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(template, name + " 템플릿 생성");
        template.layer = layer >= 0 ? layer : 0;
        template.transform.SetParent(parent, false);
        template.transform.position = position;
        template.transform.rotation = Quaternion.identity;
        template.transform.localScale = Vector3.one;
        BoxCollider box = Undo.AddComponent<BoxCollider>(template);
        box.center = Vector3.zero;
        box.size = size;
        box.isTrigger = false;
        template.SetActive(false);
    }

    private static Bounds GetVisualBounds()
    {
        TilemapRenderer renderer = Object.FindFirstObjectByType<TilemapRenderer>(FindObjectsInactive.Include);
        return renderer != null ? renderer.bounds : new Bounds(Vector3.zero, new Vector3(20f, 10f, 1f));
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects()) if (root.name == name) return root;
        return null;
    }

    private static void CreatePrefabAssetsIfMissing()
    {
        EnsureFolder(PrefabFolder);
        CreateSurfacePrefabIfMissing("GroundCollider", "Ground", new Vector3(4f, 0.25f, 1f));
        CreateSurfacePrefabIfMissing("WallCollider", "Wall", new Vector3(0.25f, 4f, 1f));

        string path = PrefabFolder + "/MapCollisionSet.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = new GameObject("MapCollisionSet");
        var ground = new GameObject("Ground"); ground.transform.SetParent(root.transform, false);
        var wall = new GameObject("Wall"); wall.transform.SetParent(root.transform, false);
        GameObject groundPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/GroundCollider.prefab");
        GameObject wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/WallCollider.prefab");
        GameObject groundInstance = (GameObject)PrefabUtility.InstantiatePrefab(groundPrefab); groundInstance.name = "Ground_01"; groundInstance.transform.SetParent(ground.transform, false);
        GameObject wallInstance = (GameObject)PrefabUtility.InstantiatePrefab(wallPrefab); wallInstance.name = "Wall_01"; wallInstance.transform.SetParent(wall.transform, false);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    private static void CreateSurfacePrefabIfMissing(string name, string layerName, Vector3 size)
    {
        string path = PrefabFolder + "/" + name + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = new GameObject(name);
        int layer = LayerMask.NameToLayer(layerName);
        root.layer = layer >= 0 ? layer : 0;
        BoxCollider box = root.AddComponent<BoxCollider>();
        box.center = Vector3.zero;
        box.size = size;
        box.isTrigger = false;
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    private static void EnsureFolder(string path)
    {
        string current = "Assets";
        string[] parts = path.Split('/');
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
