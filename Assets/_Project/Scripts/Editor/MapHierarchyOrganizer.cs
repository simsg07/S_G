using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MapHierarchyOrganizer
{
    private const string SharedRootName = "Shared";
    private const string WorldACurrentRootName = "World_A_Current";
    private const string WorldBPastRootName = "World_B_Past";
    private const string MapVisualRootName = "Map_Visual";
    private const string MapCollisionRootName = "Map_Collision";
    private const string SceneTransitionsRootName = "Scene_Transitions";

    private const string FloorCollisionsName = "Floor_Collisions";
    private const string WallCollisionsName = "Wall_Collisions";
    private const string BlockCollisionsName = "Block_Collisions";
    private const string MonsterSightBlocksName = "MonsterSight_Blocks";
    private const string LightSightBlocksName = "LightSight_Blocks";
    private const string SpawnPointsName = "SpawnPoints";
    private const string ExitTriggersName = "ExitTriggers";

    [MenuItem("_Project/Map/Organize Current Scene Hierarchy")]
    public static void OrganizeCurrentSceneHierarchy()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            Debug.LogWarning("[MapHierarchyOrganizer] Active scene is not valid.");
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Organize Current Scene Hierarchy");
        int undoGroup = Undo.GetCurrentGroup();

        EnsureRoot(scene, MapVisualRootName);
        Transform mapCollisionRoot = EnsureRoot(scene, MapCollisionRootName).transform;
        Transform sceneTransitionsRoot = EnsureRoot(scene, SceneTransitionsRootName).transform;

        Transform floorParent = EnsureChild(mapCollisionRoot, FloorCollisionsName).transform;
        Transform wallParent = EnsureChild(mapCollisionRoot, WallCollisionsName).transform;
        Transform blockParent = EnsureChild(mapCollisionRoot, BlockCollisionsName).transform;
        Transform monsterSightParent = EnsureChild(mapCollisionRoot, MonsterSightBlocksName).transform;
        Transform lightSightParent = EnsureChild(mapCollisionRoot, LightSightBlocksName).transform;
        Transform spawnPointParent = EnsureChild(sceneTransitionsRoot, SpawnPointsName).transform;
        Transform exitTriggerParent = EnsureChild(sceneTransitionsRoot, ExitTriggersName).transform;

        int floorCount = 0;
        int wallCount = 0;
        int blockCount = 0;
        int monsterSightCount = 0;
        int lightSightCount = 0;
        int spawnPointCount = 0;
        int exitTriggerCount = 0;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            OrganizeBranch(
                roots[i].transform,
                floorParent,
                wallParent,
                blockParent,
                monsterSightParent,
                lightSightParent,
                spawnPointParent,
                exitTriggerParent,
                ref floorCount,
                ref wallCount,
                ref blockCount,
                ref monsterSightCount,
                ref lightSightCount,
                ref spawnPointCount,
                ref exitTriggerCount);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log($"[MapHierarchyOrganizer] {FloorCollisionsName} moved: {floorCount}");
        Debug.Log($"[MapHierarchyOrganizer] {WallCollisionsName} moved: {wallCount}");
        Debug.Log($"[MapHierarchyOrganizer] {BlockCollisionsName} moved: {blockCount}");
        Debug.Log($"[MapHierarchyOrganizer] {MonsterSightBlocksName} moved: {monsterSightCount}");
        Debug.Log($"[MapHierarchyOrganizer] {LightSightBlocksName} moved: {lightSightCount}");
        Debug.Log($"[MapHierarchyOrganizer] {SpawnPointsName} moved: {spawnPointCount}");
        Debug.Log($"[MapHierarchyOrganizer] {ExitTriggersName} moved: {exitTriggerCount}");
        Debug.Log("[MapHierarchyOrganizer] Complete. Review the hierarchy, then save the scene manually.");
    }

    private static void OrganizeBranch(
        Transform current,
        Transform floorParent,
        Transform wallParent,
        Transform blockParent,
        Transform monsterSightParent,
        Transform lightSightParent,
        Transform spawnPointParent,
        Transform exitTriggerParent,
        ref int floorCount,
        ref int wallCount,
        ref int blockCount,
        ref int monsterSightCount,
        ref int lightSightCount,
        ref int spawnPointCount,
        ref int exitTriggerCount)
    {
        if (current == null || ShouldSkipBranch(current))
        {
            return;
        }

        Transform[] children = GetChildrenSnapshot(current);
        for (int i = 0; i < children.Length; i++)
        {
            OrganizeBranch(
                children[i],
                floorParent,
                wallParent,
                blockParent,
                monsterSightParent,
                lightSightParent,
                spawnPointParent,
                exitTriggerParent,
                ref floorCount,
                ref wallCount,
                ref blockCount,
                ref monsterSightCount,
                ref lightSightCount,
                ref spawnPointCount,
                ref exitTriggerCount);
        }

        if (TryGetTargetParent(
                current.name,
                floorParent,
                wallParent,
                blockParent,
                monsterSightParent,
                lightSightParent,
                spawnPointParent,
                exitTriggerParent,
                out Transform targetParent,
                out HierarchyBucket bucket) &&
            MoveKeepingWorldTransform(current, targetParent))
        {
            IncrementBucket(
                bucket,
                ref floorCount,
                ref wallCount,
                ref blockCount,
                ref monsterSightCount,
                ref lightSightCount,
                ref spawnPointCount,
                ref exitTriggerCount);
        }
    }

    private static bool TryGetTargetParent(
        string objectName,
        Transform floorParent,
        Transform wallParent,
        Transform blockParent,
        Transform monsterSightParent,
        Transform lightSightParent,
        Transform spawnPointParent,
        Transform exitTriggerParent,
        out Transform targetParent,
        out HierarchyBucket bucket)
    {
        string normalizedName = StripCloneSuffix(objectName);

        if (IsNamed(normalizedName, "Floor_Collision"))
        {
            targetParent = floorParent;
            bucket = HierarchyBucket.Floor;
            return true;
        }

        if (IsNamed(normalizedName, "Wall_Tile"))
        {
            targetParent = wallParent;
            bucket = HierarchyBucket.Wall;
            return true;
        }

        if (IsNamed(normalizedName, "Block_Tile"))
        {
            targetParent = blockParent;
            bucket = HierarchyBucket.Block;
            return true;
        }

        if (IsNamed(normalizedName, "MonsterSightBlock") || IsNamed(normalizedName, "DetectionBlock"))
        {
            targetParent = monsterSightParent;
            bucket = HierarchyBucket.MonsterSight;
            return true;
        }

        if (IsNamed(normalizedName, "LightSightBlock"))
        {
            targetParent = lightSightParent;
            bucket = HierarchyBucket.LightSight;
            return true;
        }

        if (IsNamed(normalizedName, "PlayerSpawnPoint"))
        {
            targetParent = spawnPointParent;
            bucket = HierarchyBucket.SpawnPoint;
            return true;
        }

        if (IsNamed(normalizedName, "StageExitTrigger"))
        {
            targetParent = exitTriggerParent;
            bucket = HierarchyBucket.ExitTrigger;
            return true;
        }

        targetParent = null;
        bucket = HierarchyBucket.None;
        return false;
    }

    private static bool MoveKeepingWorldTransform(Transform target, Transform parent)
    {
        if (target == null || parent == null || target.parent == parent)
        {
            return false;
        }

        Undo.SetTransformParent(target, parent, "Organize Map Object");
        target.SetParent(parent, true);
        EditorUtility.SetDirty(target.gameObject);
        return true;
    }

    private static GameObject EnsureRoot(Scene scene, string rootName)
    {
        GameObject existing = FindRoot(scene, rootName);
        if (existing != null)
        {
            return existing;
        }

        GameObject created = new GameObject(rootName);
        Undo.RegisterCreatedObjectUndo(created, $"Create {rootName}");
        SceneManager.MoveGameObjectToScene(created, scene);
        return created;
    }

    private static GameObject EnsureChild(Transform parent, string childName)
    {
        Transform existing = parent.Find(childName);
        if (existing != null)
        {
            return existing.gameObject;
        }

        GameObject created = new GameObject(childName);
        Undo.RegisterCreatedObjectUndo(created, $"Create {childName}");
        created.transform.SetParent(parent, false);
        return created;
    }

    private static GameObject FindRoot(Scene scene, string rootName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == rootName)
            {
                return roots[i];
            }
        }

        return null;
    }

    private static Transform[] GetChildrenSnapshot(Transform parent)
    {
        Transform[] children = new Transform[parent.childCount];
        for (int i = 0; i < children.Length; i++)
        {
            children[i] = parent.GetChild(i);
        }

        return children;
    }

    private static bool ShouldSkipBranch(Transform target)
    {
        string name = target.name;
        if (name == SharedRootName ||
            name == WorldACurrentRootName ||
            name == WorldBPastRootName)
        {
            return true;
        }

        return HasAncestorNamed(target, WorldACurrentRootName) ||
               HasAncestorNamed(target, WorldBPastRootName);
    }

    private static bool HasAncestorNamed(Transform target, string ancestorName)
    {
        Transform current = target.parent;
        while (current != null)
        {
            if (current.name == ancestorName)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static bool IsNamed(string objectName, string targetName)
    {
        return objectName.Equals(targetName, StringComparison.Ordinal) ||
               objectName.StartsWith(targetName + "_", StringComparison.Ordinal);
    }

    private static string StripCloneSuffix(string objectName)
    {
        int suffixIndex = objectName.IndexOf(" (", StringComparison.Ordinal);
        return suffixIndex > 0 ? objectName.Substring(0, suffixIndex) : objectName;
    }

    private static void IncrementBucket(
        HierarchyBucket bucket,
        ref int floorCount,
        ref int wallCount,
        ref int blockCount,
        ref int monsterSightCount,
        ref int lightSightCount,
        ref int spawnPointCount,
        ref int exitTriggerCount)
    {
        switch (bucket)
        {
            case HierarchyBucket.Floor:
                floorCount++;
                break;
            case HierarchyBucket.Wall:
                wallCount++;
                break;
            case HierarchyBucket.Block:
                blockCount++;
                break;
            case HierarchyBucket.MonsterSight:
                monsterSightCount++;
                break;
            case HierarchyBucket.LightSight:
                lightSightCount++;
                break;
            case HierarchyBucket.SpawnPoint:
                spawnPointCount++;
                break;
            case HierarchyBucket.ExitTrigger:
                exitTriggerCount++;
                break;
        }
    }

    private enum HierarchyBucket
    {
        None,
        Floor,
        Wall,
        Block,
        MonsterSight,
        LightSight,
        SpawnPoint,
        ExitTrigger
    }
}
