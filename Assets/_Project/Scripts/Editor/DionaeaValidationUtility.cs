#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
public static class DionaeaValidationUtility
{
    private const string PrefabPath = "Assets/_Project/Prefabs/Enemies/Dionaea.prefab";
    private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player/Player.prefab";
    private const string CircleSpikePrefabPath = "Assets/_Project/Prefabs/Objects/Gravity/CircleSpike.prefab";

    static DionaeaValidationUtility()
    {
        // DionaeaAnimationSetupUtility schedules validation after clips/controller are rebuilt.
    }

    [MenuItem("Tools/Project/Validate Dionaea (M_OBJ_004)")]
    public static void ValidatePrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Require(prefab != null, "Dionaea prefab is missing.");
        Require(prefab.GetComponent<DionaeaAI>() != null, "DionaeaAI is missing.");
        Require(prefab.GetComponent<MonsterHealth>() == null,
            "Dionaea must not have MonsterHealth because it is invulnerable and has no death state.");
        DataDrivenMonsterController dataController = prefab.GetComponent<DataDrivenMonsterController>();
        Require(dataController != null, "Dionaea DataDrivenMonsterController is missing.");
        SerializedObject serializedDataController = new SerializedObject(dataController);
        MonsterData dionaeaData = serializedDataController.FindProperty("monsterData").objectReferenceValue as MonsterData;
        Require(dionaeaData != null && string.IsNullOrEmpty(dionaeaData.deadBoolName),
            "Dionaea MonsterData must not retain an IsDead animator parameter name.");
        DionaeaAttack prefabAttack = prefab.GetComponent<DionaeaAttack>();
        Require(prefabAttack != null, "DionaeaAttack is missing.");
        Require(prefab.GetComponentInChildren<DionaeaLightReceiver>(true) != null, "DionaeaLightReceiver is missing.");
        DionaeaAnimatorBridge prefabAnimatorBridge = prefab.GetComponent<DionaeaAnimatorBridge>();
        Require(prefabAnimatorBridge != null, "DionaeaAnimatorBridge is missing.");
        Require(prefab.GetComponent<Collider>() == null, "A root Collider can physically block Player movement.");
        Transform bodyColliderRoot = prefab.transform.Find("BodyCollider");
        Require(bodyColliderRoot != null, "BodyCollider child is missing.");
        BoxCollider bodyCollider = bodyColliderRoot.GetComponent<BoxCollider>();
        Require(bodyCollider != null && bodyCollider.isTrigger, "BodyCollider must be a Trigger.");
        Require(bodyCollider.size.x <= 0.7f && bodyCollider.size.y <= 0.55f,
            "BodyCollider is too large and may block Player movement.");
        Collider[] prefabColliders = prefab.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < prefabColliders.Length; i++)
        {
            Require(prefabColliders[i].isTrigger, "Dionaea contains a non-trigger Collider: " + prefabColliders[i].name);
        }
        Animator visualAnimator = prefab.transform.Find("Visual")?.GetComponent<Animator>();
        Require(visualAnimator != null && !visualAnimator.applyRootMotion, "Visual Animator is missing or Root Motion is enabled.");
        AnimatorController controller = visualAnimator.runtimeAnimatorController as AnimatorController;
        Require(controller != null, "Dionaea Animator Controller is not connected.");
        Require(controller.layers.Length > 0 && controller.layers[0].stateMachine.defaultState != null &&
            controller.layers[0].stateMachine.defaultState.name == "Idle",
            "Dionaea Animator default state is not Idle.");
        Require(HasParameter(controller, "Attack", AnimatorControllerParameterType.Trigger), "Animator Attack trigger is missing.");
        Require(HasParameter(controller, "IsAttacking", AnimatorControllerParameterType.Bool), "Animator IsAttacking bool is missing.");
        Require(HasParameter(controller, "IsRetracted", AnimatorControllerParameterType.Bool), "Animator IsRetracted bool is missing.");
        Require(HasParameter(controller, "IsRecovering", AnimatorControllerParameterType.Bool), "Animator IsRecovering bool is missing.");
        Require(!HasParameter(controller, "IsRetracting", AnimatorControllerParameterType.Bool), "Obsolete Animator IsRetracting bool must be removed.");
        Require(!HasParameter(controller, "IsDead", AnimatorControllerParameterType.Bool), "Dionaea Animator must not use IsDead.");
        Require(HasStateMotion(controller, "Idle", "Dionaea_Idle"), "Idle state or clip is missing.");
        Require(HasStateMotion(controller, "Attack", "Dionaea_Attack"), "Attack state or clip is missing.");
        Require(HasStateMotion(controller, "Retracting", "Dionaea_Retracted"), "Retracting state or clip is missing.");
        Require(HasStateMotion(controller, "Retracted", "Dionaea_RetractedHold"), "Retracted hold state or clip is missing.");
        Require(HasStateMotion(controller, "Recovering", "Dionaea_Recover"), "Recovering state or clip is missing.");
        Require(controller.layers[0].stateMachine.anyStateTransitions.Length == 0,
            "Dionaea must not use Any State transitions, especially Any State -> Attack.");
        Require(!HasState(controller, "Dead"), "Dionaea Animator must not contain a Dead state.");
        AnimationClip idleClip = controller.layers[0].stateMachine.defaultState.motion as AnimationClip;
        Require(idleClip != null && AnimationUtility.GetAnimationClipSettings(idleClip).loopTime,
            "Idle clip is missing or is not configured to loop.");
        EditorCurveBinding[] idleBindings = AnimationUtility.GetObjectReferenceCurveBindings(idleClip);
        Require(idleBindings.Length == 1 && AnimationUtility.GetObjectReferenceCurve(idleClip, idleBindings[0]).Length == 4,
            "Idle clip must animate the Visual SpriteRenderer with four Sprite frames.");
        AnimationClip retractedClip = GetStateMotion(controller, "Retracting") as AnimationClip;
        Require(retractedClip != null, "Retracting clip is missing.");
        EditorCurveBinding[] retractedBindings = AnimationUtility.GetObjectReferenceCurveBindings(retractedClip);
        ObjectReferenceKeyframe[] retractedFrames = retractedBindings.Length == 1
            ? AnimationUtility.GetObjectReferenceCurve(retractedClip, retractedBindings[0])
            : Array.Empty<ObjectReferenceKeyframe>();
        Require(retractedFrames.Length == 8 && Mathf.Abs(retractedFrames[retractedFrames.Length - 1].time - 0.875f) <= 0.001f &&
            Mathf.Abs(retractedClip.length - 1f) <= 0.001f && Mathf.Abs(retractedClip.frameRate - 8f) <= 0.001f,
            "Retracting animation must contain eight ordered frames at 8 FPS with a one-second length.");
        AnimationClip recoverClip = GetStateMotion(controller, "Recovering") as AnimationClip;
        EditorCurveBinding[] recoverBindings = recoverClip != null ? AnimationUtility.GetObjectReferenceCurveBindings(recoverClip) : Array.Empty<EditorCurveBinding>();
        ObjectReferenceKeyframe[] recoverFrames = recoverBindings.Length == 1
            ? AnimationUtility.GetObjectReferenceCurve(recoverClip, recoverBindings[0])
            : Array.Empty<ObjectReferenceKeyframe>();
        Require(recoverClip != null && recoverFrames.Length == 8 &&
            Mathf.Abs(recoverFrames[recoverFrames.Length - 1].time - 0.875f) <= 0.001f &&
            Mathf.Abs(recoverClip.length - 1f) <= 0.001f && Mathf.Abs(recoverClip.frameRate - 8f) <= 0.001f,
            "Recover animation must contain eight reverse-ordered frames at 8 FPS with a one-second length.");
        Require(prefabAnimatorBridge.Animator == visualAnimator,
            "DionaeaAnimatorBridge is not connected to the Visual Animator.");
        prefabAnimatorBridge.ValidateAnimatorSetup();
        Transform attackOrigin = prefab.transform.Find("Head_AttackOrigin");
        Require(attackOrigin != null && prefabAttack.AttackOrigin == attackOrigin,
            "Head_AttackOrigin is missing or is not connected to DionaeaAttack.");
        Require(prefabAttack.VisualRoot == prefab.transform.Find("Visual"),
            "DionaeaAttack VisualRoot is not connected, so facing cannot flip the attack box.");
        Require(prefab.GetComponentInChildren<SpriteRenderer>(true)?.sprite != null, "Dionaea default Sprite is missing.");
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        Require(playerPrefab != null && playerPrefab.CompareTag("Player"),
            "The actual Player prefab is missing or does not have the Player tag.");
        GameObject circleSpikePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CircleSpikePrefabPath);
        GravityObjectDamageDealer circleSpikeDamage = circleSpikePrefab != null ? circleSpikePrefab.GetComponent<GravityObjectDamageDealer>() : null;
        Require(circleSpikeDamage != null, "CircleSpike GravityObjectDamageDealer is missing.");
        SerializedObject circleSpikeDamageSettings = new SerializedObject(circleSpikeDamage);
        Require(circleSpikeDamageSettings.FindProperty("instantKillPlayer").boolValue,
            "CircleSpike must remain an instant KillAndRespawn object.");
        SerializedObject prefabAI = new SerializedObject(prefab.GetComponent<DionaeaAI>());
        Require(!prefabAI.FindProperty("canDie").boolValue && prefabAI.FindProperty("isIndestructible").boolValue,
            "Dionaea must serialize canDie=false and isIndestructible=true.");
        Require(Mathf.Abs(prefabAI.FindProperty("retractAnimationDuration").floatValue - 1f) <= 0.001f &&
            prefabAI.FindProperty("waitRetractAnimationBeforeFullRetracted").boolValue,
            "Dionaea must wait for its configured one-second retract animation before becoming fully Retracted.");
        Require(Mathf.Abs(prefabAI.FindProperty("recoverAnimationDuration").floatValue - 1f) <= 0.001f &&
            Mathf.Abs(prefabAI.FindProperty("postRecoverAttackLockTime").floatValue - 0.5f) <= 0.001f,
            "Dionaea recovery must last one second and keep attacks locked for 0.5 seconds afterward.");
        SerializedObject prefabAttackSettings = new SerializedObject(prefabAttack);
        int playerLayerBit = 1 << playerPrefab.layer;
        Require((prefabAI.FindProperty("playerLayerMask").intValue & playerLayerBit) != 0,
            "Dionaea playerLayerMask does not include the actual Player prefab layer.");
        Require((prefabAttackSettings.FindProperty("playerLayerMask").intValue & playerLayerBit) != 0,
            "DionaeaAttack playerLayerMask does not include the actual Player prefab layer.");
        Vector3 attackOffset = prefabAttackSettings.FindProperty("attackBoxOffset").vector3Value;
        Require(Mathf.Abs(attackOffset.x) <= 0.001f && attackOffset.y > 0f,
            "Dionaea attack box must be offset upward (+Y), not left or right.");
        Vector3 detectionDirection = prefabAI.FindProperty("forwardDirection").vector3Value;
        Require(Vector3.Dot(detectionDirection.normalized, Vector3.up) > 0.99f,
            "Dionaea forward detection direction must point upward.");

        GameObject monster = null;
        GameObject player = null;
        GameObject wall = null;
        GameObject gameplayLight = null;
        try
        {
            monster = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            monster.hideFlags = HideFlags.HideAndDontSave;
            monster.transform.position = Vector3.zero;

            player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.hideFlags = HideFlags.HideAndDontSave;
            player.transform.position = new Vector3(0f, 1.6f, 0f);
            PlayerDamageReceiver playerDamageReceiver = player.GetComponent<PlayerDamageReceiver>();
            Require(playerDamageReceiver != null && player.GetComponent<Collider>() != null,
                "Actual Player prefab is missing PlayerDamageReceiver or its 3D Collider.");
            SerializedObject playerDamageSettings = new SerializedObject(playerDamageReceiver);
            playerDamageSettings.FindProperty("infiniteHealth").boolValue = false;
            playerDamageSettings.FindProperty("maxHp").intValue = 10;
            playerDamageSettings.FindProperty("currentHp").intValue = 10;
            playerDamageSettings.ApplyModifiedPropertiesWithoutUndo();

            DionaeaAI ai = monster.GetComponent<DionaeaAI>();
            DionaeaAttack attack = monster.GetComponent<DionaeaAttack>();
            DionaeaLightReceiver lightReceiver = monster.GetComponentInChildren<DionaeaLightReceiver>(true);
            DionaeaAnimatorBridge runtimeAnimatorBridge = monster.GetComponent<DionaeaAnimatorBridge>();
            Animator runtimeAnimator = monster.transform.Find("Visual").GetComponent<Animator>();
            Rigidbody body = monster.GetComponent<Rigidbody>();
            VerifyIdleSpriteSampling(monster.transform.Find("Visual"), idleClip);
            runtimeAnimatorBridge.PlayAttack();
            runtimeAnimatorBridge.SetRetracted(true);
            Require(runtimeAnimator.GetBool("IsRetracted"), "DionaeaAnimatorBridge did not set IsRetracted true.");
            runtimeAnimatorBridge.SetRetracted(false);
            Require(!runtimeAnimator.GetBool("IsRetracted"), "DionaeaAnimatorBridge did not set IsRetracted false.");
            runtimeAnimatorBridge.SetRecovering(true);
            Require(runtimeAnimator.GetBool("IsRecovering"), "DionaeaAnimatorBridge did not set IsRecovering true.");
            runtimeAnimatorBridge.SetRecovering(false);
            Physics.SyncTransforms();

            Require(body != null && body.isKinematic && body.constraints == RigidbodyConstraints.FreezeAll,
                "Dionaea Rigidbody is not fixed/kinematic.");
            Require(ai.CheckPlayerDetection() != null, "Player in the forward box was not detected.");

            player.transform.position = new Vector3(0f, -0.8f, 0f);
            Physics.SyncTransforms();
            Require(ai.CheckPlayerDetection() == null, "Player below Dionaea was detected as an upward target.");

            player.transform.position = new Vector3(0f, 1.6f, 0f);
            wall = new GameObject("DionaeaValidationWall");
            wall.hideFlags = HideFlags.HideAndDontSave;
            wall.layer = LayerMask.NameToLayer("Wall");
            wall.transform.position = new Vector3(0f, 1.85f, 0f);
            wall.AddComponent<BoxCollider>().size = new Vector3(2f, 0.15f, 0.8f);
            Physics.SyncTransforms();
            Require(ai.CheckPlayerDetection() == null, "Wall did not block Dionaea LOS.");

            UnityEngine.Object.DestroyImmediate(wall);
            wall = null;
            Physics.SyncTransforms();
            Require(attack.PerformAttack(), "Dionaea attack did not find the Player damage target.");
            Require(playerDamageReceiver.CurrentHp == 8, $"Actual Player HP was {playerDamageReceiver.CurrentHp}, expected 8 after 2 damage.");

            Transform visual = monster.transform.Find("Visual");
            visual.localScale = new Vector3(-1f, 1f, 1f);
            Physics.SyncTransforms();
            Require(attack.IsTargetInsideAttackBox(player.transform), "Visual X flip moved the upward attack box away from the Player.");
            Require(attack.PerformAttack(), "Upward Dionaea attack failed after Visual X flip.");
            Require(playerDamageReceiver.CurrentHp == 6, $"Actual Player HP was {playerDamageReceiver.CurrentHp}, expected 6 after the second upward 2 damage.");
            visual.localScale = Vector3.one;
            Physics.SyncTransforms();

            ai.SetLit(true);
            Require(ai.IsLit && !ai.CanAttack, "Dionaea can attack while lit.");
            ai.TryAttack(player.transform);
            Require(ai.CurrentState != DionaeaState.Attacking, "Dionaea entered attack state while lit.");
            lightReceiver.SetLightReceived(true);
            lightReceiver.AddLightExposure(1.01f);
            Require(ai.IsRetracting && !ai.IsRetracted && !ai.CanAttack,
                "Dionaea must remain Retracting and unable to attack before the one-second animation completes.");
            InvokeAiMethod(ai, "CompleteRetract");
            Require(ai.IsRetracted && !ai.CanAttack, "Dionaea can attack while retracted.");
            ai.SetLit(false);
            InvokeLightTick(ai, 1.01f);
            Require(ai.IsRecovering && !ai.CanAttack, "Dionaea did not begin recovery or can attack while recovering.");
            InvokeAiMethod(ai, "CompleteRecovery");
            Require(ai.CurrentState == DionaeaState.Idle && !ai.IsLit && !ai.CanAttack,
                "Dionaea did not return to attack-locked Idle after recovery.");

            gameplayLight = new GameObject("DionaeaValidationColliderlessLight");
            gameplayLight.transform.position = lightReceiver.transform.position;
            Light pointLight = gameplayLight.AddComponent<Light>();
            pointLight.type = LightType.Point;
            pointLight.range = 3f;
            pointLight.intensity = 2f;
            InvokeReceiverMethod(lightReceiver, "RefreshSceneLights");
            Require((bool)InvokeReceiverMethod(lightReceiver, "DetectLightOverlap"),
                "Dionaea did not detect an active Point Light without a Collider.");

            Debug.Log("[DionaeaValidation] PASS: CircleSpike behavior preserved, ordinary player damage=2 verified, Stone-proof invulnerable Dionaea has no MonsterHealth/Dead state/IsDead parameter, light uses Retracted, upward detection/LOS works, and Console-safe Animator wiring is valid.");
        }
        finally
        {
            if (wall != null) UnityEngine.Object.DestroyImmediate(wall);
            if (gameplayLight != null) UnityEngine.Object.DestroyImmediate(gameplayLight);
            if (player != null) UnityEngine.Object.DestroyImmediate(player);
            if (monster != null) UnityEngine.Object.DestroyImmediate(monster);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("[DionaeaValidation] " + message);
    }

    private static void InvokeLightTick(DionaeaAI ai, float deltaTime)
    {
        MethodInfo method = typeof(DionaeaAI).GetMethod("UpdateLightState", BindingFlags.Instance | BindingFlags.NonPublic);
        Require(method != null, "Dionaea light state tick method is missing.");
        method.Invoke(ai, new object[] { deltaTime });
    }

    private static object InvokeReceiverMethod(DionaeaLightReceiver receiver, string methodName)
    {
        MethodInfo method = typeof(DionaeaLightReceiver).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Require(method != null, "DionaeaLightReceiver method is missing: " + methodName);
        return method.Invoke(receiver, null);
    }

    private static object InvokeAiMethod(DionaeaAI ai, string methodName)
    {
        MethodInfo method = typeof(DionaeaAI).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Require(method != null, "DionaeaAI method is missing: " + methodName);
        return method.Invoke(ai, null);
    }

    private static bool HasParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        AnimatorControllerParameter[] parameters = controller.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == name && parameters[i].type == type) return true;
        }
        return false;
    }

    private static bool HasStateMotion(AnimatorController controller, string stateName, string motionName)
    {
        ChildAnimatorState[] states = controller.layers[0].stateMachine.states;
        for (int i = 0; i < states.Length; i++)
        {
            AnimatorState state = states[i].state;
            if (state.name == stateName && state.motion != null && state.motion.name == motionName) return true;
        }
        return false;
    }

    private static Motion GetStateMotion(AnimatorController controller, string stateName)
    {
        ChildAnimatorState[] states = controller.layers[0].stateMachine.states;
        for (int i = 0; i < states.Length; i++)
        {
            if (states[i].state.name == stateName) return states[i].state.motion;
        }
        return null;
    }

    private static bool HasState(AnimatorController controller, string stateName)
    {
        ChildAnimatorState[] states = controller.layers[0].stateMachine.states;
        for (int i = 0; i < states.Length; i++)
        {
            if (states[i].state.name == stateName) return true;
        }
        return false;
    }

    private static void VerifyIdleSpriteSampling(Transform visual, AnimationClip idleClip)
    {
        Require(visual != null, "Visual child is missing.");
        SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
        Require(renderer != null && renderer.sprite != null, "Visual SpriteRenderer or default Sprite is missing.");
        Sprite initialSprite = renderer.sprite;
        AnimationMode.StartAnimationMode();
        try
        {
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(visual.gameObject, idleClip, 0.3f);
            AnimationMode.EndSampling();
            Require(renderer.sprite != null && renderer.sprite != initialSprite,
                "Idle clip sampling did not change the Visual SpriteRenderer frame.");
        }
        finally
        {
            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
        }
    }
}
#endif
