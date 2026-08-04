using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class VerticalCranePrefabBuilder
{
    private const string PrefabPath = "Assets/_Project/Prefabs/Objects/Crane/VerticalCrane_Set.prefab";
    private const string HorizontalPrefabPath = "Assets/_Project/Prefabs/Objects/Crane/Crane_Set.prefab";
    private const string CraneArtPath = "Assets/_Project/Art/Objects/Dynamic/Crane/crane.psd";
    private const int DefaultSegmentCount = 8;

    [MenuItem("Tools/_Project/Crane/Create Vertical Crane Set")]
    public static void CreateVerticalCraneSet()
    {
        Dictionary<string, Sprite> sprites = LoadCraneSprites();
        Sprite hoist = Require(sprites, "Hoist");
        Sprite wire = Require(sprites, "wire");
        Sprite hook = Require(sprites, "hook");
        Sprite cableCar = Require(sprites, "cable car");
        float segmentLength = Mathf.Max(0.01f, wire.bounds.size.y);
        Material leverMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Debug/MAT_Debug_Lever.mat");

        GameObject root = new GameObject("VerticalCrane_Set");
        try
        {
            Transform fixedTop = Child(root.transform, "FixedTopRoot", Vector3.zero);
            CreateSprite(fixedTop, "CraneBodyVisual", hoist, Vector3.zero, 12);
            Transform connector = CreateSprite(fixedTop, "RopeConnectorVisual", wire,
                new Vector3(0f, -hoist.bounds.extents.y, 0f), 11);
            connector.localScale = new Vector3(1f, 0.2f, 1f);
            Transform topAnchor = Child(fixedTop, "RopeTopAnchor", connector.localPosition);

            Transform leverRoot = Child(root.transform, "LeverRoot", new Vector3(-2f, -1f, 0f));
            Transform leverVisual = CreateCubeVisual(leverRoot, "LeverVisual", Vector3.zero,
                new Vector3(0.65f, 1.2f, 0.65f), leverMaterial);
            Transform triggerRoot = Child(leverRoot, "InteractionTrigger", Vector3.zero);
            BoxCollider interactionTrigger = triggerRoot.gameObject.AddComponent<BoxCollider>();
            interactionTrigger.isTrigger = true;
            interactionTrigger.size = new Vector3(1.5f, 1.8f, 1.2f);
            CraneLeverSwitch lever = triggerRoot.gameObject.AddComponent<CraneLeverSwitch>();

            Transform ropeRoot = Child(root.transform, "RopeVisualRoot", connector.localPosition);
            List<SpriteRenderer> segments = new List<SpriteRenderer>(DefaultSegmentCount);
            for (int i = 0; i < DefaultSegmentCount; i++)
            {
                Transform segment = CreateSprite(ropeRoot, $"RopeSegment_{i:000}", wire,
                    new Vector3(0f, -(i + 0.5f) * segmentLength, 0f), 11);
                segments.Add(segment.GetComponent<SpriteRenderer>());
            }

            Transform moving = Child(root.transform, "MovingAssemblyRoot", connector.localPosition);
            Rigidbody body = moving.gameObject.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;

            Transform hookVisual = CreateSprite(moving, "HookVisual", hook,
                new Vector3(0f, -hook.bounds.extents.y, 0f), 12);
            Transform bottomAnchor = Child(moving, "RopeBottomAnchor", Vector3.zero);
            Transform platformVisual = CreateSprite(moving, "PlatformVisual", cableCar,
                new Vector3(0f, -hook.bounds.size.y - cableCar.bounds.extents.y, 0f), 10);

            Transform colliderRoot = Child(moving, "PlatformCollider", platformVisual.localPosition);
            BoxCollider platformCollider = colliderRoot.gameObject.AddComponent<BoxCollider>();
            platformCollider.center = Vector3.zero;
            platformCollider.size = new Vector3(
                Mathf.Max(0.5f, cableCar.bounds.size.x),
                Mathf.Max(0.15f, cableCar.bounds.size.y * 0.25f),
                0.8f);

            Transform carryRoot = Child(moving, "CarryArea", platformVisual.localPosition);
            CraneCarryZone3D carry = carryRoot.gameObject.AddComponent<CraneCarryZone3D>();
            Set(carry, "carryBoxCenterOffset", new Vector3(0f, cableCar.bounds.extents.y + 0.35f, 0f));
            Set(carry, "carryBoxSize", new Vector3(
                Mathf.Max(1f, cableCar.bounds.size.x * 0.9f), 0.8f, 0.9f));
            Set(carry, "matchPlayerPlatformVelocity", true);
            Set(carry, "playerPlatformVelocityMultiplier", 1f);

            VerticalCraneController3D controller = root.AddComponent<VerticalCraneController3D>();
            Set(controller, "fixedTopRoot", fixedTop);
            Set(controller, "ropeTopAnchor", topAnchor);
            Set(controller, "ropeVisualRoot", ropeRoot);
            Set(controller, "movingAssemblyRoot", moving);
            Set(controller, "ropeBottomAnchor", bottomAnchor);
            Set(controller, "movingRigidbody", body);
            Set(controller, "carryZone", carry);
            Set(controller, "leverInteraction", lever);
            Set(controller, "ropeSegmentCount", DefaultSegmentCount);
            Set(controller, "ropeSegmentLength", segmentLength);
            Set(controller, "moveSpeed", 2f);
            Set(controller, "waitTimeAtTop", 1f);
            Set(controller, "waitTimeAtBottom", 1f);
            Set(controller, "startMovingDown", true);
            Set(controller, "autoLoop", false);
            SetArray(controller, "ropeSegmentRenderers", segments);
            Set(lever, "targetVerticalCrane", controller);
            Set(lever, "autoFindSingleCraneIfMissing", false);
            Set(lever, "activationDelay", 3f);
            Set(lever, "autoLoop", false);
            Set(lever, "canRetriggerWhileMoving", false);
            Set(lever, "canCancelDuringDelay", false);
            Set(lever, "interactionTrigger", interactionTrigger);
            Set(lever, "playerLayerMask", 1 << 13);
            Set(lever, "leverRenderer", leverVisual.GetComponent<Renderer>());

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            if (!success) throw new System.InvalidOperationException($"Failed to save {PrefabPath}");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateCreatedPrefab();
            Debug.Log($"[VerticalCranePrefabBuilder] Created and validated {PrefabPath}; segmentLength={segmentLength:0.###}, maxDrop={segmentLength * DefaultSegmentCount:0.###}");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [MenuItem("Tools/_Project/Crane/Upgrade Vertical Crane Rope Anchors")]
    public static void UpgradeVerticalCraneRopeAnchors()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            VerticalCraneController3D controller = root.GetComponent<VerticalCraneController3D>();
            Transform fixedTop = root.transform.Find("FixedTopRoot");
            Transform moving = root.transform.Find("MovingAssemblyRoot");
            Transform connector = fixedTop != null ? fixedTop.Find("RopeConnectorVisual") : null;
            SpriteRenderer hookRenderer = moving != null ? moving.Find("HookVisual")?.GetComponent<SpriteRenderer>() : null;
            if (controller == null || fixedTop == null || moving == null || connector == null || hookRenderer == null)
                throw new MissingReferenceException("Vertical Crane structure is incomplete; Rope Anchors cannot be derived.");

            Transform topAnchor = fixedTop.Find("RopeTopAnchor");
            if (topAnchor == null) topAnchor = Child(fixedTop, "RopeTopAnchor", connector.localPosition);

            Transform bottomAnchor = moving.Find("RopeBottomAnchor") ?? moving.Find("MovingRopeAttachPoint");
            if (bottomAnchor == null) bottomAnchor = Child(moving, "RopeBottomAnchor", Vector3.zero);
            bottomAnchor.name = "RopeBottomAnchor";
            Vector3 hookTopWorld = new Vector3(
                hookRenderer.bounds.center.x,
                hookRenderer.bounds.max.y,
                hookRenderer.bounds.center.z);
            bottomAnchor.position = hookTopWorld;

            Set(controller, "ropeTopAnchor", topAnchor);
            Set(controller, "ropeBottomAnchor", bottomAnchor);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            if (!success) throw new System.InvalidOperationException($"Failed to save {PrefabPath}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ValidateCreatedPrefab();
        Debug.Log("[VerticalCranePrefabBuilder] RopeTopAnchor and RopeBottomAnchor upgraded from the actual connector and HookVisual bounds.");
    }

    [MenuItem("Tools/_Project/Crane/Configure And Validate All Crane Levers")]
    public static void ConfigureAndValidateAllCraneLevers()
    {
        CreateVerticalCraneSet();
        ConfigureHorizontalLeverPrefab();
        ValidateCreatedPrefab();
        ValidateHorizontalLeverPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[VerticalCranePrefabBuilder] Horizontal and Vertical Crane levers configured and validated.");
    }

    private static void ConfigureHorizontalLeverPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(HorizontalPrefabPath);
        try
        {
            CraneObject crane = root.GetComponentInChildren<CraneObject>(true);
            if (crane == null) throw new MissingComponentException("Horizontal CraneObject is missing.");
            CraneLeverSwitch[] levers = root.GetComponentsInChildren<CraneLeverSwitch>(true);
            if (levers.Length == 0) throw new MissingComponentException("Horizontal Crane has no Lever.");
            foreach (CraneLeverSwitch lever in levers)
            {
                Collider trigger = lever.GetComponent<Collider>();
                if (trigger == null) throw new MissingComponentException($"{lever.name} interaction trigger is missing.");
                trigger.isTrigger = true;
                Renderer visual = lever.GetComponentInChildren<Renderer>(true);
                Set(lever, "targetCrane", crane);
                Set(lever, "targetVerticalCrane", null);
                Set(lever, "interactionTrigger", trigger);
                Set(lever, "activationDelay", 3f);
                Set(lever, "autoLoop", false);
                Set(lever, "canRetriggerWhileMoving", false);
                Set(lever, "canCancelDuringDelay", false);
                Set(lever, "playerLayerMask", 1 << 13);
                if (visual != null) Set(lever, "leverRenderer", visual);
            }
            PrefabUtility.SaveAsPrefabAsset(root, HorizontalPrefabPath, out bool success);
            if (!success) throw new System.InvalidOperationException($"Failed to save {HorizontalPrefabPath}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ValidateHorizontalLeverPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HorizontalPrefabPath);
        CraneObject crane = prefab != null ? prefab.GetComponentInChildren<CraneObject>(true) : null;
        if (crane == null) throw new MissingComponentException("Horizontal CraneObject is missing.");
        CraneLeverSwitch[] levers = prefab.GetComponentsInChildren<CraneLeverSwitch>(true);
        if (levers.Length == 0) throw new MissingComponentException("Horizontal Crane Lever is missing.");
        foreach (CraneLeverSwitch lever in levers)
        {
            SerializedObject data = new SerializedObject(lever);
            if (data.FindProperty("targetCrane").objectReferenceValue != crane)
                throw new MissingReferenceException($"{lever.name} targetCrane is missing.");
            if (data.FindProperty("interactionTrigger").objectReferenceValue == null)
                throw new MissingReferenceException($"{lever.name} interactionTrigger is missing.");
            if (!Mathf.Approximately(data.FindProperty("activationDelay").floatValue, 3f))
                throw new System.InvalidOperationException($"{lever.name} activationDelay must default to 3 seconds.");
        }
    }

    public static void ValidateCreatedPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) throw new System.InvalidOperationException("Vertical Crane prefab is missing.");
        VerticalCraneController3D controller = prefab.GetComponent<VerticalCraneController3D>();
        if (controller == null) throw new MissingComponentException("VerticalCraneController3D is missing.");
        SerializedObject data = new SerializedObject(controller);
        string[] references = { "fixedTopRoot", "ropeTopAnchor", "ropeVisualRoot", "movingAssemblyRoot", "ropeBottomAnchor", "movingRigidbody", "carryZone", "leverInteraction" };
        foreach (string reference in references)
            if (data.FindProperty(reference).objectReferenceValue == null)
                throw new MissingReferenceException($"Vertical Crane reference '{reference}' is missing.");
        SpriteRenderer[] renderers = prefab.transform.Find("RopeVisualRoot")?.GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers == null || renderers.Length != DefaultSegmentCount)
            throw new MissingReferenceException($"Expected {DefaultSegmentCount} rope segments, found {(renderers == null ? 0 : renderers.Length)}.");
        foreach (SpriteRenderer renderer in renderers)
            if (renderer.sprite == null) throw new MissingReferenceException($"{renderer.name} has no sprite.");
        if (prefab.transform.Find("MovingAssemblyRoot/PlatformCollider")?.GetComponent<BoxCollider>() == null)
            throw new MissingComponentException("PlatformCollider is missing.");
        if (prefab.transform.Find("FixedTopRoot/RopeTopAnchor") == null || prefab.transform.Find("MovingAssemblyRoot/RopeBottomAnchor") == null)
            throw new MissingReferenceException("Explicit Rope Anchor hierarchy is missing.");
        CraneLeverSwitch lever = prefab.GetComponentInChildren<CraneLeverSwitch>(true);
        if (lever == null || prefab.GetComponentsInChildren<CraneLeverSwitch>(true).Length != 1)
            throw new MissingComponentException("Exactly one LeverInteraction3D is required.");
        SerializedObject leverData = new SerializedObject(lever);
        if (leverData.FindProperty("targetVerticalCrane").objectReferenceValue != controller)
            throw new MissingReferenceException("Vertical Lever target is missing.");
    }

    private static Dictionary<string, Sprite> LoadCraneSprites()
    {
        Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>(System.StringComparer.OrdinalIgnoreCase);
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(CraneArtPath))
            if (asset is Sprite sprite && !sprites.ContainsKey(sprite.name)) sprites.Add(sprite.name, sprite);
        return sprites;
    }

    private static Sprite Require(Dictionary<string, Sprite> sprites, string name)
    {
        if (sprites.TryGetValue(name, out Sprite sprite)) return sprite;
        throw new MissingReferenceException($"Sprite '{name}' was not found in {CraneArtPath}.");
    }

    private static Transform Child(Transform parent, string name, Vector3 localPosition)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = localPosition;
        return child.transform;
    }

    private static Transform CreateSprite(Transform parent, string name, Sprite sprite, Vector3 position, int sortingOrder)
    {
        Transform child = Child(parent, name, position);
        SpriteRenderer renderer = child.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
        return child;
    }

    private static Transform CreateCubeVisual(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = position;
        cube.transform.localScale = scale;
        cube.GetComponent<MeshRenderer>().sharedMaterial = material;
        Object.DestroyImmediate(cube.GetComponent<BoxCollider>());
        return cube.transform;
    }

    private static void Set(Object target, string name, Object value)
    {
        SerializedObject data = new SerializedObject(target);
        data.FindProperty(name).objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Set(Object target, string name, float value)
    {
        SerializedObject data = new SerializedObject(target);
        data.FindProperty(name).floatValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Set(Object target, string name, int value)
    {
        SerializedObject data = new SerializedObject(target);
        data.FindProperty(name).intValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Set(Object target, string name, bool value)
    {
        SerializedObject data = new SerializedObject(target);
        data.FindProperty(name).boolValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Set(Object target, string name, Vector3 value)
    {
        SerializedObject data = new SerializedObject(target);
        data.FindProperty(name).vector3Value = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetArray(Object target, string name, List<SpriteRenderer> values)
    {
        SerializedObject data = new SerializedObject(target);
        SerializedProperty array = data.FindProperty(name);
        array.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        data.ApplyModifiedPropertiesWithoutUndo();
    }
}
