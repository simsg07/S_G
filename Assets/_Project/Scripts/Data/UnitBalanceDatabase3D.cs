using UnityEngine;

[CreateAssetMenu(fileName = "UnitBalanceDatabase", menuName = "S_G/Unit Balance Database")]
public class UnitBalanceDatabase3D : ScriptableObject
{
    private const string ResourceName = "UnitBalanceDatabase";
    private static UnitBalanceDatabase3D cachedDatabase;

    [Header("Player")]
    [SerializeField] private PlayerMovementBalance3D playerMovement = new PlayerMovementBalance3D(); // Shared player movement and robot-leg jump tuning.
    [SerializeField] private PlayerAttackAnimationBalance3D playerAttackAnimation = new PlayerAttackAnimationBalance3D(); // Legacy player sprite frame tuning. Combat attack logic is removed.

    [Header("Monster")]
    [SerializeField] private MonsterAni1Balance3D monsterAni1 = new MonsterAni1Balance3D(); // Floating monster-in-tube prop tuning.
    [SerializeField] private SlimeBalance3D slime = new SlimeBalance3D(); // Basic ground slime tuning.
    [SerializeField] private RangedSlimeBalance3D rangedSlime = new RangedSlimeBalance3D(); // Ranged slime prototype tuning.
    [SerializeField] private ThornSlimeBalance3D thornSlime = new ThornSlimeBalance3D(); // Thorn slime prototype tuning.
    [SerializeField] private BalloonSlimeBalance3D balloonSlime = new BalloonSlimeBalance3D(); // Balloon slime prototype tuning.
    [SerializeField] private SlimeHybeBalance3D slimeHybe = new SlimeHybeBalance3D(); // Slime hive prototype tuning.
    [SerializeField] private ProjectileBalance3D projectiles = new ProjectileBalance3D(); // Monster projectile visual and movement tuning.

    public PlayerMovementBalance3D PlayerMovement => playerMovement;
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
    public float moveSpeed = 6f; // Horizontal player movement speed.
    public float gravityScale = 3f; // Gravity multiplier applied to the player.
    public float fallGravityMultiplier = 1.35f; // Extra gravity multiplier while falling.
    public float maxFallSpeed = 18f; // Maximum downward velocity.
    public float coyoteTime = 0.08f; // Grace time after leaving ground where jump is still allowed.
    public float jumpBufferTime = 0.1f; // Time a jump input is buffered before landing.
    public float jumpCutMultiplier = 0.5f; // Upward velocity multiplier when jump is released early.
    public float groundCheckDistance = 0.08f; // Extra distance used for ground checks.
    public float dropThroughDuration = 0.45f; // Minimum time to ignore a drop-through platform.
    public float passThroughClearance = 0.05f; // Clearance required before platform collision is restored.
    public Vector3 colliderSize = new Vector3(0.8f, 1.2f, 1f); // Player body and collider scale.
}

[System.Serializable]
public class PlayerAttackAnimationBalance3D
{
    public string resourceFolder = "PlayerAttack"; // Legacy sprite folder used for the current temporary player visual.
    public string[] frameNames = { "attack_01", "attack_02", "attack_03", "attack_04" }; // Legacy sprite frame names. Combat playback is no longer called.
    public float frameDuration = 0.045f; // Legacy frame duration if prototype playback is manually reused.
    public float pixelsPerUnit = 340f; // Sprite pixels-per-unit scale.
    public Vector3 localOffset = new Vector3(0.2f, 0.04f, -0.45f); // Sprite local offset from the player.
    public int sortingOrder = 30; // Sprite sorting order.
}

[System.Serializable]
public class SlimeBalance3D
{
    public Vector3 bodySize = new Vector3(0.75f, 0.55f, 0.8f); // Basic slime body and collider size.
    public int maxHealth = 4; // Basic slime durability for prototype removal/respawn.
    public float patrolSpeed = 1.15f; // Patrol movement speed.
    public float chaseSpeed = 2.1f; // Chase movement speed.
    public float edgeProbeDistance = 0.16f; // Forward distance used to check platform edges.
    public float groundProbeDepth = 0.16f; // Downward probe depth used near platform edges.
    public float wallProbeDistance = 0.12f; // Forward wall probe distance.
    public float detectionSize = 4.2f; // Square detection range size.
    public float chaseSize = 6.2f; // Square range where chasing remains active.
    public int contactDamage = 1; // Legacy contact damage value. Player damage is currently disabled.
    public float contactDamageCooldown = 0.8f; // Legacy contact damage cooldown.
    public float respawnDelay = 3f; // Respawn delay after prototype defeat.
    public bool showRanges = true; // Shows detection and chase range previews.
    public Color slimeColor = new Color(0.32f, 0.82f, 0.28f, 1f); // Normal slime color.
    public Color chaseColor = new Color(0.6f, 0.96f, 0.35f, 1f); // Chase slime color.
    public Color detectionColor = new Color(1f, 0.08f, 0.04f, 0.18f); // Detection range preview color.
    public Color chaseRangeColor = new Color(1f, 0.15f, 0.78f, 0.13f); // Chase range preview color.
}

