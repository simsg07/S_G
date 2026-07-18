using UnityEngine;

[DisallowMultipleComponent]
public class OpenPathOnBreak : MonoBehaviour, ITriggerableObject
{
    [Header("Disable On Open")]
    [Tooltip("Objects disabled when the path opens.")]
    [SerializeField] private GameObject[] objectsToDisable;
    [Tooltip("Colliders disabled when the path opens.")]
    [SerializeField] private Collider[] collidersToDisable;

    [Header("Enable On Open")]
    [Tooltip("Objects enabled when the path opens.")]
    [SerializeField] private GameObject[] objectsToEnable;

    [Header("Debug")]
    [Tooltip("Print path open logs in the Console.")]
    [SerializeField] private bool debugMode = true;

    private bool isOpen;

    public bool IsOpen => isOpen;
    public bool CanTrigger => !isOpen;

    public void TriggerObject()
    {
        OpenPath();
    }

    public void ResetObject()
    {
        isOpen = false;
    }

    public void OpenPath()
    {
        if (isOpen)
        {
            return;
        }

        isOpen = true;

        SetObjectsActive(objectsToDisable, false);
        SetCollidersEnabled(collidersToDisable, false);
        SetObjectsActive(objectsToEnable, true);

        if (debugMode)
        {
            Debug.Log("[OpenPathOnBreak] Path opened.", this);
        }
    }

    private static void SetObjectsActive(GameObject[] targets, bool value)
    {
        if (targets == null)
        {
            return;
        }

        foreach (GameObject target in targets)
        {
            if (target != null)
            {
                target.SetActive(value);
            }
        }
    }

    private static void SetCollidersEnabled(Collider[] targets, bool value)
    {
        if (targets == null)
        {
            return;
        }

        foreach (Collider target in targets)
        {
            if (target != null)
            {
                target.enabled = value;
            }
        }
    }
}
