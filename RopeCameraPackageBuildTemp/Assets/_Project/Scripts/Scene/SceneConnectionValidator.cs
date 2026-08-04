using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class SceneConnectionValidator
{
    public static int ValidateLoadedScene(Scene scene, bool logPortalOk = true)
    {
        int warnings = 0;
        ScenePortal3D[] portals = Object.FindObjectsByType<ScenePortal3D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        SceneSpawnPoint3D[] points = Object.FindObjectsByType<SceneSpawnPoint3D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Dictionary<string, int> spawnCounts = new Dictionary<string, int>();
        bool hasDefault = false;

        foreach (SceneSpawnPoint3D point in points)
        {
            if (point == null || point.gameObject.scene != scene) continue;
            string id = point.SpawnId;
            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogWarning($"[SceneConnectionValidator] Missing spawnId: {GetPath(point.transform)}", point);
                warnings++;
                continue;
            }
            spawnCounts.TryGetValue(id, out int count);
            spawnCounts[id] = count + 1;
            if (point.IsDefaultSpawn) hasDefault = true;
        }

        foreach (KeyValuePair<string, int> pair in spawnCounts)
        {
            if (pair.Value <= 1) continue;
            Debug.LogWarning($"[SceneConnectionValidator] Duplicate spawnId: {pair.Key} ({pair.Value})");
            warnings++;
        }

        if (!hasDefault)
        {
            Debug.LogWarning($"[SceneConnectionValidator] No default SceneSpawnPoint3D in scene: {scene.name}");
            warnings++;
        }

        foreach (ScenePortal3D portal in portals)
        {
            if (portal == null || portal.gameObject.scene != scene) continue;
            bool valid = true;
            if (string.IsNullOrWhiteSpace(portal.TargetSceneName))
            {
                Debug.LogWarning($"[SceneConnectionValidator] Missing targetSceneName: {portal.PortalId}", portal);
                warnings++;
                valid = false;
            }
            if (string.IsNullOrWhiteSpace(portal.TargetSpawnId))
            {
                Debug.LogWarning($"[SceneConnectionValidator] Missing targetSpawnId: {portal.PortalId}", portal);
                warnings++;
                valid = false;
            }
            if (portal.TriggerCollider == null || !portal.TriggerCollider.isTrigger)
            {
                Debug.LogWarning($"[SceneConnectionValidator] Portal Collider missing or isTrigger=false: {portal.PortalId}", portal);
                warnings++;
                valid = false;
            }
            if (!string.IsNullOrWhiteSpace(portal.TargetSceneName) && !SceneLoader.IsSceneRegisteredInBuildSettings(portal.TargetSceneName))
            {
                Debug.LogWarning($"[SceneConnectionValidator] Scene not in Build Settings: {portal.TargetSceneName}", portal);
                warnings++;
                valid = false;
            }
            if (valid && logPortalOk) Debug.Log($"[SceneConnectionValidator] Portal OK: {portal.PortalId}", portal);
        }

#if UNITY_EDITOR
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            int missing = CountMissingScripts(root);
            if (missing <= 0) continue;
            Debug.LogWarning($"[SceneConnectionValidator] Missing Script count under '{root.name}': {missing}", root);
            warnings += missing;
        }
#endif
        Debug.Log($"[SceneConnectionValidator] '{scene.name}' validation complete. Warning count: {warnings}");
        return warnings;
    }

    private static string GetPath(Transform target)
    {
        string path = target.name;
        while (target.parent != null)
        {
            target = target.parent;
            path = target.name + "/" + path;
        }
        return path;
    }

#if UNITY_EDITOR
    private static int CountMissingScripts(GameObject gameObject)
    {
        int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
        foreach (Transform child in gameObject.transform) count += CountMissingScripts(child.gameObject);
        return count;
    }
#endif
}
