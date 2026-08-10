using UnityEngine;
using UnityEngine.InputSystem;

internal readonly struct CameraInputSnapshot3D
{
    public CameraInputSnapshot3D(
        bool hasMouse,
        bool hasSecondaryInput,
        bool secondaryPressedThisFrame,
        bool secondaryHeld,
        bool secondaryReleasedThisFrame,
        bool primaryPressedThisFrame,
        bool worldSwitchPressedThisFrame,
        bool lightPressedThisFrame,
        Vector2 pointerScreenPosition,
        Vector2 pointerDelta)
    {
        HasMouse = hasMouse;
        HasSecondaryInput = hasSecondaryInput;
        SecondaryPressedThisFrame = secondaryPressedThisFrame;
        SecondaryHeld = secondaryHeld;
        SecondaryReleasedThisFrame = secondaryReleasedThisFrame;
        PrimaryPressedThisFrame = primaryPressedThisFrame;
        WorldSwitchPressedThisFrame = worldSwitchPressedThisFrame;
        LightPressedThisFrame = lightPressedThisFrame;
        PointerScreenPosition = pointerScreenPosition;
        PointerDelta = pointerDelta;
    }

    public bool HasMouse { get; }
    public bool HasSecondaryInput { get; }
    public bool SecondaryPressedThisFrame { get; }
    public bool SecondaryHeld { get; }
    public bool SecondaryReleasedThisFrame { get; }
    public bool PrimaryPressedThisFrame { get; }
    public bool WorldSwitchPressedThisFrame { get; }
    public bool LightPressedThisFrame { get; }
    public Vector2 PointerScreenPosition { get; }
    public Vector2 PointerDelta { get; }
}

internal sealed class CameraModeInputReader3D
{
    public CameraInputSnapshot3D Read(
        bool readPrimaryMouse,
        bool readSecondaryMouse,
        Key worldSwitchKey,
        Key lightToggleKey)
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        bool hasMouse = mouse != null;

        bool secondaryPressedThisFrame = readSecondaryMouse
            && mouse != null
            && mouse.rightButton.wasPressedThisFrame;
        bool secondaryHeld = readSecondaryMouse
            && mouse != null
            && mouse.rightButton.isPressed;
        bool secondaryReleasedThisFrame = readSecondaryMouse
            && mouse != null
            && mouse.rightButton.wasReleasedThisFrame;
        bool primaryPressedThisFrame = readPrimaryMouse
            && mouse != null
            && mouse.leftButton.wasPressedThisFrame;
        bool worldSwitchPressedThisFrame = WasKeyPressed(keyboard, worldSwitchKey);
        bool lightPressedThisFrame = WasKeyPressed(keyboard, lightToggleKey);
        Vector2 pointerScreenPosition = mouse != null
            ? mouse.position.ReadValue()
            : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 pointerDelta = mouse != null
            ? mouse.delta.ReadValue()
            : Vector2.zero;

        return new CameraInputSnapshot3D(
            hasMouse,
            readSecondaryMouse && mouse != null,
            secondaryPressedThisFrame,
            secondaryHeld,
            secondaryReleasedThisFrame,
            primaryPressedThisFrame,
            worldSwitchPressedThisFrame,
            lightPressedThisFrame,
            pointerScreenPosition,
            pointerDelta);
    }

    public Vector2 ReadPointerScreenPosition(Vector2 fallback)
    {
        Mouse mouse = Mouse.current;
        return mouse != null ? mouse.position.ReadValue() : fallback;
    }

    private static bool WasKeyPressed(Keyboard keyboard, Key key)
    {
        return keyboard != null && key != Key.None && keyboard[key].wasPressedThisFrame;
    }
}
