using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AbilityTestSceneBootstrap3D : MonoBehaviour
{
    [SerializeField] private bool resetProgressOnStart = true;
    [SerializeField] private bool buildGeneratedMapOnStart; // 켜면 옛 테스트용 맵을 실행 시 코드로 생성합니다. 기본은 꺼서 씬에 배치한 맵을 그대로 사용합니다.
    [SerializeField] private bool createRuntimeHud = true; // 씬 배치형 테스트에서도 월드/조작 HUD를 실행 중 표시할지 정합니다.
    [SerializeField] private Color worldAColor = new Color(0.35f, 0.62f, 1f, 1f);
    [SerializeField] private Color worldBColor = new Color(0.78f, 0.78f, 0.82f, 1f);

    private readonly List<Material> generatedMaterials = new List<Material>();
    private Text worldText;
    private Text helpText;
    private CameraAbilitySystem3D abilitySystem;
    private WorldVisualEffects3D visualEffects;

    private void Start()
    {
        if (resetProgressOnStart)
        {
            GameProgressSave3D.ResetProgress();
        }

        if (buildGeneratedMapOnStart)
        {
            BuildScene();
            return;
        }

        InitializePlacedScene();
    }

    private void Update()
    {
        RefreshHud();
    }

    private void OnDestroy()
    {
        for (int i = 0; i < generatedMaterials.Count; i++)
        {
            if (generatedMaterials[i] != null)
            {
                Destroy(generatedMaterials[i]);
            }
        }
    }

    private void BuildScene()
    {
        WorldSystem3D.EnsureInstance().SetWorld(ResearchWorldId.WorldA);
        CreateCamera();
        CreateLight();
        CreatePlayer();
        CreateAbilityCourse();
        CreateDummyMonsters();
        CreateHud();
        WorldSystem3D.EnsureInstance().RefreshWorldObjects();
    }

    private void InitializePlacedScene()
    {
        WorldSystem3D.EnsureInstance().SetWorld(ResearchWorldId.WorldA);
        CreateCamera();
        CreateLight();

        PlatformerPlayer3D player = FindFirstObjectByType<PlatformerPlayer3D>();
        if (player != null)
        {
            abilitySystem = player.GetComponent<CameraAbilitySystem3D>();
            if (abilitySystem == null)
            {
                abilitySystem = player.gameObject.AddComponent<CameraAbilitySystem3D>();
            }

            abilitySystem.UnlockAbility(CameraAbilityId.Focus);
            abilitySystem.UnlockAbility(CameraAbilityId.Flash);
            abilitySystem.UnlockAbility(CameraAbilityId.Relay);

            if (visualEffects != null)
            {
                visualEffects.SetDustTarget(player.transform);
            }
        }

        if (createRuntimeHud)
        {
            CreateHud();
        }

        WorldSystem3D.EnsureInstance().RefreshWorldObjects();
    }

    private void CreateCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(CameraFollow3D));
            mainCamera = cameraObject.GetComponent<Camera>();
            cameraObject.tag = "MainCamera";
        }

        mainCamera.orthographic = true;
        mainCamera.orthographicSize = 5.2f;
        mainCamera.transform.position = new Vector3(0f, 1.4f, -10f);
        TwoPointFiveDUtility3D.ConfigureSideViewCamera(mainCamera, 5.2f);
        mainCamera.backgroundColor = new Color(0.06f, 0.07f, 0.09f, 1f);

        visualEffects = mainCamera.GetComponent<WorldVisualEffects3D>();
        if (visualEffects == null)
        {
            visualEffects = mainCamera.gameObject.AddComponent<WorldVisualEffects3D>();
        }
    }

    private void CreateLight()
    {
        if (FindFirstObjectByType<Light>() != null)
        {
            return;
        }

        GameObject lightObject = new GameObject("Ability Test Directional Light", typeof(Light));
        Light light = lightObject.GetComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    private void CreatePlayer()
    {
        GameObject player = CreateBox("Player", new Vector3(-5.8f, 0.95f, 0f), new Vector3(0.8f, 1.2f, 1f), new Color(0.88f, 0.92f, 0.96f, 1f));
        player.tag = "Player";
        player.GetComponent<MeshFilter>().sharedMesh.name = "Ability Test Player Mesh";
        player.GetComponent<MeshRenderer>().sharedMaterial.name = "Ability Test Player Material";

        player.AddComponent<PlatformerPlayer3D>();
        abilitySystem = player.GetComponent<CameraAbilitySystem3D>();
        if (abilitySystem == null)
        {
            abilitySystem = player.AddComponent<CameraAbilitySystem3D>();
        }

        abilitySystem.UnlockAbility(CameraAbilityId.Focus);
        abilitySystem.UnlockAbility(CameraAbilityId.Flash);
        abilitySystem.UnlockAbility(CameraAbilityId.Relay);

        if (visualEffects != null)
        {
            visualEffects.SetDustTarget(player.transform);
        }
    }

    private void CreateAbilityCourse()
    {
        CreateBox("Ground", new Vector3(0f, -0.35f, 0f), new Vector3(15f, 0.45f, 1f), new Color(0.23f, 0.24f, 0.26f, 1f));

        GameObject worldPlatform = CreateBox("World Reactive Platform", new Vector3(-0.8f, 1.2f, 0f), new Vector3(2.6f, 0.32f, 1f), worldAColor);
        AddWorldState(worldPlatform, true, true, worldAColor, worldBColor);

        GameObject flashBridge = CreateBox("Flash Bridge", new Vector3(2.6f, 1.25f, 0f), new Vector3(2.3f, 0.3f, 1f), new Color(0.24f, 0.9f, 0.55f, 1f));
        flashBridge.SetActive(false);

        GameObject flashTarget = CreateBox("Flash Target", new Vector3(0.9f, 0.35f, 0f), new Vector3(0.55f, 0.75f, 0.8f), new Color(1f, 0.82f, 0.15f, 1f));
        CameraTagUtility3D.TrySetTag(flashTarget, CameraTagUtility3D.TargetTag);
        flashTarget.AddComponent<AbilityTestFlashTarget3D>().Configure(new[] { flashBridge });

        GameObject relayStone = CreateBox("Relay Stone", new Vector3(4.35f, 0.25f, 0f), new Vector3(0.72f, 0.72f, 0.8f), new Color(0.8f, 0.78f, 0.72f, 1f));
        CameraTagUtility3D.TrySetTag(relayStone, CameraTagUtility3D.RelayTargetTag);
        AddWorldState(relayStone, true, true, new Color(0.8f, 0.78f, 0.72f, 1f), new Color(0.62f, 0.66f, 0.74f, 1f));

        GameObject worldBMarker = CreateBox("World B Marker", new Vector3(4.35f, 1.45f, 0f), new Vector3(0.9f, 0.2f, 0.8f), worldBColor);
        AddWorldState(worldBMarker, false, true, worldAColor, worldBColor);

        GameObject goal = CreateBox("Goal Marker", new Vector3(6.6f, 0.35f, 0f), new Vector3(0.45f, 1.2f, 0.8f), new Color(0.95f, 0.95f, 0.2f, 1f));
        AddWorldState(goal, true, true, new Color(0.95f, 0.95f, 0.2f, 1f), new Color(0.85f, 0.85f, 0.95f, 1f));
    }

    private void CreateDummyMonsters()
    {
        MonsterDummyDatabase3D database = MonsterDummyDatabase3D.Load();
        if (database == null)
        {
            SpawnFallbackDummyMonster();
            return;
        }

        IReadOnlyList<MonsterDummyProfile3D> monsters = database.Monsters;
        if (monsters == null || monsters.Count == 0)
        {
            SpawnFallbackDummyMonster();
            return;
        }

        for (int i = 0; i < monsters.Count; i++)
        {
            MonsterDummyProfile3D profile = monsters[i];
            GameObject monster = new GameObject($"Dummy Monster - {profile.displayName}");
            monster.AddComponent<MonsterDummyActor3D>().Initialize(profile);
        }
    }

    private void SpawnFallbackDummyMonster()
    {
        MonsterDummyProfile3D profile = new MonsterDummyProfile3D
        {
            id = "fallback_shutter_dummy",
            displayName = "Fallback Shutter Dummy",
            position = new Vector3(-3.2f, 0.35f, 0f),
            size = new Vector3(0.8f, 0.8f, 0.8f),
            color = new Color(0.75f, 0.45f, 0.95f, 1f),
            canBeFrozen = true,
            reactsToFlash = true,
            health = 1
        };

        GameObject monster = new GameObject("Dummy Monster - Fallback");
        monster.AddComponent<MonsterDummyActor3D>().Initialize(profile);
    }

    private void CreateHud()
    {
        GameObject canvasObject = new GameObject("Ability Test HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;

        worldText = CreateHudText(canvasObject.transform, "World Label", new Vector2(-24f, -24f), 44, TextAnchor.UpperRight, true);
        helpText = CreateHudText(canvasObject.transform, "Controls Label", new Vector2(24f, -84f), 24, TextAnchor.UpperLeft, false);
        RefreshHud();
    }

    private Text CreateHudText(Transform parent, string name, Vector2 anchoredPosition, int fontSize, TextAnchor alignment, bool anchorRight)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);

        RectTransform rectTransform = textObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(anchorRight ? 1f : 0f, 1f);
        rectTransform.anchorMax = new Vector2(anchorRight ? 1f : 0f, 1f);
        rectTransform.pivot = new Vector2(anchorRight ? 1f : 0f, 1f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = new Vector2(760f, 220f);

        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private void RefreshHud()
    {
        if (worldText != null)
        {
            ResearchWorldId world = WorldSystem3D.ActiveWorld;
            worldText.text = world == ResearchWorldId.WorldA ? "WORLD A" : "WORLD B";
            worldText.color = world == ResearchWorldId.WorldA ? worldAColor : worldBColor;
        }

        if (helpText != null)
        {
            helpText.text =
                "Mouse : Aim camera frame\n" +
                "Left Click : Use selected camera function\n" +
                "E : Change Shutter / Focus\n" +
                "Q : Switch whole World A/B\n" +
                "R : Toggle camera light\n" +
                "Camera intervention succeeds once by default";
        }
    }

    private GameObject CreateBox(string name, Vector3 position, Vector3 size, Color color)
    {
        GameObject box = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider));
        box.transform.position = TwoPointFiveDUtility3D.ProjectPositionToPlane(position);
        box.transform.localScale = size;

        MeshFilter meshFilter = box.GetComponent<MeshFilter>();
        meshFilter.sharedMesh = CreateCubeMesh($"{name} Mesh");

        MeshRenderer meshRenderer = box.GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = CreateMaterial($"{name} Material", color);

        BoxCollider boxCollider = box.GetComponent<BoxCollider>();
        boxCollider.size = Vector3.one;
        boxCollider.center = Vector3.zero;
        return box;
    }

    private WorldStateObject3D AddWorldState(GameObject target, bool enabledInWorldA, bool enabledInWorldB, Color worldAStateColor, Color worldBStateColor)
    {
        WorldStateObject3D stateObject = target.AddComponent<WorldStateObject3D>();

        WorldObjectState3D stateA = stateObject.GetState(ResearchWorldId.WorldA);
        stateA.enabledInWorld = enabledInWorldA;
        stateA.rendererEnabled = enabledInWorldA;
        stateA.collisionEnabled = enabledInWorldA;
        stateA.operationEnabled = enabledInWorldA;
        stateA.useColorOverride = true;
        stateA.colorOverride = worldAStateColor;

        WorldObjectState3D stateB = stateObject.GetState(ResearchWorldId.WorldB);
        stateB.enabledInWorld = enabledInWorldB;
        stateB.rendererEnabled = enabledInWorldB;
        stateB.collisionEnabled = enabledInWorldB;
        stateB.operationEnabled = enabledInWorldB;
        stateB.useColorOverride = true;
        stateB.colorOverride = worldBStateColor;

        stateObject.Apply(WorldSystem3D.ActiveWorld);
        return stateObject;
    }

    private Mesh CreateCubeMesh(string meshName)
    {
        Mesh mesh = new Mesh { name = meshName };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f),
            new Vector3(0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f)
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
        return mesh;
    }

    private Material CreateMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader)
        {
            name = materialName,
            color = color,
            hideFlags = HideFlags.HideAndDontSave
        };
        generatedMaterials.Add(material);
        return material;
    }
}
