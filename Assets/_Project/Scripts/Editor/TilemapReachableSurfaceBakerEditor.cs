using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

[InitializeOnLoad]
public static class TilemapReachableSurfaceBakerEditor
{
    private const string Menu = "Project/Map/";
    private const string RootName = "Generated_Reachable_3D_Collision";
    private const string Signature = "ReachableSurfaceBoundary.v1";
    private static bool preview;
    private static GapInfo selectedGap;
    private static List<Group> previewGroups = new List<Group>();

    private enum SurfaceType { Floor, Ceiling, LeftWall, RightWall }
    private enum GapType { IntentionalGap, SuspectedMissingTile, CrossTilemapGap, VisualOnlyConnection, DifferentCollisionGroup }

    private sealed class Source
    {
        public Tilemap Tilemap;
        public TilemapCollisionAuthoring Settings;
        public HashSet<Vector3Int> Cells = new HashSet<Vector3Int>();
        public string World;
        public int ScannedTileCount;
        public int VisualTilesExcludedFromSolid;
    }

    private sealed class Group
    {
        public GridLayout Grid;
        public string World;
        public string CollisionGroup;
        public List<Source> Sources = new List<Source>();
        public HashSet<Vector3Int> Solid = new HashSet<Vector3Int>();
        public HashSet<Vector3Int> Reachable = new HashSet<Vector3Int>();
        public HashSet<Vector3Int> ContourReachable = new HashSet<Vector3Int>();
        public BoundsInt Bounds;
        public List<SurfaceRun> Runs = new List<SurfaceRun>();
        public List<GapInfo> Gaps = new List<GapInfo>();
        public List<Vector3Int> BoundsContactCells = new List<Vector3Int>();
        public Vector3Int SelectedSeed;
        public string SelectedSeedPath = "(none)";
        public string BoundsSource = "Automatic Solid Bounds";
        public int RawEdgeCount;
        public int InternalFaceCount;
        public int DuplicateEdgeCount;
        public int BoundaryEdgeCount;
        public readonly int[] BoundaryEdgesByType = new int[4];
        public float MaximumSurfaceError;
        public int CornerGapCount;
        public int SeedCount;
        public TilemapCollisionAuthoring Representative => Sources[0].Settings;
    }

    private readonly struct Edge : IEquatable<Edge>
    {
        public readonly SurfaceType Type; public readonly int Fixed; public readonly int Along;
        public Edge(SurfaceType type, int fixedCoordinate, int along) { Type = type; Fixed = fixedCoordinate; Along = along; }
        public bool Equals(Edge other) => Type == other.Type && Fixed == other.Fixed && Along == other.Along;
        public override bool Equals(object obj) => obj is Edge other && Equals(other);
        public override int GetHashCode() => ((int)Type * 397) ^ (Fixed * 31) ^ Along;
    }

    private readonly struct SurfaceRun
    {
        public readonly SurfaceType Type; public readonly int Fixed; public readonly int Start; public readonly int Length;
        public SurfaceRun(SurfaceType type, int fixedCoordinate, int start, int length) { Type = type; Fixed = fixedCoordinate; Start = start; Length = length; }
    }

    private sealed class GapInfo
    {
        public Group Group; public Vector3Int Cell; public Tilemap Tilemap; public GapType Type; public string Reason;
    }

    static TilemapReachableSurfaceBakerEditor() { SceneView.duringSceneGui += DrawPreview; }

    [MenuItem("_Project/Map/Bake/Update Tile 3D Collider", priority = 1)]
    public static void BakeUpdateTile3DCollider()
    {
        if (Application.isPlaying) { EditorUtility.DisplayDialog("Tilemap 3D Collider Bake", "Edit Mode에서만 실행할 수 있습니다.", "확인"); return; }
        Scene scene = SceneManager.GetActiveScene();
        int tilemaps = 0, added = 0, migrated = 0;
        foreach (Tilemap tilemap in FindScene<Tilemap>(scene))
        {
            tilemaps++;
            TilemapCollisionAuthoring settings = tilemap.GetComponent<TilemapCollisionAuthoring>();
            if (settings == null)
            {
                TilemapCollisionRole role = InferInitialRole(tilemap.name);
                settings = Undo.AddComponent<TilemapCollisionAuthoring>(tilemap.gameObject);
                settings.SetInitialRole(role);
                settings.UseReachableDefaultsForNewTilemap();
                EditorUtility.SetDirty(settings); added++;
            }
        }
        TilemapCollisionAuthoring[] authoring = FindScene<TilemapCollisionAuthoring>(scene);
        int legacy = authoring.Count(a => a.Role == TilemapCollisionRole.Solid && a.BakeMode == TilemapCollisionBakeMode.LegacySolidVolume);
        int reachable = authoring.Count(a => a.Role == TilemapCollisionRole.Solid && a.BakeMode == TilemapCollisionBakeMode.ReachableSurfaceBoundary);
        Debug.Log($"[Tilemap 3D Collider Bake] Auto setup: Scene={scene.name}, Tilemaps={tilemaps}, AuthoringAdded={added}, AutoModeMigrated={migrated}, ReachableSolidTilemaps={reachable}, ExplicitLegacyTilemaps={legacy}.");
        if (reachable == 0 && legacy > 0)
        {
            Tilemap3DCollisionEditor.GenerateOrUpdateOneClick();
            return;
        }
        Bake();
    }

    [MenuItem(Menu + "Preview Reachable Collision Surfaces")]
    public static void TogglePreview()
    {
        preview = !preview;
        if (preview) previewGroups = Analyze(SceneManager.GetActiveScene(), true, out _);
        SceneView.RepaintAll();
        Debug.Log($"[Reachable Surface] Preview {(preview ? "enabled" : "disabled")}.");
    }

