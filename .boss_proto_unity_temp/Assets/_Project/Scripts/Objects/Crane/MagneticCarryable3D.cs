using UnityEngine;

[DisallowMultipleComponent]
public sealed class MagneticCarryable3D : MonoBehaviour
{
    [Header("Magnetic Permission")]
    [SerializeField] private bool canBeMovedByMagnet = true;
    [SerializeField] private Transform magneticAnchorOverride;
    [Min(0f)] [SerializeField] private float massOverride;
    [SerializeField] private bool preserveRotation = true;
    [SerializeField] private bool lockRotationWhileHeld = true;
    [SerializeField] private bool allowWorldA = true;
    [SerializeField] private bool allowWorldB = true;
    [SerializeField] private bool canBeReleased = true;

    [Header("State Restoration")]
    [SerializeField] private bool restoreOriginalParent = true;
    [SerializeField] private bool restoreRigidbodyState = true;
    [SerializeField] private bool restoreGravityState = true;
    [SerializeField] private bool restoreConstraints = true;
    [SerializeField] private Vector3 magnetHoldOffset;

    private CraneMagnetController3D owner;

    public bool CanBeMovedByMagnet => canBeMovedByMagnet;
    public Transform MagneticAnchor => magneticAnchorOverride != null ? magneticAnchorOverride : transform;
    public float MassOverride => massOverride;
    public bool PreserveRotation => preserveRotation;
    public bool LockRotationWhileHeld => lockRotationWhileHeld;
    public bool CanBeReleased => canBeReleased;
    public bool RestoreOriginalParent => restoreOriginalParent;
    public bool RestoreRigidbodyState => restoreRigidbodyState;
    public bool RestoreGravityState => restoreGravityState;
    public bool RestoreConstraints => restoreConstraints;
    public Vector3 MagnetHoldOffset => magnetHoldOffset;
    public bool IsReserved => owner != null;

    public bool IsAllowedInCurrentWorld()
    {
        ResearchWorldId world = WorldSystem3D.ActiveWorld;
        return world == ResearchWorldId.WorldA ? allowWorldA : allowWorldB;
    }

    public Rigidbody ResolveRigidbody() => GetComponent<Rigidbody>() ?? GetComponentInParent<Rigidbody>();

    public bool TryReserve(CraneMagnetController3D magnet)
    {
        if (magnet == null || (owner != null && owner != magnet)) return false;
        owner = magnet;
        return true;
    }

    public void ReleaseReservation(CraneMagnetController3D magnet)
    {
        if (owner == magnet) owner = null;
    }
}
