using UnityEngine;

[DisallowMultipleComponent]
public class CameraInterventionLimiter : MonoBehaviour
{
    [SerializeField] private int maxCameraInterventions = 1; // Maximum number of successful camera interventions allowed.
    [SerializeField] private int remainingCameraInterventions = 1; // Current remaining camera interventions.
    [SerializeField] private bool resetOnAwake = true; // Resets remainingCameraInterventions to maxCameraInterventions when the scene starts.
    [SerializeField] private bool debugMode; // Logs successful or blocked camera intervention attempts.

    public int MaxCameraInterventions => maxCameraInterventions;
    public int RemainingCameraInterventions => remainingCameraInterventions;
    public bool CanUseIntervention => remainingCameraInterventions > 0;

    private void Awake()
    {
        ClampValues();
        if (resetOnAwake)
        {
            ResetCameraInterventions();
        }
    }

    private void OnValidate()
    {
        ClampValues();
    }

    public bool TryConsumeIntervention(string reason)
    {
        ClampValues();
        if (remainingCameraInterventions <= 0)
        {
            if (debugMode)
            {
                Debug.Log($"Camera intervention blocked: {reason}", this);
            }

            return false;
        }

        remainingCameraInterventions--;
        if (debugMode)
        {
            Debug.Log($"Camera intervention used: {reason}. Remaining {remainingCameraInterventions}/{maxCameraInterventions}", this);
        }

        return true;
    }

    public void ResetCameraInterventions()
    {
        maxCameraInterventions = Mathf.Max(0, maxCameraInterventions);
        remainingCameraInterventions = maxCameraInterventions;
    }

    public void RestoreCameraInterventions(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        maxCameraInterventions = Mathf.Max(0, maxCameraInterventions);
        remainingCameraInterventions = Mathf.Clamp(remainingCameraInterventions + amount, 0, maxCameraInterventions);
    }

    public void SetMaxCameraInterventions(int value, bool refill)
    {
        maxCameraInterventions = Mathf.Max(0, value);
        remainingCameraInterventions = refill
            ? maxCameraInterventions
            : Mathf.Clamp(remainingCameraInterventions, 0, maxCameraInterventions);
    }

    private void ClampValues()
    {
        maxCameraInterventions = Mathf.Max(0, maxCameraInterventions);
        remainingCameraInterventions = Mathf.Clamp(remainingCameraInterventions, 0, maxCameraInterventions);
    }
}
