using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FiveStageFinalRepair
{
    private const string Folder = "Assets/_Project/Scenes/Stages/ImportedSummerCampMap/";
    private const string CollisionRoot = "Collision";

    [MenuItem("Tools/Project/Create Missing First Five Border Collisions")]
    public static void CreateMissingFromMenu()
    {
        Run(false);
    }

    [MenuItem("Tools/Project/Rebuild First Five Border Collisions")]
    public static void RunFromMenu()
    {
        if (!EditorUtility.DisplayDialog(
                "경계 충돌체 다시 생성",
                "5개 씬의 Collision 아래 수동 Collider를 삭제하고 사전 정의된 바닥·벽·천장 경계로 교체합니다. 계속할까요?\n\n기존 Entrance와 Exit Transform은 변경하지 않습니다.",
                "교체", "취소"))
        {
            return;
        }
        Run(true);
    }

    public static void ApplyFromBatchMode()
    {
        Run(false);
    }

    private static void Run(bool replaceExisting)
    {
        string previous = SceneManager.GetActiveScene().path;
        Repair("Start_Room", replaceExisting,
            new[] { F("MainFloor", -16.5f, 24f, -11.5f), W("LeftWall", -17f, -11.5f, 8.5f), W("RightWallLower", 24.5f, -11.5f, -4.5f), C("Ceiling", -16.5f, 24f, 8.5f) });

        Repair("hallwa_01", replaceExisting,
            new[] { F("LowerFloor", -30f, -2f, -5.6f), F("MiddleFloor", -22f, -10f, 6.1f), F("UpperFloor", -2.5f, 36.5f, 17.4f), W("LowerLeftWall", -30.3f, -5.6f, 0f), W("UpperRightWall", 36.8f, 17.4f, 24f) });

        Repair("middle_Room", replaceExisting,
            new[] { F("LowerMainFloor", -35.5f, 65f, -14f), F("CenterPlatform", -0.5f, 35f, 8.5f), F("UpperRightLanding", 65f, 72f, 43f), W("LeftLowerWall", -36f, -14f, 20f), W("RightMainWallLower", 72.3f, -14f, 43f), C("TopBoundary", -35f, 72f, 142.5f) });

        Repair("hallwa_02", replaceExisting,
            new[] { F("UpperCorridorFloor", -58f, -13f, -13.5f), F("LowerLanding", -13f, -6f, -32f), W("UpperLeftWall", -58.3f, -13.5f, -5f), W("ShaftLeftWall", -13.3f, -13.5f, -32f), W("ShaftRightWall", -5.7f, -13.5f, -32f), C("UpperCorridorCeiling", -58f, -6f, 1f) });

        Repair("Item_Room_01", replaceExisting,
            new[] { F("MainFloor", -43f, 29.5f, -13.5f), F("LeftPlatform", -30f, -19.5f, 12f), F("RightPlatform", 14f, 25f, -0.5f), F("RightExitLanding", 29.5f, 38f, 12.8f), W("LeftWall", -43.3f, -13.5f, 23f), W("RightWallLower", 29.8f, -13.5f, 12.8f), C("Ceiling", -43f, 29.5f, 32.5f) });

        if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        AssetDatabase.SaveAssets();
        Debug.Log(replaceExisting
            ? "[Map Border Collision] 확인 후 5개 씬의 경계 충돌체를 교체했습니다. Entrance와 Exit는 변경하지 않았습니다."
            : "[Map Border Collision] Collision이 없는 씬만 생성했습니다. 기존 수동 Collider는 건너뛰었습니다.");
    }

    private static void Repair(string sceneName, bool replaceExisting, Surface[] surfaces)
    {
        Scene scene = EditorSceneManager.OpenScene(Folder + sceneName + ".unity", OpenSceneMode.Single);
        GameObject old = FindObject(scene, CollisionRoot) ?? FindObject(scene, "[Generated3DCollision]");
        if (old != null && !replaceExisting)
        {
            Debug.Log($"[Map Border Collision] {sceneName}: 기존 Collision을 보존하고 생성을 건너뛰었습니다.", old);
            return;
        }

        RemoveTilemapColliders();
        if (old != null)
        {
            Undo.RegisterFullObjectHierarchyUndo(old, "경계 충돌체 교체");
            Undo.DestroyObjectImmediate(old);
        }
        int ground = LayerMask.NameToLayer("Ground");
        int wall = LayerMask.NameToLayer("Wall");
        var root = new GameObject(CollisionRoot);
        Undo.RegisterCreatedObjectUndo(root, "경계 충돌체 생성");
        var floors = new GameObject("Ground"); floors.transform.SetParent(root.transform, false);
        var walls = new GameObject("Wall"); walls.transform.SetParent(root.transform, false);
        var ceilings = new GameObject("Ceiling"); ceilings.transform.SetParent(root.transform, false);
        foreach (Surface surface in surfaces)
        {
            bool isFloor = surface.Type == SurfaceType.Floor;
            GameObject go = new GameObject(surface.Name);
            go.layer = isFloor ? ground : wall;
            go.transform.SetParent(isFloor ? floors.transform : surface.Type == SurfaceType.Ceiling ? ceilings.transform : walls.transform, false);
            go.transform.position = surface.Center;
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.isTrigger = false;
            box.size = new Vector3(Mathf.Abs(surface.Size.x), Mathf.Abs(surface.Size.y), Mathf.Abs(surface.Size.z));
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[FiveStageFinalRepair] {sceneName}: {surfaces.Length} merged BoxColliders saved.");
    }

    private static void RemoveTilemapColliders()
    {
        foreach (UnityEngine.Tilemaps.Tilemap tilemap in UnityEngine.Object.FindObjectsByType<UnityEngine.Tilemaps.Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (tilemap.name != "Visual_Tilemap") continue;
            foreach (Collider collider in tilemap.GetComponents<Collider>()) Undo.DestroyObjectImmediate(collider);
            foreach (Collider2D collider in tilemap.GetComponents<Collider2D>()) Undo.DestroyObjectImmediate(collider);
        }
    }

    private static GameObject FindObject(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects()) if (root.name == name) return root;
        return null;
    }

    private const float Thickness = 0.24f;
    private const float Epsilon = 0.02f;
    private static Surface F(string name, float min, float max, float top) { Sort(ref min, ref max); return new Surface(name, SurfaceType.Floor, new Vector3((min + max) * .5f, top - Thickness * .5f, 0f), new Vector3(max - min + Epsilon, Thickness, 1f)); }
    private static Surface C(string name, float min, float max, float bottom) { Sort(ref min, ref max); return new Surface(name, SurfaceType.Ceiling, new Vector3((min + max) * .5f, bottom + Thickness * .5f, 0f), new Vector3(max - min + Epsilon, Thickness, 1f)); }
    private static Surface W(string name, float x, float min, float max) { Sort(ref min, ref max); return new Surface(name, SurfaceType.Wall, new Vector3(x, (min + max) * .5f, 0f), new Vector3(Thickness, max - min + Epsilon, 1f)); }
    private static void Sort(ref float min, ref float max) { if (min <= max) return; float value = min; min = max; max = value; }
    private enum SurfaceType { Floor, Wall, Ceiling }
    private readonly struct Surface { public Surface(string name, SurfaceType type, Vector3 center, Vector3 size) { Name=name; Type=type; Center=center; Size=size; } public string Name { get; } public SurfaceType Type { get; } public Vector3 Center { get; } public Vector3 Size { get; } }
}
