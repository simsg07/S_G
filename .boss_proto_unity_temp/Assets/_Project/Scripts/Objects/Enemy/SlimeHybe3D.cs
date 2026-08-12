using System.Collections;
using UnityEngine;

[ExecuteAlways]
public class SlimeHybe3D : MonoBehaviour, IAttackable3D
{
    [SerializeField] private Vector3 bodySize = new Vector3(0.95f, 0.62f, 0.8f); // 하이브 슬라임 몸체와 충돌 박스 크기입니다.
    [SerializeField] private int maxHealth = 7; // 하이브 슬라임 최대 체력입니다.
    [SerializeField] private float wanderSpeed = 0.85f; // 플레이어를 감지하지 않았을 때 배회 이동 속도입니다.
    [SerializeField] private float fleeSpeed = 1.65f; // 플레이어에게서 도망갈 때 이동 속도입니다.
    [SerializeField] private float edgeProbeDistance = 0.18f; // 발판 끝을 확인하는 앞쪽 탐지 거리입니다.
    [SerializeField] private float groundProbeDepth = 0.16f; // 앞쪽 바닥 유무를 확인하는 아래쪽 탐지 깊이입니다.
    [SerializeField] private float wallProbeDistance = 0.12f; // 앞쪽 벽을 확인하는 탐지 거리입니다.
    [SerializeField] private float detectionSize = 4.8f; // 패턴을 시작하는 정사각형 인식 범위 크기입니다.
    [SerializeField] private float fleeDuration = 0.85f; // 패턴 시작 전 플레이어 반대 방향으로 도망가는 시간입니다.
    [SerializeField] private float shakeDuration = 0.75f; // 패턴 발동 전 몸을 흔드는 준비 시간입니다.
    [SerializeField] private float patternCooldown = 1.7f; // 패턴 사용 후 다음 패턴까지 기다리는 시간입니다.
    [SerializeField] private int balloonSpawnCount = 2; // 풍선 슬라임 소환 패턴에서 생성할 수입니다.
    [SerializeField] private int mucusProjectileMin = 5; // 점액 흩뿌리기 패턴의 최소 투사체 수입니다.
    [SerializeField] private int mucusProjectileMax = 6; // 점액 흩뿌리기 패턴의 최대 투사체 수입니다.
    [SerializeField] private float mucusProjectileSpeed = 4.2f; // 점액 투사체 기본 발사 속도입니다.
    [SerializeField] private float mucusProjectileLifetime = 2.5f; // 점액 투사체가 자동으로 사라지기까지의 시간입니다.
    [SerializeField] private float mucusProjectileGravity = 3.5f; // 점액 투사체에 적용되는 아래 방향 중력 세기입니다.
    [SerializeField] private Vector3 mucusProjectileSize = new Vector3(0.24f, 0.24f, 0.8f); // 점액 투사체 충돌 박스와 표시 크기입니다.
    [SerializeField] private int contactDamage = 1; // 몸체 또는 점액 투사체가 플레이어에게 주는 피해량입니다.
    [SerializeField] private float contactDamageCooldown = 0.85f; // 몸체 접촉 피해를 다시 줄 수 있을 때까지의 대기 시간입니다.
    [SerializeField] private float respawnDelay = 3f; // 처치된 뒤 다시 나타나는 시간입니다.
    [SerializeField] private bool showRanges = true; // 인식 범위 미리보기를 표시할지 정합니다.
    [SerializeField] private Color bodyColor = new Color(0.58f, 0.72f, 0.18f, 1f); // 평상시 하이브 슬라임 색상입니다.
    [SerializeField] private Color alertColor = new Color(0.78f, 0.88f, 0.25f, 1f); // 도주 또는 패턴 준비 중 하이브 슬라임 색상입니다.
    [SerializeField] private Color detectionColor = new Color(1f, 0.08f, 0.04f, 0.17f); // 인식 범위 미리보기 색상입니다.
    [SerializeField] private Color mucusColor = new Color(0.55f, 0.95f, 0.25f, 1f); // 점액 투사체 색상입니다.

