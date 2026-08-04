using UnityEngine;

[DisallowMultipleComponent]
public class MonsterFacing : MonoBehaviour
{
    [Header("Facing Settings")]
    [Tooltip("Turn off to prevent visual facing changes.")]
    public bool enableFacing = true;

    [Tooltip("Visual transform to flip. Root transform scale is not changed.")]
    public Transform visualRoot;

    [Tooltip("True if the source image faces right. False if it faces left.")]
    public bool visualFacesRightByDefault = true;

    [Tooltip("Extra correction when a specific monster prefab is reversed.")]
    public bool invertFacing;

    [Tooltip("Only face target while the target is detected or visible.")]
    public bool faceOnlyWhenDetected = true;

    [Header("Debug")]
    public bool debugMode;

    private void Reset()
    {
        if (visualRoot == null)
        {
            Animator animator = GetComponentInChildren<Animator>(true);
            visualRoot = animator != null ? animator.transform : transform;
        }
    }
}
