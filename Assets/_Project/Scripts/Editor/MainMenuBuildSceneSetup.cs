using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class MainMenuBuildSceneSetup
{
    private const string MainMenuScenePath = "Assets/_Project/Scenes/Title/MainMenu.unity";
    private const string StartRoomScenePath =
        "Assets/_Project/Scenes/Stages/ImportedSummerCampMap/Start_Room.unity";

    static MainMenuBuildSceneSetup()
    {
        EditorApplication.delayCall += EnsureRequiredScenes;
    }

    [MenuItem("Tools/Project/Ensure Main Menu Build Scenes")]
    public static void EnsureRequiredScenes()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuScenePath) == null ||
            AssetDatabase.LoadAssetAtPath<SceneAsset>(StartRoomScenePath) == null)
        {
            Debug.LogError("Required main menu scenes could not be found. Build scene list was not changed.");
            return;
        }

        var requiredPaths = new[]
        {
            MainMenuScenePath,
            StartRoomScenePath,
            "Assets/_Project/Scenes/Stages/ImportedSummerCampMap/hallwa_01.unity",
            "Assets/_Project/Scenes/Stages/ImportedSummerCampMap/middle_Room.unity",
            "Assets/_Project/Scenes/Stages/ImportedSummerCampMap/hallwa_02.unity",
            "Assets/_Project/Scenes/Stages/ImportedSummerCampMap/Item_Room_01.unity"
        };
        var existingScenes = EditorBuildSettings.scenes;
        var result = new List<EditorBuildSettingsScene>(existingScenes.Length + requiredPaths.Length);
        var seenPaths = new HashSet<string>();

        // Keep existing non-required scenes in their current order, but remove duplicate paths.
        foreach (EditorBuildSettingsScene scene in existingScenes)
        {
            if (IsRequired(scene.path, requiredPaths) || !seenPaths.Add(scene.path))
            {
                continue;
            }

            result.Add(scene);
        }

        // MainMenu must be first so player builds start at the menu.
        for (int i = requiredPaths.Length - 1; i >= 0; i--)
        {
            result.Insert(0, new EditorBuildSettingsScene(requiredPaths[i], true));
        }

        if (!SceneListsMatch(existingScenes, result))
        {
            EditorBuildSettings.scenes = result.ToArray();
            Debug.Log("Registered MainMenu and Start_Room in the shared build scene list.");
        }
    }

    private static bool IsRequired(string path, string[] requiredPaths)
    {
        foreach (string requiredPath in requiredPaths)
        {
            if (path == requiredPath)
            {
                return true;
            }
        }

        return false;
    }

    private static bool SceneListsMatch(EditorBuildSettingsScene[] current, List<EditorBuildSettingsScene> expected)
    {
        if (current.Length != expected.Count)
        {
            return false;
        }

        for (int i = 0; i < current.Length; i++)
        {
            if (current[i].path != expected[i].path || current[i].enabled != expected[i].enabled)
            {
                return false;
            }
        }

        return true;
    }
}
