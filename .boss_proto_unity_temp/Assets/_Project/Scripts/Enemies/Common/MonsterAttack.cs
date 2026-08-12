using UnityEngine;

[DisallowMultipleComponent]
public class MonsterAttack : MonoBehaviour
{
    [Header("Attack Settings")]
    [Tooltip("Turn off to prevent attacks and damage.")]
    public bool enableAttack = true;

    public float attackRange = 0.5f;
    public int attackDamage = 1;
    public float attackInterval = 1f;
    public float attackWindup;
    public float attackCooldown = 1f;

    [Tooltip("If true, Light targets can trigger attack animation without damage.")]
    public bool allowLightAttackVisual = true;

    [Header("Debug")]
    public bool debugMode;
    public bool showGizmos = true;

    private void OnValidate()
    {
        attackRange = Mathf.Max(0f, attackRange);
        attackDamage = Mathf.Max(0, attackDamage);
        attackInterval = Mathf.Max(0.05f, attackInterval);
        attackWindup = Mathf.Max(0f, attackWindup);
        attackCooldown = Mathf.Max(0f, attackCooldown);
    }
}
