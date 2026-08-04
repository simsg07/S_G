using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

[InitializeOnLoad]
public static class Physics2DPrototypeBuilder
{
    private const string ScenePath = "Assets/_Project/Scenes/Test/Physics2DPrototypeScene.unity";
    private const string PrefabPath = "Assets/_Project/Prefabs/Test/Player2D_Test.prefab";
    private const string GroundTilePath = "Assets/_Project/Tiles/Physics2DPrototype/TILE_Prototype_Ground.asset";
    private const string WallTilePath = "Assets/_Project/Tiles/Physics2DPrototype/TILE_Prototype_Wall.asset";
    private const string PalettePath = "Assets/_Project/TilePalettes/Physics2DPrototypePalette.prefab";
    private const string AutoBuildKey = "Physics2DPrototypeBuilder.AutoBuild.v1";
    private const string PlayValidationRunningKey = "Physics2DPrototypeBuilder.PlayValidationRunning";
    private const string PlayValidationPassedKey = "Physics2DPrototypeBuilder.PlayValidationPassed";
    private static double validationStartTime;

    static Physics2DPrototypeBuilder()
    {
        EditorApplication.delayCall += AutoBuildOnce;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.update -= MonitorPlayValidation;
        EditorApplication.update += MonitorPlayValidation;
        Application.logMessageReceived -= OnValidationLog;
        Application.logMessageReceived += OnValidationLog;
    }

    public static void RunPlayModeValidation()
    {
        if (!File.Exists(ScenePath)) Build();
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        // Keep unrelated one-shot project setup utilities from refreshing assets while
        // the isolated automated Play Mode validation is running.
        SessionState.SetBool("TilePaletteSetupUtility.VisualOnly.AutoRunComplete.v1", true);
        SessionState.SetBool(PlayValidationRunningKey, true);
        SessionState.SetBool(PlayValidationPassedKey, false);
        validationStartTime = EditorApplication.timeSinceStartup;
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    public static void BuildStandaloneValidationPlayer()
    {
        if (!File.Exists(ScenePath)) Build();
        const string outputDirectory = ".physics2d_validation_build";
        Directory.CreateDirectory(outputDirectory);
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = outputDirectory + "/Physics2DPrototype.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        };
        var report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            throw new System.InvalidOperationException(
                $"Physics2D prototype player build failed: {report.summary.result}");
        }
        Debug.Log($"[Physics2D Prototype] Standalone validation player built: {options.locationPathName}");
    }

    [MenuItem("_Project/Test/Physics2D Prototype/Rebuild Test Assets")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[Physics2D Prototype] Exit Play Mode before rebuilding test assets.");
            return;
        }

        string previousScenePath = SceneManager.GetActiveScene().path;
        bool previousSceneDirty = SceneManager.GetActiveScene().isDirty;
        if (previousSceneDirty && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        EnsureFolders();
        Tile groundTile = CreatePrototypeTile(
            GroundTilePath,
            "Assets/_Project/Art/Map/TempTiles/TEMP_Tile_Ground_Black.png");
        Tile wallTile = CreatePrototypeTile(
            WallTilePath,
            "Assets/_Project/Art/Map/TempTiles/TEMP_Tile_Wall_DarkGray.png");
        CreatePalette(groundTile, wallTile);

        GameObject playerPrefab = CreatePlayerPrefab();
        CreateTestScene(playerPrefab, groundTile, wallTile);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Physics2D Prototype] Built test-only scene: {ScenePath}");

