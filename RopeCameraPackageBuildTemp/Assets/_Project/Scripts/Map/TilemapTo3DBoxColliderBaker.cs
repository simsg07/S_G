using UnityEngine;

// Kept as a compatibility facade for scenes which referenced the former baker.
[DisallowMultipleComponent]
public class TilemapTo3DBoxColliderBaker : MonoBehaviour
{
    [ContextMenu("Validate Baker Setup")]
    public void ValidateBakerSetup()
    {
        Debug.Log("[TilemapBaker] Use _Project > Map > Validate Tilemap Collision.", this);
    }

    [ContextMenu("Rebuild Colliders")]
    public void RebuildColliders()
    {
        Debug.Log("[TilemapBaker] Use _Project > Map > Bake / Update Tilemap 3D Collision.", this);
    }

    [ContextMenu("Clear Colliders")]
    public void ClearColliders()
    {
        Debug.Log("[TilemapBaker] Use _Project > Map > Clear Generated Tilemap Collision.", this);
    }

    [ContextMenu("Apply Parent Layer To Generated Children")]
    public void ApplyParentLayerToGeneratedChildren()
    {
        Debug.Log("[TilemapBaker] Generated child layers are managed by TilemapCollisionAuthoring.", this);
    }
}
