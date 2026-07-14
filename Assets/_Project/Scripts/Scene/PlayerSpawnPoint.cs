using UnityEngine;

public class PlayerSpawnPoint : MonoBehaviour
{
    [SerializeField] private string spawnPointId = "Default";
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private float gizmoRadius = 0.3f;

    public string SpawnPointId => spawnPointId;

    public bool Matches(string id)
    {
        string targetId = string.IsNullOrWhiteSpace(id) ? "Default" : id;
        return string.Equals(spawnPointId, targetId, System.StringComparison.Ordinal);
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, gizmoRadius);
        Gizmos.DrawLine(transform.position + Vector3.left * gizmoRadius, transform.position + Vector3.right * gizmoRadius);
        Gizmos.DrawLine(transform.position + Vector3.down * gizmoRadius, transform.position + Vector3.up * gizmoRadius);
    }
}
