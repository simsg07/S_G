using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class HumanBoxHowling : MonoBehaviour
{
    [Header("Howling Settings")]
    public bool enableHowling = true;
    public float howlDuration = 1f;
    public float howlStunDuration = 1.5f;
    public bool howlOnlyOncePerDetection = true;

    [Header("Howl Range")]
    public Transform howlOrigin;
    public Vector3 howlBoxOffset = Vector3.zero;
    public Vector3 howlBoxSize = new Vector3(3f, 2f, 1.5f);
    public LayerMask playerLayerMask;
    public bool useBoxRange = true;
    public bool requirePlayerInsideHowlRange = true;

    [Header("Debug")]
    public bool debugMode;
    public bool showGizmo = true;

    public bool TryStunPlayersInRange()
    {
        if (!enableHowling)
        {
            Log("Howling disabled.");
            return false;
        }

        Collider[] candidates = GetPlayersInRange();
        HashSet<IStunnable> stunned = new HashSet<IStunnable>();

        Log($"Found {candidates.Length} collider candidate(s) in howl range.");
        for (int i = 0; i < candidates.Length; i++)
        {
            IStunnable stunnable = FindStunnable(candidates[i].transform);
            if (stunnable == null || !stunned.Add(stunnable))
            {
                continue;
            }

            stunnable.Stun(howlStunDuration);
            Log($"Stunned {GetStunnableName(stunnable)} for {howlStunDuration:0.##} seconds.");
        }

        return stunned.Count > 0;
    }

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

        if (requirePlayerInsideHowlRange && !IsPlayerInHowlRange(playerTarget))
        {
            Log($"{playerTarget.name} is outside the howl range.");
            return false;
        }

        IStunnable stunnable = FindStunnable(playerTarget);

        if (stunnable == null)
        {
            Debug.LogWarning("[HumanBoxHowling] No IStunnable found on Player.", this);
            return false;
        }

        stunnable.Stun(howlStunDuration);
        Log($"Player stunned for {howlStunDuration:0.##} seconds.");
        return true;
    }

    public bool IsPlayerInHowlRange(Transform playerTarget)
    {
        if (playerTarget == null)
        {
            return false;
        }

        IStunnable targetStunnable = FindStunnable(playerTarget);
        Collider[] candidates = GetPlayersInRange();
        for (int i = 0; i < candidates.Length; i++)
        {
            Transform candidate = candidates[i].transform;
            if (candidate == playerTarget || candidate.IsChildOf(playerTarget) || playerTarget.IsChildOf(candidate))
            {
                return true;
            }

            if (targetStunnable != null && ReferenceEquals(FindStunnable(candidate), targetStunnable))
            {
                return true;
            }
        }

        return false;
    }

    private Collider[] GetPlayersInRange()
    {
        Vector3 center = GetHowlCenter();
        if (useBoxRange)
        {
            Quaternion rotation = howlOrigin != null ? howlOrigin.rotation : transform.rotation;
            return Physics.OverlapBox(center, howlBoxSize * 0.5f, rotation, playerLayerMask, QueryTriggerInteraction.Collide);
        }

        float radius = Mathf.Max(howlBoxSize.x, howlBoxSize.y, howlBoxSize.z) * 0.5f;
        return Physics.OverlapSphere(center, radius, playerLayerMask, QueryTriggerInteraction.Collide);
    }

    private static IStunnable FindStunnable(Transform target)
    {
        return target.GetComponent<IStunnable>()
            ?? target.GetComponentInParent<IStunnable>()
            ?? target.GetComponentInChildren<IStunnable>();
    }

    private static string GetStunnableName(IStunnable stunnable)
    {
        Component component = stunnable as Component;
        return component != null ? component.name : stunnable.GetType().Name;
    }

    private Vector3 GetHowlCenter()
    {
        Transform origin = howlOrigin != null ? howlOrigin : transform;
        return origin.TransformPoint(howlBoxOffset);
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmo)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.35f, 0.1f, 0.35f);
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Transform origin = howlOrigin != null ? howlOrigin : transform;
        Gizmos.matrix = Matrix4x4.TRS(GetHowlCenter(), origin.rotation, Vector3.one);
        if (useBoxRange)
        {
            Gizmos.DrawCube(Vector3.zero, howlBoxSize);
            Gizmos.color = new Color(1f, 0.35f, 0.1f, 1f);
            Gizmos.DrawWireCube(Vector3.zero, howlBoxSize);
        }
        else
        {
            float radius = Mathf.Max(howlBoxSize.x, howlBoxSize.y, howlBoxSize.z) * 0.5f;
            Gizmos.DrawSphere(Vector3.zero, radius);
            Gizmos.color = new Color(1f, 0.35f, 0.1f, 1f);
            Gizmos.DrawWireSphere(Vector3.zero, radius);
        }
        Gizmos.matrix = previousMatrix;
    }

    private void OnValidate()
    {
        howlDuration = Mathf.Max(0f, howlDuration);
        howlStunDuration = Mathf.Max(0f, howlStunDuration);
        howlBoxSize = new Vector3(
            Mathf.Max(0f, howlBoxSize.x),
            Mathf.Max(0f, howlBoxSize.y),
            Mathf.Max(0f, howlBoxSize.z));
    }

    private void Log(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[HumanBoxHowling] {message}", this);
        }
    }
}
