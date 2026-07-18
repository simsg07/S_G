using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class PausablePhysicsObject : MonoBehaviour
{
    [Header("Rigidbody")]
    [Tooltip("3D Rigidbody controlled by camera or shutter pause requests.")]
    [SerializeField] private Rigidbody rb;

    [Header("Pause State")]
    [Tooltip("Allow camera and shutter systems to pause this object.")]
    [SerializeField] private bool canBePaused = true;
    [Tooltip("Runtime camera pause state. Read-only during Play Mode.")]
    [SerializeField] private bool isPausedByCamera;
    [Tooltip("Runtime shutter pause state. Read-only during Play Mode.")]
    [SerializeField] private bool isPausedByShutter;
    [Tooltip("Restore the original gravity setting when all pause requests are cleared.")]
    [SerializeField] private bool restoreGravityOnResume = true;

    [Header("Debug")]
    [Tooltip("Print pause and resume logs in the Console.")]
    [SerializeField] private bool debugMode = true;

    private bool initialUseGravity;
    private bool initialIsKinematic;
    private bool hasInitialState;

    public bool IsPausedByCamera => isPausedByCamera;
    public bool IsPausedByShutter => isPausedByShutter;
    public bool IsPausedByAnySource => isPausedByCamera || isPausedByShutter;

    private void Awake()
    {
        CacheReferences();
        CaptureInitialState();
    }

    private void OnValidate()
    {
        CacheReferences();
    }

    public void SetCameraPaused(bool paused)
    {
        isPausedByCamera = paused;
        ApplyPauseState();
    }

    public void SetShutterPaused(bool paused)
    {
        isPausedByShutter = paused;
        ApplyPauseState();
    }

    public void ConfigurePause(bool canBePausedValue, bool restoreGravityOnResumeValue, bool debugModeValue)
    {
        canBePaused = canBePausedValue;
        restoreGravityOnResume = restoreGravityOnResumeValue;
        debugMode = debugModeValue;
    }

    public void PausePhysics()
    {
        if (!canBePaused || rb == null)
        {
            return;
        }

        CaptureInitialState();
        ClearVelocity();
        rb.isKinematic = true;
        rb.useGravity = false;
        Log("Paused.");
    }

    public void ResumePhysics()
    {
        if (rb == null)
        {
            return;
        }

        rb.isKinematic = initialIsKinematic;
        if (restoreGravityOnResume)
        {
            rb.useGravity = initialUseGravity;
        }

        Log("Resumed.");
    }

    private void ApplyPauseState()
    {
        if (IsPausedByAnySource)
        {
            PausePhysics();
        }
        else
        {
            ResumePhysics();
        }
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
        if (rb == null || hasInitialState)
        {
            return;
        }

        initialUseGravity = rb.useGravity;
        initialIsKinematic = rb.isKinematic;
        hasInitialState = true;
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
            Debug.Log($"[PausablePhysicsObject] {message}", this);
        }
    }
}
