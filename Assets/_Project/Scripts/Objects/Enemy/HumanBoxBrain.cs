using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(HumanBoxAI))]
public class HumanBoxBrain : MonoBehaviour
{
    [Header("Human Box Brain")]
    [Tooltip("Monster-specific decision component. The existing HumanBoxAI keeps the actual state machine.")]
    public bool enableBrain = true;

    [Tooltip("HumanBox AI controlled by this brain wrapper.")]
    public HumanBoxAI ai;

    [Header("Debug")]
    public bool debugMode;

    private void Reset()
    {
        AutoFill();
    }

    private void OnValidate()
    {
        AutoFill();
    }

    private void Awake()
    {
        AutoFill();
    }

    private void AutoFill()
    {
        if (ai == null)
        {
            ai = GetComponent<HumanBoxAI>();
        }
    }
}