    private readonly Collider[] hits = new Collider[24];
    private BoxCollider slimeCollider;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Rigidbody body;
    private Material bodyMaterial;
    private Material detectionMaterial;
    private Transform detectionVisual;
    private PlatformerPlayer3D player;
    private Coroutine patternRoutine;
    private Vector3 moveDirection = Vector3.right;
    private float nextWanderDecisionTime;
    private float nextPatternTime;
    private float nextContactDamageTime;
    private float shakeFrequencyScale = 1f;
    private int currentHealth;
    private bool wanderingPaused;
    private bool fleeing;
    private bool shaking;
    private bool defeated;

    private void Awake()
    {
        ConfigureSlime();
        ResetHealthIfNeeded();
    }

    private void OnEnable()
    {
        ConfigureSlime();
        ResetHealthIfNeeded();
    }

    private void OnDisable()
    {
        CancelPattern();
    }

    private void Update()
    {
        ConfigureSlime();

        if (!Application.isPlaying || defeated)
        {
            UpdateRangeVisual();
            return;
        }

        UpdatePlayer();
        if (patternRoutine == null)
        {
            if (PlayerInDetectionRange())
            {
                if (Time.time >= nextPatternTime)
                {
                    patternRoutine = StartCoroutine(FleeThenUsePattern());
                }
                else
                {
                    FleeFromPlayer();
                }
            }
            else
            {
                WanderIrregularly();
            }
        }

        TryDamageOnContact();
        UpdateBodyPose();
        UpdateRangeVisual();
    }

    public bool TakeAttack()
    {
        if (!Application.isPlaying || defeated)
        {
            return false;
        }

        currentHealth = Mathf.Max(0, currentHealth - 1);
        if (currentHealth == 0)
        {
            StartCoroutine(RespawnAfterDelay());
        }

        return true;
    }

    private IEnumerator RespawnAfterDelay()
    {
        defeated = true;
        CancelPattern();
        SetVisibleAndSolid(false);

        yield return new WaitForSeconds(respawnDelay);

        currentHealth = Mathf.Max(1, maxHealth);
        defeated = false;
        SetVisibleAndSolid(true);
        nextPatternTime = Time.time + 0.4f;
    }

    private void ConfigureSlime()
    {
        ApplyDatabaseTuning();

        bodySize = MonsterRuntime3D.ClampSize(bodySize, 0.1f);
        maxHealth = Mathf.Max(1, maxHealth);
        wanderSpeed = Mathf.Max(0f, wanderSpeed);
        fleeSpeed = Mathf.Max(wanderSpeed, fleeSpeed);
        edgeProbeDistance = Mathf.Max(0.02f, edgeProbeDistance);
        groundProbeDepth = Mathf.Max(0.02f, groundProbeDepth);
        wallProbeDistance = Mathf.Max(0.02f, wallProbeDistance);
        detectionSize = Mathf.Max(0.1f, detectionSize);
        fleeDuration = Mathf.Max(0.05f, fleeDuration);
        shakeDuration = Mathf.Max(0.05f, shakeDuration);
        patternCooldown = Mathf.Max(0.05f, patternCooldown);
        balloonSpawnCount = Mathf.Max(0, balloonSpawnCount);
        mucusProjectileMin = Mathf.Max(0, mucusProjectileMin);
        mucusProjectileMax = Mathf.Max(mucusProjectileMin, mucusProjectileMax);
        contactDamageCooldown = Mathf.Max(0.05f, contactDamageCooldown);

        MonsterRuntime3D.ConfigureKinematicBox(
            gameObject,
            bodySize,
            fleeing || shaking ? alertColor : bodyColor,
            "Generated Slime Hybe Material",
            ref slimeCollider,
            ref meshFilter,
            ref meshRenderer,
            ref body,
            ref bodyMaterial
        );

        EnsureRangeVisual();
    }

