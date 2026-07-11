using UnityEngine;

[DisallowMultipleComponent]
public class MonsterDummyActor3D : MonoBehaviour, IAttackable3D, IShutterFreezable3D, IFlashReactive3D, IRelayTransferable3D
{
    [SerializeField] private string monsterId = "dummy_monster"; // 데이터베이스에서 더미 몬스터를 구분하는 ID입니다.
    [SerializeField] private string displayName = "Dummy Monster"; // 에디터와 테스트 오브젝트 이름에 표시할 몬스터 이름입니다.
    [SerializeField] private MonsterDummyKind3D kind = MonsterDummyKind3D.Stationary; // 정지형 또는 순찰형 같은 더미 몬스터 동작 종류입니다.
    [SerializeField] private int maxHealth = 1; // 더미 몬스터가 공격을 몇 번 버틸 수 있는지 정합니다.
    [SerializeField] private bool canBeFrozen = true; // 셔터 사진으로 이 더미 몬스터를 멈출 수 있는지 정합니다.
    [SerializeField] private bool reactsToFlash = true; // 플래시 기능에 반응해서 색을 바꿀지 정합니다.
    [SerializeField] private bool canRelay; // 릴레이 기능으로 다른 월드에 보낼 수 있는지 정합니다.
    [SerializeField] private Color normalColor = new Color(0.75f, 0.45f, 0.95f, 1f); // 평상시 더미 몬스터 색상입니다.
    [SerializeField] private Color frozenColor = new Color(0.35f, 0.75f, 1f, 1f); // 셔터로 멈췄을 때 더미 몬스터 색상입니다.
    [SerializeField] private Color flashedColor = new Color(1f, 0.95f, 0.35f, 1f); // 플래시에 반응했을 때 더미 몬스터 색상입니다.

    private BoxCollider boxCollider;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Rigidbody body;
    private Material material;
    private WorldVariant3D worldVariant;
    private Vector3 startPosition;
    private float patrolTimer;
    private int currentHealth;
    private float freezeEndTime;
    private bool frozen;
    private bool flashed;

    private void Awake()
    {
        startPosition = TwoPointFiveDUtility3D.ProjectPositionToPlane(transform.position);
        transform.position = startPosition;
        ConfigureVisual();
    }

    private void Update()
    {
        if (frozen && Time.time >= freezeEndTime)
        {
            frozen = false;
            RefreshColor();
        }

        if (!frozen && kind == MonsterDummyKind3D.Patrol)
        {
            patrolTimer += Time.deltaTime;
            Vector3 position = startPosition;
            position.x += Mathf.Sin(patrolTimer * 1.4f) * 0.8f;
            transform.position = TwoPointFiveDUtility3D.ProjectPositionToPlane(position);
        }
    }

    public void Initialize(MonsterDummyProfile3D profile)
    {
        if (profile == null)
        {
            return;
        }

        monsterId = profile.id;
        displayName = profile.displayName;
        kind = profile.kind;
        maxHealth = Mathf.Max(1, profile.health);
        currentHealth = maxHealth;
        canBeFrozen = profile.canBeFrozen;
        reactsToFlash = profile.reactsToFlash;
        canRelay = profile.canRelay;
        normalColor = profile.color;
        startPosition = TwoPointFiveDUtility3D.ProjectPositionToPlane(profile.position);
        patrolTimer = 0f;
        transform.position = startPosition;
        transform.localScale = MonsterRuntime3D.ClampSize(profile.size, 0.05f);
        CameraTagUtility3D.TrySetTag(gameObject, canRelay ? CameraTagUtility3D.RelayTargetTag : CameraTagUtility3D.TargetTag);

        ConfigureVisual();
        EnsureWorldVariant(profile.world, profile.existsInBothWorlds);
        RefreshColor();
    }

    public bool TakeAttack()
    {
        currentHealth = Mathf.Max(0, currentHealth - 1);
        if (currentHealth <= 0)
        {
            gameObject.SetActive(false);
        }

        return true;
    }

    public bool ApplyShutterFreeze(float duration, CameraAbilitySystem3D source)
    {
        if (!canBeFrozen || duration <= 0f)
        {
            return false;
        }

        frozen = true;
        freezeEndTime = Time.time + duration;
        RefreshColor();
        return true;
    }

    public bool OnCameraFlash(CameraAbilitySystem3D source)
    {
        if (!reactsToFlash)
        {
            return false;
        }

        flashed = !flashed;
        RefreshColor();
        return true;
    }

    public bool TryRelayToWorld(ResearchWorldId targetWorld, CameraAbilitySystem3D source)
    {
        if (!canRelay)
        {
            return false;
        }

        if (worldVariant == null)
        {
            EnsureWorldVariant(WorldSystem3D.ActiveWorld, false);
        }

        worldVariant.SetActiveWorld(targetWorld);
        WorldSystem3D.EnsureInstance().RefreshWorldObjects();
        return true;
    }

    private void ConfigureVisual()
    {
        MonsterRuntime3D.ConfigureKinematicBox(
            gameObject,
            transform.localScale,
            normalColor,
            $"Generated {displayName} Material",
            ref boxCollider,
            ref meshFilter,
            ref meshRenderer,
            ref body,
            ref material
        );
    }

    private void EnsureWorldVariant(ResearchWorldId world, bool existsInBothWorlds)
    {
        worldVariant = GetComponent<WorldVariant3D>();
        if (worldVariant == null)
        {
            worldVariant = gameObject.AddComponent<WorldVariant3D>();
        }

        worldVariant.ExistsInBothWorlds = existsInBothWorlds;
        if (!existsInBothWorlds)
        {
            worldVariant.SetActiveWorld(world);
        }
        else
        {
            worldVariant.Refresh(WorldSystem3D.ActiveWorld);
        }
    }

    private void RefreshColor()
    {
        if (material == null)
        {
            return;
        }

        material.color = frozen ? frozenColor : flashed ? flashedColor : normalColor;
    }
}