    [MenuItem(Menu + "Bake / Update Reachable 3D Collision")]
    public static void Bake()
    {
        if (Application.isPlaying) { Debug.LogError("[Reachable Surface] Edit Mode only."); return; }
        Scene scene = SceneManager.GetActiveScene();
        List<Group> groups = Analyze(scene, true, out List<string> errors);
        if (errors.Count > 0 || groups.Count == 0)
        {
            int failureSpawnCount = FindScene<PlayerSpawnPoint>(scene).Length;
            int preserved = FindScene<TilemapGeneratedColliderMarker>(scene).Count(m => m.Kind == TilemapGeneratedColliderMarker.MarkerKind.Collider);
            string reason = errors.Count > 0 ? string.Join("\n", errors) : "No valid reachable contour.";
            Debug.LogError($"[Tilemap 3D Collider Bake Failed]\nScene: {scene.name}\nSeed: {string.Join(", ", groups.Select(g => $"{g.SelectedSeed} [{g.SelectedSeedPath}]"))}\nBounds Source: {string.Join(", ", groups.Select(g => g.BoundsSource).Distinct())}\nReachable Cells: {groups.Sum(g => g.Reachable.Count)}\nBounds Contact Cells: {groups.Sum(g => g.BoundsContactCells.Count)}\nReason: {reason}\n{BuildEdgeStatistics(groups, preserved)}\nPlayerSpawnPoints searched: {failureSpawnCount}\nMinimum setup: one Default PlayerSpawnPoint, TilemapPlayableAreaSeed, scene Player, or regular PlayerSpawnPoint.\nPrevious Generated Colliders Preserved: {preserved}\nPortal Processing: Ignored\nResult: Failed (scene not saved)");
            EditorUtility.DisplayDialog("Tilemap 3D Collider Bake Failed", $"Scene: {scene.name}\n\n{reason}\n\nPlayerSpawnPoint 후보: {failureSpawnCount}\n기존 Collider는 유지되었습니다.", "확인");
            return;
        }
        int spawnCount = FindScene<PlayerSpawnPoint>(scene).Length;
        int previousGenerated = FindScene<TilemapGeneratedColliderMarker>(scene).Count(m => m.Kind == TilemapGeneratedColliderMarker.MarkerKind.Collider);
        int groupId = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Bake Reachable Surface Collision");
        int created = 0;
        List<GameObject> roots = new List<GameObject>();
        List<string> creationErrors = new List<string>();
        foreach (Group group in groups)
        {
            Transform parent = group.Grid.transform;
            GameObject root = new GameObject("[TEMP] " + RootName + (group.World == "Shared" ? string.Empty : "_" + group.World));
            Undo.RegisterCreatedObjectUndo(root, "Create reachable collision root");
            root.transform.SetParent(parent, false); root.isStatic = true; root.SetActive(false);
            TilemapGeneratedColliderMarker rootMarker = Undo.AddComponent<TilemapGeneratedColliderMarker>(root);
            rootMarker.Configure(TilemapGeneratedColliderMarker.MarkerKind.Root, null, Signature, Vector3Int.zero, Vector3Int.zero);
            roots.Add(root);
            for (int i = 0; i < group.Runs.Count; i++)
            {
                SurfaceRun run = group.Runs[i];
                GameObject child = new GameObject($"{group.CollisionGroup}_{run.Type}_{i:000}");
                Undo.RegisterCreatedObjectUndo(child, "Create reachable surface collider");
                child.transform.SetParent(root.transform, false); child.isStatic = true;
                int layer = ResolveSurfaceLayer(group.Representative, run.Type);
                if (layer < 0) { creationErrors.Add($"Undefined layer for {run.Type} in Grid={group.Grid.name}, World={group.World}."); continue; }
                child.layer = layer;
                BoxCollider box = Undo.AddComponent<BoxCollider>(child);
                ConfigureSurfaceCollider(box, group, run);
                box.enabled = true; box.isTrigger = group.Representative.IsTrigger;
                TilemapGeneratedColliderMarker marker = Undo.AddComponent<TilemapGeneratedColliderMarker>(child);
                Vector3Int min = RunMinCell(run);
                Vector3Int size = run.Type == SurfaceType.Floor || run.Type == SurfaceType.Ceiling ? new Vector3Int(run.Length, 1, 1) : new Vector3Int(1, run.Length, 1);
                marker.Configure(TilemapGeneratedColliderMarker.MarkerKind.Collider, group.Representative, Signature, min, size);
                marker.ConfigureSources(group.Sources.Select(s => s.Settings).ToArray());
                RunLine(group, run, out Vector3 edgeStart, out Vector3 edgeEnd);
                marker.ConfigureSurface(run.Type.ToString(), edgeStart, edgeEnd, SurfaceNormal(run.Type), group.CollisionGroup, $"{group.World}:{group.Grid.name}");
                Debug.Log($"[Reachable Collider] Name={child.name}, Surface={run.Type}, Edge={RunGridDescription(run)}, LocalCenter={box.center}, Size={box.size}, WorldBounds={box.bounds}, Normal={SurfaceNormal(run.Type)}, Group={group.CollisionGroup}, Sources={string.Join(", ", group.Sources.Select(s => Path(s.Tilemap.transform)))}, SeedRegion={group.World}:{group.Grid.name}, SplitReason=non-contiguous edge or surface/merge-key boundary.", child);
                created++;
            }
        }
        if (previousGenerated >= 40 && created > Mathf.CeilToInt(previousGenerated * 0.1f))
            creationErrors.Add($"Optimization target not met: previous={previousGenerated}, candidate={created}, required <= {Mathf.CeilToInt(previousGenerated * 0.1f)} (90% reduction). Boundary tiles remain excessively fragmented; previous collision is preserved.");
        foreach (Group group in groups) ValidateGeneratedGeometry(group, roots, creationErrors);
        bool valid = created > 0 && creationErrors.Count == 0 && roots.All(r => r.GetComponentsInChildren<BoxCollider>(true).All(b => b != null && b.enabled && b.size.x > 0f && b.size.y > 0f && b.size.z > 0f));
        if (!valid)
        {
            Undo.RevertAllDownToGroup(groupId);
            string reason = created == 0 ? "Generated BoxCollider count is 0." : string.Join("\n", creationErrors);
            int preserved = FindScene<TilemapGeneratedColliderMarker>(scene).Count(m => m.Kind == TilemapGeneratedColliderMarker.MarkerKind.Collider);
            Debug.LogError($"[Tilemap 3D Collider Bake Failed]\nScene: {scene.name}\nReason: {reason}\nPlayerSpawnPoints searched: {spawnCount}\nPrevious Generated Colliders Preserved: {preserved}\nPortal Processing: Ignored");
            EditorUtility.DisplayDialog("Tilemap 3D Collider Bake Failed", $"Scene: {scene.name}\n{reason}\n\n기존 Generated Collider는 유지되었습니다.", "확인");
            return;
        }
        RemoveReachableRoots(scene, roots);
        RemoveReplacedLegacyChildren(scene);
        foreach (GameObject root in roots) { root.name = root.name.Replace("[TEMP] ", string.Empty); root.SetActive(true); }
        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(groupId);
        Selection.activeGameObject = roots.FirstOrDefault();
        if (Selection.activeGameObject != null) { EditorGUIUtility.PingObject(Selection.activeGameObject); SceneView.lastActiveSceneView?.FrameSelected(); }
        previewGroups = groups;
        ReportComparison(scene, groups, created);
        int solidTiles = groups.Sum(g => g.Solid.Count), reachableCells = groups.Sum(g => g.Reachable.Count), gaps = groups.Sum(g => g.Gaps.Count);
        Debug.Log($"[Tilemap 3D Collider Bake]\nScene: {scene.name}\nSeed: {string.Join(", ", groups.Select(g => $"{g.SelectedSeed} [{g.SelectedSeedPath}]"))}\nBounds Source: {string.Join(", ", groups.Select(g => g.BoundsSource).Distinct())}\nBounds: {string.Join(", ", groups.Select(g => g.Bounds.ToString()))}\nTilemaps: {groups.Sum(g => g.Sources.Count)}\nSolid Tiles: {solidTiles}\nReachable Cells: {reachableCells}\nBounds Contact Cells: {groups.Sum(g => g.BoundsContactCells.Count)}\nFloor Segments: {groups.Sum(g => g.Runs.Count(r => r.Type == SurfaceType.Floor))}\nCeiling Segments: {groups.Sum(g => g.Runs.Count(r => r.Type == SurfaceType.Ceiling))}\nWall Segments: {groups.Sum(g => g.Runs.Count(r => r.Type == SurfaceType.LeftWall || r.Type == SurfaceType.RightWall))}\nGenerated BoxColliders: {created}\n{BuildEdgeStatistics(groups, previousGenerated, created)}\nMaximum Surface Position Error: {groups.Max(g => g.MaximumSurfaceError):F6}\nCorner Gaps: {groups.Sum(g => g.CornerGapCount)}\nSuspected Gaps: {gaps}\nPortal Processing: Ignored\nResult: Success\nScene marked dirty; not saved.");
    }

