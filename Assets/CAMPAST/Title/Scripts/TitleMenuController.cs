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

        [Header("Continue")]
        [SerializeField] private ContinueCheckMode continueCheckMode = ContinueCheckMode.PlayerPrefs;
        [SerializeField] private string continuePlayerPrefsKey = "CAMPAST_SAVE_EXISTS";
        [SerializeField] private string saveFileName = "campast_save.json";

        private void Awake()
        {
            EnsureInputSystemEventModule();
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

            if (exitButton != null)
            {
                exitButton.onClick.RemoveListener(ExitGame);
                exitButton.onClick.AddListener(ExitGame);
            }
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
