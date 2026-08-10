using System.Collections;
using System;
using System.Reflection;
using UnityEngine;

namespace CAMPAST.Title
{
    /// <summary>
    /// Camera-oriented flash effect that avoids full-screen UI images.
    /// Assign a Global Volume object if the project uses URP/HDRP post processing.
    /// </summary>
    public sealed class CameraFlashEffect : MonoBehaviour
    {
        [Header("Timing")]
        [SerializeField] private float flashInDuration = 0.08f;
        [SerializeField] private float flashOutDuration = 0.35f;
        [SerializeField] private bool useUnscaledTime = true;

        [Header("Post Processing")]
        [Tooltip("Assign a Unity Volume component. Its weight will be animated for the flash.")]
        [SerializeField] private MonoBehaviour volumeBehaviour;
        [SerializeField, Range(0f, 1f)] private float volumePeakWeight = 1f;
        [SerializeField] private AnimationCurve flashInCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve flashOutCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        [Header("Camera Kick")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float fieldOfViewKick = 2f;

        [Header("Optional Light Burst")]
        [SerializeField] private Light flashLight;
        [SerializeField] private float lightPeakIntensity = 4f;

        private PropertyInfo volumeWeightProperty;
        private FieldInfo volumeWeightField;
        private float originalVolumeWeight;
        private float originalFieldOfView;
        private float originalLightIntensity;

        private void Awake()
        {
            if (volumeBehaviour != null)
            {
                volumeWeightProperty = volumeBehaviour.GetType().GetProperty("weight");
                volumeWeightField = volumeBehaviour.GetType().GetField("weight");
                originalVolumeWeight = ReadVolumeWeight();
            }

            if (targetCamera != null)
            {
                originalFieldOfView = targetCamera.fieldOfView;
            }

            if (flashLight != null)
            {
                originalLightIntensity = flashLight.intensity;
                flashLight.intensity = 0f;
            }

            ApplyFlashAmount(0f);
        }

        public IEnumerator PlayFlashIn()
        {
            yield return AnimateFlash(0f, 1f, flashInDuration, flashInCurve);
        }

        public IEnumerator PlayFlashOut()
        {
            yield return AnimateFlash(1f, 0f, flashOutDuration, flashOutCurve);
        }

        public void ResetEffect()
        {
            ApplyFlashAmount(0f);
        }

        private IEnumerator AnimateFlash(float start, float end, float duration, AnimationCurve curve)
        {
            if (duration <= 0f)
            {
                ApplyFlashAmount(end);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float curved = curve != null ? curve.Evaluate(t) : t;
                ApplyFlashAmount(Mathf.LerpUnclamped(start, end, curved));
                yield return null;
            }

            ApplyFlashAmount(end);
        }

        private void ApplyFlashAmount(float amount)
        {
            amount = Mathf.Clamp01(amount);
            WriteVolumeWeight(Mathf.Lerp(originalVolumeWeight, volumePeakWeight, amount));

            if (targetCamera != null)
            {
                targetCamera.fieldOfView = originalFieldOfView + fieldOfViewKick * amount;
            }

            if (flashLight != null)
            {
                flashLight.intensity = Mathf.Lerp(originalLightIntensity, lightPeakIntensity, amount);
            }
        }

        private float ReadVolumeWeight()
        {
            if (volumeBehaviour == null)
            {
                return 0f;
            }

            object value = null;
            if (volumeWeightProperty != null)
            {
                value = volumeWeightProperty.GetValue(volumeBehaviour);
            }
            else if (volumeWeightField != null)
            {
                value = volumeWeightField.GetValue(volumeBehaviour);
            }

            return value is float weight ? weight : 0f;
        }

        private void WriteVolumeWeight(float weight)
        {
            if (volumeBehaviour == null)
            {
                return;
            }

            if (volumeWeightProperty != null && volumeWeightProperty.CanWrite)
            {
                volumeWeightProperty.SetValue(volumeBehaviour, weight);
                return;
            }

            if (volumeWeightField != null)
            {
                volumeWeightField.SetValue(volumeBehaviour, weight);
            }
        }
    }
}
