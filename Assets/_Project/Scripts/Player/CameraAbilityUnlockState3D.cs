internal sealed class CameraAbilityUnlockState3D
{
    public const CameraAbilityFlags KnownAbilityMask = CameraAbilityFlags.Shutter | CameraAbilityFlags.Focus;

    private CameraAbilityFlags unlockedAbilities;

    public CameraAbilityUnlockState3D(CameraAbilityFlags initialAbilities)
    {
        unlockedAbilities = Clamp(initialAbilities);
    }

    public CameraAbilityFlags UnlockedAbilities => unlockedAbilities;

    public bool IsUnlocked(CameraAbilityId ability)
    {
        CameraAbilityFlags flag = ToFlag(ability);
        return flag != CameraAbilityFlags.None
            && (KnownAbilityMask & flag) != 0
            && (unlockedAbilities & flag) != 0;
    }

    public bool TryUnlock(CameraAbilityId ability)
    {
        CameraAbilityFlags flag = ToFlag(ability);
        if (flag == CameraAbilityFlags.None || (KnownAbilityMask & flag) == 0)
        {
            return false;
        }

        if ((unlockedAbilities & flag) != 0)
        {
            return false;
        }

        unlockedAbilities |= flag;
        return true;
    }

    public bool Merge(CameraAbilityFlags abilities)
    {
        CameraAbilityFlags merged = Clamp(unlockedAbilities | abilities);
        if (merged == unlockedAbilities)
        {
            return false;
        }

        unlockedAbilities = merged;
        return true;
    }

    public static CameraAbilityFlags ToFlag(CameraAbilityId ability)
    {
        switch (ability)
        {
            case CameraAbilityId.Shutter:
                return CameraAbilityFlags.Shutter;
            case CameraAbilityId.Focus:
                return CameraAbilityFlags.Focus;
            case CameraAbilityId.Flash:
                return CameraAbilityFlags.Flash;
            case CameraAbilityId.Relay:
                return CameraAbilityFlags.Relay;
            default:
                return CameraAbilityFlags.None;
        }
    }

    private static CameraAbilityFlags Clamp(CameraAbilityFlags value)
    {
        return value & KnownAbilityMask;
    }
}
