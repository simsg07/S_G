using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

public enum CraneMagnetState { Disabled, Searching, Attracting, Holding, Releasing }

[DisallowMultipleComponent]
[AddComponentMenu("_Project/Magnetic Object Mover/Magnet Controller 3D")]
public sealed class CraneMagnetController3D : MonoBehaviour
{
    [Header("Magnetic Object Mover / 자성추 오브젝트 운반 장치")]
    [SerializeField] private bool magnetEnabledOnStart;
    [SerializeField] private KeyCode magnetToggleKey = KeyCode.G;
    [SerializeField] private bool requirePlayerInControlRange = true;
    [SerializeField] private CraneMagnetControlTrigger3D magnetControlTrigger;
    [SerializeField] private bool releaseOnDisable = true;
    [SerializeField] private bool releaseOnCraneDisable = true;
    [SerializeField] private bool releaseOnSceneChange = true;
    [SerializeField] private bool allowMagnetToggleWhileMoving = true;
    [SerializeField] private CraneXYController3D craneController;

    [Header("Magnet Detection")]
    [SerializeField] private Transform magnetAnchor;
    [SerializeField] private CraneMagnetDetectionArea3D magnetDetectionArea;
    [SerializeField] private LayerMask magnetLayerMask = (1 << 0) | (1 << 9);
    [FormerlySerializedAs("magnetRange")]
    [Min(0.01f)] [SerializeField] private float detectionRadius = 4f;
    [Min(0.01f)] [SerializeField] private float magnetWidth = 2f;
    [SerializeField] private bool requireLineOfSight = true;
    [SerializeField] private LayerMask magnetObstacleMask = 7936;
    [Min(0f)] [SerializeField] private float maximumCarryMass = 100f;
    [Min(1)] [SerializeField] private int maximumCarryTargets = 1;

    [Header("Magnet Movement")]
    [Min(0f)] [SerializeField] private float attractionSpeed = 3f;
    [Min(0f)] [SerializeField] private float attractionAcceleration = 8f;
    [Min(0f)] [SerializeField] private float maximumAttractionSpeed = 5f;
    [FormerlySerializedAs("attachDistance")]
    [Min(0.001f)] [SerializeField] private float holdRadius = 0.15f;
    [SerializeField] private Vector3 holdOffset;
    [Min(0f)] [SerializeField] private float positionFollowSpeed = 20f;
    [Min(0f)] [SerializeField] private float rotationFollowSpeed = 360f;
    [SerializeField] private bool inheritCraneVelocityOnRelease;
    [Min(0f)] [SerializeField] private float releaseVelocityMultiplier = 1f;
    [Min(0f)] [SerializeField] private float releaseOffset = 0.05f;

    [Header("Future Magnet Visual (Optional)")]
    [Tooltip("Optional renderer. Null is the intended state until dedicated magnet art exists.")]
    [SerializeField] private SpriteRenderer magnetVisualRenderer;
    [SerializeField] private Sprite magnetSprite;
    [SerializeField] private Vector3 magnetVisualOffset;
    [SerializeField] private Vector3 magnetVisualScale = Vector3.one;
    [SerializeField] private string magnetSortingLayer;
    [SerializeField] private int magnetOrderInLayer;
    [SerializeField] private bool useMagnetStateTint = true;
    [SerializeField] private Color disabledColor = new Color(0.45f, 0.45f, 0.45f, 1f);
    [SerializeField] private Color searchingColor = Color.white;
    [SerializeField] private Color attractingColor = new Color(1.1f, 1.1f, 1.1f, 1f);
    [SerializeField] private Color holdingColor = new Color(1f, 0.65f, 0.2f, 1f);
    [SerializeField] private bool showGizmos = true;

