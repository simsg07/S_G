using UnityEngine;

[CreateAssetMenu(fileName = "UnitBalanceDatabase", menuName = "S_G/Unit Balance Database")]
public class UnitBalanceDatabase3D : ScriptableObject
{
    private const string ResourceName = "UnitBalanceDatabase";
    private static UnitBalanceDatabase3D cachedDatabase;

    [Header("Player")]
    [SerializeField] private PlayerMovementBalance3D playerMovement = new PlayerMovementBalance3D(); // 플레이어 이동, 점프, 대쉬, 넉백 수치 묶음입니다.
    [SerializeField] private PlayerHealthBalance3D playerHealth = new PlayerHealthBalance3D(); // 플레이어 체력과 무적 UI 수치 묶음입니다.
    [SerializeField] private PlayerAttackBalance3D playerAttack = new PlayerAttackBalance3D(); // 플레이어 평타 판정과 쿨타임 수치 묶음입니다.
    [SerializeField] private PlayerAttackAnimationBalance3D playerAttackAnimation = new PlayerAttackAnimationBalance3D(); // 플레이어 기본 이미지와 공격 모션 표시 수치 묶음입니다.

    [Header("Monster")]
    [SerializeField] private MonsterAni1Balance3D monsterAni1 = new MonsterAni1Balance3D(); // monster_ani1 유리관 오브젝트의 기본 수치 묶음입니다.
    [SerializeField] private SlimeBalance3D slime = new SlimeBalance3D(); // 기본 추적 슬라임 수치 묶음입니다.
    [SerializeField] private RangedSlimeBalance3D rangedSlime = new RangedSlimeBalance3D(); // 원거리 슬라임 수치 묶음입니다.
    [SerializeField] private ThornSlimeBalance3D thornSlime = new ThornSlimeBalance3D(); // 가시 슬라임 수치 묶음입니다.
    [SerializeField] private BalloonSlimeBalance3D balloonSlime = new BalloonSlimeBalance3D(); // 풍선 슬라임 수치 묶음입니다.
    [SerializeField] private SlimeHybeBalance3D slimeHybe = new SlimeHybeBalance3D(); // 하이브 슬라임 수치 묶음입니다.
    [SerializeField] private ProjectileBalance3D projectiles = new ProjectileBalance3D(); // 런타임 생성 투사체의 기본 수치 묶음입니다.

    public PlayerMovementBalance3D PlayerMovement => playerMovement;
    public PlayerHealthBalance3D PlayerHealth => playerHealth;
    public PlayerAttackBalance3D PlayerAttack => playerAttack;
    public PlayerAttackAnimationBalance3D PlayerAttackAnimation => playerAttackAnimation;
    public SlimeBalance3D Slime => slime;
    public RangedSlimeBalance3D RangedSlime => rangedSlime;
    public ThornSlimeBalance3D ThornSlime => thornSlime;
    public BalloonSlimeBalance3D BalloonSlime => balloonSlime;
    public MonsterAni1Balance3D MonsterAni1 => monsterAni1;
    public SlimeHybeBalance3D SlimeHybe => slimeHybe;
    public ProjectileBalance3D Projectiles => projectiles;

    public static UnitBalanceDatabase3D Load()
    {
        if (cachedDatabase == null)
        {
            cachedDatabase = Resources.Load<UnitBalanceDatabase3D>(ResourceName);
        }

        return cachedDatabase;
    }
}

