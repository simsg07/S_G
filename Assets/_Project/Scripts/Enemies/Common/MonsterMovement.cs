using UnityEngine;

[DisallowMultipleComponent]
public class MonsterMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Turn off to prevent AI-driven movement.")]
    public bool enableMovement = true;

    [Tooltip("Ground moves on X with gravity. Flying moves on X/Y without gravity.")]
    public MonsterMovementType movementType = MonsterMovementType.Ground;

    [Tooltip("Normal chase speed.")]
    public float moveSpeed = 1f;

    [Tooltip("Temporary test speed used when useTestMoveSpeed is on.")]
    public float testMoveSpeed = 1f;

    [Tooltip("Use testMoveSpeed instead of moveSpeed for quick tuning.")]
    public bool useTestMoveSpeed;

    [Tooltip("Speed used when returning to home position.")]
    public float returnSpeed = 1f;

    [Tooltip("Distance where movement can stop near target.")]
    public float stopDistance = 0.1f;

    [Tooltip("Keep Z fixed for 2.5D gameplay.")]
    public bool lockZPosition = true;

    [Tooltip("Return to the initial scene position when target is lost.")]
    public bool returnToHomeWhenLost = true;

    [Tooltip("Prevent walls, tiles, and floors from being passed through.")]
    public bool blockMovementByObstacles = true;

    [Tooltip("Layers that block horizontal movement.")]
    public LayerMask movementObstacleLayerMask;

    [Tooltip("Layers considered ground for Ground monsters.")]
    public LayerMask groundLayerMask;

    [Tooltip("Ground monsters move only on X. Y remains controlled by gravity.")]
    public bool groundOnlyMoveX = true;

    [Tooltip("Ground monsters use Rigidbody gravity.")]
    public bool useGravityForGround = true;

    [Tooltip("Reduce unwanted push from Player or other physics contacts.")]
    public bool preventExternalPush = true;

    [Tooltip("Linear damping applied to Ground Rigidbody movement.")]
    public float groundLinearDamping = 5f;

    [Tooltip("When idle/howling/attacking, keep horizontal velocity at zero.")]
    public bool stopHorizontalVelocityWhenIdle = true;

    [Header("Debug")]
    public bool debugMode;
    public bool showGizmos = true;

    public float ActiveMoveSpeed => useTestMoveSpeed ? testMoveSpeed : moveSpeed;

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        testMoveSpeed = Mathf.Max(0f, testMoveSpeed);
        returnSpeed = Mathf.Max(0f, returnSpeed);
        stopDistance = Mathf.Max(0f, stopDistance);
        groundLinearDamping = Mathf.Max(0f, groundLinearDamping);
    }
}
