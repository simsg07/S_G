using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(CanvasScaler))]
public sealed class PlayerUICameraBinder : MonoBehaviour
{
    private static PlayerUICameraBinder instance;

    [SerializeField] private bool bindMainCameraOnEnable = true;
    [SerializeField] private bool persistAcrossScenes = true;
    [SerializeField] private string sortingLayerName = "UI";
    [SerializeField] private float planeDistance = 1f;
    [SerializeField] private int sortingOrder = 1000;
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [SerializeField, Range(0f, 1f)] private float matchWidthOrHeight = 1f;

    private Canvas canvas;
    private CanvasScaler scaler;

    private void Awake()
    {
        if (persistAcrossScenes && instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        if (persistAcrossScenes)
        {
            if (transform.parent != null)
            {
                transform.SetParent(null);
            }

            DontDestroyOnLoad(gameObject);
        }

        Apply();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;

        if (bindMainCameraOnEnable)
        {
            Apply();
        }
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void LateUpdate()
    {
        if (canvas != null && canvas.worldCamera == null)
        {
            BindMainCamera();
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void OnValidate()
    {
        ApplyCanvasSettings();
    }

    [ContextMenu("Bind Main Camera")]
    public void Apply()
    {
        ApplyCanvasSettings();

        if (canvas != null && canvas.worldCamera == null)
        {
            BindMainCamera();
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyCanvasSettings();
        BindMainCamera();
    }

    private void BindMainCamera()
    {
        Camera mainCamera = Camera.main;
        if (canvas != null && mainCamera != null)
        {
            canvas.worldCamera = mainCamera;
        }
    }

    private void ApplyCanvasSettings()
    {
        if (canvas == null)
        {
            canvas = GetComponent<Canvas>();
        }

        if (scaler == null)
        {
            scaler = GetComponent<CanvasScaler>();
        }

        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.planeDistance = Mathf.Max(0.01f, planeDistance);
            canvas.overrideSorting = true;
            canvas.sortingLayerName = sortingLayerName;
            canvas.sortingOrder = sortingOrder;
        }

        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.matchWidthOrHeight = matchWidthOrHeight;
        }
    }
}
