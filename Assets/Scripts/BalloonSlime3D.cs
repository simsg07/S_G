using System.Collections;
using UnityEngine;

[ExecuteAlways]
public class BalloonSlime3D : MonoBehaviour, IAttackable3D
{
    [SerializeField] private Vector3 bodySize = new Vector3(0.62f, 0.62f, 0.8f); // 풍선 슬라임 몸체와 충돌 박스 크기입니다.
    [SerializeField] private int maxHealth = 1; // 풍선 슬라임 최대 체력입니다.
    [SerializeField] private float detectionSize = 5f; // 돌진을 시작하는 정사각형 인식 범위 크기입니다.
    [SerializeField] private float hoverSpeed = 0.35f; // 평상시 좌우로 떠다니는 이동 속도입니다.
    [SerializeField] private float hoverFrequency = 2.1f; // 위아래 둥실거림의 반복 속도입니다.
    [SerializeField] private float hoverBobStrength = 0.12f; // 위아래 둥실거림의 세기입니다.
    [SerializeField] private float chargeDuration = 1.2f; // 플레이어를 감지한 뒤 돌진 전 충전하는 시간입니다.
    [SerializeField] private float dashSpeed = 7.2f; // 돌진 공격 이동 속도입니다.
    [SerializeField] private float dashDuration = 0.46f; // 돌진 공격이 유지되는 시간입니다.
    [SerializeField] private float attackCooldown = 0.6f; // 돌진 후 다음 돌진까지 기다리는 시간입니다.
    [SerializeField] private int contactDamage = 1; // 돌진 중 플레이어에게 주는 피해량입니다.
    [SerializeField] private float respawnDelay = 3f; // 처치된 뒤 다시 나타나는 시간입니다.
    [SerializeField] private bool showRanges = true; // 인식 범위 미리보기를 표시할지 정합니다.
    [SerializeField] private Color bodyColor = new Color(0.95f, 0.82f, 0.2f, 1f); // 평상시 풍선 슬라임 색상입니다.
    [SerializeField] private Color chargeColor = new Color(1f, 0.45f, 0.18f, 1f); // 충전 또는 돌진 중 풍선 슬라임 색상입니다.
    [SerializeField] private Color detectionColor = new Color(1f, 0.08f, 0.04f, 0.16f); // 인식 범위 미리보기 색상입니다.

    private readonly Collider[] hits = new Collider[24];
    private BoxCollider slimeCollider;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Rigidbody body;
    private Material bodyMaterial;
    private Material detectionMaterial;
    private Transform detectionVisual;
    private PlatformerPlayer3D player;
    private Coroutine attackRoutine;
    private Vector3 driftDirection = Vector3.right;
    private float hoverPhase;
    private float nextDriftChangeTime;
    private int currentHealth;
    private bool charging;
    private bool dashing;
    private bool defeated;

    private void Awake()
    {
        hoverPhase = Random.value * Mathf.PI * 2f;
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
        CancelAttack();
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
        if (attackRoutine == null)
        {
            UpdateHover();
            TryStartCharge();
        }

        UpdateBodyPose();
        TryDamageOnContact();
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
        CancelAttack();
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
        detectionSize = Mathf.Max(0.1f, detectionSize);
        chargeDuration = Mathf.Max(0.05f, chargeDuration);
        dashSpeed = Mathf.Max(0.1f, dashSpeed);
        dashDuration = Mathf.Max(0.05f, dashDuration);
        attackCooldown = Mathf.Max(0f, attackCooldown);

        MonsterRuntime3D.ConfigureKinematicBox(
            gameObject,
            bodySize,
            charging || dashing ? chargeColor : bodyColor,
            "Generated Balloon Slime Material",
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
        if (database == null || database.BalloonSlime == null)
        {
            return;
        }

        BalloonSlimeBalance3D tuning = database.BalloonSlime;
        bodySize = tuning.bodySize;
        maxHealth = tuning.maxHealth;
        detectionSize = tuning.detectionSize;
        hoverSpeed = tuning.hoverSpeed;
        hoverFrequency = tuning.hoverFrequency;
        hoverBobStrength = tuning.hoverBobStrength;
        chargeDuration = tuning.chargeDuration;
        dashSpeed = tuning.dashSpeed;
        dashDuration = tuning.dashDuration;
        attackCooldown = tuning.attackCooldown;
        contactDamage = tuning.contactDamage;
        respawnDelay = tuning.respawnDelay;
        showRanges = tuning.showRanges;
        bodyColor = tuning.bodyColor;
        chargeColor = tuning.chargeColor;
        detectionColor = tuning.detectionColor;
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

    private void UpdateHover()
    {
        if (Time.time >= nextDriftChangeTime)
        {
            driftDirection = Random.value < 0.5f ? Vector3.left : Vector3.right;
            nextDriftChangeTime = Time.time + Random.Range(1.2f, 2.8f);
        }

        float bobVelocity = Mathf.Cos(Time.time * hoverFrequency + hoverPhase) * hoverBobStrength * hoverFrequency;
        transform.position += (driftDirection * hoverSpeed + Vector3.up * bobVelocity) * Time.deltaTime;
    }

    private void TryStartCharge()
    {
        if (player == null || !MonsterRuntime3D.PlayerOverlapsVisibleBox(hits, transform, transform.position, new Vector3(detectionSize, detectionSize, bodySize.z), player))
        {
            return;
        }

        attackRoutine = StartCoroutine(ChargeAndDash());
    }

    private IEnumerator ChargeAndDash()
    {
        charging = true;
        float chargeEnd = Time.time + chargeDuration;
        while (Time.time < chargeEnd)
        {
            UpdateBodyPose();
            yield return null;
        }

        charging = false;
        dashing = true;

        Vector3 direction = player != null ? player.transform.position - transform.position : driftDirection;
        direction.z = 0f;
        if (direction.sqrMagnitude < 0.01f)
        {
            direction = driftDirection;
        }
        direction.Normalize();

        float dashEnd = Time.time + dashDuration;
        while (Time.time < dashEnd)
        {
            transform.position += direction * dashSpeed * Time.deltaTime;
            if (MonsterRuntime3D.DamagePlayerInBox(hits, transform.position, bodySize, player, contactDamage, transform.position))
            {
                break;
            }

            yield return null;
        }

        dashing = false;
        yield return new WaitForSeconds(attackCooldown);
        attackRoutine = null;
    }

    private void CancelAttack()
    {
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        charging = false;
        dashing = false;
    }

    private void TryDamageOnContact()
    {
        if (!dashing)
        {
            return;
        }

        MonsterRuntime3D.DamagePlayerInBox(hits, transform.position, bodySize, player, contactDamage, transform.position);
    }

    private void UpdateBodyPose()
    {
        if (bodyMaterial != null)
        {
            bodyMaterial.color = charging || dashing ? chargeColor : bodyColor;
        }

        if (!charging)
        {
            transform.localScale = bodySize;
            return;
        }

        float pulse = 1f + Mathf.Sin(Time.time * 18f) * 0.08f;
        transform.localScale = bodySize * pulse;
    }

    private void EnsureRangeVisual()
    {
        if (detectionVisual == null)
        {
            detectionVisual = MonsterRuntime3D.FindOrCreateBoxVisual(transform, "Balloon Slime Detection Range", detectionColor, ref detectionMaterial);
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
