using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class MainMenuPlayModeStarter
{
    private const string MainMenuScenePath = "Assets/_Project/Scenes/Title/MainMenu.unity";

    static MainMenuPlayModeStarter()
    {
        EditorApplication.delayCall += SetPlayModeStartScene;
    }

    private static void SetPlayModeStartScene()
    {
        SceneAsset mainMenuScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuScenePath);
        if (mainMenuScene == null)
        {
            return;
        }

        if (EditorSceneManager.playModeStartScene != mainMenuScene)
        {
            EditorSceneManager.playModeStartScene = mainMenuScene;
        }
    }
}
