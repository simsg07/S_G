using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(Collider))]
public sealed class CraneMagnetControlTrigger3D : MonoBehaviour
{
    [SerializeField] private LayerMask playerLayerMask;
    [SerializeField] private string playerTag = "Player";
    private readonly HashSet<Collider> players = new HashSet<Collider>();
    public bool PlayerInRange => players.Count > 0;

    private void Awake() { Collider c = GetComponent<Collider>(); c.isTrigger = true; }
    private void OnDisable() => players.Clear();
    private void OnTriggerEnter(Collider other) { if (IsPlayer(other.transform)) players.Add(other); }
    private void OnTriggerExit(Collider other) { players.Remove(other); }

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