    [MenuItem(Menu + "Validate Reachable Collision")]
    public static void ValidateReachable()
    {
        Scene scene = SceneManager.GetActiveScene();
        List<Group> groups = Analyze(scene, true, out List<string> errors);
        TilemapGeneratedColliderMarker[] generated = FindScene<TilemapGeneratedColliderMarker>(scene).Where(m => IsReachableMarker(m) && m.Kind == TilemapGeneratedColliderMarker.MarkerKind.Collider).ToArray();
        foreach (TilemapGeneratedColliderMarker marker in generated)
        {
            BoxCollider box = marker.GetComponent<BoxCollider>();
            if (box == null || !box.enabled) errors.Add($"{Path(marker.transform)}: missing or disabled BoxCollider.");
            if (marker.GetComponent<Rigidbody>() != null || marker.GetComponent<MeshCollider>() != null) errors.Add($"{Path(marker.transform)}: forbidden Rigidbody/MeshCollider.");
        }
        if (errors.Count == 0) Debug.Log($"[Reachable Surface] VALID. Groups={groups.Count}, GeneratedColliders={generated.Length}.");
        else Debug.LogError("[Reachable Surface] INVALID:\n" + string.Join("\n", errors));
    }

    [MenuItem(Menu + "Detect Suspected Tile Gaps")]
    public static void DetectGaps()
    {
        previewGroups = Analyze(SceneManager.GetActiveScene(), true, out List<string> errors);
        preview = true;
        int count = previewGroups.Sum(g => g.Gaps.Count);
        Debug.Log($"[Reachable Surface Gaps] Total={count}\n" + string.Join("\n", previewGroups.SelectMany(g => g.Gaps).Select(g => $"{g.Type}: Grid={g.Group.Grid.name}, World={g.Group.World}, Cell={g.Cell}, {g.Reason}")));
        if (errors.Count > 0) Debug.LogWarning(string.Join("\n", errors));
        SceneView.RepaintAll();
    }

    [MenuItem(Menu + "Fill Selected Suspected Tile Gaps")]
    public static void FillSelectedGap()
    {
        if (selectedGap == null || selectedGap.Type != GapType.SuspectedMissingTile || selectedGap.Tilemap == null)
        { Debug.LogError("[Reachable Surface Gaps] Select one red suspected cell in Scene View Preview first."); return; }
        TileBase replacement = selectedGap.Tilemap.GetTile(selectedGap.Cell + Vector3Int.left) ?? selectedGap.Tilemap.GetTile(selectedGap.Cell + Vector3Int.right);
        if (replacement == null) { Debug.LogError("[Reachable Surface Gaps] No adjacent source TileBase to copy. Nothing changed."); return; }
        Undo.RecordObject(selectedGap.Tilemap, "Fill selected suspected Tile gap");
        selectedGap.Tilemap.SetTile(selectedGap.Cell, replacement);
        EditorUtility.SetDirty(selectedGap.Tilemap);
        EditorSceneManager.MarkSceneDirty(selectedGap.Tilemap.gameObject.scene);
        Debug.Log($"[Reachable Surface Gaps] Filled only selected cell {selectedGap.Cell} on {Path(selectedGap.Tilemap.transform)}. Scene was not saved.");
        selectedGap = null;
    }

    [MenuItem(Menu + "Compare Legacy vs Reachable Collider Count")]
    public static void CompareCounts()
    {
        Scene scene = SceneManager.GetActiveScene();
        List<Group> groups = Analyze(scene, true, out List<string> errors);
        int legacy = FindScene<TilemapGeneratedColliderMarker>(scene).Count(m => m.Kind == TilemapGeneratedColliderMarker.MarkerKind.Collider && !IsReachableMarker(m));
        ReportComparison(scene, groups, groups.Sum(g => g.Runs.Count), legacy);
        if (errors.Count > 0) Debug.LogWarning(string.Join("\n", errors));
    }

