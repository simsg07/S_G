using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using System.Reflection;

public static class CraneLeverPlayModeValidationUtility
{
    private const string HorizontalPath = "Assets/_Project/Prefabs/Objects/Crane/Crane_Set.prefab";
    private const string VerticalPath = "Assets/_Project/Prefabs/Objects/Crane/VerticalCrane_Set.prefab";

    private static CraneLeverSwitch horizontalLever;
    private static CraneLeverSwitch verticalLever;
    private static CraneObject horizontalCrane;
    private static VerticalCraneController3D verticalCrane;
    private static Vector3 horizontalStart;
    private static Vector3 verticalStart;
    private static float stageStarted;
    private static int stage;
    private static bool failed;
    private static int horizontalActivationCount;

    [MenuItem("Tools/_Project/Crane/Validate Lever Play Mode")]
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject horizontalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HorizontalPath);
        GameObject verticalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VerticalPath);
        if (horizontalPrefab == null || verticalPrefab == null) throw new MissingReferenceException("Crane validation prefab is missing.");

        GameObject horizontal = (GameObject)PrefabUtility.InstantiatePrefab(horizontalPrefab);
        GameObject vertical = (GameObject)PrefabUtility.InstantiatePrefab(verticalPrefab);
        horizontal.name = "Validation_HorizontalCrane";
        vertical.name = "Validation_VerticalCrane";
        horizontal.transform.position = new Vector3(-10f, 0f, 0f);
        vertical.transform.position = new Vector3(10f, 0f, 0f);

        horizontalCrane = horizontal.GetComponentInChildren<CraneObject>(true);
        verticalCrane = vertical.GetComponent<VerticalCraneController3D>();
        horizontalLever = horizontal.GetComponentInChildren<CraneLeverSwitch>(true);
        verticalLever = vertical.GetComponentInChildren<CraneLeverSwitch>(true);
        ConfigureFastMovement(horizontalCrane, "moveSpeed", 100f);
        ConfigureFastMovement(verticalCrane, "moveSpeed", 100f);
        ConfigureFastMovement(horizontalLever, "activationDelay", 3f);
        ConfigureFastMovement(verticalLever, "activationDelay", 3f);
        ConfigureFastMovement(horizontalLever, "requirePlayerInRange", false);
        ConfigureFastMovement(verticalLever, "requirePlayerInRange", false);

        stage = 0;
        failed = false;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            horizontalCrane = Object.FindFirstObjectByType<CraneObject>();
            verticalCrane = Object.FindFirstObjectByType<VerticalCraneController3D>();
            CraneLeverSwitch[] levers = Object.FindObjectsByType<CraneLeverSwitch>(FindObjectsSortMode.None);
            foreach (CraneLeverSwitch lever in levers)
            {
                SerializedObject data = new SerializedObject(lever);
                if (data.FindProperty("targetVerticalCrane").objectReferenceValue != null) verticalLever = lever;
                else horizontalLever ??= lever;
            }
            horizontalStart = horizontalCrane.transform.position;
            verticalStart = verticalCrane.transform.Find("MovingAssemblyRoot")?.position ?? Vector3.zero;
            horizontalActivationCount = 0;
            GetActivationEvent(horizontalLever).AddListener(CountHorizontalActivation);

            Collider circleSpikeCollider = CreateLaunchedCircleSpike();
            horizontalLever.SendMessage("OnTriggerEnter", circleSpikeCollider, SendMessageOptions.DontRequireReceiver);
            horizontalLever.SendMessage("OnTriggerEnter", circleSpikeCollider, SendMessageOptions.DontRequireReceiver);
            if (horizontalLever.State != CraneLeverOperationState.WaitingForActivation || horizontalActivationCount != 1)
                Fail("CircleSpike did not activate the Switch exactly once.");

            if (!verticalLever.TryActivate(SwitchActivationSource.Stone, new GameObject("Validation_Stone")))
                Fail("Stone activation was rejected.");

            ValidateBoxDoesNotActivateSwitch();
            if (horizontalCrane.IsMoving || verticalCrane.IsMoving) Fail("A Crane moved before Activation Delay elapsed.");
            if (horizontalLever.ActivateLever() || verticalLever.ActivateLever()) Fail("Repeated delay input was not blocked.");
            stageStarted = Time.realtimeSinceStartup;
            EditorApplication.update += Tick;
        }
        else if (change == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            if (Application.isBatchMode) EditorApplication.Exit(failed ? 1 : 0);
        }
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        float elapsed = Time.realtimeSinceStartup - stageStarted;
        if (stage == 0 && elapsed >= 2.5f)
        {
            if (horizontalCrane.IsMoving || verticalCrane.IsMoving) Fail("A Crane started before three scaled seconds.");
            if (Vector3.Distance(horizontalCrane.transform.position, horizontalStart) > 0.001f) Fail("Horizontal Crane position changed during delay.");
            Transform moving = verticalCrane.transform.Find("MovingAssemblyRoot");
            if (moving != null && Vector3.Distance(moving.position, verticalStart) > 0.001f) Fail("Vertical Crane position changed during delay.");
            stage = 1;
        }
        if (stage == 1 && elapsed >= 3.5f)
        {
            if (horizontalLever.State != CraneLeverOperationState.Arrived || verticalLever.State != CraneLeverOperationState.Arrived)
                Fail("Crane did not start exactly once and arrive after Activation Delay.");
            if (verticalCrane.RopeEndError > 0.005f)
                Fail($"Vertical Rope missed RopeBottomAnchor by {verticalCrane.RopeEndError:0.####}.");
            if (horizontalLever.ActivateLever() == false || verticalLever.ActivateLever() == false)
                Fail("Reverse activation after arrival was rejected.");
            if (horizontalLever.ActivateLever() || verticalLever.ActivateLever()) Fail("Reverse delay accepted duplicate input.");
            stage = 2;
            stageStarted = Time.realtimeSinceStartup;
            return;
        }
        if (stage == 2 && elapsed >= 3.5f)
        {
            if (horizontalLever.State != CraneLeverOperationState.Arrived || verticalLever.State != CraneLeverOperationState.Arrived)
                Fail("Crane did not arrive after reverse command.");
            if (Vector3.Distance(horizontalCrane.transform.position, horizontalStart) > 0.02f)
                Fail("Horizontal Crane did not return to its exact initial destination.");
            Transform moving = verticalCrane.transform.Find("MovingAssemblyRoot");
            if (moving != null && Vector3.Distance(moving.position, verticalStart) > 0.02f)
                Fail("Vertical Crane did not return to its exact initial destination.");
            if (verticalCrane.RopeEndError > 0.005f)
                Fail($"Vertical Rope missed RopeBottomAnchor after raising by {verticalCrane.RopeEndError:0.####}.");
            Finish();
        }
    }

    private static void ConfigureFastMovement(Object target, string propertyName, float value)
    {
        SerializedObject data = new SerializedObject(target);
        SerializedProperty property = data.FindProperty(propertyName);
        property.floatValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureFastMovement(Object target, string propertyName, bool value)
    {
        SerializedObject data = new SerializedObject(target);
        SerializedProperty property = data.FindProperty(propertyName);
        property.boolValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Collider CreateLaunchedCircleSpike()
    {
        GameObject source = new GameObject("Validation_CircleSpike");
        BoxCollider sourceCollider = source.AddComponent<BoxCollider>();
        source.AddComponent<Rigidbody>();
        source.AddComponent<CircleSpikeObject>();
        CircleSpikeProjectile3D projectile = source.AddComponent<CircleSpikeProjectile3D>();
        if (!projectile.ReleaseAndDrop()) Fail("Validation CircleSpike could not enter its launched state.");
        return sourceCollider;
    }

    private static void ValidateBoxDoesNotActivateSwitch()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HorizontalPath);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = "Validation_BoxExclusionCrane";
        instance.transform.position = new Vector3(0f, 10f, 0f);
        CraneLeverSwitch lever = instance.GetComponentInChildren<CraneLeverSwitch>(true);
        ConfigureFastMovement(lever, "activationDelay", 3f);

        GameObject box = new GameObject("Validation_Box");
        BoxCollider boxCollider = box.AddComponent<BoxCollider>();
        box.AddComponent<Rigidbody>();
        box.AddComponent<FallingBoxObject>();
        lever.SendMessage("OnTriggerEnter", boxCollider, SendMessageOptions.DontRequireReceiver);
        if (lever.State != CraneLeverOperationState.Idle) Fail("Box incorrectly activated the Switch.");
    }

    private static UnityEvent GetActivationEvent(CraneLeverSwitch lever)
    {
        FieldInfo field = typeof(CraneLeverSwitch).GetField("onLeverActivated", BindingFlags.Instance | BindingFlags.NonPublic);
        UnityEvent result = field != null ? field.GetValue(lever) as UnityEvent : null;
        if (result == null) Fail("Switch activation event was not available for validation.");
        return result;
    }

    private static void CountHorizontalActivation()
    {
        horizontalActivationCount++;
    }

    private static void Fail(string message)
    {
        failed = true;
        Debug.LogError($"[CraneLeverPlayModeValidation] {message}");
        Finish();
    }

    private static void Finish()
    {
        EditorApplication.update -= Tick;
        if (!failed) Debug.Log("[CraneLeverPlayModeValidation] PASS: Player/Stone/CircleSpike inputs, CircleSpike duplicate blocking, Box exclusion, delayed start, arrival, and reverse arrival validated.");
        EditorApplication.ExitPlaymode();
    }
}