    [Header("Events")]
    [SerializeField] private UnityEvent magnetActivated = new UnityEvent();
    [SerializeField] private UnityEvent magnetDeactivated = new UnityEvent();
    [SerializeField] private UnityEvent targetDetected = new UnityEvent();
    [SerializeField] private UnityEvent attractionStarted = new UnityEvent();
    [SerializeField] private UnityEvent targetAttached = new UnityEvent();
    [SerializeField] private UnityEvent targetReleased = new UnityEvent();
    [SerializeField] private UnityEvent attractionFailed = new UnityEvent();

    [Header("Runtime State")]
    [SerializeField] private CraneMagnetState state;
    [SerializeField] private MagneticCarryable3D currentTarget;

    [Header("Runtime Debug (Read Only)")]
    [SerializeField] private MagneticCarryable3D detectedTarget;
    [SerializeField] private string detectedTargetStatus = "No trigger candidate";
    [SerializeField] private Vector3 debugAnchorPosition;
    [SerializeField] private Vector3 debugHoldPosition;
    [SerializeField] private Vector3 debugTargetPosition;
    [SerializeField] private Bounds debugTargetBounds;
    [SerializeField] private float debugTargetDistance;
    [SerializeField] private float debugDetectionRadius;
    [SerializeField] private float debugHoldRadius;
    [SerializeField] private string debugStopReason = "None";
    [SerializeField] private float debugAttractionSpeed;
    [SerializeField] private bool debugPhysicalCraneOverlap;
    [SerializeField] private CraneXYOperationState debugCraneState;
    [SerializeField] private bool debugCraneMoving;
    [SerializeField] private Vector3 debugCraneDestination;
    [SerializeField] private float debugCraneRemainingDistance;
    [SerializeField] private string debugCraneStopReason;
    [SerializeField] private RigidbodyConstraints debugOriginalTargetConstraints;
    [SerializeField] private RigidbodyConstraints debugAttractionTargetConstraints;

    private readonly RaycastHit[] lineHits = new RaycastHit[16];
    private MaterialPropertyBlock propertyBlock;
    private Rigidbody targetBody;
    private Transform originalParent;
    private bool originalKinematic;
    private bool originalUseGravity;
    private RigidbodyConstraints originalConstraints;
    private CollisionDetectionMode originalCollisionDetection;
    private RigidbodyInterpolation originalInterpolation;
    private int originalLayer;
    private Quaternion heldRotation;
    private Vector3 previousAnchorPosition;
    private Vector3 craneVelocity;
    private float currentAttractionSpeed;
    private Collider[] craneColliders;
    private Collider[] targetColliders;
    private IMarkState3D targetMarkState;
    private bool targetAttachmentApplied;
    private bool targetPhysicsSnapshotCaptured;
    private bool targetCraneCollisionsIgnored;

    public CraneMagnetState State => state;
    public bool IsMagnetEnabled => state != CraneMagnetState.Disabled && state != CraneMagnetState.Releasing;
    public MagneticCarryable3D CurrentTarget => currentTarget;
    public string DebugStopReason => debugStopReason;
    public float DebugDetectionRadius => debugDetectionRadius;
    public float DebugHoldRadius => debugHoldRadius;
    public bool DebugCraneMoving => debugCraneMoving;
    public Vector3 DebugCraneDestination => debugCraneDestination;
    public float DebugCraneRemainingDistance => debugCraneRemainingDistance;
    public string DebugCraneStopReason => debugCraneStopReason;
    public RigidbodyConstraints DebugOriginalTargetConstraints => debugOriginalTargetConstraints;
    public RigidbodyConstraints DebugAttractionTargetConstraints => debugAttractionTargetConstraints;

