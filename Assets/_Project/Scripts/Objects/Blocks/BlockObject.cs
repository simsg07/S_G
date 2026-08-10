using System.Collections;
using UnityEngine;

[AddComponentMenu("_Project/Objects/Weak Wall (Block Object)")]
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class BlockObject : MonoBehaviour
{
    public enum WeakWallState
    {
        INTACT,
        DESTROYED
    }

    private static readonly int BreakHash = Animator.StringToHash("Break");
    private static readonly int IsBrokenHash = Animator.StringToHash("IsBroken");

    [Header("Block Type")]
    [SerializeField] private BlockObjectType blockType;
    [SerializeField] private bool isBroken;

    [Header("Weak Wall Progress")]
    [Tooltip("When enabled, a destroyed wall is permanent and reset calls cannot restore it.")]
    [SerializeField] private bool permanentDestruction;
    [Tooltip("Runtime state shown for the Weak_Wall prefab.")]
    [SerializeField] private WeakWallState currentState = WeakWallState.INTACT;

    [Header("Block Rules")]
    [SerializeField] private bool canBlockPlayer = true;
    [SerializeField] private bool canBlockMonster = true;
    [SerializeField] private bool canBlockSight;
    [SerializeField] private bool canBlockLight;
    [Header("Break Settings")]
    [SerializeField] private bool removeColliderOnBreak = true;
    [SerializeField] private bool hideVisualOnBreak;
    [SerializeField] private bool delayHideVisual = true;
    [SerializeField] private float visualHideDelay = 0.25f;

    [Header("Anti Stuck")]
    [SerializeField] private bool clearPlayerOverlapOnBreak = true;
    [SerializeField] private LayerMask playerLayerMask;
    [SerializeField] private float safePushDistance = 0.25f;
    [SerializeField] private float overlapCheckPadding = 0.05f;

    [Header("References")]
    [SerializeField] private DataDrivenObjectController dataController;
    [SerializeField] private HitReceiver hitReceiver;
    [SerializeField] private BreakableObject3D breakableObject;
    [SerializeField] private OpenPathOnBreak openPathOnBreak;
    [SerializeField] private Collider mainCollider;
    [SerializeField] private Renderer[] renderers;
    [SerializeField] private Animator animator;
    [SerializeField] private PersistentSceneObject3D persistentState;

    [Header("Runtime Debug (Read Only)")]
    [SerializeField] private string debugPersistentId = string.Empty;
    [SerializeField] private bool loadedPersistentDestroyed;
    [SerializeField] private string lastStateApplyReason = "Not applied";

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;

    private Coroutine hideVisualRoutine;
    private bool registeredHitReceiver;

    public BlockObjectType BlockType => blockType;
    public bool IsBroken => isBroken;
    public WeakWallState CurrentState => currentState;
    public string DebugPersistentId => debugPersistentId;
    public bool LoadedPersistentDestroyed => loadedPersistentDestroyed;
    public string LastStateApplyReason => lastStateApplyReason;
    public bool CanBlockPlayer => canBlockPlayer;
    public bool CanBlockMonster => canBlockMonster;
    public bool CanBlockSight => canBlockSight;
    public bool CanBlockLight => canBlockLight;

    private void Awake()
    {
        CacheReferences();
        if (permanentDestruction && !RestorePersistentState("Awake"))
        {
            ApplyIntactVisualAndCollider("Awake: no saved DESTROYED state");
        }
    }

    private void OnEnable()
    {
        CacheReferences();

        if (!permanentDestruction)
        {
            RegisterHitReceiver();
            return;
        }

        if (RestorePersistentState("OnEnable (scene/world activation)"))
        {
            return;
        }

        if (isBroken)
        {
            ApplyDestroyedVisualAndCollider("OnEnable: runtime DESTROYED state");
            return;
        }

        ApplyIntactVisualAndCollider("OnEnable: no saved DESTROYED state");
        RegisterHitReceiver();
    }

    private void OnDisable()
    {
        UnregisterHitReceiver();
    }

    private void OnValidate()
    {
        visualHideDelay = Mathf.Max(0f, visualHideDelay);
        safePushDistance = Mathf.Max(0f, safePushDistance);
        overlapCheckPadding = Mathf.Max(0f, overlapCheckPadding);
        currentState = isBroken ? WeakWallState.DESTROYED : WeakWallState.INTACT;
        CacheReferences();
#if UNITY_EDITOR
        if (permanentDestruction && persistentState != null)
        {
            persistentState.EnsureEditorPersistentId("weak_wall");
        }
#endif
        RefreshPersistentDebug();
    }

    [ContextMenu("Apply Block Data")]
    public void ApplyBlockData()
    {
        if (dataController == null)
        {
            WarnMissing(nameof(DataDrivenObjectController));
            ValidateBlockSetup();
            return;
        }

        dataController.ApplyData();
    }

    public void ApplyBlockData(ObjectData data)
    {
        if (data == null)
        {
            Debug.LogWarning("[BlockObject] ObjectData is not assigned.", this);
            return;
        }

        blockType = data.blockType;
        canBlockPlayer = data.canBlockPlayer;
        canBlockMonster = data.canBlockMonster;
        canBlockSight = data.canBlockSight;
        canBlockLight = data.canBlockLight;
        removeColliderOnBreak = data.removeColliderOnBreak;
        hideVisualOnBreak = data.hideVisualOnBreak;
        delayHideVisual = data.delayHideVisual;
        visualHideDelay = Mathf.Max(0f, data.visualHideDelay);
        clearPlayerOverlapOnBreak = data.clearPlayerOverlapOnBreak;
        safePushDistance = Mathf.Max(0f, data.safePushDistance);
        debugMode = data.debugMode;

        UnregisterHitReceiver();
        RegisterHitReceiver();

        Log($"Applied block data: {data.displayName} ({blockType})");
    }

    [ContextMenu("Test Break Block")]
    public void TestBreakBlock()
    {
        BreakBlock();
    }

    public void BreakBlock()
    {
        if (isBroken)
        {
            return;
        }

        Log($"BreakBlock started: {name}");
        isBroken = true;
        currentState = WeakWallState.DESTROYED;
        lastStateApplyReason = "Boomber damage: DESTROYED";

        if (hitReceiver != null)
        {
            hitReceiver.SetCanBeTargeted(false);
        }

        if (clearPlayerOverlapOnBreak)
        {
            NudgePlayersOverlappingMainCollider();
        }

        if (removeColliderOnBreak)
        {
            SetCollisionEnabled(false);
            Log("Collider disabled immediately.");
        }

        if (breakableObject != null)
        {
            breakableObject.BreakObject();
        }

        if (openPathOnBreak != null)
        {
            openPathOnBreak.OpenPath();
        }

        ApplyAnimatorBreak();

        if (hideVisualOnBreak)
        {
            if (delayHideVisual && visualHideDelay > 0f && Application.isPlaying)
            {
                if (hideVisualRoutine != null)
                {
                    StopCoroutine(hideVisualRoutine);
                }

                hideVisualRoutine = StartCoroutine(HideVisualDelayed());
            }
            else
            {
                SetVisualEnabled(false);
            }
        }

        Log("BreakBlock complete.");
        if (persistentState == null) persistentState = GetComponent<PersistentSceneObject3D>();
        persistentState?.MarkDestroyed();
        RefreshPersistentDebug();
    }

    [ContextMenu("Reset Block")]
    public void ResetBlock()
    {
        if (permanentDestruction && (isBroken || IsPersistentlyDestroyed()))
        {
            ApplyDestroyedVisualAndCollider("ResetBlock rejected: permanent DESTROYED state");
            Log("ResetBlock ignored because permanent destruction is saved.");
            return;
        }

        if (hideVisualRoutine != null)
        {
            StopCoroutine(hideVisualRoutine);
            hideVisualRoutine = null;
        }

        if (breakableObject != null)
        {
            breakableObject.ResetBreakable();
        }

        if (hitReceiver != null) hitReceiver.ResetHitCount();
        ApplyIntactVisualAndCollider("ResetBlock: non-permanent reset");

        if (animator != null && HasParameter(IsBrokenHash, AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(IsBrokenHash, false);
        }

        Log("ResetBlock complete.");
    }

    private bool IsPersistentlyDestroyed()
    {
        if (!permanentDestruction || persistentState == null)
        {
            return false;
        }

        return persistentState.TryGetSavedState(out PersistentSceneObjectState savedState)
            ? savedState == PersistentSceneObjectState.Destroyed
            : persistentState.CurrentState == PersistentSceneObjectState.Destroyed;
    }

    private bool RestorePersistentState(string reason)
    {
        RefreshPersistentDebug();
        if (!permanentDestruction || !loadedPersistentDestroyed)
        {
            return false;
        }

        ApplyDestroyedVisualAndCollider($"{reason}: loaded persistent DESTROYED");
        return true;
    }

    private void ApplyIntactVisualAndCollider(string reason)
    {
        isBroken = false;
        currentState = WeakWallState.INTACT;

        if (hitReceiver != null) hitReceiver.SetCanBeTargeted(true);
        SetCollisionEnabled(true);
        SetVisualEnabled(true);

        if (animator != null && HasParameter(IsBrokenHash, AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(IsBrokenHash, false);
        }

        lastStateApplyReason = reason;
    }

    private void ApplyDestroyedVisualAndCollider(string reason)
    {
        isBroken = true;
        currentState = WeakWallState.DESTROYED;

        if (hideVisualRoutine != null)
        {
            StopCoroutine(hideVisualRoutine);
            hideVisualRoutine = null;
        }

        if (hitReceiver != null)
        {
            hitReceiver.SetCanBeTargeted(false);
        }

        if (removeColliderOnBreak)
        {
            SetCollisionEnabled(false);
        }

        if (hideVisualOnBreak)
        {
            SetVisualEnabled(false);
        }

        if (animator != null && HasParameter(IsBrokenHash, AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(IsBrokenHash, true);
        }

        lastStateApplyReason = reason;
    }

    private void RefreshPersistentDebug()
    {
        debugPersistentId = persistentState != null ? persistentState.PersistentId : string.Empty;
        loadedPersistentDestroyed = persistentState != null
            && persistentState.TryGetSavedState(out PersistentSceneObjectState savedState)
            && savedState == PersistentSceneObjectState.Destroyed;
    }

    public void SetCollisionEnabled(bool enabled)
    {
        if (mainCollider != null)
        {
            mainCollider.enabled = enabled;
        }
    }

    public void SetVisualEnabled(bool enabled)
    {
        if (renderers == null)
        {
            return;
        }

        foreach (Renderer targetRenderer in renderers)
        {
            if (targetRenderer != null)
            {
                targetRenderer.enabled = enabled;
            }
        }
    }

    [ContextMenu("Validate Block Setup")]
    public void ValidateBlockSetup()
    {
        CacheReferences();

        Log($"BlockType: {blockType}");
        LogComponent("DataDrivenObjectController", dataController);
        LogComponent("HitReceiver", hitReceiver);
        LogComponent("BreakableObject3D", breakableObject);
        LogComponent("OpenPathOnBreak", openPathOnBreak);
        LogComponent("MainCollider", mainCollider);
        LogComponent("Animator", animator);
        Log($"Renderers: {(renderers != null ? renderers.Length : 0)}");

        if (blockType == BlockObjectType.Breakable && hitReceiver == null)
        {
            Debug.LogWarning("[BlockObject] Breakable block has no HitReceiver. It can still be broken by calling BreakBlock manually.", this);
        }

        if (blockType == BlockObjectType.Breakable && mainCollider == null)
        {
            Debug.LogWarning("[BlockObject] Breakable block has no main collider. Player stuck mitigation cannot run.", this);
        }
    }

    private void CacheReferences()
    {
        if (dataController == null)
        {
            dataController = GetComponent<DataDrivenObjectController>();
        }

        if (hitReceiver == null)
        {
            hitReceiver = GetComponent<HitReceiver>();
        }

        if (breakableObject == null)
        {
            breakableObject = GetComponent<BreakableObject3D>();
        }

        if (openPathOnBreak == null)
        {
            openPathOnBreak = GetComponent<OpenPathOnBreak>();
        }

        if (mainCollider == null)
        {
            mainCollider = GetComponent<Collider>();
        }

        if (renderers == null || renderers.Length == 0)
        {
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }
    }

    private void RegisterHitReceiver()
    {
        if (registeredHitReceiver || hitReceiver == null || blockType != BlockObjectType.Breakable)
        {
            return;
        }

        hitReceiver.onMaxHit.AddListener(BreakBlock);
        registeredHitReceiver = true;
    }

    private void UnregisterHitReceiver()
    {
        if (!registeredHitReceiver || hitReceiver == null)
        {
            registeredHitReceiver = false;
            return;
        }

        hitReceiver.onMaxHit.RemoveListener(BreakBlock);
        registeredHitReceiver = false;
    }

    private void NudgePlayersOverlappingMainCollider()
    {
        if (mainCollider == null || playerLayerMask.value == 0)
        {
            return;
        }

        Bounds bounds = mainCollider.bounds;
        Vector3 halfExtents = bounds.extents + Vector3.one * overlapCheckPadding;
        Collider[] overlaps = Physics.OverlapBox(
            bounds.center,
            halfExtents,
            Quaternion.identity,
            playerLayerMask,
            QueryTriggerInteraction.Ignore);

        foreach (Collider overlap in overlaps)
        {
            if (overlap == null || overlap == mainCollider)
            {
                continue;
            }

            NudgeColliderAway(overlap, bounds.center);
        }
    }

    private void NudgeColliderAway(Collider targetCollider, Vector3 blockCenter)
    {
        Transform targetTransform = targetCollider.attachedRigidbody != null
            ? targetCollider.attachedRigidbody.transform
            : targetCollider.transform;

        Vector3 delta = targetCollider.bounds.center - blockCenter;
        Vector3 direction;

        if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) && Mathf.Abs(delta.x) > 0.001f)
        {
            direction = delta.x < 0f ? Vector3.left : Vector3.right;
        }
        else if (Mathf.Abs(delta.y) > 0.001f)
        {
            direction = delta.y < 0f ? Vector3.down : Vector3.up;
        }
        else
        {
            direction = Vector3.up;
        }

        Vector3 nudge = direction * safePushDistance;
        nudge.z = 0f;
        Vector3 newPosition = targetTransform.position + nudge;
        newPosition.z = targetTransform.position.z;

        Rigidbody targetRigidbody = targetCollider.attachedRigidbody;
        if (targetRigidbody != null)
        {
            targetRigidbody.position = newPosition;
        }
        else
        {
            targetTransform.position = newPosition;
        }

        Log("Player overlap detected. Nudged player to safe side.");
    }

    private IEnumerator HideVisualDelayed()
    {
        yield return new WaitForSeconds(visualHideDelay);
        SetVisualEnabled(false);
        hideVisualRoutine = null;
    }

    private void ApplyAnimatorBreak()
    {
        if (animator == null)
        {
            return;
        }

        if (HasParameter(IsBrokenHash, AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(IsBrokenHash, true);
        }

        if (HasParameter(BreakHash, AnimatorControllerParameterType.Trigger))
        {
            animator.SetTrigger(BreakHash);
        }
    }

    private bool HasParameter(int hash, AnimatorControllerParameterType type)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].nameHash == hash && parameters[i].type == type)
            {
                return true;
            }
        }

        return false;
    }

    private void WarnMissing(string componentName)
    {
        if (debugMode)
        {
            Debug.LogWarning($"[BlockObject] {componentName} is missing.", this);
        }
    }

    private void LogComponent(string label, Object component)
    {
        if (!debugMode)
        {
            return;
        }

        if (component != null)
        {
            Debug.Log($"[BlockObject] {label} found: {component.GetType().Name}", this);
            return;
        }

        Debug.LogWarning($"[BlockObject] {label} not assigned.", this);
    }

    private void Log(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[BlockObject] {message}", this);
        }
    }
}
