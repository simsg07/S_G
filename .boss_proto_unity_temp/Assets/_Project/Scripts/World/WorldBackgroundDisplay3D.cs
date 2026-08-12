using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class WorldBackgroundDisplay3D : MonoBehaviour
{
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int GrayscaleAmountId = Shader.PropertyToID("_GrayscaleAmount");

    [SerializeField] private Camera targetCamera;
    [SerializeField] private Texture2D backgroundTexture;
    [SerializeField] private string resourcesTexturePath = "Backgrounds/StageBackground";
    [SerializeField] private bool loadFromResources = true;
    [SerializeField] private float distanceFromCamera = 24f;
    [SerializeField] private Vector2 viewportPadding = new Vector2(1.12f, 1.12f);
    [SerializeField] private Color worldATint = Color.white;
    [SerializeField] private Color worldBTint = new Color(0.76f, 0.78f, 0.82f, 1f);

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh generatedMesh;
    private Material generatedMaterial;

    public void Configure(Camera camera, Texture2D texture, string resourcesPath)
    {
        targetCamera = camera != null ? camera : targetCamera;
        backgroundTexture = texture != null ? texture : backgroundTexture;
        if (!string.IsNullOrWhiteSpace(resourcesPath))
        {
            resourcesTexturePath = resourcesPath;
        }

        EnsureVisual();
        TryLoadResourceTexture();
        ApplyWorld(WorldSystem3D.ActiveWorld);
        FitToCamera();
    }

    public void SetBackgroundTexture(Texture2D texture)
    {
        backgroundTexture = texture;
        ApplyTexture();
    }

    private void Awake()
    {
        EnsureVisual();
    }

    private void OnEnable()
    {
        WorldSystem3D.ActiveWorldChanged += HandleWorldChanged;
        TryLoadResourceTexture();
        ApplyWorld(WorldSystem3D.ActiveWorld);
    }

    private void OnDisable()
    {
        WorldSystem3D.ActiveWorldChanged -= HandleWorldChanged;
    }

    private void LateUpdate()
    {
        FitToCamera();
    }

    private void OnDestroy()
    {
        DestroyGenerated(generatedMaterial);
        DestroyGenerated(generatedMesh);
    }

    private void HandleWorldChanged(ResearchWorldId previousWorld, ResearchWorldId nextWorld)
    {
        ApplyWorld(nextWorld);
    }

    private void EnsureVisual()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        if (generatedMesh == null)
        {
            generatedMesh = CreateQuadMesh();
            generatedMesh.hideFlags = HideFlags.HideAndDontSave;
        }

        meshFilter.sharedMesh = generatedMesh;

        if (generatedMaterial == null)
        {
            Shader shader = Shader.Find("S_G/World Background Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            generatedMaterial = new Material(shader)
            {
                name = "Generated World Background Material",
                hideFlags = HideFlags.HideAndDontSave
            };
            generatedMaterial.renderQueue = 1000;
        }

        meshRenderer.sharedMaterial = generatedMaterial;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        ApplyTexture();
    }

    private void TryLoadResourceTexture()
    {
        if (!loadFromResources || backgroundTexture != null || string.IsNullOrWhiteSpace(resourcesTexturePath))
        {
            return;
        }

        backgroundTexture = Resources.Load<Texture2D>(resourcesTexturePath);
        ApplyTexture();
    }

    private void ApplyTexture()
    {
        if (generatedMaterial == null)
        {
            return;
        }

        generatedMaterial.SetTexture(BaseMapId, backgroundTexture);
        generatedMaterial.SetTexture(MainTexId, backgroundTexture);
        if (meshRenderer != null)
        {
            meshRenderer.enabled = backgroundTexture != null;
        }
    }

    private void ApplyWorld(ResearchWorldId world)
    {
        if (generatedMaterial == null)
        {
            return;
        }

        bool worldB = world == ResearchWorldId.WorldB;
        generatedMaterial.SetColor(BaseColorId, worldB ? worldBTint : worldATint);
        generatedMaterial.SetFloat(GrayscaleAmountId, worldB ? 1f : 0f);
    }

    private void FitToCamera()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            return;
        }

        float distance = Mathf.Max(0.5f, distanceFromCamera);
        transform.SetPositionAndRotation(
            targetCamera.transform.position + targetCamera.transform.forward * distance,
            targetCamera.transform.rotation
        );

        float height;
        if (targetCamera.orthographic)
        {
            height = targetCamera.orthographicSize * 2f;
        }
        else
        {
            height = 2f * Mathf.Tan(targetCamera.fieldOfView * Mathf.Deg2Rad * 0.5f) * distance;
        }

        float width = height * targetCamera.aspect;
        transform.localScale = new Vector3(width * viewportPadding.x, height * viewportPadding.y, 1f);
    }

    private static Mesh CreateQuadMesh()
    {
        Mesh mesh = new Mesh { name = "Generated World Background Quad" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f)
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateBounds();
        return mesh;
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
