#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
public static class BossProtoSetupUtility
{
    private const string ArtRoot = "Assets/_Project/Art/Enemies/BossProto";
    private const string AnimationRoot = "Assets/_Project/Animations/Enemies/BossProto";
    private const string PrefabPath = "Assets/_Project/Prefabs/Enemies/boss_proto.prefab";
    private const string ControllerPath = AnimationRoot + "/BossProto.controller";

    private readonly struct ClipDefinition
    {
        public ClipDefinition(string name, string filePrefix, float frameRate, bool loop)
        {
            Name = name;
            FilePrefix = filePrefix;
            FrameRate = frameRate;
            Loop = loop;
        }

        public string Name { get; }
        public string FilePrefix { get; }
        public float FrameRate { get; }
        public bool Loop { get; }
    }

    private static readonly ClipDefinition[] ClipDefinitions =
    {
        new ClipDefinition("Idle", "IDLE_", 1f, true),
        new ClipDefinition("EnergyBoom", "Energy _Boom_", 10f, false),
        new ClipDefinition("HitGround", "Hit_Ground_", 10f, false),
        new ClipDefinition("Lazer", "Lazer_", 10f, false),
        new ClipDefinition("Swing", "Swing_", 10f, false),
    };

    static BossProtoSetupUtility()
    {
        EditorApplication.delayCall += BuildIfNeeded;
    }

    [MenuItem("Tools/Boss Proto/Rebuild Visual Prototype")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(ArtRoot))
        {
            Debug.LogError($"[BossProtoSetup] Sprite folder is missing: {ArtRoot}");
            return;
        }

        EnsureFolder("Assets/_Project/Animations", "Enemies");
        EnsureFolder("Assets/_Project/Animations/Enemies", "BossProto");
        ConfigureSpriteImporters();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        var clips = new Dictionary<string, AnimationClip>();
        foreach (ClipDefinition definition in ClipDefinitions)
        {
            Sprite[] frames = LoadFrames(definition.FilePrefix);
            if (frames.Length == 0)
            {
                Debug.LogWarning($"[BossProtoSetup] No frames found for {definition.Name}; no empty clip was created.");
                continue;
            }

            clips.Add(definition.Name, CreateOrReplaceClip(definition, frames));
        }

        if (!clips.TryGetValue("Idle", out AnimationClip idleClip))
        {
            Debug.LogError("[BossProtoSetup] IDLE_01.png is required as the default visual.");
            return;
        }

