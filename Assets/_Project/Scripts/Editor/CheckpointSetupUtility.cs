#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CheckpointSetupUtility
{
    private const string InactiveSpritePath = "Assets/_Project/Art/Objects/Checkpoint/charger_02.png";
    private const string ActiveSpritePath = "Assets/_Project/Art/Objects/Checkpoint/charger_01.png";
    private const string PrefabPath = "Assets/_Project/Prefabs/Objects/Checkpoint/Checkpoint.prefab";

    static CheckpointSetupUtility()
    {
        EditorApplication.delayCall += BuildIfNeeded;
    }

    [MenuItem("Tools/Checkpoint/Rebuild Visual Interaction Prefab")]
    public static void Build()
    {
        ConfigureSprite(InactiveSpritePath);
        ConfigureSprite(ActiveSpritePath);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        Sprite inactiveSprite = AssetDatabase.LoadAssetAtPath<Sprite>(InactiveSpritePath);
        Sprite activeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ActiveSpritePath);
        if (inactiveSprite == null || activeSprite == null)
        {
            Debug.LogError("[CheckpointSetup] charger_01 or charger_02 could not be imported as a Sprite.");
            return;
        }

        var root = new GameObject("Checkpoint");
        try
        {
            Checkpoint3D checkpoint = root.AddComponent<Checkpoint3D>();

            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = inactiveSprite;

            var triggerObject = new GameObject("InteractionTrigger");
            triggerObject.transform.SetParent(root.transform, false);
            var trigger = triggerObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            triggerObject.AddComponent<CheckpointInteractionTrigger3D>();
            trigger.center = new Vector3(0f, 0.2f, 0f);
            trigger.size = new Vector3(4.25f, 4.5f, 1.5f);

            var spawnPosition = new GameObject("SpawnPosition");
            spawnPosition.transform.SetParent(root.transform, false);
            spawnPosition.transform.localPosition = new Vector3(0f, -1.9f, 0f);

            var serialized = new SerializedObject(checkpoint);
            serialized.FindProperty("checkpointId").stringValue = "SceneName_CP_01";
            serialized.FindProperty("interactionTrigger").objectReferenceValue = trigger;
            serialized.FindProperty("linkedSpawnPoint").objectReferenceValue = spawnPosition.transform;
            serialized.FindProperty("spriteRenderer").objectReferenceValue = renderer;
            serialized.FindProperty("inactiveSprite").objectReferenceValue = inactiveSprite;
            serialized.FindProperty("activeSprite").objectReferenceValue = activeSprite;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[CheckpointSetup] Created Checkpoint visual/F-interaction prefab. Respawn and save systems were not changed.");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void BuildIfNeeded()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            Build();
        }
    }

    private static void ConfigureSprite(string path)
    {
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 256f;
        importer.spritePivot = new Vector2(0.5f, 0.5f);
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }
}
#endif
