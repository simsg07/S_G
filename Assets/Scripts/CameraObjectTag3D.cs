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
    private Material outlineMaterial;
    private Mesh outlineMesh;

    public bool CanCameraInteract => canCameraInteract && !HasAnyTagInParents(this, CameraTagUtility3D.CameraNoInteractTag);
    public bool CanBeFrozen => CanCameraInteract && ResolveCanBeFrozen();

    private void Awake()
    {
        EnsureOutline();
        ApplyVisual();
    }

    private void Update()
    {
        ApplyVisual();
    }

    private void OnValidate()
    {
        EnsureOutline();
        ApplyVisual();
    }

    private void OnDestroy()
    {
        DestroyGenerated(outlineMaterial);
        DestroyGenerated(outlineMesh);
    }

    public void MarkAsAutoCameraTarget()
    {
        showStatusOutline = true;
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

        return !HasAnyTagInParents(component, CameraTagUtility3D.CameraNoInteractTag);
    }

    public static bool AllowsCameraFreeze(Component component)
    {
        if (component == null || !AllowsCameraInteraction(component))
        {
            return false;
        }

        CameraObjectTag3D objectTag = FindFor(component);
        if (objectTag != null)
        {
            return objectTag.CanBeFrozen;
        }

        if (HasAnyTagInParents(component, CameraTagUtility3D.CameraNoFreezeTag))
        {
            return false;
        }

        return true;
    }

    private bool ResolveCanBeFrozen()
    {
        if (HasAnyTagInParents(this, CameraTagUtility3D.CameraNoFreezeTag))
        {
            return false;
        }

        if (HasAnyTagInParents(this, CameraTagUtility3D.CameraFreezableTag))
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
        if (GetComponentInParent<Rigidbody>() != null)
        {
            return true;
        }

        MonoBehaviour[] behaviours = GetComponentsInParent<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IShutterFreezable3D)
            {
                return true;
            }
        }

        return false;
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

        outlineMesh = CreateLineCubeMesh();
        outlineMesh.hideFlags = HideFlags.HideAndDontSave;
        outlineFilter.sharedMesh = outlineMesh;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        outlineMaterial = new Material(shader)
        {
            name = "Generated Camera Freeze Outline Material",
            hideFlags = HideFlags.HideAndDontSave
        };
        outlineRenderer.sharedMaterial = outlineMaterial;
        outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        outlineRenderer.receiveShadows = false;
    }

    private void ApplyVisual()
    {
        EnsureOutline();

        bool canFreeze = CanBeFrozen;
        bool visible = showStatusOutline && (canFreeze || showBlockedOutline);
        outlineRenderer.enabled = visible;
        if (!visible)
        {
            return;
        }

        Bounds bounds = CalculateBounds();
        float pulse = outlinePulseSpeed > 0f ? 1f + Mathf.Sin(Time.time * outlinePulseSpeed) * 0.03f : 1f;
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

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
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

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
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
        if (outlineMaterial == null)
        {
            return;
        }

        if (outlineMaterial.HasProperty(BaseColorId))
        {
            outlineMaterial.SetColor(BaseColorId, color);
        }

        if (outlineMaterial.HasProperty(ColorId))
        {
            outlineMaterial.SetColor(ColorId, color);
        }

        outlineMaterial.color = color;
    }

    private static bool HasAnyTagInParents(Component component, params string[] tagNames)
    {
        Transform current = component != null ? component.transform : null;
        while (current != null)
        {
            if (CameraTagUtility3D.HasAnyTag(current.gameObject, tagNames))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static Mesh CreateLineCubeMesh()
    {
        Mesh mesh = new Mesh { name = "Generated Camera Freeze Outline Cube" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f),
            new Vector3(0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f)
        };
        mesh.SetIndices(new[]
        {
            0, 1, 1, 2, 2, 3, 3, 0,
            4, 5, 5, 6, 6, 7, 7, 4,
            0, 4, 1, 5, 2, 6, 3, 7
        }, MeshTopology.Lines, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void DestroyGenerated(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