[System.Serializable]
public class PlayerMovementBalance3D
{
    public float moveSpeed = 6f; // 플레이어 좌우 이동 속도입니다.
    public float jumpHeight = 3f; // 점프 높이입니다. 값을 올리면 더 높게 뜁니다.
    public float gravityScale = 3f; // 플레이어에게 적용되는 중력 배율입니다.
    public float fallGravityMultiplier = 1.35f; // 낙하 중 추가 중력 배율입니다.
    public float maxFallSpeed = 18f; // 최대 낙하 속도 제한입니다.
    public float coyoteTime = 0.08f; // 발판에서 떨어진 직후에도 점프를 허용하는 시간입니다.
    public float jumpBufferTime = 0.1f; // 착지 직전 점프 입력을 미리 저장해두는 시간입니다.
    public float jumpCutMultiplier = 0.5f; // 점프키를 빨리 떼면 상승 속도를 줄이는 비율입니다.
    public float groundCheckDistance = 0.08f; // 바닥 판정을 확인할 추가 거리입니다.
    public float dropThroughDuration = 0.45f; // S+스페이스로 발판을 내려갈 때 충돌을 무시하는 최소 시간입니다.
    public float passThroughClearance = 0.05f; // 내려가기 발판 충돌을 복구하기 전에 필요한 여유 거리입니다.
    public float knockbackHorizontalSpeed = 8f; // 피격 넉백의 가로 밀림 속도입니다.
    public float knockbackVerticalSpeed = 5f; // 피격 넉백의 위로 튀는 속도입니다.
    public float knockbackDuration = 0.28f; // 넉백 상태가 유지되는 시간입니다.
    public float dashSpeed = 14f; // 대쉬 이동 속도입니다.
    public float dashDuration = 0.14f; // 대쉬가 지속되는 시간입니다.
    public float dashCooldown = 0.4f; // 다음 대쉬를 다시 쓸 수 있을 때까지의 대기 시간입니다.
    public int maxAirJumps = 1; // 바닥 점프 이후 허용되는 추가 공중 점프 횟수입니다.
    public int maxAirDashes = 1; // 착지 전 허용되는 공중 대쉬 횟수입니다.
    public Vector3 colliderSize = new Vector3(0.8f, 1.2f, 1f); // 플레이어 충돌 박스와 몸 크기입니다.
    public bool useRobotLegJump = true; // 스페이스를 누르는 동안 로봇 다리가 늘어나는 점프 방식을 사용할지 정합니다.
    public float maxLegExtension = 10f; // 로봇 다리가 최대로 늘어날 수 있는 길이입니다.
    public float legExtendSpeed = 4.8f; // 로봇 다리가 늘어나는 속도입니다.
    public float legRetractSpeed = 8.5f; // 로봇 다리가 접히는 속도입니다.
    public float legReleaseJumpHeight = 2f; // 로봇 다리가 접히기 시작할 때 추가로 적용할 점프 높이입니다.
    public float ceilingCheckDistance = 0.08f; // 로봇 다리 상승 중 천장에 닿기 전에 멈추는 여유 거리입니다.
    public float legObstacleClearance = 0.04f; // 로봇 다리가 발판이나 박스에 닿기 전에 남길 여유 거리입니다.
    public bool showRobotLegVisual = true; // 로봇 다리 미리보기 박스를 화면에 표시할지 정합니다.
    public Color robotLegColor = new Color(0.55f, 0.6f, 0.62f, 1f); // 로봇 다리 미리보기 박스 색상입니다.
}

[System.Serializable]
public class PlayerHealthBalance3D
{
    public int maxHealth = 5; // 플레이어 최대 체력 칸 수입니다.
    public int currentHealth = 5; // 게임 시작 시 플레이어 현재 체력입니다.
    public float invincibilityDuration = 0.8f; // 피격 후 추가 피해를 받지 않는 무적 시간입니다.
    public float invincibleBlinkInterval = 0.1f; // 무적 중 플레이어가 깜빡이는 간격입니다.
    public Vector2 slotSize = new Vector2(28f, 18f); // 좌측 상단 체력칸 하나의 UI 크기입니다.
    public float slotSpacing = 6f; // 체력칸 사이의 간격입니다.
    public Color filledColor = new Color(0.95f, 0.2f, 0.15f, 1f); // 남아 있는 체력칸 색상입니다.
    public Color emptyColor = new Color(0.12f, 0.12f, 0.12f, 0.75f); // 소모된 체력칸 색상입니다.
    public Color borderColor = new Color(1f, 1f, 1f, 0.85f); // 체력칸 테두리 색상입니다.
}

