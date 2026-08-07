using System.Collections.Generic;
using UnityEngine;

internal sealed class ShutterMarkRegistry3D
{
    private readonly Dictionary<Component, ShutterMarkRecord> shutterMarks = new Dictionary<Component, ShutterMarkRecord>();
    private readonly List<Component> expiredMarkTargets = new List<Component>();

    public IEnumerable<Component> MarkedTargets => shutterMarks.Keys;

    public bool Mark(
        Component targetComponent,
        IShutterFreezable3D target,
        float currentTime,
        float markDuration,
        float remarkCooldown,
        out float markEndTime,
        out float cooldownEndTime)
    {
        markEndTime = 0f;
        cooldownEndTime = 0f;
        if (targetComponent == null || target == null)
        {
            return false;
        }

        markEndTime = currentTime + Mathf.Max(0.1f, markDuration);
        cooldownEndTime = markEndTime + Mathf.Max(0f, remarkCooldown);
        shutterMarks[targetComponent] = new ShutterMarkRecord(target, markEndTime, cooldownEndTime);
        return true;
    }

    public bool StartRemarkCooldown(
        Component targetComponent,
        float currentTime,
        float remarkCooldown,
        out float cooldownEndTime)
    {
        cooldownEndTime = 0f;
        if (targetComponent == null)
        {
            return false;
        }

        cooldownEndTime = currentTime + Mathf.Max(0f, remarkCooldown);
        if (shutterMarks.TryGetValue(targetComponent, out ShutterMarkRecord record))
        {
            shutterMarks[targetComponent] = new ShutterMarkRecord(record.Target, 0f, cooldownEndTime);
        }

        return true;
    }

    public bool IsMarked(Component targetComponent, float currentTime)
    {
        return targetComponent != null
            && shutterMarks.TryGetValue(targetComponent, out ShutterMarkRecord record)
            && currentTime < record.MarkEndTime;
    }

    public bool IsInRemarkCooldown(Component targetComponent, float currentTime)
    {
        return targetComponent != null
            && shutterMarks.TryGetValue(targetComponent, out ShutterMarkRecord record)
            && currentTime >= record.MarkEndTime
            && currentTime < record.CooldownEndTime;
    }

    public IReadOnlyList<Component> Tick(float currentTime)
    {
        expiredMarkTargets.Clear();
        foreach (KeyValuePair<Component, ShutterMarkRecord> pair in shutterMarks)
        {
            if (pair.Key == null || !pair.Key.gameObject.activeInHierarchy || currentTime >= pair.Value.CooldownEndTime)
            {
                expiredMarkTargets.Add(pair.Key);
            }
        }

        return expiredMarkTargets;
    }

    public void Remove(Component targetComponent)
    {
        shutterMarks.Remove(targetComponent);
    }

    public void Clear()
    {
        shutterMarks.Clear();
        expiredMarkTargets.Clear();
    }

    private readonly struct ShutterMarkRecord
    {
        public ShutterMarkRecord(IShutterFreezable3D target, float markEndTime, float cooldownEndTime)
        {
            Target = target;
            MarkEndTime = markEndTime;
            CooldownEndTime = cooldownEndTime;
        }

        public IShutterFreezable3D Target { get; }
        public float MarkEndTime { get; }
        public float CooldownEndTime { get; }
    }
}
