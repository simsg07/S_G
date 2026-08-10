using System;
using System.Collections.Generic;

public static class GameplayInputLock3D
{
    private static readonly HashSet<object> Owners = new HashSet<object>();

    public static event Action LockStateChanged;

    public static bool IsLocked => Owners.Count > 0;

    public static bool Acquire(object owner)
    {
        if (owner == null || !Owners.Add(owner)) return false;
        LockStateChanged?.Invoke();
        return true;
    }

    public static void Release(object owner)
    {
        if (owner == null || !Owners.Remove(owner)) return;
        LockStateChanged?.Invoke();
    }

    public static bool IsLockedByOther(object owner)
    {
        if (Owners.Count == 0) return false;
        if (owner == null || !Owners.Contains(owner)) return true;
        return Owners.Count > 1;
    }
}
