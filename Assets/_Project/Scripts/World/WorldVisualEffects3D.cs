using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class WorldVisualEffects3D : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private Camera targetCamera; // 월드 후처리 효과를 적용할 카메라입니다. 비워두면 같은 오브젝트의 Camera를 사용합니다.
    [SerializeField] private Transform dustTarget; // 월드 전환 먼지 파티클이 따라갈 대상입니다.

    [Header("Background")]
    [SerializeField] private bool createBackgroundDisplay = true; // 월드 배경 표시 오브젝트를 자동으로 만들지 정합니다.
    [SerializeField] private Texture2D backgroundTexture; // 직접 지정할 배경 이미지입니다. 비워두면 Resources 경로에서 불러옵니다.
    [SerializeField] private string resourcesBackgroundPath = "Backgrounds/StageBackground"; // Resources 폴더 안에서 배경 이미지를 찾을 경로입니다.

    [Header("World Tone")]
    [SerializeField] private Color worldACameraColor = new Color(0.08f, 0.1f, 0.13f, 1f); // 월드 A에서 카메라 배경색입니다.
    [SerializeField] private Color worldBCameraColor = new Color(0.05f, 0.05f, 0.055f, 1f); // 월드 B에서 카메라 배경색입니다.
    [SerializeField] private float transitionLerpSpeed = 8f; // 월드 B 후처리 효과가 목표값으로 따라가는 속도입니다.

    [Header("Static")]
    [SerializeField, Range(0f, 1f)] private float worldAStaticAlpha = 0f; // 월드 A 노이즈 투명도입니다. 0이면 월드 A에서는 노이즈가 완전히 꺼집니다.
    [SerializeField, Range(0f, 1f)] private float worldBStaticAlpha = 0.065f; // 월드 B 기본 노이즈 투명도입니다.
    [SerializeField, Range(0f, 1f)] private float transitionStaticBoost = 0.18f; // 월드 B로 전환되는 순간 추가되는 노이즈 강도입니다.
    [SerializeField] private float staticFrameInterval = 0.045f; // 노이즈 텍스처를 새로 섞는 시간 간격입니다.

    [Header("Dust")]
    [SerializeField] private int dustBurstCount = 54; // 월드 전환 때 한 번에 발생하는 먼지 파티클 수입니다.
    [SerializeField] private Vector3 dustOffset = new Vector3(0f, -0.62f, 0f); // 먼지 파티클 위치를 기준 대상에서 얼마나 옮길지 정합니다.

    private Volume worldVolume;
    private VolumeProfile runtimeProfile;
    private ColorAdjustments colorAdjustments;
    private FilmGrain filmGrain;
    private ChromaticAberration chromaticAberration;
    private LensDistortion lensDistortion;
    private Vignette vignette;

    private Canvas staticCanvas;
    private RawImage staticImage;
    private Image glitchLineA;
    private Image glitchLineB;
    private Texture2D staticTexture;
    private Color32[] staticPixels;

    private ParticleSystem dustParticles;
    private GameObject dustObject;
    private Material dustMaterial;
    private WorldBackgroundDisplay3D backgroundDisplay;

    private ResearchWorldId currentWorld = ResearchWorldId.WorldA;
    private float targetSaturation;
    private float targetContrast;
    private float targetGrain;
    private float targetChromatic;
    private float targetDistortion;
    private float targetVignette;
    private float transitionNoise;
    private float staticTimer;

    public void SetDustTarget(Transform target)
    {
        dustTarget = target;
        UpdateDustPosition();
    }

    public void SetBackgroundTexture(Texture2D texture)
    {
        backgroundTexture = texture;
        if (backgroundDisplay != null)
        {
            backgroundDisplay.SetBackgroundTexture(texture);
        }
    }

    private void Awake()
    {
        targetCamera = targetCamera != null ? targetCamera : GetComponent<Camera>();
        currentWorld = WorldSystem3D.ActiveWorld;

        SetupPostProcessing();
        SetupStaticOverlay();
        SetupDust();
        SetupBackground();
        ApplyWorld(currentWorld, true);
    }

    private void OnEnable()
    {
        WorldSystem3D.ActiveWorldChanged += HandleWorldChanged;
        ApplyWorld(WorldSystem3D.ActiveWorld, true);
    }

    private void OnDisable()
    {
        WorldSystem3D.ActiveWorldChanged -= HandleWorldChanged;
    }

    private void Update()
    {
        UpdatePostProcessing();
        UpdateStaticOverlay();
        UpdateDustPosition();
    }

    private void OnDestroy()
    {
        DestroyGenerated(runtimeProfile);
        DestroyGenerated(staticTexture);
        DestroyGenerated(dustMaterial);
    }

    private void HandleWorldChanged(ResearchWorldId previousWorld, ResearchWorldId nextWorld)
    {
        currentWorld = nextWorld;
        transitionNoise = nextWorld == ResearchWorldId.WorldB ? 1f : 0f;
        EmitDust(nextWorld);
        ApplyWorld(nextWorld, false);
    }

    private void SetupPostProcessing()
    {
        if (targetCamera == null)
        {
            return;
        }

        UniversalAdditionalCameraData cameraData = targetCamera.GetComponent<UniversalAdditionalCameraData>();
        if (cameraData == null)
        {
            cameraData = targetCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        }

        cameraData.renderPostProcessing = true;

        GameObject volumeObject = new GameObject("World Camera Post Processing", typeof(Volume));
        volumeObject.transform.SetParent(transform, false);
        worldVolume = volumeObject.GetComponent<Volume>();
        worldVolume.isGlobal = true;
        worldVolume.priority = 80f;
        worldVolume.weight = 1f;

        runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        runtimeProfile.name = "Runtime World Visual Profile";
        worldVolume.profile = runtimeProfile;

        colorAdjustments = runtimeProfile.Add<ColorAdjustments>(true);
        colorAdjustments.saturation.overrideState = true;
        colorAdjustments.contrast.overrideState = true;
        colorAdjustments.colorFilter.overrideState = true;

        filmGrain = runtimeProfile.Add<FilmGrain>(true);
        filmGrain.type.overrideState = true;
        filmGrain.intensity.overrideState = true;
        filmGrain.response.overrideState = true;
        filmGrain.type.value = FilmGrainLookup.Medium6;
        filmGrain.response.value = 0.38f;

        chromaticAberration = runtimeProfile.Add<ChromaticAberration>(true);
        chromaticAberration.intensity.overrideState = true;

        lensDistortion = runtimeProfile.Add<LensDistortion>(true);
        lensDistortion.intensity.overrideState = true;
        lensDistortion.scale.overrideState = true;
        lensDistortion.scale.value = 1.03f;

        vignette = runtimeProfile.Add<Vignette>(true);
        vignette.intensity.overrideState = true;
        vignette.smoothness.overrideState = true;
        vignette.smoothness.value = 0.48f;
    }

    private void SetupStaticOverlay()
    {
        GameObject canvasObject = new GameObject("World Static Overlay", typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        staticCanvas = canvasObject.GetComponent<Canvas>();
        staticCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        staticCanvas.sortingOrder = 450;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;

        GameObject imageObject = new GameObject("Static Noise", typeof(RectTransform), typeof(RawImage));
        imageObject.transform.SetParent(canvasObject.transform, false);
        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;

        staticTexture = new Texture2D(96, 54, TextureFormat.RGBA32, false)
        {
            name = "Generated Static Noise",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat
        };
        staticPixels = new Color32[staticTexture.width * staticTexture.height];

        staticImage = imageObject.GetComponent<RawImage>();
        staticImage.texture = staticTexture;
        staticImage.raycastTarget = false;
        staticImage.color = Color.clear;

        glitchLineA = CreateGlitchLine(canvasObject.transform, "Glitch Line A", new Color(0.75f, 0.95f, 1f, 0f));
        glitchLineB = CreateGlitchLine(canvasObject.transform, "Glitch Line B", new Color(1f, 1f, 1f, 0f));
        RebuildStaticTexture();
    }

    private Image CreateGlitchLine(Transform parent, string name, Color color)
    {
        GameObject lineObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        lineObject.transform.SetParent(parent, false);

        RectTransform rectTransform = lineObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0f, 0.5f);
        rectTransform.anchorMax = new Vector2(1f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = new Vector2(0f, 3f);

        Image image = lineObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private void SetupDust()
    {
        dustObject = new GameObject("World Switch Dust", typeof(ParticleSystem));
        dustParticles = dustObject.GetComponent<ParticleSystem>();

        ParticleSystem.MainModule main = dustParticles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.6f, 3.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.14f);
        main.gravityModifier = 0.16f;

        ParticleSystem.EmissionModule emission = dustParticles.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = dustParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.55f;
        shape.arc = 360f;
        shape.rotation = new Vector3(90f, 0f, 0f);

        ParticleSystem.VelocityOverLifetimeModule velocity = dustParticles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.25f, 0.9f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        ParticleSystemRenderer renderer = dustParticles.GetComponent<ParticleSystemRenderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Particles/Standard Unlit");
        }

        dustMaterial = new Material(shader)
        {
            name = "Generated World Dust Material",
            hideFlags = HideFlags.HideAndDontSave
        };
        renderer.sharedMaterial = dustMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingOrder = 20;

        UpdateDustPosition();
    }

    private void SetupBackground()
    {
        if (!createBackgroundDisplay || targetCamera == null)
        {
            return;
        }

        WorldBackgroundDisplay3D existing = FindFirstObjectByType<WorldBackgroundDisplay3D>();
        if (existing != null)
        {
            backgroundDisplay = existing;
        }
        else
        {
            GameObject backgroundObject = new GameObject(
                "World Background",
                typeof(MeshFilter),
                typeof(MeshRenderer),
                typeof(WorldBackgroundDisplay3D)
            );
            backgroundDisplay = backgroundObject.GetComponent<WorldBackgroundDisplay3D>();
        }

        backgroundDisplay.Configure(targetCamera, backgroundTexture, resourcesBackgroundPath);
    }

    private void ApplyWorld(ResearchWorldId world, bool instant)
    {
        currentWorld = world;
        bool worldB = world == ResearchWorldId.WorldB;

        targetSaturation = worldB ? -100f : 0f;
        targetContrast = worldB ? 18f : 0f;
        targetGrain = worldB ? 0.38f : 0f;
        targetChromatic = worldB ? 0.16f : 0f;
        targetDistortion = worldB ? -0.035f : 0f;
        targetVignette = worldB ? 0.34f : 0f;

        if (targetCamera != null)
        {
            targetCamera.backgroundColor = worldB ? worldBCameraColor : worldACameraColor;
        }

        if (instant || !worldB)
        {
            SetPostValues(targetSaturation, targetContrast, targetGrain, targetChromatic, targetDistortion, targetVignette);
        }
    }

    private void UpdatePostProcessing()
    {
        if (colorAdjustments == null)
        {
            return;
        }

        float t = 1f - Mathf.Exp(-transitionLerpSpeed * Time.deltaTime);
        float jitter = (Mathf.PerlinNoise(Time.time * 24.3f, 0.17f) - 0.5f) * transitionNoise;

        SetPostValues(
            Mathf.Lerp(colorAdjustments.saturation.value, targetSaturation, t),
            Mathf.Lerp(colorAdjustments.contrast.value, targetContrast, t),
            Mathf.Lerp(filmGrain.intensity.value, targetGrain + transitionNoise * 0.22f, t),
            Mathf.Lerp(chromaticAberration.intensity.value, targetChromatic + transitionNoise * 0.18f, t),
            Mathf.Lerp(lensDistortion.intensity.value, targetDistortion + jitter * 0.07f, t),
            Mathf.Lerp(vignette.intensity.value, targetVignette + transitionNoise * 0.12f, t)
        );

        transitionNoise = Mathf.MoveTowards(transitionNoise, 0f, Time.deltaTime * 2.25f);
    }

    private void SetPostValues(float saturation, float contrast, float grain, float chromatic, float distortion, float vignetteAmount)
    {
        if (colorAdjustments != null)
        {
            colorAdjustments.saturation.value = saturation;
            colorAdjustments.contrast.value = contrast;
            colorAdjustments.colorFilter.value = Color.white;
        }

        if (filmGrain != null)
        {
            filmGrain.intensity.value = Mathf.Clamp01(grain);
        }

        if (chromaticAberration != null)
        {
            chromaticAberration.intensity.value = Mathf.Clamp01(chromatic);
        }

        if (lensDistortion != null)
        {
            lensDistortion.intensity.value = Mathf.Clamp(distortion, -0.35f, 0.35f);
        }

        if (vignette != null)
        {
            vignette.intensity.value = Mathf.Clamp01(vignetteAmount);
        }
    }

    private void UpdateStaticOverlay()
    {
        if (staticImage == null || staticTexture == null)
        {
            return;
        }

        float baseAlpha = currentWorld == ResearchWorldId.WorldB ? worldBStaticAlpha : worldAStaticAlpha;
        float strength = Mathf.Clamp01(baseAlpha + transitionNoise * transitionStaticBoost);
        staticImage.color = new Color(1f, 1f, 1f, strength);
        if (strength <= 0.001f)
        {
            UpdateGlitchLine(glitchLineA, 0f, 1f);
            UpdateGlitchLine(glitchLineB, 0f, -1f);
            return;
        }

        if (Time.unscaledTime >= staticTimer)
        {
            staticTimer = Time.unscaledTime + Mathf.Max(0.01f, staticFrameInterval);
            RebuildStaticTexture();
            staticImage.uvRect = new Rect(Random.value, Random.value, 1f + strength * 2.5f, 1f + strength * 1.6f);
            UpdateGlitchLine(glitchLineA, strength, 1f);
            UpdateGlitchLine(glitchLineB, strength * 0.65f, -1f);
        }
    }

    private void RebuildStaticTexture()
    {
        if (staticPixels == null || staticTexture == null)
        {
            return;
        }

        for (int i = 0; i < staticPixels.Length; i++)
        {
            byte shade = (byte)Random.Range(35, 230);
            byte alpha = (byte)Random.Range(70, 190);
            staticPixels[i] = new Color32(shade, shade, shade, alpha);
        }

        staticTexture.SetPixels32(staticPixels);
        staticTexture.Apply(false);
    }

    private void UpdateGlitchLine(Image line, float strength, float direction)
    {
        if (line == null)
        {
            return;
        }

        RectTransform rectTransform = line.rectTransform;
        float y = Random.Range(-460f, 460f);
        float x = Random.Range(-90f, 90f) * direction;
        rectTransform.anchoredPosition = new Vector2(x, y);
        rectTransform.sizeDelta = new Vector2(0f, Random.Range(2f, 9f));

        Color color = line.color;
        color.a = Mathf.Clamp01(strength * Random.Range(1.6f, 3.5f));
        line.color = color;
    }

    private void UpdateDustPosition()
    {
        if (dustObject == null)
        {
            return;
        }

        if (dustTarget != null)
        {
            dustObject.transform.position = dustTarget.position + dustOffset;
        }
        else if (targetCamera != null)
        {
            dustObject.transform.position = targetCamera.transform.position + targetCamera.transform.forward * 8f;
        }
    }

    private void EmitDust(ResearchWorldId world)
    {
        if (dustParticles == null)
        {
            return;
        }

        UpdateDustPosition();
        Color dustColor = world == ResearchWorldId.WorldB
            ? new Color(0.7f, 0.72f, 0.76f, 0.55f)
            : new Color(0.5f, 0.74f, 1f, 0.55f);

        ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
        {
            startColor = dustColor
        };
        dustParticles.Emit(emitParams, Mathf.Max(1, dustBurstCount));
    }

    private static void DestroyGenerated(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