    private void ApplyDatabaseTuning()
    {
        UnitBalanceDatabase3D database = UnitBalanceDatabase3D.Load();
        if (database == null || database.SlimeHybe == null)
        {
            return;
        }

        SlimeHybeBalance3D tuning = database.SlimeHybe;
        bodySize = tuning.bodySize;
        maxHealth = tuning.maxHealth;
        wanderSpeed = tuning.wanderSpeed;
        fleeSpeed = tuning.fleeSpeed;
        edgeProbeDistance = tuning.edgeProbeDistance;
        groundProbeDepth = tuning.groundProbeDepth;
        wallProbeDistance = tuning.wallProbeDistance;
        detectionSize = tuning.detectionSize;
        fleeDuration = tuning.fleeDuration;
        shakeDuration = tuning.shakeDuration;
        patternCooldown = tuning.patternCooldown;
        balloonSpawnCount = tuning.balloonSpawnCount;
        mucusProjectileMin = tuning.mucusProjectileMin;
        mucusProjectileMax = tuning.mucusProjectileMax;
        mucusProjectileSpeed = tuning.mucusProjectileSpeed;
        mucusProjectileLifetime = tuning.mucusProjectileLifetime;
        mucusProjectileGravity = tuning.mucusProjectileGravity;
        mucusProjectileSize = tuning.mucusProjectileSize;
        contactDamage = tuning.contactDamage;
        contactDamageCooldown = tuning.contactDamageCooldown;
        respawnDelay = tuning.respawnDelay;
        showRanges = tuning.showRanges;
        bodyColor = tuning.bodyColor;
        alertColor = tuning.alertColor;
        detectionColor = tuning.detectionColor;
        mucusColor = tuning.mucusColor;
    }

    private void ResetHealthIfNeeded()
    {
        if (currentHealth <= 0 || currentHealth > maxHealth)
        {
            currentHealth = maxHealth;
        }
    }

