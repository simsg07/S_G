using System.Collections.Generic;
using UnityEngine;

public static class WorldPresenceRegistry
{
    private static readonly List<WorldPresence> presences = new List<WorldPresence>();

    public static void Register(WorldPresence presence)
    {
        if (presence != null && !presences.Contains(presence))
        {
            presences.Add(presence);
        }
    }

    public static void Unregister(WorldPresence presence)
    {
        presences.Remove(presence);
    }

    public static void ApplyWorldState(WorldState world)
    {
        RemoveMissingEntries();
        for (int i = 0; i < presences.Count; i++)
        {
            if (presences[i] != null)
            {
                presences[i].ApplyWorldState(world);
            }
        }
    }

    public static void ApplyWorldState(ResearchWorldId world)
    {
        ApplyWorldState(world == ResearchWorldId.WorldA ? WorldState.WorldA : WorldState.WorldB);
    }

    public static void ApplyWorldState(TimelineWorldState world)
    {
        ApplyWorldState(world == TimelineWorldState.WorldA_Current ? WorldState.WorldA : WorldState.WorldB);
    }

    public static void RefreshAllFromScene(WorldState world)
    {
        WorldPresence[] scenePresences = Object.FindObjectsByType<WorldPresence>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < scenePresences.Length; i++)
        {
            Register(scenePresences[i]);
        }

        ApplyWorldState(world);
    }

    public static void RefreshAllFromScene(ResearchWorldId world)
    {
        RefreshAllFromScene(world == ResearchWorldId.WorldA ? WorldState.WorldA : WorldState.WorldB);
    }

    public static void RefreshAllFromScene(TimelineWorldState world)
    {
        RefreshAllFromScene(world == TimelineWorldState.WorldA_Current ? WorldState.WorldA : WorldState.WorldB);
    }

    private static void RemoveMissingEntries()
    {
        for (int i = presences.Count - 1; i >= 0; i--)
        {
            if (presences[i] == null)
            {
                presences.RemoveAt(i);
            }
        }
    }
}
