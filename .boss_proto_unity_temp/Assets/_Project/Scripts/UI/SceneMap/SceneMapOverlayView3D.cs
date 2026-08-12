using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SceneMapOverlayView3D : MonoBehaviour
{
    private sealed class RoomVisual
    {
        public SceneMapRoomData3D room;
        public GameObject root;
        public Image background;
        public Outline outline;
    }

    private readonly Dictionary<string, RoomVisual> roomVisuals = new Dictionary<string, RoomVisual>();
    private readonly HashSet<string> builtConnectionPairs = new HashSet<string>();
    private SceneMapSettings3D settings;
    private SceneMapGraphData3D graphData;
    private GameObject mapRoot;
    private RectTransform mapContent;
    private RectTransform connectionRoot;
    private RectTransform roomRoot;
    private Text currentRoomLabel;
    private Font runtimeFont;
    private Vector2 centerOffset;
    private bool initialized;

    public void Initialize(SceneMapSettings3D mapSettings, SceneMapGraphData3D mapGraphData)
    {
        if (initialized) return;
        settings = mapSettings;
        graphData = mapGraphData;
        runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildHierarchy();
        BuildCachedVisuals();
        initialized = true;
    }

    public void SetOpen(bool open, string currentRoomKey)
    {
        if (mapRoot == null) return;
        if (open) RefreshCurrentRoom(currentRoomKey);
        if (mapRoot.activeSelf != open) mapRoot.SetActive(open);
    }

    private void RefreshCurrentRoom(string currentRoomKey)
    {
        foreach (KeyValuePair<string, RoomVisual> pair in roomVisuals)
        {
            RoomVisual visual = pair.Value;
            bool current = string.Equals(pair.Key, currentRoomKey, System.StringComparison.Ordinal);
            visual.background.color = current ? settings.CurrentRoomColor : visual.room.RoomColor;
            visual.outline.enabled = current;
        }
        if (graphData.TryGetRoomByKey(currentRoomKey, out SceneMapRoomData3D currentRoom))
            currentRoomLabel.text = "CURRENT AREA  ·  " + currentRoom.DisplayName;
        else currentRoomLabel.text = string.Empty;
    }

    private void BuildHierarchy()
    {
        mapRoot = CreateObject("MapRootPanel", transform, typeof(Image));
        RectTransform rootRect = mapRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = settings.PanelMargin;
        rootRect.offsetMax = -settings.PanelMargin;
        Image background = mapRoot.GetComponent<Image>();
        Color backgroundColor = settings.BackgroundColor;
        backgroundColor.a *= settings.BackgroundOpacity;
        background.color = backgroundColor;
        background.raycastTarget = true;

        Text title = CreateText("Title", rootRect, "AREA MAP", settings.TitleFontSize, TextAnchor.MiddleCenter);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -8f);
        titleRect.sizeDelta = new Vector2(0f, 44f);
        title.color = settings.TitleColor;

        currentRoomLabel = CreateText("CurrentRoomLabel", rootRect, string.Empty, 18, TextAnchor.MiddleCenter);
        RectTransform currentRect = currentRoomLabel.rectTransform;
        currentRect.anchorMin = new Vector2(0f, 1f);
        currentRect.anchorMax = new Vector2(1f, 1f);
        currentRect.pivot = new Vector2(0.5f, 1f);
        currentRect.anchoredPosition = new Vector2(0f, -51f);
        currentRect.sizeDelta = new Vector2(0f, 30f);

        GameObject viewport = CreateObject("MapViewport", rootRect, typeof(RectMask2D));
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(28f, 54f);
        viewportRect.offsetMax = new Vector2(-28f, -88f);

        mapContent = CreateObject("MapContent", viewportRect).GetComponent<RectTransform>();
        mapContent.anchorMin = mapContent.anchorMax = new Vector2(0.5f, 0.5f);
        mapContent.pivot = new Vector2(0.5f, 0.5f);
        mapContent.anchoredPosition = Vector2.zero;
        mapContent.sizeDelta = new Vector2(2200f, 1100f);
        connectionRoot = CreateContainer("ConnectionLineContainer", mapContent);
        roomRoot = CreateContainer("RoomNodeContainer", mapContent);

        Text hint = CreateText("CloseHint", rootRect, "TAB / ESC  ·  CLOSE MAP", settings.HintFontSize, TextAnchor.MiddleCenter);
        RectTransform hintRect = hint.rectTransform;
        hintRect.anchorMin = new Vector2(0f, 0f);
        hintRect.anchorMax = new Vector2(1f, 0f);
        hintRect.pivot = new Vector2(0.5f, 0f);
        hintRect.anchoredPosition = new Vector2(0f, 10f);
        hintRect.sizeDelta = new Vector2(0f, 32f);
        mapRoot.SetActive(false);
    }

    private void BuildCachedVisuals()
    {
        CalculateCenterOffset();
        IReadOnlyList<SceneMapConnectionData3D> connections = graphData.Connections;
        for (int i = 0; i < connections.Count; i++) CreateConnectionOnce(connections[i]);
        IReadOnlyList<SceneMapRoomData3D> rooms = graphData.Rooms;
        for (int i = 0; i < rooms.Count; i++)
            if (rooms[i] != null && rooms[i].Active) CreateRoom(rooms[i]);
    }

    private void CalculateCenterOffset()
    {
        bool found = false;
        Vector2 minimum = Vector2.zero;
        Vector2 maximum = Vector2.zero;
        IReadOnlyList<SceneMapRoomData3D> rooms = graphData.Rooms;
        for (int i = 0; i < rooms.Count; i++)
        {
            SceneMapRoomData3D room = rooms[i];
            if (room == null || !room.Active) continue;
            if (!found) { minimum = maximum = room.MapPosition; found = true; }
            else { minimum = Vector2.Min(minimum, room.MapPosition); maximum = Vector2.Max(maximum, room.MapPosition); }
        }
        centerOffset = found ? -(minimum + maximum) * 0.5f : Vector2.zero;
    }

    private void CreateRoom(SceneMapRoomData3D room)
    {
        GameObject root = CreateObject(room.SceneName, roomRoot, typeof(Image), typeof(Outline));
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = room.MapPosition + centerOffset;
        Vector2 size = room.MapSize;
        rect.sizeDelta = size.x > 0f && size.y > 0f ? size : settings.DefaultRoomSize;
        Image image = root.GetComponent<Image>();
        image.color = room.RoomColor;
        image.raycastTarget = false;
        Outline outline = root.GetComponent<Outline>();
        outline.effectColor = settings.CurrentRoomOutlineColor;
        outline.effectDistance = new Vector2(4f, -4f);
        outline.enabled = false;
        Text label = CreateText("RoomName", rect, room.DisplayName, settings.RoomFontSize, TextAnchor.MiddleCenter);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(8f, 5f);
        label.rectTransform.offsetMax = new Vector2(-8f, -5f);
        roomVisuals.Add(room.StableSceneKey, new RoomVisual { room = room, root = root, background = image, outline = outline });
    }

    private void CreateConnectionOnce(SceneMapConnectionData3D connection)
    {
        if (connection == null || !connection.CanDraw) return;
        if (!graphData.TryGetRoomByKey(connection.FromRoomKey, out SceneMapRoomData3D from)
            || !graphData.TryGetRoomByKey(connection.ToRoomKey, out SceneMapRoomData3D to)
            || !from.Active || !to.Active) return;
        string pairKey = string.CompareOrdinal(from.StableSceneKey, to.StableSceneKey) <= 0
            ? from.StableSceneKey + "|" + to.StableSceneKey
            : to.StableSceneKey + "|" + from.StableSceneKey;
        if (!builtConnectionPairs.Add(pairKey)) return;
        Vector2 start = from.MapPosition + centerOffset;
        Vector2 end = to.MapPosition + centerOffset;
        Vector2 delta = end - start;
        GameObject line = CreateObject(from.SceneName + " - " + to.SceneName, connectionRoot, typeof(Image));
        RectTransform rect = line.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = (start + end) * 0.5f;
        rect.sizeDelta = new Vector2(delta.magnitude, settings.ConnectionThickness);
        rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        Image image = line.GetComponent<Image>();
        image.color = settings.ConnectionColor;
        image.raycastTarget = false;
    }

    private RectTransform CreateContainer(string name, Transform parent)
    {
        RectTransform rect = CreateObject(name, parent).GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    private Text CreateText(string name, Transform parent, string value, int size, TextAnchor alignment)
    {
        Text text = CreateObject(name, parent, typeof(Text)).GetComponent<Text>();
        text.font = runtimeFont;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = settings.RoomLabelColor;
        text.text = value;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private GameObject CreateObject(string name, Transform parent, params System.Type[] components)
    {
        GameObject result = new GameObject(name, typeof(RectTransform));
        result.layer = gameObject.layer;
        result.transform.SetParent(parent, false);
        for (int i = 0; i < components.Length; i++) result.AddComponent(components[i]);
        return result;
    }
}
