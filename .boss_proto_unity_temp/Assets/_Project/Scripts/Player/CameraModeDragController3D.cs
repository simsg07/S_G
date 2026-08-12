using UnityEngine;

internal sealed class CameraModeDragController3D
{
    public Vector2 ScreenPosition { get; private set; }

    public Vector2 Delta { get; private set; }

    public void Initialize(Vector2 pointerScreenPosition)
    {
        ScreenPosition = pointerScreenPosition;
        Delta = Vector2.zero;
    }

    public void Tick(
        bool hasPointer,
        Vector2 pointerDelta,
        float sensitivity,
        float unscaledDeltaTime,
        Vector2 screenSize)
    {
        if (!hasPointer)
        {
            return;
        }

        float unscaledFrameFactor = Mathf.Clamp(unscaledDeltaTime * 60f, 0f, 3f);
        Delta = pointerDelta * Mathf.Max(0f, sensitivity) * unscaledFrameFactor;
        ScreenPosition += Delta;
        ScreenPosition = new Vector2(
            Mathf.Clamp(ScreenPosition.x, 0f, screenSize.x),
            Mathf.Clamp(ScreenPosition.y, 0f, screenSize.y));
    }

    public void Reset()
    {
        Delta = Vector2.zero;
    }
}
