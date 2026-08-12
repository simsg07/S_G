using UnityEngine;

[DisallowMultipleComponent]
public sealed class FocusingSpawnedInstance3D : MonoBehaviour
{
    private FocusingSpawner3D owner;
    private GameObject trackedInstance;

    public void Bind(FocusingSpawner3D spawner, GameObject instance)
    {
        owner = spawner;
        trackedInstance = instance;
    }

    public void Unbind(FocusingSpawner3D spawner)
    {
        if (owner != spawner) return;
        owner = null;
        trackedInstance = null;
    }

    private void OnDisable()
    {
        if (owner != null) owner.NotifyInstanceDied(trackedInstance);
    }

    private void OnDestroy()
    {
        if (owner != null) owner.NotifyInstanceDied(trackedInstance);
    }
}
