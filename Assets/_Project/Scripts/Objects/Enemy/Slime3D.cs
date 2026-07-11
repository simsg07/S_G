using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

[ExecuteAlways]
public class Slime3D : MonoBehaviour, IAttackable3D
{
    [SerializeField, FormerlySerializedAs("slimeSize")] private Vector3 bodySize = new Vector3(0.75f, 0.55f, 0.8f); // 기본 추적 슬라임 몸체와 충돌 박스 크기입니다.
    [SerializeField] private int maxHealth = 4; // 기본 추적 슬라임 최대 체력입니다.
    [SerializeField] private float patrolSpeed = 1.15f; // 플레이어를 추적하지 않을 때 순찰 이동 속도입니다.
    [SerializeField] private float chaseSpeed = 2.1f; // 플레이어를 추적할 때 이동 속도입니다.
    [SerializeField] private float edgeProbeDistance = 0.16f; // 발판 끝을 확인하는 앞쪽 탐지 거리입니다.
    [SerializeField] private float groundProbeDepth = 0.16f; // 앞쪽 바닥 유무를 확인하는 아래쪽 탐지 깊이입니다.
    [SerializeField] private float wallProbeDistance = 0.12f; // 앞쪽 벽을 확인하는 탐지 거리입니다.
    [SerializeField, FormerlySerializedAs("detectionSize")] private float detectionSize = 4.2f; // 추적을 시작하는 정사각형 인식 범위 크기입니다.
    [SerializeField] private float chaseSize = 6.2f; // 이미 추적 중일 때 추적을 유지하는 정사각형 범위 크기입니다.
    [SerializeField] private int contactDamage = 1; // 접촉 시 플레이어에게 주는 피해량입니다.
    [SerializeField] private float contactDamageCooldown = 0.8f; // 접촉 피해를 다시 줄 수 있을 때까지의 대기 시간입니다.
    [SerializeField] private float respawnDelay = 3f; // 처치된 뒤 다시 나타나는 시간입니다.
    [SerializeField] private bool showRanges = true; // 인식 범위와 추적 유지 범위 미리보기를 표시할지 정합니다.
    [SerializeField] private Color slimeColor = new Color(0.32f, 0.82f, 0.28f, 1f); // 평상시 기본 추적 슬라임 색상입니다.
    [SerializeField] private Color chaseColor = new Color(0.6f, 0.96f, 0.35f, 1f); // 추적 중 기본 추적 슬라임 색상입니다.
    [SerializeField] private Color detectionColor = new Color(1f, 0.08f, 0.04f, 0.18f); // 추적 시작 범위 미리보기 색상입니다.
    [SerializeField] private Color chaseRangeColor = new Color(1f, 0.15f, 0.78f, 0.13f); // 추적 유지 범위 미리보기 색상입니다.

    private readonly Collider[] hits = new Collider[24];
    private BoxCollider slimeCollider;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Rigidbody body;
    private Material bodyMaterial;
    private Material detectionMaterial;
    private Material chaseMaterial;
    private Transform detectionVisual;
    private Transform chaseVisual;
    private PlatformerPlayer3D player;
    private Vector3 facingDirection = Vector3.right;
    private int currentHealth;
    private float nextContactDamageTime;
    private bool chasing;
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

    private void Update()
    {
        ConfigureSlime();

        if (!Application.isPlaying || defeated)
        {
            UpdateRangeVisuals();
            return;
        }

        UpdatePlayer();
        UpdateChaseState();
        Move();
        TryDamageOnContact();
        UpdateBodyPose();
        UpdateRangeVisuals();
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
        chasing = false;
        SetVisibleAndSolid(false);

        yield return new WaitForSeconds(respawnDelay);

        currentHealth = Mathf.Max(1, maxHealth);
        defeated = false;
        SetVisibleAndSolid(true);
    }

    private void ConfigureSlime()
    {
        ApplyDatabaseTuning();

        bodySize = MonsterRuntime3D.ClampSize(bodySize, 0.1f);
        maxHealth = Mathf.Max(1, maxHealth);
        patrolSpeed = Mathf.Max(0f, patrolSpeed);
        chaseSpeed = Mathf.Max(patrolSpeed, chaseSpeed);
        edgeProbeDistance = Mathf.Max(0.02f, edgeProbeDistance);
        groundProbeDepth = Mathf.Max(0.02f, groundProbeDepth);
        wallProbeDistance = Mathf.Max(0.02f, wallProbeDistance);
        detectionSize = Mathf.Max(0.1f, detectionSize);
        chaseSize = Mathf.Max(detectionSize, chaseSize);
        contactDamageCooldown = Mathf.Max(0.05f, contactDamageCooldown);

        MonsterRuntime3D.ConfigureKinematicBox(
            gameObject,
            bodySize,
            chasing ? chaseColor : slimeColor,
            "Generated Slime Material",
            ref slimeCollider,
            ref meshFilter,
            ref meshRenderer,
            ref body,
            ref bodyMaterial
        );

        EnsureRangeVisuals();
    }

