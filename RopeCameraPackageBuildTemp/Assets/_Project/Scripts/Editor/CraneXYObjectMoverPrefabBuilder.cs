#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class CraneXYObjectMoverPrefabBuilder
{
    private const string PrefabPath = "Assets/_Project/Prefabs/Objects/Crane/Crane_XY_ObjectMover_Set.prefab";
    private const string CraneArtPath = "Assets/_Project/Art/Objects/Dynamic/Crane/crane.psd";
    private const int SegmentCount = 8;

    static CraneXYObjectMoverPrefabBuilder()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            EditorApplication.delayCall += CreatePrefabIfMissing;
    }

    [MenuItem("Tools/_Project/Crane/Create XY Object Mover Crane")]
    public static void CreatePrefabIfMissing()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            Debug.LogWarning("[CraneXYBuilder] Crane_XY_ObjectMover_Set already exists; no file was overwritten.");
            return;
        }

        Dictionary<string, Sprite> sprites = LoadCraneSprites();
        Sprite hoist = Require(sprites, "Hoist");
        Sprite wire = Require(sprites, "wire");
        Sprite hook = Require(sprites, "hook");
        Sprite platform = Require(sprites, "cable car");
        float segmentLength = Mathf.Max(0.01f, wire.bounds.size.y);
        Material leverMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Debug/MAT_Debug_Lever.mat");

        GameObject root = new GameObject("Crane_XY_ObjectMover_Set");
        try
        {
            Transform fixedRoot = Child(root.transform, "FixedRoot", Vector3.zero);
            Transform railVisual = CreateCube(fixedRoot, "RailVisual", new Vector3(0f, 1.5f, 0f), new Vector3(8.5f, 0.2f, 0.35f), leverMaterial);
            Transform leftPoint = Child(fixedRoot, "HorizontalLeftPoint", new Vector3(-4f, 0f, 0f));
            Transform rightPoint = Child(fixedRoot, "HorizontalRightPoint", new Vector3(4f, 0f, 0f));

            Transform horizontal = Child(root.transform, "HorizontalMovingRoot", new Vector3(-4f, 0f, 0f));
            CreateSprite(horizontal, "CraneBodyVisual", hoist, Vector3.zero, 12);
            Transform topAnchor = Child(horizontal, "RopeTopAnchor", new Vector3(0f, -hoist.bounds.extents.y, 0f));
            Transform ropeRoot = Child(horizontal, "RopeVisualRoot", Vector3.zero);
            List<SpriteRenderer> segments = new List<SpriteRenderer>(SegmentCount);
            for (int i = 0; i < SegmentCount; i++)
            {
                Transform segment = CreateSprite(ropeRoot, $"RopeSegment_{i:000}", wire,
                    topAnchor.localPosition + Vector3.down * ((i + 0.5f) * segmentLength), 10);
                segments.Add(segment.GetComponent<SpriteRenderer>());
            }

            Transform vertical = Child(horizontal, "VerticalMovingRoot", topAnchor.localPosition);
            Transform bottomAnchor = Child(vertical, "RopeBottomAnchor", Vector3.zero);
            CreateSprite(vertical, "HookVisual", hook, new Vector3(0f, -hook.bounds.extents.y, 0f), 12);
            Transform carryAnchor = Child(vertical, "CarryAnchor", new Vector3(0f, -hook.bounds.size.y - 0.2f, 0f));
            CreateSprite(vertical, "CarryPlatformVisual", platform,
                carryAnchor.localPosition + Vector3.down * platform.bounds.extents.y, 11);
            Transform carryAreaRoot = Child(vertical, "CarryArea", carryAnchor.localPosition);
            BoxCollider carryTrigger = carryAreaRoot.gameObject.AddComponent<BoxCollider>();
            carryTrigger.isTrigger = true;
            carryTrigger.center = new Vector3(0f, -0.15f, 0f);
            carryTrigger.size = new Vector3(Mathf.Max(1f, platform.bounds.size.x * 0.9f), 1f, 0.9f);
            CraneCarryZone3D carryZone = carryAreaRoot.gameObject.AddComponent<CraneCarryZone3D>();
            Set(carryZone, "carryLayerMask", 1);
            Set(carryZone, "carryRigidbodies", true);
            Set(carryZone, "carryTransformsWithoutRigidbody", false);
            Set(carryZone, "autoAttach", true);
            Set(carryZone, "releaseAtDestination", false);
            Set(carryZone, "preserveWorldPositionOnAttach", true);
            Set(carryZone, "carryAnchor", carryAnchor);
            Set(carryZone, "carryOffset", Vector3.zero);
            Set(carryZone, "maximumCarryMass", 100f);
            Set(carryZone, "allowPlayerCarry", false);
            Set(carryZone, "maximumCarriedTargets", 1);
            Set(carryZone, "carryBoxCenterOffset", carryTrigger.center);
            Set(carryZone, "carryBoxSize", carryTrigger.size);

            CraneXYController3D controller = root.AddComponent<CraneXYController3D>();
            CraneXYLeverSwitch3D horizontalLever = CreateLever(root.transform, "HorizontalLeverRoot", new Vector3(-5f, -1f, 0f),
                "HorizontalLeverVisual", CraneXYAxis.Horizontal, controller, leverMaterial);
            CraneXYLeverSwitch3D verticalLever = CreateLever(root.transform, "VerticalLeverRoot", new Vector3(5f, -1f, 0f),
                "VerticalLeverVisual", CraneXYAxis.Vertical, controller, leverMaterial);

            Set(controller, "fixedRoot", fixedRoot);
            Set(controller, "horizontalMovingRoot", horizontal);
            Set(controller, "verticalMovingRoot", vertical);
            Set(controller, "horizontalLeftPoint", leftPoint);
            Set(controller, "horizontalRightPoint", rightPoint);
            Set(controller, "ropeTopAnchor", topAnchor);
            Set(controller, "ropeBottomAnchor", bottomAnchor);
            Set(controller, "ropeVisualRoot", ropeRoot);
            Set(controller, "horizontalLever", horizontalLever);
            Set(controller, "verticalLever", verticalLever);
            Set(controller, "carryArea", carryZone);
            Set(controller, "carryAnchor", carryAnchor);
            Set(controller, "horizontalMoveSpeed", 2f);
            Set(controller, "horizontalAcceleration", 4f);
            Set(controller, "horizontalDeceleration", 4f);
            Set(controller, "horizontalArrivalTolerance", 0.02f);
            Set(controller, "startHorizontalSide", 0);
            Set(controller, "horizontalActivationDelay", 3f);
            Set(controller, "ropeSegmentCount", SegmentCount);
            Set(controller, "ropeSegmentLength", segmentLength);
            Set(controller, "verticalMoveSpeed", 2f);
            Set(controller, "verticalAcceleration", 4f);
            Set(controller, "verticalDeceleration", 4f);
            Set(controller, "verticalArrivalTolerance", 0.02f);
            Set(controller, "startVerticalSide", 0);
            Set(controller, "verticalActivationDelay", 3f);
            Set(controller, "ropeBottomOverlap", 0.03f);
            SetArray(controller, "ropeSegmentRenderers", segments);
            Set(controller, "allowSimultaneousAxisMovement", false);
            Set(controller, "queueBlockedCommand", false);
            Set(controller, "horizontalObstacleMask", 7936);
            Set(controller, "verticalObstacleMask", 7936);
            Set(controller, "includeCarriedObjectBounds", true);
            Set(controller, "stopOnObstruction", true);
            Set(controller, "obstructionCheckPadding", 0.05f);
            Set(controller, "carryAreaCollider", carryTrigger);
            Set(controller, "debugMode", false);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            if (!success) throw new InvalidOperationException("Failed to save " + PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ValidatePrefab();
        Debug.Log($"[CraneXYBuilder] Created and validated {PrefabPath} without modifying existing Crane prefabs.");
    }

    [MenuItem("Tools/_Project/Crane/Validate XY Object Mover Crane")]
    public static void ValidatePrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) throw new MissingReferenceException(PrefabPath + " is missing.");
        CraneXYController3D controller = prefab.GetComponent<CraneXYController3D>();
        if (controller == null) throw new MissingComponentException("CraneXYController3D is missing.");
        string[] paths = {
            "FixedRoot/RailVisual", "FixedRoot/HorizontalLeftPoint", "FixedRoot/HorizontalRightPoint",
            "HorizontalMovingRoot/CraneBodyVisual", "HorizontalMovingRoot/RopeTopAnchor",
            "HorizontalMovingRoot/RopeVisualRoot", "HorizontalMovingRoot/VerticalMovingRoot/RopeBottomAnchor",
            "HorizontalMovingRoot/VerticalMovingRoot/HookVisual", "HorizontalMovingRoot/VerticalMovingRoot/CarryAnchor",
            "HorizontalMovingRoot/VerticalMovingRoot/CarryArea", "HorizontalLeverRoot/InteractionTrigger",
            "VerticalLeverRoot/InteractionTrigger"
        };
        foreach (string path in paths)
            if (prefab.transform.Find(path) == null) throw new MissingReferenceException(path + " is missing.");
        SpriteRenderer[] rope = prefab.transform.Find("HorizontalMovingRoot/RopeVisualRoot").GetComponentsInChildren<SpriteRenderer>(true);
        if (rope.Length != SegmentCount) throw new InvalidOperationException($"Expected {SegmentCount} rope segments, found {rope.Length}.");
        foreach (SpriteRenderer renderer in rope)
            if (renderer.sprite == null || !string.Equals(renderer.sprite.name, "wire", StringComparison.OrdinalIgnoreCase))
                throw new MissingReferenceException(renderer.name + " does not reuse the Crane wire Sprite.");
        if (prefab.GetComponentsInChildren<CraneXYLeverSwitch3D>(true).Length != 2)
            throw new InvalidOperationException("Exactly two axis levers are required.");
    }

    private static CraneXYLeverSwitch3D CreateLever(Transform parent, string rootName, Vector3 position,
        string visualName, CraneXYAxis axis, CraneXYController3D controller, Material material)
    {
        Transform leverRoot = Child(parent, rootName, position);
        Transform visual = CreateCube(leverRoot, visualName, Vector3.zero, new Vector3(0.55f, 1f, 0.55f), material);
        Transform triggerRoot = Child(leverRoot, "InteractionTrigger", Vector3.zero);
        BoxCollider trigger = triggerRoot.gameObject.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(1.5f, 1.8f, 1.2f);
        CraneXYLeverSwitch3D lever = triggerRoot.gameObject.AddComponent<CraneXYLeverSwitch3D>();
        Set(lever, "targetCrane", controller);
        Set(lever, "controlledAxis", (int)axis);
        Set(lever, "interactionTrigger", trigger);
        Set(lever, "playerLayerMask", 1 << 13);
        Set(lever, "leverRenderer", visual.GetComponent<Renderer>());
        return lever;
    }

    private static Dictionary<string, Sprite> LoadCraneSprites()
    {
        Dictionary<string, Sprite> result = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(CraneArtPath))
            if (asset is Sprite sprite && !result.ContainsKey(sprite.name)) result.Add(sprite.name, sprite);
        return result;
    }

    private static Sprite Require(Dictionary<string, Sprite> sprites, string name)
    {
        if (sprites.TryGetValue(name, out Sprite sprite)) return sprite;
        throw new MissingReferenceException($"Sprite '{name}' was not found in {CraneArtPath}.");
    }

    private static Transform Child(Transform parent, string name, Vector3 position)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = position;
        return child.transform;
    }

    private static Transform CreateSprite(Transform parent, string name, Sprite sprite, Vector3 position, int order)
    {
        Transform child = Child(parent, name, position);
        SpriteRenderer renderer = child.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        return child;
    }

    private static Transform CreateCube(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = position;
        cube.transform.localScale = scale;
        Object.DestroyImmediate(cube.GetComponent<Collider>());
        if (material != null) cube.GetComponent<MeshRenderer>().sharedMaterial = material;
        return cube.transform;
    }

    private static SerializedProperty Property(Object target, string name)
    {
        SerializedObject data = new SerializedObject(target);
        SerializedProperty property = data.FindProperty(name);
        if (property == null) throw new MissingFieldException(target.GetType().Name, name);
        return property;
    }

    private static void Set(Object target, string name, Object value) { SerializedObject d = new SerializedObject(target); d.FindProperty(name).objectReferenceValue = value; d.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Set(Object target, string name, bool value) { SerializedObject d = new SerializedObject(target); d.FindProperty(name).boolValue = value; d.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Set(Object target, string name, int value) { SerializedObject d = new SerializedObject(target); SerializedProperty p=d.FindProperty(name); if (p.propertyType == SerializedPropertyType.LayerMask) p.intValue=value; else p.intValue=value; d.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Set(Object target, string name, float value) { SerializedObject d = new SerializedObject(target); d.FindProperty(name).floatValue = value; d.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Set(Object target, string name, Vector3 value) { SerializedObject d = new SerializedObject(target); d.FindProperty(name).vector3Value = value; d.ApplyModifiedPropertiesWithoutUndo(); }
    private static void SetArray(Object target, string name, List<SpriteRenderer> values) { SerializedObject d = new SerializedObject(target); SerializedProperty p=d.FindProperty(name); p.arraySize=values.Count; for(int i=0;i<values.Count;i++) p.GetArrayElementAtIndex(i).objectReferenceValue=values[i]; d.ApplyModifiedPropertiesWithoutUndo(); }
}
#endif