    private void OnValidate()
    {
        detectionRadius = Mathf.Max(0.01f, detectionRadius); magnetWidth = Mathf.Max(0.01f, magnetWidth);
        maximumCarryMass = Mathf.Max(0f, maximumCarryMass); maximumCarryTargets = Mathf.Max(1, maximumCarryTargets);
        attractionSpeed = Mathf.Max(0f, attractionSpeed); attractionAcceleration = Mathf.Max(0f, attractionAcceleration);
        maximumAttractionSpeed = Mathf.Max(0f, maximumAttractionSpeed); holdRadius = Mathf.Max(0.001f, holdRadius);
        holdRadius = Mathf.Min(holdRadius, detectionRadius * 0.5f);
        positionFollowSpeed = Mathf.Max(0f, positionFollowSpeed); rotationFollowSpeed = Mathf.Max(0f, rotationFollowSpeed);
        releaseVelocityMultiplier = Mathf.Max(0f, releaseVelocityMultiplier); releaseOffset = Mathf.Max(0f, releaseOffset);
    }

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        ApplyOptionalVisualSetup();
        state = magnetEnabledOnStart ? CraneMagnetState.Searching : CraneMagnetState.Disabled;
        craneColliders = GetComponentsInChildren<Collider>(true);
        previousAnchorPosition = AnchorPosition;
        RefreshDebugState();
        ApplyVisualState();
    }

    private void OnEnable() => SceneManager.activeSceneChanged += HandleSceneChanged;
    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= HandleSceneChanged;
        if (releaseOnDisable || releaseOnCraneDisable) DisableMagnet(true);
    }

    private void Update()
    {
        Vector3 anchor = AnchorPosition;
        float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        craneVelocity = (anchor - previousAnchorPosition) / dt;
        previousAnchorPosition = anchor;
        if (CanReadToggleInput() && WasTogglePressed()) ToggleMagnet();
        if (state == CraneMagnetState.Searching) SelectNearestTarget();
        RefreshDebugState();
    }

    private void FixedUpdate()
    {
        if (state == CraneMagnetState.Attracting) AttractTarget();
        else if (state == CraneMagnetState.Holding) FollowAnchor();
    }

    public void ToggleMagnet() { if (IsMagnetEnabled) DisableMagnet(true); else EnableMagnet(); }
    public void EnableMagnet()
    {
        if (state != CraneMagnetState.Disabled) return;
        state = CraneMagnetState.Searching; magnetActivated.Invoke(); ApplyVisualState();
    }
    public void DisableMagnet(bool releaseTarget)
    {
        if (state == CraneMagnetState.Disabled && currentTarget == null) return;
        if (releaseTarget) ReleaseCurrentTarget();
        state = CraneMagnetState.Disabled; magnetDeactivated.Invoke(); ApplyVisualState();
    }

    private bool CanReadToggleInput()
    {
        if (!isActiveAndEnabled || Time.timeScale <= 0f) return false;
        if (SceneTransitionManager.Instance != null && SceneTransitionManager.Instance.IsLoading) return false;
        if (requirePlayerInControlRange && (magnetControlTrigger == null || !magnetControlTrigger.PlayerInRange)) return false;
        if (!allowMagnetToggleWhileMoving && craneController != null && craneController.IsMoving) return false;
        return true;
    }

    private bool WasTogglePressed()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && System.Enum.TryParse(magnetToggleKey.ToString(), true, out Key key) && keyboard[key].wasPressedThisFrame;
    }

    private void SelectNearestTarget()
    {
        if (magnetDetectionArea == null || magnetAnchor == null || maximumCarryTargets < 1) return;
        MagneticCarryable3D nearest = null; float nearestDistance = float.PositiveInfinity;
        detectedTarget = null;
        detectedTargetStatus = "No trigger candidate";
        foreach (MagneticCarryable3D candidate in magnetDetectionArea.Candidates)
        {
            if (candidate == null) continue;
            if (detectedTarget == null) detectedTarget = candidate;
            if (!IsValidCandidate(candidate, out Rigidbody body, out string rejectionReason))
            {
                if (detectedTarget == candidate) detectedTargetStatus = rejectionReason;
                continue;
            }
            float sqr = (candidate.MagneticAnchor.position - AnchorPosition).sqrMagnitude;
            if (sqr > detectionRadius * detectionRadius)
            {
                if (detectedTarget == candidate) detectedTargetStatus = "Outside magnet range";
                continue;
            }
            if (sqr >= nearestDistance) continue;
            nearest = candidate; nearestDistance = sqr;
        }
        if (nearest == null) return;
        detectedTarget = nearest;
        if (!nearest.TryReserve(this))
        {
            detectedTargetStatus = "Reservation rejected";
            return;
        }
        currentTarget = nearest; targetBody = nearest.ResolveRigidbody(); targetColliders = nearest.GetComponentsInChildren<Collider>(true); targetMarkState = nearest.GetComponentInParent<IMarkState3D>(); currentAttractionSpeed = 0f;
        targetAttachmentApplied = false;
        targetPhysicsSnapshotCaptured = false;
        targetCraneCollisionsIgnored = false;
        CaptureTargetPhysicsState();
        detectedTargetStatus = "Reserved";
        debugStopReason = "Attracting Box toward current Anchor";
        if (craneController != null) craneController.SetMagnetTransportTargetColliders(targetColliders);
        state = CraneMagnetState.Attracting; targetDetected.Invoke(); attractionStarted.Invoke(); ApplyVisualState();
    }

    private bool IsValidCandidate(MagneticCarryable3D candidate, out Rigidbody body, out string rejectionReason)
    {
        body = null; rejectionReason = "Valid";
        if (!candidate.CanBeMovedByMagnet) { rejectionReason = "Magnet movement disabled"; return false; }
        if (candidate.IsReserved) { rejectionReason = "Reserved by another magnet"; return false; }
        if (!candidate.IsAllowedInCurrentWorld()) { rejectionReason = "Current World excluded"; return false; }
        if (IsPlayerTarget(candidate.transform)) { rejectionReason = "Player target excluded"; return false; }
        if ((magnetLayerMask.value & (1 << candidate.gameObject.layer)) == 0) { rejectionReason = "Layer excluded"; return false; }
        body = candidate.ResolveRigidbody();
        if (body == null) { rejectionReason = "Rigidbody missing"; return false; }
        if (maximumCarryMass > 0f && EffectiveMass(candidate, body) > maximumCarryMass) { rejectionReason = "Mass limit exceeded"; return false; }
        if (candidate.GetComponentInParent<IMarkState3D>() is IMarkState3D marked && marked.IsMarked) { rejectionReason = "Camera Mark active"; return false; }
        if (requireLineOfSight && !HasLineOfSight(candidate, body)) { rejectionReason = "Line of sight blocked"; return false; }
        return true;
    }

    private static float EffectiveMass(MagneticCarryable3D candidate, Rigidbody body) => candidate.MassOverride > 0f ? candidate.MassOverride : body.mass;

    private static bool IsPlayerTarget(Transform target)
    {
        int playerLayer = LayerMask.NameToLayer("Player");
        for (Transform current = target; current != null; current = current.parent)
        {
            if (current.CompareTag("Player") || (playerLayer >= 0 && current.gameObject.layer == playerLayer)) return true;
            if (current.GetComponent<PlatformerPlayer3D>() != null) return true;
        }
        return false;
    }

    private bool HasLineOfSight(MagneticCarryable3D candidate, Rigidbody body)
    {
        Vector3 delta = candidate.MagneticAnchor.position - AnchorPosition; float distance = delta.magnitude;
        if (distance <= 0.0001f || magnetObstacleMask.value == 0) return true;
        int count = Physics.RaycastNonAlloc(AnchorPosition, delta / distance, lineHits, distance, magnetObstacleMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider hit = lineHits[i].collider;
            if (hit == null || hit.transform.IsChildOf(transform) || hit.attachedRigidbody == body || hit.GetComponentInParent<MagneticCarryable3D>() == candidate) continue;
            return false;
        }
        return true;
    }

    private void AttractTarget()
    {
        if (currentTarget == null || targetBody == null || !currentTarget.CanBeMovedByMagnet) { FailAttraction(); return; }
        if (targetMarkState != null && targetMarkState.IsMarked) return;
        if (requireLineOfSight && !HasLineOfSight(currentTarget, targetBody)) { FailAttraction(); return; }
        Vector3 delta = HoldPosition - TargetCarryReferencePosition; delta.z = 0f;
        if (delta.magnitude <= holdRadius) { AttachTarget(); return; }
        debugStopReason = "Attracting Box";
        PrepareTargetForAttraction(delta);
        currentAttractionSpeed = Mathf.MoveTowards(currentAttractionSpeed, Mathf.Min(attractionSpeed, maximumAttractionSpeed), attractionAcceleration * Time.fixedDeltaTime);
        Vector3 desired = delta.normalized * currentAttractionSpeed;
        Vector3 velocity = targetBody.linearVelocity; velocity.x = desired.x; velocity.y = desired.y; targetBody.linearVelocity = velocity;
    }

    private void AttachTarget()
    {
        CaptureTargetPhysicsState();
        targetAttachmentApplied = true;
        if (!targetBody.isKinematic) { targetBody.linearVelocity = Vector3.zero; targetBody.angularVelocity = Vector3.zero; }
        targetBody.isKinematic = true; targetBody.useGravity = false;
        if (currentTarget.LockRotationWhileHeld) targetBody.constraints |= RigidbodyConstraints.FreezeRotation;
        currentTarget.transform.SetParent(magnetAnchor, true);
        currentTarget.transform.position += HoldPosition - TargetCarryReferencePosition;
        SetTargetCraneCollisionsIgnored(true);
        debugStopReason = "Holding Box";
        state = CraneMagnetState.Holding; targetAttached.Invoke(); ApplyVisualState();
    }

    private void FollowAnchor()
    {
        if (currentTarget == null || targetBody == null) { FailAttraction(); return; }
        if (targetMarkState != null && targetMarkState.IsMarked) return;
        Vector3 targetPosition = currentTarget.transform.position + (HoldPosition - TargetCarryReferencePosition);
        targetPosition.z = currentTarget.transform.position.z;
        currentTarget.transform.position = Vector3.MoveTowards(currentTarget.transform.position, targetPosition, positionFollowSpeed * Time.fixedDeltaTime);
        if (currentTarget.PreserveRotation) currentTarget.transform.rotation = Quaternion.RotateTowards(currentTarget.transform.rotation, heldRotation, rotationFollowSpeed * Time.fixedDeltaTime);
    }

    private void FailAttraction()
    {
        attractionFailed.Invoke();
        ReleaseCurrentTarget();
        state = CraneMagnetState.Searching;
        ApplyVisualState();
    }

    private void ReleaseCurrentTarget()
    {
        SetTargetCraneCollisionsIgnored(false);
        if (craneController != null) craneController.SetMagnetTransportTargetColliders(null);
        if (currentTarget == null) return;
        state = CraneMagnetState.Releasing;
        MagneticCarryable3D released = currentTarget; Rigidbody body = targetBody;
        if (targetAttachmentApplied)
        {
            if (released.RestoreOriginalParent) released.transform.SetParent(originalParent, true); else released.transform.SetParent(null, true);
            if (releaseOffset > 0f) released.transform.position += Vector3.down * releaseOffset;
        }
        RestoreTargetPhysicsState(body);
        if (body != null && inheritCraneVelocityOnRelease && !body.isKinematic) body.linearVelocity += craneVelocity * releaseVelocityMultiplier;
        released.gameObject.layer = originalLayer;
        released.ReleaseReservation(this);
        currentTarget = null; targetBody = null; targetColliders = null; targetMarkState = null; currentAttractionSpeed = 0f;
        targetAttachmentApplied = false; targetPhysicsSnapshotCaptured = false;
        debugStopReason = "Released"; targetReleased.Invoke();
    }

    private void SetTargetCraneCollisionsIgnored(bool ignore)
    {
        if (targetCraneCollisionsIgnored == ignore || craneColliders == null || targetColliders == null) return;
        for (int i = 0; i < targetColliders.Length; i++)
        {
            Collider targetCollider = targetColliders[i];
            if (targetCollider == null || targetCollider.isTrigger) continue;
            for (int j = 0; j < craneColliders.Length; j++)
            {
                Collider craneCollider = craneColliders[j];
                if (craneCollider == null || craneCollider.isTrigger || craneCollider.transform.IsChildOf(currentTarget.transform)) continue;
                Physics.IgnoreCollision(craneCollider, targetCollider, ignore);
            }
        }
        targetCraneCollisionsIgnored = ignore;
    }

    private void CaptureTargetPhysicsState()
    {
        if (targetPhysicsSnapshotCaptured || currentTarget == null || targetBody == null) return;
        originalParent = currentTarget.transform.parent;
        originalKinematic = targetBody.isKinematic;
        originalUseGravity = targetBody.useGravity;
        originalConstraints = targetBody.constraints;
        originalCollisionDetection = targetBody.collisionDetectionMode;
        originalInterpolation = targetBody.interpolation;
        originalLayer = currentTarget.gameObject.layer;
        heldRotation = currentTarget.transform.rotation;
        debugOriginalTargetConstraints = originalConstraints;
        targetPhysicsSnapshotCaptured = true;
    }

    private void PrepareTargetForAttraction(Vector3 delta)
    {
        CaptureTargetPhysicsState();
        if (targetBody.isKinematic) targetBody.isKinematic = false;
        RigidbodyConstraints constraints = targetBody.constraints & ~RigidbodyConstraints.FreezePositionX;
        if (Mathf.Abs(delta.y) > holdRadius) constraints &= ~RigidbodyConstraints.FreezePositionY;
        targetBody.constraints = constraints;
        debugAttractionTargetConstraints = constraints;
        targetBody.WakeUp();
    }

    private void RestoreTargetPhysicsState(Rigidbody body)
    {
        if (!targetPhysicsSnapshotCaptured || body == null) return;
        body.constraints = originalConstraints;
        body.collisionDetectionMode = originalCollisionDetection;
        body.interpolation = originalInterpolation;
        body.useGravity = originalUseGravity;
        body.isKinematic = originalKinematic;
    }

    private void HandleSceneChanged(Scene oldScene, Scene newScene) { if (releaseOnSceneChange) DisableMagnet(true); }
    private Vector3 AnchorPosition => magnetAnchor != null ? magnetAnchor.position : transform.position;
    private Vector3 HoldPosition => AnchorPosition + holdOffset + (currentTarget != null ? currentTarget.MagnetHoldOffset : Vector3.zero);
    private Vector3 TargetCarryReferencePosition => TryGetTargetBounds(out Bounds bounds)
        ? bounds.center
        : currentTarget != null ? currentTarget.MagneticAnchor.position : Vector3.zero;

    private void RefreshDebugState()
    {
        debugAnchorPosition = AnchorPosition;
        debugHoldPosition = HoldPosition;
        debugAttractionSpeed = currentAttractionSpeed;
        debugCraneState = craneController != null ? craneController.State : CraneXYOperationState.Idle;
        debugCraneMoving = craneController != null && craneController.IsMoving;
        debugCraneDestination = craneController != null ? craneController.CurrentRailDestination : Vector3.zero;
        debugCraneRemainingDistance = craneController != null ? craneController.RemainingRailDistance : 0f;
        debugCraneStopReason = craneController != null ? craneController.LastStopReason : "Crane controller missing";
        debugDetectionRadius = detectionRadius;
        debugHoldRadius = holdRadius;
        MagneticCarryable3D target = currentTarget != null ? currentTarget : detectedTarget;
        debugTargetPosition = target != null ? TargetCarryReferencePosition : Vector3.zero;
        if (TryGetTargetBounds(out Bounds bounds))
        {
            debugTargetBounds = bounds;
        }
        else
        {
            debugTargetBounds = new Bounds();
        }
        debugTargetDistance = target != null ? Vector3.Distance(TargetCarryReferencePosition, debugHoldPosition) : 0f;
        debugPhysicalCraneOverlap = HasPhysicalCraneOverlap();
    }

    private bool TryGetTargetBounds(out Bounds bounds)
    {
        bounds = new Bounds();
        Collider[] colliders = targetColliders;
        if (colliders == null || colliders.Length == 0) return false;
        bool hasBounds = false;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled || collider.isTrigger) continue;
            if (!hasBounds) { bounds = collider.bounds; hasBounds = true; }
            else bounds.Encapsulate(collider.bounds);
        }
        return hasBounds;
    }

    private bool HasPhysicalCraneOverlap()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (targetColliders == null || craneColliders == null) return false;
        for (int i = 0; i < targetColliders.Length; i++)
        {
            Collider targetCollider = targetColliders[i];
            if (targetCollider == null || targetCollider.isTrigger) continue;
            for (int j = 0; j < craneColliders.Length; j++)
            {
                Collider craneCollider = craneColliders[j];
                if (craneCollider == null || craneCollider.isTrigger || craneCollider.transform.IsChildOf(currentTarget.transform)) continue;
                if (Physics.ComputePenetration(targetCollider, targetCollider.transform.position, targetCollider.transform.rotation,
                    craneCollider, craneCollider.transform.position, craneCollider.transform.rotation, out _, out _)) return true;
            }
        }
        return false;