        AnimatorController controller = CreateOrReplaceController(clips, idleClip);
        CreateOrReplacePrefab(controller, LoadFrames("IDLE_")[0]);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[BossProtoSetup] Created visual-only boss_proto prefab with 5 animation states and no gameplay components.");
    }

    [MenuItem("Tools/Boss Proto/Validate Pattern Shuffle")]
    public static void ValidatePatternShuffle()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            throw new InvalidOperationException($"Missing prefab: {PrefabPath}");
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        try
        {
            BossProtoPatternPlayer player = instance.GetComponent<BossProtoPatternPlayer>();
            if (player == null)
            {
                throw new InvalidOperationException("boss_proto is missing BossProtoPatternPlayer.");
            }

            var cycles = new List<string>(3);
            int previousLast = 0;
            var serializedPlayer = new SerializedObject(player);
            SerializedProperty lastPlayed = serializedPlayer.FindProperty("lastPlayedPattern");
            for (int cycle = 0; cycle < 3; cycle++)
            {
                lastPlayed.intValue = previousLast;
                serializedPlayer.ApplyModifiedPropertiesWithoutUndo();
                player.ShufflePatterns();
                int[] order = player.CurrentCycleOrder.Split(',').Select(value => int.Parse(value.Trim())).ToArray();
                if (order.Length != 4 || order.Distinct().Count() != 4 || order.Any(value => value < 1 || value > 4))
                {
                    throw new InvalidOperationException($"Invalid pattern cycle: {player.CurrentCycleOrder}");
                }

                if (previousLast != 0 && order[0] == previousLast)
                {
                    throw new InvalidOperationException($"Cycle boundary duplicate: {previousLast} -> {order[0]}");
                }

                cycles.Add(player.CurrentCycleOrder);
                previousLast = order[3];
            }

            Debug.Log($"[BossProtoPatternValidation] PASS | Cycle1: {cycles[0]} | Cycle2: {cycles[1]} | Cycle3: {cycles[2]}");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    private static void BuildIfNeeded()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (AssetDatabase.IsValidFolder(ArtRoot)
            && (prefab == null || prefab.GetComponent<BossProtoPatternPlayer>() == null))
        {
            Build();
        }
    }

    private static void ConfigureSpriteImporters()
    {
        foreach (string path in Directory.GetFiles(ArtRoot, "*.png", SearchOption.TopDirectoryOnly).Select(ToAssetPath))
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                continue;
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

    private static Sprite[] LoadFrames(string filePrefix)
    {
        return Directory.GetFiles(ArtRoot, "*.png", SearchOption.TopDirectoryOnly)
            .Where(path => Path.GetFileName(path).StartsWith(filePrefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => ExtractTrailingNumber(Path.GetFileNameWithoutExtension(path)))
            .Select(path => AssetDatabase.LoadAssetAtPath<Sprite>(ToAssetPath(path)))
            .Where(sprite => sprite != null)
            .ToArray();
    }

    private static AnimationClip CreateOrReplaceClip(ClipDefinition definition, IReadOnlyList<Sprite> frames)
    {
        string path = $"{AnimationRoot}/BossProto_{definition.Name}.anim";
        AssetDatabase.DeleteAsset(path);

        var clip = new AnimationClip { name = $"BossProto_{definition.Name}", frameRate = definition.FrameRate };
        var keyframes = new ObjectReferenceKeyframe[frames.Count];
        for (int i = 0; i < frames.Count; i++)
        {
            keyframes[i] = new ObjectReferenceKeyframe
            {
                time = i / definition.FrameRate,
                value = frames[i],
            };
        }

        AnimationUtility.SetObjectReferenceCurve(
            clip,
            EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite"),
            keyframes);
        AnimationUtility.SetAnimationClipSettings(clip, new AnimationClipSettings { loopTime = definition.Loop });
        if (!definition.Loop)
        {
            AnimationUtility.SetAnimationEvents(clip, new[]
            {
                new AnimationEvent
                {
                    time = frames.Count / definition.FrameRate,
                    functionName = nameof(BossProtoPatternPlayer.OnPatternAnimationFinished),
                },
            });
        }
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    private static AnimatorController CreateOrReplaceController(IReadOnlyDictionary<string, AnimationClip> clips, AnimationClip idleClip)
    {
        AssetDatabase.DeleteAsset(ControllerPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        stateMachine.name = "BossProto States";

        AnimatorState idleState = null;
        int index = 0;
        foreach (ClipDefinition definition in ClipDefinitions)
        {
            if (!clips.TryGetValue(definition.Name, out AnimationClip clip))
            {
                continue;
            }

            string stateName = definition.Name == "Idle" ? "Idle" : $"Pattern{index}";
            AnimatorState state = stateMachine.AddState(stateName, new Vector3(260f + (index % 2) * 240f, 80f + (index / 2) * 90f));
            state.motion = clip;
            state.writeDefaultValues = true;
            if (clip == idleClip)
            {
                idleState = state;
            }

            index++;
        }

        stateMachine.defaultState = idleState;
        return controller;
    }

    private static void CreateOrReplacePrefab(RuntimeAnimatorController controller, Sprite defaultSprite)
    {
        var root = new GameObject("boss_proto");
        try
        {
            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = defaultSprite;
            Animator animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            BossProtoPatternPlayer patternPlayer = root.AddComponent<BossProtoPatternPlayer>();
            patternPlayer.ConfigureClips(
                AssetDatabase.LoadAssetAtPath<AnimationClip>($"{AnimationRoot}/BossProto_Idle.anim"),
                AssetDatabase.LoadAssetAtPath<AnimationClip>($"{AnimationRoot}/BossProto_EnergyBoom.anim"),
                AssetDatabase.LoadAssetAtPath<AnimationClip>($"{AnimationRoot}/BossProto_HitGround.anim"),
                AssetDatabase.LoadAssetAtPath<AnimationClip>($"{AnimationRoot}/BossProto_Lazer.anim"),
                AssetDatabase.LoadAssetAtPath<AnimationClip>($"{AnimationRoot}/BossProto_Swing.anim"));
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static int ExtractTrailingNumber(string name)
    {
        int underscore = name.LastIndexOf('_');
        return underscore >= 0 && int.TryParse(name.Substring(underscore + 1), out int number) ? number : 0;
    }

    private static string ToAssetPath(string path)
    {
        return path.Replace('\\', '/');
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }
}
#endif
