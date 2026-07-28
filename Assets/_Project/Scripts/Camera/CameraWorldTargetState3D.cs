using System;

public enum CameraWorldTargetState3D
{
    Available,
    Disabled,
    Cooldown,
    Transitioning
}

[Serializable]
public readonly struct CameraWorldTargetInfo3D
{
    public CameraWorldTargetInfo3D(WorldSwitchable target, CameraWorldTargetState3D state)
    {
        Target = target;
        State = state;
    }

    public WorldSwitchable Target { get; }
    public CameraWorldTargetState3D State { get; }
    public bool IsAvailable => State == CameraWorldTargetState3D.Available;
}
