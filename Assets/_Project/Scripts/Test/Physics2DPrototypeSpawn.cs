using UnityEngine;

[DisallowMultipleComponent]
public sealed class Physics2DPrototypeSpawn : MonoBehaviour
{
    [SerializeField] private Rigidbody2D player;
    [SerializeField, Min(1f)] private float resetBelowY = 8f;

    private void Start() => MovePlayerToSpawn();

    private void Update()
    {
        if (player != null && player.position.y < transform.position.y - resetBelowY)
        {
            MovePlayerToSpawn();
        }
    }

    private void MovePlayerToSpawn()
    {
        if (player == null) return;
        player.position = transform.position;
        player.linearVelocity = Vector2.zero;
        player.angularVelocity = 0f;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.25f);
        Gizmos.DrawLine(transform.position + Vector3.left * 0.4f, transform.position + Vector3.right * 0.4f);
    }
}
