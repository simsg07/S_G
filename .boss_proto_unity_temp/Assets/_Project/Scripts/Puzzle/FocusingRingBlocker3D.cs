using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FocusingRingBlocker3D : MonoBehaviour
{
    private static readonly HashSet<FocusingRingBlocker3D> Active = new HashSet<FocusingRingBlocker3D>();
    [SerializeField] private bool blocksWhileEnabled = true;

    public static bool IsBlocked
    {
        get
        {
            foreach (FocusingRingBlocker3D blocker in Active)
                if (blocker != null && blocker.blocksWhileEnabled && blocker.isActiveAndEnabled) return true;
            return false;
        }
    }

    private void OnEnable() => Active.Add(this);
    private void OnDisable() => Active.Remove(this);
}
