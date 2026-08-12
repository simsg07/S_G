using System.IO;
using UnityEditor;
using UnityEngine;

public static class MapTestPrefabBuilder
{
    private const string PrefabFolder = "Assets/_Project/Prefabs/Map";
    private const string GeneratedAssetFolder = "Assets/_Project/Art/Generated/MapTest";
    private const string ObstacleLayerName = "EnvironmentObstacle";

    [InitializeOnLoadMethod]
    private static void EnsureTestPrefabsOnLoad()
    {
        EditorApplication.delayCall += EnsureTestPrefabs;
    }

    [MenuItem("Tools/Project/Map/Create Test Map Prefabs")]
    public static void EnsureTestPrefabs()
    {
        EnsureFolder(PrefabFolder);
        EnsureFolder(GeneratedAssetFolder);
        EnsureEnvironmentObstacleLayer();

        Mesh cubeMesh = LoadOrCreateCubeMesh();
        Material wallMaterial = LoadOrCreateMaterial("Wall_Test_Material", new Color(0.38f, 0.41f, 0.44f, 1f));
        Material tileMaterial = LoadOrCreateMaterial("Tile_Test_Material", new Color(0.28f, 0.32f, 0.36f, 1f));
        Material floorMaterial = LoadOrCreateMaterial("Floor_Test_Material", new Color(0.22f, 0.24f, 0.27f, 1f));

        CreatePrefabIfMissing("Wall_Test", MapPieceType.Wall, new Vector3(1f, 3f, 0.3f), wallMaterial, cubeMesh);
        CreatePrefabIfMissing("Tile_Test", MapPieceType.Tile, new Vector3(1f, 1f, 0.3f), tileMaterial, cubeMesh);
        CreatePrefabIfMissing("Floor_Test", MapPieceType.Floor, new Vector3(4f, 1f, 0.3f), floorMaterial, cubeMesh);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void CreatePrefabIfMissing(string prefabName, MapPieceType pieceType, Vector3 size, Material material, Mesh mesh)
    {
        string prefabPath = $"{PrefabFolder}/{prefabName}.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
        {
            return;
        }

        int obstacleLayer = LayerMask.NameToLayer(ObstacleLayerName);

        GameObject root = new GameObject(prefabName);
        if (obstacleLayer >= 0)
        {
            root.layer = obstacleLayer;
        }

        MapPiece mapPiece = root.AddComponent<MapPiece>();
        SerializedObject serializedMapPiece = new SerializedObject(mapPiece);
        SetEnumProperty(serializedMapPiece, "pieceType", (int)pieceType);
        SetBoolProperty(serializedMapPiece, "useCollider", true);
        SetBoolProperty(serializedMapPiece, "blockMovement", true);
        SetBoolProperty(serializedMapPiece, "blockLineOfSight", true);
        SetBoolProperty(serializedMapPiece, "autoSetObstacleLayer", true);
        SetStringProperty(serializedMapPiece, "obstacleLayerName", ObstacleLayerName);
        serializedMapPiece.ApplyModifiedPropertiesWithoutUndo();

        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.size = size;
        collider.isTrigger = false;

        GameObject visual = new GameObject("Visual", typeof(MeshFilter), typeof(MeshRenderer));
        visual.transform.SetParent(root.transform, false);
        visual.transform.localScale = size;
        if (obstacleLayer >= 0)
        {
            visual.layer = obstacleLayer;
        }

        MeshFilter meshFilter = visual.GetComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        MeshRenderer meshRenderer = visual.GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;

        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        UnityEngine.Object.DestroyImmediate(root);
    }

    private static void SetBoolProperty(SerializedObject target, string propertyName, bool value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
        {
            property.boolValue = value;
        }
    }

    private static void SetEnumProperty(SerializedObject target, string propertyName, int value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
        {
            property.enumValueIndex = value;
        }
    }

    private static void SetStringProperty(SerializedObject target, string propertyName, string value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
        {
            property.stringValue = value;
        }
    }

    private static Mesh LoadOrCreateCubeMesh()
    {
        string path = $"{GeneratedAssetFolder}/MapTestCubeMesh.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null)
        {
            return mesh;
        }

        mesh = new Mesh { name = "Map Test Cube Mesh" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f), new Vector3(0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f), new Vector3(0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f)
        };
        mesh.triangles = new[]
        {
            0, 2, 1, 0, 3, 2,
            4, 5, 6, 4, 6, 7,
            0, 1, 5, 0, 5, 4,
            2, 3, 7, 2, 7, 6,
            1, 2, 6, 1, 6, 5,
            3, 0, 4, 3, 4, 7
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static Material LoadOrCreateMaterial(string name, Color color)
    {
        string path = $"{GeneratedAssetFolder}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
        {
            return material;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        material = new Material(shader) { name = name, color = color };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void EnsureFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private static void EnsureEnvironmentObstacleLayer()
    {
        if (LayerMask.NameToLayer(ObstacleLayerName) >= 0)
        {
            return;
        }

        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        for (int i = 8; i < layers.arraySize; i++)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(layer.stringValue))
            {
                continue;
            }

            layer.stringValue = ObstacleLayerName;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
            return;
        }

        Debug.LogWarning($"[MapTestPrefabBuilder] No empty user layer slot for {ObstacleLayerName}.");
    }
}
