using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class Checkpoint3D : MonoBehaviour, IInteractable3D
{
    [Header("Checkpoint Identity")]
    [SerializeField] private string checkpointId = "SceneName_CP_01";

    [Header("Interaction")]
    [Tooltip("The shared PlayerInteraction3D input is F by default. This value documents the expected shared key; Checkpoint3D does not poll input independently.")]
    [SerializeField] private Key interactionKey = Key.F;
    [SerializeField] private Collider interactionTrigger;
    [SerializeField] private Transform linkedSpawnPoint;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite inactiveSprite;
    [SerializeField] private Sprite activeSprite;
    [SerializeField] private bool showInteractionGuide = true;
    [SerializeField] private UnityEvent onCheckpointSaved;

    [Header("Debug")]
    [SerializeField] private bool debugMode;
    [SerializeField] private bool isActive;

    public string CheckpointId => checkpointId;
    public Key InteractionKey => interactionKey;
    public Transform LinkedSpawnPoint => linkedSpawnPoint;
    public bool IsActive => isActive;
    public Transform SpawnPosition => linkedSpawnPoint != null ? linkedSpawnPoint : transform;

    private void Awake()
    {
        RestoreSavedVisualState();
        RefreshVisual();
    }

    private void OnEnable()
    {
        RestoreSavedVisualState();
        RefreshVisual();
    }

    public bool TryInteract(GameObject actor)
    {
        if (actor == null || actor.GetComponentInParent<PlatformerPlayer3D>() == null)
        {
            return false;
        }

        if (interactionTrigger == null || !interactionTrigger.enabled || !IsInsideInteractionRange(actor.transform.position))
        {
            return false;
        }

        ActivateCheckpoint();
        return true;
    }

    public void ActivateCheckpoint()
    {
        bool wasAlreadyActive = isActive;
        GameProgressSave3D.RecordCheckpointActivated(SceneManager.GetActiveScene().name, checkpointId);
        isActive = true;
        RefreshVisual();
        onCheckpointSaved?.Invoke();
        Log(wasAlreadyActive
            ? $"Checkpoint '{checkpointId}' selected again as the latest respawn point."
            : $"Activated checkpoint '{checkpointId}'. Visual changed to charger_01.");
    }

    private void RestoreSavedVisualState()
    {
        isActive = !string.IsNullOrWhiteSpace(checkpointId)
            && GameProgressSave3D.IsCheckpointActivated(checkpointId);
    }

    private bool IsInsideInteractionRange(Vector3 actorPosition)
    {
        Vector3 closest = interactionTrigger.ClosestPoint(actorPosition);
        return (closest - actorPosition).sqrMagnitude <= 0.0001f;
    }

    private void RefreshVisual()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = isActive ? activeSprite : inactiveSprite;
        }
    }

    private void OnValidate()
    {
        if (interactionTrigger != null && !interactionTrigger.isTrigger)
        {
            interactionTrigger.isTrigger = true;
        }

        RefreshVisual();
        if (string.IsNullOrWhiteSpace(checkpointId))
        {
            Debug.LogWarning("[Checkpoint3D] checkpointId is required and must be stable.", this);
            return;
        }

        Checkpoint3D[] checkpoints = FindObjectsByType<Checkpoint3D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int matches = 0;
        foreach (Checkpoint3D checkpoint in checkpoints)
        {
            if (checkpoint != null
                && checkpoint.gameObject.scene == gameObject.scene
                && string.Equals(checkpoint.checkpointId, checkpointId, System.StringComparison.Ordinal))
            {
                matches++;
            }
        }

        if (matches > 1)
        {
            Debug.LogWarning($"[Checkpoint3D] Duplicate checkpointId in scene: '{checkpointId}'.", this);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showInteractionGuide || interactionTrigger == null)
        {
            return;
        }

        Bounds bounds = interactionTrigger.bounds;
        Gizmos.color = new Color(1f, 0.8f, 0.15f, 0.8f);
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }

    private void Log(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[Checkpoint3D] {message}", this);
        }
    }
}
