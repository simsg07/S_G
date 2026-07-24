public interface IShutterFreezable3D
{
    bool ApplyShutterFreeze(float duration, CameraAbilitySystem3D source);
}

public interface IShutterFreezeState3D
{
    bool IsShutterFrozen { get; }
}
