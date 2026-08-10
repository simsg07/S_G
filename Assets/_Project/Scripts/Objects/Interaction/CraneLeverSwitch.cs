using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public enum CraneLeverOperationState
{
    Idle,
    LeverActivated,
    WaitingForActivation,
    Moving,
    Arrived
}

[System.Serializable] public sealed class FloatUnityEvent : UnityEvent<float> { }

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class CraneLeverSwitch : MonoBehaviour, IInteractable3D, ISwitchActivation3D
{
    [Header("Crane Target")]
    [Tooltip("Existing horizontal Crane target. Kept for scene/prefab serialization compatibility.")]
    [SerializeField] private CraneObject targetCrane;
    [SerializeField] private VerticalCraneController3D targetVerticalCrane;
    [Tooltip("If Target Crane is empty, use the only compatible Crane found in the scene.")]
    [SerializeField] private bool autoFindSingleCraneIfMissing = true;

    [Header("Activation")]
    [Min(0f)] [SerializeField] private float activationDelay = 3f;
    [SerializeField] private bool useUnscaledActivationDelay;
    [SerializeField] private bool autoLoop;
    [FormerlySerializedAs("canUseWhileCraneMoving")]
    [SerializeField] private bool canRetriggerWhileMoving;
    [SerializeField] private bool canCancelDuringDelay;

    [Header("Player Interaction")]
    [SerializeField] private Collider interactionTrigger;
    [Tooltip("When enabled, fallback keyboard input works only while a Player is inside this trigger.")]
    [SerializeField] private bool requirePlayerInRange = true;
    [SerializeField] private bool useFallbackInput = true;
    [SerializeField] private LayerMask playerLayerMask;
    [SerializeField] private string playerTag = "Player";
    [FormerlySerializedAs("fallbackInteractKey")]
    [SerializeField] private Key interactionKey = Key.F;
    [SerializeField] private bool playerInRange;

    [Header("Activation Sources")]
    [SerializeField] private bool allowPlayerActivation = true;
    [SerializeField] private bool allowStoneActivation = true;
    [SerializeField] private bool allowCircleSpikeActivation = true;

    [Header("Lever Feedback")]
    [SerializeField] private Animator animator;
    [SerializeField] private string activateTriggerName = "Activate";
    [SerializeField] private Renderer leverRenderer;
    [SerializeField] private SpriteRenderer leverSpriteRenderer;
    [SerializeField] private Sprite leverOffSprite;
    [SerializeField] private Sprite leverOnSprite;

    [Header("Events")]
    [SerializeField] private UnityEvent onLeverActivated = new UnityEvent();
    [SerializeField] private UnityEvent onActivationDelayStarted = new UnityEvent();
    [SerializeField] private FloatUnityEvent onActivationDelayRemaining = new FloatUnityEvent();
    [SerializeField] private UnityEvent onMovementStarted = new UnityEvent();
    [SerializeField] private UnityEvent onDestinationReached = new UnityEvent();

    [Header("Runtime State")]
    [SerializeField] private CraneLeverOperationState state = CraneLeverOperationState.Idle;

    [Header("Debug")]
    [SerializeField] private bool debugMode;
    [SerializeField] private PersistentSceneObject3D persistentState;

    private int lastInteractionFrame = -1;
    private float delayRemaining;
    private bool movementObserved;

    public CraneLeverOperationState State => state;
    public float ActivationDelayRemaining => delayRemaining;

    private void Awake()
    {
        if (interactionTrigger == null) interactionTrigger = GetComponent<Collider>();
        ResolveTarget(false);
        ApplyLeverVisual(false);
    }

    private void Reset()
    {
        interactionTrigger = GetComponent<Collider>();
        if (interactionTrigger != null) interactionTrigger.isTrigger = true;
    }

    private void OnValidate()
    {
        activationDelay = Mathf.Max(0f, activationDelay);
        if (interactionTrigger == null) interactionTrigger = GetComponent<Collider>();
        if (interactionTrigger != null) interactionTrigger.isTrigger = true;
    }

    private void OnDisable()
    {
        delayRemaining = 0f;
        movementObserved = false;
        playerInRange = false;
        state = CraneLeverOperationState.Idle;
        ApplyLeverVisual(false);
    }

    private void Update()
    {
        HandleActivationDelay();
        HandleMovementCompletion();

        if (!useFallbackInput || (requirePlayerInRange && !playerInRange)) return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && interactionKey != Key.None && keyboard[interactionKey].wasPressedThisFrame)
            ActivateLever();
    }

    public bool TryInteract(GameObject actor)
    {
        if (actor != null && !IsPlayer(actor.transform)) return false;
        if (requirePlayerInRange && !playerInRange) return false;
        return TryActivate(SwitchActivationSource.Player, actor);
    }

    public bool TryActivate(SwitchActivationSource source, GameObject instigator)
    {
        if (!IsActivationSourceAllowed(source)) return false;
        if (source != SwitchActivationSource.Player && state != CraneLeverOperationState.Idle) return false;
        return ActivateLever();
    }

    public void Interact()
    {
        if (requirePlayerInRange && !playerInRange) return;
        ActivateLever();
    }

    public bool ActivateLever()
    {
        if (lastInteractionFrame == Time.frameCount) return false;
        lastInteractionFrame = Time.frameCount;
        if (state == CraneLeverOperationState.Arrived) state = CraneLeverOperationState.Idle;
        ResolveTarget(true);
        if (!HasTarget()) return false;

        if (state == CraneLeverOperationState.WaitingForActivation || state == CraneLeverOperationState.LeverActivated)
        {
            if (!canCancelDuringDelay) return false;
            CancelActivationDelay();
            return true;
        }
        if ((state == CraneLeverOperationState.Moving || IsTargetMoving()) && !canRetriggerWhileMoving) return false;

        state = CraneLeverOperationState.LeverActivated;
        ApplyLeverVisual(true);
        TriggerAnimator();
        onLeverActivated?.Invoke();
        persistentState ??= GetComponent<PersistentSceneObject3D>();
        persistentState?.MarkActivated();

        delayRemaining = activationDelay;
        state = CraneLeverOperationState.WaitingForActivation;
        onActivationDelayStarted?.Invoke();
        onActivationDelayRemaining?.Invoke(delayRemaining);
        if (delayRemaining <= 0f) StartTargetMovement();
        return true;
    }

    private void HandleActivationDelay()
    {
        if (state != CraneLeverOperationState.WaitingForActivation) return;
        delayRemaining = Mathf.Max(0f, delayRemaining - (useUnscaledActivationDelay ? Time.unscaledDeltaTime : Time.deltaTime));
        onActivationDelayRemaining?.Invoke(delayRemaining);
        if (delayRemaining <= 0f) StartTargetMovement();
    }

    private void StartTargetMovement()
    {
        if (state != CraneLeverOperationState.WaitingForActivation) return;
        bool accepted = targetVerticalCrane != null
            ? targetVerticalCrane.RequestMoveToOppositeDestination(canRetriggerWhileMoving)
            : targetCrane != null && targetCrane.TryToggleMoveTarget(canRetriggerWhileMoving);
        if (!accepted)
        {
            state = CraneLeverOperationState.Idle;
            ApplyLeverVisual(false);
            return;
        }
        movementObserved = true;
        state = CraneLeverOperationState.Moving;
        ApplyLeverVisual(false);
        onMovementStarted?.Invoke();
    }

    private void HandleMovementCompletion()
    {
        if (state != CraneLeverOperationState.Moving || !movementObserved || IsTargetMoving()) return;
        movementObserved = false;
        state = CraneLeverOperationState.Arrived;
        onDestinationReached?.Invoke();
        if (autoLoop)
        {
            state = CraneLeverOperationState.Idle;
            ActivateLever();
        }
    }

    private void CancelActivationDelay()
    {
        delayRemaining = 0f;
        state = CraneLeverOperationState.Idle;
        ApplyLeverVisual(false);
    }

    private bool HasTarget() => targetCrane != null || targetVerticalCrane != null;
    private bool IsTargetMoving() => targetVerticalCrane != null ? targetVerticalCrane.IsMoving : targetCrane != null && targetCrane.IsMoving;

    public void SetTargetCrane(CraneObject crane)
    {
        targetCrane = crane;
        targetVerticalCrane = null;
    }

    public void SetTargetVerticalCrane(VerticalCraneController3D crane)
    {
        targetVerticalCrane = crane;
        targetCrane = null;
    }

    [ContextMenu("Find Single Crane In Scene")]
    public void FindSingleCraneInScene() => ResolveTarget(true);
    [ContextMenu("Test Interact")]
    public void TestInteract() => Interact();

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;
        if (IsPlayer(other.transform)) playerInRange = true;
        TryActivateFromCircleSpikeContact(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other != null && IsPlayer(other.transform)) playerInRange = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision != null) TryActivateFromCircleSpikeContact(collision.collider);
    }

    private void TryActivateFromCircleSpikeContact(Collider contact)
    {
        if (!allowCircleSpikeActivation || contact == null) return;

        CircleSpikeProjectile3D circleSpike = contact.GetComponent<CircleSpikeProjectile3D>()
            ?? contact.GetComponentInParent<CircleSpikeProjectile3D>();
        if (circleSpike == null || !circleSpike.IsLaunched) return;

        if (!circleSpike.CanTriggerSwitch) return;

        if (TryActivate(SwitchActivationSource.CircleSpike, circleSpike.gameObject))
            circleSpike.MarkSwitchTriggered();
    }

    private bool IsPlayer(Transform target)
    {
        for (Transform current = target; current != null; current = current.parent)
        {
            if (playerLayerMask.value != 0 && (playerLayerMask.value & (1 << current.gameObject.layer)) != 0) return true;
            if (!string.IsNullOrWhiteSpace(playerTag) && current.CompareTag(playerTag)) return true;
        }
        return false;
    }

    private bool IsActivationSourceAllowed(SwitchActivationSource source)
    {
        switch (source)
        {
            case SwitchActivationSource.Player: return allowPlayerActivation;
            case SwitchActivationSource.Stone: return allowStoneActivation;
            case SwitchActivationSource.CircleSpike: return allowCircleSpikeActivation;
            default: return false;
        }
    }

    private void ResolveTarget(bool logResult)
    {
        if (HasTarget() || !autoFindSingleCraneIfMissing) return;
        CraneObject[] horizontal = FindObjectsByType<CraneObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        VerticalCraneController3D[] vertical = FindObjectsByType<VerticalCraneController3D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (horizontal.Length + vertical.Length == 1)
        {
            if (horizontal.Length == 1) targetCrane = horizontal[0];
            else targetVerticalCrane = vertical[0];
        }
        else if (logResult && debugMode)
            Debug.LogWarning($"[CraneLeverSwitch] Assign a target explicitly. Found {horizontal.Length} horizontal and {vertical.Length} vertical Cranes.", this);
    }

    private void TriggerAnimator()
    {
        if (animator == null || string.IsNullOrWhiteSpace(activateTriggerName)) return;
        foreach (AnimatorControllerParameter parameter in animator.parameters)
            if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == activateTriggerName)
            {
                animator.SetTrigger(activateTriggerName);
                return;
            }
    }

    private void ApplyLeverVisual(bool active)
    {
        if (leverSpriteRenderer != null)
        {
            Sprite sprite = active ? leverOnSprite : leverOffSprite;
            if (sprite != null) leverSpriteRenderer.sprite = sprite;
        }
        if (leverRenderer != null) leverRenderer.enabled = true;
    }
}
