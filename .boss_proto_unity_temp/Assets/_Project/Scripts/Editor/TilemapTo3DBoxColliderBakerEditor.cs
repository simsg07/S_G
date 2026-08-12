using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

[CustomEditor(typeof(TilemapTo3DBoxColliderBaker))]
public class TilemapTo3DBoxColliderBakerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("Compatibility component. Use the _Project > Map Tilemap 3D Collision commands.", MessageType.Info);
        if (GUILayout.Button("Preview")) Tilemap3DCollisionEditor.TogglePreview();
        if (GUILayout.Button("Bake / Update")) Tilemap3DCollisionEditor.Bake();
        if (GUILayout.Button("Validate")) Tilemap3DCollisionEditor.Validate();
    }
}

[InitializeOnLoad]
public static class Tilemap3DCollisionEditor
{
    private const string MenuRoot = "_Project/Map/";
    private const string GeneratedRootName = "Generated_Collision";
    private const string OneClickRootName = "Generated_3D_Collision";
    private static bool previewEnabled;
    private static readonly Color FloorColor = new Color(0.15f, 1f, 0.25f, 0.9f);
    private static readonly Color WallColor = new Color(0.15f, 0.45f, 1f, 0.9f);
    private static readonly Color CeilingColor = new Color(1f, 0.5f, 0.1f, 0.9f);
    private static readonly Color ErrorColor = new Color(1f, 0.1f, 0.1f, 0.95f);

    private sealed class TilemapData
    {
        public Tilemap Tilemap;
        public TilemapCollisionAuthoring Settings;
        public readonly HashSet<Vector3Int> Cells = new HashSet<Vector3Int>();
        public readonly Dictionary<Vector3Int, TilemapExposedFaces> Faces = new Dictionary<Vector3Int, TilemapExposedFaces>();
        public readonly List<CellRect> Rects = new List<CellRect>();
        public string WorldKey;
        public string Signature;
        public string HierarchyPath;
        public string SkipReason;
        public int ResolvedLayer = -1;
    }

    private readonly struct CellRect
    {
        public readonly Vector3Int Min;
        public readonly Vector3Int Size;
        public CellRect(Vector3Int min, Vector3Int size) { Min = min; Size = size; }
    }

    private sealed class BakeGroup
    {
        public TilemapData Representative;
        public readonly List<TilemapData> Items = new List<TilemapData>();
        public readonly HashSet<Vector3Int> Cells = new HashSet<Vector3Int>();
        public readonly List<CellRect> Rects = new List<CellRect>();
        public bool IsCrossTilemap => Items.Count > 1;
    }

    static Tilemap3DCollisionEditor()
    {
        SceneView.duringSceneGui += DrawPreview;
    }

    [MenuItem(MenuRoot + "Preview Tilemap 3D Collision")]
    public static void TogglePreview()
    {
        previewEnabled = !previewEnabled;
        Menu.SetChecked(MenuRoot + "Preview Tilemap 3D Collision", previewEnabled);
        SceneView.RepaintAll();
        Debug.Log($"[Tilemap3DCollision] Preview {(previewEnabled ? "enabled" : "disabled")}. Scene objects were not modified.");
    }

    [MenuItem(MenuRoot + "Diagnose Generated Tilemap Merge")]
    public static void DiagnoseGeneratedMerge()
    {
        Scene scene = SceneManager.GetActiveScene();
        List<TilemapData> data = Collect(scene, false);
        ReportMergeDiagnostics(scene, data, true);
        previewEnabled = true;
        Menu.SetChecked(MenuRoot + "Preview Tilemap 3D Collision", true);
        SceneView.RepaintAll();
    }

    [MenuItem(MenuRoot + "Advanced/Legacy Generate Solid Volume", priority = 140)]
    public static void GenerateOrUpdateOneClick()
    {
        if (Application.isPlaying) { Debug.LogError("[Tilemap 3D Bake] Edit Mode only. Exit Play Mode and run again."); return; }
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid()) { Debug.LogError("[Tilemap 3D Bake] No valid active scene."); return; }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Generate 3D Collision From Tilemaps");
        Tilemap[] tilemaps = FindSceneObjects<Tilemap>(scene);
        int autoAssigned = 0;
        foreach (Tilemap tilemap in tilemaps)
        {
            TilemapCollisionAuthoring existing = tilemap.GetComponent<TilemapCollisionAuthoring>();
            if (existing != null)
            {
                if (existing.AutomaticRoleVersion == 0 &&
                    (existing.Role == TilemapCollisionRole.None || existing.Role == TilemapCollisionRole.Unassigned))
                {
                    TilemapCollisionRole legacyInference = InferRole(tilemap.name);
                    if (legacyInference != TilemapCollisionRole.Unassigned)
                    {
                        Undo.RecordObject(existing, "Migrate legacy Tilemap collision role");
                        existing.SetInitialRole(legacyInference);
                        EditorUtility.SetDirty(existing);
                        autoAssigned++;
                        Debug.Log($"[Tilemap 3D Bake] Migrated legacy Role={TilemapCollisionRole.None} on '{GetPath(tilemap.transform)}' to Role={legacyInference} using the safe initial-name rule.", tilemap);
                    }
                }
                continue;
            }
            TilemapCollisionRole inferred = InferRole(tilemap.name);
            TilemapCollisionAuthoring settings = Undo.AddComponent<TilemapCollisionAuthoring>(tilemap.gameObject);
            settings.UseReachableDefaultsForNewTilemap();
            settings.SetInitialRole(inferred);
            int layer = ResolveFallbackLayer();
            if (layer >= 0) settings.SetInitialGeneratedLayer(layer);
            EditorUtility.SetDirty(settings);
            autoAssigned++;
            if (inferred == TilemapCollisionRole.Unassigned)
                Debug.LogWarning($"[Tilemap 3D Bake] '{GetPath(tilemap.transform)}' is ambiguous. Authoring was added with Role=Unassigned. Set Role in Inspector.", tilemap);
        }

        List<TilemapData> data = Collect(scene, false);
        foreach (TilemapData item in data) EvaluateBakeEligibility(item);
        LogDiscovery(scene, data, autoAssigned);

