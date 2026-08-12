using System.Collections.Generic;
using UnityEngine;

public static class ShutterTargetRegistry3D
{
    private static readonly List<Entry> Entries = new List<Entry>(64);
    private static readonly List<MarkEntry> ActiveMarks = new List<MarkEntry>(32);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Entries.Clear();
        ActiveMarks.Clear();
    }

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
        if (target is IShutterFreezable3D freezable) RemoveFreezeEntry(freezable);
    }

    public static void CancelAllFreezes()
    {
        for (int i = ActiveMarks.Count - 1; i >= 0; i--)
        {
            MarkEntry entry = ActiveMarks[i];
            ActiveMarks.RemoveAt(i);
            if (entry.Component != null) entry.Target.ReleaseShutterFreeze();
        }
    }

    public static void CancelFreezesInHierarchy(GameObject root)
    {
        if (root == null) return;
        Transform rootTransform = root.transform;
        for (int i = ActiveMarks.Count - 1; i >= 0; i--)
        {
            MarkEntry entry = ActiveMarks[i];
            Component component = entry.Component;
            if (component == null)
            {
                ActiveMarks.RemoveAt(i);
                continue;
            }
            Transform targetTransform = component.transform;
            if (targetTransform != rootTransform && !targetTransform.IsChildOf(rootTransform)) continue;
            ActiveMarks.RemoveAt(i);
            entry.Target.ReleaseShutterFreeze();
        }
    }

    public static void RemoveFreezeEntry(IShutterFreezable3D target)
    {
        if (target == null) return;
        for (int i = ActiveMarks.Count - 1; i >= 0; i--)
        {
            MarkEntry entry = ActiveMarks[i];
            if (entry.Component == null || ReferenceEquals(entry.Target, target)) ActiveMarks.RemoveAt(i);
        }
    }

    public static bool IsFreezeRegistered(IShutterFreezable3D target)
    {
        if (target == null) return false;
        for (int i = ActiveMarks.Count - 1; i >= 0; i--)
        {
            MarkEntry entry = ActiveMarks[i];
            if (entry.Component == null)
            {
                ActiveMarks.RemoveAt(i);
                continue;
            }
            if (ReferenceEquals(entry.Target, target)) return true;
        }
        return false;
    }

    public static int ActiveMarkCount
    {
        get { RemoveDeadMarks(); return ActiveMarks.Count; }
    }

    public static bool TryRegisterMark(IShutterFreezable3D target, Component component, int maxActiveMarks)
    {
        if (target == null || component == null || maxActiveMarks < 1) return false;
        RemoveDeadMarks();
        for (int i = 0; i < ActiveMarks.Count; i++)
            if (ReferenceEquals(ActiveMarks[i].Target, target)) return true;
        if (ActiveMarks.Count >= maxActiveMarks) return false;
        ActiveMarks.Add(new MarkEntry(target, component));
        return true;
    }

    public static bool ReleaseMostRecentMark()
    {
        RemoveDeadMarks();
        if (ActiveMarks.Count == 0) return false;
        int index = ActiveMarks.Count - 1;
        MarkEntry entry = ActiveMarks[index];
        if (entry.Component == null)
        {
            ActiveMarks.RemoveAt(index);
            return false;
        }
        entry.Target.ReleaseShutterFreeze();
        return true;
    }

    private static void RemoveDeadMarks()
    {
        for (int i = ActiveMarks.Count - 1; i >= 0; i--)
        {
            if (ActiveMarks[i].Component == null) ActiveMarks.RemoveAt(i);
        }
    }

    private sealed class MarkEntry
    {
        public MarkEntry(IShutterFreezable3D target, Component component)
        {
            Target = target;
            Component = component;
        }
        public readonly IShutterFreezable3D Target;
        public readonly Component Component;
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
