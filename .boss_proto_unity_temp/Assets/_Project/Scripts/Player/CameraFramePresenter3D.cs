using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

internal sealed class CameraFramePresenter3D
{
    private readonly Transform owner;
    private readonly bool showCameraFrame;
    private readonly float frameBorderThickness;
    private readonly Color frameColor;
    private readonly Color frameAccentColor;
    private readonly Color frameRecordColor;
    private readonly Color frameCooldownColor;
    private readonly float reticleReferenceHeight;
    private readonly List<Graphic> frameTintGraphics = new List<Graphic>();

    private Canvas frameCanvas;
    private RectTransform frameRoot;
    private RectTransform reticleRoot;
    private Texture2D ringTexture;
    private Texture2D diskTexture;
    private bool cursorHiddenByFrame;
    private bool disposed;

    public CameraFramePresenter3D(
        Transform owner,
        bool showCameraFrame,
        float frameBorderThickness,
        Color frameColor,
        Color frameAccentColor,
        Color frameRecordColor,
        Color frameCooldownColor,
        float reticleReferenceHeight)
    {
        this.owner = owner;
        this.showCameraFrame = showCameraFrame;
        this.frameBorderThickness = frameBorderThickness;
        this.frameColor = frameColor;
        this.frameAccentColor = frameAccentColor;
        this.frameRecordColor = frameRecordColor;
        this.frameCooldownColor = frameCooldownColor;
        this.reticleReferenceHeight = reticleReferenceHeight;
    }

    public void Initialize()
    {
        if (disposed || !showCameraFrame || frameCanvas != null)
        {
            return;
        }

        Canvas[] existingCanvases = owner.GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < existingCanvases.Length; i++)
        {
            Canvas candidate = existingCanvases[i];
            if (candidate != null && candidate.name == "Camera Ability Frame")
            {
                BindExistingCameraFrame(candidate);
                return;
            }
        }

        GameObject canvasObject = new GameObject("Camera Ability Frame", typeof(Canvas));
        canvasObject.transform.SetParent(owner, false);

        frameCanvas = canvasObject.GetComponent<Canvas>();
        frameCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        frameCanvas.sortingOrder = 490;

        GameObject frameObject = new GameObject("Shutter Frame", typeof(RectTransform));
        frameObject.transform.SetParent(canvasObject.transform, false);
        frameRoot = frameObject.GetComponent<RectTransform>();
        frameRoot.anchorMin = Vector2.zero;
        frameRoot.anchorMax = Vector2.zero;
        frameRoot.pivot = new Vector2(0.5f, 0.5f);

