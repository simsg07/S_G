using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace CAMPAST.Title
{
    public sealed class TitleTransitionManager : MonoBehaviour
    {
        [Header("Transition")]
        [SerializeField] private CameraFlashEffect cameraFlashEffect;
        [SerializeField] private AudioSource shutterAudioSource;
        [SerializeField] private AudioClip shutterClip;
        [SerializeField] private CanvasGroup menuGroup;
        [SerializeField] private float minimumWhiteHoldSeconds = 0.15f;
        [SerializeField] private bool persistThroughSceneLoad = true;
        [SerializeField] private bool destroyAfterTransition = true;
        [SerializeField] private bool useUnscaledTime = true;

        [Header("Events")]
        [SerializeField] private UnityEvent onTransitionStarted;
        [SerializeField] private UnityEvent onSceneReadyToActivate;
        [SerializeField] private UnityEvent onTransitionFinished;

        private bool isTransitioning;

        public bool IsTransitioning => isTransitioning;

        public void TransitionToScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning("Scene name is empty. Title transition was ignored.", this);
                return;
            }

            if (isTransitioning)
            {
                return;
            }

            StartCoroutine(TransitionRoutine(sceneName));
        }

        private IEnumerator TransitionRoutine(string sceneName)
        {
            isTransitioning = true;
            SetMenuInteractable(false);
            onTransitionStarted?.Invoke();

            if (persistThroughSceneLoad)
            {
                DontDestroyOnLoad(gameObject);
            }

            PlayShutterSound();

            if (cameraFlashEffect != null)
            {
                yield return cameraFlashEffect.PlayFlashIn();
            }

            float holdStartedAt = CurrentTime;
            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName);
            if (loadOperation == null)
            {
                Debug.LogError($"Failed to start loading scene '{sceneName}'.", this);
                SetMenuInteractable(true);
                isTransitioning = false;
                yield break;
            }

            loadOperation.allowSceneActivation = false;

            while (loadOperation.progress < 0.9f)
            {
                yield return null;
            }

            while (CurrentTime - holdStartedAt < minimumWhiteHoldSeconds)
            {
                yield return null;
            }

            onSceneReadyToActivate?.Invoke();
            loadOperation.allowSceneActivation = true;

            while (!loadOperation.isDone)
            {
                yield return null;
            }

            yield return null;

            if (cameraFlashEffect != null)
            {
                yield return cameraFlashEffect.PlayFlashOut();
                cameraFlashEffect.ResetEffect();
            }

            onTransitionFinished?.Invoke();
            isTransitioning = false;

            if (destroyAfterTransition)
            {
                Destroy(gameObject);
            }
        }

        private void PlayShutterSound()
        {
            if (shutterAudioSource == null)
            {
                return;
            }

            if (shutterClip != null)
            {
                shutterAudioSource.PlayOneShot(shutterClip);
                return;
            }

            shutterAudioSource.Play();
        }

        private void SetMenuInteractable(bool interactable)
        {
            if (menuGroup == null)
            {
                return;
            }

            menuGroup.interactable = interactable;
            menuGroup.blocksRaycasts = interactable;
        }

        private float CurrentTime => useUnscaledTime ? Time.unscaledTime : Time.time;
    }
}
