using UnityEngine;

internal sealed class CameraAbilityCooldowns3D
{
    public float ShutterRemaining { get; private set; }
    public float FocusRemaining { get; private set; }
    public float RelayRemaining { get; private set; }

    public bool CanUseShutter => true;
    public bool CanUseFocus => true;
    public bool CanUseRelay => true;

    public void Tick(float unscaledDeltaTime)
    {
        ShutterRemaining = Mathf.Max(0f, ShutterRemaining - unscaledDeltaTime);
        FocusRemaining = Mathf.Max(0f, FocusRemaining - unscaledDeltaTime);
        RelayRemaining = Mathf.Max(0f, RelayRemaining - unscaledDeltaTime);
    }

    public void StartShutter(float duration)
    {
        ShutterRemaining = 0f;
    }

    public void StartFocus(float duration)
    {
        FocusRemaining = 0f;
    }

    public void StartRelay(float duration)
    {
        RelayRemaining = 0f;
    }

    public void Clear()
    {
        ShutterRemaining = 0f;
        FocusRemaining = 0f;
        RelayRemaining = 0f;
    }
}
