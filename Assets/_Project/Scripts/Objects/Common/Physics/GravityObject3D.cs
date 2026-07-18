using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class GravityObject3D : MonoBehaviour, ITriggerableObject
{
    [Header("Rigidbody")]
    [Tooltip("3D Rigidbody used by this falling object.")]
    [SerializeField] private Rigidbody rb;

    [Header("Start State")]
    [Tooltip("Start as an attached object that waits for TriggerDrop.")]
    [SerializeField] private bool startAttached = true;
    [Tooltip("Disable gravity on start while attached.")]
    [SerializeField] private bool disableGravityOnStart = true;

    [Header("Fall Rules")]
    [Tooltip("Keep the starting X position while falling.")]
    [SerializeField] private bool lockXWhileFalling = true;
    [Tooltip("Keep the starting Z position for 2.5D gameplay.")]
    [SerializeField] private bool lockZPosition = true;
    [Tooltip("Optional manual downward speed. Leave at 0 to use Rigidbody gravity.")]
    [SerializeField] private float manualDropSpeed;
    [Tooltip("Runtime dropped state. Read-only during Play Mode.")]
    [SerializeField] private bool isDropped;

    [Header("Debug")]
    [Tooltip("Print gravity object logs in the Console.")]
    [SerializeField] private bool debugMode = true;

    private Vector3 startPosition;
    private Quaternion startRotation;
    private bool initialUseGravity;
    private bool initialIsKinematic;

    public bool IsDropped => isDropped;
    public bool CanTrigger => !isDropped;

    private void Awake()
    {
        CacheReferences();
        CaptureInitialState();
        ApplyStartState();
    }

    private void OnValidate()
    {
        manualDropSpeed = Mathf.Max(0f, manualDropSpeed);
        CacheReferences();
    }

    private void FixedUpdate()
    {
        if (!isDropped || rb == null)
        {
            return;
        }

        if (manualDropSpeed > 0f)
        {
            rb.MovePosition(rb.position + Vector3.down * (manualDropSpeed * Time.fixedDeltaTime));
        }

        Vector3 position = rb.position;
        if (lockXWhileFalling)
        {
            position.x = startPosition.x;
        }

        if (lockZPosition)
        {
            position.z = startPosition.z;
        }

        rb.position = position;
    }

    public void TriggerObject()
    {
        TriggerDrop();
    }

    public void ResetObject()
    {
        ResetGravityObject();
    }

    public void ConfigureGravity(
        bool startAttachedValue,
        bool disableGravityOnStartValue,
        float manualDropSpeedValue,
        bool lockXWhileFallingValue,
        bool lockZPositionValue,
        bool debugModeValue)
    {
        startAttached = startAttachedValue;
        disableGravityOnStart = disableGravityOnStartValue;
        manualDropSpeed = Mathf.Max(0f, manualDropSpeedValue);
        lockXWhileFalling = lockXWhileFallingValue;
        lockZPosition = lockZPositionValue;
        debugMode = debugModeValue;
    }

    public void TriggerDrop()
    {
        if (isDropped)
        {
            return;
        }

        isDropped = true;
        if (rb != null)
        {
            rb.isKinematic = manualDropSpeed > 0f;
            rb.useGravity = manualDropSpeed <= 0f;
        }

        Log("Dropped.");
    }

    public void ResetGravityObject()
    {
        isDropped = false;
        transform.SetPositionAndRotation(startPosition, startRotation);
        ClearVelocity();

        if (rb != null)
        {
            rb.isKinematic = initialIsKinematic;
            rb.useGravity = initialUseGravity;
        }

        ApplyStartState();
        Log("Reset.");
    }

    private void CacheReferences()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }
    }

    private void CaptureInitialState()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
        if (rb != null)
        {
            initialUseGravity = rb.useGravity;
            initialIsKinematic = rb.isKinematic;
        }
    }

    private void ApplyStartState()
    {
        if (!startAttached || rb == null)
        {
            return;
        }

        isDropped = false;
        rb.isKinematic = true;
        if (disableGravityOnStart)
        {
            rb.useGravity = false;
        }

        ClearVelocity();
    }

    private void ClearVelocity()
    {
        if (rb == null)
        {
            return;
        }

#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = Vector3.zero;
#else
        rb.velocity = Vector3.zero;
#endif
        rb.angularVelocity = Vector3.zero;
    }

    private void Log(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[GravityObject3D] {message}", this);
        }
    }
}
