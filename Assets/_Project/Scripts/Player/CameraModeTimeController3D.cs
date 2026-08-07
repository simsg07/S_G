using UnityEngine;

internal sealed class CameraModeTimeController3D
{
    private float storedTimeScale = 1f;
    private float storedFixedDeltaTime = 0.02f;
    private float appliedCameraTimeScale = 0.25f;
    private float appliedCameraFixedDeltaTime = 0.005f;

    public bool IsActive { get; private set; }

    public bool HasSnapshot { get; private set; }

    public bool Apply(float requestedTimeScale)
    {
        if (!Application.isPlaying || IsActive || HasSnapshot || requestedTimeScale <= 0f || Time.timeScale <= 0f)
        {
            return false;
        }

        storedTimeScale = Time.timeScale;
        storedFixedDeltaTime = Time.fixedDeltaTime;
        HasSnapshot = true;

        appliedCameraTimeScale = Mathf.Clamp(requestedTimeScale, 0.01f, 1f);
        float normalizedFixedDelta = storedTimeScale > 0.001f
            ? storedFixedDeltaTime / storedTimeScale
            : storedFixedDeltaTime;
        appliedCameraFixedDeltaTime = normalizedFixedDelta * appliedCameraTimeScale;

        Time.timeScale = appliedCameraTimeScale;
        Time.fixedDeltaTime = appliedCameraFixedDeltaTime;
        IsActive = true;
        return true;
    }

    public bool Restore()
    {
        if (!HasSnapshot)
        {
            return false;
        }

        bool externalPauseActive = Mathf.Approximately(Time.timeScale, 0f);
        IsActive = false;
        HasSnapshot = false;

        // A valid snapshot belongs to exactly one camera cycle. Always restore the
        // physics step so a slightly changed slow-mode value cannot become the next
        // cycle's baseline. Preserve an external pause instead of unpausing it.
        if (!externalPauseActive)
        {
            Time.timeScale = storedTimeScale;
        }

        Time.fixedDeltaTime = storedFixedDeltaTime;
        return true;
    }

    public bool HasExternalPauseOverride()
    {
        return IsActive && !(Time.timeScale > 0f);
    }
}
