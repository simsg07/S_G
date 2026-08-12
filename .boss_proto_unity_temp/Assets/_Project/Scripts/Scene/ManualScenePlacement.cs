using UnityEngine;

/// <summary>
/// Marks scene connection objects whose authored Transform belongs to the scene instance.
/// This component intentionally contains no edit-mode or runtime placement behaviour.
/// </summary>
[DisallowMultipleComponent]
public sealed class ManualScenePlacement : MonoBehaviour
{
    [Tooltip("Only used when this connection object does not exist yet.")]
    [SerializeField] private bool autoPlaceOnCreate = true;
    [Tooltip("Automatic setup tools must not change this scene instance's Transform.")]
    [SerializeField] private bool lockManualPosition = true;

    public bool AutoPlaceOnCreate => autoPlaceOnCreate;
    public bool LockManualPosition => lockManualPosition;
}
