using UnityEngine;

internal sealed class CameraLightAbilityController3D
{
    private readonly Transform owner;
    private readonly float intensity;
    private readonly float range;
    private readonly Color color;

    private Camera targetCamera;
    private CameraLightFollower follower;
    private Light flashLight;
    private bool ownsGeneratedFlashLight;
    private bool isOn;
    private bool disposed;

    public bool IsOn => isOn;
    public CameraLightFollower Follower => follower;

    public CameraLightAbilityController3D(Transform owner, float intensity, float range, Color color)
    {
        this.owner = owner;
        this.intensity = intensity;
        this.range = range;
        this.color = color;
    }

    public void Initialize(Camera camera, CameraLightFollower configuredFollower)
    {
        if (disposed)
        {
            return;
        }

        targetCamera = camera;
        follower = configuredFollower;
        SetupFlashLight();
        ResolveCameraLightFollower();
    }

    public void Toggle()
    {
        if (disposed)
        {
            return;
        }

        if (flashLight == null)
        {
            SetupFlashLight();
        }

        CameraLightFollower resolvedFollower = ResolveCameraLightFollower();
        if (resolvedFollower == null)
        {
            return;
        }

        isOn = resolvedFollower.ToggleLight();
        if (flashLight != null)
        {
            flashLight.intensity = isOn ? intensity : 0f;
            flashLight.range = range;
            flashLight.color = color;
        }
    }

    public void Tick(Vector3 origin)
    {
        if (disposed || flashLight == null)
        {
            return;
        }

        if (isOn)
        {
            SetCameraLight(true, origin);
            return;
        }

        if (flashLight.enabled)
        {
            SetCameraLight(false, owner.position);
        }
    }

    public void TurnOff()
    {
        isOn = false;
        if (flashLight == null)
        {
            return;
        }

        flashLight.intensity = 0f;
        CameraLightFollower resolvedFollower = ResolveCameraLightFollower();
        if (resolvedFollower != null)
        {
            resolvedFollower.SetLightActive(false);
        }
        else
        {
            flashLight.enabled = false;
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        TurnOff();
        disposed = true;
        if (ownsGeneratedFlashLight && flashLight != null)
        {
            DestroyGenerated(flashLight.gameObject);
        }

        flashLight = null;
        follower = null;
        targetCamera = null;
        ownsGeneratedFlashLight = false;
    }

    private CameraLightFollower ResolveCameraLightFollower()
    {
        if (follower != null)
        {
            return follower;
        }

        if (targetCamera == null)
        {
            return null;
        }

        follower = targetCamera.GetComponent<CameraLightFollower>();
        if (follower == null && Application.isPlaying)
        {
            follower = targetCamera.gameObject.AddComponent<CameraLightFollower>();
        }

        if (follower != null)
        {
            follower.Bind(targetCamera, flashLight);
            follower.SetPlayerTransform(owner);
        }

        return follower;
    }

    private Vector3 ResolveCameraLightPosition(Vector3 origin, bool instant)
    {
        Vector3 fallback = origin + new Vector3(0f, 0f, -0.55f);
        CameraLightFollower resolvedFollower = ResolveCameraLightFollower();
        return resolvedFollower != null ? resolvedFollower.MoveBoundLight(fallback, instant) : fallback;
    }

    private void SetupFlashLight()
    {
        if (follower != null && follower.LightObject != null)
        {
            flashLight = follower.LightObject;
            flashLight.type = LightType.Point;
            flashLight.color = color;
            flashLight.range = range;
            flashLight.intensity = 0f;
            follower.Bind(targetCamera, flashLight);
            follower.SetPlayerTransform(owner);
            follower.SetLightActive(false);
            ownsGeneratedFlashLight = false;
            return;
        }

        Transform existingLightTransform = targetCamera != null ? targetCamera.transform.Find("Camera Toggle Light") : null;
        GameObject lightObject = existingLightTransform != null
            ? existingLightTransform.gameObject
            : new GameObject("Camera Toggle Light", typeof(Light));
        ownsGeneratedFlashLight = existingLightTransform == null;
        if (targetCamera != null && lightObject.transform.parent != targetCamera.transform)
        {
            lightObject.transform.SetParent(targetCamera.transform, true);
        }
        CameraTagUtility3D.TrySetTag(lightObject, CameraTagUtility3D.LightTag);

        flashLight = lightObject.GetComponent<Light>();
        if (flashLight == null)
        {
            flashLight = lightObject.AddComponent<Light>();
        }
        flashLight.type = LightType.Point;
        flashLight.color = color;
        flashLight.range = range;
        flashLight.intensity = 0f;
        flashLight.enabled = false;
        if (follower != null)
        {
            follower.Bind(targetCamera, flashLight);
            follower.SetPlayerTransform(owner);
        }
    }

    private void SetCameraLight(bool active, Vector3 origin)
    {
        if (flashLight == null)
        {
            SetupFlashLight();
        }

        if (!active)
        {
            flashLight.intensity = 0f;
            CameraLightFollower resolvedFollower = ResolveCameraLightFollower();
            if (resolvedFollower != null)
            {
                resolvedFollower.SetLightActive(false);
            }
            else
            {
                flashLight.enabled = false;
            }
            return;
        }

        flashLight.range = range;
        flashLight.color = color;
        flashLight.intensity = intensity;
        CameraLightFollower activeFollower = ResolveCameraLightFollower();
        if (activeFollower != null)
        {
            activeFollower.SetLightActive(true, false);
        }
        else
        {
            flashLight.transform.position = ResolveCameraLightPosition(origin, false);
            flashLight.enabled = true;
        }
    }

    private static void DestroyGenerated(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Object.Destroy(target);
        }
        else
        {
            Object.DestroyImmediate(target);
        }
    }
}