[System.Serializable]
public class RangedSlimeBalance3D
{
    public Vector3 slimeSize = new Vector3(0.7f, 0.5f, 0.8f); // Ranged slime body and collider size.
    public float patrolSpeed = 0.85f; // Patrol speed.
    public float edgeProbeDistance = 0.16f; // Forward distance used to check platform edges.
    public float groundProbeDepth = 0.16f; // Downward probe depth near platform edges.
    public float wallProbeDistance = 0.12f; // Forward wall probe distance.
    public float detectionSize = 6f; // Square detection range size.
    public float attackWindupDuration = 0.55f; // Delay before firing the visual projectile.
    public float attackCooldown = 1.4f; // Time before the next projectile can be fired.
    public float respawnDelay = 3f; // Respawn delay after prototype defeat.
    public int projectileDamage = 1; // Legacy projectile damage value. Player damage is currently disabled.
    public float projectileSpeed = 4.8f; // Projectile movement speed.
    public float projectileLifetime = 3f; // Projectile lifetime before it despawns.
    public Vector3 projectileSize = new Vector3(0.28f, 0.28f, 0.8f); // Projectile collider and visual size.
    public bool showRanges = true; // Shows detection range previews.
    public Color slimeColor = new Color(0.15f, 0.75f, 0.95f, 1f); // Normal ranged slime color.
    public Color attackPoseColor = new Color(0.1f, 1f, 1f, 1f); // Windup pose color.
    public Color detectionColor = new Color(0.15f, 0.85f, 1f, 0.16f); // Detection range preview color.
    public Color projectileColor = new Color(0.1f, 0.95f, 1f, 1f); // Projectile color.
}

[System.Serializable]
public class ThornSlimeBalance3D
{
    public Vector3 bodySize = new Vector3(0.58f, 0.38f, 0.8f); // Thorn slime body and collider size.
    public int maxHealth = 3; // Thorn slime durability for prototype removal/respawn.
    public float crawlSpeed = 1.35f; // Crawl speed along surfaces.
    public Vector2 detectionBoxSize = new Vector2(1.35f, 0.75f); // Detection preview box size.
    public float detectionOffset = 0.58f; // Detection box offset from the body.
    public Vector2 spikeBoxSize = new Vector2(1.1f, 0.58f); // Spike attack preview box size.
    public float spikeOffset = 0.62f; // Spike box offset from the body.
    public float attackWindupDuration = 0.28f; // Delay before spike visual becomes active.
    public float attackActiveDuration = 0.22f; // Spike active visual duration.
    public float attackCooldown = 1f; // Time before the next spike pattern.
    public int contactDamage = 1; // Legacy contact damage value. Player damage is currently disabled.
    public int spikeDamage = 1; // Legacy spike damage value. Player damage is currently disabled.
    public float contactDamageCooldown = 0.8f; // Legacy contact damage cooldown.
    public float respawnDelay = 3f; // Respawn delay after prototype defeat.
    public bool showRanges = true; // Shows detection and spike previews.
    public Color slimeColor = new Color(0.18f, 0.85f, 0.35f, 1f); // Normal thorn slime color.
    public Color attackPoseColor = new Color(0.55f, 1f, 0.25f, 1f); // Windup/active color.
    public Color detectionColor = new Color(1f, 0.08f, 0.04f, 0.18f); // Detection range preview color.
    public Color spikeColor = new Color(0.05f, 0.35f, 1f, 0.35f); // Spike range preview color.
}

[System.Serializable]
public class BalloonSlimeBalance3D
{
    public Vector3 bodySize = new Vector3(0.62f, 0.62f, 0.8f); // Balloon slime body and collider size.
    public int maxHealth = 1; // Balloon slime durability for prototype removal/respawn.
    public float detectionSize = 5f; // Square detection range size.
    public float hoverSpeed = 0.35f; // Horizontal hover movement speed.
    public float hoverFrequency = 2.1f; // Hover bob frequency.
    public float hoverBobStrength = 0.12f; // Hover bob amount.
    public float chargeDuration = 1.2f; // Delay before dash movement starts.
    public float dashSpeed = 7.2f; // Enemy dash movement speed.
    public float dashDuration = 0.46f; // Enemy dash duration.
    public float attackCooldown = 0.6f; // Delay before the next enemy dash.
    public int contactDamage = 1; // Legacy contact damage value. Player damage is currently disabled.
    public float respawnDelay = 3f; // Respawn delay after prototype defeat.
    public bool showRanges = true; // Shows detection range previews.
    public Color bodyColor = new Color(0.95f, 0.82f, 0.2f, 1f); // Normal balloon slime color.
    public Color chargeColor = new Color(1f, 0.45f, 0.18f, 1f); // Charge/dash color.
    public Color detectionColor = new Color(1f, 0.08f, 0.04f, 0.16f); // Detection range preview color.
}

