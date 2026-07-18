public interface IDamageable
{
    bool CanTakeDamage { get; }

    void TakeDamage(int damage);

    void TakeDamage(DamageInfo damageInfo);
}
