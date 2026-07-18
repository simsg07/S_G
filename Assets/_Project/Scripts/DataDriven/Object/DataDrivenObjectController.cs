using UnityEngine;

[DisallowMultipleComponent]
public class DataDrivenObjectController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private ObjectData objectData;
    [SerializeField] private bool applyOnAwake = true;
    [SerializeField] private bool applyOnStart;

    [Header("Target Components")]
    [SerializeField] private DamageDealer damageDealer;
    [SerializeField] private HitReceiver hitReceiver;
    [SerializeField] private GravityObject3D gravityObject;
    [SerializeField] private PausablePhysicsObject pausablePhysicsObject;
    [SerializeField] private BreakableObject3D breakableObject;
    [SerializeField] private OpenPathOnBreak openPathOnBreak;
    [SerializeField] private ConnectedObjectLink connectedObjectLink;
    [SerializeField] private TriggerZone3D triggerZone;
    [SerializeField] private BlockObject blockObject;
    [SerializeField] private GravityDropSensor gravityDropSensor;
    [SerializeField] private StoneObject stoneObject;
    [SerializeField] private FallingBoxObject fallingBoxObject;

    [Header("Debug")]
    [SerializeField] private bool debugMode;

    private void Reset()
    {
        AutoFill();
    }

    private void Awake()
    {
        AutoFill();
        if (applyOnAwake)
        {
            ApplyData();
        }
    }

    private void Start()
    {
        if (applyOnStart)
        {
            ApplyData();
        }
    }

    private void OnValidate()
    {
        AutoFill();
    }

    [ContextMenu("Apply Object Data")]
    public void ApplyData()
    {
        AutoFill();

        if (objectData == null)
        {
            Debug.LogWarning("[DataDrivenObjectController] ObjectData is not assigned.", this);
            return;
        }

        ApplyHitReceiver();
        ApplyDamageDealer();
        ApplyGravityObject();
        ApplyPausablePhysicsObject();
        ApplyBreakableObject();
        ApplyTriggerZone();
        ApplyBlockObject();
        ApplyGravityDropSensor();
        ApplyGravitySpecificObject();
        ApplyObjectSpecificData();

        Log($"Applied ObjectData: {objectData.displayName} ({objectData.objectKind})");
    }

    [ContextMenu("Validate Object Data Setup")]
    public void ValidateObjectDataSetup()
    {
        AutoFill();

        Log($"ObjectData: {(objectData != null ? objectData.name : "None")}");
        LogComponent("DamageDealer", damageDealer);
        LogComponent("HitReceiver", hitReceiver);
        LogComponent("GravityObject3D", gravityObject);
        LogComponent("PausablePhysicsObject", pausablePhysicsObject);
        LogComponent("BreakableObject3D", breakableObject);
        LogComponent("OpenPathOnBreak", openPathOnBreak);
        LogComponent("ConnectedObjectLink", connectedObjectLink);
        LogComponent("TriggerZone3D", triggerZone);
        LogComponent("BlockObject", blockObject);
        LogComponent("GravityDropSensor", gravityDropSensor);
        LogComponent("StoneObject", stoneObject);
        LogComponent("FallingBoxObject", fallingBoxObject);

        if (objectData == null)
        {
            Debug.LogWarning("[DataDrivenObjectController] Warning: ObjectData not assigned.", this);
        }

        Debug.Log("[DataDrivenObjectController] Validate complete. Missing optional components are warnings only.", this);
    }

    private void AutoFill()
    {
        if (damageDealer == null)
        {
            damageDealer = GetComponent<DamageDealer>();
        }

        if (hitReceiver == null)
        {
            hitReceiver = GetComponent<HitReceiver>();
        }

        if (gravityObject == null)
        {
            gravityObject = GetComponent<GravityObject3D>();
        }

        if (pausablePhysicsObject == null)
        {
            pausablePhysicsObject = GetComponent<PausablePhysicsObject>();
        }

        if (breakableObject == null)
        {
            breakableObject = GetComponent<BreakableObject3D>();
        }

        if (openPathOnBreak == null)
        {
            openPathOnBreak = GetComponent<OpenPathOnBreak>();
        }

        if (connectedObjectLink == null)
        {
            connectedObjectLink = GetComponent<ConnectedObjectLink>();
        }

        if (triggerZone == null)
        {
            triggerZone = GetComponent<TriggerZone3D>();
        }

        if (blockObject == null)
        {
            blockObject = GetComponent<BlockObject>();
        }

        if (stoneObject == null)
        {
            stoneObject = GetComponent<StoneObject>();
        }

        if (gravityDropSensor == null)
        {
            gravityDropSensor = GetComponent<GravityDropSensor>();
        }

        if (fallingBoxObject == null)
        {
            fallingBoxObject = GetComponent<FallingBoxObject>();
        }
    }

    private void ApplyHitReceiver()
    {
        if (hitReceiver == null)
        {
            if (!objectData.canBeTargeted &&
                objectData.breakMode != ObjectBreakMode.OnMaxHit &&
                objectData.onMaxHitAction == ObjectTriggerAction.None)
            {
                return;
            }

            WarnMissing(nameof(HitReceiver));
            return;
        }

        hitReceiver.ConfigureHitRules(
            objectData.maxHitCount,
            objectData.canBeTargeted,
            objectData.acceptGenericHit,
            objectData.acceptEyeballFlyAttack,
            objectData.acceptBoomberContact,
            objectData.acceptBoomberExplosion,
            objectData.acceptMonsterAttack);
    }

    private void ApplyDamageDealer()
    {
        if (damageDealer == null)
        {
            if (objectData.damage <= 0)
            {
                return;
            }

            WarnMissing(nameof(DamageDealer));
            return;
        }

        damageDealer.ConfigureDamage(objectData.damage, ~0, objectData.damageOncePerTarget, HitSourceType.Environment);
        damageDealer.ConfigureDebug(objectData.debugMode);
    }

    private void ApplyGravityObject()
    {
        if (gravityObject == null)
        {
            if (!objectData.useGravity)
            {
                return;
            }

            WarnMissing(nameof(GravityObject3D));
            return;
        }

        gravityObject.ConfigureGravity(
            objectData.startAttached,
            objectData.disableGravityOnStart,
            objectData.useGravity ? 0f : objectData.dropSpeed,
            objectData.lockXWhileFalling,
            objectData.lockZPosition,
            objectData.debugMode);
    }

    private void ApplyPausablePhysicsObject()
    {
        if (pausablePhysicsObject == null)
        {
            if (!objectData.canPauseByCamera && !objectData.canPauseByShutter)
            {
                return;
            }

            WarnMissing(nameof(PausablePhysicsObject));
            return;
        }

        bool canPause = objectData.canPauseByCamera || objectData.canPauseByShutter;
        pausablePhysicsObject.ConfigurePause(canPause, true, objectData.debugMode);
    }

    private void ApplyBreakableObject()
    {
        if (breakableObject == null)
        {
            if (objectData.breakMode == ObjectBreakMode.None)
            {
                return;
            }

            WarnMissing(nameof(BreakableObject3D));
            return;
        }

        bool destroyObject = objectData.breakMode == ObjectBreakMode.DestroyAfterDelay;
        breakableObject.ConfigureBreakable(
            objectData.disableColliderOnBreak,
            objectData.disableRendererOnBreak,
            destroyObject,
            objectData.destroyDelay,
            objectData.debugMode);
    }

    private void ApplyTriggerZone()
    {
        if (triggerZone == null)
        {
            return;
        }

        triggerZone.ConfigureTrigger(objectData.triggerOnce, objectData.debugMode);
    }

    private void ApplyBlockObject()
    {
        if (blockObject == null)
        {
            if (objectData.blockType == BlockObjectType.DecorationOnly)
            {
                return;
            }

            WarnMissing(nameof(BlockObject));
            return;
        }

        blockObject.ApplyBlockData(objectData);
    }

    private void ApplyGravitySpecificObject()
    {
        switch (objectData.objectKind)
        {
            case ObjectKind.Stone:
                if (stoneObject == null)
                {
                    WarnMissing(nameof(StoneObject));
                    return;
                }

                stoneObject.ApplyStoneData(objectData);
                break;
            case ObjectKind.FallingBox:
                if (fallingBoxObject == null)
                {
                    WarnMissing(nameof(FallingBoxObject));
                    return;
                }

                fallingBoxObject.ApplyBoxData(objectData);
                break;
        }
    }

    private void ApplyGravityDropSensor()
    {
        if (gravityDropSensor == null)
        {
            if (objectData.useGravityDropSensor &&
                (objectData.objectKind == ObjectKind.Stone || objectData.objectKind == ObjectKind.FallingBox))
            {
                WarnMissing(nameof(GravityDropSensor));
            }

            return;
        }

        gravityDropSensor.ApplyDetectionData(objectData);
    }

    private void ApplyObjectSpecificData()
    {
        switch (objectData.objectKind)
        {
            case ObjectKind.Wire:
                WireObject wireObject = GetComponent<WireObject>();
                if (wireObject == null)
                {
                    WarnMissing(nameof(WireObject));
                    return;
                }

                wireObject.ConfigureDataDrivenObject(
                    objectData.maxHitCount,
                    objectData.canBeTargeted,
                    objectData.destroyDelay,
                    objectData.debugMode);
                break;
            case ObjectKind.Vine:
                VineObject vineObject = GetComponent<VineObject>();
                if (vineObject == null)
                {
                    WarnMissing(nameof(VineObject));
                    return;
                }

                vineObject.ConfigureDataDrivenObject(
                    objectData.maxHitCount,
                    objectData.canBeTargeted,
                    objectData.destroyDelay,
                    objectData.debugMode);
                break;
        }
    }

    private void WarnMissing(string componentName)
    {
        if (debugMode || (objectData != null && objectData.debugMode))
        {
            Debug.LogWarning($"[DataDrivenObjectController] {componentName} is missing. Data value skipped.", this);
        }
    }

    private void Log(string message)
    {
        if (debugMode || (objectData != null && objectData.debugMode))
        {
            Debug.Log($"[DataDrivenObjectController] {message}", this);
        }
    }

    private void LogComponent(string label, Object component)
    {
        if (!(debugMode || (objectData != null && objectData.debugMode)))
        {
            return;
        }

        if (component != null)
        {
            Debug.Log($"[DataDrivenObjectController] {label} found: {component.GetType().Name}", this);
            return;
        }

        Debug.LogWarning($"[DataDrivenObjectController] Warning: {label} not assigned. Settings skipped if needed.", this);
    }
}
