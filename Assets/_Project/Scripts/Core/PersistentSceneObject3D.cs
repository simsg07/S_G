using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public enum PersistentSceneObjectType
{
    Destructible,
    Collectible,
    Device,
    OneShotEvent
}

public enum PersistentSceneObjectState
{
    Exists,
    Destroyed,
    Collected,
    Activated,
    Deactivated
}

public enum PersistentResetPolicy
{
    KeepSavedState,
    ResetWhenSaveIsCleared
}

[DisallowMultipleComponent]
public sealed class PersistentSceneObject3D : MonoBehaviour
{
    [Header("Persistent Identity")]
    [SerializeField] private string persistentId;
    [SerializeField] private PersistentSceneObjectType persistenceType;

    [Header("Saved States")]
    [SerializeField] private bool saveDestroyedState = true;
    [SerializeField] private bool saveActiveState = true;
    [SerializeField] private bool saveCollectedState = true;
    [SerializeField] private PersistentResetPolicy resetPolicy = PersistentResetPolicy.KeepSavedState;

    [Header("Restore Events")]
    [SerializeField] private UnityEvent onRestoreActivated;
    [SerializeField] private UnityEvent onRestoreDeactivated;

    [Header("Debug")]
    [SerializeField] private bool debugMode;
    [SerializeField] private PersistentSceneObjectState currentState = PersistentSceneObjectState.Exists;

    public string PersistentId => persistentId;
    public PersistentSceneObjectType PersistenceType => persistenceType;
    public PersistentSceneObjectState CurrentState => currentState;

    private void Awake()
    {
        RestoreSavedStateBeforeFirstFrame();
    }

    public void MarkDestroyed()
    {
        if (saveDestroyedState) RecordState(PersistentSceneObjectState.Destroyed);
    }

    public void MarkCollected()
    {
        if (saveCollectedState) RecordState(PersistentSceneObjectState.Collected);
    }

    public void MarkActivated()
    {
        if (saveActiveState) RecordState(PersistentSceneObjectState.Activated);
    }

    public void MarkDeactivated()
    {
        if (saveActiveState) RecordState(PersistentSceneObjectState.Deactivated);
    }

    private void RecordState(PersistentSceneObjectState state)
    {
        if (string.IsNullOrWhiteSpace(persistentId))
        {
            Debug.LogWarning("[PersistentSceneObject3D] persistentId is empty. State was not saved.", this);
            return;
        }

        currentState = state;
        GameProgressSave3D.RecordPersistentObjectState(gameObject.scene.name, persistentId, state);
        Log($"Saved {state}: {persistentId}");
    }

    private void RestoreSavedStateBeforeFirstFrame()
    {
        if (string.IsNullOrWhiteSpace(persistentId)
            || !GameProgressSave3D.TryGetPersistentObjectState(gameObject.scene.name, persistentId, out PersistentSceneObjectState savedState))
        {
            currentState = PersistentSceneObjectState.Exists;
            return;
        }

        currentState = savedState;
        switch (savedState)
        {
            case PersistentSceneObjectState.Destroyed when saveDestroyedState:
            case PersistentSceneObjectState.Collected when saveCollectedState:
                gameObject.SetActive(false);
                break;
            case PersistentSceneObjectState.Activated when saveActiveState:
                onRestoreActivated?.Invoke();
                break;
            case PersistentSceneObjectState.Deactivated when saveActiveState:
                onRestoreDeactivated?.Invoke();
                break;
        }

        Log($"Restored {savedState}: {persistentId}");
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(persistentId))
        {
            Debug.LogWarning("[PersistentSceneObject3D] Assign a stable scene-instance persistentId.", this);
            return;
        }

        PersistentSceneObject3D[] objects = FindObjectsByType<PersistentSceneObject3D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int duplicates = 0;
        foreach (PersistentSceneObject3D candidate in objects)
        {
            if (candidate != null && candidate.gameObject.scene == gameObject.scene
                && string.Equals(candidate.persistentId, persistentId, System.StringComparison.Ordinal))
            {
                duplicates++;
            }
        }

        if (duplicates > 1)
        {
            Debug.LogWarning($"[PersistentSceneObject3D] Duplicate ID '{persistentId}' in scene '{gameObject.scene.name}'.", this);
        }
    }

    private void Log(string message)
    {
        if (debugMode) Debug.Log($"[PersistentSceneObject3D] {message}", this);
    }
}
