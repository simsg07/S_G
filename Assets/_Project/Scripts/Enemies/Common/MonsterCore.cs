using UnityEngine;

[DisallowMultipleComponent]
public class MonsterCore : MonoBehaviour
{
    [Header("Core References")]
    [Tooltip("Target player. If empty, MonsterAIBase can still find the Player by tag.")]
    public Transform playerTarget;

    [Tooltip("Target light. Used only by monsters that can detect light.")]
    public Transform lightTarget;

    [Tooltip("Visual root to flip or animate. Root transform scale is not changed.")]
    public Transform visualRoot;

    [Tooltip("Optional Rigidbody used by this monster.")]
    public Rigidbody monsterRigidbody;

    [Tooltip("Main collider used for movement checks.")]
    public Collider mainCollider;

    [Header("Debug")]
    public bool debugMode;

    public void AutoFill()
    {
        if (monsterRigidbody == null)
        {
            monsterRigidbody = GetComponent<Rigidbody>();
        }

        if (mainCollider == null)
        {
            mainCollider = GetComponent<Collider>();
        }

        if (visualRoot == null)
        {
            Animator animator = GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                visualRoot = animator.transform;
            }
            else
            {
                Renderer renderer = GetComponentInChildren<Renderer>(true);
                visualRoot = renderer != null ? renderer.transform : transform;
            }
        }
    }

    private void Reset()
    {
        AutoFill();
    }

    private void OnValidate()
    {
        AutoFill();
    }
}
