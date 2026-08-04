using UnityEngine;

public static class CameraTagUtility3D
{
    public const string TargetTag = "Target";
    public const string RelayTargetTag = "RelayTarget";
    public const string LightTag = "light";
    public const string CameraFreezableTag = "CameraFreezable";
    public const string CameraNoFreezeTag = "CameraNoFreeze";
    public const string CameraInteractTag = "CameraInteract";
    public const string CameraNoInteractTag = "CameraNoInteract";
    public const string SwitchableWorldObjectTag = "SwitchableWorldObject";
    public const string PuzzleObjectTag = "PuzzleObject";
    public const string DoorTag = "Door";
    public const string PlatformTag = "Platform";

    public static bool TrySetTag(GameObject target, string tagName)
    {
        if (target == null || string.IsNullOrWhiteSpace(tagName))
        {
            return false;
        }

        try
        {
            target.tag = tagName;
            return true;
        }
        catch (UnityException)
        {
            return false;
        }
    }

    public static bool HasAnyTag(Component component, params string[] tagNames)
    {
        return component != null && HasAnyTag(component.gameObject, tagNames);
    }

    public static bool HasAnyTag(GameObject target, params string[] tagNames)
    {
        if (target == null || tagNames == null)
        {
            return false;
        }

        for (int i = 0; i < tagNames.Length; i++)
        {
            string tagName = tagNames[i];
            if (string.IsNullOrWhiteSpace(tagName))
            {
                continue;
            }

            try
            {
                if (target.tag == tagName)
                {
                    return true;
                }
            }
            catch (UnityException)
            {
                continue;
            }
        }

        return false;
    }
}
