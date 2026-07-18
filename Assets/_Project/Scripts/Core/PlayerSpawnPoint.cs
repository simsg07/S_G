using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class PlayerSpawnPoint : MonoBehaviour
{
    [Header("Spawn Point")]
    [Tooltip("ID used by StageExitTrigger.targetSpawnPointId. IDs should be unique inside one scene.")]
    [SerializeField] private string spawnPointId = "Default";
    [Tooltip("Used as SceneLoader fallback when the requested ID cannot be found.")]
    [SerializeField] private bool isDefaultSpawn = true;
    [Tooltip("Allow PlayerDamageReceiver to use this point as a respawn destination.")]
    [SerializeField] private bool canUseAsRespawnPoint = true;
    [Tooltip("Designer note for intended facing after spawn. SceneLoader keeps Player logic unchanged and does not force flip by default.")]
    [SerializeField] private bool faceRightOnSpawn = true;
    [Tooltip("Print spawn related debug information when needed.")]
    [SerializeField] private bool debugMode = true;

    [Header("Scene Gizmo")]
    [Tooltip("Draw a Scene view marker so designers can see the spawn position.")]
    [SerializeField] private bool showGizmos = true;
    [Tooltip("Radius of the Scene view spawn marker.")]
    [SerializeField] private float gizmoRadius = 0.3f;

    public string SpawnPointId => spawnPointId;
    public string ResolvedSpawnPointId => string.IsNullOrWhiteSpace(spawnPointId) ? "Default" : spawnPointId;
    public bool FaceRightOnSpawn => faceRightOnSpawn;
    public bool IsDefaultSpawn => isDefaultSpawn;
    public bool CanUseAsRespawnPoint => canUseAsRespawnPoint;

    public bool Matches(string id)
    {
        string targetId = string.IsNullOrWhiteSpace(id) ? "Default" : id;
        return string.Equals(ResolvedSpawnPointId, targetId, System.StringComparison.Ordinal);
    }

    private void OnValidate()
    {
        gizmoRadius = Mathf.Max(0.05f, gizmoRadius);
        if (debugMode && string.IsNullOrWhiteSpace(spawnPointId))
        {
            Debug.LogWarning("[PlayerSpawnPoint] spawnPointId is empty. SceneLoader will treat empty IDs as Default.", this);
        }
    }

    [ContextMenu("Validate Spawn Point")]
    public void ValidateSpawnPoint()
    {
        string resolvedId = ResolvedSpawnPointId;
        PlayerSpawnPoint[] spawnPoints = FindObjectsByType<PlayerSpawnPoint>(FindObjectsSortMode.None);
        int duplicateCount = 0;

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i] == null)
            {
                continue;
            }

            if (spawnPoints[i].Matches(resolvedId))
            {
                duplicateCount++;
            }
        }

        Debug.Log(
            "[PlayerSpawnPoint] Validate Spawn Point\n" +
            $"- Object: {name}\n" +
            $"- spawnPointId: {(string.IsNullOrWhiteSpace(spawnPointId) ? "(empty -> Default)" : spawnPointId)}\n" +
            $"- position: {transform.position}\n" +
            $"- faceRightOnSpawn: {faceRightOnSpawn}\n" +
            $"- same ID count in current scene: {duplicateCount}",
            this);

        if (string.IsNullOrWhiteSpace(spawnPointId))
        {
            Debug.LogWarning("[PlayerSpawnPoint] spawnPointId is empty. Use Default only for the fallback spawn point.", this);
        }

        if (duplicateCount > 1)
        {
            Debug.LogWarning($"[PlayerSpawnPoint] Duplicate spawnPointId found: {resolvedId}", this);
        }
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

        Vector3 facingDirection = faceRightOnSpawn ? Vector3.right : Vector3.left;
        Gizmos.DrawLine(transform.position, transform.position + facingDirection * (gizmoRadius * 1.8f));

#if UNITY_EDITOR
        Handles.color = Color.cyan;
        Handles.Label(transform.position + Vector3.up * (gizmoRadius * 1.5f), ResolvedSpawnPointId);
#endif
    }
}
