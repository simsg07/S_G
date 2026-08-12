using System.Collections;
using UnityEngine;

public class Enemy3D : MonoBehaviour, IAttackable3D
{
    [SerializeField] private float respawnDelay = 3f; // 평타에 맞고 사라진 뒤 다시 나타나는 시간입니다.

    private Renderer[] renderers;
    private Collider[] colliders;
    private bool isDefeated;

    public bool IsDefeated => isDefeated;

    private void Awake()
    {
        CacheParts();
    }

    private void OnEnable()
    {
        CacheParts();
    }

    public bool TakeAttack()
    {
        if (!Application.isPlaying || isDefeated)
        {
            return false;
        }

        StartCoroutine(RespawnAfterDelay());
        return true;
    }

    private IEnumerator RespawnAfterDelay()
    {
        isDefeated = true;
        SetVisibleAndSolid(false);

        yield return new WaitForSeconds(respawnDelay);

        SetVisibleAndSolid(true);
        isDefeated = false;
    }

    private void CacheParts()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
    }

    private void SetVisibleAndSolid(bool value)
    {
        if (renderers == null || colliders == null)
        {
            CacheParts();
        }

        foreach (Renderer partRenderer in renderers)
        {
            if (partRenderer != null)
            {
                partRenderer.enabled = value;
            }
        }

        foreach (Collider partCollider in colliders)
        {
            if (partCollider != null)
            {
                partCollider.enabled = value;
            }
        }
    }
}
