using UnityEngine;

internal sealed class CameraAbilityCooldowns3D
{
    public float ShutterRemaining { get; private set; }
    public float FocusRemaining { get; private set; }
    public float RelayRemaining { get; private set; }

    public bool CanUseShutter => !(ShutterRemaining > 0f);
    public bool CanUseFocus => !(FocusRemaining > 0f);
    public bool CanUseRelay => !(RelayRemaining > 0f);

    public void Tick(float unscaledDeltaTime)
    {
        ShutterRemaining = Mathf.Max(0f, ShutterRemaining - unscaledDeltaTime);
        FocusRemaining = Mathf.Max(0f, FocusRemaining - unscaledDeltaTime);
        RelayRemaining = Mathf.Max(0f, RelayRemaining - unscaledDeltaTime);
    }

    public void StartShutter(float duration)
    {
        ShutterRemaining = Mathf.Max(0.01f, duration);
    }

    public void StartFocus(float duration)
    {
        FocusRemaining = duration;
    }

    public void StartRelay(float duration)
    {
        RelayRemaining = duration;
    }

    public void Clear()
    {
        ShutterRemaining = 0f;
        FocusRemaining = 0f;
        RelayRemaining = 0f;
    }
}
