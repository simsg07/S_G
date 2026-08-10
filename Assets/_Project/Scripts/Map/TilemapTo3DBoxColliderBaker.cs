using UnityEngine;

// Deprecated compatibility stub.
// Tile Palette is now visual-only. Gameplay collision should be placed with
// Floor_Collision / Wall_Tile / Block_Tile prefabs instead of auto-baked
// Tilemap colliders.
[DisallowMultipleComponent]
public class TilemapTo3DBoxColliderBaker : MonoBehaviour
{
    [ContextMenu("Validate Baker Setup")]
    public void ValidateBakerSetup()
    {
        Debug.LogWarning("[TilemapBaker] Deprecated. Tile Palette is visual-only. Use Floor_Collision / Wall_Tile / Block_Tile prefabs for gameplay collision.", this);
    }

    [ContextMenu("Rebuild Colliders")]
    public void RebuildColliders()
    {
        Debug.LogWarning("[TilemapBaker] Deprecated. Auto collider generation is disabled.", this);
    }

    [ContextMenu("Clear Colliders")]
    public void ClearColliders()
    {
        Debug.LogWarning("[TilemapBaker] Deprecated. Clear generated colliders manually if old generated objects remain in the scene.", this);
    }

    [ContextMenu("Apply Parent Layer To Generated Children")]
    public void ApplyParentLayerToGeneratedChildren()
    {
        Debug.LogWarning("[TilemapBaker] Deprecated. Auto generated collider children are no longer used.", this);
    }
}
