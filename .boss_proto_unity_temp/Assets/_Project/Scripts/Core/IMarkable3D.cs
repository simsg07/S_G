public interface IMarkable3D
{
    bool ApplyMark(float duration, CameraAbilitySystem3D source);
}

public interface IMarkState3D
{
    bool IsMarked { get; }
}

/// <summary>
/// Player-toggled Mark state layered on top of the current world-presence policy.
/// Registration is instance-scoped and owned by ShutterTargetRegistry3D.
/// </summary>
public interface IShutterFreezable3D
{
    bool IsShutterFrozen { get; }
    void ReapplyShutterFreeze();
    void ReleaseShutterFreeze();
}