    private void UpdatePlayer()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<PlatformerPlayer3D>();
        }
    }

    private IEnumerator FleeThenUsePattern()
    {
        fleeing = true;
        float fleeEnd = Time.time + fleeDuration;
        while (Time.time < fleeEnd && !defeated)
        {
            FleeFromPlayer();
            UpdateBodyPose();
            yield return null;
        }

        fleeing = false;

        bool spawnBalloons = Random.value < 0.5f;
        shakeFrequencyScale = spawnBalloons ? 1f : 1.2f;
        shaking = true;

        float shakeEnd = Time.time + shakeDuration;
        while (Time.time < shakeEnd && !defeated)
        {
            UpdateBodyPose();
            yield return null;
        }

        if (!defeated)
        {
            if (spawnBalloons)
            {
                SpawnBalloonSlimes();
            }
            else
            {
                ScatterMucusProjectiles();
            }
        }

        shaking = false;
        shakeFrequencyScale = 1f;
        nextPatternTime = Time.time + patternCooldown;
        patternRoutine = null;
    }

    private void CancelPattern()
    {
        if (patternRoutine != null)
        {
            StopCoroutine(patternRoutine);
            patternRoutine = null;
        }

        fleeing = false;
        shaking = false;
        shakeFrequencyScale = 1f;
    }

    private void WanderIrregularly()
    {
        if (Time.time >= nextWanderDecisionTime)
        {
            wanderingPaused = Random.value < 0.35f;
            moveDirection = Random.value < 0.5f ? Vector3.left : Vector3.right;
            nextWanderDecisionTime = Time.time + Random.Range(0.35f, 1.25f);
        }

        if (wanderingPaused)
        {
            return;
        }

        MoveAlongGround(moveDirection, wanderSpeed);
    }

    private void FleeFromPlayer()
    {
        if (player == null)
        {
            WanderIrregularly();
            return;
        }

        float direction = Mathf.Sign(transform.position.x - player.transform.position.x);
        if (Mathf.Abs(direction) < 0.01f)
        {
            direction = Random.value < 0.5f ? -1f : 1f;
        }

        moveDirection = direction < 0f ? Vector3.left : Vector3.right;
        MoveAlongGround(moveDirection, fleeSpeed);
    }

    private void MoveAlongGround(Vector3 direction, float speed)
    {
        if (ShouldTurnAround(direction))
        {
            moveDirection = direction.x < 0f ? Vector3.right : Vector3.left;
            direction = moveDirection;
        }

        transform.position += direction * speed * Time.deltaTime;
        TwoPointFiveDUtility3D.ClampTransformToPlane(transform);
    }

    private bool ShouldTurnAround(Vector3 direction)
    {
        return !HasGroundAhead(direction) || HasWallAhead(direction);
    }

    private bool HasGroundAhead(Vector3 direction)
    {
        Vector3 center = transform.position
            + direction * (bodySize.x * 0.5f + edgeProbeDistance)
            + Vector3.down * (bodySize.y * 0.5f + groundProbeDepth * 0.5f);
        Vector3 size = new Vector3(edgeProbeDistance * 1.6f, groundProbeDepth, bodySize.z);
        return MonsterRuntime3D.PlatformOverlapsBox(hits, transform, center, size);
    }

    private bool HasWallAhead(Vector3 direction)
    {
        Vector3 center = transform.position + direction * (bodySize.x * 0.5f + wallProbeDistance * 0.5f);
        Vector3 size = new Vector3(wallProbeDistance, bodySize.y * 0.78f, bodySize.z);
        return MonsterRuntime3D.PlatformOverlapsBox(hits, transform, center, size);
    }

    private bool PlayerInDetectionRange()
    {
        return MonsterRuntime3D.PlayerOverlapsVisibleBox(hits, transform, transform.position, new Vector3(detectionSize, detectionSize, bodySize.z), player);
    }

    private void TryDamageOnContact()
    {
        if (Time.time < nextContactDamageTime)
        {
            return;
        }

        if (MonsterRuntime3D.DamagePlayerInBox(hits, transform.position, bodySize, player, contactDamage, transform.position))
        {
            nextContactDamageTime = Time.time + contactDamageCooldown;
        }
    }

    private void SpawnBalloonSlimes()
    {
        for (int i = 0; i < balloonSpawnCount; i++)
        {
            float xOffset = (i - (balloonSpawnCount - 1) * 0.5f) * 0.7f;
            Vector3 spawnPosition = TwoPointFiveDUtility3D.ProjectPositionToPlane(transform.position + new Vector3(xOffset, bodySize.y * 0.9f + 0.5f, 0f));
            GameObject balloon = new GameObject(
                "Balloon Slime",
                typeof(BoxCollider),
                typeof(MeshFilter),
                typeof(MeshRenderer),
                typeof(Rigidbody),
                typeof(BalloonSlime3D)
            );

            balloon.transform.position = spawnPosition;
        }
    }

    private void ScatterMucusProjectiles()
    {
        int count = Random.Range(mucusProjectileMin, mucusProjectileMax + 1);
        float angleStep = 360f / Mathf.Max(1, count);

        for (int i = 0; i < count; i++)
        {
            float angle = (angleStep * i + Random.Range(-18f, 18f)) * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            if (direction.y < -0.2f)
            {
                direction.y *= 0.45f;
            }

            direction.Normalize();
            GameObject projectile = new GameObject(
                "Slime Mucus Projectile",
                typeof(BoxCollider),
                typeof(MeshFilter),
                typeof(MeshRenderer),
                typeof(Rigidbody),
                typeof(SlimeMucusProjectile3D)
            );

            projectile.transform.position = TwoPointFiveDUtility3D.ProjectPositionToPlane(transform.position + direction * (bodySize.x * 0.5f + 0.18f));
            projectile.GetComponent<SlimeMucusProjectile3D>().Initialize(
                direction,
                mucusProjectileSpeed * Random.Range(0.85f, 1.15f),
                mucusProjectileLifetime,
                contactDamage,
                mucusProjectileSize,
                mucusColor,
                mucusProjectileGravity
            );
        }
    }

    private void UpdateBodyPose()
    {
        if (bodyMaterial != null)
        {
            bodyMaterial.color = fleeing || shaking ? alertColor : bodyColor;
        }

        if (!shaking)
        {
            transform.localScale = bodySize;
            return;
        }

        float pulse = 1f + Mathf.Sin(Time.time * 20f * shakeFrequencyScale) * 0.08f;
        transform.localScale = new Vector3(bodySize.x * pulse, bodySize.y * (1f / pulse), bodySize.z);
    }

    private void EnsureRangeVisual()
    {
        if (detectionVisual == null)
        {
            detectionVisual = MonsterRuntime3D.FindOrCreateBoxVisual(transform, "Slime Hybe Detection Range", detectionColor, ref detectionMaterial);
        }
    }

    private void UpdateRangeVisual()
    {
        EnsureRangeVisual();

        if (detectionMaterial != null)
        {
            detectionMaterial.color = detectionColor;
        }

        MonsterRuntime3D.ApplyWorldBoxVisual(transform, detectionVisual, transform.position, new Vector3(detectionSize, detectionSize, bodySize.z), 0.05f);
        MonsterRuntime3D.SetVisualVisible(detectionVisual, showRanges && !defeated);
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

        MonsterRuntime3D.SetVisualVisible(detectionVisual, value && showRanges);
    }
}
