using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FiveStageFinalRepair
{
    private const string Folder = "Assets/_Project/Scenes/Stages/ImportedSummerCampMap/";
    private const string CollisionRoot = "[Generated3DCollision]";
    private const string AppliedKey = "SummerCamp.FiveStageFinalRepair.2026-07-23.v1";

    [InitializeOnLoadMethod]
    private static void Schedule()
    {
        if (!EditorPrefs.GetBool(AppliedKey, false))
        {
            EditorApplication.delayCall += Run;
        }
    }

    [MenuItem("Tools/Project/Repair First Five Collisions And Entrances")]
    private static void RunFromMenu()
    {
        EditorPrefs.DeleteKey(AppliedKey);
        Run();
    }

    public static void ApplyFromBatchMode()
    {
        EditorPrefs.DeleteKey(AppliedKey);
        Run();
    }

    private static void Run()
    {
        string previous = SceneManager.GetActiveScene().path;
        Repair("Start_Room",
            new[] { F("MainFloor", -16.5f, 24f, -11.5f), W("LeftWall", -17f, -11.5f, 8.5f), W("RightWallLower", 24.5f, -11.5f, -4.5f), C("Ceiling", -16.5f, 24f, 8.5f) },
            new Vector3(-14f, -10.87f, 0f), new Vector3(21.5f, -10.87f, 0f), null, null, null,
            null, new Exit("RightExit", new Vector3(24.8f, -9.2f, 0f), "hallwa_01", "LeftEntrance"));

        Repair("hallwa_01",
            new[] { F("LowerFloor", -30f, -2f, -5.6f), F("MiddleFloor", -22f, -10f, 6.1f), F("UpperFloor", -2.5f, 36.5f, 17.4f), W("LowerLeftWall", -30.3f, -5.6f, 0f), W("UpperRightWall", 36.8f, 17.4f, 24f) },
            new Vector3(-27.5f, -4.97f, 0f), new Vector3(33.5f, 18.03f, 0f), null, null, null,
            new Exit("LeftExit", new Vector3(-30.7f, -4.2f, 0f), "Start_Room", "RightEntrance"), new Exit("RightExit", new Vector3(37.2f, 19f, 0f), "middle_Room", "LeftEntrance"));

        Repair("middle_Room",
            new[] { F("LowerMainFloor", -35.5f, 65f, -14f), F("CenterPlatform", -0.5f, 35f, 8.5f), F("UpperRightLanding", 65f, 72f, 43f), W("LeftLowerWall", -36f, -14f, 20f), W("RightMainWallLower", 72.3f, -14f, 43f), C("TopBoundary", -35f, 72f, 142.5f) },
            new Vector3(-32.5f, -13.37f, 0f), new Vector3(68.5f, 43.63f, 0f), new Vector3(68.5f, 43.63f, 0f), new Vector3(68.5f, 9.13f, 0f), new Vector3(62f, -13.37f, 0f),
            new Exit("LeftExit", new Vector3(-36.4f, -12.4f, 0f), "hallwa_01", "RightEntrance"), new Exit("UpperRightExit", new Vector3(72.7f, 44.5f, 0f), "hallwa_02", "LeftEntrance"));

        Repair("hallwa_02",
            new[] { F("UpperCorridorFloor", -58f, -13f, -13.5f), F("LowerLanding", -13f, -6f, -32f), W("UpperLeftWall", -58.3f, -13.5f, -5f), W("ShaftLeftWall", -13.3f, -13.5f, -32f), W("ShaftRightWall", -5.7f, -13.5f, -32f), C("UpperCorridorCeiling", -58f, -6f, 1f) },
            new Vector3(-55f, -12.87f, 0f), new Vector3(-9.5f, -31.37f, 0f), null, null, null,
            new Exit("LeftExit", new Vector3(-58.7f, -11.7f, 0f), "middle_Room", "UpperRightEntrance"), new Exit("RightExit", new Vector3(-9.5f, -32.6f, 0f), "Item_Room_01", "LeftEntrance"));

        Repair("Item_Room_01",
            new[] { F("MainFloor", -43f, 29.5f, -13.5f), F("LeftPlatform", -30f, -19.5f, 12f), F("RightPlatform", 14f, 25f, -0.5f), F("RightExitLanding", 29.5f, 38f, 12.8f), W("LeftWall", -43.3f, -13.5f, 23f), W("RightWallLower", 29.8f, -13.5f, 12.8f), C("Ceiling", -43f, 29.5f, 32.5f) },
            new Vector3(-40f, -12.87f, 0f), new Vector3(35f, 13.43f, 0f), null, null, null,
            new Exit("LeftExit", new Vector3(-43.7f, -11.7f, 0f), "hallwa_02", "RightEntrance"), null);

        if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        AssetDatabase.SaveAssets();
        EditorPrefs.SetBool(AppliedKey, true);
        Debug.Log("[FiveStageFinalRepair] Five scenes repaired and saved.");
    }

    private static void Repair(string sceneName, Surface[] surfaces, Vector3 left, Vector3 right, Vector3? upper, Vector3? center, Vector3? lower, Exit leftExit, Exit rightExit)
    {
        Scene scene = EditorSceneManager.OpenScene(Folder + sceneName + ".unity", OpenSceneMode.Single);
        GameObject old = FindObject(scene, CollisionRoot);
        if (old != null) UnityEngine.Object.DestroyImmediate(old);
        int ground = LayerMask.NameToLayer("Ground");
        int wall = LayerMask.NameToLayer("Wall");
        var root = new GameObject(CollisionRoot);
        var floors = new GameObject("Ground"); floors.transform.SetParent(root.transform, false);
        var walls = new GameObject("Walls"); walls.transform.SetParent(root.transform, false);
        foreach (Surface surface in surfaces)
        {
            bool isFloor = surface.Type == SurfaceType.Floor;
            GameObject go = new GameObject(surface.Name);
            go.layer = isFloor ? ground : wall;
            go.transform.SetParent(isFloor ? floors.transform : walls.transform, false);
            go.transform.position = surface.Center;
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.size = surface.Size;
        }

        SetSpawn("LeftEntrance", left);
        SetSpawn("RightEntrance", right);
        if (upper.HasValue) SetSpawn("UpperRightEntrance", upper.Value, true);
        if (center.HasValue) SetSpawn("CenterRightEntrance", center.Value, true);
        if (lower.HasValue) SetSpawn("LowerRightEntrance", lower.Value, true);
        SetExit(leftExit);
        SetExit(rightExit);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[FiveStageFinalRepair] {sceneName}: {surfaces.Length} merged BoxColliders saved.");
    }

    private static void SetSpawn(string name, Vector3 position, bool create = false)
    {
        PlayerSpawnPoint spawn = FindComponent<PlayerSpawnPoint>(name);
        if (spawn == null && create)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Scene/PlayerSpawnPoint.prefab");
            Transform parent = GameObject.Find("[SceneConnections]/SpawnPoints")?.transform;
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            spawn = instance.GetComponent<PlayerSpawnPoint>();
            SerializedObject data = new SerializedObject(spawn);
            data.FindProperty("spawnPointId").stringValue = name;
            data.FindProperty("isDefaultSpawn").boolValue = false;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        if (spawn == null) throw new InvalidOperationException("Missing spawn: " + name);
        spawn.transform.SetPositionAndRotation(position, Quaternion.identity);
        spawn.transform.localScale = Vector3.one;
        PrefabUtility.RecordPrefabInstancePropertyModifications(spawn.transform);
    }

    private static void SetExit(Exit definition)
    {
        if (definition == null) return;
        StageExitTrigger exit = FindComponent<StageExitTrigger>(definition.Name);
        if (exit == null && definition.Name == "UpperRightExit")
        {
            exit = FindComponent<StageExitTrigger>("RightExit");
            if (exit != null) exit.name = "UpperRightExit";
        }
        if (exit == null) throw new InvalidOperationException("Missing exit: " + definition.Name);
        exit.transform.SetPositionAndRotation(definition.Position, Quaternion.identity);
        exit.transform.localScale = Vector3.one;
        SerializedObject data = new SerializedObject(exit);
        data.FindProperty("targetScene").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SceneAsset>(Folder + definition.Scene + ".unity");
        data.FindProperty("nextSceneName").stringValue = definition.Scene;
        data.FindProperty("targetSpawnPointId").stringValue = definition.Entrance;
        data.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(exit.transform);
    }

    private static T FindComponent<T>(string name) where T : Component
    {
        foreach (T component in UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (component.name == name) return component;
        return null;
    }

    private static GameObject FindObject(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects()) if (root.name == name) return root;
        return null;
    }

    private const float Thickness = 0.24f;
    private const float Epsilon = 0.02f;
    private static Surface F(string name, float min, float max, float top) => new Surface(name, SurfaceType.Floor, new Vector3((min + max) * .5f, top - Thickness * .5f, 0f), new Vector3(max - min + Epsilon, Thickness, 1f));
    private static Surface C(string name, float min, float max, float bottom) => new Surface(name, SurfaceType.Wall, new Vector3((min + max) * .5f, bottom + Thickness * .5f, 0f), new Vector3(max - min + Epsilon, Thickness, 1f));
    private static Surface W(string name, float x, float min, float max) => new Surface(name, SurfaceType.Wall, new Vector3(x, (min + max) * .5f, 0f), new Vector3(Thickness, max - min + Epsilon, 1f));
    private enum SurfaceType { Floor, Wall }
    private readonly struct Surface { public Surface(string name, SurfaceType type, Vector3 center, Vector3 size) { Name=name; Type=type; Center=center; Size=size; } public string Name { get; } public SurfaceType Type { get; } public Vector3 Center { get; } public Vector3 Size { get; } }
    private sealed class Exit { public Exit(string name, Vector3 position, string scene, string entrance) { Name=name; Position=position; Scene=scene; Entrance=entrance; } public string Name; public Vector3 Position; public string Scene; public string Entrance; }
}
