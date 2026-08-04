using UnityEngine;
using UnityEngine.Serialization;

[ExecuteAlways]
[DisallowMultipleComponent]
public class GridSnapper : MonoBehaviour
{
    [Header("Placement")]
    [Tooltip("Snaps this map object to the placement grid without changing its scale or layer.")]
    [SerializeField] private bool enableSnap = true;
    [Tooltip("Keeps the object aligned while it is moved in the Scene view.")]
    [SerializeField] private bool snapInEditMode = true;
    [Tooltip("Normally disabled so authored map positions are not changed during play.")]
    [SerializeField] private bool snapInPlayMode;
    [Tooltip("Size of one map cell. The standard map prefab value is 1 unit.")]
    [SerializeField] private float gridSize = 1f;

    [Header("Axes")]
    [SerializeField] private bool snapX = true;
    [SerializeField] private bool snapY = true;
    [Tooltip("Leave disabled for standard 2.5D map placement so Z is preserved.")]
    [SerializeField] private bool snapZ;
    [Tooltip("Optionally fixes Z to an explicit value. This does not affect X/Y snapping.")]
    [SerializeField] private bool zLockEnabled;
    [SerializeField] private float lockedZ;
    [FormerlySerializedAs("gridOrigin")]
    [Tooltip("Grid origin offset. Use 0.5 on an axis for half-cell placement.")]
    [SerializeField] private Vector3 offset;

    [Header("Debug")]
    [SerializeField] private bool debugMode;

    public float GridSize => gridSize;

    private void OnValidate()
    {
        gridSize = Mathf.Max(0.01f, gridSize);
        if (enableSnap && !Application.isPlaying && snapInEditMode)
        {
            SnapNow();
        }
    }

    private void Update()
    {
        if (!enableSnap || !transform.hasChanged)
        {
            return;
        }

        if ((Application.isPlaying && !snapInPlayMode) || (!Application.isPlaying && !snapInEditMode))
        {
            return;
        }

        SnapNow();
        transform.hasChanged = false;
    }

    [ContextMenu("Snap To Grid")]
    public void SnapNow()
    {
        if (!enableSnap || (Application.isPlaying && !snapInPlayMode))
        {
            return;
        }

        Vector3 previousPosition = transform.position;
        Vector3 position = previousPosition;
        if (snapX)
        {
            position.x = Snap(position.x, offset.x);
        }

        if (snapY)
        {
            position.y = Snap(position.y, offset.y);
        }

        if (zLockEnabled)
        {
            position.z = lockedZ;
        }
        else if (snapZ)
        {
            position.z = Snap(position.z, offset.z);
        }

        transform.position = position;

        if (debugMode && previousPosition != position)
        {
            Debug.Log($"[GridSnapper] '{name}' snapped from {previousPosition} to {position}.", this);
        }
    }

    private float Snap(float value, float origin)
    {
        return origin + Mathf.Round((value - origin) / gridSize) * gridSize;
    }
}