        List<TilemapData> solid = data.Where(d => string.IsNullOrEmpty(d.SkipReason)).ToList();
        int solidTiles = solid.Sum(d => d.Cells.Count);
        if (solid.Count == 0 || solidTiles == 0)
        {
            Undo.CollapseUndoOperations(group);
            Debug.LogError(BuildNoTargetsError(scene, data));
            return;
        }

        RemoveAllOwnedRoots(scene);
        Dictionary<string, GameObject> roots = new Dictionary<string, GameObject>();
        int colliderCount = 0;
        List<BakeGroup> bakeGroups = BuildBakeGroups(solid);
        foreach (BakeGroup bakeGroup in bakeGroups)
        {
            TilemapData item = bakeGroup.Representative;
            Transform parent = ResolveGeneratedParent(item);
            string key = parent.GetInstanceID() + "|" + item.WorldKey;
            if (!roots.TryGetValue(key, out GameObject root))
            {
                string suffix = item.WorldKey == "Shared" ? string.Empty : "_" + item.WorldKey;
                root = new GameObject(OneClickRootName + suffix);
                Undo.RegisterCreatedObjectUndo(root, "Create generated 3D collision root");
                root.transform.SetParent(parent, false);
                root.isStatic = true;
                root.SetActive(true);
                TilemapGeneratedColliderMarker rm = Undo.AddComponent<TilemapGeneratedColliderMarker>(root);
                rm.Configure(TilemapGeneratedColliderMarker.MarkerKind.Root, null, "Tilemap3D.OneClick.v2", Vector3Int.zero, Vector3Int.zero);
                roots.Add(key, root);
            }

            for (int i = 0; i < bakeGroup.Rects.Count; i++)
            {
                CellRect rect = bakeGroup.Rects[i];
                string cleanName = bakeGroup.IsCrossTilemap
                    ? item.Settings.CollisionGroupId
                    : item.Tilemap.name.Replace("Tilemap_", string.Empty);
                GameObject child = new GameObject($"{cleanName}_{i:000}");
                Undo.RegisterCreatedObjectUndo(child, "Create generated tilemap BoxCollider");
                child.transform.SetParent(item.Tilemap.transform, false);
                child.transform.SetParent(root.transform, true);
                child.layer = item.ResolvedLayer;
                child.isStatic = true;
                child.SetActive(true);
                BoxCollider box = Undo.AddComponent<BoxCollider>(child);
                ConfigureCollider(box, item, rect);
                box.enabled = true;
                TilemapGeneratedColliderMarker marker = Undo.AddComponent<TilemapGeneratedColliderMarker>(child);
                marker.Configure(TilemapGeneratedColliderMarker.MarkerKind.Collider, item.Settings, item.Signature, rect.Min, rect.Size);
                marker.ConfigureSources(bakeGroup.Items.Select(x => x.Settings).ToArray());
                colliderCount++;
                Debug.Log($"[Tilemap 3D Bake] Collider '{GetPath(child.transform)}': Center={box.center}, Size={box.size}, WorldBounds={box.bounds}, Layer={LayerMask.LayerToName(child.layer)}({child.layer}), Enabled={box.enabled}", child);
            }
        }

        List<string> issues = new List<string>();
        foreach (TilemapData item in data) ValidateItem(scene, item, issues);
        if (colliderCount == 0)
            Debug.LogError($"[Tilemap 3D Bake] ERROR: {solidTiles} Solid tiles were found but 0 BoxColliders were generated. Undo this operation and inspect the preceding diagnostics.");
        else if (issues.Count > 0)
            Debug.LogError($"[Tilemap 3D Bake] Generated {colliderCount} BoxColliders, but validation found {issues.Count} issue(s):\n" + string.Join("\n", issues));
        else
            Debug.Log($"[Tilemap 3D Bake]\nScene: {scene.name}\nTilemaps found: {data.Count}\nTilemaps with tiles: {data.Count(d => d.Cells.Count > 0)}\nSolid tilemaps: {solid.Count}\nSolid tiles: {solidTiles}\nGenerated BoxColliders: {colliderCount}\nRoots: {string.Join(", ", roots.Values.Select(r => GetPath(r.transform)))}\nScene was marked dirty but not saved.");

