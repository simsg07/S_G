public interface IMarkable3D
{
    bool ApplyMark(float duration, CameraAbilitySystem3D source);
}

public interface IMarkState3D
{
    bool IsMarked { get; }
}
