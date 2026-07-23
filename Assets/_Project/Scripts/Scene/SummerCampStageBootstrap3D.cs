using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps the existing Player feature set alive while travelling between the summer-camp rooms
/// and supplies the project's standard CameraFollow3D camera to rooms that only contain map art.
/// </summary>
public static class SummerCampStageBootstrap3D
{
    private static readonly HashSet<string> StageSceneNames = new HashSet<string>
    {
        "Start_Room",
        "hallwa_01",
        "hallwa_02",
        "Item_Room_01",
        "middle_Room"
    };

    private static SummerCampPersistentPlayerMarker persistentPlayer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        persistentPlayer = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneCallback()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!StageSceneNames.Contains(scene.name))
        {
            RemovePersistentStagePlayer();
            return;
        }

        PlatformerPlayer3D player = ResolveSinglePlayer();
        if (player == null)
        {
            Debug.LogError($"[SummerCampStageBootstrap3D] No Player is available in '{scene.name}'. Start from Start_Room so the existing Player prefab can persist between rooms.");
            return;
        }

        PreservePlayer(player);
        EnsureGameplayCamera(player.transform);
    }

    private static PlatformerPlayer3D ResolveSinglePlayer()
    {
        PlatformerPlayer3D[] players = Object.FindObjectsByType<PlatformerPlayer3D>(FindObjectsSortMode.None);
        PlatformerPlayer3D keeper = null;

        if (persistentPlayer != null)
        {
            keeper = persistentPlayer.GetComponent<PlatformerPlayer3D>();
        }

        if (keeper == null && players.Length > 0)
        {
            keeper = players[0];
        }

        foreach (PlatformerPlayer3D candidate in players)
        {
            if (candidate != null && candidate != keeper)
            {
                Object.Destroy(candidate.gameObject);
            }
        }

        return keeper;
    }

    private static void PreservePlayer(PlatformerPlayer3D player)
    {
        SummerCampPersistentPlayerMarker marker = player.GetComponent<SummerCampPersistentPlayerMarker>();
        if (marker == null)
        {
            marker = player.gameObject.AddComponent<SummerCampPersistentPlayerMarker>();
        }

        persistentPlayer = marker;
        Object.DontDestroyOnLoad(player.gameObject);
    }

    private static void EnsureGameplayCamera(Transform player)
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            GameObject cameraObject = new GameObject(
                "Main Camera",
                typeof(Camera),
                typeof(AudioListener),
                typeof(CameraFollow3D),
                typeof(UniversalAdditionalCameraData));
            cameraObject.tag = "MainCamera";
            mainCamera = cameraObject.GetComponent<Camera>();
        }

        mainCamera.gameObject.SetActive(true);
        mainCamera.enabled = true;
        mainCamera.targetDisplay = 0;
        mainCamera.cullingMask = ~0;
        mainCamera.orthographic = true;
        mainCamera.orthographicSize = 5.2f;
        mainCamera.transform.SetPositionAndRotation(player.position + new Vector3(0f, 1f, -10f), Quaternion.identity);

        if (mainCamera.GetComponent<CameraFollow3D>() == null)
        {
            mainCamera.gameObject.AddComponent<CameraFollow3D>();
        }

        UniversalAdditionalCameraData cameraData = mainCamera.GetUniversalAdditionalCameraData();
        cameraData.renderType = CameraRenderType.Base;
        cameraData.cameraStack.Clear();
    }

    private static void RemovePersistentStagePlayer()
    {
        if (persistentPlayer == null)
        {
            persistentPlayer = Object.FindFirstObjectByType<SummerCampPersistentPlayerMarker>();
        }

        if (persistentPlayer != null)
        {
            Object.Destroy(persistentPlayer.gameObject);
            persistentPlayer = null;
        }
    }
}

[DisallowMultipleComponent]
public sealed class SummerCampPersistentPlayerMarker : MonoBehaviour
{
}
