using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MonsterPatrolPath : MonoBehaviour
{
    [SerializeField] private bool showPatrolGizmos = true;
    [SerializeField] private Color pathColor = new Color(1f, 0.72f, 0.12f, 1f);
    [SerializeField, Min(0.02f)] private float pointRadius = 0.16f;

    public int PointCount => transform.childCount;
    public bool ShowPatrolGizmos => showPatrolGizmos;

    public Transform GetPoint(int index)
    {
        return index >= 0 && index < transform.childCount ? transform.GetChild(index) : null;
    }

    public int GetNearestPointIndex(Vector3 position)
    {
        int nearest = -1;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < PointCount; i++)
        {
            Transform point = GetPoint(i);
            if (point == null) continue;
            Vector3 delta = point.position - position;
            delta.z = 0f;
            if (delta.sqrMagnitude < nearestDistance)
            {
                nearestDistance = delta.sqrMagnitude;
                nearest = i;
            }
        }

        return nearest;
    }

    public IReadOnlyList<Transform> GetPoints()
    {
        List<Transform> points = new List<Transform>(PointCount);
        for (int i = 0; i < PointCount; i++) points.Add(GetPoint(i));
        return points;
    }

    private void OnDrawGizmos()
    {
        if (!showPatrolGizmos || PointCount == 0) return;
        Gizmos.color = pathColor;
        for (int i = 0; i < PointCount; i++)
        {
            Transform point = GetPoint(i);
            if (point == null) continue;
            Gizmos.DrawWireSphere(point.position, pointRadius);
            if (i + 1 < PointCount && GetPoint(i + 1) != null)
                Gizmos.DrawLine(point.position, GetPoint(i + 1).position);
        }
    }
}
