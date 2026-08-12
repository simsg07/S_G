using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DionaeaAttack : MonoBehaviour
{
    [Header("Attack Area")]
    [SerializeField] private Transform attackOrigin;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Vector3 attackBoxOffset = new Vector3(0f, 0.8f, 0f);
    [SerializeField] private Vector3 attackBoxSize = new Vector3(1.2f, 1f, 1f);
    [SerializeField] private LayerMask playerLayerMask;

    [Header("Damage")]
    [SerializeField] private int damage = 2;
    [SerializeField] private bool debugMode;
    [SerializeField] private bool showGizmo = true;

    private readonly Collider[] hits = new Collider[16];
    private readonly HashSet<IDamageable> damaged = new HashSet<IDamageable>();

    public int Damage => damage;
    public Transform AttackOrigin { get => attackOrigin; set => attackOrigin = value; }
    public Transform VisualRoot { get => visualRoot; set => visualRoot = value; }

    public bool IsTargetInsideAttackBox(Transform target)
    {
        if (target == null) return false;
        Bounds attackBounds = new Bounds(GetCenter(), attackBoxSize);
        Collider[] targetColliders = target.GetComponentsInChildren<Collider>(true);
        if (targetColliders.Length == 0) return attackBounds.Contains(target.position);
        for (int i = 0; i < targetColliders.Length; i++)
        {
            Collider targetCollider = targetColliders[i];
            if (targetCollider != null && targetCollider.enabled && attackBounds.Intersects(targetCollider.bounds)) return true;
        }
        return false;
    }

    public bool PerformAttack()
    {
        if (!MonsterWorldSimulationGate3D.AllowsPlayerInteraction(this)) return false;
        damaged.Clear();
        Vector3 center = GetCenter();
        int count = Physics.OverlapBoxNonAlloc(center, attackBoxSize * 0.5f, hits,
            Quaternion.identity, playerLayerMask, QueryTriggerInteraction.Collide);
        bool applied = false;
        for (int i = 0; i < count; i++)
        {
            Collider hit = hits[i];
            if (hit == null || hit.transform.IsChildOf(transform)) continue;
            IDamageable receiver = FindDamageable(hit.transform);
            if (receiver == null || !damaged.Add(receiver)) continue;
            receiver.TakeDamage(damage);
            applied = true;
        }
        if (debugMode && !applied) Debug.LogWarning("[DionaeaAttack] No IDamageable Player was inside the attack box.", this);
        return applied;
    }

    [ContextMenu("Test Perform Attack")]
    private void TestPerformAttack() => PerformAttack();

    [ContextMenu("Validate Attack Setup")]
    public void ValidateAttackSetup()
    {
        Debug.Log($"[DionaeaAttack] Origin={(attackOrigin != null ? attackOrigin.name : name)}, Damage={damage}, PlayerMask={playerLayerMask.value}", this);
    }

    public void Configure(int configuredDamage, LayerMask configuredPlayerMask)
    {
        damage = Mathf.Max(0, configuredDamage);
        playerLayerMask = configuredPlayerMask;
    }

    public void ResetRuntimeState()
    {
        damaged.Clear();
        for (int i = 0; i < hits.Length; i++) hits[i] = null;
    }

    private IDamageable FindDamageable(Transform target)
    {
        IDamageable result = target.GetComponent<IDamageable>();
        if (result != null) return result;
        result = target.GetComponentInParent<IDamageable>();
        return result ?? target.GetComponentInChildren<IDamageable>();
    }

    private Vector3 GetCenter()
    {
        Transform origin = attackOrigin != null ? attackOrigin : transform;
        Vector3 offset = attackBoxOffset;
        Transform facingRoot = visualRoot != null ? visualRoot : origin;
        if (facingRoot.lossyScale.x < 0f) offset.x *= -1f;
        return origin.position + offset;
    }

    private void OnValidate()
    {
        damage = Mathf.Max(0, damage);
        attackBoxSize = MonsterRuntime3D.ClampSize(attackBoxSize, 0.01f);
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmo) return;
        Gizmos.color = new Color(1f, 0.25f, 0.1f, 0.8f);
        Gizmos.DrawWireCube(GetCenter(), attackBoxSize);
    }
}
