#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
public static class DionaeaAnimationSetupUtility
{
    public const string ArtFolder = "Assets/_Project/Art/Enemies/Dionaea/";
    public const string AnimationFolder = "Assets/_Project/Animations/Enemies/Dionaea/";
    public const string ControllerPath = AnimationFolder + "Dionaea.controller";
    private const string PrefabPath = "Assets/_Project/Prefabs/Enemies/Dionaea.prefab";

    static DionaeaAnimationSetupUtility()
    {
        EditorApplication.delayCall += PrepareAssets;
    }

    [MenuItem("Tools/Project/Prepare Dionaea Animation Assets")]
    public static void PrepareAssets()
    {
        EnsureFolder("Assets/_Project/Animations/Enemies", "Dionaea");
        string[] textures = AssetDatabase.FindAssets("t:Texture2D", new[] { ArtFolder });
        for (int i = 0; i < textures.Length; i++) ConfigureSpriteImporter(AssetDatabase.GUIDToAssetPath(textures[i]));

        CreateClip("Dionaea_Idle", new[] {
            "Dionaea_Idle_01.png", "Dionaea_Idle_02.png", "Dionaea_Idle_03.png", "Dionaea_Idle_04.png"
        }, 4f, true);
        CreateClip("Dionaea_Attack", new[] {
            "Dionaea_01.png", "Dionaea_02.png", "Dionaea_03.png", "Dionaea_04.png", "Dionaea_05.png"
        }, 8f, false);
        CreateClip("Dionaea_Retracted", new[] {
            "Dionaea_Retracted_01.png", "Dionaea_Retracted_02.png", "Dionaea_Retracted_03.png", "Dionaea_Retracted_04.png",
            "Dionaea_Retracted_05.png", "Dionaea_Retracted_06.png", "Dionaea_Retracted_07.png", "Dionaea_Retracted_08.png"
        }, 8f, false);
        CreateClip("Dionaea_Recover", new[] {
            "Dionaea_Retracted_08.png", "Dionaea_Retracted_07.png", "Dionaea_Retracted_06.png", "Dionaea_Retracted_05.png",
            "Dionaea_Retracted_04.png", "Dionaea_Retracted_03.png", "Dionaea_Retracted_02.png", "Dionaea_Retracted_01.png"
        }, 8f, false);
        CreateClip("Dionaea_RetractedHold", new[] { "Dionaea_Retracted_08.png" }, 1f, true);
        AnimatorController controller = CreateControllerIfMissing();
        ConnectPrefabAnimator(controller);
        AssetDatabase.SaveAssets();
        DionaeaValidationUtility.ValidatePrefab();
    }

