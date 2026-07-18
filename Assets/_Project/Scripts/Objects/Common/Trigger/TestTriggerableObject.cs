using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class TestTriggerableObject : MonoBehaviour, ITriggerableObject
{
    [Header("Trigger Settings")]
    [Tooltip("When false, TriggerObject calls are ignored.")]
    [SerializeField] private bool canTrigger = true;
    [Tooltip("Disable this GameObject when triggered.")]
    [SerializeField] private bool deactivateSelfOnTrigger;
    [Tooltip("Objects enabled when this test object is triggered.")]
    [SerializeField] private GameObject[] objectsToEnable;
    [Tooltip("Objects disabled when this test object is triggered.")]
    [SerializeField] private GameObject[] objectsToDisable;

    [Header("Runtime")]
    [Tooltip("Runtime trigger count. Read-only during Play Mode.")]
    [SerializeField] private int triggerCount;

    [Header("Events")]
    public UnityEvent onTriggered;
    public UnityEvent onReset;

    [Header("Debug")]
    [Tooltip("Print trigger logs in the Console.")]
    [SerializeField] private bool debugMode = true;

    public bool CanTrigger => canTrigger;
    public int TriggerCount => triggerCount;

    public void TriggerObject()
    {
        if (!canTrigger)
        {
            Log("Trigger ignored because canTrigger is false.");
            return;
        }

        triggerCount++;
        SetObjectsActive(objectsToEnable, true);
        SetObjectsActive(objectsToDisable, false);
        onTriggered?.Invoke();
        Log($"TriggerObject called. Count={triggerCount}");

        if (deactivateSelfOnTrigger)
        {
            gameObject.SetActive(false);
        }
    }

    public void ResetObject()
    {
        triggerCount = 0;
        onReset?.Invoke();
        Log("ResetObject called.");
    }

    [ContextMenu("Test Trigger")]
    private void ContextTestTrigger()
    {
        TriggerObject();
    }

    [ContextMenu("Reset Test Trigger")]
    private void ContextResetTrigger()
    {
        ResetObject();
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

    private void Log(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[TestTriggerableObject] {message}", this);
        }
    }
}
