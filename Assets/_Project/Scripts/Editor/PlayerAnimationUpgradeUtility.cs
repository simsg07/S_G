using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class PlayerAnimationUpgradeUtility
{
    private const string Art = "Assets/_Project/Art/Player/New/";
    private const string Anim = "Assets/_Project/Animations/Player/";
    private const string PrefabPath = "Assets/_Project/Prefabs/Player/Player.prefab";
    private const string AutoApplyKey = "Codex.PlayerJumpAnimationApplied.v2";

    [MenuItem("Tools/Project/Apply New Player Animation Set")]
    public static void ApplyFromMenu()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ConfigureTextures();

        AnimationClip idle = CreateClip("Player_Idle", Names("Player_Idle_", 1, 9, true), 9f, true);
        AnimationClip run = CreateClip("Player_Run", RunNames(), 11f, true);
        AnimationClip lookUp = CreateClip("Player_LookUp", Names("Player_Idle_Look Up_", 1, 9, true), 9f, true);
        AnimationClip lookDown = CreateClip("Player_LookDown", Names("Player_Look Down_", 1, 9, true), 9f, true);
        AnimationClip jump = CreateClip("Player_Jump", Names("Player_Jump_", 1, 11, true), 11f, false);

        AnimatorController controller = ConfigureController(idle, run, jump, lookUp, lookDown);
        ConfigurePrefab(controller, FirstSprite("Player_Idle_01.png"));
        AssetDatabase.SaveAssets();
        Debug.Log("[Player Animation] Player_Jump 11프레임을 임시 Jump 상태에 적용했습니다.");
    }

    [InitializeOnLoadMethod]
    private static void ApplyOnceAfterCompile()
    {
        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(Anim + "Player_Jump.anim") != null) return;
        if (SessionState.GetBool(AutoApplyKey, false)) return;
        SessionState.SetBool(AutoApplyKey, true);
        EditorApplication.delayCall += ApplyFromMenu;
    }

    private static void ConfigureTextures()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Art.TrimEnd('/') }))
        {
            TextureImporter importer = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter;
            if (importer == null) continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 340f;
            importer.spritePivot = new Vector2(0.5f, 0.5f);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }

    private static AnimationClip CreateClip(string name, string[] files, float sampleRate, bool loop)
    {
        string path = Anim + name + ".anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip { name = name };
            AssetDatabase.CreateAsset(clip, path);
        }

        var keys = new List<ObjectReferenceKeyframe>();
        for (int i = 0; i < files.Length; i++)
        {
            Sprite sprite = FirstSprite(files[i]);
            if (sprite == null) throw new InvalidOperationException("Sprite import failed: " + files[i]);
            keys.Add(new ObjectReferenceKeyframe { time = i / sampleRate, value = sprite });
        }

        clip.frameRate = sampleRate;
        AnimationUtility.SetObjectReferenceCurve(clip, new EditorCurveBinding
        {
            path = string.Empty,
            type = typeof(SpriteRenderer),
            propertyName = "m_Sprite"
        }, keys.ToArray());
        SerializedObject data = new SerializedObject(clip);
        SerializedProperty loopProperty = data.FindProperty("m_AnimationClipSettings.m_LoopTime");
        if (loopProperty != null) loopProperty.boolValue = loop;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static AnimatorController ConfigureController(AnimationClip idle, AnimationClip run, AnimationClip jump, AnimationClip lookUp, AnimationClip lookDown)
    {
        string path = Anim + "PlayerAnimator.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);

        while (controller.parameters.Length > 0) controller.RemoveParameter(0);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("YVelocity", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsDead", AnimatorControllerParameterType.Bool);
        controller.AddParameter("State", AnimatorControllerParameterType.Int);

        AnimatorStateMachine machine = new AnimatorStateMachine { name = "Base Layer" };
        AssetDatabase.AddObjectToAsset(machine, controller);
        string[] names = { "Idle", "Run", "Jump", "Fall", "LookUp", "LookDown", "Dead" };
        Motion[] motions = { idle, run, jump, null, lookUp, lookDown, null };
        for (int i = 0; i < names.Length; i++)
        {
            AnimatorState state = machine.AddState(names[i], new Vector3(260f + (i % 4) * 210f, 80f + (i / 4) * 140f));
            state.motion = motions[i];
            AnimatorStateTransition transition = machine.AddAnyStateTransition(state);
            transition.hasExitTime = false;
            transition.duration = 0f;
            transition.canTransitionToSelf = false;
            transition.AddCondition(AnimatorConditionMode.Equals, i, "State");
            if (i == 0) machine.defaultState = state;
        }

        controller.layers = new[]
        {
            new AnimatorControllerLayer { name = "Base Layer", defaultWeight = 1f, stateMachine = machine }
        };
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void ConfigurePrefab(RuntimeAnimatorController controller, Sprite idleSprite)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform visual = root.transform.Find("Visual");
            if (visual == null) visual = root.transform.Find("Player Attack Animation");
            if (visual == null) throw new InvalidOperationException("Player visual child is missing.");
            visual.name = "Visual";

            SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = visual.gameObject.AddComponent<SpriteRenderer>();
            Animator animator = visual.GetComponent<Animator>();
            if (animator == null) animator = visual.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            renderer.sprite = idleSprite;
            renderer.flipX = false;
            renderer.enabled = true;

            PlayerAttackAnimation3D legacyAnimation = root.GetComponent<PlayerAttackAnimation3D>();
            if (legacyAnimation != null) UnityEngine.Object.DestroyImmediate(legacyAnimation);
            PlayerAttack3D legacyAttack = root.GetComponent<PlayerAttack3D>();
            if (legacyAttack != null)
            {
                SerializedObject legacyData = new SerializedObject(legacyAttack);
                SerializedProperty keepLegacy = legacyData.FindProperty("keepLegacyIdleSprite");
                if (keepLegacy != null) keepLegacy.boolValue = false;
                legacyData.ApplyModifiedPropertiesWithoutUndo();
            }

            PlayerAnimationController bridge = root.GetComponent<PlayerAnimationController>();
            if (bridge == null) throw new InvalidOperationException("PlayerAnimationController is missing.");
            SerializedObject bridgeData = new SerializedObject(bridge);
            bridgeData.FindProperty("animator").objectReferenceValue = animator;
            bridgeData.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Sprite FirstSprite(string file) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + file);

    private static string[] Names(string prefix, int first, int last, bool twoDigits)
    {
        var result = new List<string>();
        for (int i = first; i <= last; i++) result.Add(prefix + (twoDigits ? i.ToString("00") : i.ToString()) + ".png");
        return result.ToArray();
    }

    private static string[] RunNames()
    {
        return new[]
        {
            "Player_Run_01.png", "Player_Run_02.png", "Player_Run_03.png", "Player_Run_04.png",
            "Player_Run_05.png", "Player_Run_06.png", "Player_Run_07.png", "Player_Run_08.png",
            "Player_Run_9.png", "Player_Run_10.png", "Player_Run_11.png"
        };
    }
}
