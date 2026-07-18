using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(HitReceiver))]
[RequireComponent(typeof(ConnectedObjectLink))]
[RequireComponent(typeof(BreakableObject3D))]
[RequireComponent(typeof(Collider))]
public class WireObject : MonoBehaviour, IDamageable, ITriggerableObject
{
    private static readonly int HitCountHash = Animator.StringToHash("HitCount");
    private static readonly int IsDamagedHash = Animator.StringToHash("IsDamaged");
    private static readonly int IsCutHash = Animator.StringToHash("IsCut");
    private static readonly int CutHash = Animator.StringToHash("Cut");

    [Header("Wire Settings")]
    [Tooltip("Wire is cut when this many valid hits are registered.")]
    [SerializeField] private int maxHitCount = 2;
    [Tooltip("When false, Wire ignores incoming hits.")]
    [SerializeField] private bool canBeTargeted = true;
    [Tooltip("Activate ConnectedObjectLink when Wire is cut.")]
    [SerializeField] private bool triggerConnectedObjectOnCut = true;
    [Tooltip("Delay before disabling the Wire GameObject after cut. Set 0 for immediate disable.")]
    [SerializeField] private float destroyDelay = 0.3f;

    [Header("References")]
    [SerializeField] private HitReceiver hitReceiver;
    [SerializeField] private ConnectedObjectLink connectedObjectLink;
    [SerializeField] private BreakableObject3D breakableObject;
    [SerializeField] private Animator animator;
    [SerializeField] private Renderer[] renderers;
    [SerializeField] private Collider[] colliders;

    [Header("Debug")]
    [Tooltip("Print Wire logs in the Console.")]
    [SerializeField] private bool debugMode = true;

    private bool isCut;
    private Coroutine disableRoutine;

    public bool IsCut => isCut;
    public bool CanTakeDamage => canBeTargeted && !isCut;
    public bool CanTrigger => !isCut;

    private void Reset()
    {
        CacheReferences(true);
        ApplyHitReceiverSettings();
    }

    private void Awake()
    {
        CacheReferences(true);
        ApplyHitReceiverSettings();
    }

    private void OnEnable()
    {
        CacheReferences(false);
        ApplyHitReceiverSettings();
        ConnectHitReceiverEvents();
    }

    private void OnDisable()
    {
        DisconnectHitReceiverEvents();
    }

    private void OnValidate()
    {
        maxHitCount = Mathf.Max(2, maxHitCount);
        destroyDelay = Mathf.Max(0f, destroyDelay);
        CacheReferences(false);
        ApplyHitReceiverSettings();
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0)
        {
            return;
        }

