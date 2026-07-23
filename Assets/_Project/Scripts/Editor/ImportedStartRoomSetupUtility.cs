using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class ImportedStartRoomSetupUtility
{
    private const string ScenePath =
        "Assets/_Project/Scenes/Stages/ImportedSummerCampMap/Start_Room.unity";

    [MenuItem("Tools/Project/Repair Imported Start Room Gameplay Setup")]
    public static void RepairFromMenu()
    {
        Scene previousScene = SceneManager.GetActiveScene();
        string previousPath = previousScene.path;
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        PlatformerPlayer3D player = Object.FindFirstObjectByType<PlatformerPlayer3D>();
        if (player == null)
        {
            Debug.LogError("[ImportedStartRoomSetup] Existing Player prefab was not found. Scene was not modified.");
            RestorePreviousScene(previousPath);
            return;
        }

        Camera mainCamera = FindOrCreateGameplayCamera(player.transform);
        RemoveExtraAudioListeners(mainCamera);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log(
            $"[ImportedStartRoomSetup] Repaired '{scene.name}': camera='{mainCamera.name}', " +
            $"target='{player.name}', display={mainCamera.targetDisplay + 1}, mask={mainCamera.cullingMask}.");

        RestorePreviousScene(previousPath);
    }

    private static Camera FindOrCreateGameplayCamera(Transform player)
    {
        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Camera mainCamera = cameras.Length > 0 ? cameras[0] : null;

        if (mainCamera == null)
        {
            GameObject cameraObject = new GameObject(
                "Main Camera",
                typeof(Camera),
                typeof(AudioListener),
                typeof(CameraFollow3D),
                typeof(UniversalAdditionalCameraData));
            mainCamera = cameraObject.GetComponent<Camera>();
        }

        GameObject cameraGameObject = mainCamera.gameObject;
        cameraGameObject.name = "Main Camera";
        cameraGameObject.tag = "MainCamera";
        cameraGameObject.SetActive(true);
        SetParentsActive(cameraGameObject.transform.parent);

        mainCamera.enabled = true;
        mainCamera.targetDisplay = 0;
        mainCamera.cullingMask = ~0;
        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = new Color(0.06f, 0.07f, 0.09f, 1f);
        mainCamera.depth = 0f;
        mainCamera.orthographic = true;
        mainCamera.orthographicSize = 5.2f;
        mainCamera.transform.SetPositionAndRotation(player.position + new Vector3(0f, 1f, -10f), Quaternion.identity);

        if (mainCamera.GetComponent<AudioListener>() == null)
        {
            mainCamera.gameObject.AddComponent<AudioListener>();
        }

        if (mainCamera.GetComponent<CameraFollow3D>() == null)
        {
            mainCamera.gameObject.AddComponent<CameraFollow3D>();
        }

        UniversalAdditionalCameraData cameraData = mainCamera.GetUniversalAdditionalCameraData();
        cameraData.renderType = CameraRenderType.Base;
        cameraData.cameraStack.Clear();
        cameraData.renderPostProcessing = false;

        return mainCamera;
    }

    private static void SetParentsActive(Transform parent)
    {
        while (parent != null)
        {
            parent.gameObject.SetActive(true);
            parent = parent.parent;
        }
    }

    private static void RemoveExtraAudioListeners(Camera mainCamera)
    {
        AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (AudioListener listener in listeners)
        {
            listener.enabled = listener.gameObject == mainCamera.gameObject;
        }
    }

    private static void RestorePreviousScene(string previousPath)
    {
        if (!string.IsNullOrEmpty(previousPath) && previousPath != ScenePath)
        {
            EditorSceneManager.OpenScene(previousPath, OpenSceneMode.Single);
        }
    }
}
