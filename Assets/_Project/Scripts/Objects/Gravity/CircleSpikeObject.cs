using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(GravityObject3D))]
[RequireComponent(typeof(GravityObjectDamageDealer))]
public sealed class CircleSpikeObject : MonoBehaviour, ITriggerableObject
{
    [SerializeField] private GravityObject3D gravityObject;
    [SerializeField] private GravityObjectDamageDealer damageDealer;

    public bool CanTrigger => gravityObject != null && gravityObject.CanTrigger;
    public bool IsFalling => gravityObject != null && gravityObject.IsDropped;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
        SetDamageEnabled(false);
    }

    private void OnValidate()
    {
        CacheReferences();
    }

    public void TriggerObject()
    {
        if (gravityObject == null || !gravityObject.CanTrigger)
        {
            return;
        }

        gravityObject.TriggerDrop();
        SetDamageEnabled(true);
    }

    public void ResetObject()
    {
        if (gravityObject != null)
        {
            gravityObject.ResetGravityObject();
        }

        SetDamageEnabled(false);
    }

    private void CacheReferences()
    {
        if (gravityObject == null)
        {
            gravityObject = GetComponent<GravityObject3D>();
        }

        if (damageDealer == null)
        {
            damageDealer = GetComponent<GravityObjectDamageDealer>();
        }
    }

    private void SetDamageEnabled(bool value)
    {
        if (damageDealer != null)
        {
            damageDealer.enabled = value;
        }
    }
}
