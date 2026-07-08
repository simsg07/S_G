using UnityEngine;

[DisallowMultipleComponent]
public class CameraMarkState3D : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [SerializeField] private Color markedColor = new Color(0.35f, 0.95f, 1f, 0.95f);
    [SerializeField] private Color cooldownColor = new Color(1f, 0.76f, 0.24f, 0.65f);
    [SerializeField] private float markerWidth = 0.95f;
    [SerializeField] private float markerHeight = 0.08f;
    [SerializeField] private float markerDepth = 0.08f;
    [SerializeField] private float markerYOffset = 0.28f;
    [SerializeField] private float pulseSpeed = 8f;

    private MeshRenderer markerRenderer;
    private MeshFilter markerFilter;
    private Material markerMaterial;
    private Mesh markerMesh;
    private float markEndTime;
    private float cooldownEndTime;

    public bool IsMarked => Time.time < markEndTime;
    public bool IsCoolingDown => Time.time >= markEndTime && Time.time < cooldownEndTime;

    private void Awake()
    {
        EnsureMarker();
        ApplyVisual();
    }

    private void Update()
    {
        ApplyVisual();
    }

    private void OnDestroy()
    {
        DestroyGenerated(markerMaterial);
        DestroyGenerated(markerMesh);
    }

    public void SetMarkWindow(float markEnd, float cooldownEnd)
    {
        markEndTime = markEnd;
        cooldownEndTime = Mathf.Max(cooldownEnd, markEnd);
        EnsureMarker();
        ApplyVisual();
    }

    public void ClearMark()
    {
        markEndTime = 0f;
        cooldownEndTime = 0f;
        ApplyVisual();
    }

    private void EnsureMarker()
    {
        if (markerRenderer != null)
        {
            return;
        }

        GameObject marker = new GameObject("Camera Mark Indicator", typeof(MeshFilter), typeof(MeshRenderer));
        marker.transform.SetParent(transform, false);
        markerFilter = marker.GetComponent<MeshFilter>();
        markerRenderer = marker.GetComponent<MeshRenderer>();

        markerMesh = CreateCubeMesh();
        markerMesh.hideFlags = HideFlags.HideAndDontSave;
        markerFilter.sharedMesh = markerMesh;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        markerMaterial = new Material(shader)
        {
            name = "Generated Camera Mark Material",
            hideFlags = HideFlags.HideAndDontSave
        };
        markerRenderer.sharedMaterial = markerMaterial;
        markerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        markerRenderer.receiveShadows = false;
    }

    private void ApplyVisual()
    {
        EnsureMarker();

        bool active = IsMarked || IsCoolingDown;
        markerRenderer.enabled = active;
        if (!active)
        {
            return;
        }

        Bounds bounds = CalculateBounds();
        Transform markerTransform = markerRenderer.transform;
        markerTransform.position = new Vector3(bounds.center.x, bounds.max.y + markerYOffset, bounds.center.z);

        float pulse = IsMarked ? 1f + Mathf.Sin(Time.time * pulseSpeed) * 0.08f : 1f;
        markerTransform.localScale = new Vector3(markerWidth * pulse, markerHeight, markerDepth);

        SetMarkerColor(IsMarked ? markedColor : cooldownColor);
    }

    private void SetMarkerColor(Color color)
    {
        if (markerMaterial == null)
        {
            return;
        }

        if (markerMaterial.HasProperty(BaseColorId))
        {
            markerMaterial.SetColor(BaseColorId, color);
        }

        if (markerMaterial.HasProperty(ColorId))
        {
            markerMaterial.SetColor(ColorId, color);
        }

        markerMaterial.color = color;
    }

    private Bounds CalculateBounds()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds bounds = new Bounds(transform.position, Vector3.one);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer == markerRenderer)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return bounds;
    }

    private static Mesh CreateCubeMesh()
    {
        Mesh mesh = new Mesh { name = "Generated Camera Mark Cube" };
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