[System.Serializable]
public class MonsterAni1Balance3D
{
    public Vector3 tubeSize = new Vector3(2.05f, 3.41f, 0.8f); // Tube display and trigger size.
    public Vector2 innerMonsterSize = new Vector2(0.9f, 2.37f); // Monster image size inside the tube.
    public Vector3 innerMonsterOffset = new Vector3(0.13f, -0.08f, -0.03f); // Monster image offset inside the tube.
    public float floatSpeed = 0.45f; // Monster vertical floating speed.
    public float floatHeight = 0.055f; // Monster vertical floating height.
    public Vector2 bubbleSize = new Vector2(0.55f, 1.45f); // Bubble group display size.
    public Vector3 bubbleOffset = new Vector3(0.13f, 0f, -0.03f); // Bubble group offset inside the tube.
    public float bubbleFloatSpeed = 0.38f; // Bubble vertical floating speed.
    public float bubbleFloatHeight = 0.06f; // Bubble vertical floating height.
    public bool useTriggerCollider = true; // Uses trigger collision instead of blocking the player.
    public Color tubeTintColor = Color.white; // Tube sprite tint.
    public Color innerMonsterTintColor = Color.white; // Monster sprite tint.
    public Color bubbleTintColor = Color.white; // Bubble sprite tint.
}

[System.Serializable]
public class SlimeHybeBalance3D
{
    public Vector3 bodySize = new Vector3(0.95f, 0.62f, 0.8f); // Hive slime body and collider size.
    public int maxHealth = 7; // Hive slime durability for prototype removal/respawn.
    public float wanderSpeed = 0.85f; // Wandering speed.
    public float fleeSpeed = 1.65f; // Speed while moving away from the player.
    public float edgeProbeDistance = 0.18f; // Forward distance used to check platform edges.
    public float groundProbeDepth = 0.16f; // Downward probe depth near platform edges.
    public float wallProbeDistance = 0.12f; // Forward wall probe distance.
    public float detectionSize = 4.8f; // Square detection range size.
    public float fleeDuration = 0.85f; // Flee pattern duration.
    public float shakeDuration = 0.75f; // Shake windup duration before a pattern.
    public float patternCooldown = 1.7f; // Delay before another pattern can begin.
    public int balloonSpawnCount = 2; // Number of balloon slime spawns in the spawn pattern.
    public int mucusProjectileMin = 5; // Minimum mucus projectile count.
    public int mucusProjectileMax = 6; // Maximum mucus projectile count.
    public float mucusProjectileSpeed = 4.2f; // Mucus projectile launch speed.
    public float mucusProjectileLifetime = 2.5f; // Mucus projectile lifetime.
    public float mucusProjectileGravity = 3.5f; // Mucus projectile gravity strength.
    public Vector3 mucusProjectileSize = new Vector3(0.24f, 0.24f, 0.8f); // Mucus projectile collider and visual size.
    public int contactDamage = 1; // Legacy contact damage value. Player damage is currently disabled.
    public float contactDamageCooldown = 0.85f; // Legacy contact damage cooldown.
    public float respawnDelay = 3f; // Respawn delay after prototype defeat.
    public bool showRanges = true; // Shows detection range previews.
    public Color bodyColor = new Color(0.58f, 0.72f, 0.18f, 1f); // Normal hive slime color.
    public Color alertColor = new Color(0.78f, 0.88f, 0.25f, 1f); // Alert/pattern windup color.
    public Color detectionColor = new Color(1f, 0.08f, 0.04f, 0.17f); // Detection range preview color.
    public Color mucusColor = new Color(0.55f, 0.95f, 0.25f, 1f); // Mucus projectile color.
}

[System.Serializable]
public class ProjectileBalance3D
{
    public Vector3 rangedProjectileSize = new Vector3(0.28f, 0.28f, 0.8f); // Default ranged projectile size.
    public int rangedProjectileDamage = 1; // Legacy ranged projectile damage value. Player damage is currently disabled.
    public float rangedProjectileLifetime = 3f; // Default ranged projectile lifetime.
    public Color rangedProjectileColor = new Color(0.1f, 0.95f, 1f, 1f); // Default ranged projectile color.
    public Vector3 mucusProjectileSize = new Vector3(0.24f, 0.24f, 0.8f); // Default mucus projectile size.
    public int mucusProjectileDamage = 1; // Legacy mucus projectile damage value. Player damage is currently disabled.
    public float mucusProjectileLifetime = 2.5f; // Default mucus projectile lifetime.
    public float mucusProjectileGravity = 3.5f; // Default mucus projectile gravity strength.
    public Color mucusProjectileColor = new Color(0.5f, 0.95f, 0.25f, 1f); // Default mucus projectile color.
}
