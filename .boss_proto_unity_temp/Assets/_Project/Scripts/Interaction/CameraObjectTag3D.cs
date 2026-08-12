using UnityEngine;

public enum CameraFreezePolicy3D
{
    Auto,
    Freezable,
    NotFreezable
}

[ExecuteAlways]
[DisallowMultipleComponent]
public class CameraObjectTag3D : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [SerializeField] private CameraFreezePolicy3D freezePolicy = CameraFreezePolicy3D.Auto; // 사진 촬영으로 이 오브젝트를 멈출 수 있는지 정합니다. Auto는 Rigidbody나 셔터 프리즈 컴포넌트를 보고 판단합니다.
    [SerializeField] private bool canCameraInteract = true; // 카메라 능력이 이 오브젝트를 대상으로 삼을 수 있는지 정합니다. 끄면 촬영/간섭 대상에서 제외됩니다.
    [SerializeField] private bool showStatusOutline = true; // 멈출 수 있는지 상태를 테두리로 표시할지 정합니다.
    [SerializeField] private bool showBlockedOutline = true; // 멈출 수 없는 오브젝트도 회색 테두리로 표시할지 정합니다.
    [SerializeField] private Color freezableOutlineColor = new Color(1f, 1f, 1f, 0.95f); // 멈출 수 있는 오브젝트의 테두리 색입니다.
    [SerializeField] private Color blockedOutlineColor = new Color(0.45f, 0.45f, 0.45f, 0.55f); // 멈출 수 없거나 간섭 불가인 오브젝트의 테두리 색입니다.
    [SerializeField] private float outlinePadding = 0.08f; // 테두리가 실제 오브젝트보다 얼마나 바깥으로 떨어질지 정합니다.
    [SerializeField] private float outlinePulseSpeed = 0f; // 테두리 펄스 속도입니다. 0이면 펄스를 쓰지 않습니다.

    private MeshRenderer outlineRenderer;
    private MeshFilter outlineFilter;
    private Renderer[] cachedRenderers = System.Array.Empty<Renderer>();
    private Collider[] cachedColliders = System.Array.Empty<Collider>();
    private Rigidbody cachedRigidbody;
    private IMarkable3D cachedMarkable;
    private MaterialPropertyBlock propertyBlock;
    private bool highlightActive;

    public bool CanCameraInteract => canCameraInteract && !HasTagInParents(this, CameraTagUtility3D.CameraNoInteractTag);
    public bool CanBeFrozen => CanCameraInteract && ResolveCanBeFrozen();

    private void Awake()
    {
        RefreshCache();
        EnsureOutline();
        ApplyVisual();
        enabled = false;
    }

    private void Update()
    {
        if (!highlightActive)
        {
            enabled = false;
            return;
        }
        ApplyVisual();
    }

    private void OnValidate()
    {
        RefreshCache();
        EnsureOutline();
        ApplyVisual();
        enabled = false;
    }

    private void OnTransformChildrenChanged()
    {
        RefreshCache();
        ApplyVisual();
    }

    public void MarkAsAutoCameraTarget()
    {
        showStatusOutline = true;
    }

    public void SetHighlightActive(bool active)
    {
        highlightActive = active;
        ApplyVisual();
        enabled = active && outlinePulseSpeed > 0f && outlineRenderer != null && outlineRenderer.enabled;
        if (!active && outlineRenderer != null)
        {
            outlineRenderer.SetPropertyBlock(null);
        }
    }

    public static CameraObjectTag3D FindFor(Component component)
    {
        return component != null ? component.GetComponentInParent<CameraObjectTag3D>() : null;
    }

    public static bool AllowsCameraInteraction(Component component)
    {
        if (component == null)
        {
            return false;
        }

        CameraObjectTag3D objectTag = FindFor(component);
        if (objectTag != null)
        {
            return objectTag.CanCameraInteract;
        }

        return !HasTagInParents(component, CameraTagUtility3D.CameraNoInteractTag);
    }

    public static bool AllowsCameraFreeze(Component component)
    {
        if (component == null || !AllowsCameraInteraction(component))
        {
            return false;
        }

        WorldSwitchable switchable = WorldSwitchable.FindFor(component);
        if (switchable != null && !switchable.CanApplyShutter)
        {
            return false;
        }

        CameraObjectTag3D objectTag = FindFor(component);
        if (objectTag != null)
        {
            return objectTag.CanBeFrozen;
        }

        if (HasTagInParents(component, CameraTagUtility3D.CameraNoFreezeTag))
        {
            return false;
        }

        return true;
    }

    private bool ResolveCanBeFrozen()
    {
        if (HasTagInParents(this, CameraTagUtility3D.CameraNoFreezeTag))
        {
            return false;
        }

        if (HasTagInParents(this, CameraTagUtility3D.CameraFreezableTag))
        {
            return true;
        }

        switch (freezePolicy)
        {
            case CameraFreezePolicy3D.Freezable:
                return true;
            case CameraFreezePolicy3D.NotFreezable:
                return false;
            default:
                return HasFreezableCapability();
        }
    }

    private bool HasFreezableCapability()
    {
        return cachedRigidbody != null || cachedMarkable != null;
    }

    private void EnsureOutline()
    {
        if (outlineRenderer != null)
        {
            return;
        }

        Transform existing = transform.Find("Camera Freeze Status Outline");
        GameObject outlineObject = existing != null
            ? existing.gameObject
            : new GameObject("Camera Freeze Status Outline", typeof(MeshFilter), typeof(MeshRenderer));
        outlineObject.transform.SetParent(transform, false);

        outlineFilter = outlineObject.GetComponent<MeshFilter>();
        outlineRenderer = outlineObject.GetComponent<MeshRenderer>();

        outlineFilter.sharedMesh = CameraHighlightSharedResources3D.LineCubeMesh;
        outlineRenderer.sharedMaterial = CameraHighlightSharedResources3D.OutlineMaterial;
        outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        outlineRenderer.receiveShadows = false;
    }

    private void ApplyVisual()
    {
        EnsureOutline();

        bool canFreeze = CanBeFrozen;
        bool visible = highlightActive && showStatusOutline && (canFreeze || showBlockedOutline);
        outlineRenderer.enabled = visible;
        if (!visible)
        {
            return;
        }

        Bounds bounds = CalculateBounds();
        float pulse = outlinePulseSpeed > 0f ? 1f + Mathf.Sin(Time.unscaledTime * outlinePulseSpeed) * 0.03f : 1f;
        Vector3 paddedSize = bounds.size + Vector3.one * Mathf.Max(0f, outlinePadding);

        Transform outlineTransform = outlineRenderer.transform;
        outlineTransform.position = bounds.center;
        outlineTransform.rotation = Quaternion.identity;
        outlineTransform.localScale = new Vector3(
            Mathf.Max(0.05f, paddedSize.x * pulse),
            Mathf.Max(0.05f, paddedSize.y * pulse),
            Mathf.Max(0.05f, paddedSize.z * pulse)
        );

        SetOutlineColor(canFreeze ? freezableOutlineColor : blockedOutlineColor);
    }

    private Bounds CalculateBounds()
    {
        bool hasBounds = false;
        Bounds bounds = new Bounds(transform.position, Vector3.one);

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer renderer = cachedRenderers[i];
            if (renderer == null || renderer == outlineRenderer || renderer.transform.IsChildOf(outlineRenderer.transform))
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        for (int i = 0; i < cachedColliders.Length; i++)
        {
            Collider collider = cachedColliders[i];
            if (collider == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return bounds;
    }

    private void SetOutlineColor(Color color)
    {
        if (outlineRenderer == null) return;
        if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
        propertyBlock.Clear();
        propertyBlock.SetColor(BaseColorId, color);
        propertyBlock.SetColor(ColorId, color);
        outlineRenderer.SetPropertyBlock(propertyBlock);
    }

    public void RefreshCache()
    {
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        cachedColliders = GetComponentsInChildren<Collider>(true);
        cachedRigidbody = GetComponentInParent<Rigidbody>();
        cachedMarkable = null;
        MonoBehaviour[] behaviours = GetComponentsInParent<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IMarkable3D markable) { cachedMarkable = markable; break; }
        }
    }

    private static bool HasTagInParents(Component component, string tagName)
    {
        Transform current = component != null ? component.transform : null;
        while (current != null)
        {
            try
            {
                if (current.CompareTag(tagName)) return true;
            }
            catch (UnityException)
            {
                return false;
            }
            current = current.parent;
        }
        return false;
    }

}
