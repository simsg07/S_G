using System.Collections.Generic;
using UnityEngine;

public interface IFocusingResettable3D
{
    void BeforeFocusingDespawn();
    void AfterFocusingSpawn();
}

/// <summary>
/// Contract for fixed scene objects that keep their instance and reset runtime state in place.
/// </summary>
public interface IFocusingInPlaceResettable3D
{
    void ResetForFocusingRing();
}

/// <summary>
/// Event-driven registry used only when a Focusing Ring reset snapshot is built.
/// </summary>
public static class FocusingInPlaceResetRegistry3D
{
    private static readonly HashSet<IFocusingInPlaceResettable3D> Registered =
        new HashSet<IFocusingInPlaceResettable3D>();
    private static readonly List<IFocusingInPlaceResettable3D> Stale =
        new List<IFocusingInPlaceResettable3D>(4);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearForNewRuntime()
    {
        Registered.Clear();
        Stale.Clear();
    }

    public static void Register(IFocusingInPlaceResettable3D target)
    {
        if (IsAlive(target)) Registered.Add(target);
    }

    public static void Unregister(IFocusingInPlaceResettable3D target)
    {
        if (target != null) Registered.Remove(target);
    }

    public static void CopyRegisteredTo(List<IFocusingInPlaceResettable3D> destination)
    {
        if (destination == null) return;

        Stale.Clear();
        foreach (IFocusingInPlaceResettable3D target in Registered)
        {
            if (IsAlive(target)) destination.Add(target);
            else Stale.Add(target);
        }

        for (int i = 0; i < Stale.Count; i++) Registered.Remove(Stale[i]);
        Stale.Clear();
    }

    public static bool IsAlive(IFocusingInPlaceResettable3D target)
    {
        if (target == null) return false;
        return !(target is Object unityObject) || unityObject != null;
    }
}