        if (!string.IsNullOrEmpty(previousScenePath) && File.Exists(previousScenePath))
        {
            EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
        }
    }

    [MenuItem("_Project/Test/Physics2D Prototype/Open Test Scene")]
    public static void OpenTestScene()
    {
        if (!File.Exists(ScenePath))
        {
            Build();
        }

        if (File.Exists(ScenePath))
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
    }

    [MenuItem("_Project/Test/Physics2D Prototype/Validate Test Assets")]
    public static void ValidateAssets()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        bool prefabIs2DOnly = prefab != null &&
            prefab.GetComponent<Rigidbody2D>() != null &&
            prefab.GetComponent<Collider2D>() != null &&
            prefab.GetComponent<Rigidbody>() == null &&
            prefab.GetComponent<Collider>() == null;
        bool sceneExists = File.Exists(ScenePath);
        bool paletteExists = File.Exists(PalettePath);

        if (prefabIs2DOnly && sceneExists && paletteExists)
        {
            Debug.Log("[Physics2D Prototype] Asset validation PASS: scene, palette, and 2D-only player prefab are ready.");
        }
        else
        {
            Debug.LogError($"[Physics2D Prototype] Asset validation FAIL: Prefab2DOnly={prefabIs2DOnly}, Scene={sceneExists}, Palette={paletteExists}");
        }
    }

    private static void AutoBuildOnce()
    {
        if (SessionState.GetBool(AutoBuildKey, false)) return;
        SessionState.SetBool(AutoBuildKey, true);
        if (!File.Exists(ScenePath)) Build();
    }

    private static void OnValidationLog(string condition, string stackTrace, LogType type)
    {
        if (!SessionState.GetBool(PlayValidationRunningKey, false)) return;
        if (condition.Contains("[Physics2D Prototype] PASS:"))
        {
            SessionState.SetBool(PlayValidationPassedKey, true);
            EditorApplication.delayCall += EditorApplication.ExitPlaymode;
        }
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(PlayValidationRunningKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            validationStartTime = EditorApplication.timeSinceStartup;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            bool passed = SessionState.GetBool(PlayValidationPassedKey, false);
            SessionState.SetBool(PlayValidationRunningKey, false);
            Debug.Log(passed
                ? "[Physics2D Prototype] Play Mode validation PASS."
                : "[Physics2D Prototype] Play Mode validation FAIL.");
            EditorApplication.Exit(passed ? 0 : 1);
        }
    }

    private static void MonitorPlayValidation()
    {
        if (!SessionState.GetBool(PlayValidationRunningKey, false) || !EditorApplication.isPlaying) return;
        if (validationStartTime <= 0d)
        {
            validationStartTime = EditorApplication.timeSinceStartup;
            return;
        }
        if (EditorApplication.timeSinceStartup - validationStartTime < 10d) return;
        Debug.LogError("[Physics2D Prototype] Play Mode validation timed out.");
        EditorApplication.ExitPlaymode();
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/_Project/Prefabs/Test");
        EnsureFolder("Assets/_Project/Tiles/Physics2DPrototype");
        EnsureFolder("Assets/_Project/TilePalettes");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static Tile CreatePrototypeTile(string tilePath, string spritePath)
    {
        Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(tile, tilePath);
        }

        tile.name = Path.GetFileNameWithoutExtension(tilePath);
        tile.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        tile.color = Color.white;
        tile.transform = Matrix4x4.identity;
        tile.gameObject = null;
        tile.flags = TileFlags.LockColor;
        tile.colliderType = Tile.ColliderType.Grid;
        EditorUtility.SetDirty(tile);
        return tile;
    }

    private static void CreatePalette(Tile groundTile, Tile wallTile)
    {
        GameObject root = new GameObject("Physics2DPrototypePalette");
        root.AddComponent<Grid>();
        GameObject layer = new GameObject("Layer1");
        layer.transform.SetParent(root.transform, false);
        Tilemap tilemap = layer.AddComponent<Tilemap>();
        layer.AddComponent<TilemapRenderer>();
        tilemap.SetTile(new Vector3Int(0, 0, 0), groundTile);
        tilemap.SetTile(new Vector3Int(1, 0, 0), wallTile);
        PrefabUtility.SaveAsPrefabAsset(root, PalettePath);
        Object.DestroyImmediate(root);
    }

    private static GameObject CreatePlayerPrefab()
    {
        GameObject root = new GameObject("Player2D_Test");
        root.layer = LayerMask.NameToLayer("Player");

        Rigidbody2D body = root.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 3f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        CapsuleCollider2D capsule = root.AddComponent<CapsuleCollider2D>();
        capsule.size = new Vector2(0.75f, 1.8f);
        capsule.direction = CapsuleDirection2D.Vertical;
        capsule.sharedMaterial = CreateFrictionlessMaterial();

        GameObject visualRoot = new GameObject("VisualRoot");
        visualRoot.transform.SetParent(root.transform, false);
        SpriteRenderer renderer = visualRoot.AddComponent<SpriteRenderer>();
        renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Player/New/Player_Idle_01.png");
        renderer.sortingOrder = 10;

        Physics2DPrototypePlayer controller = root.AddComponent<Physics2DPrototypePlayer>();
        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.FindProperty("visual").objectReferenceValue = renderer;
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static PhysicsMaterial2D CreateFrictionlessMaterial()
    {
        const string path = "Assets/_Project/Prefabs/Test/Player2D_Frictionless.physicsMaterial2D";
        PhysicsMaterial2D material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
        if (material == null)
        {
            material = new PhysicsMaterial2D("Player2D_Frictionless") { friction = 0f, bounciness = 0f };
            AssetDatabase.CreateAsset(material, path);
        }
        return material;
    }

    private static void CreateTestScene(GameObject playerPrefab, Tile groundTile, Tile wallTile)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject gridObject = new GameObject("Grid");
        gridObject.AddComponent<Grid>();
        TilemapCollider2D groundCollider = CreateCollisionTilemap(gridObject.transform, "GroundTilemap", "Ground", out Tilemap ground);
        TilemapCollider2D wallCollider = CreateCollisionTilemap(gridObject.transform, "WallTilemap", "Wall", out Tilemap walls);

        for (int x = -12; x <= 12; x++) ground.SetTile(new Vector3Int(x, 0, 0), groundTile);
        for (int x = -3; x <= 2; x++) ground.SetTile(new Vector3Int(x, 4, 0), groundTile);
        for (int y = 1; y <= 7; y++)
        {
            walls.SetTile(new Vector3Int(-12, y, 0), wallTile);
            walls.SetTile(new Vector3Int(12, y, 0), wallTile);
        }
        ground.CompressBounds();
        walls.CompressBounds();

        GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
        player.transform.position = new Vector3(0f, 2.25f, 0f);

        GameObject spawn = new GameObject("SpawnPoint2D");
        spawn.transform.position = player.transform.position;
        Physics2DPrototypeSpawn spawnController = spawn.AddComponent<Physics2DPrototypeSpawn>();
        SetReference(spawnController, "player", player.GetComponent<Rigidbody2D>());

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5.5f;
        cameraObject.AddComponent<AudioListener>();
        Physics2DPrototypeCameraFollow follow = cameraObject.AddComponent<Physics2DPrototypeCameraFollow>();
        follow.SetTarget(player.transform);
        cameraObject.transform.position = player.transform.position + new Vector3(0f, 1f, -10f);

        GameObject validatorObject = new GameObject("PrototypeRuntimeValidator");
        Physics2DPrototypeRuntimeValidator validator = validatorObject.AddComponent<Physics2DPrototypeRuntimeValidator>();
        SetReference(validator, "player", player.GetComponent<Physics2DPrototypePlayer>());
        SetReference(validator, "groundCollider", groundCollider);
        SetReference(validator, "wallCollider", wallCollider);

        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    private static TilemapCollider2D CreateCollisionTilemap(
        Transform parent,
        string objectName,
        string layerName,
        out Tilemap tilemap)
    {
        GameObject target = new GameObject(objectName);
        target.transform.SetParent(parent, false);
        target.layer = LayerMask.NameToLayer(layerName);
        tilemap = target.AddComponent<Tilemap>();
        target.AddComponent<TilemapRenderer>();

        Rigidbody2D body = target.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Static;
        CompositeCollider2D composite = target.AddComponent<CompositeCollider2D>();
        composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
        composite.generationType = CompositeCollider2D.GenerationType.Synchronous;
        composite.enabled = false;
        TilemapCollider2D tilemapCollider = target.AddComponent<TilemapCollider2D>();
        tilemapCollider.compositeOperation = Collider2D.CompositeOperation.None;
        target.AddComponent<Physics2DPrototypeTilemapCollision>();
        return tilemapCollider;
    }

    private static void SetReference(Object target, string propertyName, Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(propertyName).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
