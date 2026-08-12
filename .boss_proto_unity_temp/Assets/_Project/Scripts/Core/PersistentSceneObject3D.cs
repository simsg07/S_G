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

[DefaultExecutionOrder(-200)]
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
    [Tooltip("Disable the root when a destroyed/collected state is restored. Turn this off when the owning object applies its own persistent presentation.")]
    [SerializeField] private bool deactivateRootOnDestroyedRestore = true;
    [Header("Restore Events")]
    [SerializeField] private UnityEvent onRestoreActivated;
    [SerializeField] private UnityEvent onRestoreDeactivated;

    [Header("Debug")]
    [SerializeField] private bool debugMode;
    [SerializeField] private PersistentSceneObjectState currentState = PersistentSceneObjectState.Exists;

    public string PersistentId => persistentId;
    public PersistentSceneObjectType PersistenceType => persistenceType;
    public PersistentSceneObjectState CurrentState => currentState;

    public bool TryGetSavedState(out PersistentSceneObjectState savedState)
    {
        savedState = PersistentSceneObjectState.Exists;
        return !string.IsNullOrWhiteSpace(persistentId)
            && GameProgressSave3D.TryGetPersistentObjectState(gameObject.scene.name, persistentId, out savedState);
    }

#if UNITY_EDITOR
    public bool EnsureEditorPersistentId(string prefix)
    {
        if (!string.IsNullOrWhiteSpace(persistentId)
            || UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this)
            || !gameObject.scene.IsValid())
        {
            return false;
        }

        string safePrefix = string.IsNullOrWhiteSpace(prefix) ? "persistent" : prefix.Trim();
        UnityEditor.Undo.RecordObject(this, "Assign Persistent Object ID");
        persistentId = $"{safePrefix}_{System.Guid.NewGuid():N}";
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        return true;
    }
#endif

    private void Awake()
    {
        RestoreSavedStateBeforeFirstFrame();
    }

    public void MarkDestroyed()
    {
        if (saveDestroyedState) RecordState(PersistentSceneObjectState.Destroyed);
    }

    public void MarkDestroyedRuntime()
    {
        if (!saveDestroyedState || string.IsNullOrWhiteSpace(persistentId)) return;
        currentState = PersistentSceneObjectState.Destroyed;
        GameProgressSave3D.RecordRuntimePersistentObjectState(
            gameObject.scene.name, persistentId, PersistentSceneObjectState.Destroyed);
        Log($"Staged runtime Destroyed: {persistentId}");
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
        if (!TryGetSavedState(out PersistentSceneObjectState savedState))
        {
            currentState = PersistentSceneObjectState.Exists;
            return;
        }

        currentState = savedState;
        switch (savedState)
        {
            case PersistentSceneObjectState.Destroyed when saveDestroyedState:
            case PersistentSceneObjectState.Collected when saveCollectedState:
                if (deactivateRootOnDestroyedRestore)
                {
                    gameObject.SetActive(false);
                }
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
#if UNITY_EDITOR
        // A prefab asset is only a reusable template; its stable ID must be assigned
        // on each scene instance. Warn only for scene objects, where an empty or
        // duplicate ID would actually prevent persistence.
        if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this))
        {
            return;
        }
#endif

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
