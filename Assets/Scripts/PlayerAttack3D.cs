using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAttack3D : MonoBehaviour
{
    [SerializeField] private float attackCooldown = 0.28f; // 좌클릭 평타를 다시 사용할 수 있을 때까지의 대기 시간입니다.
    [SerializeField] private float attackActiveTime = 0.12f; // 공격 판정이 실제로 유지되는 시간입니다.
    [SerializeField] private Vector3 sideHitboxSize = new Vector3(1.15f, 0.75f, 1f); // 좌우 평타 히트박스 크기입니다.
    [SerializeField] private Vector3 verticalHitboxSize = new Vector3(0.8f, 1.15f, 1f); // 위아래 평타 히트박스 크기입니다.
    [SerializeField] private float sideHitboxOffset = 0.88f; // 좌우 평타 히트박스가 플레이어 중심에서 떨어지는 거리입니다.
    [SerializeField] private float verticalHitboxOffset = 0.95f; // 위아래 평타 히트박스가 플레이어 중심에서 떨어지는 거리입니다.
    [SerializeField] private bool showHitboxPreview; // 공격 범위 파란 박스를 화면에 표시할지 정합니다.
    [SerializeField] private Color hitboxColor = new Color(0.05f, 0.35f, 1f, 1f); // 공격 범위 미리보기 박스 색상입니다.
    [SerializeField] private LayerMask attackMask = ~0; // 평타가 맞출 수 있는 레이어 범위입니다.

    private readonly Collider[] attackHits = new Collider[16];
    private readonly HashSet<IAttackable3D> hitAttackables = new HashSet<IAttackable3D>();

    private PlatformerPlayer3D movement;
    private PlayerAttackAnimation3D attackAnimation;
    private CameraAbilitySystem3D cameraAbilities;
    private GameObject hitboxVisual;
    private Transform hitboxTransform;
    private float attackTimer;
    private float cooldownTimer;
    private Vector3 currentHitboxOffset;
    private Vector3 currentHitboxCenter;
    private Vector3 currentHitboxSize;

    private enum AttackDirection
    {
        Left,
        Right,
        Up,
        Down
    }

    private void Awake()
    {
        ApplyDatabaseTuning();
        movement = GetComponent<PlatformerPlayer3D>();
        cameraAbilities = GetComponent<CameraAbilitySystem3D>();
        attackAnimation = GetComponent<PlayerAttackAnimation3D>();
        if (attackAnimation == null)
        {
            attackAnimation = gameObject.AddComponent<PlayerAttackAnimation3D>();
        }

        EnsureHitboxVisual();
    }

    private void OnEnable()
    {
        ApplyDatabaseTuning();
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (cooldownTimer > 0f)
        {
            cooldownTimer = Mathf.Max(0f, cooldownTimer - Time.deltaTime);
        }

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && !IsPrimaryFireReservedForCamera())
        {
            TryStartAttack();
        }

        if (attackTimer > 0f)
        {
            attackTimer = Mathf.Max(0f, attackTimer - Time.deltaTime);
            UpdateHitboxCenter();
            UpdateHitboxVisual();
            DamageEnemiesInHitbox();
        }
        else if (hitboxVisual != null && hitboxVisual.activeSelf)
        {
            hitboxVisual.SetActive(false);
        }
    }

    private bool IsPrimaryFireReservedForCamera()
    {
        if (cameraAbilities == null)
        {
            cameraAbilities = GetComponent<CameraAbilitySystem3D>();
        }

        return cameraAbilities != null && cameraAbilities.BlocksLegacyAttackInput;
    }

    private void TryStartAttack()
    {
        ApplyDatabaseTuning();

        if (cooldownTimer > 0f || movement == null || movement.IsKnockedBack)
        {
            return;
        }

        AttackDirection direction = ReadAttackDirection();
        ConfigureHitbox(direction);

        attackTimer = attackActiveTime;
        cooldownTimer = attackCooldown;
        hitAttackables.Clear();

        EnsureHitboxVisual();
        if (hitboxVisual != null)
        {
            hitboxVisual.SetActive(true);
        }

        UpdateHitboxCenter();
        UpdateHitboxVisual();
        attackAnimation.Play(currentHitboxOffset, movement.FacingDirection);
        DamageEnemiesInHitbox();
    }

    private void ApplyDatabaseTuning()
    {
        UnitBalanceDatabase3D database = UnitBalanceDatabase3D.Load();
        if (database == null || database.PlayerAttack == null)
        {
            return;
        }

        PlayerAttackBalance3D tuning = database.PlayerAttack;
        attackCooldown = tuning.attackCooldown;
        attackActiveTime = tuning.attackActiveTime;
        sideHitboxSize = tuning.sideHitboxSize;
        verticalHitboxSize = tuning.verticalHitboxSize;
        sideHitboxOffset = tuning.sideHitboxOffset;
        verticalHitboxOffset = tuning.verticalHitboxOffset;
        showHitboxPreview = tuning.showHitboxPreview;
        hitboxColor = tuning.hitboxColor;
        attackMask = tuning.attackMask;
    }

    private AttackDirection ReadAttackDirection()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            {
                return AttackDirection.Up;
            }

            bool wantsDown = keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed;
            if (wantsDown && !movement.IsGrounded)
            {
                return AttackDirection.Down;
            }

            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            {
                return AttackDirection.Left;
            }

            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            {
                return AttackDirection.Right;
            }
        }

        return movement.FacingDirection < 0f ? AttackDirection.Left : AttackDirection.Right;
    }

    private void ConfigureHitbox(AttackDirection direction)
    {
        currentHitboxSize = direction == AttackDirection.Up || direction == AttackDirection.Down
            ? verticalHitboxSize
            : sideHitboxSize;

        Vector3 offset = Vector3.zero;
        switch (direction)
        {
            case AttackDirection.Left:
                offset.x = -sideHitboxOffset;
                break;
            case AttackDirection.Right:
                offset.x = sideHitboxOffset;
                break;
            case AttackDirection.Up:
                offset.y = verticalHitboxOffset;
                break;
            case AttackDirection.Down:
                offset.y = -verticalHitboxOffset;
                break;
        }

        currentHitboxOffset = offset;
        currentHitboxCenter = transform.position + currentHitboxOffset;
    }

    private void DamageEnemiesInHitbox()
    {
        int hitCount = Physics.OverlapBoxNonAlloc(
            currentHitboxCenter,
            currentHitboxSize * 0.5f,
            attackHits,
            Quaternion.identity,
            attackMask,
            QueryTriggerInteraction.Collide
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = attackHits[i];
            if (hit == null || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            IAttackable3D attackable = FindAttackable(hit);
            if (attackable == null || hitAttackables.Contains(attackable))
            {
                continue;
            }

            if (attackable.TakeAttack())
            {
                hitAttackables.Add(attackable);
            }
        }
    }

    private static IAttackable3D FindAttackable(Collider hit)
    {
        MonoBehaviour[] behaviours = hit.GetComponentsInParent<MonoBehaviour>();

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IAttackable3D attackable)
            {
                return attackable;
            }
        }

        return null;
    }

    private void EnsureHitboxVisual()
    {
        if (!showHitboxPreview || hitboxVisual != null)
        {
            return;
        }

        hitboxVisual = new GameObject("Attack Hitbox Preview", typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider));
        hitboxTransform = hitboxVisual.transform;
        hitboxVisual.GetComponent<MeshFilter>().sharedMesh = GetCubeMesh();

        MeshRenderer meshRenderer = hitboxVisual.GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = CreateMaterial("Generated Attack Hitbox Material", hitboxColor);

        BoxCollider boxCollider = hitboxVisual.GetComponent<BoxCollider>();
        boxCollider.isTrigger = true;

        hitboxVisual.SetActive(false);
    }

    private void UpdateHitboxCenter()
    {
        currentHitboxCenter = transform.position + currentHitboxOffset;
    }

    private void UpdateHitboxVisual()
    {
        if (!showHitboxPreview || hitboxVisual == null)
        {
            return;
        }

        hitboxTransform.position = currentHitboxCenter;
        hitboxTransform.rotation = Quaternion.identity;
        hitboxTransform.localScale = currentHitboxSize;

        BoxCollider boxCollider = hitboxVisual.GetComponent<BoxCollider>();
        boxCollider.size = Vector3.one;
        boxCollider.center = Vector3.zero;
    }

    private static Mesh GetCubeMesh()
    {
        Mesh mesh = new Mesh { name = "Generated Attack Hitbox Mesh" };
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
