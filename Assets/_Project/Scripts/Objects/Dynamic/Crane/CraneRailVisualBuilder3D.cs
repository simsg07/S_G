using UnityEngine;
using UnityEngine.Serialization;

[ExecuteAlways]
[DisallowMultipleComponent]
public class CraneRailVisualBuilder3D : MonoBehaviour
{
    [Header("Sprites")]
    [FormerlySerializedAs("startSprite")]
    [SerializeField] private Sprite leftEndSprite;
    [SerializeField] private Sprite middleSprite;
    [FormerlySerializedAs("endSprite")]
    [SerializeField] private Sprite rightEndSprite;

    [Header("Points")]
    [Tooltip("Optional RailPath source. When assigned, Point_A and Point_B can be pulled from this component.")]
    [SerializeField] private CraneRailPath3D railPath;
    [SerializeField] private Transform pointA;
    [SerializeField] private Transform pointB;
    [SerializeField] private Transform visualRoot;

    [Header("Visual Layout")]
    [Min(0.01f)]
    [FormerlySerializedAs("segmentWorldWidth")]
    [SerializeField] private float middleSegmentWorldWidth = 1f;
    [Tooltip("Scale each sprite piece on X so the rail can be stretched by segment width instead of prefab scale.")]
    [SerializeField] private bool scalePiecesToSegmentWidth = true;
    [SerializeField] private Vector3 visualOffset;
    [SerializeField] private int sortingOrder = 5;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private bool rebuildOnValidate;
    [SerializeField] private bool clearOldSegments = true;

    [Header("Debug")]
    [SerializeField] private bool showGizmo = true;
    [SerializeField] private bool debugMode = true;

    public void SetPoints(Transform start, Transform end)
    {
        pointA = start;
        pointB = end;
    }

    public void SetRailPath(CraneRailPath3D path)
    {
        railPath = path;
        PullPointsFromRailPath();
    }

    public void SetSprites(Sprite start, Sprite middle, Sprite end)
    {
        leftEndSprite = start;
        middleSprite = middle;
        rightEndSprite = end;
    }

    [ContextMenu("Rebuild Rail Visual")]
    public void RebuildRailVisual()
    {
        EnsureVisualRoot();
        PullPointsFromRailPath();
        if (clearOldSegments) ClearRailVisual();

        if (pointA == null || pointB == null)
        {
            Warn("Point_A and Point_B must be assigned before rebuilding the rail visual.");
            return;
        }

        if (leftEndSprite == null && middleSprite == null && rightEndSprite == null)
        {
            Warn("Rail sprites are missing.");
            return;
        }

        Vector3 start = pointA.position + visualOffset;
        Vector3 end = pointB.position + visualOffset;
        Vector3 segment = end - start;
        segment.z = 0f;
        float length = segment.magnitude;
        if (length <= Mathf.Epsilon)
        {
            Warn("Point_A and Point_B are at the same position.");
            return;
        }

        Vector3 direction = segment / length;
        Quaternion rotation = Quaternion.FromToRotation(Vector3.right, direction);
        CreateSpritePart("Rail_LeftEnd", start, rotation, leftEndSprite != null ? leftEndSprite : middleSprite, middleSegmentWorldWidth);
        CreateMiddleSprites(start, direction, rotation, length);
        CreateSpritePart("Rail_RightEnd", end, rotation, rightEndSprite != null ? rightEndSprite : middleSprite, middleSegmentWorldWidth);
    }

