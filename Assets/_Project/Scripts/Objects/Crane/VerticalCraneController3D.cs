using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum VerticalCraneMovementState
{
    WaitingAtTop,
    MovingDown,
    WaitingAtBottom,
    MovingUp,
    Stopped
}

[DisallowMultipleComponent]
public sealed class VerticalCraneController3D : MonoBehaviour, IShutterFreezable3D, IShutterFreezeState3D
{
    [Header("Structure")]
    [SerializeField] private Transform fixedTopRoot;
    [FormerlySerializedAs("ropeConnector")]
    [SerializeField] private Transform ropeTopAnchor;
    [SerializeField] private Transform ropeVisualRoot;
    [SerializeField] private Transform movingAssemblyRoot;
    [FormerlySerializedAs("movingRopeAttachPoint")]
    [SerializeField] private Transform ropeBottomAnchor;
    [SerializeField] private Rigidbody movingRigidbody;
    [SerializeField] private CraneCarryZone3D carryZone;
    [SerializeField] private CraneLeverSwitch leverInteraction;

    [Header("Rope Distance")]
    [Min(1)] [SerializeField] private int ropeSegmentCount = 8;
    [Min(0.001f)] [SerializeField] private float ropeSegmentLength = 0.5f;
    [SerializeField] private GameObject ropeVisualSegmentPrefab;
    [SerializeField] private SpriteRenderer[] ropeSegmentRenderers;

    [Header("Rope Visual Alignment")]
    [SerializeField] private Vector3 topVisualOffset;
    [SerializeField] private Vector3 bottomVisualOffset;
    [Min(0f)] [SerializeField] private float bottomConnectionOverlap = 0.03f;
    [Min(0.0001f)] [SerializeField] private float ropeEndErrorTolerance = 0.005f;

    [Header("Movement")]
    [Min(0.001f)] [SerializeField] private float moveSpeed = 2f;
    [Min(0f)] [SerializeField] private float waitTimeAtTop = 1f;
    [Min(0f)] [SerializeField] private float waitTimeAtBottom = 1f;
    [Tooltip("Selects the first lever-command direction. It does not start movement automatically.")]
    [SerializeField] private bool startMovingDown = true;
    [FormerlySerializedAs("loop")]
    [SerializeField] private bool autoLoop;
    [SerializeField] private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Shutter / Debug")]
    [SerializeField] private bool canPauseByShutter = true;
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private VerticalCraneMovementState state = VerticalCraneMovementState.WaitingAtTop;

    private Vector3 initialTopPosition;
    private Vector3 bottomPosition;
    private Vector3 moveStartPosition;
    private Vector3 moveTargetPosition;
    private float moveElapsed;
    private float moveDuration;
    private float waitRemaining;
    private float shutterReleaseTime;
    private bool initialized;
    private bool nextDestinationIsBottom;

    public float MaxDropDistance => ropeSegmentCount * ropeSegmentLength;
    public Vector3 InitialTopPosition => initialTopPosition;
    public Vector3 BottomPosition => bottomPosition;
    public VerticalCraneMovementState State => state;
    public bool IsMoving => state == VerticalCraneMovementState.MovingDown || state == VerticalCraneMovementState.MovingUp;
    public bool IsShutterFrozen => shutterReleaseTime > Time.time;
    public Vector3 RopeVisualStart { get; private set; }
    public Vector3 RopeVisualEnd { get; private set; }
    public float RopeEndError { get; private set; }

    private void OnValidate()
    {
        ropeSegmentCount = Mathf.Max(1, ropeSegmentCount);
        ropeSegmentLength = Mathf.Max(0.001f, ropeSegmentLength);
        moveSpeed = Mathf.Max(0.001f, moveSpeed);
        waitTimeAtTop = Mathf.Max(0f, waitTimeAtTop);
        waitTimeAtBottom = Mathf.Max(0f, waitTimeAtBottom);
        bottomConnectionOverlap = Mathf.Max(0f, bottomConnectionOverlap);
        ropeEndErrorTolerance = Mathf.Max(0.0001f, ropeEndErrorTolerance);
        if (movementCurve == null || movementCurve.length == 0)
            movementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    }

    private void Awake()
    {
        InitializeOnce();
    }

    private void Start()
    {
        nextDestinationIsBottom = startMovingDown;
        state = VerticalCraneMovementState.Stopped;
    }

