using UnityEngine;

[DisallowMultipleComponent]
public sealed class CheckpointInteractionTrigger3D : MonoBehaviour
{
    private Checkpoint3D owner;

    public void Bind(Checkpoint3D checkpoint) => owner = checkpoint;

    private void Awake()
    {
        if (owner == null) owner = GetComponentInParent<Checkpoint3D>();
    }

    private void OnTriggerEnter(Collider other) => owner?.NotifyPlayerTriggerEnter(other);

    private void OnTriggerExit(Collider other) => owner?.NotifyPlayerTriggerExit(other);
}
