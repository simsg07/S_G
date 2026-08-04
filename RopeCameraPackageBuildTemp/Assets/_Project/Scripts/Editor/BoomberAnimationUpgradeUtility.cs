using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class BoomberAnimationUpgradeUtility
{
    private const string Art = "Assets/_Project/Art/Enemies/Boomber/New/";
    private const string Anim = "Assets/_Project/Animations/Enemies/Boomber/";
    private const string PrefabPath = "Assets/_Project/Prefabs/Enemies/Boomber.prefab";

    [MenuItem("Tools/Project/Apply New Boomber Animation Set")]
    public static void ApplyFromMenu() => Apply();

    private static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[Boomber Animation] Edit Mode에서 실행하세요.");
            return;
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ConfigureTextures();

        AnimationClip idle = CreateClip("Boomber_Idle", Names("Bomber_rig_", 1, 5), 5f, true);
        AnimationClip run = CreateClip("Boomber_Run", Names("Bomber_run_", 1, 7), 7f, true);
        AnimationClip prepare = CreateClip("Boomber_PreAttack", ReverseNames("Bomber_Attack_", 6, 8), 6f, false);
        AnimationClip leap = CreateClip("Boomber_AttackLeap", ReverseNames("Bomber_Attack_", 1, 5), 14f, false);
        AnimationClip explosion = CreateClip("Boomber_Explosion", Names("Bomber_Boom_", 5, 9), 8f, false);
        AnimationClip dead = CreateClip("Boomber_Dead", new[] { "Bomber_Dead.png" }, 1f, false);

        AnimatorController controller = ConfigureController(idle, run, prepare, leap, explosion, dead);
        ConfigurePrefab(controller, FirstSprite("Bomber_rig_01.png"));
        AssetDatabase.SaveAssets();
        Debug.Log("[Boomber Animation] 새 26개 이미지, 6개 상태, 도약/폭발 동기화를 Boomber.prefab에 적용했습니다.");
    }

    private static void ConfigureTextures()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { Art.TrimEnd('/') });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            bool changed = importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                !Mathf.Approximately(importer.spritePixelsPerUnit, 200f) || importer.mipmapEnabled;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 200f;
            importer.spritePivot = new Vector2(0.5f, 0.5f);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            if (changed) importer.SaveAndReimport();
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
        string path = Anim + "Boomber.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);

        while (controller.parameters.Length > 0) controller.RemoveParameter(0);
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsAttacking", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsDead", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("State", AnimatorControllerParameterType.Int);

        AnimatorStateMachine machine = new AnimatorStateMachine { name = "Base Layer" };
        AssetDatabase.AddObjectToAsset(machine, controller);
        string[] names = { "Idle", "Run", "PreAttack", "AttackLeap", "Explosion", "Dead" };
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
            BoomberExplosion explosion = root.GetComponent<BoomberExplosion>();
            BoomberBrain brain = root.GetComponent<BoomberBrain>();
            MonsterFacing facing = root.GetComponent<MonsterFacing>();
            if (animator == null || renderer == null || explosion == null || brain == null)
                throw new InvalidOperationException("Boomber prefab references are incomplete.");

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            renderer.sprite = idleSprite;
            renderer.flipX = false;
            explosion.attackLeapDuration = 0.35f;
            explosion.explosionVisualDuration = 0.625f;

            SetSerializedBool(brain, "visualFacesRightByDefault", false);
            if (facing != null) facing.visualFacesRightByDefault = false;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void SetSerializedBool(UnityEngine.Object target, string propertyName, bool value)
    {
        SerializedObject data = new SerializedObject(target);
        SerializedProperty property = data.FindProperty(propertyName);
        if (property != null) property.boolValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Sprite FirstSprite(string file)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(Art + file);
    }

    private static string[] Names(string prefix, int first, int last)
    {
        var names = new List<string>();
        for (int i = first; i <= last; i++) names.Add(prefix + i.ToString("00") + ".png");
        return names.ToArray();
    }

    private static string[] ReverseNames(string prefix, int first, int last)
    {
        var names = new List<string>();
        for (int i = last; i >= first; i--) names.Add(prefix + i.ToString("00") + ".png");
        return names.ToArray();
    }
}