[System.Serializable]
public class PlayerAttackBalance3D
{
    public float attackCooldown = 0.28f; // 좌클릭 평타를 다시 사용할 수 있을 때까지의 대기 시간입니다.
    public float attackActiveTime = 0.12f; // 공격 판정이 실제로 유지되는 시간입니다.
    public Vector3 sideHitboxSize = new Vector3(1.15f, 0.75f, 1f); // 좌우 평타 히트박스 크기입니다.
    public Vector3 verticalHitboxSize = new Vector3(0.8f, 1.15f, 1f); // 위아래 평타 히트박스 크기입니다.
    public float sideHitboxOffset = 0.88f; // 좌우 평타 히트박스가 플레이어 중심에서 떨어지는 거리입니다.
    public float verticalHitboxOffset = 0.95f; // 위아래 평타 히트박스가 플레이어 중심에서 떨어지는 거리입니다.
    public bool showHitboxPreview; // 공격 범위 파란 박스를 화면에 표시할지 정합니다.
    public Color hitboxColor = new Color(0.05f, 0.35f, 1f, 1f); // 공격 범위 미리보기 박스 색상입니다.
    public LayerMask attackMask = ~0; // 평타가 맞출 수 있는 레이어 범위입니다.
}

[System.Serializable]
public class PlayerAttackAnimationBalance3D
{
    public string resourceFolder = "PlayerAttack"; // 공격 이미지가 들어 있는 Resources 하위 폴더 이름입니다.
    public string[] frameNames = { "attack_01", "attack_02", "attack_03", "attack_04" }; // 공격 모션을 재생할 이미지 순서입니다.
    public float frameDuration = 0.045f; // 공격 모션 한 장면이 유지되는 시간입니다.
    public float pixelsPerUnit = 340f; // 공격 스프라이트의 게임 안 크기를 조절하는 픽셀 비율입니다.
    public Vector3 localOffset = new Vector3(0.2f, 0.04f, -0.45f); // 공격 모션 이미지가 플레이어 기준으로 표시될 위치입니다.
    public int sortingOrder = 30; // 공격 모션이 다른 스프라이트보다 앞에 보이는 순서입니다.
}

[System.Serializable]
public class SlimeBalance3D
{
    public Vector3 bodySize = new Vector3(0.75f, 0.55f, 0.8f); // 기본 추적 슬라임 몸체와 충돌 박스 크기입니다.
    public int maxHealth = 4; // 기본 추적 슬라임 최대 체력입니다.
    public float patrolSpeed = 1.15f; // 플레이어를 추적하지 않을 때 순찰 이동 속도입니다.
    public float chaseSpeed = 2.1f; // 플레이어를 추적할 때 이동 속도입니다.
    public float edgeProbeDistance = 0.16f; // 발판 끝을 확인하는 앞쪽 탐지 거리입니다.
    public float groundProbeDepth = 0.16f; // 앞쪽 바닥 유무를 확인하는 아래쪽 탐지 깊이입니다.
    public float wallProbeDistance = 0.12f; // 앞쪽 벽을 확인하는 탐지 거리입니다.
    public float detectionSize = 4.2f; // 추적을 시작하는 정사각형 인식 범위 크기입니다.
    public float chaseSize = 6.2f; // 이미 추적 중일 때 추적을 유지하는 정사각형 범위 크기입니다.
    public int contactDamage = 1; // 접촉 시 플레이어에게 주는 피해량입니다.
    public float contactDamageCooldown = 0.8f; // 접촉 피해를 다시 줄 수 있을 때까지의 대기 시간입니다.
    public float respawnDelay = 3f; // 처치된 뒤 다시 나타나는 시간입니다.
    public bool showRanges = true; // 인식 범위와 추적 유지 범위 미리보기를 표시할지 정합니다.
    public Color slimeColor = new Color(0.32f, 0.82f, 0.28f, 1f); // 평상시 기본 추적 슬라임 색상입니다.
    public Color chaseColor = new Color(0.6f, 0.96f, 0.35f, 1f); // 추적 중 기본 추적 슬라임 색상입니다.
    public Color detectionColor = new Color(1f, 0.08f, 0.04f, 0.18f); // 추적 시작 범위 미리보기 색상입니다.
    public Color chaseRangeColor = new Color(1f, 0.15f, 0.78f, 0.13f); // 추적 유지 범위 미리보기 색상입니다.
}

