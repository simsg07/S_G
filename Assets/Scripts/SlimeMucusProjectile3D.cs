using UnityEngine;

public class SlimeMucusProjectile3D : MonoBehaviour
{
    private readonly Collider[] hits = new Collider[12];
    private BoxCollider boxCollider;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Rigidbody body;
    private Material material;
    private Vector3 projectileSize = new Vector3(0.24f, 0.24f, 0.8f); // 초기화 전 사용할 점액 투사체 기본 크기입니다.
    private Vector3 velocity;
    private float gravity = 3.5f; // 초기화 전 사용할 점액 투사체 기본 중력 세기입니다.
    private float lifetime = 2.5f; // 초기화 전 사용할 점액 투사체 기본 지속 시간입니다.
    private int damage = 1; // 초기화 전 사용할 점액 투사체 기본 피해량입니다.
    private float age;

    public void Initialize(Vector3 direction, float speed, float projectileLifetime, int projectileDamage, Vector3 size, Color color, float gravityStrength)
    {
        projectileSize = MonsterRuntime3D.ClampSize(size, 0.05f);
        velocity = direction.normalized * Mathf.Max(0.1f, speed);
        lifetime = Mathf.Max(0.1f, projectileLifetime);
        damage = Mathf.Max(1, projectileDamage);
        gravity = Mathf.Max(0f, gravityStrength);
        ConfigureProjectile(color);
    }

    private void Awake()
    {
        Color color = new Color(0.5f, 0.95f, 0.25f, 1f);
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
        projectileSize = tuning.mucusProjectileSize;
        damage = tuning.mucusProjectileDamage;
        lifetime = tuning.mucusProjectileLifetime;
        gravity = tuning.mucusProjectileGravity;
        color = tuning.mucusProjectileColor;
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

        velocity += Vector3.down * gravity * Time.deltaTime;
        transform.position += velocity * Time.deltaTime;
        TryHitSomething();
    }

    private void ConfigureProjectile(Color color)
    {
        MonsterRuntime3D.ConfigureKinematicBox(
            gameObject,
            projectileSize,
            color,
            "Generated Slime Mucus Material",
            ref boxCollider,
            ref meshFilter,
            ref meshRenderer,
            ref body,
            ref material
        );

        if (boxCollider != null)
        {
            boxCollider.isTrigger = true;
        }
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
            if (hit == null || hit.GetComponentInParent<SlimeMucusProjectile3D>() == this)
            {
                continue;
            }

            PlayerHealth3D health = hit.GetComponentInParent<PlayerHealth3D>();
            if (health != null)
            {
                health.TakeDamage(damage, transform.position);
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
}