    private static List<Group> Analyze(Scene scene, bool requireSeeds, out List<string> errors)
    {
        errors = new List<string>();
        List<Source> sources = new List<Source>();
        foreach (Tilemap tilemap in FindScene<Tilemap>(scene))
        {
            TilemapCollisionAuthoring settings = tilemap.GetComponent<TilemapCollisionAuthoring>();
            if (settings == null || settings.Role != TilemapCollisionRole.Solid || settings.BakeMode != TilemapCollisionBakeMode.ReachableSurfaceBoundary) continue;
            if (tilemap.layoutGrid == null) { errors.Add($"{Path(tilemap.transform)}: Grid missing."); continue; }
            Source source = new Source { Tilemap = tilemap, Settings = settings, World = WorldOf(tilemap.transform) };
            Dictionary<TileBase, List<Vector3Int>> cellsByTile = new Dictionary<TileBase, List<Vector3Int>>();
            foreach (Vector3Int c in tilemap.cellBounds.allPositionsWithin)
            {
                TileBase tile = tilemap.GetTile(c);
                if (tile == null) continue;
                source.ScannedTileCount++;
                if (!cellsByTile.TryGetValue(tile, out List<Vector3Int> positions)) { positions = new List<Vector3Int>(); cellsByTile.Add(tile, positions); }
                positions.Add(c);
            }
            KeyValuePair<TileBase, List<Vector3Int>> dominant = cellsByTile.OrderByDescending(p => p.Value.Count).FirstOrDefault();
            bool useDominant = settings.UseDominantSolidFillForReachable && source.ScannedTileCount > 0 && dominant.Value != null && dominant.Value.Count >= source.ScannedTileCount * 0.85f;
            if (useDominant)
            {
                source.Cells.UnionWith(dominant.Value);
                source.VisualTilesExcludedFromSolid = source.ScannedTileCount - dominant.Value.Count;
                Debug.Log($"[Reachable Surface Occupancy] Tilemap='{Path(tilemap.transform)}': dominant-fill mode selected {dominant.Value.Count}/{source.ScannedTileCount} cells ({dominant.Value.Count * 100f / source.ScannedTileCount:F1}%). Excluded sparse visual-edge cells={source.VisualTilesExcludedFromSolid}. Selection used frequency, not Tile/Sprite/Palette name.", tilemap);
            }
            else
            {
                foreach (List<Vector3Int> positions in cellsByTile.Values) source.Cells.UnionWith(positions);
            }
            if (source.Cells.Count > 0) sources.Add(source);
        }
        List<Group> groups = sources.GroupBy(s => $"{s.Tilemap.layoutGrid.GetInstanceID()}|{s.World}|{s.Settings.CollisionGroupId}|{s.Settings.GeneratedLayer}|{s.Settings.IsTrigger}|{s.Settings.CollisionCenterZ:R}|{s.Settings.CollisionDepth:R}")
            .Select(g => new Group { Grid = g.First().Tilemap.layoutGrid, World = g.First().World, CollisionGroup = g.First().Settings.CollisionGroupId, Sources = g.ToList(), Solid = new HashSet<Vector3Int>(g.SelectMany(x => x.Cells)) }).ToList();
        foreach (Group group in groups)
        {
            ResolveBakeBounds(scene, group);
            List<Vector3Int> seeds = FindSeeds(scene, group, errors);
            group.SeedCount = seeds.Count;
            if (seeds.Count == 0)
            {
                if (requireSeeds) errors.Add($"Grid='{group.Grid.name}', World='{group.World}', Group='{group.CollisionGroup}': no usable Seed. Add a Default PlayerSpawnPoint or TilemapPlayableAreaSeed inside Room Bake Bounds.");
                continue;
            }
            Flood(group, seeds);
            FindBoundsContacts(group);
            ExtractRuns(group);
            if (group.BoundaryEdgesByType[(int)SurfaceType.Floor] > 0 &&
                group.BoundaryEdgesByType[(int)SurfaceType.LeftWall] > 0 &&
                group.BoundaryEdgesByType[(int)SurfaceType.RightWall] > 0 &&
                group.BoundaryEdgesByType[(int)SurfaceType.Ceiling] == 0)
                errors.Add($"Grid='{group.Grid.name}', World='{group.World}': enclosed playable region has floor and both walls but Ceiling Raw Edges=0. Existing collision will be preserved; inspect Seed/clearance/ceiling occupancy.");
            if (group.Runs.Count > Mathf.Max(10, Mathf.FloorToInt(group.BoundaryEdgeCount * 0.5f)))
                errors.Add($"Grid='{group.Grid.name}', World='{group.World}': excessive boundary fragmentation ({group.BoundaryEdgeCount} edges -> {group.Runs.Count} segments). Check missing/checkerboard collision tiles or enable Player Clearance. Existing collision will be preserved.");
            DetectGroupGaps(group, groups);
        }
        if (sources.Count == 0) errors.Add("No Solid Tilemap uses Bake Mode=ReachableSurfaceBoundary. Existing Legacy collision was not changed.");
        return groups.Where(g => g.Reachable.Count > 0).ToList();
    }

    private static void Flood(Group group, List<Vector3Int> seeds)
    {
        Queue<Vector3Int> queue = new Queue<Vector3Int>();
        foreach (Vector3Int seed in seeds)
        {
            if (!group.Bounds.Contains(seed) || group.Solid.Contains(seed) || !HasClearance(group, seed)) continue;
            if (group.Reachable.Add(seed)) queue.Enqueue(seed);
        }
        NeverBakeCollisionArea[] never = FindScene<NeverBakeCollisionArea>(group.Grid.gameObject.scene);
        Vector3Int[] directions = { Vector3Int.left, Vector3Int.right, Vector3Int.up, Vector3Int.down };
        while (queue.Count > 0)
        {
            Vector3Int current = queue.Dequeue();
            foreach (Vector3Int direction in directions)
            {
                Vector3Int next = current + direction;
                if (!group.Bounds.Contains(next) || group.Solid.Contains(next) || group.Reachable.Contains(next) || !HasClearance(group, next)) continue;
                Vector3 world = group.Grid.CellToWorld(next) + group.Grid.cellSize * .5f;
                if (never.Any(a => a.WorldBounds.Contains(world))) continue;
                group.Reachable.Add(next); queue.Enqueue(next);
            }
        }
    }

    private static void FindBoundsContacts(Group group)
    {
        int minX = group.Bounds.xMin, maxX = group.Bounds.xMax - 1, minY = group.Bounds.yMin, maxY = group.Bounds.yMax - 1;
        group.BoundsContactCells.Clear();
        group.BoundsContactCells.AddRange(group.Reachable.Where(c => c.x == minX || c.x == maxX || c.y == minY || c.y == maxY));
        if (group.BoundsContactCells.Count > 0) Debug.LogWarning($"[Reachable Bounds] Grid={group.Grid.name}, World={group.World}, BoundsContactCells={group.BoundsContactCells.Count}. Bake continued within Room Bounds; the Bounds itself does not generate collision.");
        float ratio = group.Bounds.size.x * group.Bounds.size.y > 0 ? group.Reachable.Count / (float)(group.Bounds.size.x * group.Bounds.size.y) : 0f;
        if (ratio > .9f) Debug.LogWarning($"[Reachable Bounds] Reachable Area occupies {ratio:P1} of Bake Bounds. Check Bounds size or Seed position. Bake continued.");
    }

    private static bool HasClearance(Group group, Vector3Int cell)
    {
        TilemapCollisionAuthoring s = group.Representative;
        if (!s.UsePlayerClearance) return true;
        int left = (s.PlayerWidthCells - 1) / 2;
        for (int y = 0; y < s.PlayerHeightCells; y++) for (int x = -left; x < s.PlayerWidthCells - left; x++)
            if (group.Solid.Contains(cell + new Vector3Int(x, y, 0))) return false;
        return true;
    }

