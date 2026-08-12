using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class CameraTargetHighlightManager3D : MonoBehaviour
{
    private const int MaxPooledMarkers = 16;
    private static readonly ProfilerMarker HighlightMarker = new ProfilerMarker("Camera.TargetHighlight");
    private static readonly ProfilerMarker MarkMarker = new ProfilerMarker("Camera.MarkUpdate");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private readonly Dictionary<Component, MarkEntry> activeMarks = new Dictionary<Component, MarkEntry>(16);
    private readonly List<Component> expiredTargets = new List<Component>(16);
    private readonly Stack<MeshRenderer> markerPool = new Stack<MeshRenderer>(16);
    private MaterialPropertyBlock propertyBlock;
    private Transform poolRoot;

    public int ActiveMarkCount => activeMarks.Count;
    public int PooledMarkerCount => markerPool.Count;

    public void SetMark(Component target, float markEndTime, float cooldownEndTime)
    {
        if (target == null) return;
        if (!activeMarks.TryGetValue(target, out MarkEntry entry))
        {
            entry = new MarkEntry(target, AcquireMarker(), CacheRenderers(target));
        }

        entry.MarkEndTime = markEndTime;
        entry.CooldownEndTime = Mathf.Max(markEndTime, cooldownEndTime);
        entry.Marker.gameObject.SetActive(true);
        activeMarks[target] = entry;
        enabled = true;
        UpdateEntry(ref entry);
    }

    public void ClearMark(Component target)
    {
        if (ReferenceEquals(target, null) || !activeMarks.TryGetValue(target, out MarkEntry entry)) return;
        ReleaseMarker(entry.Marker);
        activeMarks.Remove(target);
        enabled = activeMarks.Count > 0;
    }

    public void ClearAll()
    {
        foreach (KeyValuePair<Component, MarkEntry> pair in activeMarks) ReleaseMarker(pair.Value.Marker);
        activeMarks.Clear();
        expiredTargets.Clear();
        enabled = false;
    }

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        enabled = false;
    }

    private void Update()
    {
        using (HighlightMarker.Auto())
        using (MarkMarker.Auto())
        {
            expiredTargets.Clear();
            foreach (KeyValuePair<Component, MarkEntry> pair in activeMarks)
            {
                MarkEntry entry = pair.Value;
                if (pair.Key == null || Time.time >= entry.CooldownEndTime)
                {
                    expiredTargets.Add(pair.Key);
                    continue;
                }
                UpdateEntry(ref entry);
            }

            for (int i = 0; i < expiredTargets.Count; i++)
            {
                Component target = expiredTargets[i];
                if (!ReferenceEquals(target, null) && activeMarks.TryGetValue(target, out MarkEntry entry))
                {
                    ReleaseMarker(entry.Marker);
                    activeMarks.Remove(target);
                }
            }
            enabled = activeMarks.Count > 0;
        }
    }

    private void OnDisable()
    {
        if (activeMarks.Count > 0 && gameObject.activeInHierarchy) return;
        if (!gameObject.activeInHierarchy) ClearAll();
    }

    private void OnDestroy()
    {
        ClearAll();
    }

    private void UpdateEntry(ref MarkEntry entry)
    {
        if (!TryCalculateBounds(entry.Target, entry.Renderers, out Bounds bounds))
        {
            entry.Marker.enabled = false;
            return;
        }

        bool marked = Time.time < entry.MarkEndTime;
        float pulse = marked ? 1f + Mathf.Sin(Time.time * 8f) * 0.08f : 1f;
        Transform markerTransform = entry.Marker.transform;
        markerTransform.position = new Vector3(bounds.center.x, bounds.max.y + 0.28f, bounds.center.z);
        markerTransform.localScale = new Vector3(0.95f * pulse, 0.08f, 0.08f);
        entry.Marker.enabled = true;
        Color color = marked ? new Color(0.35f, 0.95f, 1f, 0.95f) : new Color(1f, 0.76f, 0.24f, 0.65f);
        propertyBlock.Clear();
        propertyBlock.SetColor(BaseColorId, color);
        propertyBlock.SetColor(ColorId, color);
        entry.Marker.SetPropertyBlock(propertyBlock);
    }

    private MeshRenderer AcquireMarker()
    {
        EnsurePoolRoot();
        if (markerPool.Count > 0) return markerPool.Pop();
        GameObject marker = new GameObject("Pooled Camera Mark", typeof(MeshFilter), typeof(MeshRenderer));
        marker.transform.SetParent(poolRoot, false);
        MeshFilter filter = marker.GetComponent<MeshFilter>();
        MeshRenderer renderer = marker.GetComponent<MeshRenderer>();
        filter.sharedMesh = CameraHighlightSharedResources3D.SolidCubeMesh;
        renderer.sharedMaterial = CameraHighlightSharedResources3D.MarkerMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    private void ReleaseMarker(MeshRenderer marker)
    {
        if (marker == null) return;
        marker.enabled = false;
        marker.gameObject.SetActive(false);
        if (markerPool.Count < MaxPooledMarkers)
        {
            markerPool.Push(marker);
        }
        else
        {
            Destroy(marker.gameObject);
        }
    }

    private void EnsurePoolRoot()
    {
        if (poolRoot != null) return;
        GameObject root = new GameObject("Camera Highlight Pool");
        root.transform.SetParent(transform, false);
        poolRoot = root.transform;
    }

    private static Renderer[] CacheRenderers(Component target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        int validCount = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && !renderers[i].name.StartsWith("Pooled Camera Mark")) renderers[validCount++] = renderers[i];
        }
        if (validCount == renderers.Length) return renderers;
        Renderer[] result = new Renderer[validCount];
        System.Array.Copy(renderers, result, validCount);
        return result;
    }

    private static bool TryCalculateBounds(Component target, Renderer[] renderers, out Bounds bounds)
    {
        bounds = new Bounds(target != null ? target.transform.position : Vector3.zero, Vector3.one);
        bool found = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return found && SafeMath3D.IsFinite(bounds.center) && SafeMath3D.IsFinite(bounds.extents);
    }

    private struct MarkEntry
    {
        public readonly Component Target;
        public readonly MeshRenderer Marker;
        public readonly Renderer[] Renderers;
        public float MarkEndTime;
        public float CooldownEndTime;

        public MarkEntry(Component target, MeshRenderer marker, Renderer[] renderers)
        {
            Target = target;
            Marker = marker;
            Renderers = renderers;
            MarkEndTime = 0f;
            CooldownEndTime = 0f;
        }
    }
}
