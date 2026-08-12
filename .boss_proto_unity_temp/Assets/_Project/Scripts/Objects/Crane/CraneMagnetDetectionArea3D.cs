using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(Collider))]
public sealed class CraneMagnetDetectionArea3D : MonoBehaviour
{
    private readonly HashSet<MagneticCarryable3D> candidates = new HashSet<MagneticCarryable3D>();
    public IEnumerable<MagneticCarryable3D> Candidates => candidates;
    private void Awake() { Collider c = GetComponent<Collider>(); c.isTrigger = true; }
    private void OnDisable() => candidates.Clear();
    private void OnTriggerEnter(Collider other) { MagneticCarryable3D c = other.GetComponentInParent<MagneticCarryable3D>(); if (c != null) candidates.Add(c); }
    private void OnTriggerExit(Collider other)
    {
        MagneticCarryable3D c = other.GetComponentInParent<MagneticCarryable3D>();
        if (c == null) return;
        Collider[] colliders = c.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++) if (colliders[i] != other && GetComponent<Collider>().bounds.Intersects(colliders[i].bounds)) return;
        candidates.Remove(c);
    }
}
