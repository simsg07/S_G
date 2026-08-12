#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PersistentSceneObjectValidator
{
    [MenuItem("Tools/Persistence/Validate All Persistent IDs")]
    public static void ValidateAllPersistentIds()
    {
        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
        var locationsById = new Dictionary<string, List<string>>();
        int emptyIds = 0;
        try
        {
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Project/Scenes" });
            foreach (string guid in sceneGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                PersistentSceneObject3D[] objects = Object.FindObjectsByType<PersistentSceneObject3D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (PersistentSceneObject3D item in objects)
                {
                    string location = $"{scene.name}/{GetHierarchyPath(item.transform)}";
                    if (string.IsNullOrWhiteSpace(item.PersistentId))
                    {
                        emptyIds++;
                        Debug.LogError($"[Persistence ID] Empty ID: {location}", item);
                        continue;
                    }
                    if (!locationsById.TryGetValue(item.PersistentId, out List<string> locations))
                    {
                        locations = new List<string>();
                        locationsById.Add(item.PersistentId, locations);
                    }
                    locations.Add(location);
                }
            }

            int duplicateIds = 0;
            foreach (KeyValuePair<string, List<string>> pair in locationsById)
            {
                if (pair.Value.Count <= 1) continue;
                duplicateIds++;
                Debug.LogError($"[Persistence ID] Duplicate '{pair.Key}': {string.Join(" | ", pair.Value)}");
            }

            ValidatePrefabSourceIds();
            Debug.Log($"[Persistence ID] Validation complete. Unique IDs={locationsById.Count}, empty={emptyIds}, duplicates={duplicateIds}.");
        }
        finally
        {
            if (previousSetup != null && previousSetup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
            }
        }
    }

    private static void ValidatePrefabSourceIds()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs" });
        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;
            PersistentSceneObject3D[] items = prefab.GetComponentsInChildren<PersistentSceneObject3D>(true);
            foreach (PersistentSceneObject3D item in items)
            {
                if (!string.IsNullOrWhiteSpace(item.PersistentId))
                {
                    Debug.LogWarning($"[Persistence ID] Prefab source has ID '{item.PersistentId}'. Clear it and assign a unique ID on each scene instance: {path}", item);
                }
            }
        }
    }

    private static string GetHierarchyPath(Transform target)
    {
        string path = target.name;
        while (target.parent != null)
        {
            target = target.parent;
            path = target.name + "/" + path;
        }
        return path;
    }
}
#endif
