using System;
using UnityEngine;
using UnityEngine.Events;

public class ResearchDevice3D : MonoBehaviour, IFlashReactive3D
{
    [SerializeField] private string deviceId;
    [SerializeField] private bool requiresFlash = true;
    [SerializeField] private Color inactiveColor = new Color(0.25f, 0.25f, 0.28f, 1f);
    [SerializeField] private Color activatedColor = new Color(0.2f, 0.9f, 1f, 1f);
    [SerializeField] private UnityEvent onActivated;

    private Renderer[] renderers;
    private bool activated;

    public event Action<bool> ActivationStateChanged;
    public bool IsActivated => activated;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
    }

    private void Start()
    {
        SetActivated(GameProgressSave3D.IsDeviceActivated(SaveKey), true);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!Application.isPlaying || requiresFlash || activated)
        {
            return;
        }

        if (other.GetComponentInParent<PlatformerPlayer3D>() != null)
        {
            Activate();
        }
    }

    public bool OnCameraFlash(CameraAbilitySystem3D source)
    {
        if (!requiresFlash || activated)
        {
            return false;
        }

        Activate();
        return true;
    }

    private void Activate()
    {
        GameProgressSave3D.RecordDeviceActivated(SaveKey);
        SetActivated(true, true);
        onActivated?.Invoke();
    }

    private void SetActivated(bool nextActivated, bool notify)
    {
        bool changed = activated != nextActivated;
        activated = nextActivated;
        ApplyVisualState();

        if (changed || notify)
        {
            ActivationStateChanged?.Invoke(activated);
        }
    }

    private void ApplyVisualState()
    {
        if (renderers == null || renderers.Length == 0)
        {
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        Color color = activated ? activatedColor : inactiveColor;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer targetRenderer = renderers[i];
            if (targetRenderer != null && targetRenderer.sharedMaterial != null)
            {
                targetRenderer.sharedMaterial.color = color;
            }
        }
    }

    private string SaveKey
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(deviceId))
            {
                return deviceId;
            }

            return $"{gameObject.scene.name}/{gameObject.name}";
        }
    }
}
