using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public class WorldPresence : MonoBehaviour
{
    [Header("World Presence")]
    [SerializeField] private WorldPresenceMode presenceMode = WorldPresenceMode.Both;
    [SerializeField] private bool applyOnStart = true;
    [SerializeField, HideInInspector] private HiddenWorldSimulationPolicy hiddenWorldSimulationPolicy = HiddenWorldSimulationPolicy.PauseWhenHidden;

    [Header("Controlled Components")]
    [FormerlySerializedAs("affectRenderers")]
    [SerializeField] private bool autoCollectRenderers = true;
    [FormerlySerializedAs("affectColliders")]
    [SerializeField] private bool autoCollectColliders = true;
    [FormerlySerializedAs("affectRigidbody")]
    [SerializeField] private bool autoCollectRigidbodies = true;
    [SerializeField] private bool autoCollectAnimators = true;
    [SerializeField] private bool disableControlledBehavioursWhenAbsent = true;
    [SerializeField] private bool autoCollectMonsterBehaviours = true;

    [FormerlySerializedAs("targetRenderers")]
    [SerializeField] private Renderer[] controlledRenderers = Array.Empty<Renderer>();
    [FormerlySerializedAs("targetColliders")]
    [SerializeField] private Collider[] controlledColliders = Array.Empty<Collider>();
    [FormerlySerializedAs("targetBehaviours")]
    [SerializeField] private MonoBehaviour[] controlledBehaviours = Array.Empty<MonoBehaviour>();
    [FormerlySerializedAs("targetRigidbody")]
    [SerializeField] private Rigidbody primaryRigidbody;
    [SerializeField] private Rigidbody[] controlledRigidbodies = Array.Empty<Rigidbody>();
    [SerializeField] private Animator[] controlledAnimators = Array.Empty<Animator>();
    [SerializeField] private Light[] controlledLights = Array.Empty<Light>();

    [Header("Debug")]
    [SerializeField] private bool debugMode;
    [SerializeField] private bool showGizmos = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [SerializeField] private bool runtimePausedByWorldPolicy;
    [SerializeField] private bool runtimePausedByShutter;
#endif

    // Kept only so old prefabs that serialized this value do not lose data noisily.
    // WorldPresence never uses root GameObject.SetActive anymore.
    [FormerlySerializedAs("affectRootActive")]
    [SerializeField, HideInInspector] private bool legacyAffectRootActive;
    [FormerlySerializedAs("visualRoot")]
    [SerializeField, HideInInspector] private GameObject legacyVisualRoot;
    [FormerlySerializedAs("affectBehaviours")]
    [SerializeField, HideInInspector] private bool legacyAffectBehaviours;

    private RendererState[] rendererStates = Array.Empty<RendererState>();
    private ColliderState[] colliderStates = Array.Empty<ColliderState>();
    private BehaviourState[] behaviourStates = Array.Empty<BehaviourState>();
    private RigidbodyState[] rigidbodyStates = Array.Empty<RigidbodyState>();
    private Vector3[] suspendedLinearVelocities = Array.Empty<Vector3>();
    private Vector3[] suspendedAngularVelocities = Array.Empty<Vector3>();
    private bool[] hasSuspendedRigidbodyState = Array.Empty<bool>();
    private bool[] suspendedIsKinematic = Array.Empty<bool>();
    private bool[] suspendedUseGravity = Array.Empty<bool>();
    private bool[] suspendedDetectCollisions = Array.Empty<bool>();
    private RigidbodyConstraints[] suspendedConstraints = Array.Empty<RigidbodyConstraints>();
    private RigidbodyInterpolation[] suspendedInterpolation = Array.Empty<RigidbodyInterpolation>();
    private bool[] suspendedColliderEnabled = Array.Empty<bool>();
    private bool[] hasSuspendedColliderState = Array.Empty<bool>();
    private bool[] suspendedBehaviourEnabled = Array.Empty<bool>();
    private bool[] hasSuspendedBehaviourState = Array.Empty<bool>();
    private bool[] suspendedAnimatorEnabled = Array.Empty<bool>();
    private bool[] hasSuspendedAnimatorState = Array.Empty<bool>();
    private AnimatorState[] animatorStates = Array.Empty<AnimatorState>();
    private LightState[] lightStates = Array.Empty<LightState>();
    private bool referencesCached;
    private bool originalStatesCached;
    private WorldState lastAppliedWorld = WorldState.WorldA;
    private bool hasAppliedPresence;
    private bool worldEventsSubscribed;
    private MonsterWorldSimulationGate3D monsterSimulationGate;
    private IShutterFreezable3D shutterFreezable;

    public WorldPresenceMode PresenceMode => presenceMode;
    public bool IsPresentInCurrentWorld { get; private set; } = true;

    /// <summary>Overrides this instance only; prefab asset data is never changed.</summary>
    public void SetPresenceMode(WorldPresenceMode mode)
    {
        presenceMode = mode;
    }

    /// <summary>
    /// Configures a newly-added runtime adapter to gate the whole spawned hierarchy.
    /// Existing prefab WorldPresence components keep their authored target lists.
    /// </summary>
    public void ConfigureRuntimeAdapter(WorldPresenceMode mode)
    {
        presenceMode = mode;
        autoCollectRenderers = true;
        autoCollectColliders = true;
        autoCollectRigidbodies = true;
        autoCollectAnimators = true;
        disableControlledBehavioursWhenAbsent = true;
        autoCollectMonsterBehaviours = false;
        controlledBehaviours = FilterControlledBehaviours(GetComponentsInChildren<MonoBehaviour>(true));
        referencesCached = false;
        originalStatesCached = false;
        RefreshReferences();
        CacheOriginalStates();
    }

    public void ConfigureHiddenWorldSimulation(HiddenWorldSimulationPolicy policy, GameObject monsterRoot)
    {
        hiddenWorldSimulationPolicy = policy;
        monsterSimulationGate = null;
        if (policy == HiddenWorldSimulationPolicy.ContinueMonsterLogic && monsterRoot != null)
        {
            monsterSimulationGate = monsterRoot.GetComponent<MonsterWorldSimulationGate3D>();
            if (monsterSimulationGate == null) monsterSimulationGate = monsterRoot.AddComponent<MonsterWorldSimulationGate3D>();
        }
        hasAppliedPresence = false;
    }

    private void Awake()
    {
        WorldPresenceRegistry.Register(this);
        EnsureMonsterSimulationGate();
        RefreshReferences();
        CacheOriginalStates();
    }

    private void OnEnable()
    {
        WorldPresenceRegistry.Register(this);
        EnsureMonsterSimulationGate();
        SubscribeWorldChanged();

        if (Application.isPlaying && applyOnStart)
        {
            // Other components on the same object may restore their Rigidbody in OnEnable.
            // Force the final world state after those initializers have run.
            hasAppliedPresence = false;
            ApplyCurrentWorld();
        }
    }

    private void Start()
    {
        if (Application.isPlaying && applyOnStart)
        {
            ApplyCurrentWorld();
        }
    }

    private void OnDisable()
    {
        UnsubscribeWorldChanged();
    }

    private void OnDestroy()
    {
        monsterSimulationGate?.RemovePresence(this);
        WorldPresenceRegistry.Unregister(this);
        UnsubscribeWorldChanged();
    }

    private void OnValidate()
    {
        RefreshReferences();

        // Do not apply presence in edit mode. Doing so can serialize disabled
        // renderers/colliders as the "original" state and prevent restoration.
    }

    [ContextMenu("Refresh References")]
    public void RefreshReferences()
    {
        controlledRenderers = autoCollectRenderers
            ? MergeComponents(GetComponentsInChildren<Renderer>(true), controlledRenderers)
            : RemoveMissing(controlledRenderers);

        controlledColliders = autoCollectColliders
            ? MergeComponents(GetComponentsInChildren<Collider>(true), controlledColliders)
            : RemoveMissing(controlledColliders);

        controlledRigidbodies = ResolveRigidbodies();
        controlledAnimators = autoCollectAnimators
            ? MergeComponents(GetComponentsInChildren<Animator>(true), controlledAnimators)
            : RemoveMissing(controlledAnimators);
        controlledLights = MergeComponents(GetComponentsInChildren<Light>(true), controlledLights);

        controlledBehaviours = ResolveControlledBehaviours();
        referencesCached = true;

        if (!Application.isPlaying)
        {
            originalStatesCached = false;
            CacheOriginalStates();
        }
    }

    public bool IsPresentInWorld(WorldState currentWorld)
    {
        return presenceMode == WorldPresenceMode.Both ||
               (presenceMode == WorldPresenceMode.WorldAOnly && currentWorld == WorldState.WorldA) ||
               (presenceMode == WorldPresenceMode.WorldBOnly && currentWorld == WorldState.WorldB);
    }

    public bool IsPresentInWorld(ResearchWorldId currentWorld)
    {
        return IsPresentInWorld(currentWorld == ResearchWorldId.WorldA ? WorldState.WorldA : WorldState.WorldB);
    }

    public bool IsPresentInWorld(TimelineWorldState currentWorld)
    {
        return IsPresentInWorld(currentWorld == TimelineWorldState.WorldA_Current ? WorldState.WorldA : WorldState.WorldB);
    }

    public bool IsHiddenByCurrentWorld()
    {
        if (presenceMode == WorldPresenceMode.Both) return false;
        if (WorldManager.Instance != null) return !IsPresentInWorld(WorldManager.Instance.CurrentWorld);
        return !IsPresentInWorld(WorldSystem3D.ActiveWorld);
    }

    public void ApplyWorldState(WorldState currentWorld)
    {
        lastAppliedWorld = currentWorld;
        SetPresenceEnabled(IsPresentInWorld(currentWorld));
    }

    public void ApplyWorldState(ResearchWorldId currentWorld)
    {
        ApplyWorldState(currentWorld == ResearchWorldId.WorldA ? WorldState.WorldA : WorldState.WorldB);
    }

    public void ApplyWorldState(TimelineWorldState currentWorld)
    {
        ApplyWorldState(currentWorld == TimelineWorldState.WorldA_Current ? WorldState.WorldA : WorldState.WorldB);
    }

    public void SetPresenceEnabled(bool present)
    {
        // Both-world objects never enter the hidden-world policy. In particular,
        // do not call EnsureCached here: a world switch can arrive while the
        // shutter owns isKinematic/useGravity/Behaviour.enabled, and caching
        // those temporary values would turn the freeze into the new base state.
        if (presenceMode == WorldPresenceMode.Both)
        {
            hasAppliedPresence = true;
            IsPresentInCurrentWorld = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            runtimePausedByWorldPolicy = false;
            runtimePausedByShutter = shutterFreezable != null && shutterFreezable.IsShutterFrozen;
#endif
            return;
        }

        EnsureCached();
        if (hasAppliedPresence && IsPresentInCurrentWorld == present) return;
        hasAppliedPresence = true;
        IsPresentInCurrentWorld = present;
        if (!present && !IsMarkOverlayActive()) CaptureRuntimeStateBeforeHide();

        int disabledRenderers = ApplyRendererStates(present);
        int disabledLights = ApplyLightStates(present);
        bool continueMonsterLogic = hiddenWorldSimulationPolicy == HiddenWorldSimulationPolicy.ContinueMonsterLogic;
        int disabledColliders = continueMonsterLogic ? 0 : ApplyColliderStates(present);
        int disabledBehaviours = !continueMonsterLogic && disableControlledBehavioursWhenAbsent
            ? ApplyBehaviourStates(present, IsMonsterDead())
            : 0;
        int stoppedRigidbodies = continueMonsterLogic ? 0 : ApplyRigidbodyStates(present);
        int disabledAnimators = continueMonsterLogic ? 0 : ApplyAnimatorStates(present);
        if (continueMonsterLogic) monsterSimulationGate?.SetPresence(this, present);
        if (present && shutterFreezable != null && shutterFreezable.IsShutterFrozen)
        {
            // WorldPresence owns the base state. The temporary shutter layer is
            // always applied last and never becomes WorldPresence's snapshot.
            shutterFreezable.ReapplyShutterFreeze();
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        runtimePausedByWorldPolicy = !present && !continueMonsterLogic;
        runtimePausedByShutter = shutterFreezable != null && shutterFreezable.IsShutterFrozen;
#endif

        if (debugMode && Application.isPlaying)
        {
            Debug.Log(
                $"[WorldPresence] {name} world={lastAppliedWorld}, mode={presenceMode}, present={present}, " +
                $"renderersOff={disabledRenderers}, collidersOff={disabledColliders}, behavioursOff={disabledBehaviours}, " +
                $"rigidbodiesStopped={stoppedRigidbodies}, animatorsOff={disabledAnimators}, lightsOff={disabledLights}",
                this);
        }
    }

    public void ReapplyCurrentWorldPolicy()
    {
        hasAppliedPresence = false;
        ApplyCurrentWorld();
    }

    private void SubscribeWorldChanged()
    {
        if (worldEventsSubscribed) return;
        WorldManager.WorldChanged += HandleTimelineWorldChanged;
        WorldSystem3D.ActiveWorldChanged += HandleResearchWorldChanged;
        worldEventsSubscribed = true;
    }

    private void UnsubscribeWorldChanged()
    {
        if (!worldEventsSubscribed) return;
        WorldManager.WorldChanged -= HandleTimelineWorldChanged;
        WorldSystem3D.ActiveWorldChanged -= HandleResearchWorldChanged;
        worldEventsSubscribed = false;
    }

    private void ApplyCurrentWorld()
    {
        if (WorldManager.Instance != null)
        {
            ApplyWorldState(WorldManager.Instance.CurrentWorld);
            return;
        }

        ApplyWorldState(WorldSystem3D.ActiveWorld);
    }

    private void HandleTimelineWorldChanged(TimelineWorldState previousWorld, TimelineWorldState nextWorld)
    {
        ApplyWorldState(nextWorld);
    }

    private void HandleResearchWorldChanged(ResearchWorldId previousWorld, ResearchWorldId nextWorld)
    {
        ApplyWorldState(nextWorld);
    }

    private void EnsureCached()
    {
        if (!referencesCached)
        {
            RefreshReferences();
        }

        if (!originalStatesCached)
        {
            CacheOriginalStates();
        }
    }

    private void EnsureMonsterSimulationGate()
    {
        if (hiddenWorldSimulationPolicy != HiddenWorldSimulationPolicy.ContinueMonsterLogic
            || monsterSimulationGate != null)
        {
            return;
        }

        MonsterAIBase monster = GetComponentInParent<MonsterAIBase>();
        if (monster == null) return;
        monsterSimulationGate = monster.GetComponent<MonsterWorldSimulationGate3D>();
        if (monsterSimulationGate == null && Application.isPlaying)
        {
            monsterSimulationGate = monster.gameObject.AddComponent<MonsterWorldSimulationGate3D>();
        }
    }

    private void CacheOriginalStates()
    {
        CacheShutterFreezable();
        rendererStates = new RendererState[controlledRenderers != null ? controlledRenderers.Length : 0];
        for (int i = 0; i < rendererStates.Length; i++)
        {
            Renderer target = controlledRenderers[i];
            rendererStates[i] = new RendererState(target, target != null && target.enabled);
        }

        colliderStates = new ColliderState[controlledColliders != null ? controlledColliders.Length : 0];
        suspendedColliderEnabled = new bool[colliderStates.Length];
        hasSuspendedColliderState = new bool[colliderStates.Length];
        for (int i = 0; i < colliderStates.Length; i++)
        {
            Collider target = controlledColliders[i];
            colliderStates[i] = new ColliderState(target, target != null && target.enabled);
        }

        behaviourStates = new BehaviourState[controlledBehaviours != null ? controlledBehaviours.Length : 0];
        suspendedBehaviourEnabled = new bool[behaviourStates.Length];
        hasSuspendedBehaviourState = new bool[behaviourStates.Length];
        for (int i = 0; i < behaviourStates.Length; i++)
        {
            MonoBehaviour target = controlledBehaviours[i];
            behaviourStates[i] = new BehaviourState(target, target != null && target.enabled, IsMonsterBehaviour(target));
        }

        rigidbodyStates = new RigidbodyState[controlledRigidbodies != null ? controlledRigidbodies.Length : 0];
        suspendedLinearVelocities = new Vector3[rigidbodyStates.Length];
        suspendedAngularVelocities = new Vector3[rigidbodyStates.Length];
        hasSuspendedRigidbodyState = new bool[rigidbodyStates.Length];
        suspendedIsKinematic = new bool[rigidbodyStates.Length];
        suspendedUseGravity = new bool[rigidbodyStates.Length];
        suspendedDetectCollisions = new bool[rigidbodyStates.Length];
        suspendedConstraints = new RigidbodyConstraints[rigidbodyStates.Length];
        suspendedInterpolation = new RigidbodyInterpolation[rigidbodyStates.Length];
        for (int i = 0; i < rigidbodyStates.Length; i++)
        {
            Rigidbody target = controlledRigidbodies[i];
            rigidbodyStates[i] = new RigidbodyState(
                target,
                target != null && target.isKinematic,
                target != null && target.useGravity,
                target != null && target.detectCollisions,
                target != null ? target.constraints : RigidbodyConstraints.None,
                target != null ? target.interpolation : RigidbodyInterpolation.None);
        }

        animatorStates = new AnimatorState[controlledAnimators != null ? controlledAnimators.Length : 0];
        suspendedAnimatorEnabled = new bool[animatorStates.Length];
        hasSuspendedAnimatorState = new bool[animatorStates.Length];
        for (int i = 0; i < animatorStates.Length; i++)
        {
            Animator target = controlledAnimators[i];
            animatorStates[i] = new AnimatorState(target, target != null && target.enabled);
        }

        lightStates = new LightState[controlledLights != null ? controlledLights.Length : 0];
        for (int i = 0; i < lightStates.Length; i++)
        {
            Light target = controlledLights[i];
            lightStates[i] = new LightState(target, target != null && target.enabled);
        }

        originalStatesCached = true;
    }

    private void CacheShutterFreezable()
    {
        shutterFreezable = null;
        MonoBehaviour[] candidates = GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < candidates.Length; i++)
        {
            if (!(candidates[i] is IShutterFreezable3D candidate)) continue;
            shutterFreezable = candidate;
            return;
        }
    }

    private int ApplyRendererStates(bool present)
    {
        int disabledCount = 0;
        for (int i = 0; i < rendererStates.Length; i++)
        {
            Renderer target = rendererStates[i].Renderer;
            if (target == null)
            {
                continue;
            }

            bool nextEnabled = present && rendererStates[i].OriginalEnabled;
            if (!nextEnabled && target.enabled)
            {
                disabledCount++;
            }

            target.enabled = nextEnabled;
        }

        return disabledCount;
    }

    private int ApplyColliderStates(bool present)
    {
        int disabledCount = 0;
        for (int i = 0; i < colliderStates.Length; i++)
        {
            Collider target = colliderStates[i].Collider;
            if (target == null)
            {
                continue;
            }

            bool baseEnabled = hasSuspendedColliderState[i]
                ? suspendedColliderEnabled[i]
                : colliderStates[i].OriginalEnabled;
            bool nextEnabled = present && baseEnabled;
            if (!nextEnabled && target.enabled)
            {
                disabledCount++;
            }

            target.enabled = nextEnabled;
            if (present) hasSuspendedColliderState[i] = false;
        }

        return disabledCount;
    }

    private int ApplyBehaviourStates(bool present, bool monsterIsDead)
    {
        int disabledCount = 0;
        for (int i = 0; i < behaviourStates.Length; i++)
        {
            MonoBehaviour target = behaviourStates[i].Behaviour;
            if (target == null || target == this)
            {
                continue;
            }

            if (present && monsterIsDead && behaviourStates[i].IsMonsterBehaviour)
            {
                continue;
            }

            bool baseEnabled = hasSuspendedBehaviourState[i]
                ? suspendedBehaviourEnabled[i]
                : behaviourStates[i].OriginalEnabled;
            bool nextEnabled = present && baseEnabled;
            if (!nextEnabled && target.enabled)
            {
                disabledCount++;
            }

            target.enabled = nextEnabled;
            if (present) hasSuspendedBehaviourState[i] = false;
        }

        return disabledCount;
    }

    private int ApplyRigidbodyStates(bool present)
    {
        int stoppedCount = 0;
        for (int i = 0; i < rigidbodyStates.Length; i++)
        {
            Rigidbody target = rigidbodyStates[i].Rigidbody;
            if (target == null)
            {
                continue;
            }

            if (present)
            {
                bool hasRuntimeState = hasSuspendedRigidbodyState[i];
                target.constraints = hasRuntimeState ? suspendedConstraints[i] : rigidbodyStates[i].OriginalConstraints;
                target.interpolation = hasRuntimeState ? suspendedInterpolation[i] : rigidbodyStates[i].OriginalInterpolation;
                target.isKinematic = hasRuntimeState ? suspendedIsKinematic[i] : rigidbodyStates[i].OriginalIsKinematic;
                target.useGravity = hasRuntimeState ? suspendedUseGravity[i] : rigidbodyStates[i].OriginalUseGravity;
                target.detectCollisions = hasRuntimeState ? suspendedDetectCollisions[i] : rigidbodyStates[i].OriginalDetectCollisions;
                if (hasSuspendedRigidbodyState[i] && !target.isKinematic)
                {
                    target.linearVelocity = suspendedLinearVelocities[i];
                    target.angularVelocity = suspendedAngularVelocities[i];
                }
                hasSuspendedRigidbodyState[i] = false;
                continue;
            }

            StopDynamicMotion(target);
            target.detectCollisions = false;
            target.isKinematic = true;
            target.useGravity = false;
            stoppedCount++;
        }

        return stoppedCount;
    }

    private static void StopDynamicMotion(Rigidbody target)
    {
        if (target == null || target.isKinematic)
        {
            return;
        }

        target.linearVelocity = Vector3.zero;
        target.angularVelocity = Vector3.zero;
    }

    private int ApplyAnimatorStates(bool present)
    {
        int disabledCount = 0;
        for (int i = 0; i < animatorStates.Length; i++)
        {
            Animator target = animatorStates[i].Animator;
            if (target == null)
            {
                continue;
            }

            bool baseEnabled = hasSuspendedAnimatorState[i]
                ? suspendedAnimatorEnabled[i]
                : animatorStates[i].OriginalEnabled;
            bool nextEnabled = present && baseEnabled;
            if (!nextEnabled && target.enabled)
            {
                disabledCount++;
            }

            target.enabled = nextEnabled;
            if (present) hasSuspendedAnimatorState[i] = false;
        }

        return disabledCount;
    }

    private bool IsMarkOverlayActive()
    {
        return shutterFreezable != null && shutterFreezable.IsShutterFrozen;
    }

    private void CaptureRuntimeStateBeforeHide()
    {
        for (int i = 0; i < colliderStates.Length; i++)
        {
            Collider target = colliderStates[i].Collider;
            if (target == null) continue;
            suspendedColliderEnabled[i] = target.enabled;
            hasSuspendedColliderState[i] = true;
        }
        for (int i = 0; i < behaviourStates.Length; i++)
        {
            MonoBehaviour target = behaviourStates[i].Behaviour;
            if (target == null || target == this) continue;
            suspendedBehaviourEnabled[i] = target.enabled;
            hasSuspendedBehaviourState[i] = true;
        }
        for (int i = 0; i < animatorStates.Length; i++)
        {
            Animator target = animatorStates[i].Animator;
            if (target == null) continue;
            suspendedAnimatorEnabled[i] = target.enabled;
            hasSuspendedAnimatorState[i] = true;
        }
        for (int i = 0; i < rigidbodyStates.Length; i++)
        {
            Rigidbody target = rigidbodyStates[i].Rigidbody;
            if (target == null) continue;
            suspendedIsKinematic[i] = target.isKinematic;
            suspendedUseGravity[i] = target.useGravity;
            suspendedDetectCollisions[i] = target.detectCollisions;
            suspendedConstraints[i] = target.constraints;
            suspendedInterpolation[i] = target.interpolation;
            if (!target.isKinematic)
            {
                suspendedLinearVelocities[i] = target.linearVelocity;
                suspendedAngularVelocities[i] = target.angularVelocity;
            }
            hasSuspendedRigidbodyState[i] = true;
        }
    }

    private int ApplyLightStates(bool present)
    {
        int disabledCount = 0;
        for (int i = 0; i < lightStates.Length; i++)
        {
            Light target = lightStates[i].Light;
            if (target == null) continue;
            bool nextEnabled = present && lightStates[i].OriginalEnabled;
            if (!nextEnabled && target.enabled) disabledCount++;
            target.enabled = nextEnabled;
        }
        return disabledCount;
    }

    private Rigidbody[] ResolveRigidbodies()
    {
        Rigidbody[] autoTargets = autoCollectRigidbodies
            ? GetComponentsInChildren<Rigidbody>(true)
            : Array.Empty<Rigidbody>();

        if (primaryRigidbody == null)
        {
            primaryRigidbody = GetComponent<Rigidbody>();
        }

        int manualCount = controlledRigidbodies != null ? controlledRigidbodies.Length : 0;
        Rigidbody[] manualTargets = new Rigidbody[manualCount + 1];
        int count = 0;
        if (primaryRigidbody != null)
        {
            manualTargets[count] = primaryRigidbody;
            count++;
        }

        for (int i = 0; i < manualCount; i++)
        {
            manualTargets[count] = controlledRigidbodies[i];
            count++;
        }

        Array.Resize(ref manualTargets, count);
        return MergeComponents(autoTargets, manualTargets);
    }

    private MonoBehaviour[] ResolveControlledBehaviours()
    {
        MonoBehaviour[] manualTargets = RemoveMissing(controlledBehaviours);
        if (!autoCollectMonsterBehaviours)
        {
            return FilterControlledBehaviours(manualTargets);
        }

        MonoBehaviour[] autoTargets = GetComponentsInChildren<MonoBehaviour>(true);
        MonoBehaviour[] monsterTargets = new MonoBehaviour[autoTargets.Length];
        int count = 0;
        for (int i = 0; i < autoTargets.Length; i++)
        {
            MonoBehaviour target = autoTargets[i];
            if (IsMonsterBehaviour(target))
            {
                monsterTargets[count] = target;
                count++;
            }
        }

        Array.Resize(ref monsterTargets, count);
        return FilterControlledBehaviours(MergeComponents(monsterTargets, manualTargets));
    }

    private MonoBehaviour[] FilterControlledBehaviours(MonoBehaviour[] targets)
    {
        int sourceCount = targets != null ? targets.Length : 0;
        int count = 0;
        MonoBehaviour[] result = new MonoBehaviour[sourceCount];
        for (int i = 0; i < sourceCount; i++)
        {
            MonoBehaviour target = targets[i];
            if (ShouldControlBehaviour(target))
            {
                result[count] = target;
                count++;
            }
        }

        Array.Resize(ref result, count);
        return result;
    }

    private bool ShouldControlBehaviour(MonoBehaviour target)
    {
        return target != null
            && target != this
            && !(target is WorldPresence)
            && !(target is WorldSwitchable)
            && !(target is WorldStateObject3D)
            && !(target is WorldVariant3D)
            && !(target is WorldSystem3D)
            && !(target is WorldManager);
    }

    private static bool IsMonsterBehaviour(MonoBehaviour target)
    {
        if (target == null)
        {
            return false;
        }

        Type type = target.GetType();
        string typeName = type.Name;
        return target is MonsterAIBase ||
               typeName.StartsWith("Monster", StringComparison.Ordinal) ||
               typeName.StartsWith("EyeballFly", StringComparison.Ordinal) ||
               typeName.StartsWith("HumanBox", StringComparison.Ordinal) ||
               typeName.StartsWith("Boomber", StringComparison.Ordinal);
    }

    private bool IsMonsterDead()
    {
        MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null)
            {
                continue;
            }

            Type type = behaviour.GetType();
            PropertyInfo property = type.GetProperty("IsDead", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.PropertyType == typeof(bool) && (bool)property.GetValue(behaviour))
            {
                return true;
            }
        }

        return false;
    }

    private static T[] MergeComponents<T>(T[] first, T[] second) where T : Component
    {
        int firstCount = first != null ? first.Length : 0;
        int secondCount = second != null ? second.Length : 0;
        T[] result = new T[firstCount + secondCount];
        int count = 0;

        for (int i = 0; i < firstCount; i++)
        {
            AddUnique(result, ref count, first[i]);
        }

        for (int i = 0; i < secondCount; i++)
        {
            AddUnique(result, ref count, second[i]);
        }

        Array.Resize(ref result, count);
        return result;
    }

    private static T[] RemoveMissing<T>(T[] values) where T : UnityEngine.Object
    {
        int sourceCount = values != null ? values.Length : 0;
        T[] result = new T[sourceCount];
        int count = 0;
        for (int i = 0; i < sourceCount; i++)
        {
            if (values[i] != null)
            {
                result[count] = values[i];
                count++;
            }
        }

        Array.Resize(ref result, count);
        return result;
    }

    private static void AddUnique<T>(T[] targets, ref int count, T target) where T : UnityEngine.Object
    {
        if (target == null)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            if (targets[i] == target)
            {
                return;
            }
        }

        targets[count] = target;
        count++;
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos)
        {
            return;
        }

        Bounds bounds = ResolveGizmoBounds();
        Gizmos.color = GetGizmoColor();
        Gizmos.DrawWireCube(bounds.center, bounds.size);

