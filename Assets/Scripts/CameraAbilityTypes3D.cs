using System;

public enum CameraAbilityId
{
    Shutter,
    Focus,
    Flash,
    Relay
}

[Flags]
public enum CameraAbilityFlags
{
    None = 0,
    Shutter = 1 << 0,
    Focus = 1 << 1,
    Flash = 1 << 2,
    Relay = 1 << 3
}
