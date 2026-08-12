using UnityEngine;

public enum MonsterPatrolMode { PingPong, Loop, Once }
public enum MonsterPatrolResumeMode { NearestPoint, LastPoint, StartPoint }

[DisallowMultipleComponent]
public sealed class MonsterPatrolController : MonoBehaviour
{
    [Header("순찰")]
    [SerializeField] private bool enablePatrol;
    [SerializeField] private MonsterPatrolPath patrolPath;
    [SerializeField] private MonsterPatrolMode patrolMode = MonsterPatrolMode.PingPong;
    [SerializeField, Min(0f)] private float patrolSpeed = 1f;
    [SerializeField, Min(0.001f)] private float arrivalDistance = 0.1f;
    [SerializeField, Min(0f)] private float waitTimeAtPoint;
    [SerializeField, Min(0)] private int startPointIndex;
    [SerializeField] private bool startFromNearestPoint;
    [SerializeField] private bool resumePatrolAfterLosingPlayer = true;
    [SerializeField] private MonsterPatrolResumeMode patrolResumeMode = MonsterPatrolResumeMode.NearestPoint;
    [SerializeField] private bool faceMovementDirection = true;
    [SerializeField] private bool showPatrolGizmos = true;

    private int currentPointIndex;
    private int direction = 1;
    private float waitUntil;
    private bool initialized;
    private bool finished;

    public bool EnablePatrol => enablePatrol;
    public MonsterPatrolPath PatrolPath => patrolPath;
    public float PatrolSpeed => patrolSpeed;
    public bool FaceMovementDirection => faceMovementDirection;
    public bool IsWaiting => Time.time < waitUntil;
    public bool CanMove => enablePatrol && !finished && !IsWaiting && TryGetCurrentPoint(out _);

    public bool TryGetCurrentPoint(out Vector3 position)
    {
        EnsureInitialized();
        Transform point = patrolPath != null ? patrolPath.GetPoint(currentPointIndex) : null;
        position = point != null ? point.position : transform.position;
        return point != null;
    }

    public void NotifyPosition(Vector3 monsterPosition)
    {
        if (!enablePatrol || finished || IsWaiting || !TryGetCurrentPoint(out Vector3 point)) return;
        Vector3 delta = point - monsterPosition;
        delta.z = 0f;
        if (delta.sqrMagnitude > arrivalDistance * arrivalDistance) return;
        waitUntil = Time.time + waitTimeAtPoint;
        AdvancePoint();
    }

    public void ResumeAfterCombat(Vector3 monsterPosition)
    {
        if (!enablePatrol || !resumePatrolAfterLosingPlayer || patrolPath == null) return;
        finished = false;
        switch (patrolResumeMode)
        {
            case MonsterPatrolResumeMode.NearestPoint:
                currentPointIndex = Mathf.Max(0, patrolPath.GetNearestPointIndex(monsterPosition));
                break;
            case MonsterPatrolResumeMode.StartPoint:
                currentPointIndex = Mathf.Clamp(startPointIndex, 0, Mathf.Max(0, patrolPath.PointCount - 1));
                break;
        }
        waitUntil = 0f;
        initialized = true;
    }

    public void PauseForCombat() => waitUntil = 0f;

    private void EnsureInitialized()
    {
        if (initialized || patrolPath == null || patrolPath.PointCount == 0) return;
        currentPointIndex = startFromNearestPoint
            ? Mathf.Max(0, patrolPath.GetNearestPointIndex(transform.position))
            : Mathf.Clamp(startPointIndex, 0, patrolPath.PointCount - 1);
        initialized = true;
    }

    private void AdvancePoint()
    {
        int count = patrolPath != null ? patrolPath.PointCount : 0;
        if (count <= 1) { finished = patrolMode == MonsterPatrolMode.Once; return; }
        if (patrolMode == MonsterPatrolMode.Loop) currentPointIndex = (currentPointIndex + 1) % count;
        else if (patrolMode == MonsterPatrolMode.Once)
        {
            if (currentPointIndex >= count - 1) finished = true;
            else currentPointIndex++;
        }
        else
        {
            if (currentPointIndex >= count - 1) direction = -1;
            else if (currentPointIndex <= 0) direction = 1;
            currentPointIndex = Mathf.Clamp(currentPointIndex + direction, 0, count - 1);
        }
    }

    private void OnValidate()
    {
        patrolSpeed = Mathf.Max(0f, patrolSpeed);
        arrivalDistance = Mathf.Max(0.001f, arrivalDistance);
        waitTimeAtPoint = Mathf.Max(0f, waitTimeAtPoint);
        startPointIndex = Mathf.Max(0, startPointIndex);
        // Transform/points are deliberately never created or repositioned here.
    }

    private void OnDrawGizmosSelected()
    {
        if (!showPatrolGizmos || patrolPath == null || !TryGetCurrentPoint(out Vector3 point)) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, point);
    }
}
