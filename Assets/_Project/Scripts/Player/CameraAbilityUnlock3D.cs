using UnityEngine;

[ExecuteAlways]
public class CameraAbilityUnlock3D : MonoBehaviour
{
    [SerializeField] private CameraAbilityId ability = CameraAbilityId.Focus;
    [SerializeField] private string pickupId;
    [SerializeField] private Vector3 pickupSize = new Vector3(0.7f, 0.7f, 1f);
    [SerializeField] private bool destroyOnPickup = true;

    private void Awake()
    {
        ConfigureTrigger();
    }

    private void OnEnable()
    {
        ConfigureTrigger();

        if (Application.isPlaying && !string.IsNullOrWhiteSpace(pickupId) && GameProgressSave3D.IsItemCollected(pickupId))
        {
            gameObject.SetActive(false);
        }
    }

    private void OnValidate()
    {
        ConfigureTrigger();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        PlatformerPlayer3D player = other.GetComponentInParent<PlatformerPlayer3D>();
        if (player == null)
        {
            return;
        }

        CameraAbilitySystem3D abilitySystem = player.GetComponent<CameraAbilitySystem3D>();
        if (abilitySystem == null)
        {
            abilitySystem = player.gameObject.AddComponent<CameraAbilitySystem3D>();
        }

        abilitySystem.UnlockAbility(ability);

        if (!string.IsNullOrWhiteSpace(pickupId))
        {
            GameProgressSave3D.RecordItemCollected(pickupId);
        }

        if (destroyOnPickup)
        {
            Destroy(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void ConfigureTrigger()
    {
        BoxCollider boxCollider = GetComponent<BoxCollider>();
        if (boxCollider == null)
        {
            boxCollider = gameObject.AddComponent<BoxCollider>();
        }

        boxCollider.isTrigger = true;
        boxCollider.size = Vector3.one;
        boxCollider.center = Vector3.zero;
        transform.localScale = new Vector3(
            Mathf.Max(0.1f, pickupSize.x),
            Mathf.Max(0.1f, pickupSize.y),
            Mathf.Max(0.1f, pickupSize.z)
        );
    }
}
