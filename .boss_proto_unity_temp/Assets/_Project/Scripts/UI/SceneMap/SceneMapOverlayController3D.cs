using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(SceneMapOverlayView3D))]
public sealed class SceneMapOverlayController3D : MonoBehaviour
{
    private const string SettingsResourcePath = "SceneMap/SceneMapSettings3D";
    private const string GraphResourcePath = "SceneMap/SceneMapGraphData3D";

    [SerializeField] private SceneMapSettings3D settings;
    [SerializeField] private SceneMapGraphData3D graphData;
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private SceneMapOverlayView3D view;

    private InputAction mapAction;
    private InputAction cancelAction;
    private bool enabledMapActionLocally;
    private bool enabledCancelActionLocally;
    private string currentRoomKey = string.Empty;
    private bool initialized;
    private bool isOpen;

    public bool IsOpen => isOpen;
    public string CurrentRoomKey => currentRoomKey;

    private void Awake()
    {
        if (settings == null) settings = Resources.Load<SceneMapSettings3D>(SettingsResourcePath);
        if (graphData == null) graphData = Resources.Load<SceneMapGraphData3D>(GraphResourcePath);
        if (view == null) view = GetComponent<SceneMapOverlayView3D>();
        if (settings == null || graphData == null || view == null)
        {
            Debug.LogError("[SceneMap] Settings, Graph Data and View are required.", this);
            enabled = false;
            return;
        }

        view.Initialize(settings, graphData);
        view.SetOpen(false, string.Empty);
        initialized = true;
        UpdateCurrentRoom(SceneManager.GetActiveScene());
    }

    private void OnEnable()
    {
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        PlayerDamageReceiver.PlayerDied -= HandlePlayerDied;
        PlayerDamageReceiver.PlayerDied += HandlePlayerDied;
        GameplayInputLock3D.LockStateChanged -= HandleInputLockChanged;
        GameplayInputLock3D.LockStateChanged += HandleInputLockChanged;
        BindActions();
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        PlayerDamageReceiver.PlayerDied -= HandlePlayerDied;
        GameplayInputLock3D.LockStateChanged -= HandleInputLockChanged;
        UnbindActions();
        CloseMap();
    }

    private void OnDestroy()
    {
        GameplayInputLock3D.Release(this);
    }

    private void HandleMapPerformed(InputAction.CallbackContext context)
    {
        if (!initialized) return;
        if (isOpen) CloseMap();
        else TryOpenMap();
    }

    private void HandleCancelPerformed(InputAction.CallbackContext context)
    {
        if (isOpen) CloseMap();
    }

    private void TryOpenMap()
    {
        if (!CanOpenMap(out GameObject player)) return;
        CameraAbilitySystem3D cameraAbilities = player.GetComponent<CameraAbilitySystem3D>();
        if (cameraAbilities != null) cameraAbilities.SuspendInputForModalUI();
        GameplayInputLock3D.Acquire(this);
        isOpen = true;
        view.SetOpen(true, currentRoomKey);
    }

    public void CloseMap()
    {
        if (!isOpen)
        {
            GameplayInputLock3D.Release(this);
            return;
        }
        isOpen = false;
        view?.SetOpen(false, currentRoomKey);
        GameplayInputLock3D.Release(this);
    }

    private bool CanOpenMap(out GameObject player)
    {
        player = null;
        if (!initialized || string.IsNullOrWhiteSpace(currentRoomKey)) return false;
        if (Time.timeScale <= 0f || GameplayInputLock3D.IsLockedByOther(this)) return false;
        if (SceneLoader.Instance != null && SceneLoader.Instance.IsLoadingScene) return false;
        if (SceneTransitionManager.Instance != null && SceneTransitionManager.Instance.IsLoading) return false;

        player = ResolvePlayer();
        if (player == null) return false;
        PlayerDamageReceiver damageReceiver = player.GetComponent<PlayerDamageReceiver>();
        return damageReceiver == null || !damageReceiver.IsDead;
    }

    private static GameObject ResolvePlayer()
    {
        return PlatformerPlayer3D.Current != null ? PlatformerPlayer3D.Current.gameObject : null;
    }

    private void HandleActiveSceneChanged(Scene previous, Scene next)
    {
        CloseMap();
        UpdateCurrentRoom(next);
    }

    private void UpdateCurrentRoom(Scene scene)
    {
        currentRoomKey = string.Empty;
        if (!scene.IsValid()) return;
        if (graphData.TryGetRoomByScene(scene.name, out SceneMapRoomData3D room) && room.Active)
            currentRoomKey = room.StableSceneKey;
    }

    private void HandlePlayerDied(PlayerDamageReceiver receiver)
    {
        if (isOpen) CloseMap();
    }

    private void HandleInputLockChanged()
    {
        if (isOpen && GameplayInputLock3D.IsLockedByOther(this)) CloseMap();
    }

    private void BindActions()
    {
        if (settings == null || inputActions == null) return;
        mapAction = inputActions.FindAction(settings.MapActionPath, false);
        cancelAction = inputActions.FindAction(settings.CancelActionPath, false);
        if (mapAction == null)
            Debug.LogWarning($"[SceneMap] Map action was not found: {settings.MapActionPath}", this);
        else
        {
            mapAction.performed -= HandleMapPerformed;
            mapAction.performed += HandleMapPerformed;
            if (!mapAction.enabled) { mapAction.Enable(); enabledMapActionLocally = true; }
        }
        if (cancelAction == null)
            Debug.LogWarning($"[SceneMap] Cancel action was not found: {settings.CancelActionPath}", this);
        else
        {
            cancelAction.performed -= HandleCancelPerformed;
            cancelAction.performed += HandleCancelPerformed;
            if (!cancelAction.enabled) { cancelAction.Enable(); enabledCancelActionLocally = true; }
        }
    }

    private void UnbindActions()
    {
        if (mapAction != null)
        {
            mapAction.performed -= HandleMapPerformed;
            if (enabledMapActionLocally) mapAction.Disable();
        }
        if (cancelAction != null)
        {
            cancelAction.performed -= HandleCancelPerformed;
            if (enabledCancelActionLocally) cancelAction.Disable();
        }
        mapAction = null;
        cancelAction = null;
        enabledMapActionLocally = false;
        enabledCancelActionLocally = false;
    }
}
