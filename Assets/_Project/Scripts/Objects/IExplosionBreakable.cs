using UnityEngine;

public interface IExplosionBreakable
{
    void ReceiveExplosion(int damage, Vector3 explosionOrigin);
}
