#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.U2D.PSD;
using UnityEngine;

public static class ProjectPsdImporterUtility
{
    private const string PsdRoot = "Assets/_Project/Art/PSD";

    [MenuItem("S_G/Art/Apply PSD Importer To PSD Source Files", false, 100)]
    public static void ApplyPsdImporterToProjectPsdFiles()
    {
        string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { PsdRoot });
        int changedCount = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!IsProjectPsd(path))
            {
                continue;
            }

            if (EnsurePsdImporter(path))
            {
                changedCount++;
            }
        }

        if (changedCount > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log($"PSD Importer checked {changedCount} PSD source file(s) under {PsdRoot}.");
    }

    [InitializeOnLoadMethod]
    private static void ApplyAfterEditorLoad()
    {
        EditorApplication.delayCall += ApplyPsdImporterToProjectPsdFiles;
    }

    private static bool EnsurePsdImporter(string path)
    {
        AssetImporter importer = AssetImporter.GetAtPath(path);
        if (importer is PSDImporter)
        {
            return false;
        }

        AssetDatabase.SetImporterOverride<PSDImporter>(path);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        return true;
    }

    private static bool IsProjectPsd(string path)
    {
        return !string.IsNullOrEmpty(path)
            && path.StartsWith(PsdRoot, System.StringComparison.OrdinalIgnoreCase)
            && string.Equals(Path.GetExtension(path), ".psd", System.StringComparison.OrdinalIgnoreCase);
    }
}
#endif
