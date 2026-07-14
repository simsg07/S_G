using UnityEngine;

[DisallowMultipleComponent]
public class HumanBoxHowling : MonoBehaviour
{
    [Header("Howling Settings")]
    public bool enableHowling = true;
    public float howlDuration = 1f;
    public float howlStunDuration = 1.5f;
    public bool howlOnlyOncePerDetection = true;

    [Header("Debug")]
    public bool debugMode;

    public bool TryStun(Transform playerTarget)
    {
        if (!enableHowling)
        {
            Log("Howling disabled.");
            return false;
        }

        if (playerTarget == null)
        {
            Log("Player target missing.");
            return false;
        }

        IStunnable stunnable = playerTarget.GetComponent<IStunnable>()
            ?? playerTarget.GetComponentInParent<IStunnable>()
            ?? playerTarget.GetComponentInChildren<IStunnable>();

        if (stunnable == null)
        {
            Debug.LogWarning("[HumanBoxHowling] No IStunnable found on Player.", this);
            return false;
        }

        stunnable.Stun(howlStunDuration);
        Log($"Player stunned for {howlStunDuration:0.##} seconds.");
        return true;
    }

    private void OnValidate()
    {
        howlDuration = Mathf.Max(0f, howlDuration);
        howlStunDuration = Mathf.Max(0f, howlStunDuration);
    }

    private void Log(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[HumanBoxHowling] {message}", this);
        }
    }
}
