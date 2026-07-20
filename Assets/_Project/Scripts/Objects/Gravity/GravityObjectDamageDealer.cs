using UnityEngine;

[DisallowMultipleComponent]
public class GravityObjectDamageDealer : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private bool instantKillPlayer = true;
    [SerializeField] private bool damageOnlyWhileFalling = true;

    [Header("Target")]
    [SerializeField] private LayerMask playerLayerMask;
    [SerializeField] private string playerTag = "Player";

    [Header("References")]
    [SerializeField] private StoneObject stoneObject;
    [SerializeField] private FallingBoxObject fallingBoxObject;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnValidate()
    {
        CacheReferences();
    }

    public void ApplyGravityObjectData(ObjectData data)
    {
        if (data == null)
        {
            return;
        }

        instantKillPlayer = data.instantKillPlayerOnFallingHit;
        playerTag = string.IsNullOrWhiteSpace(data.playerTag) ? "Player" : data.playerTag;
        debugMode = data.debugMode;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null || collision.collider == null)
        {
            return;
        }

        TryHitPlayer(collision.collider);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryHitPlayer(other);
    }

    public bool IsPlayerCollider(Collider target)
    {
        if (target == null)
        {
            return false;
        }

        if (playerLayerMask.value != 0 && (playerLayerMask.value & (1 << target.gameObject.layer)) != 0)
        {
            return true;
        }

        return IsPlayerTagged(target.transform);
    }

    private void TryHitPlayer(Collider target)
    {
        if (target == null || !IsPlayerCollider(target))
        {
            return;
        }

        Log("[GravityObjectDamageDealer] Player collision detected.");

        if (damageOnlyWhileFalling && !IsFalling())
        {
            return;
        }

        Log("[GravityObjectDamageDealer] Falling state valid.");

        PlayerDamageReceiver damageReceiver = FindPlayerDamageReceiver(target.transform);
        if (damageReceiver == null)
        {
            Debug.LogWarning("[GravityObjectDamageDealer] PlayerDamageReceiver not found on Player target.", this);
            return;
        }

        if (instantKillPlayer)
        {
            Log("[GravityObjectDamageDealer] Player hit by falling object.");
            damageReceiver.KillAndRespawn();
            Log("[GravityObjectDamageDealer] KillAndRespawn called.");
        }
        else
        {
            damageReceiver.TakeDamage(1);
        }
    }

    private bool IsFalling()
    {
        if (stoneObject != null)
        {
            return stoneObject.IsFalling;
        }

        if (fallingBoxObject != null)
        {
            return fallingBoxObject.IsFalling;
        }

        return !damageOnlyWhileFalling;
    }

    private void CacheReferences()
    {
        if (stoneObject == null)
        {
            stoneObject = GetComponent<StoneObject>();
        }

        if (fallingBoxObject == null)
        {
            fallingBoxObject = GetComponent<FallingBoxObject>();
        }
    }

    private PlayerDamageReceiver FindPlayerDamageReceiver(Transform target)
    {
        if (target == null)
        {
            return null;
        }

        PlayerDamageReceiver damageReceiver = target.GetComponent<PlayerDamageReceiver>();
        if (damageReceiver != null)
        {
            return damageReceiver;
        }

        damageReceiver = target.GetComponentInParent<PlayerDamageReceiver>();
        if (damageReceiver != null)
        {
            return damageReceiver;
        }

        return target.GetComponentInChildren<PlayerDamageReceiver>(true);
    }

    private bool IsPlayerTagged(Transform target)
    {
        Transform current = target;
        while (current != null)
        {
            if (current.tag == playerTag)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private void Log(string message)
    {
        if (debugMode)
        {
            Debug.Log(message, this);
        }
    }
}
