using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using System.Collections.Generic;

public enum CheckpointState
{
    IDLE = 0,
    ACTIVE_CURRENT = 1,
    IN_RANGE = 2
}

[DisallowMultipleComponent]
public sealed class Checkpoint3D : MonoBehaviour, IInteractable3D
{
    private static readonly List<Checkpoint3D> AvailableCheckpoints = new List<Checkpoint3D>(8);
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
    [Tooltip("Invoked only on the first transition from IDLE to ACTIVATED.")]
    [SerializeField] private UnityEvent onCheckpointActivated;
    [Tooltip("Invoked after every successful Player save, including reuse while ACTIVATED.")]
    [SerializeField] private UnityEvent onCheckpointSaved;

    [Header("Persistent Progress Event")]
    [Tooltip("Optional scene-local progress key used by MarkConfiguredProgressCompleted().")]
    [SerializeField] private string progressKey;

    [Header("Debug")]
    [SerializeField] private bool debugMode;
    [FormerlySerializedAs("isActive")]
    [SerializeField] private CheckpointState currentState;
    [SerializeField] private bool savedActivationFound;
    [SerializeField] private bool playerInRange;

    private readonly Dictionary<int, int> playerContactCounts = new Dictionary<int, int>();

    public string CheckpointId => checkpointId;
    public Key InteractionKey => interactionKey;
    public Transform LinkedSpawnPoint => linkedSpawnPoint;
    public bool IsActive => currentState == CheckpointState.ACTIVE_CURRENT;
    public CheckpointState CurrentState => currentState;
    public Transform SpawnPosition => linkedSpawnPoint != null ? linkedSpawnPoint : transform;
    public bool HasPlayerInRange => playerInRange;
    public string InteractionPrompt => showInteractionGuide && playerInRange ? "[F] 저장" : string.Empty;

    private void Awake()
    {
        EnsureTriggerRelay();
        RestoreSavedVisualState();
        RefreshVisual();
    }

    private void OnEnable()
    {
        GameProgressSave3D.ActiveCheckpointChanged -= HandleActiveCheckpointChanged;
        GameProgressSave3D.ActiveCheckpointChanged += HandleActiveCheckpointChanged;
        EnsureTriggerRelay();
        if (!AvailableCheckpoints.Contains(this)) AvailableCheckpoints.Add(this);
        RestoreSavedVisualState();
        RefreshVisual();
    }

    private void OnDisable()
    {
        GameProgressSave3D.ActiveCheckpointChanged -= HandleActiveCheckpointChanged;
        playerContactCounts.Clear();
        playerInRange = false;
        AvailableCheckpoints.Remove(this);
    }

    public bool TryInteract(GameObject actor)
    {
        PlatformerPlayer3D player = actor != null ? actor.GetComponentInParent<PlatformerPlayer3D>() : null;
        if (player == null)
        {
            return false;
        }

        PlayerDamageReceiver damageReceiver = player.GetComponent<PlayerDamageReceiver>();
        if (GameplayInputLock3D.IsLocked || Time.timeScale <= 0f || player.ControlsLocked ||
            (damageReceiver != null && damageReceiver.IsDead) || !IsPlayerInRange(player.transform))
        {
            return false;
        }

        ActivateCheckpointForPlayer();
        return true;
    }

    public static bool TryInteractNearest(GameObject actor)
    {
        PlatformerPlayer3D player = actor != null ? actor.GetComponentInParent<PlatformerPlayer3D>() : null;
        if (player == null) return false;
        Checkpoint3D best = null;
        float bestDistance = float.PositiveInfinity;
        for (int i = AvailableCheckpoints.Count - 1; i >= 0; i--)
        {
            Checkpoint3D checkpoint = AvailableCheckpoints[i];
            if (checkpoint == null)
            {
                AvailableCheckpoints.RemoveAt(i);
                continue;
            }
            if (!checkpoint.IsPlayerInRange(player.transform)) continue;
            float distance = (checkpoint.SpawnPosition.position - player.transform.position).sqrMagnitude;
            if (distance >= bestDistance) continue;
            best = checkpoint;
            bestDistance = distance;
        }
        return best != null && best.TryInteract(player.gameObject);
    }

