using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
[RequireComponent(typeof(Tilemap), typeof(TilemapCollider2D), typeof(CompositeCollider2D))]
public sealed class Physics2DPrototypeTilemapCollision : MonoBehaviour
{
    private void Awake()
    {
        Tilemap tilemap = GetComponent<Tilemap>();
        TilemapCollider2D tilemapCollider = GetComponent<TilemapCollider2D>();
        CompositeCollider2D composite = GetComponent<CompositeCollider2D>();

        tilemap.RefreshAllTiles();
        if (tilemapCollider.hasTilemapChanges)
        {
            tilemapCollider.ProcessTilemapChanges();
        }
        if (composite.enabled) composite.GenerateGeometry();
        Physics2D.SyncTransforms();
    }

    private IEnumerator Start()
    {
        // Unity 6 builds can populate TilemapCollider2D paths on the first physics step.
        // Regenerate the composite once after that step so freshly painted tiles are included.
        yield return new WaitForFixedUpdate();
        TilemapCollider2D tilemapCollider = GetComponent<TilemapCollider2D>();
        if (tilemapCollider.hasTilemapChanges)
        {
            tilemapCollider.ProcessTilemapChanges();
        }
        CompositeCollider2D composite = GetComponent<CompositeCollider2D>();
        if (composite.enabled) composite.GenerateGeometry();
        Physics2D.SyncTransforms();
    }
}