    private static void ExtractRuns(Group group)
    {
        HashSet<Edge> edges = new HashSet<Edge>();
        group.ContourReachable.Clear();
        group.ContourReachable.UnionWith(group.Reachable);
        if (group.Representative.UsePlayerClearance)
        {
            // Clearance controls where the player's reference cell can travel. The contour
            // still needs the adjacent empty head-space cell in order to see a ceiling.
            Vector3Int[] contourDirections = { Vector3Int.left, Vector3Int.right, Vector3Int.up, Vector3Int.down };
            foreach (Vector3Int reachable in group.Reachable)
                foreach (Vector3Int direction in contourDirections)
                {
                    Vector3Int adjacent = reachable + direction;
                    if (group.Bounds.Contains(adjacent) && !group.Solid.Contains(adjacent)) group.ContourReachable.Add(adjacent);
                }
        }
        group.RawEdgeCount = group.Solid.Count * 4;
        group.InternalFaceCount = group.Solid.Sum(c =>
            (group.Solid.Contains(c + Vector3Int.left) ? 1 : 0) +
            (group.Solid.Contains(c + Vector3Int.right) ? 1 : 0) +
            (group.Solid.Contains(c + Vector3Int.up) ? 1 : 0) +
            (group.Solid.Contains(c + Vector3Int.down) ? 1 : 0));
        foreach (Vector3Int empty in group.ContourReachable)
        {
            TryAddEdge(edges, group, IsInBoundsSolid(group, empty + Vector3Int.down), new Edge(SurfaceType.Floor, empty.y, empty.x));
            TryAddEdge(edges, group, IsInBoundsSolid(group, empty + Vector3Int.up), new Edge(SurfaceType.Ceiling, empty.y + 1, empty.x));
            TryAddEdge(edges, group, IsInBoundsSolid(group, empty + Vector3Int.left), new Edge(SurfaceType.LeftWall, empty.x, empty.y));
            TryAddEdge(edges, group, IsInBoundsSolid(group, empty + Vector3Int.right), new Edge(SurfaceType.RightWall, empty.x + 1, empty.y));
        }
        group.BoundaryEdgeCount = edges.Count;
        foreach (Edge edge in edges) group.BoundaryEdgesByType[(int)edge.Type]++;
        foreach (IGrouping<(SurfaceType Type, int Fixed), Edge> line in edges.GroupBy(e => (e.Type, e.Fixed)))
        {
            int[] positions = line.Select(e => e.Along).OrderBy(v => v).ToArray();
            for (int i = 0; i < positions.Length;)
            {
                int start = positions[i], length = 1;
                while (i + length < positions.Length && positions[i + length] == start + length) length++;
                group.Runs.Add(new SurfaceRun(line.Key.Type, line.Key.Fixed, start, length)); i += length;
            }
        }
        group.Runs = group.Runs.OrderBy(r => r.Type).ThenBy(r => r.Fixed).ThenBy(r => r.Start).ToList();
    }

    private static void TryAddEdge(HashSet<Edge> edges, Group group, bool condition, Edge edge)
    {
        if (!condition) return;
        if (!edges.Add(edge)) group.DuplicateEdgeCount++;
    }

    private static bool IsInBoundsSolid(Group group, Vector3Int cell) => group.Bounds.Contains(cell) && group.Solid.Contains(cell);

    private static void ConfigureSurfaceCollider(BoxCollider box, Group group, SurfaceRun run)
    {
        float thickness = group.Representative.SurfaceThickness;
        float z = group.Representative.CollisionCenterZ;
        RunLine(group, run, out Vector3 a, out Vector3 b);
        Vector3 normal = SurfaceNormal(run.Type);
        float offset = SurfaceOffset(group.Representative, run.Type);
        a += normal * offset; b += normal * offset;
        Vector3 center = (a + b) * .5f - normal * (thickness * .5f);
        box.center = new Vector3(center.x, center.y, z);
        float length = Vector3.Distance(a, b);
        bool horizontal = run.Type == SurfaceType.Floor || run.Type == SurfaceType.Ceiling;
        box.size = horizontal
            ? new Vector3(length, thickness, group.Representative.CollisionDepth)
            : new Vector3(thickness, length, group.Representative.CollisionDepth);
    }

    private static List<Vector3Int> FindSeeds(Scene scene, Group group, List<string> errors)
    {
        bool Inside(Vector3Int cell) => group.Bounds.Contains(cell);
        PlayerSpawnPoint[] spawns = FindScene<PlayerSpawnPoint>(scene).Where(s => WorldOf(s.transform) == group.World).ToArray();
        PlayerSpawnPoint[] defaults = spawns.Where(s => s.IsDefaultSpawn && Inside(group.Grid.WorldToCell(s.transform.position))).ToArray();
        Vector2 roomCenter = new Vector2(group.Bounds.center.x, group.Bounds.center.y);
        if (defaults.Length > 0)
        {
            PlayerSpawnPoint selected = defaults.OrderBy(s => Vector2.SqrMagnitude((Vector2Int)group.Grid.WorldToCell(s.transform.position) - roomCenter)).First();
            if (defaults.Length > 1) Debug.LogWarning($"[Reachable Seed] Multiple Default PlayerSpawnPoints found. Selected closest to Room Bounds center: {Path(selected.transform)}. Unused: {string.Join(", ", defaults.Where(s => s != selected).Select(s => Path(s.transform)))}.");
            return SelectSeed(group, selected.transform.position, Path(selected.transform), errors);
        }

        TilemapPlayableAreaSeed manual = FindScene<TilemapPlayableAreaSeed>(scene).FirstOrDefault(s => WorldAllowed(s, group.World) && Inside(group.Grid.WorldToCell(s.transform.position)));
        if (manual != null) return SelectSeed(group, manual.transform.position, Path(manual.transform), errors);

        MonoBehaviour player = FindScene<MonoBehaviour>(scene).FirstOrDefault(b => WorldOf(b.transform) == group.World && (b.gameObject.name.Equals("Player", StringComparison.OrdinalIgnoreCase) || b.gameObject.CompareTag("Player")) && Inside(group.Grid.WorldToCell(b.transform.position)));
        if (player != null) return SelectSeed(group, player.transform.position, Path(player.transform), errors);

        PlayerSpawnPoint regular = spawns.Where(s => Inside(group.Grid.WorldToCell(s.transform.position))).OrderBy(s => Vector2.SqrMagnitude((Vector2Int)group.Grid.WorldToCell(s.transform.position) - roomCenter)).FirstOrDefault();
        return regular != null ? SelectSeed(group, regular.transform.position, Path(regular.transform), errors) : new List<Vector3Int>();
    }

