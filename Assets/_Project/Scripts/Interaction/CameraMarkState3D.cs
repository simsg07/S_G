using UnityEngine;

[DisallowMultipleComponent]
public class CameraMarkState3D : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [SerializeField] private Color markedColor = new Color(0.35f, 0.95f, 1f, 0.95f);
    [SerializeField] private Color cooldownColor = new Color(1f, 0.76f, 0.24f, 0.65f);
    [SerializeField] private float markerWidth = 0.95f;
    [SerializeField] private float markerHeight = 0.08f;
    [SerializeField] private float markerDepth = 0.08f;
    [SerializeField] private float markerYOffset = 0.28f;
    [SerializeField] private float pulseSpeed = 8f;

    private MeshRenderer markerRenderer;
    private MeshFilter markerFilter;
    private Renderer[] cachedRenderers = System.Array.Empty<Renderer>();
    private MaterialPropertyBlock propertyBlock;
    private float markEndTime;
    private float cooldownEndTime;

    public bool IsMarked => Time.time < markEndTime;
    public bool IsCoolingDown => Time.time >= markEndTime && Time.time < cooldownEndTime;

    private void Awake()
    {
        RefreshCache();
        EnsureMarker();
        ApplyVisual();
        enabled = IsMarked || IsCoolingDown;
    }

    private void Update()
    {
        ApplyVisual();
    }

    private void OnDisable()
    {
        if (markerRenderer != null) markerRenderer.enabled = false;
    }

    public void SetMarkWindow(float markEnd, float cooldownEnd)
    {
        markEndTime = markEnd;
        cooldownEndTime = Mathf.Max(cooldownEnd, markEnd);
        EnsureMarker();
        ApplyVisual();
        enabled = IsMarked || IsCoolingDown;
    }

    public void ClearMark()
    {
        markEndTime = 0f;
        cooldownEndTime = 0f;
        ApplyVisual();
        enabled = false;
    }

    private void EnsureMarker()
    {
        if (markerRenderer != null)
        {
            return;
        }

        GameObject marker = new GameObject("Camera Mark Indicator", typeof(MeshFilter), typeof(MeshRenderer));
        marker.transform.SetParent(transform, false);
        markerFilter = marker.GetComponent<MeshFilter>();
        markerRenderer = marker.GetComponent<MeshRenderer>();

        markerFilter.sharedMesh = CameraHighlightSharedResources3D.SolidCubeMesh;
        markerRenderer.sharedMaterial = CameraHighlightSharedResources3D.MarkerMaterial;
        markerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        markerRenderer.receiveShadows = false;
    }

    private void ApplyVisual()
    {
        EnsureMarker();

        bool active = IsMarked || IsCoolingDown;
        markerRenderer.enabled = active;
        if (!active)
        {
            enabled = false;
            return;
        }

        Bounds bounds = CalculateBounds();
        if (!SafeMath3D.IsFinite(bounds.center) || !SafeMath3D.IsFinite(bounds.extents))
        {
            markerRenderer.enabled = false;
            return;
        }

        Transform markerTransform = markerRenderer.transform;
        Vector3 markerPosition = new Vector3(bounds.center.x, bounds.max.y + markerYOffset, bounds.center.z);
        if (!SafeMath3D.IsFinite(markerPosition))
        {
            markerRenderer.enabled = false;
            return;
        }
        markerTransform.position = markerPosition;

        float pulse = IsMarked ? 1f + Mathf.Sin(Time.time * pulseSpeed) * 0.08f : 1f;
        markerTransform.localScale = new Vector3(markerWidth * pulse, markerHeight, markerDepth);

        SetMarkerColor(IsMarked ? markedColor : cooldownColor);
    }

    private void SetMarkerColor(Color color)
    {
        if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
        propertyBlock.Clear();
        propertyBlock.SetColor(BaseColorId, color);
        propertyBlock.SetColor(ColorId, color);
        markerRenderer.SetPropertyBlock(propertyBlock);
    }

    private Bounds CalculateBounds()
    {
        bool hasBounds = false;
        Bounds bounds = new Bounds(transform.position, Vector3.one);

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer renderer = cachedRenderers[i];
            if (renderer == null || renderer == markerRenderer || !SafeMath3D.IsValidTransform(renderer.transform))
            {
                continue;
            }

            if (!hasBounds)
            {
                Bounds rendererBounds = renderer.bounds;
                if (!SafeMath3D.IsFinite(rendererBounds.center) || !SafeMath3D.IsFinite(rendererBounds.extents))
                {
                    continue;
                }
                bounds = rendererBounds;
                hasBounds = true;
            }
            else
            {
                Bounds rendererBounds = renderer.bounds;
                if (SafeMath3D.IsFinite(rendererBounds.center) && SafeMath3D.IsFinite(rendererBounds.extents))
                {
                    bounds.Encapsulate(rendererBounds);
                }
            }
        }

        return bounds;
    }

    public void RefreshCache()
    {
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
    }

    private void OnTransformChildrenChanged()
    {
        RefreshCache();
    }

}
