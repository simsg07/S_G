using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class EyeballFlyAssetBuilder
{
    private const string AutoBuildSessionKey = "EyeballFlyAssetBuilder.AutoBuildQueued";
    private const string SpriteFolder = "Assets/_Project/Art/Enemies/EyeballFly";
    private const string AnimationFolder = "Assets/_Project/Animations/Enemies/EyeballFly";
    private const string PrefabPath = "Assets/_Project/Prefabs/Enemies/EyeballFly.prefab";
    private const string ControllerPath = AnimationFolder + "/EyeballFly.controller";

    private static readonly string[] IdleSprites =
    {
        SpriteFolder + "/EyeballFly_Idle_01.png",
        SpriteFolder + "/EyeballFly_Idle_02.png",
        SpriteFolder + "/EyeballFly_Idle_03.png",
        SpriteFolder + "/EyeballFly_Idle_04.png",
        SpriteFolder + "/EyeballFly_Idle_05.png",
        SpriteFolder + "/EyeballFly_Idle_06.png",
    };

    private static readonly string[] AttackSprites =
    {
        SpriteFolder + "/EyeballFly_Attack_01.png",
        SpriteFolder + "/EyeballFly_Attack_02.png",
        SpriteFolder + "/EyeballFly_Attack_03.png",
        SpriteFolder + "/EyeballFly_Attack_04.png",
        SpriteFolder + "/EyeballFly_Attack_05.png",
        SpriteFolder + "/EyeballFly_Attack_06.png",
        SpriteFolder + "/EyeballFly_Attack_07.png",
    };

    private static readonly string[] DeadSprites =
    {
        SpriteFolder + "/EyeballFly_Dead_01.png",
        SpriteFolder + "/EyeballFly_Dead_02.png",
        SpriteFolder + "/EyeballFly_Dead_03.png",
        SpriteFolder + "/EyeballFly_Dead_04_Dim.png",
    };

    [InitializeOnLoadMethod]
    private static void BuildWhenAssetsAreMissing()
    {
        if (SessionState.GetBool(AutoBuildSessionKey, false))
        {
            return;
        }

        if (!AssetDatabase.IsValidFolder(SpriteFolder) || AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            return;
        }

        SessionState.SetBool(AutoBuildSessionKey, true);
        EditorApplication.delayCall += () =>
        {
            try
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
                {
                    Build();
                }
            }
            finally
            {
                SessionState.EraseBool(AutoBuildSessionKey);
            }
        };
    }

    [MenuItem("Tools/Project/Build Eyeball Fly Visual Assets")]
    public static void Build()
    {
        EnsureFolders();
        ConfigureSprites();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        AnimationClip idleClip = CreateSpriteClip("EyeballFly_Idle", IdleSprites, 10f, true);
        AnimationClip activeClip = CreatePulseClip("EyeballFly_Active", 1.04f, true);
        AnimationClip highlightClip = CreatePulseClip("EyeballFly_Highlight", 1.08f, true);
        AnimationClip disabledClip = CreateTintClip("EyeballFly_Disabled", new Color(0.45f, 0.45f, 0.45f, 1f), true);
        AnimationClip attackClip = CreateSpriteClip("EyeballFly_Attack", AttackSprites, 14f, false);
        AnimationClip deadClip = CreateSpriteClip("EyeballFly_Dead", DeadSprites, 8f, false);

        AnimatorController controller = BuildController(idleClip, activeClip, highlightClip, disabledClip, attackClip, deadClip);
        BuildPrefab(controller);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Eyeball Fly visual assets built.");
    }

    private static void EnsureFolders()
    {
        CreateFolderIfMissing("Assets/_Project/Art/Enemies");
        CreateFolderIfMissing(SpriteFolder);
        CreateFolderIfMissing(AnimationFolder);
        CreateFolderIfMissing("Assets/_Project/Prefabs/Objects/Enemy");
    }

    private static void ConfigureSprites()
    {
        foreach (string spritePath in Directory.GetFiles(SpriteFolder, "*.png"))
        {
            string assetPath = spritePath.Replace('\\', '/');
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                continue;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
    }

    private static AnimationClip CreateSpriteClip(string clipName, string[] spritePaths, float frameRate, bool loop)
    {
        AnimationClip clip = LoadOrCreateClip(clipName);
        clip.ClearCurves();
        clip.frameRate = frameRate;

        ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[spritePaths.Length];
        for (int i = 0; i < spritePaths.Length; i++)
        {
            keyframes[i] = new ObjectReferenceKeyframe
            {
                time = i / frameRate,
                value = AssetDatabase.LoadAssetAtPath<Sprite>(spritePaths[i]),
            };
        }

        EditorCurveBinding spriteBinding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = string.Empty,
            propertyName = "m_Sprite",
        };

        AnimationUtility.SetObjectReferenceCurve(clip, spriteBinding, keyframes);
        SetLoop(clip, loop);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static AnimationClip CreatePulseClip(string clipName, float peakScale, bool loop)
    {
        AnimationClip clip = LoadOrCreateClip(clipName);
        clip.ClearCurves();
        clip.frameRate = 30f;

        AnimationCurve scaleCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.25f, peakScale),
            new Keyframe(0.5f, 1f));

        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalScale.x", scaleCurve);
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalScale.y", scaleCurve);
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalScale.z", AnimationCurve.Constant(0f, 0.5f, 1f));
        SetLoop(clip, loop);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static AnimationClip CreateTintClip(string clipName, Color color, bool loop)
    {
        AnimationClip clip = LoadOrCreateClip(clipName);
        clip.ClearCurves();
        clip.frameRate = 30f;

        clip.SetCurve(string.Empty, typeof(SpriteRenderer), "m_Color.r", AnimationCurve.Constant(0f, 0.5f, color.r));
        clip.SetCurve(string.Empty, typeof(SpriteRenderer), "m_Color.g", AnimationCurve.Constant(0f, 0.5f, color.g));
        clip.SetCurve(string.Empty, typeof(SpriteRenderer), "m_Color.b", AnimationCurve.Constant(0f, 0.5f, color.b));
        clip.SetCurve(string.Empty, typeof(SpriteRenderer), "m_Color.a", AnimationCurve.Constant(0f, 0.5f, color.a));
        SetLoop(clip, loop);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static AnimatorController BuildController(
        AnimationClip idleClip,
        AnimationClip activeClip,
        AnimationClip highlightClip,
        AnimationClip disabledClip,
        AnimationClip attackClip,
        AnimationClip deadClip)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        }

        controller.parameters = new AnimatorControllerParameter[0];
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsAttacking", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsDead", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        ClearStateMachine(stateMachine);

        AnimatorState idle = stateMachine.AddState("Idle", new Vector3(260f, 120f, 0f));
        AnimatorState move = stateMachine.AddState("Move", new Vector3(520f, 120f, 0f));
        AnimatorState attack = stateMachine.AddState("Attack", new Vector3(780f, 120f, 0f));
        AnimatorState dead = stateMachine.AddState("Dead", new Vector3(780f, 300f, 0f));

        idle.motion = idleClip;
        move.motion = idleClip;
        attack.motion = attackClip;
        dead.motion = deadClip;
        stateMachine.defaultState = idle;

        AddBoolTransition(idle, move, "IsMoving", true);
        AddBoolTransition(move, idle, "IsMoving", false);
        AddBoolTransition(stateMachine, attack, "IsAttacking", true);
        AddBoolTransition(stateMachine, dead, "IsDead", true);
        AddTriggerTransition(stateMachine, attack, "Attack");
        AddAttackReturnTransition(attack, move, true);
        AddAttackReturnTransition(attack, idle, false);

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void BuildPrefab(AnimatorController controller)
    {
        GameObject root = new GameObject("EyeballFly");
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IdleSprites[0]);

        Animator animator = root.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        Rigidbody body = root.AddComponent<Rigidbody>();
        body.useGravity = false;
        body.isKinematic = true;
        body.constraints = TwoPointFiveDUtility3D.SideViewRigidbodyConstraints;

        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = new Vector3(1f, 1f, 0.2f);

        EyeballFlyAnimationController animationController = root.AddComponent<EyeballFlyAnimationController>();
        SerializedObject serializedObject = new SerializedObject(animationController);
        serializedObject.FindProperty("animator").objectReferenceValue = animator;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();

        EyeballFlyAI ai = root.AddComponent<EyeballFlyAI>();
        SerializedObject aiObject = new SerializedObject(ai);
        aiObject.FindProperty("animationController").objectReferenceValue = animationController;
        aiObject.ApplyModifiedPropertiesWithoutUndo();

        EyeballFlyHealth health = root.AddComponent<EyeballFlyHealth>();
        SerializedObject healthObject = new SerializedObject(health);
        healthObject.FindProperty("ai").objectReferenceValue = ai;
        healthObject.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
    }

    private static AnimationClip LoadOrCreateClip(string clipName)
    {
        string path = AnimationFolder + "/" + clipName + ".anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip != null)
        {
            return clip;
        }

        clip = new AnimationClip { name = clipName };
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    private static void SetLoop(AnimationClip clip, bool loop)
    {
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
    }

    private static void AddBoolTransition(AnimatorState from, AnimatorState to, string parameterName, bool value)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        ConfigureTransition(transition, false);
        transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameterName);
    }

    private static void AddBoolTransition(AnimatorStateMachine stateMachine, AnimatorState to, string parameterName, bool value)
    {
        AnimatorStateTransition transition = stateMachine.AddAnyStateTransition(to);
        ConfigureTransition(transition, false);
        transition.canTransitionToSelf = false;
        transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameterName);
    }

    private static void AddTriggerTransition(AnimatorStateMachine stateMachine, AnimatorState to, string parameterName)
    {
        AnimatorStateTransition transition = stateMachine.AddAnyStateTransition(to);
        ConfigureTransition(transition, false);
        transition.canTransitionToSelf = false;
        transition.AddCondition(AnimatorConditionMode.If, 0f, parameterName);
    }

    private static void AddExitTransition(AnimatorState from, AnimatorState to, float exitTime)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        ConfigureTransition(transition, true);
        transition.exitTime = exitTime;
    }

    private static void AddAttackReturnTransition(AnimatorState from, AnimatorState to, bool moving)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        ConfigureTransition(transition, true);
        transition.exitTime = 0.95f;
        transition.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAttacking");
        transition.AddCondition(moving ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, "IsMoving");
    }

    private static void ConfigureTransition(AnimatorStateTransition transition, bool hasExitTime)
    {
        transition.hasExitTime = hasExitTime;
        transition.duration = 0.05f;
        transition.hasFixedDuration = true;
    }

    private static void ClearStateMachine(AnimatorStateMachine stateMachine)
    {
        foreach (ChildAnimatorState state in stateMachine.states)
        {
            stateMachine.RemoveState(state.state);
        }

        foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions)
        {
            stateMachine.RemoveAnyStateTransition(transition);
        }
    }

    private static void CreateFolderIfMissing(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(name))
        {
            CreateFolderIfMissing(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
