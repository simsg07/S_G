#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class ElectricLightPrefabUtility
{
    public const string PrefabPath = "Assets/_Project/Prefabs/Objects/Light/Electric_Light.prefab";

    [MenuItem("CONTEXT/ElectricLightObject3D/Setup Electric Light Cone")]
    private static void SetupCone(MenuCommand command)
    {
        ElectricLightObject3D electricLight = command.context as ElectricLightObject3D;
        if (electricLight == null) return;

        SerializedObject settings = new SerializedObject(electricLight);
        SerializedProperty originProperty = settings.FindProperty("coneOrigin");
        Transform origin = originProperty.objectReferenceValue as Transform;
        if (origin == null) origin = electricLight.transform;

        Transform cone = origin.Find("Electric Light Cone");
        if (cone == null)
        {
            GameObject coneObject = new GameObject("Electric Light Cone");
            Undo.RegisterCreatedObjectUndo(coneObject, "Setup Electric Light Cone");
            cone = coneObject.transform;
            cone.SetParent(origin, false);
            cone.localPosition = new Vector3(0f, 0f, 0.05f);
        }

        if (cone.GetComponent<MeshFilter>() == null)
            Undo.AddComponent<MeshFilter>(cone.gameObject);
        if (cone.GetComponent<MeshRenderer>() == null)
            Undo.AddComponent<MeshRenderer>(cone.gameObject);

        settings.Update();
        settings.FindProperty("coneVisual").objectReferenceValue = cone;
        settings.ApplyModifiedProperties();
        electricLight.RefreshEditorConeVisual();
        EditorUtility.SetDirty(electricLight);
    }

    [MenuItem("Tools/Project/Build Electric Light")]
    public static void BuildPrefab()
    {
        GameObject root = new GameObject("Electric_Light");
        try
        {
            root.tag = CameraTagUtility3D.LightTag;
            root.layer = LayerMask.NameToLayer("Default");

            BoxCollider damageCollider = root.AddComponent<BoxCollider>();
            damageCollider.isTrigger = false;
            damageCollider.center = Vector3.zero;
            damageCollider.size = new Vector3(0.8f, 1.2f, 0.6f);

            ElectricLightObject3D electricLight = root.AddComponent<ElectricLightObject3D>();
            WorldPresence worldPresence = root.AddComponent<WorldPresence>();

            GameObject lightObject = new GameObject("GameplayLight", typeof(Light));
            lightObject.transform.SetParent(root.transform, false);
            Light gameplayLight = lightObject.GetComponent<Light>();
            gameplayLight.type = LightType.Point;
            gameplayLight.range = 4f;
            gameplayLight.intensity = 7.5f;
            gameplayLight.color = new Color(0.78f, 0.95f, 1f, 1f);
            gameplayLight.shadows = LightShadows.None;

            GameObject coneOriginObject = new GameObject("ConeOrigin");
            coneOriginObject.transform.SetParent(root.transform, false);
            coneOriginObject.transform.localPosition = new Vector3(0f, -0.6f, 0f);
            GameObject coneVisualObject = new GameObject("Electric Light Cone", typeof(MeshFilter), typeof(MeshRenderer));
            coneVisualObject.transform.SetParent(coneOriginObject.transform, false);
            coneVisualObject.transform.localPosition = new Vector3(0f, 0f, 0.05f);

            SerializedObject lightSettings = new SerializedObject(electricLight);
            lightSettings.FindProperty("gameplayLight").objectReferenceValue = gameplayLight;
            lightSettings.FindProperty("useDirectionalCone").boolValue = true;
            lightSettings.FindProperty("coneOrigin").objectReferenceValue = coneOriginObject.transform;
            lightSettings.FindProperty("directionTransform").objectReferenceValue = coneOriginObject.transform;
            lightSettings.FindProperty("coneVisual").objectReferenceValue = coneVisualObject.transform;
            lightSettings.FindProperty("lightRange").floatValue = 4f;
            lightSettings.FindProperty("lightAngle").floatValue = 90f;
            lightSettings.FindProperty("coneColor").colorValue = new Color(1f, 0.9f, 0.52f, 0.16f);
            lightSettings.FindProperty("coneSegments").intValue = 32;
            lightSettings.FindProperty("gameplayIntensity").floatValue = 7.5f;
            lightSettings.FindProperty("gameplayColor").colorValue = new Color(0.78f, 0.95f, 1f, 1f);
            lightSettings.FindProperty("maxHP").intValue = 3;
            SerializedProperty colliders = lightSettings.FindProperty("damageColliders");
            colliders.arraySize = 1;
            colliders.GetArrayElementAtIndex(0).objectReferenceValue = damageCollider;
            lightSettings.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject presenceSettings = new SerializedObject(worldPresence);
            presenceSettings.FindProperty("presenceMode").enumValueIndex = (int)WorldPresenceMode.WorldAOnly;
            presenceSettings.FindProperty("applyOnStart").boolValue = true;
            presenceSettings.FindProperty("autoCollectRenderers").boolValue = true;
            presenceSettings.FindProperty("autoCollectColliders").boolValue = true;
            presenceSettings.FindProperty("autoCollectRigidbodies").boolValue = true;
            presenceSettings.FindProperty("disableControlledBehavioursWhenAbsent").boolValue = true;
            SerializedProperty controlledBehaviours = presenceSettings.FindProperty("controlledBehaviours");
            controlledBehaviours.arraySize = 1;
            controlledBehaviours.GetArrayElementAtIndex(0).objectReferenceValue = electricLight;
            presenceSettings.ApplyModifiedPropertiesWithoutUndo();
            worldPresence.RefreshReferences();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Require(prefab != null, "Prefab save failed.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidatePrefab();
            Debug.Log("[ElectricLight] Prefab built and validated: " + PrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [MenuItem("Tools/Project/Validate Electric Light")]
    public static void ValidatePrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Require(prefab != null, "Prefab is missing.");
        Require(prefab.CompareTag(CameraTagUtility3D.LightTag), "Root must use the existing light tag.");

        ElectricLightObject3D electricLight = prefab.GetComponent<ElectricLightObject3D>();
        Require(electricLight != null, "ElectricLightObject3D is missing.");
        Require(ReferenceEquals(prefab.GetComponent<IDamageable>(), electricLight),
            "Root IDamageable contract is not connected.");
        Require(prefab.GetComponent<ITriggerableObject>() == null, "Electric light must not be Switch-controllable.");

        Light gameplayLight = prefab.GetComponentInChildren<Light>(true);
        Require(gameplayLight != null && gameplayLight.type == LightType.Point, "Gameplay Light is missing.");
        Require(Mathf.Abs(electricLight.LightRange - 4f) <= 0.001f, "Directional Light Range is not 4.");
        Require(Mathf.Abs(electricLight.LightAngle - 90f) <= 0.001f, "Directional Light Angle is not 90 degrees.");
        Require(electricLight.ConeOrigin != prefab.transform, "ConeOrigin must be a dedicated child Transform.");
        Transform coneVisual = electricLight.ConeOrigin.Find("Electric Light Cone");
        Require(coneVisual != null && coneVisual.GetComponent<MeshFilter>() != null &&
                coneVisual.GetComponent<MeshRenderer>() != null,
            "Electric Light Cone must contain its prebuilt MeshFilter and MeshRenderer.");
        Require(Vector3.Dot(electricLight.ConeDirection, -prefab.transform.up) > 0.999f,
            "Default cone direction must be Local Down.");
        Require(Mathf.Abs(gameplayLight.intensity - 7.5f) <= 0.001f, "Player Light-compatible intensity is not 7.5.");

        WorldPresence presence = prefab.GetComponent<WorldPresence>();
        Require(presence != null && presence.PresenceMode == WorldPresenceMode.WorldAOnly,
            "Electric light must exist only in World A (Current).");

        Collider damageCollider = prefab.GetComponent<Collider>();
        Require(damageCollider != null && !damageCollider.isTrigger, "EyeballFly damage Collider is missing.");
        Require(electricLight.MaxHP == 3, "Temporary Max HP must serialize as 3.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException("[ElectricLight] " + message);
        }
    }
}
#endif