[System.Serializable]
public class RangedSlimeBalance3D
{
    public Vector3 slimeSize = new Vector3(0.7f, 0.5f, 0.8f); // 원거리 슬라임 몸체와 충돌 박스 크기입니다.
    public float patrolSpeed = 0.85f; // 좌우 순찰 이동 속도입니다.
    public float edgeProbeDistance = 0.16f; // 발판 끝을 확인하는 앞쪽 탐지 거리입니다.
    public float groundProbeDepth = 0.16f; // 앞쪽 바닥 유무를 확인하는 아래쪽 탐지 깊이입니다.
    public float wallProbeDistance = 0.12f; // 앞쪽 벽을 확인하는 탐지 거리입니다.
    public float detectionSize = 6f; // 정사각형 인식 범위 크기입니다.
    public float attackWindupDuration = 0.55f; // 플레이어를 감지한 뒤 투사체 발사까지 기다리는 선딜 시간입니다.
    public float attackCooldown = 1.4f; // 발사 후 다음 공격까지 기다리는 시간입니다.
    public float respawnDelay = 3f; // 처치된 뒤 다시 나타나는 시간입니다.
    public int projectileDamage = 1; // 투사체가 플레이어에게 주는 피해량입니다.
    public float projectileSpeed = 4.8f; // 투사체가 날아가는 속도입니다.
    public float projectileLifetime = 3f; // 투사체가 자동으로 사라지기까지의 시간입니다.
    public Vector3 projectileSize = new Vector3(0.28f, 0.28f, 0.8f); // 투사체 충돌 박스와 표시 크기입니다.
    public bool showRanges = true; // 인식 범위 미리보기를 표시할지 정합니다.
    public Color slimeColor = new Color(0.15f, 0.75f, 0.95f, 1f); // 평상시 원거리 슬라임 색상입니다.
    public Color attackPoseColor = new Color(0.1f, 1f, 1f, 1f); // 발사 준비 중 슬라임 몸체 색상입니다.
    public Color detectionColor = new Color(0.15f, 0.85f, 1f, 0.16f); // 인식 범위 색상입니다.
    public Color projectileColor = new Color(0.1f, 0.95f, 1f, 1f); // 원거리 투사체 색상입니다.
}

[System.Serializable]
public class ThornSlimeBalance3D
{
    public Vector3 bodySize = new Vector3(0.58f, 0.38f, 0.8f); // 가시 슬라임 몸체와 충돌 박스 크기입니다.
    public int maxHealth = 3; // 가시 슬라임 최대 체력입니다.
    public float crawlSpeed = 1.35f; // 타일 둘레를 따라 이동하는 속도입니다.
    public Vector2 detectionBoxSize = new Vector2(1.35f, 0.75f); // 가시 공격을 준비하기 전 플레이어를 감지하는 박스 크기입니다.
    public float detectionOffset = 0.58f; // 감지 박스가 몸체 바깥으로 뻗는 거리입니다.
    public Vector2 spikeBoxSize = new Vector2(1.1f, 0.58f); // 실제 가시 공격 판정 박스 크기입니다.
    public float spikeOffset = 0.62f; // 가시 공격 판정이 몸체 바깥으로 뻗는 거리입니다.
    public float attackWindupDuration = 0.28f; // 감지 후 가시 공격이 나오기 전 선딜 시간입니다.
    public float attackActiveDuration = 0.22f; // 가시 공격 판정이 유지되는 시간입니다.
    public float attackCooldown = 1f; // 가시 공격 후 다음 공격까지 기다리는 시간입니다.
    public int contactDamage = 1; // 몸체 접촉 시 플레이어에게 주는 피해량입니다.
    public int spikeDamage = 1; // 가시 공격 적중 시 플레이어에게 주는 피해량입니다.
    public float contactDamageCooldown = 0.8f; // 몸체 접촉 피해를 다시 줄 수 있을 때까지의 대기 시간입니다.
    public float respawnDelay = 3f; // 처치된 뒤 다시 나타나는 시간입니다.
    public bool showRanges = true; // 감지 범위와 가시 공격 범위 미리보기를 표시할지 정합니다.
    public Color slimeColor = new Color(0.18f, 0.85f, 0.35f, 1f); // 평상시 가시 슬라임 색상입니다.
    public Color attackPoseColor = new Color(0.55f, 1f, 0.25f, 1f); // 공격 준비 또는 공격 중 가시 슬라임 색상입니다.
    public Color detectionColor = new Color(1f, 0.08f, 0.04f, 0.18f); // 감지 범위 미리보기 색상입니다.
    public Color spikeColor = new Color(0.05f, 0.35f, 1f, 0.35f); // 가시 공격 범위 미리보기 색상입니다.
}