    public void NotifyPlayerTriggerEnter(Collider other)
    {
        PlatformerPlayer3D player = other != null ? other.GetComponentInParent<PlatformerPlayer3D>() : null;
        if (player == null) return;
        int id = player.transform.root.GetInstanceID();
        playerContactCounts.TryGetValue(id, out int count);
        playerContactCounts[id] = count + 1;
        playerInRange = true;
        if (!IsCurrentSavedCheckpoint()) currentState = CheckpointState.IN_RANGE;
        RefreshVisual();
        if (!AvailableCheckpoints.Contains(this)) AvailableCheckpoints.Add(this);
    }

    public void NotifyPlayerTriggerExit(Collider other)
    {
        PlatformerPlayer3D player = other != null ? other.GetComponentInParent<PlatformerPlayer3D>() : null;
        if (player == null) return;
        int id = player.transform.root.GetInstanceID();
        if (!playerContactCounts.TryGetValue(id, out int count)) return;
        if (count <= 1) playerContactCounts.Remove(id);
        else playerContactCounts[id] = count - 1;
        playerInRange = playerContactCounts.Count > 0;
        if (!playerInRange) RestoreSavedVisualState();
    }

    private void ActivateCheckpointForPlayer()
    {
        bool wasAlreadyActive = IsActive;
        Transform spawn = SpawnPosition;
        GameProgressSave3D.RecordCheckpointActivated(
            gameObject.scene.name,
            checkpointId,
            spawn.position,
            spawn.rotation,
            WorldSystem3D.ActiveWorld);
        if (!wasAlreadyActive)
        {
            onCheckpointActivated?.Invoke();
        }
        onCheckpointSaved?.Invoke();
        Log(wasAlreadyActive
            ? $"Checkpoint '{checkpointId}' selected again as the latest respawn point."
            : $"Activated checkpoint '{checkpointId}'. Visual changed to charger_01.");
    }

    private void RestoreSavedVisualState()
    {
        savedActivationFound = IsCurrentSavedCheckpoint();
        currentState = savedActivationFound
            ? CheckpointState.ACTIVE_CURRENT
            : playerInRange ? CheckpointState.IN_RANGE : CheckpointState.IDLE;
        RefreshVisual();
    }

    private bool IsCurrentSavedCheckpoint()
    {
        return !string.IsNullOrWhiteSpace(checkpointId)
            && GameProgressSave3D.IsCurrentActiveCheckpoint(gameObject.scene.name, checkpointId);
    }

    private void HandleActiveCheckpointChanged(
        string previousScene, string previousId, string currentScene, string currentId)
    {
        RestoreSavedVisualState();
    }

    public void MarkConfiguredProgressCompleted()
    {
        if (string.IsNullOrWhiteSpace(progressKey))
        {
            Debug.LogWarning("[Checkpoint3D] Progress Key is empty. Permanent progress was not recorded.", this);
            return;
        }

        string sceneQualifiedKey = $"{SceneManager.GetActiveScene().name}.{progressKey.Trim()}";
        GameProgressSave3D.RecordPuzzlePermanentlyCompleted(sceneQualifiedKey);
    }

    private bool IsPlayerInRange(Transform player)
    {
        if (player == null || interactionTrigger == null || !interactionTrigger.enabled ||
            !interactionTrigger.gameObject.activeInHierarchy) return false;
        int id = player.root.GetInstanceID();
        if (playerContactCounts.TryGetValue(id, out int count) && count > 0) return true;
        Vector3 position = player.position;
        Vector3 closest = interactionTrigger.ClosestPoint(position);
        return (closest - position).sqrMagnitude <= 0.0001f;
    }

    private void EnsureTriggerRelay()
    {
        if (interactionTrigger == null) return;
        CheckpointInteractionTrigger3D relay = interactionTrigger.GetComponent<CheckpointInteractionTrigger3D>();
        if (relay == null && Application.isPlaying)
            relay = interactionTrigger.gameObject.AddComponent<CheckpointInteractionTrigger3D>();
        relay?.Bind(this);
    }

    private void RefreshVisual()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = IsActive ? activeSprite : inactiveSprite;
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
