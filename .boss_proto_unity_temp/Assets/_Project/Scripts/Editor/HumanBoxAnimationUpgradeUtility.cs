using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class HumanBoxAnimationUpgradeUtility
{
    // Explicit menu utility; intentionally does not run automatically.
    private const string Art = "Assets/_Project/Art/Enemies/Human_Box/New/";
    private const string Anim = "Assets/_Project/Animations/Enemies/Human_Box/";
    private const string PrefabPath = "Assets/_Project/Prefabs/Enemies/Human_Box.prefab";

    [MenuItem("Tools/Project/Apply New Human Box Animation Set")]
    public static void ApplyFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[Human Box Animation] Edit Mode에서 실행하세요.");
            return;
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ConfigureTextures();

        AnimationClip idle = CreateClip("HumanBox_Idle", new[] { "Human_Box_Wake_Up_01.png" }, 1f, true, false);
        AnimationClip howling = CreateClip("HumanBox_Howling", Names("Human_Box_Howl_", 1, 5), 5f, false, true);
        AnimationClip walk = CreateClip("HumanBox_Walk", Names("Human_Box_Run_", 1, 8), 8f, true, false);
        AnimationClip dead = CreateClip("HumanBox_Dead", new[]
        {
            "Human_Box_Dead_01.png", "Human_Box_Dead_02.png", "Human_Box_Dead_03.png",
            "Human_Box_Dead_04.png", "Human_Box_Dead_05.png", "Human_Box_Dead_07.png",
            "Human_Box_Dead_08.png"
        }, 7f, false, true);

        AnimationClip attack = LoadRequiredClip("HumanBox_Attack");
        AnimationClip attackFalse = LoadRequiredClip("HumanBox_AttackFalse");
        AnimatorController controller = ConfigureController(idle, howling, walk, attack, attackFalse, dead);
        ConfigurePrefab(controller, FirstSprite("Human_Box_Wake_Up_01.png"));
        AssetDatabase.SaveAssets();
        Debug.Log("[Human Box Animation] 새 24개 이미지와 독립 Howling 상태를 Human_Box.prefab에 적용했습니다.");
    }

    private static void ConfigureTextures()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { Art.TrimEnd('/') });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 200f;
            importer.spritePivot = new Vector2(0.5f, 0.5f);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }

    private static AnimationClip CreateClip(string name, string[] files, float sampleRate, bool loop, bool holdLastFrame)
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
        if (holdLastFrame && keys.Count > 0)
        {
            keys.Add(new ObjectReferenceKeyframe { time = files.Length / sampleRate, value = keys[keys.Count - 1].value });
        }

        clip.frameRate = sampleRate;
        var binding = new EditorCurveBinding { path = string.Empty, type = typeof(SpriteRenderer), propertyName = "m_Sprite" };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys.ToArray());
        SerializedObject data = new SerializedObject(clip);
        SerializedProperty loopProperty = data.FindProperty("m_AnimationClipSettings.m_LoopTime");
        if (loopProperty != null) loopProperty.boolValue = loop;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static AnimatorController ConfigureController(params AnimationClip[] clips)
    {
        string path = Anim + "HumanBox.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);

        while (controller.parameters.Length > 0) controller.RemoveParameter(0);
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsAttacking", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsDead", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsHowling", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsAttackFalse", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Howling", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("AttackFalse", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("State", AnimatorControllerParameterType.Int);

        AnimatorStateMachine machine = new AnimatorStateMachine { name = "Base Layer" };
        AssetDatabase.AddObjectToAsset(machine, controller);
        string[] names = { "Idle", "Howling", "Walk", "Attack", "AttackFalse", "Dead" };
        AnimatorState[] states = new AnimatorState[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            states[i] = machine.AddState(names[i], new Vector3(280f + (i % 3) * 240f, 80f + (i / 3) * 150f));
            states[i].motion = clips[i];
            AnimatorStateTransition transition = machine.AddAnyStateTransition(states[i]);
            transition.hasExitTime = false;
            transition.duration = 0f;
            transition.canTransitionToSelf = false;
            transition.AddCondition(AnimatorConditionMode.Equals, i, "State");
        }
        machine.defaultState = states[0];
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
            Animator animator = root.GetComponentInChildren<Animator>(true);
            SpriteRenderer renderer = root.GetComponentInChildren<SpriteRenderer>(true);
            if (animator == null || renderer == null || root.GetComponent<HumanBoxAI>() == null)
                throw new InvalidOperationException("Human_Box prefab references are incomplete.");

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            renderer.sprite = idleSprite;
            renderer.flipX = false;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static AnimationClip LoadRequiredClip(string name)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Anim + name + ".anim");
        if (clip == null) throw new InvalidOperationException("Existing clip is missing: " + name);
        return clip;
    }

    private static Sprite FirstSprite(string file) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + file);

    private static string[] Names(string prefix, int first, int last)
    {
        var names = new List<string>();
        for (int i = first; i <= last; i++) names.Add(prefix + i.ToString("00") + ".png");
        return names.ToArray();
    }
}