#else
        return false;
#endif
    }

    private void ApplyVisualState()
    {
        if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
        Color color = useMagnetStateTint
            ? state == CraneMagnetState.Disabled ? disabledColor : state == CraneMagnetState.Searching ? searchingColor : state == CraneMagnetState.Attracting ? attractingColor : holdingColor
            : Color.white;
        if (magnetVisualRenderer == null) return;
        magnetVisualRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor("_BaseColor", color); propertyBlock.SetColor("_Color", color);
        magnetVisualRenderer.SetPropertyBlock(propertyBlock);
    }

    private void ApplyOptionalVisualSetup()
    {
        if (magnetVisualRenderer == null) return;
        if (magnetSprite != null) magnetVisualRenderer.sprite = magnetSprite;
        magnetVisualRenderer.transform.localPosition = magnetVisualOffset;
        magnetVisualRenderer.transform.localScale = magnetVisualScale;
        if (!string.IsNullOrWhiteSpace(magnetSortingLayer)) magnetVisualRenderer.sortingLayerName = magnetSortingLayer;
        magnetVisualRenderer.sortingOrder = magnetOrderInLayer;
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos || magnetAnchor == null) return;
        Gizmos.color = Color.yellow; Gizmos.DrawWireCube(magnetAnchor.position + Vector3.down * detectionRadius * 0.5f, new Vector3(magnetWidth, detectionRadius, 0.5f));
        Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(AnchorPosition, 0.12f);
        Gizmos.color = Color.green; Gizmos.DrawWireSphere(HoldPosition, 0.12f);
        if (TryGetTargetBounds(out Bounds targetBounds))
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(targetBounds.center, targetBounds.size);
            Gizmos.color = Color.white;
            Gizmos.DrawLine(HoldPosition, targetBounds.center);
        }
        if (detectedTarget != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(AnchorPosition, detectedTarget.MagneticAnchor.position);
        }
        if (currentTarget != null)
        {
            Gizmos.color = debugPhysicalCraneOverlap ? Color.red : Color.green;
            Gizmos.DrawLine(HoldPosition, TargetCarryReferencePosition);
            Gizmos.DrawWireSphere(TargetCarryReferencePosition, 0.1f);
        }
    }
}