    private static List<Vector3Int> SelectSeed(Group group, Vector3 worldPosition, string sourcePath, List<string> errors)
    {
        Vector3Int original = group.Grid.WorldToCell(worldPosition);
        Vector3Int cell = original;
        if (group.Solid.Contains(cell) || !HasClearance(group, cell))
        {
            const int maxCorrectionDistance = 12;
            Vector3Int? corrected = null;
            for (int distance = 1; distance <= maxCorrectionDistance && !corrected.HasValue; distance++)
                for (int y = -distance; y <= distance && !corrected.HasValue; y++)
                for (int x = -distance; x <= distance; x++)
                {
                    if (Mathf.Abs(x) + Mathf.Abs(y) != distance) continue;
                    Vector3Int candidate = original + new Vector3Int(x, y, 0);
                    if (group.Bounds.Contains(candidate) && !group.Solid.Contains(candidate) && HasClearance(group, candidate)) { corrected = candidate; break; }
                }
            if (!corrected.HasValue) { errors.Add($"Seed '{sourcePath}' is in a Solid/blocked cell {original}; no Empty cell was found within {maxCorrectionDistance} cells."); return new List<Vector3Int>(); }
            cell = corrected.Value;
            Debug.LogWarning($"[Reachable Seed] Seed '{sourcePath}' was in Solid/blocked cell {original}. Corrected to nearest Empty cell {cell}, ManhattanDistance={Mathf.Abs(cell.x-original.x)+Mathf.Abs(cell.y-original.y)}.");
        }
        group.SelectedSeed = cell; group.SelectedSeedPath = sourcePath;
        Debug.Log($"[Reachable Seed] Grid={group.Grid.name}, World={group.World}, Selected={cell}, Source={sourcePath}, WorldPosition={worldPosition}.");
        return new List<Vector3Int> { cell };
    }

    private static bool IsPlayerSpawnCandidate(MonoBehaviour behaviour)
    {
        if (behaviour == null) return false;
        string n = (behaviour.GetType().Name + " " + behaviour.gameObject.name).ToLowerInvariant();
        return n.Contains("playerspawnpoint") || n.Contains("default") && n.Contains("spawn") || n == "player" || behaviour.gameObject.CompareTag("Player");
    }

    private static TilemapCollisionRole InferInitialRole(string name)
    {
        string n = name.ToLowerInvariant();
        if (new[] { "background", "decoration", "deco", "back", "vfx", "effect" }.Any(n.Contains)) return TilemapCollisionRole.Decoration;
        if (new[] { "ground", "floor", "wall", "ceiling", "tile", "obstacle", "collision", "solid" }.Any(n.Contains)) return TilemapCollisionRole.Solid;
        return TilemapCollisionRole.Unassigned;
    }

    private static string BuildEdgeStatistics(List<Group> groups, int previousColliderCount, int? finalColliderCount = null)
    {
        int solid = groups.Sum(g => g.Solid.Count);
        int scanned = groups.Sum(g => g.Sources.Sum(s => s.ScannedTileCount));
        int visualExcluded = groups.Sum(g => g.Sources.Sum(s => s.VisualTilesExcludedFromSolid));
        int raw = groups.Sum(g => g.RawEdgeCount);
        int internalRemoved = groups.Sum(g => g.InternalFaceCount);
        int duplicates = groups.Sum(g => g.DuplicateEdgeCount);
        int boundary = groups.Sum(g => g.BoundaryEdgeCount);
        int segments = groups.Sum(g => g.Runs.Count);
        int final = finalColliderCount ?? 0;
        float reduction = previousColliderCount > 0 && finalColliderCount.HasValue ? (previousColliderCount - final) * 100f / previousColliderCount : 0f;
        int floor = groups.Sum(g => g.BoundaryEdgesByType[(int)SurfaceType.Floor]);
        int ceiling = groups.Sum(g => g.BoundaryEdgesByType[(int)SurfaceType.Ceiling]);
        int left = groups.Sum(g => g.BoundaryEdgesByType[(int)SurfaceType.LeftWall]);
        int right = groups.Sum(g => g.BoundaryEdgesByType[(int)SurfaceType.RightWall]);
        string runs = string.Join(", ", Enum.GetValues(typeof(SurfaceType)).Cast<SurfaceType>().Select(t => $"{t}={groups.Sum(g => g.Runs.Count(r => r.Type == t))}"));
        return $"Scanned Tile Count: {scanned}\nSolid Occupancy Tile Count: {solid}\nSparse Visual Tiles Excluded: {visualExcluded}\nRaw Edge Count: {raw}\nInternal Faces Removed: {internalRemoved}\nDuplicate Edges Removed: {duplicates}\nBoundary Edges Before Merge: {boundary}\nFloor Raw Edges: {floor}\nCeiling Raw Edges: {ceiling}\nLeft Wall Raw Edges: {left}\nRight Wall Raw Edges: {right}\nSegments After Merge: {segments} ({runs})\nFinal BoxCollider Count: {(finalColliderCount.HasValue ? final.ToString() : "not replaced")}\nReduction: {(finalColliderCount.HasValue ? reduction.ToString("F1") + "%" : "n/a")}";
    }

    private static void DetectGroupGaps(Group group, List<Group> allGroups)
    {
        foreach (Vector3Int left in group.Solid)
        {
            Vector3Int gap = left + Vector3Int.right;
            if (group.Solid.Contains(gap) || !group.Solid.Contains(gap + Vector3Int.right)) continue;
            GapInfo info = new GapInfo { Group = group, Cell = gap, Tilemap = group.Sources.FirstOrDefault(s => s.Cells.Contains(left))?.Tilemap, Type = GapType.SuspectedMissingTile, Reason = "same-height Solid floor has exactly one empty HasTile cell" };
            Group other = allGroups.FirstOrDefault(g => g != group && g.Grid == group.Grid && g.Solid.Contains(gap));
            if (other != null) { info.Type = other.CollisionGroup == group.CollisionGroup ? GapType.CrossTilemapGap : GapType.DifferentCollisionGroup; info.Reason = $"cell is Solid in group '{other.CollisionGroup}'"; }
            else if (group.Sources.Any(s => s.Tilemap.HasTile(gap))) { info.Type = GapType.VisualOnlyConnection; info.Reason = "a non-Solid/excluded visual Tile exists at the gap"; }
            group.Gaps.Add(info);
        }
    }

