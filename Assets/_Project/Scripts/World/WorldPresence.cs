using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
public class WorldPresence : MonoBehaviour
{
    [Header("World Presence")]
    [SerializeField] private WorldPresenceMode presenceMode = WorldPresenceMode.Both;
    [SerializeField] private bool applyOnStart = true;

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

    [Header("Debug")]
    [SerializeField] private bool debugMode;
    [SerializeField] private bool showGizmos = true;

    // Kept only so old prefabs that serialized this value do not lose data noisily.
    // WorldPresence never uses root GameObject.SetActive anymore.
    [FormerlySerializedAs("affectRootActive")]
    [SerializeField, HideInInspector] private bool legacyAffectRootActive;
    [FormerlySerializedAs("visualRoot")]
    [SerializeField, HideInInspector] private GameObject legacyVisualRoot;
    [FormerlySerializedAs("affectMonsterAI")]
    [SerializeField, HideInInspector] private bool legacyAffectMonsterAI = true;
    [FormerlySerializedAs("affectBehaviours")]
    [SerializeField, HideInInspector] private bool legacyAffectBehaviours;

    private RendererState[] rendererStates = Array.Empty<RendererState>();
    private ColliderState[] colliderStates = Array.Empty<ColliderState>();
    private BehaviourState[] behaviourStates = Array.Empty<BehaviourState>();
    private RigidbodyState[] rigidbodyStates = Array.Empty<RigidbodyState>();
    private AnimatorState[] animatorStates = Array.Empty<AnimatorState>();
    private bool referencesCached;
    private bool originalStatesCached;
    private WorldState lastAppliedWorld = WorldState.WorldA;

    public WorldPresenceMode PresenceMode => presenceMode;
    public bool IsPresentInCurrentWorld { get; private set; } = true;

    private void Awake()
    {
        WorldPresenceRegistry.Register(this);
        RefreshReferences();
        CacheOriginalStates();
    }

    private void OnEnable()
    {
        WorldPresenceRegistry.Register(this);
        SubscribeWorldChanged();

        if (Application.isPlaying && applyOnStart)
        {
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

    public void ApplyWorldState(WorldState currentWorld)
    {
        EnsureCached();
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
        EnsureCached();
        IsPresentInCurrentWorld = present;

        int disabledRenderers = ApplyRendererStates(present);
        int disabledColliders = ApplyColliderStates(present);
        int disabledBehaviours = disableControlledBehavioursWhenAbsent
            ? ApplyBehaviourStates(present, IsMonsterDead())
            : 0;
        int stoppedRigidbodies = ApplyRigidbodyStates(present);
        int disabledAnimators = ApplyAnimatorStates(present);

        if (debugMode && Application.isPlaying)
        {
            Debug.Log(
                $"[WorldPresence] {name} world={lastAppliedWorld}, mode={presenceMode}, present={present}, " +
                $"renderersOff={disabledRenderers}, collidersOff={disabledColliders}, behavioursOff={disabledBehaviours}, " +
                $"rigidbodiesStopped={stoppedRigidbodies}, animatorsOff={disabledAnimators}",
                this);
        }
    }

    private void SubscribeWorldChanged()
    {
        WorldManager.WorldChanged -= HandleTimelineWorldChanged;
        WorldSystem3D.ActiveWorldChanged -= HandleResearchWorldChanged;
        WorldManager.WorldChanged += HandleTimelineWorldChanged;
        WorldSystem3D.ActiveWorldChanged += HandleResearchWorldChanged;
    }

    private void UnsubscribeWorldChanged()
    {
        WorldManager.WorldChanged -= HandleTimelineWorldChanged;
        WorldSystem3D.ActiveWorldChanged -= HandleResearchWorldChanged;
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

    private void CacheOriginalStates()
    {
        rendererStates = new RendererState[controlledRenderers != null ? controlledRenderers.Length : 0];
        for (int i = 0; i < rendererStates.Length; i++)
        {
            Renderer target = controlledRenderers[i];
            rendererStates[i] = new RendererState(target, target != null && target.enabled);
        }

        colliderStates = new ColliderState[controlledColliders != null ? controlledColliders.Length : 0];
        for (int i = 0; i < colliderStates.Length; i++)
        {
            Collider target = controlledColliders[i];
            colliderStates[i] = new ColliderState(target, target != null && target.enabled);
        }

        behaviourStates = new BehaviourState[controlledBehaviours != null ? controlledBehaviours.Length : 0];
        for (int i = 0; i < behaviourStates.Length; i++)
        {
            MonoBehaviour target = controlledBehaviours[i];
            behaviourStates[i] = new BehaviourState(target, target != null && target.enabled, IsMonsterBehaviour(target));
        }

        rigidbodyStates = new RigidbodyState[controlledRigidbodies != null ? controlledRigidbodies.Length : 0];
        for (int i = 0; i < rigidbodyStates.Length; i++)
        {
            Rigidbody target = controlledRigidbodies[i];
            rigidbodyStates[i] = new RigidbodyState(
                target,
                target != null && target.isKinematic,
                target != null && target.useGravity);
        }

        animatorStates = new AnimatorState[controlledAnimators != null ? controlledAnimators.Length : 0];
        for (int i = 0; i < animatorStates.Length; i++)
        {
            Animator target = controlledAnimators[i];
            animatorStates[i] = new AnimatorState(target, target != null && target.enabled);
        }

        originalStatesCached = true;
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

            bool nextEnabled = present && colliderStates[i].OriginalEnabled;
            if (!nextEnabled && target.enabled)
            {
                disabledCount++;
            }

            target.enabled = nextEnabled;
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

            bool nextEnabled = present && behaviourStates[i].OriginalEnabled;
            if (!nextEnabled && target.enabled)
            {
                disabledCount++;
            }

            target.enabled = nextEnabled;
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

            target.linearVelocity = Vector3.zero;
            target.angularVelocity = Vector3.zero;

            if (present)
            {
                target.isKinematic = rigidbodyStates[i].OriginalIsKinematic;
                target.useGravity = rigidbodyStates[i].OriginalUseGravity;
                continue;
            }

            target.isKinematic = true;
            target.useGravity = false;
            stoppedCount++;
        }

        return stoppedCount;
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

            bool nextEnabled = present && animatorStates[i].OriginalEnabled;
            if (!nextEnabled && target.enabled)
            {
                disabledCount++;
            }

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

        public RigidbodyState(Rigidbody rigidbody, bool originalIsKinematic, bool originalUseGravity)
        {
            Rigidbody = rigidbody;
            OriginalIsKinematic = originalIsKinematic;
            OriginalUseGravity = originalUseGravity;
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
