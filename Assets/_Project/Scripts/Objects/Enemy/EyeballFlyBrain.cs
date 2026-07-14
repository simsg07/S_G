using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EyeballFlyAI))]
public class EyeballFlyBrain : MonoBehaviour
{
    [Header("EyeballFly Brain")]
    [Tooltip("Monster-specific decision component. The existing EyeballFlyAI keeps the actual state machine.")]
    public bool enableBrain = true;

    [Tooltip("EyeballFly AI controlled by this brain wrapper.")]
    public EyeballFlyAI ai;

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
            ai = GetComponent<EyeballFlyAI>();
        }
    }
}
