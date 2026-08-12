using UnityEngine;

public static class WorldDamageFilter3D
{
    private const int CurrentMask = 1;
    private const int PastMask = 2;

    public static bool CanAffect(Component attacker, Component target)
    {
        if (attacker == null || target == null) return true;
        bool attackerKnown = TryResolveMask(attacker, true, out int attackerMask);
        bool targetKnown = TryResolveMask(target, false, out int targetMask);
        return !attackerKnown || !targetKnown || (attackerMask & targetMask) != 0;
    }

    private static bool TryResolveMask(Component component, bool useActiveWorldFallback, out int mask)
    {
        WorldPresence presence = component.GetComponentInParent<WorldPresence>();
        if (presence == null) presence = component.GetComponentInChildren<WorldPresence>(true);
        if (presence != null)
        {
            switch (presence.PresenceMode)
            {
                case WorldPresenceMode.WorldAOnly: mask = CurrentMask; return true;
                case WorldPresenceMode.WorldBOnly: mask = PastMask; return true;
                default: mask = CurrentMask | PastMask; return true;
            }
        }

        BlockObject wall = component.GetComponentInParent<BlockObject>();
        if (wall != null)
        {
            mask = wall.WorldRole == TemporalWeakWallWorldRole.Past ? PastMask : CurrentMask;
            return true;
        }

        Transform root = component.transform.root;
        if (component.CompareTag("Player") || (root != null && root.CompareTag("Player")) || useActiveWorldFallback)
        {
            mask = WorldSystem3D.ActiveWorld == ResearchWorldId.WorldA ? CurrentMask : PastMask;
            return true;
        }

        mask = 0;
        return false;
    }
}
