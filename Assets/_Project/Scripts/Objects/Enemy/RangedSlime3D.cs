using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

[ExecuteAlways]
public class RangedSlime3D : MonoBehaviour, IAttackable3D
{
    [SerializeField] private EnemyTuningDatabase3D enemyDatabase; // 감지 범위를 공통 데이터베이스 값으로 덮어쓸 때 연결합니다.
    [SerializeField] private EnemyDetectionProfile3D detectionProfile = EnemyDetectionProfile3D.Ranged; // 이 슬라임이 사용할 데이터베이스 감지 타입입니다.
    [SerializeField] private Vector3 slimeSize = new Vector3(0.7f, 0.5f, 0.8f); // 원거리 슬라임 몸체와 충돌 박스 크기입니다.
    [SerializeField] private float patrolSpeed = 0.85f; // 좌우 순찰 이동 속도입니다.
    [SerializeField] private float edgeProbeDistance = 0.16f; // 발판 끝을 확인하는 앞쪽 탐지 거리입니다.
    [SerializeField] private float groundProbeDepth = 0.16f; // 앞쪽 바닥 유무를 확인하는 아래쪽 탐지 깊이입니다.
    [SerializeField] private float wallProbeDistance = 0.12f; // 앞쪽 벽을 확인하는 탐지 거리입니다.
    [SerializeField, FormerlySerializedAs("detectionRange")] private float detectionSize = 6f; // 데이터베이스가 없을 때 사용할 정사각형 인식 범위 크기입니다.
    [SerializeField] private float attackWindupDuration = 0.55f; // 플레이어를 감지한 뒤 투사체 발사까지 기다리는 선딜 시간입니다.
    [SerializeField] private float attackCooldown = 1.4f; // 발사 후 다음 공격까지 기다리는 시간입니다.
    [SerializeField] private float respawnDelay = 3f; // 플레이어 공격에 맞고 사라진 뒤 다시 나타나는 시간입니다.
    [SerializeField] private int projectileDamage = 1; // 투사체가 플레이어에게 주는 피해량입니다.
    [SerializeField] private float projectileSpeed = 4.8f; // 투사체가 날아가는 속도입니다.
    [SerializeField] private float projectileLifetime = 3f; // 투사체가 자동으로 사라지기까지의 시간입니다.
    [SerializeField] private Vector3 projectileSize = new Vector3(0.28f, 0.28f, 0.8f); // 투사체 충돌 박스와 표시 크기입니다.
    [SerializeField] private bool showRanges = true; // 인식 범위 미리보기를 표시할지 정합니다.
    [SerializeField] private Color slimeColor = new Color(0.15f, 0.75f, 0.95f, 1f); // 평상시 원거리 슬라임 색상입니다.
    [SerializeField] private Color attackPoseColor = new Color(0.1f, 1f, 1f, 1f); // 발사 준비 중 슬라임 몸체 색상입니다.
    [SerializeField] private Color detectionColor = new Color(0.15f, 0.85f, 1f, 0.16f); // 데이터베이스가 없을 때 사용할 인식 범위 색상입니다.
    [SerializeField] private Color projectileColor = new Color(0.1f, 0.95f, 1f, 1f); // 원거리 투사체 색상입니다.

    private static Mesh cubeMesh;

    private readonly Collider[] boxHits = new Collider[16];
    private BoxCollider slimeCollider;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Rigidbody body;
    private Material bodyMaterial;
    private Material detectionMaterial;
    private Transform detectionVisual;
    private PlatformerPlayer3D player;
    private Coroutine attackRoutine;
    private Vector3 facingDirection = Vector3.right;
    private Vector3 attackBasePosition;
    private float nextAttackTime;
    private bool attackWindingUp;
    private bool defeated;

    private void Awake()
    {
        ConfigureSlime();
    }

    private void OnEnable()
    {
        ConfigureSlime();
    }

    private void OnDisable()
    {
        CancelAttackWindup();
    }

