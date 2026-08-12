#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class CosmosLiftAssetBuilder
{
    private const string PrefabPath = "Assets/_Project/Prefabs/Enemies/CosmosLift.prefab";
    private const string MaterialFolder = "Assets/_Project/Art/Enemies/CosmosLift";

    [InitializeOnLoadMethod]
    private static void BuildMissingPrefabAfterReload()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            EditorApplication.delayCall += BuildPrefab;
        }
    }

    [MenuItem("Tools/Summer Camp/Cosmos Lift/Build Prefab _F12")]
    public static void BuildPrefab()
    {
        EnsureFolder("Assets/_Project/Art/Enemies", "CosmosLift");
        EnsureFolder("Assets/_Project/Prefabs", "Enemies");

        Material stemMaterial = GetOrCreateMaterial(MaterialFolder + "/CosmosLift_Stem.mat", new Color(0.04f, 0.27f, 0.29f));
        Material flowerMaterial = GetOrCreateMaterial(MaterialFolder + "/CosmosLift_Flower.mat", new Color(0.45f, 0.24f, 0.55f));
        Material nestMaterial = GetOrCreateMaterial(MaterialFolder + "/CosmosLift_Nest.mat", new Color(0.03f, 0.18f, 0.2f));

        GameObject root = new GameObject("Cosmos Lift (M_OBJ_005)");
        root.AddComponent<MonsterCore>();

        GameObject nest = CreatePrimitive("Nest", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.2f, 0f), new Vector3(1.7f, 0.2f, 0.7f), nestMaterial);
        GameObject stem = CreatePrimitive("Stem Visual", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.35f, 0.05f), new Vector3(0.42f, 0.7f, 0.35f), stemMaterial);
        GameObject receiver = new GameObject("Light Receiver");
        receiver.transform.SetParent(root.transform, false);
        receiver.transform.localPosition = new Vector3(0f, 0.45f, 0f);

        GameObject bud = CreatePrimitive("Bud Platform", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.65f, 0f), new Vector3(1.65f, 0.22f, 0.75f), flowerMaterial);
        bud.tag = "Platform";
        Rigidbody body = bud.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
        BoxCollider platformCollider = bud.AddComponent<BoxCollider>();
        platformCollider.size = new Vector3(1f, 0.8f, 1f);

        CosmosLift3D lift = root.AddComponent<CosmosLift3D>();
        SerializedObject serializedLift = new SerializedObject(lift);
        serializedLift.FindProperty("budPlatform").objectReferenceValue = bud.transform;
        serializedLift.FindProperty("budRigidbody").objectReferenceValue = body;
        serializedLift.FindProperty("platformCollider").objectReferenceValue = platformCollider;
        serializedLift.FindProperty("stemVisual").objectReferenceValue = stem.transform;
        serializedLift.FindProperty("lightReceiver").objectReferenceValue = receiver.transform;
        serializedLift.ApplyModifiedPropertiesWithoutUndo();

        MonsterCore core = root.GetComponent<MonsterCore>();
        core.visualRoot = bud.transform;
        core.monsterRigidbody = body;
        core.mainCollider = platformCollider;

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[CosmosLiftAssetBuilder] Built {PrefabPath}");
    }

    public static void BuildPrefabBatch()
    {
        BuildPrefab();
    }

    private static GameObject CreatePrimitive(string name, PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject created = GameObject.CreatePrimitive(type);
        created.name = name;
        created.transform.SetParent(parent, false);
        created.transform.localPosition = localPosition;
        created.transform.localScale = localScale;
        Renderer renderer = created.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = material;
        Collider collider = created.GetComponent<Collider>();
        if (collider != null) Object.DestroyImmediate(collider);
        return created;
    }

    private static Material GetOrCreateMaterial(string path, Color color)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
    }
}
#endif
