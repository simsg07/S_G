using UnityEngine;

public interface IGameplayLightSource3D
{
    Transform LightSourceTransform { get; }
    bool IsProvidingLight { get; }
    bool IsIlluminating(Vector3 targetPosition);
}

public static class GameplayLightSource3D
{
    public static bool TryResolve(Component component, out IGameplayLightSource3D source)
    {
        source = null;
        if (component == null) return false;
        ElectricLightObject3D electricLight = component.GetComponentInParent<ElectricLightObject3D>(true);
        source = electricLight;
        return source != null;
    }

    public static bool Reaches(Light light, Vector3 targetPosition)
    {
        if (light == null) return false;
        if (TryResolve(light, out IGameplayLightSource3D source))
            return source.IsProvidingLight && source.IsIlluminating(targetPosition);
        if (!light.isActiveAndEnabled || light.intensity <= 0f) return false;
        if (light.type == LightType.Directional) return true;
        Vector3 delta = targetPosition - light.transform.position;
        if (delta.sqrMagnitude > light.range * light.range) return false;
        return light.type != LightType.Spot || delta.sqrMagnitude <= 0.0001f ||
            Vector3.Dot(light.transform.forward, delta.normalized) >=
            Mathf.Cos(light.spotAngle * 0.5f * Mathf.Deg2Rad);
    }
}
