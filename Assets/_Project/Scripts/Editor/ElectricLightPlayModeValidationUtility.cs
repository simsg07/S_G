#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ElectricLightPlayModeValidationUtility
{
    private const string PhaseKey = "ElectricLightPlayModeValidation.Phase";
    private static int playFrames;
    private static string RequestPath => Path.Combine(
        Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty,
        "ElectricLightValidation.request");

    static ElectricLightPlayModeValidationUtility()
    {
        if (File.Exists(RequestPath) || SessionState.GetString(PhaseKey, string.Empty) == "Entering")
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }
    }

    [MenuItem("Tools/Project/Play Mode Validate Electric Light")]
    public static void RequestValidation()
    {
        File.WriteAllText(RequestPath, "run");
        SessionState.SetString(PhaseKey, string.Empty);
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            return;
        }

        string phase = SessionState.GetString(PhaseKey, string.Empty);
        if (!EditorApplication.isPlaying)
        {
            if (phase == "Finished")
            {
                CleanupRequest();
                EditorApplication.update -= Tick;
                return;
            }

            if (phase == "Entering")
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    return;
                }

                SessionState.SetString(PhaseKey, "Aborted");
                CleanupRequest();
                EditorApplication.update -= Tick;
                Debug.LogWarning("[ElectricLightPlayModeValidation] Aborted: Play Mode did not start. No retry will be attempted.");
                return;
            }

            SessionState.SetString(PhaseKey, "Entering");
            EditorApplication.EnterPlaymode();
            return;
        }

        if (phase != "Entering")
        {
            return;
        }

        playFrames++;
        if (playFrames < 3)
        {
            return;
        }

        try
        {
            RunPlayModeValidation();
            Debug.Log("[ElectricLightPlayModeValidation] PASS: ACTIVE light compatibility, World A-only presence, Dionaea response, EyeballFly targeting/damage, and DESTROYED invalidation verified in Play Mode.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            SessionState.SetString(PhaseKey, "Finished");
            CleanupRequest();
            EditorApplication.ExitPlaymode();
        }
    }

    private static void RunPlayModeValidation()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ElectricLightPrefabUtility.PrefabPath);
        Require(prefab != null, "Electric_Light prefab is missing.");

        GameObject lightObject = null;
        GameObject dionaeaObject = null;
        GameObject eyeballObject = null;
        try
        {
            lightObject = UnityEngine.Object.Instantiate(prefab, new Vector3(1000f, 1000f, 0f), Quaternion.identity);
            lightObject.name = "Electric_Light_PlayModeValidation";
            ElectricLightObject3D electricLight = lightObject.GetComponent<ElectricLightObject3D>();
            Light gameplayLight = lightObject.GetComponentInChildren<Light>(true);
            Collider damageCollider = lightObject.GetComponent<Collider>();
            WorldPresence worldPresence = lightObject.GetComponent<WorldPresence>();

            Require(electricLight != null && electricLight.CurrentState == ElectricLightState.ACTIVE,
                "Electric light did not start ACTIVE.");
            Require(electricLight.IsProvidingLight && gameplayLight != null && gameplayLight.enabled,
                "ACTIVE state does not provide an enabled gameplay Light.");
            Require(Mathf.Abs(gameplayLight.range - 4f) <= 0.001f && Mathf.Abs(gameplayLight.intensity - 7.5f) <= 0.001f,
                "Gameplay Light does not match Electric cone range/intensity defaults.");

            worldPresence.SetPresenceEnabled(false);
            Require(!gameplayLight.enabled && !damageCollider.enabled,
                "World B absence did not disable light output and collision.");
            worldPresence.SetPresenceEnabled(true);
            Require(gameplayLight.enabled && damageCollider.enabled && electricLight.CurrentState == ElectricLightState.ACTIVE,
                "Returning to Current World did not restore ACTIVE state.");

            GameObject dionaeaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemies/Dionaea.prefab");
            Require(dionaeaPrefab != null, "Dionaea prefab is missing.");
            dionaeaObject = UnityEngine.Object.Instantiate(dionaeaPrefab, new Vector3(1000f, 998f, 0f), Quaternion.identity);
            DionaeaLightReceiver receiver = dionaeaObject.GetComponentInChildren<DionaeaLightReceiver>(true);
            DionaeaAI dionaeaAI = dionaeaObject.GetComponent<DionaeaAI>();
            Require(receiver != null && dionaeaAI != null, "Dionaea light receiver is incomplete.");
            Invoke(receiver, "RefreshSceneLights");
            Require((bool)Invoke(receiver, "DetectLightOverlap"), "Dionaea did not receive Electric_Light.");
            receiver.Configure(dionaeaAI, 0f);
            Invoke(receiver, "Update");
            Require(dionaeaAI.IsLit, "Dionaea did not enter its existing lit response.");

            GameObject eyeballPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemies/EyeballFly.prefab");
            Require(eyeballPrefab != null, "EyeballFly prefab is missing.");
            eyeballObject = UnityEngine.Object.Instantiate(eyeballPrefab, new Vector3(1000f, 997f, 0f), Quaternion.identity);
            EyeballFlyAI eyeballAI = eyeballObject.GetComponent<EyeballFlyAI>();
            Require(eyeballAI != null, "EyeballFlyAI is missing.");
            SetField(typeof(MonsterAIBase), eyeballAI, "moveAnchorPosition", eyeballObject.transform.position);
            SetField(typeof(EyeballFlyAI), eyeballAI, "nextLightCandidateRefreshTime", 0f);
            Transform selectedLight = Invoke(eyeballAI, "FindNearestVisibleLight") as Transform;
            Require(selectedLight == lightObject.transform, "EyeballFly did not select Electric_Light through the existing light target path.");

            DamageInfo genericDamage = new DamageInfo(99, eyeballObject, eyeballObject, lightObject.transform.position,
                Vector3.left, DamageType.Generic, HitSourceType.Generic);
            electricLight.TakeDamage(genericDamage);
            Require(electricLight.CurrentHP == electricLight.MaxHP, "Non-Eyeball damage was accepted.");

            for (int hit = 0; hit < electricLight.MaxHP; hit++)
            {
                SetField(typeof(EyeballFlyAI), eyeballAI, "currentState", EyeballFlyAI.EyeballFlyState.DASH_ATTACK);
                SetField(typeof(EyeballFlyAI), eyeballAI, "dashDamageEnabled", true);
                HashSet<int> damagedIds = GetField<HashSet<int>>(typeof(EyeballFlyAI), eyeballAI, "damagedTargetIds");
                damagedIds.Clear();
                bool applied = (bool)Invoke(eyeballAI, "TryDamageDashTarget", damageCollider, lightObject.transform.position);
                Require(applied, "EyeballFly attack did not deliver damage at hit " + (hit + 1) + ".");
            }

            Require(electricLight.CurrentState == ElectricLightState.DESTROYED && electricLight.CurrentHP == 0,
                "HP zero did not enter DESTROYED.");
            Require(!gameplayLight.enabled && !damageCollider.enabled && !electricLight.IsProvidingLight,
                "DESTROYED did not immediately remove light output/collision.");
            Invoke(receiver, "RefreshSceneLights");
            Require(!(bool)Invoke(receiver, "DetectLightOverlap"), "Dionaea still receives destroyed Electric_Light.");
            SetField(typeof(EyeballFlyAI), eyeballAI, "nextLightCandidateRefreshTime", 0f);
            Require(Invoke(eyeballAI, "FindNearestVisibleLight") == null,
                "EyeballFly still treats destroyed Electric_Light as a valid target.");

            worldPresence.SetPresenceEnabled(false);
            worldPresence.SetPresenceEnabled(true);
            Require(electricLight.CurrentState == ElectricLightState.DESTROYED && !gameplayLight.enabled,
                "World switching incorrectly revived DESTROYED Electric_Light.");
        }
        finally
        {
            if (eyeballObject != null) UnityEngine.Object.DestroyImmediate(eyeballObject);
            if (dionaeaObject != null) UnityEngine.Object.DestroyImmediate(dionaeaObject);
            if (lightObject != null) UnityEngine.Object.DestroyImmediate(lightObject);
        }
    }

    private static object Invoke(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Require(method != null, target.GetType().Name + "." + methodName + " is missing.");
        return method.Invoke(target, arguments);
    }

    private static void SetField(Type declaringType, object target, string fieldName, object value)
    {
        FieldInfo field = declaringType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Require(field != null, declaringType.Name + "." + fieldName + " is missing.");
        field.SetValue(target, value);
    }

    private static T GetField<T>(Type declaringType, object target, string fieldName)
    {
        FieldInfo field = declaringType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Require(field != null, declaringType.Name + "." + fieldName + " is missing.");
        return (T)field.GetValue(target);
    }

    private static void CleanupRequest()
    {
        if (File.Exists(RequestPath)) File.Delete(RequestPath);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("[ElectricLightPlayModeValidation] " + message);
    }
}
#endif
