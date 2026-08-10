using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public sealed class ObjectTestSceneBootstrap3D : MonoBehaviour
{
    [Header("Scene Navigation")]
    [SerializeField] private string titleSceneName = "MainMenu";
    [SerializeField] private Button returnToTitleButton;

    [Header("Environment Preview")]
    [SerializeField] private Renderer[] environmentRenderers;
    [SerializeField] private Color groundColor = new Color(0.08f, 0.26f, 0.3f, 1f);
    [SerializeField] private Color platformColor = new Color(0.12f, 0.45f, 0.5f, 1f);

    private bool returnRequested;

    private void Awake()
    {
        Time.timeScale = 1f;
        GameProgressSave3D.BeginTransientSession();
        EnsureGameplayCameraForPlayerInitialization();
        ApplyEnvironmentColors();

        if (returnToTitleButton != null)
        {
            returnToTitleButton.onClick.RemoveListener(ReturnToTitle);
            returnToTitleButton.onClick.AddListener(ReturnToTitle);
        }
    }

    // SummerCampStageBootstrap3D creates the Player after sceneLoaded.  The Player's
    // CameraAbilitySystem3D initializes in Awake, so this test scene must already
    // have the same Main Camera that the operating scenes serialize in the scene.
    private static void EnsureGameplayCameraForPlayerInitialization()
    {
        if (Camera.main != null)
        {
            return;
        }

        GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5.2f;
        camera.transform.position = new Vector3(0f, 1.4f, -10f);
        TwoPointFiveDUtility3D.ConfigureSideViewCamera(camera, 5.2f);
    }

    private void OnDestroy()
    {
        if (returnToTitleButton != null)
        {
            returnToTitleButton.onClick.RemoveListener(ReturnToTitle);
        }

        GameProgressSave3D.EndTransientSession();
    }

    public void ReturnToTitle()
    {
        if (returnRequested)
        {
            return;
        }

        Time.timeScale = 1f;
        returnRequested = SceneLoader.TryLoadUtilityScene(titleSceneName);
        if (returnRequested && returnToTitleButton != null)
        {
            returnToTitleButton.interactable = false;
        }
    }

    private void ApplyEnvironmentColors()
    {
        if (environmentRenderers == null)
        {
            return;
        }

        MaterialPropertyBlock properties = new MaterialPropertyBlock();
        for (int i = 0; i < environmentRenderers.Length; i++)
        {
            Renderer environmentRenderer = environmentRenderers[i];
            if (environmentRenderer == null)
            {
                continue;
            }

            Color color = environmentRenderer.name == "DropPlatform" ? platformColor : groundColor;
            environmentRenderer.GetPropertyBlock(properties);
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            environmentRenderer.SetPropertyBlock(properties);
            properties.Clear();
        }
    }
}