        if (roots.Count > 0)
        {
            Selection.activeGameObject = roots.Values.First();
            EditorGUIUtility.PingObject(Selection.activeGameObject);
            SceneView.lastActiveSceneView?.FrameSelected();
        }
        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(group);
        ReportMergeDiagnostics(scene, data, false);
    }

    [MenuItem("Project/Map/Generate / Update 3D Collision From Tilemaps", priority = 40)]
    private static void GenerateOrUpdateOneClickProjectAlias()
    {
        TilemapReachableSurfaceBakerEditor.BakeUpdateTile3DCollider();
    }

    [MenuItem(MenuRoot + "Advanced/Legacy Bake Tilemap Solid Volume")]
    public static void Bake()
    {
        if (Application.isPlaying) { Debug.LogError("[Tilemap3DCollision] Bake is Edit Mode only."); return; }
        Scene scene = SceneManager.GetActiveScene();
        List<TilemapData> data = Collect(scene, true);
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Bake Tilemap 3D Collision");
        int tileCount = 0, colliderCount = 0;

        foreach (TilemapData item in data)
        {
            if (item.Settings != null) RemoveOwnedRoots(item.Settings);
            if (item.Settings == null || item.Settings.Role != TilemapCollisionRole.Solid || item.Settings.BakeMode != TilemapCollisionBakeMode.LegacySolidVolume) continue;
            tileCount += item.Cells.Count;
            GameObject root = new GameObject(GeneratedRootName);
            Undo.RegisterCreatedObjectUndo(root, "Create generated collision root");
            root.transform.SetParent(item.Tilemap.transform, false);
            root.isStatic = true;
            TilemapGeneratedColliderMarker rootMarker = Undo.AddComponent<TilemapGeneratedColliderMarker>(root);
            rootMarker.Configure(TilemapGeneratedColliderMarker.MarkerKind.Root, item.Settings, item.Signature, Vector3Int.zero, Vector3Int.zero);

            for (int i = 0; i < item.Rects.Count; i++)
            {
                CellRect rect = item.Rects[i];
                GameObject child = new GameObject($"[Generated] {item.Tilemap.name}_{i:000}");
                Undo.RegisterCreatedObjectUndo(child, "Create generated tilemap collider");
                child.transform.SetParent(root.transform, false);
                child.layer = item.Settings.GeneratedLayer;
                child.isStatic = true;
                BoxCollider collider = Undo.AddComponent<BoxCollider>(child);
                ConfigureCollider(collider, item, rect);
                TilemapGeneratedColliderMarker marker = Undo.AddComponent<TilemapGeneratedColliderMarker>(child);
                marker.Configure(TilemapGeneratedColliderMarker.MarkerKind.Collider, item.Settings, item.Signature, rect.Min, rect.Size);
                colliderCount++;
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(group);
        if (tileCount == 0 || colliderCount == 0) Debug.LogError($"[Tilemap3DCollision Legacy] No LegacySolidVolume tiles were baked. SolidTiles={tileCount}, BoxColliders={colliderCount}. Use _Project > Map > Bake/Update Tile 3D Collider for the normal workflow.");
        else Debug.Log($"[Tilemap3DCollision Legacy] Bake complete: {tileCount} Solid tiles -> {colliderCount} BoxColliders. Save the scene manually when ready.");
    }

    [MenuItem(MenuRoot + "Validate Tilemap Collision")]
    public static void Validate()
    {
        Scene scene = SceneManager.GetActiveScene();
        List<TilemapData> all = Collect(scene, false);
        List<string> issues = new List<string>();
        foreach (TilemapData item in all) ValidateItem(scene, item, issues);
        if (all.Count == 0) Debug.LogError($"[Tilemap3DCollision] INVALID: scene '{scene.name}' contains 0 Tilemaps. Nothing can be baked.");
        else if (issues.Count == 0) Debug.Log($"[Tilemap3DCollision] VALID: scene '{scene.name}' has no statically detectable collision issues.");
        else Debug.LogError($"[Tilemap3DCollision] INVALID: {issues.Count} issue(s) in scene '{scene.name}'.\n" + string.Join("\n", issues));
    }

    [MenuItem(MenuRoot + "Clear Generated Tilemap Collision")]
    public static void Clear()
    {
        if (Application.isPlaying) { Debug.LogError("[Tilemap3DCollision] Clear is Edit Mode only."); return; }
        Scene scene = SceneManager.GetActiveScene();
        TilemapGeneratedColliderMarker[] markers = FindSceneObjects<TilemapGeneratedColliderMarker>(scene);
        int count = 0;
        foreach (TilemapGeneratedColliderMarker marker in markers)
        {
            if (marker != null && marker.Kind == TilemapGeneratedColliderMarker.MarkerKind.Root)
            { Undo.DestroyObjectImmediate(marker.gameObject); count++; }
        }
        if (count > 0) EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[Tilemap3DCollision] Cleared {count} generator-owned root(s). Manual and other-tool colliders were preserved.");
    }

    [MenuItem(MenuRoot + "Assign Missing Tilemap Collision Roles")]
    public static void AssignMissingRoles()
    {
        Scene scene = SceneManager.GetActiveScene();
        int count = 0;
        foreach (Tilemap tilemap in FindSceneObjects<Tilemap>(scene))
        {
            if (tilemap.GetComponent<TilemapCollisionAuthoring>() != null) continue;
            TilemapCollisionAuthoring settings = Undo.AddComponent<TilemapCollisionAuthoring>(tilemap.gameObject);
            settings.SetInitialRole(InferRole(tilemap.name));
            EditorUtility.SetDirty(settings); count++;
        }
        if (count > 0) EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[Tilemap3DCollision] Added editable roles to {count} Tilemap(s). Name inference was used only as the initial value.");
    }

    private static List<TilemapData> Collect(Scene scene, bool logMissing)
    {
        List<TilemapData> result = new List<TilemapData>();
        foreach (Tilemap tilemap in FindSceneObjects<Tilemap>(scene))
        {
            TilemapData data = new TilemapData { Tilemap = tilemap, Settings = tilemap.GetComponent<TilemapCollisionAuthoring>(), WorldKey = ResolveWorldKey(tilemap.transform) };
            data.HierarchyPath = GetPath(tilemap.transform);
            BoundsInt bounds = tilemap.cellBounds;
            foreach (Vector3Int cell in bounds.allPositionsWithin) if (tilemap.HasTile(cell)) data.Cells.Add(cell);
            AnalyzeFaces(data);
            if (data.Settings != null && data.Settings.Role == TilemapCollisionRole.Solid)
                BuildRects(data.Cells, data.Settings.MergeEnabled, data.Rects);
            data.Signature = ComputeSignature(data);
            result.Add(data);
            if (logMissing && data.Settings == null) Debug.LogWarning(FormatIssue(scene, data, null, "role component is missing", "Run Assign Missing Tilemap Collision Roles, then review Role in Inspector."), tilemap);
        }
        return result;
    }

    private static void AnalyzeFaces(TilemapData data)
    {
        foreach (Vector3Int cell in data.Cells)
        {
            TilemapExposedFaces faces = TilemapExposedFaces.None;
            if (!data.Cells.Contains(cell + Vector3Int.up)) faces |= TilemapExposedFaces.Top;
            if (!data.Cells.Contains(cell + Vector3Int.down)) faces |= TilemapExposedFaces.Bottom;
            if (!data.Cells.Contains(cell + Vector3Int.left)) faces |= TilemapExposedFaces.Left;
            if (!data.Cells.Contains(cell + Vector3Int.right)) faces |= TilemapExposedFaces.Right;
            data.Faces[cell] = faces;
        }
    }

    private static void BuildRects(HashSet<Vector3Int> cells, bool merge, List<CellRect> output)
    {
        HashSet<Vector3Int> remaining = new HashSet<Vector3Int>(cells);
        foreach (Vector3Int start in cells.OrderBy(c => c.z).ThenBy(c => c.y).ThenBy(c => c.x))
        {
            if (!remaining.Contains(start)) continue;
            int width = 1, height = 1;
            if (merge)
            {
                while (remaining.Contains(start + new Vector3Int(width, 0, 0))) width++;
                bool canGrow = true;
                while (canGrow)
                {
                    for (int x = 0; x < width; x++) if (!remaining.Contains(start + new Vector3Int(x, height, 0))) { canGrow = false; break; }
                    if (canGrow) height++;
                }
            }
            CellRect rect = new CellRect(start, new Vector3Int(width, height, 1)); output.Add(rect);
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) remaining.Remove(start + new Vector3Int(x, y, 0));
        }
        if (merge) CoalesceRects(output);
    }

    private static void CoalesceRects(List<CellRect> rects)
    {
        bool changed;
        do
        {
            changed = false;
            // Horizontal first: preserve long platform runs for side-scrolling gameplay.
            for (int i = 0; i < rects.Count && !changed; i++)
            for (int j = i + 1; j < rects.Count; j++)
            {
                CellRect a = rects[i], b = rects[j];
                if (a.Min.z != b.Min.z || a.Min.y != b.Min.y || a.Size.y != b.Size.y) continue;
                if (a.Min.x + a.Size.x != b.Min.x && b.Min.x + b.Size.x != a.Min.x) continue;
                int minX = Mathf.Min(a.Min.x, b.Min.x);
                rects[i] = new CellRect(new Vector3Int(minX, a.Min.y, a.Min.z), new Vector3Int(a.Size.x + b.Size.x, a.Size.y, 1));
                rects.RemoveAt(j); changed = true; break;
            }
            if (changed) continue;
            for (int i = 0; i < rects.Count && !changed; i++)
            for (int j = i + 1; j < rects.Count; j++)
            {
                CellRect a = rects[i], b = rects[j];
                if (a.Min.z != b.Min.z || a.Min.x != b.Min.x || a.Size.x != b.Size.x) continue;
                if (a.Min.y + a.Size.y != b.Min.y && b.Min.y + b.Size.y != a.Min.y) continue;
                int minY = Mathf.Min(a.Min.y, b.Min.y);
                rects[i] = new CellRect(new Vector3Int(a.Min.x, minY, a.Min.z), new Vector3Int(a.Size.x, a.Size.y + b.Size.y, 1));
                rects.RemoveAt(j); changed = true; break;
            }
        } while (changed);
        rects.Sort((a, b) => a.Min.z != b.Min.z ? a.Min.z.CompareTo(b.Min.z) : a.Min.y != b.Min.y ? a.Min.y.CompareTo(b.Min.y) : a.Min.x.CompareTo(b.Min.x));
    }

    private static void ConfigureCollider(BoxCollider collider, TilemapData data, CellRect rect)
    {
        Vector3 a = data.Tilemap.CellToLocalInterpolated(rect.Min);
        Vector3 b = data.Tilemap.CellToLocalInterpolated(rect.Min + rect.Size);
        collider.center = new Vector3((a.x + b.x) * 0.5f, (a.y + b.y) * 0.5f, data.Settings.CollisionCenterZ);
        collider.size = new Vector3(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y), data.Settings.CollisionDepth);
        collider.isTrigger = data.Settings.IsTrigger;
    }

    private static void ValidateItem(Scene scene, TilemapData item, List<string> issues)
    {
        if (item.Settings == null)
        {
            issues.Add(FormatIssue(scene, item, item.Cells.FirstOrDefault(), "collision role is unassigned", "Add TilemapCollisionAuthoring or run Assign Missing Tilemap Collision Roles.")); return;
        }
        TilemapGeneratedColliderMarker[] owned = FindSceneObjects<TilemapGeneratedColliderMarker>(scene).Where(m => m.ContainsSource(item.Settings)).ToArray();
        TilemapGeneratedColliderMarker[] roots = owned.Where(m => m.Kind == TilemapGeneratedColliderMarker.MarkerKind.Root).ToArray();
        TilemapGeneratedColliderMarker[] children = owned.Where(m => m.Kind == TilemapGeneratedColliderMarker.MarkerKind.Collider).ToArray();
        int namedRoots = Enumerable.Range(0, item.Tilemap.transform.childCount)
            .Select(i => item.Tilemap.transform.GetChild(i))
            .Count(t => t.name == GeneratedRootName);
        if (roots.Length > 1 || namedRoots > 1) issues.Add(FormatIssue(scene, item, null, "duplicate Generated_Collision roots", "Run Bake / Update or Clear, then Bake."));
        if (item.Settings.Role == TilemapCollisionRole.Solid && item.Cells.Count > 0 && children.Length == 0)
            issues.Add(FormatIssue(scene, item, item.Cells.First(), "Solid tiles have no generated collider", "Run Bake / Update Tilemap 3D Collision."));
        if (item.Settings.Role != TilemapCollisionRole.Solid && children.Length > 0)
            issues.Add(FormatIssue(scene, item, item.Cells.FirstOrDefault(), $"{item.Settings.Role} contains generated colliders", "Run Bake / Update; only Solid generates colliders."));
        foreach (TilemapGeneratedColliderMarker marker in children)
        {
            BoxCollider box = marker.GetComponent<BoxCollider>();
            if (box == null) { issues.Add(FormatIssue(scene, item, marker.MinimumCell, "generated marker has no BoxCollider", "Run Bake / Update.")); continue; }
            if ((marker.Sources == null || marker.Sources.Length <= 1) && marker.BakeSignature != item.Signature) issues.Add(FormatIssue(scene, item, marker.MinimumCell, "Tilemap/settings changed after bake", "Run Bake / Update."));
            if (marker.gameObject.layer != item.Settings.GeneratedLayer) issues.Add(FormatIssue(scene, item, marker.MinimumCell, "generated Layer is wrong", "Run Bake / Update or correct Generated Layer."));
            if (!Mathf.Approximately(box.center.z, item.Settings.CollisionCenterZ) || !Mathf.Approximately(box.size.z, item.Settings.CollisionDepth))
                issues.Add(FormatIssue(scene, item, marker.MinimumCell, "Z center/depth does not match settings", "Run Bake / Update."));
            Vector3 expectedA = item.Tilemap.CellToLocalInterpolated(marker.MinimumCell);
            Vector3 expectedB = item.Tilemap.CellToLocalInterpolated(marker.MinimumCell + marker.SizeInCells);
            Vector2 expectedCenter = new Vector2((expectedA.x + expectedB.x) * .5f, (expectedA.y + expectedB.y) * .5f);
            Vector2 expectedSize = new Vector2(Mathf.Abs(expectedB.x - expectedA.x), Mathf.Abs(expectedB.y - expectedA.y));
            if (!Approximately(new Vector2(box.center.x, box.center.y), expectedCenter) || !Approximately(new Vector2(box.size.x, box.size.y), expectedSize))
                issues.Add(FormatIssue(scene, item, marker.MinimumCell, "collider geometry does not match Grid/Tilemap bounds", "Run Bake / Update; do not edit generated BoxColliders."));
            if (ResolveWorldKey(marker.transform) != item.WorldKey) issues.Add(FormatIssue(scene, item, marker.MinimumCell, "World A/B membership is mixed", "Keep generated root under its source Tilemap and rebake."));
            if ((marker.Sources == null || marker.Sources.Length <= 1) && !RectMatchesCells(marker, item.Cells)) issues.Add(FormatIssue(scene, item, marker.MinimumCell, "collider bounds include missing/out-of-range cells", "Run Bake / Update; do not edit generated children manually."));
        }
        if (children.GroupBy(m => (m.MinimumCell, m.SizeInCells)).Any(g => g.Count() > 1))
            issues.Add(FormatIssue(scene, item, null, "duplicate generated collider ranges", "Run Clear, then Bake / Update."));
        if (item.Settings.Role == TilemapCollisionRole.Solid && children.Length > 0 && children.All(m => m.Sources == null || m.Sources.Length <= 1))
        {
            HashSet<Vector3Int> covered = new HashSet<Vector3Int>();
            foreach (TilemapGeneratedColliderMarker marker in children)
                for (int y = 0; y < marker.SizeInCells.y; y++) for (int x = 0; x < marker.SizeInCells.x; x++) covered.Add(marker.MinimumCell + new Vector3Int(x, y, 0));
            Vector3Int missing = item.Cells.FirstOrDefault(c => !covered.Contains(c));
            if (covered.Count != item.Cells.Count || item.Cells.Any(c => !covered.Contains(c)))
                issues.Add(FormatIssue(scene, item, missing, "tile area and generated collider coverage differ", "Run Bake / Update."));
        }
    }

    private static bool RectMatchesCells(TilemapGeneratedColliderMarker marker, HashSet<Vector3Int> cells)
    {
        Vector3Int min = marker.MinimumCell, size = marker.SizeInCells;
        if (size.x <= 0 || size.y <= 0) return false;
        for (int y = 0; y < size.y; y++) for (int x = 0; x < size.x; x++) if (!cells.Contains(min + new Vector3Int(x, y, 0))) return false;
        return true;
    }

    private static bool Approximately(Vector2 a, Vector2 b)
    {
        return Mathf.Abs(a.x - b.x) < 0.0001f && Mathf.Abs(a.y - b.y) < 0.0001f;
    }

    private static string ComputeSignature(TilemapData data)
    {
        StringBuilder text = new StringBuilder();
        text.Append(data.WorldKey).Append('|').Append(data.Settings != null ? data.Settings.Role.ToString() : "Missing");
        if (data.Settings != null) text.Append('|').Append(data.Settings.CollisionCenterZ).Append('|').Append(data.Settings.CollisionDepth).Append('|').Append(data.Settings.GeneratedLayer).Append('|').Append(data.Settings.IsTrigger).Append('|').Append(data.Settings.MergeEnabled).Append('|').Append(data.Settings.AllowCrossTilemapMerge).Append('|').Append(data.Settings.CollisionGroupId);
        text.Append('|').Append(data.Tilemap.transform.localToWorldMatrix);
        foreach (Vector3Int c in data.Cells.OrderBy(c => c.z).ThenBy(c => c.y).ThenBy(c => c.x)) text.Append('|').Append(c.x).Append(',').Append(c.y).Append(',').Append(c.z);
        using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "");
    }

    private static void RemoveOwnedRoots(TilemapCollisionAuthoring source)
    {
        TilemapGeneratedColliderMarker[] markers = source.GetComponentsInChildren<TilemapGeneratedColliderMarker>(true);
        foreach (TilemapGeneratedColliderMarker marker in markers)
            if (marker != null && marker.Source == source && marker.Kind == TilemapGeneratedColliderMarker.MarkerKind.Root) Undo.DestroyObjectImmediate(marker.gameObject);
    }

    private static void DrawPreview(SceneView view)
    {
        if (!previewEnabled || Application.isPlaying) return;
        foreach (TilemapData item in Collect(SceneManager.GetActiveScene(), false))
        {
            if (item.Settings == null)
            {
                Handles.color = ErrorColor;
                foreach (Vector3Int c in item.Cells) Handles.DrawWireCube(item.Tilemap.GetCellCenterWorld(c), Vector3.one * 0.85f);
                continue;
            }
            if (item.Settings.Role != TilemapCollisionRole.Solid)
            {
                Matrix4x4 excludedMatrix = Handles.matrix; Handles.matrix = item.Tilemap.transform.localToWorldMatrix;
                Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.35f);
                foreach (Vector3Int c in item.Cells) Handles.DrawWireCube(item.Tilemap.GetCellCenterLocal(c), item.Tilemap.layoutGrid != null ? item.Tilemap.layoutGrid.cellSize : Vector3.one);
                Handles.matrix = excludedMatrix;
                continue;
            }
            Matrix4x4 old = Handles.matrix; Handles.matrix = item.Tilemap.transform.localToWorldMatrix;
            Color groupColor = Color.HSVToRGB((Mathf.Abs((item.Settings.CollisionGroupId ?? item.Tilemap.name).GetHashCode()) % 997) / 997f, 0.65f, 1f);
            groupColor.a = 0.8f;
            foreach (CellRect rect in item.Rects)
            {
                Vector3 a = item.Tilemap.CellToLocalInterpolated(rect.Min), b = item.Tilemap.CellToLocalInterpolated(rect.Min + rect.Size);
                Handles.color = groupColor;
                Handles.DrawWireCube(new Vector3((a.x + b.x) * .5f, (a.y + b.y) * .5f, item.Settings.CollisionCenterZ), new Vector3(Mathf.Abs(b.x-a.x), Mathf.Abs(b.y-a.y), item.Settings.CollisionDepth));
            }
            foreach (Vector3Int c in item.Cells)
            {
                Vector3Int gap = c + Vector3Int.right;
                if (item.Cells.Contains(gap) || !item.Cells.Contains(c + Vector3Int.right * 2)) continue;
                Handles.color = ErrorColor;
                Vector3 center = item.Tilemap.GetCellCenterLocal(gap);
                Handles.DrawWireCube(center, item.Tilemap.layoutGrid != null ? item.Tilemap.layoutGrid.cellSize : Vector3.one);
                Handles.Label(center, $"Visuals appear connected, but no collision tile exists at cell {gap}.");
            }
            foreach (KeyValuePair<Vector3Int, TilemapExposedFaces> pair in item.Faces) DrawFaces(item, pair.Key, pair.Value);
            Handles.matrix = old;
        }
    }

    private static void DrawFaces(TilemapData item, Vector3Int cell, TilemapExposedFaces faces)
    {
        Vector3 a = item.Tilemap.CellToLocalInterpolated(cell), b = item.Tilemap.CellToLocalInterpolated(cell + Vector3Int.one);
        float z = item.Settings.CollisionCenterZ;
        if ((faces & TilemapExposedFaces.Top) != 0) DrawLine(new Vector3(a.x,b.y,z), new Vector3(b.x,b.y,z), FloorColor);
        if ((faces & TilemapExposedFaces.Bottom) != 0) DrawLine(new Vector3(a.x,a.y,z), new Vector3(b.x,a.y,z), CeilingColor);
        if ((faces & TilemapExposedFaces.Left) != 0) DrawLine(new Vector3(a.x,a.y,z), new Vector3(a.x,b.y,z), WallColor);
        if ((faces & TilemapExposedFaces.Right) != 0) DrawLine(new Vector3(b.x,a.y,z), new Vector3(b.x,b.y,z), WallColor);
    }

    private static void DrawLine(Vector3 a, Vector3 b, Color color) { Handles.color = color; Handles.DrawAAPolyLine(4f, a, b); }

    private static T[] FindSceneObjects<T>(Scene scene) where T : Component
    {
        return Resources.FindObjectsOfTypeAll<T>().Where(x => x != null && x.gameObject.scene == scene && !EditorUtility.IsPersistent(x)).ToArray();
    }

    private static string ResolveWorldKey(Transform transform)
    {
        for (Transform t = transform; t != null; t = t.parent)
        {
            WorldPresence presence = t.GetComponent<WorldPresence>();
            if (presence != null) return presence.PresenceMode.ToString();
            string n = t.name.ToLowerInvariant();
            if (n.Contains("world_a") || n.Contains("world a")) return "WorldA";
            if (n.Contains("world_b") || n.Contains("world b")) return "WorldB";
        }
        return "Shared";
    }

    private static TilemapCollisionRole InferRole(string name)
    {
        string n = name.ToLowerInvariant();
        if (ContainsAny(n, "background", "decoration", "deco", "back", "effect", "vfx")) return TilemapCollisionRole.Decoration;
        if (n.Contains("oneway") || n.Contains("platform")) return TilemapCollisionRole.OneWayPlatform;
        if (n.Contains("hazard") || n.Contains("spike")) return TilemapCollisionRole.Hazard;
        if (ContainsAny(n, "ground", "floor", "wall", "ceiling", "tile", "obstacle", "collision", "solid")) return TilemapCollisionRole.Solid;
        return TilemapCollisionRole.Unassigned;
    }

    private static bool ContainsAny(string value, params string[] tokens) { return tokens.Any(value.Contains); }

    private static void EvaluateBakeEligibility(TilemapData item)
    {
        if (item.Settings == null) { item.SkipReason = "TilemapCollisionAuthoring missing"; return; }
        if (item.Settings.Role == TilemapCollisionRole.Unassigned || item.Settings.Role == TilemapCollisionRole.None) { item.SkipReason = $"Role={item.Settings.Role}; choose Solid or another explicit role"; return; }
        if (item.Settings.Role != TilemapCollisionRole.Solid) { item.SkipReason = $"Role={item.Settings.Role}"; return; }
        if (item.Settings.BakeMode != TilemapCollisionBakeMode.LegacySolidVolume) { item.SkipReason = "BakeMode=ReachableSurfaceBoundary; use Bake / Update Reachable 3D Collision"; return; }
        if (item.Tilemap.layoutGrid == null) { item.SkipReason = "Grid missing"; return; }
        if (item.Cells.Count == 0) { item.SkipReason = "HasTile count=0"; return; }
        item.ResolvedLayer = ResolveLayer(item.Settings.GeneratedLayer);
        if (item.ResolvedLayer < 0) { item.SkipReason = $"Generated Layer {item.Settings.GeneratedLayer} is undefined and Ground/TileObstacle/Wall do not exist"; return; }
        if (item.ResolvedLayer != item.Settings.GeneratedLayer)
        {
            Undo.RecordObject(item.Settings, "Repair invalid generated collision layer");
            item.Settings.SetInitialGeneratedLayer(item.ResolvedLayer);
            EditorUtility.SetDirty(item.Settings);
        }
        item.SkipReason = null;
    }

    private static int ResolveLayer(int stored)
    {
        if (stored >= 0 && stored <= 31 && !string.IsNullOrEmpty(LayerMask.LayerToName(stored))) return stored;
        return ResolveFallbackLayer();
    }

    private static int ResolveFallbackLayer()
    {
        foreach (string name in new[] { "Ground", "TileObstacle", "Wall" })
        {
            int layer = LayerMask.NameToLayer(name);
            if (layer >= 0) return layer;
        }
        return -1;
    }

    private static void LogDiscovery(Scene scene, List<TilemapData> data, int autoAssigned)
    {
        foreach (TilemapData item in data)
        {
            GridLayout grid = item.Tilemap.layoutGrid;
            int storedLayer = item.Settings != null ? item.Settings.GeneratedLayer : -1;
            string storedLayerName = storedLayer >= 0 && storedLayer <= 31 ? LayerMask.LayerToName(storedLayer) : string.Empty;
            string layer = item.ResolvedLayer >= 0
                ? $"{LayerMask.LayerToName(item.ResolvedLayer)}({item.ResolvedLayer})"
                : (!string.IsNullOrEmpty(storedLayerName) ? $"{storedLayerName}({storedLayer}) [not evaluated]" : $"undefined({storedLayer})");
            string status = string.IsNullOrEmpty(item.SkipReason) ? "INCLUDED" : "SKIPPED: " + item.SkipReason;
            Transform parent = item.Tilemap.transform.parent;
            Debug.Log($"[Tilemap 3D Scan] Scene='{scene.name}' Path='{item.HierarchyPath}' ActiveSelf={item.Tilemap.gameObject.activeSelf} ActiveInHierarchy={item.Tilemap.gameObject.activeInHierarchy} Authoring={(item.Settings != null)} Role={(item.Settings != null ? item.Settings.Role.ToString() : "Missing")} CellBounds={item.Tilemap.cellBounds} HasTile={item.Cells.Count} Grid={(grid != null ? GetPath(grid.transform) : "Missing")} GridCellSize={(grid != null ? grid.cellSize.ToString() : "n/a")} TileAnchor={item.Tilemap.tileAnchor} Orientation={item.Tilemap.orientation} LocalPosition={item.Tilemap.transform.localPosition} ParentRotation={(parent != null ? parent.rotation.ToString() : "n/a")} ParentScale={(parent != null ? parent.lossyScale.ToString() : "n/a")} World={item.WorldKey} GeneratedLayer={layer} Result={status}", item.Tilemap);
        }
        Debug.Log($"[Tilemap 3D Scan] Scene='{scene.name}', Tilemaps={data.Count}, WithTiles={data.Count(d => d.Cells.Count > 0)}, AutoAuthoringAdded={autoAssigned}.");
    }

    private static string BuildNoTargetsError(Scene scene, List<TilemapData> data)
    {
        string skipped = string.Join("\n", data.Select(d => $"- {d.HierarchyPath}: {d.SkipReason ?? "no Solid tiles"}"));
        return $"[Tilemap 3D Bake] ERROR: no bakeable Tilemap.\nScene: {scene.name}\nTilemaps found: {data.Count}\nTilemaps with tiles: {data.Count(d => d.Cells.Count > 0)}\nSolid tilemaps: {data.Count(d => d.Settings != null && d.Settings.Role == TilemapCollisionRole.Solid)}\nSolid tiles: {data.Where(d => d.Settings != null && d.Settings.Role == TilemapCollisionRole.Solid).Sum(d => d.Cells.Count)}\nSkipped:\n{skipped}\nAction: set ambiguous Tilemaps to Role=Solid, ensure they are under a Grid, and configure a valid Ground/TileObstacle/Wall layer.";
    }

    private static Transform ResolveGeneratedParent(TilemapData item)
    {
        for (Transform t = item.Tilemap.transform; t != null; t = t.parent)
            if (t.GetComponent<WorldPresence>() != null || t.name.ToLowerInvariant().Contains("world_a") || t.name.ToLowerInvariant().Contains("world_b")) return t;
        return item.Tilemap.layoutGrid != null ? item.Tilemap.layoutGrid.transform : item.Tilemap.transform.parent;
    }

    private static List<BakeGroup> BuildBakeGroups(List<TilemapData> solid)
    {
        Dictionary<string, BakeGroup> groups = new Dictionary<string, BakeGroup>();
        foreach (TilemapData item in solid)
        {
            bool cross = item.Settings.AllowCrossTilemapMerge &&
                !string.IsNullOrWhiteSpace(item.Settings.CollisionGroupId) &&
                IsGridAligned(item);
            string key = cross
                ? $"cross|{item.Tilemap.layoutGrid.GetInstanceID()}|{item.WorldKey}|{item.Settings.Role}|{item.ResolvedLayer}|{item.Settings.IsTrigger}|{item.Settings.CollisionCenterZ:R}|{item.Settings.CollisionDepth:R}|{item.Settings.CollisionGroupId}"
                : $"single|{item.Tilemap.GetInstanceID()}";
            if (!groups.TryGetValue(key, out BakeGroup group))
            {
                group = new BakeGroup { Representative = item };
                groups.Add(key, group);
            }
            group.Items.Add(item);
            group.Cells.UnionWith(item.Cells);
        }
        foreach (BakeGroup group in groups.Values)
            BuildRects(group.Cells, group.Items.All(x => x.Settings.MergeEnabled), group.Rects);
        return groups.Values.OrderBy(g => g.Representative.HierarchyPath).ToList();
    }

    private static bool IsGridAligned(TilemapData item)
    {
        GridLayout grid = item.Tilemap.layoutGrid;
        if (grid == null || item.Tilemap.orientation != Tilemap.Orientation.XY) return false;
        Matrix4x4 a = item.Tilemap.transform.localToWorldMatrix;
        Matrix4x4 b = grid.transform.localToWorldMatrix;
        for (int row = 0; row < 4; row++) for (int col = 0; col < 4; col++)
            if (Mathf.Abs(a[row, col] - b[row, col]) > 0.0001f) return false;
        return true;
    }

    private static void ReportMergeDiagnostics(Scene scene, List<TilemapData> data, bool detailed)
    {
        TilemapGeneratedColliderMarker[] markers = FindSceneObjects<TilemapGeneratedColliderMarker>(scene)
            .Where(m => m.Kind == TilemapGeneratedColliderMarker.MarkerKind.Collider && m.GetComponent<BoxCollider>() != null).ToArray();
        int single = markers.Count(m => m.SizeInCells.x == 1 && m.SizeInCells.y == 1);
        int horizontal = markers.Count(m => m.SizeInCells.x > 1 && m.SizeInCells.y == 1);
        int vertical = markers.Count(m => m.SizeInCells.x == 1 && m.SizeInCells.y > 1);
        int both = markers.Count(m => m.SizeInCells.x > 1 && m.SizeInCells.y > 1);
        int gapCount = CountVisualGapCandidates(data);
        Debug.Log($"[Tilemap 3D Merge Diagnostic]\nScene={scene.name}\nSolidTiles={data.Where(d => d.Settings != null && d.Settings.Role == TilemapCollisionRole.Solid).Sum(d => d.Cells.Count)}\nBoxColliders={markers.Length}\nSingleCell={single}\nHorizontal={horizontal}\nVertical={vertical}\nBothAxes={both}\nVisualGapCandidates={gapCount}\nPerTilemap: {string.Join(", ", data.Where(d => d.Settings != null).Select(d => d.Tilemap.name + "=" + markers.Count(m => m.ContainsSource(d.Settings))))}");
        if (!detailed) return;
        foreach (TilemapGeneratedColliderMarker marker in markers.OrderBy(m => m.name))
        {
            BoxCollider box = marker.GetComponent<BoxCollider>();
            TilemapCollisionAuthoring source = marker.Source;
            Tilemap tilemap = source != null ? source.GetComponent<Tilemap>() : null;
            List<string> neighbors = new List<string>();
            foreach (TilemapGeneratedColliderMarker other in markers)
            {
                if (other == marker || !RangesTouch(marker, other)) continue;
                neighbors.Add(other.name + " (" + ExplainNotMerged(marker, other) + ")");
            }
            Debug.Log($"[Tilemap 3D Collider] Name='{marker.name}' Grid='{(tilemap != null && tilemap.layoutGrid != null ? GetPath(tilemap.layoutGrid.transform) : "Missing")}' Tilemap='{(tilemap != null ? GetPath(tilemap.transform) : "Missing")}' Cells={marker.MinimumCell} Size={marker.SizeInCells} Role={(source != null ? source.Role.ToString() : "Missing")} Group='{(source != null ? source.CollisionGroupId : string.Empty)}' World={ResolveWorldKey(marker.transform)} Layer={LayerMask.LayerToName(marker.gameObject.layer)}({marker.gameObject.layer}) Trigger={box.isTrigger} CenterZ={box.center.z} Depth={box.size.z} Merge={(source != null && source.MergeEnabled)} LocalBounds=Center:{box.center},Size:{box.size} WorldBounds={box.bounds} Neighbors=[{string.Join("; ", neighbors)}]", marker);
        }
    }

    private static bool RangesTouch(TilemapGeneratedColliderMarker a, TilemapGeneratedColliderMarker b)
    {
        Vector3Int aMax = a.MinimumCell + a.SizeInCells;
        Vector3Int bMax = b.MinimumCell + b.SizeInCells;
        bool verticalOverlap = a.MinimumCell.y < bMax.y && b.MinimumCell.y < aMax.y;
        bool horizontalOverlap = a.MinimumCell.x < bMax.x && b.MinimumCell.x < aMax.x;
        return (verticalOverlap && (aMax.x == b.MinimumCell.x || bMax.x == a.MinimumCell.x)) ||
               (horizontalOverlap && (aMax.y == b.MinimumCell.y || bMax.y == a.MinimumCell.y));
    }

    private static string ExplainNotMerged(TilemapGeneratedColliderMarker a, TilemapGeneratedColliderMarker b)
    {
        TilemapCollisionAuthoring sa = a.Source, sb = b.Source;
        if (sa == null || sb == null) return "source missing";
        Tilemap ta = sa.GetComponent<Tilemap>(), tb = sb.GetComponent<Tilemap>();
        List<string> reasons = new List<string>();
        if (ta.layoutGrid != tb.layoutGrid) reasons.Add("Grid differs");
        if (ResolveWorldKey(ta.transform) != ResolveWorldKey(tb.transform)) reasons.Add("World differs");
        if (sa.Role != sb.Role) reasons.Add("Role differs");
        if (sa.CollisionGroupId != sb.CollisionGroupId) reasons.Add("Collision Group differs");
        if (sa.GeneratedLayer != sb.GeneratedLayer) reasons.Add($"Layer {LayerMask.LayerToName(sa.GeneratedLayer)} vs {LayerMask.LayerToName(sb.GeneratedLayer)}");
        if (sa.IsTrigger != sb.IsTrigger) reasons.Add("Trigger differs");
        if (!Mathf.Approximately(sa.CollisionCenterZ, sb.CollisionCenterZ)) reasons.Add($"CenterZ {sa.CollisionCenterZ} vs {sb.CollisionCenterZ}");
        if (!Mathf.Approximately(sa.CollisionDepth, sb.CollisionDepth)) reasons.Add($"Depth {sa.CollisionDepth} vs {sb.CollisionDepth}");
        if (ta != tb && (!sa.AllowCrossTilemapMerge || !sb.AllowCrossTilemapMerge)) reasons.Add("Cross Tilemap Merge disabled");
        if (reasons.Count == 0) reasons.Add("union is not one empty-free rectangle (L/irregular boundary)");
        return string.Join(", ", reasons);
    }

    private static int CountVisualGapCandidates(List<TilemapData> data)
    {
        int count = 0;
        foreach (TilemapData item in data.Where(d => d.Settings != null && d.Settings.Role == TilemapCollisionRole.Solid))
            foreach (Vector3Int cell in item.Cells)
                if (!item.Cells.Contains(cell + Vector3Int.right) && item.Cells.Contains(cell + Vector3Int.right * 2)) count++;
        return count;
    }

    private static void RemoveAllOwnedRoots(Scene scene)
    {
        foreach (TilemapGeneratedColliderMarker marker in FindSceneObjects<TilemapGeneratedColliderMarker>(scene))
            if (marker != null && marker.Kind == TilemapGeneratedColliderMarker.MarkerKind.Root) Undo.DestroyObjectImmediate(marker.gameObject);
    }

    private static string GetPath(Transform transform)
    {
        if (transform == null) return "<null>";
        List<string> parts = new List<string>();
        for (Transform t = transform; t != null; t = t.parent) parts.Add(t.name);
        parts.Reverse(); return string.Join("/", parts);
    }

    private static string FormatIssue(Scene scene, TilemapData item, Vector3Int? cell, string problem, string fix)
    {
        GridLayout grid = item.Tilemap.layoutGrid;
        string gridName = grid != null ? grid.name : "<missing Grid>";
        string cellText = cell.HasValue ? cell.Value.ToString() : "<multiple>";
        return $"Scene='{scene.name}', Grid='{gridName}', Tilemap='{item.Tilemap.name}', Cell={cellText}: {problem}. Fix: {fix}";
    }
}
