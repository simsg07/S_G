#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class CosmosLiftValidationUtility
{
    private const string PrefabPath = "Assets/_Project/Prefabs/Enemies/CosmosLift.prefab";
    private const string ReportPath = "CosmosLiftValidation.log";

    [MenuItem("Tools/Summer Camp/Cosmos Lift/Validate Prefab _F11")]
    public static void ValidatePrefab()
    {
        ClearConsole();
        List<string> results = new List<string>();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Require(prefab != null, "CosmosLift prefab exists", results);

        GameObject instance = null;
        bool completed = false;
        try
        {
            instance = prefab != null
                ? PrefabUtility.InstantiatePrefab(prefab) as GameObject
                : null;
            Require(instance != null, "Prefab can be instantiated", results);
            if (instance == null) return;

            CosmosLift3D lift = instance.GetComponent<CosmosLift3D>();
            MonsterCore monsterCore = instance.GetComponent<MonsterCore>();
            Require(lift != null, "CosmosLift3D is attached", results);
            Require(monsterCore != null, "MonsterCore classification is attached", results);
            Require(instance.GetComponentInChildren<Rigidbody2D>(true) == null, "No Rigidbody2D is used", results);
            Require(instance.GetComponentInChildren<Collider2D>(true) == null, "No Collider2D is used", results);

            Rigidbody body = instance.GetComponentInChildren<Rigidbody>(true);
            BoxCollider platform = instance.GetComponentInChildren<BoxCollider>(true);
            Require(body != null && body.isKinematic && !body.useGravity, "Bud uses a kinematic 3D Rigidbody", results);
            Require(platform != null && !platform.isTrigger, "Bud uses a solid 3D BoxCollider", results);
            Require(instance.GetComponentInChildren<PlayerHealth3D>(true) == null, "No health component can kill the lift", results);

            if (lift != null)
            {
                ValidateMotionCycle(lift, results);
            }
            ValidateConsoleErrorCount(results);
            completed = true;
        }
        finally
        {
            if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
            string report = string.Join(Environment.NewLine, results);
            File.WriteAllText(ReportPath, report + Environment.NewLine);
            if (completed) Debug.Log("[CosmosLiftValidation] PASS\n" + report);
        }
    }

    public static void ValidatePrefabBatch()
    {
        ValidatePrefab();
    }

    private static void ValidateMotionCycle(CosmosLift3D lift, List<string> results)
    {
        MethodInfo awake = GetPrivateMethod("Awake");
        MethodInfo updateState = GetPrivateMethod("UpdateState");
        MethodInfo applyPose = GetPrivateMethod("ApplyPose");
        Require(awake != null && updateState != null && applyPose != null, "Validation can access the state machine", results);
        if (awake == null || updateState == null || applyPose == null) return;

        awake.Invoke(lift, null);
        SerializedObject serializedLift = new SerializedObject(lift);
        Transform bud = serializedLift.FindProperty("budPlatform").objectReferenceValue as Transform;
        Collider collider = serializedLift.FindProperty("platformCollider").objectReferenceValue as Collider;
        float height = serializedLift.FindProperty("maximumHeight").floatValue;
        float riseDuration = serializedLift.FindProperty("riseDuration").floatValue;
        float retractDuration = serializedLift.FindProperty("retractDuration").floatValue;
        float holdDuration = serializedLift.FindProperty("darknessHoldDuration").floatValue;
        Vector3 start = bud != null ? bud.localPosition : Vector3.zero;

        Require(lift.CurrentState == CosmosLift3D.LiftState.Retracted && Mathf.Approximately(lift.GrowthProgress, 0f),
            "Default state is fully retracted", results);
        Require(collider != null && !collider.enabled, "Nested bud is not an active platform", results);

        lift.SetLightReceived(true);
        updateState.Invoke(lift, new object[] { riseDuration + 0.01f });
        applyPose.Invoke(lift, new object[] { lift.GrowthProgress, true });
        Require(lift.CurrentState == CosmosLift3D.LiftState.Raised && Mathf.Approximately(lift.GrowthProgress, 1f),
            "Light raises the bud completely", results);
        Require(bud != null && Mathf.Abs((bud.localPosition.y - start.y) - height) < 0.01f,
            "Raised bud reaches Inspector maximumHeight", results);
        Require(collider != null && collider.enabled, "Raised bud is a solid platform", results);

        lift.SetLightReceived(false);
        updateState.Invoke(lift, new object[] { Mathf.Max(0.001f, holdDuration * 0.5f) });
        Require(lift.CurrentState == CosmosLift3D.LiftState.Holding && Mathf.Approximately(lift.GrowthProgress, 1f),
            "Darkness initially holds the current height", results);
        updateState.Invoke(lift, new object[] { holdDuration + 0.01f });
        Require(lift.CurrentState == CosmosLift3D.LiftState.Retracting, "Hold expiry starts reverse motion", results);
        updateState.Invoke(lift, new object[] { retractDuration + 0.01f });
        applyPose.Invoke(lift, new object[] { lift.GrowthProgress, true });
        Require(lift.CurrentState == CosmosLift3D.LiftState.Retracted && Mathf.Approximately(lift.GrowthProgress, 0f),
            "Reverse motion returns to the nested state", results);
        Require(bud != null && Vector3.Distance(bud.localPosition, start) < 0.01f,
            "Bud returns to its exact initial position", results);
        Require(collider != null && !collider.enabled, "Platform disables after full retraction", results);
    }

    private static MethodInfo GetPrivateMethod(string name)
    {
        return typeof(CosmosLift3D).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
    }

    private static void ValidateConsoleErrorCount(List<string> results)
    {
        Type logEntriesType = typeof(Editor).Assembly.GetType("UnityEditor.LogEntries");
        MethodInfo getCounts = logEntriesType?.GetMethod("GetCountsByType",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Require(getCounts != null, "Unity Console counts are readable", results);
        object[] counts = { 0, 0, 0 };
        getCounts.Invoke(null, counts);
        int errorCount = (int)counts[0];
        Require(errorCount == 0, "Unity Console Error count is 0", results);
    }

    private static void ClearConsole()
    {
        Type logEntriesType = typeof(Editor).Assembly.GetType("UnityEditor.LogEntries");
        MethodInfo clear = logEntriesType?.GetMethod("Clear",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        clear?.Invoke(null, null);
    }

    private static void Require(bool condition, string description, List<string> results)
    {
        if (!condition) throw new InvalidOperationException("[CosmosLiftValidation] FAILED: " + description);
        results.Add("PASS: " + description);
    }
}
#endif
