using UnityEngine;

public enum ObjectKind
{
    None,
    Stone,
    FallingBox,
    Wire,
    Vine,
    UnstableTile,
    CircleSpike,
    Crane,
    CraneSwitch
}

public enum ObjectBreakMode
{
    None,
    OnMaxHit,
    OnGroundHit,
    DisableOnly,
    DestroyAfterDelay,
    OpenPath
}

public enum ObjectTriggerAction
{
    None,
    ActivateConnectedObject,
    OpenPath,
    DropObject,
    BreakObject,
    DisableSelf,
    EnableTarget
}

[CreateAssetMenu(menuName = "_Project/Data/Object Data", fileName = "ObjectData")]
public class ObjectData : ScriptableObject
{
    [Header("Identity")]
    public string objectId;
    public string displayName;
    public ObjectKind objectKind;

    [Header("Target / Hit")]
    public bool canBeTargeted = true;
    public int maxHitCount = 1;
    public bool triggerOnce = true;
    public bool acceptGenericHit = true;
    public bool acceptEyeballFlyAttack;
    public bool acceptBoomberContact;
    public bool acceptBoomberExplosion;
    public bool acceptMonsterAttack;

    [Header("Damage")]
    public int damage = 1;
    public bool canDamagePlayer = true;
    public bool canDamageMonster = true;
    public bool damageOncePerTarget = true;

    [Header("Gravity")]
    public bool useGravity;
    public bool startAttached = true;
    public bool disableGravityOnStart = true;
    public float dropSpeed;
    public bool lockXWhileFalling = true;
    public bool lockZPosition = true;
    public bool breakOnGroundHit;
    public bool remainAsPlatformOnGround;
    public bool instantKillPlayerOnFallingHit = true;

    [Header("Gravity Detection")]
    public bool useGravityDropSensor = true;
    public Vector3 gravityDetectionCenterOffset = new Vector3(0f, -2f, 0f);
    public Vector3 gravityDetectionBoxSize = new Vector3(3f, 4f, 1f);
    public bool gravityDetectOnlyOnce = true;
    public bool usePlayerTagFallback = true;
    public string playerTag = "Player";

    [Header("Pause")]
    public bool canPauseByCamera;
    public bool canPauseByShutter;
    public bool becomePlatformWhenPaused;

    [Header("Break")]
    public ObjectBreakMode breakMode = ObjectBreakMode.None;
    public float breakDelay;
    public float destroyDelay = 0.3f;
    public bool disableColliderOnBreak = true;
    public bool disableRendererOnBreak;

    [Header("Block")]
    public BlockObjectType blockType = BlockObjectType.DecorationOnly;
    public bool canBlockPlayer = true;
    public bool canBlockMonster = true;
    public bool canBlockSight;
    public bool canBlockLight;
    public bool removeColliderOnBreak = true;
    public bool hideVisualOnBreak;
    public bool delayHideVisual = true;
    public float visualHideDelay = 0.25f;
    public bool clearPlayerOverlapOnBreak = true;
    public float safePushDistance = 0.25f;

    [Header("Connection")]
    public ObjectTriggerAction onHitAction = ObjectTriggerAction.None;
    public ObjectTriggerAction onMaxHitAction = ObjectTriggerAction.None;
    public ObjectTriggerAction onBreakAction = ObjectTriggerAction.None;

    [Header("Debug")]
    public bool debugMode = true;

    private void OnValidate()
    {
        maxHitCount = Mathf.Max(1, maxHitCount);
        damage = Mathf.Max(0, damage);
        dropSpeed = Mathf.Max(0f, dropSpeed);
        breakDelay = Mathf.Max(0f, breakDelay);
        destroyDelay = Mathf.Max(0f, destroyDelay);
        visualHideDelay = Mathf.Max(0f, visualHideDelay);
        safePushDistance = Mathf.Max(0f, safePushDistance);
        gravityDetectionBoxSize.x = Mathf.Max(0.01f, gravityDetectionBoxSize.x);
        gravityDetectionBoxSize.y = Mathf.Max(0.01f, gravityDetectionBoxSize.y);
        gravityDetectionBoxSize.z = Mathf.Max(0.01f, gravityDetectionBoxSize.z);
    }
}
