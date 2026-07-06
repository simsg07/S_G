using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class PlatformSurface3D : MonoBehaviour
{
    [SerializeField] private Vector3 platformSize = new Vector3(6f, 0.35f, 1f); // 발판의 크기입니다.
    [SerializeField] private bool dropThrough; // 켜면 S+스페이스로 아래로 내려갈 수 있는 발판이 됩니다.
    [SerializeField] private Color surfaceColor = new Color(0.35f, 0.35f, 0.35f, 1f); // 발판 색상입니다.

    private static readonly List<PlatformSurface3D> activeSurfaces = new List<PlatformSurface3D>();
    private static Mesh cubeMesh;

    private Material visualMaterial;

    public static IReadOnlyList<PlatformSurface3D> ActiveSurfaces => activeSurfaces;
    public bool CanDropThrough => dropThrough;
    public Collider PlatformCollider { get; private set; }

    private void Awake()
    {
        ConfigurePlatform();
    }

    private void OnEnable()
    {
        if (!activeSurfaces.Contains(this))
        {
            activeSurfaces.Add(this);
        }

        ConfigurePlatform();
    }

    private void OnDisable()
    {
        activeSurfaces.Remove(this);
    }

    private void ConfigurePlatform()
    {
        platformSize.x = Mathf.Max(0.1f, platformSize.x);
        platformSize.y = Mathf.Max(0.1f, platformSize.y);
        platformSize.z = Mathf.Max(0.1f, platformSize.z);

        BoxCollider boxCollider = GetComponent<BoxCollider>();
        if (boxCollider == null)
        {
            boxCollider = gameObject.AddComponent<BoxCollider>();
        }

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
        }

        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
        }

        boxCollider.size = Vector3.one;
        boxCollider.center = Vector3.zero;
        boxCollider.isTrigger = false;
        PlatformCollider = boxCollider;

        meshFilter.sharedMesh = GetCubeMesh();

        if (visualMaterial == null)
        {
            visualMaterial = CreateMaterial("Generated Platform Material", surfaceColor);
        }

        visualMaterial.color = surfaceColor;
        meshRenderer.sharedMaterial = visualMaterial;

        transform.localScale = platformSize;
    }

    private static Mesh GetCubeMesh()
    {
        if (cubeMesh != null)
        {
            return cubeMesh;
        }

        cubeMesh = new Mesh { name = "Generated Box Mesh" };
        cubeMesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f),
            new Vector3(0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f)
        };
        cubeMesh.triangles = new[]
        {
            0, 2, 1, 0, 3, 2,
            4, 5, 6, 4, 6, 7,
            0, 1, 5, 0, 5, 4,
            2, 3, 7, 2, 7, 6,
            1, 2, 6, 1, 6, 5,
            3, 0, 4, 3, 4, 7
        };
        cubeMesh.RecalculateNormals();
        cubeMesh.RecalculateBounds();
        return cubeMesh;
    }

    private static Material CreateMaterial(string materialName, Color color)
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

        return material;
    }
}
