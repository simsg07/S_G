#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneConnectionValidatorWindow : EditorWindow
{
    private const string StageRoot = "Assets/_Project/Scenes/Stages";
    private Vector2 scroll;

    [MenuItem("_Project/Scene/Scene Connection Validator")]
    private static void Open() => GetWindow<SceneConnectionValidatorWindow>("Scene Connections");

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("Scene Connection Validator", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Portal의 대상 Scene 이름과 대상 Scene의 Spawn ID가 정확히 같아야 합니다. 검사는 Warning만 출력합니다.", MessageType.Info);
        if (GUILayout.Button("Validate Current Scene Connections")) ValidateCurrentSceneConnections();
        if (GUILayout.Button("Validate All Stage Scene Connections")) ValidateAllStageSceneConnections();
        if (GUILayout.Button("Add Stage Scenes To Build Settings")) AddStageScenesToBuildSettings();
        if (GUILayout.Button("Create Scene Transition Folders")) CreateSceneTransitionFolders();
        if (GUILayout.Button("Create Or Update Scene Connection Prefabs")) CreateOrUpdatePrefabs();
        EditorGUILayout.EndScrollView();
    }

    [MenuItem("_Project/Scene/Validate Current Scene Connections")]
    public static void ValidateCurrentSceneConnections()
    {
        SceneConnectionValidator.ValidateLoadedScene(SceneManager.GetActiveScene());
    }

    [MenuItem("_Project/Scene/Validate All Stage Scene Connections")]
    public static void ValidateAllStageSceneConnections()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
        string[] scenePaths = AssetDatabase.FindAssets("t:Scene", new[] { StageRoot })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path).ToArray();
        int warnings = 0;
        try
        {
            foreach (string path in scenePaths)
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                warnings += SceneConnectionValidator.ValidateLoadedScene(scene, false);
            }
        }
        finally
        {
            if (originalSetup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
        }
        Debug.Log($"[SceneConnectionValidator] All Stage scenes checked: {scenePaths.Length}, total warnings: {warnings}");
    }

    [MenuItem("_Project/Scene/Add Stage Scenes To Build Settings")]
    public static void AddStageScenesToBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        HashSet<string> existing = new HashSet<string>(scenes.Select(scene => scene.path), StringComparer.Ordinal);
        int added = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { StageRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (existing.Add(path))
            {
                scenes.Add(new EditorBuildSettingsScene(path, true));
                added++;
            }
        }
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log($"[SceneConnectionValidator] Added {added} Stage scene(s) to Build Settings. Existing entries were preserved.");
    }

    [MenuItem("_Project/Scene/Create Scene Transition Folders")]
    public static void CreateSceneTransitionFolders()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject root = FindRoot(scene, "Scene_Transitions") ?? new GameObject("Scene_Transitions");
        Undo.RegisterCreatedObjectUndo(root, "Create Scene Transition Folders");
        EnsureChild(root.transform, "Portals");
        EnsureChild(root.transform, "SpawnPoints");
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = root;
    }

    [MenuItem("_Project/Scene/Create Or Update Scene Connection Prefabs")]
    public static void CreateOrUpdatePrefabs()
    {
        const string folder = "Assets/_Project/Prefabs/Scene";
        EnsureAssetFolder(folder);

        GameObject portal = new GameObject("Portal_Exit");
        BoxCollider collider = portal.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = new Vector3(1f, 2f, 1f);
        portal.AddComponent<ScenePortal3D>();
        CreateDebugVisual(portal.transform, "DebugVisual", new Vector3(1f, 2f, 0.25f), new Color(1f, 0.5f, 0f));
        PrefabUtility.SaveAsPrefabAsset(portal, folder + "/Portal_Exit.prefab");
        DestroyImmediate(portal);

        GameObject spawn = new GameObject("SpawnPoint");
        spawn.AddComponent<SceneSpawnPoint3D>();
        CreateDebugVisual(spawn.transform, "DebugVisual", Vector3.one * 0.35f, Color.cyan);
        PrefabUtility.SaveAsPrefabAsset(spawn, folder + "/SpawnPoint.prefab");
        DestroyImmediate(spawn);

        GameObject manager = new GameObject("SceneTransitionManager");
        manager.AddComponent<SceneTransitionManager>();
        PrefabUtility.SaveAsPrefabAsset(manager, folder + "/SceneTransitionManager.prefab");
        DestroyImmediate(manager);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[SceneConnectionValidator] Scene connection prefabs created/updated.");
    }

    private static void CreateDebugVisual(Transform parent, string name, Vector3 scale, Color color)
    {
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = name;
        visual.transform.SetParent(parent, false);
        visual.transform.localScale = scale;
        Collider visualCollider = visual.GetComponent<Collider>();
        if (visualCollider != null) DestroyImmediate(visualCollider);
        Renderer renderer = visual.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
        visual.SetActive(true);
    }

    private static GameObject FindRoot(Scene scene, string name)
        => scene.GetRootGameObjects().FirstOrDefault(root => root.name == name);

    private static void EnsureChild(Transform parent, string name)
    {
        if (parent.Find(name) != null) return;
        GameObject child = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(child, "Create Scene Transition Folders");
        child.transform.SetParent(parent, false);
    }

    private static void EnsureAssetFolder(string path)
    {
        string current = "Assets";
        foreach (string part in path.Split('/').Skip(1))
        {
            string next = current + "/" + part;
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, part);
            current = next;
        }
    }
}
#endif