    public static Sprite LoadDefaultSprite()
    {
        PrepareAssets();
        return AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "Dionaea_Idle_01.png");
    }

    public static RuntimeAnimatorController LoadAnimatorController()
    {
        PrepareAssets();
        return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
    }

    private static AnimatorController CreateControllerIfMissing()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        RemoveDeadConfiguration(controller);
        EnsureParameter(controller, "Attack", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "IsAttacking", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "IsRetracted", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "IsRecovering", AnimatorControllerParameterType.Bool);
        RemoveParameter(controller, "IsRetracting");

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        ClearStateMachine(stateMachine);
        AnimatorState idle = stateMachine.AddState("Idle");
        AnimatorState attack = stateMachine.AddState("Attack");
        AnimatorState retracting = stateMachine.AddState("Retracting");
        AnimatorState retracted = stateMachine.AddState("Retracted");
        AnimatorState recovering = stateMachine.AddState("Recovering");
        idle.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimationFolder + "Dionaea_Idle.anim");
        attack.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimationFolder + "Dionaea_Attack.anim");
        retracting.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimationFolder + "Dionaea_Retracted.anim");
        retracted.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimationFolder + "Dionaea_RetractedHold.anim");
        recovering.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimationFolder + "Dionaea_Recover.anim");
        stateMachine.defaultState = idle;

        AnimatorStateTransition toAttack = idle.AddTransition(attack);
        toAttack.hasExitTime = false;
        toAttack.duration = 0f;
        toAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");

        AnimatorStateTransition attackToIdle = attack.AddTransition(idle);
        attackToIdle.hasExitTime = true;
        attackToIdle.exitTime = 1f;
        attackToIdle.duration = 0f;

        AnimatorStateTransition idleToRetracting = idle.AddTransition(retracting);
        idleToRetracting.hasExitTime = false;
        idleToRetracting.duration = 0f;
        idleToRetracting.AddCondition(AnimatorConditionMode.If, 0f, "IsRetracted");

        AnimatorStateTransition toRetracting = attack.AddTransition(retracting);
        toRetracting.hasExitTime = false;
        toRetracting.duration = 0f;
        toRetracting.canTransitionToSelf = false;
        toRetracting.AddCondition(AnimatorConditionMode.If, 0f, "IsRetracted");

        AnimatorStateTransition retractingToRecovering = retracting.AddTransition(recovering);
        retractingToRecovering.hasExitTime = false;
        retractingToRecovering.duration = 0f;
        retractingToRecovering.AddCondition(AnimatorConditionMode.If, 0f, "IsRecovering");

        AnimatorStateTransition retractingToRetracted = retracting.AddTransition(retracted);
        retractingToRetracted.hasExitTime = true;
        retractingToRetracted.exitTime = 1f;
        retractingToRetracted.duration = 0f;

        AnimatorStateTransition retractedToRecovering = retracted.AddTransition(recovering);
        retractedToRecovering.hasExitTime = false;
        retractedToRecovering.duration = 0f;
        retractedToRecovering.AddCondition(AnimatorConditionMode.If, 0f, "IsRecovering");

        AnimatorStateTransition recoveringToIdle = recovering.AddTransition(idle);
        recoveringToIdle.hasExitTime = true;
        recoveringToIdle.exitTime = 1f;
        recoveringToIdle.duration = 0f;

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        for (int i = 0; i < controller.parameters.Length; i++)
            if (controller.parameters[i].name == name) return;
        controller.AddParameter(name, type);
    }

    private static void RemoveParameter(AnimatorController controller, string name)
    {
        for (int i = controller.parameters.Length - 1; i >= 0; i--)
            if (controller.parameters[i].name == name) controller.RemoveParameter(i);
    }

    private static void ClearStateMachine(AnimatorStateMachine stateMachine)
    {
        AnimatorStateTransition[] anyTransitions = stateMachine.anyStateTransitions;
        for (int i = anyTransitions.Length - 1; i >= 0; i--) stateMachine.RemoveAnyStateTransition(anyTransitions[i]);
        ChildAnimatorState[] states = stateMachine.states;
        for (int i = states.Length - 1; i >= 0; i--) stateMachine.RemoveState(states[i].state);
    }

    private static void RemoveDeadConfiguration(AnimatorController controller)
    {
        for (int i = controller.parameters.Length - 1; i >= 0; i--)
        {
            if (controller.parameters[i].name == "IsDead") controller.RemoveParameter(i);
        }

        if (controller.layers.Length > 0)
        {
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorStateTransition[] anyTransitions = stateMachine.anyStateTransitions;
            for (int i = anyTransitions.Length - 1; i >= 0; i--)
            {
                if (anyTransitions[i].destinationState != null && anyTransitions[i].destinationState.name == "Dead")
                    stateMachine.RemoveAnyStateTransition(anyTransitions[i]);
            }
            ChildAnimatorState[] states = stateMachine.states;
            for (int i = states.Length - 1; i >= 0; i--)
            {
                if (states[i].state.name == "Dead") stateMachine.RemoveState(states[i].state);
            }
        }

        string obsoleteDeadClip = AnimationFolder + "Dionaea_Dead.anim";
        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(obsoleteDeadClip) != null) AssetDatabase.DeleteAsset(obsoleteDeadClip);
        EditorUtility.SetDirty(controller);
    }

    private static void ConnectPrefabAnimator(RuntimeAnimatorController controller)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) return;
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform visual = prefabRoot.transform.Find("Visual");
            Animator animator = visual != null ? visual.GetComponent<Animator>() : null;
            if (animator == null) throw new MissingComponentException("Dionaea Visual Animator is missing.");
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            DionaeaAnimatorBridge bridge = prefabRoot.GetComponent<DionaeaAnimatorBridge>();
            if (bridge != null) bridge.Animator = animator;
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void ConfigureSpriteImporter(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        bool changed = importer.textureType != TextureImporterType.Sprite || importer.spritePixelsPerUnit != 384f ||
            importer.alphaIsTransparency == false || importer.mipmapEnabled;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 384f;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
        settings.spritePivot = new Vector2(0.5f, 0f);
        importer.SetTextureSettings(settings);
        if (changed) importer.SaveAndReimport();
    }

    private static void CreateClip(string name, string[] frameNames, float frameRate, bool loop)
    {
        ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[frameNames.Length];
        for (int i = 0; i < frameNames.Length; i++)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + frameNames[i]);
            if (sprite == null) throw new MissingReferenceException("Missing Dionaea sprite: " + frameNames[i]);
            keys[i] = new ObjectReferenceKeyframe { time = i / frameRate, value = sprite };
        }

        string path = AnimationFolder + name + ".anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip { name = name };
            AssetDatabase.CreateAsset(clip, path);
        }
        clip.frameRate = frameRate;
        AnimationUtility.SetObjectReferenceCurve(clip,
            EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite"), keys);
        AnimationClipSettings clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
        clipSettings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
        EditorUtility.SetDirty(clip);
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
    }
}
#endif
