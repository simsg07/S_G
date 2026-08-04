using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CodexRopeCameraPackageExporter
{
    private const string PackageName = "Codex_RopeSets_CameraOptimization_2026-08-04.unitypackage";

    private static readonly string[] AssetsToExport =
    {
        "Assets/_Project/Art/Objects/Dynamic/Crane/crane.psd",
        "Assets/_Project/Art/Sprites/Objects/CircleSpike.png",
        "Assets/_Project/Art/Sprites/Objects/WarningBox.png",
        "Assets/_Project/Data/World/CameraWorldSwitchSettings.asset",
        "Assets/_Project/Prefabs/Objects/Gravity/CircleSpike.prefab",
        "Assets/_Project/Prefabs/Objects/Gravity/FallingBox.prefab",
        "Assets/_Project/Prefabs/Objects/RopeSets/Vine_Box_Set.prefab",
        "Assets/_Project/Prefabs/Objects/RopeSets/Vine_CircleSpike_Set.prefab",
        "Assets/_Project/Prefabs/Objects/RopeSets/Wire_Box_Set.prefab",
        "Assets/_Project/Prefabs/Objects/RopeSets/Wire_CircleSpike_Set.prefab",
        "Assets/_Project/Scripts/Camera/CameraWorldSwitcher.cs",
        "Assets/_Project/Scripts/Editor/CameraToggleLeakValidationUtility.cs",
        "Assets/_Project/Scripts/Editor/RopeLengthController3DEditor.cs",
        "Assets/_Project/Scripts/Editor/RopeSetPrefabSetupUtility.cs",
        "Assets/_Project/Scripts/Interaction/CameraHighlightSharedResources3D.cs",
        "Assets/_Project/Scripts/Interaction/CameraMarkState3D.cs",
        "Assets/_Project/Scripts/Interaction/CameraObjectTag3D.cs",
        "Assets/_Project/Scripts/Interaction/CameraTargetHighlightManager3D.cs",
        "Assets/_Project/Scripts/Objects/Common/Physics/GravityObject3D.cs",
        "Assets/_Project/Scripts/Objects/Common/Trigger/ConnectedObjectLink.cs",
        "Assets/_Project/Scripts/Objects/Gravity/CircleSpikeObject.cs",
        "Assets/_Project/Scripts/Objects/Gravity/CircleSpikeProjectile3D.cs",
        "Assets/_Project/Scripts/Objects/Rope/RopeLengthController3D.cs",
        "Assets/_Project/Scripts/Player/CameraAbilitySystem3D.cs",
        "Assets/_Project/Scripts/World/Shutter/ShutterTarget3D.cs"
    };

    public static void Export()
    {
        ValidateAssetList();
        ValidateRopePrefabs();
        ValidateCameraManagerUniqueness();

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string output = Path.GetFullPath(Path.Combine(projectRoot, "..", "CodexPackages", PackageName));
        if (File.Exists(output)) throw new InvalidOperationException("Refusing to overwrite: " + output);

        AssetDatabase.ExportPackage(AssetsToExport, output, ExportPackageOptions.Recurse);
        Debug.Log($"CODEX_EXPORT_OK path={output} assets={AssetsToExport.Length}");
    }

    private static void ValidateAssetList()
    {
        foreach (string path in AssetsToExport)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) == null)
                throw new InvalidOperationException("Missing export asset: " + path);
            if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("/Deprecated/") || path.Contains("CraneXY") ||
                path.Contains("VerticalCrane") || path.Contains("/Packages/"))
                throw new InvalidOperationException("Excluded scope entered export list: " + path);
        }
    }

    private static void ValidateRopePrefabs()
    {
        string[] prefabs = AssetsToExport.Where(p => p.Contains("/RopeSets/") && p.EndsWith(".prefab")).ToArray();
        if (prefabs.Length != 4) throw new InvalidOperationException("Expected exactly four RopeSets prefabs.");
        foreach (string path in prefabs)
        {
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root);
            if (missing != 0) throw new InvalidOperationException($"Missing scripts ({missing}): {path}");

            List<string> spritePaths = root.GetComponentsInChildren<SpriteRenderer>(true)
                .Where(r => r.sprite != null).Select(r => AssetDatabase.GetAssetPath(r.sprite)).Distinct().ToList();
            bool isBox = path.Contains("_Box_");
            string expected = isBox
                ? "Assets/_Project/Art/Sprites/Objects/WarningBox.png"
                : "Assets/_Project/Art/Sprites/Objects/CircleSpike.png";
            if (!spritePaths.Contains(expected))
                throw new InvalidOperationException($"Expected visual sprite is not connected in {path}: {expected}");
            if (!spritePaths.Contains("Assets/_Project/Art/Objects/Dynamic/Crane/crane.psd"))
                throw new InvalidOperationException("Shared crane rope sprite is not connected in " + path);
        }
    }

    private static void ValidateCameraManagerUniqueness()
    {
        string[] guids = AssetDatabase.FindAssets("CameraTargetHighlightManager3D t:MonoScript");
        if (guids.Length != 1)
            throw new InvalidOperationException("Expected one CameraTargetHighlightManager3D script, found " + guids.Length);
    }
}
