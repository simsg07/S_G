using UnityEngine;

public enum CraneXYAxis
{
    Horizontal,
    Vertical
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class CraneXYLeverSwitch3D : MonoBehaviour, IInteractable3D, ISwitchActivation3D
{
    [SerializeField] private CraneXYController3D targetCrane;
    [SerializeField] private CraneXYAxis controlledAxis;
    [SerializeField] private Collider interactionTrigger;
    [SerializeField] private LayerMask playerLayerMask;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private Renderer leverRenderer;
    [SerializeField] private LeverSpriteVisual3D leverVisual;
    [SerializeField] private bool playerInRange;
    [SerializeField] private int lastEnterSequence;
    [Header("Activation Sources")]
    [SerializeField] private bool allowPlayerActivation = true;
    [SerializeField] private bool allowStoneActivation = true;
    [SerializeField] private bool allowCircleSpikeActivation = true;

    private static int enterSequence;

    public CraneXYAxis ControlledAxis => controlledAxis;
    public int LastEnterSequence => lastEnterSequence;
    public bool PlayerInRange => playerInRange;

    private void Awake()
    {
        if (interactionTrigger == null) interactionTrigger = GetComponent<Collider>();
    }

    private void OnValidate()
    {
        if (interactionTrigger == null) interactionTrigger = GetComponent<Collider>();
        if (interactionTrigger != null) interactionTrigger.isTrigger = true;
    }

    public bool TryInteract(GameObject actor)
    {
        if (actor == null || !IsPlayer(actor.transform)) return false;
        return TryActivate(SwitchActivationSource.Player, actor);
    }

    public bool TryActivate(SwitchActivationSource source, GameObject instigator)
    {
        if (!IsActivationSourceAllowed(source)) return false;
        return RequestAndAnimate(instigator != null ? instigator.transform : null);
    }

    public void Interact()
    {
        RequestAndAnimate(null);
    }

    private bool RequestAndAnimate(Transform actor)
    {
        if (targetCrane == null) return false;
        bool negativeDirection = targetCrane.WillNextMoveInNegativeDirection(controlledAxis);
        bool accepted = targetCrane.RequestAxisMove(controlledAxis, this, actor);
        if (accepted && leverVisual != null) leverVisual.PlayAcceptedCommand(negativeDirection);
        return accepted;
    }

    public void NotifyCommandFinished(bool cancelled)
    {
        if (leverVisual != null) leverVisual.FinishAcceptedCommand(cancelled);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other.transform)) return;
        playerInRange = true;
        lastEnterSequence = ++enterSequence;
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other.transform)) playerInRange = false;
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
}