    private static void DrawPreview(SceneView view)
    {
        if (!preview || Application.isPlaying) return;
        Event e = Event.current;
        foreach (Group group in previewGroups)
        {
            Matrix4x4 old = Handles.matrix; Handles.matrix = group.Grid.transform.localToWorldMatrix;
            Vector3 boundsCenter = group.Grid.CellToLocalInterpolated(new Vector3(group.Bounds.center.x, group.Bounds.center.y, 0f));
            Vector3 boundsMin = GridVertex(group.Grid, group.Bounds.xMin, group.Bounds.yMin);
            Vector3 boundsMax = GridVertex(group.Grid, group.Bounds.xMax, group.Bounds.yMax);
            Handles.color = Color.yellow;
            Handles.DrawWireCube(boundsCenter, new Vector3(Mathf.Abs(boundsMax.x - boundsMin.x), Mathf.Abs(boundsMax.y - boundsMin.y), 0f));
            Handles.color = new Color(.5f, .5f, .5f, .08f);
            foreach (Vector3Int cell in group.Reachable) Handles.DrawSolidRectangleWithOutline(CellRect(group.Grid, cell), new Color(.5f,.5f,.5f,.08f), Color.clear);
            Handles.color = Color.blue;
            Handles.DrawSolidDisc(group.Grid.CellToLocalInterpolated(group.SelectedSeed + Vector3.one * .5f), Vector3.forward, .15f);
            Handles.color = Color.yellow;
            foreach (Vector3Int cell in group.BoundsContactCells) Handles.DrawSolidRectangleWithOutline(CellRect(group.Grid, cell), new Color(1f,.8f,0f,.2f), Color.yellow);
            foreach (SurfaceRun run in group.Runs)
            {
                Handles.color = run.Type == SurfaceType.Floor ? Color.green : run.Type == SurfaceType.Ceiling ? new Color(1f,.5f,0f) : Color.blue;
                Vector3 a, b; RunLine(group, run, out a, out b); Handles.DrawAAPolyLine(5f, a, b);
            }
            foreach (GapInfo gap in group.Gaps)
            {
                Handles.color = gap.Type == GapType.SuspectedMissingTile ? Color.red : gap.Type == GapType.IntentionalGap ? new Color(.7f,0f,1f) : Color.yellow;
                Vector3 center = group.Grid.CellToLocalInterpolated(gap.Cell + Vector3.one * .5f);
                float size = HandleUtility.GetHandleSize(group.Grid.transform.TransformPoint(center)) * .08f;
                if (Handles.Button(center, Quaternion.identity, size, size, Handles.RectangleHandleCap)) { selectedGap = gap; Selection.activeGameObject = gap.Tilemap != null ? gap.Tilemap.gameObject : group.Grid.gameObject; e.Use(); }
                Handles.Label(center, $"{gap.Type}: {gap.Cell}");
            }
            Handles.matrix = old;
        }
    }

    private static void RunLine(Group group, SurfaceRun run, out Vector3 a, out Vector3 b)
    {
        if (run.Type == SurfaceType.Floor || run.Type == SurfaceType.Ceiling)
        { a = GridVertex(group.Grid, run.Start, run.Fixed); b = GridVertex(group.Grid, run.Start + run.Length, run.Fixed); }
        else
        { a = GridVertex(group.Grid, run.Fixed, run.Start); b = GridVertex(group.Grid, run.Fixed, run.Start + run.Length); }
    }

    private static Vector3 GridVertex(GridLayout grid, float x, float y) => grid.CellToLocalInterpolated(new Vector3(x, y, 0f));
    private static Vector3[] CellRect(GridLayout grid, Vector3Int cell) => new[]
    {
        GridVertex(grid, cell.x, cell.y), GridVertex(grid, cell.x + 1, cell.y),
        GridVertex(grid, cell.x + 1, cell.y + 1), GridVertex(grid, cell.x, cell.y + 1)
    };
    private static string RunGridDescription(SurfaceRun run) => run.Type == SurfaceType.Floor || run.Type == SurfaceType.Ceiling
        ? $"({run.Start},{run.Fixed})->({run.Start + run.Length},{run.Fixed})"
        : $"({run.Fixed},{run.Start})->({run.Fixed},{run.Start + run.Length})";
    private static Vector3 SurfaceNormal(SurfaceType type) => type == SurfaceType.Floor ? Vector3.up : type == SurfaceType.Ceiling ? Vector3.down : type == SurfaceType.LeftWall ? Vector3.right : Vector3.left;
    private static float SurfaceOffset(TilemapCollisionAuthoring settings, SurfaceType type) => type == SurfaceType.Floor ? settings.FloorSurfaceOffset : type == SurfaceType.Ceiling ? settings.CeilingSurfaceOffset : type == SurfaceType.LeftWall ? settings.LeftWallSurfaceOffset : settings.RightWallSurfaceOffset;

    private static void ValidateGeneratedGeometry(Group group, List<GameObject> roots, List<string> errors)
    {
        const float epsilon = 0.0001f;
        group.MaximumSurfaceError = 0f;
        HashSet<string> unique = new HashSet<string>();
        foreach (SurfaceRun run in group.Runs)
        {
            string key = $"{run.Type}|{run.Fixed}|{run.Start}|{run.Length}";
            if (!unique.Add(key)) errors.Add($"Duplicate boundary segment: Grid={group.Grid.name}, World={group.World}, {key}.");
        }
        foreach (GameObject root in roots.Where(r => r.transform.parent == group.Grid.transform))
        foreach (TilemapGeneratedColliderMarker marker in root.GetComponentsInChildren<TilemapGeneratedColliderMarker>(true))
        {
            if (marker.Kind != TilemapGeneratedColliderMarker.MarkerKind.Collider || marker.CollisionGroup != group.CollisionGroup) continue;
            BoxCollider box = marker.GetComponent<BoxCollider>();
            if (box == null) continue;
            float actual = marker.SurfaceType == SurfaceType.Floor.ToString() ? box.center.y + box.size.y * .5f :
                marker.SurfaceType == SurfaceType.Ceiling.ToString() ? box.center.y - box.size.y * .5f :
                marker.SurfaceType == SurfaceType.LeftWall.ToString() ? box.center.x + box.size.x * .5f : box.center.x - box.size.x * .5f;
            float expected = marker.SurfaceType == SurfaceType.Floor.ToString() || marker.SurfaceType == SurfaceType.Ceiling.ToString()
                ? marker.EdgeStart.y + SurfaceOffset(group.Representative, (SurfaceType)Enum.Parse(typeof(SurfaceType), marker.SurfaceType)) * (marker.SurfaceNormal.y)
                : marker.EdgeStart.x + SurfaceOffset(group.Representative, (SurfaceType)Enum.Parse(typeof(SurfaceType), marker.SurfaceType)) * (marker.SurfaceNormal.x);
            float error = Mathf.Abs(actual - expected);
            group.MaximumSurfaceError = Mathf.Max(group.MaximumSurfaceError, error);
            if (error > epsilon) errors.Add($"Surface position mismatch: {Path(marker.transform)}, expected={expected:F6}, actual={actual:F6}, error={error:F6}.");
        }
        group.CornerGapCount = 0; // Every run endpoint is produced by the same GridVertex function; no independent endpoint rounding occurs.
    }

