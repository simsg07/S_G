using UnityEditor;
using UnityEngine;

public static class CranePartPrefabSetupUtility
{
    private const string PrefabFolder = "Assets/_Project/Prefabs/Objects/Dynamic/Crane";
    private const string MaterialFolder = "Assets/_Project/Materials/Debug";
    private const string SessionKey = "CraneFunctionalDebugPrefabs.Created.V5";

    [InitializeOnLoadMethod]
    private static void ScheduleCreateOnce()
    {
        if (SessionState.GetBool(SessionKey, false)) return;
        EditorApplication.delayCall += CreateOnceAfterReload;
    }

    private static void CreateOnceAfterReload()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.playModeStateChanged -= CreateAfterPlayMode;
            EditorApplication.playModeStateChanged += CreateAfterPlayMode;
            return;
        }
        CreateOrUpdatePartPrefabs();
        SessionState.SetBool(SessionKey, true);
    }

    private static void CreateAfterPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.playModeStateChanged -= CreateAfterPlayMode;
        EditorApplication.delayCall += CreateOnceAfterReload;
    }

    [MenuItem("Tools/_Project/Crane/Create Functional Debug Prefabs")]
    public static void CreateOrUpdatePartPrefabs()
    {
        EnsureFolders();
        Material crane = EnsureMaterial("MAT_Debug_CraneBody", new Color(0.95f, 0.55f, 0.12f, 1f));
        Material rail = EnsureMaterial("MAT_Debug_Rail", new Color(0.2f, 0.65f, 0.95f, 1f));
        Material point = EnsureMaterial("MAT_Debug_Point", new Color(0.95f, 0.9f, 0.15f, 1f));
        Material lever = EnsureMaterial("MAT_Debug_Lever", new Color(0.25f, 0.85f, 0.3f, 1f));
        Material carry = EnsureMaterial("MAT_Debug_CarryZone", new Color(0.75f, 0.25f, 0.95f, 1f));

        CreateRailPathPrefab(rail, point);
        CreateCraneBodyPrefab(crane, carry);
        CreateLeverPrefab(lever);
        MakeLegacyCraneSetDebugOnly(crane, rail, point, lever, carry);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CranePartPrefabSetupUtility] Opaque functional debug prefabs created. No PSD or Sprite assets were imported.");
    }

    private static void CreateRailPathPrefab(Material railMaterial, Material pointMaterial)
    {
        GameObject root = new GameObject("Crane_RailPath");
        try
        {
            Transform pointA = CreateCube(root.transform, "Rail_Point_A", new Vector3(-3f, 0f, 0f), Vector3.one * 0.35f, pointMaterial, false);
            Transform pointB = CreateCube(root.transform, "Rail_Point_B", new Vector3(3f, 0f, 0f), Vector3.one * 0.35f, pointMaterial, false);
            Transform railVisual = CreateCube(root.transform, "Rail_Debug_Visual", Vector3.zero, new Vector3(6f, 0.15f, 0.15f), railMaterial, false);
            CraneRailPath3D path = root.AddComponent<CraneRailPath3D>();
            SerializedObject data = new SerializedObject(path);
            data.FindProperty("pointA").objectReferenceValue = pointA;
            data.FindProperty("pointB").objectReferenceValue = pointB;
            data.FindProperty("debugRailVisual").objectReferenceValue = railVisual;
            data.ApplyModifiedPropertiesWithoutUndo();
            Save(root, "Crane_RailPath.prefab");
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void CreateCraneBodyPrefab(Material bodyMaterial, Material carryMaterial)
    {
        GameObject root = new GameObject("Crane_Cabin");
        try
        {
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;

            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, -0.2f, 0f);
            collider.size = new Vector3(2.2f, 1f, 0.6f);
            CraneObject crane = root.AddComponent<CraneObject>();
            CraneCarryZone3D carryZone = root.AddComponent<CraneCarryZone3D>();

            CreateCube(root.transform, "Cabin_Debug_Visual", collider.center, collider.size, bodyMaterial, false);
            CreateCube(root.transform, "CarryPlatform_Debug_Visual", new Vector3(0f, -1f, 0f), new Vector3(2.5f, 0.25f, 0.8f), bodyMaterial, true);
            CreateCube(root.transform, "CarryZone_Debug_Visual", new Vector3(0f, -0.82f, 0f), new Vector3(2.35f, 0.08f, 0.75f), carryMaterial, false);

            SerializedObject craneData = new SerializedObject(crane);
            craneData.FindProperty("rb").objectReferenceValue = body;
            craneData.FindProperty("mainCollider").objectReferenceValue = collider;
            craneData.FindProperty("carryZone").objectReferenceValue = carryZone;
            craneData.FindProperty("cabinYOffset").floatValue = -3f;
            craneData.FindProperty("snapToStartOnPlay").boolValue = true;
            craneData.FindProperty("lockZ").boolValue = true;
            craneData.FindProperty("fixedZ").floatValue = 0f;
            craneData.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject carryData = new SerializedObject(carryZone);
            carryData.FindProperty("carryBoxCenterOffset").vector3Value = new Vector3(0f, -0.25f, 0f);
            carryData.FindProperty("carryBoxSize").vector3Value = new Vector3(2.5f, 1.5f, 1f);
            carryData.ApplyModifiedPropertiesWithoutUndo();
            Save(root, "Crane_Body.prefab");
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void CreateLeverPrefab(Material material)
    {
        GameObject root = new GameObject("Lever");
        try
        {
            BoxCollider trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(1.2f, 1.5f, 1f);
            root.AddComponent<CraneLeverSwitch>();
            CreateCube(root.transform, "Lever_Debug_Visual", Vector3.zero, new Vector3(0.65f, 1.2f, 0.65f), material, false);
            Save(root, "Lever.prefab");
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void MakeLegacyCraneSetDebugOnly(Material cabinMaterial, Material railMaterial, Material pointMaterial, Material leverMaterial, Material carryMaterial)
    {
        const string path = "Assets/_Project/Prefabs/Objects/Crane/Crane_Set.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        if (root == null) return;
        try
        {
            foreach (CraneCableVisual3D cableVisual in root.GetComponentsInChildren<CraneCableVisual3D>(true))
            {
                Object.DestroyImmediate(cableVisual);
            }
            foreach (CraneRailVisualBuilder3D railVisual in root.GetComponentsInChildren<CraneRailVisualBuilder3D>(true))
            {
                Object.DestroyImmediate(railVisual);
            }
            foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                Object.DestroyImmediate(renderer);
            }
            foreach (LineRenderer renderer in root.GetComponentsInChildren<LineRenderer>(true))
            {
                Object.DestroyImmediate(renderer);
            }

            Transform rail = FindRecursive(root.transform, "Crane_Rail");
            Transform pointA = FindRecursive(root.transform, "Point_A");
            Transform pointB = FindRecursive(root.transform, "Point_B");
            Transform cabin = FindRecursive(root.transform, "Crane");
            Transform platform = FindRecursive(root.transform, "CarryPlatform");
            Transform carryZoneTransform = FindRecursive(root.transform, "CraneCarryZone3D");
            Transform lever = FindRecursive(root.transform, "Lever_Left") ?? FindRecursive(root.transform, "Lever");

            if (cabin != null && pointA != null)
            {
                Vector3 startPosition = pointA.position + Vector3.up * -3f;
                startPosition.z = 0f;
                cabin.position = startPosition;
                Rigidbody cabinBody = cabin.GetComponent<Rigidbody>();
                if (cabinBody != null)
                {
                    cabinBody.isKinematic = true;
                    cabinBody.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
                }
            }

            if (platform != null)
            {
                platform.localPosition = new Vector3(0f, 0.45f, 0f);
                platform.localScale = new Vector3(2.5f, 0.2f, 0.8f);
            }
            if (carryZoneTransform != null)
            {
                carryZoneTransform.localPosition = Vector3.zero;
                CraneCarryZone3D carryZone = carryZoneTransform.GetComponent<CraneCarryZone3D>();
                if (carryZone != null)
                {
                    SerializedObject carryData = new SerializedObject(carryZone);
                    carryData.FindProperty("carryLayerMask").intValue = 1 << 0;
                    carryData.FindProperty("carryBoxCenterOffset").vector3Value = new Vector3(0f, 1f, 0f);
                    carryData.FindProperty("carryBoxSize").vector3Value = new Vector3(2.6f, 2f, 1f);
                    carryData.FindProperty("matchPlayerPlatformVelocity").boolValue = true;
                    carryData.FindProperty("playerPlatformVelocityMultiplier").floatValue = 1f;
                    carryData.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            if (lever != null && pointA != null && pointB != null)
            {
                lever.name = "Lever_Left";
                Vector3 left = root.transform.InverseTransformPoint(pointA.position + new Vector3(-1f, -2.2f, 0f));
                lever.localPosition = left;

                Transform right = FindRecursive(root.transform, "Lever_Right");
                if (right == null)
                {
                    GameObject rightObject = Object.Instantiate(lever.gameObject, root.transform);
                    rightObject.name = "Lever_Right";
                    right = rightObject.transform;
                }
                Vector3 rightPosition = root.transform.InverseTransformPoint(pointB.position + new Vector3(1f, -2.2f, 0f));
                right.localPosition = rightPosition;
                right.localRotation = Quaternion.identity;
            }

            if (rail != null) CreateOrUpdateCube(rail, "Rail_Debug_Block", new Vector3(0f, -1.5f, 0f), new Vector3(6f, 0.18f, 0.3f), railMaterial);
            if (pointA != null) CreateOrUpdateCube(pointA, "Point_A_Debug_Block", Vector3.zero, Vector3.one * 0.35f, pointMaterial);
            if (pointB != null) CreateOrUpdateCube(pointB, "Point_B_Debug_Block", Vector3.zero, Vector3.one * 0.35f, pointMaterial);
            if (cabin != null)
            {
                CreateOrUpdateCube(cabin, "Cabin_Debug_Block", new Vector3(0f, -0.2f, 0f), new Vector3(2.2f, 1f, 0.6f), cabinMaterial);
                CreateOrUpdateCube(cabin, "CarryZone_Debug_Block", new Vector3(0f, -1.82f, 0f), new Vector3(2.3f, 0.08f, 0.7f), carryMaterial);
            }
            if (platform != null) CreateOrUpdateCube(platform, "Platform_Debug_Block", Vector3.zero, Vector3.one, cabinMaterial);
            if (lever != null) CreateOrUpdateCube(lever, "Lever_Debug_Block", Vector3.zero, new Vector3(0.65f, 1.2f, 0.65f), leverMaterial);
            Transform rightLever = FindRecursive(root.transform, "Lever_Right");
            if (rightLever != null) CreateOrUpdateCube(rightLever, "Lever_Debug_Block", Vector3.zero, new Vector3(0.65f, 1.2f, 0.65f), leverMaterial);

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static Transform FindRecursive(Transform root, string targetName)
    {
        if (root.name == targetName) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindRecursive(root.GetChild(i), targetName);
            if (found != null) return found;
        }
        return null;
    }

    private static void CreateOrUpdateCube(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        Transform existing = parent.Find(name);
        GameObject cube = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = position;
        cube.transform.localRotation = Quaternion.identity;
        cube.transform.localScale = scale;
        MeshRenderer renderer = cube.GetComponent<MeshRenderer>();
        if (renderer != null) renderer.sharedMaterial = material;
        BoxCollider visualCollider = cube.GetComponent<BoxCollider>();
        if (visualCollider != null) Object.DestroyImmediate(visualCollider);
    }

    private static Transform CreateCube(Transform parent, string name, Vector3 position, Vector3 scale, Material material, bool keepCollider)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = position;
        cube.transform.localScale = scale;
        cube.GetComponent<MeshRenderer>().sharedMaterial = material;
        if (!keepCollider) Object.DestroyImmediate(cube.GetComponent<BoxCollider>());
        return cube.transform;
    }

    private static Material EnsureMaterial(string name, Color color)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        material.SetFloat("_Surface", 0f);
        material.SetFloat("_Mode", 0f);
        material.renderQueue = -1;
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        return material;
    }

    private static void Save(GameObject root, string file) => PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{file}");

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/_Project", "Materials");
        EnsureFolder("Assets/_Project/Materials", "Debug");
        EnsureFolder("Assets/_Project/Prefabs/Objects", "Dynamic");
        EnsureFolder("Assets/_Project/Prefabs/Objects/Dynamic", "Crane");
    }

    private static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{child}")) AssetDatabase.CreateFolder(parent, child);
    }
}