    private void Update()
    {
        if (IsShutterFrozen) return;
        if (shutterReleaseTime > 0f) shutterReleaseTime = 0f;
        if (!autoLoop || (state != VerticalCraneMovementState.WaitingAtTop && state != VerticalCraneMovementState.WaitingAtBottom)) return;

        waitRemaining -= Time.deltaTime;
        if (waitRemaining > 0f) return;
        BeginMove(state == VerticalCraneMovementState.WaitingAtBottom);
    }

    private void FixedUpdate()
    {
        if (IsShutterFrozen || (state != VerticalCraneMovementState.MovingDown && state != VerticalCraneMovementState.MovingUp)) return;

        moveElapsed = Mathf.Min(moveElapsed + Time.fixedDeltaTime, moveDuration);
        float normalized = moveDuration > 0f ? moveElapsed / moveDuration : 1f;
        float curved = Mathf.Clamp01(movementCurve.Evaluate(normalized));
        Vector3 next = Vector3.LerpUnclamped(moveStartPosition, moveTargetPosition, curved);
        Vector3 current = movingRigidbody != null ? movingRigidbody.position : movingAssemblyRoot.position;
        Vector3 delta = next - current;
        carryZone?.CarryBy(delta);
        if (movingRigidbody != null) movingRigidbody.MovePosition(next);
        else movingAssemblyRoot.position = next;

        if (moveElapsed < moveDuration) return;
        if (movingRigidbody != null) movingRigidbody.position = moveTargetPosition;
        else movingAssemblyRoot.position = moveTargetPosition;
        bool atBottom = state == VerticalCraneMovementState.MovingDown;
        state = atBottom ? VerticalCraneMovementState.WaitingAtBottom : VerticalCraneMovementState.WaitingAtTop;
        nextDestinationIsBottom = !atBottom;
        waitRemaining = atBottom ? waitTimeAtBottom : waitTimeAtTop;
    }

    private void LateUpdate()
    {
        RefreshRopeVisual();
    }

    public void InitializeOnce()
    {
        if (initialized || movingAssemblyRoot == null) return;
        initialTopPosition = movingRigidbody != null ? movingRigidbody.position : movingAssemblyRoot.position;
        bottomPosition = initialTopPosition + Vector3.down * MaxDropDistance;
        initialized = true;
        CacheRopeRenderers();
        RefreshRopeVisual();
    }

    public void BeginMoveDown() => BeginMove(false);
    public void BeginMoveUp() => BeginMove(true);

    public bool RequestMoveToOppositeDestination(bool allowWhileMoving = false)
    {
        InitializeOnce();
        if (!initialized || (IsMoving && !allowWhileMoving)) return false;
        bool moveUp = IsMoving
            ? state == VerticalCraneMovementState.MovingDown
            : !nextDestinationIsBottom;
        BeginMove(moveUp);
        return true;
    }

    private void BeginMove(bool up)
    {
        InitializeOnce();
        if (!initialized) return;
        moveStartPosition = movingRigidbody != null ? movingRigidbody.position : movingAssemblyRoot.position;
        moveTargetPosition = up ? initialTopPosition : bottomPosition;
        moveDuration = Vector3.Distance(moveStartPosition, moveTargetPosition) / moveSpeed;
        moveElapsed = 0f;
        state = up ? VerticalCraneMovementState.MovingUp : VerticalCraneMovementState.MovingDown;
    }

    public void StopMovement()
    {
        state = VerticalCraneMovementState.Stopped;
    }

    public bool ApplyShutterFreeze(float duration, CameraAbilitySystem3D source)
    {
        if (!canPauseByShutter || duration <= 0f) return false;
        shutterReleaseTime = Mathf.Max(shutterReleaseTime, Time.time + duration);
        return true;
    }

