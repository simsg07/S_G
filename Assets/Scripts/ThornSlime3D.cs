using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

[ExecuteAlways]
public class ThornSlime3D : MonoBehaviour, IAttackable3D
{
    [SerializeField, FormerlySerializedAs("tileCollider")] private BoxCollider tileCollider; // 타고 움직일 타일의 콜라이더입니다. 있으면 위치와 크기를 자동으로 사용합니다.
    [SerializeField, FormerlySerializedAs("tileTarget")] private Transform tileTarget; // 콜라이더가 없을 때 타일 중심으로 사용할 대상입니다.
    [SerializeField, FormerlySerializedAs("tileCenter")] private Vector3 tileCenter = new Vector3(4.5f, -1.75f, 0f); // 타일 대상이 없을 때 사용할 수동 타일 중심 좌표입니다.
    [SerializeField, FormerlySerializedAs("tileSize")] private Vector3 tileSize = new Vector3(1.25f, 1.25f, 1f); // 타일 대상이 없을 때 사용할 수동 타일 크기입니다.
    [SerializeField, FormerlySerializedAs("slimeSize")] private Vector3 bodySize = new Vector3(0.58f, 0.38f, 0.8f); // 가시 슬라임 몸체와 충돌 박스 크기입니다.
    [SerializeField] private int maxHealth = 3; // 가시 슬라임 최대 체력입니다.
    [SerializeField] private float crawlSpeed = 1.35f; // 타일 둘레를 따라 이동하는 속도입니다.
    [SerializeField] private Vector2 detectionBoxSize = new Vector2(1.35f, 0.75f); // 가시 공격을 준비하기 전 플레이어를 감지하는 박스 크기입니다.
    [SerializeField] private float detectionOffset = 0.58f; // 감지 박스가 몸체 바깥으로 뻗는 거리입니다.
    [SerializeField] private Vector2 spikeBoxSize = new Vector2(1.1f, 0.58f); // 실제 가시 공격 판정 박스 크기입니다.
    [SerializeField] private float spikeOffset = 0.62f; // 가시 공격 판정이 몸체 바깥으로 뻗는 거리입니다.
    [SerializeField] private float attackWindupDuration = 0.28f; // 감지 후 가시 공격이 나오기 전 선딜 시간입니다.
    [SerializeField] private float attackActiveDuration = 0.22f; // 가시 공격 판정이 유지되는 시간입니다.
    [SerializeField] private float attackCooldown = 1f; // 가시 공격 후 다음 공격까지 기다리는 시간입니다.
    [SerializeField] private int contactDamage = 1; // 몸체 접촉 시 플레이어에게 주는 피해량입니다.
    [SerializeField] private int spikeDamage = 1; // 가시 공격 적중 시 플레이어에게 주는 피해량입니다.
    [SerializeField] private float contactDamageCooldown = 0.8f; // 몸체 접촉 피해를 다시 줄 수 있을 때까지의 대기 시간입니다.
    [SerializeField] private float respawnDelay = 3f; // 처치된 뒤 다시 나타나는 시간입니다.
    [SerializeField] private bool showRanges = true; // 감지 범위와 가시 공격 범위 미리보기를 표시할지 정합니다.
    [SerializeField] private Color slimeColor = new Color(0.18f, 0.85f, 0.35f, 1f); // 평상시 가시 슬라임 색상입니다.
    [SerializeField] private Color attackPoseColor = new Color(0.55f, 1f, 0.25f, 1f); // 공격 준비 또는 공격 중 가시 슬라임 색상입니다.
    [SerializeField] private Color detectionColor = new Color(1f, 0.08f, 0.04f, 0.18f); // 감지 범위 미리보기 색상입니다.
    [SerializeField] private Color spikeColor = new Color(0.05f, 0.35f, 1f, 0.35f); // 가시 공격 범위 미리보기 색상입니다.

