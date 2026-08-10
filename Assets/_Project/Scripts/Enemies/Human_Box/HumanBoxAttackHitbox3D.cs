using UnityEngine;

[DisallowMultipleComponent]
public sealed class HumanBoxAttackHitbox3D : MonoBehaviour
{
    [SerializeField] private HumanBoxAI owner;

    private void Awake()
    {
        if (owner == null) owner = GetComponentInParent<HumanBoxAI>();
    }

    private void OnTriggerEnter(Collider other) => owner?.TryRegisterAttackHit(other);
    private void OnTriggerStay(Collider other) => owner?.TryRegisterAttackHit(other);
}