    private void ApplyDatabaseTuning()
    {
        UnitBalanceDatabase3D database = UnitBalanceDatabase3D.Load();
        if (database == null || database.Slime == null)
        {
            return;
        }

        SlimeBalance3D tuning = database.Slime;
        bodySize = tuning.bodySize;
        maxHealth = tuning.maxHealth;
        patrolSpeed = tuning.patrolSpeed;
        chaseSpeed = tuning.chaseSpeed;
        edgeProbeDistance = tuning.edgeProbeDistance;
        groundProbeDepth = tuning.groundProbeDepth;
        wallProbeDistance = tuning.wallProbeDistance;
        detectionSize = tuning.detectionSize;
        chaseSize = tuning.chaseSize;
        contactDamage = tuning.contactDamage;
        contactDamageCooldown = tuning.contactDamageCooldown;
        respawnDelay = tuning.respawnDelay;
        showRanges = tuning.showRanges;
        slimeColor = tuning.slimeColor;
        chaseColor = tuning.chaseColor;
        detectionColor = tuning.detectionColor;
        chaseRangeColor = tuning.chaseRangeColor;
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

    private void UpdateChaseState()
    {
        if (player == null)
        {
            chasing = false;
            return;
        }

        if (chasing)
        {
            chasing = PlayerOverlapsCenteredSquare(chaseSize);
            return;
        }

        chasing = PlayerOverlapsCenteredSquare(detectionSize);
    }

    private void Move()
    {
        if (chasing && player != null)
        {
            float direction = Mathf.Sign(player.transform.position.x - transform.position.x);
            if (Mathf.Abs(direction) > 0.01f)
            {
                facingDirection = direction < 0f ? Vector3.left : Vector3.right;
            }

            if (!ShouldTurnAround())
            {
                transform.position += facingDirection * chaseSpeed * Time.deltaTime;
            }

            TwoPointFiveDUtility3D.ClampTransformToPlane(transform);
            return;
        }

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
            + facingDirection * (bodySize.x * 0.5f + edgeProbeDistance)
            + Vector3.down * (bodySize.y * 0.5f + groundProbeDepth * 0.5f);
        Vector3 size = new Vector3(edgeProbeDistance * 1.6f, groundProbeDepth, bodySize.z);
        return MonsterRuntime3D.PlatformOverlapsBox(hits, transform, center, size);
    }

    private bool HasWallAhead()
    {
        Vector3 center = transform.position + facingDirection * (bodySize.x * 0.5f + wallProbeDistance * 0.5f);
        Vector3 size = new Vector3(wallProbeDistance, bodySize.y * 0.78f, bodySize.z);
        return MonsterRuntime3D.PlatformOverlapsBox(hits, transform, center, size);
    }

    private bool PlayerOverlapsCenteredSquare(float size)
    {
        return MonsterRuntime3D.PlayerOverlapsVisibleBox(hits, transform, transform.position, new Vector3(size, size, bodySize.z), player);
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

    private void UpdateBodyPose()
    {
        if (bodyMaterial != null)
        {
            bodyMaterial.color = chasing ? chaseColor : slimeColor;
        }
    }

    private void EnsureRangeVisuals()
    {
        if (detectionVisual == null)
        {
            detectionVisual = MonsterRuntime3D.FindOrCreateBoxVisual(transform, "Slime Detection Range", detectionColor, ref detectionMaterial);
        }

        if (chaseVisual == null)
        {
            chaseVisual = MonsterRuntime3D.FindOrCreateBoxVisual(transform, "Slime Chase Range", chaseRangeColor, ref chaseMaterial);
        }
    }

    private void UpdateRangeVisuals()
    {
        EnsureRangeVisuals();

        if (detectionMaterial != null)
        {
            detectionMaterial.color = detectionColor;
        }

        if (chaseMaterial != null)
        {
            chaseMaterial.color = chaseRangeColor;
        }

        MonsterRuntime3D.ApplyWorldBoxVisual(transform, detectionVisual, transform.position, new Vector3(detectionSize, detectionSize, bodySize.z), 0.05f);
        MonsterRuntime3D.ApplyWorldBoxVisual(transform, chaseVisual, transform.position, new Vector3(chaseSize, chaseSize, bodySize.z), 0.04f);

        bool visible = showRanges && !defeated;
        MonsterRuntime3D.SetVisualVisible(detectionVisual, visible);
        MonsterRuntime3D.SetVisualVisible(chaseVisual, visible && chasing);
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
        MonsterRuntime3D.SetVisualVisible(chaseVisual, value && showRanges && chasing);
    }
}