[System.Serializable]
public class BalloonSlimeBalance3D
{
    public Vector3 bodySize = new Vector3(0.62f, 0.62f, 0.8f); // 풍선 슬라임 몸체와 충돌 박스 크기입니다.
    public int maxHealth = 1; // 풍선 슬라임 최대 체력입니다.
    public float detectionSize = 5f; // 돌진을 시작하는 정사각형 인식 범위 크기입니다.
    public float hoverSpeed = 0.35f; // 평상시 좌우로 떠다니는 이동 속도입니다.
    public float hoverFrequency = 2.1f; // 위아래 둥실거림의 반복 속도입니다.
    public float hoverBobStrength = 0.12f; // 위아래 둥실거림의 세기입니다.
    public float chargeDuration = 1.2f; // 플레이어를 감지한 뒤 돌진 전 충전하는 시간입니다.
    public float dashSpeed = 7.2f; // 돌진 공격 이동 속도입니다.
    public float dashDuration = 0.46f; // 돌진 공격이 유지되는 시간입니다.
    public float attackCooldown = 0.6f; // 돌진 후 다음 돌진까지 기다리는 시간입니다.
    public int contactDamage = 1; // 돌진 중 플레이어에게 주는 피해량입니다.
    public float respawnDelay = 3f; // 처치된 뒤 다시 나타나는 시간입니다.
    public bool showRanges = true; // 인식 범위 미리보기를 표시할지 정합니다.
    public Color bodyColor = new Color(0.95f, 0.82f, 0.2f, 1f); // 평상시 풍선 슬라임 색상입니다.
    public Color chargeColor = new Color(1f, 0.45f, 0.18f, 1f); // 충전 또는 돌진 중 풍선 슬라임 색상입니다.
    public Color detectionColor = new Color(1f, 0.08f, 0.04f, 0.16f); // 인식 범위 미리보기 색상입니다.
}

[System.Serializable]
public class MonsterAni1Balance3D
{
    public Vector3 tubeSize = new Vector3(2.05f, 3.41f, 0.8f); // 유리관 전체 표시 크기와 트리거 영역 크기입니다. 882:1467 비율에 맞춘 값입니다.
    public Vector2 innerMonsterSize = new Vector2(0.9f, 2.37f); // 유리관 안에서 떠다니는 몬스터 그림 크기입니다. 368:969 비율에 맞춘 값입니다.
    public Vector3 innerMonsterOffset = new Vector3(0.13f, -0.08f, -0.03f); // 유리관 중심 기준 내부 몬스터의 기본 위치입니다.
    public float floatSpeed = 0.45f; // 내부 몬스터가 둥둥 떠다니는 속도입니다. 값을 올리면 더 빠르게 움직입니다.
    public float floatHeight = 0.055f; // 내부 몬스터가 위아래로 움직이는 높이입니다.
    public Vector2 bubbleSize = new Vector2(0.55f, 1.45f); // 유리관 안 물방울 묶음 표시 크기입니다.
    public Vector3 bubbleOffset = new Vector3(0.13f, 0f, -0.03f); // 유리관 중심 기준 물방울 묶음의 기본 위치입니다.
    public float bubbleFloatSpeed = 0.38f; // 물방울이 위아래로 움직이는 속도입니다. 괴물보다 살짝 느리게 둡니다.
    public float bubbleFloatHeight = 0.06f; // 물방울이 위아래로 움직이는 높이입니다.
    public bool useTriggerCollider = true; // 켜두면 플레이어를 막지 않고 감지만 가능한 트리거 오브젝트가 됩니다.
    public Color tubeTintColor = Color.white; // 유리관 이미지에 곱해지는 색상입니다.
    public Color innerMonsterTintColor = Color.white; // 내부 몬스터 이미지에 곱해지는 색상입니다.
    public Color bubbleTintColor = Color.white; // 물방울 이미지에 곱해지는 색상입니다.
}

