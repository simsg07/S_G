using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

[InitializeOnLoad]
public static class TilePaletteSetupUtility
{
    private const string AutoRunSessionKey = "TilePaletteSetupUtility.VisualOnly.AutoRunComplete.v1";
    private const string GroundPalettePath = "Assets/_Project/Tiles/Ground/Palette_Ground.prefab";
    private const int PixelsPerUnit = 32;
    private const int TilePixelSize = 32;

    static TilePaletteSetupUtility()
    {
        EditorApplication.delayCall += AutoRunIfNeeded;
    }

    [MenuItem("_Project/Map/Setup Tile Palette Assets")]
    public static void SetupTilePaletteAssets()
    {
        CreateFolders();

        CreateTempSprite("Assets/_Project/Art/Map/TempTiles/TEMP_Tile_Ground_Black.png", new Color32(16, 16, 16, 255));
        CreateTempSprite("Assets/_Project/Art/Map/TempTiles/TEMP_Tile_Wall_DarkGray.png", new Color32(48, 48, 48, 255));
        CreateTempSprite("Assets/_Project/Art/Map/TempTiles/TEMP_Tile_Background_Dark.png", new Color32(28, 30, 36, 255));
        CreateTempSprite("Assets/_Project/Art/Map/TempTiles/TEMP_Tile_Decoration_Gray.png", new Color32(112, 112, 112, 255));

        AssetDatabase.Refresh();

        CreateTile(
            "Assets/_Project/Tiles/Ground/TILE_Ground_Black.asset",
            "Assets/_Project/Art/Map/TempTiles/TEMP_Tile_Ground_Black.png",
            Tile.ColliderType.None);
        CreateTile(
            "Assets/_Project/Tiles/Wall/TILE_Wall_DarkGray.asset",
            "Assets/_Project/Art/Map/TempTiles/TEMP_Tile_Wall_DarkGray.png",
            Tile.ColliderType.None);
        CreateTile(
            "Assets/_Project/Tiles/Background/TILE_Background_Dark.asset",
            "Assets/_Project/Art/Map/TempTiles/TEMP_Tile_Background_Dark.png",
            Tile.ColliderType.None);
        CreateTile(
            "Assets/_Project/Tiles/Decoration/TILE_Decoration_Gray.asset",
            "Assets/_Project/Art/Map/TempTiles/TEMP_Tile_Decoration_Gray.png",
            Tile.ColliderType.None);

        PopulateGroundPalette();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[TilePaletteSetupUtility] Visual-only Tile Palette assets are ready. Tile Palette is for painting images only.");
    }

    [MenuItem("_Project/Map/Setup Visual Tilemaps In Current Scene")]
    public static void SetupVisualTilemapsInCurrentScene()
    {
        GameObject grid = GameObject.Find("Grid_Map");
        if (grid == null)
        {
            grid = new GameObject("Grid_Map");
            grid.AddComponent<Grid>();
        }

        EnsureSceneTilemap(grid.transform, "Tilemap_Background");
        EnsureSceneTilemap(grid.transform, "Tilemap_GroundVisual");
        EnsureSceneTilemap(grid.transform, "Tilemap_WallVisual");
        EnsureSceneTilemap(grid.transform, "Tilemap_Decoration");

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[TilePaletteSetupUtility] Visual Tilemaps are ready in the active scene.");
        Debug.Log("[TilePaletteSetupUtility] Created/checked: Tilemap_Background, Tilemap_GroundVisual, Tilemap_WallVisual, Tilemap_Decoration.");
        Debug.Log("[TilePaletteSetupUtility] Tile Palette is visual-only. Use Floor_Collision / Wall_Tile / Block_Tile prefabs for gameplay collision and detection.");
    }

    private static void AutoRunIfNeeded()
    {
        if (SessionState.GetBool(AutoRunSessionKey, false))
        {
            return;
        }

        SessionState.SetBool(AutoRunSessionKey, true);
        SetupTilePaletteAssets();
    }