    private void CacheRopeRenderers()
    {
        if (ropeVisualRoot == null) return;
        ropeSegmentRenderers = ropeVisualRoot.GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void RefreshRopeVisual()
    {
        if (ropeTopAnchor == null || ropeBottomAnchor == null || ropeSegmentRenderers == null) return;
        Vector3 start = ropeTopAnchor.position + topVisualOffset;
        Vector3 anchorEnd = ropeBottomAnchor.position + bottomVisualOffset;
        Vector3 delta = anchorEnd - start;
        float ropeDistance = delta.magnitude;
        Vector3 direction = ropeDistance > 0.000001f ? delta / ropeDistance : Vector3.down;
        float visualLength = ropeDistance + bottomConnectionOverlap;
        RopeVisualStart = start;
        RopeVisualEnd = start;

        for (int i = 0; i < ropeSegmentRenderers.Length; i++)
        {
            SpriteRenderer renderer = ropeSegmentRenderers[i];
            if (renderer == null) continue;
            float segmentStartDistance = i * ropeSegmentLength;
            float remainingLength = visualLength - segmentStartDistance;
            float maximumShownLength = i == ropeSegmentCount - 1
                ? Mathf.Max(ropeSegmentLength, remainingLength)
                : ropeSegmentLength;
            float shownLength = Mathf.Clamp(remainingLength, 0f, maximumShownLength);
            bool visible = shownLength > 0.0001f && i < ropeSegmentCount;
            renderer.enabled = visible;
            if (!visible) continue;

            Transform segment = renderer.transform;
            Vector3 scale = segment.localScale;
            scale.y = shownLength / ropeSegmentLength;
            segment.localScale = scale;
            segment.rotation = Quaternion.FromToRotation(Vector3.down, direction);
            segment.position = start + direction * (segmentStartDistance + shownLength * 0.5f);
            RopeVisualEnd = start + direction * (segmentStartDistance + shownLength);
        }

        float coveredLength = Mathf.Max(0f, Vector3.Dot(RopeVisualEnd - start, direction));
        RopeEndError = Mathf.Max(0f, ropeDistance - coveredLength);
    }

    [ContextMenu("Rebuild Rope Segments")]
    public void RebuildRopeSegments()
    {
#if UNITY_EDITOR
        if (Application.isPlaying || ropeVisualRoot == null) return;
        Sprite templateSprite = null;
        if (ropeVisualSegmentPrefab != null)
        {
            SpriteRenderer prefabRenderer = ropeVisualSegmentPrefab.GetComponent<SpriteRenderer>();
            if (prefabRenderer != null) templateSprite = prefabRenderer.sprite;
        }
        if (templateSprite == null && ropeSegmentRenderers != null && ropeSegmentRenderers.Length > 0 && ropeSegmentRenderers[0] != null)
            templateSprite = ropeSegmentRenderers[0].sprite;

        Undo.RegisterFullObjectHierarchyUndo(ropeVisualRoot.gameObject, "Rebuild Vertical Crane Rope");
        for (int i = ropeVisualRoot.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(ropeVisualRoot.GetChild(i).gameObject);
        for (int i = 0; i < ropeSegmentCount; i++)
        {
            GameObject segment = ropeVisualSegmentPrefab != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(ropeVisualSegmentPrefab, ropeVisualRoot)
                : new GameObject();
            segment.name = $"RopeSegment_{i:000}";
            if (segment.transform.parent != ropeVisualRoot) segment.transform.SetParent(ropeVisualRoot, false);
            SpriteRenderer renderer = segment.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = Undo.AddComponent<SpriteRenderer>(segment);
            if (renderer.sprite == null) renderer.sprite = templateSprite;
            renderer.sortingOrder = 11;
            segment.transform.localPosition = new Vector3(0f, -(i + 0.5f) * ropeSegmentLength, 0f);
        }
        CacheRopeRenderers();
        EditorUtility.SetDirty(this);
#endif
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos || ropeTopAnchor == null || ropeBottomAnchor == null) return;
        Vector3 top = ropeTopAnchor.position + topVisualOffset;
        Vector3 bottom = ropeBottomAnchor.position + bottomVisualOffset;
        Vector3 delta = bottom - top;
        Vector3 direction = delta.sqrMagnitude > 0.000001f ? delta.normalized : Vector3.down;
        float availableVisualLength = delta.magnitude + bottomConnectionOverlap;
        Vector3 calculatedEnd = top + direction * availableVisualLength;
        float error = Mathf.Max(0f, delta.magnitude - availableVisualLength);

        Gizmos.color = Color.green;
        Gizmos.DrawSphere(top, 0.08f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(bottom, 0.08f);
        Gizmos.color = Color.white;
        Gizmos.DrawLine(top, bottom);
        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(calculatedEnd, 0.06f);
        if (error > ropeEndErrorTolerance)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(calculatedEnd, bottom);
#if UNITY_EDITOR
            Handles.Label((calculatedEnd + bottom) * 0.5f, $"Rope end overlap/error: {error:0.####}");
#endif
        }
    }
}