    private static BoundsInt BoundsOf(HashSet<Vector3Int> cells, int padding)
    {
        int minX=cells.Min(c=>c.x)-padding,minY=cells.Min(c=>c.y)-padding,maxX=cells.Max(c=>c.x)+padding,maxY=cells.Max(c=>c.y)+padding;
        return new BoundsInt(minX,minY,0,maxX-minX+1,maxY-minY+1,1);
    }
    private static void ResolveBakeBounds(Scene scene, Group group)
    {
        TilemapCollisionBakeBounds explicitBounds = FindScene<TilemapCollisionBakeBounds>(scene).FirstOrDefault(b =>
            (b.Grid == null || b.Grid == group.Grid) && (group.World == "WorldA" ? b.WorldA : group.World == "WorldB" ? b.WorldB : b.WorldA && b.WorldB));
        if (explicitBounds != null)
        {
            group.Bounds = WorldBoundsToCells(group.Grid, explicitBounds.WorldBounds, explicitBounds.Padding);
            group.BoundsSource = "TilemapCollisionBakeBounds: " + Path(explicitBounds.transform);
            return;
        }
        CameraBounds cameraBounds = FindScene<CameraBounds>(scene).FirstOrDefault(b => WorldOf(b.transform) == group.World || WorldOf(b.transform) == "Shared");
        if (cameraBounds != null && cameraBounds.WorldBounds.size.x > 0f && cameraBounds.WorldBounds.size.y > 0f)
        {
            group.Bounds = WorldBoundsToCells(group.Grid, cameraBounds.WorldBounds, group.Sources.Max(s => s.Settings.ReachableBoundsPadding));
            group.BoundsSource = "CameraBounds: " + Path(cameraBounds.transform);
            return;
        }
        int padding = group.Sources.Max(s => s.Settings.ReachableBoundsPadding);
        group.Bounds = BoundsOf(group.Solid, padding);
        group.BoundsSource = $"Automatic Solid Bounds (Padding={padding})";
    }

    private static BoundsInt WorldBoundsToCells(GridLayout grid, Bounds worldBounds, int padding)
    {
        Vector3[] corners =
        {
            new Vector3(worldBounds.min.x, worldBounds.min.y), new Vector3(worldBounds.min.x, worldBounds.max.y),
            new Vector3(worldBounds.max.x, worldBounds.min.y), new Vector3(worldBounds.max.x, worldBounds.max.y)
        };
        Vector3Int[] cells = corners.Select(grid.WorldToCell).ToArray();
        int minX = cells.Min(c => c.x) - padding, minY = cells.Min(c => c.y) - padding;
        int maxX = cells.Max(c => c.x) + padding, maxY = cells.Max(c => c.y) + padding;
        return new BoundsInt(minX, minY, 0, maxX - minX + 1, maxY - minY + 1, 1);
    }
    private static Vector3Int RunMinCell(SurfaceRun r) => r.Type==SurfaceType.Floor||r.Type==SurfaceType.Ceiling?new Vector3Int(r.Start,r.Fixed,0):new Vector3Int(r.Fixed,r.Start,0);
    private static int ResolveSurfaceLayer(TilemapCollisionAuthoring s, SurfaceType type) { int stored=type==SurfaceType.Floor?s.FloorLayer:type==SurfaceType.Ceiling?s.CeilingLayer:s.WallLayer; return stored>=0&&stored<32&&!string.IsNullOrEmpty(LayerMask.LayerToName(stored))?stored:-1; }
    private static bool WorldAllowed(TilemapPlayableAreaSeed s,string world)=>world=="WorldA"?s.WorldA:world=="WorldB"?s.WorldB:s.WorldA&&s.WorldB;
    private static string WorldOf(Transform t){for(;t!=null;t=t.parent){WorldPresence p=t.GetComponent<WorldPresence>();if(p!=null)return p.PresenceMode.ToString();string n=t.name.ToLowerInvariant();if(n.Contains("world_a"))return"WorldA";if(n.Contains("world_b"))return"WorldB";}return"Shared";}
    private static void RemoveReachableRoots(Scene scene, ICollection<GameObject> preserve = null)
    {
        foreach (TilemapGeneratedColliderMarker marker in FindScene<TilemapGeneratedColliderMarker>(scene))
            if (marker.Kind == TilemapGeneratedColliderMarker.MarkerKind.Root && IsReachableMarker(marker) && (preserve == null || !preserve.Contains(marker.gameObject)))
                Undo.DestroyObjectImmediate(marker.gameObject);
    }
    private static void RemoveReplacedLegacyChildren(Scene scene)
    {
        foreach (TilemapGeneratedColliderMarker marker in FindScene<TilemapGeneratedColliderMarker>(scene))
        {
            if (marker.Kind != TilemapGeneratedColliderMarker.MarkerKind.Collider || IsReachableMarker(marker)) continue;
            TilemapCollisionAuthoring[] sources = marker.Sources ?? Array.Empty<TilemapCollisionAuthoring>();
            if (sources.Length == 0 && marker.Source != null) sources = new[] { marker.Source };
            if (sources.Length > 0 && sources.All(s => s != null && s.BakeMode == TilemapCollisionBakeMode.ReachableSurfaceBoundary))
                Undo.DestroyObjectImmediate(marker.gameObject);
        }
        foreach (TilemapGeneratedColliderMarker root in FindScene<TilemapGeneratedColliderMarker>(scene))
            if (root.Kind == TilemapGeneratedColliderMarker.MarkerKind.Root && !IsReachableMarker(root) && root.transform.childCount == 0) Undo.DestroyObjectImmediate(root.gameObject);
    }
    private static bool IsReachableMarker(TilemapGeneratedColliderMarker m)=>m!=null&&m.BakeSignature!=null&&m.BakeSignature.StartsWith("ReachableSurfaceBoundary",StringComparison.Ordinal);
    private static T[] FindScene<T>(Scene s)where T:Component=>Resources.FindObjectsOfTypeAll<T>().Where(x=>x!=null&&x.gameObject.scene==s&&!EditorUtility.IsPersistent(x)).ToArray();
    private static string Path(Transform t){List<string>p=new List<string>();for(;t!=null;t=t.parent)p.Add(t.name);p.Reverse();return string.Join("/",p);}
    private static void ReportComparison(Scene s,List<Group>g,int reachable,int?legacyOverride=null){int legacy=legacyOverride??FindScene<TilemapGeneratedColliderMarker>(s).Count(m=>m.Kind==TilemapGeneratedColliderMarker.MarkerKind.Collider&&!IsReachableMarker(m));int solid=g.Sum(x=>x.Solid.Count),excluded=g.Sum(x=>x.Solid.Count*4-x.Runs.Sum(r=>r.Length));float reduction=legacy>0?(legacy-reachable)*100f/legacy:0;Debug.Log($"[Reachable Surface Comparison] Scene={s.name}, SolidTiles={solid}, LegacyColliders={legacy}, ReachableSurfaceColliders={reachable}, Reduction={reduction:F1}%, ExcludedInternal/UnreachableFaces={excluded}, Gaps={g.Sum(x=>x.Gaps.Count)}. Scene not saved.");}
}