    private static void CreateFolders()
    {
        EnsureFolder("Assets/_Project/Art/Map/TempTiles");
        EnsureFolder("Assets/_Project/Tiles/Ground");
        EnsureFolder("Assets/_Project/Tiles/Wall");
        EnsureFolder("Assets/_Project/Tiles/Background");
        EnsureFolder("Assets/_Project/Tiles/Decoration");
        EnsureFolder("Assets/_Project/TilePalettes");
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string parent = Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
        string folderName = Path.GetFileName(folderPath);
        if (!string.IsNullOrEmpty(parent))
        {
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }

    private static void CreateTempSprite(string assetPath, Color32 color)
    {
        string fullPath = Path.GetFullPath(assetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

        if (!File.Exists(fullPath))
        {
            Texture2D texture = new Texture2D(TilePixelSize, TilePixelSize, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[TilePixelSize * TilePixelSize];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(fullPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
    }

    private static void CreateTile(string tilePath, string spritePath, Tile.ColliderType colliderType)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (sprite == null)
        {
            Debug.LogWarning($"[TilePaletteSetupUtility] Sprite not found: {spritePath}");
            return;
        }

        Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(tile, tilePath);
        }

        tile.sprite = sprite;
        tile.color = Color.white;
        tile.transform = Matrix4x4.identity;
        tile.gameObject = null;
        tile.flags = TileFlags.LockColor;
        tile.colliderType = colliderType;
        EditorUtility.SetDirty(tile);
    }

    private static void PopulateGroundPalette()
    {
        PopulatePalette(
            GroundPalettePath,
            "Palette_Ground",
            new[]
            {
                "Assets/_Project/Tiles/Ground/TILE_Ground_Black.asset",
                "Assets/_Project/Tiles/Wall/TILE_Wall_DarkGray.asset",
                "Assets/_Project/Tiles/Background/TILE_Background_Dark.asset",
                "Assets/_Project/Tiles/Decoration/TILE_Decoration_Gray.asset"
            });
    }

    private static void PopulatePalette(string palettePath, string paletteName, string[] tilePaths)
    {
        Tilemap paletteTilemap = null;
        GameObject paletteRoot;

        if (File.Exists(palettePath))
        {
            paletteRoot = PrefabUtility.LoadPrefabContents(palettePath);
        }
        else
        {
            paletteRoot = new GameObject(paletteName);
            paletteRoot.AddComponent<Grid>();

            GameObject layer = new GameObject("Layer1");
            layer.transform.SetParent(paletteRoot.transform);
            paletteTilemap = layer.AddComponent<Tilemap>();
            layer.AddComponent<TilemapRenderer>();
        }

        if (paletteTilemap == null)
        {
            paletteTilemap = paletteRoot.GetComponentInChildren<Tilemap>();
        }

        if (paletteTilemap == null)
        {
            GameObject layer = new GameObject("Layer1");
            layer.transform.SetParent(paletteRoot.transform);
            paletteTilemap = layer.AddComponent<Tilemap>();
            layer.AddComponent<TilemapRenderer>();
        }

        RemoveNonPaletteChildren(paletteRoot, paletteTilemap.gameObject);

        paletteTilemap.ClearAllTiles();
        for (int i = 0; i < tilePaths.Length; i++)
        {
            SetPaletteTile(paletteTilemap, i, tilePaths[i]);
        }

        paletteTilemap.CompressBounds();
        EditorUtility.SetDirty(paletteTilemap);
        PrefabUtility.SaveAsPrefabAsset(paletteRoot, palettePath);
        PrefabUtility.UnloadPrefabContents(paletteRoot);
    }

    private static void RemoveNonPaletteChildren(GameObject paletteRoot, GameObject paletteLayer)
    {
        if (paletteRoot == null || paletteLayer == null)
        {
            return;
        }

        for (int i = paletteRoot.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = paletteRoot.transform.GetChild(i);
            if (child == null || child.gameObject == paletteLayer)
            {
                continue;
            }

            Object.DestroyImmediate(child.gameObject);
        }
    }

    private static void SetPaletteTile(Tilemap paletteTilemap, int x, string tilePath)
    {
        Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
        if (tile == null)
        {
            Debug.LogWarning($"[TilePaletteSetupUtility] Tile not found for palette: {tilePath}");
            return;
        }

        paletteTilemap.SetTile(new Vector3Int(x, 0, 0), tile);
        paletteTilemap.SetColor(new Vector3Int(x, 0, 0), Color.white);
        paletteTilemap.SetTransformMatrix(new Vector3Int(x, 0, 0), Matrix4x4.identity);
    }

    private static Tilemap EnsureSceneTilemap(Transform gridRoot, string tilemapName)
    {
        Transform existing = gridRoot.Find(tilemapName);
        GameObject tilemapObject = existing != null ? existing.gameObject : new GameObject(tilemapName);
        tilemapObject.transform.SetParent(gridRoot);
        tilemapObject.transform.localPosition = Vector3.zero;
        tilemapObject.transform.localRotation = Quaternion.identity;
        tilemapObject.transform.localScale = Vector3.one;

        Tilemap tilemap = tilemapObject.GetComponent<Tilemap>();
        if (tilemap == null)
        {
            tilemap = tilemapObject.AddComponent<Tilemap>();
        }

        if (tilemapObject.GetComponent<TilemapRenderer>() == null)
        {
            tilemapObject.AddComponent<TilemapRenderer>();
        }

        return tilemap;
    }
}