    [ContextMenu("Clear Rail Visual")]
    public void ClearRailVisual()
    {
        EnsureVisualRoot();
        for (int i = visualRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = visualRoot.GetChild(i);
            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    [ContextMenu("Validate Rail Visual Setup")]
    public void ValidateRailVisualSetup()
    {
        Debug.Log(
            "[CraneRailVisualBuilder3D] Validate Rail Visual Setup\n" +
            $"- Point_A: {(pointA != null ? pointA.name : "None")}\n" +
            $"- Point_B: {(pointB != null ? pointB.name : "None")}\n" +
            $"- RailPath: {(railPath != null ? railPath.name : "None")}\n" +
            $"- Visual Root: {(visualRoot != null ? visualRoot.name : "None")}\n" +
            $"- Left End Sprite: {(leftEndSprite != null ? leftEndSprite.name : "None")}\n" +
            $"- Middle Sprite: {(middleSprite != null ? middleSprite.name : "None")}\n" +
            $"- Right End Sprite: {(rightEndSprite != null ? rightEndSprite.name : "None")}\n" +
            $"- Segment Width: {middleSegmentWorldWidth}\n" +
            $"- Sorting Layer: {sortingLayerName}",
            this);
    }

    private void OnValidate()
    {
        middleSegmentWorldWidth = Mathf.Max(0.01f, middleSegmentWorldWidth);
        EnsureVisualRoot();
        PullPointsFromRailPath();
        if (rebuildOnValidate && !Application.isPlaying) RebuildRailVisual();
    }

    [ContextMenu("Pull Points From RailPath")]
    public void PullPointsFromRailPath()
    {
        if (railPath == null || !railPath.IsValid)
        {
            return;
        }

        pointA = railPath.PointATransform;
        pointB = railPath.PointBTransform;
    }

    private void EnsureVisualRoot()
    {
        if (visualRoot != null)
        {
            return;
        }

        Transform existingRoot = transform.Find("RailVisualRoot");
        if (existingRoot != null)
        {
            visualRoot = existingRoot;
            return;
        }

        GameObject root = new GameObject("RailVisualRoot");
        root.transform.SetParent(transform, false);
        visualRoot = root.transform;
    }

    private void CreateMiddleSprites(Vector3 start, Vector3 direction, Quaternion rotation, float length)
    {
        Sprite sprite = middleSprite != null ? middleSprite : leftEndSprite;
        if (sprite == null || length <= middleSegmentWorldWidth)
        {
            return;
        }

        int count = Mathf.Max(0, Mathf.FloorToInt(length / middleSegmentWorldWidth) - 1);
        for (int i = 0; i < count; i++)
        {
            float distance = Mathf.Min(length - middleSegmentWorldWidth * 0.5f, middleSegmentWorldWidth * (i + 1));
            CreateSpritePart($"Rail_Middle_{i:00}", start + direction * distance, rotation, sprite, middleSegmentWorldWidth);
        }
    }

    private void CreateSpritePart(string partName, Vector3 position, Quaternion rotation, Sprite sprite, float targetWidth)
    {
        if (sprite == null || visualRoot == null)
        {
            return;
        }

        GameObject part = new GameObject(partName);
        part.transform.SetParent(visualRoot, true);
        part.transform.SetPositionAndRotation(position, rotation);
        part.transform.localScale = Vector3.one;

        SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
        if (HasSortingLayer(sortingLayerName)) renderer.sortingLayerName = sortingLayerName;

        if (scalePiecesToSegmentWidth)
        {
            float spriteWidth = Mathf.Max(0.01f, sprite.bounds.size.x);
            part.transform.localScale = new Vector3(targetWidth / spriteWidth, 1f, 1f);
        }
    }

    private static bool HasSortingLayer(string layerName)
    {
        if (string.IsNullOrWhiteSpace(layerName)) return false;
        SortingLayer[] layers = SortingLayer.layers;
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i].name == layerName) return true;
        }
        return false;
    }

    private void OnDrawGizmos()
    {
        if (!showGizmo || pointA == null || pointB == null)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.8f);
        Gizmos.DrawLine(pointA.position + visualOffset, pointB.position + visualOffset);
    }

    private void Warn(string message)
    {
        if (debugMode)
        {
            Debug.LogWarning($"[CraneRailVisualBuilder3D] {message}", this);
        }
    }
}
