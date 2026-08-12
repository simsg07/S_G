using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SceneMapSettings3D", menuName = "_Project/Scene Map/Settings 3D")]
public sealed class SceneMapSettings3D : ScriptableObject
{
    [Header("Input Actions")]
    [SerializeField] private string mapActionPath = "Player/Map";
    [SerializeField] private string cancelActionPath = "UI/Cancel";

    [Header("Overlay")]
    [SerializeField] private Vector2 panelMargin = new Vector2(80f, 55f);
    [SerializeField, Range(0f, 1f)] private float backgroundOpacity = 0.94f;
    [SerializeField] private Color backgroundColor = new Color(0.025f, 0.04f, 0.065f, 1f);
    [SerializeField] private Color titleColor = Color.white;
    [SerializeField, Min(12)] private int titleFontSize = 30;
    [SerializeField, Min(10)] private int roomFontSize = 17;
    [SerializeField, Min(10)] private int hintFontSize = 16;

    [Header("Rooms And Connections")]
    [SerializeField] private Vector2 defaultRoomSize = new Vector2(160f, 68f);
    [SerializeField, Min(1f)] private float connectionThickness = 5f;
    [SerializeField] private Color defaultRoomColor = new Color(0.18f, 0.25f, 0.34f, 1f);
    [SerializeField] private Color currentRoomColor = new Color(0.12f, 0.82f, 1f, 1f);
    [SerializeField] private Color currentRoomOutlineColor = Color.white;
    [SerializeField] private Color connectionColor = new Color(0.42f, 0.64f, 0.78f, 0.9f);
    [SerializeField] private Color roomLabelColor = Color.white;

    [Header("Draft Layout")]
    [SerializeField] private string rootRoomKey = "room-e7388b215dfc0a44984b6f7f1b564c8d";
    [SerializeField, Min(20f)] private float horizontalSpacing = 210f;
    [SerializeField, Min(20f)] private float verticalSpacing = 115f;

    [Header("Explicit Scene Exclusions")]
    [SerializeField, Tooltip("Scene GUIDs excluded from the gameplay map. Scene-name heuristics are not used.")]
    private List<string> explicitlyExcludedSceneGuids = new List<string>();

    public string MapActionPath => mapActionPath;
    public string CancelActionPath => cancelActionPath;
    public Vector2 PanelMargin => panelMargin;
    public float BackgroundOpacity => backgroundOpacity;
    public Color BackgroundColor => backgroundColor;
    public Color TitleColor => titleColor;
    public int TitleFontSize => titleFontSize;
    public int RoomFontSize => roomFontSize;
    public int HintFontSize => hintFontSize;
    public Vector2 DefaultRoomSize => defaultRoomSize;
    public float ConnectionThickness => connectionThickness;
    public Color DefaultRoomColor => defaultRoomColor;
    public Color CurrentRoomColor => currentRoomColor;
    public Color CurrentRoomOutlineColor => currentRoomOutlineColor;
    public Color ConnectionColor => connectionColor;
    public Color RoomLabelColor => roomLabelColor;
    public string RootRoomKey => rootRoomKey;
    public float HorizontalSpacing => horizontalSpacing;
    public float VerticalSpacing => verticalSpacing;

    public bool IsSceneExplicitlyExcluded(string sceneGuid)
    {
        if (string.IsNullOrWhiteSpace(sceneGuid)) return false;
        for (int i = 0; i < explicitlyExcludedSceneGuids.Count; i++)
            if (string.Equals(explicitlyExcludedSceneGuids[i], sceneGuid, System.StringComparison.Ordinal)) return true;
        return false;
    }
}
