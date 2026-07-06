using UnityEngine;

[ExecuteAlways]
public class DamageBlock3D : MonoBehaviour
{
    [SerializeField] private Vector3 blockSize = new Vector3(0.85f, 0.85f, 1f); // 피격 판정 블럭의 크기입니다.
    [SerializeField] private int damage = 1; // 블럭에 닿았을 때 플레이어에게 주는 피해량입니다.
    [SerializeField] private Color blockColor = new Color(0.95f, 0.15f, 0.1f, 1f); // 피격 판정 블럭 색상입니다.

    private static Mesh cubeMesh;
    private Material visualMaterial;

    private void Awake()
    {
        ConfigureBlock();
    }

    private void OnEnable()
    {
        ConfigureBlock();
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryDamage(collision.collider);
    }

    private void OnCollisionStay(Collision collision)
    {
        TryDamage(collision.collider);
    }

    private void ConfigureBlock()
    {
        blockSize.x = Mathf.Max(0.1f, blockSize.x);
        blockSize.y = Mathf.Max(0.1f, blockSize.y);
        blockSize.z = Mathf.Max(0.1f, blockSize.z);

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

        meshFilter.sharedMesh = GetCubeMesh();

        if (visualMaterial == null)
        {
            visualMaterial = CreateMaterial("Generated Damage Block Material", blockColor);
        }

        visualMaterial.color = blockColor;
        meshRenderer.sharedMaterial = visualMaterial;

        transform.localScale = blockSize;
    }

    private void TryDamage(Collider other)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        PlayerHealth3D health = other.GetComponentInParent<PlayerHealth3D>();
        if (health != null)
        {
            ApplyDamageTo(health);
        }
    }

    public void ApplyDamageTo(PlayerHealth3D health)
    {
        if (!Application.isPlaying || health == null)
        {
            return;
        }

        health.TakeDamage(damage, transform.position);
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