    private readonly Collider[] hits = new Collider[24];
    private BoxCollider slimeCollider;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Rigidbody body;
    private Material bodyMaterial;
    private Material detectionMaterial;
    private Material spikeMaterial;
    private Transform detectionVisual;
    private Transform spikeVisual;
    private PlatformerPlayer3D player;
    private Coroutine attackRoutine;
    private float pathDistance;
    private float moveDirection = 1f;
    private float nextAttackTime;
    private float nextContactDamageTime;
    private int currentHealth;
    private bool attackActive;
    private bool attackWindingUp;
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
        InitializePathFromCurrentPosition();
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
            UpdateRangeVisuals();
            return;
        }

        UpdatePlayer();
        if (!attackWindingUp && !attackActive)
        {
            MoveAlongTile();
        }

        TryStartAttack();
        TryDamageOnContact();
        UpdateAttackPose();
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
        CancelAttack();
        SetVisibleAndSolid(false);

        yield return new WaitForSeconds(respawnDelay);

        currentHealth = Mathf.Max(1, maxHealth);
        defeated = false;
        SetVisibleAndSolid(true);
        nextAttackTime = Time.time + 0.2f;
    }

    private void ConfigureSlime()
    {
        ApplyDatabaseTuning();

        bodySize = MonsterRuntime3D.ClampSize(bodySize, 0.1f);
        tileSize = MonsterRuntime3D.ClampSize(tileSize, 0.1f);
        maxHealth = Mathf.Max(1, maxHealth);
        crawlSpeed = Mathf.Max(0f, crawlSpeed);
        detectionBoxSize.x = Mathf.Max(0.1f, detectionBoxSize.x);
        detectionBoxSize.y = Mathf.Max(0.1f, detectionBoxSize.y);
        spikeBoxSize.x = Mathf.Max(0.1f, spikeBoxSize.x);
        spikeBoxSize.y = Mathf.Max(0.1f, spikeBoxSize.y);
        detectionOffset = Mathf.Max(0.05f, detectionOffset);
        spikeOffset = Mathf.Max(0.05f, spikeOffset);
        attackWindupDuration = Mathf.Max(0.01f, attackWindupDuration);
        attackActiveDuration = Mathf.Max(0.01f, attackActiveDuration);
        contactDamageCooldown = Mathf.Max(0.05f, contactDamageCooldown);

        MonsterRuntime3D.ConfigureKinematicBox(
            gameObject,
            bodySize,
            attackWindingUp || attackActive ? attackPoseColor : slimeColor,
            "Generated Thorn Slime Material",
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
        if (database == null || database.ThornSlime == null)
        {
            return;
        }

        ThornSlimeBalance3D tuning = database.ThornSlime;
        bodySize = tuning.bodySize;
        maxHealth = tuning.maxHealth;
        crawlSpeed = tuning.crawlSpeed;
        detectionBoxSize = tuning.detectionBoxSize;
        detectionOffset = tuning.detectionOffset;
        spikeBoxSize = tuning.spikeBoxSize;
        spikeOffset = tuning.spikeOffset;
        attackWindupDuration = tuning.attackWindupDuration;
        attackActiveDuration = tuning.attackActiveDuration;
        attackCooldown = tuning.attackCooldown;
        contactDamage = tuning.contactDamage;
        spikeDamage = tuning.spikeDamage;
        contactDamageCooldown = tuning.contactDamageCooldown;
        respawnDelay = tuning.respawnDelay;
        showRanges = tuning.showRanges;
        slimeColor = tuning.slimeColor;
        attackPoseColor = tuning.attackPoseColor;
        detectionColor = tuning.detectionColor;
        spikeColor = tuning.spikeColor;
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

    private void TryStartAttack()
    {
        if (player == null || attackRoutine != null || Time.time < nextAttackTime)
        {
            return;
        }

        GetDirectionalBox(GetOutwardDirection(), detectionOffset, detectionBoxSize, out Vector3 center, out Vector3 size);
        if (!MonsterRuntime3D.PlayerOverlapsVisibleBox(hits, transform, center, size, player))
        {
            return;
        }

        attackRoutine = StartCoroutine(AttackAfterWindup());
    }

    private IEnumerator AttackAfterWindup()
    {
        attackWindingUp = true;
        UpdateRangeVisuals();

        yield return new WaitForSeconds(attackWindupDuration);

        attackWindingUp = false;
        attackActive = true;
        bool damaged = false;
        float endTime = Time.time + attackActiveDuration;

        while (Time.time < endTime)
        {
            if (!damaged)
            {
                GetDirectionalBox(GetOutwardDirection(), spikeOffset, spikeBoxSize, out Vector3 center, out Vector3 size);
                damaged = MonsterRuntime3D.DamagePlayerInBox(hits, center, size, player, spikeDamage, transform.position);
            }

            UpdateRangeVisuals();
            yield return null;
        }

        attackActive = false;
        attackRoutine = null;
        nextAttackTime = Time.time + attackCooldown;
        UpdateRangeVisuals();
    }

    private void CancelAttack()
    {
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        attackWindingUp = false;
        attackActive = false;
    }

    private void MoveAlongTile()
    {
        Bounds crawlBounds = GetExpandedTileBounds();
        float perimeter = GetPerimeter(crawlBounds);
        if (perimeter <= 0.01f)
        {
            return;
        }

        pathDistance = Mathf.Repeat(pathDistance + moveDirection * crawlSpeed * Time.deltaTime, perimeter);
        transform.position = GetPositionOnPath(crawlBounds, pathDistance);
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

    private void UpdateAttackPose()
    {
        if (bodyMaterial != null)
        {
            bodyMaterial.color = attackWindingUp || attackActive ? attackPoseColor : slimeColor;
        }

        if (!attackWindingUp && !attackActive)
        {
            transform.localScale = bodySize;
            return;
        }

        float pulse = 1f + Mathf.Sin(Time.time * 24f) * 0.04f;
        Vector3 outward = GetOutwardDirection();
        bool horizontal = Mathf.Abs(outward.x) > Mathf.Abs(outward.y);
        transform.localScale = horizontal
            ? new Vector3(bodySize.x * 1.12f * pulse, bodySize.y * 0.94f, bodySize.z)
            : new Vector3(bodySize.x * 0.94f, bodySize.y * 1.12f * pulse, bodySize.z);
    }

    private void InitializePathFromCurrentPosition()
    {
        Bounds crawlBounds = GetExpandedTileBounds();
        pathDistance = GetClosestPathDistance(transform.position, crawlBounds);
    }

    private Bounds GetTileBounds()
    {
        if (tileCollider != null)
        {
            return tileCollider.bounds;
        }

        if (tileTarget != null)
        {
            return new Bounds(tileTarget.position, tileSize);
        }

        return new Bounds(tileCenter, tileSize);
    }

    private Bounds GetExpandedTileBounds()
    {
        Bounds tileBounds = GetTileBounds();
        Vector3 expandedSize = new Vector3(
            tileBounds.size.x + bodySize.x,
            tileBounds.size.y + bodySize.y,
            Mathf.Max(tileBounds.size.z, bodySize.z)
        );

        return new Bounds(tileBounds.center, expandedSize);
    }

    private Vector3 GetOutwardDirection()
    {
        Bounds tileBounds = GetTileBounds();
        Vector3 fromCenter = transform.position - tileBounds.center;
        float normalizedX = Mathf.Abs(fromCenter.x) / Mathf.Max(0.001f, tileBounds.extents.x);
        float normalizedY = Mathf.Abs(fromCenter.y) / Mathf.Max(0.001f, tileBounds.extents.y);

        if (normalizedX > normalizedY)
        {
            return fromCenter.x < 0f ? Vector3.left : Vector3.right;
        }

        return fromCenter.y < 0f ? Vector3.down : Vector3.up;
    }

    private void GetDirectionalBox(Vector3 direction, float offset, Vector2 boxSize, out Vector3 center, out Vector3 size)
    {
        Vector3 normalizedDirection = direction.normalized;
        bool horizontal = Mathf.Abs(normalizedDirection.x) > Mathf.Abs(normalizedDirection.y);
        float bodyHalf = horizontal ? bodySize.x * 0.5f : bodySize.y * 0.5f;

        center = transform.position + normalizedDirection * (bodyHalf + offset * 0.5f);
        size = horizontal
            ? new Vector3(offset + boxSize.y, boxSize.x, bodySize.z)
            : new Vector3(boxSize.x, offset + boxSize.y, bodySize.z);
    }

    private static float GetPerimeter(Bounds bounds)
    {
        return Mathf.Max(0.1f, 2f * (bounds.size.x + bounds.size.y));
    }

    private static Vector3 GetPositionOnPath(Bounds bounds, float distance)
    {
        float width = bounds.size.x;
        float height = bounds.size.y;
        float perimeter = GetPerimeter(bounds);
        float d = Mathf.Repeat(distance, perimeter);
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        float z = bounds.center.z;

        if (d < width)
        {
            return new Vector3(min.x + d, min.y, z);
        }

        d -= width;
        if (d < height)
        {
            return new Vector3(max.x, min.y + d, z);
        }

        d -= height;
        if (d < width)
        {
            return new Vector3(max.x - d, max.y, z);
        }

        d -= width;
        return new Vector3(min.x, max.y - d, z);
    }

    private static float GetClosestPathDistance(Vector3 worldPosition, Bounds bounds)
    {
        float width = bounds.size.x;
        float height = bounds.size.y;
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        Vector3 clamped = new Vector3(
            Mathf.Clamp(worldPosition.x, min.x, max.x),
            Mathf.Clamp(worldPosition.y, min.y, max.y),
            bounds.center.z
        );

        float leftDistance = Mathf.Abs(clamped.x - min.x);
        float rightDistance = Mathf.Abs(clamped.x - max.x);
        float bottomDistance = Mathf.Abs(clamped.y - min.y);
        float topDistance = Mathf.Abs(clamped.y - max.y);
        float nearest = Mathf.Min(leftDistance, rightDistance, bottomDistance, topDistance);

        if (nearest == bottomDistance)
        {
            return clamped.x - min.x;
        }

        if (nearest == rightDistance)
        {
            return width + (clamped.y - min.y);
        }

        if (nearest == topDistance)
        {
            return width + height + (max.x - clamped.x);
        }

        return width + height + width + (max.y - clamped.y);
    }

    private void EnsureRangeVisuals()
    {
        if (detectionVisual == null)
        {
            detectionVisual = MonsterRuntime3D.FindOrCreateBoxVisual(transform, "Thorn Slime Detection Range", detectionColor, ref detectionMaterial);
        }

        if (spikeVisual == null)
        {
            spikeVisual = MonsterRuntime3D.FindOrCreateBoxVisual(transform, "Thorn Slime Spike Hitbox", spikeColor, ref spikeMaterial);
        }
    }

    private void UpdateRangeVisuals()
    {
        EnsureRangeVisuals();

        GetDirectionalBox(GetOutwardDirection(), detectionOffset, detectionBoxSize, out Vector3 detectionCenter, out Vector3 detectionSize3D);
        GetDirectionalBox(GetOutwardDirection(), spikeOffset, spikeBoxSize, out Vector3 spikeCenter, out Vector3 spikeSize3D);

        if (detectionMaterial != null)
        {
            detectionMaterial.color = detectionColor;
        }

        if (spikeMaterial != null)
        {
            spikeMaterial.color = attackActive ? spikeColor : new Color(spikeColor.r, spikeColor.g, spikeColor.b, 0.16f);
        }

        MonsterRuntime3D.ApplyWorldBoxVisual(transform, detectionVisual, detectionCenter, detectionSize3D, 0.05f);
        MonsterRuntime3D.ApplyWorldBoxVisual(transform, spikeVisual, spikeCenter, spikeSize3D, 0.06f);

        bool visible = showRanges && !defeated;
        MonsterRuntime3D.SetVisualVisible(detectionVisual, visible);
        MonsterRuntime3D.SetVisualVisible(spikeVisual, visible && (attackWindingUp || attackActive));
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
        MonsterRuntime3D.SetVisualVisible(spikeVisual, value && showRanges && (attackWindingUp || attackActive));
    }
}