        frameTintGraphics.Clear();
        CreateCameraCursorVisual(frameRoot);
    }

    public void Present(bool visible, Rect frameRect, bool cooldownActive, bool hideSystemCursor)
    {
        if (disposed || frameCanvas == null || frameRoot == null)
        {
            return;
        }

        frameCanvas.enabled = visible;
        SetSystemCursorHidden(visible && hideSystemCursor && Application.isPlaying);
        if (!visible)
        {
            return;
        }

        frameRoot.position = frameRect.center;
        frameRoot.sizeDelta = frameRect.size;

        Color color = cooldownActive ? frameCooldownColor : frameColor;
        for (int i = 0; i < frameTintGraphics.Count; i++)
        {
            if (frameTintGraphics[i] != null)
            {
                frameTintGraphics[i].color = color;
            }
        }

        if (reticleRoot != null)
        {
            float reticleScale = Mathf.Clamp(frameRect.height / reticleReferenceHeight, 0.55f, 1.45f);
            reticleRoot.localScale = Vector3.one * reticleScale;
        }
    }

    public void RestoreCursor()
    {
        if (!cursorHiddenByFrame)
        {
            return;
        }

        Cursor.visible = true;
        cursorHiddenByFrame = false;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        RestoreCursor();
        DestroyGenerated(ringTexture);
        DestroyGenerated(diskTexture);
        ringTexture = null;
        diskTexture = null;
    }

    private void BindExistingCameraFrame(Canvas existingCanvas)
    {
        frameCanvas = existingCanvas;
        frameCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        frameCanvas.sortingOrder = 490;

        frameTintGraphics.Clear();
        RectTransform[] rects = frameCanvas.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rects.Length; i++)
        {
            RectTransform rect = rects[i];
            if (rect == null) continue;

            if (rect.name == "Shutter Frame") frameRoot = rect;
            else if (rect.name == "Cursor Reticle") reticleRoot = rect;

            Graphic graphic = rect.GetComponent<Graphic>();
            if (graphic != null && rect.name != "Capture Dot" && rect.name != "Capture Dot Halo")
            {
                frameTintGraphics.Add(graphic);
            }

            RawImage rawImage = graphic as RawImage;
            if (rawImage == null) continue;
            rawImage.texture = rect.name == "Capture Dot" ? GetDiskTexture() : GetRingTexture();
        }

        if (frameRoot == null)
        {
            Debug.LogWarning("[CameraAbilitySystem3D] Existing camera canvas has no Shutter Frame; rebuilding runtime frame.", owner);
            GameObject frameObject = new GameObject("Shutter Frame", typeof(RectTransform));
            frameObject.transform.SetParent(frameCanvas.transform, false);
            frameRoot = frameObject.GetComponent<RectTransform>();
            frameRoot.anchorMin = Vector2.zero;
            frameRoot.anchorMax = Vector2.zero;
            frameRoot.pivot = new Vector2(0.5f, 0.5f);
            frameTintGraphics.Clear();
            CreateCameraCursorVisual(frameRoot);
        }

        frameCanvas.enabled = false;
    }

    private void CreateCameraCursorVisual(RectTransform parent)
    {
        float thick = Mathf.Max(1f, frameBorderThickness);
        CreateFrameLine(parent, "Top Rail", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, thick * 1.8f), frameColor);
        CreateFrameLine(parent, "Bottom Rail", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, thick * 1.8f), frameColor);
        CreateFrameLine(parent, "Left Rail", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(thick * 1.8f, 0f), frameColor);
        CreateFrameLine(parent, "Right Rail", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(thick * 1.8f, 0f), frameColor);
        CreateFrameLine(parent, "Top Inner Rail", new Vector2(0.09f, 1f), new Vector2(0.91f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(0f, thick), frameAccentColor);
        CreateFrameLine(parent, "Bottom Inner Rail", new Vector2(0.09f, 0f), new Vector2(0.91f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(0f, thick), frameAccentColor);
        CreateFrameLine(parent, "Top Scanline A", new Vector2(0.22f, 1f), new Vector2(0.78f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(0f, 1f), frameAccentColor);
        CreateFrameLine(parent, "Bottom Scanline A", new Vector2(0.22f, 0f), new Vector2(0.78f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(0f, 1f), frameAccentColor);
        CreateFrameLine(parent, "Center Left Trace", new Vector2(0.04f, 0.5f), new Vector2(0.36f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 1f), frameAccentColor);
        CreateFrameLine(parent, "Center Right Trace", new Vector2(0.64f, 0.5f), new Vector2(0.96f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 1f), frameAccentColor);
        CreateFrameLine(parent, "Left Interior Tick", new Vector2(0.06f, 0.32f), new Vector2(0.28f, 0.32f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 1f), frameAccentColor);
        CreateFrameLine(parent, "Right Interior Tick", new Vector2(0.72f, 0.32f), new Vector2(0.94f, 0.32f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(0f, 1f), frameAccentColor);
        CreateFrameLine(parent, "Top Left Slash", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(44f, -24f), new Vector2(118f, thick * 1.5f), frameColor, -38f);
        CreateFrameLine(parent, "Top Right Slash", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-44f, -24f), new Vector2(118f, thick * 1.5f), frameColor, 38f);
        CreateFrameLine(parent, "Bottom Left Slash", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), new Vector2(44f, 24f), new Vector2(118f, thick * 1.5f), frameColor, 38f);
        CreateFrameLine(parent, "Bottom Right Slash", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-44f, 24f), new Vector2(118f, thick * 1.5f), frameColor, -38f);
        CreateFrameText(parent, "RSEQ Label", "RSEQ", new Vector2(0.5f, 0f), new Vector2(0f, 21f), new Vector2(126f, 34f), 22, frameColor, TextAnchor.MiddleCenter);
        CreateFrameText(parent, "SUB Label", "SUB", new Vector2(0.78f, 0.12f), Vector2.zero, new Vector2(86f, 30f), 18, frameColor, TextAnchor.MiddleCenter);

        GameObject reticleObject = new GameObject("Cursor Reticle", typeof(RectTransform));
        reticleObject.transform.SetParent(parent, false);
        reticleRoot = reticleObject.GetComponent<RectTransform>();
        reticleRoot.anchorMin = new Vector2(0.5f, 0.5f);
        reticleRoot.anchorMax = new Vector2(0.5f, 0.5f);
        reticleRoot.pivot = new Vector2(0.5f, 0.5f);
        reticleRoot.anchoredPosition = Vector2.zero;
        reticleRoot.sizeDelta = new Vector2(170f, 170f);
        CreateFrameTexture(reticleRoot, "Outer Reticle Ring", GetRingTexture(), Vector2.zero, new Vector2(170f, 170f), frameColor, true);
        CreateFrameTexture(reticleRoot, "Inner Reticle Ring", GetRingTexture(), Vector2.zero, new Vector2(108f, 108f), frameAccentColor, true);
        CreateFrameTexture(reticleRoot, "Capture Dot Halo", GetRingTexture(), Vector2.zero, new Vector2(64f, 64f), frameRecordColor, false);
        CreateFrameTexture(reticleRoot, "Capture Dot", GetDiskTexture(), Vector2.zero, new Vector2(34f, 34f), frameRecordColor, false);
        CreateFrameLine(reticleRoot, "Reticle Top Gap", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -11f), new Vector2(76f, thick * 1.8f), frameColor);
        CreateFrameLine(reticleRoot, "Reticle Bottom Gap", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 11f), new Vector2(76f, thick * 1.8f), frameColor);
        CreateFrameLine(reticleRoot, "Reticle Left Tick", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(12f, 0f), new Vector2(36f, thick * 1.5f), frameColor, 45f);
        CreateFrameLine(reticleRoot, "Reticle Right Tick", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-12f, 0f), new Vector2(36f, thick * 1.5f), frameColor, -45f);
    }

    private Image CreateFrameLine(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size, Color color, float rotation = 0f, bool tintWithFrame = true)
    {
        GameObject item = new GameObject(name, typeof(RectTransform), typeof(Image));
        item.transform.SetParent(parent, false);
        RectTransform rect = item.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition; rect.sizeDelta = size;
        rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
        Image image = item.GetComponent<Image>();
        image.color = color; image.raycastTarget = false;
        if (tintWithFrame) frameTintGraphics.Add(image);
        return image;
    }

    private RawImage CreateFrameTexture(RectTransform parent, string name, Texture texture, Vector2 anchoredPosition, Vector2 size, Color color, bool tintWithFrame)
    {
        GameObject item = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        item.transform.SetParent(parent, false);
        RectTransform rect = item.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition; rect.sizeDelta = size;
        RawImage image = item.GetComponent<RawImage>();
        image.texture = texture; image.color = color; image.raycastTarget = false;
        if (tintWithFrame) frameTintGraphics.Add(image);
        return image;
    }

    private Text CreateFrameText(RectTransform parent, string name, string value, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, int fontSize, Color color, TextAnchor alignment)
    {
        GameObject item = new GameObject(name, typeof(RectTransform), typeof(Text));
        item.transform.SetParent(parent, false);
        RectTransform rect = item.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor; rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition; rect.sizeDelta = size;
        Text text = item.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value; text.fontSize = fontSize; text.alignment = alignment;
        text.color = color; text.raycastTarget = false;
        frameTintGraphics.Add(text);
        return text;
    }

    private Texture2D GetRingTexture()
    {
        if (ringTexture == null) ringTexture = CreateCircleTexture("Generated Camera Ring Texture", 128, false, 0.075f);
        return ringTexture;
    }

    private Texture2D GetDiskTexture()
    {
        if (diskTexture == null) diskTexture = CreateCircleTexture("Generated Camera Dot Texture", 64, true, 0.1f);
        return diskTexture;
    }

    private static Texture2D CreateCircleTexture(string textureName, int size, bool filled, float ringThickness)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = textureName, hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
        };
        Color32[] pixels = new Color32[size * size];
        float radius = (size - 1) * 0.5f;
        float innerRadius = radius * Mathf.Clamp01(1f - ringThickness);
        Vector2 center = new Vector2(radius, radius);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                bool inside = filled ? distance <= radius : distance <= radius && distance >= innerRadius;
                pixels[y * size + x] = new Color32(255, 255, 255, inside ? (byte)255 : (byte)0);
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false);
        return texture;
    }

    private void SetSystemCursorHidden(bool hidden)
    {
        if (!Application.isPlaying || cursorHiddenByFrame == hidden) return;
        Cursor.visible = !hidden;
        cursorHiddenByFrame = hidden;
    }

    private static void DestroyGenerated(Object target)
    {
        if (target == null) return;
        if (Application.isPlaying) Object.Destroy(target);
        else Object.DestroyImmediate(target);
    }
}
