using UnityEngine;

public enum CraneXYAxis
{
    Horizontal,
    Vertical
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class CraneXYLeverSwitch3D : MonoBehaviour, IInteractable3D
{
    [SerializeField] private CraneXYController3D targetCrane;
    [SerializeField] private CraneXYAxis controlledAxis;
    [SerializeField] private Collider interactionTrigger;
    [SerializeField] private LayerMask playerLayerMask;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private Renderer leverRenderer;
    [SerializeField] private bool playerInRange;
    [SerializeField] private int lastEnterSequence;

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
        return targetCrane != null && targetCrane.RequestAxisMove(controlledAxis, this, actor.transform);
    }

    public void Interact()
    {
        if (targetCrane != null) targetCrane.RequestAxisMove(controlledAxis, this, null);
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
}
