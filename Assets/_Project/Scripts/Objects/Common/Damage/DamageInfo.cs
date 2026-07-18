using UnityEngine;

public enum DamageType
{
    Generic,
    FallingObject,
    MonsterAttack,
    Explosion,
    Trap,
    Environmental
}

public enum HitSourceType
{
    None,
    Generic,
    Player,
    EyeballFlyAttack,
    HumanBoxAttack,
    BoomberContact,
    BoomberExplosion,
    MonsterAttack,
    Environment
}

[System.Serializable]
public struct DamageInfo
{
    [Tooltip("Amount of damage applied to the target.")]
    public int damageAmount;

    [Tooltip("Object that caused the damage, such as a monster or trap owner.")]
    public GameObject attacker;

    [Tooltip("Object that physically delivered the hit.")]
    public GameObject sourceObject;

    [Tooltip("World position where the hit happened.")]
    public Vector3 hitPoint;

    [Tooltip("Direction from source to target when the hit happened.")]
    public Vector3 hitDirection;

    [Tooltip("Gameplay category of this damage.")]
    public DamageType damageType;

    [Tooltip("Detailed gameplay source used by HitReceiver filtering rules.")]
    public HitSourceType hitSourceType;

    public DamageInfo(
        int damageAmount,
        GameObject attacker,
        GameObject sourceObject,
        Vector3 hitPoint,
        Vector3 hitDirection,
        DamageType damageType)
        : this(damageAmount, attacker, sourceObject, hitPoint, hitDirection, damageType, ToHitSourceType(damageType))
    {
    }

    public DamageInfo(
        int damageAmount,
        GameObject attacker,
        GameObject sourceObject,
        Vector3 hitPoint,
        Vector3 hitDirection,
        DamageType damageType,
        HitSourceType hitSourceType)
    {
        this.damageAmount = damageAmount;
        this.attacker = attacker;
        this.sourceObject = sourceObject;
        this.hitPoint = hitPoint;
        this.hitDirection = hitDirection;
        this.damageType = damageType;
        this.hitSourceType = hitSourceType == HitSourceType.None ? ToHitSourceType(damageType) : hitSourceType;
    }

    public static HitSourceType ToHitSourceType(DamageType damageType)
    {
        switch (damageType)
        {
            case DamageType.MonsterAttack:
                return HitSourceType.MonsterAttack;
            case DamageType.Explosion:
                return HitSourceType.BoomberExplosion;
            case DamageType.FallingObject:
            case DamageType.Environmental:
            case DamageType.Trap:
                return HitSourceType.Environment;
            default:
                return HitSourceType.Generic;
        }
    }
}
