#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class CraneMagnetPrefabUpgradeUtility
{
    private const string CranePath = "Assets/_Project/Prefabs/Objects/Crane/Crane_XY_ObjectMover_Set.prefab";
    private static readonly string[] CarryablePaths =
    {
        "Assets/_Project/Prefabs/Objects/Gravity/FallingBox.prefab",
        "Assets/_Project/Prefabs/Objects/Gravity/Stone.prefab"
    };

    [MenuItem("Tools/_Project/Crane/Upgrade XY Crane Magnet")]
    public static void Upgrade()
    {
        UpgradeCrane();
        foreach (string path in CarryablePaths) AddCarryable(path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Validate();
        Debug.Log("[CraneMagnetUpgrade] PASS: existing XY prefab upgraded in place; FallingBox and Stone are magnet-enabled.");
    }

    private static void UpgradeCrane()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(CranePath);
        if (root == null) throw new MissingReferenceException(CranePath);
        try
        {
            Transform vertical = Require(root.transform, "HorizontalMovingRoot/VerticalMovingRoot");
            Transform carryAnchor = Require(vertical, "CarryAnchor");
            Transform visualRoot = EnsureChild(vertical, "MagnetVisualRoot", Vector3.zero);
            RemoveAllChildren(visualRoot);
            Transform magnetAnchor = EnsureChild(vertical, "MagnetAnchor", new Vector3(0f, -0.74f, 0f));
            Transform detectionRoot = EnsureChild(vertical, "MagnetDetectionArea", magnetAnchor.localPosition);
            BoxCollider detectionCollider = GetOrAdd<BoxCollider>(detectionRoot.gameObject);
            detectionCollider.isTrigger = true;
            detectionCollider.center = new Vector3(0f, -2f, 0f);
            detectionCollider.size = new Vector3(2f, 4f, 0.8f);
            CraneMagnetDetectionArea3D detection = GetOrAdd<CraneMagnetDetectionArea3D>(detectionRoot.gameObject);

            Transform controlRoot = EnsureChild(root.transform, "MagnetControlTrigger", Vector3.zero);
            BoxCollider controlCollider = GetOrAdd<BoxCollider>(controlRoot.gameObject);
            controlCollider.isTrigger = true;
            controlCollider.center = new Vector3(0f, -1f, 0f);
            controlCollider.size = new Vector3(12f, 4f, 1.5f);
            CraneMagnetControlTrigger3D control = GetOrAdd<CraneMagnetControlTrigger3D>(controlRoot.gameObject);
            Set(control, "playerLayerMask", 1 << 13);

            CraneMagnetController3D magnet = GetOrAdd<CraneMagnetController3D>(root);
            Set(magnet, "magnetEnabledOnStart", false);
            Set(magnet, "magnetToggleKey", (int)KeyCode.G);
            Set(magnet, "requirePlayerInControlRange", true);
            Set(magnet, "magnetControlTrigger", control);
            Set(magnet, "releaseOnDisable", true);
            Set(magnet, "releaseOnCraneDisable", true);
            Set(magnet, "releaseOnSceneChange", true);
            Set(magnet, "allowMagnetToggleWhileMoving", true);
            Set(magnet, "craneController", root.GetComponent<CraneXYController3D>());
            Set(magnet, "magnetAnchor", magnetAnchor);
            Set(magnet, "magnetDetectionArea", detection);
            Set(magnet, "magnetLayerMask", 1);
            Set(magnet, "magnetRange", 4f);
            Set(magnet, "magnetWidth", 2f);
            Set(magnet, "requireLineOfSight", true);
            Set(magnet, "magnetObstacleMask", 7936);
            Set(magnet, "maximumCarryMass", 100f);
            Set(magnet, "maximumCarryTargets", 1);
            Set(magnet, "attractionSpeed", 3f);
            Set(magnet, "attractionAcceleration", 8f);
            Set(magnet, "maximumAttractionSpeed", 5f);
            Set(magnet, "attachDistance", 0.15f);
            Set(magnet, "carryAnchor", carryAnchor);
            Set(magnet, "magnetVisualRenderer", null);
            Set(magnet, "magnetSprite", null);

            CraneXYController3D controller = root.GetComponent<CraneXYController3D>();
            Set(controller, "carryAreaCollider", detectionCollider);
            Remove(root.transform, "FixedRoot/RailVisual");
            Remove(root.transform, "HorizontalMovingRoot/CraneBodyVisual");
            Remove(vertical, "HookVisual");
            Remove(vertical, "CarryPlatformVisual");
            Remove(vertical, "CarryArea");
            Remove(root.transform, "HorizontalLeverRoot/HorizontalLeverVisual");
            Remove(root.transform, "VerticalLeverRoot/VerticalLeverVisual");

            PrefabUtility.SaveAsPrefabAsset(root, CranePath, out bool success);
            if (!success) throw new InvalidOperationException("Could not save " + CranePath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void AddCarryable(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        if (root == null) throw new MissingReferenceException(path);
        try
        {
            MagneticCarryable3D carryable = GetOrAdd<MagneticCarryable3D>(root);
            Set(carryable, "canBeMovedByMagnet", true);
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
            if (!success) throw new InvalidOperationException("Could not save " + path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [MenuItem("Tools/_Project/Crane/Validate XY Crane Magnet")]
    public static void Validate()
    {
        GameObject crane = AssetDatabase.LoadAssetAtPath<GameObject>(CranePath);
        if (crane == null || crane.GetComponent<CraneMagnetController3D>() == null) throw new MissingComponentException("CraneMagnetController3D");
        string[] paths = { "HorizontalMovingRoot/VerticalMovingRoot/MagnetVisualRoot", "HorizontalMovingRoot/VerticalMovingRoot/MagnetAnchor", "HorizontalMovingRoot/VerticalMovingRoot/MagnetDetectionArea", "HorizontalMovingRoot/VerticalMovingRoot/CarryAnchor", "MagnetControlTrigger" };
        foreach (string path in paths) if (crane.transform.Find(path) == null) throw new MissingReferenceException(path);
        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(crane) != 0) throw new MissingComponentException("XY crane has Missing Script components.");
        Transform visualRoot = crane.transform.Find("HorizontalMovingRoot/VerticalMovingRoot/MagnetVisualRoot");
        if (visualRoot.childCount != 0 || visualRoot.GetComponentsInChildren<Renderer>(true).Length != 0)
            throw new InvalidOperationException("MagnetVisualRoot must remain empty until dedicated art exists.");
        string[] forbidden = { "FixedRoot/RailVisual", "HorizontalMovingRoot/CraneBodyVisual", "HorizontalMovingRoot/VerticalMovingRoot/HookVisual", "HorizontalMovingRoot/VerticalMovingRoot/CarryPlatformVisual", "HorizontalMovingRoot/VerticalMovingRoot/CarryArea", "HorizontalLeverRoot/HorizontalLeverVisual", "VerticalLeverRoot/VerticalLeverVisual" };
        foreach (string path in forbidden) if (crane.transform.Find(path) != null) throw new InvalidOperationException("Forbidden crane/platform visual remains: " + path);
        SpriteRenderer[] renderers = crane.GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers.Length != 8) throw new InvalidOperationException($"Only 8 rope SpriteRenderers may remain, found {renderers.Length}.");
        foreach (SpriteRenderer renderer in renderers)
            if (!renderer.transform.IsChildOf(crane.transform.Find("HorizontalMovingRoot/RopeVisualRoot")))
                throw new InvalidOperationException("Non-rope runtime SpriteRenderer remains: " + renderer.name);
        foreach (string path in CarryablePaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            MagneticCarryable3D c = prefab != null ? prefab.GetComponent<MagneticCarryable3D>() : null;
            if (c == null || !c.CanBeMovedByMagnet) throw new MissingComponentException(path + " is not magnet-enabled.");
        }
    }

    private static Transform Require(Transform root, string path) => root.Find(path) ?? throw new MissingReferenceException(path);
    private static Transform EnsureChild(Transform parent, string name, Vector3 localPosition)
    {
        Transform child = parent.Find(name);
        if (child == null) { child = new GameObject(name).transform; child.SetParent(parent, false); }
        child.localPosition = localPosition; return child;
    }
    private static void Remove(Transform root, string path)
    {
        Transform target = root.Find(path);
        if (target != null) UnityEngine.Object.DestroyImmediate(target.gameObject);
    }
    private static void RemoveAllChildren(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(root.GetChild(i).gameObject);
    }
    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }
    private static SerializedProperty P(UnityEngine.Object target, string name) { SerializedProperty p = new SerializedObject(target).FindProperty(name); if (p == null) throw new MissingFieldException(target.GetType().Name, name); return p; }
    private static void Set(UnityEngine.Object target, string name, UnityEngine.Object value) { SerializedObject d=new SerializedObject(target); d.FindProperty(name).objectReferenceValue=value; d.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Set(UnityEngine.Object target, string name, bool value) { SerializedObject d=new SerializedObject(target); d.FindProperty(name).boolValue=value; d.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Set(UnityEngine.Object target, string name, int value) { SerializedObject d=new SerializedObject(target); d.FindProperty(name).intValue=value; d.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Set(UnityEngine.Object target, string name, float value) { SerializedObject d=new SerializedObject(target); d.FindProperty(name).floatValue=value; d.ApplyModifiedPropertiesWithoutUndo(); }
}
#endif