        RegisterHit(new DamageInfo(damage, null, gameObject, transform.position, Vector3.zero, DamageType.Generic));
    }

    public void TakeDamage(DamageInfo damageInfo)
    {
        RegisterHit(damageInfo);
    }

    public void TriggerObject()
    {
        CutWire();
    }

    public void ResetObject()
    {
        ResetWire();
    }

    public void ConfigureDataDrivenObject(int maxHits, bool targetable, float delay, bool debugEnabled)
    {
        maxHitCount = Mathf.Max(2, maxHits);
        canBeTargeted = targetable;
        destroyDelay = Mathf.Max(0f, delay);
        debugMode = debugEnabled;
        ApplyHitReceiverSettings();
    }

    public void RegisterHit(DamageInfo damageInfo)
    {
        if (!CanTakeDamage || damageInfo.damageAmount <= 0)
        {
            Log($"Hit ignored. CanTakeDamage={CanTakeDamage}, IsCut={isCut}, Damage={damageInfo.damageAmount}");
            return;
        }

        if (hitReceiver == null)
        {
            LogWarning("HitReceiver is missing.");
            return;
        }

        Log($"RegisterHit forwarded. Type={damageInfo.damageType}, Attacker={(damageInfo.attacker != null ? damageInfo.attacker.name : "Unknown")}");
        hitReceiver.RegisterHit(damageInfo);
    }

    public void HandleFirstHit()
    {
        if (isCut)
        {
            return;
        }

        SetAnimatorInt(HitCountHash, hitReceiver != null ? hitReceiver.CurrentHitCount : 1);
        SetAnimatorBool(IsDamagedHash, true);
        Log("First hit received.");
    }

    public void CutWire()
    {
        if (isCut)
        {
            return;
        }

        isCut = true;
        canBeTargeted = false;
        if (hitReceiver != null)
        {
            hitReceiver.SetCanBeTargeted(false);
        }

        SetAnimatorInt(HitCountHash, hitReceiver != null ? hitReceiver.CurrentHitCount : maxHitCount);
        SetAnimatorBool(IsDamagedHash, true);
        SetAnimatorBool(IsCutHash, true);
        TriggerAnimator(CutHash);

        if (triggerConnectedObjectOnCut && connectedObjectLink != null)
        {
            connectedObjectLink.ActivateConnectedObject();
        }

        if (breakableObject != null)
        {
            breakableObject.BreakObject();
        }
        else
        {
            SetCollidersEnabled(false);
        }

        StartDisableRoutine();
        Log("CutWire executed.");
    }

    public void ResetWire()
    {
        if (disableRoutine != null)
        {
            StopCoroutine(disableRoutine);
            disableRoutine = null;
        }

        isCut = false;
        canBeTargeted = true;
        gameObject.SetActive(true);

        CacheReferences(false);
        ApplyHitReceiverSettings();
        if (hitReceiver != null)
        {
            hitReceiver.ResetHitCount();
        }

        if (breakableObject != null)
        {
            breakableObject.ResetBreakable();
        }

        SetCollidersEnabled(true);
        SetRenderersEnabled(true);
        SetAnimatorInt(HitCountHash, 0);
        SetAnimatorBool(IsDamagedHash, false);
        SetAnimatorBool(IsCutHash, false);
        Log("Reset.");
    }

    [ContextMenu("Test Hit")]
    private void TestHit()
    {
        RegisterHit(new DamageInfo(1, gameObject, gameObject, transform.position, Vector3.right, DamageType.Generic));
    }

    [ContextMenu("Test Cut Wire")]
    private void TestCutWire()
    {
        CutWire();
    }

    [ContextMenu("Reset Wire")]
    private void ContextResetWire()
    {
        ResetWire();
    }

    private void CacheReferences(bool addMissingComponents)
    {
        hitReceiver = GetOrAddComponent(hitReceiver, addMissingComponents);
        connectedObjectLink = GetOrAddComponent(connectedObjectLink, addMissingComponents);
        breakableObject = GetOrAddComponent(breakableObject, addMissingComponents);

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        if (renderers == null || renderers.Length == 0)
        {
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        if (colliders == null || colliders.Length == 0)
        {
            colliders = GetComponentsInChildren<Collider>(true);
        }
    }

    private T GetOrAddComponent<T>(T current, bool addMissingComponents) where T : Component
    {
        if (current != null)
        {
            return current;
        }

        T found = GetComponent<T>();
        if (found != null || !addMissingComponents)
        {
            return found;
        }

        return gameObject.AddComponent<T>();
    }

    private void ApplyHitReceiverSettings()
    {
        if (hitReceiver != null)
        {
            hitReceiver.ConfigureHitRules(maxHitCount, canBeTargeted && !isCut);
        }
    }

    private void ConnectHitReceiverEvents()
    {
        if (hitReceiver == null)
        {
            return;
        }

        hitReceiver.onFirstHit.RemoveListener(HandleFirstHit);
        hitReceiver.onFirstHit.AddListener(HandleFirstHit);
        hitReceiver.onMaxHit.RemoveListener(CutWire);
        hitReceiver.onMaxHit.AddListener(CutWire);
    }

    private void DisconnectHitReceiverEvents()
    {
        if (hitReceiver == null)
        {
            return;
        }

        hitReceiver.onFirstHit.RemoveListener(HandleFirstHit);
        hitReceiver.onMaxHit.RemoveListener(CutWire);
    }

    private void StartDisableRoutine()
    {
        if (disableRoutine != null)
        {
            StopCoroutine(disableRoutine);
        }

        disableRoutine = StartCoroutine(DisableAfterDelay());
    }

    private IEnumerator DisableAfterDelay()
    {
        if (destroyDelay > 0f)
        {
            yield return new WaitForSeconds(destroyDelay);
        }

        SetCollidersEnabled(false);
        gameObject.SetActive(false);
        disableRoutine = null;
    }

    private void SetCollidersEnabled(bool value)
    {
        if (colliders == null)
        {
            return;
        }

        foreach (Collider targetCollider in colliders)
        {
            if (targetCollider != null)
            {
                targetCollider.enabled = value;
            }
        }
    }

    private void SetRenderersEnabled(bool value)
    {
        if (renderers == null)
        {
            return;
        }

        foreach (Renderer targetRenderer in renderers)
        {
            if (targetRenderer != null)
            {
                targetRenderer.enabled = value;
            }
        }
    }

    private void SetAnimatorBool(int hash, bool value)
    {
        if (HasAnimatorParameter(hash, AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(hash, value);
        }
    }

    private void SetAnimatorInt(int hash, int value)
    {
        if (HasAnimatorParameter(hash, AnimatorControllerParameterType.Int))
        {
            animator.SetInteger(hash, value);
        }
    }

    private void TriggerAnimator(int hash)
    {
        if (HasAnimatorParameter(hash, AnimatorControllerParameterType.Trigger))
        {
            animator.SetTrigger(hash);
        }
    }

    private bool HasAnimatorParameter(int hash, AnimatorControllerParameterType type)
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
            Debug.Log($"[WireObject] {message}", this);
        }
    }

    private void LogWarning(string message)
    {
        if (debugMode)
        {
            Debug.LogWarning($"[WireObject] {message}", this);
        }
    }
}
