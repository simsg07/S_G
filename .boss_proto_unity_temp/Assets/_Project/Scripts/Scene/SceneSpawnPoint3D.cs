using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class SceneSpawnPoint3D : MonoBehaviour
{
    [Tooltip("Unique destination ID in this scene. Recommended: Spawn_Default or Spawn_From_Left.")]
    [SerializeField] private string spawnId = "Spawn_Default";
    [SerializeField] private bool isDefaultSpawn;
    [SerializeField] private bool canUseAsRespawnPoint = true;
    [SerializeField] private Vector3 spawnOffset;
    [SerializeField] private bool faceRight = true;
    [SerializeField] private bool showGizmo = true;
    [SerializeField] private bool debugMode = true;

    public string SpawnId => spawnId;
    public bool IsDefaultSpawn => isDefaultSpawn;
    public bool CanUseAsRespawnPoint => canUseAsRespawnPoint;
    public bool FaceRight => faceRight;

    public Vector3 GetSpawnPosition() => transform.position + spawnOffset;

    public bool Matches(string id) => string.Equals(spawnId, id, System.StringComparison.Ordinal);

    [ContextMenu("Validate SpawnPoint Setup")]
    public bool ValidateSpawnPointSetup()
    {
        bool valid = !string.IsNullOrWhiteSpace(spawnId);
        if (!valid) Debug.LogWarning("[SceneSpawnPoint3D] spawnId is missing.", this);

        SceneSpawnPoint3D[] points = FindObjectsByType<SceneSpawnPoint3D>(FindObjectsSortMode.None);
        int matches = 0;
        foreach (SceneSpawnPoint3D point in points) if (point != null && point.Matches(spawnId)) matches++;
        if (valid && matches > 1)
        {
            Debug.LogWarning($"[SceneSpawnPoint3D] Duplicate spawnId: {spawnId}", this);
            valid = false;
        }
        if (valid && debugMode) Debug.Log($"[SceneSpawnPoint3D] SpawnPoint OK: {spawnId}", this);
        return valid;
    }

    [ContextMenu("Print Spawn Info")]
    private void PrintSpawnInfo() => Debug.Log($"[SceneSpawnPoint3D] {spawnId}: {GetSpawnPosition()} / default={isDefaultSpawn} / faceRight={faceRight}", this);

    private void OnDrawGizmos()
    {
        if (!showGizmo) return;
        Vector3 position = GetSpawnPosition();
        Gizmos.color = isDefaultSpawn ? Color.green : Color.cyan;
        Gizmos.DrawWireCube(position, Vector3.one * (isDefaultSpawn ? 0.5f : 0.35f));
        Vector3 direction = faceRight ? Vector3.right : Vector3.left;
        Gizmos.DrawLine(position, position + direction * 0.8f);
#if UNITY_EDITOR
        Handles.color = Gizmos.color;
        Handles.Label(position + Vector3.up * 0.5f, isDefaultSpawn ? $"★ {spawnId} (Default)" : spawnId);
#endif
    }
}
