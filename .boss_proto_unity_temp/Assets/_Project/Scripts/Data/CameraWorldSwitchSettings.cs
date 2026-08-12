using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "CameraWorldSwitchSettings", menuName = "S_G/Data/Camera World Switch Settings")]
public class CameraWorldSwitchSettings : ScriptableObject
{
    [Header("Light")]
    [SerializeField] private float lightRangeRadius = 4f; // Circular movement range for the camera light.
    [SerializeField] private float lightMoveSpeed = 24f; // Light follow speed. Higher values make the light catch the mouse faster.
    [FormerlySerializedAs("lightWorldZ")]
    [SerializeField] private float lightZPosition = -0.55f; // Fixed 2.5D Z position used by the camera light.
    [SerializeField, Range(0f, 0.45f)] private float lightViewportMargin = 0.04f; // Screen-edge padding that keeps the light inside the camera view.
    [SerializeField] private bool showLightRangeGizmo = true; // Draws the circular light range in the Scene view.

    [Header("World Switch")]
    [SerializeField] private string[] switchableTags =
    {
        "SwitchableWorldObject",
        "PuzzleObject",
        "Door",
        "Platform"
    }; // Tags allowed to be switched by the camera range world switch.
    [SerializeField] private LayerMask switchTargetLayers = ~0; // Layers searched for camera range world switching.
    [SerializeField] private float switchBoundsMargin = 0.2f; // Extra camera bounds padding used when testing switch targets.
    [SerializeField] private float switchDepth = 20f; // Z depth of the 2.5D camera range query.
    [SerializeField] private float queryPlaneZ = 0f; // Gameplay plane Z used to build camera-space query bounds.
    [SerializeField] private bool debugDraw = true; // Draws camera world-switch range in the Scene view.

    public float LightMoveSpeed => Mathf.Max(0f, lightMoveSpeed);
    public float LightRangeRadius => Mathf.Max(0f, lightRangeRadius);
    public float LightWorldZ => lightZPosition;
    public float LightZPosition => lightZPosition;
    public float LightViewportMargin => Mathf.Clamp(lightViewportMargin, 0f, 0.45f);
    public bool ShowLightRangeGizmo => showLightRangeGizmo;
    public string[] SwitchableTags => switchableTags;
    public LayerMask SwitchTargetLayers => switchTargetLayers;
    public float SwitchBoundsMargin => Mathf.Max(0f, switchBoundsMargin);
    public float SwitchDepth => Mathf.Max(0.1f, switchDepth);
    public float QueryPlaneZ => queryPlaneZ;
    public bool DebugDraw => debugDraw;
}