[System.Serializable]
public class SlimeHybeBalance3D
{
    public Vector3 bodySize = new Vector3(0.95f, 0.62f, 0.8f); // 하이브 슬라임 몸체와 충돌 박스 크기입니다.
    public int maxHealth = 7; // 하이브 슬라임 최대 체력입니다.
    public float wanderSpeed = 0.85f; // 플레이어를 감지하지 않았을 때 배회 이동 속도입니다.
    public float fleeSpeed = 1.65f; // 플레이어에게서 도망갈 때 이동 속도입니다.
    public float edgeProbeDistance = 0.18f; // 발판 끝을 확인하는 앞쪽 탐지 거리입니다.
    public float groundProbeDepth = 0.16f; // 앞쪽 바닥 유무를 확인하는 아래쪽 탐지 깊이입니다.
    public float wallProbeDistance = 0.12f; // 앞쪽 벽을 확인하는 탐지 거리입니다.
    public float detectionSize = 4.8f; // 패턴을 시작하는 정사각형 인식 범위 크기입니다.
    public float fleeDuration = 0.85f; // 패턴 시작 전 플레이어 반대 방향으로 도망가는 시간입니다.
    public float shakeDuration = 0.75f; // 패턴 발동 전 몸을 흔드는 준비 시간입니다.
    public float patternCooldown = 1.7f; // 패턴 사용 후 다음 패턴까지 기다리는 시간입니다.
    public int balloonSpawnCount = 2; // 풍선 슬라임 소환 패턴에서 생성할 수입니다.
    public int mucusProjectileMin = 5; // 점액 흩뿌리기 패턴의 최소 투사체 수입니다.
    public int mucusProjectileMax = 6; // 점액 흩뿌리기 패턴의 최대 투사체 수입니다.
    public float mucusProjectileSpeed = 4.2f; // 점액 투사체 기본 발사 속도입니다.
    public float mucusProjectileLifetime = 2.5f; // 점액 투사체가 자동으로 사라지기까지의 시간입니다.
    public float mucusProjectileGravity = 3.5f; // 점액 투사체에 적용되는 아래 방향 중력 세기입니다.
    public Vector3 mucusProjectileSize = new Vector3(0.24f, 0.24f, 0.8f); // 점액 투사체 충돌 박스와 표시 크기입니다.
    public int contactDamage = 1; // 몸체 또는 점액 투사체가 플레이어에게 주는 피해량입니다.
    public float contactDamageCooldown = 0.85f; // 몸체 접촉 피해를 다시 줄 수 있을 때까지의 대기 시간입니다.
    public float respawnDelay = 3f; // 처치된 뒤 다시 나타나는 시간입니다.
    public bool showRanges = true; // 인식 범위 미리보기를 표시할지 정합니다.
    public Color bodyColor = new Color(0.58f, 0.72f, 0.18f, 1f); // 평상시 하이브 슬라임 색상입니다.
    public Color alertColor = new Color(0.78f, 0.88f, 0.25f, 1f); // 도주 또는 패턴 준비 중 하이브 슬라임 색상입니다.
    public Color detectionColor = new Color(1f, 0.08f, 0.04f, 0.17f); // 인식 범위 미리보기 색상입니다.
    public Color mucusColor = new Color(0.55f, 0.95f, 0.25f, 1f); // 점액 투사체 색상입니다.
}

[System.Serializable]
public class ProjectileBalance3D
{
    public Vector3 rangedProjectileSize = new Vector3(0.28f, 0.28f, 0.8f); // 초기화 전 사용할 원거리 투사체 기본 크기입니다.
    public int rangedProjectileDamage = 1; // 초기화 전 사용할 원거리 투사체 기본 피해량입니다.
    public float rangedProjectileLifetime = 3f; // 초기화 전 사용할 원거리 투사체 기본 지속 시간입니다.
    public Color rangedProjectileColor = new Color(0.1f, 0.95f, 1f, 1f); // 초기화 전 사용할 원거리 투사체 기본 색상입니다.
    public Vector3 mucusProjectileSize = new Vector3(0.24f, 0.24f, 0.8f); // 초기화 전 사용할 점액 투사체 기본 크기입니다.
    public int mucusProjectileDamage = 1; // 초기화 전 사용할 점액 투사체 기본 피해량입니다.
    public float mucusProjectileLifetime = 2.5f; // 초기화 전 사용할 점액 투사체 기본 지속 시간입니다.
    public float mucusProjectileGravity = 3.5f; // 초기화 전 사용할 점액 투사체 기본 중력 세기입니다.
    public Color mucusProjectileColor = new Color(0.5f, 0.95f, 0.25f, 1f); // 초기화 전 사용할 점액 투사체 기본 색상입니다.
}
