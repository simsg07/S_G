using System.Collections.Generic;
using UnityEngine;

public static class ShutterTargetRegistry3D
{
    private static readonly List<Entry> Entries = new List<Entry>(64);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Entries.Clear();

    public static int Count => Entries.Count;

    public static Entry Get(int index) => index >= 0 && index < Entries.Count ? Entries[index] : null;

    public static void Register(IMarkable3D target, Component component)
    {
        if (target == null || component == null) return;
        for (int i = Entries.Count - 1; i >= 0; i--)
        {
            Entry entry = Entries[i];
            if (entry == null || entry.Component == null) Entries.RemoveAt(i);
            else if (ReferenceEquals(entry.Target, target)) return;
        }
        Entries.Add(new Entry(target, component));
    }

    public static void Unregister(IMarkable3D target)
    {
        if (target == null) return;
        for (int i = Entries.Count - 1; i >= 0; i--)
        {
            Entry entry = Entries[i];
            if (entry == null || entry.Component == null || ReferenceEquals(entry.Target, target))
                Entries.RemoveAt(i);
        }
    }

    public sealed class Entry
    {
        private readonly Collider[] colliders;
        private readonly Renderer[] renderers;

        public Entry(IMarkable3D target, Component component)
        {
            Target = target;
            Component = component;
            colliders = component.GetComponentsInChildren<Collider>(true);
            renderers = component.GetComponentsInChildren<Renderer>(true);
            Rigidbody = component.GetComponentInChildren<Rigidbody>(true);
        }

        public IMarkable3D Target { get; }
        public Component Component { get; }
        public Rigidbody Rigidbody { get; }

        public Collider GetActiveCollider()
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy)
                    return collider;
            }
            return null;
        }

        public bool TryGetBounds(out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy) continue;
                if (!found) { bounds = collider.bounds; found = true; }
                else bounds.Encapsulate(collider.bounds);
            }
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return found;
        }
    }
}
