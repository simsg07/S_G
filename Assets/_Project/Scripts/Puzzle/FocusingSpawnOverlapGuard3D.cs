using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FocusingSpawnOverlapGuard3D : MonoBehaviour
{
    private readonly List<Collider> temporarilyDisabled = new List<Collider>(8);
    private readonly List<Bounds> solidSpawnBounds = new List<Bounds>(8);
    private Collider[] playerColliders;
    private Coroutine protectionRoutine;

    public void ProtectUntilSeparated(Transform player)
    {
        if (player == null) return;
        playerColliders = player.GetComponentsInChildren<Collider>(true);
        Collider[] spawnedColliders = GetComponentsInChildren<Collider>(true);
        if (!OverlapsPlayer(spawnedColliders)) return;

        temporarilyDisabled.Clear();
        solidSpawnBounds.Clear();
        for (int i = 0; i < spawnedColliders.Length; i++)
        {
            Collider collider = spawnedColliders[i];
            if (collider == null) continue;
            if (!collider.isTrigger && collider.enabled) solidSpawnBounds.Add(collider.bounds);
            if (!collider.enabled) continue;
            temporarilyDisabled.Add(collider);
            collider.enabled = false;
        }

        if (protectionRoutine != null) StopCoroutine(protectionRoutine);
        protectionRoutine = StartCoroutine(WaitUntilPlayerLeaves());
    }

    private IEnumerator WaitUntilPlayerLeaves()
    {
        // Keep the player fixed. Collision returns only after the player leaves the spawn volume.
        do { yield return null; }
        while (PlayerIntersectsSolidBounds());

        for (int i = 0; i < temporarilyDisabled.Count; i++)
            if (temporarilyDisabled[i] != null) temporarilyDisabled[i].enabled = true;
        temporarilyDisabled.Clear();
        solidSpawnBounds.Clear();
        protectionRoutine = null;
    }

    private bool OverlapsPlayer(Collider[] spawnedColliders)
    {
        if (playerColliders == null) return false;
        for (int i = 0; i < spawnedColliders.Length; i++)
        {
            Collider spawned = spawnedColliders[i];
            if (spawned == null || !spawned.enabled || spawned.isTrigger) continue;
            for (int j = 0; j < playerColliders.Length; j++)
            {
                Collider player = playerColliders[j];
                if (player != null && player.enabled && spawned.bounds.Intersects(player.bounds)) return true;
            }
        }
        return false;
    }

    private bool PlayerIntersectsSolidBounds()
    {
        if (playerColliders == null) return false;
        for (int i = 0; i < solidSpawnBounds.Count; i++)
        {
            Bounds spawnedBounds = solidSpawnBounds[i];
            for (int j = 0; j < playerColliders.Length; j++)
            {
                Collider player = playerColliders[j];
                if (player != null && player.enabled && spawnedBounds.Intersects(player.bounds)) return true;
            }
        }
        return false;
    }
}
