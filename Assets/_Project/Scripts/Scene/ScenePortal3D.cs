using System;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class ScenePortal3D : MonoBehaviour
{
    [Header("Portal ID")]
    [Tooltip("Unique ID in this scene. Example: Portal_Right_To_Stage02")]
    [SerializeField] private string portalId = "Portal_Right_To_Stage02";
    [Tooltip("Exact scene asset name. Example: Stage_02_LabEntrance")]
    [SerializeField] private string targetSceneName;
    [Tooltip("Exact SceneSpawnPoint3D.spawnId in the destination scene. Example: Spawn_From_Left")]
    [SerializeField] private string targetSpawnId = "Spawn_Default";

    [Header("Activation")]
    [SerializeField] private bool requireInteract;
    [SerializeField] private KeyCode interactKey = KeyCode.F;
    [SerializeField] private bool playerInRange;
    [SerializeField] private bool isLocked;
    [Tooltip("Designer-facing lock identifier. Lock ownership remains in the existing game systems.")]
    [SerializeField] private string requiredKeyId;
    [Tooltip("Optional Player layer filter. Zero uses the Player tag fallback.")]
    [SerializeField] private LayerMask playerLayerMask;

    [Header("References")]
    [SerializeField] private Collider triggerCollider;

    [Header("Scene View")]
    [SerializeField] private bool showGizmo = true;
    [SerializeField] private bool debugMode = true;

    public string PortalId => portalId;
    public string TargetSceneName => targetSceneName;
    public string TargetSpawnId => targetSpawnId;
    public Collider TriggerCollider => triggerCollider;
    public bool PlayerInRange => playerInRange;

    private void Reset()
    {
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null) triggerCollider.isTrigger = true;
    }

    private void Awake()
    {
        if (triggerCollider == null) triggerCollider = GetComponent<Collider>();
    }

    private void Update()
    {
        if (requireInteract && playerInRange && WasInteractPressed()) TryEnterPortal();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other)) return;
        playerInRange = true;
        if (debugMode) Debug.Log("[ScenePortal3D] Portal entered.", this);
        if (!requireInteract) TryEnterPortal();
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other)) playerInRange = false;
    }

    [ContextMenu("Test Enter Portal")]
    public void TryEnterPortal()
    {
        if (isLocked)
        {
            Debug.LogWarning($"[ScenePortal3D] Portal is locked: {portalId} / requiredKeyId={requiredKeyId}", this);
            return;
        }

        if (!ValidatePortalSetup()) return;
        if (debugMode) Debug.Log($"[ScenePortal3D] Move requested: {targetSceneName} / {targetSpawnId}", this);
        SceneTransitionManager.RequestSceneMove(targetSceneName, targetSpawnId);
    }

    [ContextMenu("Validate Portal Setup")]
    public bool ValidatePortalSetup()
    {
        bool valid = true;
        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogWarning("[ScenePortal3D] targetSceneName is missing.", this);
            valid = false;
        }
        if (string.IsNullOrWhiteSpace(targetSpawnId))
        {
            Debug.LogWarning("[ScenePortal3D] targetSpawnId is missing.", this);
            valid = false;
        }
        if (triggerCollider == null) triggerCollider = GetComponent<Collider>();
        if (triggerCollider == null || !triggerCollider.isTrigger)
        {
            Debug.LogWarning("[ScenePortal3D] triggerCollider must exist and use isTrigger=true.", this);
            valid = false;
        }
        if (valid) Debug.Log($"[ScenePortal3D] Portal OK: {portalId}", this);
        return valid;
    }

    [ContextMenu("Print Portal Info")]
    private void PrintPortalInfo() => Debug.Log($"[ScenePortal3D] {portalId}: {targetSceneName} -> {targetSpawnId}", this);

    public void SetTarget(string sceneName, string spawnId)
    {
        targetSceneName = sceneName;
        targetSpawnId = spawnId;
    }

    private bool IsPlayer(Collider other)
    {
        if (other == null) return false;
        if (playerLayerMask.value != 0 && (playerLayerMask.value & (1 << other.gameObject.layer)) != 0) return true;
        Transform root = other.transform.root;
        return other.CompareTag("Player") || (root != null && root.CompareTag("Player"));
    }

    private bool WasInteractPressed()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && Enum.TryParse(interactKey.ToString(), true, out Key key) && keyboard[key].wasPressedThisFrame;
    }

    private void OnDrawGizmos()
    {
        if (!showGizmo) return;
        Gizmos.color = isLocked ? Color.red : new Color(1f, 0.55f, 0f);
        Gizmos.DrawWireCube(transform.position, new Vector3(0.8f, 1.5f, 0.4f));
        Vector3 start = transform.position + Vector3.up * 0.2f;
        Vector3 end = start + transform.right;
        Gizmos.DrawLine(start, end);
        Gizmos.DrawLine(end, end - transform.right * 0.25f + transform.up * 0.2f);
        Gizmos.DrawLine(end, end - transform.right * 0.25f - transform.up * 0.2f);
#if UNITY_EDITOR
        Handles.color = Gizmos.color;
        Handles.Label(transform.position + Vector3.up, $"{portalId}\n→ {targetSceneName} / {targetSpawnId}");
#endif
    }
}
