using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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
    [SerializeField] private LayerMask magnetLayerMask = 1;
    [Min(0.01f)] [SerializeField] private float magnetRange = 4f;
    [Min(0.01f)] [SerializeField] private float magnetWidth = 2f;
    [SerializeField] private bool requireLineOfSight = true;
    [SerializeField] private LayerMask magnetObstacleMask = 7936;
    [Min(0f)] [SerializeField] private float maximumCarryMass = 100f;
    [Min(1)] [SerializeField] private int maximumCarryTargets = 1;

    [Header("Magnet Movement")]
    [Min(0f)] [SerializeField] private float attractionSpeed = 3f;
    [Min(0f)] [SerializeField] private float attractionAcceleration = 8f;
    [Min(0f)] [SerializeField] private float maximumAttractionSpeed = 5f;
    [Min(0.001f)] [SerializeField] private float attachDistance = 0.15f;
    [SerializeField] private Vector3 holdOffset;
    [Min(0f)] [SerializeField] private float positionFollowSpeed = 20f;
    [Min(0f)] [SerializeField] private float rotationFollowSpeed = 360f;
    [SerializeField] private bool inheritCraneVelocityOnRelease;
    [Min(0f)] [SerializeField] private float releaseVelocityMultiplier = 1f;
    [Min(0f)] [SerializeField] private float releaseOffset = 0.05f;
    [SerializeField] private Transform carryAnchor;

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

    public CraneMagnetState State => state;
    public bool IsMagnetEnabled => state != CraneMagnetState.Disabled && state != CraneMagnetState.Releasing;
    public MagneticCarryable3D CurrentTarget => currentTarget;

    private void OnValidate()
    {
        magnetRange = Mathf.Max(0.01f, magnetRange); magnetWidth = Mathf.Max(0.01f, magnetWidth);
        maximumCarryMass = Mathf.Max(0f, maximumCarryMass); maximumCarryTargets = Mathf.Max(1, maximumCarryTargets);
        attractionSpeed = Mathf.Max(0f, attractionSpeed); attractionAcceleration = Mathf.Max(0f, attractionAcceleration);
        maximumAttractionSpeed = Mathf.Max(0f, maximumAttractionSpeed); attachDistance = Mathf.Max(0.001f, attachDistance);
        positionFollowSpeed = Mathf.Max(0f, positionFollowSpeed); rotationFollowSpeed = Mathf.Max(0f, rotationFollowSpeed);
        releaseVelocityMultiplier = Mathf.Max(0f, releaseVelocityMultiplier); releaseOffset = Mathf.Max(0f, releaseOffset);
    }

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        ApplyOptionalVisualSetup();
        state = magnetEnabledOnStart ? CraneMagnetState.Searching : CraneMagnetState.Disabled;
        previousAnchorPosition = AnchorPosition;
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
        foreach (MagneticCarryable3D candidate in magnetDetectionArea.Candidates)
        {
            if (!IsValidCandidate(candidate, out Rigidbody body)) continue;
            float sqr = (candidate.MagneticAnchor.position - AnchorPosition).sqrMagnitude;
            if (sqr > magnetRange * magnetRange || sqr >= nearestDistance) continue;
            nearest = candidate; nearestDistance = sqr;
        }
        if (nearest == null) return;
        if (!nearest.TryReserve(this)) return;
        currentTarget = nearest; targetBody = nearest.ResolveRigidbody(); currentAttractionSpeed = 0f;
        state = CraneMagnetState.Attracting; targetDetected.Invoke(); attractionStarted.Invoke(); ApplyVisualState();
    }

    private bool IsValidCandidate(MagneticCarryable3D candidate, out Rigidbody body)
    {
        body = null;
        if (candidate == null || !candidate.CanBeMovedByMagnet || candidate.IsReserved || !candidate.IsAllowedInCurrentWorld()) return false;
        if (IsPlayerTarget(candidate.transform)) return false;
        if ((magnetLayerMask.value & (1 << candidate.gameObject.layer)) == 0) return false;
        body = candidate.ResolveRigidbody();
        if (body == null || (maximumCarryMass > 0f && EffectiveMass(candidate, body) > maximumCarryMass)) return false;
        if (candidate.GetComponentInParent<IShutterFreezeState3D>() is IShutterFreezeState3D frozen && frozen.IsShutterFrozen) return false;
        return !requireLineOfSight || HasLineOfSight(candidate, body);
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
        if (requireLineOfSight && !HasLineOfSight(currentTarget, targetBody)) { FailAttraction(); return; }
        Vector3 delta = HoldPosition - currentTarget.MagneticAnchor.position; delta.z = 0f;
        if (delta.magnitude <= attachDistance) { AttachTarget(); return; }
        if (targetBody.isKinematic) { FailAttraction(); return; }
        currentAttractionSpeed = Mathf.MoveTowards(currentAttractionSpeed, Mathf.Min(attractionSpeed, maximumAttractionSpeed), attractionAcceleration * Time.fixedDeltaTime);
        Vector3 desired = delta.normalized * currentAttractionSpeed;
        Vector3 velocity = targetBody.linearVelocity; velocity.x = desired.x; velocity.y = desired.y; targetBody.linearVelocity = velocity;
    }

    private void AttachTarget()
    {
        originalParent = currentTarget.transform.parent; originalKinematic = targetBody.isKinematic; originalUseGravity = targetBody.useGravity;
        originalConstraints = targetBody.constraints; originalCollisionDetection = targetBody.collisionDetectionMode;
        originalInterpolation = targetBody.interpolation; originalLayer = currentTarget.gameObject.layer; heldRotation = currentTarget.transform.rotation;
        if (!targetBody.isKinematic) { targetBody.linearVelocity = Vector3.zero; targetBody.angularVelocity = Vector3.zero; }
        targetBody.isKinematic = true; targetBody.useGravity = false;
        if (currentTarget.LockRotationWhileHeld) targetBody.constraints |= RigidbodyConstraints.FreezeRotation;
        currentTarget.transform.SetParent(carryAnchor != null ? carryAnchor : magnetAnchor, true);
        currentTarget.transform.position += HoldPosition - currentTarget.MagneticAnchor.position;
        state = CraneMagnetState.Holding; targetAttached.Invoke(); ApplyVisualState();
    }

    private void FollowAnchor()
    {
        if (currentTarget == null || targetBody == null) { FailAttraction(); return; }
        Vector3 targetPosition = currentTarget.transform.position + (HoldPosition - currentTarget.MagneticAnchor.position);
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
        if (currentTarget == null) return;
        state = CraneMagnetState.Releasing;
        MagneticCarryable3D released = currentTarget; Rigidbody body = targetBody;
        if (released.RestoreOriginalParent) released.transform.SetParent(originalParent, true); else released.transform.SetParent(null, true);
        if (releaseOffset > 0f) released.transform.position += Vector3.down * releaseOffset;
        if (body != null)
        {
            if (released.RestoreConstraints) body.constraints = originalConstraints;
            body.collisionDetectionMode = originalCollisionDetection; body.interpolation = originalInterpolation;
            if (released.RestoreGravityState) body.useGravity = originalUseGravity;
            if (released.RestoreRigidbodyState) body.isKinematic = originalKinematic;
            if (inheritCraneVelocityOnRelease && !body.isKinematic) body.linearVelocity += craneVelocity * releaseVelocityMultiplier;
        }
        released.gameObject.layer = originalLayer; released.ReleaseReservation(this);
        currentTarget = null; targetBody = null; currentAttractionSpeed = 0f; targetReleased.Invoke();
    }

    private void HandleSceneChanged(Scene oldScene, Scene newScene) { if (releaseOnSceneChange) DisableMagnet(true); }
    private Vector3 AnchorPosition => magnetAnchor != null ? magnetAnchor.position : transform.position;
    private Vector3 HoldPosition => (carryAnchor != null ? carryAnchor.position : AnchorPosition) + holdOffset + (currentTarget != null ? currentTarget.MagnetHoldOffset : Vector3.zero);

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
        Gizmos.color = Color.cyan; Gizmos.DrawWireCube(magnetAnchor.position + Vector3.down * magnetRange * 0.5f, new Vector3(magnetWidth, magnetRange, 0.5f));
        Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(AnchorPosition, 0.12f);
        Gizmos.color = Color.green; Gizmos.DrawWireSphere(carryAnchor != null ? carryAnchor.position : HoldPosition, 0.12f);
        if (currentTarget != null) { Gizmos.color = requireLineOfSight && !HasLineOfSight(currentTarget, currentTarget.ResolveRigidbody()) ? Color.red : Color.blue; Gizmos.DrawLine(AnchorPosition, currentTarget.MagneticAnchor.position); }
    }
}
