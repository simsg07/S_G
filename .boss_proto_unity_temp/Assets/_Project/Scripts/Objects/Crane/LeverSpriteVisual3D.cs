using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("_Project/Magnetic Object Mover/Lever Sprite Visual 3D")]
public sealed class LeverSpriteVisual3D : MonoBehaviour
{
    [Header("Renderer / Shared Frames")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite neutralFrame;
    [SerializeField] private Sprite leftMiddleFrame;
    [SerializeField] private Sprite leftEndFrame;
    [SerializeField] private Sprite rightMiddleFrame;
    [SerializeField] private Sprite rightEndFrame;

    [Header("Visual Mapping")]
    [Tooltip("Visual-only switch. For the vertical lever, Up normally uses the left frames and Down uses the right frames.")]
    [SerializeField] private bool swapDirectionVisual;
    [Min(0.01f)] [SerializeField] private float frameDuration = 0.1f;
    [SerializeField] private bool returnToNeutralOnArrival = true;
    [SerializeField] private bool returnToNeutralOnCancel = true;

    private Sprite middleFrame;
    private Sprite endFrame;
    private float elapsed;
    private VisualPhase phase;

    private enum VisualPhase { Neutral, ToMiddle, ToEnd, Holding, ToMiddleOnReturn, ToNeutral }

    public SpriteRenderer SpriteRenderer => spriteRenderer;
    public bool IsNeutral => phase == VisualPhase.Neutral;

    private void Awake() => SetNeutralImmediate();
    private void OnEnable() => SetNeutralImmediate();
    private void OnDisable() => SetNeutralImmediate();

    private void Update()
    {
        if (phase == VisualPhase.Neutral || phase == VisualPhase.Holding) return;
        elapsed += Time.deltaTime;
        if (elapsed < frameDuration) return;
        elapsed = 0f;
        switch (phase)
        {
            case VisualPhase.ToMiddle:
                SetSprite(middleFrame);
                phase = VisualPhase.ToEnd;
                break;
            case VisualPhase.ToEnd:
                SetSprite(endFrame);
                phase = VisualPhase.Holding;
                break;
            case VisualPhase.ToMiddleOnReturn:
                SetSprite(middleFrame);
                phase = VisualPhase.ToNeutral;
                break;
            case VisualPhase.ToNeutral:
                SetNeutralImmediate();
                break;
        }
    }

    public void PlayAcceptedCommand(bool negativeDirection)
    {
        bool useLeftFrames = swapDirectionVisual ? !negativeDirection : negativeDirection;
        middleFrame = useLeftFrames ? leftMiddleFrame : rightMiddleFrame;
        endFrame = useLeftFrames ? leftEndFrame : rightEndFrame;
        elapsed = 0f;
        SetSprite(neutralFrame);
        phase = VisualPhase.ToMiddle;
    }

    public void FinishAcceptedCommand(bool cancelled)
    {
        if ((cancelled && !returnToNeutralOnCancel) || (!cancelled && !returnToNeutralOnArrival)) return;
        if (phase == VisualPhase.Neutral) return;
        elapsed = 0f;
        SetSprite(endFrame != null ? endFrame : neutralFrame);
        phase = VisualPhase.ToMiddleOnReturn;
    }

    public void SetNeutralImmediate()
    {
        elapsed = 0f;
        phase = VisualPhase.Neutral;
        SetSprite(neutralFrame);
    }

    private void SetSprite(Sprite sprite)
    {
        if (spriteRenderer != null && sprite != null) spriteRenderer.sprite = sprite;
    }
}
