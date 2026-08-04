using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SpawnedObjectLifecycle : MonoBehaviour
{
    public event Action<SpawnedObjectLifecycle> Despawned;

    private bool notified;
    private bool sceneUnloading;

    public void MarkSceneUnloading()
    {
        sceneUnloading = true;
    }

    public void NotifyGameplayDespawn()
    {
        if (notified || sceneUnloading)
        {
            return;
        }

        notified = true;
        Despawned?.Invoke(this);
    }

    private void OnDisable()
    {
        if (Application.isPlaying && gameObject.scene.isLoaded)
        {
            NotifyGameplayDespawn();
        }
    }

    private void OnDestroy()
    {
        if (Application.isPlaying && gameObject.scene.isLoaded)
        {
            NotifyGameplayDespawn();
        }
    }
}
