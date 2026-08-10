using UnityEngine;

public enum SwitchActivationSource
{
    Player,
    Stone,
    CircleSpike
}

public interface ISwitchActivation3D
{
    bool TryActivate(SwitchActivationSource source, GameObject instigator);
}
