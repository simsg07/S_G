using UnityEngine;

public class RangedSlimeProjectile3D : MonoBehaviour
{
    private static Mesh cubeMesh;

    private readonly Collider[] hits = new Collider[12];
    private Vector3 velocity;
    private Vector3 projectileSize = new Vector3(0.28f, 0.28f, 0.8f); // 초기화 전 사용할 투사체 기본 크기입니다.
    private int damage = 1; // 초기화 전 사용할 투사체 기본 피해량입니다.
    private float lifetime = 3f; // 초기화 전 사용할 투사체 기본 지속 시간입니다.
    private float age;

    public void Initialize(Vector3 direction, float speed, float projectileLifetime, int projectileDamage, Vector3 size, Color color)
    {
        projectileSize = new Vector3(Mathf.Max(0.05f, size.x), Mathf.Max(0.05f, size.y), Mathf.Max(0.05f, size.z));
        velocity = TwoPointFiveDUtility3D.ProjectVelocityToPlane(direction.normalized * Mathf.Max(0.1f, speed));
        lifetime = Mathf.Max(0.1f, projectileLifetime);
        damage = Mathf.Max(1, projectileDamage);
        ConfigureProjectile(color);
    }

    private void Awake()
    {
        Color color = new Color(0.1f, 0.95f, 1f, 1f);
        ApplyDatabaseDefaults(ref color);
        ConfigureProjectile(color);
    }

    private void ApplyDatabaseDefaults(ref Color color)
    {
        UnitBalanceDatabase3D database = UnitBalanceDatabase3D.Load();
        if (database == null || database.Projectiles == null)
        {
            return;
        }

        ProjectileBalance3D tuning = database.Projectiles;
        projectileSize = tuning.rangedProjectileSize;
        damage = tuning.rangedProjectileDamage;
        lifetime = tuning.rangedProjectileLifetime;
        color = tuning.rangedProjectileColor;
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        age += Time.deltaTime;
        if (age >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        transform.position += velocity * Time.deltaTime;
        TwoPointFiveDUtility3D.ClampTransformToPlane(transform);
        TryHitSomething();
    }

    private void ConfigureProjectile(Color color)
    {
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

        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null)
        {
            body = gameObject.AddComponent<Rigidbody>();
        }

        body.isKinematic = true;
        body.useGravity = false;
        TwoPointFiveDUtility3D.ConfigureRigidbodyForSideView(body);

        boxCollider.size = Vector3.one;
        boxCollider.center = Vector3.zero;
        boxCollider.isTrigger = true;
        meshFilter.sharedMesh = GetCubeMesh();
        meshRenderer.sharedMaterial = CreateMaterial("Generated Ranged Slime Projectile Material", color);
        transform.localScale = projectileSize;
    }

    private void TryHitSomething()
    {
        int hitCount = Physics.OverlapBoxNonAlloc(
            transform.position,
            projectileSize * 0.5f,
            hits,
            Quaternion.identity,
            ~0,
            QueryTriggerInteraction.Collide
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hits[i];
            if (hit == null || hit.GetComponentInParent<RangedSlimeProjectile3D>() == this)
            {
                continue;
            }

            if (hit.GetComponentInParent<PlatformerPlayer3D>() != null)
            {
                // Player damage is removed. Projectiles simply disappear on player contact.
                Destroy(gameObject);
                return;
            }

            if (hit.GetComponentInParent<PlatformSurface3D>() != null)
            {
                Destroy(gameObject);
                return;
            }
        }
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
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        return new Material(shader)
        {
            name = materialName,
            color = color,
            hideFlags = HideFlags.HideAndDontSave
        };
    }
}
