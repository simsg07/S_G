using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class ImportedMap3DCollisionBuilder
{
    private const string SceneFolder = "Assets/_Project/Scenes/Stages/ImportedSummerCampMap/";
    private const string CollisionRootName = "[Generated3DCollision]";
    private const float ColliderDepth = 1f;

    private static readonly string[] SceneNames =
    {
        "Start_Room",
        "hallwa_01",
        "middle_Room",
        "hallwa_02",
        "Item_Room_01"
    };

    [MenuItem("Tools/Project/Rebuild First Five 3D Map Collisions")]
    public static void RebuildFromMenu()
    {
        Debug.LogWarning("[ImportedMap3DCollisionBuilder] Automatic tile-cell collision generation is disabled. Imported tiles use variable-size sprites, so cell occupancy does not match the visible floor. Keep the manually authored merged 3D collision objects instead.");
    }

    private static void BuildAllScenes()
    {
        int groundLayer = LayerMask.NameToLayer("Ground");
        int wallLayer = LayerMask.NameToLayer("Wall");
        if (groundLayer < 0 || wallLayer < 0)
        {
            Debug.LogError("[ImportedMap3DCollisionBuilder] Ground or Wall layer is missing. No scenes were changed.");
            return;
        }

        string previousScenePath = SceneManager.GetActiveScene().path;
        foreach (string sceneName in SceneNames)
        {
            BuildScene(sceneName, groundLayer, wallLayer);
        }

        if (!string.IsNullOrEmpty(previousScenePath))
        {
            EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
        }

        AssetDatabase.SaveAssets();
    }

    private static void BuildScene(string sceneName, int groundLayer, int wallLayer)
    {
        string scenePath = SceneFolder + sceneName + ".unity";
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
        {
            Debug.LogError($"[ImportedMap3DCollisionBuilder] Scene not found: {scenePath}");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        Tilemap tilemap = FindVisualTilemap();
        if (tilemap == null)
        {
            Debug.LogError($"[ImportedMap3DCollisionBuilder] Visual_Tilemap not found in {sceneName}.");
            return;
        }

        Collider2D[] invalid2DColliders = tilemap.GetComponents<Collider2D>();
        foreach (Collider2D invalidCollider in invalid2DColliders)
        {
            UnityEngine.Object.DestroyImmediate(invalidCollider);
        }

        GameObject oldRoot = GameObject.Find(CollisionRootName);
        if (oldRoot != null && oldRoot.scene == scene)
        {
            UnityEngine.Object.DestroyImmediate(oldRoot);
        }

        GameObject collisionRoot = new GameObject(CollisionRootName);
        GameObject groundRoot = new GameObject("Ground");
        groundRoot.layer = groundLayer;
        groundRoot.transform.SetParent(collisionRoot.transform, false);
        GameObject wallRoot = new GameObject("Walls");
        wallRoot.layer = wallLayer;
        wallRoot.transform.SetParent(collisionRoot.transform, false);

        List<CellRectangle> rectangles = BuildMergedRectangles(tilemap);
        int groundCount = 0;
        int wallCount = 0;
        foreach (CellRectangle rectangle in rectangles)
        {
            bool isGround = IsGroundSurface(tilemap, rectangle);
            Transform parent = isGround ? groundRoot.transform : wallRoot.transform;
            int layer = isGround ? groundLayer : wallLayer;
            int index = isGround ? ++groundCount : ++wallCount;
            CreateCollider(tilemap, rectangle, parent, layer, isGround ? "Ground" : "Wall", index);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[ImportedMap3DCollisionBuilder] {sceneName}: {groundCount} Ground + {wallCount} Wall merged 3D BoxColliders.");
    }

    private static Tilemap FindVisualTilemap()
    {
        Tilemap[] tilemaps = UnityEngine.Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
        foreach (Tilemap tilemap in tilemaps)
        {
            if (tilemap.name == "Visual_Tilemap")
            {
                return tilemap;
            }
        }

        return tilemaps.Length > 0 ? tilemaps[0] : null;
    }

    private static List<CellRectangle> BuildMergedRectangles(Tilemap tilemap)
    {
        BoundsInt bounds = tilemap.cellBounds;
        var completed = new List<CellRectangle>();
        var active = new Dictionary<RunKey, CellRectangle>();

        for (int y = bounds.yMin; y < bounds.yMax; y++)
        {
            List<RunKey> rowRuns = FindRuns(tilemap, bounds.xMin, bounds.xMax, y);
            var next = new Dictionary<RunKey, CellRectangle>();

            foreach (RunKey run in rowRuns)
            {
                if (active.TryGetValue(run, out CellRectangle rectangle))
                {
                    rectangle.MaxYExclusive = y + 1;
                    next.Add(run, rectangle);
                }
                else
                {
                    next.Add(run, new CellRectangle(run.MinX, run.MaxXExclusive, y, y + 1));
                }
            }

            foreach (KeyValuePair<RunKey, CellRectangle> pair in active)
            {
                if (!next.ContainsKey(pair.Key))
                {
                    completed.Add(pair.Value);
                }
            }

            active = next;
        }

        completed.AddRange(active.Values);
        return completed;
    }

    private static List<RunKey> FindRuns(Tilemap tilemap, int minX, int maxX, int y)
    {
        var runs = new List<RunKey>();
        int x = minX;
        while (x < maxX)
        {
            while (x < maxX && !tilemap.HasTile(new Vector3Int(x, y, 0)))
            {
                x++;
            }

            int runStart = x;
            while (x < maxX && tilemap.HasTile(new Vector3Int(x, y, 0)))
            {
                x++;
            }

            if (runStart < x)
            {
                runs.Add(new RunKey(runStart, x));
            }
        }

        return runs;
    }

    private static bool IsGroundSurface(Tilemap tilemap, CellRectangle rectangle)
    {
        if (rectangle.Width < rectangle.Height)
        {
            return false;
        }

        for (int x = rectangle.MinX; x < rectangle.MaxXExclusive; x++)
        {
            if (!tilemap.HasTile(new Vector3Int(x, rectangle.MaxYExclusive, 0)))
            {
                return true;
            }
        }

        return false;
    }

    private static void CreateCollider(Tilemap tilemap, CellRectangle rectangle, Transform parent, int layer, string typeName, int index)
    {
        Vector3 min = tilemap.CellToWorld(new Vector3Int(rectangle.MinX, rectangle.MinY, 0));
        Vector3 max = tilemap.CellToWorld(new Vector3Int(rectangle.MaxXExclusive, rectangle.MaxYExclusive, 0));
        Vector3 size = max - min;

        GameObject colliderObject = new GameObject($"{typeName}_{index:000}_{rectangle.Width}x{rectangle.Height}");
        colliderObject.layer = layer;
        colliderObject.transform.SetParent(parent, false);
        colliderObject.transform.position = new Vector3((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f, tilemap.transform.position.z);

        BoxCollider boxCollider = colliderObject.AddComponent<BoxCollider>();
        boxCollider.isTrigger = false;
        boxCollider.center = Vector3.zero;
        boxCollider.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), ColliderDepth);
    }

    private readonly struct RunKey : IEquatable<RunKey>
    {
        public RunKey(int minX, int maxXExclusive)
        {
            MinX = minX;
            MaxXExclusive = maxXExclusive;
        }

        public int MinX { get; }
        public int MaxXExclusive { get; }

        public bool Equals(RunKey other) => MinX == other.MinX && MaxXExclusive == other.MaxXExclusive;
        public override bool Equals(object obj) => obj is RunKey other && Equals(other);
        public override int GetHashCode() => (MinX * 397) ^ MaxXExclusive;
    }

    private struct CellRectangle
    {
        public CellRectangle(int minX, int maxXExclusive, int minY, int maxYExclusive)
        {
            MinX = minX;
            MaxXExclusive = maxXExclusive;
            MinY = minY;
            MaxYExclusive = maxYExclusive;
        }

        public int MinX;
        public int MaxXExclusive;
        public int MinY;
        public int MaxYExclusive;
        public int Width => MaxXExclusive - MinX;
        public int Height => MaxYExclusive - MinY;
    }
}
