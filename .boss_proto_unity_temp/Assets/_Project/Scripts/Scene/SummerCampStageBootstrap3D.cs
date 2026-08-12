using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Guarantees one persistent official Player in every scene that contains a spawn point.
/// This is also invoked when a gameplay scene is played directly in the Editor.
/// </summary>
public static class SummerCampStageBootstrap3D
{
    private const string PlayerResourcePath = "Player/Player";

    private static SummerCampPersistentPlayerMarker persistentPlayer;
    private static bool isEnsuringPlayer;
    private static readonly HashSet<string> MissingSpawnWarnings = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        persistentPlayer = null;
        isEnsuringPlayer = false;
        MissingSpawnWarnings.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneCallback()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        bool hasSpawnPoint = HasSpawnPointInScene(scene);
        if (!hasSpawnPoint)
        {
            if (IsPortalTransitionInProgress())
            {
                WarnMissingSpawn(scene.name);
                return;
            }

            RemovePersistentStagePlayer();
            return;
        }

        EnsurePlayerForScene(scene);
    }

    private static void EnsurePlayerForScene(Scene scene)
    {
        if (isEnsuringPlayer)
        {
            return;
        }

        isEnsuringPlayer = true;
        try
        {
            PlatformerPlayer3D player = ResolveSinglePlayer(scene);
            if (player == null)
            {
                GameObject prefab = Resources.Load<GameObject>(PlayerResourcePath);
                if (prefab == null)
                {
                    Debug.LogError($"[SummerCampStageBootstrap3D] Official Player prefab was not found at Resources/{PlayerResourcePath}.prefab.");
                    return;
                }

                GameObject instance = Object.Instantiate(prefab);
                instance.name = prefab.name;
                player = instance.GetComponent<PlatformerPlayer3D>();
            }

            PreservePlayer(player);

            // Transition managers own portal destinations. Direct scene Play and a newly
            // recovered Player use the scene's configured default/fallback point here.
            if (!IsPortalTransitionInProgress())
            {
                MovePlayerToSceneSpawn(player, scene.name);
            }

            EnsureGameplayCamera(player.transform);
            ValidateSinglePlayer(scene.name, player);
        }
        finally
        {
            isEnsuringPlayer = false;
        }
    }

    private static bool HasSpawnPointInScene(Scene scene)
    {
        PlayerSpawnPoint[] legacyPoints = Object.FindObjectsByType<PlayerSpawnPoint>(FindObjectsSortMode.None);
        foreach (PlayerSpawnPoint point in legacyPoints)
        {
            if (point != null && point.gameObject.scene == scene) return true;
        }

        SceneSpawnPoint3D[] modernPoints = Object.FindObjectsByType<SceneSpawnPoint3D>(FindObjectsSortMode.None);
        foreach (SceneSpawnPoint3D point in modernPoints)
        {
            if (point != null && point.gameObject.scene == scene) return true;
        }

        return false;
    }

    private static void WarnMissingSpawn(string sceneName)
    {
        if (MissingSpawnWarnings.Add(sceneName))
        {
            Debug.LogWarning($"[SummerCampStageBootstrap3D] Scene '{sceneName}' was reached through a scene transition but has no PlayerSpawnPoint. The persistent Player was kept and was not repositioned. Add a PlayerSpawnPoint with the portal destination Spawn ID.");
        }
    }

    private static PlatformerPlayer3D ResolveSinglePlayer(Scene scene)
    {
        PlatformerPlayer3D[] players = Object.FindObjectsByType<PlatformerPlayer3D>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        PlatformerPlayer3D keeper = null;

        if (persistentPlayer != null)
        {
            keeper = persistentPlayer.GetComponent<PlatformerPlayer3D>();
        }

        if (keeper == null)
        {
            foreach (PlatformerPlayer3D candidate in players)
            {
                if (candidate != null && candidate.gameObject.scene == scene)
                {
                    keeper = candidate;
                    break;
                }
            }
        }

        foreach (PlatformerPlayer3D candidate in players)
        {
            if (candidate != null && candidate != keeper)
            {
                candidate.gameObject.SetActive(false);
                Object.Destroy(candidate.gameObject);
            }
        }

        return keeper;
    }

    private static void PreservePlayer(PlatformerPlayer3D player)
    {
        Transform root = player.transform.root;
        if (root != player.transform)
        {
            player.transform.SetParent(null, true);
        }

        SummerCampPersistentPlayerMarker marker = player.GetComponent<SummerCampPersistentPlayerMarker>();
        if (marker == null)
        {
            marker = player.gameObject.AddComponent<SummerCampPersistentPlayerMarker>();
        }

        persistentPlayer = marker;
        player.gameObject.SetActive(true);
        Object.DontDestroyOnLoad(player.gameObject);
    }

    private static bool IsPortalTransitionInProgress()
    {
        return (SceneTransitionManager.Instance != null && SceneTransitionManager.Instance.IsLoading)
            || (SceneLoader.Instance != null && SceneLoader.Instance.IsLoadingScene);
    }

    private static void MovePlayerToSceneSpawn(PlatformerPlayer3D player, string sceneName)
    {
        Vector3 destination;
        PlayerSpawnPoint legacy = FindLegacyFallbackSpawn();
        SceneSpawnPoint3D modern = legacy == null ? FindModernFallbackSpawn() : null;

        if (legacy != null)
        {
            destination = legacy.transform.position;
        }
        else if (modern != null)
        {
            destination = modern.GetSpawnPosition();
        }
        else
        {
            WarnMissingSpawn(sceneName);
            return;
        }

        Rigidbody body = player.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = destination;
        }
        else
        {
            player.transform.position = destination;
        }

        player.ResetJumpStateAfterTeleport();
        Physics.SyncTransforms();
    }

    private static PlayerSpawnPoint FindLegacyFallbackSpawn()
    {
        PlayerSpawnPoint[] points = Object.FindObjectsByType<PlayerSpawnPoint>(FindObjectsSortMode.None);
        foreach (PlayerSpawnPoint point in points)
        {
            if (point != null && point.IsDefaultSpawn) return point;
        }
        foreach (PlayerSpawnPoint point in points)
        {
            if (point != null && point.CanUseAsRespawnPoint) return point;
        }
        return points.Length > 0 ? points[0] : null;
    }

    private static SceneSpawnPoint3D FindModernFallbackSpawn()
    {
        SceneSpawnPoint3D[] points = Object.FindObjectsByType<SceneSpawnPoint3D>(FindObjectsSortMode.None);
        foreach (SceneSpawnPoint3D point in points)
        {
            if (point != null && point.IsDefaultSpawn) return point;
        }
        foreach (SceneSpawnPoint3D point in points)
        {
            if (point != null && point.CanUseAsRespawnPoint) return point;
        }
        return points.Length > 0 ? points[0] : null;
    }

    private static void EnsureGameplayCamera(Transform player)
    {
        Camera mainCamera = Camera.main;
        bool createdCamera = mainCamera == null;
        if (createdCamera)
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
        if (createdCamera)
        {
            mainCamera.orthographic = true;
            mainCamera.orthographicSize = 5.2f;
        }

        CameraFollow3D follow = mainCamera.GetComponent<CameraFollow3D>();
        if (follow == null)
        {
            follow = mainCamera.gameObject.AddComponent<CameraFollow3D>();
        }
        follow.SnapToTarget(player);

        WorldVisualEffects3D visualEffects = mainCamera.GetComponent<WorldVisualEffects3D>();
        if (visualEffects == null)
        {
            visualEffects = mainCamera.gameObject.AddComponent<WorldVisualEffects3D>();
        }
        visualEffects.SetDustTarget(player);

        UniversalAdditionalCameraData cameraData = mainCamera.GetUniversalAdditionalCameraData();
        cameraData.renderType = CameraRenderType.Base;
        cameraData.cameraStack.Clear();
    }

    private static void ValidateSinglePlayer(string sceneName, PlatformerPlayer3D keeper)
    {
        PlatformerPlayer3D[] players = Object.FindObjectsByType<PlatformerPlayer3D>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        int activeKeepers = 0;
        foreach (PlatformerPlayer3D player in players)
        {
            if (player != null && player.gameObject.activeSelf) activeKeepers++;
        }

        if (activeKeepers != 1 || keeper == null)
        {
            Debug.LogError($"[SummerCampStageBootstrap3D] Player invariant failed in '{sceneName}': active players={activeKeepers}.");
        }
    }

    /// <summary>
    /// Removes the stage-only Player before loading a utility scene such as the title.
    /// The destination scene remains responsible for creating its own camera and UI.
    /// </summary>
    public static void PrepareForUtilitySceneLoad()
    {
        RemovePersistentStagePlayer();
    }

    private static void RemovePersistentStagePlayer()
    {
        if (persistentPlayer == null)
        {
            persistentPlayer = Object.FindFirstObjectByType<SummerCampPersistentPlayerMarker>();
        }

        if (persistentPlayer != null)
        {
            persistentPlayer.gameObject.SetActive(false);
            Object.Destroy(persistentPlayer.gameObject);
            persistentPlayer = null;
        }
    }
}

[DisallowMultipleComponent]
public sealed class SummerCampPersistentPlayerMarker : MonoBehaviour
{
}
