using UnityEngine;

public class PlayerAttack3D : MonoBehaviour
{
    [Header("Legacy Visual")]
    [Tooltip("Keeps the temporary player idle sprite while combat attacks stay removed. Turn off only when the final Player visual replaces it.")]
    [SerializeField] private bool keepLegacyIdleSprite = true; // Keeps the temporary player idle sprite while combat attacks stay removed.

    private void Awake()
    {
        DisableLegacyHitbox();
        EnsureLegacyIdleSprite();
    }

    private void OnEnable()
    {
        DisableLegacyHitbox();
        EnsureLegacyIdleSprite();
    }

    private void Update()
    {
        DisableLegacyHitbox();
    }

    private void EnsureLegacyIdleSprite()
    {
        if (!keepLegacyIdleSprite || GetComponent<PlayerAttackAnimation3D>() != null)
        {
            return;
        }

        gameObject.AddComponent<PlayerAttackAnimation3D>();
    }

    private void DisableLegacyHitbox()
    {
        Transform hitboxPreview = transform.Find("Attack Hitbox Preview");
        if (hitboxPreview != null && hitboxPreview.gameObject.activeSelf)
        {
            hitboxPreview.gameObject.SetActive(false);
        }
    }
}
