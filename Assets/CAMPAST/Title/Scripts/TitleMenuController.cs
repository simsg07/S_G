using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CAMPAST.Title
{
    public sealed class TitleMenuController : MonoBehaviour
    {
        private enum ContinueCheckMode
        {
            PlayerPrefs,
            File
        }

        [Header("Scene Names")]
        [SerializeField] private string newGameSceneName = "InGame";
        [SerializeField] private string continueSceneName = "InGame";

        [Header("References")]
        [SerializeField] private TitleTransitionManager transitionManager;
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button exitButton;
        [SerializeField] private GameObject settingsPanel;

        [Header("Object Test")]
        [Tooltip("Show the Object Test entry in the title menu.")]
        [SerializeField] private bool showTestSceneButton = true;
        [Tooltip("When enabled, show the Object Test entry only in the Editor or a Development Build.")]
        [SerializeField] private bool developmentBuildOnly;
        [SerializeField] private string objectTestSceneName = "ObjectTestScene";
        [SerializeField] private Button objectTestButton;

        [Header("Continue")]
        [SerializeField] private ContinueCheckMode continueCheckMode = ContinueCheckMode.PlayerPrefs;
        [SerializeField] private string continuePlayerPrefsKey = "CAMPAST_SAVE_EXISTS";
        [SerializeField] private string saveFileName = "campast_save.json";

        private RectTransform testButtonLayoutRoot;
        private float menuHeightWithTestButton;
        private float menuHeightWithoutTestButton;

        private void Awake()
        {
            EnsureInputSystemEventModule();
            CacheTestButtonLayout();
            RefreshTestSceneButton();
            BindButtons();
            RefreshContinueButton();

            if (settingsPanel != null)
            {
                settingsPanel.SetActive(false);
            }
        }

        public void StartNewGame()
        {
            Debug.Log($"[TitleMenuController] New Game clicked. Loading scene: {newGameSceneName}", this);
            transitionManager?.TransitionToScene(newGameSceneName);
        }

        public void ContinueGame()
        {
            if (!HasSaveData())
            {
                Debug.Log("[TitleMenuController] Continue clicked, but save data was not found.", this);
                RefreshContinueButton();
                return;
            }

            Debug.Log($"[TitleMenuController] Continue clicked. Loading scene: {continueSceneName}", this);
            transitionManager?.TransitionToScene(continueSceneName);
        }

        public void ToggleSettings()
        {
            if (settingsPanel == null)
            {
                return;
            }

            settingsPanel.SetActive(!settingsPanel.activeSelf);
        }

        public void OpenObjectTestScene()
        {
            if (!ShouldShowTestSceneButton())
            {
                return;
            }

            if (!SceneLoader.IsSceneRegisteredInBuildSettings(objectTestSceneName))
            {
                Debug.LogWarning($"[TitleMenuController] Object Test scene is not registered in Build Settings: {objectTestSceneName}", this);
                return;
            }

            Time.timeScale = 1f;
            Debug.Log($"[TitleMenuController] Object Test clicked. Loading scene: {objectTestSceneName}", this);
            transitionManager?.TransitionToScene(objectTestSceneName);
        }

        public void SetTestSceneButtonVisible(bool visible)
        {
            showTestSceneButton = visible;
            RefreshTestSceneButton();
        }

        public void RefreshTestSceneButton()
        {
            if (objectTestButton != null)
            {
                bool shouldShow = ShouldShowTestSceneButton();
                objectTestButton.gameObject.SetActive(shouldShow);
                if (testButtonLayoutRoot != null)
                {
                    Vector2 size = testButtonLayoutRoot.sizeDelta;
                    size.y = shouldShow ? menuHeightWithTestButton : menuHeightWithoutTestButton;
                    testButtonLayoutRoot.sizeDelta = size;
                    LayoutRebuilder.MarkLayoutForRebuild(testButtonLayoutRoot);
                }
            }
        }

        public void ExitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void RefreshContinueButton()
        {
            if (continueButton != null)
            {
                continueButton.interactable = HasSaveData();
            }
        }

        private void BindButtons()
        {
            if (newGameButton != null)
            {
                newGameButton.onClick.RemoveListener(StartNewGame);
                newGameButton.onClick.AddListener(StartNewGame);
            }

            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(ContinueGame);
                continueButton.onClick.AddListener(ContinueGame);
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveListener(ToggleSettings);
                settingsButton.onClick.AddListener(ToggleSettings);
            }

            if (objectTestButton != null)
            {
                objectTestButton.onClick.RemoveListener(OpenObjectTestScene);
                objectTestButton.onClick.AddListener(OpenObjectTestScene);
            }

            if (exitButton != null)
            {
                exitButton.onClick.RemoveListener(ExitGame);
                exitButton.onClick.AddListener(ExitGame);
            }
        }

        private bool ShouldShowTestSceneButton()
        {
            return showTestSceneButton &&
                   (!developmentBuildOnly || Application.isEditor || Debug.isDebugBuild);
        }

        private void CacheTestButtonLayout()
        {
            if (objectTestButton == null)
            {
                return;
            }

            testButtonLayoutRoot = objectTestButton.transform.parent as RectTransform;
            if (testButtonLayoutRoot == null)
            {
                return;
            }

            menuHeightWithTestButton = testButtonLayoutRoot.sizeDelta.y;
            LayoutElement layoutElement = objectTestButton.GetComponent<LayoutElement>();
            float buttonHeight = layoutElement != null && layoutElement.preferredHeight >= 0f
                ? layoutElement.preferredHeight
                : ((RectTransform)objectTestButton.transform).rect.height;
            VerticalLayoutGroup layoutGroup = testButtonLayoutRoot.GetComponent<VerticalLayoutGroup>();
            float spacing = layoutGroup != null ? layoutGroup.spacing : 0f;
            menuHeightWithoutTestButton = Mathf.Max(0f, menuHeightWithTestButton - buttonHeight - spacing);
        }

        private static void EnsureInputSystemEventModule()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                GameObject eventSystemObject = new GameObject("EventSystem");
                eventSystem = eventSystemObject.AddComponent<EventSystem>();
            }

            StandaloneInputModule oldInputModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (oldInputModule != null)
            {
                Destroy(oldInputModule);
            }

            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            {
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }
        }

        private bool HasSaveData()
        {
            if (continueCheckMode == ContinueCheckMode.PlayerPrefs)
            {
                return GameProgressSave3D.HasSaveData || PlayerPrefs.HasKey(continuePlayerPrefsKey);
            }

            string savePath = Path.Combine(Application.persistentDataPath, saveFileName);
            return File.Exists(savePath);
        }
    }
}