    private void Update()
    {
        ConfigureSlime();

        if (!Application.isPlaying || defeated)
        {
            UpdateRangeVisuals();
            return;
        }

        UpdatePlayer();
        if (!attackWindingUp)
        {
            PatrolHorizontally();
        }

        TryStartAttack();
        UpdateAttackPose();
        UpdateRangeVisuals();
    }

    public bool TakeAttack()
    {
        if (!Application.isPlaying || defeated)
        {
            return false;
        }

        StartCoroutine(RespawnAfterDelay());
        return true;
    }

    private IEnumerator RespawnAfterDelay()
    {
        defeated = true;
        CancelAttackWindup();
        SetVisibleAndSolid(false);

        yield return new WaitForSeconds(respawnDelay);

        defeated = false;
        SetVisibleAndSolid(true);
        nextAttackTime = Time.time + 0.25f;
    }

    private void ConfigureSlime()
    {
        ApplyDatabaseTuning();

        slimeSize.x = Mathf.Max(0.1f, slimeSize.x);
        slimeSize.y = Mathf.Max(0.1f, slimeSize.y);
        slimeSize.z = Mathf.Max(0.1f, slimeSize.z);
        patrolSpeed = Mathf.Max(0f, patrolSpeed);
        edgeProbeDistance = Mathf.Max(0.02f, edgeProbeDistance);
        groundProbeDepth = Mathf.Max(0.02f, groundProbeDepth);
        wallProbeDistance = Mathf.Max(0.02f, wallProbeDistance);
        detectionSize = Mathf.Max(0.1f, detectionSize);
        attackWindupDuration = Mathf.Max(0.01f, attackWindupDuration);
        projectileSpeed = Mathf.Max(0.1f, projectileSpeed);
        projectileLifetime = Mathf.Max(0.1f, projectileLifetime);

        slimeCollider = GetComponent<BoxCollider>();
        if (slimeCollider == null)
        {
            slimeCollider = gameObject.AddComponent<BoxCollider>();
        }

        meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
        }

        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
        }

        body = GetComponent<Rigidbody>();
        if (body == null)
        {
            body = gameObject.AddComponent<Rigidbody>();
        }

        body.isKinematic = true;
        body.useGravity = false;
        TwoPointFiveDUtility3D.ConfigureRigidbodyForSideView(body);

        slimeCollider.size = Vector3.one;
        slimeCollider.center = Vector3.zero;
        slimeCollider.isTrigger = false;
        meshFilter.sharedMesh = GetCubeMesh();

        if (bodyMaterial == null)
        {
            bodyMaterial = CreateMaterial("Generated Ranged Slime Material", slimeColor, false);
        }

        meshRenderer.sharedMaterial = bodyMaterial;
        EnsureRangeVisual();

        if (!attackWindingUp)
        {
            transform.localScale = slimeSize;
            bodyMaterial.color = slimeColor;
        }
    }

    private void ApplyDatabaseTuning()
    {
        UnitBalanceDatabase3D database = UnitBalanceDatabase3D.Load();
        if (database == null || database.RangedSlime == null)
        {
            return;
        }

        RangedSlimeBalance3D tuning = database.RangedSlime;
        slimeSize = tuning.slimeSize;
        patrolSpeed = tuning.patrolSpeed;
        edgeProbeDistance = tuning.edgeProbeDistance;
        groundProbeDepth = tuning.groundProbeDepth;
        wallProbeDistance = tuning.wallProbeDistance;
        detectionSize = tuning.detectionSize;
        attackWindupDuration = tuning.attackWindupDuration;
        attackCooldown = tuning.attackCooldown;
        respawnDelay = tuning.respawnDelay;
        projectileDamage = tuning.projectileDamage;
        projectileSpeed = tuning.projectileSpeed;
        projectileLifetime = tuning.projectileLifetime;
        projectileSize = tuning.projectileSize;
        showRanges = tuning.showRanges;
        slimeColor = tuning.slimeColor;
        attackPoseColor = tuning.attackPoseColor;
        detectionColor = tuning.detectionColor;
        projectileColor = tuning.projectileColor;
    }

    private void UpdatePlayer()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<PlatformerPlayer3D>();
        }
    }

    private void TryStartAttack()
    {
        if (player == null || attackWindingUp || Time.time < nextAttackTime)
        {
            return;
        }

        if (!PlayerOverlapsDetectionSquare())
        {
            return;
        }

        facingDirection = player.transform.position.x < transform.position.x ? Vector3.left : Vector3.right;
        attackBasePosition = transform.position;
        attackRoutine = StartCoroutine(FireAfterWindup());
    }

    private IEnumerator FireAfterWindup()
    {
        attackWindingUp = true;
        UpdateRangeVisuals();

        yield return new WaitForSeconds(attackWindupDuration);

        if (!defeated && player != null && PlayerOverlapsDetectionSquare())
        {
            FireProjectileAtPlayer();
        }

        attackWindingUp = false;
        attackRoutine = null;
        transform.position = attackBasePosition;
        transform.localScale = slimeSize;
        bodyMaterial.color = slimeColor;
        nextAttackTime = Time.time + attackCooldown;
    }

    private void FireProjectileAtPlayer()
    {
        Vector3 direction = player.transform.position - transform.position;
        direction.z = 0f;
        if (direction.sqrMagnitude < 0.01f)
        {
            direction = facingDirection;
        }

        direction.Normalize();
        Vector3 spawnPosition = transform.position + direction * (slimeSize.x * 0.5f + projectileSize.x * 0.5f + 0.08f);
        spawnPosition = TwoPointFiveDUtility3D.ProjectPositionToPlane(spawnPosition);
        GameObject projectileObject = new GameObject("Ranged Slime Projectile", typeof(BoxCollider), typeof(MeshFilter), typeof(MeshRenderer), typeof(Rigidbody), typeof(RangedSlimeProjectile3D));
        projectileObject.transform.position = spawnPosition;
        projectileObject.GetComponent<RangedSlimeProjectile3D>().Initialize(direction, projectileSpeed, projectileLifetime, projectileDamage, projectileSize, projectileColor);
    }

    private void CancelAttackWindup()
    {
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        if (attackWindingUp)
        {
            transform.position = attackBasePosition;
        }

        attackWindingUp = false;
        transform.localScale = slimeSize;

        if (bodyMaterial != null)
        {
            bodyMaterial.color = slimeColor;
        }
    }

    private void PatrolHorizontally()
    {
        if (ShouldTurnAround())
        {
            ReverseDirection();
        }

        transform.position += facingDirection * patrolSpeed * Time.deltaTime;
        TwoPointFiveDUtility3D.ClampTransformToPlane(transform);
    }

    private bool ShouldTurnAround()
    {
        return !HasGroundAhead() || HasWallAhead();
    }

    private void ReverseDirection()
    {
        facingDirection = facingDirection.x < 0f ? Vector3.right : Vector3.left;
    }

    private bool HasGroundAhead()
    {
        Vector3 center = transform.position
            + facingDirection * (slimeSize.x * 0.5f + edgeProbeDistance)
            + Vector3.down * (slimeSize.y * 0.5f + groundProbeDepth * 0.5f);
        Vector3 size = new Vector3(edgeProbeDistance * 1.6f, groundProbeDepth, slimeSize.z);
        return PlatformSurfaceOverlapsBox(center, size);
    }

    private bool HasWallAhead()
    {
        Vector3 center = transform.position + facingDirection * (slimeSize.x * 0.5f + wallProbeDistance * 0.5f);
        Vector3 size = new Vector3(wallProbeDistance, slimeSize.y * 0.78f, slimeSize.z);
        return PlatformSurfaceOverlapsBox(center, size);
    }

    private bool PlatformSurfaceOverlapsBox(Vector3 center, Vector3 size)
    {
        int hitCount = Physics.OverlapBoxNonAlloc(center, size * 0.5f, boxHits, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = boxHits[i];
            if (hit == null || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            if (hit.GetComponentInParent<PlatformSurface3D>() != null)
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateAttackPose()
    {
        if (!attackWindingUp)
        {
            return;
        }

        float pulse = 1f + Mathf.Sin(Time.time * 22f) * 0.04f;
        transform.localScale = new Vector3(slimeSize.x * 0.9f, slimeSize.y * 1.18f * pulse, slimeSize.z);
        transform.position = attackBasePosition - facingDirection * 0.06f;
        TwoPointFiveDUtility3D.ClampTransformToPlane(transform);
        bodyMaterial.color = attackPoseColor;
    }

    private bool PlayerOverlapsDetectionSquare()
    {
        float activeDetectionSize = GetActiveDetectionSize();
        return MonsterRuntime3D.PlayerOverlapsVisibleBox(boxHits, transform, transform.position, new Vector3(activeDetectionSize, activeDetectionSize, slimeSize.z), player);
    }

    private void EnsureRangeVisual()
    {
        if (detectionVisual == null)
        {
            detectionVisual = FindOrCreateRangeVisual("Ranged Slime Detection Square", GetActiveDetectionColor(), ref detectionMaterial);
        }
    }

    private Transform FindOrCreateRangeVisual(string visualName, Color color, ref Material material)
    {
        Transform existing = transform.Find(visualName);
        if (existing != null)
        {
            ConfigureRangeVisual(existing.gameObject, visualName, color, ref material);
            return existing;
        }

        GameObject visualObject = new GameObject(visualName, typeof(MeshFilter), typeof(MeshRenderer));
        visualObject.transform.SetParent(transform, false);
        ConfigureRangeVisual(visualObject, visualName, color, ref material);
        return visualObject.transform;
    }

    private void ConfigureRangeVisual(GameObject visualObject, string visualName, Color color, ref Material material)
    {
        visualObject.hideFlags = HideFlags.HideAndDontSave;

        MeshFilter rangeMeshFilter = visualObject.GetComponent<MeshFilter>();
        if (rangeMeshFilter == null)
        {
            rangeMeshFilter = visualObject.AddComponent<MeshFilter>();
        }

        rangeMeshFilter.sharedMesh = GetCubeMesh();

        if (material == null)
        {
            material = CreateMaterial($"Generated {visualName} Material", color, true);
        }

        material.color = color;

        MeshRenderer renderer = visualObject.GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            renderer = visualObject.AddComponent<MeshRenderer>();
        }

        renderer.sharedMaterial = material;
        renderer.enabled = showRanges;
    }

    private void UpdateRangeVisuals()
    {
        if (detectionVisual == null)
        {
            return;
        }

        float activeDetectionSize = GetActiveDetectionSize();
        if (detectionMaterial != null)
        {
            detectionMaterial.color = GetActiveDetectionColor();
        }

        Vector3 parentScale = transform.lossyScale;
        detectionVisual.localPosition = Vector3.zero;
        detectionVisual.localRotation = Quaternion.identity;
        detectionVisual.localScale = new Vector3(
            activeDetectionSize / Mathf.Max(0.001f, parentScale.x),
            activeDetectionSize / Mathf.Max(0.001f, parentScale.y),
            0.05f / Mathf.Max(0.001f, parentScale.z)
        );
        detectionVisual.GetComponent<MeshRenderer>().enabled = showRanges && !defeated;
    }

    private void SetVisibleAndSolid(bool value)
    {
        if (meshRenderer != null)
        {
            meshRenderer.enabled = value;
        }

        if (slimeCollider != null)
        {
            slimeCollider.enabled = value;
        }

        if (detectionVisual != null)
        {
            detectionVisual.GetComponent<MeshRenderer>().enabled = value && showRanges;
        }
    }

    private float GetActiveDetectionSize()
    {
        if (UnitBalanceDatabase3D.Load() != null)
        {
            return Mathf.Max(0.1f, detectionSize);
        }

        return enemyDatabase != null
            ? enemyDatabase.GetDetectionSize(detectionProfile, detectionSize)
            : Mathf.Max(0.1f, detectionSize);
    }

    private Color GetActiveDetectionColor()
    {
        if (UnitBalanceDatabase3D.Load() != null)
        {
            return detectionColor;
        }

        return enemyDatabase != null
            ? enemyDatabase.GetDetectionColor(detectionProfile, detectionColor)
            : detectionColor;
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

    private static Material CreateMaterial(string materialName, Color color, bool transparent)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
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

        if (transparent)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        return material;
    }
}
