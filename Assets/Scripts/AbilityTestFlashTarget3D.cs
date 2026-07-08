using UnityEngine;

public class AbilityTestFlashTarget3D : MonoBehaviour, IFlashReactive3D
{
    [SerializeField] private GameObject[] revealObjects = new GameObject[0];
    [SerializeField] private Color idleColor = new Color(1f, 0.82f, 0.15f, 1f);
    [SerializeField] private Color activeColor = new Color(0.2f, 1f, 0.55f, 1f);

    private Renderer[] renderers;
    private bool activated;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        ApplyState();
    }

    public void Configure(GameObject[] objectsToReveal)
    {
        revealObjects = objectsToReveal ?? new GameObject[0];
        ApplyState();
    }

    public bool OnCameraFlash(CameraAbilitySystem3D source)
    {
        activated = !activated;
        ApplyState();
        return true;
    }

    private void ApplyState()
    {
        if (renderers == null || renderers.Length == 0)
        {
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        Color color = activated ? activeColor : idleColor;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].sharedMaterial != null)
            {
                renderers[i].sharedMaterial.color = color;
            }
        }

        for (int i = 0; i < revealObjects.Length; i++)
        {
            if (revealObjects[i] != null)
            {
                revealObjects[i].SetActive(activated);
            }
        }
    }
}
