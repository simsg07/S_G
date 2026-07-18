using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class BreakableObject3D : MonoBehaviour, ITriggerableObject
{
    private static readonly int BreakHash = Animator.StringToHash("Break");
    private static readonly int IsBrokenHash = Animator.StringToHash("IsBroken");

    [Header("Hit Source")]
    [Tooltip("Optional HitReceiver that invokes BreakObject when its max hit count is reached.")]
    [SerializeField] private HitReceiver hitReceiver;

    [Header("Break Result")]
    [Tooltip("Colliders disabled when this object breaks.")]
    [SerializeField] private Collider[] collidersToDisable;
    [Tooltip("Renderers disabled when this object breaks.")]
    [SerializeField] private Renderer[] renderersToDisable;
    [Tooltip("Optional animator that receives Break trigger and IsBroken bool when present.")]
    [SerializeField] private Animator animator;
    [Tooltip("Disable colliders after break.")]
    [SerializeField] private bool disableCollidersOnBreak = true;
    [Tooltip("Disable renderers after break.")]
    [SerializeField] private bool disableRenderersOnBreak;
    [Tooltip("Destroy this GameObject after break.")]
    [SerializeField] private bool destroyGameObject;
    [Tooltip("Delay before Destroy when destroyGameObject is enabled.")]
    [SerializeField] private float destroyDelay = 0.5f;

    [Header("Events")]
    [Tooltip("Invoked after the object enters the broken state.")]
    public UnityEvent onBreak;

    [Header("Debug")]
    [Tooltip("Print break logs in the Console.")]
    [SerializeField] private bool debugMode = true;

    private bool isBroken;

    public bool IsBroken => isBroken;
    public bool CanTrigger => !isBroken;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        if (hitReceiver != null)
        {
            hitReceiver.onMaxHit.AddListener(BreakObject);
        }
    }

    private void OnDisable()
    {
        if (hitReceiver != null)
        {
            hitReceiver.onMaxHit.RemoveListener(BreakObject);
        }
    }

    private void OnValidate()
    {
        destroyDelay = Mathf.Max(0f, destroyDelay);
        CacheReferences();
    }

    public void TriggerObject()
    {
        BreakObject();
    }

    public void ResetObject()
    {
        ResetBreakable();
    }

    public void ConfigureBreakable(
        bool disableColliders,
        bool disableRenderers,
        bool destroyObject,
        float delay,
        bool debugModeValue)
    {
        disableCollidersOnBreak = disableColliders;
        disableRenderersOnBreak = disableRenderers;
        destroyGameObject = destroyObject;
        destroyDelay = Mathf.Max(0f, delay);
        debugMode = debugModeValue;
    }

    public void BreakObject()
    {
        if (isBroken)
        {
            return;
        }

        isBroken = true;
        ApplyAnimatorBreak();

        if (disableCollidersOnBreak)
        {
            SetCollidersEnabled(false);
        }

        if (disableRenderersOnBreak)
        {
            SetRenderersEnabled(false);
        }

        onBreak?.Invoke();
        Log("Broken.");

        if (destroyGameObject)
        {
            Destroy(gameObject, destroyDelay);
        }
    }

    public void ResetBreakable()
    {
        isBroken = false;
        SetCollidersEnabled(true);
        SetRenderersEnabled(true);

        if (animator != null && HasParameter(IsBrokenHash, AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(IsBrokenHash, false);
        }

        Log("Reset.");
    }

    private void CacheReferences()
    {
        if (hitReceiver == null)
        {
            hitReceiver = GetComponent<HitReceiver>();
        }

        if ((collidersToDisable == null || collidersToDisable.Length == 0))
        {
            collidersToDisable = GetComponentsInChildren<Collider>(true);
        }

        if ((renderersToDisable == null || renderersToDisable.Length == 0))
        {
            renderersToDisable = GetComponentsInChildren<Renderer>(true);
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }
    }

    private void ApplyAnimatorBreak()
    {
        if (animator == null)
        {
            return;
        }

        if (HasParameter(IsBrokenHash, AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(IsBrokenHash, true);
        }

        if (HasParameter(BreakHash, AnimatorControllerParameterType.Trigger))
        {
            animator.SetTrigger(BreakHash);
        }
    }

    private void SetCollidersEnabled(bool value)
    {
        if (collidersToDisable == null)
        {
            return;
        }

        foreach (Collider targetCollider in collidersToDisable)
        {
            if (targetCollider != null)
            {
                targetCollider.enabled = value;
            }
        }
    }

    private void SetRenderersEnabled(bool value)
    {
        if (renderersToDisable == null)
        {
            return;
        }

        foreach (Renderer targetRenderer in renderersToDisable)
        {
            if (targetRenderer != null)
            {
                targetRenderer.enabled = value;
            }
        }
    }

    private bool HasParameter(int hash, AnimatorControllerParameterType type)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].nameHash == hash && parameters[i].type == type)
            {
                return true;
            }
        }

        return false;
    }

    private void Log(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[BreakableObject3D] {message}", this);
        }
    }
}
