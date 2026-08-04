#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class RopeSetPrefabSetupUtility
{
    private const string RopeFolder = "Assets/_Project/Prefabs/Objects/Rope";
    private const string RopeSetFolder = "Assets/_Project/Prefabs/Objects/RopeSets";
    private const string GravityFolder = "Assets/_Project/Prefabs/Objects/Gravity";
    private const string DebugMaterialFolder = "Assets/_Project/Materials/Debug";
    private const string WirePath = RopeFolder + "/Wire.prefab";
    private const string VinePath = RopeFolder + "/Vine.prefab";
    private const string BoxPath = GravityFolder + "/FallingBox.prefab";
    private const string CircleSpikePath = GravityFolder + "/CircleSpike.prefab";
    private const string CircleSpikeSpritePath = "Assets/_Project/Art/Sprites/Objects/CircleSpike.png";
    private const string WarningBoxSpritePath = "Assets/_Project/Art/Sprites/Objects/WarningBox.png";

    static RopeSetPrefabSetupUtility()
    {
        const string validationKey = "RopeSetPrefabSetupUtility.ValidatedV11";
        if (!SessionState.GetBool(validationKey, false) ||
            AssetDatabase.LoadAssetAtPath<GameObject>(RopeSetFolder + "/Wire_Box_Set.prefab") == null ||
            AssetDatabase.LoadAssetAtPath<GameObject>(RopeSetFolder + "/Wire_CircleSpike_Set.prefab") == null ||
            AssetDatabase.LoadAssetAtPath<GameObject>(RopeSetFolder + "/Vine_Box_Set.prefab") == null ||
            AssetDatabase.LoadAssetAtPath<GameObject>(RopeSetFolder + "/Vine_CircleSpike_Set.prefab") == null)
        {
            EditorApplication.delayCall += () =>
            {
                CreateOrUpdateRopeSetPrefabs();
                SessionState.SetBool(validationKey, true);
            };
        }

        const string vineRollingMigrationKey = "RopeSetPrefabSetupUtility.VineCircleSpikeRollingV1";
        if (!SessionState.GetBool(vineRollingMigrationKey, false))
        {
            EditorApplication.delayCall += () =>
            {
                string vinePath = RopeSetFolder + "/Vine_CircleSpike_Set.prefab";
                GameObject vinePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(vinePath);
                Transform payload = vinePrefab != null ? vinePrefab.transform.Find("ConnectedObject") : null;
                if (payload != null && payload.GetComponent<CircleSpikeProjectile3D>() == null)
                {
                    ConfigureCircleSpikeLauncher(vinePath);
                    AssetDatabase.SaveAssets();
                    ValidateCircleSpikeLauncher(vinePath);
                    Debug.Log("[RopeSetPrefabSetup] Added the missing CircleSpike rolling controller to Vine_CircleSpike_Set.");
                }
                SessionState.SetBool(vineRollingMigrationKey, true);
            };
        }

        const string warningBoxMigrationKey = "RopeSetPrefabSetupUtility.WarningBoxVisualV1";
        if (!SessionState.GetBool(warningBoxMigrationKey, false))
        {
            EditorApplication.delayCall += () =>
            {
                ConfigureWarningBoxAssets();
                SessionState.SetBool(warningBoxMigrationKey, true);
            };
        }
    }

    [MenuItem("Tools/Project/Objects/Create Or Update Rope Set Prefabs %#r")]
    public static void CreateOrUpdateRopeSetPrefabs()
    {
        EnsureFolder(RopeFolder);
        EnsureFolder(RopeSetFolder);
        EnsureFolder(GravityFolder);
        EnsureFolder(DebugMaterialFolder);
        Material wireMaterial = CreateOrUpdateOpaqueMaterial(DebugMaterialFolder + "/MAT_Debug_Wire.mat", new Color(0.24f, 0.55f, 0.78f, 1f));
        Material vineMaterial = CreateOrUpdateOpaqueMaterial(DebugMaterialFolder + "/MAT_Debug_Vine.mat", new Color(0.22f, 0.65f, 0.28f, 1f));
        PrepareStandaloneRopePrefab(WirePath, wireMaterial);
        PrepareStandaloneRopePrefab(VinePath, vineMaterial);
        ConfigureFallingBoxPrefab();
        CreateCircleSpikePrefab();
        CreateSet("Wire_Box_Set", WirePath, BoxPath, typeof(FallingBoxObject));
        CreateSet("Wire_CircleSpike_Set", WirePath, CircleSpikePath, typeof(CircleSpikeObject));
        CreateSet("Vine_Box_Set", VinePath, BoxPath, typeof(FallingBoxObject));
        CreateSet("Vine_CircleSpike_Set", VinePath, CircleSpikePath, typeof(CircleSpikeObject));
        ConfigureCircleSpikeLauncher(RopeSetFolder + "/Wire_CircleSpike_Set.prefab");
        ConfigureCircleSpikeLauncher(RopeSetFolder + "/Vine_CircleSpike_Set.prefab");
        DeleteLegacySetAssets();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ValidateRopeSetPrefabs();
        BeginConsoleErrorGate();
    }

    [MenuItem("Tools/Project/Objects/Configure Warning Box Rope Sets")]
    public static void ConfigureWarningBoxAssets()
    {
        ConfigureFallingBoxPrefab();
        ConfigureBoxSet(RopeSetFolder + "/Vine_Box_Set.prefab");
        ConfigureBoxSet(RopeSetFolder + "/Wire_Box_Set.prefab");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ValidateBoxSet(RopeSetFolder + "/Vine_Box_Set.prefab");
        ValidateBoxSet(RopeSetFolder + "/Wire_Box_Set.prefab");
        Debug.Log("[RopeSetPrefabSetup] WarningBox Sprite and anchor-based rope connection validation passed for Vine/Wire Box sets.");
    }

    private static void ConfigureFallingBoxPrefab()
    {
        Sprite warningSprite = AssetDatabase.LoadAssetAtPath<Sprite>(WarningBoxSpritePath);
        if (warningSprite == null)
        {
            throw new InvalidOperationException("Required WarningBox Sprite was not found: " + WarningBoxSpritePath);
        }

        GameObject root = PrefabUtility.LoadPrefabContents(BoxPath);
        try
        {
            Transform legacyVisual = root.transform.Find("Visual");
            if (legacyVisual != null)
            {
                Renderer legacyRenderer = legacyVisual.GetComponent<Renderer>();
                if (legacyRenderer != null) legacyRenderer.enabled = false;
            }

            Transform visual = root.transform.Find("BoxVisual");
            if (visual == null)
            {
                GameObject visualObject = new GameObject("BoxVisual");
                visualObject.transform.SetParent(root.transform, false);
                visual = visualObject.transform;
            }
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            if (visual.localScale == Vector3.zero) visual.localScale = Vector3.one;
            SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = visual.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = warningSprite;
            renderer.sortingLayerID = 0;
            renderer.sortingOrder = 1;
            renderer.enabled = true;

            Transform topAnchor = root.transform.Find("BoxTopAnchor");
            if (topAnchor == null)
            {
                GameObject anchorObject = new GameObject("BoxTopAnchor");
                anchorObject.transform.SetParent(root.transform, false);
                topAnchor = anchorObject.transform;
                topAnchor.localPosition = new Vector3(0f, 0.5f, 0f);
            }

            BoxCollider collider = root.GetComponent<BoxCollider>();
            if (collider != null)
            {
                collider.center = Vector3.zero;
                collider.size = new Vector3(1f, 1f, 0.3f);
            }
            PrefabUtility.SaveAsPrefabAsset(root, BoxPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureBoxSet(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Transform payload = root.transform.Find("ConnectedObject");
            Transform rope = root.transform.Find("Rope");
            Transform ceiling = root.transform.Find("CeilingAnchor");
            Transform attachPoint = root.transform.Find("ConnectedObjectAttachPoint");
            Transform topAnchor = payload != null ? payload.Find("BoxTopAnchor") : null;
            SpriteRenderer boxRenderer = payload != null ? payload.Find("BoxVisual")?.GetComponent<SpriteRenderer>() : null;
            RopeLengthController3D controller = rope != null ? rope.GetComponent<RopeLengthController3D>() : null;
            BoxCollider ropeCollider = rope != null ? rope.Find("RopeHitCollider")?.GetComponent<BoxCollider>() : null;
            Transform debugVisual = rope != null ? rope.Find("Rope_Debug_Visual") : null;
            if (payload == null || rope == null || ceiling == null || attachPoint == null || topAnchor == null ||
                boxRenderer == null || controller == null || ropeCollider == null || debugVisual == null)
            {
                throw new InvalidOperationException(path + " is missing its WarningBox rope connection structure.");
            }

            attachPoint.position = topAnchor.position;
            controller.ConfigureReferences(ceiling, topAnchor, ropeCollider, debugVisual);
            ConfigureCraneRopeSprite(root, path);
            controller.ConfigureBoxConnection(topAnchor, boxRenderer, 0.03f, Vector2.zero, 0f, -1);
            controller.ApplyRopeLength();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ValidateBoxSet(string path)
    {
        GameObject prefab = RequirePrefab(path);
        Transform payload = prefab.transform.Find("ConnectedObject");
        Transform topAnchor = payload != null ? payload.Find("BoxTopAnchor") : null;
        SpriteRenderer boxRenderer = payload != null ? payload.Find("BoxVisual")?.GetComponent<SpriteRenderer>() : null;
        RopeLengthController3D controller = prefab.transform.Find("Rope")?.GetComponent<RopeLengthController3D>();
        SpriteRenderer ropeRenderer = prefab.transform.Find("Rope/RopeVisualRoot/RopeSegment_000")?.GetComponent<SpriteRenderer>();
        GameObject cranePrefab = RequirePrefab("Assets/_Project/Prefabs/Objects/Crane/VerticalCrane_Set.prefab");
        SpriteRenderer craneRenderer = cranePrefab.transform.Find("RopeVisualRoot/RopeSegment_000")?.GetComponent<SpriteRenderer>();
        if (topAnchor == null || boxRenderer == null || boxRenderer.sprite == null || controller == null ||
            ropeRenderer == null || craneRenderer == null || ropeRenderer.sprite != craneRenderer.sprite ||
            ropeRenderer.sortingOrder >= boxRenderer.sortingOrder || controller.BoxAnchorError > 0.01f)
        {
            throw new InvalidOperationException(path + " failed WarningBox Sprite/Anchor/Rope validation.");
        }
    }

    private static Material CreateOrUpdateOpaqueMaterial(string path, Color color)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("No opaque Lit shader is available for Rope debug materials.");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.color = new Color(color.r, color.g, color.b, 1f);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", material.color);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 0f);
        if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
        material.renderQueue = -1;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void PrepareStandaloneRopePrefab(string prefabPath, Material material)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            SpriteRenderer[] sprites = root.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < sprites.Length; i++)
            {
                sprites[i].sprite = null;
                sprites[i].enabled = false;
            }

            Transform debugTransform = root.transform.Find("Rope_Debug_Visual");
            GameObject debugVisual;
            if (debugTransform == null)
            {
                debugVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                debugVisual.name = "Rope_Debug_Visual";
                debugVisual.transform.SetParent(root.transform, false);
                UnityEngine.Object.DestroyImmediate(debugVisual.GetComponent<Collider>());
            }
            else
            {
                debugVisual = debugTransform.gameObject;
            }

            MeshRenderer renderer = debugVisual.GetComponent<MeshRenderer>();
            if (renderer == null) renderer = debugVisual.AddComponent<MeshRenderer>();
            renderer.enabled = true;
            renderer.sharedMaterial = material;
            BoxCollider sourceCollider = root.GetComponent<BoxCollider>();
            if (sourceCollider != null)
            {
                debugVisual.transform.localPosition = sourceCollider.center;
                debugVisual.transform.localRotation = Quaternion.identity;
                debugVisual.transform.localScale = sourceCollider.size;
            }

            WireObject wire = root.GetComponent<WireObject>();
            VineObject vine = root.GetComponent<VineObject>();
            if (wire != null) SetObjectArray(wire, "renderers", new UnityEngine.Object[] { renderer });
            if (vine != null) SetObjectArray(vine, "renderers", new UnityEngine.Object[] { renderer });
            BreakableObject3D breakable = root.GetComponent<BreakableObject3D>();
            if (breakable != null) SetObjectArray(breakable, "renderersToDisable", new UnityEngine.Object[] { renderer });
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [MenuItem("Tools/Project/Objects/Validate Rope Set Prefabs")]
    public static void ValidateRopeSetPrefabs()
    {
        ValidateSet("Wire_Box_Set", typeof(WireObject), typeof(FallingBoxObject));
        ValidateSet("Wire_CircleSpike_Set", typeof(WireObject), typeof(CircleSpikeObject));
        ValidateSet("Vine_Box_Set", typeof(VineObject), typeof(FallingBoxObject));
        ValidateSet("Vine_CircleSpike_Set", typeof(VineObject), typeof(CircleSpikeObject));
        Debug.Log("[RopeSetPrefabSetup] Validation passed: standalone Wire/Vine and four 3D drop sets are ready.");
    }

    [MenuItem("Tools/Project/Objects/Configure CircleSpike Launchers")]
    public static void ConfigureCircleSpikeLaunchers()
    {
        ConfigureCircleSpikeLauncher(RopeSetFolder + "/Wire_CircleSpike_Set.prefab");
        ConfigureCircleSpikeLauncher(RopeSetFolder + "/Vine_CircleSpike_Set.prefab");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ValidateCircleSpikeLauncher(RopeSetFolder + "/Wire_CircleSpike_Set.prefab");
        ValidateCircleSpikeLauncher(RopeSetFolder + "/Vine_CircleSpike_Set.prefab");
        Debug.Log("[RopeSetPrefabSetup] CircleSpike launcher validation passed for Vine and Wire sets.");
    }

    private static void ConfigureCircleSpikeLauncher(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Transform payload = root.transform.Find("ConnectedObject");
            if (payload == null)
            {
                throw new InvalidOperationException(path + " is missing ConnectedObject.");
            }

            CircleSpikeProjectile3D projectile = payload.GetComponent<CircleSpikeProjectile3D>();
            if (projectile == null)
            {
                projectile = payload.gameObject.AddComponent<CircleSpikeProjectile3D>();
            }

            SetObjectReference(projectile, "body", payload.GetComponent<Rigidbody>());
            SetObjectReference(projectile, "projectileCollider", payload.GetComponent<Collider>());
            SetObjectReference(projectile, "circleSpike", payload.GetComponent<CircleSpikeObject>());
            SetObjectReference(projectile, "gravityObject", payload.GetComponent<GravityObject3D>());
            Renderer spikeRenderer = payload.GetComponentInChildren<Renderer>(true);
            SetObjectReference(projectile, "projectileRenderer", spikeRenderer);
            SetObjectReference(projectile, "circleSpikeVisual", spikeRenderer != null ? spikeRenderer.transform : null);
            SetFloat(projectile, "gravityScale", 1f);
            SetFloat(projectile, "dropImpulse", 0f);
            SetInt(projectile, "groundLayerMask", (1 << 8) | (1 << 9) | (1 << 10) | (1 << 11) | (1 << 12));
            SetFloat(projectile, "groundNormalMinimum", 0.65f);
            SetFloat(projectile, "groundCheckDistance", 0.12f);
            SetFloat(projectile, "ownerCollisionIgnoreTime", 0.1f);
            SetFloat(projectile, "initialMoveSpeed", 5f);
            SetFloat(projectile, "moveAcceleration", 0f);
            SetFloat(projectile, "maxMoveSpeed", 5f);
            SetFloat(projectile, "rollingLifetime", 8f);
            SetBool(projectile, "stopOnWall", true);
            SetBool(projectile, "disableOnStop", true);
            SetBool(projectile, "disableOnPlayerHit", true);
            SetBool(projectile, "trackPlayerWhileRolling", false);
            SetBool(projectile, "allowDirectionReversal", false);
            SetBool(projectile, "reacquirePlayerOnLanding", true);
            SetInt(projectile, "rotationMode", (int)CircleSpikeVisualRotationMode.DistanceBased);
            SetFloat(projectile, "visualRotationSpeed", 180f);
            SetFloat(projectile, "visualRotationMultiplier", 0.35f);
            SetFloat(projectile, "visualRadius", 0.65f);
            SetFloat(projectile, "rotationDirectionMultiplier", -1f);
            SetFloat(projectile, "maxVisualRotationSpeed", 360f);
            SetBool(projectile, "rotateOnlyWhileMoving", true);
            SetFloat(projectile, "minimumMovementThreshold", 0.001f);
            SetBool(projectile, "resetVisualRotationOnReuse", true);

            ConfigureCraneRopeSprite(root, path);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureCraneRopeSprite(GameObject root, string path)
    {
        GameObject cranePrefab = RequirePrefab("Assets/_Project/Prefabs/Objects/Crane/VerticalCrane_Set.prefab");
        Transform craneSegment = cranePrefab.transform.Find("RopeVisualRoot/RopeSegment_000");
        SpriteRenderer craneRenderer = craneSegment != null ? craneSegment.GetComponent<SpriteRenderer>() : null;
        Transform rope = root.transform.Find("Rope");
        RopeLengthController3D lengthController = rope != null ? rope.GetComponent<RopeLengthController3D>() : null;
        if (craneRenderer == null || craneRenderer.sprite == null || rope == null || lengthController == null)
        {
            throw new InvalidOperationException(path + " could not resolve the crane rope Sprite or Rope root.");
        }

        Transform visualRoot = rope.Find("RopeVisualRoot");
        if (visualRoot == null)
        {
            GameObject visualRootObject = new GameObject("RopeVisualRoot");
            visualRootObject.transform.SetParent(rope, false);
            visualRoot = visualRootObject.transform;
        }

        Transform segment = visualRoot.Find("RopeSegment_000");
        if (segment == null)
        {
            GameObject segmentObject = new GameObject("RopeSegment_000");
            segmentObject.transform.SetParent(visualRoot, false);
            segment = segmentObject.transform;
        }

        SpriteRenderer renderer = segment.GetComponent<SpriteRenderer>();
        if (renderer == null) renderer = segment.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = craneRenderer.sprite;
        renderer.sharedMaterial = craneRenderer.sharedMaterial;
        renderer.color = craneRenderer.color;
        renderer.sortingLayerID = craneRenderer.sortingLayerID;
        renderer.sortingOrder = craneRenderer.sortingOrder;
        renderer.flipX = craneRenderer.flipX;
        renderer.flipY = craneRenderer.flipY;
        renderer.drawMode = SpriteDrawMode.Tiled;
        renderer.enabled = true;

        const float segmentOverlap = 0.02f;
        lengthController.ConfigureSpriteVisual(renderer, segmentOverlap);
        lengthController.ApplyRopeLength();

        Transform debugVisual = rope.Find("Rope_Debug_Visual");
        if (debugVisual != null)
        {
            Renderer debugRenderer = debugVisual.GetComponent<Renderer>();
            if (debugRenderer != null) debugRenderer.enabled = false;
        }

        WireObject wire = rope.GetComponent<WireObject>();
        VineObject vine = rope.GetComponent<VineObject>();
        if (wire != null) SetObjectArray(wire, "renderers", new UnityEngine.Object[] { renderer });
        if (vine != null) SetObjectArray(vine, "renderers", new UnityEngine.Object[] { renderer });
        BreakableObject3D breakable = rope.GetComponent<BreakableObject3D>();
        if (breakable != null)
        {
            SetObjectArray(breakable, "renderersToDisable", new UnityEngine.Object[] { renderer });
            SetBool(breakable, "disableRenderersOnBreak", true);
        }
    }

    private static void ValidateCircleSpikeLauncher(string path)
    {
        GameObject prefab = RequirePrefab(path);
        Transform payload = prefab.transform.Find("ConnectedObject");
        Transform linkRoot = prefab.transform.Find("Link");
        CircleSpikeProjectile3D projectile = payload != null ? payload.GetComponent<CircleSpikeProjectile3D>() : null;
        ConnectedObjectLink link = linkRoot != null ? linkRoot.GetComponent<ConnectedObjectLink>() : null;
        SerializedObject serialized = projectile != null ? new SerializedObject(projectile) : null;
        if (payload == null || projectile == null || link == null ||
            serialized.FindProperty("body").objectReferenceValue == null ||
            serialized.FindProperty("projectileCollider").objectReferenceValue == null ||
            serialized.FindProperty("circleSpike").objectReferenceValue == null ||
            serialized.FindProperty("projectileRenderer").objectReferenceValue == null)
        {
            throw new InvalidOperationException(path + " has a missing CircleSpike launcher reference.");
        }

        Rigidbody body = payload.GetComponent<Rigidbody>();
        Collider physicalCollider = payload.GetComponent<Collider>();
        if (body == null || physicalCollider == null || physicalCollider.isTrigger ||
            (body.constraints & RigidbodyConstraints.FreezePositionX) != 0)
        {
            throw new InvalidOperationException(path + " CircleSpike requires a non-trigger physical Collider and an unfrozen X position.");
        }


        Transform rope = prefab.transform.Find("Rope");
        SpriteRenderer ropeRenderer = rope != null
            ? rope.Find("RopeVisualRoot/RopeSegment_000")?.GetComponent<SpriteRenderer>()
            : null;
        GameObject cranePrefab = RequirePrefab("Assets/_Project/Prefabs/Objects/Crane/VerticalCrane_Set.prefab");
        SpriteRenderer craneRenderer = cranePrefab.transform.Find("RopeVisualRoot/RopeSegment_000")?.GetComponent<SpriteRenderer>();
        if (ropeRenderer == null || craneRenderer == null || ropeRenderer.sprite != craneRenderer.sprite ||
            ropeRenderer.drawMode != SpriteDrawMode.Tiled || !ropeRenderer.enabled)
        {
            throw new InvalidOperationException(path + " does not reuse the crane rope Sprite as a tiled rope visual.");
        }
    }

    private static void CreateCircleSpikePrefab()
    {
        GameObject root = new GameObject("CircleSpike");
        try
        {
            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ |
                             RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;

            CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
            collider.direction = 2;
            collider.radius = 0.65f;
            collider.height = 0.3f;

            GravityObject3D gravity = root.AddComponent<GravityObject3D>();
            GravityObjectDamageDealer damage = root.AddComponent<GravityObjectDamageDealer>();
            CircleSpikeObject spike = root.AddComponent<CircleSpikeObject>();
            CircleSpikeProjectile3D projectile = root.AddComponent<CircleSpikeProjectile3D>();

            SetObjectReference(gravity, "rb", rb);
            SetBool(gravity, "startAttached", true);
            SetBool(gravity, "disableGravityOnStart", true);
            SetBool(gravity, "lockXWhileFalling", true);
            SetBool(gravity, "lockZPosition", true);
            SetBool(gravity, "debugMode", false);
            SetBool(damage, "damageOnlyWhileFalling", false);
            SetBool(damage, "debugMode", false);
            SetObjectReference(spike, "gravityObject", gravity);
            SetObjectReference(spike, "damageDealer", damage);
            damage.enabled = false;

            Sprite circleSpikeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CircleSpikeSpritePath);
            if (circleSpikeSprite == null)
            {
                throw new InvalidOperationException("Required CircleSpike Sprite was not found: " + CircleSpikeSpritePath);
            }
            GameObject visual = new GameObject("CircleSpikeVisual");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one * 0.7f;
            SpriteRenderer spriteRenderer = visual.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = circleSpikeSprite;

            SetObjectReference(projectile, "body", rb);
            SetObjectReference(projectile, "projectileCollider", collider);
            SetObjectReference(projectile, "circleSpike", spike);
            SetObjectReference(projectile, "gravityObject", gravity);
            SetObjectReference(projectile, "projectileRenderer", spriteRenderer);
            SetObjectReference(projectile, "circleSpikeVisual", visual.transform);
            SetFloat(projectile, "dropImpulse", 0f);
            SetFloat(projectile, "initialMoveSpeed", 5f);
            SetFloat(projectile, "moveAcceleration", 0f);
            SetFloat(projectile, "maxMoveSpeed", 5f);
            SetInt(projectile, "rotationMode", (int)CircleSpikeVisualRotationMode.DistanceBased);
            SetFloat(projectile, "visualRotationSpeed", 180f);
            SetFloat(projectile, "visualRotationMultiplier", 0.35f);
            SetFloat(projectile, "visualRadius", 0.65f);
            SetFloat(projectile, "rotationDirectionMultiplier", -1f);
            SetFloat(projectile, "maxVisualRotationSpeed", 360f);
            SetBool(projectile, "rotateOnlyWhileMoving", true);
            SetFloat(projectile, "minimumMovementThreshold", 0.001f);
            SetBool(projectile, "resetVisualRotationOnReuse", true);
            rb.constraints = RigidbodyConstraints.FreezePositionZ |
                             RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;

            PrefabUtility.SaveAsPrefabAsset(root, CircleSpikePath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void CreateSet(string setName, string ropePath, string payloadPath, Type payloadType)
    {
        GameObject ropeAsset = RequirePrefab(ropePath);
        GameObject payloadAsset = RequirePrefab(payloadPath);
        GameObject root = new GameObject(setName);
        try
        {
            GameObject ceilingAnchor = new GameObject("CeilingAnchor");
            GameObject attachPoint = new GameObject("ConnectedObjectAttachPoint");
            GameObject rope = (GameObject)PrefabUtility.InstantiatePrefab(ropeAsset);
            GameObject payload = (GameObject)PrefabUtility.InstantiatePrefab(payloadAsset);
            GameObject linkObject = new GameObject("Link");
            ConnectedObjectLink link = linkObject.AddComponent<ConnectedObjectLink>();
            ceilingAnchor.transform.SetParent(root.transform, false);
            attachPoint.transform.SetParent(root.transform, false);
            rope.name = "Rope";
            payload.name = "ConnectedObject";
            rope.transform.SetParent(root.transform, false);
            payload.transform.SetParent(root.transform, false);
            linkObject.transform.SetParent(root.transform, false);
            ceilingAnchor.transform.localPosition = new Vector3(0f, 4f, 0f);
            attachPoint.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            rope.transform.localPosition = Vector3.zero;
            payload.transform.localPosition = Vector3.zero;

            Transform debugVisual = rope.transform.Find("Rope_Debug_Visual");
            if (debugVisual == null)
            {
                throw new InvalidOperationException(setName + " Rope_Debug_Visual is missing.");
            }

            Collider sourceCollider = rope.GetComponent<Collider>();
            if (sourceCollider != null)
            {
                sourceCollider.enabled = false;
            }
            GameObject hitColliderObject = new GameObject("RopeHitCollider");
            hitColliderObject.transform.SetParent(rope.transform, false);
            BoxCollider ropeHitCollider = hitColliderObject.AddComponent<BoxCollider>();
            RopeLengthController3D lengthController = rope.AddComponent<RopeLengthController3D>();
            lengthController.ConfigureReferences(ceilingAnchor.transform, attachPoint.transform, ropeHitCollider, debugVisual);
            SetBool(lengthController, "updateOnValidate", false);
            SetBool(lengthController, "updateOnStart", true);
            SetBool(lengthController, "preserveManualOffsets", false);
            SetBool(lengthController, "debugMode", false);
            lengthController.ApplyRopeLength();
            SetBool(lengthController, "preserveManualOffsets", true);

            GravityDropSensor sensor = payload.GetComponent<GravityDropSensor>();
            if (sensor != null)
            {
                sensor.enabled = false;
            }

            MonoBehaviour payloadBehaviour = payload.GetComponent(payloadType) as MonoBehaviour;
            if (payloadBehaviour == null)
            {
                throw new InvalidOperationException(setName + " is missing its connection components.");
            }

            SetObjectReference(link, "connectedObject", payload);
            SetObjectReference(link, "connectedObjectAttachPoint", attachPoint.transform);
            SetObjectReference(link, "connectedBehaviour", payloadBehaviour);
            SetBool(link, "activateOnCut", true);
            SetBool(link, "releasePhysicsOnCut", true);
            SetBool(link, "detachFromAttachPointOnCut", true);
            SetBool(link, "preserveConnectedObjectScale", true);
            SetBool(link, "debugMode", false);
            WireObject wireObject = rope.GetComponent<WireObject>();
            VineObject vineObject = rope.GetComponent<VineObject>();
            if (wireObject != null)
            {
                SetObjectReference(wireObject, "connectedObjectLink", link);
                SetObjectArray(wireObject, "colliders", new UnityEngine.Object[] { ropeHitCollider });
                SetDebugMode(wireObject, false);
            }
            if (vineObject != null)
            {
                SetObjectReference(vineObject, "connectedObjectLink", link);
                SetObjectArray(vineObject, "colliders", new UnityEngine.Object[] { ropeHitCollider });
                SetDebugMode(vineObject, false);
            }
            BreakableObject3D breakable = rope.GetComponent<BreakableObject3D>();
            if (breakable != null)
            {
                SetObjectArray(breakable, "collidersToDisable", new UnityEngine.Object[] { ropeHitCollider });
            }
            if (payloadType == typeof(CircleSpikeObject))
            {
                ConfigureCraneRopeSprite(root, RopeSetFolder + "/" + setName + ".prefab");
            }
            else if (payloadType == typeof(FallingBoxObject))
            {
                Transform topAnchor = payload.transform.Find("BoxTopAnchor");
                SpriteRenderer boxRenderer = payload.transform.Find("BoxVisual")?.GetComponent<SpriteRenderer>();
                if (topAnchor == null || boxRenderer == null)
                {
                    throw new InvalidOperationException(setName + " is missing BoxTopAnchor or BoxVisual.");
                }
                attachPoint.transform.position = topAnchor.position;
                lengthController.ConfigureReferences(ceilingAnchor.transform, topAnchor, ropeHitCollider, debugVisual);
                ConfigureCraneRopeSprite(root, RopeSetFolder + "/" + setName + ".prefab");
                lengthController.ConfigureBoxConnection(topAnchor, boxRenderer, 0.03f, Vector2.zero, 0f, -1);
                lengthController.ApplyRopeLength();
            }
            PrefabUtility.SaveAsPrefabAsset(root, RopeSetFolder + "/" + setName + ".prefab");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void ValidateSet(string setName, Type ropeType, Type payloadType)
    {
        string path = RopeSetFolder + "/" + setName + ".prefab";
        GameObject prefab = RequirePrefab(path);
        Transform ropeRoot = prefab.transform.Find("Rope");
        Transform payloadRoot = prefab.transform.Find("ConnectedObject");
        Transform linkRoot = prefab.transform.Find("Link");
        Transform ceilingAnchor = prefab.transform.Find("CeilingAnchor");
        Transform attachPoint = prefab.transform.Find("ConnectedObjectAttachPoint");
        Component rope = ropeRoot != null ? ropeRoot.GetComponent(ropeType) : null;
        MonoBehaviour payload = payloadRoot != null ? payloadRoot.GetComponent(payloadType) as MonoBehaviour : null;
        ConnectedObjectLink link = linkRoot != null ? linkRoot.GetComponent<ConnectedObjectLink>() : null;
        Rigidbody body = payloadRoot != null ? payloadRoot.GetComponentInChildren<Rigidbody>(true) : null;
        Collider collider = payloadRoot != null ? payloadRoot.GetComponentInChildren<Collider>(true) : null;
        RopeLengthController3D lengthController = ropeRoot != null ? ropeRoot.GetComponent<RopeLengthController3D>() : null;
        Transform debugVisual = ropeRoot != null ? ropeRoot.Find("Rope_Debug_Visual") : null;
        BoxCollider ropeHitCollider = ropeRoot != null ? ropeRoot.Find("RopeHitCollider")?.GetComponent<BoxCollider>() : null;
        if (rope == null || payload == null || link == null || body == null || collider == null ||
            ceilingAnchor == null || attachPoint == null || lengthController == null || debugVisual == null || ropeHitCollider == null)
        {
            throw new InvalidOperationException(path + " does not contain the required Anchor/Rope/AttachPoint/ConnectedObject/Link structure.");
        }

        SerializedProperty connected = new SerializedObject(link).FindProperty("connectedBehaviour");
        if (connected == null || connected.objectReferenceValue != payload)
        {
            throw new InvalidOperationException(path + " does not connect the rope to its payload.");
        }

        SerializedProperty connectedObject = new SerializedObject(link).FindProperty("connectedObject");
        if (connectedObject == null || connectedObject.objectReferenceValue != payloadRoot.gameObject || !link.ValidateLinkSetup())
        {
            throw new InvalidOperationException(path + " does not have a valid Link child setup.");
        }

        if (!lengthController.ValidateRopeLengthSetup())
        {
            throw new InvalidOperationException(path + " does not have a valid RopeLengthController3D setup.");
        }

        ValidateOpaqueDebugVisual(ropeRoot, debugVisual, ropeHitCollider, path);

        ValidateLengthAndScaleIndependence(prefab, payloadType, path);

        if (!(payload is ITriggerableObject) && !(payload is FallingBoxObject))
        {
            throw new InvalidOperationException(path + " payload does not implement ITriggerableObject.");
        }

        ValidateReleaseBehaviour(prefab, payloadType, path);
    }

    private static void ValidateOpaqueDebugVisual(Transform ropeRoot, Transform debugVisual, BoxCollider hitCollider, string path)
    {
        MeshRenderer renderer = debugVisual.GetComponent<MeshRenderer>();
        Collider duplicateCollider = debugVisual.GetComponent<Collider>();
        SpriteRenderer[] sprites = ropeRoot.GetComponentsInChildren<SpriteRenderer>(true);
        SpriteRenderer gameplayRenderer = ropeRoot.Find("RopeVisualRoot/RopeSegment_000")?.GetComponent<SpriteRenderer>();
        bool usesGameplaySprite = gameplayRenderer != null && gameplayRenderer.enabled && gameplayRenderer.sprite != null;
        if (renderer == null || renderer.sharedMaterial == null || duplicateCollider != null)
        {
            throw new InvalidOperationException(path + " Rope_Debug_Visual must be a collider-free MeshRenderer with a material.");
        }
        if (renderer.sharedMaterial.color.a < 0.999f || renderer.sharedMaterial.renderQueue >= 3000)
        {
            throw new InvalidOperationException(path + " Rope debug material must be opaque with alpha 1.");
        }
        if (usesGameplaySprite)
        {
            if (renderer.enabled)
            {
                throw new InvalidOperationException(path + " Rope_Debug_Visual must be disabled when the gameplay rope SpriteRenderer is active.");
            }
        }
        else
        {
            if (!renderer.enabled)
            {
                throw new InvalidOperationException(path + " Rope_Debug_Visual must be visible when no gameplay rope SpriteRenderer is configured.");
            }
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i].enabled || sprites[i].sprite != null)
                {
                    throw new InvalidOperationException(path + " still uses an enabled or assigned legacy Wire/Vine SpriteRenderer.");
                }
            }
        }
        if (hitCollider == null || !hitCollider.enabled)
        {
            throw new InvalidOperationException(path + " RopeHitCollider must remain the enabled 3D hit volume.");
        }
    }

    private static void ValidateLengthAndScaleIndependence(GameObject prefab, Type payloadType, string path)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        try
        {
            Transform anchor = instance.transform.Find("CeilingAnchor");
            Transform payloadRoot = instance.transform.Find("ConnectedObject");
            RopeLengthController3D controller = instance.transform.Find("Rope")?.GetComponent<RopeLengthController3D>();
            BoxCollider hitCollider = instance.transform.Find("Rope/RopeHitCollider")?.GetComponent<BoxCollider>();
            Transform debugVisual = instance.transform.Find("Rope/Rope_Debug_Visual");
            if (anchor == null || payloadRoot == null || controller == null || hitCollider == null || debugVisual == null)
            {
                throw new InvalidOperationException(path + " could not create a rope length validation instance.");
            }

            Vector3 authoredScale = new Vector3(1.6f, 0.75f, 1.25f);
            payloadRoot.localScale = authoredScale;
            float originalLength = hitCollider.size.y;
            anchor.localPosition += Vector3.up * 3f;
            controller.ApplyRopeLength();
            Vector3 visualSize = debugVisual.localScale;
            if (hitCollider.size.y <= originalLength || payloadRoot.localScale != authoredScale ||
                Vector3.Distance(debugVisual.position, hitCollider.transform.position) > 0.001f ||
                Vector3.Distance(visualSize, hitCollider.size) > 0.001f)
            {
                throw new InvalidOperationException(path + " did not keep Rope_Debug_Visual/Collider size aligned independently from ConnectedObject scale.");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    private static void ValidateReleaseBehaviour(GameObject prefab, Type payloadType, string path)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        try
        {
            Transform payloadRoot = instance.transform.Find("ConnectedObject");
            Transform linkRoot = instance.transform.Find("Link");
            ConnectedObjectLink instanceLink = linkRoot != null ? linkRoot.GetComponent<ConnectedObjectLink>() : null;
            GravityObject3D gravity = payloadRoot != null ? payloadRoot.GetComponentInChildren<GravityObject3D>(true) : null;
            MonoBehaviour payload = payloadRoot != null ? payloadRoot.GetComponent(payloadType) as MonoBehaviour : null;
            Rigidbody body = payloadRoot != null ? payloadRoot.GetComponentInChildren<Rigidbody>(true) : null;
            if (instanceLink == null || gravity == null || payload == null || body == null)
            {
                throw new InvalidOperationException(path + " could not create a runtime validation instance.");
            }

            instanceLink.ActivateConnectedObject();
            bool payloadDropped = payload is FallingBoxObject box ? box.IsFalling :
                                  payload is CircleSpikeObject spike && spike.IsFalling;
            if (!gravity.IsDropped || !payloadDropped || body.isKinematic || !body.useGravity)
            {
                throw new InvalidOperationException(path + " did not release its payload into 3D Rigidbody gravity.");
            }

            instanceLink.ResetConnectedObject();
            if (gravity.IsDropped)
            {
                throw new InvalidOperationException(path + " did not reset its attached state.");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    private static void DeleteLegacySetAssets()
    {
        string[] names = { "Wire_Box_Set", "Wire_CircleSpike_Set", "Vine_Box_Set", "Vine_CircleSpike_Set" };
        for (int i = 0; i < names.Length; i++)
        {
            string legacyPath = RopeFolder + "/" + names[i] + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(legacyPath) != null)
            {
                AssetDatabase.DeleteAsset(legacyPath);
            }
        }
    }

    private static GameObject RequirePrefab(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            throw new InvalidOperationException("Required prefab was not found: " + path);
        }

        return prefab;
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }

    private static void SetDebugMode(MonoBehaviour target, bool value)
    {
        if (target != null)
        {
            SetBool(target, "debugMode", value);
        }
    }

    private static void SetBool(UnityEngine.Object target, string propertyName, bool value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
        {
            property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void SetFloat(UnityEngine.Object target, string propertyName, float value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null) throw new InvalidOperationException(target.GetType().Name + "." + propertyName + " was not found.");
        property.floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetInt(UnityEngine.Object target, string propertyName, int value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null) throw new InvalidOperationException(target.GetType().Name + "." + propertyName + " was not found.");
        property.intValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException(target.GetType().Name + "." + propertyName + " was not found.");
        }
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetObjectArray(UnityEngine.Object target, string propertyName, UnityEngine.Object[] values)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null || !property.isArray)
        {
            throw new InvalidOperationException(target.GetType().Name + "." + propertyName + " array was not found.");
        }
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BeginConsoleErrorGate()
    {
        Type logEntries = typeof(Editor).Assembly.GetType("UnityEditor.LogEntries");
        MethodInfo clear = logEntries != null ? logEntries.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) : null;
        MethodInfo getCounts = logEntries != null ? logEntries.GetMethod("GetCountsByType", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) : null;
        if (clear == null || getCounts == null)
        {
            throw new InvalidOperationException("Unity Console count API was not found.");
        }

        clear.Invoke(null, null);
        double checkAt = EditorApplication.timeSinceStartup + 2d;
        EditorApplication.CallbackFunction check = null;
        check = () =>
        {
            if (EditorApplication.timeSinceStartup < checkAt)
            {
                return;
            }

            EditorApplication.update -= check;
            object[] counts = { 0, 0, 0 };
            getCounts.Invoke(null, counts);
            int errors = (int)counts[0];
            if (errors != 0)
            {
                Debug.LogError("[RopeSetPrefabSetup] Console Error gate failed. Error count: " + errors);
                return;
            }

            Debug.Log("[RopeSetPrefabSetup] Console Error 0 confirmed after compilation and prefab validation.");
        };
        EditorApplication.update += check;
    }
}
#endif
