#if UNITY_EDITOR
using UnityEngine;

public sealed class DionaeaValidationDamageReceiver : MonoBehaviour, IDamageable
{
    public int TotalDamage { get; private set; }
    public bool CanTakeDamage => true;
    public void TakeDamage(int damage) => TotalDamage += damage;
    public void TakeDamage(DamageInfo damageInfo) => TotalDamage += damageInfo.damageAmount;
}
#endif