#if UNITY_EDITOR
        Handles.color = GetGizmoColor();
        Handles.Label(bounds.center + Vector3.up * Mathf.Max(0.3f, bounds.extents.y + 0.2f), GetPresenceLabel());
#endif
    }

    private Bounds ResolveGizmoBounds()
    {
        Renderer firstRenderer = controlledRenderers != null && controlledRenderers.Length > 0
            ? controlledRenderers[0]
            : GetComponentInChildren<Renderer>(true);
        if (firstRenderer != null)
        {
            return firstRenderer.bounds;
        }

        Collider firstCollider = controlledColliders != null && controlledColliders.Length > 0
            ? controlledColliders[0]
            : GetComponentInChildren<Collider>(true);
        if (firstCollider != null)
        {
            return firstCollider.bounds;
        }

        return new Bounds(transform.position, Vector3.one * 0.5f);
    }

    private Color GetGizmoColor()
    {
        switch (presenceMode)
        {
            case WorldPresenceMode.WorldAOnly:
                return new Color(0.2f, 0.65f, 1f, 0.85f);
            case WorldPresenceMode.WorldBOnly:
                return new Color(0.85f, 0.35f, 1f, 0.85f);
            default:
                return new Color(0.8f, 0.8f, 0.8f, 0.75f);
        }
    }

    private string GetPresenceLabel()
    {
        switch (presenceMode)
        {
            case WorldPresenceMode.WorldAOnly:
                return "World A";
            case WorldPresenceMode.WorldBOnly:
                return "World B";
            default:
                return "Both";
        }
    }

    private readonly struct RendererState
    {
        public readonly Renderer Renderer;
        public readonly bool OriginalEnabled;

        public RendererState(Renderer renderer, bool originalEnabled)
        {
            Renderer = renderer;
            OriginalEnabled = originalEnabled;
        }
    }

    private readonly struct LightState
    {
        public readonly Light Light;
        public readonly bool OriginalEnabled;
        public LightState(Light light, bool originalEnabled) { Light = light; OriginalEnabled = originalEnabled; }
    }

    private readonly struct ColliderState
    {
        public readonly Collider Collider;
        public readonly bool OriginalEnabled;

        public ColliderState(Collider collider, bool originalEnabled)
        {
            Collider = collider;
            OriginalEnabled = originalEnabled;
        }
    }

    private readonly struct BehaviourState
    {
        public readonly MonoBehaviour Behaviour;
        public readonly bool OriginalEnabled;
        public readonly bool IsMonsterBehaviour;

        public BehaviourState(MonoBehaviour behaviour, bool originalEnabled, bool isMonsterBehaviour)
        {
            Behaviour = behaviour;
            OriginalEnabled = originalEnabled;
            IsMonsterBehaviour = isMonsterBehaviour;
        }
    }

    private readonly struct RigidbodyState
    {
        public readonly Rigidbody Rigidbody;
        public readonly bool OriginalIsKinematic;
        public readonly bool OriginalUseGravity;
        public readonly bool OriginalDetectCollisions;
        public readonly RigidbodyConstraints OriginalConstraints;
        public readonly RigidbodyInterpolation OriginalInterpolation;

        public RigidbodyState(
            Rigidbody rigidbody,
            bool originalIsKinematic,
            bool originalUseGravity,
            bool originalDetectCollisions,
            RigidbodyConstraints originalConstraints,
            RigidbodyInterpolation originalInterpolation)
        {
            Rigidbody = rigidbody;
            OriginalIsKinematic = originalIsKinematic;
            OriginalUseGravity = originalUseGravity;
            OriginalDetectCollisions = originalDetectCollisions;
            OriginalConstraints = originalConstraints;
            OriginalInterpolation = originalInterpolation;
        }
    }

    private readonly struct AnimatorState
    {
        public readonly Animator Animator;
        public readonly bool OriginalEnabled;

        public AnimatorState(Animator animator, bool originalEnabled)
        {
            Animator = animator;
            OriginalEnabled = originalEnabled;
        }
    }
}
